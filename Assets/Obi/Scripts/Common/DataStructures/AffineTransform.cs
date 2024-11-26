using System;
using UnityEngine;

namespace Obi
{
    public struct AffineTransform
    {
        public Vector4 translation;
        public Vector4 scale;
        public Quaternion rotation;

        public AffineTransform(Vector4 translation, Quaternion rotation, Vector4 scale)
        {
            // make sure there are good values in the 4th component:
            translation[3] = 0;
            scale[3] = 1;

            this.translation = translation;
            this.rotation = rotation;
            this.scale = scale;
        }

        public void FromTransform3D(Transform source, ObiRigidbody rb)
        {
            if (rb != null && rb.unityRigidbody != null)
            {
                translation = source.position - rb.unityRigidbody.transform.position + rb.position;
                rotation = (source.rotation * Quaternion.Inverse(rb.unityRigidbody.transform.rotation)) * rb.rotation;
            }
            else
            {
                translation = source.position;
                rotation = source.rotation;
            }

            scale = source.lossyScale;
        }

        public void FromTransform2D(Transform source, ObiRigidbody2D rb)
        {
            if (rb != null && rb.unityRigidbody != null)
            {
                translation = source.position - rb.unityRigidbody.transform.position + (Vector3)rb.position;
                rotation =  (source.rotation * Quaternion.Inverse(rb.unityRigidbody.transform.rotation)) * Quaternion.AngleAxis(rb.rotation, Vector3.forward);
            }
            else
            {
                translation = source.position;
                rotation = source.rotation;
            }

            scale = source.lossyScale;
            translation[2] = 0;
        }

        public AffineTransform Inverse()
        {
            var conj = Quaternion.Inverse(rotation);
            var invScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
            return new AffineTransform(conj * Vector3.Scale(translation , -invScale),
                                       conj,
                                       invScale);
        }
    }
}
