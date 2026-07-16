using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Procedural Comet Dive spectacle. Two jobs, one object:
//
//  1. TELEGRAPH — while falling, it raycasts down to the ground the player is about to hit and
//     draws the blast area there: a filled dome, a hard rim, radial ticks, a repeating sonar
//     sweep, and a guide beam connecting the comet to the target. Everything sharpens, brightens
//     and pulses faster as the player closes in, so the range is readable long before the hit.
//
//  2. THE COMET — a white-hot head wrapped in flame that streams fire and sparks upward, then
//     erupts on landing with rings that stop at EXACTLY the damage radius.
//
// The dome/arc shapes are half-circles cut at the impact plane on purpose: the damage circle's
// lower half is buried in the ground, and drawing it would paint over the level geometry.
//
// House style: no art assets, no prefab, no Inspector wiring. Sprites are generated in code and
// cached statically (same as BossDeathVFX / PhoenixRebirthVFX).
//
// The RADIUS DRAWN HERE IS THE RADIUS THAT DAMAGES. PlayerController.LandCometDive runs
// Physics2D.OverlapCircleAll(transform.position, cometRadius) — the player's root sits at the feet,
// which is the same point this telegraph centres on. If that damage call ever moves, move this too.
public class CometDiveVFX : MonoBehaviour
{
    private static readonly Color Ember = new Color(0.92f, 0.18f, 0.05f);
    private static readonly Color Flame = new Color(1f, 0.50f, 0.10f);
    private static readonly Color Dust  = new Color(0.72f, 0.66f, 0.60f);

    const int SORT_DUST = 19;
    const int SORT_TELEGRAPH = 20;
    const int SORT_COMET = 44;

    const float PREDICT_DIST = 60f;   // how far down we look for the landing surface
    const float PROX_RANGE = 10f;     // telegraph hits full intensity within this height of the ground
    const float HEAD_OFFSET = 0.9f;   // chest height — the comet wraps the player, not their feet
    const int TICKS = 9;

    private PlayerController player;
    private LayerMask groundMask;
    private float radius;
    private bool diving;

    private SpriteRenderer halo, core, nucleus, guide, domeFill, domeRim, sweep;
    private SpriteRenderer[] ticks;
    private float emitAccum;

    // Accumulated, NOT derived from Time.time. Both of these ramp their frequency up as the comet
    // closes in, and `Sin(Time.time * risingFreq)` jumps phase violently once Time.time is large
    // (hundreds of seconds into a run) — the telegraph would strobe instead of pulse.
    private float pulsePhase, sweepPhase;

    // Tail streaks and sparks are freestanding (they must stay put while the comet falls past them),
    // so the root can't clean them up by parenting. Tracked here as a leak backstop.
    private readonly List<GameObject> loose = new List<GameObject>();

    private static Sprite glowSprite, beamSprite, barSprite, domeFillSprite, domeRimSprite;

    // --- Entry points ---------------------------------------------------------

    public static CometDiveVFX Begin(PlayerController player, float radius)
    {
        GameObject go = new GameObject("CometDiveVFX");
        CometDiveVFX fx = go.AddComponent<CometDiveVFX>();
        fx.Init(player, radius);
        return fx;
    }

    // Landing burst without a preceding dive — a safety net for the case where the dive object
    // was destroyed mid-fall (scene reload, player death) but the landing still resolved.
    public static void PlayImpact(Vector3 center, float radius)
    {
        GameObject go = new GameObject("CometImpactVFX");
        go.transform.position = center;
        CometDiveVFX fx = go.AddComponent<CometDiveVFX>();
        fx.radius = Mathf.Max(0.1f, radius);
        fx.diving = false;
        fx.StartCoroutine(fx.ImpactRoutine(center));
    }

    // The dive landed. Hand over the true damage centre so the burst matches the damage circle.
    public void Land(Vector3 center)
    {
        if (!diving) return;
        diving = false;
        ClearDiveVisuals();
        transform.position = center;
        StartCoroutine(ImpactRoutine(center));
    }

