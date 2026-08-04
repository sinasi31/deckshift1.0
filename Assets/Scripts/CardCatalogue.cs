using System.Collections.Generic;
using UnityEngine;

// Every CardData in the project, in one asset, so nothing has to maintain a card list by hand.
// Exactly the same fix as RelicCatalogue, for exactly the same failure.
//
// WHY: three cards — DeadWeight, FreefallBlade and GlassParry — existed and were IMPOSSIBLE to
// obtain. They were not in AchievementManager.defaultUnlockedCards, not attached to a completed
// challenge, and not in ShopManager.allCardsPool, and those three hand-kept lists were the only
// ways a card could reach the player. Nothing errored; the cards just silently were not in the
// game.
//
// Rebuilt automatically whenever a CardData asset changes (Editor/CardCatalogueBuilder), so
// authoring a card is all it takes to put it in the run.
public class CardCatalogue : ScriptableObject
{
    public const string ResourcePath = "CardCatalogue";

    [Tooltip("Auto-generated. Rebuilt by Deckshift → Rebuild Card Catalogue and on any CardData " +
             "asset change. Editing this by hand is pointless; it will be overwritten.")]
    public List<CardData> all = new List<CardData>();
}
