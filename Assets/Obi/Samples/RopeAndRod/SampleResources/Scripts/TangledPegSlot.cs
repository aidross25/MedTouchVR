using UnityEngine;

namespace Obi.Samples
{
    public class TangledPegSlot : MonoBehaviour
    {
        public TangledPeg currentPeg;
        public Color tintColor;

        private Material instance;
        private Color normalColor;

        public void Awake()
        {
            instance = GetComponent<Renderer>().material;
            normalColor = instance.color;
        }

        public void Tint()
        {
            instance.color = tintColor;
        }

        public void ResetColor()
        {
            instance.color = normalColor;
        }

        public void OnDestroy()
        {
            Destroy(instance);
        }
    }
}
