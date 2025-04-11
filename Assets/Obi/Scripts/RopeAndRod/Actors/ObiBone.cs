using UnityEngine;
using System;
using System.Collections.Generic;

namespace Obi
{
    [AddComponentMenu("Physics/Obi/Obi Bone", 882)]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // make sure ObiBone's LateUpdate is updated after ObiSolver's.
    public class ObiBone : ObiActor, IStretchShearConstraintsUser, IBendTwistConstraintsUser, ISkinConstraintsUser, IAerodynamicConstraintsUser
    {
        [Serializable]
        public class BonePropertyCurve
        {
            [Min(0)]
            public float multiplier;
            public AnimationCurve curve;

            public BonePropertyCurve(float multiplier, float curveValue)
            {
                this.multiplier = multiplier;
                this.curve = new AnimationCurve(new Keyframe(0, curveValue), new Keyframe(1, curveValue));
            }

            public float Evaluate(float time)
            {
                return curve.Evaluate(time) * multiplier;
            }
        }

        [Serializable]
        public class IgnoredBone
        {
            public Transform bone;
            public bool ignoreChildren;
        }

        [NonSerialized] protected ObiBoneBlueprint m_BoneBlueprint;

        [SerializeField] protected bool m_SelfCollisions = false;

        [SerializeField] protected BonePropertyCurve _radius = new BonePropertyCurve(0.1f, 1);
        [SerializeField] protected BonePropertyCurve _mass = new BonePropertyCurve(0.1f, 1);
        [SerializeField] protected BonePropertyCurve _rotationalMass = new BonePropertyCurve(0.1f, 1);

        // skin constraints:
        [SerializeField] protected bool _skinConstraintsEnabled = true;
        [SerializeField] protected BonePropertyCurve _skinCompliance = new BonePropertyCurve(0.01f, 1);
        [SerializeField] protected BonePropertyCurve _skinRadius = new BonePropertyCurve(0.1f, 1);

        // distance constraints:
        [SerializeField] protected bool _stretchShearConstraintsEnabled = true;
        [SerializeField] protected BonePropertyCurve _stretchCompliance = new BonePropertyCurve(0, 1);
        [SerializeField] protected BonePropertyCurve _shear1Compliance = new BonePropertyCurve(0, 1);
        [SerializeField] protected BonePropertyCurve _shear2Compliance = new BonePropertyCurve(0, 1);

        // bend constraints:
        [SerializeField] protected bool _bendTwistConstraintsEnabled = true;
        [SerializeField] protected BonePropertyCurve _torsionCompliance = new BonePropertyCurve(0, 1);
        [SerializeField] protected BonePropertyCurve _bend1Compliance = new BonePropertyCurve(0, 1);
        [SerializeField] protected BonePropertyCurve _bend2Compliance = new BonePropertyCurve(0, 1);
        [SerializeField] protected BonePropertyCurve _plasticYield = new BonePropertyCurve(0, 1);
        [SerializeField] protected BonePropertyCurve _plasticCreep = new BonePropertyCurve(0, 1);

        // aerodynamics
        [SerializeField] protected bool _aerodynamicsEnabled = true;
        [SerializeField] protected BonePropertyCurve _drag = new BonePropertyCurve(0.05f, 1);
        [SerializeField] protected BonePropertyCurve _lift = new BonePropertyCurve(0.02f, 1);

        [Tooltip("Filter used for collision detection.")]
        [SerializeField] private int filter = ObiUtils.MakeFilter(ObiUtils.CollideWithEverything, 1);

        public bool fixRoot = true;
        public bool stretchBones = true;
        public List<IgnoredBone> ignored = new List<IgnoredBone>();

        /// <summary>  
        /// Collision filter value used by fluid particles.
        /// </summary>
        public int Filter
        {
            set
            {
                if (filter != value)
                {
                    filter = value;
                    UpdateFilter();
                }
            }
            get { return filter; }
        }

        /// <summary>  
        /// Whether particles in this actor colide with particles using the same phase value.
        /// </summary>
        public bool selfCollisions
        {
            get { return m_SelfCollisions; }
            set { if (value != m_SelfCollisions) { m_SelfCollisions = value; SetSelfCollisions(m_SelfCollisions); } }
        }

        /// <summary>  
        /// Particle radius distribution over this bone hierarchy length.
        /// </summary>
        public BonePropertyCurve radius
        {
            get { return _radius; }
            set { _radius = value; UpdateRadius(); }
        }

