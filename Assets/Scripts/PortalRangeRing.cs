using UnityEngine;

// House-style procedural range ring for portal placement (no prefab, no art — same pattern as
// DashAfterimage / CardAimIndicator). A circle of soft energy dashes that slowly rotates and
// gently pulses, marking exactly how far the second portal may be placed. Replaces the old
// flat range-circle sprite (Portal.rangeIndicator), which read as a bland static border.
public class PortalRangeRing : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 12f;    // degrees per second
    [SerializeField] private float pulseSpeed = 2.5f;
    [SerializeField] private float wavePeriod = 1.8f;      // seconds for one wave to travel center -> border
    [SerializeField] private Color ringColor = new Color(0.3f, 0.95f, 1f, 0.95f);
    [SerializeField] private int sortingOrder = 40;

    private const int DASH_COUNT = 40;

    private SpriteRenderer[] dashes;
    private SpriteRenderer wave;       // expanding ripple from the portal to the range border
    private float ringRadius;

    private static Sprite cachedDashSprite;
    private static Sprite cachedWaveSprite;

    // Builds a ring of the given world radius as a child of `parent` (the first portal).
    // Parent scale is compensated so the radius stays honest even on a scaled portal prefab.
    public static PortalRangeRing Spawn(Transform parent, float radius)
    {
        var go = new GameObject("RangeRing");
        go.transform.SetParent(parent, false);

        Vector3 ls = parent.lossyScale;
        go.transform.localScale = new Vector3(
            ls.x != 0f ? 1f / ls.x : 1f,
            ls.y != 0f ? 1f / ls.y : 1f,
            1f);

        var ring = go.AddComponent<PortalRangeRing>();
        ring.Build(radius);
        return ring;
    }

    private void Build(float radius)
    {
        ringRadius = radius;
        dashes = new SpriteRenderer[DASH_COUNT];
        Sprite sprite = GetDashSprite();

        for (int i = 0; i < DASH_COUNT; i++)
        {
            float a = (float)i / DASH_COUNT * Mathf.PI * 2f;

            var go = new GameObject("Dash" + i);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);   // long axis along the tangent
            go.transform.localScale = new Vector3(1.15f, 0.3f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = ringColor;
            dashes[i] = sr;
        }

        // The traveling wave: a soft ring that expands from the portal to the border.
        var waveGO = new GameObject("Wave");
        waveGO.transform.SetParent(transform, false);
        wave = waveGO.AddComponent<SpriteRenderer>();
        wave.sprite = GetWaveSprite();
        wave.sortingOrder = sortingOrder - 1;   // beneath the dashes
        wave.color = ringColor;
    }

    private void Update()
    {
        transform.Rotate(0f, 0f, rotationSpeed * Time.unscaledDeltaTime);

        // Shallow pulse: stays clearly visible at all times, just breathes a little.
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
        Color c = ringColor;
        c.a *= pulse;
        for (int i = 0; i < dashes.Length; i++)
            if (dashes[i] != null) dashes[i].color = c;

        // Wave: travels center -> border, fading out as it arrives.
        // The wave sprite's band sits at ~0.41 world units at scale 1.
        if (wave != null)
        {
            float t = Mathf.Repeat(Time.unscaledTime / wavePeriod, 1f);
            float r = Mathf.Max(t * ringRadius, 0.01f);
            wave.transform.localScale = Vector3.one * (r / 0.41f);
            wave.color = new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a * 0.45f * (1f - t));
        }
    }

    // Soft radial dot, stretched per-dash into a capsule-ish energy streak.
    private static Sprite GetDashSprite()
    {
        if (cachedDashSprite != null) return cachedDashSprite;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        float half = S * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float a = Mathf.Clamp01(1f - d);
                a *= a;   // soft falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        cachedDashSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return cachedDashSprite;
    }

    // Soft ring band, 128px, band radius ~0.41 world units at scale 1 (matches CardAimIndicator's ring).
    private static Sprite GetWaveSprite()
    {
        if (cachedWaveSprite != null) return cachedWaveSprite;

        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        float half = S * 0.5f;
        const float bandCenter = 0.82f;
        const float bandWidth = 0.14f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - bandCenter) / bandWidth);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        cachedWaveSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return cachedWaveSprite;
    }
}
