using UnityEngine;

namespace Obi.Samples
{
    [RequireComponent(typeof(ObiSolver))]
    public class ColliderHighlighter : MonoBehaviour
    {

        ObiSolver solver;

        void Awake()
        {
            solver = GetComponent<Obi.ObiSolver>();
        }

        void OnEnable()
        {
            solver.OnCollision += Solver_OnCollision;
        }

        void OnDisable()
        {
            solver.OnCollision -= Solver_OnCollision;
        }

        void Solver_OnCollision(object sender, ObiNativeContactList e)
        {
            var colliderWorld = ObiColliderWorld.GetInstance();

            for (int i = 0; i < e.count; ++i)
            {
                Oni.Contact c = e[i];
                // make sure this is an actual contact:
                if (c.distance < 0.01f)
                {
                    // get the collider:
                    var col = colliderWorld.colliderHandles[c.bodyB].owner;

                    if (col != null)
                    {
                        // make it blink:
                        Blinker blinker = col.GetComponent<Blinker>();

                        if (blinker)
                            blinker.Blink();
                    }
                }
            }
        }
    }
}