        /// <summary>  
        /// Mass distribution over this bone hierarchy length.
        /// </summary>
        public BonePropertyCurve mass
        {
            get { return _mass; }
            set { _mass = value; UpdateMasses(); }
        }

        /// <summary>  
        /// Rotational mass distribution over this bone hierarchy length.
        /// </summary>
        public BonePropertyCurve rotationalMass
        {
            get { return _rotationalMass; }
            set { _rotationalMass = value; UpdateMasses(); }
        }

        /// <summary>  
        /// Whether this actor's skin constraints are enabled.
        /// </summary>
        public bool skinConstraintsEnabled
        {
            get { return _skinConstraintsEnabled; }
            set { if (value != _skinConstraintsEnabled) { _skinConstraintsEnabled = value; SetConstraintsDirty(Oni.ConstraintType.Skin); } }
        }

        /// <summary>  
        /// Compliance of this actor's skin constraints.
        /// </summary>
        public BonePropertyCurve skinCompliance
        {
            get { return _skinCompliance; }
            set { _skinCompliance = value; SetConstraintsDirty(Oni.ConstraintType.Skin); }
        }

        /// <summary>  
        /// Compliance of this actor's skin radius
        /// </summary>
        public BonePropertyCurve skinRadius
        {
            get { return _skinRadius; }
            set { _skinRadius = value; SetConstraintsDirty(Oni.ConstraintType.Skin); }
        }

        /// <summary>  
        /// Whether this actor's stretch/shear constraints are enabled.
        /// </summary>
        public bool stretchShearConstraintsEnabled
        {
            get { return _stretchShearConstraintsEnabled; }
            set { if (value != _stretchShearConstraintsEnabled) { _stretchShearConstraintsEnabled = value; SetConstraintsDirty(Oni.ConstraintType.StretchShear); } }
        }

        /// <summary>  
        /// Compliance of this actor's stretch/shear constraints, along their length.
        /// </summary>
        public BonePropertyCurve stretchCompliance
        {
            get { return _stretchCompliance; }
            set { _stretchCompliance = value; SetConstraintsDirty(Oni.ConstraintType.StretchShear); }
        }

        /// <summary>  
        /// Shearing compliance of this actor's stretch/shear constraints, along the first axis orthogonal to their length.
        /// </summary>
        public BonePropertyCurve shear1Compliance
        {
            get { return _shear1Compliance; }
            set { _shear1Compliance = value; SetConstraintsDirty(Oni.ConstraintType.StretchShear); }
        }

        /// <summary>  
        /// Shearing compliance of this actor's stretch/shear constraints, along the second axis orthogonal to their length.
        /// </summary>
        public BonePropertyCurve shear2Compliance
        {
            get { return _shear2Compliance; }
            set { _shear2Compliance = value; SetConstraintsDirty(Oni.ConstraintType.StretchShear); }
        }

        /// <summary>  
        /// Whether this actor's bend/twist constraints are enabled.
        /// </summary>
        public bool bendTwistConstraintsEnabled
        {
            get { return _bendTwistConstraintsEnabled; }
            set { if (value != _bendTwistConstraintsEnabled) { _bendTwistConstraintsEnabled = value; SetConstraintsDirty(Oni.ConstraintType.BendTwist); } }
        }

        /// <summary>  
        /// Torsional compliance of this actor's bend/twist constraints along their length.
        /// </summary>
        public BonePropertyCurve torsionCompliance
        {
            get { return _torsionCompliance; }
            set { _torsionCompliance = value; SetConstraintsDirty(Oni.ConstraintType.BendTwist); }
        }

        /// <summary>  
        /// Bending compliance of this actor's bend/twist constraints along the first axis orthogonal to their length.
        /// </summary>
        public BonePropertyCurve bend1Compliance
        {
            get { return _bend1Compliance; }
            set { _bend1Compliance = value; SetConstraintsDirty(Oni.ConstraintType.BendTwist); }
        }

        /// <summary>  
        /// Bending compliance of this actor's bend/twist constraints along the second axis orthogonal to their length.
        /// </summary>
        public BonePropertyCurve bend2Compliance
        {
            get { return _bend2Compliance; }
            set { _bend2Compliance = value; SetConstraintsDirty(Oni.ConstraintType.BendTwist); }
        }

