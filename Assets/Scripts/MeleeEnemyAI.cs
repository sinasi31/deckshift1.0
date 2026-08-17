using UnityEngine;
using System.Collections; // Coroutine (bekletme) için eklendi
using Cainos.PixelArtMonster_Dungeon;

public class MeleeEnemyAI : MonoBehaviour
{
    [Header("Yapay Zeka Ayarları")]
    public float aggroRange = 8f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1.5f;

    [Header("Hasar Ayarları (YENİ)")]
    public float damage = 15f;           // Vurduğunda kaç can gidecek?
    public float damageDelay = 0.3f;     // Animasyon başladıktan kaç saniye sonra kılıç yere iniyor?
    public float knockbackPower = 5f;    // Vurunca oyuncuyu ne kadar geri itecek?
    [Tooltip("How tall the swing reaches. Roughly one body — a player clearly above this is missed.")]
    public float attackHeight = EnemyMelee.DefaultHeight;

    [Header("Edge Detection")]
    public LayerMask groundLayer;
    public float edgeCheckOffsetX = 0.5f;
    public float edgeCheckDepth = 1f;

    [Header("Ses")]
    // Played at the start of each swing. PlayClipAtPoint keeps it working even if
    // the enemy is destroyed mid-swing (no AudioSource component required).
    [SerializeField] private AudioClip attackSound;
    [SerializeField, Range(0f, 1f)] private float attackVolume = 1f;

    private MonsterController controller;
    private EnemyHealth health;
    private Transform player;
    private float lastAttackTime;

    void Start()
    {
        controller = GetComponent<MonsterController>();
        health = GetComponent<EnemyHealth>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (player == null || controller == null || controller.IsDead)
        {
            if (controller != null) controller.inputMove = Vector2.zero;
            return;
        }

        controller.inputMove = Vector2.zero;
        controller.inputAttack = false;

        if (health != null && health.IsStunned) return;

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance < aggroRange)
        {
            if (distance <= attackRange)
            {
                controller.inputMove.x = 0f;

                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    controller.inputAttack = true;
                    lastAttackTime = Time.time;

                    // Play the swing sound at the moment the attack starts.
                    SfxManager.PlayAtPoint(attackSound, transform.position, attackVolume);

                    // The swing commits to a direction NOW. See EnemyMelee.TryHit.
                    StartCoroutine(DealDamageRoutine(player.position.x >= transform.position.x ? 1f : -1f));
                }
            }
            else
            {
                float dir = player.position.x > transform.position.x ? 1f : -1f;
                if (!IsEdgeAhead(dir))
                    controller.inputMove.x = dir;
            }
        }
    }

    // Hasar Verme Süreci — the swing lands `damageDelay` after it starts.
    IEnumerator DealDamageRoutine(float swingDir)
    {
        yield return new WaitForSeconds(damageDelay);
        if (player == null || controller.IsDead) yield break;

        // One honest box in front of the attacker, tested against the player's real collider.
        // The old check was a circle around this enemy's FEET with a hidden +0.5 on the range —
        // it hit through the enemy's back and from a body-length overhead. See EnemyMelee.
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

        // The box that actually decides the hit, not a circle that only approximates it.
        EnemyMelee.DrawGizmo(transform, attackRange, attackHeight);
    }
}