using UnityEngine;

namespace Obi
{
    public class ComputeTriangleMesh
    {
        private ComputeShader shader;
        private int kernel;

        public ComputeTriangleMesh()
        { 
            shader = GameObject.Instantiate(Resources.Load<ComputeShader>("Compute/TriangleMeshShape"));
            kernel = shader.FindKernel("GenerateContacts");
        }

        public void GenerateContacts(ComputeSolverImpl solver, ComputeColliderWorld world, float deltaTime)
        {
            if (world.triangleMeshHeaders != null)
            {
                shader.SetInt("maxContacts", ComputeColliderWorld.maxContacts);
                shader.SetInt("pointCount", solver.simplexCounts.pointCount);
                shader.SetInt("edgeCount", solver.simplexCounts.edgeCount);
                shader.SetInt("triangleCount", solver.simplexCounts.triangleCount);
                shader.SetInt("surfaceCollisionIterations", solver.abstraction.parameters.surfaceCollisionIterations);
                shader.SetFloat("surfaceCollisionTolerance", solver.abstraction.parameters.surfaceCollisionTolerance);
                shader.SetFloat("collisionMargin", solver.abstraction.parameters.collisionMargin);
                shader.SetFloat("deltaTime", deltaTime);

                shader.SetBuffer(kernel, "worldToSolver", solver.worldToSolverBuffer);
                shader.SetBuffer(kernel, "simplices", solver.simplices);
                shader.SetBuffer(kernel, "simplexBounds", solver.simplexBounds);
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

                shader.SetBuffer(kernel, "triangleMeshHeaders", world.triangleMeshHeaders);
                shader.SetBuffer(kernel, "bihNodes", world.bihNodes);
                shader.SetBuffer(kernel, "triangles", world.triangles);
                shader.SetBuffer(kernel, "vertices", world.vertices);

                shader.DispatchIndirect(kernel, world.dispatchBuffer, 32 + 16 * (int)Oni.ShapeType.TriangleMesh);
            }
        }

    }
}
