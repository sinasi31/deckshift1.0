using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The forging sequence that plays when Blompo blesses a card: a hammer swings in and strikes the
// card three times, each blow landing harder — white flash, shockwave ring, spark shower, and a
// kick that shoves the card down and shakes the whole window. The third blow sets the gem.
//
// Runs entirely in UI space (Images under a supplied RectTransform), because the card being forged
// is a screen element, not a world object. Everything is procedural (house pattern) — the hammer,
// sparks, rings and flash are all generated textures, no art files.
//
// Uses UNSCALED time throughout: the blessing screen pauses the game (timeScale 0), so anything
// driven by Time.deltaTime here would simply never advance.
public static class BlompoForgeFX
{
    private static Sprite hammerSprite, sparkSprite, ringSprite, glowSprite, softSprite;

    // Three blows, each landing harder than the last.
    private static readonly float[] StrikePower = { 0.55f, 0.78f, 1f };

    /// Plays the full sequence against `card` (a chip already parented under `host`).
    /// `onSet` fires at the instant the final blow lands, so the caller can apply the enhancement
    /// and re-skin the card exactly on the impact frame.
    // `runner` owns the spark coroutines — `host` is a plain RectTransform with no MonoBehaviour
    // on it, so it cannot start them itself.
    public static IEnumerator Play(MonoBehaviour runner, RectTransform host, RectTransform card, Color gem, System.Action onSet)
    {
        Vector2 cardHome = card.anchoredPosition;
        Vector2 hostHome = host.anchoredPosition;   // shake around where the stage actually sits
        // The card is usually shown enlarged for the forging, so squash MULTIPLIES its existing
        // scale rather than replacing it (which would snap it back to 1x on the first blow).
        Vector3 cardScale = card.localScale;

        // Layers, back to front: glow behind the card, then hammer/sparks/flash in front.
        // Tight enough to read as heat coming off the card rather than washing out the panel.
        Image backGlow = MakeImage(host, "ForgeGlow", GetGlowSprite(), new Color(1f, 0.62f, 0.18f, 0f), 380f, cardHome);
        backGlow.transform.SetSiblingIndex(card.GetSiblingIndex());

        Image hammer = MakeImage(host, "Hammer", GetHammerSprite(), Color.white, 300f, cardHome + new Vector2(210f, 210f));
        hammer.rectTransform.pivot = new Vector2(0.5f, 0.06f);   // swing about the handle's end
        hammer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 62f);
        hammer.enabled = false;

        Image flash = MakeImage(host, "Flash", GetSoftSprite(), new Color(1f, 0.95f, 0.8f, 0f), 640f, cardHome);

        for (int s = 0; s < StrikePower.Length; s++)
        {
            float power = StrikePower[s];

            // --- wind up: hammer rears back ---
            hammer.enabled = true;
            const float raise = 0.16f;
            for (float t = 0f; t < raise; t += Time.unscaledDeltaTime)
            {
                float n = t / raise;
                hammer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(28f, 72f, n));
                hammer.rectTransform.anchoredPosition = cardHome + new Vector2(Mathf.Lerp(150f, 215f, n), Mathf.Lerp(150f, 215f, n));
                SetAlpha(backGlow, Mathf.Lerp(0.05f, 0.16f, n) * power);
                yield return null;
            }

