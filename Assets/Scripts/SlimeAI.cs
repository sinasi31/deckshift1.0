using UnityEngine;
using System.Collections;
using Cainos.PixelArtMonster_Dungeon;

public class SlimeAI : MonoBehaviour
{
    [Header("AI Settings")]
    public float aggroRange = 6f;
    public float attackRange = 1.2f;
    public float attackCooldown = 1.2f;

    [Header("Damage Settings")]
    public float damage = 10f;
    public float damageDelay = 0.2f;
    public float knockbackPower = 4f;
    [Tooltip("How tall the lunge reaches. A slime is short, so this is lower than a humanoid's.")]
    public float attackHeight = 1.2f;

    [Header("Idle Patrol")]
    public float patrolFlipInterval = 2.5f;

    [Header("Edge Detection")]
    public LayerMask groundLayer;
    public float edgeCheckOffsetX = 0.5f;
    public float edgeCheckDepth = 1f;

    [Header("Ses")]
    // Played at the start of each attack. PlayClipAtPoint survives enemy destroy.
    [SerializeField] private AudioClip attackSound;
    [SerializeField, Range(0f, 1f)] private float attackVolume = 1f;

    private MonsterController controller;
    private EnemyHealth health;
    private Transform player;
    private float lastAttackTime;
    private float patrolTimer;
    private float patrolDir = 1f;

    void Start()
    {
        controller = GetComponent<MonsterController>();
        health = GetComponent<EnemyHealth>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        patrolTimer = patrolFlipInterval;
    }

    void Update()
    {
        if (controller == null || controller.IsDead)
        {
            if (controller != null) controller.inputMove = Vector2.zero;
            return;
        }

        controller.inputMove = Vector2.zero;
        controller.inputJump = false;
        controller.inputAttack = false;

        if (health != null && health.IsStunned) return;

        if (player == null)
        {
            Patrol();
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance < aggroRange)
        {
            if (distance > attackRange)
            {
                float dir = player.position.x > transform.position.x ? 1f : -1f;
                if (!IsEdgeAhead(dir))
                    controller.inputMove.x = dir;
            }
            else
            {
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    controller.inputAttack = true;
                    lastAttackTime = Time.time;

                    // Play the attack sound at the moment the attack starts.
                    SfxManager.PlayAtPoint(attackSound, transform.position, attackVolume);

                    StartCoroutine(DealDamageRoutine(player.position.x >= transform.position.x ? 1f : -1f));
                }
            }
        }
        else
        {
            Patrol();
        }
    }

    void Patrol()
    {
        patrolTimer -= Time.deltaTime;
        if (patrolTimer <= 0f || IsEdgeAhead(patrolDir))
        {
            patrolDir = -patrolDir;
            patrolTimer = patrolFlipInterval;
        }
        controller.inputMove.x = patrolDir;
    }

    IEnumerator DealDamageRoutine(float swingDir)
    {
        yield return new WaitForSeconds(damageDelay);
        if (player == null || controller.IsDead) yield break;

        // A box in front, not a circle around the slime's feet with a hidden +0.5. See EnemyMelee.
        EnemyMelee.TryHit(transform, swingDir, attackRange, damage, knockbackPower, attackHeight);
    }

    private bool IsEdgeAhead(float dirX)
    {
        Vector2 checkPos = (Vector2)transform.position + new Vector2(dirX * edgeCheckOffsetX, -0.1f);
        return Physics2D.Raycast(checkPos, Vector2.down, edgeCheckDepth, groundLayer).collider == null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        EnemyMelee.DrawGizmo(transform, attackRange, attackHeight);
    }
}
