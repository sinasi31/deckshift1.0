using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum ShopItemType { Card, Relic, Service }

public class ShopItemUI : MonoBehaviour
{
    [Header("UI References")]
    public Image itemIcon;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI descriptionText; // Hover yapılınca görünecek (opsiyonel)
    public GameObject soldOutImage; // Satılınca üzerine gelecek "SOLD" görseli
    public Button buyButton;

    private int price;
    private ShopItemType type;

    // Satılan şeyin verisi
    private CardData cardData;
    private RelicData relicData;
    private System.Action serviceAction; // Servis (Heal, Remove Card vb.) için fonksiyon

    // --- KART İÇİN KURULUM ---
    public void SetupCard(CardData card, int cost)
    {
        type = ShopItemType.Card;
        cardData = card;
        price = cost;

        itemIcon.sprite = card.cardArt;
        if (nameText) nameText.text = card.cardName;
        UpdatePriceUI();

        // Tooltip/Açıklama ayarı (CardUI'daki gibi hover sistemi de kurabilirsin)
        if (descriptionText) descriptionText.text = card.description;
    }

    // --- RELIC İÇİN KURULUM ---
    public void SetupRelic(RelicData relic, int cost)
    {
        type = ShopItemType.Relic;
        relicData = relic;
        price = cost;

        itemIcon.sprite = relic.relicArt;
        if (nameText) nameText.text = relic.relicName;
        UpdatePriceUI();

        if (descriptionText) descriptionText.text = relic.description;
    }

    // --- SERVİS (CAN DOLDURMA VB.) İÇİN KURULUM ---
    public void SetupService(string name, Sprite icon, int cost, string desc, System.Action onBuy)
    {
        type = ShopItemType.Service;
        serviceAction = onBuy;
        price = cost;

        itemIcon.sprite = icon;
        if (nameText) nameText.text = name;
        UpdatePriceUI();

        if (descriptionText) descriptionText.text = desc;
    }

    private void UpdatePriceUI()
    {
        if (priceText) priceText.text = price.ToString() + " G";
        if (soldOutImage) soldOutImage.SetActive(false);
        buyButton.interactable = true;
    }

    // Butona bağlanacak fonksiyon
    public void OnClickBuy()
    {
        PlayerController player = GameManager.instance.player;

        // 1. Para Kontrolü
        if (player.TrySpendGold(price))
        {
            // 2. Ürünü Ver
            switch (type)
            {
                case ShopItemType.Card:
                    DeckManager.instance.AddCardToDeck(cardData);
                    Debug.Log("Kart Satın Alındı: " + cardData.cardName);
                    break;
                case ShopItemType.Relic:
                    RelicManager.instance.AddRelic(relicData);
                    Debug.Log("Relic Satın Alındı: " + relicData.relicName);
                    break;
                case ShopItemType.Service:
                    serviceAction?.Invoke(); // Servis fonksiyonunu çalıştır
                    Debug.Log("Servis Satın Alındı.");
                    break;
            }

            // 3. Görseli "Satıldı" yap
            BuySuccessful();
        }
        else
        {
            // Para yetmedi efekti (Kızarabilir veya ses çıkabilir)
            Debug.Log("Para Yetmiyor!");
            // Örn: priceText.color = Color.red; (Sonra geri düzeltmek gerekir)
        }
    }

    private void BuySuccessful()
    {
        if (soldOutImage) soldOutImage.SetActive(true);
        buyButton.interactable = false; // Tekrar alınamasın
        // İstersen burada itemIcon.color = Color.gray; yapabilirsin.
    }
}