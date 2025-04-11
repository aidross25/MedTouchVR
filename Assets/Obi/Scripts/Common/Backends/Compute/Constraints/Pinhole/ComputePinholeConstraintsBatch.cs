using UnityEngine;

namespace Obi
{
    public class ComputePinholeConstraintsBatch : ComputeConstraintsBatchImpl, IPinholeConstraintsBatchImpl
    {
        GraphicsBuffer colliderIndices;
        GraphicsBuffer offsets;
        GraphicsBuffer edgeMus;
        GraphicsBuffer edgeRanges;
        GraphicsBuffer edgeRangeMus;
        GraphicsBuffer relativeVelocities;
        GraphicsBuffer parameters;

        public ComputePinholeConstraintsBatch(ComputePinholeConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Pinhole;
        }

        public void SetPinholeConstraints(ObiNativeIntList particleIndices, ObiNativeIntList colliderIndices, ObiNativeVector4List offsets, ObiNativeFloatList edgeMus, ObiNativeIntList edgeRanges, ObiNativeFloatList edgeRangeMus, ObiNativeFloatList parameters, ObiNativeFloatList relativeVelocities, ObiNativeFloatList lambdas, int count)
        {
            this.particleIndices = particleIndices.AsComputeBuffer<int>();
            this.colliderIndices = colliderIndices.AsComputeBuffer<int>();
            this.offsets = offsets.AsComputeBuffer<Vector4>();
            this.edgeMus = edgeMus.AsComputeBuffer<float>();
            this.edgeRanges = edgeRanges.AsComputeBuffer<Vector2Int>();
            this.edgeRangeMus = edgeRangeMus.AsComputeBuffer<Vector2>();
            this.parameters = parameters.AsComputeBuffer<float>();
            this.lambdas = lambdas.AsComputeBuffer<float>();
            this.relativeVelocities = relativeVelocities.AsComputeBuffer<float>();
            this.lambdasList = lambdas;

            m_ConstraintCount = count;
        }

        public override void Initialize(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (m_ConstraintCount > 0)
            {
                var shader = ((ComputePinholeConstraints)m_Constraints).constraintsShader;
                int clearKernel = ((ComputePinholeConstraints)m_Constraints).clearKernel;
                int initializeKernel = ((ComputePinholeConstraints)m_Constraints).initializeKernel;

                shader.SetBuffer(clearKernel, "colliderIndices", colliderIndices);
                shader.SetBuffer(clearKernel, "shapes", this.solverImplementation.colliderGrid.shapesBuffer);
                shader.SetBuffer(clearKernel, "RW_rigidbodies", this.solverImplementation.colliderGrid.rigidbodiesBuffer);

                shader.SetBuffer(initializeKernel, "particleIndices", particleIndices);
                shader.SetBuffer(initializeKernel, "colliderIndices", colliderIndices);
                shader.SetBuffer(initializeKernel, "offsets", offsets);
                shader.SetBuffer(initializeKernel, "edgeMus", edgeMus);
                shader.SetBuffer(initializeKernel, "edgeRanges", edgeRanges);
                shader.SetBuffer(initializeKernel, "edgeRangeMus", edgeRangeMus);
                shader.SetBuffer(initializeKernel, "relativeVelocities", relativeVelocities);
                shader.SetBuffer(initializeKernel, "parameters", parameters);

                shader.SetBuffer(initializeKernel, "deformableEdges", solverImplementation.deformableEdgesBuffer);
                shader.SetBuffer(initializeKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(initializeKernel, "prevPositions", solverImplementation.prevPositionsBuffer);
                shader.SetBuffer(initializeKernel, "invMasses", solverImplementation.invMassesBuffer);

                shader.SetBuffer(initializeKernel, "colliderIndices", colliderIndices);
                shader.SetBuffer(initializeKernel, "transforms", this.solverImplementation.colliderGrid.transformsBuffer);
                shader.SetBuffer(initializeKernel, "shapes", this.solverImplementation.colliderGrid.shapesBuffer);
                shader.SetBuffer(initializeKernel, "rigidbodies", this.solverImplementation.colliderGrid.rigidbodiesBuffer);
                shader.SetBuffer(initializeKernel, "RW_rigidbodies", this.solverImplementation.colliderGrid.rigidbodiesBuffer);

                shader.SetBuffer(initializeKernel, "linearDeltasAsInt", solverImplementation.rigidbodyLinearDeltasIntBuffer);
                shader.SetBuffer(initializeKernel, "angularDeltasAsInt", solverImplementation.rigidbodyAngularDeltasIntBuffer);

                shader.SetBuffer(initializeKernel, "inertialSolverFrame", solverImplementation.inertialFrameBuffer);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetFloat("stepTime", stepTime);
                shader.SetFloat("substepTime", substepTime);
                shader.SetInt("steps", steps);
                shader.SetFloat("timeLeft", timeLeft);

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
                var shader = ((ComputePinholeConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputePinholeConstraints)m_Constraints).projectKernel;

                shader.SetBuffer(projectKernel, "particleIndices", particleIndices);
                shader.SetBuffer(projectKernel, "colliderIndices", colliderIndices);
                shader.SetBuffer(projectKernel, "offsets", offsets);
                shader.SetBuffer(projectKernel, "edgeMus", edgeMus);
                shader.SetBuffer(projectKernel, "parameters", parameters);
                shader.SetBuffer(projectKernel, "lambdas", lambdas);

                shader.SetBuffer(projectKernel, "transforms", this.solverImplementation.colliderGrid.transformsBuffer);
                shader.SetBuffer(projectKernel, "shapes", this.solverImplementation.colliderGrid.shapesBuffer);
                shader.SetBuffer(projectKernel, "rigidbodies", this.solverImplementation.colliderGrid.rigidbodiesBuffer);

                shader.SetBuffer(projectKernel, "deformableEdges", solverImplementation.deformableEdgesBuffer);
                shader.SetBuffer(projectKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(projectKernel, "prevPositions", solverImplementation.prevPositionsBuffer);
                shader.SetBuffer(projectKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(projectKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);

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
                var param = solverAbstraction.GetConstraintParameters(m_ConstraintType);

                var shader = ((ComputePinholeConstraints)m_Constraints).constraintsShader;
                int applyKernel = ((ComputePinholeConstraints)m_Constraints).applyKernel;

                shader.SetBuffer(applyKernel, "particleIndices", particleIndices);
                shader.SetBuffer(applyKernel, "deformableEdges", solverImplementation.deformableEdgesBuffer);

                shader.SetBuffer(applyKernel, "RW_positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(applyKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(applyKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetFloat("sorFactor", param.SORFactor);

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