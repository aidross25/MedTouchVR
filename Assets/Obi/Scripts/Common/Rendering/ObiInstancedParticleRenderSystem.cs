using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Profiling;
using System.Runtime.InteropServices;

namespace Obi
{

    public abstract class ObiInstancedParticleRenderSystem : RenderSystem<ObiInstancedParticleRenderer>
    {
        public Oni.RenderingSystemType typeEnum { get => Oni.RenderingSystemType.InstancedParticles; }

        public RendererSet<ObiInstancedParticleRenderer> renderers { get; } = new RendererSet<ObiInstancedParticleRenderer>();
        public bool isSetup => activeParticles != null;


        static protected ProfilerMarker m_SetupRenderMarker = new ProfilerMarker("SetupParticleRendering");
        static protected ProfilerMarker m_RenderMarker = new ProfilerMarker("ParticleRendering");

        protected ObiSolver m_Solver;

        protected List<InstancedRenderBatch> batchList = new List<InstancedRenderBatch>();

        protected ObiNativeList<int> activeParticles;
        protected ObiNativeList<int> rendererIndex;
        protected ObiNativeList<ParticleRendererData> rendererData;

        protected ObiNativeList<Matrix4x4> instanceTransforms;
        protected ObiNativeList<Matrix4x4> invInstanceTransforms;
        protected ObiNativeList<Vector4> instanceColors;

        public ObiInstancedParticleRenderSystem(ObiSolver solver)
        {
            m_Solver = solver;

            activeParticles = new ObiNativeList<int>();
            rendererIndex = new ObiNativeList<int>();
            rendererData = new ObiNativeList<ParticleRendererData>();
            instanceTransforms = new ObiNativeList<Matrix4x4>();
            invInstanceTransforms = new ObiNativeList<Matrix4x4>();
            instanceColors = new ObiNativeList<Vector4>();
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
            if (instanceTransforms != null)
                instanceTransforms.Dispose();
            if (invInstanceTransforms != null)
                invInstanceTransforms.Dispose();
            if (instanceColors != null)
                instanceColors.Dispose();
        }

        protected virtual void Clear()
        {
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Dispose();
            batchList.Clear();

            activeParticles.Clear();
            rendererData.Clear();
            rendererIndex.Clear();
            instanceTransforms.Clear();
            invInstanceTransforms.Clear();
            instanceColors.Clear();
        }

        protected virtual void CreateBatches()
        {
            // generate batches:
            for (int i = 0; i < renderers.Count; ++i)
            {
                renderers[i].renderParameters.layer = renderers[i].gameObject.layer;
                batchList.Add(new InstancedRenderBatch(i, renderers[i].mesh, renderers[i].material, renderers[i].renderParameters));
            }

            // sort batches:
            batchList.Sort();

            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];
                var renderer = renderers[batch.firstRenderer];
                int actorParticleCount = renderer.actor.particleCount;

                batch.firstInstance = activeParticles.count;
                batch.instanceCount = actorParticleCount;

                // add active particles here, respecting batch order:
                activeParticles.AddRange(renderer.actor.solverIndices, actorParticleCount);
                rendererData.Add(new ParticleRendererData(renderer.instanceColor, renderer.instanceScale));
                rendererIndex.AddReplicate(i, actorParticleCount);
            }

            instanceTransforms.ResizeUninitialized(activeParticles.count);
            invInstanceTransforms.ResizeUninitialized(activeParticles.count);
            instanceColors.ResizeUninitialized(activeParticles.count);
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

