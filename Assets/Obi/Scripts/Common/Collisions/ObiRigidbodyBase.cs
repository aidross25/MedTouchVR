using UnityEngine;
using Unity.Profiling;
using System;
using System.Collections;

namespace Obi{

    /**
	 * Small helper class that lets you specify Obi-only properties for rigidbodies.
	 */

    [ExecuteInEditMode]
    public abstract class ObiRigidbodyBase : MonoBehaviour
    {

        public bool kinematicForParticles = false;

        protected ObiRigidbodyHandle rigidbodyHandle;
        public ObiRigidbodyHandle handle
        {
            get
            {
                if (rigidbodyHandle == null || !rigidbodyHandle.isValid)
                {
                    var world = ObiColliderWorld.GetInstance();

                    // create the material:
                    rigidbodyHandle = world.CreateRigidbody();
                    rigidbodyHandle.owner = this;
                }
                return rigidbodyHandle;
            }
        }

        protected virtual void OnEnable()
        {
            //handle = ObiColliderWorld.GetInstance().CreateRigidbody();
            //handle.owner = this;
        }

		public void OnDisable()
        {
            ObiColliderWorld.GetInstance().DestroyRigidbody(handle);
        }

		public abstract void UpdateIfNeeded(float stepTime);

		/**
		 * Reads velocities back from the solver.
		 */
		public abstract void UpdateVelocities(Vector3 linearDelta, Vector3 angularDelta);

	}
}

