#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Mathematics;

namespace Obi
{
    public struct BurstQueryShape
    {
        public float4 center;
        public float4 size;
        public QueryShape.QueryType type;
        public float contactOffset;
        public float maxDistance;
        public int filter;
    }
}
#endif