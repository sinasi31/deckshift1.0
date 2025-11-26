using UnityEngine;

public class PatrolEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f; // Enemy's movement speed
    public float damage = 10f; // Damage dealt to the player on collision

    [Header("Ground Check")]
    public Transform groundCheckPoint; // Point to check for the end of a platform
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer; // Which layer counts as ground

    // --- YENİ: SAĞLIK SİSTEMİ ---
    [Header("Health Settings")]
    public float maxHealth = 30f;
    private float currentHealth;
    // --- BİTİŞ ---

    private Rigidbody2D rb;
    private bool isFacingRight = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        currentHealth = maxHealth; // Canı doldur
        rb.linearVelocity = new Vector2(moveSpeed, 0); // Sağa doğru hareket et
    }

    private void Update()
    {
        bool isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);

        // If there's no ground ahead, turn around.
        if (!isGrounded)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        rb.linearVelocity = new Vector2(moveSpeed * (isFacingRight ? 1 : -1), 0);

        // Flip the sprite's scale
        Vector3 newScale = transform.localScale;
        newScale.x *= -1;
        transform.localScale = newScale;
    }

    // Oyuncuyla çarpıştığında
    private void OnCollisionEnter2D(Collision2D other)
    {
        PlayerController player = other.gameObject.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(damage);

            // TODO: Oyuncuya bir geri itme (knockback) uygulayabiliriz
            // Vector2 knockbackDirection = (player.transform.position - transform.position).normalized;
            // player.ApplyKnockback(knockbackDirection * 5f);
        }
    }

    // --- YENİ FONKSİYON: Hasar Alma ---
    /// <summary>
    /// Bu fonksiyon dışarıdan (örn: Fireball'dan) çağrılacak.
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        currentHealth -= damageAmount;
        Debug.Log($"Enemy took {damageAmount} damage, {currentHealth} HP left.");

        // TODO: Hasar alma efekti (sprite'ı kırmızı yapıp flaşör gibi yakıp söndür)

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // --- YENİ FONKSİYON: Ölüm ---
    private void Die()
    {
        Debug.Log("Enemy died!");

        // TODO: Ölüm animasyonu veya parçacık efekti oynat
        // TODO: Altın veya puan düşür

        Destroy(gameObject); // Objenin kendisini yok et
    }
    // --- BİTİŞ ---

    private void OnDrawGizmos()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
}