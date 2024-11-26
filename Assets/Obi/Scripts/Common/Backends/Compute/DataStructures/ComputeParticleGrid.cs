using UnityEngine;
using System;

namespace Obi
{
    public class ComputeParticleGrid : IDisposable
    {
        public ComputeShader gridShader;
        public int clearKernel;
        public int insertSimplicesKernel;
        public int gridPopulationKernel;
        public int sortSimplicesKernel;
        public int buildFluidDispatchKernel;
        public int buildMortonKernel;
        public int mortonSortKernel;
        public int sortFluidSimplicesKernel;

        public int sameLevelNeighborsKernel;
        public int upperLevelsNeighborsKernel;
        public int contactsKernel;

        public ComputePrefixSum cellsPrefixSum;
        private ComputePrefixSum fluidPrefixSum;

        public GraphicsBuffer cellCounts;                   // for each cell, how many simplices in it.
        public GraphicsBuffer cellOffsets;                  // index of the first simplex in the cell (on gridToMorton buffer).
        public GraphicsBuffer offsetInCell;                 // for each simplex, offset within its cell.
        public GraphicsBuffer levelPopulation;              // buffer storing amount of entries in each grid level

        public GraphicsBuffer neighbors;                    // neighbor indices for all particles. (maxNeighbors * simplexCount in size)
        public GraphicsBuffer neighborCounts;               // amount of neighbors for each particle. (simplexCount in size)

        public GraphicsBuffer contactPairs;                 // list of particle pairs.
        public GraphicsBuffer dispatchBuffer;               // dispatch info for iterating trough contacts.

        public GraphicsBuffer cellHashToMortonIndex;
        public GraphicsBuffer mortonSortedCellHashes;       // contains all cell hashes sorted according to their morton index.
        public GraphicsBuffer sortedSimplexIndices;         // maps from grid index to simplex index.

        public GraphicsBuffer sortedFluidIndices;           // contains compacted sorted indices of fluid simplices *only*. Eg, entry 0 contains the index of the first fluid simplex along the morton curve.
        public GraphicsBuffer sortedSimplexToFluid;         // maps from sorted simplex index to sorted fluid index, or -1 if the simplex is not fluid.

        public GraphicsBuffer sortedPrincipalRadii;         // Used by Density constraints: needs to be sorted once per step (not changed by constraints)
        public GraphicsBuffer sortedFluidMaterials;         // Used by Density constraints: needs to be sorted once per step (not changed by constraints)
        public GraphicsBuffer sortedFluidInterface;         // Used by Density constraints: needs to be sorted once per step (not changed by constraints)
        public GraphicsBuffer sortedFluidDataVel;           // Used by Density constraints: no need to be sorted (constraints output). Reuse for velocities (foam advection and mesher).

        public GraphicsBuffer sortedPositions;              // Used by Density constraints: needs to be sorted once per iteration.
        public GraphicsBuffer sortedPrevPosOrientations;    // Used by Density constraints: needs to be sorted once per iteration. Reuse for orientations (foam advection and mesher).
        public GraphicsBuffer sortedUserDataColor;          // Used by Density constraints: needs to be sorted once per iteration. Reuse for colors (mesher).

        private const int maxGridLevels = 24;
        private uint[] clearDispatch = { 0, 1, 1, 0 };

        public int maxParticleNeighbors { get; private set; } = 128;
        public int maxParticleContacts { get; private set; } = 4;

        public int maxCells{
            get { return cellCounts != null ? cellCounts.count : 0; }
        }

