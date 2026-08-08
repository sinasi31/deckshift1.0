using System.Collections.Generic;
using UnityEngine;

// Procedural crescent slash for Freefall Blade — house VFX style (cached generated
// sprites, Update-driven, alpha-blend, self-destroys).
//
// Glint streaks appear one after another along a ")" arc that runs from ahead of the
// player down to under their feet — the stagger IS the swipe. Each streak drifts
// outward slightly and dies fast. Empowered (played while falling, 2x damage) draws
// a bigger, denser, warmer arc so the bonus is unmistakable.
public class FreefallBladeVFX : MonoBehaviour
{
    private class Streak
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 vel;
        public float delay;       // seconds until this streak appears (swipe stagger)
        public float life, maxLife;
        public Color baseColor;
        public Vector3 baseScale;
    }

    private static Sprite streakSprite;
    private static readonly Color GlassWhite = new Color(0.85f, 0.97f, 1f);
    private static readonly Color MomentumGold = new Color(1f, 0.85f, 0.55f);

    private readonly List<Streak> streaks = new List<Streak>();

    public static void Spawn(Vector3 origin, bool facingRight, bool empowered, float range)
    {
        EnsureSprite();
        var vfx = new GameObject("FreefallBladeVFX").AddComponent<FreefallBladeVFX>();
        vfx.transform.position = origin;

        int count = empowered ? 13 : 9;
        float radius = range * (empowered ? 1.05f : 0.9f);
        Color color = empowered ? MomentumGold : GlassWhite;
        float scale = empowered ? 0.85f : 0.65f;

        // Arc angles relative to facing: +55° (up-front) sweeping down to -115° (below,
        // slightly behind the feet) — the ")" bracket. Mirrored for facing left.
        for (int i = 0; i < count; i++)
        {
            float t01 = i / (float)(count - 1);
            float angDeg = Mathf.Lerp(55f, -115f, t01);
            float worldAng = facingRight ? angDeg : 180f - angDeg;
            float rad = worldAng * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            var go = new GameObject("streak");
            go.transform.SetParent(vfx.transform, false);
            go.transform.position = origin + (Vector3)(dir * radius);
            // Rotate the vertical sliver to lie tangent to the arc.
            go.transform.rotation = Quaternion.Euler(0f, 0f, worldAng);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = streakSprite;
            sr.color = color;
            sr.sortingOrder = 60;
            sr.enabled = false;   // hidden until its stagger delay elapses

            float life = empowered ? 0.18f : 0.14f;
            vfx.streaks.Add(new Streak
            {
                t = go.transform, sr = sr,
                vel = dir * (empowered ? 3f : 2f),
                delay = t01 * (empowered ? 0.09f : 0.07f),
                life = life, maxLife = life,
                baseColor = color,
                baseScale = new Vector3(scale, scale * 1.7f, 1f),
            });
        }
    }

    private void Update()
    {
        bool anyAlive = false;
        float dt = Time.deltaTime;

        for (int i = 0; i < streaks.Count; i++)
        {
            Streak s = streaks[i];
            if (s.t == null) continue;

            if (s.delay > 0f)
            {
                s.delay -= dt;
                anyAlive = true;
                if (s.delay <= 0f) s.sr.enabled = true;
                else continue;
            }

            if (s.life <= 0f) continue;
            s.life -= dt;
            if (s.life <= 0f) { s.sr.enabled = false; continue; }
            anyAlive = true;

            s.t.position += (Vector3)(s.vel * dt);
            float t01 = 1f - s.life / s.maxLife;
            float fade = 1f - t01;
            Color c = s.baseColor;
            c.a = fade * fade;
            s.sr.color = c;
            s.t.localScale = s.baseScale * (1f + 0.5f * t01);
        }

        if (!anyAlive) Destroy(gameObject);
    }

    private static void EnsureSprite()
    {
        if (streakSprite != null) return;
        // Elongated sliver: thin horizontally, long vertically (rotated per-streak).
        streakSprite = GenSprite(32, (x, y) =>
        {
            float v = 1f - (Mathf.Abs(x) / 0.22f + Mathf.Abs(y) / 0.95f);
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
}
