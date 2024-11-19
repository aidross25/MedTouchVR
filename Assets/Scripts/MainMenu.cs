using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System.Threading.Tasks;

public class MainMenu : MonoBehaviour
{
    public AudioSource clickSound;    // Reference to the AudioSource component for the click sound

    public async void PlayGame() {
        if (clickSound != null) {
            clickSound.Play();
            await WaitForSoundToFinish(clickSound);
        }
        SceneManager.LoadScene("operatingRoom");
    }

    public async void QuitGame() {
        if (clickSound != null) {
            clickSound.Play();
            await WaitForSoundToFinish(clickSound);
        }
        Application.Quit();
    }

    public async void Credits() {
        if (clickSound != null) {
            clickSound.Play();
            await WaitForSoundToFinish(clickSound);
        }
    }

    private async Task WaitForSoundToFinish(AudioSource audioSource) {
        while (audioSource.isPlaying) {
            await Task.Yield();
        }
    }
}
