using UnityEngine;
using System.Collections.Generic;

public class Shopkeeper : MonoBehaviour
{
    private bool playerInRange = false;
    public KeyCode interactKey = KeyCode.E;

    [Header("Görsel Referans")]
    public GameObject interactionPopup;

    [Header("Bu Dükkanýn Ýçeriði")]
    public List<CardData> specificCardPool;
    public List<RelicData> specificRelicPool;

    // Dükkanýn Hafýzasý
    public List<ShopSlotData> myInventory = new List<ShopSlotData>();

    private bool isInitialized = false;

    private void Start()
    {
        if (interactionPopup != null) interactionPopup.SetActive(false);

        if (!isInitialized)
        {
            GenerateShopContent();
            isInitialized = true;
        }
    }

    private void GenerateShopContent()
    {
        if (specificCardPool.Count == 0 && ShopManager.instance != null)
            specificCardPool = ShopManager.instance.allCardsPool;

        if (specificRelicPool.Count == 0 && ShopManager.instance != null)
            specificRelicPool = ShopManager.instance.allRelicsPool;

        // Kartlarý Oluþtur
        for (int i = 0; i < 5; i++)
        {
            if (specificCardPool != null && specificCardPool.Count > 0)
            {
                CardData card = specificCardPool[Random.Range(0, specificCardPool.Count)];

                ShopSlotData data = new ShopSlotData();
                data.itemType = ShopItemType.Card;
                data.cardReference = card;
                data.itemName = card.cardName;
                data.price = Random.Range(40, 70);
                data.isSold = false;

                myInventory.Add(data);
            }
        }

        // Relicleri Oluþtur
        for (int i = 0; i < 3; i++)
        {
            if (specificRelicPool != null && specificRelicPool.Count > 0)
            {
                RelicData relic = specificRelicPool[Random.Range(0, specificRelicPool.Count)];

                ShopSlotData data = new ShopSlotData();
                data.itemType = ShopItemType.Relic;
                data.relicReference = relic;
                data.itemName = relic.relicName;
                data.price = Random.Range(100, 150);
                data.isSold = false;

                myInventory.Add(data);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (interactionPopup != null) interactionPopup.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (interactionPopup != null) interactionPopup.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            if (interactionPopup != null) interactionPopup.SetActive(false);

            if (ShopManager.instance != null)
            {
                ShopManager.instance.OpenShop(this);
            }
        }
    }
}

public enum ShopItemType
{
    Card,
    Relic,
    Service
}

[System.Serializable]
public class ShopSlotData
{
    public string itemName;
    public int price;
    public bool isSold;

    public ShopItemType itemType;
    public CardData cardReference;
    public RelicData relicReference;
}