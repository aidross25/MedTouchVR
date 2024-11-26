#ifndef COLLIDERDEFS_INCLUDE
#define COLLIDERDEFS_INCLUDE

#define SPHERE_SHAPE 0
#define BOX_SHAPE 1
#define CAPSULE_SHAPE 2
#define HEIGHTMAP_SHAPE 3
#define TRIANGLE_MESH_SHAPE 4
#define EDGE_MESH_SHAPE 5
#define SDF_SHAPE 6

#define FORCEMODE_FORCE 0
#define FORCEMODE_ACCEL 1
#define FORCEMODE_WIND  2

#define DAMPDIR_ALL 0
#define DAMPDIR_FORCE 1
#define DAMPDIR_SURFACE  2

#define ZONETYPE_DIRECTIONAL 0
#define ZONETYPE_RADIAL 1
#define ZONETYPE_VORTEX 2
#define ZONETYPE_VOID 3

struct shape
{
    float4 center;   
    float4 size;     /**<     box: size of the box in each axis.
                                      sphere: radius of sphere (x,y,z),
                                      capsule: radius (x), height(y), direction (z, can be 0, 1 or 2).
                                      heightmap: width (x axis), height (y axis) and depth (z axis) in world units.*/
    uint type;       /**<  Sphere = 0,
                           Box = 1,
                           Capsule = 2,
                           Heightmap = 3,
                           TriangleMesh = 4,
                           EdgeMesh = 5,
                           SignedDistanceField = 6*/

    float contactOffset;
    int dataIndex;
    int rigidbodyIndex;  // index of the associated rigidbody in the collision world.
    int materialIndex;   // index of the associated material in the collision world.
    int forceZoneIndex;   // index of the associated force zone in the collision world.
    int phase;
    int flags;           // first bit whether the collider is 2D (1) or 3D (0), second bit whether it's a trigger (1) or regular collider (0).
                         // third bit determines whether shape is inverted or not.
   
    bool is2D()
    {
        return (flags & 1) != 0;
    }

    bool isTrigger()
    {
        // TODO: using bools doesn't work... why?
        int a = (flags & 1 << 1) != 0;
        int b = forceZoneIndex >= 0;
        return a || b;
    }

    float isInverted()
    {
        return (flags & 1 << 2) != 0 ? -1 : 1;
    }
};

struct forceZone
{
    uint type;
    uint mode;
    uint dampingDir;
    float intensity;
    float minDistance;
    float maxDistance;
    float falloffPower;
    float damping;
};

#endif