using Haply.Inverse.Unity;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Haply.Samples.Tutorials._2_BasicForceFeedback
{
    public class HaplyInverseXRController : MonoBehaviour
    {
        public Inverse3 inverse3;
        public VerseGrip versegripController;
        public ObiSolver solver;

        [Range(0, 100000)]
        public float stiffness = 300f;

        [Range(0, 5)]
        public float damping = 1f;

        private Vector3 _calculatedForce;
        private bool _forceCalculated;
        private Queue<Action> _mainThreadActions = new Queue<Action>();

        private void Awake()
        {
            SaveSceneData();
        }

        private void Start(){
            //
        }

        private void OnEnable()
        {
            inverse3.DeviceStateChanged += OnDeviceStateChanged;
            versegripController.ButtonDown.AddListener(OnButtonDown);
            versegripController.ButtonUp.AddListener(OnButtonUp);
            solver.OnCollision += Solver_OnCollision;
        }

        private void OnDisable()
        {
            inverse3.DeviceStateChanged -= OnDeviceStateChanged;
            inverse3.Release();
            versegripController.ButtonDown.RemoveListener(OnButtonDown);
            versegripController.ButtonUp.RemoveListener(OnButtonUp);
            solver.OnCollision -= Solver_OnCollision;
        }

        private void SaveSceneData()
        {
            // Implement SaveSceneData logic here
            _meshCollider = GetComponent<MeshCollider>();
            if(inverse3.Cursor.Model == inverse3.Cursor.Model.transform.root)
                _cursorRadius = inverse3.Cursor.Model.transform.lossyScale / 2f;
            else if()
        }

        private void OnDeviceStateChanged(Inverse3 device)
        {
            // Implement OnDeviceStateChanged logic here
            // Calculate the ball force

            

        }

        private void OnButtonDown(VerseGrip grip, VerseGripButton button)
        {
            // Implement OnButtonDown logic here
        }

        private void OnButtonUp(VerseGrip grip, VerseGripButton button)
        {
            // Implement OnButtonUp logic here
        }

        private void Solver_OnCollision(object sender, ObiNativeContactList e)
        {
            // Implement Solver_OnCollision logic here
            
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
    }
}