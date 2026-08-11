using UnityEngine;

// The one place an enemy melee swing decides whether it connected.
//
// ⚠️ WHY THIS EXISTS. Every melee enemy used to resolve its hit as
// `Vector2.Distance(transform.position, player.position) <= attackRange + 0.5f`, i.e. a CIRCLE
// centred on the attacker's FEET tested against a single point at the player's FEET. Three things
// were wrong with that, and together they are what made combat feel unfair:
//
//   · IT REACHED BEHIND THE ENEMY. No facing was involved at all, so standing behind something
//     swinging the other way still hit you.
//   · IT LARGELY IGNORED HEIGHT. Your feet could be most of a body-length above the enemy — on a
//     ledge, or mid-jump clearly over its head — and the raw distance was still inside the circle.
//   · THE RANGE WAS SECRETLY 33% BIGGER THAN AUTHORED. The `+0.5` tolerance was applied at strike
//     time, while OnDrawGizmos drew `attackRange`. Tuning 1.5 shipped 2.0, and the editor said 1.5.
//
// And the player's carefully-placed capsule was never consulted for any of it.
//
// The replacement is an honest box in FRONT of the attacker, tested against the player's real
// collider. What you see in the gizmo is what hits you.
public static class EnemyMelee
{
    public const int PlayerLayer = 7;

    // How tall a swing reaches, as a multiple of the attacker's own reach, when a caller doesn't
    // say. Roughly a body — enough to hit a player standing on the same floor and no more.
    public const float DefaultHeight = 1.8f;

    // `dirX` is the direction the attacker committed to when the swing STARTED, not its facing at
    // the moment of impact. That is deliberate: a swing is a commitment, so a player who gets
    // behind the enemy during the wind-up should not be hit by it.
    public static bool TryHit(Transform attacker, float dirX, float reach, float damage,
                              float knockback, float height = DefaultHeight)
    {
        if (attacker == null) return false;

        Collider2D hit = Physics2D.OverlapBox(HitCentre(attacker, dirX, reach, height),
                                              new Vector2(reach, height), 0f, 1 << PlayerLayer);
        if (hit == null) return false;

        // GetComponentInParent, not GetComponent: the capsule is on the player root today, but a
        // hurtbox moved onto a child later must not silently stop registering.
        PlayerController pc = hit.GetComponentInParent<PlayerController>();
        if (pc == null) return false;

        pc.TakeDamage(damage);

        if (knockback > 0f)
        {
            // Away from the attacker, as before — but derived from the collider that was actually
            // hit rather than from a transform origin buried in the enemy's feet.
            Vector2 away = ((Vector2)hit.bounds.center - (Vector2)attacker.position).normalized;
            if (away.sqrMagnitude < 0.001f) away = new Vector2(dirX, 0.3f).normalized;
            pc.ApplyKnockback(away * knockback);
        }
        return true;
    }

    // Spans from the attacker outward to `reach`, centred on its body rather than its feet —
    // enemy transforms are grounded at floor level by the level importer.
    public static Vector2 HitCentre(Transform attacker, float dirX, float reach, float height)
    {
        return (Vector2)attacker.position + new Vector2(dirX * reach * 0.5f, height * 0.5f);
    }

    // Draw the box a swing WOULD use. Both directions, because which way it faces at the moment you
    // happen to select it in the editor tells you nothing about where it will swing.
    public static void DrawGizmo(Transform attacker, float reach, float height = DefaultHeight)
    {
        if (attacker == null) return;
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(HitCentre(attacker, 1f, reach, height), new Vector3(reach, height, 0.1f));
        Gizmos.DrawWireCube(HitCentre(attacker, -1f, reach, height), new Vector3(reach, height, 0.1f));
    }
}
