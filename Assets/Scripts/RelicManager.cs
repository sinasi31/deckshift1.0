using System.Collections.Generic;
using UnityEngine;

public class RelicManager : MonoBehaviour
{
    public static RelicManager instance;

    // Oyuncunun þu anda sahip olduðu tüm pasif eþyalarýn (Relic) listesi
    private List<RelicData> ownedRelics = new List<RelicData>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // DontDestroyOnLoad(gameObject); // (Gelecekte sahneler arasý geçiþ olursa bu gerekebilir)
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Oyuncuya yeni bir pasif eþya ekler. (Slot Machine bu fonksiyonu çaðýracak)
    /// </summary>
    public void AddRelic(RelicData newRelic)
    {
        if (ownedRelics.Contains(newRelic))
        {
            Debug.LogWarning($"Oyuncu zaten '{newRelic.relicName}' eþyasýna sahip.");
            return;
        }

        ownedRelics.Add(newRelic);
        Debug.Log($"Yeni eþya kazanýldý: {newRelic.relicName}");

        // TODO: UI'da bir yere bu eþyanýn ikonunu ekle
    }

    /// <summary>
    /// Diðer script'lerin (PlayerController gibi) oyuncuda bir eþya olup olmadýðýný
    /// ID'sine (kimliðine) bakarak kontrol etmesini saðlar.
    /// </summary>
    public bool HasRelic(string relicID)
    {
        foreach (RelicData relic in ownedRelics)
        {
            if (relic.relicID == relicID)
            {
                return true; // Eþya bulundu
            }
        }
        return false; // Eþya bulunamadý
    }

    // (Test için: Sahip olunan tüm eþyalarý listele)
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) // 'R' tuþuna bas (Relics)
        {
            Debug.Log("--- SAHÝP OLUNAN EÞYALAR ---");
            foreach (var relic in ownedRelics)
            {
                Debug.Log(relic.relicName);
            }
            Debug.Log("---------------------");
        }
    }
}