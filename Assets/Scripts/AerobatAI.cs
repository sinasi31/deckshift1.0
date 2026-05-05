using UnityEngine;
using System.Collections;
using Cainos.PixelArtMonster_Dungeon;

public class AeroBatAI : MonoBehaviour
{
    [Header("Referanslar")]
    public PixelMonster pixelMonster; // PF Bat - Black objesini sürükle (animasyon için)

    [Header("Algılama")]
    public float detectRange = 9f;
    public LayerMask obstacleLayer;

    [Header("Hareket")]
    public float moveSpeed = 3f;
    public float diveSpeedMultiplier = 2f;

    [Header("Saldırı")]
    public float windUpTime = 0.8f;
    public float diveDuration = 1.5f;
    public float damage = 15f;
    public float knockbackForce = 5f;

    [Header("Hover")]
    public float hoverHeight = 0.3f;
    public float hoverSpeed = 2f;

    [Header("Görsel")]
    public GameObject alertIcon;

    private Rigidbody2D rb;
    private Transform player;
    private Vector3 startPos;

    private enum State { Idle, Preparing, Diving, Returning }
    private State currentState = State.Idle;

    private Vector3 diveTarget;
    private float diveTimer;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        startPos = transform.position;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        if (alertIcon != null) alertIcon.SetActive(false);
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        switch (currentState)
        {
            case State.Idle:
                IdleBehavior();
                break;
            case State.Preparing:
                rb.linearVelocity = Vector2.zero;
                break;
            case State.Diving:
                DiveBehavior();
                break;
            case State.Returning:
                ReturnBehavior();
                break;
        }
    }

    private void Update()
    {
        if (player == null) return;

        // Idle'dayken oyuncuyu kontrol et
        if (currentState == State.Idle)
        {
            CheckForPlayer();
        }

        // Yön değişimi
        if (currentState == State.Diving)
            FaceTarget(diveTarget);
        else if (currentState == State.Returning)
            FaceTarget(startPos);
        else if (player != null)
            FaceTarget(player.position);
    }

    void IdleBehavior()
    {
        // Yumuşak hover hareketi
        float hoverY = Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;
        Vector2 targetPos = (Vector2)startPos + Vector2.up * hoverY;
        rb.MovePosition(Vector2.Lerp(rb.position, targetPos, Time.fixedDeltaTime * 5f));
    }

    void CheckForPlayer()
    {
        Vector2 playerCenter = (Vector2)player.position + Vector2.up * 0.5f; // Oyuncunun göğsüne nişan al
        float dist = Vector2.Distance(transform.position, playerCenter);
        if (dist > detectRange) return;

        Vector2 dir = (playerCenter - (Vector2)transform.position).normalized;

        // Oyuncuya tam ulaşmadan biraz önce dur, ayağındaki tile'a çarpmasın
        float raycastDist = dist - 0.3f;
        if (raycastDist <= 0) raycastDist = dist;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, raycastDist, obstacleLayer);
        if (hit.collider != null) return;

        StartCoroutine(PrepareAttackRoutine());
    }

    IEnumerator PrepareAttackRoutine()
    {
        currentState = State.Preparing;
        if (alertIcon != null) alertIcon.SetActive(true);

        yield return new WaitForSeconds(windUpTime);

        if (alertIcon != null) alertIcon.SetActive(false);

        diveTarget = player.position;
        diveTimer = 0f;
        currentState = State.Diving;
    }

    void DiveBehavior()
    {
        diveTimer += Time.fixedDeltaTime;

        Vector2 dir = ((Vector2)diveTarget - rb.position).normalized;
        float speed = moveSpeed * diveSpeedMultiplier;
        rb.linearVelocity = dir * speed;

        if (Vector2.Distance(rb.position, diveTarget) < 0.5f || diveTimer > diveDuration)
        {
            currentState = State.Returning;
        }
    }

    void ReturnBehavior()
    {
        Vector2 dir = ((Vector2)startPos - rb.position).normalized;
        rb.linearVelocity = dir * moveSpeed;

        if (Vector2.Distance(rb.position, startPos) < 0.3f)
        {
            rb.linearVelocity = Vector2.zero;
            currentState = State.Idle;
        }
    }

    void FaceTarget(Vector3 target)
    {
        if (pixelMonster == null) return;

        if (target.x < transform.position.x)
            pixelMonster.Facing = PixelMonster.FacingType.Left;
        else
            pixelMonster.Facing = PixelMonster.FacingType.Right;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && currentState == State.Diving)
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(damage);
                pc.ApplyKnockback((other.transform.position - transform.position).normalized * knockbackForce);
            }
            currentState = State.Returning;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}