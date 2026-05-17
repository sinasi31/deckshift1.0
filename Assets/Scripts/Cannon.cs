using UnityEngine;

public class Cannon : MonoBehaviour
{
    [Header("Fýrlatma Ayarlarý")]
    public float launchForce = 20f; // Fýrlatma gücü
    public KeyCode fireKey = KeyCode.Space; // Ateþleme tuþu
    [Header("Dönme Ayarlarý")]
    public Transform barrel;
    public Transform shotPoint;
    public float rotationSpeed = 2f; // Dönme hýzý
    public float minAngle = -45f; // En sol açý
    public float maxAngle = 45f;  // En sað açý

    private bool playerInside = false;
    private PlayerController player;

    void Update()
    {
        // 1. Namluyu Sürekli Döndür (PingPong Efekti)
        // Mathf.PingPong 0 ile 1 arasýnda gidip gelir. Bunu açýlara yayýyoruz.
        if (barrel != null)
        {
            float t = Mathf.PingPong(Time.time * rotationSpeed, 1f);
            float angle = Mathf.Lerp(minAngle, maxAngle, t);
            barrel.localRotation = Quaternion.Euler(0, 0, angle);
        }

        // 2. Oyuncu Ýçindeyse Fýrlatmayý Bekle
        if (playerInside && Input.GetKeyDown(fireKey))
        {
            Shoot();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (playerInside) return; // Zaten doluysa baþkasýný alma

        if (other.CompareTag("Player"))
        {
            player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                playerInside = true;
                // Oyuncuyu namlunun içine hapset
                player.EnterCannon(barrel);
            }
        }
    }

    private void Shoot()
    {
        if (player != null)
        {
            // 1. Önce oyuncuyu namlunun ucuna ýþýnla
            if (shotPoint != null)
            {
                player.transform.position = shotPoint.position;
            }

            // 2. Yönü ShotPoint'in yönüne göre belirle
            // (Namlunun ucu nereye bakýyorsa oraya)
            Vector2 direction = shotPoint.up; // Eðer namlun yatay çizildiyse 'shotPoint.right' yap.

            player.LaunchFromCannon(direction * launchForce);

            Debug.Log("GÜLLE FIRLATILDI!");
        }

        playerInside = false;
        player = null;
    }
}