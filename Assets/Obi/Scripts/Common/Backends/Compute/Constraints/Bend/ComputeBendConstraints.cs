using System;
using UnityEngine;

namespace Obi
{
    public class ComputeBendConstraints : ComputeConstraintsImpl<ComputeBendConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int projectKernel;
        public int applyKernel;

        public ComputeBendConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Bending)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/BendConstraints"));
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeBendConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeBendConstraintsBatch);
            batch.Destroy();
        }
    }
}
