using Haply.Inverse.Unity;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;  // Add this for generic Queue
using System;

namespace Haply.Samples.Tutorials._2_BasicForceFeedback
{
    public class BasicMeshForce : MonoBehaviour
    {
        // Must assign in inspector
        public Inverse3 inverse3;

        [Range(0, 800)]
        // Stiffness of the force feedback.
        public float stiffness = 300f;

        [Range(0, 3)]
        public float damping = 1f;

        private MeshCollider _meshCollider;
        private Vector3 _cursorRadius;
        private Vector3 _calculatedForce;
        private bool _forceCalculated;

        // Queue of Actions to execute on the main thread
        private Queue<Action> _mainThreadActions = new Queue<Action>();

        /// <summary>
        /// Stores the mesh collider and cursor transform data for access by the haptic thread.
        /// </summary>
        private void SaveSceneData()
        {
            _meshCollider = GetComponent<MeshCollider>(); // Assumes MeshCollider is attached to the same object
            _cursorRadius = inverse3.Cursor.Model.transform.lossyScale / 2f; // Assuming uniform scaling
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
        /// Calculates the force based on the cursor's position and mesh collider surface.
        /// This is run on the main thread.
        /// </summary>
        /// <param name="cursorPosition">The position of the cursor.</param>
        /// <param name="cursorVelocity">The velocity of the cursor.</param>
        /// <param name="cursorRadius">The radius of the cursor.</param>
        /// <param name="meshCollider">The mesh collider of the object.</param>
        private void CalculateForceOnMainThread(Vector3 cursorPosition, Vector3 cursorVelocity, Vector3 cursorRadius, MeshCollider meshCollider)
        {
            var force = Vector3.zero;

            // Perform a raycast from the cursor's position toward the mesh to find the closest point on the surface
            RaycastHit hitInfo;
            Ray ray = new Ray(cursorPosition, Vector3.down); // Adjust direction as needed
            if (_meshCollider.Raycast(ray, out hitInfo, Mathf.Infinity))
            {
                // Get the precise contact point and normal
                Vector3 closestPoint = hitInfo.point;
                Vector3 normal = hitInfo.normal;

                // Calculate penetration (distance from cursor to mesh surface)
                float distance = Vector3.Distance(cursorPosition, closestPoint);
                float penetration = cursorRadius.x - distance;  // Assuming uniform scaling on the cursor

                if (penetration > 0)
                {
                    // Calculate the force based on penetration, stiffness, and the normal at the contact point
                    force = normal * penetration * stiffness;

                    // Apply damping based on the cursor's velocity
                    force -= cursorVelocity * damping;
                }
            }

            _calculatedForce = force;  // Store the calculated force to apply on the haptic thread
            _forceCalculated = true;   // Flag indicating force has been calculated
        }

        /// <summary>
        /// Event handler that calculates and sends the force to the device when the cursor's position changes.
        /// This is run on the main thread to ensure compatibility with Unity's thread safety.
        /// </summary>
        /// <param name="device">The Inverse3 device instance.</param>
        private void OnDeviceStateChanged(Inverse3 device)
        {
            // Schedule force calculation to be done on the main thread
            QueueMainThreadAction(() => 
            {
                // Calculate the force on the main thread (safe)
                CalculateForceOnMainThread(device.CursorLocalPosition, device.CursorLocalVelocity, _cursorRadius, _meshCollider);
            });
        }

        /// <summary>
        /// Queue actions to be executed on the main thread.
        /// </summary>
        private void QueueMainThreadAction(Action action)
        {
            lock (_mainThreadActions)
            {
                _mainThreadActions.Enqueue(action);
            }
        }

        /// <summary>
        /// Main thread Update function where queued actions are processed.
        /// </summary>
        private void Update()
        {
            // Execute any actions that were queued on the main thread
            while (_mainThreadActions.Count > 0)
            {
                Action action;
                lock (_mainThreadActions)
                {
                    action = _mainThreadActions.Dequeue();
                }
                action.Invoke();
            }

            // Apply the calculated force to the device's cursor if ready
            if (_forceCalculated)
            {
                inverse3.CursorSetLocalForce(_calculatedForce);
                _forceCalculated = false; // Reset flag
            }
        }
    }
}
