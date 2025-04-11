using UnityEngine;

namespace Obi
{
    public struct InertialFrame
    {
        public AffineTransform frame;
        public AffineTransform prevFrame;

        public Vector4 velocity;
        public Vector4 angularVelocity;

        public Vector4 acceleration;
        public Vector4 angularAcceleration;

        public InertialFrame(Vector4 position, Vector4 scale, Quaternion rotation)
        {
            this.frame = new AffineTransform(position, rotation, scale);
            this.prevFrame = frame;

            velocity = Vector4.zero;
            angularVelocity = Vector4.zero;
            acceleration = Vector4.zero;
            angularAcceleration = Vector4.zero;
        }

        public InertialFrame(AffineTransform frame)
        {
            this.frame = frame;
            this.prevFrame = frame;

            velocity = Vector4.zero;
            angularVelocity = Vector4.zero;
            acceleration = Vector4.zero;
            angularAcceleration = Vector4.zero;
        }

        public void Update(Vector4 position, Vector4 scale, Quaternion rotation, float dt)
        {
            prevFrame = frame;
            Vector4 prevVelocity = velocity;
            Vector4 prevAngularVelocity = angularVelocity;

            frame.translation = position;
            frame.rotation = rotation;
            frame.scale = scale;

            velocity = ObiIntegration.DifferentiateLinear(frame.translation, prevFrame.translation, dt);
            angularVelocity = ObiIntegration.DifferentiateAngular(frame.rotation, prevFrame.rotation, dt);

            acceleration = ObiIntegration.DifferentiateLinear(velocity, prevVelocity, dt);
            angularAcceleration = ObiIntegration.DifferentiateLinear(angularVelocity, prevAngularVelocity, dt);
        }

    }
}