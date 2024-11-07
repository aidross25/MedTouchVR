/*
 * Copyright 2024 Haply Robotics Inc. All rights reserved.
 */

using Haply.Inverse.Unity;
using UnityEngine;

namespace Haply.Samples.Tutorials._6_VerseGripPositionControl
{
    /// <summary>
    /// Demonstrates how to control the device cursor position using the VerseGrip.
    /// </summary>
    public class VerseGripPositionControl : MonoBehaviour
    {
        // Must be assigned in inspector
        public Inverse3 inverse3;
        public VerseGrip verseGrip;

        [Tooltip("Cursor moving speed")]
        [Range(0, 1)]
        public float speed = 0.5f;

        [Tooltip("Maximum radius for cursor movement")]
        [Range(0, 0.2f)]
        public float movementLimitRadius = 0.2f;

        private Vector3 _targetPosition; // Target position for the cursor

        /// <summary>
        /// Subscribes to the DeviceStateChanged event.
        /// </summary>
        private void OnEnable()
        {
            verseGrip.DeviceStateChanged += OnDeviceStateChanged;
        }

        /// <summary>
        /// Unsubscribes from the DeviceStateChanged event.
        /// </summary>
        private void OnDisable()
        {
            verseGrip.DeviceStateChanged -= OnDeviceStateChanged;
            inverse3.Release();
        }

        private void Update()
        {
            // Check for space key to disable position control
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Reset cursor force to disable position control
                inverse3.Release();
            }
        }

        private void OnDeviceStateChanged(VerseGrip grip)
        {
            // Calculate the direction based on the VerseGrip's rotation
            var direction = grip.Orientation * Vector3.forward;

            // Check if the VerseGrip button is pressed down
            if (grip.GetButtonDown())
            {
                // Initialize target position
                _targetPosition = inverse3.CursorLocalPosition;
            }

            // Check if the VerseGrip button is being held down
            if (grip.GetButton())
            {
                // Move the target position toward the grip direction
                _targetPosition += direction * (0.0025f * speed);

                // Clamp the target position within the movement limit radius
                var workspaceCenter = inverse3.WorkspaceCenterLocalPosition;
                _targetPosition = Vector3.ClampMagnitude(_targetPosition - workspaceCenter, movementLimitRadius)
                    + workspaceCenter;

                // Move cursor to new position
                inverse3.CursorSetLocalPosition(_targetPosition);
            }
        }

        # region Optional GUI Display and Gizmos
        // --------------------
        // Optional GUI Display
        // --------------------

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(inverse3.WorkspaceCenterLocalPosition, movementLimitRadius); // Draw movement limit
        }

        private void OnGUI()
        {
            const float width = 600;
            const float height = 60;
            var rect = new Rect((Screen.width - width) / 2, Screen.height - height - 10, width, height);

            var text = verseGrip.GetButton()
                ? "Rotate the VerseGrip to change the cursor's movement direction."
                : "Press and hold the VerseGrip button to move the cursor in the pointed direction.";

            GUI.Box(rect, text, CenteredStyle());
        }

        private static GUIStyle CenteredStyle()
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = Color.white
                },
                fontSize = 14
            };
            return style;
        }

        #endregion
    }
}
