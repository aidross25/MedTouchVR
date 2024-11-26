#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)using UnityEngine;using Unity.Jobs;using Unity.Mathematics;using Unity.Collections;

namespace Obi{    public class BurstSolverImpl : ISolverImpl    {        public ObiSolver abstraction { get; }        public int particleCount        {            get { return abstraction.positions.count; }        }        public int activeParticleCount        {            get { return abstraction.activeParticles.count; }        }        public BurstInertialFrame inertialFrame        {            get { return m_InertialFrame; }        }        public BurstAffineTransform solverToWorld        {            get { return m_InertialFrame.frame; }        }        public BurstAffineTransform worldToSolver        {            get { return m_InertialFrame.frame.Inverse(); }        }        public uint activeFoamParticleCount { private set; get; }        private const int maxBatches = 17;        private ConstraintBatcher<ContactProvider> collisionConstraintBatcher;        private ConstraintBatcher<FluidInteractionProvider> fluidConstraintBatcher;        // Per-type constraints array:        private IBurstConstraintsImpl[] constraints;        // Per-type iteration padding array:        private int[] padding = new int[Oni.ConstraintTypeCount];        // job handle:        private BurstJobHandle jobHandle;        // particle contact generation:        public ParticleGrid particleGrid;        public NativeArray<BatchData> particleBatchData;        // fluid interaction generation:        public NativeArray<FluidInteraction> fluidInteractions;        public NativeArray<BatchData> fluidBatchData;        // collider contact generation:        private BurstColliderWorld colliderGrid;        // deformable triangles:        private NativeArray<int> deformableTriangles;        private NativeArray<float2> deformableUVs;        // deformable edges:        private NativeArray<int> deformableEdges;

        // simplices:
        public NativeArray<int> simplices;        public SimplexCounts simplexCounts;        private BurstInertialFrame m_InertialFrame; // local to world inertial frame.        private int scheduledJobCounter = 0;

        // cached particle data arrays (just wrappers over raw unmanaged data held by the abstract solver)
        public NativeArray<int> activeParticles;        public NativeArray<float4> positions;        public NativeArray<float4> restPositions;        public NativeArray<float4> prevPositions;        public NativeArray<float4> renderablePositions;        public NativeArray<quaternion> orientations;        public NativeArray<quaternion> restOrientations;        public NativeArray<quaternion> prevOrientations;        public NativeArray<quaternion> renderableOrientations;        public NativeArray<float4> velocities;        public NativeArray<float4> angularVelocities;        public NativeArray<float> invMasses;        public NativeArray<float> invRotationalMasses;        public NativeArray<float4> externalForces;        public NativeArray<float4> externalTorques;        public NativeArray<float4> wind;        public NativeArray<float4> positionDeltas;        public NativeArray<quaternion> orientationDeltas;        public NativeArray<int> positionConstraintCounts;        public NativeArray<int> orientationConstraintCounts;        public NativeArray<float4> colors;        public NativeArray<int> collisionMaterials;        public NativeArray<int> phases;        public NativeArray<int> filters;        public NativeArray<float4> renderableRadii;        public NativeArray<float4> principalRadii;        public NativeArray<float4> normals;        private NativeArray<float4> tangents;        public NativeArray<float> life;        public NativeArray<float4> fluidData;        public NativeArray<float4> userData;        public NativeArray<float4> fluidInterface;        public NativeArray<float4> fluidMaterials;
        public NativeArray<float4x4> anisotropies;

