using UnityEngine;

// House-style procedural visual for the Zombie Spitter's projectile (no art — the sprite is
// generated at runtime, same pattern as EnemyHealthBar / CardAimIndicator). Replaces the old
// placeholder that reused the turret's red bolt (Mermi, still used by the turret). Gives the
// glob a wet acid-green blob look, a squash-stretch wobble along its flight, and a tapering
// goo trail. The actual movement/damage still lives on the sibling Projectile component.
[RequireComponent(typeof(SpriteRenderer))]
public class SpitGlob : MonoBehaviour
{
    [SerializeField] private Color gooCore = new Color(0.45f, 0.75f, 0.12f, 1f);   // acid green
    [SerializeField] private Color gooRim = new Color(0.62f, 0.95f, 0.25f, 1f);    // brighter edge
    [SerializeField] private float wobbleAmount = 0.14f;   // squash/stretch magnitude
    [SerializeField] private float wobbleSpeed = 18f;

    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private Vector3 baseScale;

    private static Sprite cachedGooSprite;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        baseScale = transform.localScale;

        sr.sprite = GetGooSprite();
        sr.color = Color.white;   // color lives in the baked sprite
        BuildTrail();
    }

    private void Update()
    {
        // Gentle squash-stretch: stretch along travel, squash across it — reads as a wet glob.
        float w = Mathf.Sin(Time.time * wobbleSpeed) * wobbleAmount;
        Vector3 s = baseScale;
        s.x *= 1f + w;
        s.y *= 1f - w;
        transform.localScale = s;

        // Face travel direction so the stretch axis follows the arc.
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            float ang = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }
    }

    // A tapering green goo streak behind the glob.
    private void BuildTrail()
    {
        var trailGO = new GameObject("GooTrail");
        trailGO.transform.SetParent(transform, false);

        var trail = trailGO.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.startWidth = 0.32f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.03f;
        trail.autodestruct = false;
        trail.numCapVertices = 4;
        trail.sortingOrder = sr.sortingOrder - 1;
        trail.material = new Material(Shader.Find("Sprites/Default"));

        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(gooRim, 0f), new GradientColorKey(gooCore, 1f) },
            new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;
    }

    // Wet acid-green blob: irregular gooey rim + a bright wet highlight.
    private static Sprite GetGooSprite()
    {
        if (cachedGooSprite != null) return cachedGooSprite;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        Color core = new Color(0.45f, 0.75f, 0.12f, 1f);
        Color rim = new Color(0.62f, 0.95f, 0.25f, 1f);
        Color highlight = new Color(0.85f, 1f, 0.6f, 1f);

        float half = S * 0.5f;
        var hlCenter = new Vector2(half * 0.72f, half * 1.28f);   // upper-ish wet spot

        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 d = p - new Vector2(half, half);
                float dist = d.magnitude / half;

                // Low-frequency wobble on the edge so the blob isn't a perfect circle.
                float ang = Mathf.Atan2(d.y, d.x);
                float wobble = 0.06f * Mathf.Sin(ang * 5f) + 0.04f * Mathf.Sin(ang * 8f + 1.3f);
                float edge = 0.82f + wobble;

                float a = Mathf.Clamp01((edge - dist) / 0.18f);   // soft gooey falloff
                if (a <= 0f) { tex.SetPixel(x, y, new Color(0, 0, 0, 0)); continue; }

                // Core->rim gradient (brighter toward the edge).
                Color c = Color.Lerp(core, rim, Mathf.Clamp01(dist / edge));

                // Wet highlight.
                float hl = Mathf.Clamp01(1f - Vector2.Distance(p, hlCenter) / (half * 0.55f));
                c = Color.Lerp(c, highlight, hl * hl * 0.8f);

                c.a = a;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();

        cachedGooSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return cachedGooSprite;
    }
}
