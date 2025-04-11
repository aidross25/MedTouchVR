using UnityEngine;

namespace Obi.Samples
{
    public class RatchetController : MonoBehaviour
    {
        public PinholeRatchet ratchet;

        public Transform ratchetVisualizer;
        public float minAngle = 0;
        public float maxAngle = 25;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                ratchet.enabled = !ratchet.enabled;

            float angle = ratchet.enabled ? Mathf.LerpAngle(minAngle, maxAngle, ratchet.distanceToNextTooth / ratchet.teethSeparation) : maxAngle;
            ratchetVisualizer.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

    }
}