using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 3f;
    public float damage = 10f;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public void Launch(Vector2 direction)
    {
        rb.linearVelocity = direction.normalized * speed;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.isTrigger) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(damage); // Hasar ver
            Destroy(gameObject); // Mermiyi yok et
            return;
        }

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target is EnemyHealth) return; // friendly fire — pass through
        if (target != null) target.TakeDamage(damage);
        Destroy(gameObject);
    }
}
