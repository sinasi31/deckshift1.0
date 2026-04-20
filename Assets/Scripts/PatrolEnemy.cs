using UnityEngine;
using Cainos.PixelArtMonster_Dungeon; // YENİ: Paketin kütüphanesi

public class PatrolEnemy : MonoBehaviour
{
    [Header("Görsel Bağlantı (YENİ)")]
    public PixelMonster pixelMonster; // YENİ: Yeni karakterin animasyon kodu

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
        // Otomatik bulmaya çalış
        if (pixelMonster == null) pixelMonster = GetComponentInChildren<PixelMonster>();

        rb.linearVelocity = new Vector2(moveSpeed, 0);

        // YENİ: Başlangıç yönünü yeni bedene söyle
        UpdateVisualFacing();

        // YENİ: Karaktere "Yürü" emrini ver (0 durma, 1 yürüme/koşma blendidir)
        if (pixelMonster != null) pixelMonster.MovingBlend = 1f;
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
        rb.linearVelocity = new Vector2(moveSpeed * (isFacingRight ? 1 : -1), rb.linearVelocity.y);

        // ESKİ localScale ile objeyi yamultan kodu SİLDİK
        // Onun yerine yön bilgisini PixelMonster'a iletiyoruz
        UpdateVisualFacing();
    }

    // YENİ: Sadece yeni bedeni döndüren fonksiyon
    private void UpdateVisualFacing()
    {
        if (pixelMonster != null)
        {
            if (isFacingRight)
                pixelMonster.Facing = PixelMonster.FacingType.Right;
            else
                pixelMonster.Facing = PixelMonster.FacingType.Left;
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        PlayerController player = other.gameObject.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(damage);

            // YENİ: Oyuncuya çarpınca saldırı animasyonu oynatsın
            if (pixelMonster != null) pixelMonster.Attack();
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
            rb.linearVelocity = Vector2.zero;
        }
        // Kapanırken animasyonu durdur
        if (pixelMonster != null) pixelMonster.MovingBlend = 0f;
    }

    private void OnEnable()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(moveSpeed * (isFacingRight ? 1 : -1), rb.linearVelocity.y);
        }
        // Açılırken animasyonu başlat
        if (pixelMonster != null) pixelMonster.MovingBlend = 1f;
    }
}