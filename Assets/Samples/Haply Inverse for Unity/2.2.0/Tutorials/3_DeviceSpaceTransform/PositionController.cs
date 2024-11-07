/*
 * Copyright 2024 Haply Robotics Inc. All rights reserved.
 */

using UnityEngine;

namespace Haply.Samples.Tutorials._3_DeviceSpaceTransform
{
    /// <summary>
    /// Controls the position and rotation of a GameObject using keyboard inputs.
    /// Allows for movement along the X, Y, and Z axes and rotation around the Y-axis.
    /// </summary>
    public class PositionController : MonoBehaviour
    {
        [Range(0, 1)]
        [Tooltip("Speed of movement along the X, Y, and Z axes.")]
        public float moveSpeed = 0.5f;

        [Range(0, 1)]
        [Tooltip("Speed of rotation around the Y-axis.")]
        public float rotationSpeed = 0.5f;

        private Quaternion _targetRotation;

        void Start()
        {
            // Initialize the target rotation to the current rotation
            _targetRotation = transform.rotation;
        }

        private void Update()
        {
            // Calculate movement based on keyboard input
            var movement = new Vector3(
                Input.GetAxis("Horizontal"), // A/D keys for left/right
                0f, // No vertical movement here
                Input.GetAxis("Vertical") // W/S keys for forward/backward
            );

            // Adjust for E/Q keys for up/down
            if (Input.GetKey(KeyCode.E))
            {
                movement.y = 1f; // Move up
            }
            else if (Input.GetKey(KeyCode.Q))
            {
                movement.y = -1f; // Move down
            }

            // Apply the movement in world space
            transform.position += movement * (moveSpeed * 0.1f * Time.deltaTime);

            // Rotation logic
            if (Input.GetKeyDown(KeyCode.R))
            {
                _targetRotation *= Quaternion.Euler(0f, 90f, 0f); // Rotate 90 degrees to the right
            }
            else if (Input.GetKeyDown(KeyCode.L))
            {
                _targetRotation *= Quaternion.Euler(0f, -90f, 0f); // Rotate 90 degrees to the left
            }

            // Smoothly rotate towards the target rotation
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _targetRotation,
                90f * rotationSpeed * Time.deltaTime);
        }

        private void OnGUI()
        {
            var controlBoxRect = new Rect(10, 10, 200, 90);
            GUI.Box(controlBoxRect, "Device position controls");
            GUI.Label(new Rect(20, 30, 180, 20), "Move: WASDQE keys");
            GUI.Label(new Rect(20, 50, 180, 20), "Rotate Right: R key");
            GUI.Label(new Rect(20, 70, 180, 20), "Rotate Left: L key");
        }
    }
}
