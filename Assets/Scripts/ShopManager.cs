using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShopManager : MonoBehaviour
{
    public static ShopManager instance;

    [Header("UI Panel")]
    public GameObject shopPanel;
    public TextMeshProUGUI playerGoldText; // "Gold: 150" yazýsý

    [Header("Satýlacak Ürün Havuzlarý")]
    public List<CardData> allCardsPool;
    public List<RelicData> allRelicsPool;
    public Sprite healIcon; // Can doldurma ikonu

    [Header("Dükkan Raflarý (UI Slotlarý)")]
    // Unity Inspector'da bu listelere sahnede yarattýðýn ShopItemUI objelerini sürükle
    public List<ShopItemUI> cardSlots;
    public List<ShopItemUI> relicSlots;
    public ShopItemUI serviceSlot; // Genelde 1 tane heal/upgrade slotu olur

    [Header("Fiyatlandýrma")]
    public int cardPriceMin = 40;
    public int cardPriceMax = 70;
    public int relicPriceMin = 100;
    public int relicPriceMax = 150;
    public int healCost = 50;
    public int healAmount = 30;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void Start()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    public void OpenShop()
    {
        GameManager.instance.SetGameState(GameState.Paused);
        Time.timeScale = 0f; // Oyunu durdur
        shopPanel.SetActive(true);

        UpdateGoldUI();
        PopulateShop();
    }

    public void CloseShop()
    {
        shopPanel.SetActive(false);
        Time.timeScale = 1f; // Oyunu devam ettir
        GameManager.instance.SetGameState(GameState.Playing);
    }

    private void UpdateGoldUI()
    {
        if (playerGoldText && GameManager.instance.player)
        {
            playerGoldText.text = "Gold: " + GameManager.instance.player.currentGold;
        }
    }

    // Satýn alým yaptýkça parayý güncellemek için Update'de veya event ile çaðýrabilirsin
    private void Update()
    {
        if (shopPanel.activeSelf) UpdateGoldUI();
    }

    private void PopulateShop()
    {
        // 1. Kartlarý Doldur (Rastgele)
        foreach (var slot in cardSlots)
        {
            if (allCardsPool.Count > 0)
            {
                CardData randomCard = allCardsPool[Random.Range(0, allCardsPool.Count)];
                int price = Random.Range(cardPriceMin, cardPriceMax);
                // Nadirliðe göre fiyat artýrabilirsin (Opsiyonel)
                // if(randomCard.rarity == Rarity.Rare) price += 50;

                slot.gameObject.SetActive(true);
                slot.SetupCard(randomCard, price);
            }
            else
            {
                slot.gameObject.SetActive(false);
            }
        }

        // 2. Relicleri Doldur
        foreach (var slot in relicSlots)
        {
            if (allRelicsPool.Count > 0)
            {
                RelicData randomRelic = allRelicsPool[Random.Range(0, allRelicsPool.Count)];
                int price = Random.Range(relicPriceMin, relicPriceMax);

                slot.gameObject.SetActive(true);
                slot.SetupRelic(randomRelic, price);
            }
            else
            {
                slot.gameObject.SetActive(false);
            }
        }

        // 3. Servis (Can Doldurma)
        if (serviceSlot != null)
        {
            serviceSlot.SetupService(
                "Repair Kit",
                healIcon,
                healCost,
                $"Heal {healAmount} HP",
                () => {
                    // Satýn alýnýnca çalýþacak kod:
                    GameManager.instance.player.Heal(healAmount);
                }
            );
        }
    }
}