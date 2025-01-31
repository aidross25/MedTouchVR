using UnityEngine;

namespace Obi
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObiCollider))]
	public class ObiForceZone : MonoBehaviour
	{
        [SerializeProperty("sourceCollider")]
        [SerializeField] private ObiCollider m_SourceCollider;

        protected ObiForceZoneHandle forcezoneHandle;

        /// <summary>
        /// The ObiCollider this ObiForceZone should affect.
        /// </summary>
        /// This is automatically set when you first create the ObiForceZone component, but you can override it afterwards.
        public ObiCollider sourceCollider
        {
            set
            {
                if (value != null && value.gameObject != this.gameObject)
                {
                    Debug.LogError("The ObiCollider component must reside in the same GameObject as ObiForceZone.");
                    return;
                }

                RemoveCollider();
                m_SourceCollider = value;
                AddCollider();

            }
            get { return m_SourceCollider; }
        }

        public ObiForceZoneHandle handle
        {
            get
            {
                if (forcezoneHandle == null || !forcezoneHandle.isValid)
                {
                    var world = ObiColliderWorld.GetInstance();

                    // create the material:
                    forcezoneHandle = world.CreateForceZone();
                    forcezoneHandle.owner = this;
                }
                return forcezoneHandle;
            }
        }

        public ForceZone.ZoneType type;
        public ForceZone.ForceMode mode;
        public float intensity;

        [Header("Damping")]
        public ForceZone.DampingDirection dampingDir;
        public float damping = 0;

        [Header("Falloff")]
        public float minDistance;
        public float maxDistance;
        [Min(0)]
        public float falloffPower = 1;

        [Header("Pulse")]
        public float pulseIntensity;
        public float pulseFrequency;
        public float pulseSeed;

        protected float intensityVariation;

        public void OnEnable()
        {
            FindSourceCollider();

            //handle = ObiColliderWorld.GetInstance().CreateForceZone();
            //handle.owner = this;
        }

        public void OnDisable()
        {
            RemoveCollider();
            ObiColliderWorld.GetInstance().DestroyForceZone(handle);
        }

        private void FindSourceCollider()
        {
            if (sourceCollider == null)
                sourceCollider = GetComponent<ObiCollider>();
            else
                AddCollider();
        }

        private void AddCollider()
        {
            if (m_SourceCollider != null)
                m_SourceCollider.ForceZone = this;
        }

        private void RemoveCollider()
        {
            if (m_SourceCollider != null)
                m_SourceCollider.ForceZone = null;
        }

        public virtual void UpdateIfNeeded()
        {
            var fc = ObiColliderWorld.GetInstance().forceZones[handle.index];
            fc.type = type;
            fc.mode = mode;
            fc.intensity = intensity + intensityVariation;
            fc.minDistance = minDistance;
            fc.maxDistance = maxDistance;
            fc.falloffPower = falloffPower;
            fc.damping = damping;
            fc.dampingDir = dampingDir;
            ObiColliderWorld.GetInstance().forceZones[handle.index] = fc;
        }

        public void Update()
        {
            if (Application.isPlaying)
                intensityVariation = Mathf.PerlinNoise(Time.time * pulseFrequency, pulseSeed) * pulseIntensity;
        }
    }
}

