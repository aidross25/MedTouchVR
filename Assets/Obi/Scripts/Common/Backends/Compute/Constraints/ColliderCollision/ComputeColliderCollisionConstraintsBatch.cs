using UnityEngine;

namespace Obi
{
    public class ComputeColliderCollisionConstraintsBatch : ComputeConstraintsBatchImpl, IColliderCollisionConstraintsBatchImpl
    {

        public ComputeColliderCollisionConstraintsBatch(ComputeColliderCollisionConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Collision;
        }

        public override void Initialize(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (solverAbstraction.simplexCounts.simplexCount > 0 && solverImplementation.colliderGrid.colliderCount > 0)
            {
                var shader = ((ComputeColliderCollisionConstraints)m_Constraints).constraintsShader;
                int initializeKernel = ((ComputeColliderCollisionConstraints)m_Constraints).initializeKernel;
                int clearKernel = ((ComputeColliderCollisionConstraints)m_Constraints).clearKernel;

                shader.SetInt("pointCount", solverAbstraction.simplexCounts.pointCount);
                shader.SetInt("edgeCount", solverAbstraction.simplexCounts.edgeCount);
                shader.SetInt("triangleCount", solverAbstraction.simplexCounts.triangleCount);

                shader.SetBuffer(clearKernel, "contacts", solverAbstraction.colliderContacts.computeBuffer);
                shader.SetBuffer(clearKernel, "shapes", this.solverImplementation.colliderGrid.shapesBuffer);
                shader.SetBuffer(clearKernel, "RW_rigidbodies", this.solverImplementation.colliderGrid.rigidbodiesBuffer);
                shader.SetBuffer(clearKernel, "dispatchBuffer", this.solverImplementation.colliderGrid.dispatchBuffer);

                shader.SetBuffer(initializeKernel, "contacts", solverAbstraction.colliderContacts.computeBuffer);
                shader.SetBuffer(initializeKernel, "effectiveMasses", solverAbstraction.contactEffectiveMasses.computeBuffer);
                shader.SetBuffer(initializeKernel, "dispatchBuffer", this.solverImplementation.colliderGrid.dispatchBuffer);
                shader.SetBuffer(initializeKernel, "collisionMaterials", this.solverImplementation.colliderGrid.materialsBuffer);

                shader.SetBuffer(initializeKernel, "simplices", this.solverImplementation.simplices);
                shader.SetBuffer(initializeKernel, "transforms", this.solverImplementation.colliderGrid.transformsBuffer);
                shader.SetBuffer(initializeKernel, "shapes", this.solverImplementation.colliderGrid.shapesBuffer);
                shader.SetBuffer(initializeKernel, "RW_rigidbodies", this.solverImplementation.colliderGrid.rigidbodiesBuffer);
                shader.SetBuffer(initializeKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(initializeKernel, "orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(initializeKernel, "prevPositions", solverImplementation.prevPositionsBuffer);
                shader.SetBuffer(initializeKernel, "prevOrientations", solverImplementation.prevOrientationsBuffer);
                shader.SetBuffer(initializeKernel, "velocities", solverImplementation.velocitiesBuffer);
                shader.SetBuffer(initializeKernel, "principalRadii", solverImplementation.principalRadiiBuffer);
                shader.SetBuffer(initializeKernel, "collisionMaterialIndices", solverImplementation.collisionMaterialIndexBuffer);
                shader.SetBuffer(initializeKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(initializeKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(initializeKernel, "invMasses", solverImplementation.invMassesBuffer); 
                shader.SetBuffer(initializeKernel, "invRotationalMasses", solverImplementation.invRotationalMassesBuffer);

                shader.SetBuffer(initializeKernel, "linearDeltasAsInt", solverImplementation.rigidbodyLinearDeltasIntBuffer);
                shader.SetBuffer(initializeKernel, "angularDeltasAsInt", solverImplementation.rigidbodyAngularDeltasIntBuffer);

                shader.SetBuffer(initializeKernel, "inertialSolverFrame", solverImplementation.inertialFrameBuffer);

                shader.SetFloat("substepTime", substepTime);

                shader.DispatchIndirect(clearKernel, this.solverImplementation.colliderGrid.dispatchBuffer);
                shader.DispatchIndirect(initializeKernel, this.solverImplementation.colliderGrid.dispatchBuffer);
            }
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (solverAbstraction.simplexCounts.simplexCount > 0 && solverImplementation.colliderGrid.colliderCount > 0)
            {
                var shader = ((ComputeColliderCollisionConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputeColliderCollisionConstraints)m_Constraints).projectKernel;

                shader.SetBuffer(projectKernel, "contacts", solverAbstraction.colliderContacts.computeBuffer);
                shader.SetBuffer(projectKernel, "effectiveMasses", this.solverAbstraction.contactEffectiveMasses.computeBuffer);
                shader.SetBuffer(projectKernel, "dispatchBuffer", this.solverImplementation.colliderGrid.dispatchBuffer);
                shader.SetBuffer(projectKernel, "collisionMaterials", this.solverImplementation.colliderGrid.materialsBuffer);

                shader.SetBuffer(projectKernel, "simplices", this.solverImplementation.simplices);
                shader.SetBuffer(projectKernel, "transforms", this.solverImplementation.colliderGrid.transformsBuffer);
                shader.SetBuffer(projectKernel, "shapes", this.solverImplementation.colliderGrid.shapesBuffer);
                shader.SetBuffer(projectKernel, "rigidbodies", this.solverImplementation.colliderGrid.rigidbodiesBuffer);
                shader.SetBuffer(projectKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(projectKernel, "prevPositions", solverImplementation.prevPositionsBuffer);
                shader.SetBuffer(projectKernel, "orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(projectKernel, "prevOrientations", solverImplementation.prevOrientationsBuffer);
                shader.SetBuffer(projectKernel, "principalRadii", solverImplementation.principalRadiiBuffer);
                shader.SetBuffer(projectKernel, "collisionMaterialIndices", solverImplementation.collisionMaterialIndexBuffer);
                shader.SetBuffer(projectKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(projectKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "invMasses", solverImplementation.invMassesBuffer);

                shader.SetBuffer(projectKernel, "linearDeltasAsInt", solverImplementation.rigidbodyLinearDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "angularDeltasAsInt", solverImplementation.rigidbodyAngularDeltasIntBuffer);

                shader.SetBuffer(projectKernel, "inertialSolverFrame", solverImplementation.inertialFrameBuffer);
                shader.SetFloat("substepTime", substepTime);
                shader.SetFloat("stepTime", stepTime);
                shader.SetInt("steps", steps);
                shader.SetFloat("timeLeft", timeLeft);
                shader.SetFloat("maxDepenetration", solverAbstraction.parameters.maxDepenetration);

                shader.DispatchIndirect(projectKernel, this.solverImplementation.colliderGrid.dispatchBuffer);
            }
        }

        public override void Apply(float substepTime)
        {
            var shader = ((ComputeColliderCollisionConstraints)m_Constraints).constraintsShader;
            int applyKernel = ((ComputeColliderCollisionConstraints)m_Constraints).applyKernel;

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