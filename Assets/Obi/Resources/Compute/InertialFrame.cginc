#ifndef INERTIALFRAME_INCLUDE
#define INERTIALFRAME_INCLUDE

#include "Transform.cginc"

struct inertialFrame
{
    transform frame;
    transform prevFrame;

    float4 velocity;
    float4 angularVelocity;

    float4 acceleration;
    float4 angularAcceleration;
};

#endif