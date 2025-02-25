using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace Obi
{
    [AddComponentMenu("Physics/Obi/Obi Rope Chain Renderer", 885)]
    [ExecuteInEditMode]
    public class ObiRopeChainRenderer : MonoBehaviour, ObiActorRenderer<ObiRopeChainRenderer>
    {
        [Serializable]
        public struct LinkModifier
        {
            public Vector3 translation;
            public Vector3 scale;
            public Vector3 rotation;

            public void Clear()
            {
                translation = Vector3.zero;
                scale = Vector3.one;
                rotation = Vector3.zero;
            }
        }

        public Mesh linkMesh;
        public Material linkMaterial;
        public Vector3 linkScale = Vector3.one;     /**< Scale of chain links.*/

        [Range(0, 1)]
        public float twistAnchor = 0;               /**< Normalized position of twisting origin along rope.*/
        public float linkTwist = 0;              /**< Amount of twist applied to each section, in degrees.*/

        public List<LinkModifier> linkModifiers = new List<LinkModifier>();

        public RenderBatchParams renderParameters = new RenderBatchParams(true);

        public ObiActor actor { get; private set; }

        void Awake()
        {
            actor = GetComponent<ObiActor>();
        }

        public void OnEnable()
        {
            ((ObiActorRenderer<ObiRopeChainRenderer>)this).EnableRenderer();
        }

        public void OnDisable()
        {
            ((ObiActorRenderer<ObiRopeChainRenderer>)this).DisableRenderer();
        }

        public void OnValidate()
        {
            ((ObiActorRenderer<ObiRopeChainRenderer>)this).SetRendererDirty(Oni.RenderingSystemType.ChainRope);
        }

        RenderSystem<ObiRopeChainRenderer> ObiRenderer<ObiRopeChainRenderer>.CreateRenderSystem(ObiSolver solver)
        {
            switch (solver.backendType)
            {

#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
                case ObiSolver.BackendType.Burst: return new BurstChainRopeRenderSystem(solver);
#endif
                case ObiSolver.BackendType.Compute:
                default:

                    if (SystemInfo.supportsComputeShaders)
                        return new ComputeChainRopeRenderSystem(solver);
                    return null;
            }
        }
    }
}

