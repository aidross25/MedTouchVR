#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    public struct BurstEdgeMesh : BurstLocalOptimization.IDistanceFunction
    {

        public BurstColliderShape shape;
        public BurstAffineTransform colliderToSolver;
        public int dataOffset;

        public EdgeMeshHeader header;
        public NativeArray<BIHNode> edgeBihNodes;
        public NativeArray<Edge> edges;
        public NativeArray<float2> vertices;

        public void Evaluate(float4 point, float4 radii, quaternion orientation, ref BurstLocalOptimization.SurfacePoint projectedPoint)
        {
            point = colliderToSolver.InverseTransformPointUnscaled(point);

            if (shape.is2D)
                point[2] = 0;

            Edge t = edges[header.firstEdge + dataOffset];
            float4 v1 = (new float4(vertices[header.firstVertex + t.i1], 0, 0) + shape.center) * colliderToSolver.scale;
            float4 v2 = (new float4(vertices[header.firstVertex + t.i2], 0, 0) + shape.center) * colliderToSolver.scale;

            float4 nearestPoint = BurstMath.NearestPointOnEdge(v1, v2, point, out float mu);
            float4 normal = math.normalizesafe(point - nearestPoint);

            projectedPoint.normal = colliderToSolver.TransformDirection(normal);
            projectedPoint.point = colliderToSolver.TransformPointUnscaled(nearestPoint + normal * shape.contactOffset);
        }

        public static JobHandle GenerateContacts(ObiColliderWorld world,
                                               BurstSolverImpl solver,
                                               NativeList<Oni.ContactPair> contactPairs,
                                               NativeQueue<BurstContact> contactQueue,
                                               NativeArray<int> contactOffsetsPerType,
                                               float deltaTime,
                                               JobHandle inputDeps)
        {
            int pairCount = contactOffsetsPerType[(int)Oni.ShapeType.EdgeMesh + 1] - contactOffsetsPerType[(int)Oni.ShapeType.EdgeMesh];
            if (pairCount == 0) return inputDeps;

            var job = new GenerateEdgeMeshContactsJob
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

                edgeMeshHeaders = world.edgeMeshContainer.headers.AsNativeArray<EdgeMeshHeader>(),
                edgeBihNodes = world.edgeMeshContainer.bihNodes.AsNativeArray<BIHNode>(),
                edges = world.edgeMeshContainer.edges.AsNativeArray<Edge>(),
                edgeVertices = world.edgeMeshContainer.vertices.AsNativeArray<float2>(),

                contactsQueue = contactQueue.AsParallelWriter(),

                solverToWorld = solver.solverToWorld,
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
    struct GenerateEdgeMeshContactsJob : IJobParallelFor
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

        // edge mesh data:
        [ReadOnly] public NativeArray<EdgeMeshHeader> edgeMeshHeaders;
        [ReadOnly] public NativeArray<BIHNode> edgeBihNodes;
        [ReadOnly] public NativeArray<Edge> edges;
        [ReadOnly] public NativeArray<float2> edgeVertices;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeQueue<BurstContact>.ParallelWriter contactsQueue;

        // auxiliar data:
        [ReadOnly] public int firstPair;
        [ReadOnly] public BurstAffineTransform solverToWorld;
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

            var header = edgeMeshHeaders[shape.dataIndex];

            int simplexStart = simplexCounts.GetSimplexStartAndSize(simplexIndex, out int simplexSize);
            var simplexBound = simplexBounds[simplexIndex];

            BurstAffineTransform colliderToSolver = worldToSolver * transforms[colliderIndex];

            // invert a full matrix here to accurately represent collider bounds scale.
            var solverToCollider = math.inverse(float4x4.TRS(colliderToSolver.translation.xyz, colliderToSolver.rotation, colliderToSolver.scale.xyz));
            var simplexBoundsCS = simplexBound.Transformed(solverToCollider);

            float4 marginCS = new float4((shape.contactOffset + parameters.collisionMargin) / colliderToSolver.scale.xyz, 0);

            BurstEdgeMesh edgeMeshShape = new BurstEdgeMesh()
            {
                colliderToSolver = colliderToSolver,
                shape = shape,
                header = header,
                edgeBihNodes = edgeBihNodes,
                edges = edges,
                vertices = edgeVertices
            };

            NativeQueue<int> queue = new NativeQueue<int>(Allocator.Temp);

            queue.Enqueue(0);

            while (!queue.IsEmpty())
            {
                int nodeIndex = queue.Dequeue();
                var node = edgeBihNodes[header.firstNode + nodeIndex];

                // leaf node:
                if (node.firstChild < 0)
                {
                    // check for contact against all triangles:
                    for (int dataOffset = node.start; dataOffset < node.start + node.count; ++dataOffset)
                    {
                        Edge t = edges[header.firstEdge + dataOffset];
                        float4 v1 = new float4(edgeVertices[header.firstVertex + t.i1], 0, 0) + shape.center;
                        float4 v2 = new float4(edgeVertices[header.firstVertex + t.i2], 0, 0) + shape.center;
                        BurstAabb edgeBounds = new BurstAabb(v1, v2, marginCS);

                        if (edgeBounds.IntersectsAabb(simplexBoundsCS, shape.is2D))
                        {
                            float4 simplexBary = BurstMath.BarycenterForSimplexOfSize(simplexSize);

                            edgeMeshShape.dataOffset = dataOffset;
                            var colliderPoint = BurstLocalOptimization.Optimize(ref edgeMeshShape, positions, orientations, radii, simplices, simplexStart, simplexSize,
                                                                                ref simplexBary, out float4 convexPoint, parameters.surfaceCollisionIterations, parameters.surfaceCollisionTolerance);

                            contactsQueue.Enqueue(new BurstContact(){
                                bodyA = simplexIndex,
                                bodyB = colliderIndex,
                                pointA = simplexBary,
                                pointB = colliderPoint.point,
                                normal = colliderPoint.normal * edgeMeshShape.shape.sign
                            });
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