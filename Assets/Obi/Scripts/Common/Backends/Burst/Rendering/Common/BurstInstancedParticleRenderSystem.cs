#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace Obi
{

    public class BurstInstancedParticleRenderSystem : ObiInstancedParticleRenderSystem
    {

        public BurstInstancedParticleRenderSystem(ObiSolver solver) : base(solver)
        {
        }

        public override void Render()
        {
            using (m_RenderMarker.Auto())
            {
                var instanceTransformsJob = new InstancedParticleTransforms
                {
                    activeParticles = activeParticles.AsNativeArray<int>(),
                    rendererData = rendererData.AsNativeArray<ParticleRendererData>(),
                    rendererIndex = rendererIndex.AsNativeArray<int>(),
                    instanceTransforms = instanceTransforms.AsNativeArray<float4x4>(),
                    instanceColors = instanceColors.AsNativeArray<float4>(),

                    renderablePositions = m_Solver.renderablePositions.AsNativeArray<float4>(),
                    renderableOrientations = m_Solver.renderableOrientations.AsNativeArray<quaternion>(),
                    renderableRadii = m_Solver.renderableRadii.AsNativeArray<float4>(),
                    colors = m_Solver.colors.AsNativeArray<float4>(),
                    solverToWorld = m_Solver.transform.localToWorldMatrix
                };

                instanceTransformsJob.Schedule(activeParticles.count, 32).Complete();

                var mpb = new MaterialPropertyBlock();

                //Draw instances:
                for (int i = 0; i < batchList.Count; i++)
                {
                    var batch = batchList[i];

                    if (batch.instanceCount > 0)
                    {
                        // workaround for RenderMeshInstanced bug
                        // (https://forum.unity.com/threads/gpu-instanced-custom-properties-dont-take-unity_baseinstanceid-into-account.1520602/)
                        // also, no NativeArray<> overload :(
                        mpb.SetVectorArray("_Colors", instanceColors.AsNativeArray<Vector4>().Slice(batch.firstInstance, batch.instanceCount).ToArray()); 

                        var rp = batch.renderParams;
                        rp.material = batch.material;
                        rp.worldBounds = m_Solver.bounds;
                        rp.matProps = mpb;

                        // TODO: use generic overload to pass matrix + instance color.
                        Graphics.RenderMeshInstanced(rp, batch.mesh, 0, instanceTransforms.AsNativeArray<Matrix4x4>(), batch.instanceCount, batch.firstInstance);
                    }
                }
                
            }
        }

        [BurstCompile]
        struct InstancedParticleTransforms : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> activeParticles;
            [ReadOnly] public NativeArray<ParticleRendererData> rendererData;
            [ReadOnly] public NativeArray<int> rendererIndex;

            [ReadOnly] public NativeArray<float4> renderablePositions;
            [ReadOnly] public NativeArray<quaternion> renderableOrientations;
            [ReadOnly] public NativeArray<float4> renderableRadii;
            [ReadOnly] public NativeArray<float4> colors;
            [ReadOnly] public float4x4 solverToWorld;

            [NativeDisableParallelForRestriction] public NativeArray<float4x4> instanceTransforms;
            [NativeDisableParallelForRestriction] public NativeArray<float4> instanceColors;

            public void Execute(int i)
            {
                int p = activeParticles[i];

                Matrix4x4 tfrm = float4x4.TRS(renderablePositions[p].xyz,
                                              renderableOrientations[p],
                                              renderableRadii[p].xyz * renderableRadii[p][3] * rendererData[rendererIndex[i]].radiusScale);

                instanceTransforms[i] = math.mul(solverToWorld, tfrm);

                instanceColors[i] = colors[p] * (Vector4)rendererData[rendererIndex[i]].color;
            }
           
        }
    }
}
#endif

