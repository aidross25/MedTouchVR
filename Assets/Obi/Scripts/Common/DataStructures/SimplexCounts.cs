using UnityEngine;
using System.Collections;


namespace Obi
{
    public struct SimplexCounts
    {
        public int pointCount;
        public int edgeCount;
        public int triangleCount;

        public int simplexCount
        {
            get { return pointCount + edgeCount + triangleCount; }
        }

        public SimplexCounts(int pointCount, int edgeCount, int triangleCount)
        {
            this.pointCount = pointCount;
            this.edgeCount = edgeCount;
            this.triangleCount = triangleCount;
        }

        public int GetSimplexStartAndSize(int index, out int size)
        {
            if (index < triangleCount)
            {
                size = 3;
                return index * 3;
            }
            else if (index < triangleCount + edgeCount)
            {
                size = 2;
                return triangleCount * 3 + (index - triangleCount) * 2;
            }
            else if (index < simplexCount)
            {
                size = 1;
                return triangleCount * 3 + edgeCount * 2 + (index - triangleCount - edgeCount);
            }
            size = 0;
            return 0;
        }
    }
}
