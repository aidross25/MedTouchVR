using UnityEngine;

namespace Obi
{
    public class ComputeDistanceConstraintsBatch : ComputeConstraintsBatchImpl, IDistanceConstraintsBatchImpl
    {

        GraphicsBuffer restLengthsBuffer;
        GraphicsBuffer stiffnessesBuffer;

        public ComputeDistanceConstraintsBatch(ComputeDistanceConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Distance;
        }

        public void SetDistanceConstraints(ObiNativeIntList particleIndices, ObiNativeFloatList restLengths, ObiNativeVector2List stiffnesses, ObiNativeFloatList lambdas, int count)
        {
            this.particleIndices = particleIndices.AsComputeBuffer<int>();
            this.restLengthsBuffer = restLengths.AsComputeBuffer<float>();
            this.stiffnessesBuffer = stiffnesses.AsComputeBuffer<Vector2>();
            this.lambdas = lambdas.AsComputeBuffer<float>();
            this.lambdasList = lambdas;

            m_ConstraintCount = count;
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (m_ConstraintCount > 0)
            {
                var shader = ((ComputeDistanceConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputeDistanceConstraints)m_Constraints).projectKernel;

                shader.SetBuffer(projectKernel, "particleIndices", particleIndices);
                shader.SetBuffer(projectKernel, "restLengths", restLengthsBuffer);
                shader.SetBuffer(projectKernel, "stiffnesses", stiffnessesBuffer);
                shader.SetBuffer(projectKernel, "lambdas", lambdas);

                shader.SetBuffer(projectKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(projectKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(projectKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetFloat("deltaTime", substepTime);

                int threadGroups = ComputeMath.ThreadGroupCount(m_ConstraintCount, 128);
                shader.Dispatch(projectKernel, threadGroups, 1, 1);
            }
        }

        public override void Apply(float deltaTime)
        {
            if (m_ConstraintCount > 0)
            {
                var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

                var shader = ((ComputeDistanceConstraints)m_Constraints).constraintsShader;
                int applyKernel = ((ComputeDistanceConstraints)m_Constraints).applyKernel;

                shader.SetBuffer(applyKernel, "particleIndices", particleIndices);
                shader.SetBuffer(applyKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(applyKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(applyKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetFloat("sorFactor", parameters.SORFactor);

                int threadGroups = ComputeMath.ThreadGroupCount(m_ConstraintCount, 128);
                shader.Dispatch(applyKernel, threadGroups, 1, 1);
            }
        }

        public void RequestDataReadback()
        {
            lambdasList.Readback();
        }

        public void WaitForReadback()
        {
            lambdasList.WaitForReadback();
        }
    }
}