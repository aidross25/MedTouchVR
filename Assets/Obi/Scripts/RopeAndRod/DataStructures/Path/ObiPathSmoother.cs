using UnityEngine;
using System;

namespace Obi
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObiRopeBase))]
    public class ObiPathSmoother : MonoBehaviour, ObiActorRenderer<ObiPathSmoother>
    {
        [Range(0, 1)]
        [Tooltip("Curvature threshold below which the path will be decimated. A value of 0 won't apply any decimation. As you increase the value, decimation will become more aggresive.")]
        public float decimation = 0;

        [Range(0, 3)]
        [Tooltip("Smoothing iterations applied to the path. A smoothing value of 0 won't perform any smoothing at all. Note that smoothing is applied after decimation.")]
        public uint smoothing = 0;

        [Tooltip("Twist in degrees applied to each sucessive path section.")]
        public float twist = 0;

        public ObiActor actor { get; private set; }

        [HideInInspector] public int indexInSystem = 0;

        public float SmoothLength
        {
            get
            {
                if (actor.isLoaded)
                {
                    var system = actor.solver.GetRenderSystem<ObiPathSmoother>() as ObiPathSmootherRenderSystem;

                    if (system != null)
                        return system.GetSmoothLength(indexInSystem);
                }
                return 0;
            }
        }

        public float SmoothSections
        {
            get {
                if (actor.isLoaded)
                {
                    var system = actor.solver.GetRenderSystem<ObiPathSmoother>() as ObiPathSmootherRenderSystem;

                    if (system != null)
                        return system.GetSmoothFrameCount(indexInSystem);
                }
                return 0;
            }
        }

        public void OnEnable()
        {
            actor = GetComponent<ObiActor>();
            ((ObiActorRenderer<ObiPathSmoother>)this).EnableRenderer();
        }

        private void OnDisable()
        {
            ((ObiActorRenderer<ObiPathSmoother>)this).DisableRenderer();
        }

        private void OnValidate()
        {
            ((ObiActorRenderer<ObiPathSmoother>)this).SetRendererDirty(Oni.RenderingSystemType.AllSmoothedRopes);
        }

        public ObiPathFrame GetSectionAt(float mu)
        {
            if (actor.isLoaded)
            {
                var system = actor.solver.GetRenderSystem<ObiPathSmoother>() as ObiPathSmootherRenderSystem;

                if (system != null)
                    return system.GetFrameAt(indexInSystem, mu);
            }

            return ObiPathFrame.Identity;
        }

        RenderSystem<ObiPathSmoother> ObiRenderer<ObiPathSmoother>.CreateRenderSystem(ObiSolver solver)
        {
            switch (solver.backendType)
            {

#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
                case ObiSolver.BackendType.Burst: return new BurstPathSmootherRenderSystem(solver);
#endif
                case ObiSolver.BackendType.Compute:
                default:

                    if (SystemInfo.supportsComputeShaders)
                        return new ComputePathSmootherRenderSystem(solver);
                    return null;

            }
        }
    }
}