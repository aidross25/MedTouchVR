#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;

namespace Obi
{


    public class BurstColliderWorld : MonoBehaviour, IColliderWorldImpl
    {
        struct MovingCollider
        {
            public BurstCellSpan oldSpan;
            public BurstCellSpan newSpan;
            public int entity;
        }

        public int referenceCount { get; private set; } = 0;
        public int colliderCount { get; private set; } = 0;

        private NativeMultilevelGrid<int> grid;
        private NativeQueue<MovingCollider> movingColliders;

        private NativeArray<int> colliderTypeCounts;
        private NativeQueue<Oni.ContactPair> contactPairQueue;
        public NativeList<Oni.ContactPair> contactPairs;
        public NativeArray<int> contactOffsetsPerType;

        public NativeQueue<BurstContact> colliderContactQueue;

        public ObiNativeCellSpanList cellSpans;

        public void Awake()
        {
            this.grid = new NativeMultilevelGrid<int>(1000, Allocator.Persistent);
            this.movingColliders = new NativeQueue<MovingCollider>(Allocator.Persistent);
            this.colliderContactQueue = new NativeQueue<BurstContact>(Allocator.Persistent);

            this.contactPairQueue = new NativeQueue<Oni.ContactPair>(Allocator.Persistent);
            this.colliderTypeCounts = new NativeArray<int>(Oni.ColliderShapeTypeCount, Allocator.Persistent);
            this.contactOffsetsPerType = new NativeArray<int>(Oni.ColliderShapeTypeCount + 1, Allocator.Persistent);
            this.contactPairs = new NativeList<Oni.ContactPair>(Allocator.Persistent);

            this.cellSpans = new ObiNativeCellSpanList();

            ObiColliderWorld.GetInstance().RegisterImplementation(this);
        }

        public void OnDestroy()
        {
            ObiColliderWorld.GetInstance().UnregisterImplementation(this);

            grid.Dispose();
            movingColliders.Dispose();

            colliderTypeCounts.Dispose();
            contactPairQueue.Dispose();
            contactPairs.Dispose();
            contactOffsetsPerType.Dispose();

            colliderContactQueue.Dispose();

            cellSpans.Dispose();
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
            colliderCount = shapes.count;

            // insert new empty cellspans at the end if needed:
            while (colliderCount > cellSpans.count)
                cellSpans.Add(new CellSpan(new VInt4(10000), new VInt4(10000)));
        }

        public void SetRigidbodies(ObiNativeRigidbodyList rigidbody)
        {
        }

        public void SetForceZones(ObiNativeForceZoneList rigidbody)
        {
        }

        public void SetCollisionMaterials(ObiNativeCollisionMaterialList materials)
        {

        }

        public void SetTriangleMeshData(ObiNativeTriangleMeshHeaderList headers, ObiNativeBIHNodeList nodes, ObiNativeTriangleList triangles, ObiNativeVector3List vertices)
        {
        }

        public void SetEdgeMeshData(ObiNativeEdgeMeshHeaderList headers, ObiNativeBIHNodeList nodes, ObiNativeEdgeList edges, ObiNativeVector2List vertices)
        {
        }

        public void SetDistanceFieldData(ObiNativeDistanceFieldHeaderList headers, ObiNativeDFNodeList nodes) { }
        public void SetHeightFieldData(ObiNativeHeightFieldHeaderList headers, ObiNativeFloatList samples) { }

        public void UpdateWorld(float deltaTime)
        {
            var world = ObiColliderWorld.GetInstance();

            var identifyMoving = new IdentifyMovingColliders
            {
                movingColliders = movingColliders.AsParallelWriter(),
                shapes = world.colliderShapes.AsNativeArray<BurstColliderShape>(cellSpans.count),
                rigidbodies = world.rigidbodies.AsNativeArray<BurstRigidbody>(),
                collisionMaterials = world.collisionMaterials.AsNativeArray<BurstCollisionMaterial>(),
                bounds = world.colliderAabbs.AsNativeArray<BurstAabb>(cellSpans.count),
                cellIndices = cellSpans.AsNativeArray<BurstCellSpan>(),
                colliderCount = colliderCount,
                dt = deltaTime
            };
            JobHandle movingHandle = identifyMoving.Schedule(cellSpans.count, 128);

            var updateMoving = new UpdateMovingColliders
            {
                movingColliders = movingColliders,
                grid = grid,
                colliderCount = colliderCount
            };

            updateMoving.Schedule(movingHandle).Complete();

            // remove tail from the current spans array:
            if (colliderCount < cellSpans.count)
                cellSpans.count -= cellSpans.count - colliderCount;
        }

