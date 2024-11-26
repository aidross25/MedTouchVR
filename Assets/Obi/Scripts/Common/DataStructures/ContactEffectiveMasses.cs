#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Mathematics;
#endif

namespace Obi
{
    public struct ContactEffectiveMasses
    {
        public float normalInvMassA;
        public float tangentInvMassA;
        public float bitangentInvMassA;

        public float normalInvMassB;
        public float tangentInvMassB;
        public float bitangentInvMassB;

        public float TotalNormalInvMass => normalInvMassA + normalInvMassB;
        public float TotalTangentInvMass => tangentInvMassA + tangentInvMassB;
        public float TotalBitangentInvMass => bitangentInvMassA + bitangentInvMassB;

        public void ClearContactMassesA()
        {
            normalInvMassA = tangentInvMassA = bitangentInvMassA = 0;
        }

        public void ClearContactMassesB()
        {
            normalInvMassB = tangentInvMassB = bitangentInvMassB = 0;
        }

        #if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
        public void CalculateContactMassesA(float invMass,
                                            float4 inverseInertiaTensor,
                                            float4 position,
                                            quaternion orientation,
                                            float4 contactPoint,
                                            float4 normal,
                                            float4 tangent,
                                            float4 bitangent,
                                            bool rollingContacts)
        {
            // initialize inverse linear masses:
            normalInvMassA = tangentInvMassA = bitangentInvMassA = invMass;

            if (rollingContacts)
            {
                float4 rA = contactPoint - position;
                float4x4 solverInertiaA = BurstMath.TransformInertiaTensor(inverseInertiaTensor, orientation);

                normalInvMassA += BurstMath.RotationalInvMass(solverInertiaA, rA, normal);
                tangentInvMassA += BurstMath.RotationalInvMass(solverInertiaA, rA, tangent);
                bitangentInvMassA += BurstMath.RotationalInvMass(solverInertiaA, rA, bitangent);
            }
        }

        public void CalculateContactMassesB(float invMass,
                                            float4 inverseInertiaTensor,
                                            float4 position,
                                            quaternion orientation,
                                            float4 contactPoint,
                                            float4 normal,
                                            float4 tangent,
                                            float4 bitangent,
                                            bool rollingContacts)
        {
            // initialize inverse linear masses:
            normalInvMassB = tangentInvMassB = bitangentInvMassB = invMass;

            if (rollingContacts)
            {
                float4 rB = contactPoint - position;
                float4x4 solverInertiaB = BurstMath.TransformInertiaTensor(inverseInertiaTensor, orientation);

                normalInvMassB += BurstMath.RotationalInvMass(solverInertiaB, rB, normal);
                tangentInvMassB += BurstMath.RotationalInvMass(solverInertiaB, rB, tangent);
                bitangentInvMassB += BurstMath.RotationalInvMass(solverInertiaB, rB, bitangent);
            }
        }


        public void CalculateContactMassesB(in BurstRigidbody rigidbody, in BurstAffineTransform solver2World, float4 pointB, float4 normal, float4 tangent, float4 bitangent)
        {
            float4 rB = solver2World.TransformPoint(pointB) - rigidbody.com;

            // initialize inverse linear masses:
            normalInvMassB = tangentInvMassB = bitangentInvMassB = rigidbody.inverseMass;
            normalInvMassB += BurstMath.RotationalInvMass(rigidbody.inverseInertiaTensor, rB, normal);
            tangentInvMassB += BurstMath.RotationalInvMass(rigidbody.inverseInertiaTensor, rB, tangent);
            bitangentInvMassB += BurstMath.RotationalInvMass(rigidbody.inverseInertiaTensor, rB, bitangent);
        }
        #endif
    }
}
