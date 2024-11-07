/*
 * Copyright 2024 Haply Robotics Inc. All rights reserved.
 */

using Haply.Inverse.Unity;
using UnityEngine;

namespace Haply.Samples.Tutorials._1_ForceAndPosition
{
    /// <summary>
    /// Demonstrates the application of force to maintain the cursor at its initial position.
    /// </summary>
    public class ForceAndPosition : MonoBehaviour
    {
        // Must be assigned in inspector
        public Inverse3 inverse3;

        [Range(0, 400)]
        // Stiffness of the force feedback.
        public float stiffness = 100;

        // Stores the initial position of the cursor.
        private Vector3 _initialPosition = Vector3.zero;

        /// <summary>
        /// Subscribes to the DeviceStateChanged event when the component is enabled.
        /// </summary>
        protected void OnEnable()
        {
            inverse3.DeviceStateChanged += OnDeviceStateChanged;
        }

        /// <summary>
        /// Unsubscribes from the DeviceStateChanged event and reset the force when the component is disabled.
        /// </summary>
        protected void OnDisable()
        {
            inverse3.DeviceStateChanged -= OnDeviceStateChanged;
            inverse3.Release();
        }

        /// <summary>
        /// Calculates the force required to maintain the cursor at its initial position.
        /// </summary>
        /// <param name="position">The current position of the cursor.</param>
        /// <returns>The calculated force vector.</returns>
        private Vector3 ForceCalculation(in Vector3 position)
        {
            if (_initialPosition == Vector3.zero)
            {
                // save the first device effector position
                _initialPosition = position;
            }
            // return opposite force to stay at initial position
            return (_initialPosition - position) * stiffness;
        }

        /// <summary>
        /// Event handler that calculates and send the force to the device when the cursor's position changes.
        /// </summary>
        /// <param name="device">The Inverse3 device instance.</param>
        private void OnDeviceStateChanged(Inverse3 device)
        {
            // Calculate the force.
            var force = ForceCalculation(device.CursorLocalPosition);

            // Apply the force to the cursor.
            inverse3.CursorSetLocalForce(force);
        }
    }
}
