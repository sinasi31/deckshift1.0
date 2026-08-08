using UnityEngine;
using Cainos.PixelArtMonster_Dungeon;

public class RangedEnemyAI : MonoBehaviour
{
    [Header("Yapay Zeka Ayarları")]
    public float aggroRange = 10f;
    public float attackRange = 7f;
    public float fireRate = 2.5f;

    [Header("Nişan Ayarları (YENİ)")]
    [Tooltip("Oyuncu okçudan en fazla ne kadar yukarıda/aşağıda olursa ateş etsin? (Y ekseni farkı)")]
    public float yTolerance = 1.5f;

    [Header("Ses")]
    // Played when the archer fires. PlayClipAtPoint requires no AudioSource component.
    [SerializeField] private AudioClip shootSound;
    [SerializeField, Range(0f, 1f)] private float shootVolume = 1f;

    private MonsterController controller;
    private EnemyHealth health;
    private PixelMonster pm; // Yüzünü dönmesi için eklendi
    private Transform player;
    private float lastAttackTime;

    void Start()
    {
        controller = GetComponent<MonsterController>();
        health = GetComponent<EnemyHealth>();
        pm = GetComponent<PixelMonster>();

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
        float yDifference = Mathf.Abs(player.position.y - transform.position.y); // Yükseklik farkı

        if (distance < aggroRange)
        {
            // YENİ: Motor dursa bile yüzünü her karede oyuncuya dön (Arkaya sıkma sorununu çözer)
            if (player.position.x > transform.position.x)
                pm.Facing = PixelMonster.FacingType.Right;
            else
                pm.Facing = PixelMonster.FacingType.Left;

            // MENZİLE GİRDİ Mİ?
            if (distance <= attackRange)
            {
                controller.inputMove.x = 0f; // Yürümeyi kes

                // YENİ: Yükseklik olarak oyuncuyla aynı hizada mıyız? (Boşa sıkmaması için)
                if (yDifference <= yTolerance)
                {
                    if (Time.time >= lastAttackTime + fireRate)
                    {
                        controller.inputAttack = true;
                        lastAttackTime = Time.time;

                        // Play the bow-shot sound when the archer fires.
                        SfxManager.PlayAtPoint(shootSound, transform.position, shootVolume);
                    }
                }
                // (Eğer menzilde ama yukarıdaysa sadece bekler ve oyuncuya bakar)
            }
            else
            {
                // MENZİLDE DEĞİL, YAKLAŞ!
                if (player.position.x > transform.position.x)
                    controller.inputMove.x = 1f;
                else
                    controller.inputMove.x = -1f;
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