        [BurstCompile]
        struct IdentifyMovingColliders : IJobParallelFor
        {
            [WriteOnly]
            [NativeDisableParallelForRestriction]
            public NativeQueue<MovingCollider>.ParallelWriter movingColliders;

            [ReadOnly] public NativeArray<BurstColliderShape> shapes;
            [ReadOnly] public NativeArray<BurstRigidbody> rigidbodies;
            [ReadOnly] public NativeArray<BurstCollisionMaterial> collisionMaterials;
            public NativeArray<BurstAabb> bounds;

            public NativeArray<BurstCellSpan> cellIndices;
            [ReadOnly] public int colliderCount;
            [ReadOnly] public float dt;

            // Iterate over all colliders and store those whose cell span has changed.
            public void Execute(int i)
            {
                BurstAabb velocityBounds = bounds[i];

                int rb = shapes[i].rigidbodyIndex;

                // Expand bounds by rigidbody's linear velocity
                // (check against out of bounds rigidbody access, can happen when a destroyed collider references a rigidbody that has just been destroyed too)
                if (rb >= 0 && rb < rigidbodies.Length)
                    velocityBounds.Sweep(rigidbodies[rb].velocity * dt);

                // Expand bounds by collision material's stick distance:
                if (shapes[i].materialIndex >= 0) 
                    velocityBounds.Expand(collisionMaterials[shapes[i].materialIndex].stickDistance);

                float size = velocityBounds.AverageAxisLength();
                int level = NativeMultilevelGrid<int>.GridLevelForSize(size);
                float cellSize = NativeMultilevelGrid<int>.CellSizeOfLevel(level);

                // get new collider bounds cell coordinates:
                BurstCellSpan newSpan = new BurstCellSpan(new int4(GridHash.Quantize(velocityBounds.min.xyz, cellSize), level),
                                                          new int4(GridHash.Quantize(velocityBounds.max.xyz, cellSize), level));

                // if the collider is 2D, project it to the z = 0 cells.
                if (shapes[i].is2D)
                {
                    newSpan.min[2] = 0;
                    newSpan.max[2] = 0;
                }

                // if the collider is at the tail (removed), we will only remove it from its current cellspan.
                // if the new cellspan and the current one are different, we must remove it from its current cellspan and add it to its new one.
                if (i >= colliderCount || cellIndices[i] != newSpan)
                {
                    // Add the collider to the list of moving colliders:
                    movingColliders.Enqueue(new MovingCollider
                    {
                        oldSpan = cellIndices[i],
                        newSpan = newSpan,
                        entity = i
                    });

                    // Update previous coords:
                    cellIndices[i] = newSpan;
                }

            }
        }

        [BurstCompile]
        struct UpdateMovingColliders : IJob
        {
            public NativeQueue<MovingCollider> movingColliders;
            public NativeMultilevelGrid<int> grid;
            [ReadOnly] public int colliderCount;

            public void Execute()
            {
                while (movingColliders.Count > 0)
                {
                    MovingCollider movingCollider = movingColliders.Dequeue();

                    // remove from old cells:
                    grid.RemoveFromCells(movingCollider.oldSpan, movingCollider.entity);

                    // insert in new cells, as long as the index is below the amount of colliders.
                    // otherwise, the collider is at the "tail" and there's no need to add it back.
                    if (movingCollider.entity < colliderCount)
                        grid.AddToCells(movingCollider.newSpan, movingCollider.entity);
                }

                // remove all empty cells from the grid:
                grid.RemoveEmpty();
            }
        }

