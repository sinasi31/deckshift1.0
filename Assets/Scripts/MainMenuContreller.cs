using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Paneller")]
    public GameObject settingsPanel;
    public GameObject tutorialPanel;

    // PLAY butonu için
    public void PlayGame()
    {
        // Build Settings'deki sýradaki sahneyi yükle
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    // SETTINGS butonu için
    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    // HOW TO PLAY butonu için
    public void OpenTutorial()
    {
        tutorialPanel.SetActive(true);
    }

    // QUIT butonu için
    public void QuitGame()
    {
        Debug.Log("Çýkýþ yapýldý.");
        Application.Quit();
    }

    // Tutorial panelinin içindeki "Back" butonuna bunu baðlayabilirsin
    // Ya da direkt Button'ýn OnClick olayýna TutorialPanel objesini sürükleyip
    // GameObject.SetActive (false) yapabilirsin. Kodsuz çözüm :)
    public void CloseTutorial()
    {
        tutorialPanel.SetActive(false);
    }
}