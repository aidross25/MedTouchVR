using UnityEngine;

namespace Obi
{
    public class ComputePinConstraintsBatch : ComputeConstraintsBatchImpl, IPinConstraintsBatchImpl
    {
        GraphicsBuffer colliderIndices;
        GraphicsBuffer offsets;
        GraphicsBuffer restDarboux;
        GraphicsBuffer stiffnesses;

        public ComputePinConstraintsBatch(ComputePinConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Pin;
        }

        public void SetPinConstraints(ObiNativeIntList particleIndices, ObiNativeIntList colliderIndices, ObiNativeVector4List offsets, ObiNativeQuaternionList restDarbouxVectors, ObiNativeFloatList stiffnesses, ObiNativeFloatList lambdas, int count)
        {
            this.particleIndices = particleIndices.AsComputeBuffer<int>();
            this.colliderIndices = colliderIndices.AsComputeBuffer<int>();
            this.offsets = offsets.AsComputeBuffer<Vector4>();
            this.restDarboux = restDarbouxVectors.AsComputeBuffer<Quaternion>();
            this.stiffnesses = stiffnesses.AsComputeBuffer<Vector2>();
            this.lambdas = lambdas.AsComputeBuffer<Vector4>();
            this.lambdasList = lambdas;

            m_ConstraintCount = count;
        }

        public override void Initialize(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (m_ConstraintCount > 0)
            {
                var shader = ((ComputePinConstraints)m_Constraints).constraintsShader;
                int clearKernel = ((ComputePinConstraints)m_Constraints).clearKernel;
                int initializeKernel = ((ComputePinConstraints)m_Constraints).initializeKernel;

                shader.SetBuffer(clearKernel, "colliderIndices", colliderIndices);
                shader.SetBuffer(clearKernel, "shapes", this.solverImplementation.colliderGrid.shapesBuffer);
                shader.SetBuffer(clearKernel, "RW_rigidbodies", this.solverImplementation.colliderGrid.rigidbodiesBuffer);

                shader.SetBuffer(initializeKernel, "colliderIndices", colliderIndices);
                shader.SetBuffer(initializeKernel, "shapes", this.solverImplementation.colliderGrid.shapesBuffer);
                shader.SetBuffer(initializeKernel, "RW_rigidbodies", this.solverImplementation.colliderGrid.rigidbodiesBuffer);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);

                int threadGroups = ComputeMath.ThreadGroupCount(m_ConstraintCount, 128);
                shader.Dispatch(clearKernel, threadGroups, 1, 1);
                shader.Dispatch(initializeKernel, threadGroups, 1, 1);
            }

            // clear lambdas:
            base.Initialize(stepTime, substepTime, steps, timeLeft);
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (m_ConstraintCount > 0)
            {
                var shader = ((ComputePinConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputePinConstraints)m_Constraints).projectKernel;

                shader.SetBuffer(projectKernel, "particleIndices", particleIndices);
                shader.SetBuffer(projectKernel, "colliderIndices", colliderIndices);
                shader.SetBuffer(projectKernel, "offsets", offsets);
                shader.SetBuffer(projectKernel, "restDarboux", restDarboux);
                shader.SetBuffer(projectKernel, "stiffnesses", stiffnesses);
                shader.SetBuffer(projectKernel, "lambdas", lambdas);

                shader.SetBuffer(projectKernel, "transforms", this.solverImplementation.colliderGrid.transformsBuffer);
                shader.SetBuffer(projectKernel, "shapes", this.solverImplementation.colliderGrid.shapesBuffer);
                shader.SetBuffer(projectKernel, "rigidbodies", this.solverImplementation.colliderGrid.rigidbodiesBuffer);

                shader.SetBuffer(projectKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(projectKernel, "prevPositions", solverImplementation.prevPositionsBuffer);
                shader.SetBuffer(projectKernel, "orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(projectKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(projectKernel, "invRotationalMasses", solverImplementation.invRotationalMassesBuffer);
                shader.SetBuffer(projectKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(projectKernel, "orientationDeltasAsInt", solverImplementation.orientationDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "orientationConstraintCounts", solverImplementation.orientationConstraintCountBuffer);

                shader.SetBuffer(projectKernel, "linearDeltasAsInt", solverImplementation.rigidbodyLinearDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "angularDeltasAsInt", solverImplementation.rigidbodyAngularDeltasIntBuffer);

                shader.SetBuffer(projectKernel, "inertialSolverFrame", solverImplementation.inertialFrameBuffer);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetFloat("stepTime", stepTime);
                shader.SetFloat("substepTime", substepTime);
                shader.SetInt("steps", steps);
                shader.SetFloat("timeLeft", timeLeft);

                int threadGroups = ComputeMath.ThreadGroupCount(m_ConstraintCount, 128);
                shader.Dispatch(projectKernel, threadGroups, 1, 1);
            }
        }

        public override void Apply(float substepTime)
        {
            if (m_ConstraintCount > 0)
            {
                var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

                var shader = ((ComputePinConstraints)m_Constraints).constraintsShader;
                int applyKernel = ((ComputePinConstraints)m_Constraints).applyKernel;

                shader.SetBuffer(applyKernel, "particleIndices", particleIndices);

                shader.SetBuffer(applyKernel, "RW_positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(applyKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(applyKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);

                shader.SetBuffer(applyKernel, "RW_orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(applyKernel, "orientationDeltasAsInt", solverImplementation.orientationDeltasIntBuffer);
                shader.SetBuffer(applyKernel, "orientationConstraintCounts", solverImplementation.orientationConstraintCountBuffer);

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