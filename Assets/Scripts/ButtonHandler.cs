using UnityEngine;
// using UnityEngine.UI; // Removed unnecessary using directive
using UnityEngine.EventSystems;
using System.Collections;     // Needed for IEnumerator
using UnityEngine.SceneManagement;
public class ButtonHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public AudioSource hoverSound;    // Reference to the AudioSource for the hover sound
    public float scaleMultiplier = 1.2f;   // The amount to scale the button by on hover
    private Vector3 originalScale;    // Store the original scale to revert back to
public AudioSource clickSound;       // Reference to the AudioSource component for the click sound
    public string sceneToLoad;           // Name of the scene to load on button click

    // Method to handle button click
    public void OnButtonClick()
{
    Debug.Log("Button Clicked!");
    if (clickSound != null)
    {
        clickSound.Play();
    }

    StartCoroutine(LoadSceneAfterSound());
}


    // Coroutine to delay the scene load until the sound has finished
    private IEnumerator LoadSceneAfterSound() {
        // Wait until the sound is done playing
        yield return new WaitForSeconds(clickSound.clip.length);
        
        // Load the new scene
        SceneManager.LoadScene(sceneToLoad);
    }
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
