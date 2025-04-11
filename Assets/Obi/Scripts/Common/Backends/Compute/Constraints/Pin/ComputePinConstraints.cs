using System;
using UnityEngine;

namespace Obi
{
    public class ComputePinConstraints : ComputeConstraintsImpl<ComputePinConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int clearKernel;
        public int initializeKernel;
        public int projectKernel;
        public int applyKernel;

        public ComputePinConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Pin)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/PinConstraints"));
            clearKernel = constraintsShader.FindKernel("Clear");
            initializeKernel = constraintsShader.FindKernel("Initialize");
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputePinConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputePinConstraintsBatch);
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
