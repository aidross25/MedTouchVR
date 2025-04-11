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
    public class BurstPinholeConstraintsBatch : BurstConstraintsBatchImpl, IPinholeConstraintsBatchImpl
    {
        private NativeArray<int> colliderIndices;
        private NativeArray<float4> offsets;
        private NativeArray<float> edgeMus;
        private NativeArray<int2> edgeRanges;
        private NativeArray<float2> edgeRangeMus;
        private NativeArray<float> parameters;
        private NativeArray<float> relativeVelocities;

        public BurstPinholeConstraintsBatch(BurstPinholeConstraints constraints)
        {
            m_Constraints = constraints;
            m_ConstraintType = Oni.ConstraintType.Pinhole;
        }

        public void SetPinholeConstraints(ObiNativeIntList particleIndices, ObiNativeIntList colliderIndices, ObiNativeVector4List offsets, ObiNativeFloatList edgeMus, ObiNativeIntList edgeRanges, ObiNativeFloatList edgeRangeMus, ObiNativeFloatList parameters, ObiNativeFloatList relativeVelocities, ObiNativeFloatList lambdas, int count)
        {
            this.particleIndices = particleIndices.AsNativeArray<int>();
            this.colliderIndices = colliderIndices.AsNativeArray<int>();
            this.offsets = offsets.AsNativeArray<float4>();
            this.edgeMus = edgeMus.AsNativeArray<float>();
            this.edgeRanges = edgeRanges.AsNativeArray<int2>();
            this.edgeRangeMus = edgeRangeMus.AsNativeArray<float2>();
            this.parameters = parameters.AsNativeArray<float>();
            this.relativeVelocities = relativeVelocities.AsNativeArray<float>();
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
                particleIndices = particleIndices,
                colliderIndices = colliderIndices,
                offsets = offsets,
                edgeMus = edgeMus,
                edgeRangeMus = edgeRangeMus,
                relativeVelocities = relativeVelocities,
                parameters = parameters,
                edgeRanges = edgeRanges,

                positions = solverImplementation.positions,
                prevPositions = solverImplementation.prevPositions,
                invMasses = solverImplementation.invMasses,

                deformableEdges = solverImplementation.abstraction.deformableEdges.AsNativeArray<int>(),

                shapes = ObiColliderWorld.GetInstance().colliderShapes.AsNativeArray<BurstColliderShape>(),
                transforms = ObiColliderWorld.GetInstance().colliderTransforms.AsNativeArray<BurstAffineTransform>(),
                rigidbodies = ObiColliderWorld.GetInstance().rigidbodies.AsNativeArray<BurstRigidbody>(),
                rigidbodyLinearDeltas = solverImplementation.abstraction.rigidbodyLinearDeltas.AsNativeArray<float4>(),
                rigidbodyAngularDeltas = solverImplementation.abstraction.rigidbodyAngularDeltas.AsNativeArray<float4>(),

                inertialFrame = ((BurstSolverImpl)constraints.solver).inertialFrame,
                stepTime = stepTime,
                steps = steps,
                substepTime = substepTime,
                timeLeft = timeLeft,
                activeConstraintCount = m_ConstraintCount
            };
            inputDeps = updatePins.Schedule(m_ConstraintCount, 128, inputDeps);

            // clear lambdas:
            return base.Initialize(inputDeps, stepTime, substepTime, steps, timeLeft);
        }

        public override JobHandle Evaluate(JobHandle inputDeps, float stepTime, float substepTime, int steps, float timeLeft)
        {
            var projectConstraints = new PinholeConstraintsBatchJob()
            {
                particleIndices = particleIndices,
                colliderIndices = colliderIndices,
                offsets = offsets,
                edgeMus = edgeMus,
                parameters = parameters,
                lambdas = lambdas,

                positions = solverImplementation.positions,
                prevPositions = solverImplementation.prevPositions,
                invMasses = solverImplementation.invMasses,

                deformableEdges = solverImplementation.abstraction.deformableEdges.AsNativeArray<int>(),

                shapes = ObiColliderWorld.GetInstance().colliderShapes.AsNativeArray<BurstColliderShape>(),
                transforms = ObiColliderWorld.GetInstance().colliderTransforms.AsNativeArray<BurstAffineTransform>(),
                rigidbodies = ObiColliderWorld.GetInstance().rigidbodies.AsNativeArray<BurstRigidbody>(),
                rigidbodyLinearDeltas = solverImplementation.abstraction.rigidbodyLinearDeltas.AsNativeArray<float4>(),
                rigidbodyAngularDeltas = solverImplementation.abstraction.rigidbodyAngularDeltas.AsNativeArray<float4>(),

                deltas = solverImplementation.positionDeltas,
                counts = solverImplementation.positionConstraintCounts,

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
            var cparameters = solverAbstraction.GetConstraintParameters(m_ConstraintType);

            var applyConstraints = new ApplyPinholeConstraintsBatchJob()
            {
                particleIndices = particleIndices,
                deformableEdges = solverImplementation.abstraction.deformableEdges.AsNativeArray<int>(),

                positions = solverImplementation.positions,
                deltas = solverImplementation.positionDeltas,
                counts = solverImplementation.positionConstraintCounts,

                sorFactor = cparameters.SORFactor,
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
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<int> particleIndices;

            [ReadOnly] public NativeArray<int2> edgeRanges;
            [ReadOnly] public NativeArray<float2> edgeRangeMus;
            [ReadOnly] public NativeArray<float4> offsets;
            [ReadOnly] public NativeArray<float> parameters; // compliance, friction, motor speed, motor force, clamp behavior.
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float> edgeMus;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float> relativeVelocities;

            [ReadOnly] public NativeArray<float4> positions;
            [ReadOnly] public NativeArray<float4> prevPositions;
            [ReadOnly] public NativeArray<float> invMasses;

            [ReadOnly] public NativeArray<int> deformableEdges;

            [ReadOnly] public NativeArray<int> colliderIndices;
            [ReadOnly] public NativeArray<BurstColliderShape> shapes;
            [ReadOnly] public NativeArray<BurstAffineTransform> transforms;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<BurstRigidbody> rigidbodies;

            [ReadOnly] public NativeArray<float4> rigidbodyLinearDeltas;
            [ReadOnly] public NativeArray<float4> rigidbodyAngularDeltas;

            [ReadOnly] public BurstInertialFrame inertialFrame;
            [ReadOnly] public float stepTime;
            [ReadOnly] public float substepTime;
            [ReadOnly] public float timeLeft;
            [ReadOnly] public int steps;
            [ReadOnly] public int activeConstraintCount;

            private bool IsEdgeValid(int edgeIndex, int nextEdgeIndex, float mix)
            {
                return mix < 0 ? deformableEdges[nextEdgeIndex * 2 + 1] == deformableEdges[edgeIndex * 2] :
                                 deformableEdges[nextEdgeIndex * 2] == deformableEdges[edgeIndex * 2 + 1];
            }

            private bool ClampToRange(int i, int edgeIndex, ref float mix)
            {
                bool clamped = false;
                if (edgeIndex == edgeRanges[i].x && mix < edgeRangeMus[i].x)
                {
                    mix = edgeRangeMus[i].x;
                    clamped = true;
                }
                if (edgeIndex == edgeRanges[i].y && mix > edgeRangeMus[i].y)
                {
                    mix = edgeRangeMus[i].y;
                    clamped = true;
                }
                return clamped;
            }

            public void Execute(int i)
            {
                int edgeIndex = particleIndices[i];
                int colliderIndex = colliderIndices[i];

                // if no collider or edge, ignore the constraint.
                if (colliderIndex < 0 || edgeIndex < 0)
                    return;

                // Increment the amount of constraints affecting this rigidbody for mass splitting:
                int rigidbodyIndex = shapes[colliderIndex].rigidbodyIndex;
                if (rigidbodyIndex >= 0)
                {
                    BurstRigidbody* arr = (BurstRigidbody*)rigidbodies.GetUnsafePtr();
                    Interlocked.Increment(ref arr[rigidbodyIndex].constraintCount);
                }

                float frameEnd = stepTime * steps;
                float substepsToEnd = timeLeft / substepTime;

                int p1 = deformableEdges[edgeIndex * 2];
                int p2 = deformableEdges[edgeIndex * 2 + 1];
                int edgeCount = math.max(0, edgeRanges[i].y - edgeRanges[i].x + 1);

                // express pin offset in world space:
                float4 worldPinOffset = transforms[colliderIndex].TransformPoint(offsets[i]);
                float4 predictedPinOffset = worldPinOffset;

                if (rigidbodyIndex >= 0)
                {
                    // predict offset point position using rb velocity at that point (can't integrate transform since position != center of mass)
                    float4 pointVelocity = BurstMath.GetRigidbodyVelocityAtPoint(rigidbodyIndex, inertialFrame.frame.InverseTransformPoint(worldPinOffset), rigidbodies, rigidbodyLinearDeltas, rigidbodyAngularDeltas, inertialFrame);
                    predictedPinOffset = BurstIntegration.IntegrateLinear(predictedPinOffset, inertialFrame.frame.TransformVector(pointVelocity), frameEnd);
                }

                // transform pinhole position to solver space for constraint solving:
                float4 solverPredictedOffset = inertialFrame.frame.InverseTransformPoint(predictedPinOffset);

                // get current edge data:
                float4 particlePosition1 = math.lerp(prevPositions[p1], positions[p1], substepsToEnd);
                float4 particlePosition2 = math.lerp(prevPositions[p2], positions[p2], substepsToEnd);
                float edgeLength = math.length(particlePosition1 - particlePosition2) + BurstMath.epsilon;
                BurstMath.NearestPointOnEdge(particlePosition1, particlePosition2, solverPredictedOffset, out float mix, false);

                // calculate current relative velocity between rope and pinhole:
                float velocity = (mix - edgeMus[i]) / substepTime * edgeLength; // vel = pos / time.
                relativeVelocities[i] = velocity;

                //apply motor force:
                float targetAccel = (parameters[i * 5 + 2] - velocity) / substepTime; // accel = vel / time.
                float maxAccel = parameters[i * 5 + 3] * math.max(math.lerp(invMasses[p1], invMasses[p2], mix), BurstMath.epsilon); // accel = force / mass. Guard against inf*0
                velocity += math.clamp(targetAccel, -maxAccel, maxAccel) * substepTime;

                // calculate new position by adding motor acceleration:
                float corrMix = edgeMus[i] + velocity * substepTime / edgeLength;

                // apply artificial friction by interpolating predicted position and corrected position.
                mix = math.lerp(mix, corrMix, parameters[i * 5 + 1]);

                // move to an adjacent edge if needed:
                if (!ClampToRange(i, edgeIndex, ref mix) && (mix < 0 || mix > 1))
                {
                    bool clampOnEnd = parameters[i * 5 + 4] > 0.5f;

                    // calculate distance we need to travel along edge chain:
                    float distToTravel = math.length(particlePosition1 - particlePosition2) * (mix < 0 ? -mix : mix - 1);

                    int nextEdgeIndex;
                    for (int k = 0; k < 10; ++k) // look up to 10 edges away.
                    {
                        // calculate index of next edge:
                        nextEdgeIndex = mix < 0 ? edgeIndex - 1 : edgeIndex + 1;
                        nextEdgeIndex = edgeRanges[i].x + (int)BurstMath.nfmod(nextEdgeIndex - edgeRanges[i].x, edgeCount);

                        // see if it's valid
                        if (!IsEdgeValid(edgeIndex, nextEdgeIndex, mix))
                        {
                            // disable constraint if needed
                            if (!clampOnEnd) { particleIndices[i] = -1; return; }

                            // otherwise clamp:
                            mix = math.saturate(mix);
                            break;
                        }

                        // advance to next edge:
                        edgeIndex = nextEdgeIndex;
                        p1 = deformableEdges[edgeIndex * 2];
                        p2 = deformableEdges[edgeIndex * 2 + 1];
                        particlePosition1 = math.lerp(prevPositions[p1], positions[p1], substepsToEnd);
                        particlePosition2 = math.lerp(prevPositions[p2], positions[p2], substepsToEnd);
                        edgeLength = math.length(particlePosition1 - particlePosition2) + BurstMath.epsilon;

                        // stop if we reached target edge:
                        if (distToTravel <= edgeLength)
                        {
                            mix = mix < 0 ? 1 - math.saturate(distToTravel / edgeLength) : math.saturate(distToTravel / edgeLength);
                            ClampToRange(i, edgeIndex, ref mix);
                            break;
                        }

                        // stop if we reached end of range:
                        if (ClampToRange(i, edgeIndex, ref mix))
                            break;

                        distToTravel -= edgeLength;
                    }
                }

                edgeMus[i] = mix;
                particleIndices[i] = edgeIndex;
            }
        }

        [BurstCompile]
        public unsafe struct PinholeConstraintsBatchJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> particleIndices;
            [ReadOnly] public NativeArray<int> colliderIndices;

            [ReadOnly] public NativeArray<float4> offsets;
            [ReadOnly] public NativeArray<float> parameters; // compliance, friction, motor speed, motor force, clamp behavior.
            [ReadOnly] public NativeArray<float> edgeMus;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float> lambdas;

            [ReadOnly] public NativeArray<float4> positions;
            [ReadOnly] public NativeArray<float4> prevPositions;
            [ReadOnly] public NativeArray<float> invMasses;

            [ReadOnly] public NativeArray<int> deformableEdges;

            [ReadOnly] public NativeArray<BurstColliderShape> shapes;
            [ReadOnly] public NativeArray<BurstAffineTransform> transforms;
            [ReadOnly] public NativeArray<BurstRigidbody> rigidbodies;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> rigidbodyLinearDeltas;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> rigidbodyAngularDeltas;

            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<float4> deltas;
            [NativeDisableContainerSafetyRestriction][NativeDisableParallelForRestriction] public NativeArray<int> counts;

            [ReadOnly] public BurstInertialFrame inertialFrame;
            [ReadOnly] public float stepTime;
            [ReadOnly] public float substepTime;
            [ReadOnly] public float timeLeft;
            [ReadOnly] public int steps;
            [ReadOnly] public int activeConstraintCount;

            public void Execute(int i)
            {
                int edgeIndex = particleIndices[i];
                int colliderIndex = colliderIndices[i];

                // if no collider or edge, ignore the constraint.
                if (edgeIndex < 0 || colliderIndex < 0)
                    return;

                float frameEnd = stepTime * steps;
                float substepsToEnd = timeLeft / substepTime;

                // calculate time adjusted compliance
                float compliance = parameters[i * 5] / (substepTime * substepTime);

                int p1 = deformableEdges[edgeIndex * 2];
                int p2 = deformableEdges[edgeIndex * 2 + 1];

                // calculate projection on current edge:
                float mix = edgeMus[i];
                float4 particlePosition1 = math.lerp(prevPositions[p1], positions[p1], substepsToEnd);
                float4 particlePosition2 = math.lerp(prevPositions[p2], positions[p2], substepsToEnd);
                float4 projection = math.lerp(particlePosition1, particlePosition2, mix);

                // express pin offset in world space:
                float4 worldPinOffset = transforms[colliderIndex].TransformPoint(offsets[i]);
                float4 predictedPinOffset = worldPinOffset;

                float rigidbodyLinearW = 0;
                float rigidbodyAngularW = 0;

                int rigidbodyIndex = shapes[colliderIndex].rigidbodyIndex;
                if (rigidbodyIndex >= 0)
                {
                    var rigidbody = rigidbodies[rigidbodyIndex];

                    // predict offset point position using rb velocity at that point (can't integrate transform since position != center of mass)
                    float4 pointVelocity = BurstMath.GetRigidbodyVelocityAtPoint(rigidbodyIndex, inertialFrame.frame.InverseTransformPoint(worldPinOffset), rigidbodies, rigidbodyLinearDeltas, rigidbodyAngularDeltas, inertialFrame);
                    predictedPinOffset = BurstIntegration.IntegrateLinear(predictedPinOffset, inertialFrame.frame.TransformVector(pointVelocity), frameEnd);

                    // calculate linear and angular rigidbody effective masses (mass splitting: multiply by constraint count)
                    rigidbodyLinearW = rigidbody.inverseMass * rigidbody.constraintCount;
                    rigidbodyAngularW = BurstMath.RotationalInvMass(rigidbody.inverseInertiaTensor,
                                                                    worldPinOffset - rigidbody.com,
                                                                    math.normalizesafe(inertialFrame.frame.TransformPoint(projection) - predictedPinOffset)) * rigidbody.constraintCount;
                }

                // Transform pinhole position to solver space for constraint solving:
                predictedPinOffset = inertialFrame.frame.InverseTransformPoint(predictedPinOffset);

                float4 gradient = projection - predictedPinOffset;
                float constraint = math.length(gradient);
                float4 gradientDir = gradient / (constraint + BurstMath.epsilon);

                float lambda = (-constraint - compliance * lambdas[i]) / (math.lerp(invMasses[p1], invMasses[p2], mix) + rigidbodyLinearW + rigidbodyAngularW + compliance + BurstMath.epsilon);
                lambdas[i] += lambda;
                float4 correction = lambda * gradientDir;

                float baryScale =  BurstMath.BaryScale(new float4(1 - mix, mix, 0, 0));

                deltas[p1] += correction * baryScale * invMasses[p1] * (1 - mix) / substepsToEnd;
                counts[p1]++;

                deltas[p2] += correction * baryScale * invMasses[p2] * mix / substepsToEnd;
                counts[p2]++;

                if (rigidbodyIndex >= 0)
                {
                    BurstMath.ApplyImpulse(rigidbodyIndex,
                                            -correction / frameEnd,
                                           inertialFrame.frame.InverseTransformPoint(worldPinOffset),
                                           rigidbodies, rigidbodyLinearDeltas, rigidbodyAngularDeltas, inertialFrame.frame);
                }
            }
        }

        [BurstCompile]
        public struct ApplyPinholeConstraintsBatchJob : IJob
        {
            [ReadOnly] public NativeArray<int> particleIndices;
            [ReadOnly] public float sorFactor;

            [ReadOnly] public NativeArray<int> deformableEdges;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> positions;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<float4> deltas;
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] public NativeArray<int> counts;

            [ReadOnly] public int activeConstraintCount;

            public void Execute()
            {
                for (int i = 0; i < activeConstraintCount; ++i)
                {
                    int edgeIndex = particleIndices[i];
                    if (edgeIndex < 0) continue;

                    int p1 = deformableEdges[edgeIndex * 2];
                    int p2 = deformableEdges[edgeIndex * 2 + 1];

                    if (counts[p1] > 0)
                    {
                        positions[p1] += deltas[p1] * sorFactor / counts[p1];
                        deltas[p1] = float4.zero;
                        counts[p1] = 0;
                    }

                    if (counts[p2] > 0)
                    {
                        positions[p2] += deltas[p2] * sorFactor / counts[p2];
                        deltas[p2] = float4.zero;
                        counts[p2] = 0;
                    }
                }
            }
        }
    }
}
#endif