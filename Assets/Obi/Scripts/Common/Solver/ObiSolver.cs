/**
\mainpage Obi documentation
 
Introduction:
------------- 

Obi is a position-based dynamics framework for unity. It enables the simulation of cloth, ropes and fluid in realtime, complete with two-way
rigidbody interaction.
 
Features:
-------------------

- Particles can be pinned both in local space and to rigidbodies (kinematic or not).
- Realistic wind forces.
- Rigidbodies react to particle dynamics, and particles reach to each other and to rigidbodies too.
- Easy prefab instantiation, particle-based actors can be translated, scaled and rotated.
- Custom editor tools.

*/

using UnityEngine;
using Unity.Profiling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace Obi
{

    /**
     * ObiSolver simulates particles and constraints, provided by a list of ObiActor. Particles belonging to different solvers won't interact with each other in any way.
     */
    [AddComponentMenu("Physics/Obi/Obi Solver", 800)]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [HelpURL("https://obi.virtualmethodstudio.com/manual/7.0/obisolver.html")]
    public sealed class ObiSolver : MonoBehaviour
    {
        static ProfilerMarker m_StateInterpolationPerfMarker = new ProfilerMarker("ApplyStateInterpolation");
        static ProfilerMarker m_UpdateVisibilityPerfMarker = new ProfilerMarker("UpdateVisibility");
        static ProfilerMarker m_GetSolverBoundsPerfMarker = new ProfilerMarker("GetSolverBounds");
        static ProfilerMarker m_TestBoundsPerfMarker = new ProfilerMarker("TestBoundsAgainstCameras");
        static ProfilerMarker m_GetAllCamerasPerfMarker = new ProfilerMarker("GetAllCameras");
        static ProfilerMarker m_PushActiveParticles = new ProfilerMarker("PushActiveParticles");
        static ProfilerMarker m_UpdateColliderWorld = new ProfilerMarker("UpdateColliderWorld");
        static ProfilerMarker m_PushSimplices = new ProfilerMarker("PushSimplices");
        static ProfilerMarker m_PushDeformableEdges = new ProfilerMarker("PushDeformableEdges");
        static ProfilerMarker m_PushDeformableTriangles = new ProfilerMarker("PushDeformableTriangles");

        public enum BackendType
        {
            [InspectorName("Compute (GPU)")]
            Compute,
            [InspectorName("Burst (CPU)")]
            Burst
        }

        public enum Synchronization
        {
            Asynchronous,
            Synchronous,
            SynchronousFixed
        }

        [Serializable]
        public class ParticleInActor
        {
            public ObiActor actor;
            public int indexInActor;

            public ParticleInActor()
            {
                actor = null;
                indexInActor = -1;
            }

            public ParticleInActor(ObiActor actor, int indexInActor)
            {
                this.actor = actor;
                this.indexInActor = indexInActor;
            }
        }

        public class SpatialQuery
        {
            public ObiNativeQueryShapeList shapes;
            public ObiNativeAffineTransformList transforms;
            public ObiNativeQueryResultList results;
            public Action callback;
            public bool synchronous = false;

            public bool isValid => shapes != null && transforms != null && results != null && shapes.count > 0 && transforms.count > 0;
            public bool done => results.noReadbackInFlight;

            public SpatialQuery(ObiNativeQueryShapeList shapes, ObiNativeAffineTransformList transforms, ObiNativeQueryResultList results, Action callback = null, bool synchronous = false)
            {
                this.shapes = shapes;
                this.transforms = transforms;
                this.results = results;
                this.callback = callback;
                this.synchronous = synchronous;
            }

            public void WaitForCompletion()
            {
                results.WaitForReadback();
            }
        }

        public delegate void SolverCallback(ObiSolver solver);
        public delegate void SolverStepCallback(ObiSolver solver, float timeToSimulate, float substepTime);
        public delegate void CollisionCallback(ObiSolver solver, ObiNativeContactList contacts);
        public delegate void SpatialQueryCallback(ObiSolver solver, ObiNativeQueryResultList results);

        public event CollisionCallback OnCollision;
        public event CollisionCallback OnParticleCollision;
        public event SpatialQueryCallback OnSpatialQueryResults;
        public event SolverCallback OnAdvection;

        public event SolverCallback OnInitialize;
        public event SolverCallback OnTeardown;
        public event SolverCallback OnUpdateParameters;
        public event SolverCallback OnParticleCountChanged;

        public event SolverStepCallback OnSimulationStart; /**< Called at the start of physics simulation, before updating active particles, constraints, etc.*/
        public event SolverCallback OnRequestReadback;
        public event SolverStepCallback OnSimulationEnd; /**< Called at the end of physics simulation.*/
        public event SolverStepCallback OnInterpolate; /**< Called every frame after interpolation, right before updating rendering.*/

        [Tooltip("If enabled, will force the solver to keep simulating even when not visible from any camera.")]
        public bool simulateWhenInvisible = true;

        private IObiBackend m_SimulationBackend =
#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
        new BurstBackend();
#else
        new NullBackend();
#endif

        [SerializeField] private BackendType m_Backend = BackendType.Burst;
        private ObiRenderSystemStack m_RenderSystems = new ObiRenderSystemStack(3);

        [Min(1)]
        public int substeps = 4;

        [Min(0)]
        public int maxStepsPerFrame = 1;

        public Synchronization synchronization = Synchronization.Asynchronous;

        public Oni.SolverParameters parameters = new Oni.SolverParameters(Oni.SolverParameters.Interpolation.None,
                                                                          new Vector4(0, -9.81f, 0, 0));

        [Min(32)]
        [SerializeField]
        private uint m_MaxSurfaceChunks = 32768;
        public uint maxSurfaceChunks
        {
            set
            {
                // make sure anytime active particles need to be updated, simplices will be updated too:
                m_MaxSurfaceChunks = value;
                dirtyRendering |= (int)Oni.RenderingSystemType.Fluid;
            }
            get { return m_MaxSurfaceChunks; }
        }

        public uint usedSurfaceChunks
        {
            get {
                var system = GetRenderSystem(Oni.RenderingSystemType.Fluid) as ISurfaceChunkUser;
                if (system == null)
                    return 0;
                return system.usedChunkCount;
            }
        }

        public uint maxQueryResults = 8192;
        public uint maxFoamParticles = 8192;
        public uint maxParticleNeighbors = 128;
        public uint maxParticleContacts = 6;

        public Vector3 gravity = new Vector3(0, -9.81f, 0);
        public Space gravitySpace = Space.Self;

        public Vector3 ambientWind = new Vector3(0, 0, 0);
        public Space windSpace = Space.Self;

        [Min(1)]
        public int foamSubsteps = 1;

        [Tooltip("Foam particles can stretch along the direction of their velocity. This parameter controls the maximum amount of stretch.")]
        [Range(0, 3)]
        public float maxFoamVelocityStretch = 0.3f;

        [Tooltip("Determines how foam particles fade in/out during its lifetime.")]
        [MinMax(0, 1)]
        public Vector2 foamFade = new Vector2(0.05f, 0.8f);

        [Tooltip("Determines the utilization % range in which particles age faster.")]
        [MinMax(0, 1)]
        public Vector2 foamAccelAgingRange = new Vector2(0.5f, 0.8f);

        [Tooltip("Determines the utilization % range in which particles age faster.")]
        [Min(1)]
        public float foamAccelAging = 4;

        [Tooltip("How much does world-space linear inertia affect particles in the solver.")]
        [Range(0, 1)]
        public float worldLinearInertiaScale = 0;           /**< how much does world-space linear inertia affect particles in the solver*/

        [Tooltip("How much does world-space angular inertia affect particles in the solver.")]
        [Range(0, 1)]
        public float worldAngularInertiaScale = 0;          /**< how much does world-space angular inertia affect particles in the solver.*/

        [HideInInspector] [NonSerialized] public List<ObiActor> actors = new List<ObiActor>();
        [HideInInspector] [NonSerialized] private ParticleInActor[] m_ParticleToActor;

        [HideInInspector] [NonSerialized] private Queue<ObiActor> addBuffer = new Queue<ObiActor>(); /**< actors pending insertion into the solver.*/

        private ObiNativeIntList freeList;
        private Stack<int> freeGroupIDs = new Stack<int>();

        [NonSerialized] public ObiNativeIntList deformableTriangles;
        [NonSerialized] public ObiNativeIntList deformableEdges;
        [NonSerialized] public ObiNativeVector2List deformableUVs;

        [NonSerialized] private ObiNativeIntList m_Points;      /**< 0-simplices*/
        [NonSerialized] private ObiNativeIntList m_Edges;       /**< 1-simplices*/
        [NonSerialized] private ObiNativeIntList m_Triangles;   /**< 2-simplices*/
        [NonSerialized] public SimplexCounts m_SimplexCounts;

        [NonSerialized] private IObiJobHandle simulationHandle;
        [NonSerialized] private Synchronization bufferedSynchronization;
        [NonSerialized] private int steps = 0;
        [NonSerialized] private float substepTime = 0;
        [NonSerialized] private float simulatedTime = 0;
        [NonSerialized] private float accumulatedTime = 0;

        public float timeSinceSimulationStart { get; private set; } = 0;

        [HideInInspector] [NonSerialized] public bool dirtyDeformableTriangles = true;
        [HideInInspector] [NonSerialized] public bool dirtyDeformableEdges = true;
        [HideInInspector] [NonSerialized] public Oni.SimplexType dirtySimplices = Oni.SimplexType.All;
        [HideInInspector] [NonSerialized] public int dirtyRendering = 0;
        [HideInInspector] [NonSerialized] public int dirtyConstraints = 0;

        public bool synchronousSpatialQueries = false;

        private bool m_dirtyActiveParticles = true;
        public bool dirtyActiveParticles
        {
            set
            {
                m_dirtyActiveParticles = value;
            }
            get { return m_dirtyActiveParticles; }
        }

        private Bounds m_Bounds = new Bounds();
        private Bounds m_BoundsWS = new Bounds();
        private Plane[] planes = new Plane[6];
        private Camera[] sceneCameras = new Camera[1];

        // constraints:
        [NonSerialized] private IObiConstraints[] m_Constraints = new IObiConstraints[Oni.ConstraintTypeCount];

        // constraint parameters:
        public Oni.ConstraintParameters distanceConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 1);
        public Oni.ConstraintParameters bendingConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters particleCollisionConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 1);
        public Oni.ConstraintParameters particleFrictionConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters collisionConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 1);
        public Oni.ConstraintParameters frictionConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters skinConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 1);
        public Oni.ConstraintParameters volumeConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters shapeMatchingConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters tetherConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters pinConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters pinholeConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters stitchConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters densityConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Parallel, 1);
        public Oni.ConstraintParameters stretchShearConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 1);
        public Oni.ConstraintParameters bendTwistConstraintParameters = new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 1);
        public Oni.ConstraintParameters chainConstraintParameters = new Oni.ConstraintParameters(false, Oni.ConstraintParameters.EvaluationOrder.Sequential, 1);

        // rigidbodies:
        ObiNativeVector4List m_RigidbodyLinearVelocities;
        ObiNativeVector4List m_RigidbodyAngularVelocities;

        // colors:
        [NonSerialized] private ObiNativeColorList m_Colors;

        // cell indices:
        [NonSerialized] private ObiNativeInt4List m_CellCoords;

        // status:
        [NonSerialized] private ObiNativeIntList m_ActiveParticles;
        [NonSerialized] private ObiNativeIntList m_Simplices;

        // positions:
        [NonSerialized] private ObiNativeVector4List m_Positions;
        [NonSerialized] private ObiNativeVector4List m_PrevPositions;
        [NonSerialized] private ObiNativeVector4List m_RestPositions;

        [NonSerialized] private ObiNativeVector4List m_StartPositions;
        [NonSerialized] private ObiNativeVector4List m_EndPositions;
        [NonSerialized] private ObiNativeVector4List m_RenderablePositions;

        // orientations:
        [NonSerialized] private ObiNativeQuaternionList m_Orientations;
        [NonSerialized] private ObiNativeQuaternionList m_PrevOrientations;
        [NonSerialized] private ObiNativeQuaternionList m_RestOrientations;

        [NonSerialized] private ObiNativeQuaternionList m_StartOrientations;
        [NonSerialized] private ObiNativeQuaternionList m_EndOrientations;
        [NonSerialized] private ObiNativeQuaternionList m_RenderableOrientations; /**< renderable particle orientations.*/

        // velocities:
        [NonSerialized] private ObiNativeVector4List m_Velocities;
        [NonSerialized] private ObiNativeVector4List m_AngularVelocities;

        // masses tensors:
        [NonSerialized] private ObiNativeFloatList m_InvMasses;
        [NonSerialized] private ObiNativeFloatList m_InvRotationalMasses;

        // external forces:
        [NonSerialized] private ObiNativeVector4List m_ExternalForces;
        [NonSerialized] private ObiNativeVector4List m_ExternalTorques;
        [NonSerialized] private ObiNativeVector4List m_Wind;

        // deltas:
        [NonSerialized] private ObiNativeVector4List m_PositionDeltas;
        [NonSerialized] private ObiNativeQuaternionList m_OrientationDeltas;
        [NonSerialized] private ObiNativeIntList m_PositionConstraintCounts;
        [NonSerialized] private ObiNativeIntList m_OrientationConstraintCounts;

        // particle collisions:
        [NonSerialized] private ObiNativeIntList m_CollisionMaterials;
        [NonSerialized] private ObiNativeIntList m_Phases;
        [NonSerialized] private ObiNativeIntList m_Filters;

        // particle shape:
        [NonSerialized] private ObiNativeVector4List m_PrincipalRadii;
        [NonSerialized] private ObiNativeVector4List m_RenderableRadii;
        [NonSerialized] private ObiNativeVector4List m_Normals;

        // fluids:
        [NonSerialized] private ObiNativeFloatList m_Life;
        [NonSerialized] private ObiNativeVector4List m_FluidData;
        [NonSerialized] private ObiNativeVector4List m_FluidMaterials; /**< fluidRadius / surfTension / viscosity / vorticity */
        [NonSerialized] private ObiNativeVector4List m_FluidInterface; /**< drag / pressure / buoyancy / miscibility */
        [NonSerialized] private ObiNativeVector4List m_UserData;
        [NonSerialized] private ObiNativeMatrix4x4List m_Anisotropy;

        // foam particles:
        [NonSerialized] private ObiNativeVector4List m_FoamPositions;  /**< xyz = position, w = amount of neighbors*/
        [NonSerialized] private ObiNativeVector4List m_FoamVelocities; /**< xyz = velocity, w = buoyancy*/
        [NonSerialized] private ObiNativeVector4List m_FoamColors;
        [NonSerialized] private ObiNativeVector4List m_FoamAttributes; /**< life, aging rate, size, drag*/
        [NonSerialized] private ObiNativeIntList m_FoamCount;

        // contacts:
        [NonSerialized] private ObiNativeContactList m_ColliderContacts;
        [NonSerialized] private ObiNativeContactList m_ParticleContacts;
        [NonSerialized] private ObiNativeEffectiveMassesList m_ContactEffectiveMasses;
        [NonSerialized] private ObiNativeEffectiveMassesList m_ParticleContactEffectiveMasses;

        // queries:
        [NonSerialized] private ObiNativeQueryShapeList m_BufferedQueryShapes;
        [NonSerialized] private ObiNativeAffineTransformList m_BufferedQueryTransforms;
        [NonSerialized] private ObiNativeQueryShapeList m_QueryShapes;
        [NonSerialized] private ObiNativeAffineTransformList m_QueryTransforms;
        [NonSerialized] private ObiNativeQueryResultList m_QueryResults;

        public ISolverImpl implementation { get; private set; }

        public bool initialized
        {
            get { return implementation != null; }
        }

        public IObiBackend simulationBackend
        {
            get { return m_SimulationBackend; }
        }

        public BackendType backendType
        {
            set
            {
                if (m_Backend != value)
                {
                    m_Backend = value;
                    UpdateBackend();
                }
            }
            get { return m_Backend; }
        }

        public SimplexCounts simplexCounts
        {
            get { return m_SimplexCounts; }
        }

        /// <summary>
        /// Solver bounds expressed in world space. 
        /// </summary>
        public UnityEngine.Bounds bounds
        {
            get { return m_BoundsWS; }
        }

        /// <summary>
        /// Solver bounds expressed in the solver's local space. 
        /// </summary>
        public UnityEngine.Bounds localBounds
        {
            get { return m_Bounds; }
        }

        public bool isVisible { get; private set; } = true;

        public float maxScale { get; private set; } = 1;

        public bool simulationInFlight { get; private set; } = false;

        public int pendingQueryCount => bufferedQueryShapes.count;

        public int allocParticleCount
        {
            get { return particleToActor.Count(s => s != null && s.actor != null); }
        }

        public int activeParticleCount => activeParticles.count;

        public int contactCount
        {
            get { return (backendType == BackendType.Burst || OnCollision != null) ? colliderContacts.count : 0; }
        }

        public int particleContactCount
        {
            get { return (backendType == BackendType.Burst || OnParticleCollision != null) ? particleContacts.count : 0; }
        }

        public ParticleInActor[] particleToActor
        {
            get
            {
                if (m_ParticleToActor == null)
                    m_ParticleToActor = new ParticleInActor[0];

                return m_ParticleToActor;
            }
        }

        public ObiNativeIntList activeParticles
        {
            get
            {
                if (m_ActiveParticles == null)
                    m_ActiveParticles = new ObiNativeIntList();

                return m_ActiveParticles;
            }
        }

        #region Simplices
        public ObiNativeIntList simplices
        {
            get
            {
                if (m_Simplices == null)
                    m_Simplices = new ObiNativeIntList();

                return m_Simplices;
            }
        }

        public ObiNativeIntList points
        {
            get
            {
                if (m_Points == null)
                    m_Points = new ObiNativeIntList(8);

                return m_Points;
            }
        }

        public ObiNativeIntList edges
        {
            get
            {
                if (m_Edges == null)
                    m_Edges = new ObiNativeIntList(8);

                return m_Edges;
            }
        }

        public ObiNativeIntList triangles
        {
            get
            {
                if (m_Triangles == null)
                    m_Triangles = new ObiNativeIntList(8);

                return m_Triangles;
            }
        }

        #endregion

        #region Rigidbodies
        public ObiNativeVector4List rigidbodyLinearDeltas
        {
            get
            {
                if (m_RigidbodyLinearVelocities == null)
                {
                    m_RigidbodyLinearVelocities = new ObiNativeVector4List();
                }
                return m_RigidbodyLinearVelocities;
            }
        }

        public ObiNativeVector4List rigidbodyAngularDeltas
        {
            get
            {
                if (m_RigidbodyAngularVelocities == null)
                {
                    m_RigidbodyAngularVelocities = new ObiNativeVector4List();
                }
                return m_RigidbodyAngularVelocities;
            }
        }
        #endregion

        public ObiNativeColorList colors
        {
            get
            {
                if (m_Colors == null)
                {
                    m_Colors = new ObiNativeColorList();
                }
                return m_Colors;
            }
        }

        public ObiNativeInt4List cellCoords
        {
            get
            {
                if (m_CellCoords == null)
                {
                    m_CellCoords = new ObiNativeInt4List(8, 16, new VInt4(int.MaxValue));
                }
                return m_CellCoords;
            }
        }

        #region Position arrays

        public ObiNativeVector4List positions
        {
            get
            {
                if (m_Positions == null)
                    m_Positions = new ObiNativeVector4List();
                return m_Positions;
            }
        }


        public ObiNativeVector4List prevPositions
        {
            get
            {
                if (m_PrevPositions == null)
                    m_PrevPositions = new ObiNativeVector4List();
                return m_PrevPositions;
            }
        }

        public ObiNativeVector4List restPositions
        {
            get
            {
                if (m_RestPositions == null)
                    m_RestPositions = new ObiNativeVector4List();
                return m_RestPositions;
            }
        }

        public ObiNativeVector4List startPositions
        {
            get
            {
                if (m_StartPositions == null)
                    m_StartPositions = new ObiNativeVector4List();
                return m_StartPositions;
            }
        }

        public ObiNativeVector4List endPositions
        {
            get
            {
                if (m_EndPositions == null)
                    m_EndPositions = new ObiNativeVector4List();
                return m_EndPositions;
            }
        }

        public ObiNativeVector4List renderablePositions
        {
            get
            {
                if (m_RenderablePositions == null)
                    m_RenderablePositions = new ObiNativeVector4List();
                return m_RenderablePositions;
            }
        }

        #endregion

        #region Orientation arrays

        public ObiNativeQuaternionList orientations
        {
            get
            {
                if (m_Orientations == null)
                    m_Orientations = new ObiNativeQuaternionList();
                return m_Orientations;
            }
        }

        public ObiNativeQuaternionList prevOrientations
        {
            get
            {
                if (m_PrevOrientations == null)
                    m_PrevOrientations = new ObiNativeQuaternionList();
                return m_PrevOrientations;
            }
        }

        public ObiNativeQuaternionList restOrientations
        {
            get
            {
                if (m_RestOrientations == null)
                    m_RestOrientations = new ObiNativeQuaternionList();
                return m_RestOrientations;
            }
        }


        public ObiNativeQuaternionList startOrientations
        {
            get
            {
                if (m_StartOrientations == null)
                    m_StartOrientations = new ObiNativeQuaternionList();
                return m_StartOrientations;
            }
        }

        public ObiNativeQuaternionList endOrientations
        {
            get
            {
                if (m_EndOrientations == null)
                    m_EndOrientations = new ObiNativeQuaternionList();
                return m_EndOrientations;
            }
        }


        public ObiNativeQuaternionList renderableOrientations
        {
            get
            {
                if (m_RenderableOrientations == null)
                    m_RenderableOrientations = new ObiNativeQuaternionList();
                return m_RenderableOrientations;
            }
        }

        #endregion

        #region Velocity arrays

        public ObiNativeVector4List velocities
        {
            get
            {
                if (m_Velocities == null)
                    m_Velocities = new ObiNativeVector4List();
                return m_Velocities;
            }
        }

        public ObiNativeVector4List angularVelocities
        {
            get
            {
                if (m_AngularVelocities == null)
                    m_AngularVelocities = new ObiNativeVector4List();
                return m_AngularVelocities;
            }
        }

        #endregion

        #region Mass arrays

        public ObiNativeFloatList invMasses
        {
            get
            {
                if (m_InvMasses == null)
                    m_InvMasses = new ObiNativeFloatList();
                return m_InvMasses;
            }
        }

        public ObiNativeFloatList invRotationalMasses
        {
            get
            {
                if (m_InvRotationalMasses == null)
                    m_InvRotationalMasses = new ObiNativeFloatList();
                return m_InvRotationalMasses;
            }
        }

        #endregion

        #region External forces

        public ObiNativeVector4List externalForces
        {
            get
            {
                if (m_ExternalForces == null)
                    m_ExternalForces = new ObiNativeVector4List();
                return m_ExternalForces;
            }
        }

        public ObiNativeVector4List externalTorques
        {
            get
            {
                if (m_ExternalTorques == null)
                    m_ExternalTorques = new ObiNativeVector4List();
                return m_ExternalTorques;
            }
        }

        public ObiNativeVector4List wind
        {
            get
            {
                if (m_Wind == null)
                    m_Wind = new ObiNativeVector4List();
                return m_Wind;
            }
        }

        #endregion

        #region Deltas

        public ObiNativeVector4List positionDeltas
        {
            get
            {
                if (m_PositionDeltas == null)
                    m_PositionDeltas = new ObiNativeVector4List();
                return m_PositionDeltas;
            }
        }

        public ObiNativeQuaternionList orientationDeltas
        {
            get
            {
                if (m_OrientationDeltas == null)
                    m_OrientationDeltas = new ObiNativeQuaternionList(8, 16, new Quaternion(0, 0, 0, 0));
                return m_OrientationDeltas;
            }
        }

        public ObiNativeIntList positionConstraintCounts
        {
            get
            {
                if (m_PositionConstraintCounts == null)
                    m_PositionConstraintCounts = new ObiNativeIntList();
                return m_PositionConstraintCounts;
            }
        }

        public ObiNativeIntList orientationConstraintCounts
        {
            get
            {
                if (m_OrientationConstraintCounts == null)
                    m_OrientationConstraintCounts = new ObiNativeIntList();
                return m_OrientationConstraintCounts;
            }
        }

        #endregion

        #region Shape and phase

        public ObiNativeIntList collisionMaterials
        {
            get
            {
                if (m_CollisionMaterials == null)
                    m_CollisionMaterials = new ObiNativeIntList();
                return m_CollisionMaterials;
            }
        }

        public ObiNativeIntList phases
        {
            get
            {
                if (m_Phases == null)
                    m_Phases = new ObiNativeIntList();
                return m_Phases;
            }
        }

        public ObiNativeIntList filters
        {
            get
            {
                if (m_Filters == null)
                    m_Filters = new ObiNativeIntList();
                return m_Filters;
            }
        }

        public ObiNativeVector4List renderableRadii
        {
            get
            {
                if (m_RenderableRadii == null)
                    m_RenderableRadii = new ObiNativeVector4List();
                return m_RenderableRadii;
            }
        }

        public ObiNativeVector4List principalRadii
        {
            get
            {
                if (m_PrincipalRadii == null)
                    m_PrincipalRadii = new ObiNativeVector4List();
                return m_PrincipalRadii;
            }
        }

        public ObiNativeVector4List normals
        {
            get
            {
                if (m_Normals == null)
                    m_Normals = new ObiNativeVector4List();
                return m_Normals;
            }
        }

        #endregion

        #region Fluid properties

        public ObiNativeFloatList life
        {
            get
            {
                if (m_Life == null)
                    m_Life = new ObiNativeFloatList();
                return m_Life;
            }
        }

        public ObiNativeVector4List fluidData
        {
            get
            {
                if (m_FluidData == null)
                    m_FluidData = new ObiNativeVector4List();
                return m_FluidData;
            }
        }

        public ObiNativeVector4List userData
        {
            get
            {
                if (m_UserData == null)
                    m_UserData = new ObiNativeVector4List();
                return m_UserData;
            }
        }

        public ObiNativeVector4List fluidInterface
        {
            get
            {
                if (m_FluidInterface == null)
                    m_FluidInterface = new ObiNativeVector4List();
                return m_FluidInterface;
            }
        }

        public ObiNativeVector4List fluidMaterials
        {
            get
            {
                if (m_FluidMaterials == null)
                    m_FluidMaterials = new ObiNativeVector4List();
                return m_FluidMaterials;
            }
        }

        public ObiNativeMatrix4x4List anisotropies
        {
            get
            {
                if (m_Anisotropy == null)
                    m_Anisotropy = new ObiNativeMatrix4x4List();
                return m_Anisotropy;
            }
        }

        public ObiNativeVector4List foamPositions
        {
            get
            {
                if (m_FoamPositions == null)
                    m_FoamPositions = new ObiNativeVector4List();
                return m_FoamPositions;
            }
        }

        public ObiNativeVector4List foamVelocities
        {
            get
            {
                if (m_FoamVelocities == null)
                    m_FoamVelocities = new ObiNativeVector4List();
                return m_FoamVelocities;
            }
        }

        public ObiNativeVector4List foamColors
        {
            get
            {
                if (m_FoamColors == null)
                    m_FoamColors = new ObiNativeVector4List();
                return m_FoamColors;
            }
        }

        public ObiNativeVector4List foamAttributes
        {
            get
            {
                if (m_FoamAttributes == null)
                    m_FoamAttributes = new ObiNativeVector4List();
                return m_FoamAttributes;
            }
        }

        public ObiNativeIntList foamCount
        {
            get
            {
                if (m_FoamCount == null)
                {
                    m_FoamCount = new ObiNativeIntList();
                    m_FoamCount.ResizeUninitialized(9);

                    // post-emission particle dispatch (4 floats), post-update particle  dispatch (4 floats),
                    // plus 1 extra float for storing currently alive particles while updating/killing.
                    m_FoamCount.CopyFrom(new int[] { 0, 1, 1, 0, 0, 1, 1, 0, 0 }, 0, 0, 9);
                }
                return m_FoamCount;
            }
        }

        #endregion

        #region Contacts

        public ObiNativeContactList colliderContacts
        {
            get
            {
                if (m_ColliderContacts == null)
                    m_ColliderContacts = new ObiNativeContactList();
                return m_ColliderContacts;
            }
        }

        public ObiNativeContactList particleContacts
        {
            get
            {
                if (m_ParticleContacts == null)
                    m_ParticleContacts = new ObiNativeContactList();
                return m_ParticleContacts;
            }
        }

        public ObiNativeEffectiveMassesList contactEffectiveMasses
        {
            get
            {
                if (m_ContactEffectiveMasses == null)
                    m_ContactEffectiveMasses = new ObiNativeEffectiveMassesList();
                return m_ContactEffectiveMasses;
            }
        }

        public ObiNativeEffectiveMassesList particleContactEffectiveMasses
        {
            get
            {
                if (m_ParticleContactEffectiveMasses == null)
                    m_ParticleContactEffectiveMasses = new ObiNativeEffectiveMassesList();
                return m_ParticleContactEffectiveMasses;
            }
        }

        #endregion

        #region Queries

        private ObiNativeQueryShapeList bufferedQueryShapes
        {
            get
            {
                if (m_BufferedQueryShapes == null)
                    m_BufferedQueryShapes = new ObiNativeQueryShapeList();
                return m_BufferedQueryShapes;
            }
        }

        private ObiNativeAffineTransformList bufferedQueryTransforms
        {
            get
            {
                if (m_BufferedQueryTransforms == null)
                    m_BufferedQueryTransforms = new ObiNativeAffineTransformList(8);
                return m_BufferedQueryTransforms;
            }
        }

        private ObiNativeQueryShapeList queryShapes
        {
            get
            {
                if (m_QueryShapes == null)
                    m_QueryShapes = new ObiNativeQueryShapeList();
                return m_QueryShapes;
            }
        }

        private ObiNativeAffineTransformList queryTransforms
        {
            get
            {
                if (m_QueryTransforms == null)
                    m_QueryTransforms = new ObiNativeAffineTransformList(8);
                return m_QueryTransforms;
            }
        }

        public ObiNativeQueryResultList queryResults
        {
            get
            {
                if (m_QueryResults == null)
                    m_QueryResults = new ObiNativeQueryResultList();
                return m_QueryResults;
            }
        }

        #endregion

        public void OnEnable()
        {
            bufferedSynchronization = synchronization;
            accumulatedTime = 0;
        }

        private void FixedUpdate()
        {
            // first fixed update this frame:
            if (steps++ == 0)
            {
                // Signal the start of a frame, so we know the world needs to be updated this frame.
                ObiColliderWorld.GetInstance().FrameStart();

                // Wait for the previous frame's simulation to end and GPU data to be available.
                if (bufferedSynchronization == Synchronization.Asynchronous)
                    CompleteSimulation();
            }

            if (bufferedSynchronization == Synchronization.SynchronousFixed)
            {
                // Update collider world, making sure it will also update after FixedUpdate() for solvers not using fixed sync.
                ObiColliderWorld.GetInstance().UpdateWorld(Time.fixedDeltaTime);
                ObiColliderWorld.GetInstance().FrameStart();

                // kick off this step's simulation, and immediately wait for it to complete:
                StartSimulation(Time.fixedDeltaTime, 1);
                CompleteSimulation();
            }
           
        }

        private void Update()
        {
            // Make sure ObiColliderWorld updates after all solvers have called CompleteSimulation() on their FixedUpdate.
            // This way we can be sure no physics updates are in flight.
            if (steps > 0 && bufferedSynchronization != Synchronization.SynchronousFixed)
            {
                ObiColliderWorld.GetInstance().UpdateWorld(Time.fixedDeltaTime * steps);
            }
        }

        private void LateUpdate()
        {
            var scale = transform.lossyScale;
            maxScale = Mathf.Max(Mathf.Max(scale.x, scale.y), scale.z);

            // Accumulate amount of time to simulate (duration of the frame - time already simulated)
            if (Application.isPlaying)
            {
                accumulatedTime += Time.deltaTime - Time.fixedDeltaTime * steps;

                // clamp to a single timestep, in case the simulation is dropping time.
                accumulatedTime = Mathf.Clamp(accumulatedTime, 0, Time.fixedDeltaTime);
            }
            else
            {
                // if in editor, we don't accumulate any simulation time
                // and just update solver bounds before rendering/simulation.
                accumulatedTime = 0;
                UpdateBounds();
            }

            if (bufferedSynchronization == Synchronization.Asynchronous ||
                bufferedSynchronization == Synchronization.SynchronousFixed)
                Render(accumulatedTime);

            // if in play mode, kick off this frame's simulation.
            if (Application.isPlaying && bufferedSynchronization != Synchronization.SynchronousFixed)
                StartSimulation(Time.fixedDeltaTime, steps);

            if (bufferedSynchronization == Synchronization.Synchronous)
            {
                // if the simulation has been stepped this frame,
                // sychronously wait for completion before rendering.
                if (steps > 0)
                    CompleteSimulation();

                Render(accumulatedTime);
            }

            // Reset step counter to zero, now that
            // simulation tasks for this frame have been dispatched.
            steps = 0;
        }

        private void OnApplicationQuit()
        {
            // Make sure solvers finish their simulation before Unity automatically destroys collider world
            // when closing app or exiting play mode.
            OnDestroy();
        }

        private void OnDestroy()
        {
            // Remove all actors from the solver. This will trigger Teardown() when the last actor is removed.
            while (actors.Count > 0)
                RemoveActor(actors[actors.Count - 1]);
        }

        private void CreateBackend()
        {
            switch (m_Backend)
            {

#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
                case BackendType.Burst: m_SimulationBackend = new BurstBackend(); break;
#endif
                case BackendType.Compute:

                    if (SystemInfo.supportsComputeShaders)
                        m_SimulationBackend = new ComputeBackend();
                    else
                        goto default;
                    break;

                default:
                    Debug.LogWarning("The Burst backend depends on the following packages: Mathematics, Collections, Jobs and Burst. Please install the required dependencies. Simulation will fall back to the compute backend, if possible.");
                    if (SystemInfo.supportsComputeShaders)
                        m_SimulationBackend = new ComputeBackend();
                    else
                    {
                        Debug.LogError("This platform doesn't support compute shaders. Please switch to the Burst backend.");
                        m_SimulationBackend = new NullBackend();
                    }
                    break;
            }
        }

        public void Initialize()
        {
            if (!initialized)
            {
                CreateBackend();

                substepTime = Time.fixedDeltaTime / substeps;

                // Set up local actor and particle buffers:
                actors = new List<ObiActor>();
                freeList = new ObiNativeIntList();
                m_ParticleToActor = new ParticleInActor[0];

                deformableUVs = new ObiNativeVector2List();
                deformableTriangles = new ObiNativeIntList();
                deformableEdges = new ObiNativeIntList();

                // Create constraints:
                m_Constraints[(int)Oni.ConstraintType.Distance] = new ObiDistanceConstraintsData();
                m_Constraints[(int)Oni.ConstraintType.Bending] = new ObiBendConstraintsData();
                m_Constraints[(int)Oni.ConstraintType.Aerodynamics] = new ObiAerodynamicConstraintsData();
                m_Constraints[(int)Oni.ConstraintType.StretchShear] = new ObiStretchShearConstraintsData();
                m_Constraints[(int)Oni.ConstraintType.BendTwist] = new ObiBendTwistConstraintsData();
                m_Constraints[(int)Oni.ConstraintType.Chain] = new ObiChainConstraintsData();
                m_Constraints[(int)Oni.ConstraintType.ShapeMatching] = new ObiShapeMatchingConstraintsData();
                m_Constraints[(int)Oni.ConstraintType.Volume] = new ObiVolumeConstraintsData();
                m_Constraints[(int)Oni.ConstraintType.Tether] = new ObiTetherConstraintsData();
                m_Constraints[(int)Oni.ConstraintType.Skin] = new ObiSkinConstraintsData();
                m_Constraints[(int)Oni.ConstraintType.Pin] = new ObiPinConstraintsData();
                m_Constraints[(int)Oni.ConstraintType.Pinhole] = new ObiPinholeConstraintsData();

                // Create the solver:
                implementation = m_SimulationBackend.CreateSolver(this, 0);

                // Set data arrays:
                implementation.ParticleCountChanged(this);
                implementation.SetRigidbodyArrays(this);
                OnParticleCountChanged?.Invoke(this);

                // Initialize moving transform:
                InitializeTransformFrame();

                // Initial collider world update:
                ObiColliderWorld.GetInstance().FrameStart();
                ObiColliderWorld.GetInstance().UpdateWorld(0);

                OnInitialize?.Invoke(this);

                // Set initial parameter values:
                PushSolverParameters();

#if UNITY_EDITOR
                ObiActorEditorSelectionHandler.SolverInitialized(this);
#endif
            }
        }

        public void Teardown()
        {
            if (initialized)
            {
                CompleteSimulation();

                // Clear all constraints:
                PushConstraints();

                // Destroy the solver:
                m_SimulationBackend.DestroySolver(implementation);
                implementation = null;

                // Free particle / rigidbody memory:
                FreeParticleArrays();
                FreeRigidbodyArrays();

                freeList.Dispose();

                // Reset bounds:
                m_Bounds = new Bounds();

                OnTeardown?.Invoke(this);

#if UNITY_EDITOR
                ObiActorEditorSelectionHandler.SolverTeardown(this);
#endif
            }
        }

        public void UpdateBackend()
        {
            // remove all actors, this will trigger a teardown:
            List<ObiActor> temp = new List<ObiActor>(actors);
            foreach (ObiActor actor in temp)
                actor.RemoveFromSolver();

            // re-add all actors.
            // Solver will be re-initialized on adding the first one.
            foreach (ObiActor actor in temp)
                actor.AddToSolver();
        }

        private void FreeRigidbodyArrays()
        {
            rigidbodyLinearDeltas.Dispose();
            rigidbodyAngularDeltas.Dispose();

            m_RigidbodyLinearVelocities = null;
            m_RigidbodyAngularVelocities = null;
        }

        public void EnsureRigidbodyArraysCapacity(int count)
        {
            if (initialized && (count > rigidbodyLinearDeltas.count || !rigidbodyLinearDeltas.isCreated))
            {
                rigidbodyLinearDeltas.ResizeInitialized(count);
                rigidbodyAngularDeltas.ResizeInitialized(count);

                implementation.SetRigidbodyArrays(this);
            }
        }

        private void FreeParticleArrays()
        {
            activeParticles.Dispose();
            simplices.Dispose();
            points.Dispose();
            edges.Dispose();
            triangles.Dispose();

            colors.Dispose();
            cellCoords.Dispose();
            startPositions.Dispose();
            endPositions.Dispose();
            startOrientations.Dispose();
            endOrientations.Dispose();
            positions.Dispose();
            prevPositions.Dispose();
            restPositions.Dispose();
            velocities.Dispose();
            orientations.Dispose();
            prevOrientations.Dispose();
            restOrientations.Dispose();
            angularVelocities.Dispose();
            invMasses.Dispose();
            invRotationalMasses.Dispose();
            principalRadii.Dispose();
            collisionMaterials.Dispose();
            phases.Dispose();
            filters.Dispose();
            renderablePositions.Dispose();
            renderableOrientations.Dispose();
            renderableRadii.Dispose();
            fluidInterface.Dispose();
            fluidMaterials.Dispose();
            foamPositions.Dispose();
            foamVelocities.Dispose();
            foamColors.Dispose();
            foamAttributes.Dispose();
            foamCount.Dispose();
            anisotropies.Dispose();
            life.Dispose();
            fluidData.Dispose();
            userData.Dispose();
            externalForces.Dispose();
            externalTorques.Dispose();
            wind.Dispose();
            positionDeltas.Dispose();
            orientationDeltas.Dispose();
            positionConstraintCounts.Dispose();
            orientationConstraintCounts.Dispose();
            normals.Dispose();
            colliderContacts.Dispose();
            particleContacts.Dispose();
            contactEffectiveMasses.Dispose();
            particleContactEffectiveMasses.Dispose();

            bufferedQueryShapes.Dispose();
            bufferedQueryTransforms.Dispose();
            queryShapes.Dispose();
            queryTransforms.Dispose();
            queryResults.Dispose();

            deformableUVs.Dispose();
            deformableTriangles.Dispose();
            deformableEdges.Dispose();

            m_ActiveParticles = null;
            m_Simplices = null;
            m_Points = null;
            m_Edges = null;
            m_Triangles = null;

            m_Colors = null;
            m_CellCoords = null;
            m_Positions = null;
            m_RestPositions = null;
            m_PrevPositions = null;
            m_StartPositions = null;
            m_EndPositions = null;
            m_RenderablePositions = null;
            m_Orientations = null;
            m_RestOrientations = null;
            m_PrevOrientations = null;
            m_StartOrientations = null;
            m_EndOrientations = null;
            m_RenderableOrientations = null;
            m_Velocities = null;
            m_AngularVelocities = null;
            m_InvMasses = null;
            m_InvRotationalMasses = null;
            m_ExternalForces = null;
            m_ExternalTorques = null;
            m_Wind = null;
            m_PositionDeltas = null;
            m_OrientationDeltas = null;
            m_PositionConstraintCounts = null;
            m_OrientationConstraintCounts = null;
            m_CollisionMaterials = null;
            m_Phases = null;
            m_Filters = null;
            m_RenderableRadii = null;
            m_PrincipalRadii = null;
            m_Normals = null;
            m_Life = null;
            m_FluidData = null;
            m_UserData = null;
            m_FluidInterface = null;
            m_FluidMaterials = null;
            m_FoamPositions = null;
            m_FoamVelocities = null;
            m_FoamColors = null;
            m_FoamAttributes = null;
            m_FoamCount = null;
            m_Anisotropy = null;
            m_ColliderContacts = null;
            m_ParticleContacts = null;
            m_ContactEffectiveMasses = null;
            m_ParticleContactEffectiveMasses = null;

            m_BufferedQueryShapes = null;
            m_BufferedQueryTransforms = null;
            m_QueryShapes = null;
            m_QueryTransforms = null;
            m_QueryResults = null;

            deformableUVs = null;
            deformableTriangles = null;
            deformableEdges = null;
        }

        private void EnsureParticleArraysCapacity(int count)
        {
            // only resize if the count is larger than the current amount of particles:
            if (count >= positions.count)
            {
                colors.ResizeInitialized(count, Color.white);
                startPositions.ResizeInitialized(count);
                endPositions.ResizeInitialized(count);
                positions.ResizeInitialized(count);
                prevPositions.ResizeInitialized(count);
                restPositions.ResizeInitialized(count);
                startOrientations.ResizeInitialized(count, Quaternion.identity);
                endOrientations.ResizeInitialized(count, Quaternion.identity);
                orientations.ResizeInitialized(count, Quaternion.identity);
                prevOrientations.ResizeInitialized(count, Quaternion.identity);
                restOrientations.ResizeInitialized(count, Quaternion.identity);
                renderablePositions.ResizeInitialized(count);
                renderableOrientations.ResizeInitialized(count, Quaternion.identity);
                velocities.ResizeInitialized(count);
                angularVelocities.ResizeInitialized(count);
                invMasses.ResizeInitialized(count);
                invRotationalMasses.ResizeInitialized(count);
                principalRadii.ResizeInitialized(count);
                collisionMaterials.ResizeInitialized(count);
                phases.ResizeInitialized(count);
                filters.ResizeInitialized(count);
                renderableRadii.ResizeInitialized(count);
                fluidInterface.ResizeInitialized(count);
                fluidMaterials.ResizeInitialized(count);
                anisotropies.ResizeInitialized(count);
                life.ResizeInitialized(count);
                fluidData.ResizeInitialized(count);
                userData.ResizeInitialized(count);
                externalForces.ResizeInitialized(count);
                externalTorques.ResizeInitialized(count);
                wind.ResizeInitialized(count);
                positionDeltas.ResizeInitialized(count);
                orientationDeltas.ResizeInitialized(count, new Quaternion(0, 0, 0, 0));
                positionConstraintCounts.ResizeInitialized(count);
                orientationConstraintCounts.ResizeInitialized(count);
                normals.ResizeInitialized(count);
            }

            if (count >= m_ParticleToActor.Length)
            {
                Array.Resize(ref m_ParticleToActor, count * 2);
            }
        }

        private void UpdateFoamParticleCapacity()
        {
            if (maxFoamParticles != foamPositions.count)
            {
                foamPositions.ResizeUninitialized((int)maxFoamParticles);
                foamVelocities.ResizeUninitialized((int)maxFoamParticles);
                foamColors.ResizeUninitialized((int)maxFoamParticles);
                foamAttributes.ResizeUninitialized((int)maxFoamParticles);
                foamCount[3] = Mathf.Min(foamCount[3], (int)maxFoamParticles);

                implementation.MaxFoamParticleCountChanged(this);
            }
        }

        private void AllocateParticles(ObiNativeIntList particleIndices)
        {

            // If attempting to allocate more particles than we have:
            if (particleIndices.count > freeList.count)
            {
                int grow = particleIndices.count - freeList.count;

                // append new free indices:
                for (int i = 0; i < grow; ++i)
                    freeList.Add(positions.count + i);

                // grow particle arrays:
                EnsureParticleArraysCapacity(positions.count + particleIndices.count);
            }

            // determine first particle in the free list to use:
            int first = freeList.count - particleIndices.count;

            // copy free indices to the input array:
            particleIndices.CopyFrom(freeList, first, 0, particleIndices.count);

            // shorten the free list:
            freeList.ResizeUninitialized(first);

        }

        private void FreeParticles(ObiNativeIntList particleIndices)
        {
            freeList.AddRange(particleIndices);
        }

        private void CollisionCallbacks()
        {
            if (OnCollision != null)
            {
                colliderContacts.WaitForReadback();
                OnCollision.Invoke(this, colliderContacts);
            }
            if (OnParticleCollision != null)
            {
                particleContacts.WaitForReadback();
                OnParticleCollision.Invoke(this, particleContacts);
            }
            if (OnAdvection != null)
            {
                foamPositions.WaitForReadback();
                foamVelocities.WaitForReadback();
                foamAttributes.WaitForReadback();
                foamColors.WaitForReadback();
                foamCount.WaitForReadback();

                OnAdvection.Invoke(this);


                foamPositions.Upload();
                foamVelocities.Upload();
                foamAttributes.Upload();
                foamColors.Upload();
                foamCount.Upload();
            }
        }

        public void StartSimulation(float stepDelta, int simulationSteps)
        {
            if (simulationSteps > 0)
            {
                // Complete previous simulation call, if any:
                CompleteSimulation();

                simulatedTime = stepDelta * simulationSteps; // physics time that has been simulated by Unity this frame. Might be more than the time we actually simulate, due to maxStepsPerFrame.
                substepTime = stepDelta / substeps;          // duration of a substep.

                // only update buffered synchronization before starting a new step.
                bufferedSynchronization = synchronization;

                // AddActor() calls are buffered, new actors should be inserted as this particular point in time:
                while (addBuffer.TryDequeue(out ObiActor actor))
                    InsertBufferedActor(actor);

                if (initialized && maxStepsPerFrame > 0)
                {
                    simulationInFlight = true;

                    int frameSubsteps = Mathf.Min(maxStepsPerFrame, simulationSteps) * substeps; // amount of substeps *actually* simulated this frame.
                    float timeToSimulate = frameSubsteps * substepTime; // amount of time we need to simulate, might be less than simulatedTime.

                    UpdateFoamParticleCapacity();

                    // Update collision materials/rigidbodies after adding new actors to make sure collision materials are up to date.
                    // Also call it before SimulationStart, so that constraints referencing rigidbodies (such as Pin constraints in attachments)
                    // use handle data that's up to date. 
                    using (m_UpdateColliderWorld.Auto())
                    {
                        ObiColliderWorld.GetInstance().UpdateCollisionMaterials();
                        EnsureRigidbodyArraysCapacity(ObiColliderWorld.GetInstance().rigidbodyHandles.Count);
                    }

                    // We need SimulationStart to be called before PushConstraints for updating pin constraints.
                    OnSimulationStart?.Invoke(this, timeToSimulate, substepTime);
                    foreach (ObiActor actor in actors)
                        actor.SimulationStart(timeToSimulate, substepTime);

                    // Update the active particles array:
                    PushActiveParticles();

                    // Update the simplices array:
                    PushSimplices();

                    // Update deformable triangles/edges arrays:
                    PushDeformableTriangles();
                    PushDeformableEdges();

                    // Update constraint batches:
                    PushConstraints();

                    // Update parameters:
                    parameters.gravity = gravitySpace == Space.World ? transform.InverseTransformVector(gravity) : gravity;
                    parameters.ambientWind = windSpace == Space.World ? transform.InverseTransformVector(ambientWind) : ambientWind;
                    implementation.SetParameters(parameters);

                    // Notify render systems that a step has started:
                    m_RenderSystems.Step();

                    // CPU -> GPU data transfer
                    implementation.PushData();

                    // Update inertial reference frame:
                    simulationHandle = UpdateTransformFrame(simulatedTime);

                    // Calculate bounds:
                    simulationHandle = implementation.UpdateBounds(simulationHandle, simulatedTime);

                    // Perform collision detection:
                    if (simulateWhenInvisible || isVisible)
                    {
                        simulationHandle = implementation.CollisionDetection(simulationHandle, simulatedTime);
                        simulationHandle?.Complete(); // complete here, since several jobs need fluidParticles.Length. TODO: use deferred jobs.
                    }

                    // Perform queued queries. This ensures queries "see" the same state as collision callbacks, and ensures no
                    // data races (queries performed while the simulation is running).
                    FlushSpatialQueries();

                    // Divide each step into multiple substeps:
                    float timeLeft = simulatedTime;         
                    for (int i = 0; i < frameSubsteps; ++i)
                    {
                        // Only update the solver if it is visible, or if we must simulate even when invisible.
                        if ((simulateWhenInvisible || isVisible) && initialized)
                        {
                            simulationHandle = implementation.Substep(simulationHandle, stepDelta, substepTime, simulationSteps, timeLeft);
                        }
                        timeLeft -= substepTime;
                    }

                    timeSinceSimulationStart += timeToSimulate;

                    // Request GPU data to be brought back to the CPU.
                    RequestReadback();
                }
            }
        }

        private void FlushSpatialQueries()
        {
            while (bufferedQueryShapes.count > 0)
            {
                // copy buffered queries to the lists used for performing the queries:
                queryShapes.ResizeUninitialized(bufferedQueryShapes.count);
                queryTransforms.ResizeUninitialized(bufferedQueryTransforms.count);

                queryShapes.CopyFrom(bufferedQueryShapes);
                queryTransforms.CopyFrom(bufferedQueryTransforms);

                bufferedQueryShapes.Clear();
                bufferedQueryTransforms.Clear();

                implementation.SpatialQuery(queryShapes, queryTransforms, queryResults);
                queryResults.Readback();

                if (synchronousSpatialQueries)
                {
                    // Wait for query results right now and trigger query results event:
                    queryResults.WaitForReadback();
                    OnSpatialQueryResults?.Invoke(this, queryResults);
                }
            }
        }

        public void CompleteSimulation()
        {
            // if the solver is not yet initialized or there's no previous call to SimulationStart, return.
            if (!initialized || !simulationInFlight)
                return;

            // Make sure previous simulation call has completed.
            simulationHandle?.Complete();

            // Update physics state for rendering, and wait for GPU readbacks to finish.
            implementation.FinishSimulation();

            // Trigger simulation end callback, after GPU readbacks are completed but before query/collision callbacks.
            OnSimulationEnd?.Invoke(this, simulatedTime, substepTime);
            foreach (ObiActor actor in actors)
                actor.SimulationEnd(simulatedTime, substepTime);

            // Update rigidbody velocities with the simulation results:
            ObiColliderWorld.GetInstance().UpdateRigidbodyVelocities(this);

            // Trigger spatial query results:
            if (!synchronousSpatialQueries)
            {
                queryResults.WaitForReadback();
                OnSpatialQueryResults?.Invoke(this, queryResults);
            }

            // Trigger collision callbacks now that GPU data (including rigidbody velocity deltas) is available.
            CollisionCallbacks();

            simulationInFlight = false;
        }

        /// <summary>
        /// Performs physics state interpolation and updates rendering.
        /// </summary>
        /// <param name="unsimulatedTime"> Remaining time that could not be simulated during this frame (in seconds). This is used to interpolate physics state. </param>  
        public void Render(float unsimulatedTime)
        {
            if (!initialized)
                return;

            // Only perform interpolation if the solver is visible, or if we must simulate even when invisible.
            if (simulateWhenInvisible || isVisible)
            {
                using (m_StateInterpolationPerfMarker.Auto())
                {
                    // interpolate physics state:
                    simulationHandle = implementation.ApplyInterpolation(simulationHandle, startPositions, startOrientations, Time.fixedDeltaTime, unsimulatedTime);
                }
            }

            simulationHandle?.Complete();

            // test bounds against all cameras to update visibility.
            UpdateVisibility();

            OnInterpolate?.Invoke(this, simulatedTime, substepTime);

            foreach (ObiActor actor in actors)
                actor.Interpolate(simulatedTime, substepTime);

            if (!Application.isPlaying)
            {
                // in-editor, actors update their positions/orientations in Interpolate when transformed,
                // so we must copy them to the GPU:
                positions.Upload();
                orientations.Upload();
                renderablePositions.Upload();
                renderableOrientations.Upload();
            }

            // Update render systems if dirty:
            if (dirtyRendering != 0)
            {
                m_RenderSystems.Setup(dirtyRendering);
                dirtyRendering = 0;
            }

            // Only render if visible:
            if (simulateWhenInvisible || isVisible)
                m_RenderSystems.Render();
        }

        private void UpdateBounds()
        {
            // While in-editor, update active particles and simplices so that
            // solver bounds are correct.
            if (initialized)
            {
                PushActiveParticles();
                PushSimplices();

                simulationHandle = UpdateTransformFrame(0);
                simulationHandle = implementation.UpdateBounds(simulationHandle, 0);
                simulationHandle?.Complete();
            }
        }

        private void RequestReadback()
        {
            if (!initialized)
                return;

            OnRequestReadback?.Invoke(this);
            foreach (ObiActor actor in actors)
                actor.RequestReadback();

            implementation.RequestReadback();

            // We must read the entire contacts buffer instead of the amount of contacts the CPU
            // has from last frame, since we need to get both the counter value and the contacts data on the same
            // frame. Alternative would be to read back amount of contacts from last frame, but that
            // means we could find invalid/uninitialized contacts if the amount of contacts decreases from one frame to the next.
            if (OnCollision != null)
                colliderContacts.Readback();
            if (OnParticleCollision != null)
                particleContacts.Readback();

            if (OnAdvection != null)
            {
                foamPositions.Readback();
                foamVelocities.Readback();
                foamAttributes.Readback();
                foamColors.Readback();
                foamCount.Readback();
            }
        }

        /// <summary>
        /// Adds an actor to the solver.
        /// </summary> 
        /// Attemps to add the actor to this solver returning whether this was successful or not. In case the actor was already added, or had no reference to a blueprint, this operation will return false.
        /// If this was the first actor added to the solver, will attempt to initialize the solver.
        /// While in play mode, if the actor is sucessfully added to the solver, will also call actor.LoadBlueprint().
        /// <param name="actor"> An actor.</param>  
        /// <returns>
        /// Whether the actor was sucessfully added.
        /// </returns> 
        public bool AddActor(ObiActor actor)
        {
            if (actor == null || actors == null || actor.sourceBlueprint == null || actor.sourceBlueprint.empty || actors.Contains(actor) || addBuffer.Contains(actor))
                return false;

            // in-editor, we insert actors right away since the simulation is not running,
            // yet we need to perform rendering.

            if (!Application.isPlaying)
                InsertBufferedActor(actor);
            else
                addBuffer.Enqueue(actor);
            return true;
        }

        /// <summary>  
        /// Attempts to remove an actor from this solver, and returns  whether this was sucessful or not. 
        /// </summary>
        /// Will only reurn true if the actor had been previously added successfully to this solver. 
        /// If the actor is sucessfully removed from the solver, will also call actor.UnloadBlueprint(). Once the last actor is removed from the solver,
        /// this method will attempt to tear down the solver.
        /// <param name="actor"> An actor.</param>  
        /// <returns>
        /// Whether the actor was sucessfully removed.
        /// </returns> 
        public bool RemoveActor(ObiActor actor)
        {
            if (actor == null)
                return false;

            // remove from add buffer: TODO: use list instead of queue.
            addBuffer = new Queue<ObiActor>(addBuffer.Where(s => s != actor));

            // Find actor index in our actors array:
            int index = actors.IndexOf(actor);

            // If we are in charge of this actor indeed, perform all steps necessary to release it.
            if (index >= 0)
            {
                actor.UnloadBlueprint();

                for (int i = 0; i < actor.solverIndices.count; ++i)
                    particleToActor[actor.solverIndices[i]] = null;

                FreeParticles(actor.solverIndices);
                freeGroupIDs.Push(actor.groupID);

                actors.RemoveAt(index);

                actor.solverIndices.Dispose();
                actor.solverIndices = null;

                for (int i = 0; i < actor.solverBatchOffsets.Length; ++i)
                    actor.solverBatchOffsets[i].Clear();

                // If this was the last actor in the solver, tear it down:
                if (actors.Count == 0)
                    Teardown();

                return true;
            }

            return false;
        }

        private void InsertBufferedActor(ObiActor actor)
        {
            if (actor == null)
                return;

            // If the solver is not initialized yet, do so:
            Initialize();

            if (actor.solverIndices == null)
                actor.solverIndices = new ObiNativeIntList();
            actor.solverIndices.ResizeUninitialized(actor.sourceBlueprint.particleCount);

            AllocateParticles(actor.solverIndices);

            for (int i = 0; i < actor.solverIndices.count; ++i)
                particleToActor[actor.solverIndices[i]] = new ParticleInActor(actor, i);

            actors.Add(actor);

            if (freeGroupIDs.Count == 0)
                freeGroupIDs.Push(actors.Count);
            actor.groupID = freeGroupIDs.Pop();

            actor.LoadBlueprint();

            implementation.ParticleCountChanged(this);
            OnParticleCountChanged?.Invoke(this);
        }

        /// <summary>  
        /// Updates solver parameters. 
        /// </summary>
        /// Call this after modifying solver or constraint parameters.
        public void PushSolverParameters()
        {
            if (!initialized)
                return;

            implementation.SetParameters(parameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Distance, ref distanceConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Bending, ref bendingConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.ParticleCollision, ref particleCollisionConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.ParticleFriction, ref particleFrictionConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Collision, ref collisionConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Friction, ref frictionConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Density, ref densityConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Skin, ref skinConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Volume, ref volumeConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.ShapeMatching, ref shapeMatchingConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Tether, ref tetherConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Pin, ref pinConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Pinhole, ref pinholeConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Stitch, ref stitchConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.StretchShear, ref stretchShearConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.BendTwist, ref bendTwistConstraintParameters);

            implementation.SetConstraintGroupParameters(Oni.ConstraintType.Chain, ref chainConstraintParameters);

            if (OnUpdateParameters != null)
                OnUpdateParameters(this);

        }

        /// <summary>  
        /// Returns the parameters used by a given constraint type. 
        /// </summary>
        /// If you know the type of the constraints at runtime,
        /// this is the same as directly accessing the appropiate public Oni.ConstraintParameters struct in the solver.
        /// <param name="constraintType"> Type of the constraints whose parameters will be returned by this method.</param>  
        /// <returns>
        /// Parameters for the constraints of the specified type.
        /// </returns> 
        public Oni.ConstraintParameters GetConstraintParameters(Oni.ConstraintType constraintType)
        {
            switch (constraintType)
            {
                case Oni.ConstraintType.Distance: return distanceConstraintParameters;
                case Oni.ConstraintType.Bending: return bendingConstraintParameters;
                case Oni.ConstraintType.ParticleCollision: return particleCollisionConstraintParameters;
                case Oni.ConstraintType.ParticleFriction: return particleFrictionConstraintParameters;
                case Oni.ConstraintType.Collision: return collisionConstraintParameters;
                case Oni.ConstraintType.Friction: return frictionConstraintParameters;
                case Oni.ConstraintType.Skin: return skinConstraintParameters;
                case Oni.ConstraintType.Volume: return volumeConstraintParameters;
                case Oni.ConstraintType.ShapeMatching: return shapeMatchingConstraintParameters;
                case Oni.ConstraintType.Tether: return tetherConstraintParameters;
                case Oni.ConstraintType.Pin: return pinConstraintParameters;
                case Oni.ConstraintType.Pinhole: return pinholeConstraintParameters;
                case Oni.ConstraintType.Stitch: return stitchConstraintParameters;
                case Oni.ConstraintType.Density: return densityConstraintParameters;
                case Oni.ConstraintType.StretchShear: return stretchShearConstraintParameters;
                case Oni.ConstraintType.BendTwist: return bendTwistConstraintParameters;
                case Oni.ConstraintType.Chain: return chainConstraintParameters;

                default: return new Oni.ConstraintParameters(true, Oni.ConstraintParameters.EvaluationOrder.Sequential, 1);
            }
        }

        /// <summary>  
        /// Returns the runtime representation of constraints of a given type being simulated by this solver.
        /// </summary>  
        /// <param name="type"> Type of the constraints that will be returned by this method.</param>  
        /// <returns>
        /// The runtime constraints of the type speficied.
        /// </returns> 
        public IObiConstraints GetConstraintsByType(Oni.ConstraintType type)
        {
            int index = (int)type;
            if (m_Constraints != null && index >= 0 && index < m_Constraints.Length)
                return m_Constraints[index];
            return null;
        }

        private void PushActiveParticles()
        {
            if (dirtyActiveParticles)
            {
                using (m_PushActiveParticles.Auto())
                {
                    activeParticles.Clear();
                    for (int i = 0; i < actors.Count; ++i)
                    {
                        if (actors[i].isActiveAndEnabled)
                            activeParticles.AddRange(actors[i].solverIndices, actors[i].activeParticleCount);
                    }

                    implementation.SetActiveParticles(activeParticles);

                    dirtyActiveParticles = false;
                }
            }
        }

        private void PushDeformableTriangles()
        {
            if (dirtyDeformableTriangles)
            {
                using (m_PushDeformableTriangles.Auto())
                {
                    deformableTriangles.Clear();
                    deformableUVs.Clear();

                    for (int i = 0; i < actors.Count; ++i)
                    {
                        ObiActor currentActor = actors[i];
                        if (currentActor.isActiveAndEnabled)
                        {
                            currentActor.ProvideDeformableTriangles(deformableTriangles, deformableUVs);
                        }
                    }

                    implementation.SetDeformableTriangles(deformableTriangles, deformableUVs);

                    dirtyDeformableTriangles = false;
                }
            }
        }

        private void PushDeformableEdges()
        {
            if (dirtyDeformableEdges)
            {
                using (m_PushDeformableEdges.Auto())
                {
                    deformableEdges.Clear();

                    for (int i = 0; i < actors.Count; ++i)
                    {
                        ObiActor currentActor = actors[i];
                        if (currentActor.isActiveAndEnabled)
                        {
                            currentActor.ProvideDeformableEdges(deformableEdges);
                        }
                    }

                    implementation.SetDeformableEdges(deformableEdges);

                    dirtyDeformableEdges = false;
                }
            }
        }

        private void PushSimplices()
        {

            if (dirtySimplices != Oni.SimplexType.None)
            {
                using (m_PushSimplices.Auto())
                {
                    simplices.Clear();

                    if ((dirtySimplices & Oni.SimplexType.Point) != 0)
                        points.Clear();

                    if ((dirtySimplices & Oni.SimplexType.Edge) != 0)
                        edges.Clear();

                    if ((dirtySimplices & Oni.SimplexType.Triangle) != 0)
                        triangles.Clear();

                    for (int i = 0; i < actors.Count; ++i)
                    {
                        var currentActor = actors[i];

                        if (currentActor.isActiveAndEnabled && currentActor.isLoaded)
                        {
                            //simplex based contacts
                            if (currentActor.surfaceCollisions)
                            {
                                if (currentActor.sharedBlueprint.points != null && (dirtySimplices & Oni.SimplexType.Point) != 0)
                                    for (int j = 0; j < currentActor.sharedBlueprint.points.Length; ++j)
                                    {
                                        int actorIndex = currentActor.sharedBlueprint.points[j];

                                        if (actorIndex < currentActor.activeParticleCount)
                                            points.Add(currentActor.solverIndices[actorIndex]);
                                    }

                                if (currentActor.sharedBlueprint.edges != null && (dirtySimplices & Oni.SimplexType.Edge) != 0)
                                    for (int j = 0; j < currentActor.sharedBlueprint.edges.Length / 2; ++j)
                                    {
                                        int actorIndex1 = currentActor.sharedBlueprint.edges[j * 2];
                                        int actorIndex2 = currentActor.sharedBlueprint.edges[j * 2 + 1];

                                        if (actorIndex1 < currentActor.activeParticleCount && actorIndex2 < currentActor.activeParticleCount)
                                        {
                                            edges.Add(currentActor.solverIndices[actorIndex1]);
                                            edges.Add(currentActor.solverIndices[actorIndex2]);
                                        }
                                    }

                                if (currentActor.sharedBlueprint.triangles != null && (dirtySimplices & Oni.SimplexType.Triangle) != 0)
                                    for (int j = 0; j < currentActor.sharedBlueprint.triangles.Length / 3; ++j)
                                    {
                                        int actorIndex1 = currentActor.sharedBlueprint.triangles[j * 3];
                                        int actorIndex2 = currentActor.sharedBlueprint.triangles[j * 3 + 1];
                                        int actorIndex3 = currentActor.sharedBlueprint.triangles[j * 3 + 2];

                                        if (actorIndex1 < currentActor.activeParticleCount &&
                                            actorIndex2 < currentActor.activeParticleCount &&
                                            actorIndex3 < currentActor.activeParticleCount)
                                        {
                                            triangles.Add(currentActor.solverIndices[actorIndex1]);
                                            triangles.Add(currentActor.solverIndices[actorIndex2]);
                                            triangles.Add(currentActor.solverIndices[actorIndex3]);
                                        }
                                    }
                            }
                            // particle based contacts
                            else if ((dirtySimplices & Oni.SimplexType.Point) != 0)
                            {
                                // generate a point simplex out of each active particle:
                                points.AddRange(currentActor.solverIndices, currentActor.activeParticleCount);
                            }
                        }
                    }

                    simplices.EnsureCapacity(points.count + edges.count + triangles.count);
                    simplices.AddRange(triangles);
                    simplices.AddRange(edges);
                    simplices.AddRange(points);

                    m_SimplexCounts = new SimplexCounts(points.count, edges.count / 2, triangles.count / 3);

                    cellCoords.ResizeInitialized(m_SimplexCounts.simplexCount);

                    implementation.SetSimplices(simplices, m_SimplexCounts);

                    dirtySimplices = Oni.SimplexType.None;
                }
            }
        }

        private void PushConstraints()
        {
            if (dirtyConstraints != 0)
            {
                // Clear all dirty constraints:
                for (int i = 0; i < Oni.ConstraintTypeCount; ++i)
                    if (m_Constraints[i] != null && ((1 << i) & dirtyConstraints) != 0)
                        m_Constraints[i].Clear();

                // Iterate over all actors, merging their batches together:
                for (int k = 0; k < actors.Count; ++k)
                {
                    if (actors[k].isLoaded)
                    {
                        for (int i = 0; i < Oni.ConstraintTypeCount; ++i)
                            if (m_Constraints[i] != null && ((1 << i) & dirtyConstraints) != 0)
                            {
                                var constraints = actors[k].GetConstraintsByType((Oni.ConstraintType)i);
                                m_Constraints[i].Merge(actors[k], constraints);
                            }
                    }
                }

                // Readd the constraints to the solver:
                for (int i = 0; i < Oni.ConstraintTypeCount; ++i)
                    if (m_Constraints[i] != null && ((1 << i) & dirtyConstraints) != 0)
                        m_Constraints[i].AddToSolver(this);

                // Reset the dirty flag:
                dirtyConstraints = 0;
            }
        }

        /**
         * Updates solver bounds, then checks if they're visible from at least one camera. If so, sets isVisible to true, false otherwise.
         */
        private void UpdateVisibility()
        {
            using (m_UpdateVisibilityPerfMarker.Auto())
            {
                using (m_GetSolverBoundsPerfMarker.Auto())
                {
                    // get bounds in solver space:
                    Vector3 min = Vector3.zero, max = Vector3.zero;
                    implementation.GetBounds(ref min, ref max);
                    m_Bounds.SetMinMax(min, max);
                }

                if (m_Bounds.AreValid())
                {
                    using (m_TestBoundsPerfMarker.Auto())
                    {
                        // transform bounds to world space:
                        m_BoundsWS = m_Bounds.Transform(transform.localToWorldMatrix);

                        using (m_GetAllCamerasPerfMarker.Auto())
                        {
                            Array.Resize(ref sceneCameras, Camera.allCamerasCount);
                            Camera.GetAllCameras(sceneCameras);
                        }

                        foreach (Camera cam in sceneCameras)
                        {
                            GeometryUtility.CalculateFrustumPlanes(cam, planes);
                            if (GeometryUtility.TestPlanesAABB(planes, m_BoundsWS))
                            {
                                if (!isVisible)
                                {
                                    isVisible = true;
                                    foreach (ObiActor actor in actors)
                                        actor.OnSolverVisibilityChanged(isVisible);
                                }
                                return;
                            }
                        }
                    }
                }

                if (isVisible)
                {
                    isVisible = false;
                    foreach (ObiActor actor in actors)
                        actor.OnSolverVisibilityChanged(isVisible);
                }
            }
        }

        private void InitializeTransformFrame()
        {
            Vector4 translation = transform.position;
            Vector4 scale = transform.lossyScale;
            Quaternion rotation = transform.rotation;

            implementation.InitializeFrame(translation, scale, rotation);
        }

        private IObiJobHandle UpdateTransformFrame(float dt)
        {
            Vector4 translation = transform.position;
            Vector4 scale = transform.lossyScale;
            Quaternion rotation = transform.rotation;

            implementation.UpdateFrame(translation, scale, rotation, dt);
            return implementation.ApplyFrame(worldLinearInertiaScale, worldAngularInertiaScale, dt);
        }

        public void RegisterRenderSystem(IRenderSystem renderSystem)
        {
            m_RenderSystems.RegisterRenderSystem(renderSystem);
        }

        public void UnregisterRenderSystem(IRenderSystem renderSystem)
        {
            m_RenderSystems.UnregisterRenderSystem(renderSystem);
        }

        public RenderSystem<T> GetRenderSystem<T>() where T : ObiRenderer<T>
        {
            return m_RenderSystems.GetRenderSystem<T>();
        }

        public IRenderSystem GetRenderSystem(Oni.RenderingSystemType type)
        {
            return m_RenderSystems.GetRenderSystem(type);
        }

        /// <summary>
        /// Enqueues a generic spatial query to be performed during the next physics update.
        /// If called when the solver is yet uninitialized,
        /// the query will be ignored and this method will return -1.
        /// </summary>
        /// <param name="shape"> Query shape to test against all simplices in the solver.</param>
        /// <param name="transform"> Transform to apply to the query shape.</param>
        /// <returns>
        /// Index of the query in the queue. Use the queryIndex member of each query result to correlate each result to the query that spawned it. For instance:
        /// a query result with queryIndex 5, belongs to the query shape at index 5 in the queue.
        /// </returns>
        public int EnqueueSpatialQuery(QueryShape shape, AffineTransform transform)
        {
            // if the solver is not initialized, bail out.
            if (!initialized)
                return -1;

            int index = bufferedQueryShapes.count;
            bufferedQueryShapes.Add(shape);
            bufferedQueryTransforms.Add(transform);
            return index;
        }

        /// <summary>
        /// Enqueues multiple generic spatial query to be performed during the next physics update.
        /// If called when the solver is yet uninitialized,
        /// the query will be ignored and this method will return -1.
        /// </summary>
        /// <param name="shapes"> Query shapes to test against all simplices in the solver.</param>
        /// <param name="transforms"> Transforms to apply to the query shapes.</param>
        /// <returns>
        /// Index of the first query in the queue. Use the queryIndex member of each query result to correlate each result to the query that spawned it. For instance:
        /// a query result with queryIndex 5, belongs to the query shape at index 5 in the queue.
        /// </returns>
        public int EnqueueSpatialQueries(ObiNativeQueryShapeList shapes, ObiNativeAffineTransformList transforms)
        {
            // if the solver is not initialized or input is not ok, bail out.
            if (!initialized || shapes == null || transforms == null || shapes.count != transforms.count)
                return -1;

            int index = bufferedQueryShapes.count;
            bufferedQueryShapes.AddRange(shapes);
            bufferedQueryTransforms.AddRange(transforms);
            return index;
        }

        /// <summary>
        /// Enqueues a raycast to be performed during the next physics update.
        /// If called when the solver is yet uninitialized,
        /// the query will be ignored and this method will return -1.
        /// </summary>
        /// <param name="ray"> Ray to cast against all simplices in the solver. Expressed in world space.</param>
        /// <param name="filter"> Filter (mask, category) used to filter out collisions against certain simplices. </param>
        /// <param name="maxDistance"> Ray length. </param>
        /// <param name="rayThickness">
        /// Ray thickness. If the ray hits a simplex, hitInfo will contain a point on the simplex.
        /// If it merely passes near the simplex (within its thickness distance, but no actual hit), it will contain the point on the ray closest to the simplex surface. </param>
        /// <returns>
        /// Index of the query in the queue. Use the queryIndex member of each query result to correlate each result to the query that spawned it. For instance:
        /// a query result with queryIndex 5, belongs to the query shape at index 5 in the queue.
        /// </returns>
        public int EnqueueRaycast(Ray ray, int filter, float maxDistance = 100, float rayThickness = 0)
        {
            // if the solver is not initialized or simulation is currently underway, bail out.
            if (!initialized)
                return -1;

            int index = bufferedQueryShapes.count;

            bufferedQueryShapes.Add(new QueryShape
            {
                type = QueryShape.QueryType.Ray,
                center = ray.origin,
                size = ray.direction * maxDistance,
                contactOffset = rayThickness,
                maxDistance = 0.0001f,
                filter = filter
            });

            bufferedQueryTransforms.Add(new AffineTransform(Vector4.zero, Quaternion.identity, Vector4.one));

            return index;
        }

    }

}
