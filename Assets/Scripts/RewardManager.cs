using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardManager : MonoBehaviour
{
    public static RewardManager instance;

    [Header("References")]
    public GameObject rewardScreen;

    // --- DEÐÝÞÝKLÝK: Artýk Button listesi deðil, CardUI listesi tutuyoruz ---
    public List<CardUI> rewardCardSlots;
    // --- BÝTÝÞ ---

    private List<CardData> offeredCards = new List<CardData>();
    private int bonusCardIndex = -1;

    private void Awake()
    {
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        if (rewardScreen != null) rewardScreen.SetActive(false);
    }

    public void ShowRewardScreen()
    {
        offeredCards.Clear();
        bonusCardIndex = -1;

        List<CardData> cardPool = AchievementManager.instance.GetAvailableCardPool();
        GameManager.instance.SetGameState(GameState.Paused);

        // 3 Kart Seç
        for (int i = 0; i < 3; i++)
        {
            if (cardPool.Count == 0) break;
            int randomIndex = Random.Range(0, cardPool.Count);
            offeredCards.Add(cardPool[randomIndex]);
            cardPool.RemoveAt(randomIndex);
        }

        // Bonus þansý
        if (offeredCards.Count > 0)
            bonusCardIndex = Random.Range(0, offeredCards.Count);

        // Kartlarý UI'a yerleþtir
        for (int i = 0; i < rewardCardSlots.Count; i++)
        {
            if (i < offeredCards.Count)
            {
                CardUI slot = rewardCardSlots[i];
                slot.gameObject.SetActive(true);

                CardData data = offeredCards[i];

                // --- GÖRSEL KURULUM (En Önemli Kýsým) ---
                // CardUI'ýn kendi Setup fonksiyonunu kullanýyoruz!
                // Görsel olmasý için geçici bir RuntimeCard oluþturuyoruz.
                RuntimeCard visualCard = new RuntimeCard(data);

                // Bonus varsa açýklamasýný güncelle (Sadece görsel için)
                if (i == bonusCardIndex)
                {
                    // Not: Bu kalýcý veriyi deðiþtirmez, sadece visualCard'ý etkiler
                    // (CardUI scriptinde descriptionText'i description'dan aldýðýmýzý varsayarsak)
                    // Ancak CardData ScriptableObject olduðu için açýklamayý kodla deðiþtirmek riskli olabilir.
                    // Þimdilik bonusu göstermek için basit bir yöntem:
                    Debug.Log($"Kart {i} BONUSLU (+1 Shift)");
                }

                // Setup'ý çaðýr (Bu; resmi, frame'i, daireleri her þeyi ayarlar!)
                slot.Setup(visualCard, i + 1);

                // --- BUTON TIKLAMA OLAYI ---
                Button btn = slot.GetComponent<Button>();
                if (btn != null)
                {
                    int cardIndex = i;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => SelectCard(cardIndex));
                }
            }
            else
            {
                // Yeterli kart yoksa slotu kapat
                rewardCardSlots[i].gameObject.SetActive(false);
            }
        }

        rewardScreen.SetActive(true);
        Time.timeScale = 0f;
    }

    public void SelectCard(int cardIndex)
    {
        CardData selectedCard = offeredCards[cardIndex];
        DeckManager.instance.AddCardToDeck(selectedCard);

        if (cardIndex == bonusCardIndex)
        {
            GameManager.instance.player.AddShift(1);
        }

        rewardScreen.SetActive(false);
        Time.timeScale = 1f;
        GameManager.instance.SetGameState(GameState.Playing);
        LevelManager.instance.SpawnNextRoom();
    }
}