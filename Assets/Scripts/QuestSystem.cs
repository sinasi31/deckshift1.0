using System.Collections.Generic;
using UnityEngine;

public class QuestSystem : MonoBehaviour
{
    public static QuestSystem instance;

    [Header("UI Baðlantýlarý")]
    public GameObject overlayPanel;
    public Transform container;     
    public GameObject paperPrefab; 

    [Header("Görev Havuzu")]
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

    // "E"ye basýnca bu çaðrýlacak
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
            Time.timeScale = 0;
        }
    }
    public void CloseBoard()
    {
        overlayPanel.SetActive(false);
        Time.timeScale = 1; 
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
        Debug.Log($"Görev Takibi Baþladý: {quest.questName}");
    }
    [System.Serializable]
    public class ActiveQuest
    {
        public QuestData data;      // Hangi görev?
        public int currentAmount;   // Þu an kaç yaptýk? (Örn: 1/3)
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
                Debug.Log($"{quest.data.questName} Ýlerlemesi: {quest.currentAmount}/{quest.data.targetAmount}");

                CheckCompletion(quest);
            }
        }
    }

    private void CheckCompletion(ActiveQuest quest)
    {
        if (quest.currentAmount >= quest.data.targetAmount)
        {
            quest.isCompleted = true;
            Debug.Log($"GÖREV TAMAMLANDI! Ödül: {quest.data.rewardText}");
            GiveReward(quest.data);
        }
    }
    private void GiveReward(QuestData data)
    {
        // Oyuncuyu bul (Sahne deðiþse bile bulur)
        PlayerController player = FindFirstObjectByType<PlayerController>();

        if (player != null)
        {
            switch (data.rewardType)
            {
                case RewardType.Gold:
                    player.AddGold(data.rewardAmount);
                    Debug.Log($"{data.rewardAmount} Altýn Eklendi!");
                    break;

                case RewardType.Heal:
                    player.Heal(data.rewardAmount);
                    break;

                case RewardType.ShiftCharge:
                    player.IncreaseMaxShift(data.rewardAmount); // PlayerController'da bu fonksiyonu yazmýþtýk
                    player.ResetShiftToMax();
                    break;
            }
        }
    }

}