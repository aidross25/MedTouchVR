using UnityEngine;
using UnityEngine.Rendering;

using Unity.Profiling;
using System.Collections.Generic;
using Unity.Collections;

namespace Obi
{
    public class ComputeChainRopeRenderSystem : ObiChainRopeRenderSystem
    {

        private ComputeShader ropeShader;
        private int updateKernel;
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        public ComputeChainRopeRenderSystem(ObiSolver solver) : base(solver)
        {
            ropeShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/RopeChainRendering"));
            updateKernel = ropeShader.FindKernel("UpdateChainMesh");
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

            rendererData.AsComputeBuffer<ChainRendererData>();
            chunkData.AsComputeBuffer<ChunkData>();
            modifiers.SafeAsComputeBuffer<ObiRopeChainRenderer.LinkModifier>();
            elements.AsComputeBuffer<Vector2Int>();

            instanceTransforms.AsComputeBuffer<Matrix4x4>();
            invInstanceTransforms.AsComputeBuffer<Matrix4x4>();
            instanceColors.AsComputeBuffer<Vector4>();

        }

        public override void Render()
        {
            using (m_RenderMarker.Auto())
            {

                var computeSolver = m_Solver.implementation as ComputeSolverImpl;

                if (computeSolver.renderablePositionsBuffer != null && computeSolver.renderablePositionsBuffer.count > 0 && elements.count > 0)
                {
                    ropeShader.SetBuffer(updateKernel, "rendererData", rendererData.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "chunksData", chunkData.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "modifiers", modifiers.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "elements", elements.computeBuffer);

                    ropeShader.SetBuffer(updateKernel, "renderablePositions", computeSolver.renderablePositionsBuffer);
                    ropeShader.SetBuffer(updateKernel, "renderableOrientations", computeSolver.renderableOrientationsBuffer);
                    ropeShader.SetBuffer(updateKernel, "principalRadii", computeSolver.renderableRadiiBuffer);
                    ropeShader.SetBuffer(updateKernel, "colors", computeSolver.colorsBuffer);

                    ropeShader.SetBuffer(updateKernel, "instanceTransforms", instanceTransforms.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "invInstanceTransforms", invInstanceTransforms.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "instanceColors", instanceColors.computeBuffer);

                    ropeShader.SetMatrix("solverToWorld", m_Solver.transform.localToWorldMatrix);

                    ropeShader.SetInt("chunkCount", chunkData.count);
                    int threadGroups = ComputeMath.ThreadGroupCount(chunkData.count, 32);

                    ropeShader.Dispatch(updateKernel, threadGroups, 1, 1);

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

