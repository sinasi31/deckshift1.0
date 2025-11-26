using UnityEngine;
using System.Collections;

public class Teleportable : MonoBehaviour
{
    // Iþýnlandýktan sonra tekrar ýþýnlanabilmek için bekleme süresi
    private float teleportCooldown = 0.5f;
    private bool canTeleport = true;

    /// <summary>
    /// Bu fonksiyon Portal tarafýndan çaðrýlýr.
    /// </summary>
    public void TeleportTo(Vector3 targetPosition)
    {
        if (!canTeleport) return;

        // 1. Pozisyonu deðiþtir
        transform.position = targetPosition;

        // 2. Cooldown baþlat (Hemen geri ýþýnlanmasýn)
        StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        canTeleport = false;
        yield return new WaitForSeconds(teleportCooldown);
        canTeleport = true;
    }
}