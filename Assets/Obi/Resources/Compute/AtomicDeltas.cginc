#ifndef ATOMICDELTAS_INCLUDE
#define ATOMICDELTAS_INCLUDE

#include "InterlockedUtils.cginc"

RWStructuredBuffer<uint4> deltasAsInt;
RWStructuredBuffer<uint> positionConstraintCounts;

RWStructuredBuffer<uint4> orientationDeltasAsInt;
RWStructuredBuffer<uint> orientationConstraintCounts;

// atomic delta add:
void AtomicAddPositionDelta(in int index, in float4 delta)
{
    InterlockedAddFloat(deltasAsInt, index, 0, delta.x);
    InterlockedAddFloat(deltasAsInt, index, 1, delta.y);
    InterlockedAddFloat(deltasAsInt, index, 2, delta.z);
    InterlockedAdd(positionConstraintCounts[index], 1);
}

void AtomicAddOrientationDelta(in int index, in quaternion delta)
{
    InterlockedAddFloat(orientationDeltasAsInt, index, 0, delta.x);
    InterlockedAddFloat(orientationDeltasAsInt, index, 1, delta.y);
    InterlockedAddFloat(orientationDeltasAsInt, index, 2, delta.z);
    InterlockedAddFloat(orientationDeltasAsInt, index, 3, delta.w);
    InterlockedAdd(orientationConstraintCounts[index], 1);
}

// non-atomic versions:
void AddPositionDelta(in int index, in float4 delta)
{
    deltasAsInt[index] = asuint(delta + asfloat(deltasAsInt[index]));  
    positionConstraintCounts[index]++;
}

void AddOrientationDelta(in int index, in quaternion delta)
{
    orientationDeltasAsInt[index] = asuint(delta + asfloat(orientationDeltasAsInt[index]));  
    orientationConstraintCounts[index]++;
}

// applying deltas:
void ApplyPositionDelta(RWStructuredBuffer<float4> positions, in int index, in float SOR)
{
    int count = positionConstraintCounts[index];
    if (count > 0)
    {
        positions[index].xyz += float3(asfloat(deltasAsInt[index].x),
                                       asfloat(deltasAsInt[index].y),
                                       asfloat(deltasAsInt[index].z)) * SOR / count;

        deltasAsInt[index] = uint4(0, 0, 0, 0);
        positionConstraintCounts[index] = 0;
    }
}

void ApplyOrientationDelta(RWStructuredBuffer<quaternion> orientations, in int index, in float SOR)
{
    int count = orientationConstraintCounts[index];
    if (count > 0)
    {
        orientations[index] += quaternion(asfloat(orientationDeltasAsInt[index].x),
                                          asfloat(orientationDeltasAsInt[index].y),
                                          asfloat(orientationDeltasAsInt[index].z),
                                          asfloat(orientationDeltasAsInt[index].w)) * SOR / count;

        orientations[index] = normalize(orientations[index]);

        orientationDeltasAsInt[index] = uint4(0, 0, 0, 0);
        orientationConstraintCounts[index] = 0;
    }
}

#endif