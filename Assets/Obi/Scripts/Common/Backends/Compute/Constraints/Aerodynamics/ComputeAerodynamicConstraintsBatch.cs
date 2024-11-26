using UnityEngine;

namespace Obi
{
    public class ComputeAerodynamicConstraintsBatch : ComputeConstraintsBatchImpl, IAerodynamicConstraintsBatchImpl
    {
        GraphicsBuffer aerodynamicCoeffs;

        public ComputeAerodynamicConstraintsBatch(ComputeAerodynamicConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Aerodynamics;
        }

        public void SetAerodynamicConstraints(ObiNativeIntList particleIndices, ObiNativeFloatList aerodynamicCoeffs, int count)
        {
            this.particleIndices = particleIndices.AsComputeBuffer<int>();
            this.aerodynamicCoeffs = aerodynamicCoeffs.AsComputeBuffer<float>();

            m_ConstraintCount = count;
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (m_ConstraintCount > 0)
            {
                var shader = ((ComputeAerodynamicConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputeAerodynamicConstraints)m_Constraints).projectKernel;

                shader.SetBuffer(projectKernel, "particleIndices", particleIndices);
                shader.SetBuffer(projectKernel, "aerodynamicCoeffs", aerodynamicCoeffs);

                shader.SetBuffer(projectKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(projectKernel, "normals", solverImplementation.normalsIntBuffer);
                shader.SetBuffer(projectKernel, "wind", solverImplementation.windBuffer);
                shader.SetBuffer(projectKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(projectKernel, "velocities", solverImplementation.velocitiesBuffer);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetFloat("deltaTime", substepTime);

                int threadGroups = ComputeMath.ThreadGroupCount(m_ConstraintCount, 128);
                shader.Dispatch(projectKernel, threadGroups, 1, 1);
            }
        }

        public override void Apply(float substepTime)
        {
        }

    }
}