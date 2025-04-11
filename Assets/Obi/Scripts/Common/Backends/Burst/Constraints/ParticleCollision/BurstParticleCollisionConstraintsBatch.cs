#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections;

namespace Obi
{
    public class BurstParticleCollisionConstraintsBatch : BurstConstraintsBatchImpl, IParticleCollisionConstraintsBatchImpl
    {
        public BatchData batchData;

        public BurstParticleCollisionConstraintsBatch(BurstParticleCollisionConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.ParticleCollision;
        }

        public BurstParticleCollisionConstraintsBatch(BatchData batchData) : base()
        {
            this.batchData = batchData;
        }

        public override JobHandle Initialize(JobHandle inputDeps, float stepTime, float substepTime, int steps, float timeLeft)
        {
            var updateContacts = new UpdateParticleContactsJob()
            {
                prevPositions = solverImplementation.prevPositions,
                prevOrientations = solverImplementation.prevOrientations,
                velocities = solverImplementation.velocities,
                radii = solverImplementation.principalRadii,
                invMasses = solverImplementation.invMasses,
                invRotationalMasses = solverImplementation.invRotationalMasses,

                simplices = solverImplementation.simplices,
                simplexCounts = solverImplementation.simplexCounts,

                particleMaterialIndices = solverImplementation.collisionMaterials,
                collisionMaterials = ObiColliderWorld.GetInstance().collisionMaterials.AsNativeArray<BurstCollisionMaterial>(),

                contacts = ((BurstSolverImpl)constraints.solver).abstraction.particleContacts.AsNativeArray<BurstContact>(),
                effectiveMasses = ((BurstSolverImpl)constraints.solver).abstraction.particleContactEffectiveMasses.AsNativeArray<ContactEffectiveMasses>(),
                batchData = batchData
            };

            int batchCount = batchData.isLast ? batchData.workItemCount : 1;
            return updateContacts.Schedule(batchData.workItemCount, batchCount, inputDeps);
        }

        public override JobHandle Evaluate(JobHandle inputDeps, float stepTime, float substepTime, int steps, float timeLeft)
        {
            var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

            var projectConstraints = new ParticleCollisionConstraintsBatchJob()
            {
                positions = solverImplementation.positions,
                orientations = solverImplementation.orientations,
                invMasses = solverImplementation.invMasses,
                radii = solverImplementation.principalRadii,
                particleMaterialIndices = solverImplementation.collisionMaterials,
                collisionMaterials = ObiColliderWorld.GetInstance().collisionMaterials.AsNativeArray<BurstCollisionMaterial>(),

                simplices = solverImplementation.simplices,
                simplexCounts = solverImplementation.simplexCounts,

                deltas = solverImplementation.positionDeltas,
                counts = solverImplementation.positionConstraintCounts,
                contacts = solverAbstraction.particleContacts.AsNativeArray<BurstContact>(),
                effectiveMasses = ((BurstSolverImpl)constraints.solver).abstraction.particleContactEffectiveMasses.AsNativeArray<ContactEffectiveMasses>(),
                batchData = batchData,

                constraintParameters = parameters,
                solverParameters = solverImplementation.abstraction.parameters,
                gravity = new float4(solverImplementation.abstraction.parameters.gravity, 0),
                substepTime = substepTime,
            };

            int batchCount = batchData.isLast ? batchData.workItemCount : 1;
            return projectConstraints.Schedule(batchData.workItemCount, batchCount, inputDeps);
        }

        public override JobHandle Apply(JobHandle inputDeps, float substepTime)
        {
            var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

            var applyConstraints = new ApplyBatchedCollisionConstraintsBatchJob()
            {
                contacts = solverAbstraction.particleContacts.AsNativeArray<BurstContact>(),

                simplices = solverImplementation.simplices,
                simplexCounts = solverImplementation.simplexCounts,

                positions = solverImplementation.positions,
                deltas = solverImplementation.positionDeltas,
                counts = solverImplementation.positionConstraintCounts,
                orientations = solverImplementation.orientations,
                orientationDeltas = solverImplementation.orientationDeltas,
                orientationCounts = solverImplementation.orientationConstraintCounts,
                batchData = batchData,
                constraintParameters = parameters,
            };

            int batchCount = batchData.isLast ? batchData.workItemCount : 1;
            return applyConstraints.Schedule(batchData.workItemCount, batchCount, inputDeps);
        }

