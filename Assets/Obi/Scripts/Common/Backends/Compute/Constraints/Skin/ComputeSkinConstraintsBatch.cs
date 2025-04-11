using UnityEngine;

namespace Obi
{
    public class ComputeSkinConstraintsBatch : ComputeConstraintsBatchImpl, ISkinConstraintsBatchImpl
    {
        GraphicsBuffer skinPoints;
        GraphicsBuffer skinNormalsBuffer;
        GraphicsBuffer skinRadiiBackstopBuffer;
        GraphicsBuffer skinComplianceBuffer;

        public ComputeSkinConstraintsBatch(ComputeSkinConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Skin;
        }

        public void SetSkinConstraints(ObiNativeIntList particleIndices, ObiNativeVector4List skinPoints, ObiNativeVector4List skinNormals, ObiNativeFloatList skinRadiiBackstop, ObiNativeFloatList skinCompliance, ObiNativeFloatList lambdas, int count)
        {
            this.particleIndices = particleIndices.AsComputeBuffer<int>();
            this.skinPoints = skinPoints.AsComputeBuffer<Vector4>();
            this.skinNormalsBuffer = skinNormals.AsComputeBuffer<Vector4>();
            this.skinRadiiBackstopBuffer = skinRadiiBackstop.AsComputeBuffer<float>();
            this.skinComplianceBuffer = skinCompliance.AsComputeBuffer<float>();
            this.lambdas = lambdas.AsComputeBuffer<float>();
            this.lambdasList = lambdas;

            m_ConstraintCount = count;
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (m_ConstraintCount > 0)
            {
                var shader = ((ComputeSkinConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputeSkinConstraints)m_Constraints).projectKernel;

                shader.SetBuffer(projectKernel, "particleIndices", particleIndices);
                shader.SetBuffer(projectKernel, "skinPoints", skinPoints);
                shader.SetBuffer(projectKernel, "skinNormals", skinNormalsBuffer);
                shader.SetBuffer(projectKernel, "skinRadiiBackstop", skinRadiiBackstopBuffer);
                shader.SetBuffer(projectKernel, "skinCompliance", skinComplianceBuffer);
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

        public override void Apply(float substepTime)
        {
            if (m_ConstraintCount > 0)
            {
                var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

                var shader = ((ComputeSkinConstraints)m_Constraints).constraintsShader;
                int applyKernel = ((ComputeSkinConstraints)m_Constraints).applyKernel;

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

    }
}