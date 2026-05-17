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

                    // YENİ: Kılıcı kaldırdığı an hasar hesaplama sürecini başlat
                    StartCoroutine(DealDamageRoutine());
                }
            }
            else
            {
                if (player.position.x > transform.position.x)
                    controller.inputMove.x = 1f;
                else
                    controller.inputMove.x = -1f;
            }
        }
    }

    // YENİ: Hasar Verme Süreci
    IEnumerator DealDamageRoutine()
    {
        // 1. Kılıcın havadan yere inmesi için gereken süreyi bekle
        yield return new WaitForSeconds(damageDelay);

        // 2. Bu süre içinde düşman öldüyse veya oyuncu yok olduysa iptal et
        if (player == null || controller.IsDead) yield break;

        // 3. Vuruş anında oyuncu hala menzilde mi kontrol et? (Oyuncu zekice geri kaçmış olabilir)
        float currentDistance = Vector2.Distance(transform.position, player.position);

        // +0.5f ufak bir tolerans payıdır, tam sınırdayken boşa gitmesin diye
        if (currentDistance <= attackRange + 0.5f)
        {
            // Senin PlayerController kodundaki TakeDamage ve Knockback sistemlerini çağırıyoruz
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(damage);

                // Oyuncuyu vurduğumuz yönün tersine doğru fırlat
                Vector2 knockbackDir = (player.position - transform.position).normalized;
                pc.ApplyKnockback(knockbackDir * knockbackPower);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}