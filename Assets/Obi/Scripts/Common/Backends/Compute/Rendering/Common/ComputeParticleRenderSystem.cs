using UnityEngine;

namespace Obi
{

    public class ComputeParticleRenderSystem : ObiParticleRenderSystem
    {
        public ComputeShader meshComputeShader;
        private int buildMeshKernel;

        public ComputeParticleRenderSystem(ObiSolver solver) : base(solver)
        {
            meshComputeShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/ParticleMeshBuilding"));
            buildMeshKernel = meshComputeShader.FindKernel("BuildMesh");
        }

        protected override void CloseBatches()
        {
            // Initialize each batch:
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Initialize(layout, true);

            activeParticles.AsComputeBuffer<int>();
            rendererIndex.AsComputeBuffer<int>();
            rendererData.AsComputeBuffer<ParticleRendererData>();
        }

        public override void Render()
        {
            using (m_RenderMarker.Auto())
            {
                var solver = m_Solver.implementation as ComputeSolverImpl;

                if (solver.renderablePositionsBuffer != null &&
                    activeParticles.computeBuffer != null &&
                    solver.renderablePositionsBuffer.count > 0)
                {
                    meshComputeShader.SetBuffer(buildMeshKernel, "particleIndices", activeParticles.computeBuffer);

                    meshComputeShader.SetBuffer(buildMeshKernel, "positions", solver.renderablePositionsBuffer);
                    meshComputeShader.SetBuffer(buildMeshKernel, "orientations", solver.renderableOrientationsBuffer);
                    meshComputeShader.SetBuffer(buildMeshKernel, "radii", solver.renderableRadiiBuffer);
                    meshComputeShader.SetBuffer(buildMeshKernel, "colors", solver.colorsBuffer);

                    meshComputeShader.SetBuffer(buildMeshKernel, "rendererIndices", rendererIndex.computeBuffer);
                    meshComputeShader.SetBuffer(buildMeshKernel, "rendererData", rendererData.computeBuffer);

                    for (int i = 0; i < batchList.Count; ++i)
                    {
                        var batch = batchList[i];
                        int threadGroups = ComputeMath.ThreadGroupCount(batch.vertexCount / 4, 128);

                        meshComputeShader.SetInt("firstParticle", batch.firstParticle);
                        meshComputeShader.SetInt("particleCount", batch.vertexCount / 4);

                        meshComputeShader.SetBuffer(buildMeshKernel, "vertices", batch.gpuVertexBuffer);
                        meshComputeShader.SetBuffer(buildMeshKernel, "indices", batch.gpuIndexBuffer);

                        meshComputeShader.Dispatch(buildMeshKernel, threadGroups, 1, 1);

                        var rp = batch.renderParams;
                        rp.worldBounds = m_Solver.bounds;

                        Graphics.RenderMesh(rp, batch.mesh, 0, m_Solver.transform.localToWorldMatrix, m_Solver.transform.localToWorldMatrix);
                    }
                }
            }
        }
    }
}

