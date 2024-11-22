using Haply.Inverse.Unity;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Haply.Samples.Tutorials._2_BasicForceFeedback
{
    public class MeshForce : MonoBehaviour
    {
        // Must assign in inspector
        public Inverse3 inverse3;
        public VerseGrip versegripController; // VersegripController for button input

        [Range(0, 1000)]
        public float stiffness = 300f;

        [Range(0, 5)]
        public float damping = 1f;

        public GameObject ballPrefab;
        public float ballSize = 0.1f;
        public float creationInterval = 1f;

        [Range(0.001f, 1f)]
        public float cylinderDiameter = 0.05f;

        public Color cylinderColor = Color.white;

        private MeshCollider _meshCollider;
        private Vector3 _cursorRadius;
        private Vector3 _calculatedForce;
        private bool _forceCalculated;

        private float _lastBallCreationTime = 0f;

        private List<GameObject> _balls = new List<GameObject>();
        private List<GameObject> _cylinders = new List<GameObject>();
        private Queue<Action> _mainThreadActions = new Queue<Action>();

        private bool isPressing = false; // Tracks whether the button is currently being pressed
        private GameObject lastBall = null; // Tracks the last ball created in the current session

        // Double-click detection variables
        private float lastButtonPressTime = 0f; // Time of the last button press
        private float doubleClickThreshold = 0.35f; // Maximum time between clicks for a double-click (in seconds)

        private void SaveSceneData()
        {
            _meshCollider = GetComponent<MeshCollider>();
            _cursorRadius = inverse3.Cursor.Model.transform.lossyScale / 2f;
        }

        private void Awake()
        {
            SaveSceneData();
        }

        private void OnEnable()
        {
            inverse3.DeviceStateChanged += OnDeviceStateChanged;

            // Subscribe to Versegrip button events
            versegripController.ButtonDown.AddListener(OnButtonDown);
            versegripController.ButtonUp.AddListener(OnButtonUp);
        }

        private void OnDisable()
        {
            inverse3.DeviceStateChanged -= OnDeviceStateChanged;
            inverse3.Release();

            // Unsubscribe from Versegrip button events
            versegripController.ButtonDown.RemoveListener(OnButtonDown);
            versegripController.ButtonUp.RemoveListener(OnButtonUp);
        }

        private void CalculateForceOnMainThread(Vector3 cursorPosition, Vector3 cursorVelocity, Vector3 cursorRadius, MeshCollider meshCollider)
        {
            var force = Vector3.zero;

            // Adjust raycast to prevent clipping
            RaycastHit hitInfo;
            Ray ray = new Ray(cursorPosition, Vector3.down);
            if (_meshCollider.Raycast(ray, out hitInfo, Mathf.Infinity))
            {
                Vector3 closestPoint = hitInfo.point;
                Vector3 normal = hitInfo.normal;

                // Adjust distance and penetration for better collision feedback
                float distance = Vector3.Distance(cursorPosition, closestPoint);
                float penetration = cursorRadius.x - distance;

                // Only apply force if there is penetration
                if (penetration > 0)
                {
                    force = normal * penetration * stiffness;
                    force -= cursorVelocity * damping;

                    // Prevent rapid ball creation and control spacing
                    if (isPressing && Time.time - _lastBallCreationTime >= creationInterval)
                    {
                        CreateBallAtCollisionPoint(closestPoint);
                        _lastBallCreationTime = Time.time;
                    }
                }
                else
                {
                    // Apply a gentle repulsion to prevent clipping, without pushing the cursor too far
                    force = normal * -0.1f; // Slight force to prevent penetration
                }
            }

            _calculatedForce = force;
            _forceCalculated = true;
        }

        private void CreateBallAtCollisionPoint(Vector3 collisionPoint)
        {
            if (ballPrefab != null)
            {
                GameObject ball = Instantiate(ballPrefab, collisionPoint, Quaternion.identity);
                ball.transform.localScale = Vector3.one * ballSize;
                _balls.Add(ball);

                // Only create a cylinder if this is not the first ball in the current session
                if (lastBall != null)
                {
                    CreateCylinderBetweenBalls(lastBall, ball);
                }

                lastBall = ball; // Update the last ball for the current session
            }
            else
            {
                Debug.LogWarning("Ball Prefab is not assigned in the inspector.");
            }
        }

        private void CreateCylinderBetweenBalls(GameObject startBall, GameObject endBall)
        {
            Vector3 start = startBall.transform.position;
            Vector3 end = endBall.transform.position;

            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.transform.SetParent(null); // Detach from parent to avoid inheriting transforms
            cylinder.transform.position = (start + end) / 2f;
            cylinder.transform.up = (end - start).normalized; // Explicitly set the cylinder's direction
            cylinder.transform.localScale = new Vector3(
                cylinderDiameter,
                Vector3.Distance(start, end) / 2f,
                cylinderDiameter);

            Renderer renderer = cylinder.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = cylinderColor;
            }

            _cylinders.Add(cylinder);
        }

        private void OnDeviceStateChanged(Inverse3 device)
        {
            QueueMainThreadAction(() =>
            {
                CalculateForceOnMainThread(device.CursorLocalPosition, device.CursorLocalVelocity, _cursorRadius, _meshCollider);
            });
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
                _forceCalculated = false;
            }
        }

        private void OnButtonDown(VerseGrip grip, VerseGripButton button)
        {
            // Detect if this is a double-click event
            if (Time.time - lastButtonPressTime <= doubleClickThreshold)
            {
                // Double-click detected, clear all objects
                DeleteAllCylindersAndBalls();
            }
            else
            {
                lastButtonPressTime = Time.time; // Update the last button press time
            }

            if (!isPressing)
            {
                isPressing = true;
                lastBall = null; // Reset last ball when starting a new session
            }
        }

        private void OnButtonUp(VerseGrip grip, VerseGripButton button)
        {
            if (isPressing)
            {
                isPressing = false; // Stop creating balls and cylinders
            }
        }

        private void DeleteAllCylindersAndBalls()
        {
            foreach (var cylinder in _cylinders)
            {
                Destroy(cylinder);
            }
            _cylinders.Clear();

            foreach (var ball in _balls)
            {
                Destroy(ball);
            }
            _balls.Clear();
        }
    }
}
