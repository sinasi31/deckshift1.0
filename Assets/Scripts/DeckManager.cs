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

    // Reclaimer's Clamp salvages one exhausting card per room; reset on each new room.
    private bool clampUsedThisRoom = false;

    // The RuntimeCard currently mid-play — set around ExecuteAction in PlayCard so actions
    // that need a handle on their own card (Glass Parry's refund) can capture it. Only
    // valid during that call; null at all other times.
    public RuntimeCard CardBeingPlayed { get; private set; }

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
        // Blompo: "On the House" zeroes the cost. CardAimIndicator mirrors this in
        // EffectiveShiftCost — keep the two in sync or the affordability dimming lies.
        if (playedCard.enhancement == CardEnhancement.OnTheHouse)
            cost = 0;
        if (isNextCardFree)
        {
            cost = 0;
        }
        if (player.GetCurrentShift() < cost) return;
        if (!playedCard.isInfinite && playedCard.currentUses <= 0) return;

        // Blompo: "Extra Spicy" scales the action's damage value.
        float actionValue = data.actionValue;
        if (playedCard.enhancement == CardEnhancement.ExtraSpicy)
            actionValue *= CardEnhancements.EXTRA_SPICY_MULT;

        CardBeingPlayed = playedCard;
        bool success = player.ExecuteAction(data.actionType, actionValue, out bool keepInHand);
        CardBeingPlayed = null;

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
            // Blompo: "Kickback" refunds Shift on play. Gated by the hub rule like every other
            // resource change — the sandbox neither charges nor pays out.
            if (playedCard.enhancement == CardEnhancement.Kickback)
            {
                if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())
                    player.AddShift(CardEnhancements.KICKBACK_SHIFT);
            }

            // Blompo: "Double Dip" casts a second time. Only ever offered on cards that hold no
            // ConflictFlags (CardEnhancements.IsInstantCard), so unlike Echo Chamber below this
            // second cast can't be silently refused by TryExecute.
            if (playedCard.enhancement == CardEnhancement.DoubleDip)
                player.ExecuteAction(data.actionType, actionValue, out bool _);

            if (SkillManager.instance != null &&
                SkillManager.instance.HasSkill(SkillType.EchoChamber) &&
                UnityEngine.Random.value < 0.5f) // <--- BURASI DÜZELDÝ
            {
                Debug.Log("ECHO CHAMBER: Çift Etki!");
                // Ýkinci kez çalýþtýr
                player.ExecuteAction(data.actionType, actionValue, out bool _);
            }
            bool inHub = LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub();
            if (!playedCard.isInfinite && !inHub) playedCard.currentUses--;

            if (inHub || (playedCard.isInfinite || playedCard.currentUses > 0) && (!data.singleUse || playedCard.isInfinite))
            {
                discardPile.Add(playedCard);
            }
            else if (!clampUsedThisRoom && RelicManager.instance != null
                     && RelicManager.instance.HasRelic("ReclaimersClamp"))
            {
                // Reclaimer's Clamp: the first card that would exhaust each room is salvaged —
                // it returns to hand with a single charge instead of going to the exhaust pile.
                clampUsedThisRoom = true;
                playedCard.currentUses = 1;
                hand.Add(playedCard);
            }
            else
            {
                exhaustPile.Add(playedCard);

                // A card burning out leaves scrap behind — a small consolation so losing a card
                // isn't a total loss, deliberately far below what it costs to salvage one back
                // (see ScrapEconomy). No hub guard needed: charges don't decrement in the hub,
                // so this branch is unreachable there.
                if (player != null) player.AddScrap(ScrapEconomy.EXHAUST_REBATE);
            }
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
        // (null guard: a recompile during Play mode resets the singleton's static instance.)
        if (GameManager.instance != null && GameManager.instance.currentState == GameState.Playing)
        {
            CheckForStaggerCondition();
        }
    }
    // Lets systems that mutate cards IN PLACE ask the hand UI to redraw. Blompo needs this: a
    // blessing changes an existing RuntimeCard without adding/removing/playing anything, so no
    // normal hand event fires and the new badge would not appear until the next redraw.
    // OnHandChanged is an event, so only DeckManager can raise it — hence this wrapper.
    public void RefreshHandUI()
    {
        OnHandChanged?.Invoke(false);
    }

    public void ResetRecallCost()
    {
        currentRecallCost = baseRecallCost;
        OnRecallCostChanged?.Invoke(currentRecallCost);
    }

    // Clears per-room relic state (currently Reclaimer's Clamp's once-per-room salvage).
    public void ResetRoomRelicState()
    {
        clampUsedThisRoom = false;
    }

    // Glass Parry's mastery refund: gives one charge back to a card that was already
    // played this frame. If spending that charge exhausted the card, pull it back out
    // of the exhaust pile — perfect play means the card never really left.
    public void RefundCharge(RuntimeCard card)
    {
        if (card == null) return;
        if (!card.isInfinite)
            card.currentUses = Mathf.Min(card.currentUses + 1, card.cardData.maxUses);
        if (exhaustPile.Remove(card))
            discardPile.Add(card);
        OnHandChanged?.Invoke(false);
    }

    // --- Scrap forge operations ----------------------------------------------------------------
    // Both are all-or-nothing: the scrap is only spent if the operation actually applies, so a
    // failed call leaves the player's wallet and the piles untouched. The UI (ScrapForgeScreen)
    // gates on the same conditions, but these re-check independently — never trust the caller.

    // Tops a card the player still owns back up to full charges.
    public bool TryRechargeCard(RuntimeCard card)
    {
        if (card == null || player == null) return false;
        if (exhaustPile.Contains(card)) return false;   // must be Salvaged first, not recharged

        int missing = ScrapEconomy.MissingCharges(card);
        if (missing <= 0) return false;                 // already full, or infinite

        int cost = ScrapEconomy.RechargeCost(card);
        if (!player.TrySpendScrap(cost)) return false;

        card.currentUses = card.cardData.maxUses;
        OnHandChanged?.Invoke(false);
        return true;
    }

    // Pulls a card back out of the exhaust pile. It returns to the DISCARD pile (not the hand) so
    // it re-enters the deck through the normal shuffle, and comes back only half charged — exhaust
    // is meant to stay a real loss, so a full recovery costs the salvage plus a recharge on top.
    public bool TrySalvageCard(RuntimeCard card)
    {
        if (card == null || player == null) return false;
        if (!exhaustPile.Contains(card)) return false;

        if (!player.TrySpendScrap(ScrapEconomy.SALVAGE_COST)) return false;

        exhaustPile.Remove(card);
        card.currentUses = ScrapEconomy.SalvageCharges(card);
        card.isSelected = false;
        discardPile.Add(card);
        OnHandChanged?.Invoke(false);
        return true;
    }

    // Called by LevelManager.SpawnNextRoom at the moment a COMBAT room ends, while the
    // ending room's hand still exists (the reload that discards it comes right after).
    // Held payoff cards trigger here — Dead Weight: +actionValue Shift per copy still
    // in hand. Recalling earlier discarded it and forfeited this.
    public void OnRoomEnd()
    {
        foreach (RuntimeCard card in hand)
        {
            if (card.cardData != null && card.cardData.actionType == CardActionType.DeadWeight)
            {
                int payout = Mathf.RoundToInt(card.cardData.actionValue);
                player.AddShift(payout);
                Debug.Log($"DEAD WEIGHT held to room end: +{payout} Shift.");
            }
        }
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

        bool inHub = LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub();
        bool overclocked = RelicManager.instance != null && RelicManager.instance.HasRelic("OverclockedRecall");

        if (overclocked)
        {
            // Overclocked Recall: no Shift cost — paid in blood instead (5 HP per Recall,
            // never in the sandbox hub). Cost escalation is irrelevant when Shift is free.
            if (!inHub) player.TakeDamage(5);
        }
        else
        {
            // 2. Maliyet kontrolü
            if (player.GetCurrentShift() < currentRecallCost)
            {
                Debug.Log("Yetersiz Shift! Recall yapılamıyor.");
                // Buraya "Yetersiz Enerji" sesi veya görseli eklenebilir
                return;
            }

            // 3. Shift Harca + 4. Maliyeti Artır (Level bitene kadar)
            if (!inHub)
            {
                player.SpendShift(currentRecallCost);
                currentRecallCost++;
                OnRecallCostChanged?.Invoke(currentRecallCost);
            }
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

        // Blompo: "Clingy" cards are held back instead of being discarded, so they survive the
        // Recall and are still in hand afterwards. They occupy their slot, so a hand full of
        // Clingy cards simply doesn't refresh — that's the intended trade-off.
        List<RuntimeCard> retained = new List<RuntimeCard>();
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] != null && hand[i].enhancement == CardEnhancement.Clingy)
                retained.Add(hand[i]);
            else
                discardPile.Add(hand[i]);
        }
        hand.Clear();
        hand.AddRange(retained);

        for (int i = hand.Count; i < handCapacity; i++)
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