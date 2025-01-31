using System;
using UnityEngine;

namespace Obi
{
    public class ComputeDistanceConstraints : ComputeConstraintsImpl<ComputeDistanceConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int projectKernel;
        public int applyKernel;

        public ComputeDistanceConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Distance)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/DistanceConstraints"));
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeDistanceConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeDistanceConstraintsBatch);
            batch.Destroy();
        }

        public void RequestDataReadback()
        {
            foreach (var batch in batches)
                batch.RequestDataReadback();
        }

        public void WaitForReadback()
        {
            foreach (var batch in batches)
                batch.WaitForReadback();
        }
    }
}
