using System.Collections;
using UnityEngine;

// Procedural "chest opened!" burst, spawned above a chest when it grants a relic. Fully self-building
// (no art assets), colour-coded by the relic's Rarity so the player reads the drop quality straight
// from the effect: a rising light beam, a flash + ground ring, a shower of twinkling motes, and the
// relic icon lifting out with a bounce. Self-destroys when finished. House style mirrors ShockwaveVFX
// / EnemyHealthBar — sprites are generated in code and cached statically.
public class ChestOpenVFX : MonoBehaviour
{
    private Color tint = Color.white;
    private int moteCount = 10;
    private float beamHeight = 3f;
    private float ringMax = 2f;
    private Sprite relicSprite;
    private bool started;

    const int SORT = 30;   // draw above the chest sprite + world

    private static Sprite glowSprite, ringSprite, beamSprite;

    // Called by the chest right after Instantiate. Maps rarity -> colour + intensity, then plays.
    public void Play(Rarity rarity, Sprite relic)
    {
        relicSprite = relic;
        switch (rarity)
        {
            case Rarity.Legendary: tint = new Color(1f, 0.80f, 0.25f); moteCount = 20; beamHeight = 4.2f; ringMax = 3.0f; break;
            case Rarity.Epic:      tint = new Color(0.72f, 0.38f, 1f);  moteCount = 14; beamHeight = 3.4f; ringMax = 2.4f; break;
            case Rarity.Rare:      tint = new Color(0.35f, 0.62f, 1f);  moteCount = 10; beamHeight = 2.8f; ringMax = 2.0f; break;
            default:               tint = new Color(0.90f, 0.93f, 1f);  moteCount = 7;  beamHeight = 2.2f; ringMax = 1.6f; break;
        }
        if (!started) { started = true; StartCoroutine(Run()); }
    }

    private IEnumerator Run()
    {
        StartCoroutine(FlashRoutine());
        StartCoroutine(RingRoutine());
        StartCoroutine(BeamRoutine());
        for (int i = 0; i < moteCount; i++) StartCoroutine(MoteRoutine());

        if (relicSprite != null) yield return StartCoroutine(RelicRevealRoutine());
        else yield return new WaitForSeconds(1f);

        yield return new WaitForSeconds(0.15f);   // let any tail settle
        Destroy(gameObject);
    }

