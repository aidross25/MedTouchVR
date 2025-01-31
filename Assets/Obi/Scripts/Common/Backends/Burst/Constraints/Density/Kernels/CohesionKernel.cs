#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using System;
using Unity.Mathematics;

namespace Obi
{
    public struct CohesionKernel
    {
        public float W(float r, float h)
        {
            return math.cos(math.min(r, h) * 3 * math.PI / (2 * h));
        }
    }
}
#endif