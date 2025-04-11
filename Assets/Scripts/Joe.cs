using UnityEngine;
using Obi;

ArrayList<ObiColliderBase> segments = new ArrayList<ObiColliderBase>();
using System.Collections.Generic;
using System.Collections;

ArrayList.add(this.elements[0].particle1);
for (int i = 0; i < this.elements.Count ; i++)
{
    Arryalist.add(this.elements[i].particle2);
}

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

		// just iterate over all contacts in the current frame:
		foreach (Oni.Contact contact in e)
		{
			// if this one is an actual collision:
			if (contact.distance < 0.01)
			{
				ObiColliderBase col = world.colliderHandles[contact.bodyB].owner;
				if (col != null)
				{
					
				}
			}
		}
	}

}