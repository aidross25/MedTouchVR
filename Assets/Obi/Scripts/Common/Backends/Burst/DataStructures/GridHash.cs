#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Mathematics;

namespace Obi
{
    public struct GridHash
    {
        public readonly static int3[] cellOffsets3D =
        {
            new int3(1,0,0),
            new int3(0,1,0),
            new int3(1,1,0),
            new int3(0,0,1),
            new int3(1,0,1),
            new int3(0,1,1),
            new int3(1,1,1),
            new int3(-1,1,0),
            new int3(-1,-1,1),
            new int3(0,-1,1),
            new int3(1,-1,1),
            new int3(-1,0,1),
            new int3(-1,1,1)
        };

        public readonly static int3[] cellOffsets =
        {
            new int3(0, 0, 0),
            new int3(-1, 0, 0),
            new int3(0, -1, 0),
            new int3(0, 0, -1),
            new int3(1, 0, 0),
            new int3(0, 1, 0),
            new int3(0, 0, 1)
        };

        public readonly static int2[] cell2DOffsets =
        {
            new int2(0, 0),
            new int2(-1, 0),
            new int2(0, -1),
            new int2(1, 0),
            new int2(0, 1),
        };

        public static int3 Quantize(float3 v, float cellSize)
        {
            return new int3(math.floor(v / cellSize));
        }

        public static int2 Quantize(float2 v, float cellSize)
        {
            return new int2(math.floor(v / cellSize));
        }

        public static int Hash(in int4 cellIndex, int maxCells)
        {
            const int p1 = 73856093;
            const int p2 = 19349663;
            const int p3 = 83492791;
            const int p4 = 10380569;
            return math.abs(p1 * cellIndex.x ^ p2 * cellIndex.y ^ p3 * cellIndex.z ^ p4 * cellIndex.w) % maxCells;
        }

        public static int Hash(in int3 cellIndex, int maxCells)
        {
            const int p1 = 73856093;
            const int p2 = 19349663;
            const int p3 = 83492791;
            return ((p1 * cellIndex.x ^ p2 * cellIndex.y ^ p3 * cellIndex.z) & 0x7fffffff) % maxCells;

            /*var index = cellIndex - new int3(-32, -32, -32);
            return index.x + index.y * 64 + index.z * 64 * 64;*/
        }
    }
}
#endif