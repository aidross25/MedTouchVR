using System;
using UnityEngine;

namespace Obi
{
    public class ComputeParticleFrictionConstraints : ComputeConstraintsImpl<ComputeParticleFrictionConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int projectKernel;
        public int applyKernel;

        public ComputeParticleFrictionConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.ParticleFriction)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/ParticleFrictionConstraints"));
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeParticleFrictionConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeParticleFrictionConstraintsBatch);
            batch.Destroy();
        }
    }
}
