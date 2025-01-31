#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Obi
{
    public struct BurstRay : BurstLocalOptimization.IDistanceFunction
    {
        public BurstQueryShape shape;
        public BurstAffineTransform colliderToSolver;

        public void Evaluate(float4 point, float4 radii, quaternion orientation, ref BurstLocalOptimization.SurfacePoint projectedPoint)
        {
            float4x4 simplexToSolver = float4x4.TRS(point.xyz, orientation, radii.xyz);
            float4x4 solverToSimplex = math.inverse(simplexToSolver);
            float4x4 colliderToSimplex = math.mul(solverToSimplex, float4x4.TRS(colliderToSolver.translation.xyz, colliderToSolver.rotation, colliderToSolver.scale.xyz));

            // express ray in simplex space (ellipsoid == scaled sphere)
            float4 rayOrigin = math.mul(colliderToSimplex, new float4(shape.center.xyz,1));
            float4 rayDirection = math.normalizesafe(math.mul(colliderToSimplex, new float4(shape.size.xyz,0)));

            float rayDistance = ObiUtils.RaySphereIntersection(rayOrigin.xyz, rayDirection.xyz, float3.zero, 1);

            if (rayDistance < 0)
            {
                point = colliderToSolver.InverseTransformPointUnscaled(point);

                float4 centerLine = BurstMath.NearestPointOnEdge(shape.center * colliderToSolver.scale, (shape.center + shape.size) * colliderToSolver.scale, point, out float mu);
                float4 centerToPoint = point - centerLine;
                float distanceToCenter = math.length(centerToPoint);

                float4 normal = centerToPoint / (distanceToCenter + BurstMath.epsilon);

                projectedPoint.point = colliderToSolver.TransformPointUnscaled(centerLine + normal * shape.contactOffset);
                projectedPoint.normal = colliderToSolver.TransformDirection(normal);
            }
            else
            {
                float4 rayPoint = math.mul(simplexToSolver, new float4((rayOrigin + rayDirection * rayDistance).xyz,1));
                float4 normal = math.normalizesafe(new float4((point - rayPoint).xyz,0));

                projectedPoint.point = rayPoint + normal * shape.contactOffset;
                projectedPoint.normal = normal;
            }
        }

        public void Query(int shapeIndex,
                             NativeArray<float4> positions,
                             NativeArray<quaternion> orientations,
                             NativeArray<float4> radii,
                             NativeArray<int> simplices,
                             int simplexIndex,
                             int simplexStart,
                             int simplexSize,

                             NativeQueue<BurstQueryResult>.ParallelWriter results,
                             int optimizationIterations,
                             float optimizationTolerance)
        {
            var co = new BurstQueryResult { simplexIndex = simplexIndex, queryIndex = shapeIndex };
            float4 simplexBary = BurstMath.BarycenterForSimplexOfSize(simplexSize);

            var colliderPoint = BurstLocalOptimization.Optimize(ref this, positions, orientations, radii, simplices, simplexStart, simplexSize,
                                                                ref simplexBary, out float4 convexPoint, optimizationIterations, optimizationTolerance);

            float4 simplexPrevPosition = float4.zero;
            float simplexRadius = 0;

            for (int j = 0; j < simplexSize; ++j)
            {
                int particleIndex = simplices[simplexStart + j];
                simplexPrevPosition += positions[particleIndex] * simplexBary[j];
                simplexRadius += BurstMath.EllipsoidRadius(colliderPoint.normal, orientations[particleIndex], radii[particleIndex].xyz) * simplexBary[j];
            }

            co.queryPoint = colliderPoint.point;  
            co.normal = colliderPoint.normal;
            co.simplexBary = simplexBary;
            co.distance = math.dot(simplexPrevPosition - colliderPoint.point, colliderPoint.normal) - simplexRadius;

            if (co.distance <= shape.maxDistance)
            {
                float4 pointOnRay = colliderPoint.point + colliderPoint.normal * co.distance;
                co.distanceAlongRay = math.dot(pointOnRay.xyz - shape.center.xyz, math.normalizesafe(shape.size.xyz));
                results.Enqueue(co);
            }
        }
    }

}
#endif