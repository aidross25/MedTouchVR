#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Burst;

namespace Obi
{
    public class BurstDensityConstraintsBatch : BurstConstraintsBatchImpl, IDensityConstraintsBatchImpl
    {
        public BatchData batchData;

        public BurstDensityConstraintsBatch(BurstDensityConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Density;
        }

        public override JobHandle Initialize(JobHandle inputDeps, float stepTime, float substepTime, int steps, float timeLeft)
        {
            return inputDeps;
        }

        public override JobHandle Evaluate(JobHandle inputDeps, float stepTime, float substepTime, int steps, float timeLeft)
        {

            // update densities and gradients:
            var updateDensities = new UpdateDensitiesJob()
            {
                pairs = ((BurstSolverImpl)constraints.solver).fluidInteractions,
                positions = solverImplementation.positions,
                prevPositions = solverImplementation.prevPositions,
                principalRadii = solverImplementation.principalRadii,
                fluidMaterials = solverImplementation.fluidMaterials,
                fluidData = solverImplementation.fluidData,
                moments = solverImplementation.anisotropies,
                massCenters = solverImplementation.normals,
                prevMassCenters = solverImplementation.renderablePositions,
                densityKernel = new Poly6Kernel(solverAbstraction.parameters.mode == Oni.SolverParameters.Mode.Mode2D),
                batchData = batchData,
                solverParams = solverAbstraction.parameters
            };

            int batchCount = batchData.isLast ? batchData.workItemCount : 1;
            return updateDensities.Schedule(batchData.workItemCount, batchCount, inputDeps);
        }

        public override JobHandle Apply(JobHandle inputDeps, float substepTime)
        {
            var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

            // update densities and gradients:
            var apply = new ApplyDensityConstraintsJob()
            {
                principalRadii = solverImplementation.principalRadii,
                fluidMaterials = solverImplementation.fluidMaterials,
                pairs = ((BurstSolverImpl)constraints.solver).fluidInteractions,
                densityKernel = new Poly6Kernel(solverAbstraction.parameters.mode == Oni.SolverParameters.Mode.Mode2D),
                positions = solverImplementation.positions,
                fluidData = solverImplementation.fluidData,
                batchData = batchData,
                solverParams = solverAbstraction.parameters,
                sorFactor = parameters.SORFactor
            };

            int batchCount = batchData.isLast ? batchData.workItemCount : 1;
            return apply.Schedule(batchData.workItemCount, batchCount, inputDeps);
        }

        public JobHandle CalculateNormals(JobHandle inputDeps, float deltaTime)
        {
            int batchCount = batchData.isLast ? batchData.workItemCount : 1;

            var vorticity = new NormalsJob()
            {
                invMasses = solverImplementation.invMasses,
                principalRadii = solverImplementation.principalRadii,
                fluidMaterials = solverImplementation.fluidMaterials,
                fluidData = solverImplementation.fluidData,
                fluidInterface = solverImplementation.fluidInterface,
                userData = solverImplementation.userData,
                pairs = ((BurstSolverImpl)constraints.solver).fluidInteractions,
                normals = solverImplementation.normals,
                batchData = batchData,
                dt = deltaTime,
            };

            return vorticity.Schedule(batchData.workItemCount, batchCount, inputDeps);
        }

        public JobHandle ViscosityAndVorticity(JobHandle inputDeps)
        {
            var eta = new ViscosityVorticityJob()
            {
                positions = solverImplementation.positions,
                prevPositions = solverImplementation.prevPositions,
                orientations = solverImplementation.orientations,
                pairs = ((BurstSolverImpl)constraints.solver).fluidInteractions,
                massCenters = solverImplementation.normals,
                prevMassCenters = solverImplementation.renderablePositions,
                fluidParams = solverImplementation.fluidMaterials,
                deltas = solverImplementation.positionDeltas,
                counts = solverImplementation.positionConstraintCounts,
                batchData = batchData
            };

            int batchCount = batchData.isLast ? batchData.workItemCount : 1;
            return eta.Schedule(batchData.workItemCount, batchCount, inputDeps);
        }

