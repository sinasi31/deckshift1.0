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
    private struct Ember
    {
        public RectTransform rt;
        public Image img;
        public Vector2 pos;
        public Vector2 vel;
        public float swayAmp, swaySpeed, phase;
        public float age, life, peakAlpha, size;
    }

    private Ember[] embers;
    private RectTransform host;
    private Color tint;

    // Kept inside the plate's border and clear of the cut corners.
    private const float MARGIN = 18f;

    public static UIEmberField Attach(RectTransform parent, int count, Color colour)
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
            img.sprite = FlatUI.EmberDot();
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

        // Rise from along the bottom edge, anywhere across the width.
        float x = Random.Range(-halfW, halfW);
        float y = scatter ? Random.Range(-halfH, halfH) : Random.Range(-halfH, -halfH * 0.72f);
        e.pos = new Vector2(x, y);

        // Mostly upward, with a lateral drift that can go EITHER way. Squaring a signed random
        // keeps the bulk near-vertical while letting a few peel off to one side — every ember
        // sliding the same direction read as wind rather than as rising heat.
        float lateral = Random.Range(-1f, 1f);
        lateral *= Mathf.Abs(lateral);
        e.vel = new Vector2(lateral * 14f, Random.Range(10f, 26f));
        e.swayAmp = Random.Range(2f, 7f);
        e.swaySpeed = Random.Range(0.5f, 1.4f);
        e.phase = Random.Range(0f, Mathf.PI * 2f);

        e.life = Random.Range(4.5f, 9f);
        e.age = scatter ? Random.Range(0f, e.life * 0.8f) : 0f;

        // Smaller embers burn brighter — it keeps the field from looking like uniform dust.
        e.size = Random.Range(1.6f, 3.6f);
        e.peakAlpha = Mathf.Lerp(0.55f, 0.20f, Mathf.InverseLerp(1.6f, 3.6f, e.size));

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

            // Drift is two-directional now, so retire on either side, not just the left.
            if (embers[i].age >= embers[i].life ||
                embers[i].pos.y > halfH || Mathf.Abs(embers[i].pos.x) > halfW)
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

            Color c = tint;
            c.a = embers[i].peakAlpha * fade;
            embers[i].img.color = c;
        }
    }
}
