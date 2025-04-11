using System.Collections.Generic;
using UnityEngine;

namespace Obi
{
    [AddComponentMenu("Physics/Obi/Obi Rope Mesh Renderer", 886)]
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObiPathSmoother))]
    public class ObiRopeMeshRenderer : MonoBehaviour, ObiActorRenderer<ObiRopeMeshRenderer>, IMeshDataProvider
    {
        public Renderer sourceRenderer { get; protected set; }
        public ObiActor actor { get; private set; }
        public uint meshInstances { get {return instances;} }

        [field: SerializeField]
        public Mesh sourceMesh { get; set; }

        [field: SerializeField]
        public Material[] materials { get; set; }

        public virtual int vertexCount { get { return sourceMesh ? sourceMesh.vertexCount : 0; } }
        public virtual int triangleCount { get { return sourceMesh ? sourceMesh.triangles.Length / 3 : 0; } }

        public RenderBatchParams renderParameters = new RenderBatchParams(true);

        public ObiPathFrame.Axis axis;

        public float volumeScaling = 0;
        public bool stretchWithRope = true;
        public bool spanEntireLength = true;

        public uint instances = 1;
        public float instanceSpacing = 0;

        public float offset = 0;
        public Vector3 scale = Vector3.one;

        public void Awake()
        {
            actor = GetComponent<ObiActor>();
            sourceRenderer = GetComponent<MeshRenderer>();
        }

        public void OnEnable()
        {
            ((ObiActorRenderer<ObiRopeMeshRenderer>)this).EnableRenderer();
        }

        public void OnDisable()
        {
            ((ObiActorRenderer<ObiRopeMeshRenderer>)this).DisableRenderer();
        }

        public void OnValidate()
        {
            ((ObiActorRenderer<ObiRopeMeshRenderer>)this).SetRendererDirty(Oni.RenderingSystemType.MeshRope);
        }

        RenderSystem<ObiRopeMeshRenderer> ObiRenderer<ObiRopeMeshRenderer>.CreateRenderSystem(ObiSolver solver)
        {
            switch (solver.backendType)
            {

#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
                case ObiSolver.BackendType.Burst: return new BurstMeshRopeRenderSystem(solver);
#endif
                case ObiSolver.BackendType.Compute:
                default:

                    if (SystemInfo.supportsComputeShaders)
                        return new ComputeMeshRopeRenderSystem(solver);
                    return null;
            }
        }

        public virtual void GetVertices(List<Vector3> vertices) { sourceMesh.GetVertices(vertices); }
        public virtual void GetNormals(List<Vector3> normals) { sourceMesh.GetNormals(normals); }
        public virtual void GetTangents(List<Vector4> tangents) { sourceMesh.GetTangents(tangents); }
        public virtual void GetColors(List<Color> colors) { sourceMesh.GetColors(colors); }
        public virtual void GetUVs(int channel, List<Vector2> uvs) { sourceMesh.GetUVs(channel, uvs); }

        public virtual void GetTriangles(List<int> triangles) { triangles.Clear(); triangles.AddRange(sourceMesh.triangles); }
    }
}


