using System.Collections.Generic;
using UnityEngine;

public class RelicManager : MonoBehaviour
{
    public static RelicManager instance;

    // Oyuncunun şu anda sahip olduğu tüm pasif eşyaların (Relic) listesi
    private List<RelicData> ownedRelics = new List<RelicData>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // DontDestroyOnLoad(gameObject); // (Gelecekte sahneler arası geçiş olursa bu gerekebilir)
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Oyuncuya yeni bir pasif eşya ekler. (Slot Machine bu fonksiyonu çağıracak)
    /// </summary>
    public void AddRelic(RelicData newRelic)
    {
        if (ownedRelics.Contains(newRelic))
        {
            Debug.LogWarning($"Oyuncu zaten '{newRelic.relicName}' eşyasına sahip.");
            return;
        }

        ownedRelics.Add(newRelic);
        Debug.Log($"Yeni eşya kazanıldı: {newRelic.relicName}");

        // TODO: UI'da bir yere bu eşyanın ikonunu ekle
    }

    /// <summary>
    /// Diğer script'lerin (PlayerController gibi) oyuncuda bir eşya olup olmadığını
    /// ID'sine (kimliğine) bakarak kontrol etmesini sağlar.
    /// </summary>
    public bool HasRelic(string relicID)
    {
        foreach (RelicData relic in ownedRelics)
        {
            if (relic.relicID == relicID)
            {
                return true; // Eşya bulundu
            }
        }
        return false; // Eşya bulunamadı
    }

    // (Test için: Sahip olunan tüm eşyaları listele)
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) // 'R' tuşuna bas (Relics)
        {
            Debug.Log("--- SAHİP OLUNAN EŞYALAR ---");
            foreach (var relic in ownedRelics)
            {
                Debug.Log(relic.relicName);
            }
            Debug.Log("---------------------");
        }
    }
    public void OnEnemyKilled()
    {
        // RELIC 1: Vampire Tooth (ID: VampireTooth)
        // Özellik: Düşman öldürünce +5 Can
        if (HasRelic("VampireTooth"))
        {
            if (GameManager.instance != null && GameManager.instance.player != null)
            {
                GameManager.instance.player.Heal(5);
                Debug.Log("🧛 Vampire Tooth: +5 Can kazanıldı!");
            }
        }

        // RELIC 2: Kinetic Capacitor (ID: KineticCapacitor)
        // Özellik: Düşman öldürünce +2 Shift
        if (HasRelic("KineticCapacitor"))
        {
            if (GameManager.instance != null && GameManager.instance.player != null)
            {
                GameManager.instance.player.AddShift(2);
                Debug.Log("⚡ Kinetic Capacitor: +2 Shift kazanıldı!");
            }
        }
    }

    // 2. Oyuncu hasar aldığında bu fonksiyon çağrılacak
    public void OnPlayerTakeDamage()
    {
        // RELIC 3: Spiked Carapace (ID: SpikedCarapace)
        // Özellik: Hasar alınca etraftaki düşmanlara hasar yansıt
        if (HasRelic("SpikedCarapace"))
        {
            if (GameManager.instance != null && GameManager.instance.player != null)
            {
                PlayerController player = GameManager.instance.player;

                // Oyuncunun 3 birim etrafındaki düşmanları bul
                Collider2D[] enemies = Physics2D.OverlapCircleAll(player.transform.position, 3f, player.enemyLayer);

                foreach (Collider2D enemy in enemies)
                {
                    // Düşmanın can scriptine eriş (Script adın EnemyHealth ise)
                    EnemyHealth eHealth = enemy.GetComponent<EnemyHealth>();
                    if (eHealth != null)
                    {
                        eHealth.TakeDamage(20f); // 20 Hasar yansıt
                    }
                }
                Debug.Log("🌵 Spiked Carapace: Düşmanlara hasar yansıtıldı!");
            }
        }
    }
}