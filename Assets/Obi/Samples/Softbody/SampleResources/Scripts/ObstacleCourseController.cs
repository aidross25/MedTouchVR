using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Obi;

public class ObstacleCourseController : MonoBehaviour
{
    ObiSolver solver;

    public ObiSoftbody softbody;
    public ActorCOMTransform softbodyCOM;
    public ObiCollider deathPitCollider;
	public ObiCollider finishCollider;
    public Transform spawnPoint;
	public Transform cameraSpawnPoint;

    public UnityEvent onDeath = new UnityEvent();
	public UnityEvent onFinish = new UnityEvent();
	public UnityEvent onRestart = new UnityEvent();

    bool restart;

    private void Awake()
    {
        solver = GetComponent<ObiSolver>();
    }

    // Start is called before the first frame update
    void OnEnable()
    {
        solver.OnCollision += Solver_OnCollision;
        solver.OnSimulationStart += Solver_OnSimulationStart;
    }

    private void OnDisable()
    {
        solver.OnCollision -= Solver_OnCollision;
        solver.OnSimulationStart -= Solver_OnSimulationStart;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            restart = true;
    }

    private void Solver_OnSimulationStart(ObiSolver s, float timeToSimulate, float substepTime)
    {
        if (softbody != null && softbodyCOM != null)
        {
            // restart:
            if (restart && spawnPoint != null)
            {
                softbody.Teleport(spawnPoint.position, spawnPoint.rotation);

                // update the COM after teleporting the softbody, but before
                // teleporting the camera so that the cam works with up to date COM.
                softbodyCOM.Update();

                Camera.main.GetComponent<ExtrapolationCamera>().Teleport(cameraSpawnPoint.position, cameraSpawnPoint.rotation);

                restart = false;
                onRestart.Invoke();
            }
        }
    }

    private void Solver_OnCollision(ObiSolver s, ObiNativeContactList e)
    {
        var world = ObiColliderWorld.GetInstance();
        foreach (Oni.Contact contact in e)
        {
            // look for actual contacts only:
            if (contact.distance > 0.01)
            {
                var col = world.colliderHandles[contact.bodyB].owner;
                if (col == deathPitCollider)
                {
                    onDeath.Invoke();
                    return;
                }
				if (col == finishCollider)
				{
					onFinish.Invoke();
					return;
				}
			}
        }
    }
}
