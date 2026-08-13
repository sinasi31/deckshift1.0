using UnityEngine;

// House-style procedural containment bubble for the Phase card (no prefab, no art — same pattern
// as DashAfterimage / PortalRangeRing / CardAimIndicator). Marks the world-anchored sphere the
// player may travel inside while phasing.
//
// It is deliberately NOT parented to the player: the whole point of the bubble is that it stays
// where the card was CAST while the player moves around inside it, so the player can read how much
// room they have left. Parenting it would make it follow them and say nothing.
//
// Visual language is shared with PortalRangeRing (a rotating ring of soft dashes = "this is the
// limit of the card you're using") but pitched violet rather than cyan, so Phase and Portal read as
// different cards at a glance.
//
// ⚠️ IT OWNS ITS OWN LIFETIME. PhaseRoutine calls Collapse() on the normal path, but a phase can
// also end by the coroutine being killed — dying mid-Phase does exactly that. Watching the player's
// own isPhasing flag covers every one of those paths, including ones not written yet. Same lesson
// as EnemyHealthBar self-destructing when its follow target vanishes.
public class PhaseBoundary : MonoBehaviour
{
    // ⚠️ THE CONTINUOUS LINE IS THE BOUNDARY; THE DASHES ARE ONLY TEXTURE ON IT.
    // A first pass drew dashes alone, as PortalRangeRing does. That works at the portal's small
    // radius, but a 6-unit bubble is a ~37-unit circumference, and the same dash count spread over
    // it reads as scattered specks — the exact "sparse floating crumbs" failure the tile painter
    // warns about. A LineRenderer holds a constant WORLD-space width at any radius, so the circle
    // stays crisp however far phaseMaxRadius is tuned; the dashes ride on top as moving beads and
    // carry the contact flare, which a LineRenderer can't do (its gradient can't track a hotspot).
    private const int RING_SEGMENTS = 72;
    private const float DASH_SPACING = 0.5f;        // world units of arc between dashes
    private const float SPIN_SPEED = 10f;           // degrees per second
    private const float GROW_TIME = 0.22f;
    private const float COLLAPSE_TIME = 0.28f;

    // Contact flare: the boundary lights up where the player presses against it, so hitting the
    // edge reads as hitting something rather than as the controls dying.
    private const float FLARE_START = 0.72f;        // fraction of the radius where the flare begins
    private const float FLARE_ARC = 42f;            // degrees either side of the player that light up

    private static readonly Color BubbleColor = new Color(0.72f, 0.55f, 1f, 0.9f);

    private PlayerController player;
    private Vector2 anchor;
    private float radius;

    private LineRenderer ring;                      // the continuous membrane
    private SpriteRenderer[] dashes;
    private float[] dashAngles;                     // degrees, base angle before spin
    private SpriteRenderer fill;

    private float spin;
    private float age;
    private bool collapsing;
    private float collapseAge;

    private static Sprite cachedDotSprite;
    private static Material cachedLineMaterial;

    // `anchorWorld` is the player's BODY CENTER at cast time — the same point PlayerController
    // clamps against, so what is drawn is exactly what is enforced.
    public static PhaseBoundary Spawn(Vector2 anchorWorld, float radius, PlayerController owner)
    {
        var go = new GameObject("PhaseBoundary");
        go.transform.position = new Vector3(anchorWorld.x, anchorWorld.y, 0f);

        var b = go.AddComponent<PhaseBoundary>();
        b.player = owner;
        b.anchor = anchorWorld;
        b.radius = radius;
        b.Build();
        return b;
    }

