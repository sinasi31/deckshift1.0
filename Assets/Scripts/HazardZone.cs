using UnityEngine;
using System.Collections;

public class HazardZone : MonoBehaviour
{
    [Header("Ayarlar")]
    public float damagePerSecond = 10f; // Saniyede kaç hasar?
    public string requiredRelicID = "LavaBoots"; // Hangi eþya korur?

    private void OnTriggerStay2D(Collider2D other)
    {
        // Ýçindeki þey Oyuncu mu?
        if (other.CompareTag("Player"))
        {
            // 1. Oyuncuda koruyucu Relic var mý?
            if (RelicManager.instance != null && RelicManager.instance.HasRelic(requiredRelicID))
            {
                // Varsa hasar verme, çýk.
                return;
            }

            // 2. Yoksa Canýný Yak
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                // Time.deltaTime ile çarparak saniyeye yayýyoruz
                player.TakeDamage(damagePerSecond * Time.deltaTime);
            }
        }
    }
}