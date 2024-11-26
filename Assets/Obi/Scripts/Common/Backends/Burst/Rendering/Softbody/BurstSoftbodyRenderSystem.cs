#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Obi
{
    public class BurstSoftbodyRenderSystem : ObiSoftbodyRenderSystem
    {
        protected override IReadOnlyList<ObiSoftbodySkinner> baseRenderers { get { return renderers.AsReadOnly(); } }

        public BurstSoftbodyRenderSystem(ObiSolver solver) : base(solver)
        {
        }

        protected override void CloseBatches()
        {
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Initialize(sortedRenderers, meshData, meshIndices, layout, false);

            base.CloseBatches();
        }

        public override void Render()
        {
            if (!Application.isPlaying)
                return;

            using (m_RenderMarker.Auto())
            {

                UpdateBoneTransformData();

                for (int i = 0; i < batchList.Count; ++i)
                {
                    var batch = batchList[i];

                    // update meshes:
                    var updateJob = new BuildSoftbodyMeshJob
                    {
                        rendererIndices = batch.vertexToRenderer.AsNativeArray<int>(),
                        skinmapIndices = skinMapIndices.AsNativeArray<int>(),
                        meshIndices = meshIndices.AsNativeArray<int>(),
                        skeletonIndices = skeletonIndices.AsNativeArray<int>(),

                        vertexOffsets = vertexOffsets.AsNativeArray<int>(),
                        particleOffsets = particleOffsets.AsNativeArray<int>(),
                        influenceOffsets = skinmapData.influenceOffsets.AsNativeArray<int>(),

                        particleIndices = batch.particleIndices.AsNativeArray<int>(),

                        skinData = skinmapData.skinData.AsNativeArray<SkinmapDataBatch.SkinmapData>(),
                        meshData = meshData.meshData.AsNativeArray<MeshDataBatch.MeshData>(),
                        skeletonData = skeletonData.skeletonData.AsNativeArray<SkeletonDataBatch.SkeletonData>(),

                        bindPoses = skinmapData.particleBindPoses.AsNativeArray<float4x4>(),
                        influences = skinmapData.influences.AsNativeArray<ObiInfluenceMap.Influence>(),

                        renderablePositions = m_Solver.renderablePositions.AsNativeArray<float4>(),
                        renderableOrientations = m_Solver.renderableOrientations.AsNativeArray<quaternion>(),
                        colors = m_Solver.colors.AsNativeArray<float4>(),

                        bonePos = skeletonData.bonePositions.AsNativeArray<float3>(),
                        boneRot = skeletonData.boneRotations.AsNativeArray<quaternion>(),
                        boneScl = skeletonData.boneScales.AsNativeArray<float3>(),

                        positions = meshData.restPositions.AsNativeArray<float3>(),
                        normals = meshData.restNormals.AsNativeArray<float3>(),
                        tangents = meshData.restTangents.AsNativeArray<float4>(),

                        world2Solver = m_Solver.transform.worldToLocalMatrix,
                        vertices = batch.dynamicVertexData.AsNativeArray<DynamicBatchVertex>(),
                    };

                    updateJob.Schedule(batch.vertexCount, 16).Complete();

                    batch.mesh.SetVertexBufferData(batch.dynamicVertexData.AsNativeArray<DynamicBatchVertex>(), 0, 0, batch.vertexCount, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontNotifyMeshUsers);

                    var rp = batch.renderParams;
                    rp.worldBounds = m_Solver.bounds;

                    for (int m = 0; m < batch.materials.Length; ++m)
                    {
                        rp.material = batch.materials[m];
                        Graphics.RenderMesh(rp, batch.mesh, m, m_Solver.transform.localToWorldMatrix, m_Solver.transform.localToWorldMatrix);
                    }
                }
            }
        }

        [BurstCompile]
        struct BuildSoftbodyMeshJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> rendererIndices;
            [ReadOnly] public NativeArray<int> skinmapIndices;
            [ReadOnly] public NativeArray<int> meshIndices;
            [ReadOnly] public NativeArray<int> skeletonIndices;

            [ReadOnly] public NativeArray<int> vertexOffsets;
            [ReadOnly] public NativeArray<int> particleOffsets;
            [ReadOnly] public NativeArray<int> influenceOffsets;

            [ReadOnly] public NativeArray<int> particleIndices;

            [ReadOnly] public NativeArray<SkinmapDataBatch.SkinmapData> skinData;
            [ReadOnly] public NativeArray<MeshDataBatch.MeshData> meshData;
            [ReadOnly] public NativeArray<SkeletonDataBatch.SkeletonData> skeletonData;

            [ReadOnly] public NativeArray<float4x4> bindPoses;
            [ReadOnly] public NativeArray<ObiInfluenceMap.Influence> influences;

            [ReadOnly] public NativeArray<float4> renderablePositions;
            [ReadOnly] public NativeArray<quaternion> renderableOrientations;
            [ReadOnly] public NativeArray<float4> colors;

            [ReadOnly] public NativeArray<float3> bonePos;
            [ReadOnly] public NativeArray<quaternion> boneRot;
            [ReadOnly] public NativeArray<float3> boneScl;

            [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public NativeArray<float3> normals;
            [ReadOnly] public NativeArray<float4> tangents;

            [ReadOnly] public float4x4 world2Solver;

            public NativeArray<DynamicBatchVertex> vertices;

            public void Execute(int i)
            {
                int rendererIndex = rendererIndices[i];

                // get skin map, skeleton and  mesh data:
                var skin = skinData[skinmapIndices[rendererIndex]];
                var mesh = meshData[meshIndices[rendererIndex]];
                var skel = skeletonData[skeletonIndices[rendererIndex]];

                // get index of this vertex in its original mesh:
                int originalVertexIndex = i - vertexOffsets[rendererIndex];

                // get index of the vertex in the mesh batch:
                int batchedVertexIndex = mesh.firstVertex + originalVertexIndex;

                // get first influence and amount of influences for this vertex:
                int influenceStart = influenceOffsets[skin.firstInfOffset + originalVertexIndex];
                int influenceCount = influenceOffsets[skin.firstInfOffset + originalVertexIndex + 1] - influenceStart;

                var vertex = vertices[i];
                vertex.pos = float3.zero;
                vertex.normal = float3.zero;
                vertex.tangent = float4.zero;
                vertex.color = float4.zero;

                for (int k = influenceStart; k < influenceStart + influenceCount; ++k)
                {
                    var inf = influences[skin.firstInfluence + k];
                    float4x4 trfm;

                    if (inf.index < skin.bindPoseCount)
                    {
                        int boneIndex = skel.firstBone + inf.index;
                        int bindIndex = skin.firstParticleBindPose + inf.index;

                        float4x4 bind = bindPoses[bindIndex];

                        //  there might be more bind poses than bones.
                        float4x4 deform = inf.index < skel.boneCount ? float4x4.TRS(bonePos[boneIndex], boneRot[boneIndex], boneScl[boneIndex]) : float4x4.identity;

                        // bone skinning leaves vertices in world space, so convert to solver space afterwards:
                        trfm = math.mul(world2Solver, math.mul(deform, bind));
                    }
                    else // particle influence
                    {
                        int p = particleIndices[particleOffsets[rendererIndex] + inf.index - skin.bindPoseCount];

                        float4x4 deform = math.mul(float4x4.Translate(renderablePositions[p].xyz), renderableOrientations[p].toMatrix());
                        trfm = math.mul(deform, bindPoses[skin.firstParticleBindPose + inf.index]);
                        vertex.color += (Vector4)colors[p] * inf.weight;
                    }

                    // update vertex/normal/tangent:
                    vertex.pos += (Vector3)math.mul(trfm, new float4(positions[batchedVertexIndex], 1)).xyz * inf.weight;
                    vertex.normal += (Vector3)math.mul(trfm, new float4(normals[batchedVertexIndex], 0)).xyz * inf.weight;
                    vertex.tangent += (Vector4)new float4(math.mul(trfm, new float4(tangents[batchedVertexIndex].xyz, 0)).xyz, tangents[batchedVertexIndex].w) * inf.weight;
                }

                vertices[i] = vertex;
            }
        }
    }
}
#endif

