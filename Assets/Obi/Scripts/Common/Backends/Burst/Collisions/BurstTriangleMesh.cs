#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    public struct BurstTriangleMesh : BurstLocalOptimization.IDistanceFunction
    {
        public BurstColliderShape shape;
        public BurstAffineTransform colliderToSolver;

        public BurstMath.CachedTri tri;

        public void Evaluate(float4 point, float4 radii, quaternion orientation, ref BurstLocalOptimization.SurfacePoint projectedPoint)
        {
            point = colliderToSolver.InverseTransformPointUnscaled(point);

            if (shape.is2D)
                point[2] = 0;

            float4 nearestPoint = BurstMath.NearestPointOnTri(tri, point, out float4 bary);
            float4 normal = math.normalizesafe(point - nearestPoint);

            projectedPoint.point = colliderToSolver.TransformPointUnscaled(nearestPoint + normal * shape.contactOffset);
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
            int pairCount = contactOffsetsPerType[(int)Oni.ShapeType.TriangleMesh + 1] - contactOffsetsPerType[(int)Oni.ShapeType.TriangleMesh];
            if (pairCount == 0) return inputDeps;

            var job = new GenerateTriangleMeshContactsJob
            {
                contactPairs = contactPairs,

                positions = solver.positions,
                orientations = solver.orientations,
                velocities = solver.velocities,
                invMasses = solver.invMasses,
                radii = solver.principalRadii,

                simplices = solver.simplices,
                simplexCounts = solver.simplexCounts,
                simplexBounds = solver.simplexBounds,

                transforms = world.colliderTransforms.AsNativeArray<BurstAffineTransform>(),
                shapes = world.colliderShapes.AsNativeArray<BurstColliderShape>(),
                rigidbodies = world.rigidbodies.AsNativeArray<BurstRigidbody>(),

                triangleMeshHeaders = world.triangleMeshContainer.headers.AsNativeArray<TriangleMeshHeader>(),
                bihNodes = world.triangleMeshContainer.bihNodes.AsNativeArray<BIHNode>(),
                triangles = world.triangleMeshContainer.triangles.AsNativeArray<Triangle>(),
                vertices = world.triangleMeshContainer.vertices.AsNativeArray<float3>(),

                contactsQueue = contactQueue.AsParallelWriter(),

                solverToWorld = solver.inertialFrame,
                worldToSolver = solver.worldToSolver,
                deltaTime = deltaTime,
                parameters = solver.abstraction.parameters,
                firstPair = contactOffsetsPerType[(int)Oni.ShapeType.TriangleMesh]
            };

            inputDeps = job.Schedule(pairCount, 1, inputDeps);
            return inputDeps;
        }
    }

    [BurstCompile]
    struct GenerateTriangleMeshContactsJob : IJobParallelFor
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
        [ReadOnly] public NativeArray<BurstAabb> simplexBounds;

        // collider arrays:
        [ReadOnly] public NativeArray<BurstAffineTransform> transforms;
        [ReadOnly] public NativeArray<BurstColliderShape> shapes;
        [ReadOnly] public NativeArray<BurstRigidbody> rigidbodies;

        // triangle mesh data:
        [ReadOnly] public NativeArray<TriangleMeshHeader> triangleMeshHeaders;
        [ReadOnly] public NativeArray<BIHNode> bihNodes;
        [ReadOnly] public NativeArray<Triangle> triangles;
        [ReadOnly] public NativeArray<float3> vertices;

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
            var shape = shapes[colliderIndex];

            if (shape.dataIndex < 0)
                return;

            int rigidbodyIndex = shape.rigidbodyIndex;
            var header = triangleMeshHeaders[shape.dataIndex];

            int simplexStart = simplexCounts.GetSimplexStartAndSize(simplexIndex, out int simplexSize);
            var simplexBound = simplexBounds[simplexIndex];

            BurstAffineTransform colliderToSolver = worldToSolver * transforms[colliderIndex];

            // invert a full matrix here to accurately represent collider bounds scale.
            var solverToCollider = math.inverse(float4x4.TRS(colliderToSolver.translation.xyz, colliderToSolver.rotation, colliderToSolver.scale.xyz));
            var simplexBoundsCS = simplexBound.Transformed(solverToCollider);

            float4 marginCS = new float4((shape.contactOffset + parameters.collisionMargin) / colliderToSolver.scale.xyz, 0);

            BurstTriangleMesh triangleMeshShape = new BurstTriangleMesh()
            {
                colliderToSolver = colliderToSolver,
                shape = shape
            };

            NativeQueue<int> queue = new NativeQueue<int>(Allocator.Temp);

            queue.Enqueue(0);

            while (!queue.IsEmpty())
            {
                int nodeIndex = queue.Dequeue();
                var node = bihNodes[header.firstNode + nodeIndex];

                // leaf node:
                if (node.firstChild < 0)
                {
                    // check for contact against all triangles:
                    for (int dataOffset = node.start; dataOffset < node.start + node.count; ++dataOffset)
                    {
                        Triangle t = triangles[header.firstTriangle + dataOffset];
                        float4 v1 = new float4(vertices[header.firstVertex + t.i1], 0);
                        float4 v2 = new float4(vertices[header.firstVertex + t.i2], 0);
                        float4 v3 = new float4(vertices[header.firstVertex + t.i3], 0);
                        BurstAabb triangleBounds = new BurstAabb(v1, v2, v3, marginCS);

                        if (triangleBounds.IntersectsAabb(simplexBoundsCS, shape.is2D))
                        {
                            float4 simplexBary = BurstMath.BarycenterForSimplexOfSize(simplexSize);

                            triangleMeshShape.tri.Cache(v1 * colliderToSolver.scale, v2 * colliderToSolver.scale, v3 * colliderToSolver.scale);

                            var colliderPoint = BurstLocalOptimization.Optimize(ref triangleMeshShape, positions, orientations, radii, simplices, simplexStart, simplexSize,
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

                            if (vel * deltaTime + dAB <= simplexRadius + shape.contactOffset + parameters.collisionMargin)
                            {
                                contactsQueue.Enqueue(new BurstContact()
                                {
                                    bodyA = simplexIndex,
                                    bodyB = colliderIndex,
                                    pointA = simplexBary,
                                    pointB = colliderPoint.point,
                                    normal = colliderPoint.normal * triangleMeshShape.shape.sign
                                });
                            }
                        }
                    }
                }
                else // check min and/or max children:
                {
                    // visit min node:
                    if (simplexBoundsCS.min[node.axis] <= node.min)
                        queue.Enqueue(node.firstChild);

                    // visit max node:
                    if (simplexBoundsCS.max[node.axis] >= node.max)
                        queue.Enqueue(node.firstChild + 1);
                }
            }
        }
    }
}
#endif