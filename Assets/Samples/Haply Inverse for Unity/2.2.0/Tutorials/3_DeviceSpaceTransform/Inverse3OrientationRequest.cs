using Haply.Inverse.Unity;
using UnityEngine;

namespace Haply.Samples.Tutorials._3_DeviceSpaceTransform
{
    /// <summary>
    /// This class demonstrates how to interact with the Inverse3's orientation data and provides a simple interface for
    /// requesting and displaying this information in real-time.
    /// </summary>
    public class Inverse3OrientationRequest : MonoBehaviour
    {
        /// <summary>
        /// Reference to the Inverse3 component attached to the GameObject.
        /// </summary>
        private Inverse3 _inverse3;

        /// <summary>
        /// Stores the current orientation of the Inverse3 device.
        /// The [ContextMenuItem] attribute allows requesting orientation from the Unity Editor.
        /// </summary>
        [ContextMenuItem("Request Orientation", "RequestOrientation")]
        public Quaternion orientation;

        /// <summary>
        /// Initializes the Inverse3 component and subscribes to orientation change events.
        /// </summary>
        private void Awake()
        {
            // Find the Inverse3 component in the child objects
            _inverse3 = GetComponent<Inverse3>();

            // Subscribe to the OrientationChangedAsync event
            _inverse3.OrientationChangedAsync += inverse3 =>
            {
                // Update the orientation field with the new orientation
                orientation = inverse3.Orientation;
            };
        }

        /// <summary>
        /// Requests the current orientation from the Inverse3 device.
        /// This method can be triggered from the Unity Editor or programmatically.
        /// </summary>
        /// <remarks>
        /// If a <see cref="Inverse3Body"/> is attached to the inverse3 GameObject, it will automatically update
        /// the orientation of the attached GameObject when the <see cref="Inverse3.OrientationChangedAsync"/> event
        /// is triggered.
        /// </remarks>
        public void RequestOrientation()
        {
            _inverse3.RequestOrientation();
        }

        /// <summary>
        /// Checks for user input and requests the device's orientation if the 'O' key is pressed.
        /// </summary>
        private void Update()
        {
            if (Input.GetKey(KeyCode.O))
            {
                RequestOrientation();
            }
        }

        private void OnGUI()
        {
            var controlBoxRect = new Rect(10, 200, 200, 40);
            GUI.Box(controlBoxRect, "Body Orientation Controls");
            GUI.Label(new Rect(20, 220, 180, 20), "Update: O key");
        }
    }
}
