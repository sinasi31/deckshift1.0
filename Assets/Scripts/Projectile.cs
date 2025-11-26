using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f; // Merminin hýzý
    public float lifeTime = 3f; // Merminin sahnede kalacaðý süre
    public float damage = 10f;  // Merminin vereceði hasar

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        // Yaratýldýktan 'lifeTime' saniye sonra kendini yok et
        Destroy(gameObject, lifeTime);
    }

    // Bu fonksiyon, mermiyi belirli bir yöne doðru fýrlatmak için dýþarýdan çaðrýlacak
    public void Launch(Vector2 direction)
    {
        rb.linearVelocity = direction.normalized * speed;
    }

    // Bir þeye çarptýðýnda...
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Çarptýðý þey oyuncu mu?
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            // Oyuncuya hasar ver
            player.TakeDamage(damage);
        }

        // Mermi, oyuncuya veya baþka bir þeye (duvar gibi) çarptýðýnda kendini yok et.
        // Not: 'Ground' katmanýna sahip duvarlar gibi þeylerin de bir Collider'ý olmalý.
        Destroy(gameObject);
    }
}