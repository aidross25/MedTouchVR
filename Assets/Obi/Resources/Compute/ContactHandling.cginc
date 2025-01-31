#ifndef CONTACTHANDLING_INCLUDE
#define CONTACTHANDLING_INCLUDE

#include "MathUtils.cginc"
#include "Transform.cginc"

struct contact // 96 bytes
{
    float4 pointA; // point A, expressed as simplex barycentric coords for simplices, as a solver-space position for colliders.
    float4 pointB; // point B, expressed as simplex barycentric coords for simplices, as a solver-space position for colliders.
    float4 normal;         /**< Normal direction. */
    float4 tangent;        /**< Tangent direction. */

    float dist;            /** distance between both colliding entities at the beginning of the timestep.*/
   
    float normalLambda;
    float tangentLambda;
    float bitangentLambda;
    float stickLambda;
    float rollingFrictionImpulse;

    int bodyA;
    int bodyB;
};

// 24 bytes
struct contactMasses
{
    float normalInvMassA;
    float tangentInvMassA;
    float bitangentInvMassA;

    float normalInvMassB;
    float tangentInvMassB;
    float bitangentInvMassB;
};

float4 GetBitangent(in contact c)
{
   return normalizesafe(float4(cross(c.normal.xyz,c.tangent.xyz),0));
}

void CalculateBasis(in float4 relativeVelocity, in float4 normal, out float4 tangent)
{
    tangent = normalizesafe(relativeVelocity - dot(relativeVelocity, normal) * normal);
}

void CalculateContactMassesA(float invMass,
                             float4 inverseInertiaTensor,
                             float4 position,
                             quaternion orientation,
                             float4 contactPoint,
                             bool rollingContacts,
                             float4 normal,
                             float4 bitangent,
                             float4 tangent,
                             out float normalInvMassA,
                             out float tangentInvMassA,
                             out float bitangentInvMassA)
{
    // initialize inverse linear masses:
    normalInvMassA = tangentInvMassA = bitangentInvMassA = invMass;

    if (rollingContacts)
    {
        float4 rA = contactPoint - position;
        float4x4 solverInertiaA = TransformInertiaTensor(inverseInertiaTensor, orientation);

        normalInvMassA += RotationalInvMass(solverInertiaA, rA, normal);
        tangentInvMassA += RotationalInvMass(solverInertiaA, rA, tangent);
        bitangentInvMassA += RotationalInvMass(solverInertiaA, rA, bitangent);
    }
}

void CalculateContactMassesB(float invMass,
                            float4 inverseInertiaTensor,
                            float4 position,
                            quaternion orientation,
                            float4 contactPoint,
                            bool rollingContacts,
                            float4 normal,
                            float4 bitangent,
                            float4 tangent,
                            out float normalInvMassB,
                            out float tangentInvMassB,
                            out float bitangentInvMassB)
{
    // initialize inverse linear masses:
    normalInvMassB = tangentInvMassB = bitangentInvMassB = invMass;

    if (rollingContacts)
    {
        float4 rB = contactPoint - position;
        float4x4 solverInertiaB = TransformInertiaTensor(inverseInertiaTensor, orientation);

        normalInvMassB += RotationalInvMass(solverInertiaB, rB, normal);
        tangentInvMassB += RotationalInvMass(solverInertiaB, rB, tangent);
        bitangentInvMassB += RotationalInvMass(solverInertiaB, rB, bitangent);
    }
}

void ClearContactMasses(out float normalInvMass,
                        out float tangentInvMass,
                        out float bitangentInvMass)
{
    normalInvMass = tangentInvMass = bitangentInvMass = 0;
}

float SolveAdhesion(inout contact c, float normalMass, float4 posA, float4 posB, float stickDistance, float stickiness, float dt)
{
    float lambdaChange = 0;

    if (normalMass > 0 && stickDistance > 0 && stickiness > 0 && dt > 0)
    {
        c.dist = dot(posA - posB, c.normal);

        // calculate stickiness position correction:
        float constraint = stickiness * (1 - max(c.dist / stickDistance, 0)) * dt;

        // calculate lambda multiplier:
        float dlambda = -constraint / normalMass;

        // accumulate lambda:
        float newStickinessLambda = min(c.stickLambda + dlambda, 0);

        // calculate lambda change and update accumulated lambda:
        lambdaChange = newStickinessLambda - c.stickLambda;
        c.stickLambda = newStickinessLambda;
    }

    return lambdaChange;
}

