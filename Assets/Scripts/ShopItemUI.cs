using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ShopItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    public Image itemIcon;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public GameObject soldOutImage;
    public Button buyButton;

    [Header("Kart Bilgileri (Sadece Kartlar İçin)")]
    public GameObject cardStatsPanel; // Shift ve Charge'ın olduğu kutu
    public TextMeshProUGUI shiftText;  // Shift bedelini yazan text
    public TextMeshProUGUI chargeText; // Charge sayısını yazan text

    [Header("Hover / Tooltip")]
    public GameObject descriptionPanel;
    public TextMeshProUGUI descriptionText;

    // --- SES AYARLARI EKLENDİ ---
    [Header("Ses Efektleri")]
    public AudioClip buySound; // Ses dosyasını buraya sürükle
    [Range(0f, 1f)] public float soundVolume = 0.5f;
    // ----------------------------

    private ShopSlotData myData;
    private ShopItemType type;
    private int price;
    private System.Action serviceAction;

    private void Start()
    {
        if (descriptionPanel != null) descriptionPanel.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (descriptionPanel != null && buyButton.interactable)
            descriptionPanel.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (descriptionPanel != null)
            descriptionPanel.SetActive(false);
    }

    public void SetupFromData(ShopSlotData data)
    {
        myData = data;
        type = data.itemType;
        price = data.price;

        if (type == ShopItemType.Card)
        {
            itemIcon.sprite = data.cardReference.cardArt;
            if (nameText) nameText.text = data.cardReference.cardName;
            if (descriptionText) descriptionText.text = data.cardReference.description;

            if (cardStatsPanel) cardStatsPanel.SetActive(true);
            if (shiftText) shiftText.text = data.cardReference.shiftCost.ToString();
            if (chargeText) chargeText.text = data.cardReference.maxUses.ToString();
        }
        else if (type == ShopItemType.Relic)
        {
            itemIcon.sprite = data.relicReference.relicArt;
            if (nameText) nameText.text = data.relicReference.relicName;
            if (descriptionText) descriptionText.text = data.relicReference.description;
            if (cardStatsPanel) cardStatsPanel.SetActive(false);
        }

        UpdatePriceUI();

        if (myData.isSold) BuySuccessful();
    }

    public void SetupService(string name, Sprite icon, int cost, string desc, System.Action onBuy)
    {
        myData = null;
        type = ShopItemType.Service;
        serviceAction = onBuy;
        price = cost;

        itemIcon.sprite = icon;
        if (nameText) nameText.text = name;
        if (descriptionText) descriptionText.text = desc;

        if (cardStatsPanel) cardStatsPanel.SetActive(false);

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

        // Relics route through the slot system so the "pay only if you take it" rule holds
        // (RelicRedesign.md): if the loadout is full, the Swap Screen decides first and gold is
        // charged only on TAKE — a "leave it" costs nothing.
        if (type == ShopItemType.Relic && myData != null)
        {
            if (player.currentGold < price) return;   // can't afford — same silent no-op as before
            RelicManager.instance.TryGrantRelic(myData.relicReference, () =>
            {
                player.TrySpendGold(price);            // charged only when actually acquired
                PlayBuySound();
                myData.isSold = true;
                BuySuccessful();
            });
            return;
        }

        // Cards & services: charge upfront as before.
        if (player.TrySpendGold(price))
        {
            PlayBuySound();

            switch (type)
            {
                case ShopItemType.Card:
                    if (myData != null) DeckManager.instance.AddCardToDeck(myData.cardReference);
                    break;
                case ShopItemType.Service:
                    serviceAction?.Invoke();
                    break;
            }

            if (myData != null) myData.isSold = true;
            BuySuccessful();
        }
    }

    // Buy SFX at the camera so it's audible everywhere.
    private void PlayBuySound()
    {
        if (buySound != null)
            SfxManager.PlayAtPoint(buySound, Camera.main.transform.position, soundVolume);
    }

    private void BuySuccessful()
    {
        if (soldOutImage) soldOutImage.SetActive(true);
        buyButton.interactable = false;
        if (descriptionPanel != null) descriptionPanel.SetActive(false);
    }
}