        [BurstCompile]
        unsafe struct GenerateContactsJob : IJobParallelFor
        {
            //collider grid:
            [ReadOnly] public NativeMultilevelGrid<int> colliderGrid;

            [DeallocateOnJobCompletion]
            [ReadOnly] public NativeArray<int> gridLevels;

            // particle arrays:
            [ReadOnly] public NativeArray<float4> velocities;
            [ReadOnly] public NativeArray<float4> positions;
            [ReadOnly] public NativeArray<quaternion> orientations;
            [ReadOnly] public NativeArray<float> invMasses;
            [ReadOnly] public NativeArray<float4> radii;
            [ReadOnly] public NativeArray<int> filters;
            [ReadOnly] public NativeArray<int> particleMaterialIndices;

            // simplex arrays:
            [ReadOnly] public NativeArray<int> simplices;
            [ReadOnly] public SimplexCounts simplexCounts;
            [ReadOnly] public NativeArray<BurstAabb> simplexBounds;

            // collider arrays:
            [ReadOnly] public NativeArray<BurstAffineTransform> transforms;
            [ReadOnly] public NativeArray<BurstColliderShape> shapes;
            [ReadOnly] public NativeArray<BurstCollisionMaterial> collisionMaterials;
            [ReadOnly] public NativeArray<BurstRigidbody> rigidbodies;
            [ReadOnly] public NativeArray<BurstAabb> bounds;

            [WriteOnly]
            [NativeDisableParallelForRestriction]
            public NativeQueue<Oni.ContactPair>.ParallelWriter contactPairQueue;

            [NativeDisableParallelForRestriction]
            public NativeArray<int> colliderTypeCounts;

            // auxiliar data:
            [ReadOnly] public BurstAffineTransform solverToWorld;
            [ReadOnly] public float deltaTime;
            [ReadOnly] public Oni.SolverParameters parameters;

            public void Execute(int i)
            {
                int simplexStart = simplexCounts.GetSimplexStartAndSize(i, out int simplexSize);

                // get all colliders overlapped by the cell bounds, in all grid levels:
                BurstAabb simplexBoundsWS = simplexBounds[i].Transformed(solverToWorld);
                NativeList<int> candidates = new NativeList<int>(16,Allocator.Temp);

                // max size of the simplex bounds in cells:
                int3 maxSize = new int3(10);
                bool is2D = parameters.mode == Oni.SolverParameters.Mode.Mode2D;

                for (int l = 0; l < gridLevels.Length; ++l)
                {
                    float cellSize = NativeMultilevelGrid<int>.CellSizeOfLevel(gridLevels[l]);

                    int3 minCell = GridHash.Quantize(simplexBoundsWS.min.xyz, cellSize);
                    int3 maxCell = GridHash.Quantize(simplexBoundsWS.max.xyz, cellSize);
                    maxCell = minCell + math.min(maxCell - minCell, maxSize);

                    for (int x = minCell[0]; x <= maxCell[0]; ++x)
                    {
                        for (int y = minCell[1]; y <= maxCell[1]; ++y)
                        {
                            // for 2D mode, project each cell at z == 0 and check them too. This way we ensure 2D colliders
                            // (which are inserted in cells with z == 0) are accounted for in the broadphase.
                            if (is2D)
                            {
                                if (colliderGrid.TryGetCellIndex(new int4(x, y, 0, gridLevels[l]), out int cellIndex))
                                {
                                    var colliderCell = colliderGrid.usedCells[cellIndex];
                                    candidates.AddRange(colliderCell.ContentsPointer, colliderCell.Length);
                                }
                            }

                            for (int z = minCell[2]; z <= maxCell[2]; ++z)
                            {
                                if (colliderGrid.TryGetCellIndex(new int4(x, y, z, gridLevels[l]), out int cellIndex))
                                {
                                    var colliderCell = colliderGrid.usedCells[cellIndex];
                                    candidates.AddRange(colliderCell.ContentsPointer, colliderCell.Length);
                                }
                            }
                        }
                    }
                }

                if (candidates.Length > 0)
                {
                    // make sure each candidate collider only shows up once in the array:
                    NativeArray<int> uniqueCandidates = candidates.AsArray();
                    uniqueCandidates.Sort();
                    int uniqueCount = uniqueCandidates.Unique();

                    // iterate over candidate colliders, generating contacts for each one
                    for (int k = 0; k < uniqueCount; ++k)
                    {
                        int c = uniqueCandidates[k];
                        if (c < shapes.Length)
                        {
                            BurstColliderShape shape = shapes[c];
                            BurstAabb colliderBoundsWS = bounds[c];
                            int rb = shape.rigidbodyIndex;

                            // Expand bounds by rigidbody's linear velocity:
                            if (rb >= 0)
                                colliderBoundsWS.Sweep(rigidbodies[rb].velocity * deltaTime);

                            // Expand bounds by collision material's stick distance:
                            if (shape.materialIndex >= 0)
                                colliderBoundsWS.Expand(collisionMaterials[shape.materialIndex].stickDistance);

                            // check if any simplex particle and the collider should collide:
                            bool shouldCollide = false;
                            var colliderCategory = shape.filter & ObiUtils.FilterCategoryBitmask;
                            var colliderMask = (shape.filter & ObiUtils.FilterMaskBitmask) >> 16;
                            for (int j = 0; j < simplexSize; ++j)
                            {
                                var simplexCategory = filters[simplices[simplexStart + j]] & ObiUtils.FilterCategoryBitmask;
                                var simplexMask =    (filters[simplices[simplexStart + j]] & ObiUtils.FilterMaskBitmask) >> 16;
                                shouldCollide |= (simplexCategory & colliderMask) != 0 && (simplexMask & colliderCategory) != 0;
                            }

                            if (shouldCollide && simplexBoundsWS.IntersectsAabb(in colliderBoundsWS, is2D))
                            {
                                // increment the amount of contacts for this shape type:
                                Interlocked.Increment(ref ((int*)colliderTypeCounts.GetUnsafePtr())[(int)shape.type]);

                                // enqueue a new contact pair:
                                contactPairQueue.Enqueue(new Oni.ContactPair{
                                    bodyA = i,
                                    bodyB = c
                                });
                            }
                        }
                    }
                }
            }
        }

