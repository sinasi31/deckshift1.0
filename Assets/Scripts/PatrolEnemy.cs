using UnityEngine;

public class PatrolEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float damage = 10f;

    [Header("Ground Check")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private bool isFacingRight = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // BURADA ESKİDEN CAN KODLARI VARDI, ARTIK YOK.
        rb.linearVelocity = new Vector2(moveSpeed, 0);
    }

    private void Update()
    {
        bool isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);

        if (!isGrounded)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        rb.linearVelocity = new Vector2(moveSpeed * (isFacingRight ? 1 : -1), 0);

        Vector3 newScale = transform.localScale;
        newScale.x *= -1;
        transform.localScale = newScale;
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        PlayerController player = other.gameObject.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(damage);
        }
    }

    private void OnDrawGizmos()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
    private void OnDisable()
    {
        if (rb != null)
        {
            // Sadece yatay hızı (Yürüme) sıfırla. 
            // Dikey hız (rb.linearVelocity.y) kalsın ki havadaysa yere düşebilsin.
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            
            // Eğer havada asılı kalsın (tamamen donsun) istersen:
            rb.linearVelocity = Vector2.zero; 
            // rb.gravityScale = 0; // (Ama bunu OnEnable'da geri açman gerekir)
        }
    }
}