        public JobHandle AccumulateSmoothPositions(JobHandle inputDeps)
        {
            var accumulateSmooth = new AccumulateSmoothPositionsJob()
            {
                renderablePositions = solverImplementation.renderablePositions,
                anisotropies = solverImplementation.anisotropies,
                fluidMaterials = solverImplementation.fluidMaterials,
                densityKernel = new Poly6Kernel(solverAbstraction.parameters.mode == Oni.SolverParameters.Mode.Mode2D),
                pairs = ((BurstSolverImpl)constraints.solver).fluidInteractions,
                batchData = batchData
            };

            int batchCount = batchData.isLast ? batchData.workItemCount : 1;
            return accumulateSmooth.Schedule(batchData.workItemCount, batchCount, inputDeps);
        }

        public JobHandle AccumulateAnisotropy(JobHandle inputDeps)
        {
            var accumulateAnisotropy = new AccumulateAnisotropyJob()
            {
                renderablePositions = solverImplementation.renderablePositions,
                anisotropies = solverImplementation.anisotropies,
                pairs = ((BurstSolverImpl)constraints.solver).fluidInteractions,
                batchData = batchData
            };

            int batchCount = batchData.isLast ? batchData.workItemCount : 1;
            return accumulateAnisotropy.Schedule(batchData.workItemCount, batchCount, inputDeps);
        }

        [BurstCompile]
        public struct UpdateDensitiesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4> positions;
            [ReadOnly] public NativeArray<float4> prevPositions;
            [ReadOnly] public NativeArray<float4> fluidMaterials;
            [ReadOnly] public NativeArray<float4> principalRadii;
            [ReadOnly] public NativeArray<FluidInteraction> pairs;

            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> fluidData;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4x4> moments;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> massCenters;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> prevMassCenters;

            [ReadOnly] public Poly6Kernel densityKernel;
            [ReadOnly] public BatchData batchData;

            [ReadOnly] public Oni.SolverParameters solverParams;

            public void Execute(int workItemIndex)
            {
                int start, end;
                batchData.GetConstraintRange(workItemIndex, out start, out end);

                for (int i = start; i < end; ++i)
                {
                    var pair = pairs[i];

                    float restVolumeA = math.pow(principalRadii[pair.particleA].x * 2, 3 - (int)solverParams.mode);
                    float restVolumeB = math.pow(principalRadii[pair.particleB].x * 2, 3 - (int)solverParams.mode);

                    float gradA = restVolumeB * pair.avgGradient;
                    float gradB = restVolumeA * pair.avgGradient;

                    float vA = restVolumeB / restVolumeA;
                    float vB = restVolumeA / restVolumeB;

                    // accumulate pbf data (density, gradients):
                    fluidData[pair.particleA] += new float4(vA * pair.avgKernel, 0, gradA, gradA * gradA);
                    fluidData[pair.particleB] += new float4(vB * pair.avgKernel, 0, gradB, gradB * gradB);

                    // accumulate masses for COMs and moment matrices:
                    float wAvg = pair.avgKernel / ((densityKernel.W(0, fluidMaterials[pair.particleA].x) + densityKernel.W(0, fluidMaterials[pair.particleB].x)) * 0.5f);

                    massCenters[pair.particleA] += wAvg * new float4(positions[pair.particleB].xyz, 1) / positions[pair.particleB].w;
                    massCenters[pair.particleB] += wAvg * new float4(positions[pair.particleA].xyz, 1) / positions[pair.particleA].w;

                    prevMassCenters[pair.particleA] += wAvg * new float4(prevPositions[pair.particleB].xyz, 1) / positions[pair.particleB].w;
                    prevMassCenters[pair.particleB] += wAvg * new float4(prevPositions[pair.particleA].xyz, 1) / positions[pair.particleA].w;

                    moments[pair.particleA] += wAvg * (BurstMath.multrnsp4(positions[pair.particleB], prevPositions[pair.particleB]) + float4x4.identity * math.pow(principalRadii[pair.particleB].x, 2) * 0.2f) / positions[pair.particleB].w;
                    moments[pair.particleB] += wAvg * (BurstMath.multrnsp4(positions[pair.particleA], prevPositions[pair.particleA]) + float4x4.identity * math.pow(principalRadii[pair.particleA].x, 2) * 0.2f) / positions[pair.particleA].w;
                }
            }
        }

        [BurstCompile]
        public struct ApplyDensityConstraintsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4> principalRadii;
            [ReadOnly] public NativeArray<float4> fluidMaterials;
            [ReadOnly] public NativeArray<FluidInteraction> pairs;
            [ReadOnly] public Poly6Kernel densityKernel;
            [ReadOnly] public CohesionKernel cohesionKernel;

            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> positions;
            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> fluidData;

