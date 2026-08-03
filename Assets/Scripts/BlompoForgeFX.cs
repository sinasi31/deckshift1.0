using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The BINDING: the sequence that plays when Blompo blesses a card.
//
// This replaced a hammer-and-anvil forging animation (three blows, sparks, screen shake) once
// Blompo's screen moved to the arcane theme. He is a mythic creature granting a charm, not a
// blacksmith, and a percussive smithy sequence fought everything else on the panel. The whole
// motion vocabulary is inverted to match:
//
//   forging  ->  strikes, impacts, gravity, sparks flying OUT, the window rattling
//   binding  ->  orbit, convergence, weightlessness, motes drawn IN, nothing ever hit
//
// Four beats: GATHER (ring forms, motes stream in) -> DRAW (ring contracts, everything
// accelerates) -> BIND (the instant the charm sets; onSet fires here) -> SETTLE.
//
// Runs entirely in UI space under a supplied RectTransform, and on UNSCALED time throughout — the
// blessing screen pauses the game, so Time.deltaTime would never advance.
public static class BlompoForgeFX
{
    private const int RUNE_COUNT = 12;

    // GEOMETRY IS BOUNDED BY THE WINDOW, NOT THE STAGE. UI children are not clipped, so anything
    // that overruns the panel simply draws on the dark backdrop outside it — which is what a first
    // pass at 520 did, scattering runes across the whole screen.
    //
    // The stage sits 60px below the window centre in a 762-tall window, leaving ~321px of room
    // downward (the tighter of the two) and ~441px up. Everything below is sized against that,
    // and anything that needs to travel FURTHER does so on an ellipse squashed in Y (VERT_SQUASH)
    // so it can spread wide without dropping out of the bottom of the panel.
    private const float RING_START = 280f;   // radius the runes form at
    private const float RING_BOUND = 135f;   // radius they collapse to at the bind
    private const float VERT_SQUASH = 0.58f; // Y scale for anything reaching past the ring
    private const float HALO_MAX = 620f;     // halo diameter cap (310 radius, inside the 321 floor)

