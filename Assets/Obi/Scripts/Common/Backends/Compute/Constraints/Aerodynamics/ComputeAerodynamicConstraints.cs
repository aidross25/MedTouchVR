using System;
using UnityEngine;

namespace Obi
{
    public class ComputeAerodynamicConstraints : ComputeConstraintsImpl<ComputeAerodynamicConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int projectKernel;

        public ComputeAerodynamicConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Aerodynamics)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/AerodynamicConstraints"));
            projectKernel = constraintsShader.FindKernel("Project");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeAerodynamicConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeAerodynamicConstraintsBatch);
            batch.Destroy();
        }
    }
}
