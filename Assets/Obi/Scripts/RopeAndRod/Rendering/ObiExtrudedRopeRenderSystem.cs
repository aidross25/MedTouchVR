
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace Obi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BurstExtrudedMeshData
    {
        public int sectionVertexCount;
        public float thicknessScale;
        public float uvAnchor;
        public uint normalizeV;

        public Vector2 uvScale;

        public BurstExtrudedMeshData(ObiRopeExtrudedRenderer renderer)
        {
            sectionVertexCount = renderer.section.vertices.Count;
            uvAnchor = renderer.uvAnchor;
            thicknessScale = renderer.thicknessScale;
            uvScale = renderer.uvScale;
            normalizeV = (uint)(renderer.normalizeV ? 1 : 0);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ProceduralRopeVertex
    {
        public Vector3 pos;
        public Vector3 normal;
        public Vector4 tangent;
        public Vector4 color;
        public Vector2 uv;
    }

    public abstract class ObiExtrudedRopeRenderSystem : RenderSystem<ObiRopeExtrudedRenderer>
    {
        public Oni.RenderingSystemType typeEnum { get => Oni.RenderingSystemType.ExtrudedRope; }

        public RendererSet<ObiRopeExtrudedRenderer> renderers { get; } = new RendererSet<ObiRopeExtrudedRenderer>();
        protected List<ObiRopeExtrudedRenderer> sortedRenderers = new List<ObiRopeExtrudedRenderer>(); /**< temp list used to store renderers sorted by batch.*/

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

        protected ObiNativeList<BurstExtrudedMeshData> rendererData; /**< for each renderer, data about smoother.*/
        protected ObiNativeList<int> pathSmootherIndices; /**< renderer indices, sorted by batch */

        protected Dictionary<ObiRopeSection, int> sectionToIndex = new Dictionary<ObiRopeSection, int>();
        protected ObiNativeVector2List sectionData;
        protected ObiNativeList<int> sectionOffsets;  /**< for each section, offset of its first entry in the sectionData array.*/
        protected ObiNativeList<int> sectionIndices;  /**< for each renderer, index of the section used.*/

        protected ObiNativeList<int> vertexOffsets;   /**< for each renderer, vertex offset in its batch mesh data.*/
        protected ObiNativeList<int> triangleOffsets; /**< for each renderer, triangle offset in its batch mesh data.*/

        protected ObiNativeList<int> vertexCounts;    /**< for each renderer, vertex count.*/
        protected ObiNativeList<int> triangleCounts;  /**< for each renderer, triangle count.*/

        protected ObiPathSmootherRenderSystem pathSmootherSystem;

        public ObiExtrudedRopeRenderSystem(ObiSolver solver)
        {
            m_Solver = solver;

            rendererData = new ObiNativeList<BurstExtrudedMeshData>();
            pathSmootherIndices = new ObiNativeList<int>();

            sectionData = new ObiNativeVector2List();
            sectionOffsets = new ObiNativeList<int>();
            sectionIndices = new ObiNativeList<int>();

            vertexOffsets = new ObiNativeList<int>();
            triangleOffsets = new ObiNativeList<int>();

            vertexCounts = new ObiNativeList<int>();
            triangleCounts = new ObiNativeList<int>();
        }

        public void Dispose()
        {
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Dispose();
            batchList.Clear();

            if (rendererData != null)
                rendererData.Dispose();
            if (pathSmootherIndices != null)
                pathSmootherIndices.Dispose();

            if (sectionData != null)
                sectionData.Dispose();
            if (sectionOffsets != null)
                sectionOffsets.Dispose();
            if (sectionIndices != null)
                sectionIndices.Dispose();

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
            rendererData.Clear();
            pathSmootherIndices.Clear();

            sectionData.Clear();
            sectionToIndex.Clear();
            sectionOffsets.Clear();
            sectionIndices.Clear();

            vertexOffsets.Clear();
            triangleOffsets.Clear();

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
                    renderers[i].renderParameters.layer = renderers[i].gameObject.layer;
                    batchList.Add(new ProceduralRenderBatch<ProceduralRopeVertex>(i, renderers[i].material, renderers[i].renderParameters));
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

                // get or create extruded section:
                if (!sectionToIndex.TryGetValue(sortedRenderers[i].section, out int sectionIndex))
                {
                    sectionIndex = sectionOffsets.count;
                    sectionToIndex[sortedRenderers[i].section] = sectionIndex;
                    sectionOffsets.Add(sectionData.count);
                    sectionData.AddRange(sortedRenderers[i].section.vertices);
                }

                sectionIndices.Add(sectionIndex);

                // calculate vertex and triangle counts for each renderer:
                int chunkStart = pathSmootherSystem.chunkOffsets[pathIndex];
                int chunkAmount = pathSmootherSystem.chunkOffsets[pathIndex + 1] - chunkStart;

                for (int k = chunkStart; k < chunkStart + chunkAmount; ++k)
                {
                    int frameCount = pathSmootherSystem.smoothFrameCounts[k];
                    batch.vertexCount += frameCount * sortedRenderers[i].section.vertices.Count;
                    batch.triangleCount += (frameCount - 1) * (sortedRenderers[i].section.vertices.Count - 1) * 2;
                }

                vertexCounts.Add(batch.vertexCount);
                triangleCounts.Add(batch.triangleCount);

                rendererData.Add(new BurstExtrudedMeshData(sortedRenderers[i]));
            }

            // add last entry to section offsets:
            sectionOffsets.Add(sectionData.count);

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

        public abstract void Render();

        public void Step()
        {
        }

        public void BakeMesh(ObiRopeExtrudedRenderer renderer, ref Mesh mesh, bool transformToActorLocalSpace = false)
        {
            int index = sortedRenderers.IndexOf(renderer);

            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];
                if (index >= batch.firstRenderer && index < batch.firstRenderer + batch.rendererCount)
                {
                    batch.BakeMesh(vertexOffsets[index], vertexCounts[index], triangleOffsets[index], triangleCounts[index],
                                   renderer.actor.actorSolverToLocalMatrix, ref mesh, transformToActorLocalSpace);
                    return;
                }
            }
        }
    }
}


