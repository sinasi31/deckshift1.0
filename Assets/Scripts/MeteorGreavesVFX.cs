using System.Collections.Generic;
using UnityEngine;

// Procedural ground-slam shockwave for Meteor Greaves — house VFX style (cached generated
// sprites, Update-driven mote list, alpha-blend, self-destroys). Intensity scales with the
// fall power (0..1): a small drop puffs dust, a big one cracks a dust ring and throws debris.
public class MeteorGreavesVFX : MonoBehaviour
{
    private class Mote
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 vel;
        public float gravity;      // downward accel (debris arcs; dust ignores)
        public float spin;
        public float life, maxLife;
        public Color baseColor;
        public Vector3 baseScale;
        public float scaleGain;    // uniform scale added over lifetime (ring expands)
    }

    private static Sprite dotSprite, ringSprite, chipSprite;
    private static readonly Color DustColor = new Color(0.78f, 0.66f, 0.48f);
    private static readonly Color DustDark  = new Color(0.55f, 0.45f, 0.32f);

    private readonly List<Mote> motes = new List<Mote>();

    // radius = shockwave world radius (matches the damage circle); power01 scales intensity.
    public static void Play(Vector3 pos, float radius, float power01)
    {
        EnsureSprites();
        var vfx = new GameObject("MeteorGreavesVFX").AddComponent<MeteorGreavesVFX>();
        vfx.transform.position = pos;

        // Expanding dust ring — grows to roughly the damage radius. Its final scale is
        // reached via scaleGain, so start small and bloom outward.
        float ringLife = Mathf.Lerp(0.3f, 0.5f, power01);
        vfx.AddMote(ringSprite, DustColor, pos, Vector2.zero, 0f, 0f, ringLife,
                    radius * 0.5f, radius * 1.7f, 60);

        // Impact flash core.
        vfx.AddMote(dotSprite, Color.Lerp(DustColor, Color.white, 0.6f), pos, Vector2.zero, 0f, 0f,
                    0.14f, radius * 0.9f, 0f, 61);

        // Dust puffs kicked out low along the ground, both directions.
        int puffs = Mathf.RoundToInt(Mathf.Lerp(6f, 16f, power01));
        for (int i = 0; i < puffs; i++)
        {
            float side = (i % 2 == 0) ? 1f : -1f;
            float ang = Random.Range(8f, 45f) * Mathf.Deg2Rad;
            float speed = Random.Range(2.5f, 6f) * Mathf.Lerp(0.7f, 1.4f, power01);
            Vector2 vel = new Vector2(Mathf.Cos(ang) * side, Mathf.Sin(ang)) * speed;
            Color c = Color.Lerp(DustColor, DustDark, Random.value);
            vfx.AddMote(dotSprite, c, pos, vel, 0f, 0f,
                        Random.Range(0.35f, 0.6f), Random.Range(0.5f, 0.9f) * radius * 0.4f,
                        Random.Range(0.4f, 1.1f), 59);
        }

        // Debris chips arc up and fall back (only meaningful on harder landings).
        int chips = Mathf.RoundToInt(Mathf.Lerp(3f, 12f, power01));
        for (int i = 0; i < chips; i++)
        {
            float ang = Random.Range(55f, 125f) * Mathf.Deg2Rad;
            float speed = Random.Range(4f, 9f) * Mathf.Lerp(0.8f, 1.5f, power01);
            Vector2 vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
            vfx.AddMote(chipSprite, DustDark, pos, vel, Random.Range(-540f, 540f), 22f,
                        Random.Range(0.4f, 0.75f), Random.Range(0.12f, 0.24f), 0f, 62);
        }
    }

    private void Update()
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

            if (m.gravity != 0f) m.vel.y -= m.gravity * dt;
            m.t.position += (Vector3)(m.vel * dt);
            m.vel.x *= 1f - 2.5f * dt;                    // horizontal drag
            if (m.spin != 0f) m.t.Rotate(0f, 0f, m.spin * dt);

            float t01 = 1f - m.life / m.maxLife;
            float fade = 1f - t01;
            Color c = m.baseColor;
            c.a *= fade;
            m.sr.color = c;
            m.t.localScale = m.baseScale * (1f + m.scaleGain * t01);
        }

        if (!anyAlive) Destroy(gameObject);
    }

    private void AddMote(Sprite sprite, Color color, Vector3 pos, Vector2 vel, float spin,
                         float gravity, float life, float scale, float scaleGain, int order)
    {
        SpriteRenderer sr = NewSR(transform, sprite, color, order);
        sr.transform.position = pos;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        sr.transform.localScale = Vector3.one * scale;
        motes.Add(new Mote
        {
            t = sr.transform, sr = sr, vel = vel, spin = spin, gravity = gravity,
            life = life, maxLife = life, baseColor = color,
            baseScale = Vector3.one * scale, scaleGain = scaleGain,
        });
    }

    // ---- sprite generation (cached for the session) ----

    private static void EnsureSprites()
    {
        if (dotSprite != null) return;
        dotSprite = GenSprite(48, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01(1f - d);
            return a * a;
        });
        ringSprite = GenSprite(64, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.18f);
        });
        chipSprite = GenSprite(16, (x, y) =>
            (Mathf.Abs(x) < 0.6f && Mathf.Abs(y) < 0.6f) ? 1f : 0f);   // small solid shard
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
