using System;
using System.Runtime.InteropServices;

#if (OBI_MATHEMATICS)
using Unity.Mathematics;
#endif


namespace Obi
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct VInt4
    {
        public int x;
        public int y;
        public int z;
        public int w;

        public VInt4(int x, int y, int z, int w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public VInt4(int x)
        {
            this.x = x;
            this.y = x;
            this.z = x;
            this.w = x;
        }

#if (OBI_MATHEMATICS)
        public static implicit operator VInt4(int4 i) => new VInt4(i.x, i.y, i.z, i.w);
        public static implicit operator int4(VInt4 i) => new int4(i.x, i.y, i.z, i.w);
#endif
    }
}
