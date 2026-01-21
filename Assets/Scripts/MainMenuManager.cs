using UnityEngine;
using UnityEngine.SceneManagement; // Sahne deðiþimi için bu kütüphane þart!

public class MainMenuManager : MonoBehaviour
{
    // BAÞLA BUTONU ÝÇÝN
    public void PlayGame()
    {
        // Build Settings listesindeki bir sonraki sahneye geçer
        // (Menü 0 ise, Oyun 1. sahnede olmalý)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    // ÇIKIÞ BUTONU ÝÇÝN
    public void QuitGame()
    {
        Debug.Log("Oyundan Çýkýldý!"); // Editörde kapanmaz, sadece konsola yazar.
        Application.Quit();
    }

    // AYARLAR BUTONU ÝÇÝN (Þimdilik boþ býrakabilirsin veya panel açtýrabilirsin)
    public void OpenSettings()
    {
        Debug.Log("Ayarlar açýlacak...");
        // Ýlerde buraya panel açma kodu ekleyeceðiz.
    }
}