        public ComputeParticleGrid()
        {
            gridShader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/ParticleGrid"));
            clearKernel = gridShader.FindKernel("Clear");
            insertSimplicesKernel = gridShader.FindKernel("InsertSimplices");
            gridPopulationKernel = gridShader.FindKernel("FindPopulatedLevels");
            sortSimplicesKernel = gridShader.FindKernel("SortSimplices");
            buildFluidDispatchKernel = gridShader.FindKernel("BuildFluidDispatch");
            buildMortonKernel = gridShader.FindKernel("BuildMortonIndices");
            mortonSortKernel = gridShader.FindKernel("MortonSort");
            sortFluidSimplicesKernel = gridShader.FindKernel("SortFluidSimplices");
            sameLevelNeighborsKernel = gridShader.FindKernel("FindFluidNeighborsInSameLevel");
            upperLevelsNeighborsKernel = gridShader.FindKernel("FindFluidNeighborsInUpperLevels");
            contactsKernel = gridShader.FindKernel("BuildContactList");

            levelPopulation = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxGridLevels + 1, 4);
            dispatchBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 4, sizeof(uint));
        }

        private void Clear()
        {
            cellsPrefixSum?.Dispose();
            fluidPrefixSum?.Dispose();

            if (cellCounts != null)
                cellCounts.Dispose();
            if (cellOffsets != null)
                cellOffsets.Dispose();
            if (offsetInCell != null)
                offsetInCell.Dispose();

            if (contactPairs != null)
                contactPairs.Dispose();

            if (neighbors != null)
                neighbors.Dispose();
            if (neighborCounts != null)
                neighborCounts.Dispose();

            if (cellHashToMortonIndex != null)
                cellHashToMortonIndex.Dispose();
            if (mortonSortedCellHashes != null)
                mortonSortedCellHashes.Dispose();
            if (sortedSimplexIndices != null)
                sortedSimplexIndices.Dispose();
            if (sortedFluidIndices != null)
                sortedFluidIndices.Dispose();
            if (sortedSimplexToFluid != null)
                sortedSimplexToFluid.Dispose();

            if (sortedPositions != null)
                sortedPositions.Dispose();
            if (sortedPrevPosOrientations != null)
                sortedPrevPosOrientations.Dispose();
            if (sortedPrincipalRadii != null)
                sortedPrincipalRadii.Dispose();
            if (sortedFluidMaterials != null)
                sortedFluidMaterials.Dispose();
            if (sortedFluidInterface != null)
                sortedFluidInterface.Dispose();
            if (sortedUserDataColor != null)
                sortedUserDataColor.Dispose();
            if (sortedFluidDataVel != null)
                sortedFluidDataVel.Dispose();

            cellsPrefixSum = null;
            fluidPrefixSum = null;
            cellCounts = null;
            cellOffsets = null;
            offsetInCell = null;
            contactPairs = null;
            neighbors = null;
            neighborCounts = null;
            sortedSimplexIndices = null;
            cellHashToMortonIndex = null;
            sortedFluidIndices = null;
            sortedSimplexToFluid = null;
            sortedPositions = null;
            sortedPrevPosOrientations = null;
            sortedPrincipalRadii = null;
            sortedFluidMaterials = null;
            sortedFluidInterface = null;
            sortedUserDataColor = null;
            sortedFluidDataVel = null;
        }

        public void Dispose()
        {
            if (levelPopulation != null)
                levelPopulation.Dispose();

            if (dispatchBuffer != null)
                dispatchBuffer.Dispose();

            levelPopulation = null;
            dispatchBuffer = null;

            Clear();
        }

        // Sets the maximum amount of items that can be stored in the grid.
        public bool SetCapacity(int capacity, uint maxParticleContacts, uint maxParticleNeighbors)
        {
            if (offsetInCell == null || capacity > offsetInCell.count ||
                maxParticleNeighbors != this.maxParticleNeighbors ||
                maxParticleContacts != this.maxParticleContacts)
            {
                Clear();

                this.maxParticleNeighbors = (int)maxParticleNeighbors;
                this.maxParticleContacts = (int)maxParticleContacts;

                capacity *= 2;
                int hashtableSize = (int)(capacity * 1.5f);

                // hashtable data:
                cellCounts = new GraphicsBuffer(GraphicsBuffer.Target.Structured, hashtableSize, 4);
                cellOffsets = new GraphicsBuffer(GraphicsBuffer.Target.Structured, hashtableSize, 4);
                mortonSortedCellHashes = new GraphicsBuffer(GraphicsBuffer.Target.Structured, hashtableSize, 4);
                cellHashToMortonIndex = new GraphicsBuffer(GraphicsBuffer.Target.Structured, hashtableSize, 4);
                cellsPrefixSum = new ComputePrefixSum(hashtableSize);

                // per simplex data:
                offsetInCell = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 4);
                sortedSimplexIndices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 4);
                sortedFluidIndices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 4);
                sortedSimplexToFluid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 4);

                // contact pairs and neighbor lists:
                contactPairs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity * (int)maxParticleContacts, 8);
                neighbors = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity * (int)maxParticleNeighbors, 4);
                neighborCounts = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 4);

                // sorted fluid simplex data:
                fluidPrefixSum = new ComputePrefixSum(capacity);
                sortedPrincipalRadii = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
                sortedFluidMaterials = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
                sortedFluidInterface = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
                sortedPositions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
                sortedPrevPosOrientations = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
                sortedUserDataColor = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
                sortedFluidDataVel = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);

                return true;
            }
            return false;
        }

        public void BuildGrid(ComputeSolverImpl solver, float deltaTime)
        {
            dispatchBuffer.SetData(clearDispatch);
            solver.fluidDispatchBuffer.SetData(clearDispatch);

            if (solver.simplexCounts.simplexCount > 0)
            {
                gridShader.SetInt("pointCount", solver.simplexCounts.pointCount);
                gridShader.SetInt("edgeCount", solver.simplexCounts.edgeCount);
                gridShader.SetInt("triangleCount", solver.simplexCounts.triangleCount);
                gridShader.SetInt("maxContacts", contactPairs.count);
                gridShader.SetInt("maxCells", cellCounts.count);
                gridShader.SetInt("maxNeighbors", maxParticleNeighbors);
                gridShader.SetFloat("deltaTime", deltaTime);
                gridShader.SetFloat("collisionMargin", solver.abstraction.parameters.collisionMargin);
                gridShader.SetFloat("particleCCD", solver.abstraction.parameters.particleCCD);
                int cellThreadGroups = ComputeMath.ThreadGroupCount(cellCounts.count, 128);

                solver.abstraction.particleContacts.computeBuffer.SetCounterValue(0);

                int simplexThreadGroups = ComputeMath.ThreadGroupCount(solver.simplexCounts.simplexCount, 128);

                // clear grid:
                gridShader.SetBuffer(clearKernel, "cellCounts", cellCounts);
                gridShader.SetBuffer(clearKernel, "cellOffsets", cellOffsets);
                gridShader.SetBuffer(clearKernel, "levelPopulation", levelPopulation);
                gridShader.SetBuffer(clearKernel, "mortonSortedCellHashes", mortonSortedCellHashes);
                gridShader.Dispatch(clearKernel, cellThreadGroups, 1, 1);

                // insert simplices in grid and flag fluid simplices:
                gridShader.SetBuffer(insertSimplicesKernel, "solverBounds", solver.reducedBounds);
                gridShader.SetBuffer(insertSimplicesKernel, "simplexBounds", solver.simplexBounds);
                gridShader.SetBuffer(insertSimplicesKernel, "simplices", solver.simplices);
                gridShader.SetBuffer(insertSimplicesKernel, "phases", solver.phasesBuffer);
                gridShader.SetBuffer(insertSimplicesKernel, "neighborCounts", neighborCounts);
                gridShader.SetBuffer(insertSimplicesKernel, "levelPopulation", levelPopulation);
                gridShader.SetBuffer(insertSimplicesKernel, "cellCoords", solver.cellCoordsBuffer);
                gridShader.SetBuffer(insertSimplicesKernel, "cellCounts", cellCounts);
                gridShader.SetBuffer(insertSimplicesKernel, "offsetInCell", offsetInCell);
                gridShader.SetBuffer(insertSimplicesKernel, "cellOffsets", cellOffsets);
                gridShader.Dispatch(insertSimplicesKernel, simplexThreadGroups, 1, 1);

                // find populated grid levels:
                gridShader.SetBuffer(gridPopulationKernel, "levelPopulation", levelPopulation);
                gridShader.Dispatch(gridPopulationKernel, 1, 1, 1);

                // sort cells along morton curve:
                gridShader.SetBuffer(mortonSortKernel, "mortonSortedCellHashes", mortonSortedCellHashes);
                gridShader.SetBuffer(mortonSortKernel, "cellOffsets", cellOffsets);
                gridShader.SetBuffer(mortonSortKernel, "cellCounts", cellCounts);

                int numPairs = ObiUtils.CeilToPowerOfTwo(maxCells) / 2;
                int numStages = (int)Mathf.Log(numPairs * 2, 2);
                int groups = ComputeMath.ThreadGroupCount(numPairs, 128);

                for (int stageIndex = 0; stageIndex < numStages; stageIndex++)
                {
                    for (int stepIndex = 0; stepIndex < stageIndex + 1; stepIndex++)
                    {
                        int groupWidth = 1 << (stageIndex - stepIndex);
                        int groupHeight = 2 * groupWidth - 1;
                        gridShader.SetInt("groupWidth", groupWidth);
                        gridShader.SetInt("groupHeight", groupHeight);
                        gridShader.SetInt("stepIndex", stepIndex);
                        gridShader.Dispatch(mortonSortKernel, groups, 1, 1);
                    }
                }

                // build morton indices:
                gridShader.SetBuffer(buildMortonKernel, "mortonSortedCellHashes", mortonSortedCellHashes);
                gridShader.SetBuffer(buildMortonKernel, "cellHashToMortonIndex", cellHashToMortonIndex);
                gridShader.Dispatch(buildMortonKernel, cellThreadGroups, 1, 1);

                // prefix sum to build cell start array: 
                cellsPrefixSum.Sum(cellCounts, cellOffsets);

                // sort simplex indices and compact fluid simplex indices:
                gridShader.SetBuffer(sortSimplicesKernel, "phases", solver.phasesBuffer);
                gridShader.SetBuffer(sortSimplicesKernel, "cellHashToMortonIndex", cellHashToMortonIndex);
                gridShader.SetBuffer(sortSimplicesKernel, "sortedSimplexIndices", sortedSimplexIndices);
                gridShader.SetBuffer(sortSimplicesKernel, "sortedFluidIndices", sortedFluidIndices);
                gridShader.SetBuffer(sortSimplicesKernel, "R_offsetInCell", offsetInCell);
                gridShader.SetBuffer(sortSimplicesKernel, "R_cellOffsets", cellOffsets);
                gridShader.SetBuffer(sortSimplicesKernel, "R_cellCoords", solver.cellCoordsBuffer);
                gridShader.SetBuffer(sortSimplicesKernel, "simplices", solver.simplices);
                gridShader.Dispatch(sortSimplicesKernel, simplexThreadGroups, 1, 1);

                // prefix sum of fluid flags:
                fluidPrefixSum.Sum(sortedFluidIndices, sortedSimplexToFluid);

                // build fluid dispatch buffer:
                gridShader.SetBuffer(buildFluidDispatchKernel, "fluidDispatchBuffer", solver.fluidDispatchBuffer);
                gridShader.SetBuffer(buildFluidDispatchKernel, "sortedFluidIndices", sortedFluidIndices);
                gridShader.SetBuffer(buildFluidDispatchKernel, "sortedSimplexToFluid", sortedSimplexToFluid);
                gridShader.Dispatch(buildFluidDispatchKernel, 1, 1, 1);

                // sort fluid data:
                gridShader.SetBuffer(sortFluidSimplicesKernel, "sortedFluidIndices", sortedFluidIndices);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "cellHashToMortonIndex", cellHashToMortonIndex);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "positions", solver.positionsBuffer);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "fluidInterface", solver.fluidInterfaceBuffer);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "principalRadii", solver.principalRadiiBuffer);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "phases", solver.phasesBuffer);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "sortedPositions", sortedPositions);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "sortedFluidMaterials", sortedFluidMaterials);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "sortedFluidInterface", sortedFluidInterface);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "sortedPrincipalRadii", sortedPrincipalRadii);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "fluidMaterials", solver.fluidMaterialsBuffer);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "R_offsetInCell", offsetInCell);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "R_cellOffsets", cellOffsets);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "R_cellCoords", solver.cellCoordsBuffer);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "sortedSimplexToFluid", sortedSimplexToFluid);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "simplices", solver.simplices);
                gridShader.SetBuffer(sortFluidSimplicesKernel, "fluidDispatchBuffer", solver.fluidDispatchBuffer);
                gridShader.Dispatch(sortFluidSimplicesKernel, simplexThreadGroups, 1, 1);
            }
        }

        public void GenerateContacts(ComputeSolverImpl solver)
        {
            if (solver.simplexCounts.simplexCount > 0)
            {
                int simplexThreadGroups = ComputeMath.ThreadGroupCount(solver.simplexCounts.simplexCount, 128);

                // generate contacts list:
                gridShader.SetBuffer(contactsKernel, "simplices", solver.simplices);
                gridShader.SetBuffer(contactsKernel, "sortedSimplexIndices", sortedSimplexIndices);
                gridShader.SetBuffer(contactsKernel, "sortedPositions", sortedPositions);
                gridShader.SetBuffer(contactsKernel, "sortedFluidMaterials", sortedFluidMaterials);
                gridShader.SetBuffer(contactsKernel, "positions", solver.positionsBuffer);

                gridShader.SetBuffer(contactsKernel, "cellHashToMortonIndex", cellHashToMortonIndex);
                gridShader.SetBuffer(contactsKernel, "restPositions", solver.restPositionsBuffer);
                gridShader.SetBuffer(contactsKernel, "orientations", solver.orientationsBuffer);
                gridShader.SetBuffer(contactsKernel, "restOrientations", solver.restOrientationsBuffer);
                gridShader.SetBuffer(contactsKernel, "velocities", solver.velocitiesBuffer);
                gridShader.SetBuffer(contactsKernel, "invMasses", solver.invMassesBuffer);
                gridShader.SetBuffer(contactsKernel, "phases", solver.phasesBuffer);
                gridShader.SetBuffer(contactsKernel, "filters", solver.filtersBuffer);
                gridShader.SetBuffer(contactsKernel, "principalRadii", solver.principalRadiiBuffer);
                gridShader.SetBuffer(contactsKernel, "normals", solver.normalsIntBuffer);
                gridShader.SetBuffer(contactsKernel, "R_cellCoords", solver.cellCoordsBuffer);
                gridShader.SetBuffer(contactsKernel, "R_cellOffsets", cellOffsets);
                gridShader.SetBuffer(contactsKernel, "R_cellCounts", cellCounts);
                gridShader.SetBuffer(contactsKernel, "R_offsetInCell", offsetInCell);
                gridShader.SetBuffer(contactsKernel, "R_levelPopulation", levelPopulation);
                gridShader.SetBuffer(contactsKernel, "particleContacts", solver.abstraction.particleContacts.computeBuffer);
                gridShader.SetBuffer(contactsKernel, "contactPairs", contactPairs);
                gridShader.SetBuffer(contactsKernel, "dispatchBuffer", dispatchBuffer);
                gridShader.Dispatch(contactsKernel, simplexThreadGroups, 1, 1);
            }
        }

        public void GenerateFluidNeighborhoods(ComputeSolverImpl solver)
        {
            if (solver.simplexCounts.simplexCount > 0)
            {
                // generate fluid neighbors list:
                gridShader.SetBuffer(sameLevelNeighborsKernel, "solverBounds", solver.reducedBounds);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "cellHashToMortonIndex", cellHashToMortonIndex);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "sortedFluidIndices", sortedFluidIndices);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "sortedPositions", sortedPositions);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "sortedFluidMaterials", sortedFluidMaterials);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "R_cellCoords", solver.cellCoordsBuffer);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "R_cellOffsets", cellOffsets);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "R_cellCounts", cellCounts);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "R_offsetInCell", offsetInCell);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "R_levelPopulation", levelPopulation);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "sortedSimplexToFluid", sortedSimplexToFluid);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "simplices", solver.simplices);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "neighbors", neighbors);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "neighborCounts", neighborCounts);
                gridShader.SetBuffer(sameLevelNeighborsKernel, "dispatchBuffer", solver.fluidDispatchBuffer);
                gridShader.DispatchIndirect(sameLevelNeighborsKernel, solver.fluidDispatchBuffer);

                gridShader.SetBuffer(upperLevelsNeighborsKernel, "solverBounds", solver.reducedBounds);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "cellHashToMortonIndex", cellHashToMortonIndex);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "sortedPositions", sortedPositions);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "sortedFluidIndices", sortedFluidIndices);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "mortonSortedSimplexIndices", mortonSortedCellHashes);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "sortedFluidMaterials", sortedFluidMaterials);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "R_cellCoords", solver.cellCoordsBuffer);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "R_cellOffsets", cellOffsets);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "R_cellCounts", cellCounts);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "R_offsetInCell", offsetInCell);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "R_levelPopulation", levelPopulation);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "sortedSimplexToFluid", sortedSimplexToFluid);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "simplices", solver.simplices);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "neighbors", neighbors);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "neighborCounts", neighborCounts);
                gridShader.SetBuffer(upperLevelsNeighborsKernel, "dispatchBuffer", solver.fluidDispatchBuffer);
                gridShader.DispatchIndirect(upperLevelsNeighborsKernel, solver.fluidDispatchBuffer);
            }
        }
    }
}
