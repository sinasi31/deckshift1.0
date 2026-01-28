using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;

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

    private int selectedIndex = -1;
    private bool isReloading = false;

    public List<RuntimeCard> GetCurrentHand()
    {
        return hand;
    }

    public int GetSelectedIndex()
    {
        return selectedIndex;
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
        ReloadHand();
    }

    // Inputlarý PlayerController'a taþýdýðýmýz için Update temizlendi

    public void SelectCard(int index)
    {
        if (isReloading) return;
        if (index < 0 || index >= hand.Count) return;

        if (selectedIndex != -1 && selectedIndex < hand.Count)
        {
            hand[selectedIndex].isSelected = false;
        }

        selectedIndex = index;
        hand[selectedIndex].isSelected = true;

        // UI'ýn seçimi göstermesi için event tetikleyebiliriz
        OnHandChanged?.Invoke();
    }

    public void DeselectCard()
    {
        if (selectedIndex != -1 && selectedIndex < hand.Count)
        {
            hand[selectedIndex].isSelected = false;
        }
        selectedIndex = -1;
        OnHandChanged?.Invoke();
    }

    public void TryCastSelectedCard()
    {
        if (isReloading || selectedIndex == -1) return;
        PlayCard(selectedIndex);
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
        drawnCard.isSelected = false;

        hand.Add(drawnCard);
    }

    public void ReloadHand()
    {
        if (isReloading) return;
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        DeselectCard();

        // Buraya "Shuffling..." sesi veya efekti eklenebilir
        yield return new WaitForSeconds(0.5f);

        // Mevcut eli ýskartaya at
        if (hand.Count > 0)
        {
            discardPile.AddRange(hand);
            hand.Clear();
        }

        // Yeni el çek
        for (int i = 0; i < handCapacity; i++)
        {
            DrawCard();
        }

        OnHandChanged?.Invoke();
        isReloading = false;
    }

    private void PlayCard(int handIndex)
    {
        if (handIndex >= hand.Count) return;

        RuntimeCard playedCard = hand[handIndex];
        CardData cardTemplate = playedCard.cardData;

        int finalCost = cardTemplate.shiftCost;
        if (SkillManager.instance != null && SkillManager.instance.HasSkill(SkillType.KineticDiscount))
        {
            finalCost = Mathf.Max(0, finalCost - 1);
        }

        if (player.GetCurrentShift() < finalCost)
        {
            Debug.LogWarning($"Yeterli SHIFT yok! Gerekli: {finalCost}");
            return;
        }

        if (!playedCard.isInfinite && playedCard.currentUses <= 0)
        {
            Debug.LogWarning($"Kartýn kullaným hakký bitmiþ: {cardTemplate.cardName}");
            return;
        }

        if (cardTemplate.actionType != CardActionType.Portal)
        {
            player.SpendShift(finalCost);
        }

        bool success = player.ExecuteAction(cardTemplate.actionType, cardTemplate.actionValue, out bool keepInHand);

        if (success)
        {
            if (keepInHand) return;

            HandUI ui = FindFirstObjectByType<HandUI>();
            if (ui != null)
            {
                ui.AnimateCardFromHand(handIndex);
            }

            if (!playedCard.isInfinite)
            {
                playedCard.currentUses--;
            }

            hand.RemoveAt(handIndex);

            // Seçimi sýfýrla çünkü liste kaydý
            selectedIndex = -1;

            if (playedCard.isInfinite || playedCard.currentUses > 0)
            {
                if (!cardTemplate.singleUse || playedCard.isInfinite)
                    discardPile.Add(playedCard);
            }

            // DÝKKAT: DrawCard() ÇAÐIRMIYORUZ. Slot boþ kalmalý.
            OnHandChanged?.Invoke();
        }
    }

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
    public List<RuntimeCard> GetDrawPile()
    {
        return drawPile;
    }

    public List<RuntimeCard> GetDiscardPile()
    {
        return discardPile;
    }
}