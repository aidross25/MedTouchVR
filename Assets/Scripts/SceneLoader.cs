using UnityEngine;
using UnityEngine.SceneManagement;  // For SceneManager

public class SceneLoader : MonoBehaviour
{
    // The names of the scenes to toggle between
    public string firstSceneName = "operatingRoom";  // Name of your first scene
    public string secondSceneName = "mainMenu";  // Name of your second scene

    // Track which scene is currently loaded (used for toggling)
    private bool isFirstScene = true;

    void Update()
    {
        // Check if the Space key is pressed
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Toggle between scenes
            LoadNextScene();
        }
    }

    void LoadNextScene()
    {
        // Alternate between the scenes
        string sceneToLoad = isFirstScene ? firstSceneName : secondSceneName;

        // Check if the scene exists and load it
        if (SceneExists(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
            // Toggle the scene for the next press
            isFirstScene = !isFirstScene;
        }
        else
        {
            Debug.LogError("Scene " + sceneToLoad + " does not exist.");
        }
    }

    bool SceneExists(string sceneName)
    {
        // Check if the scene exists in the Build Settings
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            if (SceneManager.GetSceneByBuildIndex(i).name == sceneName)
            {
                return true;
            }
        }
        return false;
    }
}
