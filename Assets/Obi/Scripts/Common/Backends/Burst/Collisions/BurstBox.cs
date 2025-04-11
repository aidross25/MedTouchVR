#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    public struct BurstBox : BurstLocalOptimization.IDistanceFunction
    {
        public BurstColliderShape shape;
        public BurstAffineTransform colliderToSolver;

        public void Evaluate(float4 point, float4 radii, quaternion orientation, ref BurstLocalOptimization.SurfacePoint projectedPoint)
        {
            float4 center = shape.center * colliderToSolver.scale;
            float4 size = shape.size * colliderToSolver.scale * 0.5f;

            // clamp the point to the surface of the box:
            point = colliderToSolver.InverseTransformPointUnscaled(point) - center;

            if (shape.is2D)
                point[2] = 0;

            // get minimum distance for each axis:
            float4 distances = size - math.abs(point);

            if (distances.x >= 0 && distances.y >= 0 && distances.z >= 0)
            {
                // find minimum distance in all three axes and the axis index:
                float min = float.MaxValue;
                int axis = 0;

                for (int i = 0; i < 3; ++i)
                {
                    if (distances[i] < min)
                    {
                        min = distances[i];
                        axis = i;
                    }
                }

                projectedPoint.normal = float4.zero;
                projectedPoint.point = point;

                projectedPoint.normal[axis] = point[axis] > 0 ? 1 : -1;
                projectedPoint.point[axis] = size[axis] * projectedPoint.normal[axis];
            }
            else
            {
                projectedPoint.point = math.clamp(point, -size, size);
                projectedPoint.normal = math.normalizesafe(point - projectedPoint.point);
            }

            projectedPoint.point = colliderToSolver.TransformPointUnscaled(projectedPoint.point + center + projectedPoint.normal * shape.contactOffset);
            projectedPoint.normal = colliderToSolver.TransformDirection(projectedPoint.normal);
        }

        public static JobHandle GenerateContacts(ObiColliderWorld world,
                                                BurstSolverImpl solver,
                                                NativeList<Oni.ContactPair> contactPairs,
                                                NativeQueue<BurstContact> contactQueue,
                                                NativeArray<int> contactOffsetsPerType,
                                                float deltaTime,
                                                JobHandle inputDeps)
        {
            int pairCount = contactOffsetsPerType[(int)Oni.ShapeType.Box + 1] - contactOffsetsPerType[(int)Oni.ShapeType.Box];
            if (pairCount == 0) return inputDeps;

            var job = new GenerateBoxContactsJob
            {
                contactPairs = contactPairs,

                positions = solver.positions,
                orientations = solver.orientations,
                velocities = solver.velocities,
                invMasses = solver.invMasses,
                radii = solver.principalRadii,

                simplices = solver.simplices,
                simplexCounts = solver.simplexCounts,

                transforms = world.colliderTransforms.AsNativeArray<BurstAffineTransform>(),
                shapes = world.colliderShapes.AsNativeArray<BurstColliderShape>(),

                contactsQueue = contactQueue.AsParallelWriter(),

                worldToSolver = solver.worldToSolver,
                deltaTime = deltaTime,
                parameters = solver.abstraction.parameters,
                firstPair = contactOffsetsPerType[(int)Oni.ShapeType.Box]
            };

            inputDeps = job.Schedule(pairCount, 8, inputDeps);
            return inputDeps;
        }
    }

    [BurstCompile]
    struct GenerateBoxContactsJob : IJobParallelFor
    {
        [ReadOnly] public NativeList<Oni.ContactPair> contactPairs;

        // particle arrays:
        [ReadOnly] public NativeArray<float4> velocities;
        [ReadOnly] public NativeArray<float4> positions;
        [ReadOnly] public NativeArray<quaternion> orientations;
        [ReadOnly] public NativeArray<float> invMasses;
        [ReadOnly] public NativeArray<float4> radii;

        // simplex arrays:
        [ReadOnly] public NativeArray<int> simplices;
        [ReadOnly] public SimplexCounts simplexCounts;

        // collider arrays:
        [ReadOnly] public NativeArray<BurstAffineTransform> transforms;
        [ReadOnly] public NativeArray<BurstColliderShape> shapes;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeQueue<BurstContact>.ParallelWriter contactsQueue;

        // auxiliar data:
        [ReadOnly] public int firstPair;
        [ReadOnly] public BurstAffineTransform worldToSolver;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public Oni.SolverParameters parameters;

        public void Execute(int i)
        {
            int simplexIndex = contactPairs[firstPair + i].bodyA;
            int colliderIndex = contactPairs[firstPair + i].bodyB;

            int simplexStart = simplexCounts.GetSimplexStartAndSize(simplexIndex, out int simplexSize);

            BurstAffineTransform colliderToSolver = worldToSolver * transforms[colliderIndex];

            BurstBox shape = new BurstBox { colliderToSolver = colliderToSolver, shape = shapes[colliderIndex] };

            float4 simplexBary = BurstMath.BarycenterForSimplexOfSize(simplexSize);
            var colliderPoint = BurstLocalOptimization.Optimize(ref shape, positions, orientations, radii, simplices, simplexStart, simplexSize,
                                                                ref simplexBary, out _, parameters.surfaceCollisionIterations, parameters.surfaceCollisionTolerance);

            contactsQueue.Enqueue(new BurstContact
            {
                bodyA = simplexIndex,
                bodyB = colliderIndex,
                pointA = simplexBary,
                pointB = colliderPoint.point,
                normal = colliderPoint.normal * shape.shape.sign
            });
        }
    }
}
#endif