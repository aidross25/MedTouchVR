using UnityEngine;

namespace Obi
{
    public class ComputeChainConstraintsBatch : ComputeConstraintsBatchImpl, IChainConstraintsBatchImpl
    {
        GraphicsBuffer firstIndex;
        GraphicsBuffer numIndices;
        GraphicsBuffer restLengths;

        GraphicsBuffer ni;
        GraphicsBuffer diagonals;

        public ComputeChainConstraintsBatch(ComputeChainConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Chain;
        }

        public void SetChainConstraints(ObiNativeIntList particleIndices, ObiNativeVector2List restLengths, ObiNativeIntList firstIndex, ObiNativeIntList numIndex, int count)
        {
            this.particleIndices = particleIndices.AsComputeBuffer<int>();
            this.firstIndex = firstIndex.AsComputeBuffer<int>();
            this.numIndices = numIndex.AsComputeBuffer<int>();
            this.restLengths = restLengths.AsComputeBuffer<Vector2>();

            int numEdges = 0;
            for (int i = 0; i < numIndex.count; ++i)
                numEdges += numIndex[i] - 1;

            ni = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numEdges, 16);
            diagonals = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numEdges, 12);

            m_ConstraintCount = count;
        }

        public override void Destroy()
        {
            ni.Dispose();
            diagonals.Dispose();
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (m_ConstraintCount > 0)
            {
                var shader = ((ComputeChainConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputeChainConstraints)m_Constraints).projectKernel;

                shader.SetBuffer(projectKernel, "particleIndices", particleIndices);
                shader.SetBuffer(projectKernel, "firstIndex", firstIndex);
                shader.SetBuffer(projectKernel, "numIndices", numIndices);
                shader.SetBuffer(projectKernel, "restLengths", restLengths);

                shader.SetBuffer(projectKernel, "ni", ni);
                shader.SetBuffer(projectKernel, "diagonals", diagonals);

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

                var shader = ((ComputeChainConstraints)m_Constraints).constraintsShader;
                int applyKernel = ((ComputeChainConstraints)m_Constraints).applyKernel;

                shader.SetBuffer(applyKernel, "particleIndices", particleIndices);
                shader.SetBuffer(applyKernel, "firstIndex", firstIndex);
                shader.SetBuffer(applyKernel, "numIndices", numIndices);

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