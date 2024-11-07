/*
 * Copyright 2024 Haply Robotics Inc. All rights reserved.
 */

using System.Threading;
using Haply.Inverse.Unity;
using Haply.Samples.Tutorials.Utils;
using UnityEngine;

namespace Haply.Samples.Tutorials._4A_DynamicForceFeedback
{
    public class SphereForceFeedback : MonoBehaviour
    {
        // must assign in inspector
        public Inverse3 inverse3;

        [Range(0, 800)]
        // Stiffness of the force feedback.
        public float stiffness = 500f;

        [Range(0, 3)]
        public float damping = 1f;

        #region Thread-safe cached data

        /// <summary>
        /// Represents scene data that can be updated in the Update() call.
        /// </summary>
        private struct SceneData
        {
            public Vector3 ballPosition;
            public Vector3 ballVelocity;
            public float ballRadius;
            public float cursorRadius;
        }

        /// <summary>
        /// Cached version of the scene data.
        /// </summary>
        private SceneData _cachedSceneData;

        private MovableObject _movableObject;

        /// <summary>
        /// Lock to ensure thread safety when reading or writing to the cache.
        /// </summary>
        private readonly ReaderWriterLockSlim _cacheLock = new();

        /// <summary>
        /// Safely reads the cached data.
        /// </summary>
        /// <returns>The cached scene data.</returns>
        private SceneData GetSceneData()
        {
            _cacheLock.EnterReadLock();
            try
            {
                return _cachedSceneData;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Safely updates the cached data.
        /// </summary>
        private void SaveSceneData()
        {
            _cacheLock.EnterWriteLock();
            try
            {
                var t = transform;
                _cachedSceneData.ballPosition = t.position;
                _cachedSceneData.ballRadius = t.lossyScale.x / 2f;

                _cachedSceneData.cursorRadius = inverse3.Cursor.Model.transform.lossyScale.x / 2f;

                _cachedSceneData.ballVelocity = _movableObject.Velocity;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        #endregion

        /// <summary>
        /// Saves the initial scene data cache.
        /// </summary>
        private void Start()
        {
            _movableObject = GetComponent<MovableObject>();
            SaveSceneData();
        }

        /// <summary>
        /// Update scene data cache.
        /// </summary>
        private void FixedUpdate()
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
        /// <param name="otherVelocity">The velocity of the other sphere (e.g., ball).</param>
        /// <param name="otherRadius">The radius of the other sphere.</param>
        /// <returns>The calculated force vector.</returns>
        private Vector3 ForceCalculation(Vector3 cursorPosition, Vector3 cursorVelocity, float cursorRadius,
            Vector3 otherPosition, Vector3 otherVelocity, float otherRadius)
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

                // Calculate the relative velocity
                var relativeVelocity = cursorVelocity - otherVelocity;

                // Apply damping based on the relative velocity
                force -= relativeVelocity * damping;
            }

            return force;
        }

        /// <summary>
        /// Event handler that calculates and send the force to the device when the cursor's position changes.
        /// </summary>
        /// <param name="device">The Inverse3 device instance.</param>
        private void OnDeviceStateChanged(Inverse3 device)
        {
            var sceneData = GetSceneData();

            // Calculate the moving ball force.
            var force = ForceCalculation(device.CursorLocalPosition, device.CursorLocalVelocity, sceneData.cursorRadius,
                sceneData.ballPosition, sceneData.ballVelocity, sceneData.ballRadius);

            // Apply the force to the cursor.
            device.CursorSetLocalForce(force);
        }
    }
}
