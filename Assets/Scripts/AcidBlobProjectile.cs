using UnityEngine;
using System.Collections;

// A lobbed acid blob — the Moss Knight's "Lob" attack (acid variant). It arcs from the knight
// onto the player's position (or the platform they are camping), then bursts into a short-lived
// acid puddle that damages the player on contact. The puddle reuses HazardZone, so the LavaBoots
// relic still protects against it, exactly like every other acid in the game.
//
// All visuals are generated procedurally (a green blob in flight, a flat puddle on landing) — no
// art assets required. The knight throws it via LobRoutine in MossKnightBoss.
public class AcidBlobProjectile : MonoBehaviour
{
    [Header("Blob visual")]
    public Color blobColor = new Color(0.45f, 0.95f, 0.25f, 1f);
    public float blobRadius = 0.35f;

    [Header("Splash / puddle")]
    [Tooltip("Green shockwave spawned on landing (assign AcidShockwaveVFX).")]
    public GameObject splashEffect;
    [Tooltip("How long the acid puddle lingers after landing.")]
    public float patchDuration = 4f;
    [Tooltip("Damage per second the puddle deals to the player (same model as floor acid).")]
    public float patchDamagePerSecond = 10f;
    [Tooltip("Half-width of the acid puddle in world units.")]
    public float patchRadius = 1.2f;
    [Tooltip("Slow the player while they stand on the sticky goo puddle.")]
    public bool patchSlows = true;
    [Tooltip("Movement-speed multiplier while on the puddle. 1 = no slow, 0.5 = half speed.")]
    [Range(0.05f, 1f)] public float patchSlowMultiplier = 0.5f;

    [Header("Sprites (optional — real art beats the procedural fallback)")]
    [Tooltip("In-flight blob sprite (e.g. Slime Gel). Procedural circle if empty.")]
    public Sprite blobSprite;
    [Tooltip("Puddle splat sprite (e.g. Slime Gel). Procedural pool if empty.")]
    public Sprite puddleSprite;

    [Header("Payload (optional)")]
    [Tooltip("If set, spawned at the landing point instead of/with the puddle (e.g. a Slime add). Set by the thrower.")]
    public GameObject landPayload;
    [Tooltip("Leave a lingering acid puddle on landing. Turned off for slime throws.")]
    public bool createPuddle = true;

    private Vector2 start;
    private Vector2 target;
    private float arcHeight;
    private float travelTime;
    private float u;             // 0..1 progress along the arc
    private bool launched;
    private bool landed;

    private SpriteRenderer blobSr;
    private static Sprite circleSprite;

    void Awake()
    {
        // Flying-blob visual — a real gel sprite if provided, else a procedural orb.
        blobSr = gameObject.AddComponent<SpriteRenderer>();
        bool hasSprite = blobSprite != null;
        blobSr.sprite = hasSprite ? blobSprite : GetCircleSprite();
        blobSr.color = hasSprite ? Color.white : blobColor;
        blobSr.sortingLayerName = "Default";
        blobSr.sortingOrder = 11;
        transform.localScale = Vector3.one * (blobRadius * 2f);
    }

    // Called by the boss right after the throw gesture. Drives a clean parabola from -> to.
    public void Launch(Vector2 from, Vector2 to, float arc, float time)
    {
        start = from;
        target = to;
        arcHeight = Mathf.Max(0.5f, arc);
        travelTime = Mathf.Max(0.1f, time);
        u = 0f;
        launched = true;
        landed = false;
        transform.position = from;
    }

    void Update()
    {
        if (!launched || landed) return;

        u += Time.deltaTime / travelTime;
        float k = Mathf.Clamp01(u);
        float x = Mathf.Lerp(start.x, target.x, k);
        float baseY = Mathf.Lerp(start.y, target.y, k);
        float lift = arcHeight * 4f * k * (1f - k);   // parabola: 0 at both ends, peak at the middle
        transform.position = new Vector3(x, baseY + lift, 0f);
        transform.Rotate(0f, 0f, -150f * Time.deltaTime);   // a slow tumble for life

        if (u >= 1f) Land();
    }

    private void Land()
    {
        landed = true;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;   // reset so a child puddle scales cleanly
        blobSr.enabled = false;               // hide the flying orb

        if (splashEffect != null)
            Instantiate(splashEffect, transform.position, Quaternion.identity);

        // Optional payload (e.g. a lobbed Slime add) drops in at the landing point.
        if (landPayload != null)
            Instantiate(landPayload, transform.position + Vector3.up * 0.3f, Quaternion.identity);

        if (!createPuddle)
        {
            Destroy(gameObject);   // nothing lingers (slime throw) — clean up the carrier
            return;
        }

        // Build the lingering acid puddle as a child. Reuses HazardZone (LavaBoots protects, like all acid).
        GameObject patch = new GameObject("AcidPatch");
        patch.transform.SetParent(transform, false);

        SpriteRenderer[] pieces = BuildSplat(patch.transform);

        BoxCollider2D col = patch.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(patchRadius * 1.9f, patchRadius * 0.7f);

        HazardZone hz = patch.AddComponent<HazardZone>();
        hz.damagePerSecond = patchDamagePerSecond;
        hz.requiredRelicID = "LavaBoots";
        hz.appliesSlow = patchSlows;
        hz.slowMultiplier = patchSlowMultiplier;

        StartCoroutine(PuddleLife(pieces));
    }

