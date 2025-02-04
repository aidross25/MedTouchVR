using UnityEngine;
using UnityEngine.Rendering;

namespace Obi
{
    public class ComputeSolverImpl : ISolverImpl
    {
        ObiSolver m_Solver;

        public ObiSolver abstraction
        {
            get { return m_Solver; }
        }

        public int particleCount
        {
            get { return m_Solver.positions.count; }
        }

        public int activeParticleCount
        {
            get { return m_Solver.activeParticles.count; }
        }

        public int deformableTriangleCount
        {
            get { return m_Solver.deformableTriangles.count / 3; }
        }

        public int deformableEdgeCount
        {
            get { return m_Solver.deformableEdges.count / 2; }
        }

        public InertialFrame inertialFrame
        {
            get { return m_InertialFrame; }
        }

        public uint activeFoamParticleCount { private set; get; }

        // Per-type constraints array:
        IComputeConstraintsImpl[] constraints;

        // Per-type iteration padding array:
        private int[] padding = new int[Oni.ConstraintTypeCount];

        // job handle:
        private ComputeJobHandle jobHandle;

        // particle contact generation:
        public ComputeParticleGrid particleGrid;

        // collider contact generation:
        public ComputeColliderWorld colliderGrid;

        // spatial queries:
        public SpatialQueries spatialQueries;

        // misc data:
        private InertialFrame m_InertialFrame;

        // cached particle data arrays (just wrappers over raw unmanaged data held by the abstract solver)
        public GraphicsBuffer positionsBuffer;
        public GraphicsBuffer orientationsBuffer;
        public GraphicsBuffer startPositionsBuffer;
        public GraphicsBuffer endPositionsBuffer;
        public GraphicsBuffer startOrientationsBuffer;
        public GraphicsBuffer endOrientationsBuffer;
        public GraphicsBuffer restPositionsBuffer;
        public GraphicsBuffer prevPositionsBuffer;
        public GraphicsBuffer restOrientationsBuffer;
        public GraphicsBuffer prevOrientationsBuffer;
        public GraphicsBuffer renderablePositionsBuffer;
        public GraphicsBuffer renderableOrientationsBuffer;
        public GraphicsBuffer renderableRadiiBuffer;
        public GraphicsBuffer colorsBuffer;
        public GraphicsBuffer collisionMaterialIndexBuffer;

        public GraphicsBuffer principalRadiiBuffer;
        public GraphicsBuffer velocitiesBuffer;
        public GraphicsBuffer invMassesBuffer;
        public GraphicsBuffer phasesBuffer;
        public GraphicsBuffer filtersBuffer;

        public GraphicsBuffer angularVelocitiesBuffer;
        public GraphicsBuffer invRotationalMassesBuffer;
        public GraphicsBuffer externalForcesBuffer;
        public GraphicsBuffer externalTorquesBuffer;
        public GraphicsBuffer windBuffer;

        public GraphicsBuffer lifeBuffer;
        public GraphicsBuffer fluidDataBuffer;
        public GraphicsBuffer userDataBuffer;
        public GraphicsBuffer fluidMaterialsBuffer;
        public GraphicsBuffer fluidInterfaceBuffer;
        public GraphicsBuffer anisotropiesBuffer;

        public GraphicsBuffer auxPositions;
        public GraphicsBuffer auxVelocities;
        public GraphicsBuffer auxColors;
        public GraphicsBuffer auxAttributes;
        public GraphicsBuffer auxOffsetInCell;
        public GraphicsBuffer auxSortedToOriginal;

        public GraphicsBuffer normalsBuffer;
        public GraphicsBuffer cellCoordsBuffer;

        public GraphicsBuffer positionDeltasIntBuffer;
        public GraphicsBuffer orientationDeltasIntBuffer;

        public GraphicsBuffer positionConstraintCountBuffer;
        public GraphicsBuffer orientationConstraintCountBuffer;

        public GraphicsBuffer activeParticlesBuffer;
        public GraphicsBuffer fluidDispatchBuffer;

        public GraphicsBuffer normalsIntBuffer;
        public GraphicsBuffer tangentsIntBuffer;

        public GraphicsBuffer solverToWorldBuffer;
        public GraphicsBuffer worldToSolverBuffer;
        public GraphicsBuffer inertialFrameBuffer;
        private AffineTransform[] solverToWorldArray;
        private AffineTransform[] worldToSolverArray;
        private InertialFrame[] inertialFrameArray;

        public GraphicsBuffer rigidbodyLinearDeltasBuffer;
        public GraphicsBuffer rigidbodyAngularDeltasBuffer;

        public GraphicsBuffer rigidbodyLinearDeltasIntBuffer;
        public GraphicsBuffer rigidbodyAngularDeltasIntBuffer;

        public GraphicsBuffer reducedBounds;

        // simplices:
        public SimplexCounts simplexCounts;
        public GraphicsBuffer simplices;
        public GraphicsBuffer simplexBounds;

        public Aabb solverBounds;
        private AsyncGPUReadbackRequest boundsRequest;

        private ComputeShader solverShader;
        private int applyInertialForcesKernel;
        private int applyRigidbodyDeltasKernel;
        private int storeStartStateKernel;
        private int predictPositionsKernel;
        private int updateVelocitiesKernel;
        private int updatePositionsKernel;
        private int interpolateKernel;

        private ComputeShader boundsShader;
        private int simplexBoundsKernel;
        private int editSimplexBoundsKernel;
        private int boundsReductionKernel;

        private ComputeShader deformableTrisShader;
        private int resetNormalsKernel;
        private int updateNormalsKernel;
        private int updateEdgeNormalsKernel;
        private int orientationFromNormalsKernel;

        private ComputeShader foamShader;
        private int sortDataKernel;
        private int emitFoamKernel;
        private int copyAliveKernel;
        private int updateFoamKernel;
        private int copyKernel;

        private ComputeShader foamDensityShader;
        private int clearGridKernel;
        private int insertGridKernel;
        private int sortByGridKernel;
        private int computeDensityKernel;
        private int applyDensityKernel;

