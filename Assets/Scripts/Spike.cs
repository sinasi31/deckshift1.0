using UnityEngine;

public class Spike : MonoBehaviour
{
    public float damageAmount = 10f;
    [SerializeField] internal float knockbackMultiplier = 1.2f;
    [SerializeField] internal float minimumKnockbackForce = 8f;

    private void OnTriggerEnter2D(Collider2D otherCollider)
    {
        PlayerController player = otherCollider.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(damageAmount);

            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            Vector2 incoming = playerRb != null ? playerRb.linearVelocity : Vector2.zero;
            // transform.up is the spike's outward normal — correct for floor, ceiling, and wall spikes
            Vector2 reflected = Vector2.Reflect(incoming, (Vector2)transform.up) * knockbackMultiplier;

            if (reflected.magnitude < minimumKnockbackForce)
                reflected = (Vector2)transform.up * minimumKnockbackForce;

            player.ApplyKnockback(reflected);
        }
    }
}
