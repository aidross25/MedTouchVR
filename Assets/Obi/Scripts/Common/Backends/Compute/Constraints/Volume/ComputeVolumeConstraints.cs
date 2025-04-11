using System;
using UnityEngine;

namespace Obi
{
    public class ComputeVolumeConstraints : ComputeConstraintsImpl<ComputeVolumeConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int gradientsKernel;
        public int volumeKernel;
        public int denominatorsKernel;
        public int constraintKernel;
        public int deltasKernel;
        public int applyKernel;

        public ComputeVolumeConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Volume)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/VolumeConstraints"));
            gradientsKernel = constraintsShader.FindKernel("Gradients");
            volumeKernel = constraintsShader.FindKernel("CalculateVolume");
            denominatorsKernel = constraintsShader.FindKernel("Denominators");
            constraintKernel = constraintsShader.FindKernel("Constraint");
            deltasKernel = constraintsShader.FindKernel("AccumulateDeltas");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeVolumeConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeVolumeConstraintsBatch);
            batch.Destroy();
        }
    }
}