    // Quick bright pop of light at the lid.
    private IEnumerator FlashRoutine()
    {
        SpriteRenderer sr = MakeSprite("Flash", GetGlowSprite(), tint, SORT + 2);
        float life = 0.28f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            sr.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.4f, EaseOut(n));
            Color c = tint; c.a = 1f - n; sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // Ground ring pulse.
    private IEnumerator RingRoutine()
    {
        SpriteRenderer sr = MakeSprite("Ring", GetRingSprite(), tint, SORT + 1);
        float life = 0.5f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            float r = Mathf.Lerp(0.3f, ringMax, EaseOut(n));
            sr.transform.localScale = Vector3.one * r * 2f;
            Color c = tint; c.a = 1f - n; sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // Vertical light pillar shooting up out of the chest.
    private IEnumerator BeamRoutine()
    {
        SpriteRenderer sr = MakeSprite("Beam", GetBeamSprite(), tint, SORT);
        float life = 0.6f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            float grow = EaseOut(Mathf.Min(1f, n * 2f));   // shoots up fast, then holds
            sr.transform.localScale = new Vector3(0.7f * (1f - n * 0.3f), beamHeight * grow, 1f);
            Color c = tint; c.a = (1f - n) * 0.85f; sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // A single glowing mote flung up-and-out, arcing down under light gravity while it twinkles out.
    private IEnumerator MoteRoutine()
    {
        SpriteRenderer sr = MakeSprite("Mote", GetGlowSprite(), tint, SORT + 3);
        Transform tr = sr.transform;
        float ang = Mathf.Deg2Rad * Random.Range(50f, 130f);       // upward fan
        Vector2 vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(2.5f, 6f);
        float baseScale = Random.Range(0.12f, 0.28f);
        float life = Random.Range(0.6f, 1.0f), t = 0f;
        float phase = Random.value * 6.28f;
        Color moteColor = Color.Lerp(tint, Color.white, 0.4f);     // brighter core
        while (t < life)
        {
            vel.y += Physics2D.gravity.y * 0.35f * Time.deltaTime;
            tr.position += (Vector3)(vel * Time.deltaTime);
            float n = t / life;
            float twinkle = 0.6f + 0.4f * Mathf.Sin(Time.time * 18f + phase);
            tr.localScale = Vector3.one * baseScale * (1f - n * 0.5f) * twinkle;
            Color c = moteColor; c.a = (1f - n) * twinkle; sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // The relic icon lifts out of the chest with an over-shoot pop, glows/bobs, then floats up and fades.
    private IEnumerator RelicRevealRoutine()
    {
        SpriteRenderer glow = MakeSprite("RelicGlow", GetGlowSprite(), tint, SORT + 4);
        SpriteRenderer icon = MakeSprite("RelicIcon", relicSprite, Color.white, SORT + 5);

        float targetSize = 0.9f;   // world height of the icon
        float unit = relicSprite.bounds.size.y > 0.001f ? targetSize / relicSprite.bounds.size.y : targetSize;

        Vector3 startPos = transform.position;
        Vector3 topPos = startPos + Vector3.up * 0.8f;

        // Rise + overshoot pop.
        float inT = 0.45f, t = 0f;
        while (t < inT)
        {
            float n = t / inT;
            float pop = EaseOutBack(n);
            Vector3 p = Vector3.Lerp(startPos, topPos, EaseOut(n));
            icon.transform.position = p;
            icon.transform.localScale = Vector3.one * unit * Mathf.Max(0f, pop);
            glow.transform.position = p;
            glow.transform.localScale = Vector3.one * targetSize * 2.4f * Mathf.Max(0f, pop);
            Color gc = tint; gc.a = 0.6f * n; glow.color = gc;
            t += Time.deltaTime;
            yield return null;
        }

        // Hold with a gentle glow pulse + bob.
        float hold = 0.5f; t = 0f;
        while (t < hold)
        {
            Vector3 p = topPos + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.03f);
            icon.transform.position = p;
            glow.transform.position = p;
            Color gc = tint; gc.a = 0.45f + 0.18f * Mathf.Sin(Time.time * 6f); glow.color = gc;
            t += Time.deltaTime;
            yield return null;
        }

        // Float up and fade out.
        float outT = 0.5f; t = 0f;
        Vector3 fadeStart = icon.transform.position;
        while (t < outT)
        {
            float n = t / outT;
            Vector3 p = fadeStart + Vector3.up * (n * 0.5f);
            icon.transform.position = p; glow.transform.position = p;
            Color ic = Color.white; ic.a = 1f - n; icon.color = ic;
            Color gc = tint; gc.a = (1f - n) * 0.45f; glow.color = gc;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(glow.gameObject);
        Destroy(icon.gameObject);
    }

    private SpriteRenderer MakeSprite(string n, Sprite sprite, Color color, int order)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    // --- Procedural sprites (soft radial glow, ring, bottom-pivoted beam), cached + shared. ---

    private static Sprite GetGlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, rad = c;
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rad;
                float a = Mathf.Clamp01(1f - d); a *= a;          // soft quadratic falloff
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);   // 1 unit wide
        return glowSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, outer = c - 1f, band = s * 0.10f, inner = outer - band, feather = 2f;
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float ao = Mathf.Clamp01((outer - d) / feather);
                float ai = Mathf.Clamp01((d - inner) / feather);
                float a = Mathf.Min(ao, ai);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return ringSprite;
    }

    private static Sprite GetBeamSprite()
    {
        if (beamSprite != null) return beamSprite;
        int w = 64, h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float cx = (w - 1) * 0.5f;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float hx = Mathf.Clamp01(1f - Mathf.Abs(x - cx) / cx); hx *= hx;   // bright centre column
                float vy = 1f - (y / (float)(h - 1));                              // bright at base, fade up
                float a = hx * vy;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        // Bottom-centre pivot so scaling Y grows the beam upward from the chest; ppu = h => 1 unit tall.
        beamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);
        return beamSprite;
    }
}
