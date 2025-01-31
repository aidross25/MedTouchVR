using UnityEngine;
using System;

namespace Obi
{
    [Serializable]
    public struct EmitPoint
    {
        public Vector4 position;
        public Vector4 direction;
        public Color color;

        public EmitPoint(Vector3 position, Vector3 direction)
        {
            this.position = position;
            this.direction = direction;
            this.color = Color.white;
        }

        public EmitPoint(Vector3 position, Vector3 direction, Color color)
        {
            this.position = position;
            this.direction = direction;
            this.color = color;
        }

        public EmitPoint GetTransformed(Matrix4x4 transform, Color multiplyColor)
        {
            return new EmitPoint(transform.MultiplyPoint3x4(position),
                                 transform.MultiplyVector(direction),
                                 color * multiplyColor);
        }
    }
}