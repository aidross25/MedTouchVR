using System;
using UnityEngine;

namespace Obi
{
    public class ComputeColliderFrictionConstraints : ComputeConstraintsImpl<ComputeColliderFrictionConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int projectKernel;
        public int applyKernel;

        public ComputeColliderFrictionConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Friction)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/ColliderFrictionConstraints"));
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeColliderFrictionConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeColliderFrictionConstraintsBatch);
            batch.Destroy();
        }
    }
}
