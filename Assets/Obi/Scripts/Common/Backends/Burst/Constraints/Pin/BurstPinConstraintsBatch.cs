#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections.Generic;
using System.Threading;

namespace Obi
{
    public class BurstPinConstraintsBatch : BurstConstraintsBatchImpl, IPinConstraintsBatchImpl
    {
        private NativeArray<int> colliderIndices;
        private NativeArray<float4> offsets;
        private NativeArray<quaternion> restDarbouxVectors;
        private NativeArray<float2> stiffnesses;

        public BurstPinConstraintsBatch(BurstPinConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Pin;
        }

        public void SetPinConstraints(ObiNativeIntList particleIndices, ObiNativeIntList colliderIndices, ObiNativeVector4List offsets, ObiNativeQuaternionList restDarbouxVectors, ObiNativeFloatList stiffnesses, ObiNativeFloatList lambdas, int count)
        {
            this.particleIndices = particleIndices.AsNativeArray<int>();
            this.colliderIndices = colliderIndices.AsNativeArray<int>();
            this.offsets = offsets.AsNativeArray<float4>();
            this.restDarbouxVectors = restDarbouxVectors.AsNativeArray<quaternion>();
            this.stiffnesses = stiffnesses.AsNativeArray<float2>();
            this.lambdas = lambdas.AsNativeArray<float>();
            m_ConstraintCount = count;
        }

        public override JobHandle Initialize(JobHandle inputDeps, float stepTime, float substepTime, int steps, float timeLeft)
        {
            var clearPins = new ClearPinsJob
            {
                colliderIndices = colliderIndices,
                shapes = ObiColliderWorld.GetInstance().colliderShapes.AsNativeArray<BurstColliderShape>(),
                rigidbodies = ObiColliderWorld.GetInstance().rigidbodies.AsNativeArray<BurstRigidbody>(),
            };
            inputDeps = clearPins.Schedule(m_ConstraintCount, 128, inputDeps);

            var updatePins = new UpdatePinsJob
            {
                colliderIndices = colliderIndices,
                shapes = ObiColliderWorld.GetInstance().colliderShapes.AsNativeArray<BurstColliderShape>(),
                rigidbodies = ObiColliderWorld.GetInstance().rigidbodies.AsNativeArray<BurstRigidbody>(),
            };
            inputDeps = updatePins.Schedule(m_ConstraintCount, 128, inputDeps);

            // clear lambdas:
            return base.Initialize(inputDeps, stepTime, substepTime, steps, timeLeft);
        }

        public override JobHandle Evaluate(JobHandle inputDeps, float stepTime, float substepTime, int steps, float timeLeft)
        {
            var projectConstraints = new PinConstraintsBatchJob()
            {
                particleIndices = particleIndices,
                colliderIndices = colliderIndices,
                offsets = offsets,
                stiffnesses = stiffnesses,
                restDarboux = restDarbouxVectors,
                lambdas = lambdas.Reinterpret<float, float4>(),

                positions = solverImplementation.positions,
                prevPositions = solverImplementation.prevPositions,
                invMasses = solverImplementation.invMasses,
                orientations = solverImplementation.orientations,
                invRotationalMasses = solverImplementation.invRotationalMasses,

                shapes = ObiColliderWorld.GetInstance().colliderShapes.AsNativeArray<BurstColliderShape>(),
                transforms = ObiColliderWorld.GetInstance().colliderTransforms.AsNativeArray<BurstAffineTransform>(),
                rigidbodies = ObiColliderWorld.GetInstance().rigidbodies.AsNativeArray<BurstRigidbody>(),
                rigidbodyLinearDeltas = solverImplementation.abstraction.rigidbodyLinearDeltas.AsNativeArray<float4>(),
                rigidbodyAngularDeltas = solverImplementation.abstraction.rigidbodyAngularDeltas.AsNativeArray<float4>(),

                deltas = solverImplementation.positionDeltas,
                counts = solverImplementation.positionConstraintCounts,
                orientationDeltas = solverImplementation.orientationDeltas,
                orientationCounts = solverImplementation.orientationConstraintCounts,

                inertialFrame = ((BurstSolverImpl)constraints.solver).inertialFrame,
                stepTime = stepTime,
                steps = steps,
                substepTime = substepTime,
                timeLeft = timeLeft,
                activeConstraintCount = m_ConstraintCount
            };

            return projectConstraints.Schedule(m_ConstraintCount, 16, inputDeps);
        }

        public override JobHandle Apply(JobHandle inputDeps, float substepTime)
        {
            var parameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

            var applyConstraints = new ApplyPinConstraintsBatchJob()
            {
                particleIndices = particleIndices,

                positions = solverImplementation.positions,
                deltas = solverImplementation.positionDeltas,
                counts = solverImplementation.positionConstraintCounts,

                orientations = solverImplementation.orientations,
                orientationDeltas = solverImplementation.orientationDeltas,
                orientationCounts = solverImplementation.orientationConstraintCounts,

                sorFactor = parameters.SORFactor,
                activeConstraintCount = m_ConstraintCount,
            };

            return applyConstraints.Schedule(inputDeps);
        }

