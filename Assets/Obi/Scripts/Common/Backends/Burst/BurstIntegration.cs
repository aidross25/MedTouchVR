#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)

using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace Obi
{
    public static class BurstIntegration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 IntegrateLinear(float4 position, float4 velocity, float dt)
        {
            return position + velocity * dt;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 DifferentiateLinear(float4 position, float4 prevPosition, float dt)
        {
            return (position - prevPosition) / dt;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion AngularVelocityToSpinQuaternion(quaternion rotation, float4 angularVelocity, float dt)
        {
            var delta = new quaternion(angularVelocity.x,
                                       angularVelocity.y,
                                       angularVelocity.z, 0);

            return new quaternion(0.5f * math.mul(delta,rotation).value * dt); 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion IntegrateAngular(quaternion rotation, float4 angularVelocity, float dt)
        {
            rotation.value += AngularVelocityToSpinQuaternion(rotation,angularVelocity, dt).value;
            return math.normalize(rotation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 DifferentiateAngular(quaternion rotation, quaternion prevRotation, float dt)
        {
            quaternion deltaq = math.mul(rotation, math.inverse(prevRotation));
            float sign = deltaq.value.w >= 0 ? 1 : -1;
            return new float4(sign * deltaq.value.xyz * 2.0f / dt, 0);
        }
    }
}
#endif