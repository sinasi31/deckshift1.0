using UnityEngine;
using UnityEngine.UI;

// A drift of tiny embers across a UI panel — the forge breathing in the background.
//
// Attach to any RectTransform with UIEmberField.Attach(rect, count, colour). It builds its own
// Image dots as children and animates them; there is nothing to wire and no particle system.
//
// TWO THINGS THAT WOULD BREAK THIS IF CHANGED:
//   1. It runs on Time.unscaledDeltaTime. Every screen this belongs on pauses the game
//      (GameManager.RequestPause sets timeScale to 0), so scaled time would freeze the embers
//      solid the instant the panel opened.
//   2. It re-reads the parent's rect every frame. The forge window's height changes with its
//      content, so a bounds snapshot taken at build time would leave embers drifting outside a
//      collapsed panel.
//
// Embers rise from along the bottom edge and drift EITHER way as they climb, with a slow sideways
// sway on top. The lateral speed is biased toward zero so most travel near-vertically and only a
// few peel off to a side — when every ember slid the same direction it read as wind rather than as
// rising heat. Each one fades in, drifts, and fades out before wrapping, so nothing visibly pops.
public class UIEmberField : MonoBehaviour
{
    // Per-screen motion. The field is shared, the BEHAVIOUR is not: the Forge's embers rise fast
    // from the fire below, Blompo's motes settle slowly downward and twinkle. Same system, opposite
    // reading.
    public struct Settings
    {
        public Vector2 riseSpeed;    // vertical px/sec; NEGATIVE values fall
        public float lateralMax;     // max sideways drift, either direction
        public Vector2 sizeRange;
        public Vector2 lifeRange;
        public float swayAmpMax;
        public float alphaScale;
        public bool twinkle;
        public Sprite sprite;        // null -> FlatUI.EmberDot()

        // Rising heat off a forge fire.
        public static Settings Embers => new Settings
        {
            riseSpeed = new Vector2(10f, 26f),
            lateralMax = 14f,
            sizeRange = new Vector2(1.6f, 3.6f),
            lifeRange = new Vector2(4.5f, 9f),
            swayAmpMax = 7f,
            alphaScale = 1f,
            twinkle = false,
            sprite = null,
        };

        // Settling motes of light. Slower, cooler, drifting DOWN, and they twinkle — magic coming
        // to rest on the cards rather than heat escaping upward.
        public static Settings Motes => new Settings
        {
            riseSpeed = new Vector2(-9f, -3f),
            lateralMax = 7f,
            sizeRange = new Vector2(1.5f, 3.2f),
            lifeRange = new Vector2(6f, 12f),
            swayAmpMax = 11f,
            alphaScale = 0.9f,
            twinkle = true,
            sprite = null,
        };
    }

    private struct Ember
    {
        public RectTransform rt;
        public Image img;
        public Vector2 pos;
        public Vector2 vel;
        public float swayAmp, swaySpeed, phase;
        public float age, life, peakAlpha, size;
        public float twinkleSpeed, twinklePhase;
    }

    private Ember[] embers;
    private RectTransform host;
    private Color tint;
    private Settings cfg;

    // Kept inside the plate's border and clear of the cut corners.
    private const float MARGIN = 18f;

