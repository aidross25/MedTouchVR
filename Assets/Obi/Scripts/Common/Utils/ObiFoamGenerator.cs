using UnityEngine;
using System;

namespace Obi
{
	/**
	 * Foam generators create diffuse particles in areas where certain conditions meet (high velocity constrasts, high vorticity, low density, high normal values, etc.). These particles
	 * are then advected trough the fluid velocity field.
	 */

    [AddComponentMenu("Physics/Obi/Obi Foam Generator", 1000)]
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObiActor))]
    [DisallowMultipleComponent]
    public class ObiFoamGenerator : MonoBehaviour, ObiActorRenderer<ObiFoamGenerator>
    {
        public ObiActor actor { get; private set; }

        [Header("Foam spawning")]
        public float foamGenerationRate = 100;
        public float foamPotential = 50;

        [Range(0,1)]
        public float foamPotentialDiffusion = 0.95f;
        public Vector2 velocityRange = new Vector2(2, 4);
        public Vector2 vorticityRange = new Vector2(4, 8);

        [Header("Foam properties")]
        public Color color = new Color(1,1,1,0.25f);
        public float size = 0.02f;
        [Range(0,1)]
        public float sizeRandom = 0.2f;
        public float lifetime = 5;
        [Range(0, 1)]
        public float lifetimeRandom = 0.2f;

        public float buoyancy = 0.5f;

        [Range(0, 1)]
        public float drag = 0.5f;

        [Range(0, 1)]
        public float atmosphericDrag = 0.5f;

        [Range(1, 50)]
        public float airAging = 2;

        [Range(0, 1)]
        public float isosurface = 0.02f;

        [Header("Density Control (Compute only)")]
        [Range(0, 1)]
        public float pressure = 1;
        [Range(0, 1)]
        public float density = 0.3f;
        [Range(1, 4)]
        public float smoothingRadius = 2.5f;
        [Min(0)]
        public float surfaceTension = 2;

        public void Awake()
        {
            actor = GetComponent<ObiActor>();
        }

        public void OnEnable()
        {
            ((ObiActorRenderer<ObiFoamGenerator>)this).EnableRenderer();
        }

        public void OnDisable()
        {
            ((ObiActorRenderer<ObiFoamGenerator>)this).DisableRenderer();
        }

        public void OnValidate()
        {
            ((ObiActorRenderer<ObiFoamGenerator>)this).SetRendererDirty(Oni.RenderingSystemType.FoamParticles);
        }

        RenderSystem<ObiFoamGenerator> ObiRenderer<ObiFoamGenerator>.CreateRenderSystem(ObiSolver solver)
        {
            switch (solver.backendType)
            {

#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
                case ObiSolver.BackendType.Burst: return new BurstFoamRenderSystem(solver);
#endif
                case ObiSolver.BackendType.Compute:
                default:

                    if (SystemInfo.supportsComputeShaders)
                        return new ComputeFoamRenderSystem(solver);
                    return null;
            }
        }
    }
}
