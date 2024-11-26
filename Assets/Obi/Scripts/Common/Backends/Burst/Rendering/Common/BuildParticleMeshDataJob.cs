#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;

namespace Obi
{
    [BurstCompile]
    struct BuildParticleMeshDataJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> particleIndices;
        [ReadOnly] public NativeArray<int> rendererIndices;
        [ReadOnly] public NativeArray<ParticleRendererData> rendererData;

        [ReadOnly] public NativeArray<float4> renderablePositions;
        [ReadOnly] public NativeArray<quaternion> renderableOrientations;
        [ReadOnly] public NativeArray<float4> renderableRadii;
        [ReadOnly] public NativeArray<float4> colors;

        [NativeDisableParallelForRestriction] public NativeArray<ParticleVertex> vertices;
        [NativeDisableParallelForRestriction] public NativeArray<int> indices;

        [ReadOnly] public int firstParticle;

        public void Execute(int i)
        {
            int p = particleIndices[firstParticle + i];
            int r = rendererIndices[firstParticle + i];

            ParticleVertex v = new ParticleVertex();

            v.pos = new float4(renderablePositions[p].xyz, 1);
            v.color = colors[p] * (Vector4)rendererData[r].color;
            v.b1 = new float4(math.mul(renderableOrientations[p], new float3(1, 0, 0)), renderableRadii[p][0] * renderableRadii[p][3] * rendererData[r].radiusScale);
            v.b2 = new float4(math.mul(renderableOrientations[p], new float3(0, 1, 0)), renderableRadii[p][1] * renderableRadii[p][3] * rendererData[r].radiusScale);
            v.b3 = new float4(math.mul(renderableOrientations[p], new float3(0, 0, 1)), renderableRadii[p][2] * renderableRadii[p][3] * rendererData[r].radiusScale);

            v.offset = new float3(1, 1, 0);
            vertices[i * 4] = v;

            v.offset = new float3(-1, 1, 0);
            vertices[i * 4 + 1] = v;

            v.offset = new float3(-1, -1, 0);
            vertices[i * 4 + 2] = v;

            v.offset = new float3(1, -1, 0);
            vertices[i * 4 + 3] = v;

            indices[i * 6] = (i * 4 + 2);
            indices[i * 6 + 1] = (i * 4 + 1);
            indices[i * 6 + 2] = (i * 4);

            indices[i * 6 + 3] = (i * 4 + 3);
            indices[i * 6 + 4] = (i * 4 + 2);
            indices[i * 6 + 5] = (i * 4);
        }
    }
}
#endif