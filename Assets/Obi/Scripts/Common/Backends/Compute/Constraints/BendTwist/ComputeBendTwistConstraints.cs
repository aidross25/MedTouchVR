using System;
using UnityEngine;

namespace Obi
{
    public class ComputeBendTwistConstraints : ComputeConstraintsImpl<ComputeBendTwistConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int projectKernel;
        public int applyKernel;

        public ComputeBendTwistConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.BendTwist)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/BendTwistConstraints"));
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeBendTwistConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeBendTwistConstraintsBatch);
            batch.Destroy();
        }
    }
}
