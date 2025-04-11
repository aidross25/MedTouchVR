#ifndef FLUIDCHUNKDEFS_INCLUDE
#define FLUIDCHUNKDEFS_INCLUDE

#define chunkResolution 4u // amount of voxels in width/height/depth

struct keyvalue
{
    uint key;
    uint handle;
};

uint3 chunkGridResolution; // height/width/depth of chunk grid
float3 chunkGridOrigin;
float voxelSize;

uint maxChunks;

uint VoxelID(uint3 coords)
{
    return coords.x + coords.y * chunkGridResolution.x + coords.z * chunkGridResolution.x * chunkGridResolution.y;
}

uint hash(uint k)
{
    k ^= k >> 16;
    k *= 0x85ebca6b;
    k ^= k >> 13;
    k *= 0xc2b2ae35;
    k ^= k >> 16;
    return k % maxChunks;
}

#endif