float SolvePenetration(inout contact c, float normalMass, float4 posA, float4 posB, float maxDepenetrationDelta)
{
    float lambdaChange = 0;

    if (normalMass > 0)
    {
        //project position delta to normal vector:
        c.dist = dot(posA - posB, c.normal);

        // calculate max projection distance based on depenetration velocity:
        float maxProjection = max(-c.dist - maxDepenetrationDelta, 0);

        // calculate lambda multiplier:
        float dlambda = -(c.dist  + maxProjection) / normalMass;

        // accumulate lambda:
        float newLambda = max(c.normalLambda + dlambda, 0);

        // calculate lambda change and update accumulated lambda:
        lambdaChange = newLambda - c.normalLambda;
        c.normalLambda = newLambda;
    }

    return lambdaChange;
}

float2 SolveFriction(inout contact c, float tangentMass, float bitangentMass, float4 relativeVelocity, float staticFriction, float dynamicFriction, float dt)
{
    float2 lambdaChange = float2(0,0);

    if (tangentMass > 0 && bitangentMass > 0 &&
        (dynamicFriction > 0 || staticFriction > 0) && (c.normalLambda > 0 /*|| stickLambda > 0*/))
    {
        // calculate delta projection on both friction axis:
        float tangentPosDelta = dot(relativeVelocity, c.tangent);
        float bitangentPosDelta = dot(relativeVelocity, GetBitangent(c));

        // calculate friction pyramid limit:
        float dynamicFrictionCone = c.normalLambda / dt * dynamicFriction;
        float staticFrictionCone  = c.normalLambda / dt * staticFriction;

        // tangent impulse:
        float tangentLambdaDelta = -tangentPosDelta / tangentMass; 
        float newTangentLambda = c.tangentLambda + tangentLambdaDelta;

        if (abs(newTangentLambda) > staticFrictionCone)
            newTangentLambda = clamp(newTangentLambda, -dynamicFrictionCone, dynamicFrictionCone);

        lambdaChange[0] = newTangentLambda - c.tangentLambda;
        c.tangentLambda = newTangentLambda;

        // bitangent impulse:
        float bitangentLambdaDelta = -bitangentPosDelta / bitangentMass;
        float newBitangentLambda = c.bitangentLambda + bitangentLambdaDelta;

        if (abs(newBitangentLambda) > staticFrictionCone)
            newBitangentLambda = clamp(newBitangentLambda, -dynamicFrictionCone, dynamicFrictionCone);

        lambdaChange[1] = newBitangentLambda - c.bitangentLambda;
        c.bitangentLambda = newBitangentLambda;
    }

    return lambdaChange;
}

float SolveRollingFriction(inout contact c,
                           float4 angularVelocityA,
                           float4 angularVelocityB,
                           float rollingFriction,
                           float invMassA,
                           float invMassB,
                           inout float4 rolling_axis)
{
    float rolling_impulse_change = 0;
    float totalInvMass = invMassA + invMassB;

    if (totalInvMass > 0)
    {
        rolling_axis = normalizesafe(angularVelocityA - angularVelocityB);

        float vel1 = dot(angularVelocityA,rolling_axis);
        float vel2 = dot(angularVelocityB,rolling_axis);

        float relativeVelocity = vel1 - vel2;

        float maxImpulse = c.normalLambda * rollingFriction;
        float newRollingImpulse = clamp(c.rollingFrictionImpulse - relativeVelocity / totalInvMass, -maxImpulse, maxImpulse);
        rolling_impulse_change = newRollingImpulse - c.rollingFrictionImpulse;
        c.rollingFrictionImpulse = newRollingImpulse;
    }

    return rolling_impulse_change;
}

#endif