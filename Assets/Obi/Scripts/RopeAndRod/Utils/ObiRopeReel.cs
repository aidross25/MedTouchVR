using UnityEngine;

namespace Obi
{
	[RequireComponent(typeof(ObiRopeCursor))]
	public class ObiRopeReel : MonoBehaviour
	{
		private ObiRopeCursor cursor;
		private ObiRope rope;

		[Header("Roll out/in thresholds")]
		public float outThreshold = 0.8f;
		public float inThreshold = 0.4f;

		[Header("Roll out/in speeds")]
		public float outSpeed = 4;
		public float inSpeed = 2;

        public float maxLength = 10;

        private float restLength;

        public void Awake()
		{
			cursor = GetComponent<ObiRopeCursor>();
			rope = GetComponent<ObiRope>();
            restLength = rope.restLength;
        }

		public void OnValidate()
		{
			// Make sure the range thresholds don't cross:
			outThreshold = Mathf.Max(inThreshold, outThreshold);
		}

		// Update is called once per frame
		void Update()
		{
			// get current and rest lengths:
			float length = rope.CalculateLength();

			// calculate difference between current length and rest length:
			float diff = Mathf.Max(0, length - restLength);

            float lengthChange = 0;

			// if the rope has been stretched beyond the reel out threshold, increase its rest length:
			if (diff > outThreshold)
                lengthChange = outSpeed * Time.deltaTime;

			// if the rope is not stretched past the reel in threshold, decrease its rest length:
			if (diff < inThreshold)
                lengthChange = -inSpeed * Time.deltaTime;

            // make sure not to exceed maxLength:
            lengthChange -= Mathf.Max(0, restLength + lengthChange - maxLength);

            // set the new rest length:
            restLength = cursor.ChangeLength(lengthChange);
		}
	}
}
