using UnityEngine;

namespace Obi
{
    public class ComputeParticleCollisionConstraintsBatch : ComputeConstraintsBatchImpl, IParticleCollisionConstraintsBatchImpl
    {

        public ComputeParticleCollisionConstraintsBatch(ComputeParticleCollisionConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.ParticleCollision;
        }

        public override void Initialize(float stepTime, float substepTime, int steps, float timeLeft)
        {
            var shader = ((ComputeParticleCollisionConstraints)m_Constraints).constraintsShader;
            int initializeKernel = ((ComputeParticleCollisionConstraints)m_Constraints).initializeKernel;

            if (solverImplementation.simplexCounts.simplexCount > 0)
            {
                shader.SetInt("pointCount", solverAbstraction.simplexCounts.pointCount);
                shader.SetInt("edgeCount", solverAbstraction.simplexCounts.edgeCount);
                shader.SetInt("triangleCount", solverAbstraction.simplexCounts.triangleCount);
                shader.SetFloat("shockPropagation", solverAbstraction.parameters.shockPropagation);
                shader.SetVector("gravity", solverAbstraction.parameters.gravity);

                shader.SetBuffer(initializeKernel, "simplices", this.solverImplementation.simplices);
                shader.SetBuffer(initializeKernel, "particleContacts", solverAbstraction.particleContacts.computeBuffer);
                shader.SetBuffer(initializeKernel, "effectiveMasses", solverAbstraction.particleContactEffectiveMasses.computeBuffer);
                shader.SetBuffer(initializeKernel, "dispatchBuffer", this.solverImplementation.particleGrid.dispatchBuffer);
                shader.SetBuffer(initializeKernel, "collisionMaterials", this.solverImplementation.colliderGrid.materialsBuffer);

                shader.SetBuffer(initializeKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(initializeKernel, "prevPositions", solverImplementation.prevPositionsBuffer);
                shader.SetBuffer(initializeKernel, "orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(initializeKernel, "prevOrientations", solverImplementation.prevOrientationsBuffer);
                shader.SetBuffer(initializeKernel, "principalRadii", solverImplementation.principalRadiiBuffer);
                shader.SetBuffer(initializeKernel, "velocities", solverImplementation.velocitiesBuffer);
                shader.SetBuffer(initializeKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(initializeKernel, "collisionMaterialIndices", solverImplementation.collisionMaterialIndexBuffer);
                shader.SetBuffer(initializeKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(initializeKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(initializeKernel, "invRotationalMasses", solverImplementation.invMassesBuffer);

                shader.SetFloat("deltaTime", substepTime);

                shader.DispatchIndirect(initializeKernel, this.solverImplementation.particleGrid.dispatchBuffer);
            }
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            //if (m_ConstraintCount > 0)

            if (solverImplementation.simplexCounts.simplexCount > 0)
            {
                var shader = ((ComputeParticleCollisionConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputeParticleCollisionConstraints)m_Constraints).projectKernel;

                shader.SetBuffer(projectKernel, "particleContacts", solverAbstraction.particleContacts.computeBuffer);
                shader.SetBuffer(projectKernel, "effectiveMasses", solverAbstraction.particleContactEffectiveMasses.computeBuffer);
                shader.SetBuffer(projectKernel, "dispatchBuffer", this.solverImplementation.particleGrid.dispatchBuffer);

                shader.SetBuffer(projectKernel, "simplices", this.solverImplementation.simplices);
                shader.SetBuffer(projectKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(projectKernel, "prevPositions", solverImplementation.prevPositionsBuffer);
                shader.SetBuffer(projectKernel, "orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(projectKernel, "prevOrientations", solverImplementation.prevOrientationsBuffer);
                shader.SetBuffer(projectKernel, "principalRadii", solverImplementation.principalRadiiBuffer);
                shader.SetBuffer(projectKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(projectKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "invMasses", solverImplementation.invMassesBuffer);

                shader.SetFloat("deltaTime", substepTime);

                shader.DispatchIndirect(projectKernel, this.solverImplementation.particleGrid.dispatchBuffer);

            }

        }

        public override void Apply(float substepTime)
        {
            var shader = ((ComputeParticleCollisionConstraints)m_Constraints).constraintsShader;
            int applyKernel = ((ComputeParticleCollisionConstraints)m_Constraints).applyKernel;

            if (solverImplementation.activeParticleCount > 0)
            {
                var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

                shader.SetBuffer(applyKernel, "particleIndices", this.solverImplementation.activeParticlesBuffer);
                shader.SetBuffer(applyKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(applyKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(applyKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);

                shader.SetInt("particleCount", this.solverAbstraction.activeParticleCount);
                shader.SetFloat("sorFactor", parameters.SORFactor);

                int threadGroups = ComputeMath.ThreadGroupCount(this.solverAbstraction.activeParticleCount, 128);
                shader.Dispatch(applyKernel, threadGroups, 1, 1);
            }
        }
    }
}