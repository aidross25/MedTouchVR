using System;
using System.Collections.Generic;
using UnityEngine;

namespace Obi
{
    public class ComputeColliderWorld : MonoBehaviour, IColliderWorldImpl
    {
        public int referenceCount { get; private set; } = 0;

        public int colliderCount { get; private set; } = 0;
        public int rigidbodyCount { get; private set; } = -1; // make sure the buffer is created even if there's 0.
        public int forceZoneCount { get; private set; } = -1; // make sure the buffer is created even if there's 0.
        public int materialCount { get; private set; } = -1; // make sure the buffer is created even if there's 0.

        public int triangleMeshCount { get; private set; } = -1;
        public int edgeMeshCount { get; private set; } = -1;
        public int distanceFieldCount { get; private set; } = -1;
        public int heightFieldCount { get; private set; } = -1;

        private ComputePrefixSum prefixSum;

        private ComputeShader gridShader;
        private int buildKernel;
        private int gridPopulationKernel;
        private int sortKernel;
        private int contactsKernel;
        private int clearKernel;
        private int prefixSumPairsKernel;
        private int sortPairsKernel;
        private int applyForceZonesKernel;
        private int writeForceZoneResultsKernel;

        public GraphicsBuffer materialsBuffer;
        public GraphicsBuffer aabbsBuffer;
        public GraphicsBuffer transformsBuffer;
        public GraphicsBuffer shapesBuffer;
        public GraphicsBuffer forceZonesBuffer;
        public GraphicsBuffer rigidbodiesBuffer;
        public GraphicsBuffer sortedColliderIndicesBuffer;

        public GraphicsBuffer cellIndicesBuffer;     //for each collider, the IDs of the 8 cells it covers.
        public GraphicsBuffer cellOffsetsBuffer;     //for each cell, start offset in the sorted span indices buffer.

        public GraphicsBuffer cellCountsBuffer;      // for each cell, how many colliders in it.
        public GraphicsBuffer offsetInCells; // for each collider, its offset in each of the 8 cells.
        public GraphicsBuffer levelPopulation;      // buffer storing amount of entries in each grid level

        private GraphicsBuffer colliderTypeCounts;   // amount of contacts against each collider type.
        public GraphicsBuffer unsortedContactPairs;  // unsorted contact pairs.
        public GraphicsBuffer contactPairs;          // list of contact pairs.
        public GraphicsBuffer contactOffsetsPerType; // offset in the contact pairs array for each collider type.

        public GraphicsBuffer dispatchBuffer;        // dispatch info for iterating trough contacts.

        public GraphicsBuffer heightFieldHeaders;
        public GraphicsBuffer heightFieldSamples;

        public GraphicsBuffer distanceFieldHeaders;
        public GraphicsBuffer dfNodes;

        public GraphicsBuffer edgeMeshHeaders;
        public GraphicsBuffer edgeBihNodes;
        public GraphicsBuffer edges;
        public GraphicsBuffer edgeVertices;

        public GraphicsBuffer triangleMeshHeaders;
        public GraphicsBuffer bihNodes;
        public GraphicsBuffer triangles;
        public GraphicsBuffer vertices;

        public const int maxContacts = 512 * 512;
        private const int maxCells = 512 * 512;
        private const int cellsPerCollider = 8;
        private const int maxGridLevels = 24;
        private uint[] colliderCountClear = new uint[Oni.ColliderShapeTypeCount];
        private uint[] dispatchClear = { 0, 1, 1, 0, // contacts
                                         0, 1, 1, 0, // pairs
                                         0, 1, 1, 0, // spheres
                                         0, 1, 1, 0, // boxes
                                         0, 1, 1, 0, // capsules
                                         0, 1, 1, 0, // heighmaps
                                         0, 1, 1, 0, // tri mesh
                                         0, 1, 1, 0, // edge mesh 
                                         0, 1, 1, 0, // distance field
                                        };

        private ComputeSphere spheres;
        private ComputeBox boxes;
        private ComputeCapsule capsules;
        private ComputeTriangleMesh triangleMeshes;
        private ComputeEdgeMesh edgeMeshes;
        private ComputeDistanceField distanceFields;
        private ComputeHeightField heightFields;

