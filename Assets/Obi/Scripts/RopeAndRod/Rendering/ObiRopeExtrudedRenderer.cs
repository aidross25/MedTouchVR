using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace Obi
{
    [AddComponentMenu("Physics/Obi/Obi Rope Extruded Renderer", 883)]
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObiPathSmoother))]
    public class ObiRopeExtrudedRenderer : MonoBehaviour, ObiActorRenderer<ObiRopeExtrudedRenderer>
    {
        public ObiPathSmoother smoother { get; private set; } // Each renderer should have its own smoother. The renderer then has a method to get position and orientation at a point.

        public Material material;

        public RenderBatchParams renderParameters = new RenderBatchParams(true);

        [Range(0, 1)]
        public float uvAnchor = 0;                  /**< Normalized position of texture coordinate origin along rope.*/

        public Vector2 uvScale = Vector2.one;       /**< Scaling of uvs along rope.*/

        public bool normalizeV = true;

        public ObiRopeSection section = null;       /**< Section asset to be extruded along the rope.*/

        public float thicknessScale = 0.8f;  /**< Scales section thickness.*/

        public ObiActor actor { get; private set; }

        public void Awake()
        {
            actor = GetComponent<ObiActor>();
        }

        public void OnEnable()
        {
            smoother = GetComponent<ObiPathSmoother>();
            ((ObiActorRenderer<ObiRopeExtrudedRenderer>)this).EnableRenderer();

        }

        public void OnDisable()
        {
            ((ObiActorRenderer<ObiRopeExtrudedRenderer>)this).DisableRenderer();
        }

        public void OnValidate()
        {
            ((ObiActorRenderer<ObiRopeExtrudedRenderer>)this).SetRendererDirty(Oni.RenderingSystemType.AllSmoothedRopes);
        }

        RenderSystem<ObiRopeExtrudedRenderer> ObiRenderer<ObiRopeExtrudedRenderer>.CreateRenderSystem(ObiSolver solver)
        {
            switch (solver.backendType)
            {

#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
                case ObiSolver.BackendType.Burst: return new BurstExtrudedRopeRenderSystem(solver);
#endif
                case ObiSolver.BackendType.Compute:
                default:

                    if (SystemInfo.supportsComputeShaders)
                        return new ComputeExtrudedRopeRenderSystem(solver);
                    return null;
            }
        }

    }
}


