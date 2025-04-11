using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Obi.Samples
{
    [RequireComponent(typeof(LineRenderer))]
    public class RenderLineBetweenTransforms : MonoBehaviour
    {
        public Transform transformA;
        public Transform transformB;
        LineRenderer line;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
        }

        void Update()
        {
            if (transformA != null && transformB != null)
                line.SetPositions(new Vector3[] { transformA.position, transformB.position });
        }
    }
}
