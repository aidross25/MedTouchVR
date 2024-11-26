using UnityEngine;

namespace Obi
{
    public class ComputeColliderFrictionConstraintsBatch : ComputeConstraintsBatchImpl, IColliderCollisionConstraintsBatchImpl
    {

        public ComputeColliderFrictionConstraintsBatch(ComputeColliderFrictionConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Friction;
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (solverAbstraction.simplexCounts.simplexCount > 0 && solverImplementation.colliderGrid.colliderCount > 0)
            {
                var shader = ((ComputeColliderFrictionConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputeColliderFrictionConstraints)m_Constraints).projectKernel;

                shader.SetInt("pointCount", solverAbstraction.simplexCounts.pointCount);
                shader.SetInt("edgeCount", solverAbstraction.simplexCounts.edgeCount);
                shader.SetInt("triangleCount", solverAbstraction.simplexCounts.triangleCount);

                shader.SetBuffer(projectKernel, "contacts", solverAbstraction.colliderContacts.computeBuffer);
                shader.SetBuffer(projectKernel, "effectiveMasses", solverAbstraction.contactEffectiveMasses.computeBuffer);
                shader.SetBuffer(projectKernel, "dispatchBuffer", this.solverImplementation.colliderGrid.dispatchBuffer);
                shader.SetBuffer(projectKernel, "collisionMaterials", this.solverImplementation.colliderGrid.materialsBuffer);

                shader.SetBuffer(projectKernel, "simplices", this.solverImplementation.simplices);
                shader.SetBuffer(projectKernel, "transforms", this.solverImplementation.colliderGrid.transformsBuffer);
                shader.SetBuffer(projectKernel, "shapes", this.solverImplementation.colliderGrid.shapesBuffer);
                shader.SetBuffer(projectKernel, "rigidbodies", this.solverImplementation.colliderGrid.rigidbodiesBuffer);
                shader.SetBuffer(projectKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(projectKernel, "collisionMaterialIndices", solverImplementation.collisionMaterialIndexBuffer);
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

                shader.SetBuffer(projectKernel, "linearDeltasAsInt", solverImplementation.rigidbodyLinearDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "angularDeltasAsInt", solverImplementation.rigidbodyAngularDeltasIntBuffer);

                shader.SetBuffer(projectKernel, "solverToWorld", solverImplementation.solverToWorldBuffer);
                shader.SetBuffer(projectKernel, "inertialSolverFrame", solverImplementation.inertialFrameBuffer);
                shader.SetFloat("stepTime", stepTime);
                shader.SetFloat("substepTime", substepTime);

                shader.DispatchIndirect(projectKernel, this.solverImplementation.colliderGrid.dispatchBuffer);
            }
        }

        public override void Apply(float substepTime)
        {
            var shader = ((ComputeColliderFrictionConstraints)m_Constraints).constraintsShader;
            int applyKernel = ((ComputeColliderFrictionConstraints)m_Constraints).applyKernel;

            if (solverImplementation.activeParticleCount > 0)
            {
                var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

                shader.SetBuffer(applyKernel, "particleIndices", this.solverImplementation.activeParticlesBuffer);
                shader.SetBuffer(applyKernel, "RW_positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(applyKernel, "RW_orientations", solverImplementation.orientationsBuffer);
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