#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    [BurstCompile]
    struct ParallelTransportJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<BurstPathFrame> pathFrames;
        [ReadOnly] public NativeArray<int> frameOffsets;
        [ReadOnly] public NativeArray<int> particleIndices;

        [ReadOnly] public NativeArray<float4> renderablePositions;
        [ReadOnly] public NativeArray<quaternion> renderableOrientations;
        [ReadOnly] public NativeArray<float4> principalRadii;
        [ReadOnly] public NativeArray<float4> colors;
        [ReadOnly] public NativeArray<BurstPathSmootherData> pathData;

        public void Execute(int i)
        {
            BurstPathFrame nextFrame = new BurstPathFrame(); 
            BurstPathFrame currFrame = new BurstPathFrame(); 
            BurstPathFrame prevFrame = new BurstPathFrame();

            nextFrame.Reset();
            currFrame.Reset();
            prevFrame.Reset();

            int firstIndex = i > 0 ? frameOffsets[i - 1] : 0;
            int frameCount = frameOffsets[i] - firstIndex;

            // initialize current and previous frame:
            PathFrameFromParticle(ref currFrame, particleIndices[firstIndex], pathData[i].usesOrientedParticles == 1, false);
            prevFrame = currFrame;

            // parallel transport:
            for (int m = 1; m <= frameCount; ++m)
            {
                int index = firstIndex + math.min(m, frameCount - 1);
                int pIndex = particleIndices[index];

                // generate curve frame from particle:
                PathFrameFromParticle(ref nextFrame, pIndex, pathData[i].usesOrientedParticles == 1);

                if (pathData[i].usesOrientedParticles == 1)
                {
                    // copy frame directly.
                    prevFrame = currFrame;
                }
                else
                {
                    // perform parallel transport, using forward / backward average to calculate tangent.
                    // if the average is too small, reuse the previous frame tangent.
                    currFrame.tangent = math.normalizesafe((currFrame.position - prevFrame.position) +
                                                           (nextFrame.position - currFrame.position), prevFrame.tangent);
                    prevFrame.Transport(currFrame, pathData[i].twist);
                }

                // advance current frame:
                currFrame = nextFrame;
                pathFrames[firstIndex + m - 1] = prevFrame;
            }

        }

        private void PathFrameFromParticle(ref BurstPathFrame frame, int particleIndex, bool useOrientedParticles, bool interpolateOrientation = false)
        {
            // Update current frame values from particles:
            frame.position = renderablePositions[particleIndex].xyz;
            frame.thickness = principalRadii[particleIndex][0];
            frame.color = colors[particleIndex];

            // Use particle orientation if possible:
            if (useOrientedParticles)
            {
                quaternion current = renderableOrientations[particleIndex];
                quaternion previous = renderableOrientations[math.max(0, particleIndex - 1)];
                float4x4 average = (interpolateOrientation ? math.slerp(current, previous, 0.5f) : current).toMatrix();
                frame.normal = average.c1.xyz;
                frame.binormal = average.c0.xyz;
                frame.tangent = average.c2.xyz;
            }
        }
    }
}
#endif