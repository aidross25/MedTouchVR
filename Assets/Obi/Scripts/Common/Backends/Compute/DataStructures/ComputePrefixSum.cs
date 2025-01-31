using System.Collections;
using System.Collections.Generic;
using Obi;
using UnityEngine;

namespace Obi
{
    public class ComputePrefixSum
    {
        private ComputeShader scanShader;
        private int scanInBucketKernel;
        private int scanAddBucketResult;

        private List<GraphicsBuffer> blockSums = new List<GraphicsBuffer>();
        private List<GraphicsBuffer> prefixBlockSums = new List<GraphicsBuffer>();
        private int inputSize;
        private const int threadsPerGroup = 512;

        public ComputePrefixSum(int inputSize)
        {
            scanShader = Resources.Load<ComputeShader>("Compute/Scan");
            scanInBucketKernel = scanShader.FindKernel("ScanInBucketExclusive");
            scanAddBucketResult = scanShader.FindKernel("ScanAddBucketResult");

            this.inputSize = inputSize;

            // intermediate auxiliary buffers to store prefix sum of partial block sums:
            int c = inputSize;
            while (c > 1)
            {
                c = (c + threadsPerGroup - 1) / threadsPerGroup;
                blockSums.Add(new GraphicsBuffer(GraphicsBuffer.Target.Structured, c, 4));
                prefixBlockSums.Add(new GraphicsBuffer(GraphicsBuffer.Target.Structured, c, 4));
            }
        }

        public void Dispose()
        {
            foreach (var sums in blockSums)
                if (sums != null && sums.IsValid())
                    sums.Dispose();
            blockSums.Clear();

            foreach (var sums in prefixBlockSums)
                if (sums != null && sums.IsValid())
                    sums.Dispose();
            prefixBlockSums.Clear();
        }

        public void Sum(GraphicsBuffer input, GraphicsBuffer result)
        {
            if (input.count != inputSize)
                return;
            Sum(input, result, input.count, 0);
        }

        private void Sum(GraphicsBuffer input, GraphicsBuffer result, int count, int level)
        {
            int groups = (count + threadsPerGroup - 1) / threadsPerGroup;

            // calculate partial prefix sums, one per block:
            scanShader.SetInt("count", count);
            scanShader.SetBuffer(scanInBucketKernel, "_Input", input);
            scanShader.SetBuffer(scanInBucketKernel, "_Result", result);
            scanShader.SetBuffer(scanInBucketKernel, "_BlockSum", blockSums[level]);
            scanShader.Dispatch(scanInBucketKernel, groups, 1, 1);

            if (groups <= 1)
                return;

            // recursively calculate prefix sum of the partial block sums:
            Sum(blockSums[level], prefixBlockSums[level], groups, level + 1);

            // add the scanned partial block sums to the result:
            // (it's important to set the count again, as we just returned from a recursive call).
            scanShader.SetInt("count", count);
            scanShader.SetBuffer(scanAddBucketResult, "_Input", prefixBlockSums[level]);
            scanShader.SetBuffer(scanAddBucketResult, "_Result", result);
            scanShader.Dispatch(scanAddBucketResult, groups, 1, 1);
        }
    }
}