using UnityEngine;
using System;

namespace Obi
{
    public class NullSolverImpl : ISolverImpl
    {
        public uint activeFoamParticleCount { private set; get; }

        public void Destroy()
        {
        }

        public void PushData()
        {
        }
        public void RequestReadback()
        {
        }

        public void InitializeFrame(Vector4 translation, Vector4 scale, Quaternion rotation)
        {
        }

        public void UpdateFrame(Vector4 translation, Vector4 scale, Quaternion rotation, float deltaTime)
        {
        }

        public IObiJobHandle ApplyFrame(float worldLinearInertiaScale, float worldAngularInertiaScale, float deltaTime)
        {
            return null;
        }

        public IObiJobHandle ApplyForceZones(ObiNativeForceZoneList zones, ObiNativeAffineTransformList transforms)
        {
            return null;
        }

        public void SetDeformableTriangles(ObiNativeIntList indices, ObiNativeVector2List uvs)
        {
           
        }

        public void SetDeformableEdges(ObiNativeIntList indices)
        {

        }

        public void SetSimplices(ObiNativeIntList simplices, SimplexCounts counts)
        {
        }

        public void ParticleCountChanged(ObiSolver solver)
        {
        }

        public void MaxFoamParticleCountChanged(ObiSolver solver)
        {

        }

        public void SetRigidbodyArrays(ObiSolver solver)
        {
        }

        public void SetActiveParticles(ObiNativeIntList indices)
        {
        }

        public void GetBounds(ref Vector3 min, ref Vector3 max)
        {
        }

        public void SetParameters(Oni.SolverParameters parameters)
        {
        }

        public int GetConstraintCount(Oni.ConstraintType type)
        {
            return 0;
        }

        public void SetConstraintGroupParameters(Oni.ConstraintType type, ref Oni.ConstraintParameters parameters)
        {
        }

        public IConstraintsBatchImpl CreateConstraintsBatch(Oni.ConstraintType constraintType)
        {
            return null;
        }

        public void DestroyConstraintsBatch(IConstraintsBatchImpl group)
        {
        }

        public void FinishSimulation()
        {

        }

        public IObiJobHandle UpdateBounds(IObiJobHandle inputDeps, float stepTime)
        {
            return null;
        }

        public IObiJobHandle CollisionDetection(IObiJobHandle inputDeps, float stepTime)
        {
            return null;
        }

        public IObiJobHandle Substep(IObiJobHandle inputDeps, float stepTime, float substepTime, int index, float timeLeft)
        {
            return null;
        }

        public IObiJobHandle ApplyInterpolation(IObiJobHandle inputDeps, ObiNativeVector4List startPositions, ObiNativeQuaternionList startOrientations, float stepTime, float unsimulatedTime)
        {
            return null;
        }

        public int GetParticleGridSize()
        {
            return 0;
        }

        public void GetParticleGrid(ObiNativeAabbList cells)
        {
        }

        public void SpatialQuery(ObiNativeQueryShapeList shapes, ObiNativeAffineTransformList transforms, ObiNativeQueryResultList results)
        {
        }
    }
}
