#ifndef INTEGRATION_INCLUDE
#define INTEGRATION_INCLUDE

#include "Quaternion.cginc"

float4 IntegrateLinear(float4 position, float4 velocity, float dt)
{
    return position + velocity * dt;
}

float4 DifferentiateLinear(float4 position, float4 prevPosition, float dt)
{
    return (position - prevPosition) / dt;
}

quaternion AngularVelocityToSpinQuaternion(quaternion rotation, float4 angularVelocity, float dt)
{
    quaternion delta = quaternion(angularVelocity.x,
                                  angularVelocity.y,
                                  angularVelocity.z, 0);

    return quaternion(0.5f * qmul(delta,rotation) * dt); 
}

quaternion IntegrateAngular(quaternion rotation, float4 angularVelocity, float dt)
{
    rotation += AngularVelocityToSpinQuaternion(rotation,angularVelocity, dt);
    return normalize(rotation);
}

float4 DifferentiateAngular(quaternion rotation, quaternion prevRotation, float dt)
{
    quaternion deltaq = qmul(rotation, q_conj(prevRotation));
    float s = deltaq.w >= 0 ? 1 : -1;
    return float4(s * deltaq.xyz * 2.0f / dt, 0);
}

#endif