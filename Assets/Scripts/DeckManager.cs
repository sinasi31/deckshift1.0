using System.Collections.Generic;
using UnityEngine;
using System;

public class DeckManager : MonoBehaviour
{
    public static DeckManager instance;
    public static event Action OnHandChanged;

    [Header("References")]
    public PlayerController player;

    [Header("Deck Settings")]
    public List<CardData> startingDeck;
    private List<RuntimeCard> drawPile = new List<RuntimeCard>();
    private List<RuntimeCard> hand = new List<RuntimeCard>();
    private List<RuntimeCard> discardPile = new List<RuntimeCard>();
    public int handCapacity = 4;

    public List<RuntimeCard> GetCurrentHand()
    {
        return hand;
    }

    private void Awake()
    {
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        foreach (CardData data in startingDeck)
        {
            RuntimeCard newCardInstance = new RuntimeCard(data);
            drawPile.Add(newCardInstance);
        }
        ShuffleDeck();
        for (int i = 0; i < handCapacity; i++)
        {
            DrawCard();
        }
        OnHandChanged?.Invoke();
    }

    void Update()
    {
        if (GameManager.instance.currentState != GameState.Playing) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) PlayCard(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) PlayCard(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) PlayCard(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) PlayCard(3);
    }

    public void DrawCard()
    {
        if (hand.Count >= handCapacity) return;
        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0) return;
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            ShuffleDeck();
        }

        RuntimeCard drawnCard = drawPile[0];
        drawPile.RemoveAt(0);
        hand.Add(drawnCard);
    }

    // --- DÜZELTÝLMÝÞ VE SKILL ENTEGRELÝ PLAYCARD FONKSÝYONU ---
    private void PlayCard(int handIndex)
    {
        if (handIndex >= hand.Count) return;

        // Deðiþkenleri SADECE BURADA tanýmlýyoruz
        RuntimeCard playedCard = hand[handIndex];
        CardData cardTemplate = playedCard.cardData;

        // --- SKILL KONTROLÜ: Kinetic Discount ---
        int finalCost = cardTemplate.shiftCost;
        if (SkillManager.instance != null && SkillManager.instance.HasSkill(SkillType.KineticDiscount))
        {
            finalCost = Mathf.Max(0, finalCost - 1);
        }
        // ----------------------------------------

        // 1. Shift Maliyet Kontrolü
        if (player.GetCurrentShift() < finalCost)
        {
            Debug.LogWarning($"Yeterli SHIFT yok! Gerekli: {finalCost}");
            return;
        }

        // 2. Kullaným Hakký Kontrolü (Sonsuz deðilse)
        if (!playedCard.isInfinite && playedCard.currentUses <= 0)
        {
            Debug.LogWarning($"Kartýn kullaným hakký bitmiþ: {cardTemplate.cardName}");
            return;
        }

        // --- Kartý Oyna ---
        // Portal gibi özel kartlar shift'i kendi içinde harcayabilir, 
        // standart kartlar için burada harcýyoruz.
        if (cardTemplate.actionType != CardActionType.Portal)
        {
            player.SpendShift(finalCost);
        }

        bool success = player.ExecuteAction(cardTemplate.actionType, cardTemplate.actionValue, out bool keepInHand);

        if (success)
        {
            // Eðer kart (Portal'ýn ilk aþamasý gibi) elde kalmalýysa çýk.
            if (keepInHand) return;
            // --- BURAYI YAPIÞTIR (DÜZELTÝLMÝÞ HALÝ) ---

            // Sahnede HandUI scriptini bul ve çalýþtýr
            HandUI ui = FindFirstObjectByType<HandUI>();
            if (ui != null)
            {
                ui.AnimateCardFromHand(handIndex);
            }
            // ------------------------------------------
            // --- KULLANIM HAKKI DÜÞME ---
            if (!playedCard.isInfinite)
            {
                playedCard.currentUses--;
                Debug.Log($"{cardTemplate.cardName} oynandý. Kalan: {playedCard.currentUses}");
            }
            else
            {
                Debug.Log($"{cardTemplate.cardName} (SONSUZ) oynandý.");
            }
            // ----------------------------

            hand.RemoveAt(handIndex);

            // Kartý Mezarlýða Gönder (Sonsuzsa veya hakký varsa)
            if (playedCard.isInfinite || playedCard.currentUses > 0)
            {
                if (!cardTemplate.singleUse || playedCard.isInfinite)
                    discardPile.Add(playedCard);
            }
            else
            {
                Debug.Log($"{cardTemplate.cardName} bitti, silindi.");
            }

            DrawCard();
            OnHandChanged?.Invoke();
        }
    }
    // -----------------------------------------------------------

    private void ShuffleDeck()
    {
        for (int i = 0; i < drawPile.Count; i++)
        {
            RuntimeCard temp = drawPile[i];
            int randomIndex = UnityEngine.Random.Range(i, drawPile.Count);
            drawPile[i] = drawPile[randomIndex];
            drawPile[randomIndex] = temp;
        }
    }

    public void AddCardToDeck(CardData newCardData)
    {
        RuntimeCard newCardInstance = new RuntimeCard(newCardData);
        discardPile.Add(newCardInstance);
    }
    public void RefillHand()
    {
        while (hand.Count < handCapacity)
        {
            if (drawPile.Count == 0 && discardPile.Count == 0)
            {
                break;
            }

            DrawCard();
        }
        OnHandChanged?.Invoke();
    }
}