        /// <summary>  
        /// Threshold for plastic behavior. 
        /// </summary>
        /// Once bending goes above this value, a percentage of the deformation (determined by <see cref="plasticCreep"/>) will be permanently absorbed into the rod's rest shape.
        public BonePropertyCurve plasticYield
        {
            get { return _plasticYield; }
            set { _plasticYield = value; SetConstraintsDirty(Oni.ConstraintType.BendTwist); }
        }

        /// <summary>  
        /// Percentage of deformation that gets absorbed into the rest shape per second, once deformation goes above the <see cref="plasticYield"/> threshold.
        /// </summary>
        public BonePropertyCurve plasticCreep
        {
            get { return _plasticCreep; }
            set { _plasticCreep = value; SetConstraintsDirty(Oni.ConstraintType.BendTwist); }
        }

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
        public BonePropertyCurve drag
        {
            get { return _drag; }
            set { _drag = value; SetConstraintsDirty(Oni.ConstraintType.Aerodynamics); }
        }

        /// <summary>  
        /// Aerodynamic lift value.
        /// </summary>
        public BonePropertyCurve lift
        {
            get { return _lift; }
            set { _lift = value; SetConstraintsDirty(Oni.ConstraintType.Aerodynamics); }
        }


        public override ObiActorBlueprint sourceBlueprint
        {
            get { return m_BoneBlueprint; }
        }

        public ObiBoneBlueprint boneBlueprint
        {
            get { return m_BoneBlueprint; }
            set
            {
                if (m_BoneBlueprint != value)
                {
                    RemoveFromSolver();
                    ClearState();
                    m_BoneBlueprint = value;
                    AddToSolver();
                }
            }
        }

        protected override void Awake()
        {
            m_BoneBlueprint = ScriptableObject.CreateInstance<ObiBoneBlueprint>();
            UpdateBlueprint();
            base.Awake();
        }

        protected override void OnDestroy()
        {
            if (m_BoneBlueprint != null)
                DestroyImmediate(m_BoneBlueprint);

            base.OnDestroy();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            UpdateFilter();
            UpdateRadius();
            UpdateMasses();
            SetupRuntimeConstraints();
        }

        public void UpdateBlueprint()
        {
            if (m_BoneBlueprint != null)
            {
                m_BoneBlueprint.root = transform;
                m_BoneBlueprint.ignored = ignored;
                m_BoneBlueprint.mass = mass;
                m_BoneBlueprint.rotationalMass = rotationalMass;
                m_BoneBlueprint.radius = radius;
                m_BoneBlueprint.GenerateImmediate();
            }
        }

        internal override void LoadBlueprint()
        {
            base.LoadBlueprint();

            // synchronously read required data from GPU:
            solver.renderablePositions.Readback(false);
            solver.renderableOrientations.Readback(false);
            solver.orientations.Readback(false);
            solver.angularVelocities.Readback(false);

            SetupRuntimeConstraints();
            ResetToCurrentShape();
        }

        internal override void UnloadBlueprint()
        {
            ResetParticles();
            CopyParticleDataToTransforms();
            base.UnloadBlueprint();
        }

        public override void RequestReadback()
        {
            base.RequestReadback();

            solver.orientations.Readback();
            solver.angularVelocities.Readback();
            solver.renderablePositions.Readback();
            solver.renderableOrientations.Readback();
        }

        public override void SimulationEnd(float simulatedTime, float substepTime)
        {
            base.SimulationEnd(simulatedTime, substepTime);

            solver.orientations.WaitForReadback();
            solver.angularVelocities.WaitForReadback();
            solver.renderablePositions.WaitForReadback();
            solver.renderableOrientations.WaitForReadback();
        }

        private void SetupRuntimeConstraints()
        {
            SetConstraintsDirty(Oni.ConstraintType.Skin);
            SetConstraintsDirty(Oni.ConstraintType.StretchShear);
            SetConstraintsDirty(Oni.ConstraintType.BendTwist);
            SetConstraintsDirty(Oni.ConstraintType.Aerodynamics);
            SetSelfCollisions(selfCollisions);
            SetSimplicesDirty();
            UpdateFilter();
        }

        public override void ProvideDeformableEdges(ObiNativeIntList deformableEdges)
        {
            var boneBprint = sharedBlueprint as ObiBoneBlueprint;
            if (boneBprint != null && boneBprint.deformableEdges != null)
            {
                // Send deformable edge indices to the solver:
                for (int i = 0; i < boneBprint.deformableEdges.Length; ++i)
                    deformableEdges.Add(solverIndices[boneBprint.deformableEdges[i]]);
            }
        }