        // aux foam data:
        public NativeArray<float4> auxPositions;
        public NativeArray<float4> auxVelocities;
        public NativeArray<float4> auxColors;
        public NativeArray<float4> auxAttributes;
        public NativeArray<int4> cellCoords;        public NativeArray<BurstAabb> simplexBounds;
        public NativeArray<BurstAabb> reducedBounds;        public BurstAabb solverBounds;        private ConstraintSorter<BurstContact> contactSorter;        public BurstSolverImpl(ObiSolver solver)        {            this.abstraction = solver;            jobHandle = new BurstJobHandle();            contactSorter = new ConstraintSorter<BurstContact>();            // Initialize collision world:            GetOrCreateColliderWorld();            colliderGrid.IncreaseReferenceCount();

            // Initialize contact generation acceleration structure:
            particleGrid = new ParticleGrid();            // Initialize constraint batcher:            collisionConstraintBatcher = new ConstraintBatcher<ContactProvider>(maxBatches);            fluidConstraintBatcher = new ConstraintBatcher<FluidInteractionProvider>(maxBatches);            // Initialize constraint arrays:            constraints = new IBurstConstraintsImpl[Oni.ConstraintTypeCount];            constraints[(int)Oni.ConstraintType.Tether] = new BurstTetherConstraints(this);            constraints[(int)Oni.ConstraintType.Volume] = new BurstVolumeConstraints(this);            constraints[(int)Oni.ConstraintType.Chain] = new BurstChainConstraints(this);            constraints[(int)Oni.ConstraintType.Bending] = new BurstBendConstraints(this);            constraints[(int)Oni.ConstraintType.Distance] = new BurstDistanceConstraints(this);            constraints[(int)Oni.ConstraintType.ShapeMatching] = new BurstShapeMatchingConstraints(this);            constraints[(int)Oni.ConstraintType.BendTwist] = new BurstBendTwistConstraints(this);            constraints[(int)Oni.ConstraintType.StretchShear] = new BurstStretchShearConstraints(this);            constraints[(int)Oni.ConstraintType.Pin] = new BurstPinConstraints(this);            constraints[(int)Oni.ConstraintType.ParticleCollision] = new BurstParticleCollisionConstraints(this);            constraints[(int)Oni.ConstraintType.Density] = new BurstDensityConstraints(this);            constraints[(int)Oni.ConstraintType.Collision] = new BurstColliderCollisionConstraints(this);            constraints[(int)Oni.ConstraintType.Skin] = new BurstSkinConstraints(this);            constraints[(int)Oni.ConstraintType.Aerodynamics] = new BurstAerodynamicConstraints(this);            constraints[(int)Oni.ConstraintType.Stitch] = new BurstStitchConstraints(this);            constraints[(int)Oni.ConstraintType.ParticleFriction] = new BurstParticleFrictionConstraints(this);            constraints[(int)Oni.ConstraintType.Friction] = new BurstColliderFrictionConstraints(this);            var c = constraints[(int)Oni.ConstraintType.Collision] as BurstColliderCollisionConstraints;            c.CreateConstraintsBatch();            var f = constraints[(int)Oni.ConstraintType.Friction] as BurstColliderFrictionConstraints;            f.CreateConstraintsBatch();        }        public void Destroy()        {            for (int i = 0; i < constraints.Length; ++i)                if (constraints[i] != null)                    constraints[i].Dispose();            // Get rid of particle and collider grids:            particleGrid.Dispose();            if (colliderGrid != null)                colliderGrid.DecreaseReferenceCount();            collisionConstraintBatcher.Dispose();            fluidConstraintBatcher.Dispose();            if (simplexBounds.IsCreated)                simplexBounds.Dispose();            if (reducedBounds.IsCreated)                reducedBounds.Dispose();            if (tangents.IsCreated)                tangents.Dispose();            if (particleBatchData.IsCreated)                particleBatchData.Dispose();            if (fluidInteractions.IsCreated)                fluidInteractions.Dispose();            if (fluidBatchData.IsCreated)                fluidBatchData.Dispose();            if (auxPositions.IsCreated)                auxPositions.Dispose();            if (auxVelocities.IsCreated)                auxVelocities.Dispose();            if (auxColors.IsCreated)                auxColors.Dispose();            if (auxAttributes.IsCreated)                auxAttributes.Dispose();        }        // Utility function to count scheduled jobs. Call it once per job.        // Will JobHandle.ScheduleBatchedJobs once there's a good bunch of scheduled jobs.        public void ScheduleBatchedJobsIfNeeded()        {            if (scheduledJobCounter++ > 16)            {                scheduledJobCounter = 0;                JobHandle.ScheduleBatchedJobs();            }        }          private void GetOrCreateColliderWorld()        {            colliderGrid = GameObject.FindObjectOfType<BurstColliderWorld>();            if (colliderGrid == null)            {                var world = new GameObject("BurstCollisionWorld", typeof(BurstColliderWorld));                colliderGrid = world.GetComponent<BurstColliderWorld>();            }        }        public void InitializeFrame(Vector4 translation, Vector4 scale, Quaternion rotation)        {            m_InertialFrame = new BurstInertialFrame(translation, scale, rotation);        }         public void UpdateFrame(Vector4 translation, Vector4 scale, Quaternion rotation, float deltaTime)        {            m_InertialFrame.Update(translation, scale, rotation, deltaTime);        }        public IObiJobHandle ApplyFrame(float worldLinearInertiaScale, float worldAngularInertiaScale, float deltaTime)        {            // inverse linear part:            float4x4 linear = float4x4.TRS(float3.zero, inertialFrame.frame.rotation, math.rcp(inertialFrame.frame.scale.xyz));            float4x4 linearInv = math.transpose(linear);            // non-inertial frame accelerations:            float4 angularVel = math.mul(linearInv, math.mul(float4x4.Scale(inertialFrame.angularVelocity.xyz), linear)).diagonal();            float4 eulerAccel = math.mul(linearInv, math.mul(float4x4.Scale(inertialFrame.angularAcceleration.xyz), linear)).diagonal();            float4 inertialAccel = math.mul(linearInv, inertialFrame.acceleration);            var applyInertialForces = new ApplyInertialForcesJob()            {                activeParticles = activeParticles,                positions = positions,                velocities = velocities,                invMasses = invMasses,                angularVel = angularVel,                inertialAccel = inertialAccel,                eulerAccel = eulerAccel,                worldLinearInertiaScale = worldLinearInertiaScale,                worldAngularInertiaScale = worldAngularInertiaScale,                deltaTime = deltaTime,            };            jobHandle.jobHandle = applyInertialForces.Schedule(activeParticleCount, 64);            return jobHandle;        }        public void SetDeformableTriangles(ObiNativeIntList indices, ObiNativeVector2List uvs)        {            deformableTriangles = indices.AsNativeArray<int>();            deformableUVs = uvs.AsNativeArray<float2>();        }        public void SetDeformableEdges(ObiNativeIntList indices)        {            deformableEdges = indices.AsNativeArray<int>();        }        public void SetSimplices(ObiNativeIntList simplices, SimplexCounts counts)        {            this.simplices = simplices.AsNativeArray<int>();            this.simplexCounts = counts;

            cellCoords = abstraction.cellCoords.AsNativeArray<int4>();            if (simplexBounds.IsCreated)                simplexBounds.Dispose();            simplexBounds = new NativeArray<BurstAabb>(counts.simplexCount, Allocator.Persistent);

            if (reducedBounds.IsCreated)                reducedBounds.Dispose();

            reducedBounds = new NativeArray<BurstAabb>(counts.simplexCount, Allocator.Persistent);        }        public void SetActiveParticles(ObiNativeIntList activeIndices)        {            activeParticles = activeIndices.AsNativeArray<int>();        }        public IObiJobHandle UpdateBounds(IObiJobHandle inputDeps, float stepTime)        {
            BurstJobHandle burstHandle = inputDeps as BurstJobHandle;            if (burstHandle == null)                return inputDeps;

            // calculate bounding boxes for all simplices:
            var boundsJob = new CalculateSimplexBoundsJob()
            {
                radii = principalRadii,
                fluidMaterials = fluidMaterials,
                positions = positions,
                velocities = velocities,
                simplices = simplices,
                simplexCounts = simplexCounts,
                particleMaterialIndices = collisionMaterials,
                collisionMaterials = ObiColliderWorld.GetInstance().collisionMaterials.AsNativeArray<BurstCollisionMaterial>(),
                parameters = abstraction.parameters,
                simplexBounds = simplexBounds,
                reducedBounds = reducedBounds,
                dt = stepTime
            };

            burstHandle.jobHandle = boundsJob.Schedule(simplexCounts.simplexCount, 64, burstHandle.jobHandle);

            // parallel reduction:
            int chunkSize = 4;            int chunks = simplexCounts.simplexCount;            int stride = 1;            while (chunks > 1)            {                var reductionJob = new BoundsReductionJob()                {                    bounds = reducedBounds,                    stride = stride,                    size = chunkSize,                };                burstHandle.jobHandle = reductionJob.Schedule(chunks, 1, burstHandle.jobHandle);                chunks = (int)math.ceil(chunks / (float)chunkSize);                stride *= chunkSize;            }            return burstHandle;        }        public void GetBounds(ref Vector3 min, ref Vector3 max)
        {
            // update solver bounds struct:
            if (reducedBounds.IsCreated && reducedBounds.Length > 0)
            {
                solverBounds.min = reducedBounds[0].min;
                solverBounds.max = reducedBounds[0].max;
            }

            min = solverBounds.min.xyz;
            max = solverBounds.max.xyz;
        }        public int GetConstraintCount(Oni.ConstraintType type)        {            if ((int)type > 0 && (int)type < constraints.Length)                return constraints[(int)type].GetConstraintCount();            return 0;        }        public void SetParameters(Oni.SolverParameters parameters)        {
        }        public void SetConstraintGroupParameters(Oni.ConstraintType type, ref Oni.ConstraintParameters parameters)        {            // No need to implement. This backend grabs parameters from the abstraction when it needs them.        }        public void ParticleCountChanged(ObiSolver solver)        {            positions = abstraction.positions.AsNativeArray<float4>();            restPositions = abstraction.restPositions.AsNativeArray<float4>();            prevPositions = abstraction.prevPositions.AsNativeArray<float4>();            renderablePositions = abstraction.renderablePositions.AsNativeArray<float4>();            orientations = abstraction.orientations.AsNativeArray<quaternion>();            restOrientations = abstraction.restOrientations.AsNativeArray<quaternion>();            prevOrientations = abstraction.prevOrientations.AsNativeArray<quaternion>();            renderableOrientations = abstraction.renderableOrientations.AsNativeArray<quaternion>();            colors = abstraction.colors.AsNativeArray<float4>();            velocities = abstraction.velocities.AsNativeArray<float4>();            angularVelocities = abstraction.angularVelocities.AsNativeArray<float4>();            invMasses = abstraction.invMasses.AsNativeArray<float>();            invRotationalMasses = abstraction.invRotationalMasses.AsNativeArray<float>();            externalForces = abstraction.externalForces.AsNativeArray<float4>();            externalTorques = abstraction.externalTorques.AsNativeArray<float4>();            wind = abstraction.wind.AsNativeArray<float4>();            positionDeltas = abstraction.positionDeltas.AsNativeArray<float4>();            orientationDeltas = abstraction.orientationDeltas.AsNativeArray<quaternion>();            positionConstraintCounts = abstraction.positionConstraintCounts.AsNativeArray<int>();            orientationConstraintCounts = abstraction.orientationConstraintCounts.AsNativeArray<int>();            collisionMaterials = abstraction.collisionMaterials.AsNativeArray<int>();            phases = abstraction.phases.AsNativeArray<int>();            filters = abstraction.filters.AsNativeArray<int>();            renderableRadii = abstraction.renderableRadii.AsNativeArray<float4>();            principalRadii = abstraction.principalRadii.AsNativeArray<float4>();            normals = abstraction.normals.AsNativeArray<float4>();            life = abstraction.life.AsNativeArray<float>();            fluidData = abstraction.fluidData.AsNativeArray<float4>();            userData = abstraction.userData.AsNativeArray<float4>();            fluidInterface = abstraction.fluidInterface.AsNativeArray<float4>();            fluidMaterials = abstraction.fluidMaterials.AsNativeArray<float4>();            anisotropies = abstraction.anisotropies.AsNativeArray<float4x4>();            cellCoords = abstraction.cellCoords.AsNativeArray<int4>();            if (tangents.IsCreated)                tangents.Dispose();            tangents = new NativeArray<float4>(normals.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);        }        public void MaxFoamParticleCountChanged(ObiSolver solver)
        {
            if (auxPositions.IsCreated)
                auxPositions.Dispose();
            if (auxVelocities.IsCreated)
                auxVelocities.Dispose();
            if (auxColors.IsCreated)
                auxColors.Dispose();
            if (auxAttributes.IsCreated)
                auxAttributes.Dispose();

            auxPositions = new NativeArray<float4>((int)abstraction.maxFoamParticles, Allocator.Persistent);
            auxVelocities = new NativeArray<float4>((int)abstraction.maxFoamParticles, Allocator.Persistent);
            auxColors = new NativeArray<float4>((int)abstraction.maxFoamParticles, Allocator.Persistent);
            auxAttributes = new NativeArray<float4>((int)abstraction.maxFoamParticles, Allocator.Persistent);
        }        public void SetRigidbodyArrays(ObiSolver solver)        {            // No need to implement. This backend grabs arrays from the abstraction when it needs them.        }        public IConstraintsBatchImpl CreateConstraintsBatch(Oni.ConstraintType type)        {            return constraints[(int)type].CreateConstraintsBatch();        }        public void DestroyConstraintsBatch(IConstraintsBatchImpl batch)        {            if (batch != null)                constraints[(int)batch.constraintType].RemoveBatch(batch);        }        public void FinishSimulation()        {
            // Wipe all forces to zero. However we can't wipe wind here, since we
            // need wind values during interpolation to calculate rope normals.
            abstraction.externalForces.WipeToZero();
            abstraction.externalTorques.WipeToZero();

            abstraction.externalForces.Upload();
            abstraction.externalTorques.Upload();

            // store current end positions as the start positions for the next step.
            abstraction.startPositions.CopyFrom(abstraction.endPositions);
            abstraction.startOrientations.CopyFrom(abstraction.endOrientations);
            abstraction.endPositions.CopyFrom(abstraction.positions);            abstraction.endOrientations.CopyFrom(abstraction.orientations);        }

        public void PushData()
        {
            // Initialize wind values with solver's ambient wind.
            abstraction.wind.WipeToValue(abstraction.parameters.ambientWind);
            abstraction.wind.Upload();
        }

        public void RequestReadback()
        {
        }        public IObiJobHandle CollisionDetection(IObiJobHandle inputDeps, float stepTime)        {            BurstJobHandle burstHandle = inputDeps as BurstJobHandle;            if (burstHandle == null)                return inputDeps;

            burstHandle.jobHandle = FindFluidParticles(burstHandle.jobHandle);            burstHandle.jobHandle = GenerateContacts(burstHandle.jobHandle, stepTime);            return burstHandle;         }        protected JobHandle FindFluidParticles(JobHandle inputDeps)        {            var d = constraints[(int)Oni.ConstraintType.Density] as BurstDensityConstraints;            // Update positions:            var findFluidJob = new FindFluidParticlesJob()            {                activeParticles = activeParticles,                phases = phases,                fluidParticles = d.fluidParticles,            };            return findFluidJob.Schedule(inputDeps);        }        protected JobHandle GenerateContacts(JobHandle inputDeps, float deltaTime)        {            // Dispose of previous fluid interactions.            // We need fluid data during interpolation, for anisotropic fluid particles. For this reason,            // we can't dispose of these arrays in ResetForces() at the end of each full step. They must use persistent allocation.            if (fluidInteractions.IsCreated)                fluidInteractions.Dispose();            if (fluidBatchData.IsCreated)                fluidBatchData.Dispose();
            if (particleBatchData.IsCreated)                particleBatchData.Dispose();

            // get constraint parameters for constraint types that depend on broadphases:
            var collisionParameters = abstraction.GetConstraintParameters(Oni.ConstraintType.Collision);            var particleCollisionParameters = abstraction.GetConstraintParameters(Oni.ConstraintType.ParticleCollision);            var densityParameters = abstraction.GetConstraintParameters(Oni.ConstraintType.Density);            // if no enabled constraints that require broadphase info, skip it entirely.            if (collisionParameters.enabled ||                particleCollisionParameters.enabled ||                densityParameters.enabled)            {
                // generate particle-particle and particle-collider interactions in parallel:
                JobHandle generateParticleInteractionsHandle = inputDeps, generateContactsHandle = inputDeps;                // particle-particle interactions (contacts, fluids)                if (particleCollisionParameters.enabled || densityParameters.enabled)                {                    particleGrid.Update(this, inputDeps);                    generateParticleInteractionsHandle = particleGrid.GenerateContacts(this, deltaTime);                }                // particle-collider interactions (contacts)                if (collisionParameters.enabled)                {                    generateContactsHandle = colliderGrid.GenerateContacts(this, deltaTime, inputDeps);                }                JobHandle.CombineDependencies(generateParticleInteractionsHandle, generateContactsHandle).Complete();

                // allocate arrays for interactions and batch data:                particleBatchData = new NativeArray<BatchData>(maxBatches, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);                fluidInteractions = new NativeArray<FluidInteraction>(particleGrid.fluidInteractionQueue.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);                fluidBatchData = new NativeArray<BatchData>(maxBatches, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                // allocate effective mass arrays:
                abstraction.contactEffectiveMasses.ResizeUninitialized(colliderGrid.colliderContactQueue.Count);
                abstraction.particleContactEffectiveMasses.ResizeUninitialized(particleGrid.particleContactQueue.Count);

                // dequeue contacts/interactions into temporary arrays:
                var rawParticleContacts = new NativeArray<BurstContact>(particleGrid.particleContactQueue.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);                var sortedParticleContacts = new NativeArray<BurstContact>(particleGrid.particleContactQueue.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);                var rawFluidInteractions = new NativeArray<FluidInteraction>(particleGrid.fluidInteractionQueue.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                abstraction.particleContacts.ResizeUninitialized(particleGrid.particleContactQueue.Count);                DequeueIntoArrayJob<BurstContact> dequeueParticleContacts = new DequeueIntoArrayJob<BurstContact>                {                    InputQueue = particleGrid.particleContactQueue,                    OutputArray = rawParticleContacts                };

                abstraction.colliderContacts.ResizeUninitialized(colliderGrid.colliderContactQueue.Count);                DequeueIntoArrayJob<BurstContact> dequeueColliderContacts = new DequeueIntoArrayJob<BurstContact>                {                    InputQueue = colliderGrid.colliderContactQueue,                    OutputArray = abstraction.colliderContacts.AsNativeArray<BurstContact>()                };                DequeueIntoArrayJob<FluidInteraction> dequeueFluidInteractions = new DequeueIntoArrayJob<FluidInteraction>                {                    InputQueue = particleGrid.fluidInteractionQueue,                    OutputArray = rawFluidInteractions                };                var dequeueHandle = JobHandle.CombineDependencies(dequeueParticleContacts.Schedule(), dequeueFluidInteractions.Schedule(), dequeueColliderContacts.Schedule());                // Sort contacts for jitter-free gauss-seidel (sequential) solving:                dequeueHandle = contactSorter.SortConstraints(simplexCounts.simplexCount, rawParticleContacts, ref sortedParticleContacts, dequeueHandle);                 ContactProvider contactProvider = new ContactProvider()                {                    contacts = sortedParticleContacts,                    sortedContacts = abstraction.particleContacts.AsNativeArray<BurstContact>(),                    simplices = simplices,                    simplexCounts = simplexCounts                };                FluidInteractionProvider fluidProvider = new FluidInteractionProvider()                {                    interactions = rawFluidInteractions,                    sortedInteractions = fluidInteractions,                };                // batch particle contacts:                var activeParticleBatchCount = new NativeArray<int>(1, Allocator.TempJob);                var particleBatchHandle = collisionConstraintBatcher.BatchConstraints(ref contactProvider, particleCount, ref particleBatchData, ref activeParticleBatchCount, dequeueHandle);                // batch fluid interactions:                var activeFluidBatchCount = new NativeArray<int>(1, Allocator.TempJob);                var fluidBatchHandle = fluidConstraintBatcher.BatchConstraints(ref fluidProvider, particleCount, ref fluidBatchData, ref activeFluidBatchCount, dequeueHandle);                JobHandle.CombineDependencies(particleBatchHandle, fluidBatchHandle).Complete();                // Generate particle contact/friction batches:                var pc = constraints[(int)Oni.ConstraintType.ParticleCollision] as BurstParticleCollisionConstraints;                var pf = constraints[(int)Oni.ConstraintType.ParticleFriction] as BurstParticleFrictionConstraints;                for (int i = 0; i < pc.batches.Count; ++i)                    pc.batches[i].enabled = false;                for (int i = 0; i < pf.batches.Count; ++i)                    pf.batches[i].enabled = false;                for (int i = 0; i < activeParticleBatchCount[0]; ++i)                {                    // create extra batches if not enough:                    if (i == pc.batches.Count)                    {                        pc.CreateConstraintsBatch();                        pf.CreateConstraintsBatch();                    }                    pc.batches[i].enabled = true;                    pf.batches[i].enabled = true;                    (pc.batches[i] as BurstParticleCollisionConstraintsBatch).batchData = particleBatchData[i];                    (pf.batches[i] as BurstParticleFrictionConstraintsBatch ).batchData = particleBatchData[i];                }                // Generate fluid interaction batches:                var dc = constraints[(int)Oni.ConstraintType.Density] as BurstDensityConstraints;                for (int i = 0; i < dc.batches.Count; ++i)                    dc.batches[i].enabled = false;                for (int i = 0; i < activeFluidBatchCount[0]; ++i)                {                    // create extra batches if not enough:                    if (i == dc.batches.Count)                        dc.CreateConstraintsBatch();                    dc.batches[i].enabled = true;                    (dc.batches[i] as BurstDensityConstraintsBatch).batchData = fluidBatchData[i];                }                // dispose of temporary buffers:                rawParticleContacts.Dispose();                rawFluidInteractions.Dispose();                sortedParticleContacts.Dispose();                activeParticleBatchCount.Dispose();                activeFluidBatchCount.Dispose();                inputDeps = colliderGrid.ApplyForceZones(this, deltaTime, inputDeps);            }            return inputDeps;        }        public IObiJobHandle Substep(IObiJobHandle handle, float stepTime, float substepTime, int steps, float timeLeft)        {            BurstJobHandle burstHandle = handle as BurstJobHandle;            if (burstHandle == null)                return handle;

            // Apply aerodynamics
            burstHandle.jobHandle = constraints[(int)Oni.ConstraintType.Aerodynamics].Project(burstHandle.jobHandle, stepTime, substepTime, steps, timeLeft);

            // Predict positions:
            var predictPositions = new PredictPositionsJob()            {                activeParticles = activeParticles,                phases = phases,                buoyancies = fluidInterface,                externalForces = externalForces,                inverseMasses = invMasses,                positions = positions,                previousPositions = prevPositions,                velocities = velocities,                externalTorques = externalTorques,                inverseRotationalMasses = invRotationalMasses,                orientations = orientations,                previousOrientations = prevOrientations,                angularVelocities = angularVelocities,                gravity = new float4(abstraction.parameters.gravity, 0),                deltaTime = substepTime,                is2D = abstraction.parameters.mode == Oni.SolverParameters.Mode.Mode2D            };            burstHandle.jobHandle = predictPositions.Schedule(activeParticles.Length, 128, burstHandle.jobHandle);

            // Project position constraints:
            burstHandle.jobHandle = ApplyConstraints(burstHandle.jobHandle, stepTime, substepTime, steps, timeLeft);            // Update velocities:            var updateVelocitiesJob = new UpdateVelocitiesJob            {                activeParticles = activeParticles,                inverseMasses = invMasses,                previousPositions = prevPositions,                positions = positions,                velocities = velocities,                inverseRotationalMasses = invRotationalMasses,                previousOrientations = prevOrientations,                orientations = orientations,                angularVelocities = angularVelocities,                deltaTime = substepTime,                is2D = abstraction.parameters.mode == Oni.SolverParameters.Mode.Mode2D            };            burstHandle.jobHandle = updateVelocitiesJob.Schedule(activeParticles.Length, 128, burstHandle.jobHandle);

            // velocity constraints:
            burstHandle.jobHandle = ApplyVelocityCorrections(burstHandle.jobHandle, substepTime);            // Update positions:            var updatePositionsJob = new UpdatePositionsJob            {                activeParticles = activeParticles,                positions = positions,                previousPositions = prevPositions,                velocities = velocities,                orientations = orientations,                previousOrientations = prevOrientations,                angularVelocities = angularVelocities,                velocityScale = math.pow(1 - math.saturate(abstraction.parameters.damping), substepTime),                sleepThreshold = abstraction.parameters.sleepThreshold,                maxVelocity = abstraction.parameters.maxVelocity,                maxAngularVelocity = abstraction.parameters.maxAngularVelocity            };            burstHandle.jobHandle = updatePositionsJob.Schedule(activeParticles.Length, 128, burstHandle.jobHandle);

            // Update diffuse particles:
            int substepsLeft = (int)math.round(timeLeft / substepTime); 
            int foamPadding = (int)math.ceil(abstraction.substeps / (float)abstraction.foamSubsteps);

            if (substepsLeft % foamPadding == 0)
                burstHandle.jobHandle = UpdateDiffuseParticles(burstHandle.jobHandle, substepTime * foamPadding);            return burstHandle;        }        private JobHandle ApplyVelocityCorrections(JobHandle inputDeps, float deltaTime)        {            var densityParameters = abstraction.GetConstraintParameters(Oni.ConstraintType.Density);            if (densityParameters.enabled)            {                var d = constraints[(int)Oni.ConstraintType.Density] as BurstDensityConstraints;                if (d != null)                {                    return d.ApplyVelocityCorrections(inputDeps, deltaTime);                }            }            return inputDeps;        }        private JobHandle ApplyConstraints(JobHandle inputDeps, float stepTime, float substepTime, int steps, float timeLeft)        {            // calculate max amount of iterations required, and initialize constraints..            int maxIterations = 0;            for (int i = 0; i < Oni.ConstraintTypeCount; ++i)            {                var parameters = abstraction.GetConstraintParameters((Oni.ConstraintType)i);                if (parameters.enabled)                {                    maxIterations = math.max(maxIterations, parameters.iterations);                    inputDeps = constraints[i].Initialize(inputDeps, substepTime);                }            }            // calculate iteration paddings:            for (int i = 0; i < Oni.ConstraintTypeCount; ++i)            {                var parameters = abstraction.GetConstraintParameters((Oni.ConstraintType)i);                if (parameters.enabled && parameters.iterations > 0)                    padding[i] = (int)math.ceil(maxIterations / (float)parameters.iterations);                else                    padding[i] = maxIterations;            }            // perform projection iterations:            for (int i = 1; i < maxIterations; ++i)            {                for (int j = 0; j < Oni.ConstraintTypeCount; ++j)                {                    if (j != (int)Oni.ConstraintType.Aerodynamics)                    {                        var parameters = abstraction.GetConstraintParameters((Oni.ConstraintType)j);                        if (parameters.enabled && i % padding[j] == 0)                            inputDeps = constraints[j].Project(inputDeps, stepTime, substepTime, steps, timeLeft);                    }                }            }            // final iteration, all groups together:            for (int i = 0; i < Oni.ConstraintTypeCount; ++i)            {                if (i != (int)Oni.ConstraintType.Aerodynamics)                {                    var parameters = abstraction.GetConstraintParameters((Oni.ConstraintType)i);                    if (parameters.enabled && parameters.iterations > 0)                        inputDeps = constraints[i].Project(inputDeps, stepTime, substepTime, steps, timeLeft);                }            }            // Despite friction constraints being applied after collision (since coulomb friction depends on normal impulse)            // we perform a collision iteration right at the end to ensure the final state meets the Signorini-Fichera conditions.            var param = abstraction.GetConstraintParameters(Oni.ConstraintType.ParticleCollision);            if (param.enabled && param.iterations > 0)                inputDeps = constraints[(int)Oni.ConstraintType.ParticleCollision].Project(inputDeps, stepTime, substepTime, steps, timeLeft);            param = abstraction.GetConstraintParameters(Oni.ConstraintType.Collision);            if (param.enabled && param.iterations > 0)                inputDeps = constraints[(int)Oni.ConstraintType.Collision].Project(inputDeps, stepTime, substepTime, steps, timeLeft);            return inputDeps;        }        public IObiJobHandle ApplyInterpolation(IObiJobHandle inputDeps, ObiNativeVector4List startPositions, ObiNativeQuaternionList startOrientations, float stepTime, float unsimulatedTime)        {            if (inputDeps == null)                inputDeps = new BurstJobHandle();            BurstJobHandle burstHandle = inputDeps as BurstJobHandle;            if (burstHandle == null)                return inputDeps;            // Interpolate particle positions and orientations.            var interpolate = new InterpolationJob            {                positions = positions,                endPositions = abstraction.endPositions.AsNativeArray<float4>(),                startPositions = startPositions.AsNativeArray<float4>(),                renderablePositions = renderablePositions,                orientations = orientations,                endOrientations = abstraction.endOrientations.AsNativeArray<quaternion>(),                startOrientations = startOrientations.AsNativeArray<quaternion>(),                renderableOrientations = renderableOrientations,                principalRadii = principalRadii,                renderableRadii = renderableRadii,

                blendFactor = stepTime > 0 ? unsimulatedTime / stepTime : 0,                interpolationMode = abstraction.parameters.interpolation            };            burstHandle.jobHandle = interpolate.Schedule(abstraction.positions.count, 128, burstHandle.jobHandle);            // Update deformable triangle normals            var resetNormals = new ResetNormals()            {                phases = phases,                normals = normals,                tangents = tangents            };            burstHandle.jobHandle = resetNormals.Schedule(normals.Length, 128, burstHandle.jobHandle);            // Update deformable triangle normals            var updateTriNormals = new UpdateTriangleNormalsJob()            {                renderPositions = renderablePositions,                deformableTriangles = deformableTriangles,                deformableTriangleUVs = deformableUVs,                normals = normals,                tangents = tangents            };            burstHandle.jobHandle = updateTriNormals.Schedule(deformableTriangles.Length / 3, 1, burstHandle.jobHandle);

            // Update deformable edge normals
            var updateEdgeNormals = new UpdateEdgeNormalsJob()            {                renderPositions = renderablePositions,                deformableEdges = deformableEdges,                wind = wind,                normals = normals,            };            burstHandle.jobHandle = updateEdgeNormals.Schedule(deformableEdges.Length / 2, 1, burstHandle.jobHandle);

            // Update deformable triangle orientations
            var updateOrientations = new RenderableOrientationFromNormals()            {                phases = phases,                normals = normals,                tangents = tangents,                renderableOrientations = renderableOrientations            };            burstHandle.jobHandle = updateOrientations.Schedule(normals.Length, 128, burstHandle.jobHandle);

            //make sure density constraints are enabled, otherwise particles have no neighbors and neighbor lists will be uninitialized.
            var param = abstraction.GetConstraintParameters(Oni.ConstraintType.Density);            if (param.enabled && param.iterations > 0)
            {
                // Fluid laplacian/anisotropy (only if we're in play mode, in-editor we have no particlegrid/sorted data).
                var d = constraints[(int)Oni.ConstraintType.Density] as BurstDensityConstraints;
                if (Application.isPlaying && d != null)
                    burstHandle.jobHandle = d.CalculateAnisotropyLaplacianSmoothing(burstHandle.jobHandle);            }            return burstHandle;        }        private unsafe JobHandle UpdateDiffuseParticles(JobHandle inputDeps, float deltaTime)
        {
            var system = abstraction.GetRenderSystem<ObiFoamGenerator>() as BurstFoamRenderSystem;
            if (system != null)
            {
                int* dispatchPtr = (int*)abstraction.foamCount.AddressOfElement(0);

                for (int i = 0; i < system.renderers.Count; ++i)
                {
                    var emitJob = new EmitParticlesJob
                    {
                        // when the actor gets removed from solver, solverIndices is destroyed and
                        // this job may still be running. As a solution, create a temporary copy of the array.
                        activeParticles = new NativeArray<int>(system.renderers[i].actor.solverIndices.AsNativeArray<int>(), Allocator.TempJob), 
                        positions = prevPositions,
                        velocities = velocities,
                        angularVelocities = angularVelocities,
                        principalRadii = principalRadii,

                        outputPositions = abstraction.foamPositions.AsNativeArray<float4>(),
                        outputVelocities = abstraction.foamVelocities.AsNativeArray<float4>(),
                        outputColors = abstraction.foamColors.AsNativeArray<float4>(),
                        outputAttributes = abstraction.foamAttributes.AsNativeArray<float4>(),

                        dispatchBuffer = abstraction.foamCount.AsNativeArray<int>(),

                        vorticityRange = system.renderers[i].vorticityRange,
                        velocityRange = system.renderers[i].velocityRange,
                        foamGenerationRate = system.renderers[i].foamGenerationRate,
                        potentialIncrease = system.renderers[i].foamPotential,
                        potentialDiffusion = math.pow(1 - math.saturate(system.renderers[i].foamPotentialDiffusion), deltaTime),
                        buoyancy = system.renderers[i].buoyancy,
                        drag = system.renderers[i].drag,
                        airdrag = math.pow(1 - math.saturate(system.renderers[i].atmosphericDrag), deltaTime),
                        isosurface = system.renderers[i].isosurface,
                        airAging = system.renderers[i].airAging,
                        particleSize = system.renderers[i].size,
                        sizeRandom = system.renderers[i].sizeRandom,
                        lifetime = system.renderers[i].lifetime,
                        lifetimeRandom = system.renderers[i].lifetimeRandom,
                        foamColor = (Vector4)system.renderers[i].color,

                        deltaTime = deltaTime
                    };

                    inputDeps = emitJob.Schedule(system.renderers[i].actor.activeParticleCount, 128, inputDeps);
                }

                var updateJob = new UpdateParticlesJob
                {
                    positions = prevPositions,
                    orientations = renderableOrientations,
                    principalRadii = renderableRadii,
                    velocities = velocities,
                    fluidData = fluidData,
                    fluidMaterial = fluidMaterials,

                    simplices = simplices,
                    simplexCounts = simplexCounts,

                    grid = particleGrid.grid,
                    gridLevels = particleGrid.grid.populatedLevels.GetKeyArray(Allocator.TempJob),

                    densityKernel = new Poly6Kernel(abstraction.parameters.mode == Oni.SolverParameters.Mode.Mode2D),

                    inputPositions = abstraction.foamPositions.AsNativeArray<float4>(),
                    inputVelocities = abstraction.foamVelocities.AsNativeArray<float4>(),
                    inputColors = abstraction.foamColors.AsNativeArray<float4>(),
                    inputAttributes = abstraction.foamAttributes.AsNativeArray<float4>(),

                    outputPositions = auxPositions,
                    outputVelocities = auxVelocities,
                    outputColors = auxColors,
                    outputAttributes = auxAttributes,

                    dispatchBuffer = abstraction.foamCount.AsNativeArray<int>(),

                    parameters = abstraction.parameters,

                    agingOverPopulation = new Vector3(abstraction.foamAccelAgingRange.x, abstraction.foamAccelAgingRange.y, abstraction.foamAccelAging),
                    currentAliveParticles = dispatchPtr[3],
                    deltaTime = deltaTime
                };

                inputDeps = IJobParallelForDeferExtensions.Schedule(updateJob, &dispatchPtr[3], 64, inputDeps);

                var copyJob = new CopyJob
                {
                    inputPositions = auxPositions,
                    inputVelocities = auxVelocities,
                    inputColors = auxColors,
                    inputAttributes = auxAttributes,

                    outputPositions = abstraction.foamPositions.AsNativeArray<float4>(),
                    outputVelocities = abstraction.foamVelocities.AsNativeArray<float4>(),
                    outputColors = abstraction.foamColors.AsNativeArray<float4>(),
                    outputAttributes = abstraction.foamAttributes.AsNativeArray<float4>(),

                    dispatchBuffer = abstraction.foamCount.AsNativeArray<int>()
                };

                inputDeps = IJobParallelForDeferExtensions.Schedule(copyJob, &dispatchPtr[7], 256, inputDeps);

                activeFoamParticleCount = (uint)dispatchPtr[3];
            }
            return inputDeps;
        }        public void SpatialQuery(ObiNativeQueryShapeList shapes, ObiNativeAffineTransformList transforms, ObiNativeQueryResultList results)        {            var resultsQueue = new NativeQueue<BurstQueryResult>(Allocator.Persistent);            particleGrid.SpatialQuery(this,                                      shapes.AsNativeArray<BurstQueryShape>(),                                      transforms.AsNativeArray<BurstAffineTransform>(),                                      resultsQueue).Complete();            int count = resultsQueue.Count;            results.ResizeUninitialized(count);            var dequeueQueryResults = new DequeueIntoArrayJob<BurstQueryResult>()            {                InputQueue = resultsQueue,                OutputArray = results.AsNativeArray<BurstQueryResult>()            };            dequeueQueryResults.Schedule().Complete();            resultsQueue.Dispose();        }        public int GetParticleGridSize()        {            return particleGrid.grid.usedCells.Length;        }        public void GetParticleGrid(ObiNativeAabbList cells)        {            particleGrid.GetCells(cells);        }    }}
#endif


