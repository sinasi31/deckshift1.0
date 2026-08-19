using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A pair of heavy wooden doors hung in a stone archway. Driven by Lever/ShiftAltar via
// Open()/Close()/Toggle(); the level importer wires those automatically ('G' marker).
//
// REBUILT 2026-08-20. The previous version slid the whole thing DOWN into the floor and left it
// there. Two things were wrong with that, and the second one is the real bug:
//
//  1. IT SANK THE MASONRY. "TX Dungeon Props - Gate 01" is not a portcullis - it is a stone arch
//     with a pair of solid wooden double doors hung inside it, ring handles and all. Sliding the
//     entire archway into the ground is not something masonry does, which is exactly why the
//     designer reported that it "does not make sense". A double door opens. So now the arch stays
//     bolted in the wall and the two LEAVES open, each narrowing toward its own hinge - which is
//     how this art pack draws its own door animations ("Door Wood 01" runs 37px wide down to 11px
//     at a constant height). The pieces are cut by Editor/GateArtBaker.
//
//  2. THE COLLIDER NEVER MOVED. Nothing in the old file ever touched the BoxCollider2D - opening
//     just translated the transform, so the solid box travelled with it and came to rest in the
//     open space BELOW the floor. Measured in GenLevel8: closed it spans y 21->24, open y 18->21,
//     while the floor tile is only y 20->21 thick. That left an invisible 1x2 wall standing in
//     playable space under the floor, in every room with a gate. The gate is now genuinely GONE
//     when open: the collider is disabled, so the doorway is clear and nothing lingers anywhere.
//
// ORDERING RULE (inherited from the project's earlier swing work, and it still holds): opening
// drops the collider FIRST, closing restores it LAST. The passage must never be solid at a moment
// the doors visibly are not. The reverse ordering lets a player be stopped by an open doorway, or
// sealed inside a door still swinging shut.
//
// The old sequence's best idea is kept: a beat of COMPLETE STILLNESS before the movement. Weight is
// communicated by the pause before a thing gives, not by the travel itself.
public class Gate : MonoBehaviour
{
    [SerializeField] private bool startOpen = false;
    [SerializeField, Range(0f, 2f)] private float volume = 1f;

    // --- movement shape (seconds) ---
    private const float BoltTime = 0.26f;   // the bar drawing back; nothing has moved yet
    private const float StillHold = 0.10f;  // the beat. do not remove.
    private const float StrainTime = 0.30f; // they crack open, grudgingly
    private const float SwingTime = 0.62f;
    private const float ShutTime = 0.70f;   // closing accelerates into the slam
    private const float CrackOpen = 0.13f;  // how far "cracked open" is, in openness units

    // A door turned edge-on still shows its thickness, so the leaves never reach zero width. Going
    // to 0 would make them vanish, which reads as the doors being deleted rather than opened.
    private const float LeafOpenScale = 0.07f;

    private BoxCollider2D solid;
    private bool isOpen;
    private Coroutine mover;
    private AudioSource sfxSource;

    private GateArt art;
    private Transform visual;          // the importer's "Visual" child; carries the fit scale
    private SpriteRenderer archSr, passageSr, leafLSr, leafRSr;
    private Transform leafL, leafR;
    private float visScale = 1f;
    // Captured ONCE. The shudder must never read the live position as its rest pose - see Shudder.
    private Vector3 visualHome;

    // ---- dust motes (procedural, Update-driven) ----
    private class Mote
    {
        public SpriteRenderer sr;
        public Vector2 vel;
        public float life, maxLife, size;
    }
    private readonly List<Mote> motes = new List<Mote>();
    private static Sprite dotSprite;
    private static readonly Color DustColor = new Color(0.52f, 0.45f, 0.38f, 0.85f);

    void Awake()
    {
        solid = GetComponent<BoxCollider2D>();

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        BuildVisual();
    }

    void Start()
    {
        if (startOpen)
        {
            isOpen = true;
            SetLeaves(1f);
            if (solid != null) solid.enabled = false;
        }
    }

