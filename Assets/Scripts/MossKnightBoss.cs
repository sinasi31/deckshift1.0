using UnityEngine;
using System.Collections;
using Cainos.PixelArtMonster_Dungeon;

// Act 1 boss — the Moss Knight. Attack state machine driving the Cainos MonsterController.
// Relies on EnemyHealth for HP/damage/death, so cards AND the Crusher Trap damage it for free
// (both call TakeDamage) and Glass Wail stuns it for free (EnemyHealth.IsStunned).
//
// Implemented: pursue, Acid Cleave (melee), Charge (run-across dash), Leap Slam (+ green acid
// shockwave), Lob (acid blob → lingering puddle; the slime-throw variant will reuse the same arc).
// Still to come: phase transitions, slime-spit enemy, the full acid system (flank pools → P3 rise).
[RequireComponent(typeof(EnemyHealth))]
public class MossKnightBoss : MonoBehaviour
{
    [Header("Pursuit")]
    [Tooltip("Engage range. Default arena-wide so the boss always pursues.")]
    public float aggroRange = 40f;
    [Tooltip("Stop closing in once this near (so it doesn't shove into the player).")]
    public float moveStopRange = 1.8f;

    [Header("Acid Cleave (melee)")]
    public float cleaveRange = 2.2f;
    public float cleaveCooldown = 2.0f;
    public float cleaveDamage = 15f;
    [Tooltip("Delay from swing start to the contact frame.")]
    public float cleaveDamageDelay = 0.3f;
    public float cleaveKnockback = 6f;
    public float cleaveRecover = 0.3f;

    [Header("Charge (dash across)")]
    [Tooltip("Only charge when the player is at least this far.")]
    public float chargeMinRange = 4f;
    public float chargeMaxRange = 18f;
    public float chargeCooldown = 5f;
    [Tooltip("Wind-up pause before the dash (the player's cue to dodge).")]
    public float chargeTelegraph = 0.55f;
    [Tooltip("Safety cap on dash length; he normally stops once he passes the player or hits a wall.")]
    public float chargeDuration = 1.4f;
    [Tooltip("Vulnerable pause after the dash.")]
    public float chargeRecover = 0.7f;
    public float chargeDamage = 18f;
    public float chargeKnockback = 9f;
    [Tooltip("Contact range that connects during the dash.")]
    public float chargeHitRange = 1.6f;
    [Tooltip("Dash speed — overrides the run cap during the charge. Higher = more menacing.")]
    public float chargeSpeed = 14f;
    [Tooltip("Dash acceleration — high so he launches fast instead of ramping up.")]
    public float chargeAccel = 70f;
    [Tooltip("Stop this far past the player so he doesn't run to the far wall.")]
    public float chargeOvershoot = 2f;

    [Header("Leap Slam")]
    [Tooltip("Only leap when the player is at least this far.")]
    public float leapMinRange = 4f;
    public float leapCooldown = 7f;
    [Tooltip("Crouch wind-up before launching.")]
    public float leapTelegraph = 0.5f;
    [Tooltip("Arc apex height in world units.")]
    public float leapApexHeight = 4f;
    [Tooltip("Max horizontal distance of a leap (the leap clamps to this toward the player).")]
    public float maxLeapDistance = 12f;
    [Tooltip("Safety cap on air time before forcing the slam.")]
    public float leapMaxAirTime = 3f;
    public float slamRadius = 3f;
    public float slamDamage = 22f;
    public float slamKnockback = 11f;
    [Tooltip("Vulnerable pause after landing.")]
    public float leapRecover = 0.6f;
    [Tooltip("Green acid shockwave spawned on landing (assign AcidShockwaveVFX).")]
    public GameObject slamShockwaveEffect;

    [Header("Lob (Acid Blob)")]
    [Tooltip("Acid blob projectile thrown onto the player/platform (assign AcidBlobProjectile).")]
    public GameObject lobProjectile;
    [Tooltip("On the floor, only lob when the player is at least this far.")]
    public float lobMinRange = 6f;
    [Tooltip("Never lob past this range (the throw can't reach).")]
    public float lobMaxRange = 30f;
    public float lobCooldown = 6f;
    [Tooltip("Wind-up of the throw gesture (the player's cue).")]
    public float lobTelegraph = 0.45f;
    [Tooltip("Vulnerable pause after the throw.")]
    public float lobRecover = 0.5f;
    [Tooltip("How high the blob arcs — raise this to clear tall platforms.")]
    public float lobArcHeight = 6f;
    [Tooltip("Air time of the blob — longer is easier to read and dodge.")]
    public float lobTravelTime = 0.9f;
    [Tooltip("Player counts as 'camped on a platform' (priority lob target) when this far above the boss.")]
    public float lobAboveThreshold = 2f;

