using UnityEngine;
using TMPro; // TextMeshPro kullandýðýný varsayýyorum

public class GoldUI : MonoBehaviour
{
    public TextMeshProUGUI goldText; // Inspector'dan Text'i buraya sürükle

    private void Start()
    {
        // Oyun baþýnda Player'ý bulmaya çalýþ (GameManager veya Singleton üzerinden)
        PlayerController player = FindFirstObjectByType<PlayerController>();

        if (player != null)
        {
            // Event'e abone ol (Altýn deðiþince bana haber ver)
            player.OnGoldChanged += UpdateGoldText;

            // Baþlangýç miktarýný yazdýr
            UpdateGoldText(player.currentGold);
        }
    }

    private void OnDestroy()
    {
        // Sahne deðiþirken abonelikten çýk (Hata vermemesi için)
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.OnGoldChanged -= UpdateGoldText;
        }
    }

    private void UpdateGoldText(int amount)
    {
        if (goldText != null)
        {
            // Ýstersen baþýna simge falan koyabilirsin: $"{amount} G"
            goldText.text = amount.ToString();
        }
    }
}