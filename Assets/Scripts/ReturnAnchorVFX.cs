using UnityEngine;

// The marker "Second Thoughts" leaves behind — the spot the card will snap you back to.
// House pattern: procedural, no prefab, no art (same as DashAfterimage / PhaseBoundary).
//
// ⚠️ THE INWARD MOTION IS THE POINT, AND IT IS THE WHOLE DESIGN.
// The first version was a fat ring orbited by chevrons, and it read as an area-of-effect around the
// player rather than as a place. It described the wrong thing. Everything here now CONTRACTS onto a
// single spot and snuffs out there — that is the card's entire sentence ("you come back to this
// point") said in one gesture, and it is deliberately the inverse of every other world marker in the
// game: Glass Wail's ripples expand, the Phase bubble holds a static boundary you may not cross, the
// portal ring is a fixed circle. Nothing else moves inward, so this is unmistakable.
//
// It is also small. A marker that claims a wide area is lying about what it does, and the player
// usually stands on this one, so anything big is occluded by the character anyway.
//
// Amber, kept clear of the violet Phase bubble and the cyan portal ring, so three different
// "a card put something in the world" markers never read as each other.
public class ReturnAnchorVFX : MonoBehaviour
{
    private static readonly Color AnchorColor = new Color(1f, 0.72f, 0.28f, 1f);

    private const int SEGMENTS = 40;
    // ⚠️ FLAT ENOUGH TO BE ON THE FLOOR. At 0.4 the outer ring reached the player's waist and read as
    // a hoop AROUND the character rather than a mark beneath them. A ground decal in a side-on game
    // has to be much flatter than the maths suggests before the eye puts it on the floor plane.
    private const float FLATTEN = 0.24f;
    private const float SPOT_R = 0.62f;      // the decal, and where the rings land
    private const float RING_START = 1.8f;
    private const float RING_PERIOD = 1.5f;
    private const float GROW_TIME = 0.3f;
    private const float FADE_TIME = 0.25f;

    private LineRenderer decal;
    private LineRenderer[] rings;
    private SpriteRenderer core;
    private SpriteRenderer beacon;           // faint upward shaft: findable when you are across the room

    private float age;
    private bool dismissing;
    private float dismissAge;

    private static Sprite cachedDot;
    private static Material cachedLineMat;

    public static ReturnAnchorVFX Spawn(Vector2 feetPos)
    {
        var go = new GameObject("ReturnAnchor");
        go.transform.position = new Vector3(feetPos.x, feetPos.y + 0.12f, 0f);

        // Belt-and-braces: a marker must never outlive the room that owns it. The card clears it
        // explicitly on room change, but one left behind would point at a place that no longer
        // exists — exactly the runtime-spawn class of bug this project has been bitten by twice.
        go.AddComponent<TemporaryObject>();

        var vfx = go.AddComponent<ReturnAnchorVFX>();
        vfx.Build();
        return vfx;
    }

    private void Build()
    {
        beacon = MakeSprite("Beacon", 38);
        core   = MakeSprite("Core", 39);

        decal = MakeLine("Decal", 0.05f, 41);
        rings = new LineRenderer[2];
        for (int i = 0; i < rings.Length; i++) rings[i] = MakeLine("Ring" + i, 0.055f, 40);
    }

    public void Dismiss()
    {
        if (dismissing) return;
        dismissing = true;
        dismissAge = 0f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;

        float scale, alpha;
        if (dismissing)
        {
            dismissAge += dt;
            float t = Mathf.Clamp01(dismissAge / FADE_TIME);
            scale = Mathf.Lerp(1f, 0.35f, t * t);     // collapses INWARD, finishing the idea
            alpha = 1f - t;
            if (t >= 1f) { Destroy(gameObject); return; }
        }
        else
        {
            float t = Mathf.Clamp01(age / GROW_TIME);
            scale = Mathf.Lerp(0.35f, 1f, 1f - (1f - t) * (1f - t));
            alpha = t;
        }

        float breathe = 0.75f + 0.25f * Mathf.Sin(age * 3f);
        float spot = SPOT_R * scale;

        SetEllipse(decal, spot, spot * FLATTEN);
        decal.startColor = decal.endColor = Tint(alpha * breathe);

        for (int i = 0; i < rings.Length; i++)
        {
            float t = Mathf.Repeat(age / RING_PERIOD + (float)i / rings.Length, 1f);
            float r = Mathf.Lerp(RING_START, SPOT_R, t * t) * scale;
            SetEllipse(rings[i], r, r * FLATTEN);

            // Zero at both ends, so the loop point is invisible. A ring snapping back outward would
            // undo the inward reading everything here depends on.
            float fade = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((1f - t) / 0.2f);
            rings[i].startColor = rings[i].endColor = Tint(alpha * fade * 0.85f);
        }

        core.transform.localScale = new Vector3(spot * 2.4f, spot * 2.4f * FLATTEN, 1f);
        core.color = Tint(alpha * 0.12f * breathe);

        // Kept very low: this renders through the scene's 0.5-intensity global Light2D in LINEAR
        // colour space, where a plausible-sounding alpha composites far brighter than it reads on
        // paper. It only has to say "something is over there", not light the room.
        beacon.transform.localPosition = new Vector3(0f, 1.5f * scale, 0f);
        beacon.transform.localScale = new Vector3(spot * 1.1f, 3.4f * scale, 1f);
        beacon.color = Tint(alpha * 0.05f * breathe);
    }

    private Color Tint(float a) => new Color(AnchorColor.r, AnchorColor.g, AnchorColor.b, a);

    private void SetEllipse(LineRenderer lr, float rx, float ry)
    {
        for (int i = 0; i < SEGMENTS; i++)
        {
            float a = (float)i / SEGMENTS * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, 0f));
        }
    }

    private LineRenderer MakeLine(string name, float width, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.material = GetLineMaterial();
        lr.useWorldSpace = false;          // local: the object already sits on the anchor
        lr.loop = true;
        lr.positionCount = SEGMENTS;
        lr.startWidth = lr.endWidth = width;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.sortingOrder = order;
        return lr;
    }

    private SpriteRenderer MakeSprite(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDot();
        sr.sortingOrder = order;
        return sr;
    }

    private static Material GetLineMaterial()
    {
        if (cachedLineMat == null) cachedLineMat = new Material(Shader.Find("Sprites/Default"));
        return cachedLineMat;
    }

    // Soft radial dot, 1 world unit at scale 1. Doubles as the core glow and the beacon shaft.
    private static Sprite GetDot()
    {
        if (cachedDot != null) return cachedDot;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        float half = S * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        cachedDot = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return cachedDot;
    }
}