    public static UIEmberField Attach(RectTransform parent, int count, Color colour, Settings? settings = null)
    {
        GameObject go = new GameObject("EmberField", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        UIEmberField f = go.AddComponent<UIEmberField>();
        f.host = parent;
        f.tint = colour;
        f.cfg = settings ?? Settings.Embers;
        f.Build(count);
        return f;
    }

    private void Build(int count)
    {
        embers = new Ember[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("Ember", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image img = go.AddComponent<Image>();
            img.sprite = cfg.sprite != null ? cfg.sprite : FlatUI.EmberDot();
            img.raycastTarget = false;

            embers[i].rt = rt;
            embers[i].img = img;

            Respawn(ref embers[i], true);
        }
    }

    // `scatter` seeds an ember anywhere in the panel (used once at build time) so the field is
    // already alive when the screen opens instead of visibly filling from the bottom.
    private void Respawn(ref Ember e, bool scatter)
    {
        Rect r = host != null ? host.rect : new Rect(0, 0, 600f, 400f);
        float halfW = Mathf.Max(40f, r.width * 0.5f - MARGIN);
        float halfH = Mathf.Max(30f, r.height * 0.5f - MARGIN);

        // Enter from the edge travel STARTS at: bottom for rising embers, top for falling motes.
        bool rising = cfg.riseSpeed.y > 0f;
        float x = Random.Range(-halfW, halfW);
        float entry = rising ? Random.Range(-halfH, -halfH * 0.72f) : Random.Range(halfH * 0.72f, halfH);
        float y = scatter ? Random.Range(-halfH, halfH) : entry;
        e.pos = new Vector2(x, y);

        // Lateral drift can go EITHER way. Squaring a signed random keeps the bulk near-vertical
        // while letting a few peel off to one side — every particle sliding the same direction
        // read as wind rather than as rising heat.
        float lateral = Random.Range(-1f, 1f);
        lateral *= Mathf.Abs(lateral);
        e.vel = new Vector2(lateral * cfg.lateralMax, Random.Range(cfg.riseSpeed.x, cfg.riseSpeed.y));
        e.swayAmp = Random.Range(cfg.swayAmpMax * 0.3f, cfg.swayAmpMax);
        e.swaySpeed = Random.Range(0.5f, 1.4f);
        e.phase = Random.Range(0f, Mathf.PI * 2f);

        e.life = Random.Range(cfg.lifeRange.x, cfg.lifeRange.y);
        e.age = scatter ? Random.Range(0f, e.life * 0.8f) : 0f;

        // Smaller particles burn brighter — it keeps the field from looking like uniform dust.
        e.size = Random.Range(cfg.sizeRange.x, cfg.sizeRange.y);
        e.peakAlpha = Mathf.Lerp(0.55f, 0.20f, Mathf.InverseLerp(cfg.sizeRange.x, cfg.sizeRange.y, e.size))
                      * cfg.alphaScale;

        e.twinkleSpeed = Random.Range(1.6f, 3.4f);
        e.twinklePhase = Random.Range(0f, Mathf.PI * 2f);

        e.rt.sizeDelta = new Vector2(e.size * 4f, e.size * 4f);   // sprite is mostly halo
    }

    private void Update()
    {
        if (embers == null) return;

        float dt = Time.unscaledDeltaTime;   // the panel pauses the game; scaled time is frozen
        if (dt <= 0f || dt > 0.25f) return;  // skip the huge first frame after a domain reload

        Rect r = host != null ? host.rect : new Rect(0, 0, 600f, 400f);
        float halfW = Mathf.Max(40f, r.width * 0.5f - MARGIN);
        float halfH = Mathf.Max(30f, r.height * 0.5f - MARGIN);

        for (int i = 0; i < embers.Length; i++)
        {
            embers[i].age += dt;

            // Retire on either side, and off whichever vertical edge this field travels toward.
            bool offVertical = cfg.riseSpeed.y > 0f
                ? embers[i].pos.y > halfH
                : embers[i].pos.y < -halfH;

            if (embers[i].age >= embers[i].life || offVertical || Mathf.Abs(embers[i].pos.x) > halfW)
            {
                Respawn(ref embers[i], false);
                continue;
            }

            embers[i].pos += embers[i].vel * dt;

            float sway = Mathf.Sin(embers[i].age * embers[i].swaySpeed + embers[i].phase) * embers[i].swayAmp;
            embers[i].rt.anchoredPosition = new Vector2(embers[i].pos.x + sway, embers[i].pos.y);

            // Fade in over the first fifth, hold, fade out over the last third.
            float t = embers[i].age / embers[i].life;
            float fade = Mathf.Min(Mathf.Clamp01(t / 0.2f), Mathf.Clamp01((1f - t) / 0.33f));

            // Twinkle: a slow brightness flicker, so motes read as points of light catching
            // something rather than as solid dots sliding down the panel.
            float shimmer = cfg.twinkle
                ? 0.62f + 0.38f * Mathf.Sin(embers[i].age * embers[i].twinkleSpeed + embers[i].twinklePhase)
                : 1f;

            Color c = tint;
            c.a = embers[i].peakAlpha * fade * shimmer;
            embers[i].img.color = c;
        }
    }
}
