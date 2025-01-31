using UnityEngine;

namespace Obi
{
    public class ComputeBendTwistConstraintsBatch : ComputeConstraintsBatchImpl, IBendTwistConstraintsBatchImpl
    {
        GraphicsBuffer orientationIndices;
        GraphicsBuffer restDarboux;
        GraphicsBuffer stiffnesses;
        GraphicsBuffer plasticity;

        public ComputeBendTwistConstraintsBatch(ComputeBendTwistConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.BendTwist;
        }

        public void SetBendTwistConstraints(ObiNativeIntList orientationIndices, ObiNativeQuaternionList restDarboux, ObiNativeVector3List stiffnesses, ObiNativeVector2List plasticity, ObiNativeFloatList lambdas, int count)
        {
            this.orientationIndices = orientationIndices.AsComputeBuffer<int>();
            this.restDarboux = restDarboux.AsComputeBuffer<Quaternion>();
            this.stiffnesses = stiffnesses.AsComputeBuffer<Vector3>();
            this.plasticity = plasticity.AsComputeBuffer<Vector2>();
            this.lambdas = lambdas.AsComputeBuffer<float>();
            this.lambdasList = lambdas;

            m_ConstraintCount = count;
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (m_ConstraintCount > 0)
            {
                var shader = ((ComputeBendTwistConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputeBendTwistConstraints)m_Constraints).projectKernel;

                shader.SetBuffer(projectKernel, "orientationIndices", orientationIndices);
                shader.SetBuffer(projectKernel, "restDarboux", restDarboux);
                shader.SetBuffer(projectKernel, "stiffnesses", stiffnesses);
                shader.SetBuffer(projectKernel, "plasticity", plasticity);
                shader.SetBuffer(projectKernel, "lambdas", lambdas);

                shader.SetBuffer(projectKernel, "orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(projectKernel, "invRotationalMasses", solverImplementation.invRotationalMassesBuffer);

                shader.SetBuffer(projectKernel, "orientationDeltasAsInt", solverImplementation.orientationDeltasIntBuffer);
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

                var shader = ((ComputeBendTwistConstraints)m_Constraints).constraintsShader;
                int applyKernel = ((ComputeBendTwistConstraints)m_Constraints).applyKernel;

                shader.SetBuffer(applyKernel, "orientationIndices", orientationIndices);
                shader.SetBuffer(applyKernel, "orientations", solverImplementation.orientationsBuffer);

                shader.SetBuffer(applyKernel, "orientationDeltasAsInt", solverImplementation.orientationDeltasIntBuffer);
                shader.SetBuffer(applyKernel, "orientationConstraintCounts", solverImplementation.orientationConstraintCountBuffer);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetFloat("sorFactor", parameters.SORFactor);

                int threadGroups = ComputeMath.ThreadGroupCount(m_ConstraintCount, 128);
                shader.Dispatch(applyKernel, threadGroups, 1, 1);
            }
        }

    }
}