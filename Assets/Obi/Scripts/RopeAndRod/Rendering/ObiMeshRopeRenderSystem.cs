
using System.Runtime.InteropServices;
using UnityEngine;

using Unity.Profiling;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System;

namespace Obi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BurstMeshData
    {
        public uint axis;
        public float volumeScaling;
        public uint stretchWithRope;
        public uint spanEntireLength;

        public uint instances;
        public float instanceSpacing;
        public float offset;
        public float meshSizeAlongAxis;

        public Vector4 scale;

        public BurstMeshData(ObiRopeMeshRenderer renderer)
        {
            axis = (uint)renderer.axis;
            volumeScaling = renderer.volumeScaling;
            stretchWithRope = (uint)(renderer.stretchWithRope ? 1 : 0);
            spanEntireLength = (uint)(renderer.spanEntireLength ? 1 : 0);
            instances = renderer.instances;
            instanceSpacing = renderer.instanceSpacing;
            offset = renderer.offset;
            meshSizeAlongAxis = renderer.sourceMesh != null ? renderer.sourceMesh.bounds.size[(int)renderer.axis] : 0;
            scale = renderer.scale;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RopeMeshVertex
    {
        public Vector3 pos;
        public Vector3 normal;
        public Vector4 tangent;
        public Vector4 color;
    }

    public abstract class ObiMeshRopeRenderSystem : RenderSystem<ObiRopeMeshRenderer>
    {
        public Oni.RenderingSystemType typeEnum { get => Oni.RenderingSystemType.MeshRope; }

        public RendererSet<ObiRopeMeshRenderer> renderers { get; } = new RendererSet<ObiRopeMeshRenderer>();
        protected List<ObiRopeMeshRenderer> sortedRenderers = new List<ObiRopeMeshRenderer>(); /**< temp list used to store renderers sorted by batch.*/

        static protected ProfilerMarker m_SetupRenderMarker = new ProfilerMarker("SetupMeshRopeRendering");
        static protected ProfilerMarker m_RenderMarker = new ProfilerMarker("MeshRopeRendering");

        // specify vertex count and layout
        protected VertexAttributeDescriptor[] layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3,0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3,0),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4,0),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4,0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2,1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2,1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2,1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 2,1),
        };

        protected ObiSolver m_Solver;
        protected List<DynamicRenderBatch<ObiRopeMeshRenderer>> batchList = new List<DynamicRenderBatch<ObiRopeMeshRenderer>>();


        protected MeshDataBatch meshData;
        protected ObiNativeList<int> meshIndices; // for each renderer, its mesh index.

        protected ObiNativeList<int> pathSmootherIndices;       /**< for each renderer, index of its path smoother in the path smoother system.*/
        protected ObiNativeList<BurstMeshData> rendererData;

        protected ObiNativeList<int> sortedIndices;  /**< axis-sorted vertex indices. */
        protected ObiNativeList<int> sortedOffsets;  /**< for each renderer, offset in the sortedIndices array.*/

        protected ObiNativeList<int> vertexOffsets;  /**< for each renderer, vertex offset in its batch mesh data.*/
        protected ObiNativeList<int> vertexCounts;   /**< for each renderer, vertex count.*/

        protected ObiPathSmootherRenderSystem pathSmootherSystem;

        public ObiMeshRopeRenderSystem(ObiSolver solver)
        {
            m_Solver = solver;

            meshData = new MeshDataBatch();
            meshIndices = new ObiNativeList<int>();

            pathSmootherIndices = new ObiNativeList<int>();
            rendererData = new ObiNativeList<BurstMeshData>();

            sortedIndices = new ObiNativeList<int>();
            sortedOffsets = new ObiNativeList<int>();

            vertexOffsets = new ObiNativeList<int>();
            vertexCounts = new ObiNativeList<int>();
        }

        public void Dispose()
        {
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Dispose();
            batchList.Clear();

            meshData.Dispose();

            if (pathSmootherIndices != null)
                pathSmootherIndices.Dispose();
            if (meshIndices != null)
                meshIndices.Dispose();

            if (sortedIndices != null)
                sortedIndices.Dispose();
            if (sortedOffsets != null)
                sortedOffsets.Dispose();

            if (vertexOffsets != null)
                vertexOffsets.Dispose();
            if (vertexCounts != null)
                vertexCounts.Dispose();

            if (rendererData != null)
                rendererData.Dispose();
        }

        private void Clear()
        {
            meshData.Clear();
            meshIndices.Clear();

            pathSmootherIndices.Clear();
            rendererData.Clear();

            vertexOffsets.Clear();
            vertexCounts.Clear();

            sortedIndices.Clear();
            sortedOffsets.Clear();

            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Dispose();
            batchList.Clear();

            meshData.InitializeStaticData();
            meshData.InitializeTempData();
        }

        private void CreateBatches()
        {
            // generate batches:
            sortedRenderers.Clear();
            for (int i = 0; i < renderers.Count; ++i)
            {
                if (renderers[i].sourceMesh != null && renderers[i].TryGetComponent(out ObiPathSmoother smoother) && smoother.enabled)
                {
                    int vertexCount = renderers[i].vertexCount * (int)renderers[i].meshInstances;
                    renderers[i].renderParameters.layer = renderers[i].gameObject.layer;
                    batchList.Add(new DynamicRenderBatch<ObiRopeMeshRenderer>(i, vertexCount, renderers[i].materials, renderers[i].renderParameters));
                    sortedRenderers.Add(renderers[i]);
                }
            }

            vertexOffsets.ResizeUninitialized(sortedRenderers.Count);

            // sort batches: 
            batchList.Sort();

            // reorder renderers based on sorted batches:
            sortedRenderers.Clear();
            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];

                // store amount of vertices in this batch, prior to merging:
                vertexCounts.Add(batch.vertexCount);

                // write renderers in the order dictated by the sorted batch:
                sortedRenderers.Add(renderers[batch.firstRenderer]);
                batch.firstRenderer = i;

                pathSmootherIndices.Add(sortedRenderers[i].GetComponent<ObiPathSmoother>().indexInSystem);

                rendererData.Add(new BurstMeshData(sortedRenderers[i]));
            }
        }

        protected virtual void PopulateBatches()
        {
            List<Vector3> verts = new List<Vector3>();

            // store per-mesh data 
            for (int i = 0; i < sortedRenderers.Count; ++i)
            {
                // sort vertices along curve axis:
                sortedRenderers[i].GetVertices(verts);
                float[] keys = new float[sortedRenderers[i].vertexCount];
                var orderedVertices = new int[sortedRenderers[i].vertexCount];

                for (int j = 0; j < keys.Length; ++j)
                {
                    keys[j] = verts[j][(int)sortedRenderers[i].axis];
                    orderedVertices[j] = j;
                }

                Array.Sort(keys, orderedVertices);

                sortedOffsets.Add(sortedIndices.count);
                sortedIndices.AddRange(orderedVertices);

                // add mesh index
                meshIndices.Add(meshData.AddMesh(sortedRenderers[i]));
            }
        }

        private void CalculateMeshOffsets()
        {
            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];

                int vtxCount = 0;

                // Calculate vertex and triangle offsets for each renderer in the batch:
                for (int j = 0; j < batch.rendererCount; ++j)
                {
                    int r = batch.firstRenderer + j;

                    vertexOffsets[r] = vtxCount;
                    vtxCount += vertexCounts[r];
                }
            }
        }

        protected virtual void CloseBatches()
        {
            meshData.DisposeOfStaticData();
            meshData.DisposeOfTempData();
        }

        public void  Setup()
        {
            pathSmootherSystem = m_Solver.GetRenderSystem<ObiPathSmoother>() as ObiPathSmootherRenderSystem;
            if (pathSmootherSystem == null)
                return;

            using (m_SetupRenderMarker.Auto())
            {
                Clear();

                CreateBatches();

                PopulateBatches();

                ObiUtils.MergeBatches(batchList);

                CalculateMeshOffsets();

                CloseBatches();
            }
        }

        public void Step()
        {
        }

        public virtual void Render()
        {
        }

        public void BakeMesh(ObiRopeMeshRenderer renderer, ref Mesh mesh, bool transformToActorLocalSpace = false)
        {
            int index = sortedRenderers.IndexOf(renderer);

            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];
                if (index >= batch.firstRenderer && index < batch.firstRenderer + batch.rendererCount)
                {
                    batch.BakeMesh(sortedRenderers, renderer, ref mesh, transformToActorLocalSpace);
                    return;
                }
            }
        }
    }
}


