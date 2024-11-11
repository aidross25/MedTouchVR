using System.Collections;     // Needed for IEnumerator
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ButtonClickHandler : MonoBehaviour {
    public AudioSource clickSound;       // Reference to the AudioSource component for the click sound
    public string sceneToLoad;           // Name of the scene to load on button click

    // Method to handle button click
    public void OnButtonClick() {
        // Play the click sound if it's assigned
        if (clickSound != null) {
            clickSound.Play();
        }

        // Load the target scene after the sound plays (with delay)
        StartCoroutine(LoadSceneAfterSound());
    }

    // Coroutine to delay the scene load until the sound has finished
    private IEnumerator LoadSceneAfterSound() {
        // Wait until the sound is done playing
        yield return new WaitForSeconds(clickSound.clip.length);
        
        // Load the new scene
        SceneManager.LoadScene(sceneToLoad);
    }
}
