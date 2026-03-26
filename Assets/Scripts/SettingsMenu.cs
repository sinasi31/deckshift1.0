using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SettingsMenu : MonoBehaviour
{
    [Header("UI Elemanlarý")]
    public Slider volumeSlider;
    public GameObject settingsPanel; // Panelin kendisi (Kapatmak için)

    void Start()
    {
        // Oyun açýldýðýnda ses seviyesi neyse slider oraya gelsin
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
            // Slider oynatýldýðýnda SetVolume fonksiyonunu çalýþtýr
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    // Slider bu fonksiyonu tetikleyecek
    public void SetVolume(float volume)
    {
        AudioListener.volume = volume; // Oyunun genel sesini ayarlar
    }

    // Back butonuna bu fonksiyonu ver
    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }
}