using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Obi.Samples
{
    public class CableCarController : MonoBehaviour
    {
        public ObiPinhole pinhole;
        public float carSpeed = 1;

        // Update is called once per frame
        void Update()
        {
            float speed = 0;
            if (Input.GetKey(KeyCode.W))
            {
                speed = carSpeed;
            }

            if (Input.GetKey(KeyCode.S))
            {
                speed = -carSpeed;
            }
            pinhole.motorSpeed = speed;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                pinhole.friction = pinhole.friction > 0.5f ? 0 : 1;
            }
        }
    }
}
