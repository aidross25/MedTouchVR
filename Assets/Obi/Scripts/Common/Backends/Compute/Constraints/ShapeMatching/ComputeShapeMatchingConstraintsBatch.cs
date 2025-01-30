using UnityEngine;

namespace Obi
{
    public class ComputeShapeMatchingConstraintsBatch : ComputeConstraintsBatchImpl, IShapeMatchingConstraintsBatchImpl
    {
        private GraphicsBuffer firstIndexBuffer;
        private GraphicsBuffer numIndicesBuffer;
        private GraphicsBuffer explicitGroupBuffer;
        private GraphicsBuffer shapeMaterialParametersBuffer;
        private GraphicsBuffer restComsBuffer;
        private GraphicsBuffer comsBuffer;
        private GraphicsBuffer constraintOrientationsBuffer;

        private GraphicsBuffer AqqBuffer;
        private GraphicsBuffer linearTransformsBuffer;
        private GraphicsBuffer plasticDeformationsBuffer;

        private ObiNativeVector4List m_RestComs;
        private ObiNativeVector4List m_Coms;
        private ObiNativeQuaternionList m_ConstraintOrientations;

        private bool m_RecalculateRestShape = false;
        public ComputeShapeMatchingConstraintsBatch(ComputeShapeMatchingConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.ShapeMatching;
        }

        public void SetShapeMatchingConstraints(ObiNativeIntList particleIndices,
                                         ObiNativeIntList firstIndex,
                                         ObiNativeIntList numIndices,
                                         ObiNativeIntList explicitGroup,
                                         ObiNativeFloatList shapeMaterialParameters,
                                         ObiNativeVector4List restComs,
                                         ObiNativeVector4List coms,
                                         ObiNativeQuaternionList constraintOrientations,
                                         ObiNativeMatrix4x4List linearTransforms,
                                         ObiNativeMatrix4x4List plasticDeformations,
                                         ObiNativeFloatList lambdas,
                                         int count)
        {
            this.particleIndices = particleIndices.AsComputeBuffer<int>();
            this.firstIndexBuffer = firstIndex.AsComputeBuffer<int>();
            this.numIndicesBuffer = numIndices.AsComputeBuffer<int>();
            this.explicitGroupBuffer = explicitGroup.AsComputeBuffer<int>();
            this.shapeMaterialParametersBuffer = shapeMaterialParameters.AsComputeBuffer<float>();
            this.restComsBuffer = restComs.AsComputeBuffer<Vector4>();
            this.comsBuffer = coms.AsComputeBuffer<Vector4>();
            this.constraintOrientationsBuffer = constraintOrientations.AsComputeBuffer<Quaternion>();
            this.linearTransformsBuffer = linearTransforms.AsComputeBuffer<Matrix4x4>();
            this.plasticDeformationsBuffer = plasticDeformations.AsComputeBuffer<Matrix4x4>();

            if (AqqBuffer != null)
                AqqBuffer.Dispose();

            AqqBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 64); // float4x4

            m_RestComs = restComs;
            m_Coms = coms;
            m_ConstraintOrientations = constraintOrientations;

            m_ConstraintCount = count;
        }

