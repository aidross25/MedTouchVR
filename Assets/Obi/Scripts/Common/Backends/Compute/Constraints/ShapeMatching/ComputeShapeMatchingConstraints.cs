using System;
using UnityEngine;

namespace Obi
{
    public class ComputeShapeMatchingConstraints : ComputeConstraintsImpl<ComputeShapeMatchingConstraintsBatch>
    {
        public ComputeShader constraintsShader;
        public int projectKernel;
        public int plasticityKernel;
        public int restStateKernel;
        public int applyKernel;

        public ComputeShapeMatchingConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.ShapeMatching)
        {
            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/ShapeMatchingConstraints"));
            projectKernel = constraintsShader.FindKernel("Project");
            plasticityKernel = constraintsShader.FindKernel("PlasticDeformation");
            restStateKernel = constraintsShader.FindKernel("CalculateRestShapeMatching");
            applyKernel = constraintsShader.FindKernel("Apply");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeShapeMatchingConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeShapeMatchingConstraintsBatch);
            batch.Destroy();
        }

        public void RequestDataReadback()
        {
            foreach(var batch in batches)
                batch.RequestDataReadback();
        }

        public void WaitForReadback()
        {
            foreach (var batch in batches)
                batch.WaitForReadback();
        }
    }
}
