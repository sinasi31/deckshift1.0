using UnityEngine;
using System.Collections;

public class Teleportable : MonoBehaviour
{
    // I��nland�ktan sonra tekrar ���nlanabilmek i�in bekleme s�resi
    private float teleportCooldown = 0.5f;
    private bool canTeleport = true;

    /// <summary>
    /// Bu fonksiyon Portal taraf�ndan �a�r�l�r.
    /// </summary>
    public void TeleportTo(Vector3 targetPosition)
    {
        if (!canTeleport) return;

        // 1. Pozisyonu de�i�tir
        // Both the body and the transform: Physics2D.autoSyncTransforms is OFF, so a transform-only
        // write leaves Rigidbody2D.position reporting the old spot until the next physics step —
        // and code that reads rb.position (e.g. the Phase bubble clamp) would act on the stale one.
        var body = GetComponent<Rigidbody2D>();
        if (body != null) body.position = targetPosition;
        transform.position = targetPosition;

        // Let the player know they moved without walking. A live Phase bubble is anchored in world
        // space and would otherwise drag them straight back out of the portal they just took.
        var pc = GetComponent<PlayerController>();
        if (pc != null) pc.OnTeleported();

        // 2. Cooldown ba�lat (Hemen geri ���nlanmas�n)
        StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        canTeleport = false;
        yield return new WaitForSeconds(teleportCooldown);
        canTeleport = true;
    }
}