using Haply.Inverse.Unity;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Obi;

namespace Haply.Samples.Tutorials._2_BasicForceFeedback
{
    public class MeshForceSoftbody : MonoBehaviour
    {
        #region Initialize

   

        //Haply Devices

        //Inverse3 for Force Feedback
        public Inverse3 inverse3;

        //VerseGrip to use the button (which happens to be on the haptic device)
        public VerseGrip versegripController;

        //ObiSolver for softbody collision
        public ObiSolver solver;

        //Force Scalars

        //Stiffness Scalar "acts like a spring, generating more force the more it is compressed." -https://docs.haply.co/inverseSDK/2.2.0/unity/tutorials/basic-force-feedback
        [Range(0, 100000)]
        public float stiffness = 1f;

        //Damping Scalar "represents an object's resistance to movement, offering more resistance the faster it is moved through"
        [Range(0, 5)]
        public float damping = 1f;

        //joint

        //Asset Model
        public GameObject jointPrefab;

        //Asset Size
        public float jointSize = 0.1f;

        //Delay scalar (minimum time which must elapse before next joint may be created)
        public float creationInterval = 1f;

        //Time since last joint was created (used with creationInterval to prevent overwhelmingly rapid joint creation)
        private float _lastJointCreationTime = 0f;

        //Last joint created (where the beginning of the next threadPartition will be)
        private GameObject lastJoint = null;

        //threadPartition

        //Thickness Scalar
        [Range(0.001f, 1f)]
        public float threadPartitionDiameter = 0.05f;

        //Color
        public Color threadPartitionColor = Color.white;

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
        private List<GameObject> _joints = new List<GameObject>();

        //List of existing threadPartitions
        private List<GameObject> _threadPartitions = new List<GameObject>();

        //List of actions (delegates or UnityEvents) the main thread should handle
        private Queue<Action> _mainThreadActions = new Queue<Action>();

        //Versegrip

        //Whether the button is being pressed
        private bool isPressing = false;

        //Time since last clicked Versegrip button
        private float lastButtonPressTime = 0f;

        //Maximum time between Versegrip button "double clicks"
        private float doubleClickThreshold = 0.35f;

        

        #endregion

        #region Maintenance Functions

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
            //We use "+=" as a way to signal that we're "adding" the RHS as a delegate (another thing to do when the LHS gets called)
            //inverse3.DeviceStateChanged += OnDeviceStateChanged;

            //also subscribe to versegrip button events
            //This has different syntax because the inverse3 was called using C#'s Event/Delegate functionality, but this is called through a UnityEvent.
            //The difference is that there are more functionalities available in the inspector for UnityEvent (easier function binding)
            //versegripController.ButtonDown.AddListener(OnButtonDown);
            //versegripController.ButtonUp.AddListener(OnButtonUp);

            //Sub to solver collision event
            solver.OnCollision += Solver_OnCollision;
            Debug.Log("Solver: " + solver);
        }

        //"Unsubscribes from the DeviceStateChanged event." - https://docs.haply.co/inverseSDK/2.2.0/unity/tutorials/basic-force-feedback
        private void OnDisable()
        {
            //Remove OnDeviceStateChanged from DeviceStateChanged Event
            //inverse3.DeviceStateChanged -= OnDeviceStateChanged;
            inverse3.Release();

            // Unsubscribe from Versegrip button events
            // versegripController.ButtonDown.RemoveListener(OnButtonDown);
            // versegripController.ButtonUp.RemoveListener(OnButtonUp);

            //unsub from solver collision event
            solver.OnCollision -= Solver_OnCollision;
        }

        #endregion

        #region Handle Force

        //The one and only force calculating function lol
        // //Calculates force based on whether a ray  (which is the need for raycast)
        // private void CalculateForceOnMainThread(Vector3 cursorPosition, Vector3 cursorVelocity, Vector3 cursorRadius, object sender, ObiNativeContactList e)
        // {

        // }  

        //Main force calculation function
        

        #endregion

        #region manage threadPartitions and joints

        //Creates a joint at the collision point (referred to as "closestPoint" in the CalculateForceOnMainThread function)
        // private void CreateJointAtCollisionPoint(Vector3 collisionPoint)
        // {
        //     //check to make sure we have an asset for the joint.
        //     if (jointPrefab != null)
        //     {
        //         //create a joint with an asset of jointPrefab, at collisionPoint, and a rotation of Quaternion.identity (no rotation)
        //         GameObject joint = Instantiate(jointPrefab, collisionPoint, Quaternion.identity);

        //         //scale the joint to joint size (but multiply by a vector so it's in the right format)
        //         joint.transform.localScale = Vector3.one * jointSize;

        //         //put in data structure (so we can delete all joints if we double-click)
        //         _joints.Add(joint);

        //         // Only create a threadPartition if this is not the first joint
        //         if (lastJoint != null)
        //         {
        //             //call that function
        //             CreatethreadPartitionsBetweenJoints(lastJoint, joint);
        //         }

        //         // Update the last joint to know where to make the next threadPartition
        //         lastJoint = joint;
        //     }
        //     //if we don't have an asset for the joint throw a warning
        //     else
        //     {
        //         Debug.LogWarning("Joint Prefab is not assigned in the inspector.");
        //     }
        // }










    //     //Creates threadpartitions. Called in CreateJointAtCollisionPoint
    //     //need the start and end joint to know where to make the thread partition
    //     private void CreatethreadPartitionsBetweenJoints(GameObject startJoint, GameObject endJoint)
    //     {
    //         //get the positions of the joints
    //         Vector3 start = startJoint.transform.position;
    //         Vector3 end = endJoint.transform.position;


    //         //get the distance between start and end joints
    //         float vectorDistance = Vector3.Distance(start, end);


