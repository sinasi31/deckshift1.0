using System.Collections;
using UnityEngine;

public class Turret : MonoBehaviour
{
    [Header("Referanslar")]
    public GameObject projectilePrefab; // Inspector'dan Mermi prefab'ýný sürükleyeceðiz
    public Transform firePoint;         // Merminin çýkacaðý noktanýn Transform'u

    [Header("Ateþ Etme Ayarlarý")]
    public float fireRate = 2f; // Saniye cinsinden ateþ etme sýklýðý (her 2 saniyede bir)

    private Transform playerTransform; // Oyuncunun pozisyonunu takip etmek için

    void Start()
    {
        // Oyuncuyu sahnede bul ve transform'unu referans al.
        // Not: Bu yöntem sahnede sadece 1 oyuncu olduðunda güvenilirdir.
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }

        // Ateþ etme döngüsünü baþlat
        StartCoroutine(FireRoutine());
    }

    private IEnumerator FireRoutine()
    {
        // Bu döngü oyun boyunca devam edecek
        while (true)
        {
            // 'fireRate' saniye kadar bekle
            yield return new WaitForSeconds(fireRate);

            // Ateþ etme fonksiyonunu çaðýr
            Fire();
        }
    }

    private void Fire()
    {
        // Eðer oyuncu veya prefab'lar atanmamýþsa, güvenlik için fonksiyondan çýk.
        if (playerTransform == null || projectilePrefab == null || firePoint == null)
        {
            return;
        }

        // 1. Hedefi Belirle: Oyuncunun o anki pozisyonu
        Vector2 targetPosition = playerTransform.position;

        // 2. Yönü Hesapla: Hedef pozisyonundan bizim pozisyonumuzu çýkararak yön vektörünü bul.
        Vector2 fireDirection = (targetPosition - (Vector2)firePoint.position).normalized;

        // 3. Mermiyi Yarat: Mermi prefab'ýný, ateþ noktasýnda yarat.
        GameObject projectileObject = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        // 4. Mermiyi Fýrlat: Yaratýlan merminin Projectile script'ine ulaþ ve Launch fonksiyonunu çaðýr.
        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Launch(fireDirection);
        }
    }
}