using System.Collections;
using UnityEngine;

// Energy "rush" aura for the Adrenaline card. Bright sparks streak upward around the
// player for the buff's duration, then taper off. Procedural (no art assets), world-space,
// meant to be parented to the player ROOT (identity rotation). PlayerController sets
// `duration` to the Adrenaline duration right after spawning; the aura stops emitting after
// that and self-destroys once the last spark has faded.
public class AdrenalineAuraVFX : MonoBehaviour
{
    [Header("Lifetime")]
    [Tooltip("Seconds the aura keeps emitting. PlayerController overrides this with the Adrenaline duration.")]
    public float duration = 3f;

    [Header("Emission")]
    public float sparksPerSecond = 36f;
    public float spawnRadius = 0.5f;
    public float spawnYOffset = -0.4f;

    [Header("Spark Motion")]
    public float riseSpeed = 5f;
    public float riseSpeedVariance = 1.5f;
    public float horizontalDrift = 0.5f;
    public float sparkLifetime = 0.5f;

    [Header("Look")]
    public Color color = new Color(1f, 0.75f, 0.2f, 0.9f);   // warm energy
    public float sparkSize = 0.12f;
    public float sparkSizeVariance = 0.05f;
    public string sortingLayer = "Default";
    public int sortingOrder = 9;

    static Sprite cachedDot;
    float emitAccumulator;
    float elapsed;
    bool emitting = true;

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;     // unscaled so slow-mo Adrenaline doesn't stall the aura
        if (elapsed >= duration) emitting = false;

        if (emitting)
        {
            emitAccumulator += Time.unscaledDeltaTime * sparksPerSecond;
            while (emitAccumulator >= 1f)
            {
                emitAccumulator -= 1f;
                StartCoroutine(SparkRoutine());
            }
        }
        else if (elapsed >= duration + sparkLifetime)
        {
            Destroy(gameObject);
        }
    }

    IEnumerator SparkRoutine()
    {
        GameObject go = new GameObject("Spark");
        go.transform.SetParent(transform, false);
        Vector3 pos = new Vector3(Random.Range(-spawnRadius, spawnRadius), spawnYOffset, 0f);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * Mathf.Max(0.01f, sparkSize + Random.Range(-sparkSizeVariance, sparkSizeVariance));

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDotSprite();
        sr.color = color;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;

        Vector2 vel = new Vector2(
            Random.Range(-horizontalDrift, horizontalDrift),
            riseSpeed + Random.Range(-riseSpeedVariance, riseSpeedVariance));

        float t = 0f;
        while (t < sparkLifetime)
        {
            pos += (Vector3)(vel * Time.unscaledDeltaTime);
            go.transform.localPosition = pos;
            Color c = color;
            c.a = color.a * (1f - t / sparkLifetime);
            sr.color = c;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Destroy(go);
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
}
