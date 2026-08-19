using UnityEngine;
using UnityEngine.UI;

// Points the player at the way out.
//
// WHY IT EXISTS: the generated rooms are roughly 2.5x the area of the hand-made ones, and the exit
// is deliberately placed far from the spawn (Level Design Law 7 - entry and exit must sit in
// different regions so a Phase or Portal card cannot skip the level). Together those mean the exit
// is usually somewhere off screen with no indication of which way, and the designer reported not
// knowing where the level ends.
//
// THE MATERIAL: chalk on stone. A wayfinding mark somebody scratched on the wall to say "out is
// this way" - which is a real dungeon-crawling habit, so it belongs in the fiction rather than
// being a HUD widget bolted over it.
//
// It is drawn with Parchment's pen - the same hand that annotates the run map in red - but with the
// GROUND INVERTED: the map is dark ink on pale paper, this is pale chalk on dark rock. That is the
// inversion, and it is deliberately a VALUE one rather than a hue one, because the hue budget is
// nearly spent (see the UI skill). It therefore claims no colour at all, which is also the correct
// weight for something that sits over gameplay permanently - the same reason the relic bar is
// near-colourless.
//
// ⚠️ NOT RED, even though red is what the map's annotations use. On paper oxblood reads as pen; over
// gameplay red is already damage - the health bar, damage numbers, hurt flashes. A red arrow on the
// screen edge reads as "you are being hurt", not "the exit is that way". Same lesson as the card's
// last-charge warning, which could not be red because it sat on a red medallion: pick a status
// colour against what it will actually appear on and mean, never for its symbolism alone.
//
// TWO STATES, and the second one is what makes it teach rather than nag:
//   off screen -> an arrow rides the screen edge, pointing at the door.
//   on screen  -> the chalk circles the door ONCE, then gets out of the way for good.
// Once you can see the arch there is nothing left to say, so it says nothing. A permanent overlay
// must not compete with the game behind it.
public class ExitMarker : MonoBehaviour
{
    // Chalk: warm, near-white, and deliberately not fully white so it reads as a soft mineral mark
    // rather than as UI. Measured on screen against the dungeon's dark rock, not computed.
    private static readonly Color Chalk = new Color(0.93f, 0.90f, 0.82f, 1f);

    // How far inside the screen edge the arrow rides, in canvas units. The bottom inset is much
    // larger because the hand drawer lives there, and the top clears the resource panel / relic bar.
    private const float InsetX = 104f;
    private const float InsetTop = 150f;
    private const float InsetBottom = 232f;

    // Hysteresis: the door has to come this much further inside the frame to count as "seen" than it
    // does to count as "lost". Without the gap the arrow flickers on and off while the player walks
    // along the boundary, which is exactly where they spend their time.
    private const float LostMargin = 0.02f;
    private const float SeenMargin = 0.10f;

    private const float ArrowLen = 66f;
    private const float FadeSpeed = 5.5f;

    // A slow nudge ALONG the direction it points. This is the one piece of motion, and it is here
    // instead of a brightness or size increase: the mark has to be findable on a busy screen without
    // being loud, and drift in the direction of travel says "that way" while a pulse only says
    // "look at me". Kept well under the relic bar's rarity pulse - a permanent overlay must not
    // compete with the game behind it.
    private const float NudgeAmp = 3.5f;
    private const float NudgeSpeed = 2.2f;

    private static ExitMarker instance;

    private Canvas canvas;
    private RectTransform canvasRect;
    private CanvasGroup group;
    private RectTransform arrow;          // container: rotated to point, never scaled
    private RectTransform ring;
    private Image ringImg;

    private ExitDoor exit;
    private float findRetry;
    private float shown;                  // 0..1 arrow visibility, eased