    /// Plays the full sequence against `card` (a chip already parented under `host`).
    /// `onSet` fires at the instant the charm binds, so the caller can apply the enhancement and
    /// re-skin the card on exactly that frame.
    ///
    /// `runner` owns the child coroutines — `host` is a plain RectTransform with no MonoBehaviour.
    ///
    /// CONTRACT: `onSet` must MUTATE the card chip, never destroy and rebuild it — this coroutine
    /// keeps animating `card` afterwards. Every frame here null-checks it anyway, so a future
    /// violation degrades into a shortened animation instead of the screen hanging forever with
    /// only the X button to escape (which is exactly what the old "gets stuck" bug was).
    public static IEnumerator Play(MonoBehaviour runner, RectTransform host, RectTransform card,
                                   Color gem, System.Action onSet)
    {
        Vector2 cardHome = card != null ? card.anchoredPosition : Vector2.zero;
        Vector3 cardScale = card != null ? card.localScale : Vector3.one;

        AudioSource audio = EnsureSource(runner);
        // The gather is a BED — it sits under the visuals and must not compete with the bind that
        // pays it off, so it's mixed noticeably lower.
        if (audio != null) SfxManager.PlayOn(audio, ProcSfx.ArcaneGather, 0.6f);

        // --- build the cast: aura behind the card, a rotating ring of runes, and inbound motes ---
        Image aura = MakeImage(host, "Aura", FlatUI.SoftGlow(), new Color(gem.r, gem.g, gem.b, 0f), 300f, cardHome);
        aura.transform.SetSiblingIndex(card != null ? card.GetSiblingIndex() : 0);

        RectTransform ring = MakePoint(host, "RuneRing", cardHome, Vector2.one * 10f);
        Image[] runes = new Image[RUNE_COUNT];
        for (int i = 0; i < RUNE_COUNT; i++)
        {
            runes[i] = MakeImage(ring, "Rune", FlatUI.FourPointStar(), new Color(gem.r, gem.g, gem.b, 0f), 34f, Vector2.zero);
            float a = (i / (float)RUNE_COUNT) * Mathf.PI * 2f;
            runes[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RING_START;
        }

        // --- BEAT 1: GATHER ---------------------------------------------------------------------
        const float gather = 0.95f;
        for (float t = 0f; t < gather; t += Time.unscaledDeltaTime)
        {
            float n = Mathf.Clamp01(t / gather);

            SetAlpha(aura, 0.30f * n);
            aura.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(300f, 430f, n);

            for (int i = 0; i < RUNE_COUNT; i++) SetAlpha(runes[i], 0.85f * n);
            ring.localRotation = Quaternion.Euler(0f, 0f, n * 55f);

            // The card drifts upward and swells a touch — lifted, not struck.
            if (card != null)
            {
                card.anchoredPosition = cardHome + Vector2.up * (26f * EaseOut(n));
                card.localScale = cardScale * (1f + 0.04f * EaseOut(n));
            }

            // Motes peel in from beyond the ring, a few per frame.
            if (Random.value < 0.55f) runner.StartCoroutine(MoteLife(host, cardHome, gem, 0.75f));
            yield return null;
        }

        // --- BEAT 2: DRAW IN --------------------------------------------------------------------
        const float draw = 0.70f;
        float spinAt = 55f;
        for (float t = 0f; t < draw; t += Time.unscaledDeltaTime)
        {
            float n = Mathf.Clamp01(t / draw);
            float e = n * n;                       // accelerate into the bind

            float radius = Mathf.Lerp(RING_START, RING_BOUND, e);
            for (int i = 0; i < RUNE_COUNT; i++)
            {
                float a = (i / (float)RUNE_COUNT) * Mathf.PI * 2f;
                runes[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                runes[i].rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(34f, 20f, e);
                SetAlpha(runes[i], Mathf.Lerp(0.85f, 1f, e));
            }

            spinAt += Mathf.Lerp(120f, 900f, e) * Time.unscaledDeltaTime;
            ring.localRotation = Quaternion.Euler(0f, 0f, spinAt);

            SetAlpha(aura, Mathf.Lerp(0.30f, 0.65f, e));
            aura.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(430f, 330f, e);

            if (card != null)
            {
                card.anchoredPosition = cardHome + Vector2.up * (26f + 10f * e);
                card.localScale = cardScale * (1f + 0.04f + 0.03f * e);
            }

            if (Random.value < 0.85f) runner.StartCoroutine(MoteLife(host, cardHome, gem, 0.42f));
            yield return null;
        }

        // --- BEAT 3: BIND -----------------------------------------------------------------------
        onSet?.Invoke();
        if (audio != null) SfxManager.PlayOn(audio, ProcSfx.ArcaneBind, 1f);

        // The runes stop orbiting and fly outward as loose sparks.
        for (int i = 0; i < RUNE_COUNT; i++)
        {
            float a = (i / (float)RUNE_COUNT) * Mathf.PI * 2f;
            runner.StartCoroutine(ScatterRune(runes[i], new Vector2(Mathf.Cos(a), Mathf.Sin(a))));
        }
        Object.Destroy(ring.gameObject, 1.2f);

        Image halo = MakeImage(host, "Halo", FlatUI.Ring(), new Color(gem.r, gem.g, gem.b, 0.95f), RING_BOUND, cardHome);
        Image flash = MakeImage(host, "Flash", FlatUI.SoftGlow(), new Color(1f, 1f, 1f, 0.85f), 260f, cardHome);

        // --- BEAT 4: SETTLE ---------------------------------------------------------------------
        const float settle = 1.05f;
        for (float t = 0f; t < settle; t += Time.unscaledDeltaTime)
        {
            float n = Mathf.Clamp01(t / settle);

            // Halo expands outward and thins away — a wave leaving, not an impact arriving.
            halo.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(RING_BOUND, HALO_MAX, EaseOut(n));
            SetAlpha(halo, Mathf.Clamp01(1f - n * 1.35f));

            // The white flash is brief; the rarity-coloured aura is what lingers.
            float fn = Mathf.Clamp01(n * 4.5f);
            flash.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(260f, 520f, fn);
            SetAlpha(flash, Mathf.Lerp(0.85f, 0f, fn));

            SetAlpha(aura, Mathf.Lerp(0.65f, 0.16f, n));
            aura.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(330f, 400f, n);

            // Card breathes once and drifts back down to rest.
            if (card != null)
            {
                float breath = Mathf.Sin(Mathf.Clamp01(n * 2.2f) * Mathf.PI) * 0.05f;
                card.localScale = cardScale * (1f + 0.07f * (1f - EaseOut(n)) + breath);
                card.anchoredPosition = cardHome + Vector2.up * (36f * (1f - EaseOut(n)));
            }
            yield return null;
        }

        if (card != null)
        {
            card.anchoredPosition = cardHome;
            card.localScale = cardScale;
        }

        Object.Destroy(halo.gameObject);
        Object.Destroy(flash.gameObject);
        Object.Destroy(aura.gameObject);
    }

    // A single mote drawn in from beyond the ring, spiralling as it falls toward the card. The
    // spiral is what sells "pulled in by something" — a straight line reads as a projectile.
    private static IEnumerator MoteLife(RectTransform host, Vector2 target, Color gem, float duration)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        // Starts OUTSIDE the rune ring, on a squashed ellipse so a wide spread still clears the
        // bottom of the panel.
        float radius = Random.Range(RING_START * 1.25f, RING_START * 1.85f);
        float spin = Random.Range(1.1f, 2.4f) * (Random.value < 0.5f ? -1f : 1f);
        float size = Random.Range(6f, 13f);

        Color tint = Random.value < 0.35f ? Color.Lerp(gem, Color.white, 0.6f) : gem;
        Image mote = MakeImage(host, "Mote", FlatUI.EmberDot(), tint, size, target);

        float life = duration * Random.Range(0.8f, 1.25f);
        for (float t = 0f; t < life; t += Time.unscaledDeltaTime)
        {
            if (mote == null) yield break;
            float n = Mathf.Clamp01(t / life);

            float r = Mathf.Lerp(radius, 12f, n * n);       // accelerates inward
            angle += spin * Time.unscaledDeltaTime;
            mote.rectTransform.anchoredPosition = target +
                new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * VERT_SQUASH);

            // Fade in at the far end, out as it arrives — nothing pops into or out of existence.
            SetAlpha(mote, Mathf.Min(Mathf.Clamp01(n / 0.2f), Mathf.Clamp01((1f - n) / 0.3f)) * 0.95f);
            mote.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(size, size * 0.4f, n);
            yield return null;
        }
        if (mote != null) Object.Destroy(mote.gameObject);
    }

