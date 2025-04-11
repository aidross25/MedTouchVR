using UnityEngine;

namespace Obi
{
    public class ComputeStretchShearConstraintsBatch : ComputeConstraintsBatchImpl, IStretchShearConstraintsBatchImpl
    {
        GraphicsBuffer orientationIndices;
        GraphicsBuffer restLengths;
        GraphicsBuffer restOrientations;
        GraphicsBuffer stiffnesses;

        public ComputeStretchShearConstraintsBatch(ComputeStretchShearConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.StretchShear;
        }

        public void SetStretchShearConstraints(ObiNativeIntList particleIndices, ObiNativeIntList orientationIndices, ObiNativeFloatList restLengths, ObiNativeQuaternionList restOrientations, ObiNativeVector3List stiffnesses, ObiNativeFloatList lambdas, int count)
        {
            this.particleIndices = particleIndices.AsComputeBuffer<int>();
            this.orientationIndices = orientationIndices.AsComputeBuffer<int>();
            this.restLengths = restLengths.AsComputeBuffer<float>();
            this.restOrientations = restOrientations.AsComputeBuffer<Quaternion>();
            this.stiffnesses = stiffnesses.AsComputeBuffer<Vector3>();
            this.lambdas = lambdas.AsComputeBuffer<float>();
            this.lambdasList = lambdas;

            m_ConstraintCount = count;
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (m_ConstraintCount > 0)
            {
                var shader = ((ComputeStretchShearConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputeStretchShearConstraints)m_Constraints).projectKernel;

                shader.SetBuffer(projectKernel, "particleIndices", particleIndices);
                shader.SetBuffer(projectKernel, "orientationIndices", orientationIndices);
                shader.SetBuffer(projectKernel, "restLengths", restLengths);
                shader.SetBuffer(projectKernel, "restOrientations", restOrientations);
                shader.SetBuffer(projectKernel, "stiffnesses", stiffnesses);
                shader.SetBuffer(projectKernel, "lambdas", lambdas);

                shader.SetBuffer(projectKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(projectKernel, "orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(projectKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(projectKernel, "invRotationalMasses", solverImplementation.invRotationalMassesBuffer);

                shader.SetBuffer(projectKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "orientationDeltasAsInt", solverImplementation.orientationDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(projectKernel, "orientationConstraintCounts", solverImplementation.orientationConstraintCountBuffer);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetFloat("deltaTime", substepTime);

                int threadGroups = ComputeMath.ThreadGroupCount(m_ConstraintCount, 128);
                shader.Dispatch(projectKernel, threadGroups, 1, 1);
            }
        }

        public override void Apply(float substepTime)
        {
            if (m_ConstraintCount > 0)
            {
                var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

                var shader = ((ComputeStretchShearConstraints)m_Constraints).constraintsShader;
                int applyKernel = ((ComputeStretchShearConstraints)m_Constraints).applyKernel;

                shader.SetBuffer(applyKernel, "particleIndices", particleIndices);
                shader.SetBuffer(applyKernel, "orientationIndices", orientationIndices);
                shader.SetBuffer(applyKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(applyKernel, "orientations", solverImplementation.orientationsBuffer);

                shader.SetBuffer(applyKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(applyKernel, "orientationDeltasAsInt", solverImplementation.orientationDeltasIntBuffer);
                shader.SetBuffer(applyKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(applyKernel, "orientationConstraintCounts", solverImplementation.orientationConstraintCountBuffer);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetFloat("sorFactor", parameters.SORFactor);

                int threadGroups = ComputeMath.ThreadGroupCount(m_ConstraintCount, 128);
                shader.Dispatch(applyKernel, threadGroups, 1, 1);
            }
        }

    }
}