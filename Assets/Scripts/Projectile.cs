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
    void OnTriggerEnter2D(Collider2D other)
    {
        // --- YENÝ EKLENEN KISIM ---
        // Eðer çarptýðým þey bir Trigger (Pervane, Portal, Iþýk vb.) ise
        // beni yok etme, içinden geçmeme izin ver.
        if (other.isTrigger)
        {
            return;
        }
        // -------------------------

        // 1. Oyuncuya Çarptý mý?
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(damage); // Hasar ver
            Destroy(gameObject); // Mermiyi yok et
            return;
        }

        // 2. Duvara Çarptý mý?
        // (Düþmanlara çarparsa yok olmasýn, içinden geçsin diye EnemyHealth kontrolü yapabiliriz
        //  veya düþmanlar birbirini vurmasýn istiyorsan buraya EnemyHealth kontrolü de ekleyebilirsin)
        if (other.GetComponent<EnemyHealth>() == null)
        {
            Destroy(gameObject); // Duvara çarptý, yok et
        }
    }
}