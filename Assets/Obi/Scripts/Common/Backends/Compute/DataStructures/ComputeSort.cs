using UnityEngine;

namespace Obi
{
    public class ComputeSort
    {
        private ComputeShader sortShader;
        private int sortKernel;

        public ComputeSort()
        {
            sortShader = Resources.Load<ComputeShader>("Compute/BitonicSort");
            sortKernel = sortShader.FindKernel("BitonicSort");
        }

        public void Sort(GraphicsBuffer keys, GraphicsBuffer values)
        {
            if (keys.count != values.count)
                return;

            sortShader.SetInt("numEntries", keys.count);
            sortShader.SetBuffer(sortKernel, "Keys", keys);
            sortShader.SetBuffer(sortKernel, "Values", values);

            // Launch each step of the sorting algorithm (once the previous step is complete)
            // Number of steps = [log2(n) * (log2(n) + 1)] / 2
            // where n = nearest power of 2 that is greater or equal to the number of inputs
            int numPairs = ObiUtils.CeilToPowerOfTwo(keys.count) / 2;
            int numStages = (int)Mathf.Log(numPairs * 2, 2);
            int groups = ComputeMath.ThreadGroupCount(numPairs, 128);

            for (int stageIndex = 0; stageIndex < numStages; stageIndex++)
            {
                for (int stepIndex = 0; stepIndex < stageIndex + 1; stepIndex++)
                {
                    int groupWidth = 1 << (stageIndex - stepIndex);
                    int groupHeight = 2 * groupWidth - 1;
                    sortShader.SetInt("groupWidth", groupWidth);
                    sortShader.SetInt("groupHeight", groupHeight);
                    sortShader.SetInt("stepIndex", stepIndex);
                    sortShader.Dispatch(sortKernel, groups,1,1);
                }
            }
        }
    }
}