        public override void Destroy()
        {
            if (AqqBuffer != null)
                AqqBuffer.Dispose();
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (m_ConstraintCount > 0)
            {
                var shader = ((ComputeShapeMatchingConstraints)m_Constraints).constraintsShader;
                int threadGroups = ComputeMath.ThreadGroupCount(m_ConstraintCount, 128);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetFloat("deltaTime", substepTime);

                if (m_RecalculateRestShape)
                {
                    m_RecalculateRestShape = false;

                    int restKernel = ((ComputeShapeMatchingConstraints)m_Constraints).restStateKernel;

                    shader.SetBuffer(restKernel, "particleIndices", particleIndices);
                    shader.SetBuffer(restKernel, "firstIndex", firstIndexBuffer);
                    shader.SetBuffer(restKernel, "numIndices", numIndicesBuffer);
                    shader.SetBuffer(restKernel, "RW_restComs", restComsBuffer);

                    shader.SetBuffer(restKernel, "RW_Aqq", AqqBuffer);
                    shader.SetBuffer(restKernel, "RW_deformation", plasticDeformationsBuffer);

                    shader.SetBuffer(restKernel, "restPositions", solverImplementation.restPositionsBuffer);
                    shader.SetBuffer(restKernel, "restOrientations", solverImplementation.restOrientationsBuffer);
                    shader.SetBuffer(restKernel, "invMasses", solverImplementation.invMassesBuffer);
                    shader.SetBuffer(restKernel, "invRotationalMasses", solverImplementation.invRotationalMassesBuffer);
                    shader.SetBuffer(restKernel, "principalRadii", solverImplementation.principalRadiiBuffer);

                    shader.Dispatch(restKernel, threadGroups, 1, 1);

                    m_RestComs.Readback();
                    m_RestComs.WaitForReadback();
                }

                //var shader = ((ComputeShapeMatchingConstraints)m_Constraints).constraintsShader;
                int projectKernel = ((ComputeShapeMatchingConstraints)m_Constraints).projectKernel;
                int plasticityKernel = ((ComputeShapeMatchingConstraints)m_Constraints).plasticityKernel;

                shader.SetBuffer(projectKernel, "particleIndices", particleIndices);
                shader.SetBuffer(projectKernel, "firstIndex", firstIndexBuffer);
                shader.SetBuffer(projectKernel, "numIndices", numIndicesBuffer);
                shader.SetBuffer(projectKernel, "explicitGroup", explicitGroupBuffer);
                shader.SetBuffer(projectKernel, "shapeMaterialParameters", shapeMaterialParametersBuffer);

                shader.SetBuffer(projectKernel, "restComs", restComsBuffer);
                shader.SetBuffer(projectKernel, "coms", comsBuffer);
                shader.SetBuffer(projectKernel, "constraintOrientations", constraintOrientationsBuffer);

                shader.SetBuffer(projectKernel, "Aqq", AqqBuffer);
                shader.SetBuffer(projectKernel, "RW_linearTransforms", linearTransformsBuffer);
                shader.SetBuffer(projectKernel, "deformation", plasticDeformationsBuffer);

                shader.SetBuffer(projectKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(projectKernel, "restPositions", solverImplementation.restPositionsBuffer);
                shader.SetBuffer(projectKernel, "orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(projectKernel, "restOrientations", solverImplementation.restOrientationsBuffer);
                shader.SetBuffer(projectKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(projectKernel, "invRotationalMasses", solverImplementation.invRotationalMassesBuffer);
                shader.SetBuffer(projectKernel, "principalRadii", solverImplementation.principalRadiiBuffer);
                shader.SetBuffer(projectKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);

                shader.Dispatch(projectKernel, threadGroups, 1, 1);

                shader.SetBuffer(plasticityKernel, "particleIndices", particleIndices);
                shader.SetBuffer(plasticityKernel, "firstIndex", firstIndexBuffer);
                shader.SetBuffer(plasticityKernel, "numIndices", numIndicesBuffer);
                shader.SetBuffer(plasticityKernel, "shapeMaterialParameters", shapeMaterialParametersBuffer);

                shader.SetBuffer(plasticityKernel, "RW_restComs", restComsBuffer);
                shader.SetBuffer(plasticityKernel, "constraintOrientations", constraintOrientationsBuffer);

                shader.SetBuffer(plasticityKernel, "RW_Aqq", AqqBuffer);
                shader.SetBuffer(plasticityKernel, "linearTransforms", linearTransformsBuffer);
                shader.SetBuffer(plasticityKernel, "RW_deformation", plasticDeformationsBuffer);

                shader.SetBuffer(plasticityKernel, "restPositions", solverImplementation.restPositionsBuffer);
                shader.SetBuffer(plasticityKernel, "restOrientations", solverImplementation.restOrientationsBuffer);
                shader.SetBuffer(plasticityKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(plasticityKernel, "invRotationalMasses", solverImplementation.invRotationalMassesBuffer);
                shader.SetBuffer(plasticityKernel, "principalRadii", solverImplementation.principalRadiiBuffer);

                shader.Dispatch(plasticityKernel, threadGroups, 1, 1);
            }
        }

        public override void Apply(float substepTime)
        {
            if (m_ConstraintCount > 0)
            {
                var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

                var shader = ((ComputeShapeMatchingConstraints)m_Constraints).constraintsShader;
                int applyKernel = ((ComputeShapeMatchingConstraints)m_Constraints).applyKernel;

                shader.SetBuffer(applyKernel, "particleIndices", particleIndices);
                shader.SetBuffer(applyKernel, "firstIndex", firstIndexBuffer);
                shader.SetBuffer(applyKernel, "numIndices", numIndicesBuffer);
                shader.SetBuffer(applyKernel, "RW_positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(applyKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(applyKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);

                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetFloat("sorFactor", parameters.SORFactor);

                int threadGroups = ComputeMath.ThreadGroupCount(m_ConstraintCount, 128);
                shader.Dispatch(applyKernel, threadGroups, 1, 1);
            }
        }

        public void CalculateRestShapeMatching()
        {
            // just set a flag and do the actual calculation at the start of Evaluate().
            // This ensures GPu data of both particles and constraints is up to date when calculating the rest shape.
            m_RecalculateRestShape = true;
        }

        public void RequestDataReadback()
        {
            m_Coms.Readback();
            m_ConstraintOrientations.Readback();
        }

        public void WaitForReadback()
        {
            m_Coms.WaitForReadback();
            m_ConstraintOrientations.WaitForReadback();
        }

    }
}