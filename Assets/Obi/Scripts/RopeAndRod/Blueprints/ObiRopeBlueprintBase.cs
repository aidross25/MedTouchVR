using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace Obi
{
    public abstract class ObiRopeBlueprintBase : ObiActorBlueprint
    {
        [HideInInspector] [SerializeField] public ObiPath path = new ObiPath();
        public float thickness = 0.1f;

        [Range(0, 1)]
        public float resolution = 1;

        [HideInInspector] [SerializeField] protected float m_InterParticleDistance;
        [HideInInspector] [SerializeField] protected int totalParticles;
        [HideInInspector] [SerializeField] protected float m_RestLength;

        [HideInInspector] public int[] deformableEdges = null;    /**< Indices of deformable edges (2 per edge)*/
        [HideInInspector] public float[] restLengths;

        public float interParticleDistance
        {
            get { return m_InterParticleDistance; }
        }

        public float restLength
        {
            get { return m_RestLength; }
        }

        public void OnEnable()
        {
            path.OnPathChanged.AddListener(GenerateImmediate);
            path.OnControlPointAdded.AddListener(ControlPointAdded);
            path.OnControlPointRemoved.AddListener(ControlPointRemoved);
            path.OnControlPointRenamed.AddListener(ControlPointRenamed);
        }

        public void OnDisable()
        {
            path.OnPathChanged.RemoveAllListeners();
            path.OnControlPointAdded.RemoveAllListeners();
            path.OnControlPointRemoved.RemoveAllListeners();
            path.OnControlPointRenamed.RemoveAllListeners();
        }

        protected void ControlPointAdded(int index)
        {
            var group = InsertNewParticleGroup(path.GetName(index), index);
        }

        protected void ControlPointRenamed(int index)
        {
            SetParticleGroupName(index, path.GetName(index));
        }

        protected void ControlPointRemoved(int index)
        {
            RemoveParticleGroupAt(index);
        }

        protected virtual IEnumerator CreateAerodynamicConstraints()
        {
            aerodynamicConstraintsData = new ObiAerodynamicConstraintsData();
            var aeroBatch = new ObiAerodynamicConstraintsBatch();
            aerodynamicConstraintsData.AddBatch(aeroBatch);

            for (int i = 0; i < totalParticles; i++)
            {
                aeroBatch.AddConstraint(i, 2 * principalRadii[i].x, 1, 1);

                if (i % 500 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiRope generating aerodynamic constraints...", i / (float)totalParticles);
            }

            // Set initial amount of active constraints:
            for (int i = 0; i < aerodynamicConstraintsData.batches.Count; ++i)
            {
                aerodynamicConstraintsData.batches[i].activeConstraintCount = m_ActiveParticleCount;
            }
        }

        protected void CreateDeformableEdges(int numSegments)
        {
            deformableEdges = new int[numSegments * 2];
            for (int i = 0; i < numSegments; ++i)
            {
                deformableEdges[i * 2] = i % activeParticleCount;
                deformableEdges[i * 2 + 1] = (i + 1) % activeParticleCount;
            }
        }

        protected void CreateSimplices(int numSegments)
        {
            edges = new int[numSegments * 2];
            for (int i = 0; i < numSegments; ++i)
            {
                edges[i * 2] = i % activeParticleCount;
                edges[i * 2 + 1] = (i + 1) % activeParticleCount;
            }
        }

        protected override IEnumerator Initialize() { yield return null; }
    }
}