        /**
         * Updates contact data (contact distance and frame) at the beginning of each substep. This is
         * necessary because contacts are generated only once at the beginning of each step, not every substep.
         */
        [BurstCompile]
        public struct UpdateParticleContactsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4> prevPositions;
            [ReadOnly] public NativeArray<quaternion> prevOrientations;
            [ReadOnly] public NativeArray<float4> velocities;
            [ReadOnly] public NativeArray<float4> radii;
            [ReadOnly] public NativeArray<float> invMasses;
            [ReadOnly] public NativeArray<float> invRotationalMasses;

            [ReadOnly] public NativeArray<int> particleMaterialIndices;
            [ReadOnly] public NativeArray<BurstCollisionMaterial> collisionMaterials;

            // simplex arrays:
            [ReadOnly] public NativeArray<int> simplices;
            [ReadOnly] public SimplexCounts simplexCounts;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<ContactEffectiveMasses> effectiveMasses;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<BurstContact> contacts;

            [ReadOnly] public BatchData batchData;

            public void Execute(int workItemIndex)
            {
                int start, end;
                batchData.GetConstraintRange(workItemIndex, out start, out end);

                for (int i = start; i < end; ++i)
                {
                    var contact = contacts[i];
                    var efMasses = effectiveMasses[i];

                    int simplexStartA = simplexCounts.GetSimplexStartAndSize(contact.bodyA, out int simplexSizeA);
                    int simplexStartB = simplexCounts.GetSimplexStartAndSize(contact.bodyB, out int simplexSizeB);

                    float4 simplexVelocityA = float4.zero;
                    float4 simplexPrevPositionA = float4.zero;
                    quaternion simplexPrevOrientationA = new quaternion(0, 0, 0, 0);
                    float simplexRadiusA = 0;
                    float simplexInvMassA = 0;
                    float simplexInvRotationalMassA = 0;

                    float4 simplexVelocityB = float4.zero;
                    float4 simplexPrevPositionB = float4.zero;
                    quaternion simplexPrevOrientationB = new quaternion(0, 0, 0, 0);
                    float simplexRadiusB = 0;
                    float simplexInvMassB = 0;
                    float simplexInvRotationalMassB = 0;

                    for (int j = 0; j < simplexSizeA; ++j)
                    {
                        int particleIndex = simplices[simplexStartA + j];
                        simplexVelocityA += velocities[particleIndex] * contact.pointA[j];
                        simplexPrevPositionA += prevPositions[particleIndex] * contact.pointA[j];
                        simplexPrevOrientationA.value += prevOrientations[particleIndex].value * contact.pointA[j];
                        simplexInvMassA += invMasses[particleIndex] * contact.pointA[j];
                        simplexInvRotationalMassA += invRotationalMasses[particleIndex] * contact.pointA[j];
                        simplexRadiusA += BurstMath.EllipsoidRadius(contact.normal, prevOrientations[particleIndex], radii[particleIndex].xyz) * contact.pointA[j];
                    }

                    for (int j = 0; j < simplexSizeB; ++j)
                    {
                        int particleIndex = simplices[simplexStartB + j];
                        simplexVelocityB += velocities[particleIndex] * contact.pointB[j];
                        simplexPrevPositionB += prevPositions[particleIndex] * contact.pointB[j];
                        simplexPrevOrientationB.value += prevOrientations[particleIndex].value * contact.pointB[j];
                        simplexInvMassB += invMasses[particleIndex] * contact.pointB[j];
                        simplexInvRotationalMassB += invRotationalMasses[particleIndex] * contact.pointB[j];
                        simplexRadiusB += BurstMath.EllipsoidRadius(contact.normal, prevOrientations[particleIndex], radii[particleIndex].xyz) * contact.pointB[j];
                    }

                    // update contact distance
                    float dAB = math.dot(simplexPrevPositionA - simplexPrevPositionB, contact.normal);
                    contact.distance = dAB - (simplexRadiusA + simplexRadiusB);

                    // calculate contact points:
                    float4 contactPointA = simplexPrevPositionB + contact.normal * (contact.distance + simplexRadiusB);
                    float4 contactPointB = simplexPrevPositionA - contact.normal * (contact.distance + simplexRadiusA);

                    // update contact basis:
                    contact.CalculateTangent(simplexVelocityA - simplexVelocityB);

                    // update contact masses:
                    int aMaterialIndex = particleMaterialIndices[simplices[simplexStartA]];
                    int bMaterialIndex = particleMaterialIndices[simplices[simplexStartB]];
                    bool rollingContacts = (aMaterialIndex >= 0 ? collisionMaterials[aMaterialIndex].rollingContacts > 0 : false) |
                                           (bMaterialIndex >= 0 ? collisionMaterials[bMaterialIndex].rollingContacts > 0 : false);

                    float4 invInertiaTensorA = math.rcp(BurstMath.GetParticleInertiaTensor(simplexRadiusA, simplexInvRotationalMassA) + new float4(BurstMath.epsilon));
                    float4 invInertiaTensorB = math.rcp(BurstMath.GetParticleInertiaTensor(simplexRadiusB, simplexInvRotationalMassB) + new float4(BurstMath.epsilon));

                    efMasses.CalculateContactMassesA(simplexInvMassA, invInertiaTensorA, simplexPrevPositionA, simplexPrevOrientationA, contactPointA, contact.normal, contact.tangent, contact.bitangent, rollingContacts);
                    efMasses.CalculateContactMassesB(simplexInvMassB, invInertiaTensorB, simplexPrevPositionB, simplexPrevOrientationB, contactPointB, contact.normal, contact.tangent, contact.bitangent, rollingContacts);

                    contacts[i] = contact;
                    effectiveMasses[i] = efMasses;
                }
            }
        }

