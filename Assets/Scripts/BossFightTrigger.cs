using UnityEngine;

// Wakes a dormant MossKnightBoss when the player enters this trigger zone.
// Place an empty GameObject over the arena platform with a BoxCollider2D (Is Trigger ON)
// sized to the area that should start the fight, add this script, and assign the boss.
// One-shot: disables itself after firing so the fight can't "restart".
[RequireComponent(typeof(Collider2D))]
public class BossFightTrigger : MonoBehaviour
{
    [Tooltip("The boss this trigger wakes (assign the Moss Knight in the arena).")]
    public MossKnightBoss boss;

    void Reset()
    {
        // When the component is first added, default the collider to a trigger.
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (boss != null) boss.StartFight();
        gameObject.SetActive(false);   // one-shot
    }
}
