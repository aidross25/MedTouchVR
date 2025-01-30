using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
using System.Collections.Generic;

namespace Obi
{
    public abstract class ObiSoftbodyRenderSystem : RenderSystem<ObiSoftbodySkinner>
    {
        public Oni.RenderingSystemType typeEnum { get => Oni.RenderingSystemType.Softbody; }

        public RendererSet<ObiSoftbodySkinner> renderers { get; } = new RendererSet<ObiSoftbodySkinner>();
        protected abstract IReadOnlyList<ObiSoftbodySkinner> baseRenderers { get; }

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

        static protected ProfilerMarker m_SetupRenderMarker = new ProfilerMarker("SetupSoftbodyRendering");
        static protected ProfilerMarker m_RenderMarker = new ProfilerMarker("SoftbodyRendering");

        protected ObiSolver m_Solver;

        protected List<DynamicRenderBatch<ObiSoftbodySkinner>> batchList = new List<DynamicRenderBatch<ObiSoftbodySkinner>>();
        protected List<ObiSoftbodySkinner> sortedRenderers = new List<ObiSoftbodySkinner>(); /**< temp list used to store renderers sorted by batch.*/

        protected SkeletonDataBatch skeletonData;
        protected SkinmapDataBatch skinmapData;
        protected MeshDataBatch meshData;

        protected ObiNativeList<int> skeletonIndices; // for each renderer, its skeleton index.
        protected ObiNativeList<int> skinMapIndices; // for each renderer, its skinmap index.
        protected ObiNativeList<int> meshIndices; // for each renderer, its mesh index.

        protected ObiNativeList<int> vertexOffsets;   /**< for each renderer, vertex offset in its batch mesh data.*/
        protected ObiNativeList<int> particleOffsets; /**< for each renderer, particle offset in its batch data.*/

        public ObiSoftbodyRenderSystem(ObiSolver solver)
        {
            m_Solver = solver;

            skeletonData = new SkeletonDataBatch();
            skinmapData = new SkinmapDataBatch();
            meshData = new MeshDataBatch();

            skeletonIndices = new ObiNativeList<int>();
            skinMapIndices = new ObiNativeList<int>();
            meshIndices = new ObiNativeList<int>();

            vertexOffsets = new ObiNativeList<int>();
            particleOffsets = new ObiNativeList<int>();
        }

        public virtual void Dispose()
        {
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Dispose();
            batchList.Clear();

            skeletonData.Dispose();
            skinmapData.Dispose();
            meshData.Dispose();

            if (skeletonIndices != null)
                skeletonIndices.Dispose();
            if (skinMapIndices != null)
                skinMapIndices.Dispose();
            if (meshIndices != null)
                meshIndices.Dispose();

            if (vertexOffsets != null)
                vertexOffsets.Dispose();
            if (particleOffsets != null)
                particleOffsets.Dispose();
        }

        protected virtual void Clear()
        {
            skeletonData.Clear();
            skinmapData.Clear();
            meshData.Clear();

            skeletonIndices.Clear();
            skinMapIndices.Clear();
            meshIndices.Clear();
            vertexOffsets.Clear();
            particleOffsets.Clear();

            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Dispose();
            batchList.Clear();

            meshData.InitializeStaticData();
            meshData.InitializeTempData();
        }

        protected virtual void CreateBatches()
        {
            // generate one batch per renderer:
            sortedRenderers.Clear();
            for (int i = 0; i < baseRenderers.Count; ++i)
            {
                int vertexCount = baseRenderers[i].vertexCount * (int)baseRenderers[i].meshInstances;
                batchList.Add(new DynamicRenderBatch<ObiSoftbodySkinner>(i, vertexCount, baseRenderers[i].materials, new RenderBatchParams(baseRenderers[i].sourceRenderer)));
                sortedRenderers.Add(baseRenderers[i] as ObiSoftbodySkinner);
            }

            // sort batches:
            batchList.Sort();

            // reorder renderers based on sorted batches:
            sortedRenderers.Clear();
            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];

                // write renderers in the order dictated by the sorted batch:
                sortedRenderers.Add(baseRenderers[batch.firstRenderer]);
                batch.firstRenderer = i;
            }
        }

        protected virtual void PopulateBatches()
        {
            // store per-mesh data 
            for (int i = 0; i < sortedRenderers.Count; ++i)
            {
                // add skinmap index TODO: need to validate skinmap.
                skinMapIndices.Add(skinmapData.AddSkinmap(sortedRenderers[i].skinMap, sortedRenderers[i].sourceMesh.bindposes));

                // add mesh index
                meshIndices.Add(meshData.AddMesh(sortedRenderers[i]));

                // add skeleton index:
                var skRenderer = sortedRenderers[i].GetComponent<SkinnedMeshRenderer>();
                skeletonIndices.Add(skeletonData.AddSkeleton(skRenderer.bones,
                                                             sortedRenderers[i].actor.solver.transform.worldToLocalMatrix));
            }
        }

        protected void CalculateOffsets()
        {
            vertexOffsets.ResizeUninitialized(sortedRenderers.Count);
            particleOffsets.ResizeInitialized(sortedRenderers.Count);

            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];

                int vtxCount = 0;
                int ptCount = 0;

                // Calculate vertex and triangle offsets for each renderer in the batch:
                for (int j = batch.firstRenderer; j < batch.firstRenderer + batch.rendererCount; ++j)
                {
                    vertexOffsets[j] = vtxCount;
                    particleOffsets[j] = ptCount;

                    vtxCount += meshData.GetVertexCount(meshIndices[j]);
                    ptCount += sortedRenderers[j].actor.particleCount;
                }
            }
        }

        protected virtual void CloseBatches()
        {
            meshData.DisposeOfStaticData();
            meshData.DisposeOfTempData();
        }

        protected void UpdateBoneTransformData()
        {
            // iterate over all renderers, copying bone transform data to bone arrays:
            int k = 0;
            for (int i = 0; i < sortedRenderers.Count; ++i)
            {
                var renderer = sortedRenderers[i] as ObiSoftbodySkinner;
                skeletonData.SetWorldToSolverTransform(k, renderer.actor.solver.transform.worldToLocalMatrix);

                var bones = renderer.GetComponent<SkinnedMeshRenderer>().bones;
                for (int j = 0; j < bones.Length; ++j)
                {
                    skeletonData.SetBoneTransform(k, j, bones[j]);
                }
                k++;
            }
        }

        public virtual void  Setup()
        {
            using (m_SetupRenderMarker.Auto())
            {
                Clear();

                CreateBatches();

                PopulateBatches();

                ObiUtils.MergeBatches(batchList);

                CalculateOffsets();

                CloseBatches();
            }
        }

        public abstract void Render();

        public void Step()
        {
        }

        public void BakeMesh(ObiSoftbodySkinner renderer, ref Mesh mesh, bool transformToActorLocalSpace = false)
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