    // The reveal is per-room and one-shot: 0 = not yet seen, >0 = playing, <0 = done.
    private float revealT;
    private bool revealed;
    private ExitDoor revealedFor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // ⚠️ Via SceneBootstrap, never a bare RuntimeInitializeOnLoadMethod: that fires ONCE PER
        // SESSION, so a scene-local self-bootstrapped object is destroyed by the first scene load
        // and never comes back. That is how the run map and the scrap counter silently disappeared
        // for the rest of the session after the player's first death.
        SceneBootstrap.Register(Create);
    }

    private static void Create()
    {
        if (instance != null) return;

        // Parent under GameplayHUD so it inherits the HUD auto-hide when a full-screen panel opens -
        // free, and it cannot fall out of step with screens added later.
        GameObject hud = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.WorldSpace) continue;   // enemy health bars
            var t = c.transform.Find("GameplayHUD");
            if (t != null) { hud = t.gameObject; break; }
        }
        if (hud == null) return;   // no gameplay HUD in this scene (main menu, game over) - nothing to do

        var go = new GameObject("ExitMarker", typeof(RectTransform));
        go.transform.SetParent(hud.transform, false);
        instance = go.AddComponent<ExitMarker>();
    }

    void Awake()
    {
        instance = this;
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = canvas.transform as RectTransform;

        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;    // it must never eat a click

        BuildArrow();
        BuildRing();
    }

    // A chalk arrow: one shaft and two barbs, each a tapered hand-drawn stroke, each set down at a
    // slightly different length and angle. Perfectly symmetrical barbs read as a vector icon; the
    // small inconsistencies are the whole reason it reads as drawn by a person.
    private void BuildArrow()
    {
        var go = new GameObject("Arrow", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        arrow = (RectTransform)go.transform;
        arrow.sizeDelta = new Vector2(ArrowLen, ArrowLen);
        arrow.localScale = Vector3.one;

        // Offsets are fractions of ArrowLen, not pixels, so changing the size keeps the barbs on the
        // head instead of drifting off it. The two barbs deliberately differ in length and angle.
        float L = ArrowLen;
        AddStroke(arrow, new Vector2(-0.069f * L, 0f), 0f, L, 0.224f * L);                    // shaft
        AddStroke(arrow, new Vector2(0.259f * L, 0.164f * L), 145f, 0.50f * L, 0.207f * L);   // upper barb
        AddStroke(arrow, new Vector2(0.267f * L, -0.155f * L), -147f, 0.47f * L, 0.207f * L); // lower barb
    }

    private void AddStroke(RectTransform parent, Vector2 pos, float angle, float len, float thick)
    {
        var go = new GameObject("Stroke", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(len, thick);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        rt.localScale = Vector3.one;

        var img = go.AddComponent<Image>();
        img.sprite = Parchment.Stroke();
        img.color = Chalk;
        img.raycastTarget = false;
    }

    private void BuildRing()
    {
        var go = new GameObject("Ring", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        ring = (RectTransform)go.transform;
        ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
        ring.pivot = new Vector2(0.5f, 0.5f);
        ring.localScale = Vector3.one;

        ringImg = go.AddComponent<Image>();
        ringImg.sprite = Parchment.InkRing(false);
        ringImg.color = Chalk;
        ringImg.raycastTarget = false;
        go.SetActive(false);
    }

    void LateUpdate()
    {
        // unscaled throughout: HitStop parks timeScale at 0 for whole frames, and a marker that
        // freezes mid-fade during every hit reads as a stutter.
        float dt = Time.unscaledDeltaTime;

        if (!ResolveExit() || canvasRect == null || Camera.main == null)
        {
            shown = Mathf.MoveTowards(shown, 0f, dt * FadeSpeed);
            Apply();
            return;
        }

        Vector3 world = ExitCentre();
        Vector3 sp = Camera.main.WorldToScreenPoint(world);

        // ⚠️ Viewport point, NOT screenPoint / Screen.width. `Screen` reports the Game View window
        // rather than the render target for at least a frame after a resolution change - measured
        // 2269x334 while the canvas was correctly 1440x1080 - so dividing by it can put the test a
        // whole aspect ratio out. WorldToViewportPoint comes off the camera's own rect and cannot
        // disagree with what was actually drawn.
        Vector3 vp = Camera.main.WorldToViewportPoint(world);
        float margin = revealed ? LostMargin : SeenMargin;
        bool onScreen = vp.x > margin && vp.x < 1f - margin && vp.y > margin && vp.y < 1f - margin;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, sp, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out local);

        if (onScreen)
        {
            shown = Mathf.MoveTowards(shown, 0f, dt * FadeSpeed);
            if (!revealed) { revealed = true; revealT = 0f; ring.gameObject.SetActive(true); }
        }
        else
        {
            shown = Mathf.MoveTowards(shown, 1f, dt * FadeSpeed);
            PlaceArrow(local);
        }

        TickReveal(dt, local, world);
        Apply();
    }

    // Clamp to the inset frame and point along the line from the screen centre to the door.
    private void PlaceArrow(Vector2 local)
    {
        Rect r = canvasRect.rect;
        float hx = r.width * 0.5f - InsetX;
        float hyTop = r.height * 0.5f - InsetTop;
        float hyBot = r.height * 0.5f - InsetBottom;

        Vector2 dir = local.sqrMagnitude < 0.0001f ? Vector2.right : local.normalized;

        // Scale the direction out until it meets whichever inset edge it hits first, so the arrow
        // sits ON the frame rather than inside a rounded box - the edge it rests against is itself
        // information about which way the door lies.
        float tx = Mathf.Abs(dir.x) > 0.0001f ? hx / Mathf.Abs(dir.x) : float.MaxValue;
        float limY = dir.y >= 0f ? hyTop : hyBot;
        float ty = Mathf.Abs(dir.y) > 0.0001f ? limY / Mathf.Abs(dir.y) : float.MaxValue;
        float t = Mathf.Min(tx, ty);

        float nudge = Mathf.Sin(Time.unscaledTime * NudgeSpeed) * NudgeAmp;
        arrow.anchoredPosition = dir * (t + nudge);
        arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    // The one-shot circle: it grows in fast, holds, then fades. Sized to the door itself so it reads
    // as circling THAT thing rather than as a generic blip on top of it.
    private void TickReveal(float dt, Vector2 local, Vector3 world)
    {
        if (!revealed || revealT < 0f) { if (ring.gameObject.activeSelf && revealT < 0f) ring.gameObject.SetActive(false); return; }

        revealT += dt;
        const float draw = 0.30f, hold = 0.55f, fade = 0.65f;
        float total = draw + hold + fade;
        if (revealT >= total) { revealT = -1f; ring.gameObject.SetActive(false); return; }

        float k = Mathf.Clamp01(revealT / draw);
        float ease = 1f - (1f - k) * (1f - k);

        // Size from the door's real on-screen extent: project a point one world unit above it and
        // measure the gap in canvas units, so the circle fits the arch at any zoom or aspect.
        Vector3 upSp = Camera.main.WorldToScreenPoint(world + Vector3.up);
        Vector2 upLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, upSp, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out upLocal);
        float perUnit = Mathf.Max(1f, Vector2.Distance(local, upLocal));

        // Resize, never scale - the door is ~3.2 world units tall, so a ring a shade wider than that
        // encircles the arch without swallowing half the room.
        float size = perUnit * Mathf.Lerp(5.2f, 4.1f, ease);
        ring.sizeDelta = new Vector2(size, size);
        ring.anchoredPosition = local;

        float a = revealT < draw + hold ? ease : 1f - Mathf.Clamp01((revealT - draw - hold) / fade);
        ringImg.color = new Color(Chalk.r, Chalk.g, Chalk.b, a * 0.85f);
    }

    private void Apply()
    {
        float e = shown * shown * (3f - 2f * shown);
        arrow.gameObject.SetActive(e > 0.001f);
        // The group carries the arrow; the ring drives its own alpha so it can play while the arrow
        // is fading out, which is exactly the moment the door comes into view.
        group.alpha = 1f;
        foreach (var img in arrow.GetComponentsInChildren<Image>(true))
            img.color = new Color(Chalk.r, Chalk.g, Chalk.b, e * 0.92f);
    }

    private Vector3 ExitCentre()
    {
        var box = exit.GetComponent<BoxCollider2D>();
        return box != null ? (Vector3)box.bounds.center : exit.transform.position;
    }

    // Re-found whenever the cached door dies with its room. Derived, never pushed at it, so a room
    // spawned by any path is picked up without LevelManager having to remember to tell us.
    private bool ResolveExit()
    {
        if (exit != null) return true;
        if (revealedFor != null || revealed) { revealed = false; revealT = 0f; revealedFor = null; }

        findRetry -= Time.unscaledDeltaTime;
        if (findRetry > 0f) return false;
        findRetry = 0.4f;

        exit = Object.FindFirstObjectByType<ExitDoor>();
        if (exit != null) { revealed = false; revealT = 0f; revealedFor = exit; }
        return exit != null;
    }
}
