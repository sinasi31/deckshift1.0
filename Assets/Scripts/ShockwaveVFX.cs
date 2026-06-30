using System.Collections;
using UnityEngine;

// Procedural expanding-ring shockwave, built for the Glass Wail card.
// Self-contained: generates its own ring sprite in code (mirrors the project's
// EnemyHealthBar.GetWhiteSprite approach), ripples several rings outward from the
// spawn point with a fade, then destroys itself. World-space (SpriteRenderer-based),
// so spawn it with a plain Instantiate at a world position. Needs no art assets and
// is fully tunable from the Inspector.
public class ShockwaveVFX : MonoBehaviour
{
    [Header("Wave Shape")]
    [Tooltip("How many concentric rings ripple out per cast.")]
    public int ringCount = 3;
    [Tooltip("Seconds between each ring starting. Gives the pulsing wail feel.")]
    public float ringInterval = 0.12f;
    [Tooltip("Seconds for a single ring to grow from start to full radius.")]
    public float expandDuration = 0.5f;
    [Tooltip("Radius a ring starts at, in world units.")]
    public float startRadius = 0.3f;
    [Tooltip("Full radius a ring reaches, in world units.")]
    public float maxRadius = 6f;

    [Header("Look")]
    public Color color = Color.white;
    [Tooltip("Ring band thickness as a fraction of the ring (0.02 thin, 0.2 thick).")]
    [Range(0.01f, 0.4f)] public float thickness = 0.08f;

    [Header("Rendering")]
    public string sortingLayer = "Default";
    public int sortingOrder = 10;

    const int TEX_SIZE = 256;

    Texture2D ringTex;
    Sprite ringSprite;

    void Awake()
    {
        ringSprite = BuildRingSprite();
        StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        // Launch each ring on its own staggered timeline.
        for (int i = 0; i < Mathf.Max(1, ringCount); i++)
            StartCoroutine(AnimateRing(i * ringInterval));

        float totalLife = (Mathf.Max(1, ringCount) - 1) * ringInterval + expandDuration;
        yield return new WaitForSecondsRealtime(totalLife + 0.05f);
        Destroy(gameObject);
    }

    IEnumerator AnimateRing(float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        GameObject go = new GameObject("Ring");
        go.transform.SetParent(transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ringSprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;

        float t = 0f;
        while (t < expandDuration)
        {
            float n = Mathf.Clamp01(t / expandDuration);     // 0..1 normalized life
            float eased = 1f - (1f - n) * (1f - n);           // ease-out: fast then settle
            float radius = Mathf.Lerp(startRadius, maxRadius, eased);

            // Sprite is authored 1 world unit in diameter, so world scale == diameter.
            go.transform.localScale = Vector3.one * radius * 2f;

            Color c = color;
            c.a = color.a * (1f - n);                          // fade out as it expands
            sr.color = c;

            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Destroy(go);
    }

    // Builds a soft-edged ring (annulus) sprite, 1 world unit in diameter at scale 1.
    Sprite BuildRingSprite()
    {
        ringTex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        ringTex.wrapMode = TextureWrapMode.Clamp;
        ringTex.filterMode = FilterMode.Bilinear;

        float center = (TEX_SIZE - 1) * 0.5f;
        float outerR = center - 1f;                            // leave a 1px margin at the rim
        float bandPx = TEX_SIZE * thickness;
        float innerR = Mathf.Max(0f, outerR - bandPx);
        const float feather = 1.5f;                            // soft edge width in pixels

        Color32[] pixels = new Color32[TEX_SIZE * TEX_SIZE];
        for (int y = 0; y < TEX_SIZE; y++)
        {
            for (int x = 0; x < TEX_SIZE; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float aOuter = Mathf.Clamp01((outerR - d) / feather);
                float aInner = Mathf.Clamp01((d - innerR) / feather);
                float a = Mathf.Min(aOuter, aInner);

                pixels[y * TEX_SIZE + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        ringTex.SetPixels32(pixels);
        ringTex.Apply();

        return Sprite.Create(ringTex, new Rect(0, 0, TEX_SIZE, TEX_SIZE),
            new Vector2(0.5f, 0.5f), TEX_SIZE);             // pixelsPerUnit == TEX_SIZE -> 1 unit wide
    }

    void OnDestroy()
    {
        if (ringSprite != null) Destroy(ringSprite);
        if (ringTex != null) Destroy(ringTex);
    }
}
