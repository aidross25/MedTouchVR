#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    public struct BurstHeightField : BurstLocalOptimization.IDistanceFunction
    {
        public BurstColliderShape shape;
        public BurstAffineTransform colliderToSolver;

        public BurstMath.CachedTri tri;
        public float4 triNormal;

        public HeightFieldHeader header;
        public NativeArray<float> heightFieldSamples;

        public void Evaluate(float4 point, float4 radii, quaternion orientation, ref BurstLocalOptimization.SurfacePoint projectedPoint)
        {
            point = colliderToSolver.InverseTransformPoint(point);

            float4 nearestPoint = BurstMath.NearestPointOnTri(tri, point, out _);
            float4 normal = math.normalizesafe(point - nearestPoint);

            // flip the contact normal if it points below ground: (doesn't work with holes)
            //BurstMath.OneSidedNormal(triNormal, ref normal);

            projectedPoint.point = colliderToSolver.TransformPoint(nearestPoint + normal * shape.contactOffset);
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
            int pairCount = contactOffsetsPerType[(int)Oni.ShapeType.Heightmap + 1] - contactOffsetsPerType[(int)Oni.ShapeType.Heightmap];
            if (pairCount == 0) return inputDeps;

            var job = new GenerateHeightFieldContactsJob
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

                heightFieldHeaders = world.heightFieldContainer.headers.AsNativeArray<HeightFieldHeader>(),
                heightFieldSamples = world.heightFieldContainer.samples.AsNativeArray<float>(),

                contactsQueue = contactQueue.AsParallelWriter(),

                solverToWorld = solver.inertialFrame,
                worldToSolver = solver.worldToSolver,
                deltaTime = deltaTime,
                parameters = solver.abstraction.parameters,
                firstPair = contactOffsetsPerType[(int)Oni.ShapeType.Heightmap]
            };

            inputDeps = job.Schedule(pairCount, 1, inputDeps);
            return inputDeps;
        }

    }

    [BurstCompile]
    struct GenerateHeightFieldContactsJob : IJobParallelFor
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

        // height field data:
        [ReadOnly] public NativeArray<HeightFieldHeader> heightFieldHeaders;
        [ReadOnly] public NativeArray<float> heightFieldSamples;

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

            var header = heightFieldHeaders[shape.dataIndex];
            int rigidbodyIndex = shapes[colliderIndex].rigidbodyIndex;

            int simplexStart = simplexCounts.GetSimplexStartAndSize(simplexIndex, out int simplexSize);
            var simplexBound = simplexBounds[simplexIndex];

            BurstAffineTransform colliderToSolver = worldToSolver * transforms[colliderIndex];

            // invert a full matrix here to accurately represent collider bounds scale.
            var solverToCollider = math.inverse(float4x4.TRS(colliderToSolver.translation.xyz, colliderToSolver.rotation, colliderToSolver.scale.xyz));
            var simplexBoundsCS = simplexBound.Transformed(solverToCollider);

            BurstHeightField triangleMeshShape = new BurstHeightField()
            {
                colliderToSolver = colliderToSolver,
                shape = shapes[colliderIndex],
                header = heightFieldHeaders[shapes[colliderIndex].dataIndex],
                heightFieldSamples = heightFieldSamples
            };

            float4 triNormal = float4.zero;

            var co = new BurstContact { bodyA = simplexIndex, bodyB = colliderIndex };

            int resolutionU = (int)shape.center.x;
            int resolutionV = (int)shape.center.y;

            // calculate terrain cell size:
            float cellWidth = shape.size.x / (resolutionU - 1);
            float cellHeight = shape.size.z / (resolutionV - 1);

            // calculate particle bounds min/max cells:
            int2 min = new int2((int)math.floor(simplexBoundsCS.min[0] / cellWidth), (int)math.floor(simplexBoundsCS.min[2] / cellHeight));
            int2 max = new int2((int)math.floor(simplexBoundsCS.max[0] / cellWidth), (int)math.floor(simplexBoundsCS.max[2] / cellHeight));

            for (int su = min[0]; su <= max[0]; ++su)
            {
                if (su >= 0 && su < resolutionU - 1)
                {
                    for (int sv = min[1]; sv <= max[1]; ++sv)
                    {
                        if (sv >= 0 && sv < resolutionV - 1)
                        {
                            // calculate neighbor sample indices:
                            int csu1 = math.clamp(su + 1, 0, resolutionU - 1);
                            int csv1 = math.clamp(sv + 1, 0, resolutionV - 1);

                            // sample heights:
                            float h1 = heightFieldSamples[header.firstSample + sv * resolutionU + su] * shape.size.y;
                            float h2 = heightFieldSamples[header.firstSample + sv * resolutionU + csu1] * shape.size.y;
                            float h3 = heightFieldSamples[header.firstSample + csv1 * resolutionU + su] * shape.size.y;
                            float h4 = heightFieldSamples[header.firstSample + csv1 * resolutionU + csu1] * shape.size.y;

                            if (h1 < 0) continue;
                            h1 = math.abs(h1);
                            h2 = math.abs(h2);
                            h3 = math.abs(h3);
                            h4 = math.abs(h4);

                            float min_x = su * shape.size.x / (resolutionU - 1);
                            float max_x = csu1 * shape.size.x / (resolutionU - 1);
                            float min_z = sv * shape.size.z / (resolutionV - 1);
                            float max_z = csv1 * shape.size.z / (resolutionV - 1);

                            float4 convexPoint;
                            float4 simplexBary = BurstMath.BarycenterForSimplexOfSize(simplexSize);

                            // ------contact against the first triangle------:
                            float4 v1 = new float4(min_x, h3, max_z, 0);
                            float4 v2 = new float4(max_x, h4, max_z, 0);
                            float4 v3 = new float4(min_x, h1, min_z, 0);

                            triangleMeshShape.tri.Cache(v1, v2, v3);
                            triNormal.xyz = math.normalizesafe(math.cross((v2 - v1).xyz, (v3 - v1).xyz));

                            var colliderPoint = BurstLocalOptimization.Optimize(ref triangleMeshShape, positions, orientations, radii, simplices, simplexStart, simplexSize,
                                                                                ref simplexBary, out convexPoint, parameters.surfaceCollisionIterations, parameters.surfaceCollisionTolerance);

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

                            float dAB = math.dot(convexPoint - colliderPoint.point, colliderPoint.normal);
                            float vel = math.dot(velocity - rbVelocity, colliderPoint.normal);

                            if (vel * deltaTime + dAB <= simplexRadius + shape.contactOffset + parameters.collisionMargin)
                            {
                                co.pointB = colliderPoint.point;
                                co.normal = colliderPoint.normal * triangleMeshShape.shape.sign;
                                co.pointA = simplexBary;
                                contactsQueue.Enqueue(co);
                            }

                            // ------contact against the second triangle------:
                            v1 = new float4(min_x, h1, min_z, 0);
                            v2 = new float4(max_x, h4, max_z, 0);
                            v3 = new float4(max_x, h2, min_z, 0);

                            triangleMeshShape.tri.Cache(v1, v2, v3);
                            triNormal.xyz = math.normalizesafe(math.cross((v2 - v1).xyz, (v3 - v1).xyz));

                            colliderPoint = BurstLocalOptimization.Optimize(ref triangleMeshShape, positions, orientations, radii, simplices, simplexStart, simplexSize,
                                                                            ref simplexBary, out convexPoint, parameters.surfaceCollisionIterations, parameters.surfaceCollisionTolerance);

                            velocity = float4.zero;
                            simplexRadius = 0;
                            for (int j = 0; j < simplexSize; ++j)
                            {
                                int particleIndex = simplices[simplexStart + j];
                                simplexRadius += radii[particleIndex].x * simplexBary[j];
                                velocity += velocities[particleIndex] * simplexBary[j];
                            }

                            rbVelocity = float4.zero;
                            if (rigidbodyIndex >= 0)
                                rbVelocity = BurstMath.GetRigidbodyVelocityAtPoint(rigidbodyIndex, colliderPoint.point, rigidbodies, solverToWorld);

                            dAB = math.dot(convexPoint - colliderPoint.point, colliderPoint.normal);
                            vel = math.dot(velocity - rbVelocity, colliderPoint.normal);

                            if (vel * deltaTime + dAB <= simplexRadius + shape.contactOffset + parameters.collisionMargin)
                            {
                                co.pointB = colliderPoint.point;
                                co.normal = colliderPoint.normal * triangleMeshShape.shape.sign;
                                co.pointA = simplexBary;

                                contactsQueue.Enqueue(co);
                            }
                        }
                    }
                }
            }
        }
    }

}
#endif