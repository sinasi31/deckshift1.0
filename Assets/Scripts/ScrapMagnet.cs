using UnityEngine;

// Scrap Magnet (relic): while owned, nearby pickups drift toward the player and speed up as
// they close in. Implemented as a shared static pull so both GoldPickup and ShiftCrystal (and
// any future pickup) can opt in with a single Update line — no per-prefab wiring, works on
// level-imported pickups too. Pause-safe: Time.deltaTime is 0 while paused, so nothing moves.
public static class ScrapMagnet
{
    public const string RelicID = "ScrapMagnet";

    // ⚠️ RANGE IS THE WHOLE BALANCE OF THIS RELIC — keep it short. At 6 units it reached most of a
    // screen height and hoovered up loot the player had not gone anywhere near, which deletes the
    // "reaching the gold costs Shift" trade the level design is built on (see the room law in
    // CLAUDE.md). At 3 it is a convenience — you still have to get to the loot, you just stop
    // fumbling for the last pixel of overlap — which is what a Common relic should be.
    public const float Range = 3f;          // world units within which a pickup is pulled
    public const float MinSpeed = 3f;       // pull speed at the edge of range
    public const float MaxSpeed = 12f;      // pull speed right next to the player

    // Drifts the given pickup toward the player when the relic is owned and the player is in
    // range. No-op otherwise. The pickup's own OnTriggerEnter2D still handles actual collection.
    public static void Attract(Transform pickup)
    {
        if (RelicManager.instance == null || !RelicManager.instance.HasRelic(RelicID)) return;

        PlayerController player = GameManager.instance != null ? GameManager.instance.player : null;
        if (player == null) return;

        Vector3 target = player.transform.position;
        float dist = Vector2.Distance(pickup.position, target);
        if (dist > Range) return;

        // Ease in: faster the closer it gets, so it snaps into the player at the end.
        float speed = Mathf.Lerp(MaxSpeed, MinSpeed, dist / Range);
        pickup.position = Vector3.MoveTowards(pickup.position, target, speed * Time.deltaTime);
    }
}
