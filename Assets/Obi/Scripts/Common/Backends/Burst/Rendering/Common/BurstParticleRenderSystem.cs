#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;
using UnityEngine.Rendering;

using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    public class BurstParticleRenderSystem : ObiParticleRenderSystem
    {
        public BurstParticleRenderSystem(ObiSolver solver) : base(solver)
        {
            m_Solver = solver;
        }

        public override void Render()
        {
            using (m_RenderMarker.Auto())
            {
                for (int i = 0; i < batchList.Count; ++i)
                {
                    var batch = batchList[i];

                    var buildArraysJob = new BuildParticleMeshDataJob
                    {
                        particleIndices = activeParticles.AsNativeArray<int>(),
                        rendererIndices = rendererIndex.AsNativeArray<int>(),
                        rendererData = rendererData.AsNativeArray<ParticleRendererData>(),

                        renderablePositions = m_Solver.renderablePositions.AsNativeArray<float4>(),
                        renderableOrientations = m_Solver.renderableOrientations.AsNativeArray<quaternion>(),
                        renderableRadii = m_Solver.renderableRadii.AsNativeArray<float4>(),
                        colors = m_Solver.colors.AsNativeArray<float4>(),

                        vertices = batch.vertices,
                        indices = batch.triangles,

                        firstParticle = batch.firstParticle,
                    };

                    buildArraysJob.Schedule(batch.vertexCount / 4, 32).Complete();

                    batch.mesh.SetVertexBufferData(batch.vertices, 0, 0, batch.vertexCount, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontNotifyMeshUsers);
                    batch.mesh.SetIndexBufferData(batch.triangles, 0, 0, batch.triangleCount * 3, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                    var rp = batch.renderParams;
                    rp.worldBounds = m_Solver.bounds;

                    Graphics.RenderMesh(rp, batch.mesh, 0, m_Solver.transform.localToWorldMatrix, m_Solver.transform.localToWorldMatrix);
                }

            }
        }
    }
}
#endif