        // for each particle in parallel:
        // determine its cell span in the collider grid.
        // iterate over all of them, generating contacts.

        // we just need to get collider indices from each cell.
        // sort by cell, store offset for each cell.

        // each collider keeps track of 8 uints: IDs of the cells it overlaps. unused are invalid.
        // each collider must know offset within each cell: another 8 units per collider.
        // we can keep using the same system as we did with particles.


        public void Awake()
        {
            ObiColliderWorld.GetInstance().RegisterImplementation(this);

            prefixSum = new ComputePrefixSum(maxCells);

            gridShader = Resources.Load<ComputeShader>("Compute/ColliderGrid");
            buildKernel = gridShader.FindKernel("BuildUnsortedList");
            gridPopulationKernel = gridShader.FindKernel("FindPopulatedLevels");
            sortKernel = gridShader.FindKernel("SortList");
            contactsKernel = gridShader.FindKernel("BuildContactList");
            clearKernel = gridShader.FindKernel("Clear");
            prefixSumPairsKernel = gridShader.FindKernel("PrefixSumColliderCounts");
            sortPairsKernel = gridShader.FindKernel("SortContactPairs");
            applyForceZonesKernel = gridShader.FindKernel("ApplyForceZones");
            writeForceZoneResultsKernel = gridShader.FindKernel("WriteForceZoneResults");

            gridShader.SetInt("shapeTypeCount", Oni.ColliderShapeTypeCount);
            gridShader.SetInt("maxContacts", maxContacts);
            gridShader.SetInt("colliderCount", colliderCount);
            gridShader.SetInt("cellsPerCollider", cellsPerCollider);
            gridShader.SetInt("maxCells", (int)maxCells);

            cellOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxCells, 4);
            cellCountsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxCells, 4);

