using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShopManager : MonoBehaviour
{
    public static ShopManager instance;

    [Header("UI Panel")]
    public GameObject shopPanel;
    [SerializeField] private GameObject gameplayHUD;
    public TextMeshProUGUI playerGoldText;

    [Header("Genel Havuzlar")]
    public List<CardData> allCardsPool;
    public List<RelicData> allRelicsPool;

    [Header("Servis �konlar�")]
    public Sprite healIcon;
    public Sprite shiftIcon; // <-- YEN�: Shift ikonu i�in bunu s�r�kle

    [Header("D�kkan Raflar� (UI Slotlar)")]
    // Unity'de buraya 5 tane slot s�r�kle
    public List<ShopItemUI> cardSlots;
    // Unity'de buraya 3 tane slot s�r�kle
    public List<ShopItemUI> relicSlots;

    // ARTIK 2 AYRI SLOTUMUZ VAR
    public ShopItemUI healServiceSlot;
    public ShopItemUI shiftServiceSlot; // <-- YEN�: +3 Shift slotu

    // �U AN A�IK OLAN MARKET
    private Shopkeeper currentShopkeeper;

    [Header("Servis Fiyatlar�")]
    public int healCost = 50;
    public int healAmount = 30;

    public int shiftCost = 75; // <-- YEN�: Shift fiyat�
    public int shiftAmount = 3; // <-- YEN�: Ka� shift verece�i

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
        if (GameManager.instance != null) GameManager.instance.RequestPause();
        shopPanel.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        UpdateGoldUI();
        LoadShopContent();
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);
        if (gameplayHUD != null) gameplayHUD.SetActive(false);
    }

    public void CloseShop()
    {
        if (gameplayHUD != null) gameplayHUD.SetActive(true);
        shopPanel.SetActive(false);
        if (GameManager.instance != null) GameManager.instance.ReleasePause();
        GameManager.instance.SetGameState(GameState.Playing);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.Confined;
        currentShopkeeper = null;
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);
    }

    private void UpdateGoldUI()
    {
        if (playerGoldText && GameManager.instance.player)
        {
            playerGoldText.text = "Gold: " + GameManager.instance.player.currentGold;
        }
    }

    // Frame on which the shop consumed the Escape press. PauseMenu checks this so a
    // single ESC can't both close the shop and open the pause menu (audit High 1.6).
    public static int escapeConsumedFrame = -1;

    private void Update()
    {
        if (shopPanel.activeSelf)
        {
            UpdateGoldUI();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                escapeConsumedFrame = Time.frameCount;
                CloseShop();
            }
        }
    }

    private void LoadShopContent()
    {
        // 1. �nce t�m slotlar� kapat (Temizlik)
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
            // Kartlar� Diz (Listenin boyutu kadar)
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
                    // Sat�n al�n�nca �al��acak kod:
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