    //         //Make a new threadPartition (which is just a cylinder)
    //         GameObject threadPartition = GameObject.CreatePrimitive(PrimitiveType.Cylinder);


    //         //Make sure we don't inherit anything by mistake
    //         threadPartition.transform.SetParent(null);


    //         //Start the threadPartition in the middle of the two joints
    //         threadPartition.transform.position = (start + end) / 2f;


    //         //Set the rotation of the threadPartition to be the direction of end to start (or start to end, doesn't matter)
    //         //Normalize it because we don't care about the magnitude of the vector
    //         threadPartition.transform.up = (end - start).normalized;












    //         //create the dimensions of the threadPartition
    //         threadPartition.transform.localScale = new Vector3(
    //             //x-radius is how thick we want the threadPartition
    //             threadPartitionDiameter,


    //             //height (y-RADIUS) of threadPartition (which is the distance between start and end divided by 2))
    //             vectorDistance / 2f,


    //             //z-radius is how thick we want the threadPartition
    //             threadPartitionDiameter);














    //         //get the renderer of the threadPartition
    //         Renderer renderer = threadPartition.GetComponent<Renderer>();


    //         //make sure we don't throw and error my checking for null reference
    //         if (renderer != null)
    //         {
    //             //color the threadPartition
    //             renderer.material.color = threadPartitionColor;
    //         }


    //         //put in data structure (so we can delete them all if we need)
    //         _threadPartitions.Add(threadPartition);
    //     }






    //     //Get rid of joints and threadPartitions
    //     //Called in OnButtonDown (subscribed to versegrip button down UnityEvent)
    //     private void DeleteAllthreadPartitionsAndJoints()
    //     {
    //         //simple for every threadPartition
    //         foreach (var threadPartition in _threadPartitions)
    //         {
    //             //destroy me
    //             Destroy(threadPartition);
    //         }
    //         //flash the threadPartition data structure
    //         _threadPartitions.Clear();






    //         //now do the same thing but with joints VVVVVVV


    //         foreach (var joint in _joints)
    //         {
    //             Destroy(joint);
    //         }
    //         _joints.Clear();
    //     }






   #endregion




    #region Manage Versegrip button


    //subscribed to versegrip button down unityEvent
    //Make sure to get the versegrip and button
   

       
    #endregion






    #region Updating Functions


        // //Main update function.
        // //TLDR, we need to make sure some actions are running on the main thread. so things get a little complicated.
        private void Update()
        {
            //while we have something the main thread should handle
            while (_mainThreadActions.Count > 0)
            {
                //declare an action
                Action action;


                //lock the thread for race condition security
                lock (_mainThreadActions)
                {
                    action = _mainThreadActions.Dequeue();
                }
                //do that action
                action.Invoke();
            }


            // //if we have calculated the current force
            // if (_forceCalculated)
            // {
            //     //tell the inverse to execute the force
            // inverse3.CursorSetLocalForce(_calculatedForce);


            //     //say we no longer have calculated the (next) current force
            //     _forceCalculated = false;
            // }
            
        }




        //when Inverse3 changes
        // private void OnDeviceStateChanged(Inverse3 device)
        // {


        //     Debug.Log("Device State Changed");
        //     if(!device.IsReady)
        //     {
        //         return;
        //     } else if(!_forceCalculated)
        //     {
        //         //CalculateForceOnMainThread(device.CursorLocalPosition, device.CursorLocalVelocity, _cursorRadius, this, null);

        //     } else {
        //         device.CursorSetLocalForce(_calculatedForce);
        //     }
            
        // }




        // //Function that puts an action into the main thread.
        private void QueueMainThreadAction(Action action)
        {
            //assure no race conditions
            lock (_mainThreadActions)
            {
                _mainThreadActions.Enqueue(action);
            }
        }

        #endregion

        #region Obi Solver Collision

        void Solver_OnCollision(object sender, ObiNativeContactList e)
        {
            Debug.Log("collision");

            // Initialize Current Force with 0. This will be changed throughout the gameloop
            var force = Vector3.zero;

            // Get obi instance
            var world = ObiColliderWorld.GetInstance();
            var tempForces = 0;
            var mostPenetration = 0f;
            Vector3 contactNormal = Vector3.zero;

            // just iterate over all contacts in the current frame:
            foreach (Oni.Contact contact in e)
            {
                tempForces++;
                // account for cursor model size in distance
                var penetration = _cursorRadius.x + contact.distance;

                

                // if this one is an actual collision:
                if (penetration < mostPenetration)
                {
                    ObiColliderBase col = world.colliderHandles[contact.bodyB].owner;
                    if (col != null && col.gameObject.CompareTag("haply"))
                    {
                        // get the index of the particle involved in the contact:
                        int particleIndex = solver.simplices[contact.bodyA];
                        contactNormal = contact.normal;

                        mostPenetration = penetration;
                    }
                }
            }
            

            // The depth, direction (particle contact normal) and stiffness of the penetration positively correlates to the force applied
            Vector3 tempForce = contactNormal * mostPenetration * stiffness;

            // This mimics friction
            // The velocity and damping negatively correlate to the force applied (only subtracted from the original force)
            tempForce -= inverse3.CursorLocalVelocity * damping;
            

            // Apply a gentle repulsion to prevent clipping, without pushing the cursor too far
            tempForce = contactNormal * -0.1f; // Slight force to prevent penetration

            Debug.Log("Calculated Force: " + tempForce);
            // Queue the action to apply the force on the main thread
            QueueMainThreadAction(() =>
            {
                inverse3.CursorSetLocalForce(tempForce);
            });

            // Signal that the force has been calculated for this frame
            _forceCalculated = true;

            
        }

        #endregion
    }
}
