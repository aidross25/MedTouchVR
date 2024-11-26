using System;
using UnityEngine;

namespace Obi
{
    [Serializable]
    public class ObiNativeInfluenceList : ObiNativeList<ObiInfluenceMap.Influence>
    {
        public ObiNativeInfluenceList() { }
        public ObiNativeInfluenceList(int capacity = 8, int alignment = 16) : base(capacity, alignment)
        {
            for (int i = 0; i < capacity; ++i)
                this[i] = new ObiInfluenceMap.Influence(0,0);
        }

    }
}

