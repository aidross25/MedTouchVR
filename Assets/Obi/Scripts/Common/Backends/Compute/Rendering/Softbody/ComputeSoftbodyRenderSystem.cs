using System.Collections.Generic;
using UnityEngine;

namespace Obi
{
    public class ComputeSoftbodyRenderSystem : ObiSoftbodyRenderSystem
    {
        protected override IReadOnlyList<ObiSoftbodySkinner> baseRenderers { get { return renderers.AsReadOnly(); } }

        private ComputeShader softbodyShader;
        private int updateSoftbodyKernel;

        public ComputeSoftbodyRenderSystem(ObiSolver solver) : base(solver)
        {
            softbodyShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/SoftbodyRendering"));
            updateSoftbodyKernel = softbodyShader.FindKernel("UpdateSoftbodyMesh");
        }

        protected override void CloseBatches()
        {
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Initialize(sortedRenderers, meshData, meshIndices, layout, true);

            skinmapData.PrepareForCompute();
            meshData.PrepareForCompute();
            skeletonData.PrepareForCompute();

            skinMapIndices.AsComputeBuffer<int>();
            meshIndices.AsComputeBuffer<int>();
            skeletonIndices.AsComputeBuffer<int>();

            particleOffsets.AsComputeBuffer<int>();
            vertexOffsets.AsComputeBuffer<int>();

            base.CloseBatches();
        }

        public override void Render()
        {
            if (!Application.isPlaying)
                return;

            using (m_RenderMarker.Auto())
            {

                UpdateBoneTransformData();
                skeletonData.UpdateBoneTransformsCompute();

                var computeSolver = m_Solver.implementation as ComputeSolverImpl;

                if (computeSolver.renderablePositionsBuffer != null && computeSolver.renderablePositionsBuffer.count > 0)
                {
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "skinmapIndices", skinMapIndices.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "skeletonIndices", skeletonIndices.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "meshIndices", meshIndices.computeBuffer);

                    softbodyShader.SetBuffer(updateSoftbodyKernel, "particleOffsets", particleOffsets.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "vertexOffsets", vertexOffsets.computeBuffer);

                    softbodyShader.SetBuffer(updateSoftbodyKernel, "skeletonData", skeletonData.skeletonData.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "bonePos", skeletonData.bonePositions.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "boneRot", skeletonData.boneRotations.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "boneScl", skeletonData.boneScales.computeBuffer);

                    softbodyShader.SetBuffer(updateSoftbodyKernel, "skinData", skinmapData.skinData.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "influences", skinmapData.influences.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "influenceOffsets", skinmapData.influenceOffsets.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "bindPoses", skinmapData.particleBindPoses.computeBuffer);

                    softbodyShader.SetBuffer(updateSoftbodyKernel, "meshData", meshData.meshData.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "positions", meshData.restPositions.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "normals", meshData.restNormals.computeBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "tangents", meshData.restTangents.computeBuffer);

                    softbodyShader.SetBuffer(updateSoftbodyKernel, "renderablePositions", computeSolver.renderablePositionsBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "renderableOrientations", computeSolver.renderableOrientationsBuffer);
                    softbodyShader.SetBuffer(updateSoftbodyKernel, "colors", computeSolver.colorsBuffer);

                    softbodyShader.SetMatrix("world2Solver", m_Solver.transform.worldToLocalMatrix);

                    for (int i = 0; i < batchList.Count; ++i)
                    {
                        var batch = batchList[i];
                        int threadGroups = ComputeMath.ThreadGroupCount(batch.vertexCount, 128);

                        softbodyShader.SetInt("vertexCount", batch.vertexCount);

                        softbodyShader.SetBuffer(updateSoftbodyKernel, "rendererIndices", batch.vertexToRenderer.computeBuffer);
                        softbodyShader.SetBuffer(updateSoftbodyKernel, "particleIndices", batch.particleIndices.computeBuffer);

                        softbodyShader.SetBuffer(updateSoftbodyKernel, "vertices", batch.gpuVertexBuffer);

                        softbodyShader.Dispatch(updateSoftbodyKernel, threadGroups, 1, 1);

                        var rp = batch.renderParams;
                        rp.worldBounds = m_Solver.bounds;

                        for (int m = 0; m < batch.materials.Length; ++m)
                        {
                            rp.material = batch.materials[m];
                            Graphics.RenderMesh(rp, batch.mesh, m, m_Solver.transform.localToWorldMatrix, m_Solver.transform.localToWorldMatrix);
                        }
                    }
                }
            }
        }
    }
}