        private void FixRoot()
        {
            if (isLoaded)
            {
                int rootIndex = solverIndices[0];

                var actor2Solver = actorLocalToSolverMatrix;
                var actor2SolverR = actor2Solver.rotation;

                solver.invMasses[rootIndex] = 0;
                solver.invRotationalMasses[rootIndex] = 0;
                solver.velocities[rootIndex] = Vector4.zero;
                solver.angularVelocities[rootIndex] = Vector4.zero;

                // take particle rest position in actor space (which is always zero), converts to solver space:
                solver.startPositions[rootIndex] = solver.endPositions[rootIndex] = solver.positions[rootIndex] = actor2Solver.MultiplyPoint3x4(Vector3.zero);

                // take particle rest orientation in actor space, and convert to solver space:
                solver.startOrientations[rootIndex] = solver.endOrientations[rootIndex] = solver.orientations[rootIndex] = actor2SolverR * boneBlueprint.orientations[0];
            }
        }

        private void UpdateFilter()
        {
            for (int i = 0; i < particleCount; i++)
            {
                boneBlueprint.filters[i] = filter;
                if (isLoaded)
                    solver.filters[solverIndices[i]] = filter;
            }
        }

        public void UpdateRadius()
        {
            for (int i = 0; i < particleCount; ++i)
            {
                var normalizedCoord = boneBlueprint.normalizedLengths[i];
                var radii = Vector3.one * radius.Evaluate(normalizedCoord);
                boneBlueprint.principalRadii[i] = radii;

                if (isLoaded)
                    solver.principalRadii[solverIndices[i]] = radii;
            }
        }

        public void UpdateMasses()
        {
            for (int i = 0; i < particleCount; ++i)
            {
                var normalizedCoord = boneBlueprint.normalizedLengths[i];
                var invMass = ObiUtils.MassToInvMass(mass.Evaluate(normalizedCoord));
                var invRotMass = ObiUtils.MassToInvMass(rotationalMass.Evaluate(normalizedCoord));

                boneBlueprint.invMasses[i] = invMass;
                boneBlueprint.invRotationalMasses[i] = invRotMass;

                if (isLoaded)
                {
                    solver.invMasses[solverIndices[i]] = invMass;
                    solver.invRotationalMasses[solverIndices[i]] = invRotMass;
                }
            }
        }

        public Vector3 GetSkinRadiiBackstop(ObiSkinConstraintsBatch batch, int constraintIndex)
        {
            float normalizedCoord = boneBlueprint.normalizedLengths[batch.particleIndices[constraintIndex]];
            return new Vector3(skinRadius.Evaluate(normalizedCoord), 0, 0);
        }

        public float GetSkinCompliance(ObiSkinConstraintsBatch batch, int constraintIndex)
        {
            float normalizedCoord = boneBlueprint.normalizedLengths[batch.particleIndices[constraintIndex]];
            return skinCompliance.Evaluate(normalizedCoord);
        }


        public Vector3 GetBendTwistCompliance(ObiBendTwistConstraintsBatch batch, int constraintIndex)
        {
            float normalizedCoord = boneBlueprint.normalizedLengths[batch.particleIndices[constraintIndex * 2]];
            return new Vector3(bend1Compliance.Evaluate(normalizedCoord),
                               bend2Compliance.Evaluate(normalizedCoord),
                               torsionCompliance.Evaluate(normalizedCoord));
        }

        public Vector2 GetBendTwistPlasticity(ObiBendTwistConstraintsBatch batch, int constraintIndex)
        {
            float normalizedCoord = boneBlueprint.normalizedLengths[batch.particleIndices[constraintIndex * 2]];
            return new Vector2(plasticYield.Evaluate(normalizedCoord),
                               plasticCreep.Evaluate(normalizedCoord));
        }

        public Vector3 GetStretchShearCompliance(ObiStretchShearConstraintsBatch batch, int constraintIndex)
        {
            float normalizedCoord = boneBlueprint.normalizedLengths[batch.particleIndices[constraintIndex * 2]];
            return new Vector3(shear1Compliance.Evaluate(normalizedCoord),
                               shear2Compliance.Evaluate(normalizedCoord),
                               stretchCompliance.Evaluate(normalizedCoord));
        }

        public float GetDrag(ObiAerodynamicConstraintsBatch batch, int constraintIndex)
        {
            float normalizedCoord = boneBlueprint.normalizedLengths[batch.particleIndices[constraintIndex]];
            return drag.Evaluate(normalizedCoord);
        }

