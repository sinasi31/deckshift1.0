using System.Collections;
using UnityEngine;

// Vampiric Bite effect: a red glow flares on the target, two blood-tipped fang rows
// (top + bottom) snap shut like a mouth, then blood drips fall from the chomp.
// All visuals are generated procedurally in code (no art assets). World-space; spawn
// it with a plain Instantiate at the bitten enemy's position. Self-destroying.
// Built to resemble the Vampiric Bite card art (white interlocking fangs on red).
public class BiteVFX : MonoBehaviour
{
    [Header("Overall")]
    [Tooltip("Scales the whole mouth. Raise for a bigger, more engulfing bite.")]
    public float mouthScale = 1.2f;
    [Tooltip("Sorting order of the glow; jaws sit one above, blood two above. Keep above the enemy sprite.")]
    public int baseSortingOrder = 20;

    [Header("Jaws")]
    public Color fangColor = Color.white;
    [Tooltip("Number of fangs per jaw. Tuning takes effect on the next bite.")]
    public int fangCount = 5;
    [Tooltip("Vertical gap of each jaw from center when the mouth is open (local units).")]
    public float openOffset = 1.3f;
    [Tooltip("Vertical gap when closed; smaller = fangs overlap more at the chomp.")]
    public float closeOffset = 0.42f;
    [Tooltip("Seconds for the jaws to snap shut.")]
    public float biteDuration = 0.10f;
    [Tooltip("Seconds the jaws stay clenched.")]
    public float holdTime = 0.12f;
    [Tooltip("Seconds for the jaws to part and fade out.")]
    public float retractTime = 0.18f;

    [Header("Glow")]
    public Color glowColor = new Color(0.9f, 0.05f, 0.1f, 0.8f);
    public float glowSize = 2.6f;

    [Header("Blood")]
    public int bloodDrops = 8;
    public Color bloodColor = new Color(0.7f, 0f, 0.05f, 1f);
    public float bloodFallSpeed = 4f;
    public float bloodSpread = 0.5f;
    public float bloodSize = 0.16f;
    public float bloodLifetime = 0.55f;

    static Sprite cachedDot;      // uniform soft dot, shared across instances
    Sprite fangSprite;            // per-instance so fangCount tuning applies live
    Texture2D fangTex;

