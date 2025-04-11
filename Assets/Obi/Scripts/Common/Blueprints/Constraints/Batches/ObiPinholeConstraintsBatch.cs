using UnityEngine;
using System;
using System.Collections.Generic;

namespace Obi
{
    [Serializable]
    public class ObiPinholeConstraintsBatch : ObiConstraintsBatch
    {
        [Serializable]
        public struct PinholeEdge
        {
            public int edgeIndex;
            public float coordinate;

            public PinholeEdge(int edgeIndex, float coordinate)
            {
                this.edgeIndex = edgeIndex;
                this.coordinate = coordinate;
            }

            public float GetRopeCoordinate(ObiActor actor) 
            {
                int edgeCount = actor.GetDeformableEdgeCount();
                return edgeCount > 0 ? Mathf.Clamp01((edgeIndex + coordinate) / edgeCount) : 0;
            }
        }

        protected IPinholeConstraintsBatchImpl m_BatchImpl;

        /// <summary>
        /// for each constraint, handle of the pinned collider.
        /// </summary>
        [HideInInspector] public List<ObiColliderHandle> pinBodies = new List<ObiColliderHandle>();

        /// <summary>
        /// for each constraint, reference to the actor pinned.
        /// </summary>
        [HideInInspector] public List<ObiActor> pinActors = new List<ObiActor>();

        /// <summary>
        /// index of the pinned collider in the collider world.
        /// </summary>
        [HideInInspector] public ObiNativeIntList colliderIndices = new ObiNativeIntList();

        /// <summary>
        /// Pinhole position expressed in the attachment's local space.
        /// </summary>
        [HideInInspector] public ObiNativeVector4List offsets = new ObiNativeVector4List();

        /// <summary>
        // Normalized coordinate along current edge.
        /// </summary>
        [HideInInspector] public ObiNativeFloatList edgeMus = new ObiNativeFloatList();

        /// <summary>
        /// Edge range as 2 ints (first edge, last edge) for each constraint.
        /// </summary>
        [HideInInspector] public ObiNativeIntList edgeRanges = new ObiNativeIntList();

        /// <summary>
        /// Min/max cooridnate in each of the first and last edges as 2 floats for each constraint.
        /// </summary>
        [HideInInspector] public ObiNativeFloatList edgeRangeMus = new ObiNativeFloatList();

        /// <summary>
        /// Parameters of pinhole constraints. 5 floats per constraint (compliance, friction, motor speed, motor force and normalized coordinate along edge).
        /// </summary>
        [HideInInspector] public ObiNativeFloatList parameters = new ObiNativeFloatList();

        /// <summary>
        /// Relative velocities between rope and pinhole.
        /// </summary>
        [HideInInspector] public ObiNativeFloatList relativeVelocities = new ObiNativeFloatList();


        public override Oni.ConstraintType constraintType
        {
            get { return Oni.ConstraintType.Pinhole; }
        }

        public override IConstraintsBatchImpl implementation
        {
            get { return m_BatchImpl; }
        }

        public ObiPinholeConstraintsBatch(ObiPinholeConstraintsData constraints = null) : base()
        {
        }

        public void AddConstraint(PinholeEdge edge, PinholeEdge firstEdge, PinholeEdge lastEdge, ObiActor actor, ObiColliderBase body, Vector3 offset, float compliance, float friction, float motorSpeed, float motorForce, bool clampAtEnds)
        {
            RegisterConstraint();

            particleIndices.Add(edge.edgeIndex);
            edgeMus.Add(edge.coordinate);
            edgeRanges.Add(firstEdge.edgeIndex);
            edgeRanges.Add(lastEdge.edgeIndex);
            edgeRangeMus.Add(firstEdge.coordinate);
            edgeRangeMus.Add(lastEdge.coordinate);

            pinBodies.Add(body != null ? body.Handle : new ObiColliderHandle());
            pinActors.Add(actor);
            colliderIndices.Add(body != null ? body.Handle.index : -1);
            offsets.Add(offset);
            parameters.Add(compliance);
            parameters.Add(friction);
            parameters.Add(motorSpeed);
            parameters.Add(motorForce);
            parameters.Add(clampAtEnds ? 1 : 0);
            relativeVelocities.Add(0);
        }

        public override void Clear()
        {
            base.Clear();
            particleIndices.Clear();
            pinBodies.Clear();
            colliderIndices.Clear();
            offsets.Clear();
            edgeMus.Clear();
            edgeRanges.Clear();
            edgeRangeMus.Clear();
            parameters.Clear();
            relativeVelocities.Clear();
        }

        public override void GetParticlesInvolved(int index, List<int> particles)
        {
            particles.Add(particleIndices[index]);
        }

