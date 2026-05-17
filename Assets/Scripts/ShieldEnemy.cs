using UnityEngine;

public class ShieldEnemy : MonoBehaviour
{
    [Header("Ayarlar")]
    public float moveSpeed = 1.5f;
    public Transform groundCheck;
    public LayerMask groundLayer;
    public Transform hitBox; // Kalkanın çarpma alanı (Trigger)

    private Rigidbody2D rb;
    private bool isFacingRight = true;

    // Bloklama durumunu görselleştirmek için (Opsiyonel)
    private SpriteRenderer sr;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        Move();
    }

    void Move()
    {
        // Basit devriye mantığı (PatrolEnemy ile benzer)
        rb.linearVelocity = new Vector2(isFacingRight ? moveSpeed : -moveSpeed, rb.linearVelocity.y);

        // Uçurum kontrolü
        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);
        if (!isGrounded)
        {
            Flip();
        }
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    // --- KRİTİK FONKSİYON: EnemyHealth Burayı Çağıracak ---
    public bool IsBlocking(Vector2 attackerPos)
    {
        // Düşman sağa bakıyorsa ve saldıran sağındaysa -> ARKADAN YEMİŞTİR (Hasar alır)
        // Düşman sağa bakıyorsa ve saldıran solundaysa -> ÖNDEN YEMİŞTİR (Bloklar)

        float directionToAttacker = attackerPos.x - transform.position.x;

        if (isFacingRight)
        {
            // Sağa bakarken saldırgan da sağdaysa (pozitif) -> Önümdedir -> BLOK
            if (directionToAttacker > 0) return true;
        }
        else
        {
            // Sola bakarken saldırgan da soldaysa (negatif) -> Önümdedir -> BLOK
            if (directionToAttacker < 0) return true;
        }

        return false; // Bloklanmadı, hasar al.
    }

    // Oyuncuya çarpınca hasar ver
    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            other.gameObject.GetComponent<PlayerController>()?.TakeDamage(25);
            // Hulk olduğu için biraz geri de itebiliriz
            other.gameObject.GetComponent<PlayerController>()?.ApplyKnockback(new Vector2(isFacingRight ? 5 : -5, 5));
        }
    }
}