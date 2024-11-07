/*
 * Copyright 2024 Haply Robotics Inc. All rights reserved.
 */

using Haply.Inverse.Unity;
using UnityEngine;

namespace Haply.Samples.Tutorials._2_BasicForceFeedback
{
    public class SphereForceFeedback : MonoBehaviour
    {
        // must assign in inspector
        public Inverse3 inverse3;

        [Range(0, 800)]
        // Stiffness of the force feedback.
        public float stiffness = 300f;

        [Range(0, 3)]
        public float damping = 1f;

        private Vector3 _ballPosition;
        private float _ballRadius;
        private float _cursorRadius;

        /// <summary>
        /// Stores the cursor and sphere transform data for access by the haptic thread.
        /// </summary>
        private void SaveSceneData()
        {
            var t = transform;
            _ballPosition = t.position;
            _ballRadius = t.lossyScale.x / 2f;

            _cursorRadius = inverse3.Cursor.Model.transform.lossyScale.x / 2f;
        }

        /// <summary>
        /// Saves the initial scene data cache.
        /// </summary>
        private void Awake()
        {
            SaveSceneData();
        }

        /// <summary>
        /// Subscribes to the DeviceStateChanged event.
        /// </summary>
        private void OnEnable()
        {
            inverse3.DeviceStateChanged += OnDeviceStateChanged;
        }

        /// <summary>
        /// Unsubscribes from the DeviceStateChanged event.
        /// </summary>
        private void OnDisable()
        {
            inverse3.DeviceStateChanged -= OnDeviceStateChanged;
            inverse3.Release();
        }

        /// <summary>
        /// Calculates the force based on the cursor's position and another sphere position.
        /// </summary>
        /// <param name="cursorPosition">The position of the cursor.</param>
        /// <param name="cursorVelocity">The velocity of the cursor.</param>
        /// <param name="cursorRadius">The radius of the cursor.</param>
        /// <param name="otherPosition">The position of the other sphere (e.g., ball).</param>
        /// <param name="otherRadius">The radius of the other sphere.</param>
        /// <returns>The calculated force vector.</returns>
        private Vector3 ForceCalculation(Vector3 cursorPosition, Vector3 cursorVelocity, float cursorRadius,
            Vector3 otherPosition, float otherRadius)
        {
            var force = Vector3.zero;

            var distanceVector = cursorPosition - otherPosition;
            var distance = distanceVector.magnitude;
            var penetration = otherRadius + cursorRadius - distance;

            if (penetration > 0)
            {
                // Normalize the distance vector to get the direction of the force
                var normal = distanceVector.normalized;

                // Calculate the force based on penetration
                force = normal * penetration * stiffness;

                // Apply damping based on the cursor velocity
                force -= cursorVelocity * damping;
            }

            return force;
        }

        /// <summary>
        /// Event handler that calculates and send the force to the device when the cursor's position changes.
        /// </summary>
        /// <param name="device">The Inverse3 device instance.</param>
        private void OnDeviceStateChanged(Inverse3 device)
        {
            // Calculate the ball force
            var force = ForceCalculation(device.CursorLocalPosition, device.CursorLocalVelocity,
                _cursorRadius, _ballPosition, _ballRadius);

            device.CursorSetLocalForce(force);
        }
    }
}
