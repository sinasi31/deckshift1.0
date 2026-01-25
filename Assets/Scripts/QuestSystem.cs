using System.Collections.Generic;
using UnityEngine;

public class QuestSystem : MonoBehaviour
{
    public static QuestSystem instance;

    [Header("UI Baðlantýlarý")]
    public GameObject overlayPanel; // Siyah arka plan
    public Transform container;     // Kaðýtlarýn dizileceði yer
    public GameObject paperPrefab;  // Kaðýt prefabý

    [Header("Görev Havuzu")]
    public List<QuestData> allQuests; // Editörden sürükle
    public List<ActiveQuest> activeQuests = new List<ActiveQuest>();

    private void Awake()
    {
        // Singleton & Kalýcýlýk
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Sahne deðiþse de silinme
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Baþlangýçta paneli kapat
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
            Time.timeScale = 0; // Oyunu durdur (Player hareket edemesin)
        }
    }
    public void CloseBoard()
    {
        overlayPanel.SetActive(false);
        Time.timeScale = 1; 
    }

    void GenerateQuests()
    {
        // Eski kaðýtlarý temizle
        foreach (Transform child in container) Destroy(child.gameObject);

        // 3 tane rastgele görev seç ve oluþtur
        for (int i = 0; i < Mathf.Min(3, allQuests.Count); i++)
        {
            GameObject paper = Instantiate(paperPrefab, container);

            // Layout Group buglarýný önlemek için scale'i resetle
            paper.transform.localScale = Vector3.one;

            // Veriyi doldur
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
        }
    }
}