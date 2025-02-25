using UnityEngine;
using UnityEngine.Rendering;

namespace Obi
{
    public class ComputeExtrudedRopeRenderSystem : ObiExtrudedRopeRenderSystem
    {

        private ComputeShader ropeShader;
        private int updateKernel;

        public ComputeExtrudedRopeRenderSystem(ObiSolver solver) : base(solver)
        {
            ropeShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/RopeExtrudedRendering"));
            updateKernel = ropeShader.FindKernel("UpdateRopeMesh");
        }


        public override void Setup()
        {
            base.Setup();

            // Initialize each batch:
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Initialize(layout, true);

            sectionData.AsComputeBuffer<Vector2>();
            sectionOffsets.AsComputeBuffer<int>();
            sectionIndices.AsComputeBuffer<int>();

            vertexOffsets.AsComputeBuffer<int>();
            triangleOffsets.AsComputeBuffer<int>();
            triangleCounts.AsComputeBuffer<int>();

            pathSmootherIndices.AsComputeBuffer<int>();
            rendererData.AsComputeBuffer<BurstExtrudedMeshData>();

            pathSmootherSystem.chunkOffsets.AsComputeBuffer<int>();
        }

        public override void Render()
        {
            using (m_RenderMarker.Auto())
            {
                if (pathSmootherSystem == null)
                    return;

                // Single array: Cannot merge into a single vertices array, otherwise we would need to bring back to CPU for passing indices to each mesh.
                // Individual meshes: Cannot do each renderer independently (like we do with cloth) since each rope is done sequentially, would not parallelize at all.
                // Batches: 1 mesh per batch: best approach, but 1) bounds must be calculated per or solver, so we can only cull entire solver. Culling happens on the CPU, cannot bring back bounds from the CPU.
                // Cloth and Softbodies are rendered manually, particles are too. So Ropes could too.

                // In Burst, we need  merge all cloth mesh data into array for parallel processing, without using one schedule() per mesh.
                // So instead of writing slices of mesh data back to their original meshes, let's create one mesh per batch and draw it ourselves.
                // Basically the same as with ropes.

                if (pathSmootherSystem.chunkOffsets != null && pathSmootherSystem.chunkOffsets.count > 0)
                {
                    ropeShader.SetBuffer(updateKernel, "pathSmootherIndices", pathSmootherIndices.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "chunkOffsets", pathSmootherSystem.chunkOffsets.computeBuffer);

                    ropeShader.SetBuffer(updateKernel, "frames", pathSmootherSystem.smoothFrames.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "frameOffsets", pathSmootherSystem.smoothFrameOffsets.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "frameCounts", pathSmootherSystem.smoothFrameCounts.computeBuffer);

                    ropeShader.SetBuffer(updateKernel, "sectionData", sectionData.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "sectionOffsets", sectionOffsets.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "sectionIndices", sectionIndices.computeBuffer);

                    ropeShader.SetBuffer(updateKernel, "vertexOffsets", vertexOffsets.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "triangleOffsets", triangleOffsets.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "triangleCounts", triangleCounts.computeBuffer);

                    ropeShader.SetBuffer(updateKernel, "extrudedData", rendererData.computeBuffer);
                    ropeShader.SetBuffer(updateKernel, "pathData", pathSmootherSystem.pathData.computeBuffer);

                    for (int i = 0; i < batchList.Count; ++i)
                    {
                        var batch = batchList[i];
                        int threadGroups = ComputeMath.ThreadGroupCount(batch.rendererCount, 128);

                        ropeShader.SetInt("firstRenderer", batch.firstRenderer);
                        ropeShader.SetInt("rendererCount", batch.rendererCount);

                        ropeShader.SetBuffer(updateKernel, "vertices", batch.gpuVertexBuffer);
                        ropeShader.SetBuffer(updateKernel, "tris", batch.gpuIndexBuffer);

                        ropeShader.Dispatch(updateKernel, threadGroups, 1, 1);

                        var rp = batch.renderParams;
                        rp.worldBounds = m_Solver.bounds;

                        Graphics.RenderMesh(rp, batch.mesh, 0, m_Solver.transform.localToWorldMatrix, m_Solver.transform.localToWorldMatrix);
                    }
                }
            }
        }

    }
}

