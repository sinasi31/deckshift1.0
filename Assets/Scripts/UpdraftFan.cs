using UnityEngine;

public class UpdraftFan : MonoBehaviour
{
    [Header("Ayarlar")]
    public float liftForce = 20f; // Kaldýrma kuvveti (Inspector'dan ayarla)
    public float maxUpwardSpeed = 10f; // Çok fazla hýzlanýp fýrlamayý engellemek için limit

    // Bir þey trigger alanýnýn içinde durduðu sürece çalýþýr
    private void OnTriggerStay2D(Collider2D other)
    {
        Rigidbody2D rb = other.attachedRigidbody;

        // Sadece Rigidbody'si olan objeleri (Oyuncu, Düþman) iter
        if (rb != null)
        {
            // Eðer dikey hýzý limitin altýndaysa itmeye devam et
            if (rb.linearVelocity.y < maxUpwardSpeed)
            {
                // Yukarý doðru kuvvet uygula
                rb.AddForce(Vector2.up * liftForce);
            }
        }
    }
}