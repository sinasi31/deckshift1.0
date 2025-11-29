using UnityEngine;

// �smi "Projectile" de�il, "Fireball" yapt�k
public class Fireball : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 15f;
    public float lifeTime = 3f;

    public float damage = 10f; // Bu, PlayerController taraf�ndan ayarlanacak

    private Rigidbody2D rb;
    private bool hasHit = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        rb.linearVelocity = transform.right * speed;
        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        // Portal kontrolü
        if (other.GetComponent<Portal>() != null) return;

        // --- YENİ VE TEK KONTROL ---
        // Çarptığım şeyin Canı (EnemyHealth) var mı?
        EnemyHealth targetHealth = other.GetComponent<EnemyHealth>();

        if (targetHealth != null)
        {
            hasHit = true;
            targetHealth.TakeDamage(damage); // Ortak hasar fonksiyonunu çağır
            Destroy(gameObject);
            return;
        }
        // --- BİTİŞ ---

        // Duvar kontrolü
        if (other.gameObject.GetComponent<PlayerController>() == null)
        {
            hasHit = true;
            Destroy(gameObject);
        }
    }
}