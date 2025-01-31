#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;

#if (SRP_UNIVERSAL)
using UnityEngine.Rendering.Universal;
#endif

namespace Obi
{

    public class BurstFoamRenderSystem : ObiFoamRenderSystem
    {
        protected NativeArray<float2> sortHandles;

        protected struct SortHandleComparer : IComparer<float2>
        {
            public int Compare(float2 a, float2 b)
            {
                return b.y.CompareTo(a.y);
            }
        }

        protected SortHandleComparer comparer = new SortHandleComparer();

        public BurstFoamRenderSystem(ObiSolver solver) : base(solver)
        {
#if (SRP_UNIVERSAL)
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                renderBatch = new ProceduralRenderBatch<DiffuseParticleVertex>(0, Resources.Load<Material>("ObiMaterials/URP/Fluid/FoamParticlesURP"), new RenderBatchParams(true));
            else
#endif
                renderBatch = new ProceduralRenderBatch<DiffuseParticleVertex>(0, Resources.Load<Material>("ObiMaterials/Fluid/FoamParticles"), new RenderBatchParams(true));
            ReallocateRenderBatch();
        }

        public override void Dispose()
        {
            base.Dispose();

            if (sortHandles.IsCreated)
                sortHandles.Dispose();
        }

        private void ReallocateRenderBatch()
        {
            // in case the amount of particles allocated does not match
            // the amount requested by the solver, reallocate
            if (!sortHandles.IsCreated || m_Solver.foamPositions.count * 4 != renderBatch.vertexCount)
            {
                renderBatch.Dispose();
                renderBatch.vertexCount = m_Solver.foamPositions.count * 4;
                renderBatch.triangleCount = m_Solver.foamPositions.count * 2;
                renderBatch.Initialize(layout);

                if (sortHandles.IsCreated)
                    sortHandles.Dispose();
                sortHandles = new NativeArray<float2>(m_Solver.foamPositions.count, Allocator.Persistent);
            }
        }

        public override void Setup()
        {
        }

        public override void Step()
        {
        }

        public override unsafe void Render()
        {
            if (!Application.isPlaying)
                return;

            var solver = m_Solver.implementation as BurstSolverImpl;

            ReallocateRenderBatch();

            foreach (Camera camera in cameras)
            {
                if (camera == null)
                    continue;

                JobHandle inputDeps = new JobHandle();
                var sortJob = sortHandles.Slice(0, m_Solver.foamCount[3]).SortJob(comparer);

                //Clear all triangle indices to zero:
                UnsafeUtility.MemClear(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(renderBatch.triangles),
                    UnsafeUtility.SizeOf<int>() * renderBatch.triangles.Length);

                var projectJob = new ProjectOnSortAxisJob
                {
                    inputPositions = solver.abstraction.foamPositions.AsNativeArray<float4>(),
                    sortHandles = sortHandles,
                    sortAxis = camera.transform.forward
                };

                inputDeps = projectJob.Schedule(m_Solver.foamCount[3], 256, inputDeps);

                inputDeps = sortJob.Schedule(inputDeps);

                var sortParticlesJob = new SortParticles
                {
                    sortHandles = sortHandles,
                    inputPositions = solver.abstraction.foamPositions.AsNativeArray<float4>(),
                    inputVelocities = solver.abstraction.foamVelocities.AsNativeArray<float4>(),
                    inputColors = solver.abstraction.foamColors.AsNativeArray<float4>(),
                    inputAttributes = solver.abstraction.foamAttributes.AsNativeArray<float4>(),

                    outputPositions = solver.auxPositions,
                    outputVelocities = solver.auxVelocities,
                    outputColors = solver.auxColors,
                    outputAttributes = solver.auxAttributes
                }; 

                inputDeps = sortParticlesJob.Schedule(m_Solver.foamCount[3], 256, inputDeps);

                var meshJob = new BuildFoamMeshDataJob
                {
                    inputPositions = solver.auxPositions,
                    inputVelocities = solver.auxVelocities,
                    inputColors = solver.auxColors,
                    inputAttributes = solver.auxAttributes,

                    vertices = renderBatch.vertices,
                    indices = renderBatch.triangles,
                };

                inputDeps = meshJob.Schedule(m_Solver.foamCount[3], 128, inputDeps);

                inputDeps.Complete();

                renderBatch.mesh.SetVertexBufferData(renderBatch.vertices, 0, 0, renderBatch.vertexCount, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontNotifyMeshUsers);
                renderBatch.mesh.SetIndexBufferData(renderBatch.triangles, 0, 0, renderBatch.triangleCount * 3, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                matProps.SetFloat("_FadeDepth", 0);
                matProps.SetFloat("_VelocityStretching", m_Solver.maxFoamVelocityStretch);
                matProps.SetFloat("_FadeIn", m_Solver.foamFade.x);
                matProps.SetFloat("_FadeOut", m_Solver.foamFade.y);

                var rp = renderBatch.renderParams;
                rp.worldBounds = m_Solver.bounds;
                rp.camera = camera;
                rp.matProps = matProps;

                Graphics.RenderMesh(rp, renderBatch.mesh, 0, m_Solver.transform.localToWorldMatrix, m_Solver.transform.localToWorldMatrix);
            }
        }



        [BurstCompile]
        unsafe struct ProjectOnSortAxisJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4> inputPositions;
            [NativeDisableParallelForRestriction] public NativeArray<float2> sortHandles;

            public float3 sortAxis;

            public void Execute(int i)
            {
                sortHandles[i] = new float2(i, math.dot(inputPositions[i].xyz, sortAxis));
            }
        }

        [BurstCompile]
        unsafe struct SortParticles : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float2> sortHandles;

            [ReadOnly] public NativeArray<float4> inputPositions;
            [ReadOnly] public NativeArray<float4> inputVelocities;
            [ReadOnly] public NativeArray<float4> inputColors;
            [ReadOnly] public NativeArray<float4> inputAttributes;

            [NativeDisableParallelForRestriction] public NativeArray<float4> outputPositions;
            [NativeDisableParallelForRestriction] public NativeArray<float4> outputVelocities;
            [NativeDisableParallelForRestriction] public NativeArray<float4> outputColors;
            [NativeDisableParallelForRestriction] public NativeArray<float4> outputAttributes;

            public void Execute(int i)
            {
                int o = (int)sortHandles[i].x;
                outputPositions[i] = inputPositions[o];
                outputVelocities[i] = inputVelocities[o];
                outputColors[i] = inputColors[o];
                outputAttributes[i] = inputAttributes[o];
            }
        }

        [BurstCompile]
        struct BuildFoamMeshDataJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4> inputPositions;
            [ReadOnly] public NativeArray<float4> inputVelocities;
            [ReadOnly] public NativeArray<float4> inputColors;
            [ReadOnly] public NativeArray<float4> inputAttributes;

            [NativeDisableParallelForRestriction] public NativeArray<DiffuseParticleVertex> vertices;
            [NativeDisableParallelForRestriction] public NativeArray<int> indices;

            public void Execute(int i)
            {
                DiffuseParticleVertex v = new DiffuseParticleVertex();

                v.pos = new float4(inputPositions[i].xyz, 1);
                v.color = inputColors[i];
                v.velocity = inputVelocities[i];
                v.attributes = inputAttributes[i];

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
}
#endif
