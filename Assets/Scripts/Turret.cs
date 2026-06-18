using System.Collections;
using UnityEngine;

public class Turret : MonoBehaviour
{
    [Header("Referanslar")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Ate� Ayarlar�")]
    public float fireRate = 2f;
    private Transform playerTransform;
    private EnemyHealth health;

    // Reference lookups live in Awake so they exist before OnEnable's coroutine runs.
    // The firing loop is owned exclusively by OnEnable/OnDisable — starting it here too
    // ran two overlapping loops (double fire rate), per audit_report.md High 1.5.
    void Awake()
    {
        health = GetComponent<EnemyHealth>();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }
    }

    private IEnumerator FireRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireRate);
            if (health != null && health.IsStunned) continue;
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
    // Turret.cs i�ine ekle:
    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private void OnEnable()
    {
        StartCoroutine(FireRoutine());
    }
}