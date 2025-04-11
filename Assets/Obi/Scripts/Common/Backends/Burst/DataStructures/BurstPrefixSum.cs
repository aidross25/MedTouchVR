#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    public class BurstPrefixSum
    {
        private int inputSize;
        private const int numBlocks = 8;

        private NativeArray<int> blockSums;

        public BurstPrefixSum(int inputSize)
        {
            this.inputSize = inputSize;
            blockSums = new NativeArray<int>(numBlocks, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (blockSums.IsCreated)
                blockSums.Dispose();
        }

        public unsafe JobHandle Sum(NativeArray<int> input, NativeArray<int> result, int* count, JobHandle inputDeps)
        {

            // calculate partial prefix sums, one per block:
            var job = new BlockSumJob
            {
                input = input,
                output = result,
                blocks = blockSums,
                count = count
            };
            inputDeps = job.Schedule(numBlocks, 1, inputDeps);

            var job3 = new BlockSum
            {
                blocks = blockSums
            };
            inputDeps = job3.Schedule(inputDeps);

            // add the scanned partial block sums to the result:
            var job2 = new PrefixSumJob
            {
                prefixBlocks = blockSums,
                output = result,
                count = count
            };
            return job2.Schedule(numBlocks, 1, inputDeps);
        }

        [BurstCompile]
        unsafe struct BlockSumJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> input;
            [NativeDisableParallelForRestriction] public NativeArray<int> output;
            public NativeArray<int> blocks;

            [ReadOnly] [NativeDisableUnsafePtrRestriction] public int* count;

            public void Execute(int block)
            {
                int length = *count + 1; // add 1 to get total sum in last element+1
                int blockSize = (int)math.ceil(length / (float)numBlocks);

                int start = block * blockSize;
                int end = math.min(start + blockSize, length);

                output[start] = 0;

                if (blockSize == 0) { blocks[block] = 0; return; }

                for (int i = start + 1; i < end; ++i)
                    output[i] = output[i - 1] + input[i - 1];

                blocks[block] = output[end - 1] + input[end - 1];
            }
        }

        [BurstCompile]
        struct BlockSum : IJob
        {
            public NativeArray<int> blocks;

            public void Execute()
            {
                int aux = blocks[0];
                blocks[0] = 0;

                for (int i = 1; i < blocks.Length; ++i)
                {
                    int a = blocks[i];
                    blocks[i] = blocks[i - 1] + aux;
                    aux = a;
                }
            }
        }

        [BurstCompile]
        unsafe struct PrefixSumJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> prefixBlocks;
            [NativeDisableParallelForRestriction] public NativeArray<int> output;

            [ReadOnly] [NativeDisableUnsafePtrRestriction] public int* count;

            public void Execute(int block)
            {
                int length = *count + 1; // add 1 to get total sum in last element+1
                int blockSize = (int)math.ceil(length / (float)numBlocks);

                int start = block * blockSize;
                int end = math.min(start + blockSize, length);

                for (int i = start; i < end; ++i)
                    output[i] += prefixBlocks[block];
            }
        }
    }
}
#endif