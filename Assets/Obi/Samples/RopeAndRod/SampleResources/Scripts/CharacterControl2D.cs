using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Obi.Samples
{
    public class CharacterControl2D : MonoBehaviour
    {

        public float floorRaycastDistance = 1.2f;

        [Header("Grounded")]
        public float acceleration = 80;
        public float maxSpeed = 6;
        public float damping = 0.005f;
        public float jumpPower = 10;

        [Header("Airborne")]
        public float airAcceleration = 16;
        public float airMaxSpeed = 12;
        public float extraGravity = -12;

        [Header("Auto upright")]
        public Vector3 centerOfMass = new Vector3(0, -0.25f, 0);
        public float P = 2;
        public float D = 0.1f;

        private Rigidbody unityRigidbody;
        private float axis;
        private bool grounded;
        private float error;
        private float prevError;

        public void Awake()
        {
            unityRigidbody = GetComponent<Rigidbody>();
            unityRigidbody.centerOfMass = centerOfMass;
        }

        private void Update()
        {
            axis = Input.GetAxisRaw("Horizontal");
            grounded = Physics.Raycast(new Ray(transform.position, -Vector3.up), floorRaycastDistance);

            if (Input.GetButtonDown("Jump") && grounded)
                unityRigidbody.AddForce(Vector3.up * jumpPower, ForceMode.VelocityChange);
        }

        private void FixedUpdate()
        {
            var velocity = unityRigidbody.velocity;

            prevError = error;
            error = Vector3.SignedAngle(unityRigidbody.transform.up, Vector3.up, Vector3.forward);

            if (grounded)
            {
                float accel = axis * acceleration * Time.deltaTime;
                if ((velocity.x < maxSpeed && accel > 0) || (velocity.x > -maxSpeed && accel < 0))
                    velocity.x += accel;

                if (Mathf.Approximately(axis, 0))
                    velocity.x *= Mathf.Pow(damping, Time.deltaTime);

                unityRigidbody.AddTorque(new Vector3(0, 0, error * P + (error - prevError) / Time.deltaTime * D));
            }
            else
            {
                float accel = axis * airAcceleration * Time.deltaTime;
                if ((velocity.x < airMaxSpeed && accel > 0) || (velocity.x > -airMaxSpeed && accel < 0))
                    velocity.x += accel;

                velocity.y += extraGravity * Time.deltaTime;
            }

            unityRigidbody.velocity = velocity;
        }
    }
}