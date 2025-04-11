using System.Collections;
using UnityEngine;

namespace Obi.Samples
{
    [RequireComponent(typeof(Rigidbody))]
    public class TangledPeg : MonoBehaviour
    {
        public TangledPegSlot currentSlot;
        public Collider floorCollider;
        public ObiRope attachedRope;

        [Header("Movement")]
        public float stiffness = 200;
        public float damping = 20;
        public float maxAccel = 50;
        public float minDistance = 0.05f;


        public Rigidbody rb { get; private set; }
        public ObiRigidbody orb { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            orb = GetComponent<ObiRigidbody>();

            // Ignore collisions with the floor:
            Physics.IgnoreCollision(GetComponent<Collider>(), floorCollider);

            // Initialize the peg's current slot, if any:
            if (currentSlot != null)
            {
                currentSlot.currentPeg = this;
                transform.position = currentSlot.transform.position;
            }
        }

        public float MoveTowards(Vector3 position)
        {
            Vector3 vector = position - transform.position;
            float distance = Vector3.Magnitude(vector);

            // simple damped spring: F = -kx - vu
            Vector3 accel = stiffness * vector - damping * rb.velocity;

            // clamp spring acceleration:
            accel = Vector3.ClampMagnitude(accel, maxAccel);

            rb.AddForce(accel, ForceMode.Acceleration);

            return distance;
        }

        public void DockInSlot(TangledPegSlot slot)
        {
            StopAllCoroutines();
            StartCoroutine(MoveTowardsSlot(slot));
        }

        public void UndockFromCurrentSlot()
        {
            if (currentSlot != null)
            {
                currentSlot.currentPeg = null;
                rb.isKinematic = false;
            }
        }

        private IEnumerator MoveTowardsSlot(TangledPegSlot slot)
        {
            float distance = float.MaxValue;
            orb.kinematicForParticles = true;

            while (distance > minDistance)
            {
                distance = MoveTowards(slot.transform.position);
                yield return 0;
            }

            currentSlot = slot;
            currentSlot.currentPeg = this;
            transform.position = currentSlot.transform.position;
            rb.isKinematic = true;
            orb.kinematicForParticles = false;
        }
    }
}