    void Awake()
    {
        transform.localScale = Vector3.one * mouthScale;
        fangSprite = BuildFangSprite();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        StartCoroutine(GlowRoutine());

        SpriteRenderer top = MakeJaw(false);
        SpriteRenderer bottom = MakeJaw(true);

        // Snap shut (ease-in for a fast chomp).
        float t = 0f;
        while (t < biteDuration)
        {
            float n = t / biteDuration;
            float y = Mathf.Lerp(openOffset, closeOffset, n * n);
            top.transform.localPosition = new Vector3(0f, y, 0f);
            bottom.transform.localPosition = new Vector3(0f, -y, 0f);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        top.transform.localPosition = new Vector3(0f, closeOffset, 0f);
        bottom.transform.localPosition = new Vector3(0f, -closeOffset, 0f);

        SpawnBlood();   // chomp impact

        yield return new WaitForSecondsRealtime(holdTime);

        // Part the jaws slightly and fade them out.
        t = 0f;
        while (t < retractTime)
        {
            float n = t / retractTime;
            float y = Mathf.Lerp(closeOffset, closeOffset + 0.25f, n);
            top.transform.localPosition = new Vector3(0f, y, 0f);
            bottom.transform.localPosition = new Vector3(0f, -y, 0f);
            SetAlpha(top, 1f - n);
            SetAlpha(bottom, 1f - n);
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(bloodLifetime);   // let drips finish
        Destroy(gameObject);
    }

    SpriteRenderer MakeJaw(bool isBottom)
    {
        GameObject go = new GameObject(isBottom ? "BottomJaw" : "TopJaw");
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = fangSprite;
        sr.color = fangColor;
        sr.flipY = isBottom;
        sr.sortingOrder = baseSortingOrder + 1;
        go.transform.localPosition = new Vector3(0f, isBottom ? -openOffset : openOffset, 0f);
        return sr;
    }

    IEnumerator GlowRoutine()
    {
        GameObject go = new GameObject("Glow");
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDotSprite();
        sr.sortingOrder = baseSortingOrder;     // behind the jaws

        float life = biteDuration + holdTime + 0.15f;
        float t = 0f;
        while (t < life)
        {
            float n = t / life;
            go.transform.localScale = Vector3.one * Mathf.Lerp(glowSize * 0.4f, glowSize, Mathf.Sqrt(n));
            Color c = glowColor;
            c.a = glowColor.a * (1f - n);
            sr.color = c;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Destroy(go);
    }

    void SpawnBlood()
    {
        for (int i = 0; i < bloodDrops; i++)
            StartCoroutine(BloodRoutine());
    }

    IEnumerator BloodRoutine()
    {
        GameObject go = new GameObject("Blood");
        go.transform.SetParent(transform, false);
        Vector3 pos = new Vector3(Random.Range(-bloodSpread, bloodSpread), Random.Range(-0.1f, 0.1f), 0f);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * bloodSize * Random.Range(0.6f, 1.2f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDotSprite();
        sr.color = bloodColor;
        sr.sortingOrder = baseSortingOrder + 2;

        float vx = Random.Range(-0.6f, 0.6f);
        float vy = -bloodFallSpeed * Random.Range(0.6f, 1.1f);
        const float grav = -6f;

        float t = 0f;
        while (t < bloodLifetime)
        {
            vy += grav * Time.unscaledDeltaTime;
            pos += new Vector3(vx, vy, 0f) * Time.unscaledDeltaTime;
            go.transform.localPosition = pos;
            Color c = bloodColor;
            c.a = bloodColor.a * (1f - t / bloodLifetime);
            sr.color = c;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Destroy(go);
    }

    void SetAlpha(SpriteRenderer sr, float a)
    {
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    // A jaw: a connected gum band along the top with downward-pointing, blood-tipped fangs.
    // The renderer tints by fangColor; the bottom jaw reuses this via flipY.
    Sprite BuildFangSprite()
    {
        const int W = 256, H = 128;
        fangTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        fangTex.wrapMode = TextureWrapMode.Clamp;
        fangTex.filterMode = FilterMode.Bilinear;

        int n = Mathf.Max(1, fangCount);
        float slotW = (float)W / n;
        const float gumStart = 0.82f;                  // v above this is the solid gum band
        Color body = Color.white;
        Color blood = new Color(0.72f, 0.05f, 0.09f, 1f);

        Color32[] px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = (float)y / (H - 1);              // 0 at fang tips (bottom), 1 at gum (top)
            for (int x = 0; x < W; x++)
            {
                float a;
                if (v >= gumStart)
                {
                    a = 1f;
                }
                else
                {
                    int slot = Mathf.Clamp((int)(x / slotW), 0, n - 1);
                    float center = (slot + 0.5f) * slotW;
                    float dx = Mathf.Abs(x - center);
                    float halfWidth = slotW * 0.5f * 0.9f * (v / gumStart);   // 0 at tip, near-slot at gum
                    a = Mathf.Clamp01((halfWidth - dx) / 1.5f);
                }

                Color c = (v < 0.18f) ? Color.Lerp(blood, body, v / 0.18f) : body;
                px[y * W + x] = new Color32(
                    (byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(a * 255f));
            }
        }
        fangTex.SetPixels32(px);
        fangTex.Apply();
        return Sprite.Create(fangTex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 128f);
    }

    Sprite GetDotSprite()
    {
        if (cachedDot != null) return cachedDot;

        const int S = 64;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float center = (S - 1) * 0.5f;
        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        cachedDot = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return cachedDot;
    }

    void OnDestroy()
    {
        if (fangSprite != null) Destroy(fangSprite);
        if (fangTex != null) Destroy(fangTex);
    }
}
