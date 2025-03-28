using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;


public class ButtonHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public AudioSource hoverSound;    // Reference to the AudioSource for the hover sound
    public float scaleMultiplier = 1.2f;   // The amount to scale the button by on hover
    private Vector3 originalScale;    // Store the original scale to revert back to

    void Start() {
        originalScale = transform.localScale;
    }
    void Update() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            SceneManager.LoadScene("endMenu");
        }
    }
    public void OnPointerEnter(PointerEventData eventData) {
        transform.localScale = originalScale * scaleMultiplier;
        if (hoverSound != null) {
            hoverSound.Play();
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        transform.localScale = originalScale;
    }
}