        public ComputeSolverImpl(ObiSolver solver)
        {
            this.m_Solver = solver;

            jobHandle = new ComputeJobHandle();
            solverBounds = new Aabb(solver.transform.position - Vector3.one, solver.transform.position + Vector3.one);

            solver.queryResults.ResizeUninitialized((int)abstraction.maxQueryResults);
            solver.queryResults.SafeAsComputeBuffer<QueryResult>(GraphicsBuffer.Target.Counter);

            solver.foamCount.AsComputeBuffer<int>(GraphicsBuffer.Target.IndirectArguments);
            solver.foamPositions.AsComputeBuffer<Vector4>();
            solver.foamVelocities.AsComputeBuffer<Vector4>();
            solver.foamColors.AsComputeBuffer<Vector4>();
            solver.foamAttributes.AsComputeBuffer<Vector4>();

            solverShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/Solver"));
            applyInertialForcesKernel = solverShader.FindKernel("ApplyInertialForces");
            applyRigidbodyDeltasKernel = solverShader.FindKernel("ApplyRigidbodyDeltas");
            storeStartStateKernel = solverShader.FindKernel("StoreStartState");
            predictPositionsKernel = solverShader.FindKernel("PredictPositions");
            updateVelocitiesKernel = solverShader.FindKernel("UpdateVelocities");
            updatePositionsKernel = solverShader.FindKernel("UpdatePositions");
            interpolateKernel = solverShader.FindKernel("Interpolate");

            boundsShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/BoundsReduction"));
            simplexBoundsKernel = boundsShader.FindKernel("RuntimeSimplexBounds");
            editSimplexBoundsKernel = boundsShader.FindKernel("EditSimplexBounds");
            boundsReductionKernel = boundsShader.FindKernel("Reduce");

            deformableTrisShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/DeformableTriangles"));
            resetNormalsKernel = deformableTrisShader.FindKernel("ResetNormals");
            updateNormalsKernel = deformableTrisShader.FindKernel("UpdateNormals");
            updateEdgeNormalsKernel = deformableTrisShader.FindKernel("UpdateEdgeNormals");
            orientationFromNormalsKernel = deformableTrisShader.FindKernel("OrientationFromNormals");

            foamShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/FluidFoam"));
            sortDataKernel = foamShader.FindKernel("SortFluidData");
            emitFoamKernel = foamShader.FindKernel("Emit");
            copyAliveKernel = foamShader.FindKernel("CopyAliveCount");
            updateFoamKernel = foamShader.FindKernel("Update");
            copyKernel = foamShader.FindKernel("Copy");

            foamDensityShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/FluidFoamDensity"));
            clearGridKernel = foamDensityShader.FindKernel("Clear");
            insertGridKernel = foamDensityShader.FindKernel("InsertInGrid");
            sortByGridKernel = foamDensityShader.FindKernel("SortByGrid");
            computeDensityKernel = foamDensityShader.FindKernel("ComputeDensity");
            applyDensityKernel = foamDensityShader.FindKernel("ApplyDensity");

            solverToWorldBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 48);
            solverToWorldArray = new AffineTransform[1];
            worldToSolverBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 48);
            worldToSolverArray = new AffineTransform[1];
            inertialFrameBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 160);
            inertialFrameArray = new InertialFrame[1];

            fluidDispatchBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 4, sizeof(uint));

            // Initialize collision world:
            GetOrCreateColliderWorld();
            colliderGrid.IncreaseReferenceCount();

            // Initialize contact generation acceleration structure:
            particleGrid = new ComputeParticleGrid();

            // Initialize spatial query system.
            spatialQueries = new SpatialQueries(solver.maxQueryResults);

            // Initialize constraint arrays:
            constraints = new IComputeConstraintsImpl[Oni.ConstraintTypeCount];
            constraints[(int)Oni.ConstraintType.Tether] = new ComputeTetherConstraints(this);
            constraints[(int)Oni.ConstraintType.Volume] = new ComputeVolumeConstraints(this);
            constraints[(int)Oni.ConstraintType.Chain] = new ComputeChainConstraints(this);
            constraints[(int)Oni.ConstraintType.Bending] = new ComputeBendConstraints(this);
            constraints[(int)Oni.ConstraintType.Distance] = new ComputeDistanceConstraints(this);
            constraints[(int)Oni.ConstraintType.ShapeMatching] = new ComputeShapeMatchingConstraints(this);
            constraints[(int)Oni.ConstraintType.BendTwist] = new ComputeBendTwistConstraints(this);
            constraints[(int)Oni.ConstraintType.StretchShear] = new ComputeStretchShearConstraints(this);
            constraints[(int)Oni.ConstraintType.Pin] = new ComputePinConstraints(this);
            constraints[(int)Oni.ConstraintType.Skin] = new ComputeSkinConstraints(this);
            constraints[(int)Oni.ConstraintType.Aerodynamics] = new ComputeAerodynamicConstraints(this);
            constraints[(int)Oni.ConstraintType.Stitch] = new ComputeStitchConstraints(this);

            constraints[(int)Oni.ConstraintType.ParticleCollision] = new ComputeParticleCollisionConstraints(this);
            constraints[(int)Oni.ConstraintType.ParticleCollision].CreateConstraintsBatch();

            constraints[(int)Oni.ConstraintType.Collision] = new ComputeColliderCollisionConstraints(this);
            constraints[(int)Oni.ConstraintType.Collision].CreateConstraintsBatch();

            constraints[(int)Oni.ConstraintType.ParticleFriction] = new ComputeParticleFrictionConstraints(this);
            constraints[(int)Oni.ConstraintType.ParticleFriction].CreateConstraintsBatch();

            constraints[(int)Oni.ConstraintType.Friction] = new ComputeColliderFrictionConstraints(this);
            constraints[(int)Oni.ConstraintType.Friction].CreateConstraintsBatch();

            constraints[(int)Oni.ConstraintType.Density] = new ComputeDensityConstraints(this);
            constraints[(int)Oni.ConstraintType.Density].CreateConstraintsBatch();
        }

        public void Destroy()
        {

            reducedBounds?.Dispose();
            solverToWorldBuffer?.Dispose();
            worldToSolverBuffer?.Dispose();
            inertialFrameBuffer?.Dispose();
            fluidDispatchBuffer?.Dispose();

            for (int i = 0; i < constraints.Length; ++i)
                if (constraints[i] != null)
                    constraints[i].Dispose();

            // Get rid of particle and collider grids/queries:
            particleGrid?.Dispose();

            // cannot use null-coalescing because this is a GameObject, and Unity overrides !=
            if (colliderGrid != null)
                colliderGrid.DecreaseReferenceCount();

            spatialQueries?.Dispose();

            positionDeltasIntBuffer?.Dispose();
            orientationDeltasIntBuffer?.Dispose();

            rigidbodyLinearDeltasIntBuffer?.Dispose();
            rigidbodyAngularDeltasIntBuffer?.Dispose();

            normalsIntBuffer?.Dispose();
            tangentsIntBuffer?.Dispose();

            simplexBounds?.Dispose();

            auxPositions?.Dispose();
            auxVelocities?.Dispose();
            auxColors?.Dispose();
            auxAttributes?.Dispose();
            auxOffsetInCell?.Dispose();
            auxSortedToOriginal?.Dispose();
        }

        private void GetOrCreateColliderWorld()
        {
            colliderGrid = GameObject.FindObjectOfType<ComputeColliderWorld>();
            if (colliderGrid == null)
            {
                var world = new GameObject("ComputeCollisionWorld", typeof(ComputeColliderWorld));
                colliderGrid = world.GetComponent<ComputeColliderWorld>();
            }
        }

        public void PushData()
        {
            // Send data to the GPU:
            abstraction.positions.Upload();
            abstraction.orientations.Upload();
            abstraction.velocities.Upload();
            abstraction.angularVelocities.Upload();
            abstraction.colors.Upload();

            abstraction.startPositions.Upload();
            abstraction.startOrientations.Upload();
            abstraction.endPositions.Upload();
            abstraction.endOrientations.Upload();

            abstraction.restPositions.Upload();
            abstraction.restOrientations.Upload();
            abstraction.principalRadii.Upload();
            abstraction.invMasses.Upload();
            abstraction.invRotationalMasses.Upload();
            abstraction.phases.Upload();
            abstraction.filters.Upload();
            abstraction.externalForces.Upload();
            abstraction.externalTorques.Upload();
            abstraction.wind.WipeToValue(abstraction.parameters.ambientWind);
            abstraction.wind.Upload();

            abstraction.life.Upload();
            abstraction.fluidData.Upload();
            abstraction.userData.Upload();
            abstraction.fluidInterface.Upload();
            abstraction.fluidMaterials.Upload();

            rigidbodyLinearDeltasIntBuffer.SetData(abstraction.rigidbodyLinearDeltas.AsNativeArray<VInt4>());
            rigidbodyAngularDeltasIntBuffer.SetData(abstraction.rigidbodyAngularDeltas.AsNativeArray<VInt4>());
        }

        public void RequestReadback()
        {
            // Copy rigidbody deltas (int) to output buffers (float), then request readback:
            solverShader.SetBuffer(applyRigidbodyDeltasKernel, "linearDeltasAsInt", rigidbodyLinearDeltasIntBuffer);
            solverShader.SetBuffer(applyRigidbodyDeltasKernel, "angularDeltasAsInt", rigidbodyAngularDeltasIntBuffer);
            solverShader.SetBuffer(applyRigidbodyDeltasKernel, "linearDeltas", rigidbodyLinearDeltasBuffer);
            solverShader.SetBuffer(applyRigidbodyDeltasKernel, "angularDeltas", rigidbodyAngularDeltasBuffer);

            solverShader.SetInt("particleCount", abstraction.rigidbodyLinearDeltas.count);

            int threadGroups = ComputeMath.ThreadGroupCount(abstraction.rigidbodyLinearDeltas.count, 128);
            solverShader.Dispatch(applyRigidbodyDeltasKernel, threadGroups, 1, 1);

            abstraction.rigidbodyLinearDeltas.Readback();
            abstraction.rigidbodyAngularDeltas.Readback();

            // begin particle data async GPU -> CPU transfer.
            // by default, only positions and velocities are read back.
            // ObiActors can request whatever data they need in RequestReadback,
            // then wait for it in SimulationEnd.
            abstraction.positions.Readback();
            abstraction.velocities.Readback();

            // begin constraint data async GPU -> CPU transfer.
            var sm = constraints[(int)Oni.ConstraintType.ShapeMatching] as ComputeShapeMatchingConstraints;
            if (sm != null)
                sm.RequestDataReadback();

            // needed for tearing.
            var dm = constraints[(int)Oni.ConstraintType.Distance] as ComputeDistanceConstraints;
            if (dm != null)
                dm.RequestDataReadback();

            var pm = constraints[(int)Oni.ConstraintType.Pin] as ComputePinConstraints;
            if (pm != null)
                pm.RequestDataReadback();
        }

        public void InitializeFrame(Vector4 translation, Vector4 scale, Quaternion rotation)
        {
            m_InertialFrame = new InertialFrame(translation, scale, rotation);
        }

        public void UpdateFrame(Vector4 translation, Vector4 scale, Quaternion rotation, float deltaTime)
        {
            m_InertialFrame.Update(translation, scale, rotation, deltaTime);

            solverToWorldArray[0] = m_InertialFrame.frame;
            solverToWorldBuffer.SetData(solverToWorldArray);

            worldToSolverArray[0] = m_InertialFrame.frame.Inverse();
            worldToSolverBuffer.SetData(worldToSolverArray);

            inertialFrameArray[0] = m_InertialFrame;
            inertialFrameBuffer.SetData(inertialFrameArray);
        }

        public IObiJobHandle ApplyFrame(float worldLinearInertiaScale, float worldAngularInertiaScale, float deltaTime)
        {
            if (activeParticleCount > 0)
            {
                // inverse linear part:
                Matrix4x4 linear = Matrix4x4.TRS(Vector3.zero, inertialFrame.frame.rotation, new Vector3(1 / inertialFrame.frame.scale.x, 1 / inertialFrame.frame.scale.y, 1 / inertialFrame.frame.scale.z));
                Matrix4x4 linearInv = Matrix4x4.Transpose(linear);

                // non-inertial frame accelerations:
                Vector4 angularVel = (linearInv * Matrix4x4.Scale(inertialFrame.angularVelocity) * linear).Diagonal();
                Vector4 eulerAccel = (linearInv * Matrix4x4.Scale(inertialFrame.angularAcceleration) * linear).Diagonal();
                Vector4 inertialAccel = linearInv * inertialFrame.acceleration;

                int threadGroups = ComputeMath.ThreadGroupCount(activeParticleCount, 128);
                solverShader.SetInt("particleCount", activeParticleCount);

                solverShader.SetFloat("deltaTime", deltaTime);
                solverShader.SetFloat("worldLinearInertiaScale", abstraction.worldLinearInertiaScale);
                solverShader.SetFloat("worldAngularInertiaScale", abstraction.worldAngularInertiaScale);
                solverShader.SetVector("angularVel", angularVel);
                solverShader.SetVector("eulerAccel", eulerAccel);
                solverShader.SetVector("inertialAccel", inertialAccel);

                solverShader.SetBuffer(applyInertialForcesKernel, "activeParticles", activeParticlesBuffer);
                solverShader.SetBuffer(applyInertialForcesKernel, "positions", positionsBuffer);
                solverShader.SetBuffer(applyInertialForcesKernel, "velocities", velocitiesBuffer);
                solverShader.SetBuffer(applyInertialForcesKernel, "invMasses", invMassesBuffer);

                solverShader.Dispatch(applyInertialForcesKernel, threadGroups, 1, 1);
            }

            return jobHandle;
        }

        public void SetDeformableTriangles(ObiNativeIntList indices, ObiNativeVector2List uvs)
        {
            if (indices.count > 0)
            {
                var deformableTrianglesBuffer = indices.AsComputeBuffer<int>();
                var deformableUVsBuffer = uvs.AsComputeBuffer<Vector2>();

                deformableTrisShader.SetBuffer(updateNormalsKernel, "deformableTriangles", deformableTrianglesBuffer);
                deformableTrisShader.SetBuffer(updateNormalsKernel, "deformableTriangleUVs", deformableUVsBuffer);
                deformableTrisShader.SetInt("triangleCount", deformableTriangleCount);
            }
        }

        public void SetDeformableEdges(ObiNativeIntList indices)
        {
            if (indices.count > 0)
            {
                var deformableEdgesBuffer = indices.AsComputeBuffer<int>();

                deformableTrisShader.SetBuffer(updateEdgeNormalsKernel, "deformableEdges", deformableEdgesBuffer);
                deformableTrisShader.SetInt("edgeCount", deformableEdgeCount);
            }
        }

        public void SetSimplices(ObiNativeIntList simplices, SimplexCounts counts)
        {
            this.simplexCounts = counts;

            if (simplices.count > 0)
            {
                boundsShader.SetInt("pointCount", simplexCounts.pointCount);
                boundsShader.SetInt("edgeCount", simplexCounts.edgeCount);
                boundsShader.SetInt("triangleCount", simplexCounts.triangleCount);

                this.simplices = simplices.AsComputeBuffer<int>();
                cellCoordsBuffer = abstraction.cellCoords.AsComputeBuffer<VInt4>();

                if (simplexBounds == null || counts.simplexCount > simplexBounds.count)
                {
                    simplexBounds?.Dispose();
                    simplexBounds = new GraphicsBuffer(GraphicsBuffer.Target.Structured, counts.simplexCount * 2, 32);

                    reducedBounds?.Dispose();
                    reducedBounds = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ComputeMath.NextMultiple(counts.simplexCount * 2, 256), 32);
                }

                // Even though we usually store simplices for collision detection, the grid is reused for fluid meshing
                // so the capacity we set should be at least the total amount of particles in the solver.
                if (particleGrid != null)
                {
                    if (particleGrid.SetCapacity(Mathf.Max(counts.simplexCount, particleCount),
                                            (uint)Mathf.Max(1, abstraction.maxParticleContacts),
                                            (uint)Mathf.Max(1, abstraction.maxParticleNeighbors)))
                    {
                        // resize to maximum number of contacts:
                        abstraction.colliderContacts.ResizeUninitialized(particleGrid.contactPairs.count);
                        abstraction.colliderContacts.SafeAsComputeBuffer<Oni.Contact>(GraphicsBuffer.Target.Counter);

                        abstraction.particleContacts.ResizeUninitialized(particleGrid.contactPairs.count);
                        abstraction.particleContacts.SafeAsComputeBuffer<Oni.Contact>(GraphicsBuffer.Target.Counter);

                        abstraction.contactEffectiveMasses.ResizeUninitialized(particleGrid.contactPairs.count);
                        abstraction.contactEffectiveMasses.SafeAsComputeBuffer<ContactEffectiveMasses>();

                        abstraction.particleContactEffectiveMasses.ResizeUninitialized(particleGrid.contactPairs.count);
                        abstraction.particleContactEffectiveMasses.SafeAsComputeBuffer<ContactEffectiveMasses>();
                    }
                }
            }
            else
                this.simplices = null;
        }

        public void SetActiveParticles(ObiNativeIntList indices)
        {
            // TODO: indices.computebuffer has been deleted when adding an item. We now need to
            // update the compute buffer if needed.
            if (indices.computeBuffer == null || indices.computeBuffer.count != indices.capacity)
            {
                //Debug.Log("create");
                activeParticlesBuffer = indices.AsComputeBuffer<int>(indices.capacity);
            }
            else
            {
                //Debug.Log("update");
                indices.UploadFullCapacity(); //unmaps the entire memory buffer up to capacity.
            }

            if (activeParticlesBuffer != null)
            {
                solverShader.SetBuffer(predictPositionsKernel, "activeParticles", activeParticlesBuffer);
                solverShader.SetBuffer(updateVelocitiesKernel, "activeParticles", activeParticlesBuffer);
                solverShader.SetBuffer(updatePositionsKernel, "activeParticles", activeParticlesBuffer);
            }
        }

        public IObiJobHandle UpdateBounds(IObiJobHandle inputDeps, float stepTime)
        {
            if (activeParticleCount > 0 && reducedBounds != null)
            {
                boundsShader.SetFloat("deltaTime", stepTime);

                int boundsCount = simplexCounts.simplexCount;
                int threadGroups = ComputeMath.ThreadGroupCount(boundsCount, 256);

                // at edit time, the collision materials buffer will be null since
                // the collider world is not updated.
                if (colliderGrid.materialsBuffer != null)
                {
                    boundsShader.SetBuffer(simplexBoundsKernel, "simplexBounds", simplexBounds);
                    boundsShader.SetBuffer(simplexBoundsKernel, "simplices", simplices);
                    boundsShader.SetBuffer(simplexBoundsKernel, "reducedBounds", reducedBounds);
                    boundsShader.SetBuffer(simplexBoundsKernel, "activeParticles", activeParticlesBuffer);
                    boundsShader.SetBuffer(simplexBoundsKernel, "positions", positionsBuffer);
                    boundsShader.SetBuffer(simplexBoundsKernel, "velocities", velocitiesBuffer);
                    boundsShader.SetBuffer(simplexBoundsKernel, "principalRadii", principalRadiiBuffer);
                    boundsShader.SetBuffer(simplexBoundsKernel, "fluidMaterials", fluidMaterialsBuffer);
                    boundsShader.SetBuffer(simplexBoundsKernel, "collisionMaterials", colliderGrid.materialsBuffer);
                    boundsShader.SetBuffer(simplexBoundsKernel, "collisionMaterialIndices", collisionMaterialIndexBuffer);
                    boundsShader.Dispatch(simplexBoundsKernel, threadGroups, 1, 1);
                }
                else
                {
                    boundsShader.SetBuffer(editSimplexBoundsKernel, "simplexBounds", simplexBounds);
                    boundsShader.SetBuffer(editSimplexBoundsKernel, "simplices", simplices);
                    boundsShader.SetBuffer(editSimplexBoundsKernel, "reducedBounds", reducedBounds);
                    boundsShader.SetBuffer(editSimplexBoundsKernel, "activeParticles", activeParticlesBuffer);
                    boundsShader.SetBuffer(editSimplexBoundsKernel, "positions", positionsBuffer);
                    boundsShader.SetBuffer(editSimplexBoundsKernel, "velocities", velocitiesBuffer);
                    boundsShader.SetBuffer(editSimplexBoundsKernel, "principalRadii", principalRadiiBuffer);
                    boundsShader.SetBuffer(editSimplexBoundsKernel, "fluidMaterials", fluidMaterialsBuffer);
                    boundsShader.Dispatch(editSimplexBoundsKernel, threadGroups, 1, 1);
                }

                boundsShader.SetBuffer(boundsReductionKernel, "reducedBounds", reducedBounds);
                do
                {
                    boundsShader.Dispatch(boundsReductionKernel, threadGroups, 1, 1);
                    threadGroups = ComputeMath.ThreadGroupCount(boundsCount, 256);
                    boundsCount /= 256;
                }
                while (threadGroups > 1);

                boundsRequest = AsyncGPUReadback.Request(reducedBounds, 32, 0);
            }

            return inputDeps;
        }

        public void GetBounds(ref Vector3 min, ref Vector3 max)
        {
            // wait for last pending bounds async request:
            boundsRequest.WaitForCompletion();
            if (boundsRequest.done && !boundsRequest.hasError)
                solverBounds = boundsRequest.GetData<Aabb>(0)[0];

            min = solverBounds.min;
            max = solverBounds.max;
        }

        public int GetConstraintCount(Oni.ConstraintType type)
        {
            /*if ((int)type > 0 && (int)type < constraints.Length)
            {
                int count = 0;
                for (int i = 0; i < constraints[(int)type].Count; ++i)
                {
                    count += constraints[(int)type][i].GetConstraintCount();
                }
                return count;
            }
            return 0;*/
            return 0;
        }

        public void SetParameters(Oni.SolverParameters parameters)
        {
            // These should be better passed using a constant buffer, but constant buffers do not work in 2021 :(
            //https://issuetracker.unity3d.com/issues/compute-shader-is-not-using-defined-constants-when-setting-data-with-setconstantbuffer

            solverShader.SetInt("mode", (int)parameters.mode);
            solverShader.SetInt("interpolation", (int)parameters.interpolation);
            solverShader.SetVector("gravity", parameters.gravity);
            solverShader.SetFloat("damping", parameters.damping);
            solverShader.SetFloat("sleepThreshold", parameters.sleepThreshold);
            solverShader.SetFloat("collisionMargin", parameters.collisionMargin);
            solverShader.SetFloat("maxVelocity", parameters.maxVelocity);
            solverShader.SetFloat("maxAngularVelocity", parameters.maxAngularVelocity);
        }

        public void SetConstraintGroupParameters(Oni.ConstraintType type, ref Oni.ConstraintParameters parameters)
        {
            // No need to implement. This backend grabs parameters from the abstraction when it needs them.
        }

        public void ParticleCountChanged(ObiSolver solver)
        {
            colorsBuffer = abstraction.colors.AsComputeBuffer<Vector4>();
            positionsBuffer = abstraction.positions.AsComputeBuffer<Vector4>();
            orientationsBuffer = abstraction.orientations.AsComputeBuffer<Quaternion>();
            startPositionsBuffer = abstraction.startPositions.AsComputeBuffer<Vector4>();
            endPositionsBuffer = abstraction.endPositions.AsComputeBuffer<Vector4>();
            startOrientationsBuffer = abstraction.startOrientations.AsComputeBuffer<Quaternion>();
            endOrientationsBuffer = abstraction.endOrientations.AsComputeBuffer<Quaternion>();
            restPositionsBuffer = abstraction.restPositions.AsComputeBuffer<Vector4>();
            restOrientationsBuffer = abstraction.restOrientations.AsComputeBuffer<Vector4>();
            prevPositionsBuffer = abstraction.prevPositions.AsComputeBuffer<Vector4>();
            prevOrientationsBuffer = abstraction.prevOrientations.AsComputeBuffer<Quaternion>();
            renderablePositionsBuffer = abstraction.renderablePositions.AsComputeBuffer<Vector4>();
            renderableOrientationsBuffer = abstraction.renderableOrientations.AsComputeBuffer<Quaternion>();
            renderableRadiiBuffer = abstraction.renderableRadii.AsComputeBuffer<Vector4>();
            collisionMaterialIndexBuffer = abstraction.collisionMaterials.AsComputeBuffer<int>();

            angularVelocitiesBuffer = abstraction.angularVelocities.AsComputeBuffer<Vector4>();
            invRotationalMassesBuffer = abstraction.invRotationalMasses.AsComputeBuffer<float>();
            externalForcesBuffer = abstraction.externalForces.AsComputeBuffer<Vector4>();
            externalTorquesBuffer = abstraction.externalTorques.AsComputeBuffer<Vector4>();
            windBuffer = abstraction.wind.AsComputeBuffer<Vector4>();

            velocitiesBuffer = abstraction.velocities.AsComputeBuffer<Vector4>();
            principalRadiiBuffer = abstraction.principalRadii.AsComputeBuffer<Vector4>();
            invMassesBuffer = abstraction.invMasses.AsComputeBuffer<float>();
            phasesBuffer = abstraction.phases.AsComputeBuffer<int>();
            filtersBuffer = abstraction.filters.AsComputeBuffer<int>();

            lifeBuffer = abstraction.life.AsComputeBuffer<float>();
            fluidDataBuffer = abstraction.fluidData.AsComputeBuffer<Vector4>();
            userDataBuffer = abstraction.userData.AsComputeBuffer<Vector4>();
            fluidInterfaceBuffer = abstraction.fluidInterface.AsComputeBuffer<Vector4>();
            fluidMaterialsBuffer = abstraction.fluidMaterials.AsComputeBuffer<Vector4>();
            anisotropiesBuffer = abstraction.anisotropies.AsComputeBuffer<Matrix4x4>();

            normalsBuffer = abstraction.normals.AsComputeBuffer<Vector4>();
            positionConstraintCountBuffer = abstraction.positionConstraintCounts.AsComputeBuffer<int>();
            orientationConstraintCountBuffer = abstraction.orientationConstraintCounts.AsComputeBuffer<int>();

            if (positionDeltasIntBuffer != null)
            {
                positionDeltasIntBuffer.Dispose();
                positionDeltasIntBuffer = null;
            }

            if (abstraction.positionDeltas.count > 0)
            {
                positionDeltasIntBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, abstraction.positionDeltas.count, abstraction.positionDeltas.stride);
                positionDeltasIntBuffer.SetData(new Vector4[abstraction.positionDeltas.count]);
            }

            if (orientationDeltasIntBuffer != null)
            {
                orientationDeltasIntBuffer.Dispose();
                orientationDeltasIntBuffer = null;
            }

            if (abstraction.orientationDeltas.count > 0)
            {
                orientationDeltasIntBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, abstraction.orientationDeltas.count, abstraction.orientationDeltas.stride);
                orientationDeltasIntBuffer.SetData(new Vector4[abstraction.orientationDeltas.count]);
            }

            if (normalsIntBuffer != null)
            {
                normalsIntBuffer.Dispose();
                normalsIntBuffer = null;
            }

            if (tangentsIntBuffer != null)
            {
                tangentsIntBuffer.Dispose();
                tangentsIntBuffer = null;
            }

            if (abstraction.normals.count > 0)
            {
                var zeroes = new VInt4[abstraction.normals.count];
                normalsIntBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, abstraction.normals.count, abstraction.normals.stride);
                normalsIntBuffer.SetData(zeroes);

                tangentsIntBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, abstraction.normals.count, abstraction.normals.stride);
                tangentsIntBuffer.SetData(zeroes);
            }

            if (positionsBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "positions", positionsBuffer);
            if (prevPositionsBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "prevPositions", prevPositionsBuffer);
            if (orientationsBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "orientations", orientationsBuffer);
            if (prevOrientationsBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "prevOrientations", prevOrientationsBuffer);
            if (velocitiesBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "velocities", velocitiesBuffer);
            if (invMassesBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "invMasses", invMassesBuffer);
            if (angularVelocitiesBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "angularVelocities", angularVelocitiesBuffer);
            if (invRotationalMassesBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "invRotationalMasses", invRotationalMassesBuffer);
            if (externalForcesBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "externalForces", externalForcesBuffer);
            if (externalTorquesBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "externalTorques", externalTorquesBuffer);
            if (phasesBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "phases", phasesBuffer);
            if (fluidInterfaceBuffer != null) solverShader.SetBuffer(predictPositionsKernel, "buoyancies", fluidInterfaceBuffer);

            if (positionsBuffer != null) solverShader.SetBuffer(updateVelocitiesKernel, "positions", positionsBuffer);
            if (prevPositionsBuffer != null) solverShader.SetBuffer(updateVelocitiesKernel, "prevPositions", prevPositionsBuffer);
            if (orientationsBuffer != null) solverShader.SetBuffer(updateVelocitiesKernel, "orientations", orientationsBuffer);
            if (prevOrientationsBuffer != null) solverShader.SetBuffer(updateVelocitiesKernel, "prevOrientations", prevOrientationsBuffer);
            if (velocitiesBuffer != null) solverShader.SetBuffer(updateVelocitiesKernel, "velocities", velocitiesBuffer);
            if (angularVelocitiesBuffer != null) solverShader.SetBuffer(updateVelocitiesKernel, "angularVelocities", angularVelocitiesBuffer);
            if (invMassesBuffer != null) solverShader.SetBuffer(updateVelocitiesKernel, "invMasses", invMassesBuffer);
            if (invRotationalMassesBuffer != null) solverShader.SetBuffer(updateVelocitiesKernel, "invRotationalMasses", invRotationalMassesBuffer);

            if (positionsBuffer != null) solverShader.SetBuffer(updatePositionsKernel, "positions", positionsBuffer);
            if (prevPositionsBuffer != null) solverShader.SetBuffer(updatePositionsKernel, "prevPositions", prevPositionsBuffer);
            if (orientationsBuffer != null) solverShader.SetBuffer(updatePositionsKernel, "orientations", orientationsBuffer);
            if (prevOrientationsBuffer != null) solverShader.SetBuffer(updatePositionsKernel, "prevOrientations", prevOrientationsBuffer);
            if (velocitiesBuffer != null) solverShader.SetBuffer(updatePositionsKernel, "velocities", velocitiesBuffer);
            if (angularVelocitiesBuffer != null) solverShader.SetBuffer(updatePositionsKernel, "angularVelocities", angularVelocitiesBuffer);

            if (positionsBuffer != null) solverShader.SetBuffer(interpolateKernel, "positions", positionsBuffer);
            if (startPositionsBuffer != null) solverShader.SetBuffer(interpolateKernel, "R_startPositions", startPositionsBuffer);
            if (endPositionsBuffer != null) solverShader.SetBuffer(interpolateKernel, "R_endPositions", endPositionsBuffer);
            if (renderablePositionsBuffer != null) solverShader.SetBuffer(interpolateKernel, "renderablePositions", renderablePositionsBuffer);
            if (orientationsBuffer != null) solverShader.SetBuffer(interpolateKernel, "orientations", orientationsBuffer);
            if (startOrientationsBuffer != null) solverShader.SetBuffer(interpolateKernel, "R_startOrientations", startOrientationsBuffer);
            if (endOrientationsBuffer != null) solverShader.SetBuffer(interpolateKernel, "R_endOrientations", endOrientationsBuffer);
            if (renderableOrientationsBuffer != null) solverShader.SetBuffer(interpolateKernel, "renderableOrientations", renderableOrientationsBuffer);
            if (principalRadiiBuffer != null) solverShader.SetBuffer(interpolateKernel, "principalRadii", principalRadiiBuffer);
            if (renderableRadiiBuffer != null) solverShader.SetBuffer(interpolateKernel, "renderableRadii", renderableRadiiBuffer);

            if (positionsBuffer != null) solverShader.SetBuffer(storeStartStateKernel, "positions", positionsBuffer);
            if (startPositionsBuffer != null) solverShader.SetBuffer(storeStartStateKernel, "startPositions", startPositionsBuffer);
            if (endPositionsBuffer != null) solverShader.SetBuffer(storeStartStateKernel, "endPositions", endPositionsBuffer);
            if (orientationsBuffer != null) solverShader.SetBuffer(storeStartStateKernel, "orientations", orientationsBuffer);
            if (startOrientationsBuffer != null) solverShader.SetBuffer(storeStartStateKernel, "startOrientations", startOrientationsBuffer);
            if (endOrientationsBuffer != null) solverShader.SetBuffer(storeStartStateKernel, "endOrientations", endOrientationsBuffer);
        }

        public void MaxFoamParticleCountChanged(ObiSolver solver)
        {
            auxPositions?.Dispose();
            auxVelocities?.Dispose();
            auxColors?.Dispose();
            auxAttributes?.Dispose();
            auxOffsetInCell?.Dispose();
            auxSortedToOriginal?.Dispose();

            if (m_Solver.maxFoamParticles > 0)
            {
                solver.foamPositions.AsComputeBuffer<Vector4>();
                solver.foamVelocities.AsComputeBuffer<Vector4>();
                solver.foamColors.AsComputeBuffer<Vector4>();
                solver.foamAttributes.AsComputeBuffer<Vector4>();

                auxPositions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)m_Solver.maxFoamParticles, 16);
                auxVelocities = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)m_Solver.maxFoamParticles, 16);
                auxColors = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)m_Solver.maxFoamParticles, 16);
                auxAttributes = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)m_Solver.maxFoamParticles, 16);
                auxOffsetInCell = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)m_Solver.maxFoamParticles, 4);
                auxSortedToOriginal = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)m_Solver.maxFoamParticles, 4);
            }
        }

        public void SetRigidbodyArrays(ObiSolver solver)
        {
            rigidbodyLinearDeltasBuffer = solver.rigidbodyLinearDeltas.SafeAsComputeBuffer<Vector4>();
            rigidbodyAngularDeltasBuffer = solver.rigidbodyAngularDeltas.SafeAsComputeBuffer<Vector4>();

            if (rigidbodyLinearDeltasIntBuffer != null)
            {
                rigidbodyLinearDeltasIntBuffer.Dispose();
                rigidbodyLinearDeltasIntBuffer = null;
            }

            if (rigidbodyAngularDeltasIntBuffer != null)
            {
                rigidbodyAngularDeltasIntBuffer.Dispose();
                rigidbodyAngularDeltasIntBuffer = null;
            }

            rigidbodyLinearDeltasIntBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, rigidbodyLinearDeltasBuffer.count, solver.rigidbodyLinearDeltas.stride);
            rigidbodyLinearDeltasIntBuffer.SetData(new Vector4[rigidbodyLinearDeltasBuffer.count]);

            rigidbodyAngularDeltasIntBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, rigidbodyAngularDeltasBuffer.count, solver.rigidbodyAngularDeltas.stride);
            rigidbodyAngularDeltasIntBuffer.SetData(new Vector4[rigidbodyAngularDeltasBuffer.count]);
        }

        public IConstraintsBatchImpl CreateConstraintsBatch(Oni.ConstraintType type)
        {
            if (constraints[(int)type] != null)
                return constraints[(int)type].CreateConstraintsBatch();
            return null;
        }

        public void DestroyConstraintsBatch(IConstraintsBatchImpl batch)
        {
            if (batch != null && constraints[(int)batch.constraintType] != null)
                constraints[(int)batch.constraintType].RemoveBatch(batch);
        }

        public void FinishSimulation()
        {
            // Make sure GPU->CPU readbacks have finished.
            // by default, only positions and velocities are read back.
            // ObiActors can request whatever data they need in RequestData,
            // then wait for it in SimulationEnd.
            abstraction.positions.WaitForReadback();
            abstraction.velocities.WaitForReadback();

            abstraction.rigidbodyLinearDeltas.WaitForReadback();
            abstraction.rigidbodyAngularDeltas.WaitForReadback();

            var sm = constraints[(int)Oni.ConstraintType.ShapeMatching] as ComputeShapeMatchingConstraints;
            if (sm != null)
                sm.WaitForReadback();

            var dm = constraints[(int)Oni.ConstraintType.Distance] as ComputeDistanceConstraints;
            if (dm != null)
                dm.WaitForReadback();

            var pm = constraints[(int)Oni.ConstraintType.Pin] as ComputePinConstraints;
            if (pm != null)
                pm.WaitForReadback();

            abstraction.externalForces.WipeToZero();
            abstraction.externalTorques.WipeToZero();
            abstraction.externalForces.Upload();
            abstraction.externalTorques.Upload();

            // copy end to start positions.
            abstraction.startPositions.CopyFrom(abstraction.endPositions);            abstraction.startOrientations.CopyFrom(abstraction.endOrientations);

            // now that we got position / orientation data in the CPU set them as this step's end positions / orientations.
            abstraction.endPositions.CopyFrom(abstraction.positions);            abstraction.endOrientations.CopyFrom(abstraction.orientations);

            abstraction.startPositions.Upload(true);
            abstraction.startOrientations.Upload(true);
            abstraction.endPositions.Upload(true);
            abstraction.endOrientations.Upload(true);
        }

        public IObiJobHandle CollisionDetection(IObiJobHandle inputDeps, float stepTime)
        {
            var collisionParameters = abstraction.GetConstraintParameters(Oni.ConstraintType.Collision);            var particleCollisionParameters = abstraction.GetConstraintParameters(Oni.ConstraintType.ParticleCollision);            var densityParameters = abstraction.GetConstraintParameters(Oni.ConstraintType.Density);

            if (particleCollisionParameters.enabled ||
                densityParameters.enabled)            {
                UpdateFoamDensity();

                UnityEngine.Profiling.Profiler.BeginSample("Build Simplex Grid");
                particleGrid.BuildGrid(this, stepTime);
                UnityEngine.Profiling.Profiler.EndSample();

                if (densityParameters.enabled)                {
                    UnityEngine.Profiling.Profiler.BeginSample("Generate Fluid Neighborhoods");
                    particleGrid.GenerateFluidNeighborhoods(this);
                    UnityEngine.Profiling.Profiler.EndSample();
                }

                if (particleCollisionParameters.enabled)                {
                    UnityEngine.Profiling.Profiler.BeginSample("Generate Particle Contacts");
                    particleGrid.GenerateContacts(this);
                    UnityEngine.Profiling.Profiler.EndSample();
                }
            }

            if (collisionParameters.enabled)
            {
                UnityEngine.Profiling.Profiler.BeginSample("Generate Collider Contacts");
                colliderGrid.GenerateContacts(this, stepTime);
                UnityEngine.Profiling.Profiler.EndSample();

                UnityEngine.Profiling.Profiler.BeginSample("Apply Force Zones");
                colliderGrid.ApplyForceZones(this, stepTime);
                UnityEngine.Profiling.Profiler.EndSample();
            }

            return inputDeps;
        }

        public IObiJobHandle Substep(IObiJobHandle handle, float stepTime, float substepTime, int steps, float timeLeft)
        {
            // if there's no active particles yet, don't do anything:
            if (activeParticleCount > 0)
            {
                int threadGroups = ComputeMath.ThreadGroupCount(activeParticleCount, 128);
                solverShader.SetInt("particleCount", activeParticleCount);

                solverShader.SetFloat("deltaTime", substepTime);
                solverShader.SetFloat("velocityScale", Mathf.Pow(1 - Mathf.Clamp(m_Solver.parameters.damping, 0, 1), substepTime));

                // Apply aerodynamics
                constraints[(int)Oni.ConstraintType.Aerodynamics].Project(stepTime, substepTime, steps, timeLeft);

                // Predict positions:
                solverShader.Dispatch(predictPositionsKernel, threadGroups, 1, 1);

                ApplyConstraints(stepTime, substepTime, steps, timeLeft);

                // Update velocities;
                solverShader.Dispatch(updateVelocitiesKernel, threadGroups, 1, 1);

                // Postprocess velocities:
                ApplyVelocityCorrections(substepTime);

                // Update positions:
                solverShader.Dispatch(updatePositionsKernel, threadGroups, 1, 1);
            }

            // Update diffuse particles:
            int substepsLeft = Mathf.RoundToInt(timeLeft / substepTime);
            int foamPadding = Mathf.CeilToInt(abstraction.substeps / (float)abstraction.foamSubsteps);

            if (substepsLeft % foamPadding == 0)
                UpdateDiffuseParticles(substepTime * foamPadding);

            return handle;
        }

        private void ApplyVelocityCorrections(float deltaTime)
        {
            var densityParameters = m_Solver.GetConstraintParameters(Oni.ConstraintType.Density);

            if (densityParameters.enabled)
            {
                var d = constraints[(int)Oni.ConstraintType.Density] as ComputeDensityConstraints;
                if (d != null)
                    d.ApplyVelocityCorrections(deltaTime);
            }
        }

        private void ApplyConstraints(float stepTime, float substepTime, int substeps, float timeLeft)
        {
            // calculate max amount of iterations required, and initialize constraints..
            int maxIterations = 0;            for (int i = 0; i < Oni.ConstraintTypeCount; ++i)            {                var parameters = m_Solver.GetConstraintParameters((Oni.ConstraintType)i);                if (parameters.enabled)                {                    maxIterations = Mathf.Max(maxIterations, parameters.iterations);                    constraints[i].Initialize(substepTime);                }            }

            // calculate iteration paddings:
            for (int i = 0; i < Oni.ConstraintTypeCount; ++i)            {                var parameters = m_Solver.GetConstraintParameters((Oni.ConstraintType)i);                if (parameters.enabled && parameters.iterations > 0)                    padding[i] = Mathf.CeilToInt(maxIterations / (float)parameters.iterations);                else                    padding[i] = maxIterations;            }

            // perform projection iterations:
            for (int i = 1; i < maxIterations; ++i)            {                for (int j = 0; j < Oni.ConstraintTypeCount; ++j)                {                    if (j != (int)Oni.ConstraintType.Aerodynamics)                    {                        var parameters = m_Solver.GetConstraintParameters((Oni.ConstraintType)j);                        if (parameters.enabled && i % padding[j] == 0)                            constraints[j].Project(stepTime, substepTime, substeps, timeLeft);                    }                }            }

            // final iteration, all groups together:
            for (int i = 0; i < Oni.ConstraintTypeCount; ++i)            {                if (i != (int)Oni.ConstraintType.Aerodynamics)                {                    var parameters = m_Solver.GetConstraintParameters((Oni.ConstraintType)i);                    if (parameters.enabled && parameters.iterations > 0)                        constraints[i].Project(stepTime, substepTime, substeps, timeLeft);                }            }

            // Despite friction constraints being applied after collision (since coulomb friction depends on normal impulse)
            // we perform a collision iteration right at the end to ensure the final state meets the Signorini-Fichera conditions.
            var param = m_Solver.GetConstraintParameters(Oni.ConstraintType.ParticleCollision);            if (param.enabled && param.iterations > 0)                constraints[(int)Oni.ConstraintType.ParticleCollision].Project(stepTime, substepTime, substeps, timeLeft);            param = m_Solver.GetConstraintParameters(Oni.ConstraintType.Collision);            if (param.enabled && param.iterations > 0)                constraints[(int)Oni.ConstraintType.Collision].Project(stepTime, substepTime, substeps, timeLeft);
        }

        public IObiJobHandle ApplyInterpolation(IObiJobHandle inputDeps, ObiNativeVector4List startPositions, ObiNativeQuaternionList startOrientations, float stepTime, float unsimulatedTime)
        {
            if (particleCount <= 0)
                return inputDeps;

            int threadGroups = ComputeMath.ThreadGroupCount(particleCount, 128);
            solverShader.SetInt("particleCount", particleCount);

            solverShader.SetFloat("deltaTime", stepTime);
            solverShader.SetFloat("blendFactor", stepTime > 0 ? unsimulatedTime / stepTime : 0);
            solverShader.SetInt("interpolate", (int)m_Solver.parameters.interpolation);

            // Interpolate particle state:
            solverShader.Dispatch(interpolateKernel, threadGroups, 1, 1);

            // Reset normals:
            if ((deformableTriangleCount > 0 || deformableEdgeCount > 0) && normalsIntBuffer != null)
            {
                threadGroups = ComputeMath.ThreadGroupCount(normalsIntBuffer.count, 128);
                deformableTrisShader.SetInt("normalsCount", normalsIntBuffer.count);
                deformableTrisShader.SetBuffer(resetNormalsKernel, "phases", phasesBuffer);
                deformableTrisShader.SetBuffer(resetNormalsKernel, "normals", normalsIntBuffer);
                deformableTrisShader.SetBuffer(resetNormalsKernel, "tangents", tangentsIntBuffer);

                deformableTrisShader.Dispatch(resetNormalsKernel, threadGroups, 1, 1);

                // Update deformable triangle normals
                if (deformableTriangleCount > 0)
                {
                    threadGroups = ComputeMath.ThreadGroupCount(deformableTriangleCount, 128);
                    deformableTrisShader.SetBuffer(updateNormalsKernel, "renderablePositions", renderablePositionsBuffer);
                    deformableTrisShader.SetBuffer(updateNormalsKernel, "normals", normalsIntBuffer);
                    deformableTrisShader.SetBuffer(updateNormalsKernel, "tangents", tangentsIntBuffer);

                    deformableTrisShader.Dispatch(updateNormalsKernel, threadGroups, 1, 1);
                }

                // Update deformable edge normals
                if (deformableEdgeCount > 0)
                {
                    threadGroups = ComputeMath.ThreadGroupCount(deformableEdgeCount, 128);
                    deformableTrisShader.SetBuffer(updateEdgeNormalsKernel, "renderablePositions", renderablePositionsBuffer);
                    deformableTrisShader.SetBuffer(updateEdgeNormalsKernel, "wind", windBuffer);
                    deformableTrisShader.SetBuffer(updateEdgeNormalsKernel, "normals", normalsIntBuffer);

                    deformableTrisShader.Dispatch(updateEdgeNormalsKernel, threadGroups, 1, 1);
                }

                // Update particle orientations
                threadGroups = ComputeMath.ThreadGroupCount(normalsIntBuffer.count, 128);
                deformableTrisShader.SetBuffer(orientationFromNormalsKernel, "phases", phasesBuffer);
                deformableTrisShader.SetBuffer(orientationFromNormalsKernel, "renderableOrientations", renderableOrientationsBuffer);
                deformableTrisShader.SetBuffer(orientationFromNormalsKernel, "normals", normalsIntBuffer);
                deformableTrisShader.SetBuffer(orientationFromNormalsKernel, "tangents", tangentsIntBuffer);

                deformableTrisShader.Dispatch(orientationFromNormalsKernel, threadGroups, 1, 1);
            }

            //make sure density constraints are enabled, otherwise particles have no neighbors and neighbor lists will be uninitialized.
            var param = m_Solver.GetConstraintParameters(Oni.ConstraintType.Density);            if (param.enabled && param.iterations > 0)
            {
                // Fluid laplacian/anisotropy (only if we're in play mode, in-editor we have no particlegrid/sorted data).
                var d = constraints[(int)Oni.ConstraintType.Density] as ComputeDensityConstraints;
                if (Application.isPlaying && d != null)
                    d.CalculateAnisotropyLaplacianSmoothing();
            }

            return inputDeps;
        }

        private void UpdateFoamDensity()
        {
            var system = abstraction.GetRenderSystem<ObiFoamGenerator>() as ComputeFoamRenderSystem;
            if (system != null && m_Solver.maxFoamParticles > 0 && particleGrid.cellCounts != null)
            {
                for (int i = 0; i < system.renderers.Count; ++i)
                {
                    // solver indices compute buffer may be null 
                    if (system.renderers[i].pressure > 0 && 
                        system.renderers[i].actor.solverIndices?.computeBuffer != null)
                    {
                        float scale = 0.01f + Mathf.Clamp01(1 - system.renderers[i].density);
                        float radius = system.renderers[i].size * scale;

                        int cellThreadGroups = ComputeMath.ThreadGroupCount(particleGrid.cellCounts.count, 128);
                        foamDensityShader.SetInt("maxCells", particleGrid.cellCounts.count);
                        foamDensityShader.SetInt("maxFoamParticles", abstraction.foamPositions.computeBuffer.count);
                        foamDensityShader.SetInt("mode", (int)abstraction.parameters.mode);
                        foamDensityShader.SetFloat("pressure", system.renderers[i].pressure);
                        foamDensityShader.SetFloat("particleRadius", radius);
                        foamDensityShader.SetFloat("smoothingRadius", radius * 2 * system.renderers[i].smoothingRadius);
                        foamDensityShader.SetFloat("invMass", 1000 * Mathf.Pow(radius * 2, 3 - (int)abstraction.parameters.mode));
                        foamDensityShader.SetFloat("surfaceTension", system.renderers[i].surfaceTension);

                        foamDensityShader.SetBuffer(clearGridKernel, "cellStart", particleGrid.cellOffsets);
                        foamDensityShader.SetBuffer(clearGridKernel, "cellCounts", particleGrid.cellCounts);
                        foamDensityShader.Dispatch(clearGridKernel, cellThreadGroups, 1, 1);

                        foamDensityShader.SetBuffer(insertGridKernel, "inputPositions", abstraction.foamPositions.computeBuffer);
                        foamDensityShader.SetBuffer(insertGridKernel, "offsetInCell", auxOffsetInCell); 
                        foamDensityShader.SetBuffer(insertGridKernel, "cellCounts", particleGrid.cellCounts);
                        foamDensityShader.SetBuffer(insertGridKernel, "dispatch", abstraction.foamCount.computeBuffer);
                        foamDensityShader.DispatchIndirect(insertGridKernel, abstraction.foamCount.computeBuffer);

                        // prefix sum
                        particleGrid.cellsPrefixSum.Sum(particleGrid.cellCounts, particleGrid.cellOffsets);

                        foamDensityShader.SetBuffer(sortByGridKernel, "inputPositions", abstraction.foamPositions.computeBuffer);
                        foamDensityShader.SetBuffer(sortByGridKernel, "sortedPositions", auxPositions);
                        foamDensityShader.SetBuffer(sortByGridKernel, "sortedToOriginal", auxSortedToOriginal);
                        foamDensityShader.SetBuffer(sortByGridKernel, "offsetInCell", auxOffsetInCell);
                        foamDensityShader.SetBuffer(sortByGridKernel, "cellStart", particleGrid.cellOffsets);
                        foamDensityShader.SetBuffer(sortByGridKernel, "cellCounts", particleGrid.cellCounts);
                        foamDensityShader.SetBuffer(sortByGridKernel, "dispatch", abstraction.foamCount.computeBuffer);
                        foamDensityShader.DispatchIndirect(sortByGridKernel, abstraction.foamCount.computeBuffer);

                        foamDensityShader.SetBuffer(computeDensityKernel, "inputPositions", abstraction.foamPositions.computeBuffer);
                        foamDensityShader.SetBuffer(computeDensityKernel, "sortedPositions", auxPositions);
                        foamDensityShader.SetBuffer(computeDensityKernel, "fluidData", auxVelocities);
                        foamDensityShader.SetBuffer(computeDensityKernel, "cellStart", particleGrid.cellOffsets);
                        foamDensityShader.SetBuffer(computeDensityKernel, "cellCounts", particleGrid.cellCounts);
                        foamDensityShader.SetBuffer(computeDensityKernel, "dispatch", abstraction.foamCount.computeBuffer);
                        foamDensityShader.DispatchIndirect(computeDensityKernel, abstraction.foamCount.computeBuffer);

                        foamDensityShader.SetBuffer(applyDensityKernel, "inputPositions", abstraction.foamPositions.computeBuffer);
                        foamDensityShader.SetBuffer(applyDensityKernel, "sortedPositions", auxPositions);
                        foamDensityShader.SetBuffer(applyDensityKernel, "sortedToOriginal", auxSortedToOriginal);
                        foamDensityShader.SetBuffer(applyDensityKernel, "fluidData", auxVelocities);
                        foamDensityShader.SetBuffer(applyDensityKernel, "cellStart", particleGrid.cellOffsets);
                        foamDensityShader.SetBuffer(applyDensityKernel, "cellCounts", particleGrid.cellCounts);
                        foamDensityShader.SetBuffer(applyDensityKernel, "dispatch", abstraction.foamCount.computeBuffer);
                        foamDensityShader.DispatchIndirect(applyDensityKernel, abstraction.foamCount.computeBuffer);
                    }
                }
            }
            else
                activeFoamParticleCount = 0;
        }

        private void UpdateDiffuseParticles(float deltaTime)
        {
            var system = abstraction.GetRenderSystem<ObiFoamGenerator>() as ComputeFoamRenderSystem;
            if (system != null && m_Solver.maxFoamParticles > 0 && particleGrid.sortedUserDataColor != null)
            {
                foamShader.SetFloat("deltaTime", deltaTime);
                foamShader.SetVector("gravity", m_Solver.parameters.gravity * m_Solver.parameters.foamGravityScale);
                foamShader.SetVector("agingOverPopulation", new Vector3(m_Solver.foamAccelAgingRange.x, m_Solver.foamAccelAgingRange.y, m_Solver.foamAccelAging));
                foamShader.SetInt("maxFoamParticles", abstraction.foamPositions.computeBuffer.count);
                foamShader.SetInt("maxCells", particleGrid.maxCells);

                foamShader.SetInt("pointCount", simplexCounts.pointCount);
                foamShader.SetInt("edgeCount", simplexCounts.edgeCount);
                foamShader.SetInt("triangleCount", simplexCounts.triangleCount);

                foamShader.SetBuffer(sortDataKernel, "positions", prevPositionsBuffer);
                foamShader.SetBuffer(sortDataKernel, "velocities", velocitiesBuffer);
                foamShader.SetBuffer(sortDataKernel, "orientations", renderableOrientationsBuffer);
                foamShader.SetBuffer(sortDataKernel, "principalRadii", renderableRadiiBuffer);
                foamShader.SetBuffer(sortDataKernel, "sortedPositions", particleGrid.sortedPositions);
                foamShader.SetBuffer(sortDataKernel, "sortedVelocities", particleGrid.sortedFluidDataVel);
                foamShader.SetBuffer(sortDataKernel, "sortedOrientations", particleGrid.sortedPrevPosOrientations);
                foamShader.SetBuffer(sortDataKernel, "sortedRadii", particleGrid.sortedPrincipalRadii);
                foamShader.SetBuffer(sortDataKernel, "sortedToOriginal", particleGrid.sortedFluidIndices);
                foamShader.SetBuffer(sortDataKernel, "fluidMaterial", fluidMaterialsBuffer);
                foamShader.SetBuffer(sortDataKernel, "fluidData", fluidDataBuffer);
                foamShader.SetBuffer(sortDataKernel, "dispatch", fluidDispatchBuffer);
                foamShader.DispatchIndirect(sortDataKernel, fluidDispatchBuffer);

                int threadGroups;
                foamShader.SetBuffer(emitFoamKernel, "positions", prevPositionsBuffer);
                foamShader.SetBuffer(emitFoamKernel, "velocities", velocitiesBuffer);
                foamShader.SetBuffer(emitFoamKernel, "angularVelocities", angularVelocitiesBuffer);
                foamShader.SetBuffer(emitFoamKernel, "principalRadii", principalRadiiBuffer);
                foamShader.SetBuffer(emitFoamKernel, "outputPositions", abstraction.foamPositions.computeBuffer);
                foamShader.SetBuffer(emitFoamKernel, "outputVelocities", abstraction.foamVelocities.computeBuffer);
                foamShader.SetBuffer(emitFoamKernel, "outputColors", abstraction.foamColors.computeBuffer);
                foamShader.SetBuffer(emitFoamKernel, "outputAttributes", abstraction.foamAttributes.computeBuffer);
                foamShader.SetBuffer(emitFoamKernel, "dispatch", abstraction.foamCount.computeBuffer);
                for (int i = 0; i < system.renderers.Count; ++i)
                {
                    // solver indices compute buffer may be null 
                    if (system.renderers[i].actor.solverIndices?.computeBuffer != null)
                    {
                        threadGroups = ComputeMath.ThreadGroupCount(system.renderers[i].actor.activeParticleCount, 128);
                        foamShader.SetInt("activeParticleCount", system.renderers[i].actor.activeParticleCount);
                        foamShader.SetVector("vorticityRange", system.renderers[i].vorticityRange);
                        foamShader.SetVector("velocityRange", system.renderers[i].velocityRange);
                        foamShader.SetFloat("foamGenerationRate", system.renderers[i].foamGenerationRate);
                        foamShader.SetFloat("potentialIncrease", system.renderers[i].foamPotential);
                        foamShader.SetFloat("potentialDiffusion", Mathf.Pow(1 - Mathf.Clamp01(system.renderers[i].foamPotentialDiffusion), deltaTime));
                        foamShader.SetFloat("buoyancy", system.renderers[i].buoyancy);
                        foamShader.SetFloat("drag", system.renderers[i].drag);
                        foamShader.SetFloat("airDrag", Mathf.Pow(1 - Mathf.Clamp01(system.renderers[i].atmosphericDrag), deltaTime));
                        foamShader.SetFloat("airAging", system.renderers[i].airAging);
                        foamShader.SetFloat("isosurface", system.renderers[i].isosurface);

                        foamShader.SetFloat("particleSize", system.renderers[i].size);
                        foamShader.SetFloat("sizeRandom", system.renderers[i].sizeRandom);
                        foamShader.SetFloat("lifetime", system.renderers[i].lifetime);
                        foamShader.SetFloat("lifetimeRandom", system.renderers[i].lifetimeRandom);
                        foamShader.SetVector("foamColor", system.renderers[i].color);

                        foamShader.SetBuffer(emitFoamKernel, "activeParticles", system.renderers[i].actor.solverIndices.computeBuffer);
                        foamShader.Dispatch(emitFoamKernel, threadGroups, 1, 1);
                    }
                }

                foamShader.SetBuffer(copyAliveKernel, "dispatch", abstraction.foamCount.computeBuffer);
                foamShader.Dispatch(copyAliveKernel, 1, 1, 1);

                foamShader.SetBuffer(updateFoamKernel, "cellOffsets", particleGrid.cellOffsets);
                foamShader.SetBuffer(updateFoamKernel, "cellCounts", particleGrid.cellCounts);
                foamShader.SetBuffer(updateFoamKernel, "gridHashToSortedIndex", particleGrid.cellHashToMortonIndex);
                foamShader.SetBuffer(updateFoamKernel, "levelPopulation", particleGrid.levelPopulation);
                foamShader.SetBuffer(updateFoamKernel, "solverBounds", reducedBounds);

                foamShader.SetBuffer(updateFoamKernel, "positions", particleGrid.sortedPositions);
                foamShader.SetBuffer(updateFoamKernel, "orientations", particleGrid.sortedPrevPosOrientations);
                foamShader.SetBuffer(updateFoamKernel, "principalRadii", particleGrid.sortedPrincipalRadii);
                foamShader.SetBuffer(updateFoamKernel, "velocities", particleGrid.sortedFluidDataVel);
                foamShader.SetBuffer(updateFoamKernel, "fluidSimplices", particleGrid.sortedSimplexToFluid);
                foamShader.SetBuffer(updateFoamKernel, "sortedToOriginal", particleGrid.sortedFluidIndices);
                foamShader.SetBuffer(updateFoamKernel, "inputPositions", abstraction.foamPositions.computeBuffer);
                foamShader.SetBuffer(updateFoamKernel, "inputVelocities", abstraction.foamVelocities.computeBuffer);
                foamShader.SetBuffer(updateFoamKernel, "inputColors", abstraction.foamColors.computeBuffer);
                foamShader.SetBuffer(updateFoamKernel, "inputAttributes", abstraction.foamAttributes.computeBuffer);
                foamShader.SetBuffer(updateFoamKernel, "outputPositions", auxPositions);
                foamShader.SetBuffer(updateFoamKernel, "outputVelocities", auxVelocities);
                foamShader.SetBuffer(updateFoamKernel, "outputColors", auxColors);
                foamShader.SetBuffer(updateFoamKernel, "outputAttributes", auxAttributes);
                foamShader.SetBuffer(updateFoamKernel, "dispatch", abstraction.foamCount.computeBuffer);
                foamShader.DispatchIndirect(updateFoamKernel, abstraction.foamCount.computeBuffer);

                // copy aux buffers to solver buffers:
                foamShader.SetBuffer(copyKernel, "inputPositions", auxPositions);
                foamShader.SetBuffer(copyKernel, "inputVelocities", auxVelocities);
                foamShader.SetBuffer(copyKernel, "inputColors", auxColors);
                foamShader.SetBuffer(copyKernel, "inputAttributes", auxAttributes);
                foamShader.SetBuffer(copyKernel, "outputPositions", abstraction.foamPositions.computeBuffer);
                foamShader.SetBuffer(copyKernel, "outputVelocities", abstraction.foamVelocities.computeBuffer);
                foamShader.SetBuffer(copyKernel, "outputColors", abstraction.foamColors.computeBuffer);
                foamShader.SetBuffer(copyKernel, "outputAttributes", abstraction.foamAttributes.computeBuffer);
                foamShader.SetBuffer(copyKernel, "dispatch", abstraction.foamCount.computeBuffer);
                foamShader.DispatchIndirect(copyKernel, abstraction.foamCount.computeBuffer, 16);

                AsyncGPUReadback.Request(abstraction.foamCount.computeBuffer, 4, 12, (AsyncGPUReadbackRequest obj) =>
                {
                    if (obj.done && !obj.hasError)
                        this.activeFoamParticleCount = obj.GetData<uint>()[0];
                });
            }
            else
                activeFoamParticleCount = 0;
        }

        public void SpatialQuery(ObiNativeQueryShapeList shapes, ObiNativeAffineTransformList transforms, ObiNativeQueryResultList results)        {
            if (abstraction.queryResults.count != abstraction.maxQueryResults)
            {
                abstraction.queryResults.ResizeUninitialized((int)abstraction.maxQueryResults);
                abstraction.queryResults.SafeAsComputeBuffer<QueryResult>(GraphicsBuffer.Target.Counter);
            }

            spatialQueries.SpatialQuery(this, shapes.SafeAsComputeBuffer<QueryShape>(),
                                              transforms.SafeAsComputeBuffer<AffineTransform>(),
                                              results.computeBuffer);
        }

        public int GetParticleGridSize()
        {
            //return particleGrid.grid.usedCells.Length;
            return 0;
        }
        public void GetParticleGrid(ObiNativeAabbList cells)
        {
            //particleGrid.GetCells(cells);
        }
    }
}
