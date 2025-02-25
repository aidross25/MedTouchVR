
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace Obi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BurstLineMeshData
    {
        public Vector2 uvScale;
        public float thicknessScale;
        public float uvAnchor;
        public uint normalizeV;

        public BurstLineMeshData(ObiRopeLineRenderer renderer)
        {
            uvAnchor = renderer.uvAnchor;
            thicknessScale = renderer.thicknessScale;
            uvScale = renderer.uvScale;
            normalizeV = (uint)(renderer.normalizeV ? 1 : 0);
        }
    }

    public abstract class ObiLineRopeRenderSystem : RenderSystem<ObiRopeLineRenderer>
    {
        public Oni.RenderingSystemType typeEnum { get => Oni.RenderingSystemType.LineRope; }

        public RendererSet<ObiRopeLineRenderer> renderers { get; } = new RendererSet<ObiRopeLineRenderer>();
        protected List<ObiRopeLineRenderer> sortedRenderers = new List<ObiRopeLineRenderer>();

        // specify vertex count and layout
        protected VertexAttributeDescriptor[] layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        };

        static protected ProfilerMarker m_SetupRenderMarker = new ProfilerMarker("SetupExtrudedRopeRendering");
        static protected ProfilerMarker m_RenderMarker = new ProfilerMarker("ExtrudedRopeRendering");

        protected ObiSolver m_Solver;
        protected SubMeshDescriptor subMeshDescriptor = new SubMeshDescriptor(0, 0);

        protected List<ProceduralRenderBatch<ProceduralRopeVertex>> batchList = new List<ProceduralRenderBatch<ProceduralRopeVertex>>();

        protected ObiNativeList<int> pathSmootherIndices;
        protected ObiNativeList<BurstLineMeshData> rendererData; /**< for each renderer, data about smoother.*/

        protected ObiNativeList<int> vertexOffsets;   /**< for each renderer, vertex offset in its batch mesh data.*/
        protected ObiNativeList<int> triangleOffsets; /**< for each renderer, triangle offset in its batch mesh data.*/

        protected ObiNativeList<int> vertexCounts;    /**< for each renderer, vertex count.*/
        protected ObiNativeList<int> triangleCounts;  /**< for each renderer, triangle count.*/

        protected ObiPathSmootherRenderSystem pathSmootherSystem;

#if (UNITY_2019_1_OR_NEWER)
        System.Action<ScriptableRenderContext, Camera> renderCallback;
#endif

        public ObiLineRopeRenderSystem(ObiSolver solver)
        {
#if (UNITY_2019_1_OR_NEWER)
            renderCallback = new System.Action<ScriptableRenderContext, Camera>((cntxt, cam) => { RenderFromCamera(cam); });
            RenderPipelineManager.beginCameraRendering += renderCallback;
#endif
            Camera.onPreCull += RenderFromCamera;

            m_Solver = solver;

            pathSmootherIndices = new ObiNativeList<int>();
            rendererData = new ObiNativeList<BurstLineMeshData>();

            vertexOffsets = new ObiNativeList<int>();
            triangleOffsets = new ObiNativeList<int>();

            vertexCounts = new ObiNativeList<int>();
            triangleCounts = new ObiNativeList<int>();
        }

        public void Dispose()
        {
#if (UNITY_2019_1_OR_NEWER)
            RenderPipelineManager.beginCameraRendering -= renderCallback;
#endif
            Camera.onPreCull -= RenderFromCamera;

            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Dispose();
            batchList.Clear();

            if (pathSmootherIndices != null)
                pathSmootherIndices.Dispose();
            if (rendererData != null)
                rendererData.Dispose();

            if (vertexOffsets != null)
                vertexOffsets.Dispose();
            if (triangleOffsets != null)
                triangleOffsets.Dispose();

            if (vertexCounts != null)
                vertexCounts.Dispose();
            if (triangleCounts != null)
                triangleCounts.Dispose();
        }

        private void Clear()
        {
            pathSmootherIndices.Clear();
            rendererData.Clear();

            vertexOffsets.Clear();
            vertexCounts.Clear();

            triangleCounts.Clear();

            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Dispose();
            batchList.Clear();
        }

        private void CreateBatches()
        {
            // generate batches:
            sortedRenderers.Clear();
            for (int i = 0; i < renderers.Count; ++i)
            {
                if (renderers[i].TryGetComponent(out ObiPathSmoother smoother) && smoother.enabled)
                {
                    renderers[i].renderParams.layer = renderers[i].gameObject.layer;
                    batchList.Add(new ProceduralRenderBatch<ProceduralRopeVertex>(i, renderers[i].material, renderers[i].renderParams));
                    sortedRenderers.Add(renderers[i]);
                }
            }

            vertexOffsets.ResizeUninitialized(sortedRenderers.Count);
            triangleOffsets.ResizeUninitialized(sortedRenderers.Count);

            // sort batches:
            batchList.Sort();

            // reorder renderers based on sorted batches:
            sortedRenderers.Clear();
            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];

                sortedRenderers.Add(renderers[batch.firstRenderer]);
                batch.firstRenderer = i;

                int pathIndex = sortedRenderers[i].GetComponent<ObiPathSmoother>().indexInSystem;
                pathSmootherIndices.Add(pathIndex);

                // calculate vertex and triangle counts for each renderer:
                int chunkStart = pathSmootherSystem.chunkOffsets[pathIndex];
                int chunkAmount = pathSmootherSystem.chunkOffsets[pathIndex + 1] - chunkStart;

                for (int k = chunkStart; k < chunkStart + chunkAmount; ++k)
                {
                    int frameCount = pathSmootherSystem.smoothFrameCounts[k];
                    batch.vertexCount += frameCount * 2; // in a triangle strip, there's 2 vertices per frame.
                    batch.triangleCount += (frameCount - 1) * 2; // and 2 triangles per frame (except for the last one)
                }

                vertexCounts.Add(batch.vertexCount);
                triangleCounts.Add(batch.triangleCount);

                rendererData.Add(new BurstLineMeshData(sortedRenderers[i]));
            }
        }

        private void CalculateMeshOffsets()
        {
            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];

                int vtxCount = 0;
                int triCount = 0;

                // Calculate vertex and triangle offsets for each renderer in the batch:
                for (int j = 0; j < batch.rendererCount; ++j)
                {
                    int r = batch.firstRenderer + j;

                    vertexOffsets[r] = vtxCount;
                    triangleOffsets[r] = triCount;

                    vtxCount += vertexCounts[r];
                    triCount += triangleCounts[r];
                }
            }
        }

        public virtual void Setup()
        {
            pathSmootherSystem = m_Solver.GetRenderSystem<ObiPathSmoother>() as ObiPathSmootherRenderSystem;
            if (pathSmootherSystem == null)
                return;

            using (m_SetupRenderMarker.Auto())
            {
                Clear();

                CreateBatches();

                ObiUtils.MergeBatches(batchList);

                CalculateMeshOffsets();
            }
        }

        public abstract void RenderFromCamera(Camera camera);

        public abstract void Render();

        public void Step()
        {
        }
    }
}


