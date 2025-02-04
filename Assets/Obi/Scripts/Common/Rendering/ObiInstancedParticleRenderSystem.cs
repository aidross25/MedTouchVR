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

                // Create multiple batches of at most maxInstancesPerBatch particles each:
                int instanceCount = 0;
                while (instanceCount < renderers[i].actor.particleCount)
                {
                    var batch = new InstancedRenderBatch(i, renderers[i].mesh, renderers[i].material, renderers[i].renderParameters);
                    batch.firstInstance = instanceCount;
                    batch.instanceCount = Mathf.Min(renderers[i].actor.particleCount - instanceCount, Constants.maxInstancesPerBatch);
                    instanceCount += batch.instanceCount;
                    batchList.Add(batch);
                }
            }

            // sort batches:
            batchList.Sort();

            for (int i = 0; i < batchList.Count; ++i)
            {
                var batch = batchList[i];
                var renderer = renderers[batch.firstRenderer];
                int particlesSoFar = activeParticles.count;

                // add active particles here, respecting batch order:
                activeParticles.AddRange(renderer.actor.solverIndices, batch.firstInstance, batch.instanceCount);
                rendererIndex.AddReplicate(i, batch.instanceCount);
                rendererData.Add(new ParticleRendererData(renderer.instanceColor, renderer.instanceScale));

                batch.firstInstance = particlesSoFar;
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

