using UnityEngine;

namespace Obi
{
    public class ComputeSphere
    {
        private ComputeShader shader;
        private int kernel;

        public ComputeSphere()
        { 
            shader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/SphereShape"));
            kernel = shader.FindKernel("GenerateContacts");
        }

        public void GenerateContacts(ComputeSolverImpl solver, ComputeColliderWorld world)
        {
            shader.SetInt("maxContacts", ComputeColliderWorld.maxContacts);
            shader.SetInt("pointCount", solver.simplexCounts.pointCount);
            shader.SetInt("edgeCount", solver.simplexCounts.edgeCount);
            shader.SetInt("triangleCount", solver.simplexCounts.triangleCount);
            shader.SetInt("surfaceCollisionIterations", solver.abstraction.parameters.surfaceCollisionIterations);
            shader.SetFloat("surfaceCollisionTolerance", solver.abstraction.parameters.surfaceCollisionTolerance);
            shader.SetFloat("collisionMargin", solver.abstraction.parameters.collisionMargin);

            shader.SetBuffer(kernel, "worldToSolver", solver.worldToSolverBuffer);
            shader.SetBuffer(kernel, "simplices", solver.simplices);
            shader.SetBuffer(kernel, "positions", solver.positionsBuffer);
            shader.SetBuffer(kernel, "orientations", solver.orientationsBuffer);
            shader.SetBuffer(kernel, "velocities", solver.velocitiesBuffer);
            shader.SetBuffer(kernel, "principalRadii", solver.principalRadiiBuffer);
            shader.SetBuffer(kernel, "transforms", world.transformsBuffer);
            shader.SetBuffer(kernel, "shapes", world.shapesBuffer);
            shader.SetBuffer(kernel, "contactPairs", world.contactPairs);
            shader.SetBuffer(kernel, "contactOffsetsPerType", world.contactOffsetsPerType);
            shader.SetBuffer(kernel, "contacts", solver.abstraction.colliderContacts.computeBuffer);
            shader.SetBuffer(kernel, "dispatchBuffer", world.dispatchBuffer);
            shader.DispatchIndirect(kernel, world.dispatchBuffer, 32 + 16 * (int)Oni.ShapeType.Sphere); 
        }

    }
}
