using UnityEngine;
using System.Runtime.CompilerServices;

namespace Obi
{
    public static class ObiIntegration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 IntegrateLinear(Vector4 position, Vector4 velocity, float dt)
        {
            return position + velocity * dt;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 DifferentiateLinear(Vector4 position, Vector4 prevPosition, float dt)
        {
            return (position - prevPosition) / dt;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion AngularVelocityToSpinQuaternion(Quaternion rotation, Vector4 angularVelocity, float dt)
        {
            var delta = new Quaternion(angularVelocity.x,
                                       angularVelocity.y,
                                       angularVelocity.z, 0);

            var rot = delta * rotation;
            var v = new Vector4(rot.x, rot.y, rot.z, rot.w) * 0.5f * dt;
            return new Quaternion(v.x, v.y, v.z, v.w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion IntegrateAngular(Quaternion rotation, Vector4 angularVelocity, float dt)
        {
            var spin = AngularVelocityToSpinQuaternion(rotation, angularVelocity, dt);
            rotation.x += spin.x;
            rotation.y += spin.y;
            rotation.z += spin.z;
            rotation.w += spin.w;
            return rotation.normalized; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 DifferentiateAngular(Quaternion rotation, Quaternion prevRotation, float dt)
        {
            var delta = rotation * Quaternion.Inverse(prevRotation);
            return new Vector4(delta.x, delta.y, delta.z, 0) * 2.0f / dt;
        }
    }
}