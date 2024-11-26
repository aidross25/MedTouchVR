using System;
using UnityEngine;

namespace Obi
{
    public class ComputeColliderCollisionConstraints : ComputeConstraintsImpl<ComputeColliderCollisionConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int clearKernel;
        public int initializeKernel;
        public int projectKernel;
        public int applyKernel;

        public ComputeColliderCollisionConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Collision)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/ColliderCollisionConstraints"));
            clearKernel = constraintsShader.FindKernel("Clear");
            initializeKernel = constraintsShader.FindKernel("Initialize");
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeColliderCollisionConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeColliderCollisionConstraintsBatch);
            batch.Destroy();
        }
    }
}
