using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class MainMenu : MonoBehaviour
{
    public AudioSource clickSound;    // Reference to the AudioSource component for the click sound

    public async void PlayGame(string sceneName) {
        if (clickSound != null) {
            clickSound.Play();
            await WaitForSoundToFinish(clickSound);
        }
        SceneManager.LoadScene(sceneName);
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
