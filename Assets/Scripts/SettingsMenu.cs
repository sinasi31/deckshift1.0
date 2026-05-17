using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [Header("UI Elemanları")]
    public Slider volumeSlider;
    public Toggle showEnemyNumbersToggle;
    public GameObject settingsPanel;

    // EnemyHealthBar scriptleri bu event'i dinleyerek sayıları göster/gizler
    public static System.Action<bool> OnShowNumbersChanged;

    void Start()
    {
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (showEnemyNumbersToggle != null)
        {
            bool showNumbers = PlayerPrefs.GetInt("ShowEnemyNumbers", 1) == 1;
            showEnemyNumbersToggle.isOn = showNumbers;
            showEnemyNumbersToggle.onValueChanged.AddListener(SetShowEnemyNumbers);
        }
    }

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
    }

    public void SetShowEnemyNumbers(bool show)
    {
        PlayerPrefs.SetInt("ShowEnemyNumbers", show ? 1 : 0);
        PlayerPrefs.Save();
        OnShowNumbersChanged?.Invoke(show);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }
}
