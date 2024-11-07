/*
 * Copyright 2024 Haply Robotics Inc. All rights reserved.
 */

using UnityEngine;

namespace Haply.Samples.Tutorials.Utils
{
    /// <summary>
    /// Controls the movement of a game object, allowing for both random and manual movement.
    /// This class provides functionality to move the object within a specified range from its base position
    /// and to adjust its movement speed and maximum distance dynamically.
    /// </summary>
    public class MovableObject : MonoBehaviour
    {
        // Constants for adjusting maxMovementDistance
        private const float DistanceAdjustment = 0.1f;
        private const float MinDistance = 0;
        private const float MaxDistance = 0.5f;

        // Constants for adjusting movementSpeed
        private const float MinSpeed = 0f;
        private const float MaxSpeed = 1f;
        private const float SpeedAdjustment = 0.1f;

        [Tooltip("Enables or disables the movement of the object.")]
        [SerializeField]
        private bool randomMoveMode;

        [Tooltip("The initial position from where the object starts moving.")]
        [SerializeField]
        private Vector3 basePosition;

        [Tooltip("Maximum distance the object can move from its initial position.")]
        [Range(MinDistance, MaxDistance)]
        public float maxDistance = 0.2f;

        [Tooltip("Speed at which the object moves towards the next position. Higher values result in faster movement.")]
        [Range(MinSpeed, MaxSpeed)]
        public float movementSpeed = 0.3f;

        [Tooltip("Threshold for checking proximity to the target position")]
        [Range(0.001f, 1f)]
        public float distanceThreshold = 0.02f;

        public bool moveEnabled = true;

        public bool showGUI = true;

        // Used by SmoothDamp function to smooth out the object's movement.
        private Vector3 _movementVelocity = Vector3.zero;

        /// <summary>
        /// Gets the current velocity of the object.
        /// </summary>
        public Vector3 Velocity => _movementVelocity;

        private void OnEnable()
        {
            BasePosition = transform.position;
        }

        private void Update()
        {
            HandleInput();
        }

        private void FixedUpdate()
        {
            if (moveEnabled)
                Move();
        }

        #region Movement

        // The next position the object will move towards.
        private Vector3 TargetPosition { get; set; }

        // The initial position from where the object starts moving
        private Vector3 BasePosition
        {
            get => basePosition;
            set => basePosition = TargetPosition = value;
        }

        /// <summary>
        /// Sets the target position for the object. If 'teleport' is true, the object is immediately moved to the new
        /// position.
        /// This method allows for either smooth transition towards a target position or an immediate update of the
        /// object's position.
        /// </summary>
        /// <param name="position">The new target position for the object.</param>
        /// <param name="teleport">If true, the object's position is immediately updated to the new position, bypassing
        /// any smooth transition.</param>
        public void SetTargetPosition(Vector3 position, bool teleport=false)
        {
            if (teleport)
            {
                basePosition = position;
                transform.position = position;
            }
            TargetPosition = position;
        }

        private void Move()
        {
            if (RandomMoveEnabled && Vector3.Distance(transform.position, TargetPosition) < distanceThreshold)
            {
                TargetPosition = BasePosition + Random.insideUnitSphere * maxDistance;
            }

            var smoothness = MaxSpeed - movementSpeed;
            transform.position = Vector3.SmoothDamp(transform.position, TargetPosition,
                ref _movementVelocity, smoothness, Mathf.Infinity, Time.fixedDeltaTime);
        }

        /// <summary>
        /// Enables or disables the movement of the object. When enabled, the object starts moving from its start
        /// position.
        /// </summary>
        private bool RandomMoveEnabled
        {
            get => randomMoveMode;
            set {
                if (value)
                {
                    TargetPosition = BasePosition;
                }
                randomMoveMode = value;
            }
        }

        #endregion

        #region Keyboard Inputs

        private void HandleInput()
        {
            // Toggle random movement with the Space key.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                RandomMoveEnabled = !RandomMoveEnabled;
            }

            // Check for manual movement keys
            var manualMoveKeyPressed = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
                Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.A) ||
                Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Q);

            if (manualMoveKeyPressed)
            {
                RandomMoveEnabled = false; // Disable random movement when manually moving
                var moveDirection = Vector3.zero;

                if (Input.GetKey(KeyCode.W)) moveDirection.y += 1;
                if (Input.GetKey(KeyCode.S)) moveDirection.y -= 1;
                if (Input.GetKey(KeyCode.D)) moveDirection.x += 1;
                if (Input.GetKey(KeyCode.A)) moveDirection.x -= 1;
                if (Input.GetKey(KeyCode.E)) moveDirection.z += 1;
                if (Input.GetKey(KeyCode.Q)) moveDirection.z -= 1;

                TargetPosition += moveDirection;
                TargetPosition = Vector3.ClampMagnitude(TargetPosition - BasePosition, maxDistance) + BasePosition;
            }

            var adjustKeyPressed = Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.Equals) ||
                Input.GetKey(KeyCode.RightBracket) ||
                Input.GetKey(KeyCode.LeftBracket);

            if (adjustKeyPressed)
            {
                // Adjust movementSpeed with - and + keys.
                if (Input.GetKeyDown(KeyCode.Equals))
                {
                    movementSpeed = Mathf.Clamp(movementSpeed + SpeedAdjustment, MinSpeed, MaxSpeed);
                }
                else if (Input.GetKeyDown(KeyCode.Minus))
                {
                    movementSpeed = Mathf.Clamp(movementSpeed - SpeedAdjustment, MinSpeed, MaxSpeed);
                }

                // Adjust maxDistance with [ and ] keys.
                else if (Input.GetKeyDown(KeyCode.RightBracket))
                {
                    maxDistance = Mathf.Clamp(maxDistance + DistanceAdjustment, MinDistance, MaxDistance);
                }
                else if (Input.GetKeyDown(KeyCode.LeftBracket))
                {
                    maxDistance = Mathf.Clamp(maxDistance - DistanceAdjustment, MinDistance, MaxDistance);
                }
            }
        }

        #endregion

        # region Optional GUI Display

        // --------------------
        // Optional GUI Display
        // --------------------

        // Variables for GUI display
        private const string GUIMessage = "Random Move : <b>SPACE</b>\n" +
            "Move : <b>WASDQE</b>\n" +
            "Speed ({0:F2}) : <b>+</b> , <b>-</b>\n" +
            "Distance ({1:F2}) : <b>[</b> , <b>]</b>";

        private void OnGUI()
        {
            if (showGUI)
            {
                // Calculate the position for the top right corner
                float boxWidth = 250;
                float boxHeight = 120;
                Rect guiBoxRect = new Rect(10, 10, boxWidth, boxHeight);

                // Always display the GUI box
                GUI.Box(guiBoxRect, "Controls");
                string formattedMessage = string.Format(GUIMessage, movementSpeed, maxDistance);
                GUI.Label(new Rect(guiBoxRect.x + 10, guiBoxRect.y + 30, guiBoxRect.width - 20, guiBoxRect.height - 40),
                    formattedMessage);
            }
        }

        #endregion
    }
}
