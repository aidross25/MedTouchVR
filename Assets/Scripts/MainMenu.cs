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
        await PlayClickedSound();
        // SceneManager.LoadScene("operatingRoom");
    }

    public async void QuitGame() {
        await PlayClickedSound();
        Application.Quit();
    }

    public async void PlayClickSound() {
        await PlayClickedSound();
    }

    private async Task PlayClickedSound() {
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
