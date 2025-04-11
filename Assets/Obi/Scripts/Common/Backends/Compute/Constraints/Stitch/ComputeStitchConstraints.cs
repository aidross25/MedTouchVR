using System;
using UnityEngine;

namespace Obi
{
    public class ComputeStitchConstraints : ComputeConstraintsImpl<ComputeStitchConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int projectKernel;
        public int applyKernel;

        public ComputeStitchConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Stitch)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/StitchConstraints"));
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeStitchConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeStitchConstraintsBatch);
            batch.Destroy();
        }
    }
}
