using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class JaceDroneEnemy : MonoBehaviour
{
    [Header("Patrol")]
    public Transform patrolPointA;
    public Transform patrolPointB;
    public float     patrolSpeed = 2f;

    [Header("Chase")]
    public float detectionRange = 6f;
    public float chaseSpeed     = 3.5f;

    [Header("Combat")]
    public float attackRange    = 0.9f;
    public int   damageToPlayer = 1;
    public float attackCooldown = 1f;

    [Header("Health")]
    public int maxHealth = 3;

    private int            currentHealth;
    private Transform      player;
    private Rigidbody2D    rb;
    private SpriteRenderer sr;
    private bool           goingToB = true;
    private float          attackTimer;
    private bool           isDead;

    void Start()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    void Update()
    {
        if (isDead || player == null) return;
        attackTimer -= Time.deltaTime;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= detectionRange) Chase(dist);
        else                        Patrol();
    }

    void Patrol()
    {
        if (patrolPointA == null || patrolPointB == null) return;
        Transform target = goingToB ? patrolPointB : patrolPointA;
        Vector2   dir    = ((Vector2)target.position - (Vector2)transform.position).normalized;
        rb.linearVelocity = new Vector2(dir.x * patrolSpeed, rb.linearVelocity.y);
        FlipSprite(dir.x);
        if (Vector2.Distance(transform.position, target.position) < 0.3f)
            goingToB = !goingToB;
    }

    void Chase(float distToPlayer)
    {
        Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
        rb.linearVelocity = new Vector2(dir.x * chaseSpeed, rb.linearVelocity.y);
        FlipSprite(dir.x);
        if (distToPlayer <= attackRange && attackTimer <= 0f)
        {
            player.GetComponent<JacePlayerHealth>()?.TakeDamage(damageToPlayer);
            attackTimer = attackCooldown;
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;
        currentHealth -= amount;
        FlashRed();
        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        Destroy(gameObject, 0.15f);
    }

    void FlipSprite(float dirX)
    {
        if (sr == null) return;
        if (dirX >  0.05f) sr.flipX = false;
        if (dirX < -0.05f) sr.flipX = true;
    }

    void FlashRed()
    {
        if (sr == null) return;
        sr.color = Color.red;
        Invoke(nameof(ResetColor), 0.12f);
    }

    void ResetColor() { if (sr != null) sr.color = Color.white; }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
