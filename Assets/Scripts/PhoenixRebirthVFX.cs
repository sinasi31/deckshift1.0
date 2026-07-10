using System.Collections;
using UnityEngine;

// Procedural "Phoenix Cog" rebirth spectacle — plays the instant a lethal hit is refused and the
// player is pulled back to 1 HP. Fully self-building (no art assets, no prefab) and self-destroying,
// in the same house style as BossDeathVFX / ChestOpenVFX / ShockwaveVFX: sprites are generated in
// code and cached statically.
//
// It runs on its OWN GameObject rather than on the player, so it survives the hit-stop and keeps
// directing the sequence. The core + wings are children, so they ride along with the player for the
// first beat (LateUpdate follow); the rings, pillar and embers are freestanding world sprites left
// behind at the point of rebirth.
//
// Beats: freeze-frame + fiery screen flare + slow-mo -> white-hot core and a pillar of fire ->
// two wings of embers unfurl and beat once -> scouring shockwave rings (the 60-dmg blast) ->
// embers spiral up out of the ashes.
public class PhoenixRebirthVFX : MonoBehaviour
{
    private static readonly Color Ember = new Color(0.95f, 0.22f, 0.06f);
    private static readonly Color Flame = new Color(1f, 0.55f, 0.12f);
    private static readonly Color Gold  = new Color(1f, 0.85f, 0.35f);

    const int SORT = 45;              // above the world and the boss-death burst
    const int FEATHERS = 14;          // per wing

    private Transform follow;
    private AudioClip sound;
    private float volume;
    private bool started;
    private bool slowActive;          // guards the global time-scale so OnDestroy can always restore it
    private float followUntil;        // unscaled timestamp: wings stop tracking the player after this

    private static Sprite glowSprite, ringSprite, beamSprite, featherSprite;

    public void Play(Transform follow, AudioClip sound, float volume)
    {
        this.follow = follow;
        this.sound = sound;
        this.volume = volume;
        if (!started) { started = true; StartCoroutine(Run()); }
    }

    private void LateUpdate()
    {
        // Core + wings are children, so keeping the root on the player makes them ride along while
        // the phoenix "wears" them. After the first beat we let go and the embers stay behind.
        if (follow != null && Time.unscaledTime < followUntil)
            transform.position = follow.position + Vector3.up * 0.9f;
    }

    private IEnumerator Run()
    {
        followUntil = Time.unscaledTime + 1.3f;
        Vector3 center = transform.position;

        if (sound != null)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;    // 2D — a rebirth should be heard, not localised
            SfxManager.PlayOn(src, sound, volume);
        }

