using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public AudioSource hoverSound;    // Reference to the AudioSource for the hover sound
    public float scaleMultiplier = 1.2f;   // The amount to scale the button by on hover
    private Vector3 originalScale;    // Store the original scale to revert back to

    void Start() {
        originalScale = transform.localScale; // Store the initial scale
    }

    public void OnPointerEnter(PointerEventData eventData) {
        // Scale up the button
        transform.localScale = originalScale * scaleMultiplier;

        // Play the hover sound
        if (hoverSound != null) {
            hoverSound.Play();
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        // Reset the button to its original scale
        transform.localScale = originalScale;
    }
}
