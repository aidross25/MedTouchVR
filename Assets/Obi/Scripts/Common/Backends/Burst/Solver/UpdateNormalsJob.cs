#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;

namespace Obi
{

    [BurstCompile]
    struct ResetNormals : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> phases;
        [WriteOnly] public NativeArray<float4> normals;
        [WriteOnly] public NativeArray<float4> tangents;

        public void Execute(int i)
        {
            // leave fluid normals intact.
            if ((phases[i] & (int)ObiUtils.ParticleFlags.Fluid) == 0)
            {
                normals[i] = float4.zero;
                tangents[i] = float4.zero;
            }
        }
    }

    [BurstCompile]
    unsafe struct UpdateTriangleNormalsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> deformableTriangles;
        [ReadOnly] public NativeArray<float2> deformableTriangleUVs;
        [ReadOnly] public NativeArray<float4> renderPositions;

        [NativeDisableParallelForRestriction] public NativeArray<float4> normals;
        [NativeDisableParallelForRestriction] public NativeArray<float4> tangents;

        public void Execute(int i)
        {
            int p1 = deformableTriangles[i*3];
            int p2 = deformableTriangles[i*3 + 1];
            int p3 = deformableTriangles[i*3 + 2];

            float3 m1 = (renderPositions[p2] - renderPositions[p1]).xyz;
            float3 m2 = (renderPositions[p3] - renderPositions[p1]).xyz;

            float2 s = deformableTriangleUVs[i * 3 + 1] - deformableTriangleUVs[i * 3];
            float2 t = deformableTriangleUVs[i * 3 + 2] - deformableTriangleUVs[i * 3];

            float4 normal  = new float4(math.cross(m1, m2), 0);
            float4 tangent = float4.zero;

            float area = s.x * t.y - t.x * s.y;

            if (math.abs(area) > BurstMath.epsilon)
            {
                tangent = new float4(t.y * m1.x - s.y * m2.x,
                                     t.y * m1.y - s.y * m2.y,
                                     t.y * m1.z - s.y * m2.z, 0) / area;
            }

            BurstMath.AtomicAdd(normals, p1, normal);
            BurstMath.AtomicAdd(normals, p2, normal);
            BurstMath.AtomicAdd(normals, p3, normal);

            BurstMath.AtomicAdd(tangents, p1, tangent);
            BurstMath.AtomicAdd(tangents, p2, tangent);
            BurstMath.AtomicAdd(tangents, p3, tangent);
        }
    }

    [BurstCompile]
    unsafe struct UpdateEdgeNormalsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> deformableEdges;
        [ReadOnly] public NativeArray<float4> wind;
        [ReadOnly] public NativeArray<float4> renderPositions;

        [NativeDisableParallelForRestriction] public NativeArray<float4> normals;

        public void Execute(int i)
        {
            int p1 = deformableEdges[i * 2];
            int p2 = deformableEdges[i * 2 + 1];

            float4 edge = renderPositions[p2] - renderPositions[p1];
            float4 avgWind = (wind[p1] + wind[p2]) * 0.5f;
            float4 normal = avgWind - math.projectsafe(avgWind, edge);

            BurstMath.AtomicAdd(normals, p1, normal);
            BurstMath.AtomicAdd(normals, p2, normal);
        }
    }

    [BurstCompile]
    struct RenderableOrientationFromNormals : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> phases;

        public NativeArray<float4> normals;
        public NativeArray<float4> tangents;

        [WriteOnly] public NativeArray<quaternion> renderableOrientations;

        public void Execute(int i)
        {
            if (math.lengthsq(normals[i]) > BurstMath.epsilon &&
                math.lengthsq(tangents[i]) > BurstMath.epsilon &&
                (phases[i] & (int)ObiUtils.ParticleFlags.Fluid) == 0)
            {
                normals[i] = math.normalizesafe(normals[i]);
                tangents[i] = math.normalizesafe(tangents[i]);

                // particle orientation from normal/tangent:
                renderableOrientations[i] = quaternion.LookRotation(normals[i].xyz, tangents[i].xyz);
            }
        }
    }
}
#endif
