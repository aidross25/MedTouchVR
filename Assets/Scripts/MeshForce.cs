using Haply.Inverse.Unity;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;  // Add this for generic Queue
using System;

namespace Haply.Samples.Tutorials._2_BasicForceFeedback
{
    public class MeshForce : MonoBehaviour
    {
        // Must assign in inspector
        public Inverse3 inverse3;

        [Range(0, 800)]
        // Stiffness of the force feedback.
        public float stiffness = 300f;

        [Range(0, 3)]
        public float damping = 1f;

        public GameObject ballPrefab;  // Ball prefab to instantiate
        public float ballSize = 0.1f;  // Size of the ball when created
        public float creationInterval = 1f;  // Time interval between ball creations (in seconds)

        // New parameter for cylinder diameter
        [Range(0.01f, 1f)]
        public float cylinderDiameter = 0.05f; // Diameter of the cylinder connecting balls

        // New parameter for cylinder color
        public Color cylinderColor = Color.white;  // Default to white color for the cylinders

        private MeshCollider _meshCollider;
        private Vector3 _cursorRadius;
        private Vector3 _calculatedForce;
        private bool _forceCalculated;

        // Track the last time a ball was created
        private float _lastBallCreationTime = 0f;

        // List to store all the created balls
        private List<GameObject> _balls = new List<GameObject>();

        // List to store the cylinders (instead of LineRenderers) connecting the balls
        private List<GameObject> _cylinders = new List<GameObject>();

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
                Vector3 closestPoint = hitInfo.point;  // Collision point where the ray hit the mesh
                Vector3 normal = hitInfo.normal;  // Normal of the surface at the collision point

                // Calculate penetration (distance from cursor to mesh surface)
                float distance = Vector3.Distance(cursorPosition, closestPoint);
                float penetration = cursorRadius.x - distance;  // Assuming uniform scaling on the cursor

                if (penetration > 0)
                {
                    // Calculate the force based on penetration, stiffness, and the normal at the contact point
                    force = normal * penetration * stiffness;

                    // Apply damping based on the cursor's velocity
                    force -= cursorVelocity * damping;

                    // Check if enough time has passed to create a new ball
                    if (Time.time - _lastBallCreationTime >= creationInterval)
                    {
                        CreateBallAtCollisionPoint(closestPoint);
                        _lastBallCreationTime = Time.time;  // Update last ball creation time
                    }
                }
            }

            _calculatedForce = force;  // Store the calculated force to apply on the haptic thread
            _forceCalculated = true;   // Flag indicating force has been calculated
        }

        /// <summary>
        /// Creates a ball at the exact collision point with the specified size.
        /// </summary>
        private void CreateBallAtCollisionPoint(Vector3 collisionPoint)
        {
            if (ballPrefab != null)
            {
                // Instantiate the ball at the collision point and set its size
                GameObject ball = Instantiate(ballPrefab, collisionPoint, Quaternion.identity);
                ball.transform.localScale = new Vector3(ballSize, ballSize, ballSize);  // Set the ball's size

                // Add the ball to the list of balls
                _balls.Add(ball);

                // If there is more than one ball, create a cylinder to connect the current ball to the previous one
                if (_balls.Count > 1)
                {
                    CreateCylinderBetweenBalls(_balls[_balls.Count - 2], ball);  // Connect the new ball to the previous one
                }
            }
            else
            {
                Debug.LogWarning("Ball Prefab is not assigned in the inspector.");
            }
        }

        /// <summary>
        /// Creates a cylinder that connects two balls.
        /// </summary>
        private void CreateCylinderBetweenBalls(GameObject startBall, GameObject endBall)
        {
            // Calculate the distance between the two balls
            float distance = Vector3.Distance(startBall.transform.position, endBall.transform.position);

            // Create the cylinder
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.transform.SetParent(transform);  // Optionally set as a child of this object
            cylinder.transform.position = (startBall.transform.position + endBall.transform.position) / 2f;  // Position in the middle
            cylinder.transform.LookAt(endBall.transform.position);  // Make it look toward the end ball
            cylinder.transform.Rotate(90f, 0f, 0f);  // Rotate it to align properly

            // Scale the cylinder to match the distance between the balls and the desired diameter
            cylinder.transform.localScale = new Vector3(cylinderDiameter, distance / 2f, cylinderDiameter);

            // Apply the specified cylinder color to the material
            Renderer cylinderRenderer = cylinder.GetComponent<Renderer>();
            if (cylinderRenderer != null)
            {
                cylinderRenderer.material.color = cylinderColor;
            }

            // Add the cylinder to the list for later access
            _cylinders.Add(cylinder);
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

            // Check if the "X" key is pressed and delete all cylinders and connected objects
            if (Input.GetKeyDown(KeyCode.X))
            {
                DeleteAllCylindersAndBalls();
            }
        }

        /// <summary>
        /// Deletes all cylinders and the balls they connect.
        /// </summary>
        private void DeleteAllCylindersAndBalls()
        {
            // Destroy all cylinders
            foreach (var cylinder in _cylinders)
            {
                Destroy(cylinder);
            }
            _cylinders.Clear(); // Clear the list of cylinders

            // Destroy all balls
            foreach (var ball in _balls)
            {
                Destroy(ball);
            }
            _balls.Clear(); // Clear the list of balls
        }
    }
}
