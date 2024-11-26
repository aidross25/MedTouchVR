using UnityEngine;

namespace Obi
{
    public class ComputeParticleFrictionConstraintsBatch : ComputeConstraintsBatchImpl, IParticleCollisionConstraintsBatchImpl
    {

        public ComputeParticleFrictionConstraintsBatch(ComputeParticleFrictionConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.ParticleFriction;
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            //if (m_ConstraintCount > 0)
            if (solverImplementation.simplexCounts.simplexCount > 0 && solverImplementation.activeParticleCount > 0)
            {
                var shader = ((ComputeParticleFrictionConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputeParticleFrictionConstraints)m_Constraints).projectKernel;

                shader.SetInt("pointCount", solverAbstraction.simplexCounts.pointCount);
                shader.SetInt("edgeCount", solverAbstraction.simplexCounts.edgeCount);
                shader.SetInt("triangleCount", solverAbstraction.simplexCounts.triangleCount);

                shader.SetBuffer(projectKernel, "particleContacts", solverAbstraction.particleContacts.computeBuffer);
                shader.SetBuffer(projectKernel, "effectiveMasses", solverAbstraction.particleContactEffectiveMasses.computeBuffer);
                shader.SetBuffer(projectKernel, "dispatchBuffer", solverImplementation.particleGrid.dispatchBuffer);
                shader.SetBuffer(projectKernel, "collisionMaterials", solverImplementation.colliderGrid.materialsBuffer);

                shader.SetBuffer(projectKernel, "simplices", solverImplementation.simplices);
                shader.SetBuffer(projectKernel, "collisionMaterialIndices", solverImplementation.collisionMaterialIndexBuffer);
                shader.SetBuffer(projectKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(projectKernel, "orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(projectKernel, "prevPositions", solverImplementation.prevPositionsBuffer);
                shader.SetBuffer(projectKernel, "prevOrientations", solverImplementation.prevOrientationsBuffer);
                shader.SetBuffer(projectKernel, "principalRadii", solverImplementation.principalRadiiBuffer);
                shader.SetBuffer(projectKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(projectKernel, "invRotationalMasses", solverImplementation.invRotationalMassesBuffer);

                shader.SetBuffer(projectKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(projectKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "orientationConstraintCounts", solverImplementation.orientationConstraintCountBuffer);
                shader.SetBuffer(projectKernel, "orientationDeltasAsInt", solverImplementation.orientationDeltasIntBuffer);

                shader.SetFloat("stepTime", stepTime);
                shader.SetFloat("substepTime", substepTime);

                shader.DispatchIndirect(projectKernel, this.solverImplementation.particleGrid.dispatchBuffer);
            }

        }

        public override void Apply(float substepTime)
        {
            var shader = ((ComputeParticleFrictionConstraints)m_Constraints).constraintsShader;
            int applyKernel = ((ComputeParticleFrictionConstraints)m_Constraints).applyKernel;

            if (solverImplementation.activeParticleCount > 0)
            {
                var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

                shader.SetBuffer(applyKernel, "particleIndices", this.solverImplementation.activeParticlesBuffer);
                shader.SetBuffer(applyKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(applyKernel, "orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(applyKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(applyKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(applyKernel, "orientationConstraintCounts", solverImplementation.orientationConstraintCountBuffer);
                shader.SetBuffer(applyKernel, "orientationDeltasAsInt", solverImplementation.orientationDeltasIntBuffer);

                shader.SetInt("particleCount", this.solverAbstraction.activeParticleCount);
                shader.SetFloat("sorFactor", parameters.SORFactor);

                int threadGroups = ComputeMath.ThreadGroupCount(this.solverAbstraction.activeParticleCount, 128);
                shader.Dispatch(applyKernel, threadGroups, 1, 1);
            }
        }
    }
}