    // The dive was interrupted (knockback, death, fall-respawn). Fade out, no burst.
    public void Cancel()
    {
        if (!diving) return;
        diving = false;
        StartCoroutine(FadeOutRoutine());
    }

    // --- Dive -----------------------------------------------------------------

    private void Init(PlayerController p, float r)
    {
        player = p;
        radius = Mathf.Max(0.1f, r);
        groundMask = p.groundLayer;
        diving = true;

        transform.position = p.transform.position + Vector3.up * HEAD_OFFSET;

        // The comet itself: a low-alpha halo so the player still reads through the fire, a bright
        // body, and a white-hot nucleus.
        halo    = MakeChild("Halo",    GetGlowSprite(), Flame,       SORT_COMET);
        core    = MakeChild("Core",    GetGlowSprite(), Color.Lerp(Flame, Color.white, 0.55f), SORT_COMET + 1);
        nucleus = MakeChild("Nucleus", GetGlowSprite(), Color.white, SORT_COMET + 2);

        // The telegraph. Children of the root, but positioned in world space every frame.
        guide    = MakeChild("GuideBeam", GetBeamSprite(),     Flame, SORT_TELEGRAPH);
        domeFill = MakeChild("Dome",      GetDomeFillSprite(), Ember, SORT_TELEGRAPH);
        domeRim  = MakeChild("Rim",       GetDomeRimSprite(),  Flame, SORT_TELEGRAPH + 2);
        sweep    = MakeChild("Sweep",     GetDomeRimSprite(),  Flame, SORT_TELEGRAPH + 1);

        ticks = new SpriteRenderer[TICKS];
        for (int i = 0; i < TICKS; i++)
            ticks[i] = MakeChild("Tick", GetBarSprite(), Flame, SORT_TELEGRAPH + 2);

        // Telegraph parts are placed in world space by LateUpdate. Hide them until it has run once,
        // so they can't flash at the player's chest (their local origin) on the first frame.
        SetTelegraphAlpha(0f);
    }

    private void LateUpdate()
    {
        if (!diving) return;
        if (player == null) { Cancel(); return; }

        Vector3 head = player.transform.position + Vector3.up * HEAD_OFFSET;
        transform.position = head;

        AnimateComet();
        EmitTail(head);

        Vector3 impact;
        if (TryFindGround(out impact))
            AnimateTelegraph(impact, head);
        else
            SetTelegraphAlpha(0f);
    }

    // Straight down from the feet. Skips the player, triggers, and enemies — groundLayer includes
    // the Default layer, where AeroBat and MeleeEnemy live, so an unfiltered ray would happily
    // plant the target circle on an enemy's head.
    private bool TryFindGround(out Vector3 point)
    {
        point = default;
        Vector3 origin = player.transform.position + Vector3.up * 0.15f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, PREDICT_DIST, groundMask);

