using System.Collections.Generic;
using UnityEngine;

public class QuestBoardUI : MonoBehaviour
{
    // --- YENÝ EKLENEN KISIM: Singleton ---
    public static QuestBoardUI instance;
    private void Start()
    {
        // Oyun baþlar baþlamaz paneli kapat ki ekranda durmasýn
        CloseBoard();
    }
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // Bu objeyi (ve baðlý olduðu Canvas'ý) sahneler arasý taþýnýrken yok etme
            DontDestroyOnLoad(gameObject.transform.root);
        }
        else
        {
            // Eðer baþka bir sahneden zaten yüklenmiþ bir UI geldiyse, bu yenisini yok et
            Destroy(gameObject);
        }
    }
    // -------------------------------------

    [Header("Settings")]
    public GameObject boardPanel;
    public Transform questsContainer;
    public GameObject questItemPrefab;

    [Header("Data Source")]
    public List<QuestData> allPossibleQuests;

    public void OpenBoard()
    {
        boardPanel.SetActive(true);
        // Ýstersen her açýþta yeni görevler üretebilirsin
        // Veya sadece Hub'da üretip, Campfire'da kalanlarý gösterebilirsin.
        // Þimdilik her açýþta yenileyelim:
        GenerateDailyQuests();
    }

    public void CloseBoard()
    {
        boardPanel.SetActive(false);
    }

    // ... (GenerateDailyQuests fonksiyonun aynen kalsýn) ...
    private void GenerateDailyQuests()
    {
        // Temizlik
        foreach (Transform child in questsContainer) Destroy(child.gameObject);

        // Debug Satýrý Ekle
        Debug.Log($"Havuzdaki Toplam Quest: {allPossibleQuests.Count}");

        int countToSpawn = Mathf.Min(3, allPossibleQuests.Count);

        // Debug Satýrý Ekle
        Debug.Log($"Oluþturulmaya Çalýþýlan Sayý: {countToSpawn}");

        // Karýþtýrma
        List<QuestData> shuffled = new List<QuestData>(allPossibleQuests);
        for (int i = 0; i < shuffled.Count; i++)
        {
            QuestData temp = shuffled[i];
            int rand = Random.Range(i, shuffled.Count);
            shuffled[i] = shuffled[rand];
            shuffled[rand] = temp;
        }

        // Oluþturma
        for (int i = 0; i < countToSpawn; i++)
        {
            GameObject obj = Instantiate(questItemPrefab, questsContainer);
            QuestItemUI ui = obj.GetComponent<QuestItemUI>();
            ui.Setup(shuffled[i]);

            // Debug: Objenin nereye oluþtuðunu kontrol et
            Debug.Log($"{i}. Quest Oluþtu: {obj.name} - Parent: {obj.transform.parent.name}");
        }
    }
}