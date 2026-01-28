using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;

public class DeckManager : MonoBehaviour
{
    public static DeckManager instance;

    // DEÐÝÞÝKLÝK: Event artýk bir 'bool' taþýyor.
    // true = Animasyonlu yenile (Start, Reload)
    // false = Hýzlý yenile (Kart oynama)
    public static event Action<bool> OnHandChanged;

    [Header("Referanslar")]
    public PlayerController player;

    [Header("Deste Ayarlarý")]
    public List<CardData> startingDeck;

    private List<RuntimeCard> drawPile = new List<RuntimeCard>();
    private List<RuntimeCard> hand = new List<RuntimeCard>();
    private List<RuntimeCard> discardPile = new List<RuntimeCard>();
    private List<RuntimeCard> exhaustPile = new List<RuntimeCard>();

    public int handCapacity = 4;
    private int selectedIndex = -1;
    private bool isReloading = false;

    // Getterlar
    public List<RuntimeCard> GetDrawPile() { return drawPile; }
    public List<RuntimeCard> GetDiscardPile() { return discardPile; }
    public List<RuntimeCard> GetExhaustPile() { return exhaustPile; }
    public List<RuntimeCard> GetCurrentHand() { return hand; }
    public int GetSelectedIndex() { return selectedIndex; }

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        foreach (CardData data in startingDeck)
        {
            drawPile.Add(new RuntimeCard(data));
        }
        ShuffleDeck();
        ReloadHand(); // Baþlangýçta animasyon olsun
    }

    public void SelectCard(int index)
    {
        if (isReloading || index < 0 || index >= hand.Count) return;

        if (selectedIndex != -1 && selectedIndex < hand.Count)
            hand[selectedIndex].isSelected = false;

        selectedIndex = index;
        hand[selectedIndex].isSelected = true;

        // Seçim deðiþikliðinde animasyona gerek yok (false)
        OnHandChanged?.Invoke(false);
    }

    public void DeselectCard()
    {
        if (selectedIndex != -1 && selectedIndex < hand.Count)
            hand[selectedIndex].isSelected = false;

        selectedIndex = -1;

        // Seçim iptalinde animasyona gerek yok (false)
        OnHandChanged?.Invoke(false);
    }

    public void TryCastSelectedCard()
    {
        if (isReloading || selectedIndex == -1) return;
        PlayCard(selectedIndex);
    }

    private void PlayCard(int index)
    {
        if (index >= hand.Count) return;

        RuntimeCard playedCard = hand[index];
        CardData data = playedCard.cardData;

        int cost = data.shiftCost;
        if (SkillManager.instance != null && SkillManager.instance.HasSkill(SkillType.KineticDiscount))
            cost = Mathf.Max(0, cost - 1);

        if (player.GetCurrentShift() < cost) return;
        if (!playedCard.isInfinite && playedCard.currentUses <= 0) return;

        if (data.actionType != CardActionType.Portal) player.SpendShift(cost);

        bool success = player.ExecuteAction(data.actionType, data.actionValue, out bool keepInHand);

        if (success && !keepInHand)
        {
            hand.RemoveAt(index);
            selectedIndex = -1;

            if (!playedCard.isInfinite) playedCard.currentUses--;

            if ((playedCard.isInfinite || playedCard.currentUses > 0) && (!data.singleUse || playedCard.isInfinite))
                discardPile.Add(playedCard);
            else
                exhaustPile.Add(playedCard);

            // KART OYNANDI: Animasyon ÝSTEMÝYORUZ (false)
            // Sadece eldeki boþluðu kapatmak için hýzlý güncelleme.
            OnHandChanged?.Invoke(false);
        }
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
        yield return new WaitForSeconds(0.2f);

        discardPile.AddRange(hand);
        hand.Clear();

        for (int i = 0; i < handCapacity; i++)
        {
            if (drawPile.Count == 0 && discardPile.Count > 0)
            {
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                ShuffleDeck();
            }

            if (drawPile.Count > 0)
            {
                RuntimeCard c = drawPile[0];
                drawPile.RemoveAt(0);
                c.isSelected = false;
                hand.Add(c);
            }
        }

        // EL YENÝLENDÝ: Animasyon ÝSTÝYORUZ (true)
        OnHandChanged?.Invoke(true);
        isReloading = false;
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

        RuntimeCard c = drawPile[0];
        drawPile.RemoveAt(0);
        c.isSelected = false;
        hand.Add(c);

        // Tek kart çekme: Ýsteðe baðlý. Þimdilik animasyonsuz olsun ki hýzlý aksýn.
        // Ýstersen bunu 'true' yapabilirsin.
        OnHandChanged?.Invoke(false);
    }

    public void AddCardToDeck(CardData newCardData)
    {
        RuntimeCard newCardInstance = new RuntimeCard(newCardData);
        discardPile.Add(newCardInstance);
    }

    private void ShuffleDeck()
    {
        for (int i = 0; i < drawPile.Count; i++)
        {
            RuntimeCard temp = drawPile[i];
            int rnd = UnityEngine.Random.Range(i, drawPile.Count);
            drawPile[i] = drawPile[rnd];
            drawPile[rnd] = temp;
        }
    }
}