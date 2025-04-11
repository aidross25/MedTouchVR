using System;
using UnityEngine;

namespace Obi
{
    public class ComputeStretchShearConstraints : ComputeConstraintsImpl<ComputeStretchShearConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int projectKernel;
        public int applyKernel;

        public ComputeStretchShearConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.StretchShear)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/StretchShearConstraints"));
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeStretchShearConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeStretchShearConstraintsBatch);
            batch.Destroy();
        }
    }
}
