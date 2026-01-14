using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // Fare hareketlerini algılamak için gerekli

public enum ShopItemType { Card, Relic, Service }

public class ShopItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    public Image itemIcon;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public GameObject soldOutImage; // "SOLD" görseli buraya gelecek
    public Button buyButton;

    [Header("Hover / Tooltip Settings")]
    public GameObject descriptionPanel; // Yeni: Açıklama kutusunun kendisi
    public TextMeshProUGUI descriptionText; // Yeni: Açıklamanın yazıldığı text

    private int price;
    private ShopItemType type;
    private CardData cardData;
    private RelicData relicData;
    private System.Action serviceAction;

    private void Start()
    {
        // Oyun başında açıklama paneli kapalı olsun
        if (descriptionPanel != null) descriptionPanel.SetActive(false);
    }

    // Fare üzerine gelince paneli aç
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (descriptionPanel != null && buyButton.interactable)
        {
            descriptionPanel.SetActive(true);
        }
    }

    // Fare üzerinden gidince paneli kapat
    public void OnPointerExit(PointerEventData eventData)
    {
        if (descriptionPanel != null)
        {
            descriptionPanel.SetActive(false);
        }
    }

    public void SetupCard(CardData card, int cost)
    {
        type = ShopItemType.Card;
        cardData = card;
        price = cost;
        itemIcon.sprite = card.cardArt;
        if (nameText) nameText.text = card.cardName;
        if (descriptionText) descriptionText.text = card.description;
        UpdatePriceUI();
    }

    public void SetupRelic(RelicData relic, int cost)
    {
        type = ShopItemType.Relic;
        relicData = relic;
        price = cost;
        itemIcon.sprite = relic.relicArt;
        if (nameText) nameText.text = relic.relicName;
        if (descriptionText) descriptionText.text = relic.description;
        UpdatePriceUI();
    }

    public void SetupService(string name, Sprite icon, int cost, string desc, System.Action onBuy)
    {
        type = ShopItemType.Service;
        serviceAction = onBuy;
        price = cost;
        itemIcon.sprite = icon;
        if (nameText) nameText.text = name;
        if (descriptionText) descriptionText.text = desc;
        UpdatePriceUI();
    }

    private void UpdatePriceUI()
    {
        if (priceText) priceText.text = price.ToString() + " G";
        if (soldOutImage) soldOutImage.SetActive(false);
        buyButton.interactable = true;
    }

    public void OnClickBuy()
    {
        PlayerController player = GameManager.instance.player;
        if (player.TrySpendGold(price))
        {
            switch (type)
            {
                case ShopItemType.Card:
                    DeckManager.instance.AddCardToDeck(cardData);
                    break;
                case ShopItemType.Relic:
                    RelicManager.instance.AddRelic(relicData);
                    break;
                case ShopItemType.Service:
                    serviceAction?.Invoke();
                    break;
            }
            BuySuccessful();
        }
    }

    private void BuySuccessful()
    {
        if (soldOutImage) soldOutImage.SetActive(true);
        buyButton.interactable = false;
        if (descriptionPanel != null) descriptionPanel.SetActive(false);
    }
}