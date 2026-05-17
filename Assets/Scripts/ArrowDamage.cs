using UnityEngine;

public class ArrowDamage : MonoBehaviour
{
    [Header("Ok Hasar Ayarları")]
    public float damage = 10f;
    public float knockbackPower = 3f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Eğer çarptığımız şey oyuncuysa canını yak ve ittir
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(damage);

                // Okun kendi baktığı yöne doğru oyuncuyu fırlat
                Vector2 knockbackDir = transform.right;
                if (transform.localScale.x < 0) knockbackDir *= -1; // Ters dönmüşse yönü düzelt

                pc.ApplyKnockback(knockbackDir * knockbackPower);
            }

            // Oyuncuya çarptıktan sonra oku yok et
            Destroy(gameObject);
        }
        // 2. Eğer duvara veya zemine çarparsa oku yok et (Eğer zeminlerinin tag'i farklıysa burayı güncelleyebilirsin)
        else if (other.gameObject.layer == LayerMask.NameToLayer("Ground") || other.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }
}