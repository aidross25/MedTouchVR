using UnityEngine;
using System.Collections.Generic;

namespace Obi
{
    public class ComputeVolumeConstraintsBatch : ComputeConstraintsBatchImpl, IVolumeConstraintsBatchImpl
    {
        GraphicsBuffer firstTriangle;
        GraphicsBuffer numTriangles;
        GraphicsBuffer restVolumes;
        GraphicsBuffer pressureStiffness;

        GraphicsBuffer volumes;
        GraphicsBuffer denominators;
        GraphicsBuffer triangleConstraintIndex; // for each triangle, its constraint index.
        GraphicsBuffer particles; // indices of particles involved in each constraint.
        GraphicsBuffer particleConstraintIndex; // for each particle, its constraint index.

        public ComputeVolumeConstraintsBatch(ComputeVolumeConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Volume;
        }

        public void SetVolumeConstraints(ObiNativeIntList triangles,
                                          ObiNativeIntList firstTriangle,
                                          ObiNativeIntList numTriangles,
                                          ObiNativeFloatList restVolumes,
                                          ObiNativeVector2List pressureStiffness,
                                          ObiNativeFloatList lambdas,
                                          int count)
        {
            // store volume and denominator per constraint:
            volumes = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 4);
            denominators = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 4);


            // get particles involved in each constraint:
            List<int> partic = new List<int>();
            List<int> partConstIndex = new List<int>();
            List<int> triConstIndex = new List<int>();
            for (int i = 0; i < numTriangles.count; ++i)
            {
                List<int> parts = new List<int>();
                for (int j = 0; j < numTriangles[i]; ++j)
                {
                    int tri = firstTriangle[i] + j;

                    parts.Add(triangles[tri * 3]);
                    parts.Add(triangles[tri * 3+1]);
                    parts.Add(triangles[tri * 3+2]);

                    partConstIndex.Add(i);
                    partConstIndex.Add(i);
                    partConstIndex.Add(i);

                    triConstIndex.Add(i);
                }

                // make them unique:
                parts.Sort();
                int resultIndex = parts.Unique((int x, int y) => { return x == y; });

                // remove excess at the end of the list:
                if (resultIndex < parts.Count)
                {
                    int removeCount = parts.Count - resultIndex;

                    parts.RemoveRange(resultIndex, removeCount);
                    partConstIndex.RemoveRange(partConstIndex.Count - removeCount, removeCount);
                }

                partic.AddRange(parts);
            }