        [BurstCompile]
        struct PrefixSumJob : IJob
        {
            [ReadOnly] public NativeArray<int> array;
            public NativeArray<int> sum;

            public void Execute()
            {
                sum[0] = 0;
                for (int i = 1; i < sum.Length; ++i)
                    sum[i] = sum[i - 1] + array[i-1];
            }
        }

        [BurstCompile]
        struct SortContactPairsByShape : IJob
        {
            public NativeQueue<Oni.ContactPair> contactPairQueue;
            [ReadOnly] public NativeArray<BurstColliderShape> shapes;
            [ReadOnly] public NativeArray<int> start; // prefix sum
            public NativeArray<int> count;

            public NativeList<Oni.ContactPair> contactPairs;

            public void Execute()
            {
                contactPairs.ResizeUninitialized(contactPairQueue.Count);

                while (!contactPairQueue.IsEmpty())
                {
                    var pair = contactPairQueue.Dequeue();
                    int shapeType = (int)shapes[pair.bodyB].type;

                    // write the pair directly at its position in the sorted array:
                    contactPairs[start[shapeType] + (--count[shapeType])] = pair;
                }
            }
        }

        [BurstCompile]
        unsafe struct ApplyForceZonesJob : IJobParallelFor
        {
            // particle arrays:
            [NativeDisableParallelForRestriction] public NativeArray<float4> externalForces;
            [NativeDisableParallelForRestriction] public NativeArray<float4> wind;
            [NativeDisableParallelForRestriction] public NativeArray<float4> velocities;
            [NativeDisableParallelForRestriction] public NativeArray<float> life;
            [ReadOnly] public NativeArray<float4> positions;
            [ReadOnly] public NativeArray<float> invMasses;

            // simplex arrays:
            [ReadOnly] public NativeArray<int> simplices;
            [ReadOnly] public SimplexCounts simplexCounts;

