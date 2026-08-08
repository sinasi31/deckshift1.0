using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A heavy stone portcullis. OPEN: anticipation jerk, then a ratcheting descent
// into the floor — step, clunk, dust — fading away near the end. CLOSE: slams
// back up with an overshoot bounce and a dust burst. Camera rumbles throughout.
// Dust is procedural (house style: generated sprites, Update-driven motes).
// Driven by Lever/ShiftAltar via Open()/Close()/Toggle(); the level importer
// wires those automatically ('G' marker).
public class Gate : MonoBehaviour
{
    [Tooltip("Local offset the gate slides by when opening. The importer sets this to (0, -height) so it sinks into the floor.")]
    public Vector2 openOffset = new Vector2(0f, -4f);
    [SerializeField] private bool startOpen = false;
    [SerializeField] private int ratchetSteps = 5;
    [SerializeField] private AudioClip moveSound;   // played per ratchet step + on slam
    [SerializeField, Range(0f, 2f)] private float moveVolume = 1f;

    private Vector3 closedPos;
    private bool isOpen;
    private Coroutine mover;
    private AudioSource sfxSource;
    private SpriteRenderer[] renderers;
    private float baseHalfHeight = 2f;

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
        renderers = GetComponentsInChildren<SpriteRenderer>();
        var box = GetComponent<BoxCollider2D>();
        if (box != null) baseHalfHeight = box.size.y * 0.5f;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
    }

    void Start()
    {
        if (startOpen)
        {
            isOpen = true;
            transform.position = closedPos + (Vector3)openOffset;
            SetAlpha(0f);
        }
    }

    void Update()
    {
        // animate dust
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
            m.sr.transform.localScale = Vector3.one * m.size * (0.8f + 0.5f * t); // puffs expand as they fade
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

    // Heavy descent: jerk up, then ratchet down step by step, fading near the end.
    private IEnumerator OpenRoutine()
    {
        Vector3 from = transform.position;
        Vector3 to = closedPos + (Vector3)openOffset;

        // anticipation: the mechanism takes the weight
        yield return MoveOver(from, from + Vector3.up * 0.07f, 0.08f);
        Clunk(0.05f, 0.04f, 2);

        Vector3 top = transform.position;
        int steps = Mathf.Max(2, ratchetSteps);
        for (int i = 1; i <= steps; i++)
        {
            Vector3 a = Vector3.Lerp(top, to, (i - 1) / (float)steps);
            Vector3 b = Vector3.Lerp(top, to, i / (float)steps);
            yield return MoveOver(a, b, 0.07f);
            Clunk(0.05f, 0.05f, 3);
            // fade out over the last third of the descent
            float prog = i / (float)steps;
            if (prog > 0.66f) SetAlpha(1f - (prog - 0.66f) / 0.34f);
            yield return new WaitForSeconds(0.045f);
        }
        transform.position = to;
        SetAlpha(0f);
        Clunk(0.09f, 0.09f, 6); // final thud
        mover = null;
    }

    // Slam shut: fast rise, overshoot, settle, dust burst.
    private IEnumerator CloseRoutine()
    {
        SetAlpha(1f);
        Vector3 from = transform.position;
        Vector3 overshoot = closedPos + Vector3.up * 0.10f;

        float t = 0f, dur = 0.20f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - (1f - Mathf.Clamp01(t / dur)) * (1f - Mathf.Clamp01(t / dur)); // ease-out
            transform.position = Vector3.Lerp(from, overshoot, k);
            yield return null;
        }
        yield return MoveOver(overshoot, closedPos, 0.06f);
        Clunk(0.12f, 0.14f, 8); // SLAM
        mover = null;
    }

    private IEnumerator MoveOver(Vector3 a, Vector3 b, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(a, b, Mathf.Clamp01(t / dur));
            yield return null;
        }
        transform.position = b;
    }

    // shake + sound + dust at the gate's base, scaled to the moment
    private void Clunk(float shakeDur, float shakeAmp, int dustCount)
    {
        if (CameraShake.instance != null) CameraShake.instance.Shake(shakeDur, shakeAmp);
        if (moveSound != null) SfxManager.PlayOn(sfxSource, moveSound, moveVolume);
        Vector3 basePos = new Vector3(transform.position.x, closedPos.y - baseHalfHeight, 0f);
        SpawnDust(basePos, dustCount);
    }

    private void SpawnDust(Vector3 pos, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("GateDust");
            go.transform.position = pos + new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(0f, 0.12f), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetDotSprite();
            sr.color = DustColor;
            sr.sortingOrder = 12;
            float size = Random.Range(0.18f, 0.34f);
            go.transform.localScale = Vector3.one * size;
            motes.Add(new Mote
            {
                sr = sr,
                vel = new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(0.7f, 1.9f)),
                life = 0f,
                maxLife = Random.Range(0.35f, 0.6f),
                size = size,
            });
        }
    }

    private void SetAlpha(float a)
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            Color c = r.color;
            c.a = a;
            r.color = c;
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
