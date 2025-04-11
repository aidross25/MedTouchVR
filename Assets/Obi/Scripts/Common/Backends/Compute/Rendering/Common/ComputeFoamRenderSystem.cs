using UnityEngine;
using UnityEngine.Rendering;
#if (SRP_UNIVERSAL)
using UnityEngine.Rendering.Universal;
#endif

namespace Obi
{

    public class ComputeFoamRenderSystem : ObiFoamRenderSystem
    { 

        private ComputeShader foamShader;
        private int sortKernel;
        private int clearMeshKernel;
        private int buildMeshKernel;

        protected Material thickness_Material;
        protected Material color_Material;
        protected LocalKeyword shader2DFeature;

        public ComputeFoamRenderSystem(ObiSolver solver) : base (solver)
        {
            foamShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/FluidFoam"));
            sortKernel = foamShader.FindKernel("Sort");
            clearMeshKernel = foamShader.FindKernel("ClearMesh");
            buildMeshKernel = foamShader.FindKernel("BuildMesh");

#if (SRP_UNIVERSAL)
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                renderBatch = new ProceduralRenderBatch<DiffuseParticleVertex>(0, Resources.Load<Material>("ObiMaterials/URP/Fluid/FoamParticlesURP"), new RenderBatchParams(true));
            else
#endif
                renderBatch = new ProceduralRenderBatch<DiffuseParticleVertex>(0, Resources.Load<Material>("ObiMaterials/Fluid/FoamParticles"), new RenderBatchParams(true));


            renderBatch.vertexCount = (int)m_Solver.maxFoamParticles * 4;
            renderBatch.triangleCount = (int)m_Solver.maxFoamParticles * 2;
            renderBatch.Initialize(layout, true);
        }

        private void ReallocateParticleBuffers()
        {
            // in case the amount of particles allocated does not match
            // the amount requested by the solver, reallocate
            if (m_Solver.foamPositions.count * 4 != renderBatch.vertexCount)
            {
                renderBatch.Dispose();
                renderBatch.vertexCount = m_Solver.foamPositions.count * 4;
                renderBatch.triangleCount = m_Solver.foamPositions.count * 2;
                renderBatch.Initialize(layout, true);
            }
        }

        public override void Setup()
        {
            using (m_SetupRenderMarker.Auto())
            {
                for (int i = 0; i < renderers.Count; ++i)
                {
                    renderers[i].actor.solverIndices.AsComputeBuffer<int>();
                }
            }
        }

        public override void Step()
        {
            // update solver indices, since particles may have died while updating the emitter.
            for (int i = 0; i < renderers.Count; ++i)
            {
                renderers[i].actor.solverIndices.Upload();
            }
        }

        public override void Render()
        {
            var solver = m_Solver.implementation as ComputeSolverImpl;

            if (!Application.isPlaying)
                return;

            ReallocateParticleBuffers();

            if (solver.activeParticlesBuffer == null || solver.abstraction.foamPositions.computeBuffer == null)
                return;

            foreach (Camera camera in cameras)
            {
                if (camera == null)
                    continue;

                // sort by distance to camera:
                foamShader.SetVector("sortAxis", camera.transform.forward);
                foamShader.SetBuffer(sortKernel, "inputPositions", solver.abstraction.foamPositions.computeBuffer);
                foamShader.SetBuffer(sortKernel, "inputVelocities", solver.abstraction.foamVelocities.computeBuffer);
                foamShader.SetBuffer(sortKernel, "inputColors", solver.abstraction.foamColors.computeBuffer);
                foamShader.SetBuffer(sortKernel, "inputAttributes", solver.abstraction.foamAttributes.computeBuffer);
                foamShader.SetBuffer(sortKernel, "outputPositions", solver.abstraction.foamPositions.computeBuffer);
                foamShader.SetBuffer(sortKernel, "outputVelocities", solver.abstraction.foamVelocities.computeBuffer);
                foamShader.SetBuffer(sortKernel, "outputColors", solver.abstraction.foamColors.computeBuffer);
                foamShader.SetBuffer(sortKernel, "outputAttributes", solver.abstraction.foamAttributes.computeBuffer);
                foamShader.SetBuffer(sortKernel, "dispatch", solver.abstraction.foamCount.computeBuffer);

                int numPairs = ObiUtils.CeilToPowerOfTwo(m_Solver.foamPositions.count) / 2;
                int numStages = (int)Mathf.Log(numPairs * 2, 2);
                int groups = ComputeMath.ThreadGroupCount(numPairs, 128);

                for (int stageIndex = 0; stageIndex < numStages; stageIndex++)
                {
                    for (int stepIndex = 0; stepIndex < stageIndex + 1; stepIndex++)
                    {
                        int groupWidth = 1 << (stageIndex - stepIndex);
                        int groupHeight = 2 * groupWidth - 1;
                        foamShader.SetInt("groupWidth", groupWidth);
                        foamShader.SetInt("groupHeight", groupHeight);
                        foamShader.SetInt("stepIndex", stepIndex);
                        foamShader.Dispatch(sortKernel, groups, 1, 1);
                    }
                }

                // build mesh:
                int threadGroups = ComputeMath.ThreadGroupCount(m_Solver.foamPositions.count, 128);
                foamShader.SetInt("maxFoamParticles", m_Solver.foamPositions.count);
                foamShader.SetBuffer(clearMeshKernel, "indices", renderBatch.gpuIndexBuffer);
                foamShader.Dispatch(clearMeshKernel, threadGroups, 1, 1);

                foamShader.SetBuffer(buildMeshKernel, "inputPositions", solver.abstraction.foamPositions.computeBuffer);
                foamShader.SetBuffer(buildMeshKernel, "inputVelocities", solver.abstraction.foamVelocities.computeBuffer);
                foamShader.SetBuffer(buildMeshKernel, "inputColors", solver.abstraction.foamColors.computeBuffer);
                foamShader.SetBuffer(buildMeshKernel, "inputAttributes", solver.abstraction.foamAttributes.computeBuffer);

                foamShader.SetBuffer(buildMeshKernel, "vertices", renderBatch.gpuVertexBuffer);
                foamShader.SetBuffer(buildMeshKernel, "indices", renderBatch.gpuIndexBuffer);
                foamShader.SetBuffer(buildMeshKernel, "dispatch", solver.abstraction.foamCount.computeBuffer);

                foamShader.DispatchIndirect(buildMeshKernel, solver.abstraction.foamCount.computeBuffer);

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

    }
}