            // collider arrays:
            [ReadOnly] public NativeArray<BurstAffineTransform> transforms;
            [ReadOnly] public NativeArray<BurstColliderShape> shapes;
            [ReadOnly] public NativeArray<ForceZone> forceZones;

            // contacts
            [ReadOnly] public NativeArray<BurstContact> contacts;

            // auxiliar data:
            [ReadOnly] public BurstAffineTransform worldToSolver;
            [ReadOnly] public float deltaTime;

            public void Execute(int i)
            {
                var contact = contacts[i];
                int forceZoneIndex = shapes[contact.bodyB].forceZoneIndex;

                if (forceZoneIndex >= 0)
                {
                    int simplexStart = simplexCounts.GetSimplexStartAndSize(contact.bodyA, out int simplexSize);
                    for (int j = 0; j < simplexSize; ++j)
                    {
                        int particleIndex = simplices[simplexStart + j];

                        if (invMasses[particleIndex] > 0)
                        {
                            float distance = -math.dot(positions[particleIndex] - contact.pointB, contact.normal);
                            if (distance < 0) continue;

                            float4 axis = (worldToSolver * transforms[contact.bodyB]).TransformDirection(new float4(0, 0, 1, 0));

                            // calculate falloff region based on min/max distances:
                            float falloff = 1;
                            float range = forceZones[forceZoneIndex].maxDistance - forceZones[forceZoneIndex].minDistance;
                            if (math.abs(range) > BurstMath.epsilon)
                                falloff = math.pow(math.saturate((distance - forceZones[forceZoneIndex].minDistance) / range), forceZones[forceZoneIndex].falloffPower);

                            float forceIntensity = forceZones[forceZoneIndex].intensity * falloff;
                            float dampIntensity  = forceZones[forceZoneIndex].damping * falloff;

                            // calculate force direction, depending on the type of the force field:
                            float4 result = float4.zero;
                            switch (forceZones[forceZoneIndex].type)
                            {
                                case ForceZone.ZoneType.Radial:
                                        result = contact.normal * forceIntensity;
                                    break;
                                case ForceZone.ZoneType.Vortex:
                                        result = new float4(math.cross(axis.xyz * forceIntensity, contact.normal.xyz).xyz, 0);
                                    break;
                                case ForceZone.ZoneType.Directional:
                                        result = axis * forceIntensity;
                                    break;
                                default:
                                    BurstMath.AtomicAdd(life, particleIndex, -forceIntensity * deltaTime);
                                    continue;
                            }

                            // apply damping:
                            switch (forceZones[forceZoneIndex].dampingDir)
                            {
                                case ForceZone.DampingDirection.ForceDirection:
                                    {
                                        float4 forceDir = math.normalizesafe(result);
                                        result -= forceDir * math.dot(velocities[particleIndex], forceDir) * dampIntensity;
                                    }
                                    break;
                                case ForceZone.DampingDirection.SurfaceDirection:
                                    result -= contact.normal * math.dot(velocities[particleIndex], contact.normal) * dampIntensity;
                                    break;
                                default:
                                    result -= velocities[particleIndex] * dampIntensity;
                                    break;
                            }

                            switch (forceZones[forceZoneIndex].mode)
                            {
                                case ForceZone.ForceMode.Acceleration:
                                    BurstMath.AtomicAdd(externalForces, particleIndex, result / simplexSize / invMasses[particleIndex]);
                                break;
                                case ForceZone.ForceMode.Force:
                                    BurstMath.AtomicAdd(externalForces, particleIndex, result / simplexSize);
                                break;
                                case ForceZone.ForceMode.Wind:
                                    BurstMath.AtomicAdd(wind, particleIndex, result / simplexSize);
                                break;
                            }
                        }
                    }
                }
            }
        }

