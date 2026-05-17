using UnityEngine;

public class Portal : MonoBehaviour
{
    public Portal linkedPortal;
    public SpriteRenderer spriteRenderer;

    [Header("Görsel Efektler")]
    public GameObject rangeIndicator;
    [Tooltip("Eðer daire küçük kalýyorsa bu sayýyý artýr (örn: 1.1 veya 1.2)")]
    public float visualSizeMultiplier = 1.0f;
    // ---------------------------------------

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (linkedPortal == null) return;

        // Teleportable (Iþýnlanabilir) kontrolü
        Teleportable traveller = other.GetComponent<Teleportable>();
        if (traveller != null)
        {
            traveller.TeleportTo(linkedPortal.transform.position);
        }
    }
    public void ShowRangeCircle(float range)
    {
        if (rangeIndicator != null)
        {
            rangeIndicator.SetActive(true);

            float finalSize = range * 2f * visualSizeMultiplier;

            rangeIndicator.transform.localScale = new Vector3(finalSize, finalSize, 1);
        }
    }

    public void HideRangeCircle()
    {
        if (rangeIndicator != null)
        {
            rangeIndicator.SetActive(false);
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