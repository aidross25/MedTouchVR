using UnityEngine;

namespace Obi
{
    public struct EmittedParticleData
    {
        public Vector4 fluidMaterial;
        public Vector4 fluidInterface;
        public Vector4 userData;
        public int phase;
        public float invMass;
        public float radius;
        public float volume;
    }
}