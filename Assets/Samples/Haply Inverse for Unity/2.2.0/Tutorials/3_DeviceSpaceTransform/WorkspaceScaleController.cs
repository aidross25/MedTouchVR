/*
 * Copyright 2024 Haply Robotics Inc. All rights reserved.
 */

using Haply.Inverse.Unity;
using UnityEngine;

namespace Haply.Samples.Tutorials._3_DeviceSpaceTransform
{
    /// <summary>
    /// Controls the scaling of the workspace associated with a HapticOrigin.
    /// Allows for dynamic adjustment of the workspace scale using keyboard inputs.
    /// </summary>
    public class WorkspaceScaleController : MonoBehaviour
    {
        private const float MinimumScale = 1f;
        private const float MaximumScale = 5f;

        [Range(0, 1)]
        [Tooltip("Speed at which the workspace scale changes.")]
        public float scaleSpeed = 0.5f;

        private HapticOrigin _hapticOrigin;

        private void Start()
        {
            if (_hapticOrigin == null)
            {
                _hapticOrigin = GetComponent<HapticOrigin>();
            }
        }

        protected void Update()
        {
            // Get the current scale of the HapticOrigin
            var scale = _hapticOrigin.UniformScale;

            // Adjust scale based on keyboard input
            if (Input.GetKey(KeyCode.Equals))
            {
                scale += scaleSpeed * Time.deltaTime; // Increase scale
            }
            else if (Input.GetKey(KeyCode.Minus))
            {
                scale -= scaleSpeed * Time.deltaTime; // Decrease scale
            }

            // Clamp the scale to be within the defined minimum and maximum limits
            scale = Mathf.Clamp(scale, MinimumScale, MaximumScale);

            // Apply the new scale to the HapticOrigin
            _hapticOrigin.UniformScale = scale;
        }

        private void OnGUI()
        {
            Rect controlBoxRect = new Rect(10, 110, 200, 70);
            GUI.Box(controlBoxRect, "Workspace Scale Controls");

            GUI.Label(new Rect(20, 130, 180, 20), "Increase Scale: Equals key");
            GUI.Label(new Rect(20, 150, 180, 20), "Decrease Scale: Minus key");

            // Display Gizmos toggle button
            GUI.Label(new Rect(Screen.width - 210, 5, 200, 50), "Enable Gizmos to see workspace");
        }
    }
}