        // --- Beat 1: the death that wasn't. Freeze, flare, and a hard shake. ---
        if (HitStop.instance != null) HitStop.instance.Stop(0.14f);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.7f, 1.7f);
        StartCoroutine(ScreenFlashRoutine(new Color(1f, 0.62f, 0.22f, 0.85f), 0.5f));
        StartCoroutine(SlowMoRoutine());

        // --- Beat 2: a white-hot heart reignites, and fire erupts upward. ---
        StartCoroutine(CoreRoutine());
        StartCoroutine(BeamRoutine(center, 6.5f));

        // --- Beat 3: the wings unfurl and beat once. ---
        StartCoroutine(WingRoutine(+1));
        StartCoroutine(WingRoutine(-1));

        // --- Beat 4: the shockwave that scours the room (this is the 60-dmg blast made visible). ---
        StartCoroutine(RingRoutine(center, 0.00f, 5.5f, Flame));
        StartCoroutine(RingRoutine(center, 0.10f, 4.0f, Gold));
        StartCoroutine(RingRoutine(center, 0.24f, 7.0f, Ember));

        // --- Beat 5: embers spiral up out of the ashes. ---
        for (int i = 0; i < 26; i++) StartCoroutine(EmberRoutine(center));

        yield return new WaitForSeconds(3f);
        Destroy(gameObject);
    }

    // Brief, robust slow-mo. Uses unscaled time internally and always restores the time-scale (also in
    // OnDestroy) so a fire-and-forget object can never leave the game running slow.
    private IEnumerator SlowMoRoutine()
    {
        yield return new WaitForSecondsRealtime(0.16f);   // let the freeze-frame land first
        slowActive = true;
        Time.timeScale = 0.3f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(0.5f);

        float t = 0f, ramp = 0.55f;
        while (t < ramp)
        {
            Time.timeScale = Mathf.Lerp(0.3f, 1f, t / ramp);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        slowActive = false;
    }

    private void OnDestroy()
    {
        if (slowActive) { Time.timeScale = 1f; Time.fixedDeltaTime = 0.02f; }
    }

    // The white-hot heart: a hard flare that settles into a pulsing gold ember and fades.
    private IEnumerator CoreRoutine()
    {
        SpriteRenderer sr = MakeChildSprite("Core", GetGlowSprite(), Color.white, SORT + 6);
        float life = 1.6f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            float flare = n < 0.12f
                ? Mathf.Lerp(0.5f, 4.2f, n / 0.12f)                       // snap open
                : Mathf.Lerp(4.2f, 1.1f, EaseOut((n - 0.12f) / 0.88f));   // then breathe down
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 14f);
            sr.transform.localScale = Vector3.one * flare * pulse;

            Color c = Color.Lerp(Color.white, Gold, Mathf.Clamp01(n * 2f));
            c.a = 1f - n * n;
            sr.color = c;

            t += Time.deltaTime;
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    // One wing. Feathers all pivot from a shoulder point and fan outward, so lengthening them
    // "unfurls" the wing; then a single sine-driven beat sweeps the whole fan upward.
    // side = +1 right wing, -1 left wing (mirrored by reflecting the feather angle).
    private IEnumerator WingRoutine(int side)
    {
        SpriteRenderer[] srs = new SpriteRenderer[FEATHERS];
        for (int i = 0; i < FEATHERS; i++)
            srs[i] = MakeChildSprite("Feather", GetFeatherSprite(), Gold, SORT + 5);

        float life = 1.15f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            float unfurl = EaseOut(Mathf.Clamp01(n / 0.28f));                             // snap open
            float beat = Mathf.Sin(Mathf.Clamp01((n - 0.3f) / 0.55f) * Mathf.PI) * 26f;   // one downbeat->up

            for (int i = 0; i < FEATHERS; i++)
            {
                if (srs[i] == null) continue;
                float f = i / (float)(FEATHERS - 1);

                float angle = Mathf.Lerp(-18f, 82f, f) + beat;          // fan, swept up by the beat
                float length = Mathf.Lerp(0.9f, 2.4f, Mathf.Sin(f * Mathf.PI));  // longest mid-wing (world units)
                float thickness = Mathf.Lerp(0.6f, 0.28f, f);           // ~0.25u at the root, tapering out

                Transform tr = srs[i].transform;
                tr.localPosition = new Vector3(side * 0.25f, 0.05f, 0f);              // shoulder
                tr.localRotation = Quaternion.Euler(0f, 0f, side > 0 ? angle : 180f - angle);
                tr.localScale = new Vector3(length * unfurl, thickness, 1f);

                Color c = Color.Lerp(Gold, Ember, f);                  // gold at the root, ember at the tips
                c = Color.Lerp(Color.white, c, Mathf.Clamp01(n * 3f)); // white-hot for the first instant
                c.a = (1f - n * n) * Mathf.Lerp(1f, 0.75f, f);
                srs[i].color = c;
            }

            t += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < FEATHERS; i++)
            if (srs[i] != null) Destroy(srs[i].gameObject);
    }

    // Full-screen colour flash that fades out. Self-contained ScreenSpaceOverlay canvas, unscaled so it
    // plays cleanly through the freeze-frame; destroys itself when done.
    private IEnumerator ScreenFlashRoutine(Color color, float duration)
    {
        GameObject canvasGO = new GameObject("PhoenixFlash");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;

        GameObject imgGO = new GameObject("Flash");
        imgGO.transform.SetParent(canvasGO.transform, false);
        UnityEngine.UI.Image img = imgGO.AddComponent<UnityEngine.UI.Image>();
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        float startA = color.a, t = 0f;
        while (t < duration)
        {
            float n = 1f - (t / duration);
            Color c = color; c.a = startA * n * n; img.color = c;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Destroy(canvasGO);
    }

    // Expanding shockwave ring.
    private IEnumerator RingRoutine(Vector3 pos, float delay, float maxR, Color color)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        SpriteRenderer sr = MakeSprite("Ring", GetRingSprite(), color, pos, SORT + 1);
        float life = 0.6f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            sr.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, maxR, EaseOut(n)) * 2f;
            Color c = color; c.a = 1f - n; sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // A tall pillar of fire bursting upward, then fading.
    private IEnumerator BeamRoutine(Vector3 pos, float height)
    {
        SpriteRenderer sr = MakeSprite("Pillar", GetBeamSprite(), Color.Lerp(Flame, Color.white, 0.35f), pos, SORT);
        float life = 0.8f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            float grow = EaseOut(Mathf.Min(1f, n * 2.5f));
            sr.transform.localScale = new Vector3(1.1f * (1f - n * 0.35f), height * grow, 1f);
            Color c = Color.Lerp(Color.white, Flame, n);
            c.a = (1f - n) * 0.95f;
            sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // A single ember drifting up out of the ashes, swaying and twinkling out.
    private IEnumerator EmberRoutine(Vector3 center)
    {
        float roll = Random.value;
        Color tint = roll < 0.4f ? Flame : (roll < 0.75f ? Gold : Ember);

        SpriteRenderer sr = MakeSprite("Ember", GetGlowSprite(), tint,
            center + (Vector3)(Random.insideUnitCircle * 0.7f), SORT + 4);
        Transform tr = sr.transform;

        float rise = Random.Range(1.6f, 4.2f);
        float swayAmp = Random.Range(0.15f, 0.7f);
        float swayFreq = Random.Range(1.5f, 3.5f);
        float phase = Random.value * 6.28f;
        float baseX = tr.position.x;
        float scale = Random.Range(0.08f, 0.24f);
        float life = Random.Range(0.9f, 1.9f), t = 0f;
        Color emberColor = Color.Lerp(tint, Color.white, 0.35f);

        while (t < life)
        {
            float n = t / life;
            Vector3 p = tr.position;
            p.y += rise * Time.deltaTime * (1f - n * 0.4f);          // rises, slowing as it cools
            p.x = baseX + Mathf.Sin(Time.time * swayFreq + phase) * swayAmp * n;
            tr.position = p;

            float twinkle = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 20f + phase);
            tr.localScale = Vector3.one * scale * (1f - n * 0.6f) * twinkle;
            Color c = emberColor; c.a = (1f - n) * twinkle; sr.color = c;

            t += Time.deltaTime;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    private SpriteRenderer MakeSprite(string n, Sprite sprite, Color color, Vector3 pos, int order)
    {
        GameObject go = new GameObject(n);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    // Parented to this object so it follows the player during the first beat.
    private SpriteRenderer MakeChildSprite(string n, Sprite sprite, Color color, int order)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    // --- Procedural sprites (cached + shared). ---

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
                px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Min(ao, ai) * 255f));
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
                float hx = Mathf.Clamp01(1f - Mathf.Abs(x - cx) / cx); hx *= hx;
                float vy = 1f - (y / (float)(h - 1));
                px[y * w + x] = new Color32(255, 255, 255, (byte)(hx * vy * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        beamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);   // bottom pivot
        return beamSprite;
    }

    // A soft, tapering feather. Pivot sits at the BASE (left-centre) so a feather rotates and
    // lengthens out of the shoulder — scaling X unfurls it.
    private static Sprite GetFeatherSprite()
    {
        if (featherSprite != null) return featherSprite;
        int w = 96, h = 40;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float cy = (h - 1) * 0.5f;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);                              // 0 at base, 1 at tip
                float halfWidth = Mathf.Sin((1f - u) * Mathf.PI * 0.5f);   // tapers toward the tip
                float dy = Mathf.Abs(y - cy) / cy;
                float a = Mathf.Clamp01(1f - dy / Mathf.Max(0.08f, halfWidth));
                a *= a;
                a *= Mathf.Lerp(1f, 0.25f, u);                             // dims toward the tip
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        // pixelsPerUnit = w so the feather is exactly 1 world unit long at scale 1 — that lets
        // WingRoutine treat localScale.x directly as the feather's length in world units.
        featherSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), w);  // base pivot
        return featherSprite;
    }
}
