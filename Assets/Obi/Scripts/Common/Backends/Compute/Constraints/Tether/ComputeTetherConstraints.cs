using System;
using UnityEngine;

namespace Obi
{
    public class ComputeTetherConstraints : ComputeConstraintsImpl<ComputeTetherConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int projectKernel;
        public int applyKernel;

        public ComputeTetherConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Tether)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/TetherConstraints"));
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeTetherConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeTetherConstraintsBatch);
            batch.Destroy();
        }
    }
}
