using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using Obi;


public static class  Oni
{
    public const int ConstraintTypeCount = 18;
    public const int ColliderShapeTypeCount = 7;
    public const int QueryTypeCount = 3;

    public enum ConstraintType
    {
        Tether = 0,
        Volume = 1,
        Chain = 2,
        Pinhole = 3,
        Bending = 4,
        Distance = 5,
        ShapeMatching = 6,
        BendTwist = 7,
        StretchShear = 8,
        Pin = 9,
        ParticleCollision = 10,
        Density = 11,
        Collision = 12,
        Skin = 13,
        Aerodynamics = 14,
        Stitch = 15,
        ParticleFriction = 16,
        Friction = 17,
    };

    [Flags]
    public enum RenderingSystemType
    {
        None = 0,
        PathSmoother = 1 << 0,
        ExtrudedRope = 1 << 1,
        ChainRope = 1 << 2,
        LineRope = 1 << 3,
        MeshRope = 1 << 4,
        Cloth = 1 << 5,
        SkinnedCloth = 1 << 6,
        TearableCloth = 1 << 7,
        Softbody = 1 << 8,
        Fluid = 1 << 9,
        Particles = 1 << 10,
        InstancedParticles = 1 << 11,
        FoamParticles = 1 << 12,

        AllSmoothedRopes = PathSmoother | ExtrudedRope  | LineRope | MeshRope,
        AllRopes = PathSmoother | ExtrudedRope | ChainRope | LineRope | MeshRope | Particles | InstancedParticles,
        AllClothes = Cloth | SkinnedCloth | TearableCloth | Particles | InstancedParticles,
        AllParticles = Fluid | Particles | InstancedParticles | FoamParticles
    };

    [Flags]
    public enum SimplexType
    {
        None = 0,
        Point = 1 << 0,
        Edge = 1 << 1,
        Triangle = 1 << 2,
        All = ~0
    };

    public enum ShapeType
    {
        Sphere = 0,
        Box = 1,
        Capsule = 2,
        Heightmap = 3,
        TriangleMesh = 4,
        EdgeMesh = 5,
        SignedDistanceField = 6
    }

    public enum MaterialCombineMode
    {
        Average = 0,
        Minimum = 1,
        Multiply = 2,
        Maximum = 3
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SolverParameters
    {

        public enum Interpolation
        {
            None,
            Interpolate,
            Extrapolate
        };

        public enum Mode
        {
            Mode3D,
            Mode2D,
        };

        [Tooltip("In 2D mode, particles are simulated on the XY plane only. For use in conjunction with Unity's 2D mode.")]
        public Mode mode;

        [Tooltip("Same as Rigidbody.interpolation. Set to INTERPOLATE for cloth that is applied on a main character or closely followed by a camera. NONE for everything else.")]
        public Interpolation interpolation;

        [Tooltip("Simulation gravity expressed in local space.")]
        public Vector3 gravity;

        [Tooltip("Simulation wind expressed in local space.")]
        public Vector3 ambientWind;

        [Tooltip("Foam gravity scale.")]
        [Range(-1, 3)]
        public float foamGravityScale;

        [Tooltip("Percentage of velocity lost per second, between 0% (0) and 100% (1).")]
        [Range(0, 1)]
        public float damping;

        [Tooltip("Max ratio between a particle's longest and shortest axis. Use 1 for isotropic (completely round) particles.")]
        [Range(1, 5)]
        public float maxAnisotropy;

        [Tooltip("Mass-normalized kinetic energy threshold below which particle positions aren't updated.")]
        public float sleepThreshold;

        [Tooltip("Maximum particle linear velocity.")]
        public float maxVelocity;

        [Tooltip("Maximum particle angular velocity.")]
        public float maxAngularVelocity;

        [Tooltip("Maximum distance between elements (simplices/colliders) for a contact to be generated.")]
        public float collisionMargin;

        [Tooltip("Maximum depenetration velocity applied to particles that start a frame inside an object. Low values ensure no 'explosive' collision resolution. Should be > 0 unless looking for non-physical effects.")]
        public float maxDepenetration;

        [Tooltip("Percentage of collider velocities used for continuous collision detection. Set to 0 for purely static collisions, set to 1 for pure continuous collisions.")]
        [Range(0, 1)]
        public float colliderCCD;

        [Tooltip("Percentage of particle velocities used for continuous collision detection. Set to 0 for purely static collisions, set to 1 for pure continuous collisions.")]
        [Range(0, 1)]
        public float particleCCD;

        [Tooltip("Percentage of shock propagation applied to particle-particle collisions. Useful for particle stacking.")]
        [Range(0, 1)]
        public float shockPropagation;

        [Tooltip("Amount of iterations spent on convex optimization for surface collisions.")]
        [Range(1, 32)]
        public int surfaceCollisionIterations;

        [Tooltip("Error threshold at which to stop convex optimization for surface collisions.")]
        public float surfaceCollisionTolerance;


        public SolverParameters(Interpolation interpolation, Vector4 gravity)
        {
            this.mode = Mode.Mode3D;
            this.gravity = gravity;
            this.ambientWind = Vector3.zero;
            this.interpolation = interpolation;
            foamGravityScale = 1;
            damping = 0;
            shockPropagation = 0;
            surfaceCollisionIterations = 8;
            surfaceCollisionTolerance = 0.005f;
            maxAnisotropy = 3;
            maxDepenetration = 10;
            sleepThreshold = 0.0005f;
            maxVelocity = 50.0f;
            maxAngularVelocity = 20.0f;
            collisionMargin = 0.02f;
            colliderCCD = 1;
            particleCCD = 0;
        }

    }

    [Serializable]
    public struct ConstraintParameters
    {

        public enum EvaluationOrder
        {
            Sequential,
            Parallel
        };

        [Tooltip("Order in which constraints are evaluated. SEQUENTIAL converges faster but is not very stable. PARALLEL is very stable but converges slowly, requiring more iterations to achieve the same result.")]
        public EvaluationOrder evaluationOrder;                             /**< Constraint evaluation order.*/

        [Tooltip("Number of relaxation iterations performed by the constraint solver. A low number of iterations will perform better, but be less accurate.")]
        public int iterations;                                              /**< Amount of solver iterations per step for this constraint group.*/

        [Tooltip("Over (or under if < 1) relaxation factor used. At 1, no overrelaxation is performed. At 2, constraints double their relaxation rate. High values reduce stability but improve convergence.")]
        [Range(0.1f, 2)]
        public float SORFactor;                                             /**< Sucessive over-relaxation factor for parallel evaluation order.*/

        [Tooltip("Whether this constraint group is solved or not.")]
        [MarshalAs(UnmanagedType.I1)]
        public bool enabled;

        public ConstraintParameters(bool enabled, EvaluationOrder order, int iterations)
        {
            this.enabled = enabled;
            this.iterations = iterations;
            this.evaluationOrder = order;
            this.SORFactor = 1;
        }

    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ContactPair
    {
        public int bodyA;    /** simplex index*/
        public int bodyB;    /** simplex or rigidbody index*/
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Contact
    {
        public Vector4 pointA;
        public Vector4 pointB; 		   /**< Speculative point of contact. */
        public Vector4 normal;         /**< Normal direction. */
        public Vector4 tangent;        /**< Tangent direction. */

        public float distance;    /** distance between both colliding entities at the beginning of the timestep.*/

        public float normalImpulse;
        public float tangentImpulse;
        public float bitangentImpulse;
        public float stickImpulse;
        public float rollingFrictionImpulse;

        public int bodyA;    /** simplex index*/
        public int bodyB;    /** simplex or rigidbody index*/
    }
}
