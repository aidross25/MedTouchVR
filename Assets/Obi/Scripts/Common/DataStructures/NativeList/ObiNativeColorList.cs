using System;
using UnityEngine;

namespace Obi
{
    [Serializable]
    public class ObiNativeColorList : ObiNativeList<Color>
    {
        public ObiNativeColorList() { }
        public ObiNativeColorList(int capacity = 8, int alignment = 16) : base(capacity, alignment)
        {
            for (int i = 0; i < capacity; ++i)
                this[i] = Color.white;
        }
    }
}