            [ReadOnly] public BatchData batchData;
            [ReadOnly] public float sorFactor;
            [ReadOnly] public Oni.SolverParameters solverParams;

            public void Execute(int workItemIndex)
            {
                int start, end;
                batchData.GetConstraintRange(workItemIndex, out start, out end);

                for (int i = start; i < end; ++i)
                {
                    var pair = pairs[i];

                    float restVolumeA = math.pow(principalRadii[pair.particleA].x * 2, 3 - (int)solverParams.mode);
                    float restVolumeB = math.pow(principalRadii[pair.particleB].x * 2, 3 - (int)solverParams.mode);

                    float dist = math.length(positions[pair.particleA].xyz - positions[pair.particleB].xyz); // TODO: FIX! we cant read positions while we are writing to them.

                    // calculate tensile instability correction factor:
                    float cAvg = (cohesionKernel.W(dist, fluidMaterials[pair.particleA].x * 1.4f) + cohesionKernel.W(dist, fluidMaterials[pair.particleB].x * 1.4f)) * 0.5f;
                    float st = 0.2f * cAvg * (1 - math.saturate(math.abs(fluidMaterials[pair.particleA].y - fluidMaterials[pair.particleB].y))) * (fluidMaterials[pair.particleA].y + fluidMaterials[pair.particleB].y) * 0.5f;
                    float scorrA = -st / (positions[pair.particleA].w * fluidData[pair.particleA][3] + math.FLT_MIN_NORMAL);
                    float scorrB = -st / (positions[pair.particleB].w * fluidData[pair.particleB][3] + math.FLT_MIN_NORMAL);

                    // calculate position delta:
                    float4 delta = pair.gradient * pair.avgGradient * ((fluidData[pair.particleA][1] + scorrA) * restVolumeB + (fluidData[pair.particleB][1] + scorrB) * restVolumeA) * sorFactor;
                    delta.w = 0;
                    positions[pair.particleA] += delta * positions[pair.particleA].w;
                    positions[pair.particleB] -= delta * positions[pair.particleB].w;

                }
            }
        }

        [BurstCompile]
        public struct ViscosityVorticityJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4> positions;
            [ReadOnly] public NativeArray<float4> prevPositions;
            [ReadOnly] public NativeArray<quaternion> orientations;
            [ReadOnly] public NativeArray<float4> fluidParams;
            [ReadOnly] public NativeArray<FluidInteraction> pairs;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> massCenters;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> prevMassCenters;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> deltas;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<int> counts;

            [ReadOnly] public BatchData batchData;

            public void Execute(int workItemIndex)
            {
                int start, end;
                batchData.GetConstraintRange(workItemIndex, out start, out end);

                for (int i = start; i < end; ++i)
                {
                    var pair = pairs[i];

                    // viscosity:
                    float4 goalA = new float4(massCenters[pair.particleB].xyz + math.rotate(orientations[pair.particleB], (prevPositions[pair.particleA] - prevMassCenters[pair.particleB]).xyz), 0);
                    float4 goalB = new float4(massCenters[pair.particleA].xyz + math.rotate(orientations[pair.particleA], (prevPositions[pair.particleB] - prevMassCenters[pair.particleA]).xyz), 0);
                    deltas[pair.particleA] += (goalA - positions[pair.particleA]) * fluidParams[pair.particleB].z;
                    deltas[pair.particleB] += (goalB - positions[pair.particleB]) * fluidParams[pair.particleA].z;

                    // vorticity:
                    goalA = new float4(massCenters[pair.particleB].xyz + math.rotate(orientations[pair.particleB], (positions[pair.particleA] - massCenters[pair.particleB]).xyz), 0);
                    goalB = new float4(massCenters[pair.particleA].xyz + math.rotate(orientations[pair.particleA], (positions[pair.particleB] - massCenters[pair.particleA]).xyz), 0);
                    deltas[pair.particleA] += (goalA - positions[pair.particleA]) * fluidParams[pair.particleB].w * 0.1f;
                    deltas[pair.particleB] += (goalB - positions[pair.particleB]) * fluidParams[pair.particleA].w * 0.1f;

                    counts[pair.particleA]++;
                    counts[pair.particleB]++;
                }
            }
        }

        [BurstCompile]
        public struct NormalsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> invMasses;
            [ReadOnly] public NativeArray<float4> principalRadii;
            [ReadOnly] public NativeArray<float4> fluidMaterials;
            [ReadOnly] public NativeArray<float4> fluidInterface;
            [ReadOnly] public NativeArray<float4> fluidData;
            [ReadOnly] public NativeArray<FluidInteraction> pairs;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> userData;
            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> normals;

            [ReadOnly] public BatchData batchData;
            [ReadOnly] public Oni.SolverParameters solverParams;
            [ReadOnly] public float dt;

            public void Execute(int workItemIndex)
            {
                int start, end;
                batchData.GetConstraintRange(workItemIndex, out start, out end);

                for (int i = start; i < end; ++i)
                {
                    var pair = pairs[i];

                    float restVolumeA = math.pow(principalRadii[pair.particleA].x * 2, 3 - (int)solverParams.mode);
                    float restVolumeB = math.pow(principalRadii[pair.particleB].x * 2, 3 - (int)solverParams.mode);

                    // property diffusion:
                    float diffusionSpeed = (fluidInterface[pair.particleA].w + fluidInterface[pair.particleB].w) * pair.avgKernel * dt;
                    float4 userDelta = (userData[pair.particleB] - userData[pair.particleA]) * diffusionSpeed;
                    userData[pair.particleA] += restVolumeB / restVolumeA * userDelta;
                    userData[pair.particleB] -= restVolumeA / restVolumeB * userDelta;

                    // calculate color field  normal:
                    float4 vgrad = pair.gradient * pair.avgGradient;
                    float radius = (fluidMaterials[pair.particleA].x + fluidMaterials[pair.particleB].x) * 0.5f;
                    normals[pair.particleA] += vgrad * radius * restVolumeB;
                    normals[pair.particleB] -= vgrad * radius * restVolumeA;
                }
            }
        }

        [BurstCompile]
        public struct AccumulateSmoothPositionsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4> renderablePositions;
            [ReadOnly] public NativeArray<float4> fluidMaterials;
            [ReadOnly] public Poly6Kernel densityKernel;

            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4x4> anisotropies;
            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<FluidInteraction> pairs;

            [ReadOnly] public BatchData batchData;

            public void Execute(int workItemIndex)
            {
                int start, end;
                batchData.GetConstraintRange(workItemIndex, out start, out end);

                for (int i = start; i < end; ++i)
                {
                    var pair = pairs[i];

                    float distance = math.length((renderablePositions[pair.particleA] - renderablePositions[pair.particleB]).xyz);

                    pair.avgKernel = (densityKernel.W(distance, fluidMaterials[pair.particleA].x) +
                                      densityKernel.W(distance, fluidMaterials[pair.particleB].x)) * 0.5f;

                    var A = anisotropies[pair.particleA];
                    var B = anisotropies[pair.particleB];

                    A.c3 += new float4(renderablePositions[pair.particleB].xyz,1) * pair.avgKernel;
                    B.c3 += new float4(renderablePositions[pair.particleA].xyz,1) * pair.avgKernel;

                    anisotropies[pair.particleA] = A;
                    anisotropies[pair.particleB] = B;

                    pairs[i] = pair;
                }
            }
        }

        [BurstCompile]
        public struct AccumulateAnisotropyJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4> renderablePositions;
            [ReadOnly] public NativeArray<FluidInteraction> pairs;

            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4x4> anisotropies;

            [ReadOnly] public BatchData batchData;

            public void Execute(int workItemIndex)
            {
                int start, end;
                batchData.GetConstraintRange(workItemIndex, out start, out end);

                for (int i = start; i < end; ++i)
                {
                    var pair = pairs[i];

                    float4 distanceA = (renderablePositions[pair.particleB] - anisotropies[pair.particleA].c3) * pair.avgKernel;
                    float4 distanceB = (renderablePositions[pair.particleA] - anisotropies[pair.particleB].c3) * pair.avgKernel;

                    anisotropies[pair.particleA] += BurstMath.multrnsp4(distanceA,distanceA);
                    anisotropies[pair.particleB] += BurstMath.multrnsp4(distanceB,distanceB);
                }
            }
        }

    }
}
#endif
