using System;using System.Collections.Generic;using UnityEngine;using Unity.Collections;using System.Linq;using UnityEngine.Rendering;using System.Runtime.InteropServices;namespace Obi{    [AddComponentMenu("Physics/Obi/Obi Softbody Skinner", 931)]    [RequireComponent(typeof(SkinnedMeshRenderer))]
    [ExecuteInEditMode]    public class ObiSoftbodySkinner : MonoBehaviour, ObiActorRenderer<ObiSoftbodySkinner>, IMeshDataProvider    {        public struct BoneWeightComparer : IComparer<BoneWeight1>        {            public int Compare(BoneWeight1 x, BoneWeight1 y)            {                return y.weight.CompareTo(x.weight);            }        }        [Tooltip("Softbody to skin to.")]        public ObiSoftbody softbody;        [Tooltip("Skinmap asset to store the skin data into.")]        [SerializeField] public ObiSkinMap customSkinMap;        [Tooltip("The maximum distance a cluster can be from a vertex before it will not influence it any more.")]        public float radius = 0.5f;        [Tooltip("The ratio at which the cluster's influence on a vertex falls off with distance.")]        public float falloff = 1.0f;        [Tooltip("Maximum amount of bone influences for each vertex.")]        public uint maxInfluences = 4;        [Tooltip("Influence of the softbody in the resulting skin.")]        [Range(0, 1)]        public float softbodyInfluence = 1;        public Renderer sourceRenderer { get; protected set; }        public Material[] materials        {            get { return skinnedMeshRenderer.sharedMaterials; }        }        public virtual ObiSkinMap skinMap
        {
            get
            {
                if (customSkinMap != null || softbody == null || softbody.softbodyBlueprint == null)
                    return customSkinMap;
                return softbody.softbodyBlueprint.defaultSkinmap;
            }
            set { customSkinMap = value; }
        }        [HideInInspector] [SerializeField] public float[] m_softbodyInfluences;        [HideInInspector] private List<Transform> boneTransforms;        private SkinnedMeshRenderer skinnedMeshRenderer;        public ObiActor actor { get { return softbody; } }
        public uint meshInstances { get { return 1; } }

        // specify vertex count and layout
        public static VertexAttributeDescriptor[] layout =        {            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3,0),            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3,0),            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4,0),            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4,0),            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2,1),            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2,1),            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2,1),            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 2,1),        };        [StructLayout(LayoutKind.Sequential)]        public struct StaticClothVertexData        {            public Vector2 uv;            public Vector2 uv1;            public Vector2 uv2;            public Vector2 uv3;        }        public Matrix4x4 renderMatrix        {            get { return softbody.transform.worldToLocalMatrix; }        }        [field: SerializeField]
        [HideInInspector]        public Mesh sourceMesh { get; protected set; }        public int vertexCount { get { return sourceMesh.vertexCount; } }        public int triangleCount { get { return sourceMesh.triangles.Length / 3; } }        public void GetVertices(List<Vector3> vertices) { sourceMesh.GetVertices(vertices); }        public void GetNormals(List<Vector3> normals) { sourceMesh.GetNormals(normals); }        public void GetTangents(List<Vector4> tangents) { sourceMesh.GetTangents(tangents); }        public void GetColors(List<Color> colors) { sourceMesh.GetColors(colors); }        public void GetUVs(int channel, List<Vector2> uvs) { sourceMesh.GetUVs(channel, uvs); }        public void GetTriangles(List<int> triangles) { triangles.Clear(); triangles.AddRange(sourceMesh.triangles); }        public void Awake()        {            sourceRenderer = skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();

            // In case there's no user-set softbody reference,
            // try to find one in the same object:
            if (softbody == null)                softbody = GetComponent<ObiSoftbody>();        }

        public void OnEnable()        {            ((ObiActorRenderer<ObiSoftbodySkinner>)this).EnableRenderer();            if (Application.isPlaying && softbody != null)
            {
                if (softbody.isLoaded)
                    Softbody_OnBlueprintLoaded(softbody, softbody.sourceBlueprint);

                softbody.OnBlueprintLoaded += Softbody_OnBlueprintLoaded;
                softbody.OnSimulationStart += Softbody_OnSimulate;
            }        }

        public void OnDisable()        {            ((ObiActorRenderer<ObiSoftbodySkinner>)this).DisableRenderer();            if (Application.isPlaying && softbody != null)
            {
                softbody.OnBlueprintLoaded -= Softbody_OnBlueprintLoaded;
                softbody.OnSimulationStart -= Softbody_OnSimulate;
            }        }        public void OnValidate()        {            ((ObiActorRenderer<ObiSoftbodySkinner>)this).SetRendererDirty(Oni.RenderingSystemType.Softbody);        }        public virtual void CleanupRenderer()
        {
            skinnedMeshRenderer.sharedMesh = sourceMesh;
        }        public virtual bool ValidateRenderer()
        {
            if (skinnedMeshRenderer == null)
                sourceRenderer = skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();

            // no shared mesh, no custom skinmap, use the blueprint's input mesh:
            if (skinnedMeshRenderer.sharedMesh == null && customSkinMap == null && actor.sharedBlueprint != null)
                skinnedMeshRenderer.sharedMesh = ((ObiSoftbodySurfaceBlueprint)actor.sharedBlueprint).inputMesh;

            // if there's a sharedMesh, store it.
            if (skinnedMeshRenderer.sharedMesh != null)
                sourceMesh = skinnedMeshRenderer.sharedMesh;

            // at runtime, set the sharedMesh to null since we will be doing our own rendering.
            if (Application.isPlaying)
                skinnedMeshRenderer.sharedMesh = null;

            var skm = skinMap;

            if (softbody == null || softbody.softbodyBlueprint == null || skm == null)
                return false;

            // make sure checksums match, the amount of particles and the amount of bind poses in the skinmap match,
            // and the amount of influence counts and vertices also match.
            return skm.checksum == softbody.softbodyBlueprint.checksum &&
                   skm.bindPoses.count == actor.particleCount + sourceMesh.bindposes.Length &&
                   skm.particlesOnVertices.influenceOffsets.count == vertexCount + 1;
        }        public void InitializeInfluences()        {            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)            {                if (m_softbodyInfluences == null || m_softbodyInfluences.Length != skinnedMeshRenderer.sharedMesh.vertexCount)                {                    m_softbodyInfluences = new float[skinnedMeshRenderer.sharedMesh.vertexCount];                    for (int i = 0; i < m_softbodyInfluences.Length; ++i)                        m_softbodyInfluences[i] = 1;                }            }        }

        private void Softbody_OnBlueprintLoaded(ObiActor a, ObiActorBlueprint blueprint)
        {
            BindBoneParticles();
        }

        private void BindBoneParticles()
        {
            boneTransforms = null;
            var bprint = (ObiSoftbodySurfaceBlueprint)softbody.softbodyBlueprint;

            // the blueprint contains no root bone, no work to do:
            if (bprint.rootBone == null)
                return;

            // we'll be traversing the transform hierarchy breadth-first,
            // to make sure bone order matches the one in the blueprint.
            Queue<Transform> queue = new Queue<Transform>();

            // start at the root bone in the skeleton, make sure its name
            // matches the one used in the blueprint:
            string rootName = bprint.rootBone.name;

            if (skinnedMeshRenderer.rootBone != null && skinnedMeshRenderer.rootBone.name == rootName)
                queue.Enqueue(skinnedMeshRenderer.rootBone);
            else
            {
                // root bone names do not match, try to find a bone with the same name in the hierarchy.
                var bones = skinnedMeshRenderer.bones;
                foreach (var bone in bones)
                    if (bone.name == rootName)
                    {
                        queue.Enqueue(bone);
                        break;
                    }
            }

            // didn't find the root bone, so warn the user and just ignore bones.
            if (queue.Count != 1)
            {
                Debug.LogWarning("Did not find any bone matching the root bone defined in the softbody blueprint.");
                return;
            }

            // traverse the hierarchy and store the bones in a list:
            boneTransforms = new List<Transform>();
            while (queue.Count > 0)
            {
                var bone = queue.Dequeue();

                if (bone != null)
                {
                    boneTransforms.Add(bone);
                    foreach (Transform child in bone)
                        queue.Enqueue(child);
                }
            }

            // Deactivate bone particle dynamics by setting their inverse mass to zero.
            for (int i = 0; i < bprint.bonePairs.Count; ++i)
            {
                int solverIndex = softbody.solverIndices[bprint.bonePairs[i].x];
                softbody.solver.invMasses[solverIndex] = 0;
                softbody.solver.invRotationalMasses[solverIndex] = 0;
            }

            // No need to UpdateParticleProperties: at this point, shape matching constraints haven't been added yet,
            // their rest shape will be calculated the next PushConstraints() after loading the actor's blueprint.
        }

        private void Softbody_OnSimulate(ObiActor act, float simulatedTime, float substepTime)
        {
            if (boneTransforms == null || boneTransforms.Count == 0)
                return;

            var bprint = (ObiSoftbodySurfaceBlueprint)softbody.softbodyBlueprint;

            for (int i = 0; i < bprint.bonePairs.Count; ++i)
            {
                int solverIndex = softbody.solverIndices[bprint.bonePairs[i].x];
                var bone = boneTransforms[bprint.bonePairs[i].y];

                Matrix4x4 deformMatrix = softbody.solver.transform.worldToLocalMatrix * bone.transform.localToWorldMatrix * bprint.boneBindPoses[bprint.bonePairs[i].y];

                softbody.solver.startPositions[solverIndex] =
                softbody.solver.endPositions[solverIndex] =
                softbody.solver.positions[solverIndex] = deformMatrix.MultiplyPoint3x4(softbody.solver.restPositions[solverIndex]);

                softbody.solver.startOrientations[solverIndex] =
                softbody.solver.endOrientations[solverIndex] =
                softbody.solver.orientations[solverIndex] = deformMatrix.rotation * softbody.solver.restOrientations[solverIndex];
            }
        }        public void Bind()        {
            if (skinMap != null && softbody != null && softbody.softbodyBlueprint != null)
            {
                skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();                InitializeInfluences();

                var blueprintTransform = transform.gameObject == softbody.gameObject ? Matrix4x4.TRS(Vector3.zero, softbody.softbodyBlueprint.rotation, softbody.softbodyBlueprint.scale) : Matrix4x4.identity;

                skinMap.MapParticlesToVertices(skinnedMeshRenderer.sharedMesh, softbody, transform.localToWorldMatrix * blueprintTransform, softbody.transform.worldToLocalMatrix, radius, falloff, maxInfluences, true, softbodyInfluence, m_softbodyInfluences);                skinMap.checksum = softbody.softbodyBlueprint.checksum;
            }        }        RenderSystem<ObiSoftbodySkinner> ObiRenderer<ObiSoftbodySkinner>.CreateRenderSystem(ObiSolver solver)        {            switch (solver.backendType)            {





#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)                case ObiSolver.BackendType.Burst: return new BurstSoftbodyRenderSystem(solver);
#endif                case ObiSolver.BackendType.Compute:
                default:                    if (SystemInfo.supportsComputeShaders)                        return new ComputeSoftbodyRenderSystem(solver);                    return null;            }        }    }}