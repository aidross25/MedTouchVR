using UnityEngine;

namespace Obi.Samples
{
    public class PinholeRatchet : MonoBehaviour
    {
        public ObiPinhole pinhole;
        public bool direction = false;
        public float teethSeparation = 0.1f;

        public float distanceToNextTooth { get; private set; }

        void Update()
        {
            if (pinhole == null || pinhole.rope == null)
                return;

            float restLength = (pinhole.rope as ObiRopeBase).restLength;
            float normalizedTeethDistance = Mathf.Max(0.001f, teethSeparation / restLength);
            var range = pinhole.range;

            if (direction)
            {
                distanceToNextTooth = (range.y - pinhole.position) * restLength;
                while (distanceToNextTooth > teethSeparation)
                {
                    range.y -= normalizedTeethDistance;
                    distanceToNextTooth -= teethSeparation;
                }
            }
            else
            {
                distanceToNextTooth = (pinhole.position - range.x) * restLength;
                while (distanceToNextTooth > teethSeparation)
                {
                    range.x += normalizedTeethDistance;
                    distanceToNextTooth -= teethSeparation;
                }
            }

            pinhole.range = range;
        }

        public void OnDisable()
        {
            if (pinhole != null)
                pinhole.range = new Vector2(0, 1);
        }
    }
}
