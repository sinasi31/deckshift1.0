using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardManager : MonoBehaviour
{
    public static RewardManager instance;

    [Header("References")]
    public GameObject rewardScreen;
    public List<Button> rewardCardButtons;
    private List<CardData> offeredCards = new List<CardData>();
    private int bonusCardIndex = -1;

    private void Awake()
    {
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        rewardScreen.SetActive(false);
    }

    public void ShowRewardScreen()
    {
        offeredCards.Clear();
        bonusCardIndex = -1;
        List<CardData> cardPool = AchievementManager.instance.GetAvailableCardPool();
        GameManager.instance.SetGameState(GameState.Paused);

        for (int i = 0; i < 3; i++)
        {
            if (cardPool.Count == 0) break;
            int randomIndex = Random.Range(0, cardPool.Count);
            offeredCards.Add(cardPool[randomIndex]);
            cardPool.RemoveAt(randomIndex);
        }

        if (offeredCards.Count > 0)
            bonusCardIndex = Random.Range(0, offeredCards.Count);

        for (int i = 0; i < rewardCardButtons.Count; i++)
        {
            if (i < offeredCards.Count)
            {
                rewardCardButtons[i].gameObject.SetActive(true);
                CardData data = offeredCards[i];
                string cardName = data.cardName;
                string cardDescription = data.description;

                if (i == bonusCardIndex)
                {
                    cardDescription += "\n<b>(+1 Shift!)</b>"; // 'Jump Charge' -> 'Shift'
                }

                rewardCardButtons[i].transform.Find("CardName").GetComponent<TextMeshProUGUI>().text = cardName;
                rewardCardButtons[i].transform.Find("CardDescription").GetComponent<TextMeshProUGUI>().text = cardDescription;

                int cardIndex = i;
                rewardCardButtons[i].onClick.RemoveAllListeners();
                rewardCardButtons[i].onClick.AddListener(() => SelectCard(cardIndex));
            }
            else
            {
                rewardCardButtons[i].gameObject.SetActive(false);
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
            // AddShift() fonksiyonunu çaðýrýyoruz
            GameManager.instance.player.AddShift(1);
        }

        rewardScreen.SetActive(false);
        Time.timeScale = 1f;
        GameManager.instance.SetGameState(GameState.Playing);
        LevelManager.instance.SpawnNextRoom();
    }
}