using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class JaceSizeObject : MonoBehaviour
{
    [Header("Size Limits")]
    public float minScale = 0.3f;
    public float maxScale = 3f;

    [Header("Visual Feedback")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.white;
    public Color shrinkColor = new Color(0.65f, 1f, 0.65f);
    public Color growColor   = new Color(1f, 0.70f, 0.35f);

    public float CurrentScale { get; private set; } = 1f;

    private Vector3      originalScale;
    private float        originalMass;
    private Rigidbody2D  rb;

    void Awake()
    {
        rb            = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
        originalMass  = rb.mass;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Grow(float amount)
    {
        CurrentScale = Mathf.Min(CurrentScale + amount, maxScale);
        Apply();
    }

    public void Shrink(float amount)
    {
        CurrentScale = Mathf.Max(CurrentScale - amount, minScale);
        Apply();
    }

    public void ResetSize()
    {
        CurrentScale = 1f;
        Apply();
    }

    void Apply()
    {
        transform.localScale = originalScale * CurrentScale;

        // Scale mass with cross-sectional area so bigger = heavier
        rb.mass = originalMass * (CurrentScale * CurrentScale);

        if (spriteRenderer == null) return;
        if      (CurrentScale > 1.05f) spriteRenderer.color = growColor;
        else if (CurrentScale < 0.95f) spriteRenderer.color = shrinkColor;
        else                           spriteRenderer.color = normalColor;
    }
}