        public float GetLift(ObiAerodynamicConstraintsBatch batch, int constraintIndex)
        {
            float normalizedCoord = boneBlueprint.normalizedLengths[batch.particleIndices[constraintIndex]];
            return lift.Evaluate(normalizedCoord);
        }

        public void FixedUpdate()
        {
            // This resets all bones not affected by animation,
            // needs to happen once per frame at the very start before Animators are updated.
            ResetReferenceOrientations();
        }

        public override void SimulationStart(float timeToSimulate, float substepTime)
        {
            base.SimulationStart(timeToSimulate, substepTime);

            if (fixRoot)
                FixRoot();

            UpdateRestShape();
        }

        public void LateUpdate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
                CopyParticleDataToTransforms();
        }

        /// <summary>
        /// Resets particle orientations/positions to match the current pose of the bone hierarchy, and sets all their velocities to zero.
        /// </summary>
        public void ResetToCurrentShape()
        {
            if (!isLoaded) return;

            var world2Solver = solver.transform.worldToLocalMatrix;

            for (int i = 0; i < particleCount; ++i)
            {
                var trfm = boneBlueprint.transforms[i];
                int solverIndex = solverIndices[i];

                solver.velocities[solverIndex] = Vector4.zero;
                solver.angularVelocities[solverIndex] = Vector4.zero;

                solver.startPositions[solverIndex] = solver.endPositions[solverIndex] = solver.positions[solverIndex] = world2Solver.MultiplyPoint3x4(trfm.position);

                var boneDeltaAWS = trfm.rotation * Quaternion.Inverse(boneBlueprint.restOrientations[i]);
                solver.startOrientations[solverIndex] = solver.endOrientations[solverIndex] = solver.orientations[solverIndex] = world2Solver.rotation * boneDeltaAWS * boneBlueprint.root2WorldR * boneBlueprint.orientations[i];
            }

            // Update constraint data in the blueprint, since StartSimulation won't be called until next frame.
            var bc = GetConstraintsByType(Oni.ConstraintType.BendTwist) as ObiConstraints<ObiBendTwistConstraintsBatch>;

            if (bc != null)
                for (int j = 0; j < bc.batchCount; ++j)
                {
                    var batch = bc.GetBatch(j) as ObiBendTwistConstraintsBatch;

                    for (int i = 0; i < batch.activeConstraintCount; i++)
                    {
                        int indexA = batch.particleIndices[i * 2];
                        int indexB = batch.particleIndices[i * 2 + 1];

                        // calculate bone rotation delta in world space:
                        var boneDeltaAWS = boneBlueprint.transforms[indexA].rotation * Quaternion.Inverse(boneBlueprint.restOrientations[indexA]);
                        var boneDeltaBWS = boneBlueprint.transforms[indexB].rotation * Quaternion.Inverse(boneBlueprint.restOrientations[indexB]);

                        // apply delta to rest particle orientation:
                        var orientationA = boneDeltaAWS * boneBlueprint.root2WorldR * boneBlueprint.orientations[indexA];
                        var orientationB = boneDeltaBWS * boneBlueprint.root2WorldR * boneBlueprint.orientations[indexB];

                        batch.restDarbouxVectors[i] = ObiUtils.RestDarboux(orientationA, orientationB);
                    }
                }

            var sc = GetConstraintsByType(Oni.ConstraintType.Skin) as ObiConstraints<ObiSkinConstraintsBatch>;

            if (sc != null)
                for (int j = 0; j < sc.batchCount; ++j)
                {
                    var batch = sc.GetBatch(j) as ObiSkinConstraintsBatch;
                    for (int i = 0; i < batch.activeConstraintCount; i++)
                    {
                        int index = batch.particleIndices[i];
                        batch.skinPoints[i] = solver.transform.worldToLocalMatrix.MultiplyPoint3x4(boneBlueprint.transforms[index].position);
                    }
                }
        }

        private void ResetReferenceOrientations()
        {
            if (boneBlueprint != null)
                for (int i = 1; i < boneBlueprint.restTransformOrientations.Count; ++i)
                    boneBlueprint.transforms[i].localRotation = boneBlueprint.restTransformOrientations[i];
        }

