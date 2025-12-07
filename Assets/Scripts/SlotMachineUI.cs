using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SlotMachineUI : MonoBehaviour
{
    public static SlotMachineUI instance;

    [Header("UI Referanslarý")]
    public GameObject slotPanel; // Tüm ekraný kaplayan panel
    public TextMeshProUGUI reel1_Text;
    public TextMeshProUGUI reel2_Text;
    public TextMeshProUGUI reel3_Text;
    public TextMeshProUGUI resultText; // "KAZANDIN!" yazýsý
    public Button closeButton; // Ödülü alýp kapatma butonu

    [Header("Görseller (Ýsteðe Baðlý)")]
    public Sprite[] symbolSprites;
    public Image reel1_Img, reel2_Img, reel3_Img;

    // O anki makineden gelen havuzlarý burada tutacaðýz
    private List<RelicData> currentCommonPool;
    private List<RelicData> currentEpicPool;
    private List<RelicData> currentLegendaryPool;

    private GameObject currentMachineObject; // Hangi makineyi yok edeceðiz?

    private void Awake()
    {
        if (instance == null) instance = this;
        if (slotPanel != null) slotPanel.SetActive(false);
    }

    // Bu fonksiyonu sahnedeki makine çaðýracak
    public void OpenSlotMachine(List<RelicData> common, List<RelicData> epic, List<RelicData> legendary, GameObject machineObj)
    {
        currentCommonPool = common;
        currentEpicPool = epic;
        currentLegendaryPool = legendary;
        currentMachineObject = machineObj;

        // 1. Oyunu Dondur ve Paneli Aç
        Time.timeScale = 0f;
        slotPanel.SetActive(true);
        if (closeButton != null) closeButton.gameObject.SetActive(false); // Dönme bitene kadar kapatma butonu gizli
        if (resultText != null) resultText.text = "SPINNING...";

        // 2. Dönme Ýþlemini Baþlat
        StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        // --- ANÝMASYON KISMI ---
        float duration = 2.5f; // Toplam dönme süresi
        float timer = 0f;

        // Rastgele sayýlar/resimler dönsün
        while (timer < duration)
        {
            SetReelRandom(reel1_Text, reel1_Img);
            SetReelRandom(reel2_Text, reel2_Img);
            SetReelRandom(reel3_Text, reel3_Img);

            // Oyun donuk olduðu için unscaledDeltaTime kullanýyoruz
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        // --- SONUÇ BELÝRLEME ---
        int r1 = Random.Range(1, 6);
        int r2 = Random.Range(1, 6);
        int r3 = Random.Range(1, 6);

        SetReelFinal(reel1_Text, reel1_Img, r1);
        SetReelFinal(reel2_Text, reel2_Img, r2);
        SetReelFinal(reel3_Text, reel3_Img, r3);

        int total = r1 + r2 + r3;
        Debug.Log($"SLOT SONUÇ: {total}");

        // --- ÖDÜL VERME ---
        RelicData reward = null;

        if (total == 15) reward = GetRandomReward(currentLegendaryPool);
        else if (total >= 12) reward = GetRandomReward(currentEpicPool);
        else if (total >= 8) reward = GetRandomReward(currentCommonPool);

        if (reward != null)
        {
            if (resultText != null) resultText.text = $"YOU WON!\n<color=yellow>{reward.relicName}</color>";
            RelicManager.instance.AddRelic(reward);
        }
        else
        {
            if (resultText != null) resultText.text = "BAD LUCK...";
        }

        // Kapatma butonunu göster
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(true);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    public void ClosePanel()
    {
        // Paneli kapat, oyunu devam ettir
        slotPanel.SetActive(false);
        Time.timeScale = 1f;

        // Makineyi yok et (Artýk kullanýldý)
        if (currentMachineObject != null) Destroy(currentMachineObject);
    }

    // --- YARDIMCI FONKSÝYONLAR ---
    private void SetReelRandom(TextMeshProUGUI txt, Image img)
    {
        int r = Random.Range(1, 6);
        if (img != null && symbolSprites != null && symbolSprites.Length >= 5)
            img.sprite = symbolSprites[r - 1];
        else if (txt != null)
            txt.text = r.ToString();
    }

    private void SetReelFinal(TextMeshProUGUI txt, Image img, int val)
    {
        if (img != null && symbolSprites != null && symbolSprites.Length >= 5)
            img.sprite = symbolSprites[val - 1];
        else if (txt != null)
            txt.text = val.ToString();
    }

    private RelicData GetRandomReward(List<RelicData> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }
}