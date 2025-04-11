using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Obi.Samples
{
    [RequireComponent(typeof(ObiRope))]
    public class CursorController : MonoBehaviour
    {
        public float minLength = 0.1f;
        public float speed = 1;

        private ObiRopeCursor cursor;
        private ObiRope rope;

        void OnEnable()
        {
            rope = GetComponent<ObiRope>();
            cursor = GetComponent<ObiRopeCursor>();
        }

        void Update()
        {
            float change = 0;

            if (Input.GetKey(KeyCode.W) && cursor != null)
            {
                change -= speed * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.S) && cursor != null)
            {
                change += speed * Time.deltaTime;
            }

            if (rope.restLength + change < minLength)
                change = minLength - rope.restLength;

            cursor.ChangeLength(change);

            if (Input.GetKey(KeyCode.A))
            {
                rope.transform.Translate(Vector3.left * Time.deltaTime, Space.World);
            }

            if (Input.GetKey(KeyCode.D))
            {
                rope.transform.Translate(Vector3.right * Time.deltaTime, Space.World);
            }

        }
    }
}
