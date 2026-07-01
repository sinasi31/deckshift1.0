using UnityEngine;
using System.Collections;

public class HazardZone : MonoBehaviour
{
    [Header("Ayarlar")]
    public float damagePerSecond = 10f; // Saniyede ka� hasar?
    public string requiredRelicID = "LavaBoots"; // Hangi e�ya korur?

    [Tooltip("Seconds between damage ticks. Damage per tick = damagePerSecond * this, so total DPS stays the same regardless of interval.")]
    [SerializeField] private float damageTickInterval = 0.5f;

    [Header("Slow (optional)")]
    [Tooltip("Slow the player's movement while they stand in this zone (acid drag / sticky goo).")]
    public bool appliesSlow = false;
    [Tooltip("Movement-speed multiplier while inside. 1 = no slow, 0.5 = half speed.")]
    [Range(0.05f, 1f)] public float slowMultiplier = 0.5f;
    [Tooltip("How long the slow lingers after the player leaves the zone, in seconds.")]
    public float slowLinger = 0.2f;

    private float nextTickTime;

    private void OnTriggerStay2D(Collider2D other)
    {
        // ��indeki �ey Oyuncu mu?
        if (!other.CompareTag("Player")) return;

        // Protective relic (LavaBoots) skips BOTH the damage and the slow — a full immunity.
        if (RelicManager.instance != null && RelicManager.instance.HasRelic(requiredRelicID))
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // Slow refreshes every physics frame while the player is inside, so it fades a short
        // moment (slowLinger) after they step out. Cheap: it just sets a float + a timer.
        if (appliesSlow)
            player.ApplySlow(slowMultiplier, slowLinger);

        // Tick gate: TakeDamage drives sound/shake/animator, so calling it per frame
        // spammed all of those 60x/s (audit_report.md High 2.1). First contact ticks
        // immediately, then once per interval.
        if (Time.time < nextTickTime) return;
        nextTickTime = Time.time + damageTickInterval;
        player.TakeDamage(damagePerSecond * damageTickInterval);
    }
}
