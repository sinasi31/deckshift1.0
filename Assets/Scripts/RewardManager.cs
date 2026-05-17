using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardManager : MonoBehaviour
{
    public static RewardManager instance;

    [Header("References")]
    public GameObject rewardScreen;

    // --- DE����KL�K: Art�k Button listesi de�il, CardUI listesi tutuyoruz ---
    public List<CardUI> rewardCardSlots;
    // --- B�T�� ---

    private List<CardData> offeredCards = new List<CardData>();
    private int bonusCardIndex = -1;
    private bool isShowing = false;

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
        if (isShowing) return;
        isShowing = true;

        offeredCards.Clear();
        bonusCardIndex = -1;

        List<CardData> cardPool = AchievementManager.instance.GetAvailableCardPool();
        GameManager.instance.SetGameState(GameState.Paused);

        // 3 Kart Se�
        for (int i = 0; i < 3; i++)
        {
            if (cardPool.Count == 0) break;
            int randomIndex = Random.Range(0, cardPool.Count);
            offeredCards.Add(cardPool[randomIndex]);
            cardPool.RemoveAt(randomIndex);
        }

        // Bonus �ans�
        if (offeredCards.Count > 0)
            bonusCardIndex = Random.Range(0, offeredCards.Count);

        // Kartlar� UI'a yerle�tir
        for (int i = 0; i < rewardCardSlots.Count; i++)
        {
            if (i < offeredCards.Count)
            {
                CardUI slot = rewardCardSlots[i];
                slot.gameObject.SetActive(true);

                CardData data = offeredCards[i];

                // --- G�RSEL KURULUM (En �nemli K�s�m) ---
                // CardUI'�n kendi Setup fonksiyonunu kullan�yoruz!
                // G�rsel olmas� i�in ge�ici bir RuntimeCard olu�turuyoruz.
                RuntimeCard visualCard = new RuntimeCard(data);

                // Bonus varsa a��klamas�n� g�ncelle (Sadece g�rsel i�in)
                if (i == bonusCardIndex)
                {
                    // Not: Bu kal�c� veriyi de�i�tirmez, sadece visualCard'� etkiler
                    // (CardUI scriptinde descriptionText'i description'dan ald���m�z� varsayarsak)
                    // Ancak CardData ScriptableObject oldu�u i�in a��klamay� kodla de�i�tirmek riskli olabilir.
                    // �imdilik bonusu g�stermek i�in basit bir y�ntem:
                    Debug.Log($"Kart {i} BONUSLU (+1 Shift)");
                }

                // Setup'� �a��r (Bu; resmi, frame'i, daireleri her �eyi ayarlar!)
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
        if (GameManager.instance != null) GameManager.instance.RequestPause();
    }

    public void SelectCard(int cardIndex)
    {
        CardData selectedCard = offeredCards[cardIndex];
        DeckManager.instance.AddCardToDeck(selectedCard);

        if (cardIndex == bonusCardIndex)
        {
            GameManager.instance.player.AddShift(1);
        }

        isShowing = false;
        rewardScreen.SetActive(false);
        if (GameManager.instance != null) GameManager.instance.ReleasePause();
        GameManager.instance.SetGameState(GameState.Playing);
        LevelManager.instance.SpawnNextRoom();
    }
}