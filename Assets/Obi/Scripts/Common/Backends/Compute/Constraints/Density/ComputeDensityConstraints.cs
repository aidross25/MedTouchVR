using System;
using UnityEngine;

namespace Obi
{
    public class ComputeDensityConstraints : ComputeConstraintsImpl<ComputeDensityConstraintsBatch>
    {
        public ComputeShader sortParticlesShader;
        public int sortDataKernel;

        public ComputeShader constraintsShader;
        public int updateDensitiesKernel;
        public int applyKernel;
        public int applyPositionDeltaKernel;
        public int applyAtmosphereKernel;

        public int accumSmoothPositionsKernel;
        public int accumAnisotropyKernel;
        public int averageAnisotropyKernel;

        public ComputeDensityConstraints(ComputeSolverImpl solver) : base(solver, Oni.ConstraintType.Density)
        {
            sortParticlesShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/SortParticleData"));
            sortDataKernel = sortParticlesShader.FindKernel("SortData");

            constraintsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/DensityConstraints"));
            updateDensitiesKernel = constraintsShader.FindKernel("UpdateDensities");
            applyKernel = constraintsShader.FindKernel("Apply");
            applyPositionDeltaKernel = constraintsShader.FindKernel("ApplyPositionDeltas");
            applyAtmosphereKernel = constraintsShader.FindKernel("ApplyAtmosphere");

            accumSmoothPositionsKernel = constraintsShader.FindKernel("AccumulateSmoothPositions");
            accumAnisotropyKernel = constraintsShader.FindKernel("AccumulateAnisotropy");
            averageAnisotropyKernel = constraintsShader.FindKernel("AverageAnisotropy");
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new ComputeDensityConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as ComputeDensityConstraintsBatch);
            batch.Destroy();
        }

        public void CopyDataInSortedOrder(bool renderable = false)
        {
            sortParticlesShader.SetBuffer(sortDataKernel, "sortedToOriginal", m_Solver.particleGrid.sortedFluidIndices);

            if (renderable)
            {
                sortParticlesShader.SetBuffer(sortDataKernel, "positions", m_Solver.renderablePositionsBuffer);
                sortParticlesShader.SetBuffer(sortDataKernel, "principalRadii", m_Solver.renderableRadiiBuffer);
            }
            else
            {
                sortParticlesShader.SetBuffer(sortDataKernel, "positions", m_Solver.positionsBuffer);
                sortParticlesShader.SetBuffer(sortDataKernel, "principalRadii", m_Solver.principalRadiiBuffer);
            }

            sortParticlesShader.SetBuffer(sortDataKernel, "prevPositions", m_Solver.prevPositionsBuffer);
            sortParticlesShader.SetBuffer(sortDataKernel, "userData", m_Solver.userDataBuffer);

            sortParticlesShader.SetBuffer(sortDataKernel, "sortedPositions", m_Solver.particleGrid.sortedPositions);
            sortParticlesShader.SetBuffer(sortDataKernel, "sortedPrincipalRadii", m_Solver.particleGrid.sortedPrincipalRadii);
            sortParticlesShader.SetBuffer(sortDataKernel, "sortedPrevPositions", m_Solver.particleGrid.sortedPrevPosOrientations);
            sortParticlesShader.SetBuffer(sortDataKernel, "sortedUserData", m_Solver.particleGrid.sortedUserDataColor);

            sortParticlesShader.SetBuffer(sortDataKernel, "dispatchBuffer", m_Solver.fluidDispatchBuffer);
            sortParticlesShader.DispatchIndirect(sortDataKernel, m_Solver.fluidDispatchBuffer);
        }

        public void ApplyVelocityCorrections(float deltaTime)
        {
            if (m_Solver.particleGrid.sortedFluidIndices != null && m_Solver.cellCoordsBuffer != null)
            {
                constraintsShader.SetFloat("deltaTime", deltaTime);

                constraintsShader.SetBuffer(applyAtmosphereKernel, "neighborCounts", m_Solver.particleGrid.neighborCounts);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "neighbors", m_Solver.particleGrid.neighbors);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "sortedToOriginal", m_Solver.particleGrid.sortedFluidIndices);

