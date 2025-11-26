using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SlotMachine : MonoBehaviour
{
    [Header("Ödül Havuzlarý")]
    public List<RelicData> commonRewardPool;  // Düþük Toplam (örn: 8-11)
    public List<RelicData> epicRewardPool;    // Yüksek Toplam (örn: 12-14)
    public List<RelicData> legendaryRewardPool; // Maksimum Toplam (örn: 15)

    [Header("Görsel Arayüz (UI)")]
    public TextMeshProUGUI reel1_Text;
    public TextMeshProUGUI reel2_Text;
    public TextMeshProUGUI reel3_Text;
    public TextMeshProUGUI resultText;

    [Header("Animasyon & Zamanlama")]
    // Senin ekleyeceðin "dönme" animasyonunu tetiklemek için
    public Animator machineAnimator;
    public string spinTriggerName = "Spin"; // Animator'de tetikleyeceðimiz trigger'ýn adý
    public float spinSpeed = 20f; // Sayýlarýn ne hýzla döneceði

    // Reel'lerin ne zaman duracaðýný belirler (dramatik etki)
    public float reel1_StopTime = 1.5f;
    public float reel2_StopTime = 2.5f;
    public float reel3_StopTime = 3.5f;

    [Header("Etkileþim")]
    private TextMeshProUGUI interactPromptText;
    public KeyCode interactKey = KeyCode.E;

    private bool playerInRange = false;
    private bool isSpinning = false; // Makine zaten çalýþýyorken tekrar basýlmasýný engelle

    private void Awake()
    {
        // "Tembel Yükleme" (Lazy Load) ile metni bul
        GameObject promptObject = GameObject.Find("InteractPrompt_Text");
        if (promptObject != null)
        {
            interactPromptText = promptObject.GetComponent<TextMeshProUGUI>();
            interactPromptText.gameObject.SetActive(false); // Baþlangýçta gizle
        }
        else
            Debug.LogError("Hiyerarþi'de 'InteractPrompt_Text' ADINDA bir obje bulunamadý!");

        if (resultText != null) resultText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isSpinning && playerInRange && Input.GetKeyDown(interactKey))
        {
            StartCoroutine(SpinMachineRoutine());
        }
    }

    /// <summary>
    /// Makineyi çalýþtýran yeni Coroutine
    /// </summary>
    private IEnumerator SpinMachineRoutine()
    {
        isSpinning = true;
        if (interactPromptText != null) interactPromptText.gameObject.SetActive(false);
        if (resultText != null) resultText.gameObject.SetActive(false);

        Debug.Log("SLOT MAKÝNESÝ ÇALIÞIYOR...");

        // 1. Senin görsel animasyonunu tetikle
        if (machineAnimator != null)
        {
            machineAnimator.SetTrigger(spinTriggerName);
        }

        // 2. Sonuçlarý önceden belirle (ama gösterme)
        int r1_final = Random.Range(1, 6); // 1-5
        int r2_final = Random.Range(1, 6);
        int r3_final = Random.Range(1, 6);

        // 3. Görsel "Dönme" Döngüsü
        float timer = 0f;
        bool reel1_stopped = false;
        bool reel2_stopped = false;

        while (timer < reel3_StopTime + 0.5f) // Son reel durduktan sonra yarým sn daha bekle
        {
            timer += Time.deltaTime;

            // --- Reel 1 Döngüsü ---
            if (timer < reel1_StopTime)
            {
                // Sayýlarý 1-2-3-4-5-1-2... þeklinde hýzlýca döndür
                int visualNumber = (int)(timer * spinSpeed) % 5 + 1;
                reel1_Text.text = visualNumber.ToString();
            }
            else if (!reel1_stopped)
            {
                reel1_stopped = true;
                reel1_Text.text = r1_final.ToString(); // Sonucu kilitle
                // TODO: Buraya "Reel Stop" (Makar durdu) sesi ekle
            }

            // --- Reel 2 Döngüsü ---
            if (timer < reel2_StopTime)
            {
                int visualNumber = (int)(timer * spinSpeed + 2) % 5 + 1; // +2 ekleyerek diðerinden farklý görünmesini saðla
                reel2_Text.text = visualNumber.ToString();
            }
            else if (!reel2_stopped)
            {
                reel2_stopped = true;
                reel2_Text.text = r2_final.ToString(); // Sonucu kilitle
                // TODO: Buraya "Reel Stop" (Makar durdu) sesi ekle
            }

            // --- Reel 3 Döngüsü ---
            if (timer < reel3_StopTime)
            {
                int visualNumber = (int)(timer * spinSpeed + 4) % 5 + 1; // +4 ekleyerek farklý görünmesini saðla
                reel3_Text.text = visualNumber.ToString();
            }
            else
            {
                reel3_Text.text = r3_final.ToString(); // Sonucu kilitle
                // TODO: Buraya "Reel Stop" (Makar durdu) sesi ekle
            }

            yield return null; // Bir sonraki frame'e kadar bekle
        }

        // 4. Toplama Göre Ödülü Belirle
        int totalSum = r1_final + r2_final + r3_final;
        Debug.Log($"SONUÇ: [{r1_final}][{r2_final}][{r3_final}] = TOPLAM: {totalSum}");

        RelicData finalReward = null;

        // --- YENÝ ÖDÜL MANTIÐI (TOPLAMA GÖRE) ---
        // Bu sayýlarý istediðin gibi deðiþtirebilirsin
        if (totalSum == 15) // Sadece 5-5-5
        {
            finalReward = GetRandomReward(legendaryRewardPool, "EFSANEVÝ (Toplam 15)");
        }
        else if (totalSum >= 12) // 12, 13, 14
        {
            finalReward = GetRandomReward(epicRewardPool, "EPÝK (Toplam 12+)");
        }
        else if (totalSum >= 8) // 8, 9, 10, 11
        {
            finalReward = GetRandomReward(commonRewardPool, "YAYGIN (Toplam 8+)");
        }
        // Eðer toplam 8'den azsa (3-7 arasý), 'finalReward' 'null' kalýr

        // 5. Sonucu Göster
        if (finalReward != null)
        {
            if (resultText != null)
            {
                resultText.gameObject.SetActive(true);
                resultText.text = $"KAZANDIN!\n{finalReward.relicName}";
            }
            RelicManager.instance.AddRelic(finalReward);
        }
        else
        {
            if (resultText != null)
            {
                resultText.gameObject.SetActive(true);
                resultText.text = "KAYBETTÝN!";
            }
            Debug.Log("Kaybettin! Toplam: " + totalSum);
        }

        // Makineyi 3 saniye sonra yok et ki oyuncu sonucu görsün
        yield return new WaitForSeconds(3f);
        Destroy(gameObject);
    }

    /// <summary>
    /// Belirlenen ödül havuzundan rastgele bir eþya seçer.
    /// </summary>
    private RelicData GetRandomReward(List<RelicData> pool, string tierName)
    {
        if (pool == null || pool.Count == 0)
        {
            Debug.LogError($"'{tierName}' ödül havuzu BOÞ! Inspector'u kontrol et.");
            return null;
        }
        int randomIndex = Random.Range(0, pool.Count);
        return pool[randomIndex];
    }

    // (OnTriggerEnter2D ve OnTriggerExit2D fonksiyonlarýn ayný kalýyor)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (interactPromptText != null && !isSpinning)
            {
                interactPromptText.gameObject.SetActive(true);
                interactPromptText.text = $"Press [{interactKey}] to use";
            }
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (interactPromptText != null)
            {
                interactPromptText.gameObject.SetActive(false);
            }
        }
    }
}