using System.Collections;
using UnityEngine;

// Looping "anti-gravity" aura for the Reverse Gravity / Floor is Lava card.
// Meant to be parented to the player ROOT (always identity rotation, per the project's
// facing system) so motes drift in world space regardless of the visual's 180 deg flip.
// Continuously emits soft motes that rise upward and fade, selling "gravity is pulling
// the wrong way." Builds its own mote sprite in code (no art assets). PlayerController
// destroys this object when the effect ends; in-flight motes go with it.
public class GravityAuraVFX : MonoBehaviour
{
    [Header("Emission")]
    [Tooltip("Motes spawned per second.")]
    public float motesPerSecond = 16f;
    [Tooltip("Horizontal spread around the player, world units.")]
    public float spawnRadius = 0.55f;
    [Tooltip("Vertical start offset from the player pivot.")]
    public float spawnYOffset = -0.2f;

    [Header("Mote Motion")]
    [Tooltip("Upward drift speed (world +Y is the anti-gravity feel).")]
    public float riseSpeed = 2.5f;
    public float riseSpeedVariance = 0.8f;
    public float horizontalDrift = 0.4f;
    [Tooltip("Seconds a mote lives before it has fully faded.")]
    public float moteLifetime = 0.9f;

    [Header("Look")]
    public Color color = new Color(0.55f, 0.8f, 1f, 0.9f);   // pale blue
    public float moteSize = 0.14f;
    public float moteSizeVariance = 0.06f;
    public string sortingLayer = "Default";
    public int sortingOrder = 9;

    const int TEX_SIZE = 64;
    static Sprite cachedDot;

    float accumulator;

    void Update()
    {
        accumulator += Time.deltaTime * motesPerSecond;
        while (accumulator >= 1f)
        {
            accumulator -= 1f;
            StartCoroutine(MoteRoutine());
        }
    }

    IEnumerator MoteRoutine()
    {
        GameObject go = new GameObject("Mote");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(Random.Range(-spawnRadius, spawnRadius), spawnYOffset, 0f);
        float size = Mathf.Max(0.01f, moteSize + Random.Range(-moteSizeVariance, moteSizeVariance));
        go.transform.localScale = Vector3.one * size;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDotSprite();
        sr.color = color;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;

        Vector2 vel = new Vector2(
            Random.Range(-horizontalDrift, horizontalDrift),
            riseSpeed + Random.Range(-riseSpeedVariance, riseSpeedVariance));

        float t = 0f;
        while (t < moteLifetime)
        {
            go.transform.localPosition += (Vector3)(vel * Time.deltaTime);
            float n = t / moteLifetime;
            Color c = color;
            c.a = color.a * (1f - n);     // fade out over life
            sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(go);
    }

    // Soft round dot, 1 world unit wide at scale 1. Color/size come from the renderer
    // and transform, so this texture is identical for every aura and is cached once
    // (same approach as EnemyHealthBar's cached white sprite).
    Sprite GetDotSprite()
    {
        if (cachedDot != null) return cachedDot;

        Texture2D tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float center = (TEX_SIZE - 1) * 0.5f;
        float radius = center;
        Color32[] pixels = new Color32[TEX_SIZE * TEX_SIZE];
        for (int y = 0; y < TEX_SIZE; y++)
        {
            for (int x = 0; x < TEX_SIZE; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;   // 0 center .. 1 edge
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                                          // soft round falloff
                pixels[y * TEX_SIZE + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        cachedDot = Sprite.Create(tex, new Rect(0, 0, TEX_SIZE, TEX_SIZE), new Vector2(0.5f, 0.5f), TEX_SIZE);
        return cachedDot;
    }
}
