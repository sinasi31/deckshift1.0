using UnityEngine;

// Marker + trigger that makes the water it sits on swimmable.
// Place this on a Pixel Water prefab (Clear / Normal) so the player can swim in it.
// Hazard waters (Acid, Lava, Poison) use HazardZone instead and are NOT swimmable.
// Relies on the water's own trigger BoxCollider2D, so no extra collider is needed.
[RequireComponent(typeof(Collider2D))]
public class SwimZone : MonoBehaviour
{
    private Collider2D zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
    }

    // World-space Y of the water surface (top edge of the water collider).
    public float SurfaceY => zoneCollider != null ? zoneCollider.bounds.max.y : transform.position.y;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null) player.EnterWater(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null) player.ExitWater(this);
    }
}
