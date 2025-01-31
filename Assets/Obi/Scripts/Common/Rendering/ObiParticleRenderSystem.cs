using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unity.Profiling;
using System.Runtime.InteropServices;

namespace Obi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ParticleVertex
    {
        public Vector4 pos;
        public Vector3 offset;
        public Vector4 color;
        public Vector4 b1;
        public Vector4 b2;
        public Vector4 b3;
    }

    public abstract class ObiParticleRenderSystem : RenderSystem<ObiParticleRenderer>
    {
        public Oni.RenderingSystemType typeEnum { get => Oni.RenderingSystemType.Particles; }

        public RendererSet<ObiParticleRenderer> renderers { get; } = new RendererSet<ObiParticleRenderer>();
        public bool isSetup => activeParticles != null;

        protected VertexAttributeDescriptor[] layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4)
        };

        static protected ProfilerMarker m_SetupRenderMarker = new ProfilerMarker("SetupParticleRendering");
        static protected ProfilerMarker m_RenderMarker = new ProfilerMarker("ParticleRendering");

        protected ObiSolver m_Solver;

        protected List<ProceduralRenderBatch<ParticleVertex>> batchList = new List<ProceduralRenderBatch<ParticleVertex>>();

        protected ObiNativeList<int> activeParticles;
        protected ObiNativeList<int> rendererIndex;
        protected ObiNativeList<ParticleRendererData> rendererData;

        public ObiParticleRenderSystem(ObiSolver solver)
        {
            m_Solver = solver;

            activeParticles = new ObiNativeList<int>();
            rendererIndex = new ObiNativeList<int>();
            rendererData = new ObiNativeList<ParticleRendererData>();
        }

        public virtual void Dispose()
        {
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Dispose();
            batchList.Clear();

            if (activeParticles != null)
                activeParticles.Dispose();
            if (rendererData != null)
                rendererData.Dispose();
            if (rendererIndex != null)
                rendererIndex.Dispose();
        }

        protected virtual void Clear()
        {
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Dispose();
            batchList.Clear();

            activeParticles.Clear();
            rendererData.Clear();
            rendererIndex.Clear();
        }

        protected virtual void CreateBatches()
        {
            // generate batches:
            for (int i = 0; i < renderers.Count; ++i)
            {
                renderers[i].renderParameters.layer = renderers[i].gameObject.layer;
                batchList.Add(new ProceduralRenderBatch<ParticleVertex>(i, renderers[i].material, renderers[i].renderParameters));
            }

            // sort batches:
            batchList.Sort();

            int particleCount = 0;
            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];
                var renderer = renderers[batch.firstRenderer];
                int actorParticleCount = renderer.actor.particleCount;

                batch.vertexCount += actorParticleCount * 4;
                batch.triangleCount += actorParticleCount * 2;

                batch.firstParticle = particleCount;
                particleCount += actorParticleCount;

                // add particles here, respecting batch order:
                activeParticles.AddRange(renderer.actor.solverIndices, actorParticleCount);
                rendererData.Add(new ParticleRendererData(renderer.particleColor, renderer.radiusScale));
                rendererIndex.AddReplicate(i, actorParticleCount);
            }
        }

        protected virtual void CloseBatches()
        {
            // Initialize each batch:
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Initialize(layout);
        }

        public virtual void  Setup()
        {
            using (m_SetupRenderMarker.Auto())
            {
                Clear();

                CreateBatches();

                ObiUtils.MergeBatches(batchList);

                CloseBatches();

            }
        }

        public virtual void Step()
        {
        }

        public virtual void Render()
        {
        }
    }
}

