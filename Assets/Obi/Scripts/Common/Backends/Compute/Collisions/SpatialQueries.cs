using System;
using System.Collections.Generic;
using UnityEngine;

namespace Obi
{

    public class SpatialQueries
    {
        private ComputePrefixSum prefixSum;

        private ComputeShader gridShader;
        private int buildKernel;
        private int gridPopulationKernel;
        private int sortKernel;
        private int contactsKernel;
        private int clearKernel;
        private int prefixSumPairsKernel;
        private int sortPairsKernel;

        public GraphicsBuffer sortedShapeIndicesBuffer;

        public GraphicsBuffer cellIndicesBuffer;     //for each collider, the IDs of the 8 cells it covers.
        public GraphicsBuffer cellOffsetsBuffer;     //for each cell, start offset in the sorted span indices buffer.

        public GraphicsBuffer cellCountsBuffer;      // for each cell, how many colliders in it.
        public GraphicsBuffer offsetInCells; // for each collider, its offset in each of the 8 cells.
        public GraphicsBuffer levelPopulation;      // buffer storing the lowest and highest populated level.

        private GraphicsBuffer queryTypeCounts;   // amount of contacts against each collider type.
        public GraphicsBuffer unsortedContactPairs;  // unsorted contact pairs.
        public GraphicsBuffer contactPairs;          // list of contact pairs.
        public GraphicsBuffer contactOffsetsPerType; // offset in the contact pairs array for each collider type.

        public GraphicsBuffer dispatchBuffer;        // dispatch info for iterating trough contacts.

        private const int maxCells = 512 * 512;
        private const int cellsPerShape = 8;
        private const int maxGridLevels = 24;
        private uint[] queryCountClear = new uint[Oni.QueryTypeCount];
        private uint[] dispatchClear = { 0, 1, 1, 0, // contacts
                                         0, 1, 1, 0, // pairs
                                         0, 1, 1, 0, // spheres
                                         0, 1, 1, 0, // boxes
                                         0, 1, 1, 0 // rays
                                        };

        private ComputeSphereQuery spheres;
        private ComputeBoxQuery boxes;
        private ComputeRayQuery rays;

