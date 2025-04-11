#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using System;
using System.Collections;

namespace Obi
{
    [BurstCompile]
    struct UpdatePositionsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> activeParticles;

        // linear/position properties:
        [NativeDisableParallelForRestriction] public NativeArray<float4> positions;
        [ReadOnly] public NativeArray<float4> previousPositions;
        [NativeDisableParallelForRestriction] public NativeArray<float4> velocities;

        // angular/orientation properties:
        [NativeDisableParallelForRestriction] public NativeArray<quaternion> orientations;
        [ReadOnly] public NativeArray<quaternion> previousOrientations;
        [NativeDisableParallelForRestriction] public NativeArray<float4> angularVelocities;

        [ReadOnly] public float velocityScale;
        [ReadOnly] public float sleepThreshold;
        [ReadOnly] public float maxVelocity;
        [ReadOnly] public float maxAngularVelocity;

        // The code actually running on the job
        public void Execute(int index)
        {
            int i = activeParticles[index];

            float4 velocity = velocities[i];
            float4 angVelocity = angularVelocities[i];

            // damp velocities:
            velocity *= velocityScale;
            angVelocity.xyz *= velocityScale;

            // clamp velocities:
            float velMagnitude = math.length(velocity);
            float angularVelMagnitude = math.length(angVelocity.xyz);

            if (velMagnitude > BurstMath.epsilon)
                velocity *= math.min(maxVelocity, velMagnitude) / velMagnitude;

            if (angularVelMagnitude > BurstMath.epsilon)
                angVelocity.xyz *= math.min(maxAngularVelocity, angularVelMagnitude) / angularVelMagnitude;

            // if the kinetic energy is below the sleep threshold, keep the particle at its previous position.
            if (velMagnitude * velMagnitude * 0.5f + angularVelMagnitude * angularVelMagnitude * 0.5f <= sleepThreshold)
            {
                positions[i] = previousPositions[i];
                orientations[i] = previousOrientations[i];
                velocity = float4.zero;
                angVelocity.xyz = float3.zero;
            }

            velocities[i] = velocity;
            angularVelocities[i] = angVelocity;
        }
    }
}
#endif