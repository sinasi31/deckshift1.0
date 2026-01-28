using UnityEngine;
using TMPro;

public class GoldUI : MonoBehaviour
{
    [Header("UI Referanslarý")]
    public TextMeshProUGUI goldText; // Sayýnýn yazacaðý yer

    // Eðer altýn deðiþmediyse boþuna text'i güncellemesin diye bir kontrol ekleyelim
    private int lastGoldAmount = -1;

    void Update()
    {
        // GameManager veya Player var mý kontrol et
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            int currentGold = GameManager.instance.player.currentGold;

            // Sadece sayý deðiþtiyse ekrana yaz (Performans dostu)
            if (currentGold != lastGoldAmount)
            {
                goldText.text = currentGold.ToString();
                lastGoldAmount = currentGold;
            }
        }
    }
}