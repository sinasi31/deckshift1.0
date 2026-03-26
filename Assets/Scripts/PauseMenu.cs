using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Header("UI Referanslarý")]
    public GameObject pauseMenuUI;    // Az önce yaptýðýn Panel
    public GameObject settingsMenuUI; // Eðer ayarlar penceresi yaparsan buraya baðlarsýn

    void Update()
    {
        // ESC'ye basýnca menüyü aç/kapat
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
            settingsMenuUI.SetActive(false); // Ayarlar açýksa onu da kapat

        Time.timeScale = 1f; // Zamaný tekrar akýt
        GameIsPaused = false;

        // Eðer GameManager varsa durumu güncelle (Opsiyonel)
        if (GameManager.instance != null)
            GameManager.instance.currentState = GameState.Playing;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f; // Zamaný dondur (Fizik, hareket her þey durur)
        GameIsPaused = true;

        if (GameManager.instance != null)
            GameManager.instance.currentState = GameState.Paused;
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f; // ÇOK ÖNEMLÝ: Menüye dönerken zamaný düzeltmezsen oyun donuk kalýr.
        SceneManager.LoadScene("MainMenu"); // Senin menü sahnenin adý neyse buraya onu yaz
    }

    public void QuitGame()
    {
        Debug.Log("Oyundan Çýkýlýyor..."); // Editörde çýkýþ çalýþmaz, bunu konsola yazar
        Application.Quit();
    }
    public GameObject settingsPanel;
    public void OpenSettings()
    {
        pauseMenuUI.SetActive(false); // Pause menüsünü gizle
        settingsPanel.SetActive(true); // Ayarlarý aç
    }

    public void CloseSettings()
    {
        settingsMenuUI.SetActive(false);
        pauseMenuUI.SetActive(true);
    }
}