            particles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, partic.Count, 4);
            particleConstraintIndex = new GraphicsBuffer(GraphicsBuffer.Target.Structured, partConstIndex.Count, 4);
            triangleConstraintIndex = new GraphicsBuffer(GraphicsBuffer.Target.Structured, triConstIndex.Count, 4);

            particles.SetData(partic);
            particleConstraintIndex.SetData(partConstIndex);
            triangleConstraintIndex.SetData(triConstIndex);

            this.particleIndices = triangles.AsComputeBuffer<int>();
            this.firstTriangle = firstTriangle.AsComputeBuffer<int>();
            this.numTriangles = numTriangles.AsComputeBuffer<int>();
            this.restVolumes = restVolumes.AsComputeBuffer<float>();
            this.pressureStiffness = pressureStiffness.AsComputeBuffer<Vector2>();
            this.lambdas = lambdas.AsComputeBuffer<float>();

            m_ConstraintCount = count;
        }

        public override void Destroy()
        {
            volumes.Dispose();
            denominators.Dispose();
            particles.Dispose();
            particleConstraintIndex.Dispose();
            triangleConstraintIndex.Dispose();
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            // 1: parallel over all triangles, atomic accumulate gradient on orientationDeltasInt
            // 2: reduction over triangles, sum volume.
            // 3: reduction over particles, sum denominator.
            // 4: parallel over constraints: lambda
            // 5: parallel over triangles, atomic accumulate delta.

            if (m_ConstraintCount > 0)
            {
                var shader = ((ComputeVolumeConstraints)m_Constraints).constraintsShader;
                int gradientsKernel = ((ComputeVolumeConstraints)m_Constraints).gradientsKernel;
                int volumeKernel = ((ComputeVolumeConstraints)m_Constraints).volumeKernel;
                int denominatorsKernel = ((ComputeVolumeConstraints)m_Constraints).denominatorsKernel;
                int constraintKernel = ((ComputeVolumeConstraints)m_Constraints).constraintKernel;
                int deltasKernel = ((ComputeVolumeConstraints)m_Constraints).deltasKernel;

                /*shader.SetBuffer(projectKernel, "triangles", particleIndices);
                shader.SetBuffer(projectKernel, "firstTriangle", firstTriangle);
                shader.SetBuffer(projectKernel, "numTriangles", numTriangles);
                shader.SetBuffer(projectKernel, "restVolumes", restVolumes);
                shader.SetBuffer(projectKernel, "pressureStiffness", pressureStiffness);
                shader.SetBuffer(projectKernel, "lambdas", lambdas);

                shader.SetBuffer(projectKernel, "denominators", denominators);
                shader.SetBuffer(projectKernel, "volumes", volumes);
                shader.SetBuffer(projectKernel, "gradients", solverImplementation.fluidDataBuffer);

                shader.SetBuffer(projectKernel, "particles", particles);
                shader.SetBuffer(projectKernel, "particleConstraintIndex", particleConstraintIndex);
                shader.SetBuffer(projectKernel, "triangleConstraintIndex", triangleConstraintIndex);

                shader.SetBuffer(projectKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(projectKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(projectKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(projectKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);*/

                int trianglesCount = particleIndices.count / 3;
                shader.SetInt("activeConstraintCount", m_ConstraintCount);
                shader.SetInt("trianglesCount", trianglesCount);
                shader.SetInt("particlesCount", particles.count);
                shader.SetFloat("deltaTime", substepTime);

                // Gradients:
                shader.SetBuffer(gradientsKernel, "triangles", particleIndices);
                shader.SetBuffer(gradientsKernel, "gradients", solverImplementation.fluidDataBuffer);
                shader.SetBuffer(gradientsKernel, "positions", solverImplementation.positionsBuffer);

                int threadGroups = ComputeMath.ThreadGroupCount(trianglesCount, 128);
                shader.Dispatch(gradientsKernel, threadGroups, 1, 1);

                // Volume:
                shader.SetBuffer(volumeKernel, "triangles", particleIndices);
                shader.SetBuffer(volumeKernel, "gradients", solverImplementation.fluidDataBuffer);
                shader.SetBuffer(volumeKernel, "volumes", volumes);
                shader.SetBuffer(volumeKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(volumeKernel, "triangleConstraintIndex", triangleConstraintIndex);

                shader.Dispatch(volumeKernel, threadGroups, 1, 1);

                // Denominators:
                shader.SetBuffer(denominatorsKernel, "particles", particles);
                shader.SetBuffer(denominatorsKernel, "particleConstraintIndex", particleConstraintIndex);
                shader.SetBuffer(denominatorsKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(denominatorsKernel, "gradients", solverImplementation.fluidDataBuffer);
                shader.SetBuffer(denominatorsKernel, "denominators", denominators);

                threadGroups = ComputeMath.ThreadGroupCount(particles.count, 128);
                shader.Dispatch(denominatorsKernel, threadGroups, 1, 1);

                // Constraint:
                shader.SetBuffer(constraintKernel, "denominators", denominators);
                shader.SetBuffer(constraintKernel, "volumes", volumes);
                shader.SetBuffer(constraintKernel, "restVolumes", restVolumes);
                shader.SetBuffer(constraintKernel, "pressureStiffness", pressureStiffness);
                shader.SetBuffer(constraintKernel, "lambdas", lambdas);

                threadGroups = ComputeMath.ThreadGroupCount(m_ConstraintCount, 128);
                shader.Dispatch(constraintKernel, threadGroups, 1, 1);

                // Deltas:
                shader.SetBuffer(deltasKernel, "particles", particles);
                shader.SetBuffer(deltasKernel, "particleConstraintIndex", particleConstraintIndex);
                shader.SetBuffer(deltasKernel, "lambdas", lambdas);
                shader.SetBuffer(deltasKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(deltasKernel, "gradients", solverImplementation.fluidDataBuffer);
                shader.SetBuffer(deltasKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(deltasKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);

                threadGroups = ComputeMath.ThreadGroupCount(particles.count, 128);
                shader.Dispatch(deltasKernel, threadGroups, 1, 1);
            }
        }

        public override void Apply(float substepTime)
        {
            if (m_ConstraintCount > 0)
            {
                var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

                var shader = ((ComputeVolumeConstraints)m_Constraints).constraintsShader;
                int applyKernel = ((ComputeVolumeConstraints)m_Constraints).applyKernel;

                shader.SetBuffer(applyKernel, "triangles", particleIndices);
                shader.SetBuffer(applyKernel, "firstTriangle", firstTriangle);
                shader.SetBuffer(applyKernel, "numTriangles", numTriangles);

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