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

        // --- YENİ EKLENEN KONTROL ---
        // Eğer çarptığım şey bir "Portal" ise, hiçbir şey yapma (patlama).
        // Bırak Portal script'i beni ışınlasın.
        if (other.GetComponent<Portal>() != null)
        {
            return; // Fonksiyondan çık, yok olma.
        }
        // --- BİTİŞ ---

        // 1. Bir Düşmana Çarptık mı?
        PatrolEnemy enemy = other.GetComponent<PatrolEnemy>();
        if (enemy != null)
        {
            hasHit = true;
            enemy.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // 2. Duvara/Zemine çarptık mı?
        if (other.gameObject.GetComponent<PlayerController>() == null)
        {
            hasHit = true;
            Destroy(gameObject);
        }
    }
}