                constraintsShader.SetBuffer(applyAtmosphereKernel, "invMasses", m_Solver.invMassesBuffer);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "velocities", m_Solver.velocitiesBuffer);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "wind", m_Solver.windBuffer);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "normals", m_Solver.normalsBuffer);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "userData", m_Solver.userDataBuffer);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "sortedPositions", m_Solver.particleGrid.sortedPositions);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "sortedPrincipalRadii", m_Solver.particleGrid.sortedPrincipalRadii);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "sortedFluidMaterials", m_Solver.particleGrid.sortedFluidMaterials);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "sortedFluidInterface", m_Solver.particleGrid.sortedFluidInterface);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "sortedUserData", m_Solver.particleGrid.sortedUserDataColor);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "fluidData", m_Solver.fluidDataBuffer);
                constraintsShader.SetBuffer(applyAtmosphereKernel, "dispatchBuffer", m_Solver.fluidDispatchBuffer);

                constraintsShader.DispatchIndirect(applyAtmosphereKernel, m_Solver.fluidDispatchBuffer);

            }
        }

        public void CalculateAnisotropyLaplacianSmoothing()
        {
            int pcount = ((ComputeSolverImpl)solver).particleCount;
            if (pcount > 0 && m_Solver.particleGrid.sortedFluidIndices != null && m_Solver.cellCoordsBuffer != null)
            {
                if (m_Solver.abstraction.parameters.maxAnisotropy <= 1)
                    return;

                constraintsShader.SetFloat("maxAnisotropy", m_Solver.abstraction.parameters.maxAnisotropy);
                constraintsShader.SetInt("simplexCount", m_Solver.simplexCounts.simplexCount);

                // copy render data (renderablePositions / radii) in sorted order:
                CopyDataInSortedOrder(true);

                // accumulate smoothed positions:
                constraintsShader.SetBuffer(accumSmoothPositionsKernel, "neighborCounts", m_Solver.particleGrid.neighborCounts);
                constraintsShader.SetBuffer(accumSmoothPositionsKernel, "neighbors", m_Solver.particleGrid.neighbors);
                constraintsShader.SetBuffer(accumSmoothPositionsKernel, "sortedFluidMaterials", m_Solver.particleGrid.sortedFluidMaterials);
                constraintsShader.SetBuffer(accumSmoothPositionsKernel, "renderablePositions", m_Solver.particleGrid.sortedPositions);
                constraintsShader.SetBuffer(accumSmoothPositionsKernel, "anisotropies", m_Solver.anisotropiesBuffer);
                constraintsShader.SetBuffer(accumSmoothPositionsKernel, "dispatchBuffer", m_Solver.fluidDispatchBuffer);
                constraintsShader.DispatchIndirect(accumSmoothPositionsKernel, m_Solver.fluidDispatchBuffer);

                // accumulate anisotropy:
                constraintsShader.SetBuffer(accumAnisotropyKernel, "neighborCounts", m_Solver.particleGrid.neighborCounts);
                constraintsShader.SetBuffer(accumAnisotropyKernel, "neighbors", m_Solver.particleGrid.neighbors);
                constraintsShader.SetBuffer(accumAnisotropyKernel, "anisotropies", m_Solver.anisotropiesBuffer);
                constraintsShader.SetBuffer(accumAnisotropyKernel, "renderablePositions", m_Solver.particleGrid.sortedPositions);
                constraintsShader.SetBuffer(accumAnisotropyKernel, "sortedFluidMaterials", m_Solver.particleGrid.sortedFluidMaterials);
                constraintsShader.SetBuffer(accumAnisotropyKernel, "dispatchBuffer", m_Solver.fluidDispatchBuffer);
                constraintsShader.DispatchIndirect(accumAnisotropyKernel, m_Solver.fluidDispatchBuffer);

                // average anisotropies:
                constraintsShader.SetBuffer(averageAnisotropyKernel, "sortedToOriginal", m_Solver.particleGrid.sortedFluidIndices);
                constraintsShader.SetBuffer(averageAnisotropyKernel, "anisotropies", m_Solver.anisotropiesBuffer);
                constraintsShader.SetBuffer(averageAnisotropyKernel, "renderablePositions", m_Solver.renderablePositionsBuffer);
                constraintsShader.SetBuffer(averageAnisotropyKernel, "renderableOrientations", m_Solver.renderableOrientationsBuffer);
                constraintsShader.SetBuffer(averageAnisotropyKernel, "renderableRadii", m_Solver.renderableRadiiBuffer);
                constraintsShader.SetBuffer(averageAnisotropyKernel, "sortedPrincipalRadii", m_Solver.particleGrid.sortedPrincipalRadii);
                constraintsShader.SetBuffer(averageAnisotropyKernel, "fluidData", m_Solver.fluidDataBuffer);
                constraintsShader.SetBuffer(averageAnisotropyKernel, "dispatchBuffer", m_Solver.fluidDispatchBuffer);
                constraintsShader.DispatchIndirect(averageAnisotropyKernel, m_Solver.fluidDispatchBuffer);
            }

        }
    }
}