    [Header("Edge Detection")]
    [Tooltip("Stop at ledges. Requires Ground Layer to be set, or it is ignored.")]
    public bool avoidLedges = false;
    public LayerMask groundLayer;
    public float edgeCheckOffsetX = 0.6f;
    public float edgeCheckDepth = 1.2f;

    private MonsterController controller;
    private PixelMonster pm;
    private EnemyHealth health;
    private Transform player;

    private bool isActing;              // true while an attack coroutine drives the inputs
    private float cleaveReadyTime;
    private float chargeReadyTime;
    private float leapReadyTime;
    private float lobReadyTime;

    void Start()
    {
        controller = GetComponent<MonsterController>();
        pm = GetComponent<PixelMonster>();
        health = GetComponent<EnemyHealth>();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        // Pass THROUGH the player instead of physically shoving them — otherwise a charge
        // just bulldozes/drags the player along. Damage + knockback are still applied by our
        // own contact checks; only the solid-body collision is ignored (triggers untouched,
        // so the player's attacks and the crusher still register).
        if (player != null)
        {
            Collider2D[] bossCols = GetComponentsInChildren<Collider2D>();
            Collider2D[] playerCols = player.GetComponentsInChildren<Collider2D>();
            foreach (var bc in bossCols)
                foreach (var pc in playerCols)
                    if (bc != null && pc != null && !bc.isTrigger && !pc.isTrigger)
                        Physics2D.IgnoreCollision(bc, pc, true);
        }

        if (controller == null)
            Debug.LogWarning("MossKnightBoss: no MonsterController found — add this to the Moss Knight (Cainos) prefab.");
    }

    void Update()
    {
        if (controller == null) return;

        // While an attack coroutine is running it owns the inputs — don't touch them.
        if (isActing) return;

        ClearInputs();

        if (player == null || controller.IsDead) return;
        if (health != null && health.IsStunned) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > aggroRange) return;

        // Choose an action by range + cooldown; otherwise close the distance.
        bool canCleave = dist <= cleaveRange && Time.time >= cleaveReadyTime;
        bool canCharge = dist >= chargeMinRange && dist <= chargeMaxRange && Time.time >= chargeReadyTime;
        bool canLeap = dist >= leapMinRange && Time.time >= leapReadyTime;
        bool playerAbove = player.position.y > transform.position.y + lobAboveThreshold;
        // Lob reaches a camped-above player at any range, or pokes a far player on the floor.
        bool canLob = lobProjectile != null && Time.time >= lobReadyTime && dist <= lobMaxRange
                      && (playerAbove || dist >= lobMinRange);

