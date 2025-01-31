#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;

namespace Obi
{
    [BurstCompile]
    struct InterpolationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> positions;
        [ReadOnly] public NativeArray<float4> startPositions;
        [ReadOnly] public NativeArray<float4> endPositions;
        [WriteOnly] public NativeArray<float4> renderablePositions;

        [ReadOnly] public NativeArray<quaternion> orientations;
        [ReadOnly] public NativeArray<quaternion> startOrientations;
        [ReadOnly] public NativeArray<quaternion> endOrientations;
        [WriteOnly] public NativeArray<quaternion> renderableOrientations;

        [ReadOnly] public NativeArray<float4> principalRadii;
        [WriteOnly] public NativeArray<float4> renderableRadii;

        [ReadOnly] public float blendFactor;
        [ReadOnly] public Oni.SolverParameters.Interpolation interpolationMode;

        // The code actually running on the job
        public void Execute(int i)
        {
            if (interpolationMode == Oni.SolverParameters.Interpolation.Interpolate)
            {
                renderablePositions[i] = math.lerp(startPositions[i], endPositions[i], blendFactor);
                renderableOrientations[i] = math.normalize(math.slerp(startOrientations[i], endOrientations[i], blendFactor));
                renderableRadii[i] = principalRadii[i];
            }
            else if (interpolationMode == Oni.SolverParameters.Interpolation.Extrapolate)
            {
                renderablePositions[i] = math.lerp(endPositions[i], positions[i], blendFactor);
                renderableOrientations[i] = math.normalize(math.slerp(endOrientations[i], orientations[i], blendFactor));
                renderableRadii[i] = principalRadii[i];
            }
            else
            {
                renderablePositions[i] = endPositions[i];
                renderableOrientations[i] = math.normalize(endOrientations[i]);
                renderableRadii[i] = principalRadii[i];
            }
        }
    }
}
#endif