            // first entry is amount of non-empty levels in the grid.
            // next maxGridLevels entries hold the indices of the non-empty levels. 
            // final maxGridLevels entries hold the population of each level.
            levelPopulation = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxGridLevels + 1, 4);

            colliderTypeCounts = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Oni.ColliderShapeTypeCount, 4);
            contactOffsetsPerType = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Oni.ColliderShapeTypeCount + 1, 4);
            unsortedContactPairs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxContacts, 8);

            contactPairs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxContacts, 8);
            dispatchBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, dispatchClear.Length, sizeof(uint));

            spheres = new ComputeSphere();
            boxes = new ComputeBox();
            capsules = new ComputeCapsule();
            triangleMeshes = new ComputeTriangleMesh();
            edgeMeshes = new ComputeEdgeMesh();
            distanceFields = new ComputeDistanceField();
            heightFields = new ComputeHeightField();
        }

        public void OnDestroy()
        {
            ObiColliderWorld.GetInstance().UnregisterImplementation(this);

            prefixSum.Dispose();

            cellOffsetsBuffer.Dispose();
            cellCountsBuffer.Dispose();
            levelPopulation.Dispose();

            contactPairs.Dispose();
            dispatchBuffer.Dispose();

            colliderTypeCounts.Dispose();
            contactOffsetsPerType.Dispose();
            unsortedContactPairs.Dispose();

            if (cellIndicesBuffer != null)
                cellIndicesBuffer.Dispose();
            if (offsetInCells != null)
                offsetInCells.Dispose();
            if (sortedColliderIndicesBuffer != null)
                sortedColliderIndicesBuffer.Dispose();
        }

        public void IncreaseReferenceCount()
        {
            referenceCount++;
        }
        public void DecreaseReferenceCount()
        {
            if (--referenceCount <= 0 && gameObject != null)
                DestroyImmediate(gameObject);
        }

        public void SetColliders(ObiNativeColliderShapeList shapes, ObiNativeAabbList bounds, ObiNativeAffineTransformList transforms)
        {
            if (colliderCount != shapes.count || aabbsBuffer == null || !aabbsBuffer.IsValid())
                aabbsBuffer = ObiColliderWorld.GetInstance().colliderAabbs.AsComputeBuffer<Aabb>();

            if (colliderCount != shapes.count || shapesBuffer == null || !shapesBuffer.IsValid())
                shapesBuffer = ObiColliderWorld.GetInstance().colliderShapes.AsComputeBuffer<ColliderShape>();

            if (colliderCount != shapes.count || transformsBuffer == null || !transformsBuffer.IsValid())
                transformsBuffer = ObiColliderWorld.GetInstance().colliderTransforms.AsComputeBuffer<AffineTransform>();

            // Only update in case the amount of colliders has changed:
            if (colliderCount != shapes.count)
            {
                colliderCount = shapes.count;
                gridShader.SetInt("colliderCount", colliderCount); 

                if (cellIndicesBuffer != null)
                {
                    cellIndicesBuffer.Release();
                    cellIndicesBuffer = null;
                }
                if (offsetInCells != null)
                {
                    offsetInCells.Release();
                    offsetInCells = null;
                }
                if (sortedColliderIndicesBuffer != null)
                {
                    sortedColliderIndicesBuffer.Release();
                    sortedColliderIndicesBuffer = null;
                }

                if (colliderCount > 0)
                {
                    cellIndicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, colliderCount * cellsPerCollider, 4);
                    offsetInCells = new GraphicsBuffer(GraphicsBuffer.Target.Structured, colliderCount * cellsPerCollider, 4);
                    sortedColliderIndicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, colliderCount * cellsPerCollider, 4);
                }
            }
        }

        public void SetForceZones(ObiNativeForceZoneList forceZones)
        {
            // Changing the count of a NativeList should not invalidate compute buffer. Only need to invalidate if *capacity* changes, it's up to the user to 
            // regenerate the compute buffer in case it is needed, or Uplodad() the new data in case it is not (because our compute buffer maps full capacity, instead of only up to count)
            if (forceZoneCount != forceZones.count || forceZonesBuffer == null || !forceZonesBuffer.IsValid())
            {
                forceZoneCount = forceZones.count;
                forceZonesBuffer = forceZones.SafeAsComputeBuffer<ForceZone>();
            }
        }

        public void SetRigidbodies(ObiNativeRigidbodyList rigidbody)
        {
            if (rigidbodyCount != rigidbody.count || rigidbodiesBuffer == null || !rigidbodiesBuffer.IsValid())
            {
                rigidbodyCount = rigidbody.count;
                rigidbodiesBuffer = rigidbody.SafeAsComputeBuffer<ColliderRigidbody>();
            }
        }

        public void SetCollisionMaterials(ObiNativeCollisionMaterialList materials)
        {
            if (materialCount != materials.count || materialsBuffer == null || !materialsBuffer.IsValid())
            {
                materialCount = materials.count;
                materialsBuffer = materials.SafeAsComputeBuffer<CollisionMaterial>();
            }
        }

        public void SetTriangleMeshData(ObiNativeTriangleMeshHeaderList headers, ObiNativeBIHNodeList nodes, ObiNativeTriangleList triangles, ObiNativeVector3List vertices)
        {
            if (triangleMeshCount != headers.count || triangleMeshHeaders == null || !triangleMeshHeaders.IsValid())
            {
                triangleMeshCount = headers.count;
                triangleMeshHeaders = headers.AsComputeBuffer<TriangleMeshHeader>();
                bihNodes = nodes.AsComputeBuffer<BIHNode>();
                this.triangles = triangles.AsComputeBuffer<Triangle>();
                this.vertices = vertices.AsComputeBuffer<Vector3>();
            }
        }

        public void SetEdgeMeshData(ObiNativeEdgeMeshHeaderList headers, ObiNativeBIHNodeList nodes, ObiNativeEdgeList edges, ObiNativeVector2List vertices)
        {
            if (edgeMeshCount != headers.count || edgeMeshHeaders == null || !edgeMeshHeaders.IsValid())
            {
                edgeMeshCount = headers.count;
                edgeMeshHeaders = headers.AsComputeBuffer<EdgeMeshHeader>();
                edgeBihNodes = nodes.AsComputeBuffer<BIHNode>();
                this.edges = edges.AsComputeBuffer<Edge>();
                edgeVertices = vertices.AsComputeBuffer<Vector2>();
            }
        }

        public void SetDistanceFieldData(ObiNativeDistanceFieldHeaderList headers, ObiNativeDFNodeList nodes)
        {
            if (distanceFieldCount != headers.count || distanceFieldHeaders == null || !distanceFieldHeaders.IsValid())
            {
                distanceFieldCount = headers.count;
                distanceFieldHeaders = headers.AsComputeBuffer<DistanceFieldHeader>();
                dfNodes = nodes.AsComputeBuffer<DFNode>();
            }
        }

        public void SetHeightFieldData(ObiNativeHeightFieldHeaderList headers, ObiNativeFloatList samples)
        {
            if (heightFieldCount != headers.count || heightFieldHeaders == null || !heightFieldHeaders.IsValid())
            {
                heightFieldCount = headers.count;
                heightFieldHeaders = headers.AsComputeBuffer<HeightFieldHeader>();
                heightFieldSamples = samples.AsComputeBuffer<float>();
            }
        }

        public void UpdateWorld(float deltaTime)
        {
            if (colliderCount > 0)
            {
                // Send data from the CPU to the GPU:
                ObiColliderWorld.GetInstance().colliderShapes.Upload();
                ObiColliderWorld.GetInstance().colliderTransforms.Upload();
                ObiColliderWorld.GetInstance().colliderAabbs.Upload();
                ObiColliderWorld.GetInstance().rigidbodies.Upload();
                ObiColliderWorld.GetInstance().forceZones.Upload();

                int colliderThreadGroups = ComputeMath.ThreadGroupCount(colliderCount, 128);
                int capacityThreadGroups = ComputeMath.ThreadGroupCount(colliderCount * 8, 128);
                int cellThreadGroups = ComputeMath.ThreadGroupCount(maxCells, 128);

                // clear grid:
                gridShader.SetBuffer(clearKernel, "cellOffsets", cellOffsetsBuffer);
                gridShader.SetBuffer(clearKernel, "cellIndices", cellIndicesBuffer);
                gridShader.SetBuffer(clearKernel, "cellCounts", cellCountsBuffer);
                gridShader.SetBuffer(clearKernel, "levelPopulation", levelPopulation);
                gridShader.Dispatch(clearKernel, Mathf.Max(cellThreadGroups, capacityThreadGroups), 1, 1);

                // build cell list:
                gridShader.SetBuffer(buildKernel, "aabbs", aabbsBuffer);
                gridShader.SetBuffer(buildKernel, "shapes", shapesBuffer);
                gridShader.SetBuffer(buildKernel, "rigidbodies", rigidbodiesBuffer);
                gridShader.SetBuffer(buildKernel, "collisionMaterials", materialsBuffer);
                gridShader.SetBuffer(buildKernel, "cellIndices", cellIndicesBuffer);
                gridShader.SetBuffer(buildKernel, "cellCounts", cellCountsBuffer);
                gridShader.SetBuffer(buildKernel, "offsetInCells", offsetInCells);
                gridShader.SetBuffer(buildKernel, "levelPopulation", levelPopulation);
                gridShader.Dispatch(buildKernel, colliderThreadGroups, 1, 1);

                // find populated grid levels:
                gridShader.SetBuffer(gridPopulationKernel, "levelPopulation", levelPopulation);
                gridShader.Dispatch(gridPopulationKernel, 1, 1, 1);

                // prefix sum: 
                prefixSum.Sum(cellCountsBuffer, cellOffsetsBuffer);

                // sort particle indices:
                gridShader.SetBuffer(sortKernel, "sortedColliderIndices", sortedColliderIndicesBuffer);
                gridShader.SetBuffer(sortKernel, "offsetInCells", offsetInCells);
                gridShader.SetBuffer(sortKernel, "cellIndices", cellIndicesBuffer);
                gridShader.SetBuffer(sortKernel, "cellOffsets", cellOffsetsBuffer);
                gridShader.Dispatch(sortKernel, capacityThreadGroups, 1, 1);
            }
        }

        public void ApplyForceZones(ComputeSolverImpl solver, float deltaTime)
        {
            if (colliderCount > 0)
            {
                if (solver.activeParticlesBuffer != null && solver.simplices != null && forceZonesBuffer != null)
                {
                    gridShader.SetInt("pointCount", solver.simplexCounts.pointCount);
                    gridShader.SetInt("edgeCount", solver.simplexCounts.edgeCount);
                    gridShader.SetInt("triangleCount", solver.simplexCounts.triangleCount);
                    gridShader.SetFloat("deltaTime", deltaTime);

                    gridShader.SetBuffer(applyForceZonesKernel, "contacts", solver.abstraction.colliderContacts.computeBuffer);
                    gridShader.SetBuffer(applyForceZonesKernel, "dispatchBuffer", dispatchBuffer);

                    gridShader.SetBuffer(applyForceZonesKernel, "simplices", solver.simplices);
                    gridShader.SetBuffer(applyForceZonesKernel, "positions", solver.positionsBuffer);
                    gridShader.SetBuffer(applyForceZonesKernel, "velocities", solver.velocitiesBuffer);
                    gridShader.SetBuffer(applyForceZonesKernel, "invMasses", solver.invMassesBuffer);

                    gridShader.SetBuffer(applyForceZonesKernel, "transforms", transformsBuffer);
                    gridShader.SetBuffer(applyForceZonesKernel, "shapes", shapesBuffer);
                    gridShader.SetBuffer(applyForceZonesKernel, "forceZones", forceZonesBuffer);
                    gridShader.SetBuffer(applyForceZonesKernel, "deltasAsInt", solver.positionDeltasIntBuffer);
                    gridShader.SetBuffer(applyForceZonesKernel, "orientationDeltasAsInt", solver.orientationDeltasIntBuffer);
                    gridShader.SetBuffer(applyForceZonesKernel, "worldToSolver", solver.worldToSolverBuffer);

                    gridShader.DispatchIndirect(applyForceZonesKernel, dispatchBuffer);

                    int threadGroups = ComputeMath.ThreadGroupCount(solver.activeParticleCount, 128);
                    gridShader.SetInt("particleCount", solver.activeParticleCount);

                    gridShader.SetBuffer(writeForceZoneResultsKernel, "activeParticles", solver.activeParticlesBuffer);
                    gridShader.SetBuffer(writeForceZoneResultsKernel, "externalForces", solver.externalForcesBuffer);
                    gridShader.SetBuffer(writeForceZoneResultsKernel, "life", solver.lifeBuffer);
                    gridShader.SetBuffer(writeForceZoneResultsKernel, "wind", solver.windBuffer);
                    gridShader.SetBuffer(writeForceZoneResultsKernel, "deltasAsInt", solver.positionDeltasIntBuffer);
                    gridShader.SetBuffer(writeForceZoneResultsKernel, "orientationDeltasAsInt", solver.orientationDeltasIntBuffer);

                    gridShader.Dispatch(writeForceZoneResultsKernel, threadGroups, 1, 1);
                }
            }
        }

        public void GenerateContacts(ComputeSolverImpl solver, float deltaTime)
        {
            if (colliderCount > 0)
            {
                int particleThreadGroups = ComputeMath.ThreadGroupCount(solver.simplexCounts.simplexCount, 128);

                colliderTypeCounts.SetData(colliderCountClear);
                dispatchBuffer.SetData(dispatchClear);

                if (solver.activeParticlesBuffer != null && solver.simplices != null)
                {
                    solver.abstraction.colliderContacts.computeBuffer.SetCounterValue(0);

                    gridShader.SetInt("pointCount", solver.simplexCounts.pointCount);
                    gridShader.SetInt("edgeCount", solver.simplexCounts.edgeCount);
                    gridShader.SetInt("triangleCount", solver.simplexCounts.triangleCount);

                    gridShader.SetFloat("colliderCCD", solver.abstraction.parameters.colliderCCD);
                    gridShader.SetInt("surfaceCollisionIterations", solver.abstraction.parameters.surfaceCollisionIterations);
                    gridShader.SetFloat("surfaceCollisionTolerance", solver.abstraction.parameters.surfaceCollisionTolerance);
                    gridShader.SetInt("mode", (int)solver.abstraction.parameters.mode);
                    gridShader.SetFloat("deltaTime", deltaTime);

                    gridShader.SetBuffer(contactsKernel, "simplices", solver.simplices);
                    gridShader.SetBuffer(contactsKernel, "simplexBounds", solver.simplexBounds);
                    gridShader.SetBuffer(contactsKernel, "positions", solver.positionsBuffer);
                    gridShader.SetBuffer(contactsKernel, "orientations", solver.orientationsBuffer);
                    gridShader.SetBuffer(contactsKernel, "principalRadii", solver.principalRadiiBuffer);
                    gridShader.SetBuffer(contactsKernel, "filters", solver.filtersBuffer);
                    gridShader.SetBuffer(contactsKernel, "sortedColliderIndices", sortedColliderIndicesBuffer);
                    gridShader.SetBuffer(contactsKernel, "aabbs", aabbsBuffer);
                    gridShader.SetBuffer(contactsKernel, "transforms", transformsBuffer);
                    gridShader.SetBuffer(contactsKernel, "shapes", shapesBuffer);
                    gridShader.SetBuffer(contactsKernel, "rigidbodies", rigidbodiesBuffer);
                    gridShader.SetBuffer(contactsKernel, "collisionMaterials", materialsBuffer);
                    gridShader.SetBuffer(contactsKernel, "collisionMaterialIndices", solver.collisionMaterialIndexBuffer);
                    gridShader.SetBuffer(contactsKernel, "cellIndices", cellIndicesBuffer);
                    gridShader.SetBuffer(contactsKernel, "cellOffsets", cellOffsetsBuffer);
                    gridShader.SetBuffer(contactsKernel, "cellCounts", cellCountsBuffer);
                    gridShader.SetBuffer(contactsKernel, "levelPopulation", levelPopulation);

                    gridShader.SetBuffer(contactsKernel, "solverToWorld", solver.solverToWorldBuffer);
                    gridShader.SetBuffer(contactsKernel, "colliderTypeCounts", colliderTypeCounts);
                    gridShader.SetBuffer(contactsKernel, "unsortedContactPairs", unsortedContactPairs);
                    gridShader.SetBuffer(contactsKernel, "dispatchBuffer", dispatchBuffer);

                    gridShader.Dispatch(contactsKernel, particleThreadGroups, 1, 1);

                    gridShader.SetBuffer(prefixSumPairsKernel, "colliderTypeCounts", colliderTypeCounts);
                    gridShader.SetBuffer(prefixSumPairsKernel, "contactOffsetsPerType", contactOffsetsPerType);
                    gridShader.SetBuffer(prefixSumPairsKernel, "dispatchBuffer", dispatchBuffer);

                    gridShader.Dispatch(prefixSumPairsKernel, 1, 1, 1);

                    gridShader.SetBuffer(sortPairsKernel, "shapes", shapesBuffer);
                    gridShader.SetBuffer(sortPairsKernel, "unsortedContactPairs", unsortedContactPairs);
                    gridShader.SetBuffer(sortPairsKernel, "contactPairs", contactPairs);
                    gridShader.SetBuffer(sortPairsKernel, "colliderTypeCounts", colliderTypeCounts);
                    gridShader.SetBuffer(sortPairsKernel, "contactOffsetsPerType", contactOffsetsPerType);
                    gridShader.SetBuffer(sortPairsKernel, "dispatchBuffer", dispatchBuffer);

                    gridShader.DispatchIndirect(sortPairsKernel, dispatchBuffer, 16);

                    boxes.GenerateContacts(solver, this);
                    spheres.GenerateContacts(solver, this);
                    capsules.GenerateContacts(solver, this);
                    triangleMeshes.GenerateContacts(solver, this, deltaTime);
                    edgeMeshes.GenerateContacts(solver, this, deltaTime);
                    distanceFields.GenerateContacts(solver, this, deltaTime);
                    heightFields.GenerateContacts(solver, this, deltaTime);
                }
            }
        }

    }
}
