#ifndef GRIDUTILS_INCLUDE
#define GRIDUTILS_INCLUDE

#define INVALID 0xFFFFFFFF
#define MIN_GRID_LEVEL -6 // minimum cell size is 0.01 meters, enough for very small colliders / particles (log(0.01) / log(2))
#define MAX_GRID_LEVEL 17 // maximum cell size is 131072 meters, enough for gargantuan objects.
#define GRID_LEVELS (MAX_GRID_LEVEL - MIN_GRID_LEVEL + 1)

RWStructuredBuffer<uint> levelPopulation; 
uint maxCells;         // maximum number of unique cells in the grid. 

static const float4 cellNeighborhood[27] = {
float4(-1,-1,-1, 0),
float4(-1,-1, 0, 0),
float4(-1,-1, 1, 0),
float4(-1, 0,-1, 0),
float4(-1, 0, 0, 0),
float4(-1, 0, 1, 0),
float4(-1, 1,-1, 0),
float4(-1, 1, 0, 0),
float4(-1, 1, 1, 0),
float4( 0,-1,-1, 0),
float4( 0,-1, 0, 0),
float4( 0,-1, 1, 0),
float4( 0, 0,-1, 0),
float4( 0, 0, 0, 0),
float4( 0, 0, 1, 0),
float4( 0, 1,-1, 0),
float4( 0, 1, 0, 0),
float4( 0, 1, 1, 0),
float4( 1,-1,-1, 0),
float4( 1,-1, 0, 0),
float4( 1,-1, 1, 0),
float4( 1, 0,-1, 0),
float4( 1, 0, 0, 0),
float4( 1, 0, 1, 0),
float4( 1, 1,-1, 0),
float4( 1, 1, 0, 0),
float4( 1, 1, 1, 0),
};

static const float4 aheadCellNeighborhood[13] = {
float4(1,0,0,0),    // + , 0 , 0 ( 1)
float4(0,1,0,0),    // 0 , + , 0 ( 3)
float4(1,1,0,0),    // + , + , 0 ( 4)
float4(0,0,1,0),    // 0 , 0 , + ( 9)
float4(1,0,1,0),    // + , 0 , + (10)
float4(0,1,1,0),    // 0 , + , + (12)
float4(1,1,1,0),     // + , + , + (13)

float4(-1,1,0,0),   // - , + , 0 ( 2)
float4(-1,-1,1,0),  // - , - , + ( 5)
float4(0,-1,1,0),   // 0 , - , + ( 6)
float4(1,-1,1,0),   // + , - , + ( 7)
float4(-1,0,1,0),   // - , 0 , + ( 8)
float4(-1,1,1,0),   // - , + , + (11)
};

[numthreads(1,1,1)]
void FindPopulatedLevels (uint3 id : SV_DispatchThreadID) 
{
    for (int l = 1; l <= GRID_LEVELS; ++l)
    {
        if (levelPopulation[l] > 0)
            levelPopulation[1 + levelPopulation[0]++] = l - 1;
    }
}

inline int GridLevelForSize(float size)
{
    // the magic number is 1/log(2), used because log_a(x) = log_b(x) / log_b(a)
    // level is clamped between MIN_LEVEL and MAX_LEVEL, then remapped to (0, MAX_LEVEL - MIN_LEVEL)
    // this allows us to avoid InterlockedMax issues on GPU, since it doesn't work on negative numbers on some APIs.
    return clamp((int)ceil(log(size) * 1.44269504089), MIN_GRID_LEVEL, MAX_GRID_LEVEL) - MIN_GRID_LEVEL;
}

inline float CellSizeOfLevel(int level)
{
    return exp2(level + MIN_GRID_LEVEL);
}

inline int4 GetParentCellCoords(int4 cellCoords, uint level)
{
    float decimation = exp2(level - cellCoords[3]);
    int4 cell = (int4)floor((float4)cellCoords / decimation);
    cell[3] = level;
    return cell;
}

uint Part1By1(uint x)
{
    x &= 0x0000ffff;                  // x = ---- ---- ---- ---- fedc ba98 7654 3210
    x = (x ^ (x << 8)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
    x = (x ^ (x << 4)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
    x = (x ^ (x << 2)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
    x = (x ^ (x << 1)) & 0x55555555; // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
    return x;
}

// "Insert" two 0 bits after each of the 10 low bits of x
uint Part1By2(uint x)
{
    x &= 0x000003ff;                  // x = ---- ---- ---- ---- ---- --98 7654 3210
    x = (x ^ (x << 16)) & 0xff0000ff; // x = ---- --98 ---- ---- ---- ---- 7654 3210
    x = (x ^ (x << 8)) & 0x0300f00f; // x = ---- --98 ---- ---- 7654 ---- ---- 3210
    x = (x ^ (x << 4)) & 0x030c30c3; // x = ---- --98 ---- 76-- --54 ---- 32-- --10
    x = (x ^ (x << 2)) & 0x09249249; // x = ---- 9--8 --7- -6-- 5--4 --3- -2-- 1--0
    return x;
}

uint Compact1By1(uint x)
{
    x &= 0x55555555;                  // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
    x = (x ^ (x >> 1)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
    x = (x ^ (x >> 2)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
    x = (x ^ (x >> 4)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
    x = (x ^ (x >> 8)) & 0x0000ffff; // x = ---- ---- ---- ---- fedc ba98 7654 3210
    return x;
}

uint Compact1By2(uint x)
{
    x &= 0x09249249;                  // x = ---- 9--8 --7- -6-- 5--4 --3- -2-- 1--0
    x = (x ^ (x >> 2)) & 0x030c30c3; // x = ---- --98 ---- 76-- --54 ---- 32-- --10
    x = (x ^ (x >> 4)) & 0x0300f00f; // x = ---- --98 ---- ---- 7654 ---- ---- 3210
    x = (x ^ (x >> 8)) & 0xff0000ff; // x = ---- --98 ---- ---- ---- ---- 7654 3210
    x = (x ^ (x >> 16)) & 0x000003ff; // x = ---- ---- ---- ---- ---- --98 7654 3210
    return x;
}
   
inline uint EncodeMorton2(uint2 coords)
{
    return (Part1By1(coords.y) << 1) + Part1By1(coords.x);
}

inline uint EncodeMorton3(uint3 coords)
{
    return (Part1By2(coords.z) << 2) + (Part1By2(coords.y) << 1) + Part1By2(coords.x);
}

inline uint3 DecodeMorton2(uint code)
{
    return uint3(Compact1By1(code >> 0), Compact1By1(code >> 1), 0);
}

inline uint3 DecodeMorton3(uint code)
{
    return uint3(Compact1By2(code >> 0), Compact1By2(code >> 1), Compact1By2(code >> 2));
}

inline uint GridHash(in int4 cellIndex)
{
  return (73856093*cellIndex.x ^ 
          19349663*cellIndex.y ^ 
          83492791*cellIndex.z ^ 
          10380569*cellIndex.w) % maxCells;
}

inline uint GridHash(in int3 cellIndex)
{
  return (73856093*cellIndex.x ^ 
          19349663*cellIndex.y ^ 
          83492791*cellIndex.z) % maxCells;
}

#endif