using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    // Bu fonksiyonu "Yeniden Baþla" butonunun OnClick() event'ine baðlayacaðýz.
    public void RestartGame()
    {
        // TODO: Buraya ana oyun sahnesinin adýný doðru yazdýðýndan emin ol!
        // Genellikle "SampleScene", "MainScene" veya "GameScene" olur.
        SceneManager.LoadScene("SampleScene");
    }

    // Bu fonksiyonu "Ana Menü" butonunun OnClick() event'ine baðlayacaðýz.
    public void GoToMainMenu()
    {
        // TODO: Ana Menü sahnenin adýný buraya yaz.
        SceneManager.LoadScene("MainMenuScene");
    }
}