using UnityEngine;
using System;

namespace Obi
{
    [Serializable]
    public struct ForceZone
    {
        public enum ForceMode
        {
            Force,
            Acceleration,
            Wind,
        }

        public enum ZoneType
        {
            Directional,
            Radial,
            Vortex,
            Void
        }

        public enum DampingDirection
        {
            All,   // damps motion in all directions
            ForceDirection, // damps motion in the direction of force.
            SurfaceDirection // damps motion toward/away from the surface of the zone.
        }

        public ZoneType type;
        public ForceMode mode;
        public DampingDirection dampingDir;
        public float intensity;
        public float minDistance;
        public float maxDistance;
        public float falloffPower;
        public float damping;
    }
}
