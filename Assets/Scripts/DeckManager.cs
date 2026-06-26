using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;

public class DeckManager : MonoBehaviour
{
    public static DeckManager instance;
    public static event Action<bool> OnHandChanged;
    public static event Action<int> OnCardPlayed;
    public bool isNextCardFree = false;
    [Header("Recall Settings")]
    public int baseRecallCost = 1; // Başlangıç maliyeti
    public int currentRecallCost;  // Şu anki maliyet

    public static System.Action<int> OnRecallCostChanged;

    [Header("Referanslar")]
    public PlayerController player;

    [Header("Deste Ayarlarý")]
    public List<CardData> startingDeck;

    [Header("Special Cards")]
    public CardData staggerCardData;

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
        ResetRecallCost();
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
        if (isNextCardFree)
        {
            cost = 0;
        }
        if (player.GetCurrentShift() < cost) return;
        if (!playedCard.isInfinite && playedCard.currentUses <= 0) return;

        bool success = player.ExecuteAction(data.actionType, data.actionValue, out bool keepInHand);

        // Shift is deducted only when the action actually executed — Blocked plays
        // (conflict refusal) and Failed plays (e.g. Comet Dive while grounded) cost
        // nothing. The affordability check above still gates execution up front.
        // Portal stays exempt: TryPlacePortal spends its own cost on second placement.
        if (success && data.actionType != CardActionType.Portal)
        {
            if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())
                player.SpendShift(cost);
        }

        if (success && !keepInHand)
        {
            OnCardPlayed?.Invoke(index);
            player.FlashCardPlay();

            hand.RemoveAt(index);
            selectedIndex = -1;
            if (isNextCardFree)
            {
                isNextCardFree = false;
            }
            if (SkillManager.instance != null &&
                SkillManager.instance.HasSkill(SkillType.EchoChamber) &&
                UnityEngine.Random.value < 0.5f) // <--- BURASI DÜZELDÝ
            {
                Debug.Log("ECHO CHAMBER: Çift Etki!");
                // Ýkinci kez çalýþtýr
                player.ExecuteAction(data.actionType, data.actionValue, out bool _);
            }
            bool inHub = LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub();
            if (!playedCard.isInfinite && !inHub) playedCard.currentUses--;

            if (inHub || (playedCard.isInfinite || playedCard.currentUses > 0) && (!data.singleUse || playedCard.isInfinite))
                discardPile.Add(playedCard);
            else
                exhaustPile.Add(playedCard);
            OnHandChanged?.Invoke(false);
        }
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            TryRecall();
        }
        // Her karede kontrol etmek yerine sadece oyun akarken bak
        if (GameManager.instance.currentState == GameState.Playing)
        {
            CheckForStaggerCondition();
        }
    }
    public void ResetRecallCost()
    {
        currentRecallCost = baseRecallCost;
        OnRecallCostChanged?.Invoke(currentRecallCost);
    }
    private void CheckForStaggerCondition()
    {
        if (LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub()) return;

        // 1. Shift var mý?
        if (player.GetCurrentShift() > 0) return; // Shift varsa sorun yok

        // 2. Eldeki kartlarýn Charge'ý var mý?
        foreach (RuntimeCard card in hand)
        {
            // Stagger kartýnýn kendisi hariç, kullanýlabilir kart var mý?
            if (card.cardData != staggerCardData)
            {
                if (card.isInfinite || card.currentUses > 0) return; // Kullanýlacak kart var
            }
        }

        // BURAYA GELDÝYSEK HÝÇBÝR KAYNAK YOK DEMEKTÝR!
        // Eline zaten Stagger kartý verdiysek tekrar verme
        foreach (RuntimeCard card in hand)
        {
            if (card.cardData == staggerCardData) return;
        }

        Debug.Log("KAYNAKLAR TÜKENDÝ! STAGGER KARTI VERÝLÝYOR...");
        AddStaggerCardToHand();
    }

    private void AddStaggerCardToHand()
    {
        // Eli temizle (veya doluysa yer aç)
        // Senin oyununda el kapasitesi dolunca ne oluyor? 
        // Acil durum olduðu için eldeki boþ bir yere veya direkt sona ekleyelim.

        RuntimeCard staggerInstance = new RuntimeCard(staggerCardData);
        hand.Add(staggerInstance);

        // UI Güncelle
        OnHandChanged?.Invoke(true);
    }
    public void TryRecall()
    {
        // 1. Zaten el yenileniyorsa dur
        if (isReloading) return;

        // 2. Maliyet kontrolü
        if (player.GetCurrentShift() < currentRecallCost)
        {
            Debug.Log("Yetersiz Shift! Recall yapılamıyor.");
            // Buraya "Yetersiz Enerji" sesi veya görseli eklenebilir
            return;
        }

        // 3. Shift Harca
        if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())
            player.SpendShift(currentRecallCost);

        // 4. Maliyeti Artır (Level bitene kadar)
        if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())
        {
            currentRecallCost++;
            OnRecallCostChanged?.Invoke(currentRecallCost);
        }

        // 5. Asıl işlemi başlat
        ReloadHand();
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