    // Re-dress the importer's single-sprite gate into arch + passage + two leaves.
    //
    // Done at RUNTIME, from whatever the room prefab already has, rather than by changing the
    // importer and re-importing the rooms. GenLevel7/8/9 carry hand edits that a re-import would
    // destroy, and re-importing also renumbers every fileID, which drops them out of
    // LevelManager.roomPrefabs (see CLAUDE.md). Touching only the component is free.
    private void BuildVisual()
    {
        visual = transform.Find("Visual");
        if (visual == null)
        {
            var anySr = GetComponentInChildren<SpriteRenderer>(true);
            if (anySr != null) visual = anySr.transform;
        }
        if (visual == null) return;

        archSr = visual.GetComponent<SpriteRenderer>();
        if (archSr == null) return;
        visScale = visual.localScale.x;
        visualHome = visual.localPosition;

        // ⚠️ A gate may carry MORE THAN ONE visual. GenLevel9 shipped with two identical "Visual"
        // children stacked exactly on top of each other (same sprite, position, scale and sorting
        // order) - the same duplicate-prop shape as the nested ExitDoor found in the room prefabs.
        // Only the first gets re-dressed, so the survivor would go on drawing a CLOSED gate over
        // the open one forever, and the room would look like the lever did nothing. The prefab is
        // fixed, but this guard is what stops it being a silent failure the next time.
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (c == visual) continue;
            var stray = c.GetComponent<SpriteRenderer>();
            if (stray == null) continue;
            stray.enabled = false;
            Debug.LogWarning("Gate '" + name + "': disabled a duplicate visual child '" + c.name +
                             "'. Remove it from the room prefab - it was drawing a closed gate over the open one.");
        }

        art = Resources.Load<GateArt>("GateArt/gate01");
        // Graceful degradation: with no baked art the gate keeps its original single sprite and
        // still opens - it just does it by vanishing. A missing Resources folder must never leave
        // a room unfinishable, which is what a permanently shut gate would do.
        if (art == null || art.arch == null || art.leafL == null || art.leafR == null)
        {
            Debug.LogWarning("Gate: Resources/GateArt/gate01 missing - falling back to hide-on-open. " +
                             "Run Deckshift -> Bake Gate Art.");
            art = null;
            return;
        }

        int baseOrder = archSr.sortingOrder;
        archSr.sprite = art.arch;

        // Back to front: passage, then the leaves over it, then the arch over everything.
        //
        // The stack goes UP from the sprite's original order, never down. The Ground tilemap draws
        // at Default order 1 and the gate art is wider than the 1-tile gap it stands in, so in a
        // room where geometry flanks the opening a passage at order 0 would be swallowed by the
        // floor tiles either side. Going upward can only ever put these in front of things the
        // single sprite was already in front of.
        //
        // Ordering among the three only has to get leaves-over-passage right: the pieces are
        // complementary cuts of one image, so the arch never overlaps a leaf, even at full open
        // (a leaf shrinks toward its jamb and so stays inside the opening).
        passageSr = MakeLayer("Passage", art.passage, Vector2.zero, baseOrder, 0.02f);
        leafLSr = MakeLayer("LeafL", art.leafL, art.leafLOffset, baseOrder + 1, 0.01f);
        leafRSr = MakeLayer("LeafR", art.leafR, art.leafROffset, baseOrder + 1, 0.01f);
        archSr.sortingOrder = baseOrder + 2;
        leafL = leafLSr.transform;
        leafR = leafRSr.transform;

