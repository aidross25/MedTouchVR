using System.Collections.Generic;
using UnityEngine;

namespace Obi
{
    public class SkinmapDataBatch
    {
        public struct SkinmapData
        {
            public int firstInfluence;
            public int firstInfOffset;
            public int firstParticleBindPose;

            public int firstSkinWeight;
            public int firstSkinWeightOffset;
            public int firstBoneBindPose;

            public int bindPoseCount;
        }

        private Dictionary<ObiSkinMap, int> skinMapToIndex;

        // per skinMap data:
        public ObiNativeList<SkinmapData> skinData;

        // particle on vertex influence data:
        public ObiNativeList<ObiInfluenceMap.Influence> influences;
        public ObiNativeList<int> influenceOffsets;
        public ObiNativeList<Matrix4x4> particleBindPoses;

        // bone on particle  influence data:
        public ObiNativeList<ObiInfluenceMap.Influence> skinWeights;
        public ObiNativeList<int> skinWeightOffsets;
        public ObiNativeList<Matrix4x4> boneBindPoses;

        public int Count { get { return skinData.count; } }

        public SkinmapDataBatch()
        {
            skinMapToIndex = new Dictionary<ObiSkinMap, int>();
            skinData = new ObiNativeList<SkinmapData>();

            influences = new ObiNativeList<ObiInfluenceMap.Influence>();
            influenceOffsets = new ObiNativeList<int>();
            particleBindPoses = new ObiNativeList<Matrix4x4>();

            skinWeights = new ObiNativeList<ObiInfluenceMap.Influence>();
            skinWeightOffsets = new ObiNativeList<int>();
            boneBindPoses = new ObiNativeList<Matrix4x4>();
        }

        public void Dispose()
        {
            skinData.Dispose();

            influences.Dispose();
            influenceOffsets.Dispose();
            particleBindPoses.Dispose();

            skinWeights.Dispose();
            skinWeightOffsets.Dispose();
            boneBindPoses.Dispose();
        }

        public void Clear()
        {
            skinMapToIndex.Clear();
            skinData.Clear();

            influences.Clear();
            influenceOffsets.Clear();
            particleBindPoses.Clear();

            skinWeights.Clear();
            skinWeightOffsets.Clear();
            boneBindPoses.Clear();
        }

        public int AddSkinmap(ObiSkinMap skinmap, Matrix4x4[] boneBinds)
        {
            int index = 0;
            if (!skinMapToIndex.TryGetValue(skinmap, out index))
            {
                index = skinData.count;
                skinMapToIndex[skinmap] = index;

                skinData.Add(new SkinmapData
                {
                    firstInfluence = influences.count,
                    firstInfOffset = influenceOffsets.count,
                    firstParticleBindPose = particleBindPoses.count,

                    firstSkinWeight = skinWeights.count,
                    firstSkinWeightOffset = skinWeightOffsets.count,
                    firstBoneBindPose = boneBindPoses.count,

                    bindPoseCount = boneBinds.Length
                });

                influences.AddRange(skinmap.particlesOnVertices.influences.AsNativeArray<ObiInfluenceMap.Influence>());
                influenceOffsets.AddRange(skinmap.particlesOnVertices.influenceOffsets.AsNativeArray<int>());
                particleBindPoses.AddRange(skinmap.bindPoses.AsNativeArray<Matrix4x4>());

                skinWeights.AddRange(skinmap.bonesOnParticles.influences.AsNativeArray<ObiInfluenceMap.Influence>());
                skinWeightOffsets.AddRange(skinmap.bonesOnParticles.influenceOffsets.AsNativeArray<int>());

                if (boneBinds != null)
                    boneBindPoses.AddRange(boneBinds);
            }
            return index;
        }

        public void PrepareForCompute()
        {
            skinData.AsComputeBuffer<SkinmapData>();

            influences.AsComputeBuffer<ObiInfluenceMap.Influence>();
            influenceOffsets.AsComputeBuffer<int>();
            particleBindPoses.AsComputeBuffer<Matrix4x4>();

            skinWeights.AsComputeBuffer<ObiInfluenceMap.Influence>();
            skinWeightOffsets.AsComputeBuffer<int>();
            boneBindPoses.AsComputeBuffer<Matrix4x4>();
        }

    }
}