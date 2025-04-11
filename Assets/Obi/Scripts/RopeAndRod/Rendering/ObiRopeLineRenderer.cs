using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace Obi
{
    [AddComponentMenu("Physics/Obi/Obi Rope Line Renderer", 884)]
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObiPathSmoother))] 
    public class ObiRopeLineRenderer : MonoBehaviour, ObiActorRenderer<ObiRopeLineRenderer>
    {
        public ObiActor actor { get; private set; }

        public Material material;

        public RenderBatchParams renderParams = new RenderBatchParams(true);

        [Range(0, 1)]
        public float uvAnchor = 0;                  /**< Normalized position of texture coordinate origin along rope.*/

        public Vector2 uvScale = Vector2.one;       /**< Scaling of uvs along rope.*/

        public bool normalizeV = true;

        public float thicknessScale = 0.8f;  /**< Scales section thickness.*/

        public void Awake()
        {
            actor = GetComponent<ObiActor>();
        }

        void OnEnable()
        {
            ((ObiActorRenderer<ObiRopeLineRenderer>)this).EnableRenderer();
        }

        void OnDisable()
        {
            ((ObiActorRenderer<ObiRopeLineRenderer>)this).DisableRenderer();
        }

        public void OnValidate()
        {
            ((ObiActorRenderer<ObiRopeLineRenderer>)this).SetRendererDirty(Oni.RenderingSystemType.LineRope);
        }

        RenderSystem<ObiRopeLineRenderer> ObiRenderer<ObiRopeLineRenderer>.CreateRenderSystem(ObiSolver solver)
        {
            switch (solver.backendType)
            {

#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
                case ObiSolver.BackendType.Burst: return new BurstLineRopeRenderSystem(solver);
#endif
                case ObiSolver.BackendType.Compute:
                default:

                    if (SystemInfo.supportsComputeShaders)
                        return new ComputeLineRopeRenderSystem(solver);
                    return null;
            }
        }
    }
}


