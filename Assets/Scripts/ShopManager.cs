using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShopManager : MonoBehaviour
{
    public static ShopManager instance;

    [Header("UI Panel")]
    public GameObject shopPanel;
    public TextMeshProUGUI playerGoldText;

    [Header("Genel Havuzlar")]
    public List<CardData> allCardsPool;
    public List<RelicData> allRelicsPool;

    [Header("Servis Ýkonlarý")]
    public Sprite healIcon;
    public Sprite shiftIcon; // <-- YENÝ: Shift ikonu için bunu sürükle

    [Header("Dükkan Raflarý (UI Slotlar)")]
    // Unity'de buraya 5 tane slot sürükle
    public List<ShopItemUI> cardSlots;
    // Unity'de buraya 3 tane slot sürükle
    public List<ShopItemUI> relicSlots;

    // ARTIK 2 AYRI SLOTUMUZ VAR
    public ShopItemUI healServiceSlot;
    public ShopItemUI shiftServiceSlot; // <-- YENÝ: +3 Shift slotu

    // ÞU AN AÇIK OLAN MARKET
    private Shopkeeper currentShopkeeper;

    [Header("Servis Fiyatlarý")]
    public int healCost = 50;
    public int healAmount = 30;

    public int shiftCost = 75; // <-- YENÝ: Shift fiyatý
    public int shiftAmount = 3; // <-- YENÝ: Kaç shift vereceði

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void Start()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    public void OpenShop(Shopkeeper shop)
    {
        currentShopkeeper = shop;
        GameManager.instance.SetGameState(GameState.Paused);
        Time.timeScale = 0f;
        shopPanel.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        UpdateGoldUI();
        LoadShopContent();
    }

    public void CloseShop()
    {
        shopPanel.SetActive(false);
        Time.timeScale = 1f;
        GameManager.instance.SetGameState(GameState.Playing);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.Confined;
        currentShopkeeper = null;
    }

    private void UpdateGoldUI()
    {
        if (playerGoldText && GameManager.instance.player)
        {
            playerGoldText.text = "Gold: " + GameManager.instance.player.currentGold;
        }
    }

    private void Update()
    {
        if (shopPanel.activeSelf)
        {
            UpdateGoldUI();
            if (Input.GetKeyDown(KeyCode.Escape)) CloseShop();
        }
    }

    private void LoadShopContent()
    {
        // 1. Önce tüm slotlarý kapat (Temizlik)
        foreach (var slot in cardSlots) slot.gameObject.SetActive(false);
        foreach (var slot in relicSlots) slot.gameObject.SetActive(false);
        if (healServiceSlot) healServiceSlot.gameObject.SetActive(false);
        if (shiftServiceSlot) shiftServiceSlot.gameObject.SetActive(false);

        if (currentShopkeeper == null) return;

        // 2. Shopkeeper'dan gelenleri diz
        int cardIndex = 0;
        int relicIndex = 0;

        foreach (ShopSlotData data in currentShopkeeper.myInventory)
        {
            // Kartlarý Diz (Listenin boyutu kadar)
            if (data.itemType == ShopItemType.Card && cardIndex < cardSlots.Count)
            {
                ShopItemUI slot = cardSlots[cardIndex];
                slot.gameObject.SetActive(true);
                slot.SetupFromData(data);
                cardIndex++;
            }
            // Relicleri Diz
            else if (data.itemType == ShopItemType.Relic && relicIndex < relicSlots.Count)
            {
                ShopItemUI slot = relicSlots[relicIndex];
                slot.gameObject.SetActive(true);
                slot.SetupFromData(data);
                relicIndex++;
            }
        }
        if (healServiceSlot != null)
        {
            healServiceSlot.gameObject.SetActive(true);
            healServiceSlot.SetupService(
                "Medical Kit",
                healIcon,
                healCost,
                $"Restores <color=green>{healAmount} HP</color>.",
                () => {
                    // Satýn alýnýnca çalýþacak kod:
                    GameManager.instance.player.Heal(healAmount);
                }
            );
        }
        if (shiftServiceSlot != null)
        {
            shiftServiceSlot.gameObject.SetActive(true);
            shiftServiceSlot.SetupService(
                "Shift Battery",
                shiftIcon,
                shiftCost,
                $"Grants +{shiftAmount} Shift</color>.",
                () => {
                    GameManager.instance.player.AddShift(shiftAmount);
                }
            );
        }
    }
}