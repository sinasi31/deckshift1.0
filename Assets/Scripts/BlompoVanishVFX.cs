using System.Collections;
using UnityEngine;

// Blompo's exit. He doesn't just switch off — his heart-core flares, he collapses into a burst of
// spinning gold stars that rush outward and drift up, and a bright ring snaps out where he stood.
//
// Runs on its OWN GameObject (not Blompo's), because Blompo is deactivated partway through and a
// coroutine on a disabled object would freeze mid-effect — the same trap documented for
// EnemyHealth.Die firing OnDied and destroying in the same frame.
//
// House pattern: every sprite is generated in code (like DashAfterimage / ShockwaveVFX), no art.
public class BlompoVanishVFX : MonoBehaviour
{
    private static Sprite starSprite, ringSprite, glowSprite;

    const int STAR_COUNT = 18;

    // Spawns the effect at `where` and hides `blompo` as it plays.
    public static void Play(Vector3 where, GameObject blompo, Sprite portrait, float spriteScale = 1f)
    {
        GameObject go = new GameObject("BlompoVanishVFX");
        go.transform.position = where;
        go.AddComponent<BlompoVanishVFX>().Run(blompo, portrait, spriteScale);
    }

    private void Run(GameObject blompo, Sprite portrait, float spriteScale)
    {
        StartCoroutine(Routine(blompo, portrait, spriteScale));
    }

    private IEnumerator Routine(GameObject blompo, Sprite portrait, float spriteScale)
    {
        // --- 1. anticipation: he swells and brightens for a beat ---
        Transform bt = blompo != null ? blompo.transform : null;
        Vector3 baseScale = bt != null ? bt.localScale : Vector3.one;
        SpriteRenderer bsr = blompo != null ? blompo.GetComponent<SpriteRenderer>() : null;
        Color baseCol = bsr != null ? bsr.color : Color.white;

        GameObject glow = MakeQuad("Flare", GetGlowSprite(), new Color(1f, 0.85f, 0.35f, 0f), 3.2f * spriteScale, 40);

        const float windup = 0.22f;
        for (float t = 0f; t < windup; t += Time.deltaTime)
        {
            float n = t / windup;
            if (bt != null) bt.localScale = baseScale * (1f + 0.16f * n);
            if (bsr != null) bsr.color = Color.Lerp(baseCol, Color.white, n * 0.75f);
            SetAlpha(glow, n * 0.85f);
            glow.transform.localScale = Vector3.one * (3.2f * spriteScale) * (0.5f + 0.7f * n);
            yield return null;
        }

        // --- 2. the pop: he's gone, stars everywhere, ring snaps out ---
        if (blompo != null) blompo.SetActive(false);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.18f, 0.22f);

        GameObject ring = MakeQuad("Ring", GetRingSprite(), new Color(1f, 0.9f, 0.5f, 0.95f), 0.6f * spriteScale, 41);

        var stars = new Transform[STAR_COUNT];
        var dirs = new Vector2[STAR_COUNT];
        var spins = new float[STAR_COUNT];
        var sizes = new float[STAR_COUNT];
        for (int i = 0; i < STAR_COUNT; i++)
        {
            float ang = (i / (float)STAR_COUNT) * Mathf.PI * 2f + Random.Range(-0.18f, 0.18f);
            dirs[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(1.9f, 3.9f);
            spins[i] = Random.Range(-520f, 520f);
            sizes[i] = Random.Range(0.13f, 0.27f) * spriteScale;
            GameObject s = MakeQuad("Star" + i, GetStarSprite(), new Color(1f, Random.Range(0.78f, 0.95f), 0.32f, 1f), sizes[i], 42);
            stars[i] = s.transform;
        }

        const float life = 0.95f;
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float n = t / life;

            // Ring: fast expand, quick fade.
            float rn = Mathf.Sqrt(Mathf.Min(1f, n * 2.2f));
            ring.transform.localScale = Vector3.one * (0.6f + 4.4f * rn) * spriteScale;
            SetAlpha(ring, Mathf.Clamp01(1f - n * 2.0f) * 0.95f);

            // Stars: burst out with drag, then float upward as they fade.
            float drag = 1f - Mathf.Pow(1f - n, 2f);   // decelerate
            for (int i = 0; i < STAR_COUNT; i++)
            {
                if (stars[i] == null) continue;
                Vector3 p = (Vector3)(dirs[i] * drag) + Vector3.up * (n * n * 1.5f);
                stars[i].localPosition = p;
                stars[i].localRotation = Quaternion.Euler(0f, 0f, spins[i] * n);
                float pop = n < 0.12f ? n / 0.12f : 1f;                  // brief scale-in
                float fade = n > 0.55f ? 1f - (n - 0.55f) / 0.45f : 1f;
                stars[i].localScale = Vector3.one * sizes[i] * pop * (0.7f + 0.5f * fade);
                SetAlpha(stars[i].gameObject, fade);
            }

            SetAlpha(glow, Mathf.Clamp01(1f - n * 1.8f) * 0.8f);
            glow.transform.localScale = Vector3.one * (3.2f * spriteScale) * (1.2f + 1.1f * n);
            yield return null;
        }

        Destroy(gameObject);
    }

    private GameObject MakeQuad(string name, Sprite sprite, Color color, float size, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * size;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    private static void SetAlpha(GameObject go, float a)
    {
        if (go == null) return;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        Color c = sr.color; c.a = Mathf.Clamp01(a); sr.color = c;
    }

    // --- procedural sprites (cached) ---

    // Four-pointed sparkle, matching the gold stars in Blompo's own artwork. Uses an ASTROID
    // (|x|^p + |y|^p = 1 with p < 1), whose sides are genuinely concave — that's what gives the
    // crisp needle points a plain diamond/cross shape can't. Lower p = sharper points.
    private static Sprite GetStarSprite()
    {
        if (starSprite != null) return starSprite;
        int s = 64; float half = s / 2f;
        const float p = 0.42f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float d = Mathf.Pow(Mathf.Abs(dx), p) + Mathf.Pow(Mathf.Abs(dy), p);
                float a = Mathf.Clamp01((1f - d) / 0.22f);
                // Hot white core fading to gold at the tips.
                float core = Mathf.Clamp01(1f - d * 1.35f);
                byte g = (byte)(255f * Mathf.Clamp01(0.72f + 0.28f * core));
                byte b = (byte)(255f * Mathf.Clamp01(0.18f + 0.72f * core * core));
                px[y * s + x] = new Color32(255, g, b, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        starSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return starSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        int s = 128; float c = (s - 1) * 0.5f, rad = c * 0.86f, thick = c * 0.13f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - rad) / thick);
                a *= a;
                px[y * s + x] = new Color32(255, 240, 190, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return ringSprite;
    }

    private static Sprite GetGlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        int s = 128; float c = (s - 1) * 0.5f, rad = c;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rad;
                float a = Mathf.Clamp01(1f - d); a *= a * a;
                px[y * s + x] = new Color32(255, 235, 170, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return glowSprite;
    }
}
