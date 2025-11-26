using UnityEngine;

public class Spike : MonoBehaviour
{
    public float damageAmount = 10f;
    [Header("Geri Ýtme Ayarlarý")]
    public float knockbackForceX = 5f;
    public float knockbackForceY = 10f;

    private void OnTriggerEnter2D(Collider2D otherCollider)
    {
        PlayerController player = otherCollider.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(damageAmount);
            float knockbackDirectionX = Mathf.Sign(player.transform.position.x - transform.position.x);
            if (knockbackDirectionX == 0) knockbackDirectionX = 1;
            Vector2 knockbackVector = new Vector2(knockbackDirectionX * knockbackForceX, knockbackForceY);
            player.ApplyKnockback(knockbackVector);
        }
    }
}