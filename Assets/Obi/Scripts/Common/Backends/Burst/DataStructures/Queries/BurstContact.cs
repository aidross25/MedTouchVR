#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Mathematics;

namespace Obi
{
    public struct BurstContact : IConstraint, System.IComparable<BurstContact>
    {
        public float4 pointA; // point A, expressed as simplex barycentric coords for simplices, as a solver-space position for colliders.
        public float4 pointB; // point B, expressed as simplex barycentric coords for simplices, as a solver-space position for colliders.

        public float4 normal; // contact normal on bodyB's surface.
        public float4 tangent; // contact tangent on bodyB's surface.

        public float distance;      // distance between bodyA's and bodyB's surface.

        public float normalLambda;
        public float tangentLambda;
        public float bitangentLambda;
        public float stickLambda;
        public float rollingFrictionImpulse;

        public int bodyA;
        public int bodyB;

        public int GetParticleCount() { return 2; }
        public int GetParticle(int index) { return index == 0 ? bodyA : bodyB; }

        public float4 bitangent => math.normalizesafe(new float4(math.cross(normal.xyz, tangent.xyz), 0)); 
        
        public override string ToString()
        {
            return bodyA + "," + bodyB;
        }

        public int CompareTo(BurstContact other)
        {
            int first = bodyA.CompareTo(other.bodyA);
            if (first == 0)
                return bodyB.CompareTo(other.bodyB);
            return first;
        }

        public void CalculateTangent(float4 relativeVelocity)
        {
            tangent = math.normalizesafe(relativeVelocity - math.dot(relativeVelocity, normal) * normal);
        }

        public float SolveAdhesion(float normalMass, float4 posA, float4 posB, float stickDistance, float stickiness, float dt)
        {

            if (normalMass <= 0 || stickDistance <= 0 || stickiness <= 0 || dt <= 0)
                return 0;

            distance = math.dot(posA - posB, normal);

            // calculate stickiness position correction:
            float constraint = stickiness * (1 - math.max(distance / stickDistance, 0)) * dt;

            // calculate lambda multiplier:
            float dlambda = -constraint / normalMass;

            // accumulate lambda:
            float newStickinessLambda = math.min(stickLambda + dlambda, 0);

            // calculate lambda change and update accumulated lambda:
            float lambdaChange = newStickinessLambda - stickLambda;
            stickLambda = newStickinessLambda;

            return lambdaChange;
        }

        public float SolvePenetration(float normalMass, float4 posA, float4 posB, float maxDepenetrationDelta)
        {
            if (normalMass <= 0)
                return 0;

            //project position delta to normal vector:
            distance = math.dot(posA - posB, normal);

            // calculate max projection distance based on depenetration velocity:
            float maxProjection = math.max(-distance - maxDepenetrationDelta, 0);

            // calculate lambda multiplier:
            float dlambda = -(distance + maxProjection) / normalMass;

            // accumulate lambda:
            float newLambda = math.max(normalLambda + dlambda, 0);

            // calculate lambda change and update accumulated lambda:
            float lambdaChange = newLambda - normalLambda;
            normalLambda = newLambda;

            return lambdaChange;
        }

        public float2 SolveFriction(float tangentMass, float bitangentMass, float4 relativeVelocity, float staticFriction, float dynamicFriction, float dt)
        {
            float2 lambdaChange = float2.zero;

            if (tangentMass <= 0 || bitangentMass <= 0 ||
                (dynamicFriction <= 0 && staticFriction <= 0) || (normalLambda <= 0 && stickLambda <= 0))
                return lambdaChange;

            // calculate delta projection on both friction axis:
            float tangentPosDelta = math.dot(relativeVelocity, tangent);
            float bitangentPosDelta = math.dot(relativeVelocity, bitangent);

            // calculate friction pyramid limit:
            float dynamicFrictionCone = normalLambda / dt * dynamicFriction;
            float staticFrictionCone  = normalLambda / dt * staticFriction;

            // tangent impulse:
            float tangentLambdaDelta = -tangentPosDelta / tangentMass; 
            float newTangentLambda = tangentLambda + tangentLambdaDelta;

            if (math.abs(newTangentLambda) > staticFrictionCone)
                newTangentLambda = math.clamp(newTangentLambda, -dynamicFrictionCone, dynamicFrictionCone);

            lambdaChange[0] = newTangentLambda - tangentLambda;
            tangentLambda = newTangentLambda;

            // bitangent impulse:
            float bitangentLambdaDelta = -bitangentPosDelta / bitangentMass;
            float newBitangentLambda = bitangentLambda + bitangentLambdaDelta;

            if (math.abs(newBitangentLambda) > staticFrictionCone)
                newBitangentLambda = math.clamp(newBitangentLambda, -dynamicFrictionCone, dynamicFrictionCone);

            lambdaChange[1] = newBitangentLambda - bitangentLambda;
            bitangentLambda = newBitangentLambda;

            return lambdaChange;
        }


        public float SolveRollingFriction(float4 angularVelocityA,
                                          float4 angularVelocityB,
                                          float rollingFriction,
                                          float invMassA,
                                          float invMassB,
                                          ref float4 rolling_axis)
        {
            float totalInvMass = invMassA + invMassB;
            if (totalInvMass <= 0)
                return 0;
        
            rolling_axis = math.normalizesafe(angularVelocityA - angularVelocityB);

            float vel1 = math.dot(angularVelocityA,rolling_axis);
            float vel2 = math.dot(angularVelocityB,rolling_axis);

            float relativeVelocity = vel1 - vel2;

            float maxImpulse = normalLambda * rollingFriction;
            float newRollingImpulse = math.clamp(rollingFrictionImpulse - relativeVelocity / totalInvMass, -maxImpulse, maxImpulse);
            float rolling_impulse_change = newRollingImpulse - rollingFrictionImpulse;
            rollingFrictionImpulse = newRollingImpulse;
        
            return rolling_impulse_change;
        }
    }
}
#endif