    private void Build()
    {
        Sprite dot = GetDotSprite();

        // The membrane. World-space width, so it stays a crisp line at any radius.
        var ringGO = new GameObject("Ring");
        ringGO.transform.SetParent(transform, false);
        ring = ringGO.AddComponent<LineRenderer>();
        ring.material = GetLineMaterial();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = RING_SEGMENTS;
        ring.startWidth = ring.endWidth = 0.075f;
        ring.numCapVertices = 2;
        ring.numCornerVertices = 2;
        ring.sortingOrder = 40;

        // Dash count follows the circumference, so tuning phaseMaxRadius keeps the same density
        // instead of thinning the ring out.
        int dashCount = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * radius / DASH_SPACING), 24, 128);
        dashes = new SpriteRenderer[dashCount];
        dashAngles = new float[dashCount];
        for (int i = 0; i < dashCount; i++)
        {
            dashAngles[i] = (float)i / dashCount * 360f;

            var go = new GameObject("Dash" + i);
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dot;
            sr.color = BubbleColor;
            sr.sortingOrder = 41;              // beads sit on top of the membrane
            dashes[i] = sr;
        }

        // Faint interior so the bubble reads as a volume rather than a hoop. Kept very low:
        // the project renders in LINEAR colour space, so a small alpha of a saturated colour
        // composites far brighter than the number suggests.
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(transform, false);
        fill = fillGO.AddComponent<SpriteRenderer>();
        fill.sprite = dot;
        fill.sortingOrder = 39;                     // beneath the dashes
        fill.transform.localScale = Vector3.one * (radius * 2f);
    }

    // Normal end-of-phase exit. Safe to call more than once.
    public void Collapse()
    {
        if (collapsing) return;
        collapsing = true;
        collapseAge = 0f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Watchdog: the phase ended by a path that never reached PhaseRoutine's cleanup
        // (death mid-Phase kills the coroutine outright). Collapse anyway.
        if (!collapsing && (player == null || !player.IsPhasing)) Collapse();

        age += dt;
        spin += SPIN_SPEED * dt;

        // Grow on spawn, contract on collapse. Both are scale-and-fade on the same two values,
        // so the bubble arrives and leaves as the same object.
        float scale, alpha;
        if (collapsing)
        {
            collapseAge += dt;
            float t = Mathf.Clamp01(collapseAge / COLLAPSE_TIME);
            scale = Mathf.Lerp(1f, 0.82f, t * t);
            alpha = 1f - t;
            if (t >= 1f) { Destroy(gameObject); return; }
        }
        else
        {
            float t = Mathf.Clamp01(age / GROW_TIME);
            scale = Mathf.Lerp(0.55f, 1f, 1f - (1f - t) * (1f - t));   // ease-out
            alpha = t;
        }

        float r = radius * scale;

        // Where is the player against the edge? Drives the contact flare.
        float pressT = 0f;
        float playerAngle = 0f;
        if (player != null)
        {
            Vector2 offset = player.BiteCenter - anchor;
            float dist = offset.magnitude;
            if (dist > 0.0001f)
            {
                playerAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                pressT = Mathf.Clamp01((dist / radius - FLARE_START) / (1f - FLARE_START));
            }
        }

        float breathe = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 2.4f);

        if (ring != null)
        {
            for (int i = 0; i < RING_SEGMENTS; i++)
            {
                float a = (float)i / RING_SEGMENTS * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            Color rc = BubbleColor;
            rc.a *= alpha * breathe * 0.7f;      // the beads carry the highlights; this is the base
            ring.startColor = ring.endColor = rc;
        }

        for (int i = 0; i < dashes.Length; i++)
        {
            SpriteRenderer sr = dashes[i];
            if (sr == null) continue;

            float deg = dashAngles[i] + spin;
            float rad = deg * Mathf.Deg2Rad;

            // How strongly this dash is being pressed: only near the player, only near the edge.
            float align = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(deg, playerAngle)) / FLARE_ARC);
            float flare = pressT * align * align;

            // Flared dashes bulge outward a touch — the field gives before it holds.
            float dr = r + flare * 0.16f;
            sr.transform.localPosition = new Vector3(Mathf.Cos(rad) * dr, Mathf.Sin(rad) * dr, 0f);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, deg + 90f);   // long axis along the tangent
            sr.transform.localScale = new Vector3(0.46f + flare * 0.18f, 0.15f + flare * 0.05f, 1f);

            Color c = BubbleColor;
            c.a *= alpha * Mathf.Lerp(breathe, 1f, flare) * (1f + flare * 1.2f);
            sr.color = c;
        }

        if (fill != null)
        {
            fill.transform.localScale = Vector3.one * (r * 2f);
            fill.color = new Color(BubbleColor.r, BubbleColor.g, BubbleColor.b, 0.05f * alpha);
        }
    }

    private static Material GetLineMaterial()
    {
        if (cachedLineMaterial == null)
            cachedLineMaterial = new Material(Shader.Find("Sprites/Default"));
        return cachedLineMaterial;
    }

    // Soft radial dot, 64px, 1 world unit at scale 1. Stretched per-dash into an energy streak.
    // NOTE: PortalRangeRing and CardAimIndicator carry byte-identical copies of this builder.
    // Worth consolidating into one shared procedural-sprite helper if a fourth ever appears.
    private static Sprite GetDotSprite()
    {
        if (cachedDotSprite != null) return cachedDotSprite;

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
                a *= a;   // soft falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        cachedDotSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return cachedDotSprite;
    }
}
