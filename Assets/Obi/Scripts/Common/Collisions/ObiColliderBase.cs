using UnityEngine;
using System;

namespace Obi
{

    /**
	 * Implements common functionality for ObiCollider and ObiCollider2D.
	 */
    public abstract class ObiColliderBase : MonoBehaviour
    {

        [SerializeProperty("Thickness")]
        [SerializeField] private float thickness = 0;

        [SerializeProperty("Inverted")]
        [SerializeField] private bool inverted = false;

        [SerializeProperty("CollisionMaterial")]
        [SerializeField] private ObiCollisionMaterial material;

        [SerializeField] private int filter = ObiUtils.MakeFilter(ObiUtils.CollideWithEverything, 0);

        public ObiCollisionMaterial CollisionMaterial
        {
            set
            {
                if (material != value)
                {
                    material = value;
                    needsUpdate = true;
                }
            }
            get { return material; }
        }

        public int Filter
        {
            set
            {
                if (filter != value)
                {
                    filter = value;
                    needsUpdate = true;
                }
            }
            get { return filter; }
        }

        public float Thickness
        {
            set
            {
                if (!Mathf.Approximately(thickness, value))
                {
                    thickness = value;
                    needsUpdate = true;
                }
            }
            get { return thickness; }
        }

        public bool Inverted
        {
            set
            {
                if (inverted != value)
                {
                    inverted = value;
                    needsUpdate = true;
                }
            }
            get { return inverted; }
        }

        public ObiShapeTracker Tracker
        {
            get { return tracker; }
        }

        public ObiColliderHandle Handle
        {
            get
            {
                if (shapeHandle == null)
                    FindSourceCollider();
                return shapeHandle;
            }
        }

        public ObiForceZone ForceZone 
        {
            get; set;
        }

        public ObiRigidbodyBase Rigidbody
        {
            get { return obiRigidbody; }
        }

        protected ObiColliderHandle shapeHandle;
        protected ObiRigidbodyBase obiRigidbody;
        protected bool wasUnityColliderEnabled = true;
        protected bool needsUpdate = true;

        protected ObiShapeTracker tracker;                               /**< tracker object used to determine when to update the collider's shape*/

        /**
		 * Creates an OniColliderTracker of the appropiate type.
   		 */
        protected abstract void CreateTracker();

        protected abstract Component GetUnityCollider(ref bool enabled);

        protected abstract void FindSourceCollider();

        protected void CreateRigidbody()
        {

            obiRigidbody = null;

            // find the first rigidbody up our hierarchy:
            Rigidbody rb = GetComponentInParent<Rigidbody>();
            Rigidbody2D rb2D = GetComponentInParent<Rigidbody2D>();

            // if we have an rigidbody above us, see if it has a ObiRigidbody component and add one if it doesn't:
            if (rb != null)
            {

                obiRigidbody = rb.GetComponent<ObiRigidbody>();

                if (obiRigidbody == null)
                    obiRigidbody = rb.gameObject.AddComponent<ObiRigidbody>();

            }
            else if (rb2D != null)
            {

                obiRigidbody = rb2D.GetComponent<ObiRigidbody2D>();

                if (obiRigidbody == null)
                    obiRigidbody = rb2D.gameObject.AddComponent<ObiRigidbody2D>();

            }

        }

        private void OnTransformParentChanged()
        {
            CreateRigidbody();
        }

        protected void AddCollider()
        {

            Component unityCollider = GetUnityCollider(ref wasUnityColliderEnabled);

            if (unityCollider != null && (shapeHandle == null || !shapeHandle.isValid))
            {
                shapeHandle = ObiColliderWorld.GetInstance().CreateCollider();
                shapeHandle.owner = this;

                // Create shape tracker:
                CreateTracker();

                // Create rigidbody if necessary, and link ourselves to it:
                CreateRigidbody();
            }

        }

        protected void RemoveCollider()
        {
            ObiColliderWorld.GetInstance().DestroyCollider(shapeHandle);

            // Destroy shape tracker:
            if (tracker != null)
            {
                tracker.Destroy();
                tracker = null;
            }
        }

        public void ForceUpdate()
        {
            needsUpdate = true;
        }

        /** 
		 * Check if the collider transform or its shape have changed any relevant property, and update their Oni counterparts.
		 */
        public void UpdateIfNeeded()
        {
            bool unityColliderEnabled = false;
            Component unityCollider = GetUnityCollider(ref unityColliderEnabled);

            if (unityCollider != null)
            {
                // Only if this object is not static:
                if (tracker != null && needsUpdate)
                {
                    tracker.UpdateIfNeeded();
                }

                // store isStatic, *after* updating the tracker at least once.
                needsUpdate = !unityCollider.gameObject.isStatic;
            }
            // If the unity collider is null but its handle is valid, the unity collider has been destroyed.
            else if (shapeHandle != null && shapeHandle.isValid)
                RemoveCollider();
        }

        private void OnEnable()
        {
            needsUpdate = true;

            // Initialize using the source collider specified by the user (or find an appropiate one).
            FindSourceCollider();
        }

        private void OnDisable()
        {
            RemoveCollider();
        }

    }
}

