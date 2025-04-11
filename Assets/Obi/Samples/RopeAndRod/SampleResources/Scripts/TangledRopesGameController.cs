using UnityEngine;
using UnityEngine.Events;

namespace Obi.Samples
{
    [RequireComponent(typeof(ObiSolver))]
    public class TangledRopesGameController : MonoBehaviour
    {
        public TangledPegSlot[] pegSlots;
        public float pegHoverHeight = 1;
        public float maxPegDistanceFromSlot = 1.5f;
        public int framesWithoutContactsToWin = 30;
        public UnityEvent onFinish = new UnityEvent();

        private TangledPeg selectedPeg;
        private Plane floor = new Plane(Vector3.up, 0);
        private int framesSinceLastContact = 0;

        void OnEnable()
        {
            GetComponent<ObiSolver>().OnParticleCollision += Solver_OnParticleCollision;
        }

        private void OnDisable()
        {
            GetComponent<ObiSolver>().OnParticleCollision -= Solver_OnParticleCollision;
        }

        private TangledPegSlot FindCandidateSlot(TangledPeg peg)
        {
            // Go over all slots, find the closest one to the peg that's closer than
            // maxPegDistanceFromSlot:

            TangledPegSlot closest = null;
            float closestDistance = float.MaxValue;

            foreach (TangledPegSlot slot in pegSlots)
            {
                // reset slot color, to make sure it looks normal if it's not a candidate for our peg.
                slot.ResetColor();

                // ignore occupied slots:
                if (slot.currentPeg != null)
                    continue;

                Vector3 slotOnFloor = floor.ClosestPointOnPlane(slot.transform.position);
                Vector3 pegOnFloor = floor.ClosestPointOnPlane(peg.transform.position);

                float distance = Vector3.Distance(slotOnFloor, pegOnFloor);
                if (distance < closestDistance && distance < maxPegDistanceFromSlot)
                {
                    closest = slot;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private void Update()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // User clicks, cast a ray towards the floor, see if it hits any peg.
            if (Input.GetMouseButtonDown(0))
            {
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    // if the ray hit a peg, store it as the selected peg and lift it off from its current slot.
                    if (hit.transform.TryGetComponent(out TangledPeg peg) && peg.currentSlot != null)
                    {
                        selectedPeg = peg;
                        selectedPeg.UndockFromCurrentSlot();
                    }
                }
            }

            if (selectedPeg != null)
            {
                // Make selected peg follow the mouse cursor:
                if (floor.Raycast(ray, out float enter))
                    selectedPeg.MoveTowards(ray.GetPoint(enter) + Vector3.up * pegHoverHeight);

                // Try to find a suitable slot to drop the peg:
                TangledPegSlot closest = FindCandidateSlot(selectedPeg);

                // If we found a candidate slot, tint it to give the player some visual feedback:
                if (closest != null)
                    closest.Tint();

                // Drop selected peg:
                if (Input.GetMouseButtonUp(0))
                {
                    // If we could find a free slot nearby, connect to it:
                    if (closest != null)
                    {
                        selectedPeg.currentSlot = null;
                        selectedPeg.DockInSlot(closest);
                        closest.ResetColor();
                    }
                    else  // If we couldn't find one or if it's too far, return to current slot.
                    {
                        selectedPeg.DockInSlot(selectedPeg.currentSlot);
                    }

                    selectedPeg = null;
                }
            }

            // If all ropes have been contact-free for a certain amount of frames, trigger finish event.
            if (framesSinceLastContact >= framesWithoutContactsToWin && onFinish != null)
                onFinish.Invoke();

        }

        private void Solver_OnParticleCollision(ObiSolver s, ObiNativeContactList e)
        {
            // Count contacts between different ropes (that is, exclude self-contacts):
            int contactsBetweenRopes = 0;

            for (int i = 0; i < e.count; ++i)
            {
                var ropeA = s.particleToActor[s.simplices[e[i].bodyA]].actor;
                var ropeB = s.particleToActor[s.simplices[e[i].bodyB]].actor;

                if (ropeA != ropeB)
                    contactsBetweenRopes++;
            }

            // If there's no contacts, bump the amount of frames we've been contact-free.
            // Otherwise reset the amount of frames to zero.
            if (contactsBetweenRopes == 0)
                framesSinceLastContact++;
            else
                framesSinceLastContact = 0;
        }
    }
}