    // At the bind, each rune breaks orbit and drifts outward, spinning down.
    private static IEnumerator ScatterRune(Image rune, Vector2 dir)
    {
        if (rune == null) yield break;
        Vector2 from = rune.rectTransform.anchoredPosition;
        float dist = Random.Range(140f, 260f);
        float life = Random.Range(0.5f, 0.85f);
        float spin = Random.Range(-220f, 220f);
        // Squashed outward travel, same reason as the motes: keeps the burst inside the panel.
        Vector2 travel = new Vector2(dir.x, dir.y * VERT_SQUASH);

        for (float t = 0f; t < life; t += Time.unscaledDeltaTime)
        {
            if (rune == null) yield break;
            float n = Mathf.Clamp01(t / life);
            rune.rectTransform.anchoredPosition = from + travel * (dist * EaseOut(n));
            rune.rectTransform.localRotation = Quaternion.Euler(0f, 0f, spin * n);
            rune.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(28f, 4f, n);
            SetAlpha(rune, 1f - n * n);
            yield return null;
        }
        if (rune != null) Object.Destroy(rune.gameObject);
    }

    // ---- helpers ---------------------------------------------------------------------------

    // A 2D source on the runner. UI sound must not be positional — SfxManager.PlayAtPoint is
    // distance-attenuated, which would make the blessing quieter depending on where the player
    // happened to be standing when they opened the screen.
    private static AudioSource EnsureSource(MonoBehaviour runner)
    {
        if (runner == null) return null;
        AudioSource src = runner.GetComponent<AudioSource>();
        if (src == null)
        {
            src = runner.gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
        }
        return src;
    }

    private static RectTransform MakePoint(RectTransform host, string name, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(host, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image MakeImage(Transform host, string name, Sprite sprite, Color color, float size, Vector2 pos)
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

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
