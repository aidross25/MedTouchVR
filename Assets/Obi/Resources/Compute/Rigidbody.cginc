#ifndef RIGIDBODY_INCLUDE
#define RIGIDBODY_INCLUDE

#include "InertialFrame.cginc"
#include "InterlockedUtils.cginc"
#include "Integration.cginc"

RWStructuredBuffer<uint4> linearDeltasAsInt;
RWStructuredBuffer<uint4> angularDeltasAsInt;

struct rigidbody
{
    float4x4 inverseInertiaTensor;
    float4 velocity;
    float4 angularVelocity;
    float4 com;
    float inverseMass;
         
    int constraintCount;
    int pad1;
    int pad2;
};

StructuredBuffer<rigidbody> rigidbodies;

void CalculateContactMassesB(in rigidbody rb,
                             in transform t,
                             float4 pointB,
                             float4 normal,
                             float4 bitangent,
                             float4 tangent,
                             out float normalInvMassB,
                             out float tangentInvMassB,
                             out float bitangentInvMassB)
{
    float4 rB = t.TransformPoint(pointB) - rb.com;

    // initialize inverse linear masses:
    normalInvMassB = tangentInvMassB = bitangentInvMassB = rb.inverseMass;
    normalInvMassB += RotationalInvMass(rb.inverseInertiaTensor, rB, normal);
    tangentInvMassB += RotationalInvMass(rb.inverseInertiaTensor, rB, tangent);
    bitangentInvMassB += RotationalInvMass(rb.inverseInertiaTensor, rB, bitangent);
}

float4 GetRigidbodyVelocityAtPoint(in rigidbody rb,
                                   float4 pnt,
                                   float4 linearDelta,
                                   float4 angularDelta,
                                   in inertialFrame frame)
{
    float4 linearVel  = rb.velocity + linearDelta;
    float4 angularVel = rb.angularVelocity + angularDelta;
    float4 r = frame.frame.TransformPoint(pnt) - rb.com;

    // calculate rigidbody velocity. (point is assumed to be expressed in solver space, convert it to world space):
    float4 wsRigidbodyVel = linearVel + float4(cross(angularVel.xyz, r.xyz), 0);

    // calculate solver velocity:
    float4 wsSolverVelocity = frame.velocity + float4(cross(frame.angularVelocity.xyz, pnt.xyz), 0);

    // convert the resulting velocity back to solver space:
    return frame.frame.InverseTransformVector(wsRigidbodyVel - wsSolverVelocity);
};

void AtomicAddLinearDelta(in int rigidbodyIndex, in float4 delta)
{
    InterlockedAddFloat(linearDeltasAsInt, rigidbodyIndex, 0, delta.x);
    InterlockedAddFloat(linearDeltasAsInt, rigidbodyIndex, 1, delta.y);
    InterlockedAddFloat(linearDeltasAsInt, rigidbodyIndex, 2, delta.z);
}

void AtomicAddAngularDelta(in int rigidbodyIndex, in float4 delta)
{
    InterlockedAddFloat(angularDeltasAsInt, rigidbodyIndex, 0, delta.x);
    InterlockedAddFloat(angularDeltasAsInt, rigidbodyIndex, 1, delta.y);
    InterlockedAddFloat(angularDeltasAsInt, rigidbodyIndex, 2, delta.z);
}

void ApplyImpulse(int rigidbodyIndex,
                  float4 impulse,
                  float4 pnt,
                  in transform frame)
{
    float4 impulseWS = frame.TransformVector(impulse);
    float4 r = frame.TransformPoint(pnt) - rigidbodies[rigidbodyIndex].com;

    float4 linearDelta = rigidbodies[rigidbodyIndex].inverseMass * impulseWS;
    float4 angularDelta = mul(rigidbodies[rigidbodyIndex].inverseInertiaTensor, float4(cross(r.xyz, impulseWS.xyz), 0));
    
    AtomicAddLinearDelta (rigidbodyIndex, linearDelta);
    AtomicAddAngularDelta(rigidbodyIndex, angularDelta);
}

void ApplyDeltaQuaternion(int rigidbodyIndex,
                          quaternion rotation,
                          quaternion delta,
                          in transform frame,
                          float dt)
{
    quaternion rotationWS = qmul(frame.rotation, rotation);
    quaternion deltaWS = qmul(frame.rotation, delta);

    // convert quaternion delta to angular acceleration:
    quaternion newRotation = normalize(rotationWS + deltaWS);
    AtomicAddAngularDelta(rigidbodyIndex, DifferentiateAngular(newRotation, rotationWS, dt));
}

#endif