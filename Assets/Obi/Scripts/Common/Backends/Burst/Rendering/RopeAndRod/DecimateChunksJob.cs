#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    [BurstCompile]
    struct DecimateChunksJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> inputFrameOffsets;
        [NativeDisableParallelForRestriction] public NativeArray<BurstPathFrame> inputFrames;
        [NativeDisableParallelForRestriction] public NativeArray<int> outputFrameCounts;

        [ReadOnly] public NativeArray<BurstPathSmootherData> pathData;

        public void Execute(int i)
        {

            int firstInputIndex = i > 0 ? inputFrameOffsets[i - 1] : 0;
            int inputFrameCount = inputFrameOffsets[i] - firstInputIndex;

            // no decimation, no work to do, just return:
            if (pathData[i].decimation < 0.00001f || inputFrameCount < 3)
            {
                outputFrameCounts[i] = inputFrameCount;
                return;
            }

            float scaledThreshold = pathData[i].decimation * pathData[i].decimation * 0.01f;

            int start = 0;
            int end = inputFrameCount - 1;
            outputFrameCounts[i] = 0;

            while (start < end)
            {
                // add starting point:
                inputFrames[firstInputIndex + outputFrameCounts[i]++] = inputFrames[firstInputIndex + start];

                var newEnd = end;

                while (true)
                {
                    int maxDistanceIndex = 0;
                    float maxDistance = 0;

                    // find the point that's furthest away from the current segment:
                    for (int k = start + 1; k < newEnd; k++)
                    {
                        var nearest = BurstMath.NearestPointOnEdge(inputFrames[firstInputIndex + start].position,
                                                                   inputFrames[firstInputIndex + newEnd].position,
                                                                   inputFrames[firstInputIndex + k].position, out _);
                        float d = math.lengthsq(nearest - inputFrames[firstInputIndex + k].position);

                        if (d > maxDistance)
                        {
                            maxDistanceIndex = k;
                            maxDistance = d;
                        }
                    }

                    if (maxDistance <= scaledThreshold)
                        break;

                    newEnd = maxDistanceIndex;
                }

                start = newEnd;
            }

            // add the last point:
            inputFrames[firstInputIndex + outputFrameCounts[i]++] = inputFrames[firstInputIndex + end];

        }
    }
}
#endif