        SetLeaves(0f);
    }

    private SpriteRenderer MakeLayer(string layerName, Sprite sprite, Vector2 offset, int order, float z)
    {
        var go = new GameObject(layerName);
        go.transform.SetParent(visual, false);
        go.transform.localPosition = new Vector3(offset.x, offset.y, z);
        go.transform.localScale = Vector3.one;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = archSr.sortingLayerID;
        sr.sortingOrder = order;
        return sr;
    }

    // openness: 0 = shut, 1 = fully open.
    private void SetLeaves(float openness)
    {
        if (art == null || leafL == null) return;
        float s = Mathf.Lerp(1f, LeafOpenScale, openness);
        leafL.localScale = new Vector3(s, 1f, 1f);
        leafR.localScale = new Vector3(s, 1f, 1f);

        // A leaf turning away from the room catches less light. Without this the doors read as
        // being squashed flat rather than as swinging, because a pure X-scale carries no depth cue.
        //
        // Kept deliberately shallow. A first pass went to 0.52 and, measured on a half-open frame
        // in game, the leaves fell to roughly the value of the dark passage behind them - the doors
        // stopped reading as wood and the whole opening became one dark smear. The scene's
        // 0.5-intensity global Light2D already halves this, so the tint only has to hint.
        float shade = Mathf.Lerp(1f, 0.74f, openness);
        var tint = new Color(shade, shade, shade, 1f);
        if (leafLSr != null) leafLSr.color = tint;
        if (leafRSr != null) leafRSr.color = tint;
    }

    void Update()
    {
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            Mote m = motes[i];
            m.life += Time.deltaTime;
            float t = m.life / m.maxLife;
            if (t >= 1f || m.sr == null)
            {
                if (m.sr != null) Destroy(m.sr.gameObject);
                motes.RemoveAt(i);
                continue;
            }
            m.vel += Vector2.down * 3.5f * Time.deltaTime;    // dust settles
            m.vel *= 1f - 2.2f * Time.deltaTime;              // drag
            m.sr.transform.position += (Vector3)(m.vel * Time.deltaTime);
            float fade = 1f - t;
            m.sr.color = new Color(DustColor.r, DustColor.g, DustColor.b, DustColor.a * fade * fade);
            m.sr.transform.localScale = Vector3.one * m.size * (0.8f + 0.5f * t);
        }
    }

    public void Open() { Set(true); }
    public void Close() { Set(false); }
    public void Toggle() { Set(!isOpen); }

    private void Set(bool open)
    {
        if (open == isOpen) return;
        isOpen = open;
        if (mover != null) StopCoroutine(mover);
        // Interrupting can cut Shudder off mid-jitter, so put the art back on its mark before the
        // new sequence starts rather than trusting the killed routine to have cleaned up.
        if (visual != null) visual.localPosition = visualHome;
        mover = StartCoroutine(open ? OpenRoutine() : CloseRoutine());
    }

    // BOLT -> STILL -> STRAIN -> SWING -> STOP.
    private IEnumerator OpenRoutine()
    {
        // Collider FIRST (see the ordering rule at the top of this file). This is also the whole
        // fix for the old "the barricade is still there" bug: from here on nothing about this gate
        // is solid, no matter where its art happens to be mid-animation.
        if (solid != null) solid.enabled = false;

        if (art == null) { yield return FallbackOpen(); yield break; }

        // BOLT - the bar draws back. The one sharp event in the sequence, so nothing competes.
        Play(ProcSfx.GateRelease, 1f);
        Shake(0.10f, 0.18f);
        SpawnDust(SeamPoint(), 4);
        yield return Shudder(BoltTime);

        // STILL - complete stillness before it gives. The old gate's best beat, kept.
        yield return new WaitForSeconds(StillHold);

        // STRAIN - they crack open, slowly, against their own weight. Ease IN: barely moving at
        // first is what makes them feel heavy.
        Play(ProcSfx.GateGroan, 0.85f, 0.92f);
        yield return Sweep(0f, CrackOpen, StrainTime, Ease.In);
        SpawnDust(SeamPoint(), 3);

        // SWING - smoothstep, so they gather speed and then lose it into the stops rather than
        // travelling at a constant rate (which is what made the old movement read as a lift).
        Play(ProcSfx.GateGroan, 1f, 0.74f);
        yield return Sweep(CrackOpen, 1f, SwingTime, Ease.Smooth, true);

        // STOP - the leaves reach the jambs.
        SetLeaves(1f);
        Play(ProcSfx.GateSeat, 1f, 1.04f);
        Shake(0.26f, 0.45f);
        SpawnDust(JambPoint(-1f), 7);
        SpawnDust(JambPoint(1f), 7);
        mover = null;
    }

    // The inverse, and deliberately NOT a mirror image: closing accelerates the whole way and ends
    // in a single hard slam as the two leaves meet. Opening ends softly against the jambs; closing
    // ends loudly in the middle. That is what lets the two be told apart with your eyes shut.
    private IEnumerator CloseRoutine()
    {
        if (art == null) { yield return FallbackClose(); yield break; }

        Play(ProcSfx.GateGroan, 0.8f, 1.06f);
        yield return Sweep(CurrentOpenness(), 0f, ShutTime, Ease.In, true);

        // SLAM.
        SetLeaves(0f);
        Play(ProcSfx.GateSeat, 1f, 0.94f);
        Shake(0.30f, 0.50f);
        SpawnDust(SeamPoint(), 12);

        // The bar dropping home after the leaves have met - the sound that says it is shut, not
        // merely closed. Quieter and higher than the bolt that drew it back.
        yield return new WaitForSeconds(0.14f);
        Play(ProcSfx.GateRelease, 0.55f, 1.18f);

        // Collider LAST.
        if (solid != null) solid.enabled = true;
        mover = null;
    }

    private enum Ease { In, Out, Smooth }

    // Drive openness from a to b, optionally creaking as it goes.
    private IEnumerator Sweep(float a, float b, float dur, Ease ease, bool creak = false)
    {
        float t = 0f;
        int creaks = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float x = Mathf.Clamp01(t / dur);
            float k = ease == Ease.In ? x * x
                    : ease == Ease.Out ? 1f - (1f - x) * (1f - x)
                    : x * x * (3f - 2f * x);
            float openness = Mathf.Lerp(a, b, k);
            SetLeaves(openness);

            if (creak)
            {
                EmitHingeDust(openness);
                // three hinge creaks across the travel, spaced by DISTANCE rather than by time, so
                // they crowd together while the doors are moving fastest.
                int want = Mathf.FloorToInt(k * 3f);
                while (creaks < want)
                {
                    creaks++;
                    Play(ProcSfx.GateRatchet, 0.4f, 0.80f + 0.22f * k);
                }
            }
            yield return null;
        }
        SetLeaves(b);
    }

    // Back out the current openness from the leaf scale, so an interrupted open closes from
    // wherever it actually got to rather than snapping wide first.
    private float CurrentOpenness()
    {
        if (leafL == null) return isOpen ? 0f : 1f;
        float s = leafL.localScale.x;
        return Mathf.Clamp01(Mathf.InverseLerp(1f, LeafOpenScale, s));
    }

    // The doors straining against the bar before anything opens: a sub-pixel rattle, not a move.
    //
    // ⚠️ The rest pose is the value cached at build time, NOT the position when the rattle starts.
    // Set() kills a running routine with StopCoroutine, which can cut this off mid-jitter and skip
    // the restore below - so a shudder that read the LIVE position would adopt the leftover jitter
    // as its new home and drift a little further every time. Measured on a hammered
    // Open/Close/Open/Close: 0.002 units of permanent drift per interruption, silently accumulating
    // for as long as the room is loaded.
    private IEnumerator Shudder(float dur)
    {
        if (visual == null) { yield return new WaitForSeconds(dur); yield break; }
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float fall = 1f - Mathf.Clamp01(t / dur);
            float jitter = Random.Range(-0.022f, 0.022f) * fall;
            visual.localPosition = visualHome + new Vector3(jitter, 0f, 0f);
            yield return null;
        }
        visual.localPosition = visualHome;
    }

    // ---- fallbacks, used only if the baked art is missing ----
    private IEnumerator FallbackOpen()
    {
        float t = 0f;
        while (t < 0.4f) { t += Time.deltaTime; SetAlpha(1f - t / 0.4f); yield return null; }
        SetAlpha(0f);
        mover = null;
    }

    private IEnumerator FallbackClose()
    {
        float t = 0f;
        while (t < 0.4f) { t += Time.deltaTime; SetAlpha(t / 0.4f); yield return null; }
        SetAlpha(1f);
        if (solid != null) solid.enabled = true;
        mover = null;
    }

    private void SetAlpha(float a)
    {
        if (archSr == null) return;
        var c = archSr.color; c.a = a; archSr.color = c;
    }

    // ---- points of interest, in world space, derived from the baked geometry ----

    // Where the two leaves meet: dust when they part and when they slam.
    private Vector3 SeamPoint()
    {
        if (visual == null) return transform.position;
        float midY = art != null ? (art.openingBottom + art.openingTop) * 0.35f : 0f;
        return visual.TransformPoint(new Vector3(0f, midY, 0f));
    }

    // A jamb: dust when a leaf reaches its stop. side = -1 left, +1 right.
    private Vector3 JambPoint(float side)
    {
        if (visual == null) return transform.position;
        if (art == null) return visual.position;
        float x = side < 0f ? art.openingLeft : art.openingRight;
        float midY = (art.openingBottom + art.openingTop) * 0.35f;
        return visual.TransformPoint(new Vector3(x, midY, 0f));
    }

    // A trickle of grit off the hinges the whole time the doors are moving, densest where they are
    // fastest. The old gate only made dust at its clunks, so between them nothing was happening.
    private float dustAccum;
    private void EmitHingeDust(float openness)
    {
        dustAccum += Time.deltaTime * (5f + 16f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(openness)));
        while (dustAccum >= 1f)
        {
            dustAccum -= 1f;
            SpawnDust(JambPoint(Random.value < 0.5f ? -1f : 1f), 1);
        }
    }

    private void Play(AudioClip clip, float vol, float pitch = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.pitch = pitch;
        SfxManager.PlayOn(sfxSource, clip, vol * volume);
    }

    // CameraShake.Shake is (INTENSITY, DURATION) - every other caller in the project passes it that
    // way (boss death is 0.6 over 1.6s). An older gate had the two REVERSED, which is part of why a
    // three-tonne door registered as nothing.
    private void Shake(float intensity, float duration)
    {
        if (CameraShake.instance != null) CameraShake.instance.Shake(intensity, duration);
    }

    private void SpawnDust(Vector3 pos, int count)
    {
        float spread = 0.55f * Mathf.Max(0.2f, visScale);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("GateDust");
            go.transform.position = pos + new Vector3(Random.Range(-spread, spread), Random.Range(-0.1f, 0.14f), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetDotSprite();
            sr.color = DustColor;
            sr.sortingOrder = 12;
            float size = Random.Range(0.16f, 0.34f) * Mathf.Max(0.35f, visScale);
            go.transform.localScale = Vector3.one * size;
            motes.Add(new Mote
            {
                sr = sr,
                vel = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(0.5f, 1.9f)),
                life = 0f,
                maxLife = Random.Range(0.35f, 0.65f),
                size = size,
            });
        }
    }

    // soft radial dot, generated once and shared (house pattern)
    private static Sprite GetDotSprite()
    {
        if (dotSprite != null) return dotSprite;
        int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        dotSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return dotSprite;
    }
}
