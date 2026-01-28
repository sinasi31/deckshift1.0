using UnityEngine;
using UnityEngine.SceneManagement; // Sahne deðiþimi için bu Kütüphane ÞART!

public class MainMenuController : MonoBehaviour
{
    // OYNA Butonuna basýnca çalýþacak
    public void PlayGame()
    {
        // Build Settings listesindeki bir sonraki sahneye geç (Genelde oyun sahnesi)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    // ÇIKIÞ Butonuna basýnca çalýþacak
    public void QuitGame()
    {
        Debug.Log("Oyundan Çýkýldý!"); // Unity Editöründe çýkýþ çalýþmaz, bunu konsola yazarýz.
        Application.Quit();
    }
}
