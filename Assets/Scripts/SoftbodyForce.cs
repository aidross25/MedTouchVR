using Haply.Inverse.Unity;
using UnityEngine;
using Obi;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Haply.Samples.Tutorials._2_BasicForceFeedback
{
    public class SoftbodyForce : MonoBehaviour
    {
        // Required references
        public Inverse3 inverse3; // Haply device
        public ObiSolver obiSolver; // ObiSolver managing the softbody simulation

        [Range(0, 1000)] public float stiffness = 300f;
        [Range(0, 5)] public float damping = 1f;
        [Range(0.1f, 100f)] public float maxForce = 100f;
        [Range(0.001f, 0.1f)] public float collisionThreshold = 0.01f; // Contact range for interaction

        public GameObject ballPrefab; // Prefab for visual feedback
        public float ballSize = 0.1f; // Size of the balls
        public float creationInterval = 1f; // Minimum interval between ball creations

        public float cylinderDiameter = 0.05f; // Diameter of cylinders connecting balls
        public Color cylinderColor = Color.white; // Color of cylinders

        private Vector3 _calculatedForce; // Force applied to the Haply device
        private bool _forceCalculated; // Indicates if a force was calculated

        private float _lastBallCreationTime = 0f; // Timestamp of last ball creation
        private List<GameObject> _balls = new List<GameObject>(); // List of created balls
        private List<GameObject> _cylinders = new List<GameObject>(); // List of created cylinders

        private Queue<Action> _mainThreadActions = new Queue<Action>(); // Queue for main thread actions
        private bool isPressing = false; // Tracks button press state
        private GameObject lastBall = null; // Reference to the last created ball

        private float lastButtonPressTime = 0f; // Timestamp of the last button press
        private float doubleClickThreshold = 0.35f; // Time threshold for double-click detection

        private void Awake()
        {
            if (obiSolver == null)
                Debug.LogError("ObiSolver is not assigned!");
            if (inverse3 == null)
                Debug.LogError("Haply device is not assigned!");
        }

        private void OnEnable()
        {
            inverse3.DeviceStateChanged += OnDeviceStateChanged;
        }

        private void OnDisable()
        {
            inverse3.DeviceStateChanged -= OnDeviceStateChanged;
            inverse3.Release();
        }

        /// <summary>
        /// Handles Haply device state changes and calculates forces.
        /// </summary>
        private void OnDeviceStateChanged(Inverse3 device)
        {
            QueueMainThreadAction(() =>
            {
                Vector3 cursorPosition = device.Cursor.Model.transform.position;
                Vector3 cursorVelocity = device.Cursor.Model.transform.forward; // Use the cursor model's movement
                CalculateForceOnContact(cursorPosition, cursorVelocity);
            });
        }

        /// <summary>
        /// Calculates force only when the cursor model touches the softbody particles.
        /// </summary>
        private void CalculateForceOnContact(Vector3 cursorPosition, Vector3 cursorVelocity)
        {
            _calculatedForce = Vector3.zero; // Reset force

            for (int i = 0; i < obiSolver.positions.count; i++)
            {
                Vector3 particlePosition = obiSolver.positions[i];
                float distance = Vector3.Distance(cursorPosition, particlePosition);

                // Interact only if within the collision threshold
                if (distance <= collisionThreshold)
                {
                    Vector3 normal = (particlePosition - cursorPosition).normalized;
                    float penetration = collisionThreshold - distance;

                    if (penetration > 0)
                    {
                        // Calculate the interaction force
                        Vector3 force = normal * penetration * stiffness;

                        // Apply damping
                        force -= cursorVelocity * damping;

                        // Clamp the force magnitude
                        force = Vector3.ClampMagnitude(force, maxForce);

                        // Accumulate the force for Haply feedback
                        _calculatedForce += force;

                        // Debug visualization
                        Debug.DrawRay(particlePosition, force, Color.red, 0.1f);

                        // Create visual feedback
                        if (isPressing && Time.time - _lastBallCreationTime >= creationInterval)
                        {
                            CreateBallAtCollisionPoint(particlePosition);
                            _lastBallCreationTime = Time.time;
                        }
                    }
                }
            }

            _forceCalculated = _calculatedForce.sqrMagnitude > 0; // Indicate if force was calculated
        }

        /// <summary>
        /// Creates a ball at the collision point for visual feedback.
        /// </summary>
        private void CreateBallAtCollisionPoint(Vector3 collisionPoint)
        {
            if (ballPrefab != null)
            {
                GameObject ball = Instantiate(ballPrefab, collisionPoint, Quaternion.identity);
                ball.transform.localScale = Vector3.one * ballSize;
                _balls.Add(ball);

                if (lastBall != null)
                {
                    CreateCylinderBetweenBalls(lastBall, ball);
                }

                lastBall = ball;
            }
            else
            {
                Debug.LogWarning("Ball Prefab is not assigned.");
            }
        }

        /// <summary>
        /// Creates a cylinder between two balls for visual feedback.
        /// </summary>
        private void CreateCylinderBetweenBalls(GameObject startBall, GameObject endBall)
        {
            Vector3 start = startBall.transform.position;
            Vector3 end = endBall.transform.position;

            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.transform.SetParent(null);
            cylinder.transform.position = (start + end) / 2f;
            cylinder.transform.up = (end - start).normalized;
            cylinder.transform.localScale = new Vector3(
                cylinderDiameter,
                Vector3.Distance(start, end) / 2f,
                cylinderDiameter);

            Renderer renderer = cylinder.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = cylinderColor;

            _cylinders.Add(cylinder);
        }

        private void OnButtonDown(VerseGrip grip, VerseGripButton button)
        {
            if (Time.time - lastButtonPressTime <= doubleClickThreshold)
            {
                DeleteAllCylindersAndBalls();
            }
            else
            {
                lastButtonPressTime = Time.time;
            }

            isPressing = true;
            lastBall = null; // Reset the last ball
        }

        private void OnButtonUp(VerseGrip grip, VerseGripButton button)
        {
            isPressing = false;
        }

        private void DeleteAllCylindersAndBalls()
        {
            foreach (var cylinder in _cylinders)
                Destroy(cylinder);

            foreach (var ball in _balls)
                Destroy(ball);

            _cylinders.Clear();
            _balls.Clear();
        }

        private void QueueMainThreadAction(Action action)
        {
            lock (_mainThreadActions)
            {
                _mainThreadActions.Enqueue(action);
            }
        }

        private void Update()
        {
            while (_mainThreadActions.Count > 0)
            {
                Action action;
                lock (_mainThreadActions)
                {
                    action = _mainThreadActions.Dequeue();
                }
                action.Invoke();
            }

            if (_forceCalculated)
            {
                inverse3.CursorSetLocalForce(_calculatedForce);
                _forceCalculated = false; // Reset after sending the force
            }
        }

        private void OnDrawGizmos()
        {
            if (inverse3 != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(inverse3.Cursor.Model.transform.position, collisionThreshold);
            }
        }
    }
}
