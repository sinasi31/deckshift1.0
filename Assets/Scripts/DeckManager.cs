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
        if (Input.GetKeyDown(KeyCode.Alpha2)) PlayCard(0);
        if (Input.GetKeyDown(KeyCode.Alpha3)) PlayCard(1);
        if (Input.GetKeyDown(KeyCode.Alpha4)) PlayCard(2);
        if (Input.GetKeyDown(KeyCode.Alpha5)) PlayCard(3);
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

    private void PlayCard(int handIndex)
    {
        if (handIndex >= hand.Count) return;

        RuntimeCard playedCard = hand[handIndex];
        CardData cardTemplate = playedCard.cardData;

        // 1. Shift Maliyet Kontrolü
        if (player.GetCurrentShift() < cardTemplate.shiftCost)
        {
            Debug.LogWarning($"Yeterli SHIFT yok!");
            return;
        }
        // 2. Kullaným Hakký Kontrolü
        if (playedCard.currentUses <= 0)
        {
            Debug.LogWarning($"Kullaným hakký bitmiþ.");
            return;
        }

        // Shift'i burada harcamýyoruz, PlayerController içinde manuel harcananlar olabilir (Portal gibi)
        // Ama standart kartlar için otomatik harcama:
        if (cardTemplate.actionType != CardActionType.Portal) // Portal hariç
        {
            player.SpendShift(cardTemplate.shiftCost);
        }

        // --- DEÐÝÞÝKLÝK BURADA ---
        bool keepInHand; // PlayerController'dan gelecek cevap için deðiþken
        bool success = player.ExecuteAction(cardTemplate.actionType, cardTemplate.actionValue, out keepInHand);

        if (success)
        {
            // Eðer kartýn elde kalmasý gerekiyorsa (Ýlk portalý koyduysak)
            if (keepInHand)
            {
                Debug.Log("Kart baþarýyla kullanýldý ama elde tutuluyor (Çok aþamalý kart).");
                // Kartý silmiyoruz, kullaným hakkýný düþürmüyoruz.
                // Sadece belki bir ses veya görsel efekt olabilir.
                return;
            }

            // --- Buradan aþaðýsý, kartýn iþlemi tamamen bittiðinde çalýþýr ---

            // Kullaným hakkýný düþür
            playedCard.currentUses--;
            Debug.Log($"{cardTemplate.cardName} oynandý. Kalan kullaným: {playedCard.currentUses}");

            hand.RemoveAt(handIndex);

            if (playedCard.currentUses > 0)
            {
                if (!cardTemplate.singleUse)
                    discardPile.Add(playedCard);
            }
            else
            {
                Debug.Log($"{cardTemplate.cardName} kullaným hakký bitti, desteden silindi.");
            }

            DrawCard();
            OnHandChanged?.Invoke();
        }
    }

    // --- DEÐÝÞEN FONKSÝYON: ShuffleDeck() ---
    private void ShuffleDeck()
    {
        for (int i = 0; i < drawPile.Count; i++)
        {
            RuntimeCard temp = drawPile[i]; // 'RuntimeCard' olmalý
            int randomIndex = UnityEngine.Random.Range(i, drawPile.Count);
            drawPile[i] = drawPile[randomIndex];
            drawPile[randomIndex] = temp;
        }
    }
    // --- BÝTÝÞ ---

    // --- DEÐÝÞEN FONKSÝYON: AddCardToDeck() ---
    public void AddCardToDeck(CardData newCardData)
    {
        RuntimeCard newCardInstance = new RuntimeCard(newCardData);
        discardPile.Add(newCardInstance); // 'discardPile' List<RuntimeCard> olmalý
    }
    // --- BÝTÝÞ ---
}