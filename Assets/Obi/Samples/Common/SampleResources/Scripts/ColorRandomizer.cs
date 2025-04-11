using UnityEngine;

namespace Obi.Samples
{
	[RequireComponent(typeof(ObiActor))]
	public class ColorRandomizer : MonoBehaviour
	{
		ObiActor actor;
		public Gradient gradient = new Gradient();

		void Start()
        {
			actor = GetComponent<ObiActor>();
            actor.OnBlueprintLoaded += Actor_OnBlueprintLoaded;
		}

        private void Actor_OnBlueprintLoaded(ObiActor a, ObiActorBlueprint blueprint)
        {
            for (int i = 0; i < actor.solverIndices.count; ++i)
            {
                actor.solver.colors[actor.solverIndices[i]] = gradient.Evaluate(UnityEngine.Random.value);
            }
        }
    }
}