        [BurstCompile]
        public unsafe struct ClearPinsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> colliderIndices;
            [ReadOnly] public NativeArray<BurstColliderShape> shapes;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<BurstRigidbody> rigidbodies;

            public void Execute(int i)
            {
                int colliderIndex = colliderIndices[i];

                // no collider to pin to, so ignore the constraint.
                if (colliderIndex < 0)
                    return;

                int rigidbodyIndex = shapes[colliderIndex].rigidbodyIndex;
                if (rigidbodyIndex >= 0)
                {
                    BurstRigidbody* arr = (BurstRigidbody*)rigidbodies.GetUnsafePtr();
                    Interlocked.Exchange(ref arr[rigidbodyIndex].constraintCount, 0);
                }
            }
        }

        [BurstCompile]
        public unsafe struct UpdatePinsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> colliderIndices;
            [ReadOnly] public NativeArray<BurstColliderShape> shapes;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<BurstRigidbody> rigidbodies;

            public void Execute(int i)
            {
                int colliderIndex = colliderIndices[i];

                // no collider to pin to, so ignore the constraint.
                if (colliderIndex < 0)
                    return;

                // Increment the amount of constraints affecting this rigidbody for mass splitting:
                int rigidbodyIndex = shapes[colliderIndex].rigidbodyIndex;
                if (rigidbodyIndex >= 0)
                {
                    BurstRigidbody* arr = (BurstRigidbody*)rigidbodies.GetUnsafePtr();
                    Interlocked.Increment(ref arr[rigidbodyIndex].constraintCount);
                }
            }
        }

        [BurstCompile]
        public unsafe struct PinConstraintsBatchJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> particleIndices;
            [ReadOnly] public NativeArray<int> colliderIndices;

            [ReadOnly] public NativeArray<float4> offsets;
            [ReadOnly] public NativeArray<float2> stiffnesses;
            [ReadOnly] public NativeArray<quaternion> restDarboux;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> lambdas;

            [ReadOnly] public NativeArray<float4> positions;
            [ReadOnly] public NativeArray<float4> prevPositions;
            [ReadOnly] public NativeArray<float> invMasses;
            [ReadOnly] public NativeArray<quaternion> orientations;
            [ReadOnly] public NativeArray<float> invRotationalMasses;

            [ReadOnly] public NativeArray<BurstColliderShape> shapes;
            [ReadOnly] public NativeArray<BurstAffineTransform> transforms;
            [ReadOnly] public NativeArray<BurstRigidbody> rigidbodies;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> rigidbodyLinearDeltas;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> rigidbodyAngularDeltas;

            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> deltas;
            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<int> counts;
            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<quaternion> orientationDeltas;
            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<int> orientationCounts;

            [ReadOnly] public BurstInertialFrame inertialFrame;
            [ReadOnly] public float stepTime;
            [ReadOnly] public float substepTime;
            [ReadOnly] public float timeLeft;
            [ReadOnly] public int steps;
            [ReadOnly] public int activeConstraintCount;

            public void Execute(int i)
            {
                int particleIndex = particleIndices[i];
                int colliderIndex = colliderIndices[i];

                // no collider to pin to, so ignore the constraint.
                if (colliderIndex < 0)
                    return;

                int rigidbodyIndex = shapes[colliderIndex].rigidbodyIndex;

                float frameEnd = stepTime * steps;
                float substepsToEnd = timeLeft / substepTime;

                // calculate time adjusted compliances
                float2 compliances = stiffnesses[i].xy / (substepTime * substepTime);

                // project particle position to the end of the full step:
                float4 particlePosition = math.lerp(prevPositions[particleIndex], positions[particleIndex], substepsToEnd);

                // express pin offset in world space:
                float4 worldPinOffset = transforms[colliderIndex].TransformPoint(offsets[i]);
                float4 predictedPinOffset = worldPinOffset;
                quaternion predictedRotation = transforms[colliderIndex].rotation;

                float rigidbodyLinearW = 0;
                float rigidbodyAngularW = 0;

                if (rigidbodyIndex >= 0)
                {
                    var rigidbody = rigidbodies[rigidbodyIndex];

                    // predict offset point position using rb velocity at that point (can't integrate transform since position != center of mass)
                    float4 velocityAtPoint = BurstMath.GetRigidbodyVelocityAtPoint(rigidbodyIndex, inertialFrame.frame.InverseTransformPoint(worldPinOffset), rigidbodies, rigidbodyLinearDeltas, rigidbodyAngularDeltas, inertialFrame);
                    predictedPinOffset = BurstIntegration.IntegrateLinear(predictedPinOffset, inertialFrame.frame.TransformVector(velocityAtPoint), frameEnd);

                    // predict rotation at the end of the step:
                    predictedRotation = BurstIntegration.IntegrateAngular(predictedRotation, rigidbody.angularVelocity + rigidbodyAngularDeltas[rigidbodyIndex], stepTime);

                    // calculate linear and angular rigidbody effective masses (mass splitting: multiply by constraint count)
                    rigidbodyLinearW = rigidbody.inverseMass * rigidbody.constraintCount;
                    rigidbodyAngularW = BurstMath.RotationalInvMass(rigidbody.inverseInertiaTensor,
                                                                    worldPinOffset - rigidbody.com,
                                                                    math.normalizesafe(inertialFrame.frame.TransformPoint(particlePosition) - predictedPinOffset)) * rigidbody.constraintCount;

                }

                // Transform pin position to solver space for constraint solving:
                predictedPinOffset = inertialFrame.frame.InverseTransformPoint(predictedPinOffset);
                predictedRotation = math.mul(math.conjugate(inertialFrame.frame.rotation), predictedRotation);

                float4 gradient = particlePosition - predictedPinOffset;
                float constraint = math.length(gradient);
                float4 gradientDir = gradient / (constraint + BurstMath.epsilon);

                float4 lambda = lambdas[i];
                float linearDLambda = (-constraint - compliances.x * lambda.w) / (invMasses[particleIndex] + rigidbodyLinearW + rigidbodyAngularW + compliances.x + BurstMath.epsilon);
                lambda.w += linearDLambda;
                float4 correction = linearDLambda * gradientDir;

                deltas[particleIndex] += correction * invMasses[particleIndex] / substepsToEnd;
                counts[particleIndex]++;

                if (rigidbodyIndex >= 0)
                {
                    BurstMath.ApplyImpulse(rigidbodyIndex,
                                            -correction / frameEnd,
                                            inertialFrame.frame.InverseTransformPoint(worldPinOffset),
                                            rigidbodies, rigidbodyLinearDeltas, rigidbodyAngularDeltas, inertialFrame.frame);
                }
                     
                if (rigidbodyAngularW > 0 || invRotationalMasses[particleIndex] > 0)
                {
                    // bend/twist constraint:
                    quaternion omega = math.mul(math.conjugate(orientations[particleIndex]), predictedRotation);   //darboux vector

                    quaternion omega_plus;
                    omega_plus.value = omega.value + restDarboux[i].value;  //delta Omega with - omega_0
                    omega.value -= restDarboux[i].value;                    //delta Omega with + omega_0
                    if (math.lengthsq(omega.value) > math.lengthsq(omega_plus.value))
                        omega = omega_plus;

                    float3 dlambda = (omega.value.xyz - compliances.y * lambda.xyz) / (compliances.y + invRotationalMasses[particleIndex] + rigidbodyAngularW + BurstMath.epsilon);
                    lambda.xyz += dlambda;

                    //discrete Darboux vector does not have vanishing scalar part
                    quaternion dlambdaQ = new quaternion(dlambda[0], dlambda[1], dlambda[2], 0);

                    quaternion orientDelta = orientationDeltas[particleIndex];
                    orientDelta.value += math.mul(predictedRotation, dlambdaQ).value * invRotationalMasses[particleIndex] / substepsToEnd;
                    orientationDeltas[particleIndex] = orientDelta;
                    orientationCounts[particleIndex]++;

                    if (rigidbodyIndex >= 0)
                    {
                        BurstMath.ApplyDeltaQuaternion(rigidbodyIndex,
                                                        predictedRotation,
                                                        -math.mul(orientations[particleIndex], dlambdaQ).value * rigidbodyAngularW,
                                                        rigidbodyAngularDeltas, inertialFrame.frame, frameEnd);
                    }
                }

                lambdas[i] = lambda;
            }
        }

        [BurstCompile]
        public struct ApplyPinConstraintsBatchJob : IJob
        {
            [ReadOnly] public NativeArray<int> particleIndices;
            [ReadOnly] public float sorFactor;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> positions;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> deltas;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<int> counts;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<quaternion> orientations;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<quaternion> orientationDeltas;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<int> orientationCounts;

            [ReadOnly] public int activeConstraintCount;

            public void Execute()
            {
                for (int i = 0; i < activeConstraintCount; ++i)
                {
                    int p1 = particleIndices[i];

                    if (counts[p1] > 0)
                    {
                        positions[p1] += deltas[p1] * sorFactor / counts[p1];
                        deltas[p1] = float4.zero;
                        counts[p1] = 0;
                    }

                    if (orientationCounts[p1] > 0)
                    {
                        quaternion q = orientations[p1];
                        q.value += orientationDeltas[p1].value * sorFactor / orientationCounts[p1];
                        orientations[p1] = math.normalize(q);

                        orientationDeltas[p1] = new quaternion(0, 0, 0, 0);
                        orientationCounts[p1] = 0;
                    }
                }
            }
        }
    }
}
#endif