        [BurstCompile]
        public struct ParticleCollisionConstraintsBatchJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<quaternion> orientations;
            [ReadOnly] public NativeArray<float> invMasses;
            [ReadOnly] public NativeArray<float4> radii;
            [ReadOnly] public NativeArray<int> particleMaterialIndices;
            [ReadOnly] public NativeArray<BurstCollisionMaterial> collisionMaterials;

            // simplex arrays:
            [ReadOnly] public NativeArray<int> simplices;
            [ReadOnly] public SimplexCounts simplexCounts;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> positions;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> deltas;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<int> counts;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<BurstContact> contacts;
            [ReadOnly] public NativeArray<ContactEffectiveMasses> effectiveMasses;

            [ReadOnly] public Oni.ConstraintParameters constraintParameters;
            [ReadOnly] public Oni.SolverParameters solverParameters;
            [ReadOnly] public float4 gravity;
            [ReadOnly] public float substepTime;

            [ReadOnly] public BatchData batchData;

            public void Execute(int workItemIndex)
            {
                int start, end;
                batchData.GetConstraintRange(workItemIndex, out start, out end);

                for (int i = start; i < end; ++i)
                {
                    var contact = contacts[i];

                    int simplexStartA = simplexCounts.GetSimplexStartAndSize(contact.bodyA, out int simplexSizeA);
                    int simplexStartB = simplexCounts.GetSimplexStartAndSize(contact.bodyB, out int simplexSizeB);


                    // Combine collision materials:
                    BurstCollisionMaterial material = CombineCollisionMaterials(simplices[simplexStartA], simplices[simplexStartB]);

                    float4 simplexPositionA = float4.zero, simplexPositionB = float4.zero;
                    float simplexRadiusA = 0, simplexRadiusB = 0;

                    for (int j = 0; j < simplexSizeA; ++j)
                    {
                        int particleIndex = simplices[simplexStartA + j];
                        simplexPositionA += positions[particleIndex] * contact.pointA[j];
                        simplexRadiusA += BurstMath.EllipsoidRadius(contact.normal, orientations[particleIndex], radii[particleIndex].xyz) * contact.pointA[j];
                    }
                    for (int j = 0; j < simplexSizeB; ++j)
                    {
                        int particleIndex = simplices[simplexStartB + j];
                        simplexPositionB += positions[particleIndex] * contact.pointB[j];
                        simplexRadiusB += BurstMath.EllipsoidRadius(contact.normal, orientations[particleIndex], radii[particleIndex].xyz) * contact.pointA[j];
                    }

                    float4 posA = simplexPositionA - contact.normal * simplexRadiusA;
                    float4 posB = simplexPositionB + contact.normal * simplexRadiusB;

                    // adhesion:
                    float lambda = contact.SolveAdhesion(effectiveMasses[i].TotalNormalInvMass, posA, posB, material.stickDistance, material.stickiness, substepTime);

                    // depenetration:
                    lambda += contact.SolvePenetration(effectiveMasses[i].TotalNormalInvMass, posA, posB, solverParameters.maxDepenetration * substepTime);

                    // Apply normal impulse to both particles (w/ shock propagation):
                    if (math.abs(lambda) > BurstMath.epsilon)
                    {
                        float shock = solverParameters.shockPropagation * math.dot(contact.normal, math.normalizesafe(gravity));
                        float4 delta = lambda * contact.normal;

                        float baryScale = BurstMath.BaryScale(contact.pointA);
                        for (int j = 0; j < simplexSizeA; ++j)
                        {
                            int particleIndex = simplices[simplexStartA + j];
                            deltas[particleIndex] += delta * invMasses[particleIndex] * contact.pointA[j] * baryScale * (1 - shock);
                            counts[particleIndex]++;
                        }

                        baryScale = BurstMath.BaryScale(contact.pointB);
                        for (int j = 0; j < simplexSizeB; ++j)
                        {
                            int particleIndex = simplices[simplexStartB + j];
                            deltas[particleIndex] -= delta * invMasses[particleIndex] * contact.pointB[j] * baryScale * (1 + shock);
                            counts[particleIndex]++;
                        }
                    }

                    // Apply position deltas immediately, if using sequential evaluation:
                    if (constraintParameters.evaluationOrder == Oni.ConstraintParameters.EvaluationOrder.Sequential)
                    {
                        for (int j = 0; j < simplexSizeA; ++j)
                        {
                            int particleIndex = simplices[simplexStartA + j];
                            BurstConstraintsBatchImpl.ApplyPositionDelta(particleIndex, constraintParameters.SORFactor, ref positions, ref deltas, ref counts);
                        }

                        for (int j = 0; j < simplexSizeB; ++j)
                        {
                            int particleIndex = simplices[simplexStartB + j];
                            BurstConstraintsBatchImpl.ApplyPositionDelta(particleIndex, constraintParameters.SORFactor, ref positions, ref deltas, ref counts);
                        }
                    }

                    contacts[i] = contact;
                }
            }

            private BurstCollisionMaterial CombineCollisionMaterials(int entityA, int entityB)
            {
                // Combine collision materials:
                int aMaterialIndex = particleMaterialIndices[entityA];
                int bMaterialIndex = particleMaterialIndices[entityB];

                if (aMaterialIndex >= 0 && bMaterialIndex >= 0)
                    return BurstCollisionMaterial.CombineWith(collisionMaterials[aMaterialIndex], collisionMaterials[bMaterialIndex]);
                else if (aMaterialIndex >= 0)
                    return collisionMaterials[aMaterialIndex];
                else if (bMaterialIndex >= 0)
                    return collisionMaterials[bMaterialIndex];

                return new BurstCollisionMaterial();
            }
        }


    }
}
#endif