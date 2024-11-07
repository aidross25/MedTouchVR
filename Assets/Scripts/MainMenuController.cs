using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class MainMenuController : MonoBehaviour
{
    public GameObject playMenu; // UI section that appears after clicking Play
    public GameObject loadPopup; // Popup for loading saves
    public GameObject procedureMenu; // Menu for selecting a new procedure
    public GameObject creditsView; // Credits view with team names and roles
    public Button playButton, loadButton, newGameButton, creditsButton, backButton;
    public TextMeshProUGUI lastConfigNameText;

    void Start()
    {
        HideAllMenus();
        playButton.onClick.AddListener(ShowPlayMenu);
        loadButton.onClick.AddListener(ShowLoadPopup);
        newGameButton.onClick.AddListener(ShowProcedureMenu);
        creditsButton.onClick.AddListener(ShowCredits);
        backButton.onClick.AddListener(HideAllMenus);
    }

    void HideAllMenus()
    {
        playMenu.SetActive(false);
        loadPopup.SetActive(false);
        procedureMenu.SetActive(false);
        creditsView.SetActive(false);
    }

    void ShowPlayMenu()
    {
        playMenu.SetActive(true);
    }

    void ShowLoadPopup()
    {
        loadPopup.SetActive(true);
        lastConfigNameText.text = "Last Config: [Insert Config Name Here]"; // Update with saved config name
    }

    void ConfirmLoad(bool confirm)
    {
        if (confirm)
            LoadLastSavedProcedure(); // Load last saved procedure if confirmed
        else
            HideAllMenus(); // Return to main menu if declined
    }

    void ShowProcedureMenu()
    {
        procedureMenu.SetActive(true);
    }

    void StartProcedure(string procedureName)
    {
        // Code to initialize the selected procedure in the operating room
    }

    void ShowCredits()
    {
        creditsView.SetActive(true);
    }

    void LoadLastSavedProcedure()
    {
        // Load the scene or setup for the last saved procedure
    }
}
