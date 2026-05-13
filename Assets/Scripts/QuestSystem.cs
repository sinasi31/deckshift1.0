using System.Collections.Generic;
using UnityEngine;

public class QuestSystem : MonoBehaviour
{
    public static QuestSystem instance;

    [Header("UI Ba�lant�lar�")]
    public GameObject overlayPanel;
    public Transform container;     
    public GameObject paperPrefab; 

    [Header("G�rev Havuzu")]
    public List<QuestData> allQuests;
    public List<ActiveQuest> activeQuests = new List<ActiveQuest>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        overlayPanel.SetActive(false);
    }

    // "E"ye bas�nca bu �a�r�lacak
    public void ToggleBoard()
    {
        bool isActive = overlayPanel.activeSelf;
        if (isActive)
        {
            CloseBoard();
        }
        else
        {
            overlayPanel.SetActive(true);
            GenerateQuests();
            if (GameManager.instance != null) GameManager.instance.RequestPause();
        }
    }
    public void CloseBoard()
    {
        overlayPanel.SetActive(false);
        if (GameManager.instance != null) GameManager.instance.ReleasePause();
    }

    void GenerateQuests()
    {
        foreach (Transform child in container) Destroy(child.gameObject);
        for (int i = 0; i < Mathf.Min(3, allQuests.Count); i++)
        {
            GameObject paper = Instantiate(paperPrefab, container);

            paper.transform.localScale = Vector3.one;

            paper.GetComponent<QuestPaper>().Setup(allQuests[i]);
        }
    }

    public void AcceptQuest(QuestData quest)
    {
        foreach (var q in activeQuests) { if (q.data == quest) return; }
        activeQuests.Add(new ActiveQuest(quest));
        Debug.Log($"G�rev Takibi Ba�lad�: {quest.questName}");
    }
    [System.Serializable]
    public class ActiveQuest
    {
        public QuestData data;      // Hangi g�rev?
        public int currentAmount;   // �u an ka� yapt�k? (�rn: 1/3)
        public bool isCompleted;    // Bitti mi?

        public ActiveQuest(QuestData quest)
        {
            data = quest;
            currentAmount = 0;
            isCompleted = false;
        }
    }
    public void ReportEvent(QuestType type, int amount = 1)
    {
        foreach (ActiveQuest quest in activeQuests)
        {
            if (quest.isCompleted) continue; 
            if (quest.data.type == type)
            {
                quest.currentAmount += amount;
                Debug.Log($"{quest.data.questName} �lerlemesi: {quest.currentAmount}/{quest.data.targetAmount}");

                CheckCompletion(quest);
            }
        }
    }

    private void CheckCompletion(ActiveQuest quest)
    {
        if (quest.currentAmount >= quest.data.targetAmount)
        {
            quest.isCompleted = true;
            Debug.Log($"G�REV TAMAMLANDI! �d�l: {quest.data.rewardText}");
            GiveReward(quest.data);
        }
    }
    private void GiveReward(QuestData data)
    {
        // Oyuncuyu bul (Sahne de�i�se bile bulur)
        PlayerController player = FindFirstObjectByType<PlayerController>();

        if (player != null)
        {
            switch (data.rewardType)
            {
                case RewardType.Gold:
                    player.AddGold(data.rewardAmount);
                    Debug.Log($"{data.rewardAmount} Alt�n Eklendi!");
                    break;

                case RewardType.Heal:
                    player.Heal(data.rewardAmount);
                    break;

                case RewardType.ShiftCharge:
                    player.IncreaseMaxShift(data.rewardAmount); // PlayerController'da bu fonksiyonu yazm��t�k
                    player.ResetShiftToMax();
                    break;
            }
        }
    }

}