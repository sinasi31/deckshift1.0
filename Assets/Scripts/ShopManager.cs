using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    public static ShopManager instance;

    // Fallback pools a Shopkeeper draws from when it has no specific pool assigned.
    [Header("Card / Relic Pools")]
    public List<CardData> allCardsPool;
    public List<RelicData> allRelicsPool;

    // Service prices/amounts — read by ShopScreenUI when it builds the service tiles.
    [Header("Service Prices")]
    public int healCost = 50;
    public int healAmount = 30;
    public int shiftCost = 75;
    public int shiftAmount = 3;

    // The shopkeeper whose stall is currently open (null when closed).
    private Shopkeeper currentShopkeeper;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    // Opens the code-built merchant-stall shop (ShopScreenUI). The old scene panel + slot
    // wiring is no longer used — ShopScreenUI owns all presentation, pause, HUD, and buying.
    // ShopManager stays the entry point Shopkeeper calls, and the config source for services.
    public void OpenShop(Shopkeeper shop)
    {
        currentShopkeeper = shop;
        ShopScreenUI.Open(shop);
    }

    public void CloseShop()
    {
        ShopScreenUI.Close();
        currentShopkeeper = null;
    }

    // Frame on which the shop consumed the Escape press. PauseMenu checks this so a
    // single ESC can't both close the shop and open the pause menu (audit High 1.6).
    // ShopScreenUI sets this when it closes on Escape.
    public static int escapeConsumedFrame = -1;
}