using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Boss-room environmental weapon: a hanging press activated by a Lever.
// Slams down, crushes anything in the impact zone (enemies AND the player),
// holds, winches back up, then needs a rearm period before it can fire again.
// Wire Lever.OnFlippedOn / OnFlippedOff to Activate() — the lever is a toggle,
// so both directions should trigger a slam.
public class CrusherTrap : MonoBehaviour
{
    [Header("Parts (assigned by setup)")]
    [SerializeField] private Rigidbody2D pressHead;          // kinematic body that moves
    [SerializeField] private SpriteRenderer chainRenderer;   // tiled sprite stretched between anchor and head
    [SerializeField] private Transform chainAnchor;          // fixed point at the ceiling

    [Header("Motion")]
    [SerializeField] private float travelDistance = 16f;     // how far the head descends from idle
    [SerializeField] private float slamSpeed = 40f;
    [SerializeField] private float raiseSpeed = 3.5f;
    [SerializeField] private float holdTime = 0.7f;          // stays down before winching up
    [SerializeField] private float rearmTime = 6f;           // extra cooldown after fully raised

    [Header("Damage")]
    [SerializeField] private float crushDamage = 80f;
    [SerializeField] private float playerDamage = 20f;
    [SerializeField] private Vector2 damagePadding = new Vector2(0.3f, 0.6f); // extends impact box below/around head

    [Header("Feedback")]
    [SerializeField] private AudioClip slamSound;
    [SerializeField] private float shakeDuration = 0.25f;
    [SerializeField] private float shakeIntensity = 0.35f;

    private Vector2 idleHeadPos;   // world position cached at Start
    private bool isBusy;           // mid-slam or winching up
    private bool isArmed = true;

    public bool IsReady => isArmed && !isBusy;

    private void Start()
    {
        if (pressHead != null) idleHeadPos = pressHead.position;
    }

    // Called by the Lever's UnityEvents. Ignores the pull if the press isn't rearmed yet.
    public void Activate()
    {
        if (!IsReady || pressHead == null) return;
        StartCoroutine(SlamRoutine());
    }

    private IEnumerator SlamRoutine()
    {
        isBusy = true;
        isArmed = false;

        Vector2 target = idleHeadPos + Vector2.down * travelDistance;

        // Slam down fast.
        while (pressHead.position.y > target.y + 0.01f)
        {
            float step = slamSpeed * Time.fixedDeltaTime;
            Vector2 next = Vector2.MoveTowards(pressHead.position, target, step);
            pressHead.MovePosition(next);
            yield return new WaitForFixedUpdate();
        }

        ApplyImpactDamage();

        if (CameraShake.instance != null) CameraShake.instance.Shake(shakeDuration, shakeIntensity);
        if (slamSound != null) AudioSource.PlayClipAtPoint(slamSound, pressHead.position);

        yield return new WaitForSeconds(holdTime);

        // Winch back up slowly — this is the window where the kill zone is safe.
        while (pressHead.position.y < idleHeadPos.y - 0.01f)
        {
            float step = raiseSpeed * Time.fixedDeltaTime;
            Vector2 next = Vector2.MoveTowards(pressHead.position, idleHeadPos, step);
            pressHead.MovePosition(next);
            yield return new WaitForFixedUpdate();
        }

        isBusy = false;
        yield return new WaitForSeconds(rearmTime);
        isArmed = true;
    }

    private void ApplyImpactDamage()
    {
        Collider2D headCol = pressHead.GetComponent<Collider2D>();
        if (headCol == null) return;

        Bounds b = headCol.bounds;
        Vector2 center = new Vector2(b.center.x, b.min.y);
        Vector2 size = new Vector2(b.size.x + damagePadding.x * 2f, damagePadding.y * 2f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        var damagedEnemies = new HashSet<EnemyHealth>();
        bool playerHit = false;

        foreach (Collider2D hit in hits)
        {
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy != null && damagedEnemies.Add(enemy))
            {
                enemy.TakeDamage(crushDamage);
                continue;
            }

            PlayerController player = hit.GetComponentInParent<PlayerController>();
            if (player != null && !playerHit)
            {
                playerHit = true;
                player.TakeDamage(playerDamage);
            }
        }
    }

    // Keep the chain sprite stretched between the anchor and the head.
    private void LateUpdate()
    {
        if (chainRenderer == null || chainAnchor == null || pressHead == null) return;

        float top = chainAnchor.position.y;
        float bottom = pressHead.position.y;
        float length = Mathf.Max(0.01f, top - bottom);

        chainRenderer.size = new Vector2(chainRenderer.size.x, length);
        chainRenderer.transform.position = new Vector3(chainAnchor.position.x, bottom + length * 0.5f, chainRenderer.transform.position.z);
    }
}
