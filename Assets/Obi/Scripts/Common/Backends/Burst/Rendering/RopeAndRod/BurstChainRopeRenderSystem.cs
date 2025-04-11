#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Obi
{

    public class BurstChainRopeRenderSystem : ObiChainRopeRenderSystem
    {
        protected Matrix4x4[] transformsArray = new Matrix4x4[1023];

        public BurstChainRopeRenderSystem(ObiSolver solver) : base(solver)
        {
        }

        public override void Setup()
        {
            base.Setup();
        }

        public override void Render()
        {
            using (m_RenderMarker.Auto())
            {
                // generate raw frames using parallel transport
                var instanceTransformsJob = new InstanceTransforms()
                {
                    rendererData = rendererData.AsNativeArray<ChainRendererData>(),
                    chunkData = chunkData.AsNativeArray<ChunkData>(),
                    modifiers = modifiers.AsNativeArray<ObiRopeChainRenderer.LinkModifier>(),
                    elements = elements.AsNativeArray<int2>(),

                    instanceTransforms = instanceTransforms.AsNativeArray<float4x4>(),
                    instanceColors = instanceColors.AsNativeArray<float4>(),

                    renderablePositions = m_Solver.renderablePositions.AsNativeArray<float4>(),
                    renderableOrientations = m_Solver.renderableOrientations.AsNativeArray<quaternion>(),
                    principalRadii = m_Solver.principalRadii.AsNativeArray<float4>(),
                    colors = m_Solver.colors.AsNativeArray<float4>(),
                    solverToWorld = m_Solver.transform.localToWorldMatrix,
                };

                instanceTransformsJob.Schedule(chunkData.count, 8).Complete();

                //Draw instances:
                for (int i = 0; i < batchList.Count; i++)
                {
                    var batch = batchList[i];

                    var rp = batch.renderParams;
                    rp.material = batch.material;
                    rp.worldBounds = m_Solver.bounds;

                    Graphics.RenderMeshInstanced(rp, batch.mesh, 0, instanceTransforms.AsNativeArray<Matrix4x4>(), batch.instanceCount, batch.firstInstance);
                }
                
            }
        }

        [BurstCompile]
        struct InstanceTransforms : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ChainRendererData> rendererData;
            [ReadOnly] public NativeArray<ChunkData> chunkData;
            [ReadOnly] public NativeArray<ObiRopeChainRenderer.LinkModifier> modifiers;
            [ReadOnly] public NativeArray<int2> elements;

            [ReadOnly] public NativeArray<float4> renderablePositions;
            [ReadOnly] public NativeArray<quaternion> renderableOrientations;
            [ReadOnly] public NativeArray<float4> principalRadii;
            [ReadOnly] public NativeArray<float4> colors;
            [ReadOnly] public float4x4 solverToWorld;

            [NativeDisableParallelForRestriction] public NativeArray<float4x4> instanceTransforms;
            [NativeDisableParallelForRestriction] public NativeArray<float4> instanceColors;

            public void Execute(int i)
            {
                int firstIndex = i > 0 ? chunkData[i - 1].offset : 0;
                int elementCount = chunkData[i].offset - firstIndex;

                var rendererIndex = chunkData[i].rendererIndex;
                var renderer = rendererData[rendererIndex];

                float3 rendScale = ((float4)renderer.scale).xyz;

                int firstModifier = rendererIndex > 0 ? rendererData[rendererIndex - 1].modifierOffset : 0;
                int modifierCount = renderer.modifierOffset - firstModifier;

                var modifier = new ObiRopeChainRenderer.LinkModifier();
                modifier.Clear();

                BurstPathFrame frame = new BurstPathFrame();
                frame.Reset();

                float twist = -renderer.twist * elementCount * renderer.twistAnchor;
                frame.SetTwist(twist);

                // parallel transport:
                for (int m = 0; m < elementCount; ++m)
                {
                    if (modifierCount > 0)
                        modifier = modifiers[firstModifier + m % modifierCount];

                    int index = firstIndex + m;
                    float4 pos     = renderablePositions[elements[index].x];
                    float4 nextPos = renderablePositions[elements[index].y];
                    float4 vector = nextPos - pos;
                    float3 tangent = math.normalizesafe(vector.xyz);

                    if (renderer.usesOrientedParticles == 1)
                    {
                        frame.Transport(nextPos.xyz, tangent, math.rotate(renderableOrientations[elements[index].x], new float3(0, 1, 0)), twist);
                        twist += renderer.twist;
                    }
                    else
                        frame.Transport(nextPos.xyz, tangent, renderer.twist);

                    var rotation = quaternion.LookRotationSafe(frame.tangent, frame.normal); 
                    var position = (pos + vector * 0.5f).xyz + math.mul(rotation, modifier.translation);
                    var scale = principalRadii[elements[index].x].x * 2 * rendScale * modifier.scale;

                    rotation = math.mul(rotation, quaternion.Euler(math.radians(modifier.rotation)));

                    instanceTransforms[index] = math.mul(solverToWorld,float4x4.TRS(position,rotation,scale));
                    instanceColors[index] = (colors[elements[index].x] + colors[elements[index].y]) * 0.5f;
                }
            }
           
        }
    }
}
#endif

