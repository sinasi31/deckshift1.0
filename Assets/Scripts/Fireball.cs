using UnityEngine;

// �smi "Projectile" de�il, "Fireball" yapt�k
public class Fireball : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 15f;
    public float lifeTime = 3f;
    [Header("Efektler")]
    public GameObject explosionEffect;

    public float damage = 10f;

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

        if (other.GetComponent<Portal>() != null) return;

        EnemyHealth targetHealth = other.GetComponent<EnemyHealth>();

        if (targetHealth != null)
        {
            hasHit = true;
            targetHealth.TakeDamage(damage);
            CreateExplosionEffect();
            Destroy(gameObject);
            return;
        }
        if (other.isTrigger)
        {
            return; 
        }

        // Duvar kontrolü
        if (other.gameObject.GetComponent<PlayerController>() == null)
        {
            hasHit = true;
            CreateExplosionEffect();
            Destroy(gameObject);
        }
        if (targetHealth != null)
        {
            hasHit = true;
            targetHealth.TakeDamage(damage);
            if (CameraShake.instance != null)
                CameraShake.instance.Shake(0.15f, 0.5f);
            Destroy(gameObject);
            return;
        }

    }
    void CreateExplosionEffect()
    {
        if (explosionEffect != null)
        {
            // Efekti merminin olduğu yerde yarat
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }
    }
}