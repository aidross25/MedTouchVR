using UnityEngine;
using UnityEngine.Rendering;

using Unity.Profiling;
using System.Collections.Generic;
using Unity.Collections;

namespace Obi
{
    public class ComputeInstancedParticleRenderSystem : ObiInstancedParticleRenderSystem
    {

        private ComputeShader instanceShader;
        private int updateKernel;
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        public ComputeInstancedParticleRenderSystem(ObiSolver solver) : base(solver)
        {
            instanceShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/InstancedParticleRendering"));
            updateKernel = instanceShader.FindKernel("UpdateParticleInstances");
        }

        protected override void CloseBatches()
        {
            // Initialize each batch:
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Initialize(true);
        }

        public override void Setup()
        {
            base.Setup();

            activeParticles.AsComputeBuffer<int>();
            rendererData.AsComputeBuffer<ParticleRendererData>();
            rendererIndex.AsComputeBuffer<int>();

            instanceTransforms.AsComputeBuffer<Matrix4x4>();
            invInstanceTransforms.AsComputeBuffer<Matrix4x4>();
            instanceColors.AsComputeBuffer<Vector4>();
        }

        public override void Render()
        {
            using (m_RenderMarker.Auto())
            {

                var computeSolver = m_Solver.implementation as ComputeSolverImpl;

                if (computeSolver.renderablePositionsBuffer != null && computeSolver.renderablePositionsBuffer.count > 0 && activeParticles.count > 0)
                {
                    instanceShader.SetBuffer(updateKernel, "activeParticles", activeParticles.computeBuffer);
                    instanceShader.SetBuffer(updateKernel, "rendererData", rendererData.computeBuffer);
                    instanceShader.SetBuffer(updateKernel, "rendererIndex", rendererIndex.computeBuffer);

                    instanceShader.SetBuffer(updateKernel, "renderablePositions", computeSolver.renderablePositionsBuffer);
                    instanceShader.SetBuffer(updateKernel, "renderableOrientations", computeSolver.renderableOrientationsBuffer);
                    instanceShader.SetBuffer(updateKernel, "renderableRadii", computeSolver.renderableRadiiBuffer);
                    instanceShader.SetBuffer(updateKernel, "colors", computeSolver.colorsBuffer);

                    instanceShader.SetBuffer(updateKernel, "instanceTransforms", instanceTransforms.computeBuffer);
                    instanceShader.SetBuffer(updateKernel, "invInstanceTransforms", invInstanceTransforms.computeBuffer);
                    instanceShader.SetBuffer(updateKernel, "instanceColors", instanceColors.computeBuffer);

                    instanceShader.SetMatrix("solverToWorld", m_Solver.transform.localToWorldMatrix);

                    instanceShader.SetInt("particleCount", activeParticles.count);
                    int threadGroups = ComputeMath.ThreadGroupCount(activeParticles.count, 128);

                    instanceShader.Dispatch(updateKernel, threadGroups, 1, 1);

                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    mpb.SetBuffer("_InstanceTransforms", instanceTransforms.computeBuffer);
                    mpb.SetBuffer("_InvInstanceTransforms", invInstanceTransforms.computeBuffer);
                    mpb.SetBuffer("_Colors", instanceColors.computeBuffer);

                    for (int i = 0; i < batchList.Count; ++i)
                    {
                        var batch = batchList[i];

                        if (batch.mesh == null)
                            continue;

                        args[0] = (uint)batch.mesh.GetIndexCount(0);
                        args[1] = (uint)batch.instanceCount;
                        args[2] = (uint)batch.mesh.GetIndexStart(0);
                        args[3] = (uint)batch.mesh.GetBaseVertex(0);
                        args[4] = (uint)batch.firstInstance;
                        batch.argsBuffer.SetData(args);

                        var rp = batch.renderParams;
                        rp.material = batch.material;
                        rp.worldBounds = m_Solver.bounds;
                        rp.matProps = mpb;

                        Graphics.RenderMeshIndirect(rp, batch.mesh, batch.argsBuffer);
                    }
                }
            }
        }

    }
}

