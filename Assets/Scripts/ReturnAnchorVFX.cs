using UnityEngine;

// The marker "Second Thoughts" leaves behind — the spot the card will snap you back to.
// House pattern: procedural, no prefab, no art (same as DashAfterimage / PhaseBoundary).
//
// It reads as a SIGIL PRESSED INTO THE GROUND rather than a floating object, because the thing it
// represents is a place, not a thing. Flattened rings plus inward-pointing chevrons: the chevrons
// converge, which is the whole idea of the card — everything here comes back to this point. That is
// deliberately the opposite of the Phase bubble, whose dashes ring a boundary you may not cross.
//
// Colour is amber, kept clear of the violet Phase bubble and the cyan portal ring, so three
// different "this card put something in the world" markers never get confused for each other.
public class ReturnAnchorVFX : MonoBehaviour
{
    private static readonly Color AnchorColor = new Color(1f, 0.72f, 0.28f, 1f);

    private const int CHEVRONS = 3;
    private const float RADIUS = 0.85f;
    private const float SPIN_SPEED = -22f;      // negative: inward-converging motion reads as "return"
    private const float GROW_TIME = 0.3f;
    private const float FADE_TIME = 0.25f;

    private SpriteRenderer ring;
    private SpriteRenderer glow;
    private Transform chevronRoot;
    private SpriteRenderer[] chevrons;

    private float age;
    private bool dismissing;
    private float dismissAge;

    private static Sprite cachedDot;
    private static Sprite cachedRing;

    public static ReturnAnchorVFX Spawn(Vector2 feetPos)
    {
        var go = new GameObject("ReturnAnchor");
        go.transform.position = new Vector3(feetPos.x, feetPos.y + 0.12f, 0f);   // just off the floor

        // Belt-and-braces: an anchor must never outlive the room that owns it. The card clears it
        // explicitly on room change, but a marker left behind would point at a place that no longer
        // exists — exactly the runtime-spawn class of bug this project has been bitten by twice.
        go.AddComponent<TemporaryObject>();

        var vfx = go.AddComponent<ReturnAnchorVFX>();
        vfx.Build();
        return vfx;
    }

    private void Build()
    {
        glow = MakeChild("Glow", GetDot(), 39);
        ring = MakeChild("Ring", GetRing(), 40);

        var cr = new GameObject("Chevrons");
        cr.transform.SetParent(transform, false);
        chevronRoot = cr.transform;

        chevrons = new SpriteRenderer[CHEVRONS];
        for (int i = 0; i < CHEVRONS; i++)
        {
            float a = (float)i / CHEVRONS * 360f;
            var go = new GameObject("Chevron" + i);
            go.transform.SetParent(chevronRoot, false);
            go.transform.localPosition = new Vector3(
                Mathf.Cos(a * Mathf.Deg2Rad) * RADIUS * 1.25f,
                Mathf.Sin(a * Mathf.Deg2Rad) * RADIUS * 1.25f * VERT_SQUASH, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, a + 90f);
            go.transform.localScale = new Vector3(0.34f, 0.11f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetDot();
            sr.color = AnchorColor;
            sr.sortingOrder = 41;
            chevrons[i] = sr;
        }
    }

    // Squashed in Y so the sigil sits ON the floor plane instead of standing up like a hoop. This is
    // a 2D side-on game, so a true circle would read as a ball floating in the air.
    private const float VERT_SQUASH = 0.42f;

    public void Dismiss()
    {
        if (dismissing) return;
        dismissing = true;
        dismissAge = 0f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;

        float scale, alpha;
        if (dismissing)
        {
            dismissAge += dt;
            float t = Mathf.Clamp01(dismissAge / FADE_TIME);
            scale = Mathf.Lerp(1f, 0.4f, t * t);      // collapses INWARD, matching the card's idea
            alpha = 1f - t;
            if (t >= 1f) { Destroy(gameObject); return; }
        }
        else
        {
            float t = Mathf.Clamp01(age / GROW_TIME);
            scale = Mathf.Lerp(0.3f, 1f, 1f - (1f - t) * (1f - t));
            alpha = t;
        }

        float breathe = 0.82f + 0.18f * Mathf.Sin(age * 2.2f);

        ring.transform.localScale = new Vector3(RADIUS * 2f * scale / 0.41f, RADIUS * 2f * scale * VERT_SQUASH / 0.41f, 1f);
        ring.color = new Color(AnchorColor.r, AnchorColor.g, AnchorColor.b, alpha * breathe);

        glow.transform.localScale = new Vector3(RADIUS * 3.2f * scale, RADIUS * 3.2f * scale * VERT_SQUASH, 1f);
        glow.color = new Color(AnchorColor.r, AnchorColor.g, AnchorColor.b, alpha * 0.10f * breathe);

        chevronRoot.localRotation = Quaternion.Euler(0f, 0f, age * SPIN_SPEED);
        chevronRoot.localScale = Vector3.one * scale;
        for (int i = 0; i < chevrons.Length; i++)
            chevrons[i].color = new Color(AnchorColor.r, AnchorColor.g, AnchorColor.b, alpha * breathe);
    }

    private SpriteRenderer MakeChild(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    // Soft radial dot, 1 world unit at scale 1.
    private static Sprite GetDot()
    {
        if (cachedDot != null) return cachedDot;
        cachedDot = BuildRadial(64, -1f, 0f);
        return cachedDot;
    }

    // Soft ring band; band sits at ~0.41 world units at scale 1 (matches the project's other rings).
    private static Sprite GetRing()
    {
        if (cachedRing != null) return cachedRing;
        cachedRing = BuildRadial(128, 0.82f, 0.14f);
        return cachedRing;
    }

    // bandCenter < 0 builds a filled dot; otherwise a ring band of the given width.
    private static Sprite BuildRadial(int size, float bandCenter, float bandWidth)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float a = bandCenter < 0f
                    ? Mathf.Clamp01(1f - d)
                    : Mathf.Clamp01(1f - Mathf.Abs(d - bandCenter) / bandWidth);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
