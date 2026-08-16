using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Paneller")]
    // ⚠️ `settingsPanel` is gone (2026-08-09). The main menu and the pause menu now open the SAME
    // SettingsScreen, which is the only sane arrangement: with two copies, every new setting had to
    // be added twice and the two would drift apart the first time one was missed. The old
    // SettingsPanel prefab and its SettingsMenu script were deleted with it.
    public GameObject tutorialPanel;

    // PLAY butonu i�in
    //
    // Play always asks who you are playing. The screen carries the pick over to the run itself
    // through CharacterSelection; if it cannot open for any reason it starts the run anyway rather
    // than leaving the button dead.
    public void PlayGame()
    {
        CharacterSelectScreen.Open(StartRun);
    }

    private void StartRun()
    {
        // Build Settings'deki s�radaki sahneyi y�kle
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    // SETTINGS butonu i�in
    public void OpenSettings()
    {
        SettingsScreen.Open();
    }

    // HOW TO PLAY butonu i�in
    public void OpenTutorial()
    {
        tutorialPanel.SetActive(true);
    }

    // QUIT butonu i�in
    public void QuitGame()
    {
        Debug.Log("��k�� yap�ld�.");
        Application.Quit();
    }

    // Tutorial panelinin i�indeki "Back" butonuna bunu ba�layabilirsin
    // Ya da direkt Button'�n OnClick olay�na TutorialPanel objesini s�r�kleyip
    // GameObject.SetActive (false) yapabilirsin. Kodsuz ��z�m :)
    public void CloseTutorial()
    {
        tutorialPanel.SetActive(false);
    }
}