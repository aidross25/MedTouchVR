using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Obi
{

    /**
     * Represents a group of related particles. ObiActor does not make
     * any assumptions about the relationship between these particles, except that they get allocated 
     * and released together.
     */
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public abstract class ObiActor : MonoBehaviour, IObiParticleCollection
    {
        public class ObiActorSolverArgs : System.EventArgs
        {
            public ObiSolver solver { get; }

            public ObiActorSolverArgs(ObiSolver solver)
            {
                this.solver = solver;
            }
        }

        private struct BufferedForces
        {
            public bool dirty;

            public Vector4 force;
            public Vector4 acceleration;
            public Vector4 impulse;
            public Vector4 velChange;

            public Vector4 angularForce;
            public Vector4 angularAcceleration;
            public Vector4 angularImpulse;
            public Vector4 angularVelChange;

            public void Clear()
            {
                force = Vector4.zero;
                acceleration = Vector4.zero;
                impulse = Vector4.zero;
                velChange = Vector4.zero;
                angularForce = Vector4.zero;
                angularAcceleration = Vector4.zero;
                angularImpulse = Vector4.zero;
                angularVelChange = Vector4.zero;
            }
        }

        public delegate void ActorCallback(ObiActor actor);
        public delegate void ActorStepCallback(ObiActor actor, float simulatedTime, float substepTime);
        public delegate void ActorBlueprintCallback(ObiActor actor, ObiActorBlueprint blueprint);

        /// <summary>
        /// Called when the actor blueprint has been loaded into the solver.
        /// </summary>
        public event ActorBlueprintCallback OnBlueprintLoaded;

        /// <summary>
        /// Called when the actor blueprint has been unloaded from the solver.
        /// </summary>
        public event ActorBlueprintCallback OnBlueprintUnloaded;

        /// <summary>
        /// Called when the blueprint currently in use has been re-generated. This will always be preceded by a call to
        /// OnBlueprintUnloaded and OnBlueprintLoaded.
        /// </summary>
        public event ActorBlueprintCallback OnBlueprintRegenerated;

        /// <summary>
        /// Called before simulation starts.
        /// </summary>
        public event ActorStepCallback OnSimulationStart;

        /// <summary>
        /// Called after simulation ends.
        /// </summary>
        public event ActorStepCallback OnSimulationEnd;

        /// <summary>
        /// You can use this callback to issue GPU->CPU readbacks.
        /// </summary>
        public event ActorCallback OnRequestReadback;

        /// <summary>
        /// Called at the end of each frame, after interpolation but before rendering.
        /// </summary>
        public event ActorStepCallback OnInterpolate;

        [HideInInspector] protected int m_ActiveParticleCount = 0;

        /// <summary>
        /// Index of each one of the actor's particles in the solver.
        /// </summary>
        [HideInInspector] public ObiNativeIntList solverIndices;

        /// <summary>
        /// For each of the actor's constraint types, offset of every batch in the solver.
        /// </summary>
        [HideInInspector] public List<int>[] solverBatchOffsets;

        public int deformableEdgesOffset { protected set; get; } /**< index of the first deformable edge in the solver that belongs to this rope.*/

        protected ObiSolver m_Solver;
        protected bool m_Loaded = false;
        public int groupID = 0;

        private ObiActorBlueprint m_State;
        private ObiActorBlueprint m_BlueprintInstance;
        private ObiPinConstraintsData m_PinConstraints;
        private ObiPinholeConstraintsData m_PinholeConstraints;
        private BufferedForces bufferedForces = new BufferedForces();
        [SerializeField] [HideInInspector] protected ObiCollisionMaterial m_CollisionMaterial;
        [SerializeField] [HideInInspector] protected bool m_SurfaceCollisions = false;
        [SerializeField] [HideInInspector] [Min(ObiUtils.epsilon)] protected float m_MassScale = 1;

        /// <summary>
        /// The solver in charge of simulating this actor.
        /// </summary>
        /// This is the first ObiSolver component found up the actor's hierarchy.
        public ObiSolver solver
        {
            get { return m_Solver; }
        }

        /// <summary>
        /// True if the actor blueprint has been loaded into a solver.
        /// If true, it guarantees actor.solver, actor.solverIndices and actor.solverBatchOffsets won't be null.
        /// </summary>
        public bool isLoaded
        {
            get { return m_Solver != null && m_Loaded; }
        }

        /// <summary>
        /// The collision material being used by this actor.
        /// </summary>
        public ObiCollisionMaterial collisionMaterial
        {
            get
            {
                return m_CollisionMaterial;
            }
            set
            {
                if (m_CollisionMaterial != value)
                {
                    m_CollisionMaterial = value;
                    UpdateCollisionMaterials();
                }
            }
        }

        /// <summary>
        /// Whether to use simplices (triangles, edges) for contact generation.
        /// </summary>
        public virtual bool surfaceCollisions
        {
            get
            {
                return m_SurfaceCollisions;
            }
            set
            {
                if (value != m_SurfaceCollisions)
                {
                    m_SurfaceCollisions = value;
                    if (m_Solver != null)
                    {
                        m_Solver.dirtySimplices |= simplexTypes;
                    }
                }
            }
        }

        /// <summary>
        /// Scale applied to this actor's particle masses.
        /// </summary>
        public float massScale
        {
            get
            {
                return m_MassScale;
            }
            set
            {
                if (Mathf.Abs(m_MassScale - value) > ObiUtils.epsilon)
                    SetMassScale(value);
            }
        }

        /// <summary>
        /// Amount of particles allocated by this actor.
        /// </summary>
        public int particleCount
        {
            get
            {
                return sourceBlueprint != null ? sourceBlueprint.particleCount : 0;
            }
        }

        /// <summary>
        /// Amount of particles in use by this actor. 
        /// </summary>
        /// This will always be equal to or smaller than <see cref="particleCount"/>.
        public int activeParticleCount
        {
            get
            {
                return m_ActiveParticleCount;
            }
        }

        /// <summary>
        /// Whether this actors makes use of particle orientations or not.
        /// </summary>
        public bool usesOrientedParticles
        {
            get
            {
                return sourceBlueprint != null &&
                       sourceBlueprint.invRotationalMasses != null && sourceBlueprint.invRotationalMasses.Length > 0 &&
                       sourceBlueprint.orientations != null && sourceBlueprint.orientations.Length > 0 &&
                       sourceBlueprint.restOrientations != null && sourceBlueprint.restOrientations.Length > 0;
            }
        }

        /// <summary>
        /// If true, it means particles may not be completely spherical, but ellipsoidal.
        /// </summary>
        public virtual bool usesAnisotropicParticles
        {
            get
            {
                return false;
            }
        }

        public Oni.SimplexType simplexTypes
        {
            get
            {
                return (sourceBlueprint != null) ? sourceBlueprint.simplexTypes : Oni.SimplexType.Point;
            }
        }

        /// <summary>
        /// Matrix that transforms from the actor's local space to the solver's local space.
        /// </summary>
        /// If there's no solver present, this is the same as the actor's local to world matrix.
        public Matrix4x4 actorLocalToSolverMatrix
        {
            get
            {
                if (m_Solver != null)
                    return m_Solver.transform.worldToLocalMatrix * transform.localToWorldMatrix;
                else
                    return transform.localToWorldMatrix;
            }
        }

        /// <summary>
        /// Matrix that transforms from the solver's local space to the actor's local space.
        /// </summary>
        /// If there's no solver present, this is the same as the actor's world to local matrix.
        /// This is always the same as the inverse of <see cref="actorLocalToSolverMatrix"/>.
        public Matrix4x4 actorSolverToLocalMatrix
        {
            get
            {
                if (m_Solver != null)
                    return transform.worldToLocalMatrix * m_Solver.transform.localToWorldMatrix;
                else
                    return transform.worldToLocalMatrix;
            }
        }

        /// <summary>
        /// Reference to the blueprint asset used by this actor.
        /// </summary>
        public abstract ObiActorBlueprint sourceBlueprint
        {
            get;
        }

        /// <summary>
        /// Reference to the blueprint in use by this actor. 
        /// </summary>
        /// If you haven't called <see cref="blueprint"/> before, this will be the same as <see cref="sourceBlueprint"/>.
        /// If you have called <see cref="blueprint"/> before, it will be the same as <see cref="blueprint"/>.
        public ObiActorBlueprint sharedBlueprint
        {
            get
            {
                if (m_BlueprintInstance != null)
                    return m_BlueprintInstance;
                return sourceBlueprint;
            }
        }

        /// <summary>
        /// Returns a unique instance of this actor's <see cref="sourceBlueprint"/>. 
        /// </summary>
        /// This is mostly used when the actor needs to change some blueprint data at runtime,
        /// and you don't want to change the blueprint asset as this would affect all other actors using it. Tearable cloth and ropes
        /// make use of this.
        public ObiActorBlueprint blueprint
        {
            get
            {
                if (m_BlueprintInstance == null && sourceBlueprint != null)
                    m_BlueprintInstance = Instantiate(sourceBlueprint);

                return m_BlueprintInstance;
            }
        }

        protected virtual void Awake()
        {
#if UNITY_EDITOR

            // Check if this script's GameObject is in a PrefabStage
#if UNITY_2021_2_OR_NEWER
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject);
#else
            var prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject);
#endif

            if (prefabStage != null)
            {
                // Only create a solver if there's not one up our hierarchy.
                if (GetComponentInParent<ObiSolver>() == null)
                {
                    // Add our own environment root and move it to the PrefabStage scene
                    var newParent = new GameObject("ObiSolver (Environment)", typeof(ObiSolver));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(newParent, gameObject.scene);
                    transform.root.parent = newParent.transform;
                }
            }
#endif
        }

        protected virtual void OnDestroy()
        {
            if (m_BlueprintInstance != null)
                DestroyImmediate(m_BlueprintInstance);
        }

        protected virtual void OnEnable()
        {
            solverBatchOffsets = new List<int>[Oni.ConstraintTypeCount];
            for (int i = 0; i < solverBatchOffsets.Length; ++i)
                solverBatchOffsets[i] = new List<int>();

            m_PinConstraints = new ObiPinConstraintsData();
            m_PinholeConstraints = new ObiPinholeConstraintsData();

            // when an actor is enabled, grabs the first solver up its hierarchy,
            // initializes it (if not initialized) and gets added to it.
            m_Solver = GetComponentInParent<ObiSolver>();

            AddToSolver();
        }

        protected virtual void OnDisable()
        {
            RemoveFromSolver();
        }

        protected virtual void OnValidate()
        {
            UpdateCollisionMaterials();
        }

        private void OnTransformParentChanged()
        {
            if (isActiveAndEnabled)
                SetSolver(GetComponentInParent<ObiSolver>());
        }

        /// <summary>
        /// Adds this actor to its solver, if any. Automatically called by <see cref="ObiSolver"/>.
        /// </summary>
        public void AddToSolver()
        {
            if (m_Solver != null)
            {
                if (sourceBlueprint != null)
                    sourceBlueprint.OnBlueprintGenerate += OnBlueprintRegenerate;

                m_Solver.AddActor(this);
            }
        }

        /// <summary>
        /// Remove this actor from its solver, if any. Automatically called by <see cref="ObiSolver"/>.
        /// </summary>
        public void RemoveFromSolver()
        {
            if (m_Solver != null)
            {
                if (sourceBlueprint != null)
                    sourceBlueprint.OnBlueprintGenerate -= OnBlueprintRegenerate;

                m_Solver.RemoveActor(this);
            }
        }

        /// <summary>
        /// Forcibly changes the solver in charge of this actor
        /// </summary>
        /// <param name="newSolver"> The solver we want to put in charge of this actor.</param>  
        /// First it removes the actor from its current solver, then changes the actor's current solver and then readds it to this new solver.
        protected void SetSolver(ObiSolver newSolver)
        {
            // In case the first solver up our hierarchy is not the one we're currently in, change solver.
            if (newSolver != m_Solver)
            {
                RemoveFromSolver();

                m_Solver = newSolver;

                AddToSolver();
            }
        }

        /// <summary>
        /// Sets the mass of all particles in the actor to their blueprint values, multiplied by a scale factor.
        /// </summary>
        /// <param name="scale"> new mass scale.
        protected void SetMassScale(float scale)
        {
            if (Application.isPlaying && isLoaded && particleCount > 0)
            {
                scale = Mathf.Max(ObiUtils.epsilon, scale);

                for (int i = 0; i < particleCount; ++i)
                {
                    int solverIndex = solverIndices[i];

                    if (m_Solver.invMasses[solverIndex] > 0)
                        m_Solver.invMasses[solverIndex] = sharedBlueprint.invMasses[i] / scale;

                    if (m_Solver.invRotationalMasses[solverIndex] > 0 && sharedBlueprint.invRotationalMasses != null && i < sharedBlueprint.invRotationalMasses.Length)
                       m_Solver.invRotationalMasses[solverIndex] = sharedBlueprint.invRotationalMasses[i] / scale;
                }

                UpdateParticleProperties();
            }
        }

        protected virtual void OnBlueprintRegenerate(ObiActorBlueprint blueprint)
        {
            // Reload by removing the current blueprint from the solver,
            // destroying the current blueprint instance if any,
            // then adding the shared blueprint again.

            RemoveFromSolver();

            if (m_BlueprintInstance != null)
               DestroyImmediate(m_BlueprintInstance);

            AddToSolver();
            
            OnBlueprintRegenerated?.Invoke(this, blueprint);
        }

        protected void UpdateCollisionMaterials()
        {
            if (isLoaded)
            {
                int index = m_CollisionMaterial != null ? m_CollisionMaterial.handle.index : -1;
                for (int i = 0; i < solverIndices.count; i++)
                {
                    if (solverIndices[i] < solver.collisionMaterials.count)
                        solver.collisionMaterials[solverIndices[i]] = index;
                }
            }
        }

        public virtual void ProvideDeformableTriangles(ObiNativeIntList deformableTriangles, ObiNativeVector2List deformableUVs)
        {

        }

        public virtual void ProvideDeformableEdges(ObiNativeIntList deformableEdges)
        {

        }

        public virtual int GetDeformableEdgeCount()
        {
            return 0;
        }

        /// <summary>
        /// Copies all data (position, velocity, phase, etc) from one particle to another one. 
        /// </summary>
        /// <param name="actorSourceIndex"> Index in the actor arrays of the particle we will copy data from.</param>
        /// <param name="actorDestIndex"> Index in the actor arrays of the particle we will copy data to.</param>
        /// <returns>
        /// Whether the indices passed are within actor bounds.
        /// </returns> 
        /// Extend this method to implement copying your own custom particle data in custom actors.
        public virtual bool CopyParticle(int actorSourceIndex, int actorDestIndex)
        {
            if (!isLoaded ||
                actorSourceIndex < 0 || actorSourceIndex >= solverIndices.count ||
                actorDestIndex < 0 || actorDestIndex >= solverIndices.count)
                return false;

            int sourceIndex = solverIndices[actorSourceIndex];
            int destIndex = solverIndices[actorDestIndex];

            // Copy solver data:
            m_Solver.prevPositions[destIndex] = m_Solver.prevPositions[sourceIndex];
            m_Solver.restPositions[destIndex] = m_Solver.restPositions[sourceIndex];
            m_Solver.endPositions[destIndex] = m_Solver.startPositions[destIndex] = m_Solver.positions[destIndex] = m_Solver.positions[sourceIndex];

            m_Solver.prevOrientations[destIndex] = m_Solver.prevOrientations[sourceIndex];
            m_Solver.restOrientations[destIndex] = m_Solver.restOrientations[sourceIndex];
            m_Solver.endOrientations[destIndex] = m_Solver.startOrientations[destIndex] = m_Solver.orientations[destIndex] = m_Solver.orientations[sourceIndex];

            m_Solver.velocities[destIndex] = m_Solver.velocities[sourceIndex];
            m_Solver.angularVelocities[destIndex] = m_Solver.angularVelocities[sourceIndex];
            m_Solver.invMasses[destIndex] = m_Solver.invMasses[sourceIndex];
            m_Solver.invRotationalMasses[destIndex] = m_Solver.invRotationalMasses[sourceIndex];
            m_Solver.principalRadii[destIndex] = m_Solver.principalRadii[sourceIndex];
            m_Solver.phases[destIndex] = m_Solver.phases[sourceIndex];
            m_Solver.filters[destIndex] = m_Solver.filters[sourceIndex];
            m_Solver.colors[destIndex] = m_Solver.colors[sourceIndex];

            return true;
        }

        /// <summary>
        /// Teleports one actor particle to a certain position in solver space.
        /// </summary>
        /// <param name="actorIndex"> Index in the actor arrays of the particle we will teeleport.</param>
        /// <param name="position"> Position to teleport the particle to, expressed in solver space.</param>
        public void TeleportParticle(int actorIndex, Vector3 position)
        {
            if (!isLoaded || actorIndex < 0 || actorIndex >= solverIndices.count)
                return;

            int solverIndex = solverIndices[actorIndex];

            Vector4 delta = (Vector4)position - m_Solver.positions[solverIndex];
            m_Solver.positions[solverIndex] += delta;
            m_Solver.prevPositions[solverIndex] += delta;
            m_Solver.endPositions[solverIndex] += delta;
            m_Solver.startPositions[solverIndex] += delta;
        }

        /// <summary>
        /// Teleports the entire actor to a new location / orientation.
        /// </summary>
        /// <param name="position"> World space position to teleport the actor to.</param>
        /// <param name="rotation"> World space rotation to teleport the actor to.</param>
        public virtual Matrix4x4 Teleport(Vector3 position, Quaternion rotation)
        {
            if (!isLoaded)
                return Matrix4x4.identity;

            // Subtract current transform position/rotation, then add new world space position/rotation.
            // Lastly, set the transform to the new position/rotation.
            Matrix4x4 offset = solver.transform.worldToLocalMatrix *
                               Matrix4x4.TRS(position, Quaternion.identity, Vector3.one) *
                               Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one) *
                               Matrix4x4.TRS(Vector3.zero, Quaternion.Inverse(transform.rotation), Vector3.one) *
                               Matrix4x4.TRS(-transform.position, Quaternion.identity, Vector3.one) *
                               solver.transform.localToWorldMatrix;

            Quaternion rotOffset = offset.rotation;

            for (int i = 0; i < solverIndices.count; i++)
            {
                int solverIndex = solverIndices[i];

                m_Solver.positions[solverIndex] =
                m_Solver.prevPositions[solverIndex] =
                m_Solver.endPositions[solverIndex] =
                m_Solver.startPositions[solverIndex] = offset.MultiplyPoint3x4(m_Solver.positions[solverIndex]);

                m_Solver.orientations[solverIndex] =
                m_Solver.prevOrientations[solverIndex] =
                m_Solver.endOrientations[solverIndex] =
                m_Solver.startOrientations[solverIndex] = rotOffset * m_Solver.orientations[solverIndex];

                m_Solver.velocities[solverIndex] = Vector4.zero;
                m_Solver.angularVelocities[solverIndex] = Vector4.zero;
            }

            transform.position = position;
            transform.rotation = rotation;

            return offset;
        }

        protected virtual void SwapWithFirstInactiveParticle(int actorIndex)
        {
            // update solver indices:
            m_Solver.particleToActor[solverIndices[actorIndex]].indexInActor = activeParticleCount;
            m_Solver.particleToActor[solverIndices[activeParticleCount]].indexInActor = actorIndex;
            solverIndices.Swap(actorIndex, activeParticleCount);
        }

        /// <summary>
        /// Activates one particle.
        /// </summary>
        /// <returns>
        /// True if a particle could be activated. False if there are no particles to activate.
        /// </returns> 
        /// This operation preserves the relative order of all particles.
        public virtual bool ActivateParticle()
        {
            if (activeParticleCount >= particleCount)
                return false;

            // set active particle radius W to 1.
            var radii = m_Solver.principalRadii[solverIndices[m_ActiveParticleCount]];
            radii.w = 1;
            m_Solver.principalRadii[solverIndices[m_ActiveParticleCount]] = radii;

            m_ActiveParticleCount++;
            m_Solver.dirtyActiveParticles = true;
            m_Solver.dirtySimplices |= simplexTypes;

            return true;
        }

        /// <summary>
        /// Deactivates one particle.
        /// </summary>
        /// <param name="actorIndex"> Index in the actor arrays of the particle we will deactivate.</param>
        /// <returns>
        /// True if the particle was active. False if the particle was already inactive.
        /// </returns> 
        /// This operation does not preserve the relative order of other particles, because the last active particle will
        /// swap positions with the particle being deactivated.
        public virtual bool DeactivateParticle(int actorIndex)
        {
            if (!IsParticleActive(actorIndex))
                return false;

            m_ActiveParticleCount--;

            // set inactive particle W to zero, this allows renderers to ignore it.
            var radii = m_Solver.principalRadii[solverIndices[actorIndex]];
            radii.w = 0;
            m_Solver.principalRadii[solverIndices[actorIndex]] = radii;

            SwapWithFirstInactiveParticle(actorIndex);
            m_Solver.dirtyActiveParticles = true;
            m_Solver.dirtySimplices |= simplexTypes;

            return true;
        }

        /// <summary>
        /// Returns whether a given particle is active.
        /// </summary>
        /// <param name="actorIndex"> Index in the actor arrays of the particle.</param>
        /// <returns>
        /// True if the particle is active. False if the particle is inactive.
        /// </returns> 
        public virtual bool IsParticleActive(int actorIndex)
        {
            return actorIndex < activeParticleCount;
        }

        /// <summary>
        /// Updates particle phases in the solver at runtime, including or removing the self-collision flag.
        /// </summary>
        public virtual void SetSelfCollisions(bool selfCollisions)
        {
            if (m_Solver != null && Application.isPlaying && isLoaded)
            {
                for (int i = 0; i < particleCount; i++)
                {
                    if (selfCollisions)
                        m_Solver.phases[solverIndices[i]] |= (int)ObiUtils.ParticleFlags.SelfCollide;
                    else
                        m_Solver.phases[solverIndices[i]] &= ~(int)ObiUtils.ParticleFlags.SelfCollide;
                }
            }
        }

        /// <summary>
        /// Updates particle phases in the solver at runtime, including or removing the one-sided flag.
        /// </summary>
        public virtual void SetOneSided(bool oneSided)
        {
            if (m_Solver != null && Application.isPlaying && isLoaded)
            {
                for (int i = 0; i < particleCount; i++)
                {
                    if (oneSided)
                        m_Solver.phases[solverIndices[i]] |= (int)ObiUtils.ParticleFlags.OneSided;
                    else
                        m_Solver.phases[solverIndices[i]] &= ~(int)ObiUtils.ParticleFlags.OneSided;
                }
            }
        }

        /// <summary>
        /// Marks simplices dirty.
        /// </summary>
        public void SetSimplicesDirty()
        {
            if (m_Solver != null)
                m_Solver.dirtySimplices |= simplexTypes;
        }

        /// <summary>
        /// Marks a given constraint type as dirty. 
        /// </summary>
        /// <param name="constraintType"> Type of the constraints that need re-creation.</param>
        /// This will cause the solver to perform a constraint re-creation at the start of the next step. Needed when the constraint data in an actor changes at runtime,
        /// as a result of changing topology (torn cloth or ropes), or changes in internal constraint parameters such as compliance values. This is a relatively expensive operation,
        /// so it's best to amortize as many constraint modification operations as possible in a single step.
        public void SetConstraintsDirty(Oni.ConstraintType constraintType)
        {
            if (m_Solver != null)
                m_Solver.dirtyConstraints |= 1 << (int)constraintType;
        }

        /// <summary>
        /// Marks rendering dirty.
        /// </summary>
        public void SetRenderingDirty(Oni.RenderingSystemType rendererType)
        {
            if (m_Solver != null)
                m_Solver.dirtyRendering |= (int)rendererType;
        }

        /// <summary>  
        /// Returns the data representation of constraints of a given type being simulated by this solver.
        /// </summary>  
        /// <param name="type"> Type of the constraints that will be returned by this method.</param>  
        /// <returns>
        /// The runtime constraints of the type speficied. Most constraints are stored in the blueprint, with a couple notable exceptions: pin and stitch constraints
        /// are always created at runtime, so they're not stored in the blueprint but in the actor itself.
        /// </returns> 
        public IObiConstraints GetConstraintsByType(Oni.ConstraintType type)
        {
            // pin constraints are a special case, because they're not stored in a blueprint. They are created at runtime at stored in the actor itself.
            if (type == Oni.ConstraintType.Pin)
                return m_PinConstraints;
            if (type == Oni.ConstraintType.Pinhole)
                return m_PinholeConstraints;

            if (sharedBlueprint != null)
                return sharedBlueprint.GetConstraintsByType(type);

            return null;
        }



        /// <summary>  
        /// Call when some particle properties have been modified and need updating.
        /// </summary>  
        /// Does not do anything by default. Call when manually modifying particle properties in the solver, should the actor need to do some book keeping.
        /// For softbodies, updates their rest state. 
        public virtual void UpdateParticleProperties()
        {
        }

        /// <summary>  
        /// Returns the index of this particle in the solver arrays.
        /// </summary>  
        /// <param name="actorIndex"> Index of the particle in the actor arrays.</param>  
        /// <returns>
        /// The index of a given particle in the solver arrays.
        /// </returns>
        /// At runtime when the blueprint is loaded, this is the same as calling actor.solverIndices[solverIndex].
        /// If the blueprint is not loaded it will return the same index passed to it: actorIndex.
        /// Note that this function does not perform any range checking.
        public int GetParticleRuntimeIndex(int actorIndex)
        {
            if (isLoaded)
                return solverIndices[actorIndex];
            return actorIndex;
        }


        /// <summary>  
        /// Given a solver particle index, returns the position of that particle in world space. 
        /// </summary>  
        /// <param name="solverIndex"> Index of the particle in the solver arrays.</param>  
        /// <returns>
        /// The position of a given particle in world space.
        /// </returns>
        public Vector3 GetParticlePosition(int solverIndex)
        {
            if (isLoaded)
                return m_Solver.transform.TransformPoint(m_Solver.renderablePositions[solverIndex]);
            return Vector3.zero;
        }

        /// <summary>  
        /// Given a solver particle index, returns the orientation of that particle in world space. 
        /// </summary>  
        /// <param name="solverIndex"> Index of the particle in the solver arrays.</param>  
        /// <returns>
        /// The orientation of a given particle in world space.
        /// </returns>
        public Quaternion GetParticleOrientation(int solverIndex)
        {
            if (isLoaded)
                return m_Solver.transform.rotation * m_Solver.renderableOrientations[solverIndex];
            return Quaternion.identity;
        }


        /// <summary>  
        /// Given a solver particle index, returns the rest position of that particle.
        /// </summary>  
        /// <param name="solverIndex"> Index of the particle in the solver arrays.</param>  
        /// <returns>
        /// The position of a given particle in world space.
        /// </returns>
        public Vector3 GetParticleRestPosition(int solverIndex)
        {
            if (isLoaded)
                return m_Solver.restPositions[solverIndex];
            return Vector3.zero;
        }

        /// <summary>  
        /// Given a solver particle index, returns the rest orientation of that particle.
        /// </summary>  
        /// <param name="solverIndex"> Index of the particle in the solver arrays.</param>  
        /// <returns>
        /// The orientation of a given particle in world space.
        /// </returns>
        public Quaternion GetParticleRestOrientation(int solverIndex)
        {
            if (isLoaded)
                return m_Solver.restOrientations[solverIndex];
            return Quaternion.identity;
        }

        /// <summary>  
        /// Given a solver particle index, returns the anisotropic frame of that particle in world space.
        /// </summary>  
        /// <param name="solverIndex"> Index of the particle in the solver arrays.</param>
        /// <param name="solverIndex"> First basis vector of the frame. Contains particle radius along this axis in the 4th position.</param>
        /// <param name="solverIndex"> Second basis vector of the frame. Contains particle radius along this axis in the 4th position..</param>
        /// <param name="solverIndex"> Third basis vector of the frame. Contains particle radius along this axis in the 4th position.</param>
        public void GetParticleAnisotropy(int solverIndex, ref Vector4 b1, ref Vector4 b2, ref Vector4 b3)
        {
            if (isLoaded && usesAnisotropicParticles)
            {
                b1 = m_Solver.transform.TransformDirection(m_Solver.renderableOrientations[solverIndex] * Vector3.right);
                b2 = m_Solver.transform.TransformDirection(m_Solver.renderableOrientations[solverIndex] * Vector3.up);
                b3 = m_Solver.transform.TransformDirection(m_Solver.renderableOrientations[solverIndex] * Vector3.forward);

                b1[3] = m_Solver.maxScale * m_Solver.renderableRadii[solverIndex][0];
                b2[3] = m_Solver.maxScale * m_Solver.renderableRadii[solverIndex][1];
                b3[3] = m_Solver.maxScale * m_Solver.renderableRadii[solverIndex][2];
            }
            else
            {
                b1[3] = b2[3] = b3[3] = m_Solver.maxScale * m_Solver.renderableRadii[solverIndex][0];
            }
        }

        /// <summary>  
        /// Given a solver particle index, returns the maximum world space radius of that particle, in any axis.
        /// </summary>  
        /// <param name="solverIndex"> Index of the particle in the solver arrays.</param>  
        /// <returns>
        /// The maximum radius of a given particle in world space.
        /// </returns>
        public float GetParticleMaxRadius(int solverIndex)
        {
            if (isLoaded)
                return m_Solver.maxScale * m_Solver.principalRadii[solverIndex][0];
            return 0;
        }


        /// <summary>  
        /// Given a solver particle index, returns the color of that particle.
        /// </summary>  
        /// <param name="solverIndex"> Index of the particle in the solver arrays.</param>  
        /// <returns>
        /// The color of the particle.
        /// </returns>
        public Color GetParticleColor(int solverIndex)
        {
            if (isLoaded)
                return m_Solver.colors[solverIndex];
            return Color.white;
        }

        /// <summary>  
        /// Sets a given category value for all particles in the actor.
        /// </summary>  
        /// <param name="newCategory"> Category value.</param>  
        public void SetFilterCategory(int newCategory)
        {
            newCategory = Mathf.Clamp(newCategory, ObiUtils.MinCategory, ObiUtils.MaxCategory);

            for (int i = 0; i < particleCount; ++i)
            {
                int solverIndex = solverIndices[i];
                var mask = ObiUtils.GetMaskFromFilter(solver.filters[solverIndex]);
                solver.filters[solverIndex] = ObiUtils.MakeFilter(mask, newCategory);
            }
        }

        /// <summary>  
        /// Sets a given mask value for all particles in the actor.
        /// </summary>  
        /// <param name="newMask"> Mask value.</param>  
        public void SetFilterMask(int newMask)
        {
            newMask = Mathf.Clamp(newMask, ObiUtils.CollideWithNothing, ObiUtils.CollideWithEverything);

            for (int i = 0; i < particleCount; ++i)
            {
                int solverIndex = solverIndices[i];
                var category = ObiUtils.GetCategoryFromFilter(solver.filters[solverIndex]);
                solver.filters[solverIndex] = ObiUtils.MakeFilter(newMask, category);
            }
        }

        /// <summary>  
        /// Sets the inverse mass of each particle so that the total actor mass matches the one passed by parameter.
        /// </summary>  
        /// <param name="mass"> The actor's total mass.</param>  
        public void SetMass(float mass)
        {
            if (Application.isPlaying && isLoaded && activeParticleCount > 0)
            {
                float invMass = 1.0f / (mass / activeParticleCount);

                for (int i = 0; i < activeParticleCount; ++i)
                {
                    int solverIndex = solverIndices[i];
                    m_Solver.invMasses[solverIndex] = invMass;
                    m_Solver.invRotationalMasses[solverIndex] = invMass;
                }

                UpdateParticleProperties();
            }
        }

        /// <summary>  
        /// Returns the actor's mass (sum of all particle masses), and the position of its center of mass.
        /// </summary>  
        /// <param name="com"> The actor's center of mass, expressed in solver space.</param>
        /// Particles with infinite mass (invMass = 0) are ignored.
        public float GetMass(out Vector3 com)
        {

            float actorMass = 0;
            com = Vector3.zero;

            if (Application.isPlaying && isLoaded && activeParticleCount > 0)
            {
                Vector4 com4 = Vector4.zero;

                for (int i = 0; i < activeParticleCount; ++i)
                {
                    if (m_Solver.invMasses[solverIndices[i]] > 0)
                    {
                        float mass = 1.0f / m_Solver.invMasses[solverIndices[i]];
                        actorMass += mass;
                        com4 += m_Solver.positions[solverIndices[i]] * mass;
                    }
                }

                com = com4;
                if (actorMass > float.Epsilon)
                    com /= actorMass;
            }

            return actorMass;
        }

        /// <summary>  
        ///Adds an external force to all particles in the actor. 
        /// </summary>  
        /// <param name="force"> Value expressed in solver space.</param>
        /// <param name="forceMode"> Type of "force" applied.</param>
        public void AddForce(Vector3 force, ForceMode forceMode)
        {
            if (force.sqrMagnitude > Mathf.Epsilon)
                bufferedForces.dirty = true;

            switch (forceMode)
            {
                case ForceMode.Force:
                    bufferedForces.force += (Vector4)force;
                    break;
                case ForceMode.Acceleration:
                    bufferedForces.acceleration += (Vector4)force;
                    break;
                case ForceMode.Impulse:
                    bufferedForces.impulse += (Vector4)force;
                    break;
                case ForceMode.VelocityChange:
                    bufferedForces.velChange += (Vector4)force;
                    break;
            }
        }

        /// <summary>  
        /// Adds a torque to the actor.
        /// </summary>  
        /// <param name="force"> Value expressed in solver space.</param>
        /// <param name="forceMode"> Type of "torque" applied.</param>
        public void AddTorque(Vector3 force, ForceMode forceMode)
        {
            if (force.sqrMagnitude > Mathf.Epsilon)
                bufferedForces.dirty = true;

            switch (forceMode)
            {
                case ForceMode.Force:
                        bufferedForces.angularForce += (Vector4)force;
                    break;
                case ForceMode.Acceleration:
                        bufferedForces.angularAcceleration += (Vector4)force;
                    break;
                case ForceMode.Impulse:
                        bufferedForces.angularImpulse += (Vector4)force;
                    break;
                case ForceMode.VelocityChange:
                        bufferedForces.angularVelChange += (Vector4)force;
                    break;
            }
        }

        #region Blueprints

        private void LoadBlueprintParticles(ObiActorBlueprint bp)
        {

            Matrix4x4 l2sTransform = actorLocalToSolverMatrix;
            Quaternion l2sRotation = l2sTransform.rotation;

            for (int i = 0; i < solverIndices.count; i++)
            {
                int k = solverIndices[i];

                if (bp.positions != null && i < bp.positions.Length)
                {
                    m_Solver.endPositions[k] = m_Solver.startPositions[k] = m_Solver.prevPositions[k] = m_Solver.positions[k] = l2sTransform.MultiplyPoint3x4(bp.positions[i]);
                    m_Solver.renderablePositions[k] = l2sTransform.MultiplyPoint3x4(bp.positions[i]);
                }

                if (bp.orientations != null && i < bp.orientations.Length)
                {
                    m_Solver.endOrientations[k] = m_Solver.startOrientations[k] = m_Solver.prevOrientations[k] = m_Solver.orientations[k] = l2sRotation * bp.orientations[i];
                    m_Solver.renderableOrientations[k] = l2sRotation * bp.orientations[i];
                }

                if (bp.restPositions != null && i < bp.restPositions.Length)
                    m_Solver.restPositions[k] = bp.restPositions[i];

                if (bp.restOrientations != null && i < bp.restOrientations.Length)
                    m_Solver.restOrientations[k] = bp.restOrientations[i];

                if (bp.velocities != null && i < bp.velocities.Length)
                    m_Solver.velocities[k] = l2sTransform.MultiplyVector(bp.velocities[i]);

                if (bp.angularVelocities != null && i < bp.angularVelocities.Length)
                    m_Solver.angularVelocities[k] = l2sTransform.MultiplyVector(bp.angularVelocities[i]);

                if (bp.invMasses != null && i < bp.invMasses.Length)
                    m_Solver.invMasses[k] = bp.invMasses[i] / m_MassScale;

                if (bp.invRotationalMasses != null && i < bp.invRotationalMasses.Length)
                    m_Solver.invRotationalMasses[k] = bp.invRotationalMasses[i] / m_MassScale;

                if (bp.principalRadii != null && i < bp.principalRadii.Length)
                {
                    Vector4 radii = bp.principalRadii[i];
                    radii.w = i < sourceBlueprint.activeParticleCount ? 1 : 0;
                    m_Solver.principalRadii[k] = radii;
                }
                else
                {
                    // need inactive emitter particles to zero as their flag.
                    m_Solver.principalRadii[k] = Vector4.zero; 
                }

                if (bp.filters != null && i < bp.filters.Length)
                    m_Solver.filters[k] = bp.filters[i];

                if (bp.colors != null && i < bp.colors.Length)
                    m_Solver.colors[k] = bp.colors[i];

                m_Solver.phases[k] = ObiUtils.MakePhase(groupID, 0);
            }

            m_ActiveParticleCount = sourceBlueprint.activeParticleCount;
            m_Solver.dirtyActiveParticles = true;
            m_Solver.dirtyDeformableTriangles = true;
            m_Solver.dirtyDeformableEdges = true;
            m_Solver.dirtySimplices |= simplexTypes;
            m_Solver.dirtyConstraints |= ~0;

            // Push collision materials:
            UpdateCollisionMaterials();

        }

        private void UnloadBlueprintParticles()
        {
            // Update active particles. 
            m_ActiveParticleCount = 0;
            m_Solver.dirtyActiveParticles = true;
            m_Solver.dirtyDeformableTriangles = true;
            m_Solver.dirtyDeformableEdges = true;
            m_Solver.dirtySimplices |= simplexTypes;
            m_Solver.dirtyConstraints |= ~0;
        }

        /// <summary>  
        /// Resets the position and velocity of all particles, to the values stored in the blueprint.
        /// </summary>  
        /// Note however
        /// that this does not affect constraints, so if you've torn a cloth/rope or resized a rope, calling ResetParticles won't restore
        /// the initial topology of the actor.
        public void ResetParticles()
        {
            if (isLoaded)
            {
                Matrix4x4 l2sTransform = actorLocalToSolverMatrix;
                Quaternion l2sRotation = l2sTransform.rotation;

                for (int i = 0; i < particleCount; ++i)
                {
                    int solverIndex = solverIndices[i];

                    solver.startPositions[solverIndex] = solver.endPositions[solverIndex] = solver.positions[solverIndex] = l2sTransform.MultiplyPoint3x4(sourceBlueprint.positions[i]);
                    solver.velocities[solverIndex] = l2sTransform.MultiplyVector(sourceBlueprint.velocities[i]);

                    if (usesOrientedParticles)
                    {
                        solver.startOrientations[solverIndex] = solver.endOrientations[solverIndex] = solver.orientations[solverIndex] = l2sRotation * sourceBlueprint.orientations[i];
                        solver.angularVelocities[solverIndex] = l2sTransform.MultiplyVector(sourceBlueprint.angularVelocities[i]);
                    }
                }
            }
        }

        #endregion

        #region State


        /// <summary>  
        /// Resets the position and velocity of all particles, to the values stored in the blueprint.
        /// </summary>  
        /// <param name="bp"> The blueprint that we want to fill with current particle data.</param>
        /// Note that this will not resize the blueprint's data arrays, and that it does not perform range checking. For this reason,
        /// you must supply a blueprint large enough to store all particles' data.
        public bool SaveStateToBlueprint(ObiActorBlueprint bp)
        {
            if (bp == null || !m_Loaded)
                return false;

            Matrix4x4 l2sTransform = actorLocalToSolverMatrix.inverse;
            Quaternion l2sRotation = l2sTransform.rotation;

            // blueprint might have been regenerated, and reduced its size:
            for (int i = 0; i < solverIndices.count; i++)
            {
                int k = solverIndices[i];

                if (bp.positions != null && m_Solver.positions != null && k < m_Solver.positions.count && i < bp.positions.Length)
                    bp.positions[i] = l2sTransform.MultiplyPoint3x4(m_Solver.positions[k]);

                if (bp.velocities != null && m_Solver.velocities != null && k < m_Solver.velocities.count && i < bp.velocities.Length)
                    bp.velocities[i] = l2sTransform.MultiplyVector(m_Solver.velocities[k]);
            }

            return true;
        }

        protected void StoreState()
        {
            DestroyImmediate(m_State);
            m_State = Instantiate(sourceBlueprint);
            SaveStateToBlueprint(m_State);
        }

        public void ClearState()
        {
            DestroyImmediate(m_State);
        }

        #endregion

        #region Solver callbacks

        /// <summary>  
        /// Loads this actor's blueprint into the current solver. Automatically called by <see cref="ObiSolver"/>.
        /// </summary> 
        internal virtual void LoadBlueprint()
        {
            var bp = sharedBlueprint;

            // in case we have temporary state, load that instead of the original blueprint.
            if (Application.isPlaying)
            {
                bp = m_State != null ? m_State : sourceBlueprint;
            }

            m_Loaded = true;

            LoadBlueprintParticles(bp);

            OnBlueprintLoaded?.Invoke(this, bp);
        }

        /// <summary>  
        /// Unloads this actor's blueprint from a given solver. Automatically called by <see cref="ObiSolver"/>.
        /// </summary> 
        internal virtual void UnloadBlueprint()
        {
            // instantiate blueprint and store current state in the instance:
            if (Application.isPlaying)
                StoreState();

            m_Loaded = false;

            // unload the blueprint.
            UnloadBlueprintParticles();

            OnBlueprintUnloaded?.Invoke(this, sharedBlueprint);
        }

        public virtual void SimulationStart(float timeToSimulate, float substepTime)
        {
            OnSimulationStart?.Invoke(this, timeToSimulate, substepTime);

            // Apply any buffered forces/torques:
            if (bufferedForces.dirty)
            {
                float mass = GetMass(out Vector3 com);

                if (!float.IsInfinity(mass))
                {
                    Vector4 accum;
                    foreach (var p in solverIndices)
                    {
                        accum = bufferedForces.force / m_Solver.invMasses[p] / mass;
                        accum += bufferedForces.acceleration / m_Solver.invMasses[p];
                        accum += bufferedForces.impulse / m_Solver.invMasses[p] / mass / timeToSimulate;
                        accum += bufferedForces.velChange / m_Solver.invMasses[p] / timeToSimulate;
                        m_Solver.externalForces[p] += accum;

                        accum = bufferedForces.angularForce / m_Solver.invMasses[p] / mass;
                        accum += bufferedForces.angularAcceleration / m_Solver.invMasses[p];
                        accum += bufferedForces.angularImpulse / m_Solver.invMasses[p] / mass / timeToSimulate;
                        accum += bufferedForces.angularVelChange / m_Solver.invMasses[p] / timeToSimulate;
                        m_Solver.externalForces[p] += (Vector4)Vector3.Cross(accum, (Vector3)m_Solver.positions[p] - com);
                    }
                }

                bufferedForces.Clear();
            }
        }

        public virtual void SimulationEnd(float simulatedTime, float substepTime)
        {
            OnSimulationEnd?.Invoke(this, simulatedTime, substepTime);
        }

        public virtual void RequestReadback()
        {
            OnRequestReadback?.Invoke(this);
        }

        public virtual void Interpolate(float simulatedTime, float substepTime)
        {
            // Update particle positions/orientations in the solver:
            if (!Application.isPlaying && isLoaded)
            {
                Matrix4x4 l2sTransform = actorLocalToSolverMatrix;
                Quaternion l2sRotation = l2sTransform.rotation;

                for (int i = 0; i < solverIndices.count; i++)
                {
                    int k = solverIndices[i];

                    if (sourceBlueprint.positions != null && i < sourceBlueprint.positions.Length) 
                    {
                        m_Solver.renderablePositions[k] = m_Solver.positions[k] = m_Solver.startPositions[k] = m_Solver.endPositions[k] = l2sTransform.MultiplyPoint3x4(sourceBlueprint.positions[i]);
                    }

                    if (sourceBlueprint.orientations != null && i < sourceBlueprint.orientations.Length)
                    {
                        m_Solver.renderableOrientations[k] = m_Solver.orientations[k] = m_Solver.startOrientations[k] = m_Solver.endOrientations[k] = l2sRotation * sourceBlueprint.orientations[i];
                    }
                }
            }

            OnInterpolate?.Invoke(this, simulatedTime, substepTime);
        }

        public virtual void OnSolverVisibilityChanged(bool visible)
        {
        }

        #endregion


    }
}

