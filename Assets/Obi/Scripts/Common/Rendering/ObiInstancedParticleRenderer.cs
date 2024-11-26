using UnityEngine;
using Unity.Profiling;
using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{

    [AddComponentMenu("Physics/Obi/Obi Instanced Particle Renderer", 1001)]
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObiActor))]
    public class ObiInstancedParticleRenderer : MonoBehaviour, ObiActorRenderer<ObiInstancedParticleRenderer>
    {
        public Mesh mesh;
        public Material material;
        public RenderBatchParams renderParameters = new RenderBatchParams(true);
        public Color instanceColor = Color.white;
        public float instanceScale = 1;

        public ObiActor actor { get; private set; }

        void Awake()
        {
            actor = GetComponent<ObiActor>();
        }

        public void OnEnable()
        {
            ((ObiActorRenderer<ObiInstancedParticleRenderer>)this).EnableRenderer();
        }

        public void OnDisable()
        {
            ((ObiActorRenderer<ObiInstancedParticleRenderer>)this).DisableRenderer();
        }

        public void OnValidate()
        {
            ((ObiActorRenderer<ObiInstancedParticleRenderer>)this).SetRendererDirty(Oni.RenderingSystemType.InstancedParticles);
        }

        RenderSystem<ObiInstancedParticleRenderer> ObiRenderer<ObiInstancedParticleRenderer>.CreateRenderSystem(ObiSolver solver)
        {
            switch (solver.backendType)
            {

#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
                case ObiSolver.BackendType.Burst: return new BurstInstancedParticleRenderSystem(solver);
#endif
                case ObiSolver.BackendType.Compute:
                default:

                    if (SystemInfo.supportsComputeShaders)
                        return new ComputeInstancedParticleRenderSystem(solver);
                    return null;
            }
        }

    }
}

