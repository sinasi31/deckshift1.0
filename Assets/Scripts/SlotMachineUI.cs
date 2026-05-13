using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SlotMachineUI : MonoBehaviour
{
    public static SlotMachineUI instance;

    [Header("UI Referansları")]
    public GameObject slotPanel;
    [SerializeField] private GameObject gameplayHUD;
    public TextMeshProUGUI resultText;
    public Button closeButton;

    [Header("Makaralar (Yeni Sistem)")]
    // Buraya yukarıda yazdığımız SlotReel scriptlerini sürükle
    public SlotReel reel1;
    public SlotReel reel2;
    public SlotReel reel3;

    // Havuzlar
    private List<RelicData> currentCommonPool;
    private List<RelicData> currentRarePool;
    private List<RelicData> currentEpicPool;
    private List<RelicData> currentLegendaryPool;

    private GameObject currentMachineObject;

    private void Awake()
    {
        if (instance == null) instance = this;
        if (slotPanel != null) slotPanel.SetActive(false);
    }

    public void OpenSlotMachine(List<RelicData> common, List<RelicData> rare, List<RelicData> epic, List<RelicData> legendary, GameObject machineObj)
    {
        currentCommonPool = common;
        currentRarePool = rare;
        currentEpicPool = epic;
        currentLegendaryPool = legendary;
        currentMachineObject = machineObj;

        // 1. Oyunu Dondur ve Paneli Aç
        if (GameManager.instance != null) GameManager.instance.RequestPause();
        slotPanel.SetActive(true);
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);
        if (gameplayHUD != null) gameplayHUD.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(false);
        if (resultText != null) resultText.text = "SPINNING...";

        // 2. Dönme İşlemini Başlat
        StartCoroutine(SpinProcess());
    }

    private IEnumerator SpinProcess()
    {
        int r1 = Random.Range(0, 8);
        int r2 = Random.Range(0, 8);
        int r3 = Random.Range(0, 8);
        reel1.Spin(r1, 1.5f);
        yield return new WaitForSecondsRealtime(0.2f); 

        reel2.Spin(r2, 2.0f);
        yield return new WaitForSecondsRealtime(0.2f);

        reel3.Spin(r3, 2.5f);
        yield return new WaitForSecondsRealtime(2.5f);
        CheckRewards(r1, r2, r3);

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(true);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    private void CheckRewards(int r1, int r2, int r3)
    {
        if (r1 == 0 || r2 == 0 || r3 == 0)
        {
            Debug.Log("KURUKAFA GELDİ! KAYBETTİN.");
            if (resultText != null) resultText.text = "<color=red>BAD LUCK!\n(SKULL)</color>";
        }
        else
        {
            int total = r1 + r2 + r3;
            Debug.Log($"SLOT SONUÇ: {total}");

            RelicData reward = null;
            string rarityText = "";

            if (total == 21) // 7-7-7
            {
                reward = GetRandomReward(currentLegendaryPool);
                rarityText = "<color=orange>JACKPOT! (LEGENDARY)</color>";
            }
            else if (total >= 16)
            {
                reward = GetRandomReward(currentEpicPool);
                rarityText = "<color=purple>EPIC!</color>";
            }
            else if (total >= 11)
            {
                reward = GetRandomReward(currentRarePool);
                rarityText = "<color=blue>RARE</color>";
            }
            else
            {
                reward = GetRandomReward(currentCommonPool);
                rarityText = "<color=grey>COMMON</color>";
            }

            if (reward != null)
            {
                if (resultText != null)
                    resultText.text = $"{rarityText}\nYOU WON: {reward.relicName}";

                RelicManager.instance.AddRelic(reward);
            }
        }
    }

    public void ClosePanel()
    {
        if (gameplayHUD != null) gameplayHUD.SetActive(true);
        slotPanel.SetActive(false);
        if (GameManager.instance != null) GameManager.instance.ReleasePause();
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);
        if (currentMachineObject != null) Destroy(currentMachineObject);
    }

    private RelicData GetRandomReward(List<RelicData> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }
}