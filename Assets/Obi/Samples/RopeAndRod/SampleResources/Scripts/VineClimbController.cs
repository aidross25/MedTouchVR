
using UnityEngine;

namespace Obi.Samples
{
    public class VineClimbController : MonoBehaviour
    {
        public ObiSolver solver;
        public float climbSpeed = 1.5f;

        ObiPinhole pinhole;
        bool pressedSpace = false;

        void Start()
        {
            solver.OnCollision += Solver_OnCollision;
        }

        public void Update()
        {
            if (pinhole != null)
            {
                float speed = 0;
                if (Input.GetKey(KeyCode.W))
                    speed = climbSpeed;
                if (Input.GetKey(KeyCode.S))
                    speed = -climbSpeed;

                pinhole.motorSpeed = speed;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                pressedSpace = true;
                DetachFromVine();
            }
        }

        private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList contacts)
        {
            if (pressedSpace)
            {
                int bestParticle = -1;
                float closestDistance = float.MaxValue;
                Vector3 closestOffset = Vector3.zero;
                foreach (var c in contacts)
                {
                    if (c.distance < 0.001f)
                    {
                        ObiCollider collider = ObiColliderWorld.GetInstance().colliderHandles[c.bodyB].owner as ObiCollider;
                        if (collider.sourceCollider.isTrigger)
                        {
                            int particle = solver.simplices[c.bodyA];
                            var worldPosition = solver.transform.TransformPoint(solver.positions[particle]);
                            Vector3 offset = worldPosition - collider.transform.position;
                            float distance = Vector3.Magnitude(offset);

                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestOffset = offset;
                                bestParticle = particle;
                            }
                        }
                    }
                }

                if (bestParticle >= 0)
                {
                    var actor = solver.particleToActor[bestParticle].actor;
                    AttachToVine(actor as ObiRope, bestParticle, closestOffset);
                }

                pressedSpace = false;
            }
        }

        private float GetParticleMu(ObiRope rope, int solverParticleIndex)
        {
            for (int i = 0; i < rope.elements.Count; ++i)
            {
                if (rope.elements[i].particle1 == solverParticleIndex)
                    return i / (float)rope.elements.Count;
                if (rope.elements[i].particle2 == solverParticleIndex)
                    return (i + 1) / (float)rope.elements.Count;
            }
            return 1;
        }

        // Update is called once per frame
        void AttachToVine(ObiRope rope, int particle, Vector3 offset)
        {
            if (pinhole == null && rope != null)
            {
                transform.position += offset;
                pinhole = rope.gameObject.AddComponent<ObiPinhole>();
                pinhole.position = GetParticleMu(rope, particle);
                pinhole.motorForce = Mathf.Infinity;
                pinhole.friction = 1;
                pinhole.target = this.transform;
            }
        }

        void DetachFromVine()
        {
            if (pinhole != null)
            {
                GameObject.Destroy(pinhole);
                pinhole = null;
                pressedSpace = false;
            }
        }
    }
}
