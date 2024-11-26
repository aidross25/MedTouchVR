using UnityEngine;

namespace Obi
{
    public interface IParticleRenderer
    {
        public ObiActor actor { get; }
        public Color particleColor { get; }
        public float radiusScale { get; }
    }

    public struct ParticleRendererData
    {
        public Color color;
        public float radiusScale;

        public ParticleRendererData(Color color, float radiusScale)
        {
            this.color = color;
            this.radiusScale = radiusScale;
        }
    }

    [AddComponentMenu("Physics/Obi/Obi Particle Renderer", 1000)]
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObiActor))]
    public class ObiParticleRenderer : MonoBehaviour, IParticleRenderer, ObiActorRenderer<ObiParticleRenderer>
    {
        public Material material;
        public RenderBatchParams renderParameters = new RenderBatchParams(true);

        [field: SerializeField]
        public Color particleColor { get; set; } = Color.white;

        [field: SerializeField]
        public float radiusScale { get; set; } = 1;

        public ObiActor actor { get; private set; }

        public void Awake()
        {
            actor = GetComponent<ObiActor>();
        }

        public void OnEnable()
        {
            ((ObiActorRenderer<ObiParticleRenderer>)this).EnableRenderer();
        }

        public void OnDisable()
        {
            ((ObiActorRenderer<ObiParticleRenderer>)this).DisableRenderer();
        }

        public void OnValidate()
        {
            ((ObiActorRenderer<ObiParticleRenderer>)this).SetRendererDirty(Oni.RenderingSystemType.Particles);
        }

        RenderSystem<ObiParticleRenderer> ObiRenderer<ObiParticleRenderer>.CreateRenderSystem(ObiSolver solver)
        {
            switch (solver.backendType)
            {

#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
                case ObiSolver.BackendType.Burst: return new BurstParticleRenderSystem(solver);
#endif
                case ObiSolver.BackendType.Compute:
                default:

                    if (SystemInfo.supportsComputeShaders)
                        return new ComputeParticleRenderSystem(solver);
                    return null;
            }
        }
    }
}

