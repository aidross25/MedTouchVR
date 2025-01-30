using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Obi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DiffuseParticleVertex
    {
        public Vector4 pos;
        public Vector3 offset;
        public Vector4 color;
        public Vector4 velocity;
        public Vector4 attributes;
    }

    public class ObiFoamRenderSystem : RenderSystem<ObiFoamGenerator>
    {
        public Oni.RenderingSystemType typeEnum { get => Oni.RenderingSystemType.FoamParticles; }

        public RendererSet<ObiFoamGenerator> renderers { get; } = new RendererSet<ObiFoamGenerator>();
        public bool isSetup => true;

        protected VertexAttributeDescriptor[] layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4), // velocity
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4), // attributes
        };

        static protected ProfilerMarker m_SetupRenderMarker = new ProfilerMarker("SetupSurfaceMeshing");
        static protected ProfilerMarker m_RenderMarker = new ProfilerMarker("SurfaceMeshing");

        protected HashSet<Camera> cameras = new HashSet<Camera>();
        protected MaterialPropertyBlock matProps;

        protected ObiSolver m_Solver;
        public ProceduralRenderBatch<DiffuseParticleVertex> renderBatch;

#if (UNITY_2019_1_OR_NEWER)
        System.Action<ScriptableRenderContext, Camera> renderCallback;
#endif

        // must be done before fluid meshing.
        public uint tier
        {
            get { return 0; }
        }

        public ObiFoamRenderSystem(ObiSolver solver)
        {
            m_Solver = solver;
            matProps = new MaterialPropertyBlock();

#if (UNITY_2019_1_OR_NEWER)
            renderCallback = new System.Action<ScriptableRenderContext, Camera>((cntxt, cam) => { RenderFromCamera(cam); });
            RenderPipelineManager.beginCameraRendering += renderCallback;
#endif
            Camera.onPreCull += RenderFromCamera;
        }

        public virtual void Dispose()
        {
            
#if (UNITY_2019_1_OR_NEWER)
            RenderPipelineManager.beginCameraRendering -= renderCallback;
#endif
            Camera.onPreCull -= RenderFromCamera;

            renderBatch.Dispose();
            cameras.Clear();
        }

        public void RenderFromCamera(Camera camera)
        {
            cameras.Add(camera);
        }

        public virtual void Setup()
        {
        }

        public virtual void Step()
        {
        }

        public virtual void Render()
        {
        }
    }
}