        if (canCleave)
            StartCoroutine(CleaveRoutine());
        else if (canLob && playerAbove)            // contest platforms first — rain acid on the perch
            StartCoroutine(LobRoutine());
        else if (canCharge)
            StartCoroutine(ChargeRoutine());
        else if (canLeap)
            StartCoroutine(LeapSlamRoutine());
        else if (canLob)                            // otherwise a ranged floor poke while leap/charge cool
            StartCoroutine(LobRoutine());
        else
            Pursue(dist);
    }

    private void ClearInputs()
    {
        controller.inputMove = Vector2.zero;
        controller.inputAttack = false;
        controller.inputMoveModifier = false;
    }

    private void Pursue(float dist)
    {
        if (dist <= moveStopRange) return;
        float dir = player.position.x > transform.position.x ? 1f : -1f;
        bool blockedByLedge = avoidLedges && groundLayer.value != 0 && IsLedgeAhead(dir);
        if (!blockedByLedge)
            controller.inputMove = new Vector2(dir, 0f);
    }

    // --- Acid Cleave ---
    private IEnumerator CleaveRoutine()
    {
        isActing = true;
        cleaveReadyTime = Time.time + cleaveCooldown;

        ClearInputs();
        FaceTowardPlayer();

        controller.inputAttack = true;       // trigger the Attack anim
        yield return null;                    // hold it for a full frame so the controller reads it
        controller.inputAttack = false;

        yield return new WaitForSeconds(cleaveDamageDelay);
        TryMeleeHit(cleaveRange + 0.5f, cleaveDamage, cleaveKnockback);

        yield return new WaitForSeconds(cleaveRecover);
        isActing = false;
    }

    // --- Charge ---
    private IEnumerator ChargeRoutine()
    {
        isActing = true;
        chargeReadyTime = Time.time + chargeCooldown;

        // Telegraph: face the player and wind up in place.
        float dir = player.position.x > transform.position.x ? 1f : -1f;
        ClearInputs();
        FaceDirection(dir);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.1f, 0.15f);
        yield return new WaitForSeconds(chargeTelegraph);

        // Commit the direction at the moment of launch (re-aim to where the player is now),
        // then it's locked — dodge by getting to his other side.
        if (player != null) dir = player.position.x > transform.position.x ? 1f : -1f;
        FaceDirection(dir);

        // Temporarily override the controller's run cap/accel so this is a real dash, not a jog.
        float savedMax = controller.runSpeedMax;
        float savedAcc = controller.runAcc;
        controller.runSpeedMax = chargeSpeed;
        controller.runAcc = chargeAccel;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        bool hitPlayer = false;
        float t = 0f;
        while (t < chargeDuration)
        {
            if (controller.IsDead) break;
            if (health != null && health.IsStunned) break;   // Glass Wail interrupts the charge

            controller.inputMove = new Vector2(dir, 0f);
            controller.inputMoveModifier = true;             // run
            controller.inputAttack = false;

            if (!hitPlayer && player != null &&
                Vector2.Distance(transform.position, player.position) <= chargeHitRange)
            {
                hitPlayer = true;
                controller.inputAttack = true;               // slash on impact
                PlayerController pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.TakeDamage(chargeDamage);
                    pc.ApplyKnockback(new Vector2(dir, 0.35f).normalized * chargeKnockback);
                }
            }

            // Stop once he's run past the player (with overshoot) or run into a wall.
            bool passedPlayer = player != null &&
                ((dir > 0f && transform.position.x > player.position.x + chargeOvershoot) ||
                 (dir < 0f && transform.position.x < player.position.x - chargeOvershoot));
            bool hitWall = t > 0.15f && rb != null && Mathf.Abs(rb.linearVelocity.x) < 0.5f;
            if (passedPlayer || hitWall) break;

            t += Time.deltaTime;
            yield return null;
        }

        // Restore normal movement, then the vulnerable recovery window.
        controller.runSpeedMax = savedMax;
        controller.runAcc = savedAcc;
        ClearInputs();
        yield return new WaitForSeconds(chargeRecover);
        isActing = false;
    }

    // --- Leap Slam ---
    private IEnumerator LeapSlamRoutine()
    {
        isActing = true;
        leapReadyTime = Time.time + leapCooldown;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) { isActing = false; yield break; }

        // Telegraph: crouch (jump prepare), face the player.
        ClearInputs();
        float dir = player.position.x > transform.position.x ? 1f : -1f;
        FaceDirection(dir);
        if (pm != null) pm.IsInJumpPrepare = true;
        yield return new WaitForSeconds(leapTelegraph);
        if (pm != null) pm.IsInJumpPrepare = false;

        // Aim: leap toward the player's X (clamped), landing back at floor height.
        float startY = transform.position.y;
        float targetX = player != null ? player.position.x : transform.position.x + dir * maxLeapDistance;
        float dx = Mathf.Clamp(targetX - transform.position.x, -maxLeapDistance, maxLeapDistance);
        if (Mathf.Abs(dx) > 0.01f) dir = Mathf.Sign(dx);
        FaceDirection(dir);

        // Parabolic launch velocity from the rb's own gravity.
        float g = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.1f, rb.gravityScale);
        float vy = Mathf.Sqrt(2f * g * Mathf.Max(0.5f, leapApexHeight));
        float airTime = 2f * vy / g;
        float vx = dx / Mathf.Max(0.05f, airTime);

        // Take manual control of the arc (the controller would brake the horizontal velocity).
        controller.enabled = false;
        try
        {
            rb.linearVelocity = new Vector2(vx, vy);

            float t = 0f;
            while (t < leapMaxAirTime)
            {
                if (pm != null) { pm.IsGrounded = false; pm.SpeedVertical = rb.linearVelocity.y; }

                bool descending = rb.linearVelocity.y <= 0.1f;
                bool atFloor = transform.position.y <= startY + 0.15f;
                bool stoppedVert = Mathf.Abs(rb.linearVelocity.y) < 0.05f;   // landed on ground/platform
                if (descending && t > 0.2f && (atFloor || stoppedVert)) break;

                t += Time.deltaTime;
                yield return null;
            }

            rb.linearVelocity = Vector2.zero;
            if (pm != null) { pm.IsGrounded = true; pm.SpeedVertical = 0f; }
        }
        finally
        {
            controller.enabled = true;   // always hand control back, even if interrupted
        }

        DoSlamImpact();

        yield return new WaitForSeconds(leapRecover);
        isActing = false;
    }

    private void DoSlamImpact()
    {
        if (slamShockwaveEffect != null)
            Instantiate(slamShockwaveEffect, transform.position, Quaternion.identity);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.18f, 0.5f);

        if (player != null && Vector2.Distance(transform.position, player.position) <= slamRadius)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(slamDamage);
                Vector2 kb = player.position - transform.position;
                kb.y = Mathf.Abs(kb.y) + 1f;      // bias the knockback upward
                pc.ApplyKnockback(kb.normalized * slamKnockback);
            }
        }
        // TODO (acid system): spread a temporary acid pool at the landing point.
    }

    // --- Lob (Acid Blob) ---
    // Reuses the single Attack anim as a throw gesture and arcs an acid blob onto the player's
    // spot (the platform they are camping, or the floor). The blob bursts into a lingering acid
    // puddle — see AcidBlobProjectile. This is what keeps the platforms from being a free refuge.
    private IEnumerator LobRoutine()
    {
        isActing = true;
        lobReadyTime = Time.time + lobCooldown;

        ClearInputs();
        FaceTowardPlayer();

        // Throw gesture (the Attack anim doubles as the overhead lob).
        controller.inputAttack = true;
        yield return null;
        controller.inputAttack = false;

        yield return new WaitForSeconds(lobTelegraph);   // wind-up doubles as the release cue

        if (lobProjectile != null && player != null)
        {
            Vector2 origin = (Vector2)transform.position + Vector2.up * 1.2f;   // from the hands
            Vector2 landing = player.position;                                  // where they are now (dodgeable)
            GameObject blob = Instantiate(lobProjectile, origin, Quaternion.identity);
            AcidBlobProjectile proj = blob.GetComponent<AcidBlobProjectile>();
            if (proj != null) proj.Launch(origin, landing, lobArcHeight, lobTravelTime);
        }

        yield return new WaitForSeconds(lobRecover);
        isActing = false;
    }

    // Sets facing without moving, via the PixelMonster component (MonsterController only
    // overrides facing while there is movement input, so this sticks during a wind-up).
    private void FaceDirection(float dir)
    {
        if (pm != null)
            pm.Facing = dir > 0f ? PixelMonster.FacingType.Right : PixelMonster.FacingType.Left;
    }

    private void FaceTowardPlayer()
    {
        if (player == null) return;
        FaceDirection(player.position.x > transform.position.x ? 1f : -1f);
    }

    private void TryMeleeHit(float range, float damage, float knockback)
    {
        if (player == null) return;
        if (Vector2.Distance(transform.position, player.position) > range) return;

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.TakeDamage(damage);
            Vector2 dir = (player.position - transform.position).normalized;
            pc.ApplyKnockback(dir * knockback);
        }
    }

    private bool IsLedgeAhead(float dirX)
    {
        Vector2 checkPos = (Vector2)transform.position + new Vector2(dirX * edgeCheckOffsetX, -0.1f);
        return Physics2D.Raycast(checkPos, Vector2.down, edgeCheckDepth, groundLayer).collider == null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, chargeMaxRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, cleaveRange);
    }
}