        private void UpdateRestShape()
        {
            // use current bone transforms as rest state for the simulation:
            var bc = GetConstraintsByType(Oni.ConstraintType.BendTwist) as ObiConstraints<ObiBendTwistConstraintsBatch>;
            var sbc = solver.GetConstraintsByType(Oni.ConstraintType.BendTwist) as ObiConstraints<ObiBendTwistConstraintsBatch>;

            if (bendTwistConstraintsEnabled && bc != null && sbc != null)
            {
                // iterate up to the amount of entries in solverBatchOffsets, insteaf of bc.batchCount. This ensures
                // the batches we access have been added to the solver, as solver.UpdateConstraints() could have not been called yet on a newly added actor.
                for (int j = 0; j < solverBatchOffsets[(int)Oni.ConstraintType.BendTwist].Count; ++j)
                {
                    var batch = bc.GetBatch(j) as ObiBendTwistConstraintsBatch;
                    var solverBatch = sbc.batches[j] as ObiBendTwistConstraintsBatch;
                    int offset = solverBatchOffsets[(int)Oni.ConstraintType.BendTwist][j];

                    if (solverBatch.restDarbouxVectors.isCreated)
                    {
                        if (solverBatch.restDarbouxVectors.computeBuffer == null)
                            solverBatch.restDarbouxVectors.SafeAsComputeBuffer<Vector4>();

                        for (int i = 0; i < batch.activeConstraintCount; i++)
                        {
                            int indexA = batch.particleIndices[i * 2];
                            int indexB = batch.particleIndices[i * 2 + 1];

                            // calculate bone rotation delta in world space:
                            var boneDeltaAWS = boneBlueprint.transforms[indexA].rotation * Quaternion.Inverse(boneBlueprint.restOrientations[indexA]);
                            var boneDeltaBWS = boneBlueprint.transforms[indexB].rotation * Quaternion.Inverse(boneBlueprint.restOrientations[indexB]);

                            // apply delta to rest particle orientation:
                            var orientationA = boneDeltaAWS * boneBlueprint.root2WorldR * boneBlueprint.orientations[indexA];
                            var orientationB = boneDeltaBWS * boneBlueprint.root2WorldR * boneBlueprint.orientations[indexB];

                            solverBatch.restDarbouxVectors[offset + i] = ObiUtils.RestDarboux(orientationA, orientationB);
                        }

                        solverBatch.restDarbouxVectors.Upload();
                    }
                }
            }

            var sc = GetConstraintsByType(Oni.ConstraintType.Skin) as ObiConstraints<ObiSkinConstraintsBatch>;
            var ssc = solver.GetConstraintsByType(Oni.ConstraintType.Skin) as ObiConstraints<ObiSkinConstraintsBatch>;

            if (skinConstraintsEnabled && sc != null && ssc != null)
            {
                // iterate up to the amount of entries in solverBatchOffsets, insteaf of sc.batchCount. This ensures
                // the batches we access have been added to the solver, as solver.UpdateConstraints() could have not been called yet on a newly added actor.
                for (int j = 0; j < solverBatchOffsets[(int)Oni.ConstraintType.Skin].Count; ++j)
                {
                    var batch = sc.GetBatch(j) as ObiSkinConstraintsBatch;
                    var solverBatch = ssc.batches[j] as ObiSkinConstraintsBatch;
                    int offset = solverBatchOffsets[(int)Oni.ConstraintType.Skin][j];

                    if (solverBatch.skinPoints.isCreated)
                    {
                        if (solverBatch.skinPoints.computeBuffer == null)
                            solverBatch.skinPoints.SafeAsComputeBuffer<Vector4>();

                        for (int i = 0; i < batch.activeConstraintCount; i++)
                        {
                            int index = batch.particleIndices[i];
                            solverBatch.skinPoints[offset + i] = solver.transform.worldToLocalMatrix.MultiplyPoint3x4(boneBlueprint.transforms[index].position);
                        }

                        solverBatch.skinPoints.Upload();
                    }
                }
            }
        }

        private void CopyParticleDataToTransforms()
        {
            if (isLoaded && boneBlueprint != null)
            {
                // copy current particle transforms to bones:
                for (int i = 1; i < particleCount; ++i)
                {
                    var trfm = boneBlueprint.transforms[i];

                    if (stretchBones)
                        trfm.position = GetParticlePosition(solverIndices[i]);

                    var delta = GetParticleOrientation(solverIndices[i]) * Quaternion.Inverse(boneBlueprint.root2WorldR * boneBlueprint.orientations[i]);
                    trfm.rotation = delta * boneBlueprint.restOrientations[i];
                }
            }
        }
    }
}
