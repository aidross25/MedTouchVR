using UnityEngine;

namespace Obi
{
    public struct ColliderShape
    {
        public enum ShapeType
        {
            Sphere = 0,
            Box = 1,
            Capsule = 2,
            Heightmap = 3,
            TriangleMesh = 4,
            EdgeMesh = 5,
            SignedDistanceField = 6
        }

        public Vector4 center;   
        public Vector4 size;     /**<     box: size of the box in each axis.
                                          sphere: radius of sphere (x,y,z),
                                          capsule: radius (x), height(y), direction (z, can be 0, 1 or 2).
                                          heightmap: width (x axis), height (y axis) and depth (z axis) in world units.*/
        public ShapeType type;
        public float contactOffset;
        public int dataIndex;
        public int rigidbodyIndex;  // index of the associated rigidbody in the collision world.
        public int materialIndex;   // index of the associated material in the collision world.
        public int forceZoneIndex;  // index of the associated force zone in the collision world.
        public int filter;          // bitwise category/mask.
        public int flags;           // first bit whether the collider is 2D (1) or 3D (0), second bit whether it's a trigger (1) or regular collider (0),
                                    // third bit (sign) determines whether shape is inverted or not.

        public bool is2D
        {
            get => (flags & 1) != 0;
            set => flags |= value ? 1 : 0;
        }
        public bool isTrigger
        {
            get => ((flags & 1 << 1) != 0) || forceZoneIndex >= 0;
            set => flags |= value ? 1 << 1 : 0;
        }
        public float sign
        {
            get => (flags & 1 << 2) != 0 ? -1 : 1;
        }

        public void SetSign(bool inverted)
        {
            if (inverted) flags |= 1 << 2;
            else flags &= ~(1 << 2);
        }

    }
}
