using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class JaceGameManager : MonoBehaviour
{
    public static JaceGameManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject      winPanel;
    public GameObject      gameOverPanel;
    public TextMeshProUGUI healthText;

    private bool gameEnded;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (winPanel)      winPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        JacePlayerHealth ph = FindFirstObjectByType<JacePlayerHealth>();
        if (ph != null)
        {
            ph.OnHealthChanged += UpdateHealthUI;
            UpdateHealthUI(ph.CurrentHealth, ph.maxHealth);
        }
    }

    public void Win()
    {
        if (gameEnded) return;
        gameEnded = true;
        Time.timeScale = 0.4f;
        Invoke(nameof(ShowWin), 0.6f);
    }

    public void GameOver()
    {
        if (gameEnded) return;
        gameEnded = true;
        Invoke(nameof(ShowGameOver), 0.5f);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    void UpdateHealthUI(int current, int max)
    {
        if (healthText != null)
            healthText.text = $"HP  {current} / {max}";
    }

    void ShowWin()
    {
        Time.timeScale = 1f;
        if (winPanel) winPanel.SetActive(true);
    }

    void ShowGameOver()
    {
        if (gameOverPanel) gameOverPanel.SetActive(true);
    }
}
