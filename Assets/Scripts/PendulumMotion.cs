using UnityEngine;

public class PendulumMotion : MonoBehaviour
{
    [Header("Sallanma Ayarlarý")]
    public float speed = 1.5f;      // Ne kadar hýzlý sallansýn?
    public float maxAngle = 75f;    // Saða/Sola kaç derece açýlsýn?
    public float startOffset = 0f;  // Hepsi ayný anda sallanmasýn diye zaman farký

    void Update()
    {
        // Matematiðin gücü: Sinüs dalgasý -1 ile 1 arasýnda gider gelir.
        // Bunu açýyla çarparsanýz kusursuz bir sarkaç hareketi elde edersiniz.
        float angle = maxAngle * Mathf.Sin((Time.time + startOffset) * speed);

        // Bu objeyi (ChainHolder) hesaplanan açýya döndür
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}