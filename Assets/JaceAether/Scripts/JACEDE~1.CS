using UnityEngine;

public enum JaceDensityState { Normal, Lightened, Heavied }

[RequireComponent(typeof(Rigidbody2D))]
public class JaceDensityObject : MonoBehaviour
{
    public JaceDensityState State { get; private set; } = JaceDensityState.Normal;

    [Header("Visual Feedback")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor  = Color.white;
    public Color lightenColor = new Color(0.55f, 0.85f, 1f);
    public Color heavyColor   = new Color(1f, 0.40f, 0.15f);

    private Rigidbody2D     rb;
    private float           originalMass;
    private float           originalGravity;
    private RigidbodyType2D originalBodyType;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalMass     = rb.mass;
        originalGravity  = rb.gravityScale;
        originalBodyType = rb.bodyType;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Lighten(float mass, float gravityScale)
    {
        rb.bodyType     = RigidbodyType2D.Dynamic;
        rb.mass         = mass;
        rb.gravityScale = gravityScale;
        State = JaceDensityState.Lightened;
        SetColor(lightenColor);
    }

    public void Weigh(float mass, float gravityScale)
    {
        rb.bodyType     = RigidbodyType2D.Dynamic;
        rb.mass         = mass;
        rb.gravityScale = gravityScale;
        State = JaceDensityState.Heavied;
        SetColor(heavyColor);
    }

    public void ResetDensity()
    {
        rb.mass         = originalMass;
        rb.gravityScale = originalGravity;
        rb.bodyType     = originalBodyType;
        State = JaceDensityState.Normal;
        SetColor(normalColor);
    }

    private void SetColor(Color c)
    {
        if (spriteRenderer != null)
            spriteRenderer.color = c;
    }
}
