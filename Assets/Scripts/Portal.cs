using UnityEngine;

public class Portal : MonoBehaviour
{
    public Portal linkedPortal;
    public SpriteRenderer spriteRenderer;

    [Header("G�rsel Efektler")]
    public GameObject rangeIndicator;
    [Tooltip("E�er daire k���k kal�yorsa bu say�y� art�r (�rn: 1.1 veya 1.2)")]
    public float visualSizeMultiplier = 1.0f;
    // ---------------------------------------

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (linkedPortal == null) return;

        // Teleportable (I��nlanabilir) kontrol�
        Teleportable traveller = other.GetComponent<Teleportable>();
        if (traveller != null)
        {
            traveller.TeleportTo(linkedPortal.transform.position);
        }
    }
    // The range border is now a procedural rotating dashed ring (PortalRangeRing) built at the
    // EXACT gameplay radius — the old flat rangeIndicator sprite is kept assigned on the prefab
    // for compatibility but stays hidden.
    private PortalRangeRing rangeRing;

    public void ShowRangeCircle(float range)
    {
        if (rangeIndicator != null) rangeIndicator.SetActive(false);   // superseded by the animated ring

        if (rangeRing == null)
            rangeRing = PortalRangeRing.Spawn(transform, range);
    }

    public void HideRangeCircle()
    {
        if (rangeIndicator != null) rangeIndicator.SetActive(false);

        if (rangeRing != null)
        {
            Destroy(rangeRing.gameObject);
            rangeRing = null;
        }
    }

    public void Link(Portal otherPortal)
    {
        HideRangeCircle();
        // -----------------------------------------------------

        linkedPortal = otherPortal;
        otherPortal.linkedPortal = this;

        spriteRenderer.color = Color.cyan;
        otherPortal.spriteRenderer.color = Color.red;
    }
    private void OnDisable()
    {
        HideRangeCircle();
    }
}