            // --- swing down: fast, accelerating ---
            float swing = Mathf.Lerp(0.10f, 0.07f, power);
            for (float t = 0f; t < swing; t += Time.unscaledDeltaTime)
            {
                float n = Mathf.Clamp01(t / swing);
                float e = n * n;                                   // accelerate into the blow
                hammer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(72f, -12f, e));
                hammer.rectTransform.anchoredPosition = cardHome + new Vector2(Mathf.Lerp(215f, 34f, e), Mathf.Lerp(215f, 86f, e));
                yield return null;
            }

            // --- IMPACT ---
            if (onSet != null && s == StrikePower.Length - 1) onSet();

            SpawnSparks(runner, host, cardHome + new Vector2(0f, 40f), power, gem);
            Image ring = MakeImage(host, "Ring", GetRingSprite(), new Color(1f, 0.88f, 0.55f, 0.95f), 120f, cardHome + new Vector2(0f, 30f));
            SetAlpha(flash, 0.55f * power);
            if (CameraShake.instance != null) CameraShake.instance.Shake(0.12f * power, 0.28f * power);

            float kick = 26f * power;
            float shake = 16f * power;
            const float settle = 0.34f;
            for (float t = 0f; t < settle; t += Time.unscaledDeltaTime)
            {
                float n = Mathf.Clamp01(t / settle);

                // Card takes the blow and springs back.
                float squash = Mathf.Sin(n * Mathf.PI) * (1f - n);
                card.anchoredPosition = cardHome + Vector2.down * (kick * (1f - n) * (1f - n));
                card.localScale = new Vector3(cardScale.x * (1f + 0.10f * squash * power),
                                              cardScale.y * (1f - 0.13f * squash * power), cardScale.z);

                // Whole window rattles, decaying.
                float dec = (1f - n) * (1f - n);
                host.anchoredPosition = hostHome + new Vector2(Random.Range(-shake, shake) * dec, Random.Range(-shake, shake) * dec);

                // Ring expands and fades; flash falls off fast.
                ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(120f, 560f * power, Mathf.Sqrt(n));
                SetAlpha(ring, Mathf.Clamp01(1f - n * 1.5f));
                SetAlpha(flash, Mathf.Lerp(0.55f * power, 0f, n * 1.8f));
                SetAlpha(backGlow, Mathf.Lerp(0.42f * power, 0.12f, n));

                // Hammer bounces off.
                hammer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-12f, 26f, n));
                hammer.rectTransform.anchoredPosition = cardHome + new Vector2(Mathf.Lerp(34f, 150f, n), Mathf.Lerp(86f, 150f, n));
                yield return null;
            }

            Object.Destroy(ring.gameObject);
            card.anchoredPosition = cardHome;
            card.localScale = cardScale;
            host.anchoredPosition = hostHome;
        }

        // --- the gem sets: a last bloom in the rarity colour ---
        hammer.enabled = false;
        Image bloom = MakeImage(host, "Bloom", GetGlowSprite(), new Color(gem.r, gem.g, gem.b, 0.9f), 200f, cardHome);
        const float bloomDur = 0.45f;
        for (float t = 0f; t < bloomDur; t += Time.unscaledDeltaTime)
        {
            float n = t / bloomDur;
            bloom.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(200f, 760f, Mathf.Sqrt(n));
            SetAlpha(bloom, Mathf.Lerp(0.9f, 0f, n));
            SetAlpha(backGlow, Mathf.Lerp(0.35f, 0f, n));
            card.localScale = cardScale * (1f + 0.06f * Mathf.Sin(n * Mathf.PI));
            yield return null;
        }

        card.localScale = cardScale;
        host.anchoredPosition = hostHome;
        Object.Destroy(bloom.gameObject);
        Object.Destroy(flash.gameObject);
        Object.Destroy(backGlow.gameObject);
        Object.Destroy(hammer.gameObject);
    }

    // Sparks fly out sideways-and-up from the strike, then fall under gravity.
    private static void SpawnSparks(MonoBehaviour runner, RectTransform host, Vector2 origin, float power, Color gem)
    {
        int count = Mathf.RoundToInt(Mathf.Lerp(14f, 30f, power));
        for (int i = 0; i < count; i++)
        {
            Image spark = MakeImage(host, "Spark", GetSparkSprite(), Color.white, Random.Range(9f, 20f) * (0.7f + power), origin);
            // Bias sideways so it reads as metal spitting off an anvil, not a firework.
            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(ang) * 1.5f, Mathf.Abs(Mathf.Sin(ang)) * 0.9f + 0.25f).normalized;
            float speed = Random.Range(320f, 950f) * (0.6f + power * 0.7f);
            Color c = Random.value < 0.28f ? gem : Color.Lerp(new Color(1f, 0.95f, 0.65f), new Color(1f, 0.55f, 0.12f), Random.value);
            if (runner != null) runner.StartCoroutine(SparkLife(spark, dir * speed, c));
        }
    }

    private static IEnumerator SparkLife(Image spark, Vector2 vel, Color color)
    {
        float life = Random.Range(0.30f, 0.62f);
        Vector2 pos = spark.rectTransform.anchoredPosition;
        spark.color = color;
        float baseW = spark.rectTransform.sizeDelta.x;
        for (float t = 0f; t < life; t += Time.unscaledDeltaTime)
        {
            float n = t / life;
            vel.y -= 2400f * Time.unscaledDeltaTime;         // gravity
            vel *= 1f - 2.2f * Time.unscaledDeltaTime;       // drag
            pos += vel * Time.unscaledDeltaTime;
            spark.rectTransform.anchoredPosition = pos;
            // Stretch along travel so fast sparks read as streaks.
            float sp = vel.magnitude;
            spark.rectTransform.sizeDelta = new Vector2(baseW * (1f + sp / 900f), baseW * 0.5f);
            spark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg);
            Color c = color; c.a = 1f - n * n; spark.color = c;
            yield return null;
        }
        if (spark != null) Object.Destroy(spark.gameObject);
    }

    // ---- helpers ----
    private static Image MakeImage(RectTransform host, string name, Sprite sprite, Color color, float size, Vector2 pos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(host, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
        return img;
    }

    private static void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        Color c = img.color; c.a = Mathf.Clamp01(a); img.color = c;
    }

    // ---- procedural sprites ----

    // Blocky pixel-art hammer: wooden haft, iron head with a lit top face and dark banding.
    private static Sprite GetHammerSprite()
    {
        if (hammerSprite != null) return hammerSprite;
        int s = 96;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
        Color32[] px = new Color32[s * s];
        Color wood = new Color(0.44f, 0.28f, 0.14f);
        Color woodHi = new Color(0.60f, 0.40f, 0.21f);
        Color iron = new Color(0.55f, 0.57f, 0.62f);
        Color ironHi = new Color(0.78f, 0.80f, 0.85f);
        Color ironLo = new Color(0.28f, 0.29f, 0.33f);

        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                Color c = new Color(0, 0, 0, 0);
                // Haft: vertical bar up the middle, lower two-thirds.
                if (x >= 42 && x <= 53 && y >= 4 && y < 64)
                    c = (x <= 45) ? woodHi : wood;
                // Head: wide block across the top.
                if (y >= 62 && y <= 90 && x >= 14 && x <= 82)
                {
                    c = iron;
                    if (y >= 84) c = ironHi;                       // lit top face
                    if (y <= 66 || x <= 17 || x >= 79) c = ironLo;  // dark underside + ends
                    if (x >= 30 && x <= 66 && y >= 70 && y <= 80) c = Color.Lerp(iron, ironHi, 0.35f);
                }
                px[y * s + x] = c.a > 0f ? (Color32)c : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        hammerSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.06f), s);
        return hammerSprite;
    }

    // A single bright pixel-ish shard; stretched along travel by SparkLife.
    private static Sprite GetSparkSprite()
    {
        if (sparkSprite != null) return sparkSprite;
        int w = 16, h = 8;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)(w - 1), ny = Mathf.Abs(y - (h - 1) * 0.5f) / ((h - 1) * 0.5f);
                float a = Mathf.Clamp01((1f - ny) * (1f - Mathf.Abs(nx - 0.35f) * 1.4f));
                a *= a;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        sparkSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16);
        return sparkSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        int s = 128; float c = (s - 1) * 0.5f, rad = c * 0.84f, thick = c * 0.11f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - rad) / thick); a *= a;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
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
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return glowSprite;
    }

    // Broad soft falloff used for the impact flash.
    private static Sprite GetSoftSprite()
    {
        if (softSprite != null) return softSprite;
        int s = 128; float c = (s - 1) * 0.5f, rad = c;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rad;
                float a = Mathf.Clamp01(1f - d);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        softSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return softSprite;
    }
}
