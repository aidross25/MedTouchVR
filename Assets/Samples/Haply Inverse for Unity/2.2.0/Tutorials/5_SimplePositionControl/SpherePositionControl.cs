/*
 * Copyright 2024 Haply Robotics Inc. All rights reserved.
 */

using Haply.Inverse.Unity;
using Haply.Samples.Tutorials.Utils;
using UnityEngine;

namespace Haply.Samples.Tutorials._5_SimplePositionControl
{
    /// <summary>
    /// Controls the Inverse3 cursor position based on the current position of this GameObject.
    /// When the GameObject is within a specified distance from the cursor, it initiates synchronized control,
    /// allowing the cursor to follow the GameObject's movements.
    /// </summary>
    [RequireComponent(typeof(MovableObject))]
    public class SpherePositionControl : MonoBehaviour
    {
        public Inverse3 inverse3;

        [Tooltip("Minimum distance required to initiate synchronized control between this GameObject and the Inverse3 cursor.")]
        [Range(0, 1)]
        public float minSyncDistance = 0.05f;

        private bool _isCursorSynchronized;

        private void Awake()
        {
            // Ensure inverse3 is set, finding it in the scene if necessary.
            if (inverse3 == null)
            {
                inverse3 = FindObjectOfType<Inverse3>();
            }

            // When inverse3 is ready, so the handedness is defined
            inverse3.Ready.AddListener(device =>
            {
                // Teleport the sphere to its workspace center to ensure it can be reached,
                // regardless of whether the device is left or right-handed. This ensures the GameObject starts in a
                // position that is accessible by the Inverse3 device.
                GetComponent<MovableObject>().SetTargetPosition(device.WorkspaceCenterLocalPosition, teleport:true);
            });
        }

        private void OnDisable()
        {
            // Ensure movement synchronization is disabled when the component is disabled.
            StopSynchronizeCursor();
            inverse3.Release();
        }

        private void Update()
        {
            // Calculate the distance between the Inverse3 position and this object's position.
            var distance = Vector3.Distance(inverse3.CursorPosition, transform.position);

            // Enable synchronized movement if within the minimum sync distance and not already synced.
            if (!_isCursorSynchronized && distance <= minSyncDistance)
            {
                StartSynchronizeCursor();
            }
            // Disable synchronized movement if outside the minimum sync distance and currently synced.
            else if (_isCursorSynchronized && distance > minSyncDistance)
            {
                StopSynchronizeCursor();
            }
        }

        private void FixedUpdate()
        {
            if (_isCursorSynchronized)
            {
                // If in sync, set the Inverse3 cursor position to this object's position.
                inverse3.CursorSetPosition(transform.position);
            }
        }

        private void StartSynchronizeCursor()
        {
            // Get the current cursor position.
            var cursorPosition = inverse3.Cursor.transform.position;

            // Teleport this object to the cursor position to avoid a sudden jump when position control starts.
            GetComponent<MovableObject>().SetTargetPosition(cursorPosition, teleport:true);

            // Start synchronizing the movement of this object with the cursor.
            _isCursorSynchronized = true;
        }

        private void StopSynchronizeCursor()
        {
            // Stop synchronizing the movement.
            _isCursorSynchronized = !inverse3.Release();
        }

        # region Optional GUI Display

        // --------------------
        // Optional GUI Display
        // --------------------

        private void OnGUI()
        {
            const float width = 400;
            const float height = 60;
            var rect = new Rect((Screen.width - width) / 2, Screen.height - height - 10, width, height);

            var text = _isCursorSynchronized
                ? "Position Control is active. Device LED is blue.\nUse controls to move the cursor."
                : "Align the cursor with the sphere to start 'Position Control'.";

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
