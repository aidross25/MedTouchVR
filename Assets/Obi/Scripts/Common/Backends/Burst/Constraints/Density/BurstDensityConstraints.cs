#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    public class BurstDensityConstraints : BurstConstraintsImpl<BurstDensityConstraintsBatch>
    {
        public NativeList<int> fluidParticles;

        public BurstDensityConstraints(BurstSolverImpl solver) : base(solver, Oni.ConstraintType.Density)
        {
            fluidParticles = new NativeList<int>(Allocator.Persistent);
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new BurstDensityConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void Dispose()
        {
            fluidParticles.Dispose();
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as BurstDensityConstraintsBatch);
            batch.Destroy();
        }

        protected override JobHandle EvaluateSequential(JobHandle inputDeps, float stepTime, float substepTime, int steps, float timeLeft)
        {
            return EvaluateParallel(inputDeps, stepTime, substepTime, steps, timeLeft);
        }

        protected override JobHandle EvaluateParallel(JobHandle inputDeps, float stepTime, float substepTime, int steps, float timeLeft)
        {
            inputDeps = UpdateInteractions(inputDeps);

            // evaluate all batches as a chain of dependencies:
            for (int i = 0; i < batches.Count; ++i)
            {
                if (batches[i].enabled)
                {
                    inputDeps = batches[i].Evaluate(inputDeps, stepTime, substepTime, steps, timeLeft);
                    m_Solver.ScheduleBatchedJobsIfNeeded();
                }
            }

            // calculate per-particle density lambdas:
            inputDeps = CalculateLambdas(inputDeps, substepTime);

            // calculate viscosity/vorticity:
            for (int i = 0; i < batches.Count; ++i)
            {
                if (batches[i].enabled)
                {
                    inputDeps = batches[i].ViscosityAndVorticity(inputDeps);
                    m_Solver.ScheduleBatchedJobsIfNeeded();
                }
            }

            // apply viscosity/vorticity positional deltas:
            var app = new ApplyPositionDeltasJob()
            {
                fluidParticles = fluidParticles,
                positions = m_Solver.positions,
                orientations = m_Solver.orientations,
                prevOrientations = m_Solver.prevOrientations,
                deltas = m_Solver.positionDeltas,
                counts = m_Solver.positionConstraintCounts,
                anisotropies = m_Solver.anisotropies,
                normals = m_Solver.normals
            };

            inputDeps = app.Schedule(fluidParticles.Length, 64, inputDeps);

            // apply density positional deltas::
            for (int i = 0; i < batches.Count; ++i)
            {
                if (batches[i].enabled)
                {
                    inputDeps = batches[i].Apply(inputDeps, substepTime);
                    m_Solver.ScheduleBatchedJobsIfNeeded();
                }
            }

            return inputDeps;
        }

        public JobHandle ApplyVelocityCorrections(JobHandle inputDeps, float deltaTime)
        {
            for (int i = 0; i < batches.Count; ++i)
            {
                if (batches[i].enabled)
                {
                    inputDeps = batches[i].CalculateNormals(inputDeps, deltaTime);
                    m_Solver.ScheduleBatchedJobsIfNeeded();
                }
            }

            inputDeps = ApplyAtmosphere(inputDeps, deltaTime);
            m_Solver.ScheduleBatchedJobsIfNeeded();

            return inputDeps;
        }

        public JobHandle CalculateAnisotropyLaplacianSmoothing(JobHandle inputDeps)
        {
            // if the constraints are deactivated or we need no anisotropy:
            if (((BurstSolverImpl)solver).abstraction.parameters.maxAnisotropy <= 1)
                return inputDeps;

            for (int i = 0; i < batches.Count; ++i)
            {
                if (batches[i].enabled)
                {
                    inputDeps = batches[i].AccumulateSmoothPositions(inputDeps);
                    m_Solver.ScheduleBatchedJobsIfNeeded();
                }
            }

            inputDeps = AverageSmoothPositions(inputDeps);

            for (int i = 0; i < batches.Count; ++i)
            {
                if (batches[i].enabled)
                {
                    inputDeps = batches[i].AccumulateAnisotropy(inputDeps);
                    m_Solver.ScheduleBatchedJobsIfNeeded();
                }
            }
             
            return AverageAnisotropy(inputDeps);
        }

        private JobHandle UpdateInteractions(JobHandle inputDeps)
        {
            // clear existing fluid data:
            var clearData = new ClearFluidDataJob()
            {
                fluidParticles = fluidParticles,
                fluidData = m_Solver.fluidData,
                massCenters = m_Solver.normals,
                prevMassCenters = m_Solver.renderablePositions,
                moments = m_Solver.anisotropies
            };

            inputDeps = clearData.Schedule(fluidParticles.Length, 64, inputDeps);

            // update fluid interactions:
            var updateInteractions = new UpdateInteractionsJob()
            {
                pairs = m_Solver.fluidInteractions,
                positions = m_Solver.positions,
                fluidMaterials = m_Solver.fluidMaterials,
                densityKernel = new Poly6Kernel(((BurstSolverImpl)solver).abstraction.parameters.mode == Oni.SolverParameters.Mode.Mode2D),
                gradientKernel = new SpikyKernel(((BurstSolverImpl)solver).abstraction.parameters.mode == Oni.SolverParameters.Mode.Mode2D),
            };

            return updateInteractions.Schedule(((BurstSolverImpl)solver).fluidInteractions.Length, 64, inputDeps);
        }

        private JobHandle CalculateLambdas(JobHandle inputDeps, float deltaTime)
        {
            // calculate lagrange multipliers:
            var calculateLambdas = new CalculateLambdasJob()
            {
                fluidParticles = fluidParticles,
                positions = m_Solver.positions,
                prevPositions = m_Solver.prevPositions,
                orientations = m_Solver.orientations,
                principalRadii = m_Solver.principalRadii,
                fluidMaterials = m_Solver.fluidMaterials,
                densityKernel = new Poly6Kernel(m_Solver.abstraction.parameters.mode == Oni.SolverParameters.Mode.Mode2D),
                gradientKernel = new SpikyKernel(m_Solver.abstraction.parameters.mode == Oni.SolverParameters.Mode.Mode2D),
                fluidData = m_Solver.fluidData,

                massCenters = m_Solver.normals,
                prevMassCenters = m_Solver.renderablePositions,
                moments = m_Solver.anisotropies,

                deltas = m_Solver.positionDeltas,
                counts = m_Solver.positionConstraintCounts,

                solverParams = m_Solver.abstraction.parameters
            };

            return calculateLambdas.Schedule(fluidParticles.Length,64,inputDeps);
        }

        private JobHandle ApplyAtmosphere(JobHandle inputDeps, float deltaTime)
        {
            var conf = new ApplyAtmosphereJob()
            {
                fluidParticles = fluidParticles,
                wind = m_Solver.wind,
                fluidInterface = m_Solver.fluidInterface,
                fluidMaterials = m_Solver.fluidMaterials,
                principalRadii = m_Solver.principalRadii,
                normals = m_Solver.normals,
                fluidData = m_Solver.fluidData,
                velocities = m_Solver.velocities,
                dt = deltaTime,
                solverParams = m_Solver.abstraction.parameters
            };

            return conf.Schedule(fluidParticles.Length, 64, inputDeps);
        }

        private JobHandle AverageSmoothPositions(JobHandle inputDeps)
        {
            var average = new AverageSmoothPositionsJob()
            {
                fluidParticles = fluidParticles,
                renderablePositions = m_Solver.renderablePositions,
                anisotropies = m_Solver.anisotropies
            };

            return average.Schedule(fluidParticles.Length, 64, inputDeps);
        }

        private JobHandle AverageAnisotropy(JobHandle inputDeps)
        {
            var average = new AverageAnisotropyJob()
            {
                fluidParticles = fluidParticles,
                renderablePositions = m_Solver.renderablePositions,
                renderableOrientations = m_Solver.renderableOrientations,
                principalRadii = m_Solver.principalRadii,
                anisotropies = m_Solver.anisotropies,
                maxAnisotropy = m_Solver.abstraction.parameters.maxAnisotropy,
                renderableRadii = m_Solver.renderableRadii,
                fluidData = m_Solver.fluidData,
                solverParams = m_Solver.abstraction.parameters
            };

            return average.Schedule(fluidParticles.Length, 64, inputDeps);
        }

        [BurstCompile]
        public struct ClearFluidDataJob : IJobParallelFor
        {
            [ReadOnly] public NativeList<int> fluidParticles;
            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> fluidData;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> massCenters;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> prevMassCenters;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4x4> moments;

            public void Execute(int i)
            {
                int p = fluidParticles[i];
                fluidData[p] = float4.zero;
                massCenters[p] = float4.zero;
                prevMassCenters[p] = float4.zero;
                moments[p] = float4x4.zero;
            }
        }

        [BurstCompile]
        public struct UpdateInteractionsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4> positions;
            [ReadOnly] public NativeArray<float4> fluidMaterials;
            [ReadOnly] public Poly6Kernel densityKernel;
            [ReadOnly] public SpikyKernel gradientKernel;

            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<FluidInteraction> pairs;

            [ReadOnly] public BatchData batchData;

            public void Execute(int i)
            {
                var pair = pairs[i];

                // calculate normalized gradient vector:
                pair.gradient = new float4((positions[pair.particleA] - positions[pair.particleB]).xyz,0);
                float distance = math.length(pair.gradient);
                pair.gradient /= distance + math.FLT_MIN_NORMAL;

                // calculate and store average density and gradient kernels:
                pair.avgKernel = (densityKernel.W(distance, fluidMaterials[pair.particleA].x) +
                                  densityKernel.W(distance, fluidMaterials[pair.particleB].x)) * 0.5f;

                pair.avgGradient = (gradientKernel.W(distance, fluidMaterials[pair.particleA].x) +
                                    gradientKernel.W(distance, fluidMaterials[pair.particleB].x)) * 0.5f;

                pairs[i] = pair;
            }
        }

        [BurstCompile]
        public struct CalculateLambdasJob : IJobParallelFor
        {
            [ReadOnly] public NativeList<int> fluidParticles;
            [ReadOnly] public NativeArray<float4> positions;
            [ReadOnly] public NativeArray<float4> prevPositions;
            [ReadOnly] public NativeArray<float4> principalRadii;
            [ReadOnly] public NativeArray<float4> fluidMaterials;
            [ReadOnly] public Poly6Kernel densityKernel;
            [ReadOnly] public SpikyKernel gradientKernel;

            [ReadOnly] public Oni.SolverParameters solverParams;

            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> fluidData;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> massCenters;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> prevMassCenters;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4x4> moments;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<quaternion> orientations;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> deltas;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<int> counts;

            public void Execute(int p)
            {
                int i = fluidParticles[p];

                float restVolume = math.pow(principalRadii[i].x * 2, 3 - (int)solverParams.mode);
                float4 data = fluidData[i];

                float grad = restVolume * gradientKernel.W(0, fluidMaterials[i].x);

                // self particle contribution to density, gradient and mass centers:
                data += new float4(densityKernel.W(0, fluidMaterials[i].x), 0, grad, grad * grad + data[2] * data[2]);
                massCenters[i] += new float4(positions[i].xyz, 1) / positions[i].w;
                prevMassCenters[i] += new float4(prevPositions[i].xyz, 1) / positions[i].w;

                // usually, we'd weight density by mass (density contrast formulation) by dividing by invMass. Then, multiply by invMass when
                // calculating the state equation (density / restDensity - 1, restDensity = mass / volume, so density * invMass * restVolume - 1
                // We end up with density / invMass * invMass * restVolume - 1, invMass cancels out.
                float constraint = math.max(0, data[0] * restVolume - 1);

                // calculate lambda:
                data[1] = -constraint / (positions[i].w * data[3] + math.FLT_MIN_NORMAL);

                fluidData[i] = data;

                // get total neighborhood mass:
                float M = massCenters[i][3];
                massCenters[i] /= massCenters[i][3];
                prevMassCenters[i] /= prevMassCenters[i][3];

                // update moments:
                moments[i] += (BurstMath.multrnsp4(positions[i], prevPositions[i]) + float4x4.identity * math.pow(principalRadii[i].x, 2) * 0.2f) / positions[i].w;
                moments[i] -= M * BurstMath.multrnsp4(massCenters[i], prevMassCenters[i]);

                // extract neighborhood orientation delta:
                orientations[i] = BurstMath.ExtractRotation(moments[i], quaternion.identity, 2);

                // viscosity and vorticity:
                float4 viscGoal = new float4(massCenters[i].xyz + math.rotate(orientations[i], (prevPositions[i] - prevMassCenters[i]).xyz), 0);
                float4 vortGoal = new float4(massCenters[i].xyz + math.rotate(orientations[i], (positions[i] - massCenters[i]).xyz), 0);
                deltas[i] += (viscGoal - positions[i]) * fluidMaterials[i].z;
                deltas[i] += (vortGoal - positions[i]) * fluidMaterials[i].w * 0.1f;

                counts[i]++;
            }
        }

        [BurstCompile]
        public struct ApplyPositionDeltasJob : IJobParallelFor
        {
            [ReadOnly] public NativeList<int> fluidParticles;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> positions;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> deltas;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<int> counts;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<quaternion> orientations;
            [ReadOnly] public NativeArray<quaternion> prevOrientations;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> normals;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4x4> anisotropies;

            public void Execute(int p)
            {
                int i = fluidParticles[p];

                if (counts[i] > 0)
                {
                    positions[i] += new float4(deltas[i].xyz,0) / counts[i];
                    deltas[i] = float4.zero;
                    counts[i] = 0;
                }

                normals[i] = float4.zero;
                anisotropies[i] = float4x4.zero;

                orientations[i] = math.mul(orientations[i], prevOrientations[i]);
            }
        }

        [BurstCompile]
        public struct ApplyAtmosphereJob : IJobParallelFor
        {
            [ReadOnly] public NativeList<int> fluidParticles;
            [ReadOnly] public NativeArray<float4> wind;
            [ReadOnly] public NativeArray<float4> fluidInterface;
            [ReadOnly] public NativeArray<float4> fluidMaterials;
            [ReadOnly] public NativeArray<float4> principalRadii;
            [ReadOnly] public NativeArray<float4> normals;
            [ReadOnly] public NativeArray<float4> fluidData;

            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> velocities;

            [ReadOnly] public float dt;
            [ReadOnly] public Oni.SolverParameters solverParams;

            public void Execute(int p)
            {
                int i = fluidParticles[p];

                float restVolume = math.pow(principalRadii[i].x * 2, 3 - (int)solverParams.mode);

                //atmospheric drag:
                float4 velocityDiff = velocities[i] - wind[i];

                // particles near the surface should experience drag:
                velocities[i] -= fluidInterface[i].x * velocityDiff * math.max(0, 1 - fluidData[i][0] * restVolume) * dt;

                // ambient pressure:
                velocities[i] += fluidInterface[i].y * normals[i] * dt;
            }
        }

        [BurstCompile]
        public struct AverageSmoothPositionsJob : IJobParallelFor
        {
            [ReadOnly] public NativeList<int> fluidParticles;
            [ReadOnly] public NativeArray<float4> renderablePositions;

            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4x4> anisotropies;

            public void Execute(int p)
            {
                int i = fluidParticles[p];

                var smoothPos = anisotropies[i];

                if (smoothPos.c3.w > 0)
                    smoothPos.c3 /= smoothPos.c3.w;
                else
                    smoothPos.c3.xyz = renderablePositions[i].xyz;

                anisotropies[i] = smoothPos;
            }
        }

        [BurstCompile]
        public struct AverageAnisotropyJob : IJobParallelFor
        {
            [ReadOnly] public NativeList<int> fluidParticles;
            [ReadOnly] public NativeArray<float4> principalRadii;
            [ReadOnly] public float maxAnisotropy;
            [ReadOnly] public NativeArray<float4x4> anisotropies;

            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> fluidData;
            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> renderablePositions;
            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<quaternion> renderableOrientations;
            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> renderableRadii;

            [ReadOnly] public Oni.SolverParameters solverParams;

            public void Execute(int p)
            {
                int i = fluidParticles[p];

                if (anisotropies[i].c3.w > 0 && (anisotropies[i].c0[0] + anisotropies[i].c1[1] + anisotropies[i].c2[2]) > 0.01f)
                {
                    float3 singularValues;
                    float3x3 u;
                    BurstMath.EigenSolve(math.float3x3(anisotropies[i] / anisotropies[i].c3.w), out singularValues, out u);

                    float max = singularValues[0];
                    float3 s = math.max(singularValues,new float3(max / maxAnisotropy)) / max * principalRadii[i].x;

                    renderableOrientations[i] = quaternion.LookRotationSafe(u.c2,u.c1);
                    renderableRadii[i] = new float4(s.xyz,1);
                }
                else
                {
                    float radius = principalRadii[i].x / maxAnisotropy;
                    renderableOrientations[i] = quaternion.identity;
                    renderableRadii[i] = new float4(radius,radius,radius,1);

                    float4 data = fluidData[i];
                    data.x = 1 / math.pow(math.abs(radius * 2), 3 - (int)solverParams.mode); // normal volume of an isolated particle.
                    fluidData[i] = data;
                }

                renderablePositions[i] = math.lerp(renderablePositions[i], anisotropies[i].c3, math.min((maxAnisotropy - 1)/3.0f,1));
            }
        }
    }
}
#endif