/*
 * Copyright 2024 Haply Robotics Inc. All rights reserved.
 */

using System.Threading;
using Haply.Inverse.Unity;
using Haply.Samples.Tutorials.Utils;
using UnityEngine;

namespace Haply.Samples.Tutorials._4B_DynamicForceLeftRight
{
    /// <summary>
    /// Because the haptic thread can be thousand more faster than physics, multiple haptic loop calls
    /// can occur during one `FixedUpdate()` call, and scene data used for force calculation can be
    /// in inconsistent state.
    ///
    /// So this example shows a thread safe way to synchronize dynamic scene data with the haptic loop.
    /// </summary>
    public class SphereForceFeedback : MonoBehaviour
    {
        [Range(0, 800)]
        // Stiffness of the force feedback.
        public float stiffness = 500f;

        [Range(0, 3)]
        public float damping = 1f;

        // Determines if cursors provide haptic feedback to each other.
        public bool cursorProvidesHapticsToEachOther = true;

        // Reference to the Inverse3 devices.
        private Inverse3[] inverse3s;

        #region Thread-safe cached data

        /// <summary>
        /// Represents scene data that can be updated in the Update() call.
        /// </summary>
        private struct SceneData
        {
            public Vector3 ballPosition;
            public Vector3 ballVelocity;
            public float ballRadius;
            public float[] cursorRadii;
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
                _cachedSceneData.ballPosition = t.localPosition;
                _cachedSceneData.ballRadius = t.lossyScale.x / 2f;

                for (var i = 0; i < inverse3s.Length; i++)
                {
                    _cachedSceneData.cursorRadii[i] = inverse3s[i].Cursor.Model.transform.lossyScale.x / 2f;
                }

                _cachedSceneData.ballVelocity = _movableObject.Velocity;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        #endregion

        /// <summary>
        /// Initializes the array of Inverse3 components and sets up the cursor radius cache.
        /// </summary>
        private void Awake()
        {
            inverse3s = FindObjectsOfType<Inverse3>();
            _movableObject = GetComponent<MovableObject>();
            _cachedSceneData.cursorRadii = new float[inverse3s.Length];
        }


        /// <summary>
        /// Saves the initial scene data.
        /// </summary>
        private void Start() => SaveSceneData();

        /// <summary>
        /// Subscribes to the DeviceStateChanged event for each Inverse3.
        /// </summary>
        private void OnEnable()
        {
            foreach (var inverse3 in inverse3s)
            {
                inverse3.DeviceStateChanged += OnDeviceStateChanged;
            }
        }

        /// <summary>
        /// Unsubscribes from the DeviceStateChanged event for each Inverse3.
        /// </summary>
        private void OnDisable()
        {
            foreach (var inverse3 in inverse3s)
            {
                inverse3.DeviceStateChanged -= OnDeviceStateChanged;
                inverse3.Release();
            }
        }

        /// <summary>
        /// Update scene data cache.
        /// </summary>
        private void Update() => SaveSceneData();

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
            var index = inverse3s[0] == device ? 0 : 1;
            var otherIndex = (index + 1) % inverse3s.Length;

            var sceneData = GetSceneData();

            // Calculate the force exerted by the moving ball. Using 'device.Position' instead of 'device.LocalPosition'
            // ensures the force calculation considers the device's offset and rotation in world space.
            var force = ForceCalculation(device.CursorPosition, device.CursorVelocity, sceneData.cursorRadii[index],
                sceneData.ballPosition, sceneData.ballVelocity, sceneData.ballRadius);

            // Add the other cursor force if more than one device
            if (cursorProvidesHapticsToEachOther && index != otherIndex)
            {
                force += ForceCalculation(device.CursorPosition, device.CursorVelocity, sceneData.cursorRadii[index],
                    inverse3s[otherIndex].CursorPosition, inverse3s[otherIndex].CursorVelocity, sceneData.cursorRadii[otherIndex]);
            }

            // Apply the calculated force to the cursor. Using 'device.CursorSetForce' instead of
            // 'device.CursorSetLocalForce' ensures that the force vector is correctly converted
            // from world space to the device's local space.
            device.CursorSetForce(force);
        }
    }
} // namespace Haply.Samples.DynamicObjectForceLeftRight