        public JobHandle ApplyForceZones(BurstSolverImpl solver, float deltaTime, JobHandle inputDeps)
        {
            var world = ObiColliderWorld.GetInstance();

            var applyForceFieldsJob = new ApplyForceZonesJob
            {
                contacts = solver.abstraction.colliderContacts.AsNativeArray<BurstContact>(),

                positions = solver.positions,
                velocities = solver.velocities,
                externalForces = solver.externalForces,
                wind = solver.wind,
                invMasses = solver.invMasses,
                life = solver.life,

                simplices = solver.simplices,
                simplexCounts = solver.simplexCounts,

                transforms = world.colliderTransforms.AsNativeArray<BurstAffineTransform>(),
                shapes = world.colliderShapes.AsNativeArray<BurstColliderShape>(),
                forceZones = world.forceZones.AsNativeArray<ForceZone>(),

                worldToSolver = solver.worldToSolver,
                deltaTime = deltaTime,
            };

            return applyForceFieldsJob.Schedule(solver.abstraction.colliderContacts.count, 64, inputDeps);
        }

        public JobHandle GenerateContacts(BurstSolverImpl solver, float deltaTime, JobHandle inputDeps)
        {
            var world = ObiColliderWorld.GetInstance();

            var generateColliderContactsJob = new GenerateContactsJob
            {
                colliderGrid = grid,
                gridLevels = grid.populatedLevels.GetKeyArray(Allocator.TempJob),

                positions = solver.positions,
                orientations = solver.orientations,
                velocities = solver.velocities,
                invMasses = solver.invMasses,
                radii = solver.principalRadii,
                filters = solver.filters,
                particleMaterialIndices = solver.collisionMaterials,

                simplices = solver.simplices,
                simplexCounts = solver.simplexCounts,
                simplexBounds = solver.simplexBounds,

                transforms = world.colliderTransforms.AsNativeArray<BurstAffineTransform>(),
                shapes = world.colliderShapes.AsNativeArray<BurstColliderShape>(),
                rigidbodies = world.rigidbodies.AsNativeArray<BurstRigidbody>(),
                collisionMaterials = world.collisionMaterials.AsNativeArray<BurstCollisionMaterial>(),
                bounds = world.colliderAabbs.AsNativeArray<BurstAabb>(),

                contactPairQueue = contactPairQueue.AsParallelWriter(),
                colliderTypeCounts = colliderTypeCounts,

                solverToWorld = solver.solverToWorld,
                deltaTime = deltaTime,
                parameters = solver.abstraction.parameters
            };

            inputDeps = generateColliderContactsJob.Schedule(solver.simplexCounts.simplexCount, 16, inputDeps);

            var prefixSumJob = new PrefixSumJob
            {
                array = colliderTypeCounts,
                sum = contactOffsetsPerType
            };
            inputDeps = prefixSumJob.Schedule(inputDeps);

            var sortPairsJob = new SortContactPairsByShape
            {
                contactPairQueue = contactPairQueue,
                shapes = world.colliderShapes.AsNativeArray<BurstColliderShape>(),
                start = contactOffsetsPerType,
                count = colliderTypeCounts,
                contactPairs = contactPairs
            };
            inputDeps = sortPairsJob.Schedule(inputDeps);

            inputDeps.Complete();

            inputDeps = BurstSphere.GenerateContacts(world,solver,contactPairs,colliderContactQueue,contactOffsetsPerType,deltaTime,inputDeps);
            inputDeps = BurstBox.GenerateContacts(world,solver,contactPairs,colliderContactQueue, contactOffsetsPerType,deltaTime,inputDeps);
            inputDeps = BurstCapsule.GenerateContacts(world, solver, contactPairs, colliderContactQueue, contactOffsetsPerType, deltaTime, inputDeps);
            inputDeps = BurstDistanceField.GenerateContacts(world, solver, contactPairs, colliderContactQueue, contactOffsetsPerType, deltaTime, inputDeps);
            inputDeps = BurstTriangleMesh.GenerateContacts(world, solver, contactPairs, colliderContactQueue, contactOffsetsPerType, deltaTime, inputDeps);
            inputDeps = BurstHeightField.GenerateContacts(world, solver, contactPairs, colliderContactQueue, contactOffsetsPerType, deltaTime, inputDeps);
            inputDeps = BurstEdgeMesh.GenerateContacts(world, solver, contactPairs, colliderContactQueue, contactOffsetsPerType, deltaTime, inputDeps);

            return inputDeps;
        }

    }
}
#endif
