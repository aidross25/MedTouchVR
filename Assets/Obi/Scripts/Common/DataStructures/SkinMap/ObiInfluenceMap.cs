using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Obi
{
    [Serializable]
    public class ObiInfluenceMap
    {
        [SerializeField][HideInInspector] public ObiNativeInfluenceList influences = new ObiNativeInfluenceList();
        [SerializeField][HideInInspector] public ObiNativeIntList influenceOffsets = new ObiNativeIntList();

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct Influence
        {
            public int index;
            public float weight;

            public Influence(int index, float weight)
            {
                this.index = index;
                this.weight = weight;
            }
        }

        public bool isEmpty
        {
            get { return influences.count == 0 || influenceOffsets.count == 0; }
        }

        public void Clear()
        {
            influences.Clear();
            influenceOffsets.Clear();
        }

        public void NormalizeWeights(int offset, int count)
        {
            float weightSum = 0;
            for (int i = 0; i < count; ++i)
                weightSum += influences[offset + i].weight;

            if (weightSum > 0)
            {
                for (int i = 0; i < count; ++i)
                {
                    var influence = influences[offset + i];
                    influence.weight /= weightSum;
                    influences[offset + i] = influence;
                }
            }
        }
    }
}
