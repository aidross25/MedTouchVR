using System;
using UnityEngine;

namespace Obi
{
    public class ComputeChainConstraints : ComputeConstraintsImpl<ComputeChainConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int projectKernel;
        public int applyKernel;

        public ComputeChainConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Chain)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/ChainConstraints"));
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeChainConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeChainConstraintsBatch);
            batch.Destroy();
        }
    }
}
