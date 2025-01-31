
using UnityEngine;

namespace Obi
{
    public class ComputeDensityConstraintsBatch : ComputeConstraintsBatchImpl, IDensityConstraintsBatchImpl
    {

        public ComputeDensityConstraintsBatch(ComputeDensityConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Density;
        }

        public override void Evaluate(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (solverImplementation.particleGrid.sortedFluidIndices != null && solverImplementation.cellCoordsBuffer != null)
            {
                var shader = ((ComputeDensityConstraints)m_Constraints).constraintsShader;
                int densitiesKernel = ((ComputeDensityConstraints)m_Constraints).updateDensitiesKernel;

                // Need to do this at least every simulation step, since fluid meshing reuses sorted arrays. 
                ((ComputeDensityConstraints)m_Constraints).CopyDataInSortedOrder();

                shader.SetInt("maxNeighbors", solverImplementation.particleGrid.maxParticleNeighbors);
                shader.SetInt("mode", (int)solverImplementation.abstraction.parameters.mode);
                shader.SetFloat("deltaTime", substepTime);

                // calculate densities:
                shader.SetBuffer(densitiesKernel, "neighborCounts", this.solverImplementation.particleGrid.neighborCounts);
                shader.SetBuffer(densitiesKernel, "neighbors", this.solverImplementation.particleGrid.neighbors);

                shader.SetBuffer(densitiesKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(densitiesKernel, "sortedFluidData", solverImplementation.particleGrid.sortedFluidDataVel);
                shader.SetBuffer(densitiesKernel, "sortedPositions", solverImplementation.particleGrid.sortedPositions);
                shader.SetBuffer(densitiesKernel, "sortedPrevPositions", solverImplementation.particleGrid.sortedPrevPosOrientations);
                shader.SetBuffer(densitiesKernel, "sortedFluidMaterials", solverImplementation.particleGrid.sortedFluidMaterials);
                shader.SetBuffer(densitiesKernel, "sortedPrincipalRadii", solverImplementation.particleGrid.sortedPrincipalRadii);
                shader.SetBuffer(densitiesKernel, "renderableOrientations", solverImplementation.anisotropiesBuffer);//solverImplementation.renderableOrientationsBuffer); 
                shader.SetBuffer(densitiesKernel, "prevPositions", solverImplementation.prevPositionsBuffer);
                shader.SetBuffer(densitiesKernel, "massCenters", solverImplementation.normalsBuffer);
                shader.SetBuffer(densitiesKernel, "prevMassCenters", solverImplementation.renderablePositionsBuffer);
                shader.SetBuffer(densitiesKernel, "dispatchBuffer", solverImplementation.fluidDispatchBuffer);

                shader.DispatchIndirect(densitiesKernel, solverImplementation.fluidDispatchBuffer);
            }
        }

        public override void Apply(float substepTime)
        {
            if (solverImplementation.particleGrid.sortedFluidIndices != null && solverImplementation.cellCoordsBuffer != null)
            {
                var shader = ((ComputeDensityConstraints)m_Constraints).constraintsShader;
                var applyPositionDeltaKernel = ((ComputeDensityConstraints)m_Constraints).applyPositionDeltaKernel;
                var applyKernel = ((ComputeDensityConstraints)m_Constraints).applyKernel;

                // calculate deltas:
                shader.SetBuffer(applyKernel, "neighborCounts", this.solverImplementation.particleGrid.neighborCounts);
                shader.SetBuffer(applyKernel, "neighbors", this.solverImplementation.particleGrid.neighbors);

                shader.SetBuffer(applyKernel, "invMasses", solverImplementation.invMassesBuffer);
                shader.SetBuffer(applyKernel, "sortedPositions", solverImplementation.particleGrid.sortedPositions);
                shader.SetBuffer(applyKernel, "sortedPrevPositions", solverImplementation.particleGrid.sortedPrevPosOrientations);
                shader.SetBuffer(applyKernel, "sortedFluidMaterials", solverImplementation.particleGrid.sortedFluidMaterials);
                shader.SetBuffer(applyKernel, "sortedPrincipalRadii", solverImplementation.particleGrid.sortedPrincipalRadii);
                shader.SetBuffer(applyKernel, "renderableOrientations", solverImplementation.anisotropiesBuffer);//solverImplementation.renderableOrientationsBuffer);
                shader.SetBuffer(applyKernel, "prevPositions", solverImplementation.prevPositionsBuffer);
                shader.SetBuffer(applyKernel, "massCenters", solverImplementation.normalsBuffer);
                shader.SetBuffer(applyKernel, "prevMassCenters", solverImplementation.renderablePositionsBuffer);
                shader.SetBuffer(applyKernel, "sortedFluidData", solverImplementation.particleGrid.sortedFluidDataVel);
                shader.SetBuffer(applyKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(applyKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(applyKernel, "sortedToOriginal", solverImplementation.particleGrid.sortedFluidIndices);
                shader.SetBuffer(applyKernel, "dispatchBuffer", solverImplementation.fluidDispatchBuffer);

                shader.DispatchIndirect(applyKernel, solverImplementation.fluidDispatchBuffer);

                // apply position deltas
                shader.SetBuffer(applyPositionDeltaKernel, "positions", solverImplementation.positionsBuffer);
                shader.SetBuffer(applyPositionDeltaKernel, "renderableOrientations", solverImplementation.anisotropiesBuffer);//solverImplementation.renderableOrientationsBuffer);
                shader.SetBuffer(applyPositionDeltaKernel, "orientations", solverImplementation.orientationsBuffer);
                shader.SetBuffer(applyPositionDeltaKernel, "fluidData", solverImplementation.fluidDataBuffer);
                shader.SetBuffer(applyPositionDeltaKernel, "sortedFluidData", solverImplementation.particleGrid.sortedFluidDataVel);
                shader.SetBuffer(applyPositionDeltaKernel, "prevOrientations", solverImplementation.prevOrientationsBuffer);
                shader.SetBuffer(applyPositionDeltaKernel, "deltasAsInt", solverImplementation.positionDeltasIntBuffer);
                shader.SetBuffer(applyPositionDeltaKernel, "positionConstraintCounts", solverImplementation.positionConstraintCountBuffer);
                shader.SetBuffer(applyPositionDeltaKernel, "sortedToOriginal", solverImplementation.particleGrid.sortedFluidIndices);
                shader.SetBuffer(applyPositionDeltaKernel, "dispatchBuffer", solverImplementation.fluidDispatchBuffer);

                shader.DispatchIndirect(applyPositionDeltaKernel, solverImplementation.fluidDispatchBuffer);
            }
        }
    }
}