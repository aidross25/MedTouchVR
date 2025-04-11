
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace Obi
{

    [StructLayout(LayoutKind.Sequential)]
    public struct ChainRendererData
    {
        public int modifierOffset;
        public float twistAnchor;
        public float twist;
        public uint usesOrientedParticles;

        public Vector4 scale;

        public ChainRendererData(int modifierOffset, float twistAnchor, float twist, Vector3 scale, bool usesOrientedParticles)
        {
            this.modifierOffset = modifierOffset;
            this.twistAnchor = twistAnchor;
            this.twist = twist;
            this.usesOrientedParticles = (uint)(usesOrientedParticles ? 1 : 0);
            this.scale = scale;
        }
    }

    public abstract class ObiChainRopeRenderSystem : RenderSystem<ObiRopeChainRenderer>
    {
        public Oni.RenderingSystemType typeEnum { get => Oni.RenderingSystemType.ChainRope; }

        public RendererSet<ObiRopeChainRenderer> renderers { get; } = new RendererSet<ObiRopeChainRenderer>();

        // specify vertex count and layout
        protected VertexAttributeDescriptor[] layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        };

        static protected ProfilerMarker m_SetupRenderMarker = new ProfilerMarker("SetupChainRopeRendering");
        static protected ProfilerMarker m_RenderMarker = new ProfilerMarker("ChainRopeRendering");

        protected ObiSolver m_Solver;
        protected List<InstancedRenderBatch> batchList = new List<InstancedRenderBatch>();

        protected ObiNativeList<ChainRendererData> rendererData;
        protected ObiNativeList<ChunkData> chunkData;
        protected ObiNativeList<ObiRopeChainRenderer.LinkModifier> modifiers;

        protected ObiNativeList<Vector2Int> elements;
        protected ObiNativeList<Matrix4x4> instanceTransforms;
        protected ObiNativeList<Matrix4x4> invInstanceTransforms;
        protected ObiNativeList<Vector4> instanceColors;

        public ObiChainRopeRenderSystem(ObiSolver solver)
        {
            m_Solver = solver;
        }

        public virtual void Dispose()
        {
            CleanupBatches();
            DestroyLists();
        }

        private void DestroyLists()
        {
            if (instanceTransforms != null)
                instanceTransforms.Dispose();
            if (invInstanceTransforms != null)
                invInstanceTransforms.Dispose();
            if (instanceColors != null)
                instanceColors.Dispose();

            if (elements != null)
                elements.Dispose();
            if (chunkData != null)
                chunkData.Dispose();
            if (rendererData != null)
                rendererData.Dispose();
            if (modifiers != null)
                modifiers.Dispose();
        }

        private void CreateListsIfNecessary()
        {
            DestroyLists();

            instanceTransforms = new ObiNativeList<Matrix4x4>();
            invInstanceTransforms = new ObiNativeList<Matrix4x4>();
            instanceColors = new ObiNativeList<Vector4>();
            elements = new ObiNativeList<Vector2Int>();
            chunkData = new ObiNativeList<ChunkData>();
            rendererData = new ObiNativeList<ChainRendererData>();
            modifiers = new ObiNativeList<ObiRopeChainRenderer.LinkModifier>();
        }

        private void CleanupBatches()
        {
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Dispose();
            batchList.Clear();
        }

        private void GenerateBatches()
        {
            instanceTransforms.Clear();
            invInstanceTransforms.Clear();
            instanceColors.Clear();
            elements.Clear();
            rendererData.Clear();
            chunkData.Clear();
            modifiers.Clear();

            // generate batches:
            for (int i = 0; i < renderers.Count; ++i)
            {
                var renderer = renderers[i];
                if (renderer.linkMesh != null && renderer.linkMaterial != null)
                {
                    renderer.renderParameters.layer = renderer.gameObject.layer;
                    batchList.Add(new InstancedRenderBatch(i, renderer.linkMesh, renderer.linkMaterial, renderer.renderParameters));
                }

            }

            // sort batches:
            batchList.Sort();

            // append elements:
            for (int i = 0; i < batchList.Count; ++i)
            {
                var renderer = renderers[batchList[i].firstRenderer];
                var rope = renderer.actor as ObiRopeBase;

                modifiers.AddRange(renderer.linkModifiers);

                rendererData.Add(new ChainRendererData(modifiers.count, renderer.twistAnchor, renderer.linkTwist, renderer.linkScale, rope.usesOrientedParticles));

                batchList[i].firstInstance = elements.count;
                batchList[i].instanceCount = rope.elements.Count;

                // iterate trough elements, finding discontinuities as we go:
                for (int e = 0; e < rope.elements.Count; ++e)
                {
                    elements.Add(new Vector2Int(rope.elements[e].particle1, rope.elements[e].particle2));

                    // At discontinuities, start a new chunk.
                    if (e < rope.elements.Count - 1 && rope.elements[e].particle2 != rope.elements[e + 1].particle1)
                    {
                        chunkData.Add(new ChunkData(rendererData.count - 1, elements.count));
                    }
                }
                chunkData.Add(new ChunkData(rendererData.count - 1, elements.count));
            }

            instanceTransforms.ResizeUninitialized(elements.count);
            invInstanceTransforms.ResizeUninitialized(elements.count);
            instanceColors.ResizeUninitialized(elements.count);
        }

        protected virtual void CloseBatches()
        {
            // Initialize each batch:
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Initialize();
        }

        public virtual void Setup()
        {
            using (m_SetupRenderMarker.Auto())
            {
                CreateListsIfNecessary();

                CleanupBatches();

                GenerateBatches();

                ObiUtils.MergeBatches(batchList);

                CloseBatches();
            }
        }

        public abstract void Render();

        public void Step()
        {
        }

        public void BakeMesh(ObiRopeChainRenderer renderer, ref Mesh mesh, bool transformToActorLocalSpace = false)
        {
            int index = renderers.IndexOf(renderer);

            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];
                if (index >= batch.firstRenderer && index < batch.firstRenderer + batch.rendererCount)
                {
                    batch.BakeMesh(renderers, renderer, chunkData, instanceTransforms,
                                  renderer.actor.actorSolverToLocalMatrix, ref mesh, transformToActorLocalSpace);
                    return;
                }
            }
        }
    }
}


