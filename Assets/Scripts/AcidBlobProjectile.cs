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
        // Flying-blob visual (a small green orb).
        blobSr = gameObject.AddComponent<SpriteRenderer>();
        blobSr.sprite = GetCircleSprite();
        blobSr.color = blobColor;
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
        transform.Rotate(0f, 0f, -360f * Time.deltaTime);   // a little tumble for life

        if (u >= 1f) Land();
    }

    private void Land()
    {
        landed = true;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;   // reset so the child puddle scales cleanly

        if (splashEffect != null)
            Instantiate(splashEffect, transform.position, Quaternion.identity);

        // Build the lingering puddle as a child. Reuses HazardZone (LavaBoots protects, like all acid).
        GameObject patch = new GameObject("AcidPatch");
        patch.transform.SetParent(transform, false);
        patch.transform.localScale = new Vector3(patchRadius * 2f, patchRadius, 1f);

        SpriteRenderer ps = patch.AddComponent<SpriteRenderer>();
        ps.sprite = GetCircleSprite();
        ps.color = new Color(blobColor.r, blobColor.g, blobColor.b, 0.55f);
        ps.sortingLayerName = "Default";
        ps.sortingOrder = 9;

        BoxCollider2D col = patch.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 0.6f);   // local units, scaled by the puddle's localScale above

        HazardZone hz = patch.AddComponent<HazardZone>();
        hz.damagePerSecond = patchDamagePerSecond;
        hz.requiredRelicID = "LavaBoots";

        blobSr.enabled = false;   // hide the orb; the GameObject lives on to host the puddle
        StartCoroutine(PuddleLife(ps));
    }

    private IEnumerator PuddleLife(SpriteRenderer ps)
    {
        float t = 0f;
        float fadeStart = Mathf.Max(0f, patchDuration - 0.8f);
        Color baseCol = ps.color;
        while (t < patchDuration)
        {
            if (t > fadeStart && patchDuration > fadeStart)
            {
                float fade = 1f - (t - fadeStart) / (patchDuration - fadeStart);
                ps.color = new Color(baseCol.r, baseCol.g, baseCol.b, baseCol.a * Mathf.Clamp01(fade));
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
}
