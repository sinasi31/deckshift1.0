using System.Collections.Generic;
using UnityEngine;

// Procedural Glass Parry visuals — house VFX style (ChestOpenVFX pattern): cached
// generated sprites, Update-driven mote list, alpha-blend (never additive), self-destroys.
//
// Two modes on one component:
//  - WINDOW: a thin glassy ring hugging the player for the 0.5s parry window, pulsing
//    and thinning out as the window expires (the visual IS the timing aid).
//  - SHATTER: the success burst — white flash, expanding ring shockwave, and a spray
//    of spinning glass shards that drift and die.
public class GlassParryVFX : MonoBehaviour
{
    private class Mote
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 vel;
        public float spin;
        public float life, maxLife;
        public Color baseColor;
        public Vector3 baseScale;
        public float scaleGain;   // extra uniform scale added over lifetime (rings grow)
    }

    private static Sprite dotSprite, ringSprite, shardSprite;
    private static readonly Color GlassCyan = new Color(0.68f, 0.95f, 1f);

    private readonly List<Mote> motes = new List<Mote>();
    private bool isWindow;
    private Transform follow;
    private float windowDuration, windowElapsed;
    private SpriteRenderer rim;

    // ---------------- spawning ----------------

    public static GlassParryVFX SpawnWindow(Transform follow, float duration)
    {
        EnsureSprites();
        var vfx = new GameObject("GlassParryWindowVFX").AddComponent<GlassParryVFX>();
        vfx.isWindow = true;
        vfx.follow = follow;
        vfx.windowDuration = Mathf.Max(0.05f, duration);
        vfx.transform.position = follow != null ? follow.position : Vector3.zero;
        vfx.rim = NewSR(vfx.transform, ringSprite, GlassCyan, 60);
        vfx.rim.transform.localScale = Vector3.one * 2.4f;
        return vfx;
    }

    public static GlassParryVFX SpawnShatter(Vector3 pos)
    {
        EnsureSprites();
        var vfx = new GameObject("GlassParryShatterVFX").AddComponent<GlassParryVFX>();
        vfx.isWindow = false;
        vfx.transform.position = pos;

        // White flash core.
        vfx.AddMote(dotSprite, Color.white, pos, Vector2.zero, 0f, 0.12f, 1.6f, 0f, 61);

        // Ring shockwave, growing fast and fading.
        vfx.AddMote(ringSprite, GlassCyan, pos, Vector2.zero, 0f, 0.3f, 0.8f, 5.0f, 60);

        // Glass shards: spinning slivers thrown outward, slowing as they fade.
        for (int i = 0; i < 14; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(5f, 10f);
            Vector2 vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
            Color c = Color.Lerp(Color.white, GlassCyan, Random.value);
            vfx.AddMote(shardSprite, c, pos, vel, Random.Range(-540f, 540f),
                        Random.Range(0.35f, 0.6f), Random.Range(0.5f, 0.9f), 0f, 60);
        }

        // A few soft sparkle dots between the shards.
        for (int i = 0; i < 8; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector2 vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(2f, 4.5f);
            vfx.AddMote(dotSprite, Color.white, pos, vel, 0f,
                        Random.Range(0.25f, 0.45f), Random.Range(0.15f, 0.3f), 0f, 60);
        }

        return vfx;
    }

    // Kills the window ring immediately (called the moment a parry succeeds, so the
    // calm ring never overlaps the shatter).
    public void CutShort()
    {
        if (isWindow) Destroy(gameObject);
    }

    // ---------------- lifecycle ----------------

    private void Update()
    {
        if (isWindow) UpdateWindow();
        else UpdateBurst();
    }

    private void UpdateWindow()
    {
        if (follow == null || rim == null) { Destroy(gameObject); return; }
        transform.position = follow.position;

        windowElapsed += Time.deltaTime;
        float remaining01 = Mathf.Clamp01(1f - windowElapsed / windowDuration);
        if (remaining01 <= 0f) { Destroy(gameObject); return; }

        // Tight pulse; the ring thins/dims as the window runs out.
        float pulse = 1f + Mathf.Sin(Time.time * 28f) * 0.05f;
        rim.transform.localScale = Vector3.one * 2.4f * pulse;
        Color c = GlassCyan;
        c.a = Mathf.Lerp(0.15f, 0.85f, remaining01);
        rim.color = c;
    }

    private void UpdateBurst()
    {
        bool anyAlive = false;
        float dt = Time.deltaTime;

        for (int i = 0; i < motes.Count; i++)
        {
            Mote m = motes[i];
            if (m.life <= 0f || m.t == null) continue;

            m.life -= dt;
            if (m.life <= 0f) { m.sr.enabled = false; continue; }
            anyAlive = true;

            m.t.position += (Vector3)(m.vel * dt);
            m.vel *= 1f - 4f * dt;                       // air drag
            if (m.spin != 0f) m.t.Rotate(0f, 0f, m.spin * dt);

            float t01 = 1f - m.life / m.maxLife;
            float fade = 1f - t01;
            Color c = m.baseColor;
            c.a *= fade * fade;
            m.sr.color = c;
            m.t.localScale = m.baseScale * (1f + m.scaleGain * t01);
        }

        if (!anyAlive) Destroy(gameObject);
    }

    private void AddMote(Sprite sprite, Color color, Vector3 pos, Vector2 vel, float spin,
                         float life, float scale, float scaleGain, int order)
    {
        SpriteRenderer sr = NewSR(transform, sprite, color, order);
        sr.transform.position = pos;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        sr.transform.localScale = Vector3.one * scale;
        motes.Add(new Mote
        {
            t = sr.transform, sr = sr, vel = vel, spin = spin,
            life = life, maxLife = life, baseColor = color,
            baseScale = Vector3.one * scale, scaleGain = scaleGain,
        });
    }

    // ---------------- sprite generation (cached for the whole session) ----------------

    private static void EnsureSprites()
    {
        if (dotSprite != null) return;
        // Soft radial dot.
        dotSprite = GenSprite(48, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01(1f - d);
            return a * a;
        });
        // Thin ring band.
        ringSprite = GenSprite(64, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.14f);
        });
        // Elongated glass sliver (vertical kite).
        shardSprite = GenSprite(32, (x, y) =>
        {
            float v = 1f - (Mathf.Abs(x) / 0.3f + Mathf.Abs(y) / 0.95f);
            return Mathf.Clamp01(v * 1.8f);
        });
    }

    private static Sprite GenSprite(int size, System.Func<float, float, float> alphaAt)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float nx = (px + 0.5f) / size * 2f - 1f;
                float ny = (py + 0.5f) / size * 2f - 1f;
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, alphaAt(nx, ny)));
            }
        }
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static SpriteRenderer NewSR(Transform parent, Sprite sprite, Color color, int order)
    {
        var go = new GameObject("mote");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }
}
