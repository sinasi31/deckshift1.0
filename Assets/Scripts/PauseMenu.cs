using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Header("UI Referanslar�")]
    public GameObject pauseMenuUI;    // Az �nce yapt���n Panel
    public GameObject settingsMenuUI; // E�er ayarlar penceresi yaparsan buraya ba�lars�n

    void Update()
    {
        // ESC'ye bas�nca men�y� a�/kapat
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);

        if (settingsMenuUI != null)
            settingsMenuUI.SetActive(false); // Ayarlar a��ksa onu da kapat

        if (GameManager.instance != null) GameManager.instance.ReleasePause();
        GameIsPaused = false;

        // E�er GameManager varsa durumu g�ncelle (Opsiyonel)
        if (GameManager.instance != null)
            GameManager.instance.currentState = GameState.Playing;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);
        if (GameManager.instance != null) GameManager.instance.RequestPause();
        GameIsPaused = true;

        if (GameManager.instance != null)
            GameManager.instance.currentState = GameState.Paused;
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f; // �OK �NEML�: Men�ye d�nerken zaman� d�zeltmezsen oyun donuk kal�r.
        SceneManager.LoadScene("MainMenu"); // Senin men� sahnenin ad� neyse buraya onu yaz
    }

    public void QuitGame()
    {
        Debug.Log("Oyundan ��k�l�yor..."); // Edit�rde ��k�� �al��maz, bunu konsola yazar
        Application.Quit();
    }
    public GameObject settingsPanel;
    public void OpenSettings()
    {
        pauseMenuUI.SetActive(false); // Pause men�s�n� gizle
        settingsPanel.SetActive(true); // Ayarlar� a�
    }

    public void CloseSettings()
    {
        settingsMenuUI.SetActive(false);
        pauseMenuUI.SetActive(true);
    }
}