using System;
using UnityEngine;

namespace Obi
{
    public class ComputeParticleCollisionConstraints : ComputeConstraintsImpl<ComputeParticleCollisionConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int initializeKernel;
        public int projectKernel;
        public int applyKernel;

        public ComputeParticleCollisionConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.ParticleCollision)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/ParticleCollisionConstraints"));
            initializeKernel = constraintsShader.FindKernel("Initialize");
            projectKernel = constraintsShader.FindKernel("Project");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeParticleCollisionConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeParticleCollisionConstraintsBatch);
            batch.Destroy();
        }
    }
}
