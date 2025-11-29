using System.Collections;
using UnityEngine;

public class Turret : MonoBehaviour
{
    [Header("Referanslar")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Ateþ Ayarlarý")]
    public float fireRate = 2f;
    private Transform playerTransform;

    void Start()
    {
        // BURADA ESKÝDEN CAN KODLARI VARDI, ARTIK YOK.

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }

        StartCoroutine(FireRoutine());
    }

    private IEnumerator FireRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireRate);
            Fire();
        }
    }

    private void Fire()
    {
        if (playerTransform == null || projectilePrefab == null || firePoint == null) return;

        Vector2 targetPosition = playerTransform.position;
        Vector2 fireDirection = (targetPosition - (Vector2)firePoint.position).normalized;

        GameObject projectileObject = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Launch(fireDirection);
        }
    }
}