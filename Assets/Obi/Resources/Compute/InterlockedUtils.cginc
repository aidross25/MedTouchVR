#ifndef INTERLOCKEDUTILS_INCLUDE
#define INTERLOCKEDUTILS_INCLUDE

void InterlockedAddFloat(RWStructuredBuffer<uint4> buffer, int index, int axis, float value) 
{
    uint i_val = asuint(value);
    uint tmp0 = 0;
    uint tmp1;

    [allow_uav_condition]
    while (true) 
    {
        InterlockedCompareExchange(buffer[index][axis], tmp0, i_val, tmp1);

        if (tmp1 == tmp0) 
            break;

        tmp0 = tmp1;
        i_val = asuint(value + asfloat(tmp1));
    }

    return;
}

void InterlockedAddFloat(RWStructuredBuffer<uint> buffer, int index, float value) 
{
    uint i_val = asuint(value);
    uint tmp0 = 0;
    uint tmp1;

    [allow_uav_condition]
    while (true) 
    {
        InterlockedCompareExchange(buffer[index], tmp0, i_val, tmp1);

        if (tmp1 == tmp0) 
            break;

        tmp0 = tmp1;
        i_val = asuint(value + asfloat(tmp1));
    }

    return;
}

#endif