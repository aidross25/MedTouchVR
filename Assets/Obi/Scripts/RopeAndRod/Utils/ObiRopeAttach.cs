using UnityEngine;

namespace Obi
{
    public class ObiRopeAttach : MonoBehaviour
    {
        public ObiPathSmoother smoother;
        [Range(0,1)]
        public float m;

        public void LateUpdate()
		{
            if (smoother != null && smoother.actor.isLoaded)
            {
                var trfm = smoother.actor.solver.transform;
                ObiPathFrame section = smoother.GetSectionAt(m);
                transform.position = trfm.TransformPoint(section.position);
                transform.rotation = trfm.rotation * Quaternion.LookRotation(section.tangent, section.binormal);
            }
		}

	}
}