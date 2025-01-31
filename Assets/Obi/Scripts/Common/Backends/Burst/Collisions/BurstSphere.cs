#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    public struct BurstSphere : BurstLocalOptimization.IDistanceFunction
    {
        public BurstColliderShape shape;
        public BurstAffineTransform colliderToSolver;

        public void Evaluate(float4 point, float4 radii, quaternion orientation, ref BurstLocalOptimization.SurfacePoint projectedPoint)
        {
            float4 center = shape.center * colliderToSolver.scale;
            point = colliderToSolver.InverseTransformPointUnscaled(point) - center;

            if (shape.is2D)
                point[2] = 0;

            float radius = shape.size.x * math.cmax(colliderToSolver.scale.xyz);
            float distanceToCenter = math.length(point);

            float4 normal = point / (distanceToCenter + BurstMath.epsilon);

            projectedPoint.point = colliderToSolver.TransformPointUnscaled(center + normal * (radius + shape.contactOffset));
            projectedPoint.normal = colliderToSolver.TransformDirection(normal);
        }

        public static JobHandle GenerateContacts(ObiColliderWorld world,
                                                BurstSolverImpl solver,
                                                NativeList<Oni.ContactPair> contactPairs,
                                                NativeQueue<BurstContact> contactQueue,
                                                NativeArray<int> contactOffsetsPerType,
                                                float deltaTime,
                                                JobHandle inputDeps)
        {
            int pairCount = contactOffsetsPerType[(int)Oni.ShapeType.Sphere + 1] - contactOffsetsPerType[(int)Oni.ShapeType.Sphere];
            if (pairCount == 0) return inputDeps;

            var job = new GenerateSphereContactsJob
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
                firstPair = contactOffsetsPerType[(int)Oni.ShapeType.Sphere]
            };

            inputDeps = job.Schedule(pairCount, 8, inputDeps);
            return inputDeps;
        }
    }

    [BurstCompile]
    struct GenerateSphereContactsJob : IJobParallelFor
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

            BurstSphere shape = new BurstSphere { colliderToSolver = colliderToSolver, shape = shapes[colliderIndex] };
            
            float4 simplexBary = BurstMath.BarycenterForSimplexOfSize(simplexSize);
            var colliderPoint = BurstLocalOptimization.Optimize(ref shape, positions, orientations, radii, simplices, simplexStart, simplexSize,
                                                                ref simplexBary, out _, parameters.surfaceCollisionIterations, parameters.surfaceCollisionTolerance);

            contactsQueue.Enqueue(new BurstContact {
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