        protected override void SwapConstraints(int sourceIndex, int destIndex)
        {
            particleIndices.Swap(sourceIndex, destIndex);
            pinBodies.Swap(sourceIndex, destIndex);
            colliderIndices.Swap(sourceIndex, destIndex);
            offsets.Swap(sourceIndex, destIndex);
            edgeMus.Swap(sourceIndex, destIndex);
            edgeRanges.Swap(sourceIndex * 2, destIndex * 2);
            edgeRanges.Swap(sourceIndex * 2 + 1, destIndex * 2 + 1);
            edgeRangeMus.Swap(sourceIndex * 2, destIndex * 2);
            edgeRangeMus.Swap(sourceIndex * 2 + 1, destIndex * 2 + 1);

            for (int i = 0; i < 5; ++i)
                parameters.Swap(sourceIndex * 5 + i, destIndex * 5 + i);

            relativeVelocities.Swap(sourceIndex, destIndex);
        }

        public override void Merge(ObiActor actor, IObiConstraintsBatch other)
        {
            var batch = other as ObiPinholeConstraintsBatch;

            if (batch != null)
            {

                particleIndices.ResizeUninitialized(m_ActiveConstraintCount + batch.activeConstraintCount);

                colliderIndices.ResizeUninitialized(m_ActiveConstraintCount + batch.activeConstraintCount);
                offsets.ResizeUninitialized(m_ActiveConstraintCount + batch.activeConstraintCount);
                edgeRanges.ResizeUninitialized((m_ActiveConstraintCount + batch.activeConstraintCount) * 2);
                edgeRangeMus.ResizeUninitialized((m_ActiveConstraintCount + batch.activeConstraintCount) * 2);
                edgeMus.ResizeUninitialized(m_ActiveConstraintCount + batch.activeConstraintCount);
                parameters.ResizeUninitialized((m_ActiveConstraintCount + batch.activeConstraintCount) * 5);
                relativeVelocities.ResizeInitialized(m_ActiveConstraintCount + batch.activeConstraintCount);
                lambdas.ResizeInitialized(m_ActiveConstraintCount + batch.activeConstraintCount);

                edgeMus.CopyFrom(batch.edgeMus, 0, m_ActiveConstraintCount, batch.activeConstraintCount);
                edgeRangeMus.CopyFrom(batch.edgeRangeMus, 0, m_ActiveConstraintCount * 2, batch.activeConstraintCount * 2);
                offsets.CopyFrom(batch.offsets, 0, m_ActiveConstraintCount, batch.activeConstraintCount);
                relativeVelocities.CopyFrom(batch.relativeVelocities, 0, m_ActiveConstraintCount, batch.activeConstraintCount);
                parameters.CopyFrom(batch.parameters, 0, m_ActiveConstraintCount * 5, batch.activeConstraintCount * 5);

                for (int i = 0; i < batch.activeConstraintCount; ++i)
                {
                    int currentEdge = -1, firstEdge = -1, lastEdge = -1; 
                    if (batch.pinActors[i] != null)
                    {
                        currentEdge = batch.pinActors[i].deformableEdgesOffset + batch.particleIndices[i];
                        firstEdge = batch.pinActors[i].deformableEdgesOffset + batch.edgeRanges[i * 2];
                        lastEdge = batch.pinActors[i].deformableEdgesOffset + batch.edgeRanges[i * 2 + 1];
                    }

                    edgeRanges[(m_ActiveConstraintCount + i) * 2] = firstEdge;
                    edgeRanges[(m_ActiveConstraintCount + i) * 2 + 1] = lastEdge;
                    particleIndices[m_ActiveConstraintCount + i] = Mathf.Clamp(currentEdge, firstEdge, lastEdge);
                    colliderIndices[m_ActiveConstraintCount + i] = batch.pinBodies[i] != null ? batch.pinBodies[i].index : -1;
                }

                base.Merge(actor, other);
            }
        }

        public override void AddToSolver(ObiSolver solver)
        {
            if (solver != null && solver.implementation != null)
            {
                m_BatchImpl = solver.implementation.CreateConstraintsBatch(constraintType) as IPinholeConstraintsBatchImpl;

                if (m_BatchImpl != null)
                    m_BatchImpl.SetPinholeConstraints(particleIndices, colliderIndices, offsets, edgeMus, edgeRanges, edgeRangeMus, parameters, relativeVelocities, lambdas, m_ActiveConstraintCount);
            }
        }

        public override void RemoveFromSolver(ObiSolver solver)
        {
            base.RemoveFromSolver(solver);

            edgeRanges.Dispose();
            colliderIndices.Dispose();
            offsets.Dispose();
            parameters.Dispose();

            if (solver != null && solver.implementation != null)
                solver.implementation.DestroyConstraintsBatch(m_BatchImpl as IConstraintsBatchImpl);
        }

    }
}