        float best = float.MaxValue;
        bool found = false;
        foreach (RaycastHit2D h in hits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.transform.IsChildOf(player.transform)) continue;
            if (h.collider.GetComponentInParent<EnemyHealth>() != null) continue;
            if (h.distance < best) { best = h.distance; point = h.point; found = true; }
        }
        return found;
    }

    private void AnimateComet()
    {
        float t = Time.time;
        float pulse = 1f + 0.08f * Mathf.Sin(t * 26f);
        float flicker = 0.85f + 0.15f * Mathf.Sin(t * 41f);

        halo.transform.localScale    = Vector3.one * 2.9f * pulse;
        core.transform.localScale    = Vector3.one * 1.45f * (2f - pulse);
        nucleus.transform.localScale = Vector3.one * 0.6f * pulse;

        halo.color    = WithAlpha(Flame, 0.30f * flicker);
        core.color    = WithAlpha(Color.Lerp(Flame, Color.white, 0.55f), 0.55f);
        nucleus.color = WithAlpha(Color.white, 0.95f * flicker);
    }

    // Fire streams UP off the comet because the comet is falling. Streaks are left in world space,
    // so the trail draws itself out of the player's motion rather than being a fixed tail sprite.
    private void EmitTail(Vector3 head)
    {
        emitAccum += Time.deltaTime * 110f;
        while (emitAccum >= 1f)
        {
            emitAccum -= 1f;

            Vector3 at = head + new Vector3(Random.Range(-0.36f, 0.36f), Random.Range(-0.25f, 0.35f), 0f);
            if (Random.value < 0.75f) StartCoroutine(StreakRoutine(at));
            else                      StartCoroutine(SparkRoutine(at));
        }
    }

    private IEnumerator StreakRoutine(Vector3 at)
    {
        SpriteRenderer sr = MakeLoose("Streak", GetGlowSprite(), Color.white, at, SORT_COMET);
        float w = Random.Range(0.10f, 0.30f);
        float h = Random.Range(0.9f, 2.3f);
        Color tint = Color.Lerp(Color.white, Random.value < 0.5f ? Flame : Ember, Random.value);
        float life = Random.Range(0.12f, 0.28f), t = 0f;

        while (t < life && sr != null)
        {
            float n = t / life;
            sr.transform.localScale = new Vector3(w * (1f - n * 0.7f), h * (1f + n * 0.5f), 1f);
            sr.transform.position += Vector3.up * (2.5f * Time.deltaTime);   // fire lags behind the fall
            sr.color = WithAlpha(Color.Lerp(Color.white, tint, n), 1f - n * n);
            t += Time.deltaTime;
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    private IEnumerator SparkRoutine(Vector3 at)
    {
        SpriteRenderer sr = MakeLoose("Spark", GetGlowSprite(), Flame, at, SORT_COMET + 1);
        Vector2 vel = new Vector2(Random.Range(-2.2f, 2.2f), Random.Range(1.5f, 5.5f));
        float scale = Random.Range(0.07f, 0.18f);
        float life = Random.Range(0.18f, 0.40f), t = 0f;
        Color tint = Random.value < 0.5f ? Flame : Ember;

        while (t < life && sr != null)
        {
            float n = t / life;
            vel.y -= 6f * Time.deltaTime;
            sr.transform.position += (Vector3)(vel * Time.deltaTime);
            sr.transform.localScale = Vector3.one * scale * (1f - n * 0.5f);
            sr.color = WithAlpha(Color.Lerp(Color.white, tint, n), 1f - n);
            t += Time.deltaTime;
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    // The whole point of the rework: this is the damage circle, drawn on the ground, before the hit.
    private void AnimateTelegraph(Vector3 impact, Vector3 head)
    {
        float height = Mathf.Max(0f, head.y - impact.y);
        float prox = 1f - Mathf.Clamp01(height / PROX_RANGE);     // 0 far away, 1 about to land

        // Urgency: the rim pulses faster and whiter the closer the comet gets.
        pulsePhase += Time.deltaTime * Mathf.Lerp(6f, 26f, prox);
        float pulse = 0.82f + 0.18f * Mathf.Sin(pulsePhase);
        Color hot = Color.Lerp(Flame, Color.white, prox * 0.8f);
        float diameter = radius * 2f;

        domeFill.transform.position = impact;
        domeFill.transform.localScale = Vector3.one * diameter;
        domeFill.color = WithAlpha(Color.Lerp(Ember, Flame, prox), Mathf.Lerp(0.05f, 0.20f, prox));

        // The rim sits at exactly `radius` and never breathes past it — it is the promise the
        // landing burst has to keep. Urgency is carried by alpha, not by scale.
        domeRim.transform.position = impact;
        domeRim.transform.localScale = Vector3.one * diameter;
        domeRim.color = WithAlpha(hot, Mathf.Lerp(0.30f, 0.95f, prox) * pulse);

        // Sonar sweep: an arc that keeps racing out to the rim, so the size is unmistakable even
        // if the player is only glancing at it.
        sweepPhase = Mathf.Repeat(sweepPhase + Time.deltaTime * Mathf.Lerp(1.5f, 4.2f, prox), 1f);
        float sweepT = sweepPhase;
        sweep.transform.position = impact;
        sweep.transform.localScale = Vector3.one * diameter * Mathf.Lerp(0.12f, 1f, sweepT);
        sweep.color = WithAlpha(hot, (1f - sweepT) * Mathf.Lerp(0.20f, 0.65f, prox));

        // Radial ticks around the upper rim — a targeting reticle that snaps outward on approach.
        float tickLen = Mathf.Lerp(0.20f, 0.42f, prox);
        for (int i = 0; i < TICKS; i++)
        {
            float angle = Mathf.Lerp(12f, 168f, i / (float)(TICKS - 1));
            Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f);

            Transform tr = ticks[i].transform;
            tr.position = impact + dir * (radius + tickLen * 0.5f + 0.05f);
            tr.rotation = Quaternion.Euler(0f, 0f, angle);
            tr.localScale = new Vector3(tickLen, 0.55f, 1f);
            ticks[i].color = WithAlpha(hot, Mathf.Lerp(0.15f, 1f, prox) * pulse);
        }

        // Guide beam: brightest where it meets the ground, so the eye is pulled to the target.
        guide.transform.position = impact;
        guide.transform.localScale = new Vector3(0.7f, height, 1f);
        guide.color = WithAlpha(hot, Mathf.Lerp(0.10f, 0.45f, prox));
    }

    private void SetTelegraphAlpha(float a)
    {
        domeFill.color = WithAlpha(domeFill.color, a);
        domeRim.color = WithAlpha(domeRim.color, a);
        sweep.color = WithAlpha(sweep.color, a);
        guide.color = WithAlpha(guide.color, a);
        for (int i = 0; i < TICKS; i++) ticks[i].color = WithAlpha(ticks[i].color, a);
    }

    private void ClearDiveVisuals()
    {
        DestroyIfAny(halo); DestroyIfAny(core); DestroyIfAny(nucleus);
        DestroyIfAny(guide); DestroyIfAny(domeFill); DestroyIfAny(domeRim); DestroyIfAny(sweep);
        if (ticks != null) foreach (SpriteRenderer sr in ticks) DestroyIfAny(sr);
    }

    // Waits out the longest loose-streak lifetime before killing the root, so in-flight tail
    // coroutines finish naturally instead of freezing mid-fade.
    private IEnumerator FadeOutRoutine()
    {
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>();
        Color[] start = new Color[all.Length];
        for (int i = 0; i < all.Length; i++) start[i] = all[i].color;

        float life = 0.18f, t = 0f;
        while (t < life)
        {
            float n = 1f - (t / life);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) all[i].color = WithAlpha(start[i], start[i].a * n);
            t += Time.deltaTime;
            yield return null;
        }
        ClearDiveVisuals();
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }

    // --- Impact ---------------------------------------------------------------

    private IEnumerator ImpactRoutine(Vector3 center)
    {
        if (HitStop.instance != null) HitStop.instance.Stop(0.055f);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.3f, 1.15f);

        StartCoroutine(ImpactDomeRoutine(center));
        StartCoroutine(ImpactRimRoutine(center, 0f, 0.30f, Color.white));
        StartCoroutine(ImpactRimRoutine(center, 0.07f, 0.46f, Ember));
        StartCoroutine(GroundLineRoutine(center));
        StartCoroutine(ImpactCoreRoutine(center));
        StartCoroutine(ColumnRoutine(center));
        for (int i = 0; i < 20; i++) StartCoroutine(DebrisRoutine(center));
        for (int i = 0; i < 8; i++) StartCoroutine(DustRoutine(center, i));

        yield return new WaitForSeconds(1.9f);
        Destroy(gameObject);
    }

    // A hard flash filling exactly the area that just took damage.
    private IEnumerator ImpactDomeRoutine(Vector3 center)
    {
        SpriteRenderer sr = MakeChild("ImpactDome", GetDomeFillSprite(), Color.white, SORT_COMET);
        sr.transform.position = center;
        float life = 0.34f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            sr.transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.94f, 1f, EaseOut(n));
            sr.color = WithAlpha(Color.Lerp(Color.white, Flame, n), (1f - n) * 0.75f);
            t += Time.deltaTime;
            yield return null;
        }
        DestroyIfAny(sr);
    }

    // Rings stop dead at `radius`. They are the payoff of the telegraph — never let them overshoot,
    // or the player learns the wrong range.
    private IEnumerator ImpactRimRoutine(Vector3 center, float delay, float life, Color color)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        SpriteRenderer sr = MakeChild("ImpactRim", GetDomeRimSprite(), color, SORT_COMET + 3);
        sr.transform.position = center;
        float t = 0f;
        while (t < life)
        {
            float n = t / life;
            sr.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 2f, EaseOut(n));
            sr.color = WithAlpha(color, 1f - n);
            t += Time.deltaTime;
            yield return null;
        }
        DestroyIfAny(sr);
    }

    // A blast line racing along the surface to exactly +/- radius. Reinforces the width at a glance.
    private IEnumerator GroundLineRoutine(Vector3 center)
    {
        SpriteRenderer sr = MakeChild("GroundLine", GetBarSprite(), Color.white, SORT_COMET + 2);
        sr.transform.position = center + Vector3.up * 0.04f;
        float life = 0.38f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            sr.transform.localScale = new Vector3(Mathf.Lerp(0.4f, radius * 2f, EaseOut(n)), Mathf.Lerp(0.7f, 0.15f, n), 1f);
            sr.color = WithAlpha(Color.Lerp(Color.white, Flame, n), 1f - n);
            t += Time.deltaTime;
            yield return null;
        }
        DestroyIfAny(sr);
    }

    private IEnumerator ImpactCoreRoutine(Vector3 center)
    {
        SpriteRenderer sr = MakeChild("Flash", GetGlowSprite(), Color.white, SORT_COMET + 4);
        sr.transform.position = center;
        float life = 0.28f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            sr.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, radius * 1.3f, EaseOut(n));
            sr.color = WithAlpha(Color.Lerp(Color.white, Flame, n), 1f - n);
            t += Time.deltaTime;
            yield return null;
        }
        DestroyIfAny(sr);
    }

    private IEnumerator ColumnRoutine(Vector3 center)
    {
        SpriteRenderer sr = MakeChild("Column", GetBeamSprite(), Color.white, SORT_COMET + 1);
        sr.transform.position = center;
        float life = 0.5f, t = 0f;
        float height = radius * 1.25f;
        while (t < life)
        {
            float n = t / life;
            float grow = EaseOut(Mathf.Min(1f, n * 2.6f));
            sr.transform.localScale = new Vector3(2.4f * (1f - n * 0.4f), height * grow, 1f);
            sr.color = WithAlpha(Color.Lerp(Color.white, Ember, n), (1f - n) * 0.9f);
            t += Time.deltaTime;
            yield return null;
        }
        DestroyIfAny(sr);
    }

    private IEnumerator DebrisRoutine(Vector3 center)
    {
        SpriteRenderer sr = MakeChild("Debris", GetGlowSprite(), Flame, SORT_COMET + 2);
        sr.transform.position = center + new Vector3(Random.Range(-0.4f, 0.4f), 0.05f, 0f);

        Vector2 vel = new Vector2(Random.Range(-1f, 1f), Random.Range(0.5f, 1.5f)).normalized
                      * Random.Range(radius * 1.2f, radius * 3.1f);
        float scale = Random.Range(0.08f, 0.22f);
        float life = Random.Range(0.5f, 1.1f), t = 0f;
        Color tint = Random.value < 0.6f ? Flame : Ember;

        while (t < life)
        {
            float n = t / life;
            vel.y -= 22f * Time.deltaTime;
            sr.transform.position += (Vector3)(vel * Time.deltaTime);
            sr.transform.localScale = Vector3.one * scale * (1f - n * 0.55f);
            sr.color = WithAlpha(Color.Lerp(Color.white, tint, Mathf.Clamp01(n * 3f)), 1f - n * n);
            t += Time.deltaTime;
            yield return null;
        }
        DestroyIfAny(sr);
    }

    // Kicked-up ground dust rolling outward along the surface. Stops at the radius too.
    private IEnumerator DustRoutine(Vector3 center, int index)
    {
        float dir = (index % 2 == 0) ? 1f : -1f;
        SpriteRenderer sr = MakeChild("Dust", GetGlowSprite(), Dust, SORT_DUST);
        sr.transform.position = center + Vector3.up * 0.12f;

        float speed = Random.Range(radius * 1.4f, radius * 2.6f) * dir;
        float rise = Random.Range(0.3f, 0.9f);
        float scale = Random.Range(0.5f, 1.1f);
        float life = Random.Range(0.45f, 0.8f), t = 0f;

        while (t < life)
        {
            float n = t / life;
            sr.transform.position += new Vector3(speed * (1f - n) * Time.deltaTime, rise * (1f - n) * Time.deltaTime, 0f);
            sr.transform.localScale = Vector3.one * scale * (0.5f + n * 1.3f);
            sr.color = WithAlpha(Dust, (1f - n) * 0.35f);
            t += Time.deltaTime;
            yield return null;
        }
        DestroyIfAny(sr);
    }

    // --- Plumbing -------------------------------------------------------------

    private void OnDestroy()
    {
        foreach (GameObject go in loose)
            if (go != null) Destroy(go);
    }

    private SpriteRenderer MakeChild(string n, Sprite sprite, Color color, int order)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private SpriteRenderer MakeLoose(string n, Sprite sprite, Color color, Vector3 pos, int order)
    {
        GameObject go = new GameObject(n);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        loose.Add(go);
        return sr;
    }

    private static void DestroyIfAny(SpriteRenderer sr) { if (sr != null) Destroy(sr.gameObject); }
    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    // --- Procedural sprites (cached + shared) ---------------------------------

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
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return glowSprite;
    }

    // Vertical beam, bottom pivot, brightest at the base. 1 world unit tall at scale 1.
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
                float hx = Mathf.Clamp01(1f - Mathf.Abs(x - cx) / cx); hx *= hx;
                float vy = 1f - (y / (float)(h - 1));
                px[y * w + x] = new Color32(255, 255, 255, (byte)(hx * vy * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        beamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);
        return beamSprite;
    }

    // Rounded bar, centre pivot. 1 world unit long at scale 1, so localScale.x IS its length.
    private static Sprite GetBarSprite()
    {
        if (barSprite != null) return barSprite;
        int w = 64, h = 16;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float ax = Mathf.Clamp01((cx - Mathf.Abs(x - cx)) / 3f);
                float ay = Mathf.Clamp01((cy - Mathf.Abs(y - cy)) / 2f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Min(ax, ay) * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        barSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        return barSprite;
    }

    // Upper half of a circle, pivot at the circle's centre (bottom-centre of the texture).
    // pixelsPerUnit = w, so the circle's radius is 0.5 units and localScale = radius * 2 maps
    // straight onto the world-space damage radius.
    private static Sprite GetDomeFillSprite()
    {
        if (domeFillSprite != null) return domeFillSprite;
        int w = 192, h = 96;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float cx = (w - 1) * 0.5f, r = w * 0.5f;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + y * y);
                float edge = Mathf.Clamp01((r - 1f - d) / 3f);          // crisp rim, 3px feather
                float fill = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(d / r)); // brighter toward the rim
                px[y * w + x] = new Color32(255, 255, 255, (byte)(edge * fill * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        domeFillSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
        return domeFillSprite;
    }

    private static Sprite GetDomeRimSprite()
    {
        if (domeRimSprite != null) return domeRimSprite;
        int w = 192, h = 96;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float cx = (w - 1) * 0.5f, outer = w * 0.5f - 1f, band = w * 0.028f, inner = outer - band, feather = 1.6f;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + y * y);
                float ao = Mathf.Clamp01((outer - d) / feather);
                float ai = Mathf.Clamp01((d - inner) / feather);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Min(ao, ai) * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        domeRimSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
        return domeRimSprite;
    }
}
