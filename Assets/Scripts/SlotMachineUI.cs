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
    public TextMeshProUGUI reel1_Text;
    public TextMeshProUGUI reel2_Text;
    public TextMeshProUGUI reel3_Text;
    public TextMeshProUGUI resultText;
    public Button closeButton;

    [Header("Görseller (İsteğe Bağlı)")]
    public Sprite[] symbolSprites;
    public Image reel1_Img, reel2_Img, reel3_Img;

    // Havuzlar
    private List<RelicData> currentCommonPool;
    private List<RelicData> currentRarePool; // <-- YENİ EKLENEN
    private List<RelicData> currentEpicPool;
    private List<RelicData> currentLegendaryPool;

    private GameObject currentMachineObject;

    private void Awake()
    {
        if (instance == null) instance = this;
        if (slotPanel != null) slotPanel.SetActive(false);
    }

    // --- HATANIN ÇÖZÜLDÜĞÜ YER ---
    // Parantez içine 'List<RelicData> rare' parametresini ekledik.
    public void OpenSlotMachine(List<RelicData> common, List<RelicData> rare, List<RelicData> epic, List<RelicData> legendary, GameObject machineObj)
    {
        currentCommonPool = common;
        currentRarePool = rare; // Artık 'rare' tanınıyor
        currentEpicPool = epic;
        currentLegendaryPool = legendary;
        currentMachineObject = machineObj;

        // 1. Oyunu Dondur ve Paneli Aç
        Time.timeScale = 0f;
        slotPanel.SetActive(true);
        if (closeButton != null) closeButton.gameObject.SetActive(false);
        if (resultText != null) resultText.text = "SPINNING...";

        // 2. Dönme İşlemini Başlat
        StartCoroutine(SpinRoutine());
    }
    // -----------------------------

    private IEnumerator SpinRoutine()
    {
        float duration = 2.5f;
        float timer = 0f;

        while (timer < duration)
        {
            UpdateVisualsRandomly(reel1_Text, reel1_Img);
            UpdateVisualsRandomly(reel2_Text, reel2_Img);
            UpdateVisualsRandomly(reel3_Text, reel3_Img);
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        // --- SONUÇ BELİRLEME (0-7 Arası) ---
        int r1 = Random.Range(0, 8);
        int r2 = Random.Range(0, 8);
        int r3 = Random.Range(0, 8);

        SetReelFinal(reel1_Text, reel1_Img, r1);
        SetReelFinal(reel2_Text, reel2_Img, r2);
        SetReelFinal(reel3_Text, reel3_Img, r3);

        // --- KURUKAFA KONTROLÜ ---
        if (r1 == 0 || r2 == 0 || r3 == 0)
        {
            Debug.Log("KURUKAFA GELDİ! KAYBETTİN.");
            if (resultText != null) resultText.text = "<color=red>BAD LUCK!\n(SKULL)</color>";
        }
        else
        {
            // --- ÖDÜL HESAPLA ---
            int total = r1 + r2 + r3;
            Debug.Log($"SLOT SONUÇ: {total}");

            RelicData reward = null;
            string rarityText = "";

            if (total == 21)
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
            else // 3-10
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

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(true);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    public void ClosePanel()
    {
        slotPanel.SetActive(false);
        Time.timeScale = 1f;
        if (currentMachineObject != null) Destroy(currentMachineObject);
    }

    private void UpdateVisualsRandomly(TextMeshProUGUI txt, Image img)
    {
        int randomNum = Random.Range(0, 8);
        if (img != null && symbolSprites != null && symbolSprites.Length > 0)
        {
            img.enabled = true;
            if (txt) txt.enabled = false;
            if (randomNum < symbolSprites.Length) img.sprite = symbolSprites[randomNum];
        }
        else if (txt != null)
        {
            txt.text = (randomNum == 0) ? "☠️" : randomNum.ToString();
        }
    }

    private void SetReelFinal(TextMeshProUGUI txt, Image img, int val)
    {
        if (img != null && symbolSprites != null && symbolSprites.Length > 0)
        {
            if (val < symbolSprites.Length) img.sprite = symbolSprites[val];
        }
        else if (txt != null)
        {
            txt.text = (val == 0) ? "☠️" : val.ToString();
        }
    }

    private RelicData GetRandomReward(List<RelicData> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }
}