using UnityEngine;

namespace Obi.Samples
{
    public class SnakeController : MonoBehaviour
    {
        public Transform headReferenceFrame;
        public float headSpeed = 20;
        public float upSpeed = 40;
        public float slitherSpeed = 10;

        private ObiRope rope;
        private ObiSolver solver;
        private float[] traction;
        private Vector3[] surfaceNormal;

        private void Start()
        {
            rope = GetComponent<ObiRope>();
            solver = rope.solver;

            // subscribe to solver events (to update surface information)
            rope.OnSimulationStart += ResetSurfaceInfo;
            solver.OnCollision += AnalyzeContacts;
            solver.OnParticleCollision += AnalyzeContacts;
        }


        private void OnDestroy()
        {
            rope.OnSimulationStart -= ResetSurfaceInfo;
            solver.OnCollision -= AnalyzeContacts;
            solver.OnParticleCollision -= AnalyzeContacts;
        }

        private void ResetSurfaceInfo(ObiActor a, float simulatedTime, float substepTime)
        {

            if (traction == null)
            {
                traction = new float[rope.activeParticleCount];
                surfaceNormal = new Vector3[rope.activeParticleCount];
            }

            if (Input.GetKey(KeyCode.J))
            {
                for (int i = 1; i < rope.activeParticleCount; ++i)
                {
                    int solverIndex = rope.solverIndices[i];
                    int prevSolverIndex = rope.solverIndices[i - 1];

                    // direction from current particle to previous one, projected on the contact surface:
                    Vector4 dir = Vector3.ProjectOnPlane(solver.positions[prevSolverIndex] - solver.positions[solverIndex], surfaceNormal[i]).normalized;

                    // move in that direction:
                    solver.velocities[solverIndex] += dir * traction[i] / solver.invMasses[solverIndex] * slitherSpeed * simulatedTime;
                }
            }

            int headIndex = rope.solverIndices[0];

            if (headReferenceFrame != null)
            {
                Vector3 direction = Vector3.zero;

                // Determine movement direction:
                if (Input.GetKey(KeyCode.W))
                {
                    direction += headReferenceFrame.forward * headSpeed;
                }
                if (Input.GetKey(KeyCode.A))
                {
                    direction += -headReferenceFrame.right * headSpeed;
                }
                if (Input.GetKey(KeyCode.S))
                {
                    direction += -headReferenceFrame.forward * headSpeed;
                }
                if (Input.GetKey(KeyCode.D))
                {
                    direction += headReferenceFrame.right * headSpeed;
                }

                // flatten out the direction so that it's parallel to the ground:
                direction.y = 0;

                solver.velocities[headIndex] += (Vector4)direction * simulatedTime;
            }

            if (Input.GetKey(KeyCode.Space))
                solver.velocities[headIndex] += (Vector4)Vector3.up * simulatedTime * upSpeed;


            // reset surface info:
            for (int i = 0; i < traction.Length; ++i)
            {
                traction[i] = 0;
                surfaceNormal[i] = Vector3.zero;
            }

        }

        private void AnalyzeContacts(object sender, ObiNativeContactList e)
        {
            // iterate trough all contacts:
            for (int i = 0; i < e.count; ++i)
            {
                var contact = e[i];
                if (contact.distance < 0.005f)
                {
                    int simplexIndex = solver.simplices[contact.bodyA];
                    var particleInActor = solver.particleToActor[simplexIndex];

                    if (particleInActor != null && particleInActor.actor == rope && traction != null)
                    {
                        // using 1 here, could calculate a traction value based on the type of terrain, friction, etc.
                        traction[particleInActor.indexInActor] = 1;

                        // accumulate surface normal:
                        surfaceNormal[particleInActor.indexInActor] += (Vector3)contact.normal;
                    }
                }
            }
        }
    }
}