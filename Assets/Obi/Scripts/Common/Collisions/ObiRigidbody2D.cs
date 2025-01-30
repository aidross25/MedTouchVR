using UnityEngine;
using System;
using System.Collections;

namespace Obi{

	/**
	 * Small helper class that lets you specify Obi-only properties for rigidbodies.
	 */

	[ExecuteInEditMode]
	[RequireComponent(typeof(Rigidbody2D))]
	public class ObiRigidbody2D : ObiRigidbodyBase
	{
        public Rigidbody2D unityRigidbody { get; private set; }

        public Vector2 position => unityRigidbody.position;
        public float rotation => unityRigidbody.rotation;

        public Vector2 linearVelocity { get; protected set; }
        public float angularVelocity { get; protected set; }

        private Quaternion prevRotation;
        private Vector3 prevPosition;

        protected override void OnEnable()
        {
            unityRigidbody = GetComponent<Rigidbody2D>();
            ResetPosition();
            base.OnEnable();
        }

        public void ResetPosition()
        {
            prevPosition = unityRigidbody.position;
            prevRotation = Quaternion.AngleAxis(unityRigidbody.rotation, Vector3.forward);
            linearVelocity = unityRigidbody.velocity;
            angularVelocity = unityRigidbody.angularVelocity;
        }

        private void CacheVelocities(float stepTime)
        {
            // differentiate positions/orientations to get our own velocites for kinematic objects.
            // when calling Physics.Simulate, MovePosition/Rotation do not work correctly. Also useful for animations.
            if (unityRigidbody.isKinematic && stepTime > 0)
            {
                // differentiate positions to obtain linear velocity:
                linearVelocity = (transform.position - prevPosition) / stepTime;

                // differentiate rotations to obtain angular velocity:
                Quaternion delta = transform.rotation * Quaternion.Inverse(prevRotation);
                angularVelocity = delta.z * Mathf.Rad2Deg * 2.0f / stepTime;
            }
            else
            {
                // if the object is non-kinematic, just copy velocities.
                linearVelocity = unityRigidbody.velocity;
                angularVelocity = unityRigidbody.angularVelocity;
            }

            prevPosition = transform.position;
            prevRotation = transform.rotation;
        }

        public override void UpdateIfNeeded(float stepTime)
        {
            // Rigidbody might not exist, as rigidbody deletion is buffered.
            // This means the unity rigidbody might be deleted before the rigidbody handle is invalidated.
            if (unityRigidbody == null) return;

            CacheVelocities(stepTime);
            var world = ObiColliderWorld.GetInstance();

            var rb = world.rigidbodies[handle.index];
            rb.FromRigidbody(this);
            world.rigidbodies[handle.index] = rb;
        }

		/**
		 * Reads velocities back from the solver.
		 */
		public override void UpdateVelocities(Vector3 linearDelta, Vector3 angularDelta)
        {
            // Rigidbody might not exist, as rigidbody deletion is buffered.
            // This means the unity rigidbody might be deleted before the rigidbody handle is invalidated.
            if (unityRigidbody == null) return;

            // kinematic rigidbodies are passed to Obi with zero velocity, so we must ignore the new velocities calculated by the solver:
            if (Application.isPlaying && !(unityRigidbody.isKinematic || kinematicForParticles))
            {
				unityRigidbody.velocity += new Vector2(linearDelta.x, linearDelta.y);
				unityRigidbody.angularVelocity += angularDelta[2] * Mathf.Rad2Deg;
			}

		}
	}
}

