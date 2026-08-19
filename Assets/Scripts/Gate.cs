using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A stone slab that sinks into a slot in the floor. Driven by Lever/ShiftAltar via
// Open()/Close()/Toggle(); the level importer wires those automatically ('G' marker).
//
// REBUILT FROM SCRATCH 2026-08-19. The old version was a uniform five-step descent that faded the
// gate out as it went, with no sound at all. Three things were wrong with it, and the fixes are the
// whole design of this file:
//
//  1. IT WAS SILENT. All 13 gates in the project had `moveSound` unassigned, so a three-tonne slab
//     dropped into the floor and made no noise whatsoever. That was most of why it felt like
//     nothing was happening. There are now four procedural clips (ProcSfx.Gate*) covering the four
//     beats of the movement.
//
//  2. IT FADED OUT. A stone slab does not become transparent. It faded because the sprite draws
//     ABOVE the ground tilemap, so without the fade you would watch it slide down over the floor.
//     It is now CLIPPED by a SpriteMask at the floor line instead, and stays fully opaque — it
//     genuinely disappears into the floor. See BuildSlotMask.
//
//  3. IT MOVED AT A CONSTANT RATE. Five equal steps at equal spacing reads as a lift, not as a
//     falling weight. The descent now ACCELERATES (k*k, which is what gravity actually does) and
//     the ratchet catches are spaced by DISTANCE, so they arrive faster and faster as it picks up
//     speed. That acceleration is the single thing that makes it read as heavy.
//
// The sequence is STRAIN -> CATCH -> DROP -> SEAT, and the CATCH (a beat of complete stillness
// before it gives) is doing more work than any other part. Weight is communicated by the pause
// before the movement, not by the movement.
public class Gate : MonoBehaviour
{
    [Tooltip("Local offset the gate slides by when opening. The importer sets this to (0, -height) so it sinks into the floor.")]
    public Vector2 openOffset = new Vector2(0f, -4f);

    [SerializeField] private bool startOpen = false;
    [Tooltip("Ratchet catches over the full travel. They are spaced by DISTANCE, so a taller gate gets more of them.")]
    [SerializeField] private float catchSpacing = 0.42f;
    [SerializeField, Range(0f, 2f)] private float volume = 1f;

    // --- movement shape (seconds / world units) ---
    private const float StrainRise = 0.16f;   // how far it presses UP against the catch first
    private const float StrainTime = 0.26f;
    private const float CatchHold  = 0.11f;   // the beat of stillness. do not remove.
    private const float DropTime   = 0.72f;
    private const float HeaveTime  = 1.05f;   // closing is SLOWER: it is being winched against gravity
    private const float Overshoot  = 0.13f;

