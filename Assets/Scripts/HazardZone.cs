using UnityEngine;
using System.Collections;

public class HazardZone : MonoBehaviour
{
    [Header("Ayarlar")]
    public float damagePerSecond = 10f; // Saniyede ka� hasar?
    public string requiredRelicID = "LavaBoots"; // Hangi e�ya korur?

    [Tooltip("Seconds between damage ticks. Damage per tick = damagePerSecond * this, so total DPS stays the same regardless of interval.")]
    [SerializeField] private float damageTickInterval = 0.5f;

    private float nextTickTime;

    private void OnTriggerStay2D(Collider2D other)
    {
        // ��indeki �ey Oyuncu mu?
        if (other.CompareTag("Player"))
        {
            // Tick gate: TakeDamage drives sound/shake/animator, so calling it per frame
            // spammed all of those 60x/s (audit_report.md High 2.1). First contact ticks
            // immediately, then once per interval.
            if (Time.time < nextTickTime) return;

            // 1. Oyuncuda koruyucu Relic var m�?
            if (RelicManager.instance != null && RelicManager.instance.HasRelic(requiredRelicID))
            {
                // Varsa hasar verme, ��k.
                return;
            }

            // 2. Yoksa Can�n� Yak
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                nextTickTime = Time.time + damageTickInterval;
                player.TakeDamage(damagePerSecond * damageTickInterval);
            }
        }
    }
}