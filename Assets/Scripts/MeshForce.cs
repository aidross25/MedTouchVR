using Haply.Inverse.Unity;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

/* 
    This code is organized into regions to help readability


    This code handles 2 distinct tasks: 
    1) Outputs a force on the Haply Inverse3 from the Asset (targetAsset)
    2) Creates a suture object. 



    The suture object is dynamically created as a series of cylinder objects (threadPartition).
    This suture object includes an (optionally visible) dynamically created sphere at the meeting of two threadPartitions (joint)

    The threadPartition is created by starting a cylinder at one joint, and ending at the next. (n-1 thread partitions for n joints)

 Caleb Pope, Capstone Intern of MIE 01/07/2025
*/

namespace Haply.Samples.Tutorials._2_BasicForceFeedback
{
    public class MeshForce : MonoBehaviour
    {
    #region Initialize


        //Haply Devices


        //Inverse3 for Force Feedback
        public Inverse3 inverse3;

        //VerseGrip to use the button (which happens to be on the haptic device)
        public VerseGrip versegripController;








        //Force Scalars


        //Stiffness Scalar "acts like a spring, generating more force the more it is compressed." -https://docs.haply.co/inverseSDK/2.2.0/unity/tutorials/basic-force-feedback
        [Range(0, 1000)]
        public float stiffness = 300f;

        //Damping Scalar "represents an object's resistance to movement, offering more resistance the faster it is moved through"
        [Range(0, 5)]
        public float damping = 1f;








        //joint


        //Asset Model
        public GameObject ballPrefab;

        //Asset Size
        public float ballSize = 0.1f;

        //Delay scalar (minimum time which must elapse before next joint may be created)
        public float creationInterval = 1f;

        //Time since last joint was created (used with creationInterval to prevent overwhelmingly rapid joint creation)
        private float _lastBallCreationTime = 0f;

        //Last joint created (where the beginning of the next threadPartition will be)
        private GameObject lastBall = null;












        //threadPartition


        //Thickness Scalar
        [Range(0.001f, 1f)]
        public float cylinderDiameter = 0.05f;

        //Color
        public Color cylinderColor = Color.white;











        //Haply/targetAsset interaction fields


        //targetAsset mesh
        private MeshCollider _meshCollider;

        //Radius of Haply's cursor asset. Too small will phase through targetAsset. Too big is clunky
        private Vector3 _cursorRadius;

        //Current force the haply should be outputting
        private Vector3 _calculatedForce;

        //Whether a force is being calculated
        private bool _forceCalculated;









        //Data Structures


        //List of existing joints
        private List<GameObject> _balls = new List<GameObject>();

        //List of existing threadPartitions
        private List<GameObject> _cylinders = new List<GameObject>();

        //List of actions the main thread should handle
        private Queue<Action> _mainThreadActions = new Queue<Action>();










        //Versegrip


        //Whether the button is being pressed
        private bool isPressing = false;

        //Time since last clicked Versegrip button
        private float lastButtonPressTime = 0f; 

        //Maximum time between Versegrip button "double clicks"
        private float doubleClickThreshold = 0.35f; 

    #endregion

    #region Haply Maintenance Functions

        //"Stores the cursor and [targetAsset] transform data for access by the haptic thread." -https://docs.haply.co/inverseSDK/2.2.0/unity/tutorials/basic-force-feedback
        private void SaveSceneData()
        {
            _meshCollider = GetComponent<MeshCollider>();
            _cursorRadius = inverse3.Cursor.Model.transform.lossyScale / 2f;
        }

        //"Saves the initial scene data cache" -https://docs.haply.co/inverseSDK/2.2.0/unity/tutorials/basic-force-feedback
        private void Awake()
        {
            SaveSceneData();
        }

        //"Subscribes to the DeviceStateChanged event." -https://docs.haply.co/inverseSDK/2.2.0/unity/tutorials/basic-force-feedback
        private void OnEnable()
        {
            inverse3.DeviceStateChanged += OnDeviceStateChanged;

            //also subscribe from versegrip button events
            versegripController.ButtonDown.AddListener(OnButtonDown);
            versegripController.ButtonUp.AddListener(OnButtonUp);
        }

        //"Unsubscribes from the DeviceStateChanged event." - https://docs.haply.co/inverseSDK/2.2.0/unity/tutorials/basic-force-feedback
        private void OnDisable()
        {
            inverse3.DeviceStateChanged -= OnDeviceStateChanged;
            inverse3.Release();

            // Unsubscribe from Versegrip button events
            versegripController.ButtonDown.RemoveListener(OnButtonDown);
            versegripController.ButtonUp.RemoveListener(OnButtonUp);
        }

    #endregion


    #region Handle Force

        //The one and only force calculating function lol
        private void CalculateForceOnMainThread(Vector3 cursorPosition, Vector3 cursorVelocity, Vector3 cursorRadius, MeshCollider meshCollider)
        {
            //Initialize Current Force
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
    #endregion

    #region manage threadPartition and joints
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



    #endregion


    #region Manage Versegrip button

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

        
    #endregion



    #region Updating Functions

        //Main update function. 
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

    #endregion
    }
}