    // Builds the splat from real gel sprites (a few overlapping blobs) or a procedural pool fallback.
    private SpriteRenderer[] BuildSplat(Transform parent)
    {
        if (puddleSprite != null)
        {
            Color main = new Color(blobColor.r * 0.85f, blobColor.g * 0.9f, blobColor.b * 0.7f, 0.95f);
            Color accent = new Color(blobColor.r * 0.7f, blobColor.g * 0.8f, blobColor.b * 0.55f, 0.9f);
            SpriteRenderer a = MakeSplatPiece(parent, puddleSprite, main,
                new Vector3(0f, 0f, 0f), new Vector3(patchRadius * 2.2f, patchRadius * 1.1f, 1f), 9);
            SpriteRenderer b = MakeSplatPiece(parent, puddleSprite, accent,
                new Vector3(-patchRadius * 0.7f, -0.03f, 0f), new Vector3(patchRadius * 1.2f, patchRadius * 0.7f, 1f), 8);
            SpriteRenderer c = MakeSplatPiece(parent, puddleSprite, accent,
                new Vector3(patchRadius * 0.75f, 0.02f, 0f), new Vector3(patchRadius * 1.25f, patchRadius * 0.72f, 1f), 8);
            return new[] { a, b, c };
        }

        SpriteRenderer ps = MakeSplatPiece(parent, GetPuddleSprite(),
            new Color(blobColor.r, blobColor.g, blobColor.b, 0.85f),
            Vector3.zero, new Vector3(patchRadius * 2f, patchRadius, 1f), 9);
        return new[] { ps };
    }

    private static SpriteRenderer MakeSplatPiece(Transform parent, Sprite sprite, Color color,
        Vector3 localPos, Vector3 localScale, int order)
    {
        GameObject go = new GameObject("Splat");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = order;
        return sr;
    }

    private IEnumerator PuddleLife(SpriteRenderer[] pieces)
    {
        float t = 0f;
        float fadeStart = Mathf.Max(0f, patchDuration - 0.8f);
        Color[] baseCols = new Color[pieces.Length];
        for (int i = 0; i < pieces.Length; i++)
            if (pieces[i] != null) baseCols[i] = pieces[i].color;

        while (t < patchDuration)
        {
            float shimmer = 0.9f + 0.1f * Mathf.Sin(t * 5f);   // gentle liquid shimmer
            float fade = (t > fadeStart && patchDuration > fadeStart)
                ? Mathf.Clamp01(1f - (t - fadeStart) / (patchDuration - fadeStart)) : 1f;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;
                Color b = baseCols[i];
                pieces[i].color = new Color(b.r, b.g, b.b, b.a * shimmer * fade);
            }
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    // A soft-edged white circle, tinted via SpriteRenderer.color. 1 sprite, cached and reused.
    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size / 2f;
        Vector2 c = new Vector2(r, r);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.Clamp01((r - d) / (r * 0.35f));   // solid core, soft falloff at the rim
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSprite;
    }

    // The settled acid pool: a wide, wavy-edged puddle with a dark rim, a brighter interior and a
    // few bubble highlights. Tinted via SpriteRenderer.color. 1 sprite, cached and reused.
    private static Sprite cachedPuddleSprite;
    private static Sprite GetPuddleSprite()
    {
        if (cachedPuddleSprite != null) return cachedPuddleSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float cx = size / 2f, cy = size / 2f;
        float rx = 30f, ry = 15f;   // a wide, flat pool

        // Bubble highlights, in normalized ellipse coords (-1..1) + radius.
        float[,] bubbles = {
            { -0.35f,  0.10f, 0.18f },
            {  0.28f, -0.18f, 0.14f },
            {  0.52f,  0.22f, 0.10f },
            { -0.02f, -0.28f, 0.10f },
            {  0.12f,  0.30f, 0.08f },
        };

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - cx) / rx;
                float ny = (y + 0.5f - cy) / ry;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float ang = Mathf.Atan2(ny, nx);
                float edge = 1f + 0.07f * Mathf.Sin(ang * 7f) + 0.05f * Mathf.Sin(ang * 3f + 1.3f);

                if (d > edge) { tex.SetPixel(x, y, Color.clear); continue; }

                float rim = edge - 0.22f;
                float shade = d > rim ? 0.5f : 0.95f;   // dark rim, bright interior
                float alpha = d > rim ? 0.95f : 0.82f;

                for (int b = 0; b < bubbles.GetLength(0); b++)
                {
                    float dx = nx - bubbles[b, 0];
                    float dy = ny - bubbles[b, 1];
                    float bd = Mathf.Sqrt(dx * dx + dy * dy);
                    if (bd < bubbles[b, 2])
                    {
                        float tb = bd / bubbles[b, 2];
                        shade = tb > 0.62f ? 0.4f : 1f;   // dark outline ring, bright center
                        alpha = 0.95f;
                    }
                }

                tex.SetPixel(x, y, new Color(shade, shade, shade, alpha));
            }
        tex.Apply();
        cachedPuddleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedPuddleSprite;
    }
}