    private Vector3 closedPos;
    private bool isOpen;
    private Coroutine mover;
    private AudioSource sfxSource;
    private float baseHalfHeight = 2f;
    private SpriteMask slotMask;

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
        closedPos = transform.position;
        var box = GetComponent<BoxCollider2D>();
        if (box != null) baseHalfHeight = box.size.y * 0.5f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        BuildSlotMask();
    }

    void Start()
    {
        if (startOpen)
        {
            isOpen = true;
            transform.position = closedPos + (Vector3)openOffset;
            // No SetAlpha here any more: below the floor line the mask clips it away completely.
        }
    }

    // The floor line the slab vanishes at. Everything ABOVE it is inside the mask and visible;
    // everything below is outside it and simply is not drawn.
    //
    // ⚠️ The mask is a SIBLING, not a child. It is the SLOT — it belongs to the floor and must not
    // travel with the gate. Parenting it to our own parent also means the room owning it destroys
    // it, so it cannot outlive the room (the class of bug LevelManager.ClearRuntimeSpawns exists for).
    //
    // ⚠️ It is only as WIDE as the gate, deliberately. Sprite masks ACCUMULATE: a renderer is drawn
    // wherever ANY mask covers it, so a screen-wide mask on one gate would un-hide a second gate
    // sunk into its own slot elsewhere. Measured across the project, the closest two gates in any
    // room are 8 units apart, so a ~3.5-wide local mask can never reach a neighbour.
    private void BuildSlotMask()
    {
        var vis = GetComponentInChildren<SpriteRenderer>(true);
        if (vis == null) return;

        float floorY = closedPos.y - baseHalfHeight;
        float w = vis.bounds.size.x + 1.2f;
        float h = baseHalfHeight * 2f + 2f;

        var go = new GameObject(name + " SlotMask");
        go.transform.SetParent(transform.parent, true);
        go.transform.position = new Vector3(closedPos.x, floorY + h * 0.5f, closedPos.z);
        go.transform.localScale = new Vector3(w, h, 1f);

        slotMask = go.AddComponent<SpriteMask>();
        slotMask.sprite = GetSquareSprite();
        vis.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
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
        mover = StartCoroutine(open ? OpenRoutine() : CloseRoutine());
    }

    // STRAIN -> CATCH -> DROP -> SEAT.
    private IEnumerator OpenRoutine()
    {
        Vector3 open = closedPos + (Vector3)openOffset;

        // STRAIN — the mechanism takes the weight and the slab presses up against its catch.
        // Deliberately audible before it is visible: the groan swells with no attack.
        Play(ProcSfx.GateGroan, 0.85f);
        yield return MoveOver(transform.position, closedPos + Vector3.up * StrainRise, StrainTime, true);

        // CATCH — complete stillness. This is the beat that sells the weight; without it the drop
        // reads as a lift going down rather than as something being let go.
        yield return new WaitForSeconds(CatchHold);

        Play(ProcSfx.GateRelease, 1f);
        Shake(0.13f, 0.20f);
        SpawnDust(SlotPoint(), 5);

        // DROP — accelerating (k*k is free fall), with pawl catches spaced by DISTANCE so they
        // arrive faster and faster. Nothing about this loop is uniform, and that is the point.
        Vector3 from = transform.position;
        float travel = Mathf.Abs(from.y - open.y);
        int catches = Mathf.Max(3, Mathf.RoundToInt(travel / Mathf.Max(0.05f, catchSpacing)));
        int fired = 0;
        float t = 0f;

        while (t < DropTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / DropTime);
            float eased = k * k;                       // gravity
            transform.position = Vector3.Lerp(from, open, eased);

            EmitSlotDust(eased);

            int shouldHave = Mathf.FloorToInt(eased * catches);
            while (fired < shouldHave && fired < catches)
            {
                fired++;
                // pitch rises as it speeds up — the pawl skipping faster over the rack
                Play(ProcSfx.GateRatchet, 0.55f, 0.92f + 0.30f * eased);
                Shake(0.05f + 0.04f * eased, 0.09f);
                SpawnDust(SlotPoint(), 2);
            }
            yield return null;
        }

        transform.position = open;

        // SEAT — the floor takes it.
        Play(ProcSfx.GateSeat, 1f);
        Shake(0.34f, 0.60f);   // a tonne of rock landing: on par with the Moss Knight slam
        SpawnDust(SlotPoint(), 14);
        mover = null;
    }

    // Closing is the inverse and is deliberately SLOWER and DECELERATING: the slab is being winched
    // up against its own weight, so it loses speed as it rises instead of gaining it.
    private IEnumerator CloseRoutine()
    {
        Vector3 from = transform.position;
        Vector3 top = closedPos + Vector3.up * Overshoot;

        Play(ProcSfx.GateGroan, 0.7f, 0.88f);

        float travel = Mathf.Abs(from.y - top.y);
        int catches = Mathf.Max(3, Mathf.RoundToInt(travel / Mathf.Max(0.05f, catchSpacing)));
        int fired = 0;
        float t = 0f;

        while (t < HeaveTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / HeaveTime);
            float eased = 1f - (1f - k) * (1f - k);    // ease OUT — running out of strength
            transform.position = Vector3.Lerp(from, top, eased);

            EmitSlotDust(1f - eased);

            int shouldHave = Mathf.FloorToInt(eased * catches);
            while (fired < shouldHave && fired < catches)
            {
                fired++;
                // pitch FALLS as it slows: the exact inverse of the drop, so the two are told apart
                // with the eyes shut.
                Play(ProcSfx.GateRatchet, 0.5f, 1.18f - 0.28f * eased);
                Shake(0.045f, 0.08f);
                yield return new WaitForSeconds(0.02f);   // the winch pausing between pulls
            }
            yield return null;
        }

        // settle back down off the overshoot and seat hard.
        yield return MoveOver(top, closedPos, 0.09f, false);
        Play(ProcSfx.GateSeat, 0.9f, 1.06f);
        Shake(0.28f, 0.45f);
        SpawnDust(SlotPoint(), 10);
        mover = null;
    }

    private IEnumerator MoveOver(Vector3 a, Vector3 b, float dur, bool easeOut)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            if (easeOut) k = 1f - (1f - k) * (1f - k);
            transform.position = Vector3.Lerp(a, b, k);
            yield return null;
        }
        transform.position = b;
    }

    // Where the slab meets the floor — the mouth of the slot, and the only place debris makes sense.
    private Vector3 SlotPoint()
    {
        return new Vector3(closedPos.x, closedPos.y - baseHalfHeight, 0f);
    }

    // A trickle of grit thrown out of the slot the whole time it is moving, densest where it is
    // fastest. The old gate only made dust at its clunks, so between them nothing was happening.
    private float dustAccum;
    private void EmitSlotDust(float speed01)
    {
        dustAccum += Time.deltaTime * (6f + 26f * speed01);
        while (dustAccum >= 1f)
        {
            dustAccum -= 1f;
            SpawnDust(SlotPoint(), 1);
        }
    }

    private void Play(AudioClip clip, float vol, float pitch = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.pitch = pitch;
        SfxManager.PlayOn(sfxSource, clip, vol * volume);
    }

    // ⚠️ CameraShake.Shake is (INTENSITY, DURATION) — every other caller in the project passes it
    // that way (boss death is 0.6 over 1.6s). The gate this replaced had the two REVERSED, so its
    // "slam" asked for 0.12 intensity over 0.14s while the Moss Knight's slam gets 0.28 over 0.8s.
    // Being an order of magnitude under every other impact in the game is part of why a falling
    // stone slab registered as nothing.
    private void Shake(float intensity, float duration)
    {
        if (CameraShake.instance != null) CameraShake.instance.Shake(intensity, duration);
    }

    private void SpawnDust(Vector3 pos, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("GateDust");
            go.transform.position = pos + new Vector3(Random.Range(-0.55f, 0.55f), Random.Range(0f, 0.14f), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetDotSprite();
            sr.color = DustColor;
            sr.sortingOrder = 12;
            float size = Random.Range(0.16f, 0.34f);
            go.transform.localScale = Vector3.one * size;
            motes.Add(new Mote
            {
                sr = sr,
                vel = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(0.8f, 2.2f)),
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

    // flat white unit square, for the slot mask
    private static Sprite squareSprite;
    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white); tex.Apply();
        squareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }
}
