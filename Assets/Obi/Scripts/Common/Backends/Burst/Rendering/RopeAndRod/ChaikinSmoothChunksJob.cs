#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    [BurstCompile]
    struct ChaikinSmoothChunksJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<BurstPathFrame> inputFrames;
        [ReadOnly] public NativeArray<int> inputFrameOffsets;
        [ReadOnly] public NativeArray<int> inputFrameCounts;

        [NativeDisableParallelForRestriction] public NativeArray<BurstPathFrame> outputFrames;
        [ReadOnly] public NativeArray<int> outputFrameOffsets;
        [NativeDisableParallelForRestriction] public NativeArray<int> outputFrameCounts;

        [NativeDisableParallelForRestriction] public NativeArray<BurstPathSmootherData> pathData;

        public void Execute(int i)
        {
            int firstInputIndex = i > 0 ? inputFrameOffsets[i - 1] : 0;
            int inputFrameCount = inputFrameCounts[i];

            int firstOutputIndex = outputFrameOffsets[i];

            int k = (int)pathData[i].smoothing;

            // No work to do. just copy the input to the output:
            if (k == 0)
            {
                outputFrameCounts[i] = inputFrameCount;
                for (int j = 0; j < inputFrameCount; ++j)
                    outputFrames[firstOutputIndex + j] = inputFrames[firstInputIndex + j];
            }
            else
            {
                // precalculate some quantities:
                int pCount = (int)math.pow(2, k);
                int n0 = inputFrameCount - 1;
                float twoRaisedToMinusKPlus1 = math.pow(2, -(k + 1));
                float twoRaisedToMinusK = math.pow(2, -k);
                float twoRaisedToMinus2K = math.pow(2, -2 * k);
                float twoRaisedToMinus2KMinus1 = math.pow(2, -2 * k - 1);

                outputFrameCounts[i] = (inputFrameCount - 2) * pCount + 2;

                // calculate initial curve points:
                outputFrames[firstOutputIndex] = (0.5f + twoRaisedToMinusKPlus1) * inputFrames[firstInputIndex] + (0.5f - twoRaisedToMinusKPlus1) * inputFrames[firstInputIndex + 1];
                outputFrames[firstOutputIndex + pCount * n0 - pCount + 1] = (0.5f - twoRaisedToMinusKPlus1) * inputFrames[firstInputIndex + n0 - 1] + (0.5f + twoRaisedToMinusKPlus1) * inputFrames[firstInputIndex + n0];

                // calculate internal points:
                for (int j = 1; j <= pCount; ++j)
                {
                    // precalculate coefficients:
                    float F = 0.5f - twoRaisedToMinusKPlus1 - (j - 1) * (twoRaisedToMinusK - j * twoRaisedToMinus2KMinus1);
                    float G = 0.5f + twoRaisedToMinusKPlus1 + (j - 1) * (twoRaisedToMinusK - j * twoRaisedToMinus2K);
                    float H = (j - 1) * j * twoRaisedToMinus2KMinus1;

                    for (int l = 1; l < n0; ++l)
                    {
                        BurstPathFrame.WeightedSum(F, G, H,
                                                 in GetElementAsRef(inputFrames, firstInputIndex + l - 1),
                                                 in GetElementAsRef(inputFrames, firstInputIndex + l),
                                                 in GetElementAsRef(inputFrames, firstInputIndex + l + 1),
                                                 ref GetElementAsRef(outputFrames, firstOutputIndex + (l - 1) * pCount + j));
                    }
                }

                // make first and last curve points coincide with original points:
                outputFrames[firstOutputIndex] = inputFrames[firstInputIndex];
                outputFrames[firstOutputIndex + outputFrameCounts[i] - 1] = inputFrames[firstInputIndex + inputFrameCount - 1];
            }

            var data = pathData[i];
            data.smoothLength = 0;
            for (int j = firstOutputIndex + 1; j < firstOutputIndex + outputFrameCounts[i]; ++j)
                data.smoothLength += math.distance(outputFrames[j - 1].position, outputFrames[j].position);

            pathData[i] = data;

        }

        private static unsafe ref T GetElementAsRef<T>(NativeArray<T> array, int index) where T : unmanaged
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
        }
    }
}
#endif