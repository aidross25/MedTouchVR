using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    public abstract class ObiRopeBase : ObiActor, IAerodynamicConstraintsUser
    {

        [SerializeField] protected bool m_SelfCollisions = false;
        [HideInInspector] [SerializeField] protected float restLength_ = 0;
        [HideInInspector] public List<ObiStructuralElement> elements = new List<ObiStructuralElement>();    /**< Elements.*/
        public event ActorCallback OnElementsGenerated;

        // aerodynamics
        [SerializeField] protected bool _aerodynamicsEnabled = true;
        [SerializeField] protected float _drag = 0.05f;
        [SerializeField] protected float _lift = 0.02f;

        /// <summary>  
        ///   Whether this actor's aerodynamic constraints are enabled.
        /// </summary>
        public bool aerodynamicsEnabled
        {
            get { return _aerodynamicsEnabled; }
            set { if (value != _aerodynamicsEnabled) { _aerodynamicsEnabled = value; SetConstraintsDirty(Oni.ConstraintType.Aerodynamics); } }
        }

        /// <summary>  
        /// Aerodynamic drag value.
        /// </summary>
        public float drag
        {
            get { return _drag; }
            set { _drag = value; SetConstraintsDirty(Oni.ConstraintType.Aerodynamics); }
        }

        /// <summary>  
        /// Aerodynamic lift value.
        /// </summary>
        public float lift
        {
            get { return _lift; }
            set { _lift = value; SetConstraintsDirty(Oni.ConstraintType.Aerodynamics); }
        }

        public float restLength
        {
            get { return restLength_; }
        }


        public ObiPath path
        {
            get {
                var ropeBlueprint = (sourceBlueprint as ObiRopeBlueprintBase);
                return ropeBlueprint != null ? ropeBlueprint.path : null; 
            }
        }

        public float GetDrag(ObiAerodynamicConstraintsBatch batch, int constraintIndex)
        {
            return drag;
        }

        public float GetLift(ObiAerodynamicConstraintsBatch batch, int constraintIndex)
        {
            return lift;
        }

        public override void ProvideDeformableEdges(ObiNativeIntList deformableEdges)
        {
            deformableEdgesOffset = deformableEdges.count / 2;

            var ropeBlueprint = sharedBlueprint as ObiRopeBlueprintBase;
            if (ropeBlueprint != null && ropeBlueprint.deformableEdges != null)
            {
                // Send deformable edge indices to the solver:
                for (int i = 0; i < ropeBlueprint.deformableEdges.Length; ++i)
                    deformableEdges.Add(solverIndices[ropeBlueprint.deformableEdges[i]]);
            }
        }

        public override int GetDeformableEdgeCount()
        {
            var ropeBlueprint = sharedBlueprint as ObiRopeBlueprintBase;
            if (ropeBlueprint != null && ropeBlueprint.deformableEdges != null)
                return ropeBlueprint.deformableEdges.Length / 2;
            return 0;
        }

        /// <summary>  
        /// Calculates and returns current rope length, including stretching/compression.
        /// </summary>
        public float CalculateLength()
        {
            float length = 0;

            if (isLoaded)
            {
                // Iterate trough all distance constraints in order:
                int elementCount = elements.Count;
                for (int i = 0; i < elementCount; ++i)
                    length += Vector4.Distance(solver.positions[elements[i].particle1], solver.positions[elements[i].particle2]);
            }
            return length;
        }

        /// <summary>  
        /// Recalculates the rope's rest length, that is, its length as specified by the blueprint.
        /// </summary>
        public void RecalculateRestLength()
        {
            restLength_ = 0;

            // Iterate trough all distance elements and accumulate their rest lengths.
            int elementCount = elements.Count;
            for (int i = 0; i < elementCount; ++i)
                restLength_ += elements[i].restLength;
        }

        /// <summary>  
        /// Recalculates all particle rest positions, used when filtering self-collisions.
        /// </summary>
        public void RecalculateRestPositions()
        {
            float pos = 0;
            int elementCount = elements.Count;
            for (int i = 0; i < elementCount; ++i)
            {
                solver.restPositions[elements[i].particle1] = new Vector4(pos, 0, 0, 1);
                pos += elements[i].restLength;
                solver.restPositions[elements[i].particle2] = new Vector4(pos, 0, 0, 1);
            }
        }

        /// <summary>  
        /// Regenerates all rope elements using constraints. It's the opposite of RebuildConstraintsFromElements(). This is automatically called when loading a blueprint, but should also be called when manually
        /// altering rope constraints (adding/removing/updating constraints and/or batches).
        /// </summary>
        public void RebuildElementsFromConstraints()
        {
            RebuildElementsFromConstraintsInternal();
            if (OnElementsGenerated != null)
                OnElementsGenerated(this);
        }

        protected abstract void RebuildElementsFromConstraintsInternal();

        /// <summary>  
        /// Regenerates all rope constraints using rope elements. It's the opposite of RebuildElementsFromConstraints().This should be called anytime the element representation of the rope
        /// is changed (adding/removing/updating elements). This is usually the case after tearing the rope or changing its length using a cursor.
        /// </summary>
        public virtual void RebuildConstraintsFromElements() { }


        /// <summary>  
        /// Returns a rope element that contains a length-normalized coordinate. It will also return the length-normalized coordinate within the element.
        /// </summary>
        public ObiStructuralElement GetElementAt(float mu, out float elementMu)
        {
            float edgeMu = elements.Count * Mathf.Clamp(mu, 0, 0.99999f);

            int index = (int)edgeMu;
            elementMu = edgeMu - index;

            if (elements != null && index < elements.Count)
                return elements[index];
            return null;
        }

        /// <summary>  
        /// Returns index of the edge that contains a length-normalized coordinate. It will also return the length-normalized coordinate within the edge.
        /// </summary>
        public int GetEdgeAt(float mu, out float elementMu)
        {
            elementMu = -1;
            var ropeBlueprint = sharedBlueprint as ObiRopeBlueprintBase;
            if (ropeBlueprint != null && ropeBlueprint.deformableEdges != null)
            {
                float edgeMu = ropeBlueprint.deformableEdges.Length/2 * Mathf.Clamp(mu, 0, 0.99999f);

                int index = (int)edgeMu;
                elementMu = edgeMu - index;

                if (index < ropeBlueprint.deformableEdges.Length/2)
                    return index;
            }
            return -1;
        }

    }
}