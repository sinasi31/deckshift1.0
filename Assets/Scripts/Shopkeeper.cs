using UnityEngine;
using System.Collections.Generic;

public class Shopkeeper : MonoBehaviour
{
    private bool playerInRange = false;
    public KeyCode interactKey = KeyCode.E;

    [Header("G�rsel Referans")]
    public GameObject interactionPopup;

    [Header("Bu D�kkan�n ��eri�i")]
    public List<CardData> specificCardPool;
    public List<RelicData> specificRelicPool;

    // D�kkan�n Haf�zas�
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
        if ((specificCardPool == null || specificCardPool.Count == 0) && ShopManager.instance != null)
            specificCardPool = ShopManager.instance.allCardsPool;

        if ((specificRelicPool == null || specificRelicPool.Count == 0) && ShopManager.instance != null)
            specificRelicPool = ShopManager.instance.allRelicsPool;

        // Cards — up to 5 DISTINCT offers (draw without replacement so no duplicates show).
        if (specificCardPool != null)
        {
            List<CardData> pool = new List<CardData>(specificCardPool);
            int n = Mathf.Min(5, pool.Count);
            for (int i = 0; i < n; i++)
            {
                int idx = Random.Range(0, pool.Count);
                CardData card = pool[idx];
                pool.RemoveAt(idx);
                myInventory.Add(new ShopSlotData
                {
                    itemType = ShopItemType.Card, cardReference = card, itemName = card.cardName,
                    price = Random.Range(40, 70), isSold = false
                });
            }
        }

        // Relics — up to 3 DISTINCT offers.
        if (specificRelicPool != null)
        {
            List<RelicData> pool = new List<RelicData>(specificRelicPool);
            int n = Mathf.Min(3, pool.Count);
            for (int i = 0; i < n; i++)
            {
                int idx = Random.Range(0, pool.Count);
                RelicData relic = pool[idx];
                pool.RemoveAt(idx);
                myInventory.Add(new ShopSlotData
                {
                    itemType = ShopItemType.Relic, relicReference = relic, itemName = relic.relicName,
                    price = Random.Range(100, 150), isSold = false
                });
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