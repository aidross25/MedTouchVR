using UnityEngine;
using Obi;
using System.Collections.Generic;
using System.Collections;



[RequireComponent(typeof(ObiSolver))]
public class Joe : MonoBehaviour {

 	ObiSolver solver;
	
	

	void Awake(){
		solver = GetComponent<ObiSolver>();
	}

	void OnEnable () {
		solver.OnCollision += Solver_OnCollision;
	}

	void OnDisable(){
		solver.OnCollision -= Solver_OnCollision;
	}

	void Solver_OnCollision (object sender, ObiNativeContactList e)
	{
		var world = ObiColliderWorld.GetInstance();
		var segments = new List<ObiColliderBase>();

		// just iterate over all contacts in the current frame:
		foreach (Oni.Contact contact in e)
		{
			// if this one is an actual collision:
			if (contact.distance < 0.01)
			{
				ObiColliderBase col = world.colliderHandles[contact.bodyB].owner;
				if (col != null)
				{
					segments.Add(col);

				}
			}
		}
	}

	

}