#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    public struct BurstDistanceField : BurstLocalOptimization.IDistanceFunction
    {
        public BurstColliderShape shape;
        public BurstAffineTransform colliderToSolver;

        public NativeArray<DistanceFieldHeader> distanceFieldHeaders;
        public NativeArray<BurstDFNode> dfNodes;

        public void Evaluate(float4 point, float4 radii, quaternion orientation, ref BurstLocalOptimization.SurfacePoint projectedPoint)
        {
            point = colliderToSolver.InverseTransformPoint(point);

            if (shape.is2D)
                point[2] = 0;

            var header = distanceFieldHeaders[shape.dataIndex];
            float4 sample = DFTraverse(point, in header);
            float4 normal = new float4(math.normalize(sample.xyz), 0);

            projectedPoint.point = colliderToSolver.TransformPoint(point - normal * (sample[3] - shape.contactOffset));
            projectedPoint.normal = colliderToSolver.TransformDirection(normal);
        }

        private float4 DFTraverse(float4 particlePosition, in DistanceFieldHeader header)
        {
            var stack = new NativeArray<int>(12, Allocator.Temp);
            int stackTop = 0;

            stack[stackTop++] = 0;

            while (stackTop > 0)
            {
                int nodeIndex = stack[--stackTop];
                var node = dfNodes[header.firstNode + nodeIndex];

                // if the child node exists, recurse down the df octree:
                if (node.firstChild >= 0)
                    stack[stackTop++] = node.firstChild + node.GetOctant(particlePosition);
                else
                    return node.SampleWithGradient(particlePosition);
            }
            return float4.zero;
        }

        public static JobHandle GenerateContacts(ObiColliderWorld world,
                                               BurstSolverImpl solver,
                                               NativeList<Oni.ContactPair> contactPairs,
                                               NativeQueue<BurstContact> contactQueue,
                                               NativeArray<int> contactOffsetsPerType,
                                               float deltaTime,
                                               JobHandle inputDeps)
        {
            int pairCount = contactOffsetsPerType[(int)Oni.ShapeType.SignedDistanceField + 1] - contactOffsetsPerType[(int)Oni.ShapeType.SignedDistanceField];
            if (pairCount == 0) return inputDeps;

            var job = new GenerateDistanceFieldContactsJob
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
                rigidbodies = world.rigidbodies.AsNativeArray<BurstRigidbody>(),

                distanceFieldHeaders = world.distanceFieldContainer.headers.AsNativeArray<DistanceFieldHeader>(),
                distanceFieldNodes = world.distanceFieldContainer.dfNodes.AsNativeArray<BurstDFNode>(),

                contactsQueue = contactQueue.AsParallelWriter(),

                solverToWorld = solver.inertialFrame,
                worldToSolver = solver.worldToSolver,
                deltaTime = deltaTime,
                parameters = solver.abstraction.parameters,
                firstPair = contactOffsetsPerType[(int)Oni.ShapeType.SignedDistanceField]
            };

            inputDeps = job.Schedule(pairCount, 1, inputDeps);
            return inputDeps;
        }
    }

    [BurstCompile]
    struct GenerateDistanceFieldContactsJob : IJobParallelFor
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
        [ReadOnly] public NativeArray<BurstRigidbody> rigidbodies;

        // distance field data:
        [ReadOnly] public NativeArray<DistanceFieldHeader> distanceFieldHeaders;
        [ReadOnly] public NativeArray<BurstDFNode> distanceFieldNodes;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeQueue<BurstContact>.ParallelWriter contactsQueue;

        // auxiliar data:
        [ReadOnly] public int firstPair;
        [ReadOnly] public BurstInertialFrame solverToWorld;
        [ReadOnly] public BurstAffineTransform worldToSolver;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public Oni.SolverParameters parameters;

        public void Execute(int i)
        {

            int simplexIndex = contactPairs[firstPair + i].bodyA;
            int colliderIndex = contactPairs[firstPair + i].bodyB;
            int rigidbodyIndex = shapes[colliderIndex].rigidbodyIndex;

            if (shapes[colliderIndex].dataIndex < 0) return;

            int simplexStart = simplexCounts.GetSimplexStartAndSize(simplexIndex, out int simplexSize);
            BurstAffineTransform colliderToSolver = worldToSolver * transforms[colliderIndex];

            BurstDistanceField dfShape = new BurstDistanceField()
            {
                colliderToSolver = colliderToSolver,
                shape = shapes[colliderIndex],
                distanceFieldHeaders = distanceFieldHeaders,
                dfNodes = distanceFieldNodes
            };


            float4 simplexBary = BurstMath.BarycenterForSimplexOfSize(simplexSize);
            var colliderPoint = BurstLocalOptimization.Optimize(ref dfShape, positions, orientations, radii, simplices, simplexStart, simplexSize,
                                                                ref simplexBary, out float4 simplexPoint, parameters.surfaceCollisionIterations, parameters.surfaceCollisionTolerance);

            float4 velocity = float4.zero;
            float simplexRadius = 0;
            for (int j = 0; j < simplexSize; ++j)
            {
                int particleIndex = simplices[simplexStart + j];
                simplexRadius += radii[particleIndex].x * simplexBary[j];
                velocity += velocities[particleIndex] * simplexBary[j];
            }

            float4 rbVelocity = float4.zero;
            if (rigidbodyIndex >= 0)
                rbVelocity = BurstMath.GetRigidbodyVelocityAtPoint(rigidbodyIndex, colliderPoint.point, rigidbodies, solverToWorld);

            float dAB = math.dot(simplexPoint - colliderPoint.point, colliderPoint.normal);
            float vel = math.dot(velocity - rbVelocity, colliderPoint.normal);

            //if (vel * deltaTime + dAB <= simplexRadius + shapes[colliderIndex].contactOffset + parameters.collisionMargin)
                contactsQueue.Enqueue(new BurstContact
                {
                    bodyA = simplexIndex,
                    bodyB = colliderIndex,
                    pointA = simplexBary,
                    pointB = colliderPoint.point,
                    normal = colliderPoint.normal * dfShape.shape.sign
                });
        }
    }

}
#endif