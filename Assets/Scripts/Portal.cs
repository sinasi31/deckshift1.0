using UnityEngine;

public class Portal : MonoBehaviour
{
    public Portal linkedPortal;
    public SpriteRenderer spriteRenderer;

    // Artýk "CanTeleport" mantýðý burada deðil, objenin kendisinde (Teleportable.cs)

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Baðlý portal yoksa iþlem yapma
        if (linkedPortal == null) return;

        // --- SAÐLAM KOD KISMI ---
        // Çarpan objenin üzerinde "Teleportable" bileþeni var mý?
        Teleportable traveller = other.GetComponent<Teleportable>();

        if (traveller != null)
        {
            // Varsa, onu diðer portalýn konumuna gönder
            traveller.TeleportTo(linkedPortal.transform.position);
        }
    }

    public void Link(Portal otherPortal)
    {
        linkedPortal = otherPortal;
        otherPortal.linkedPortal = this;

        spriteRenderer.color = Color.cyan;
        otherPortal.spriteRenderer.color = Color.red;
    }
}