        public SpatialQueries(uint capacity)
        {
            gridShader = Resources.Load<ComputeShader>("Compute/SpatialQueries");
            buildKernel = gridShader.FindKernel("BuildUnsortedList");
            gridPopulationKernel = gridShader.FindKernel("FindPopulatedLevels");
            sortKernel = gridShader.FindKernel("SortList");
            contactsKernel = gridShader.FindKernel("BuildContactList");
            clearKernel = gridShader.FindKernel("Clear");
            prefixSumPairsKernel = gridShader.FindKernel("PrefixSumColliderCounts");
            sortPairsKernel = gridShader.FindKernel("SortContactPairs");

            gridShader.SetInt("shapeTypeCount", Oni.QueryTypeCount);
            gridShader.SetInt("cellsPerShape", cellsPerShape);
            gridShader.SetInt("maxCells", (int)maxCells);

            cellOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxCells, 4);
            cellCountsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxCells, 4);
            levelPopulation = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxGridLevels + 1, 4);
            dispatchBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, dispatchClear.Length, sizeof(uint));

            queryTypeCounts = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Oni.QueryTypeCount, 4);
            contactOffsetsPerType = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Oni.QueryTypeCount + 1, 4);

            prefixSum = new ComputePrefixSum(maxCells);
            spheres = new ComputeSphereQuery();
            boxes = new ComputeBoxQuery();
            rays = new ComputeRayQuery();

            SetCapacity(capacity); 
        }

        public void Dispose()
        {
            prefixSum?.Dispose();

            cellOffsetsBuffer?.Dispose();
            cellCountsBuffer?.Dispose();
            levelPopulation?.Dispose();
            dispatchBuffer?.Dispose();

            queryTypeCounts?.Dispose();
            contactOffsetsPerType?.Dispose();

            DisposeOfResultsData();
            DisposeOfQueryData();
        }

        private void DisposeOfResultsData()
        {
            contactPairs?.Dispose();
            unsortedContactPairs?.Dispose();
        }

        private void DisposeOfQueryData()
        {
            cellIndicesBuffer?.Dispose();
            offsetInCells?.Dispose();
            sortedShapeIndicesBuffer?.Dispose();
        }

        private void SetCapacity(uint capacity)
        {
            DisposeOfResultsData();

            gridShader.SetInt("maxResults", (int)capacity);

            if (capacity > 0)
            {
                unsortedContactPairs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)capacity, 8);
                contactPairs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)capacity, 8);
            }
        }

        public void SpatialQuery(ComputeSolverImpl solver,
                                 GraphicsBuffer shapes,
                                 GraphicsBuffer transforms,
                                 GraphicsBuffer results)
        {
            results.SetCounterValue(0);

            if (solver.activeParticlesBuffer == null || solver.simplices == null)
                return;

            // If the maximum amount of query results has changed, set capacity:
            if (contactPairs == null || !contactPairs.IsValid() || contactPairs.count != solver.abstraction.maxQueryResults)
                SetCapacity(solver.abstraction.maxQueryResults);

            // In case we still have zero capacity, just bail out.
            if (contactPairs == null || !contactPairs.IsValid())
                return;

            // Check whether we need to reallocate space for queries:
            if (cellIndicesBuffer == null || !cellIndicesBuffer.IsValid() || shapes.count * cellsPerShape >= cellIndicesBuffer.count)
            {
                DisposeOfQueryData();

                cellIndicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, shapes.count * cellsPerShape * 2, 4);
                offsetInCells = new GraphicsBuffer(GraphicsBuffer.Target.Structured, shapes.count * cellsPerShape * 2, 4);
                sortedShapeIndicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, shapes.count * cellsPerShape * 2, 4);
            }

            gridShader.SetInt("queryCount", shapes.count);

            int particleThreadGroups = ComputeMath.ThreadGroupCount(solver.simplexCounts.simplexCount, 128);
            int shapeThreadGroups = ComputeMath.ThreadGroupCount(shapes.count, 128);
            int capacityThreadGroups = ComputeMath.ThreadGroupCount(shapes.count * 8, 128);
            int cellThreadGroups = ComputeMath.ThreadGroupCount(maxCells, 128);

            queryTypeCounts.SetData(queryCountClear);
            dispatchBuffer.SetData(dispatchClear);

            // clear grid:
            gridShader.SetBuffer(clearKernel, "cellOffsets", cellOffsetsBuffer);
            gridShader.SetBuffer(clearKernel, "cellIndices", cellIndicesBuffer);
            gridShader.SetBuffer(clearKernel, "cellCounts", cellCountsBuffer);
            gridShader.SetBuffer(clearKernel, "levelPopulation", levelPopulation);
            gridShader.Dispatch(clearKernel, Mathf.Max(cellThreadGroups, capacityThreadGroups), 1, 1);

            // build cell list:
            gridShader.SetBuffer(buildKernel, "shapes", shapes);
            gridShader.SetBuffer(buildKernel, "transforms", transforms);
            gridShader.SetBuffer(buildKernel, "cellIndices", cellIndicesBuffer);
            gridShader.SetBuffer(buildKernel, "cellCounts", cellCountsBuffer);
            gridShader.SetBuffer(buildKernel, "offsetInCells", offsetInCells);
            gridShader.SetBuffer(buildKernel, "levelPopulation", levelPopulation);
            gridShader.SetBuffer(buildKernel, "worldToSolver", solver.worldToSolverBuffer);
            gridShader.Dispatch(buildKernel, shapeThreadGroups, 1, 1);

            // find populated grid levels:
            gridShader.SetBuffer(gridPopulationKernel, "levelPopulation", levelPopulation);
            gridShader.Dispatch(gridPopulationKernel, 1, 1, 1);

            // prefix sum: 
            prefixSum.Sum(cellCountsBuffer, cellOffsetsBuffer);

            // sort query indices:
            gridShader.SetBuffer(sortKernel, "sortedColliderIndices", sortedShapeIndicesBuffer);
            gridShader.SetBuffer(sortKernel, "offsetInCells", offsetInCells);
            gridShader.SetBuffer(sortKernel, "cellIndices", cellIndicesBuffer);
            gridShader.SetBuffer(sortKernel, "cellOffsets", cellOffsetsBuffer);
            gridShader.Dispatch(sortKernel, capacityThreadGroups, 1, 1);


            gridShader.SetInt("pointCount", solver.simplexCounts.pointCount);
            gridShader.SetInt("edgeCount", solver.simplexCounts.edgeCount);
            gridShader.SetInt("triangleCount", solver.simplexCounts.triangleCount);
            gridShader.SetInt("surfaceCollisionIterations", solver.abstraction.parameters.surfaceCollisionIterations);
            gridShader.SetFloat("surfaceCollisionTolerance", solver.abstraction.parameters.surfaceCollisionTolerance);
            gridShader.SetInt("mode", (int)solver.abstraction.parameters.mode);

            gridShader.SetBuffer(contactsKernel, "simplices", solver.simplices);
            gridShader.SetBuffer(contactsKernel, "simplexBounds", solver.simplexBounds);
            gridShader.SetBuffer(contactsKernel, "positions", solver.positionsBuffer);
            gridShader.SetBuffer(contactsKernel, "orientations", solver.orientationsBuffer);
            gridShader.SetBuffer(contactsKernel, "principalRadii", solver.principalRadiiBuffer);
            gridShader.SetBuffer(contactsKernel, "filters", solver.filtersBuffer);
            gridShader.SetBuffer(contactsKernel, "sortedColliderIndices", sortedShapeIndicesBuffer);
            gridShader.SetBuffer(contactsKernel, "transforms", transforms);
            gridShader.SetBuffer(contactsKernel, "shapes", shapes);
            gridShader.SetBuffer(contactsKernel, "collisionMaterialIndices", solver.collisionMaterialIndexBuffer);
            gridShader.SetBuffer(contactsKernel, "cellIndices", cellIndicesBuffer);
            gridShader.SetBuffer(contactsKernel, "cellOffsets", cellOffsetsBuffer);
            gridShader.SetBuffer(contactsKernel, "cellCounts", cellCountsBuffer);
            gridShader.SetBuffer(contactsKernel, "levelPopulation", levelPopulation);

            gridShader.SetBuffer(contactsKernel, "solverToWorld", solver.solverToWorldBuffer);
            gridShader.SetBuffer(contactsKernel, "worldToSolver", solver.worldToSolverBuffer);
            gridShader.SetBuffer(contactsKernel, "colliderTypeCounts", queryTypeCounts);
            gridShader.SetBuffer(contactsKernel, "unsortedContactPairs", unsortedContactPairs);
            gridShader.SetBuffer(contactsKernel, "dispatchBuffer", dispatchBuffer);

            gridShader.Dispatch(contactsKernel, particleThreadGroups, 1, 1);

            gridShader.SetBuffer(prefixSumPairsKernel, "colliderTypeCounts", queryTypeCounts);
            gridShader.SetBuffer(prefixSumPairsKernel, "contactOffsetsPerType", contactOffsetsPerType);
            gridShader.SetBuffer(prefixSumPairsKernel, "dispatchBuffer", dispatchBuffer);

            gridShader.Dispatch(prefixSumPairsKernel, 1, 1, 1);

            gridShader.SetBuffer(sortPairsKernel, "shapes", shapes);
            gridShader.SetBuffer(sortPairsKernel, "unsortedContactPairs", unsortedContactPairs);
            gridShader.SetBuffer(sortPairsKernel, "contactPairs", contactPairs);
            gridShader.SetBuffer(sortPairsKernel, "colliderTypeCounts", queryTypeCounts);
            gridShader.SetBuffer(sortPairsKernel, "contactOffsetsPerType", contactOffsetsPerType);
            gridShader.SetBuffer(sortPairsKernel, "dispatchBuffer", dispatchBuffer);

            gridShader.DispatchIndirect(sortPairsKernel, dispatchBuffer, 16);

            boxes.GetResults(solver, this, transforms, shapes, results);
            spheres.GetResults(solver, this, transforms, shapes, results);
            rays.GetResults(solver, this, transforms, shapes, results);
        }
    }
}
