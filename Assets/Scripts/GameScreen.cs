using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shared contract every full-screen panel in Deckshift obeys.
///
/// ── What this is, and what it deliberately is NOT ─────────────────────────────────────────────
///
/// It is **the display contract**: taking over the screen (pause, game state, HUD, hand drawer),
/// handing it back exactly as it was, fitting an oversized window to a narrow aspect, finding the
/// right Canvas, and the one-frame Escape memory.
///
/// It is **not a lifecycle that owns activation**, and that is on purpose. Screens genuinely differ
/// there — `PauseScreen`'s root must stay ACTIVE so its `Update` can catch the Escape that *opens*
/// it, while every other screen deactivates its own GameObject. A base class that insisted on
/// `SetActive` would have to be fought by the one screen that matters most. Screens keep their own
/// Show/Hide and call <see cref="AcquireDisplay"/> / <see cref="ReleaseDisplay"/> from inside it.
///
/// ── Why it exists ────────────────────────────────────────────────────────────────────────────
///
/// The twelve lines in `AcquireDisplay` were **copy-pasted, identical, into every screen** — same
/// fields, same order, same guards, in `ScrapForgeScreen`, `BlompoScreen`, `SettingsScreen`,
/// `QuestBoardScreen`, `RunMapScreen`, `ShopScreenUI`, `RelicManagePanel`, `RelicSwapScreen`,
/// `CardChestScreen` and `PauseScreen`. Three of the details are load-bearing and none of them are
/// obvious, so every future screen is one forgotten line away from a bug that only shows up when
/// screens are opened on top of each other:
///
/// ⚠️ **`hudWasActive` is RECORDED, not assumed.** A screen opened on top of another (a chest's
/// relic swap, Blompo from the forge) must restore the HUD to what it *was*, not switch it on.
///
/// ⚠️ **The drawer lock is GATED on `hudWasActive`.** Unlocking unconditionally would let an inner
/// screen unlock a drawer the outer screen is still relying on being locked.
///
/// ⚠️ **`prevState` is SAVED, not hardcoded to `Playing`.** Closing a screen must return the game to
/// whatever state it was actually in.
///
/// ── The migration policy ─────────────────────────────────────────────────────────────────────
///
/// ⚠️ **Do NOT retrofit every screen at once.** New screens use this immediately; existing ones
/// migrate when they are already being touched for another reason. Each migration is behaviour-
/// preserving by construction — the base runs the identical sequence the screen already had — but
/// it is still eleven chances to typo, taken one at a time and verified by screenshot.
/// `QuestBoardScreen` is the worked example.
/// </summary>
public abstract class GameScreen : MonoBehaviour
{
    /// <summary>True between <see cref="AcquireDisplay"/> and <see cref="ReleaseDisplay"/>.</summary>
    protected bool isOpen;

    // ---- display ownership state (was duplicated in every screen) ----
    private GameState prevState;
    private GameObject cachedHud;
    private bool hudWasActive;
    private bool holdsDisplay;

    /// <summary>
    /// Whether this screen pauses the game and hides the HUD. False for menu-scene screens like the
    /// character select, where there is no run to pause and no HUD to hide.
    /// </summary>
    protected virtual bool OwnsGameplay { get { return true; } }

    // =====================================================================================
    // Display ownership
    // =====================================================================================

    /// <summary>
    /// Take over the screen: pause, mark the game paused, hide the HUD, lock the hand drawer.
    /// Call from the screen's own Show, after it has activated and raised itself.
    /// </summary>
    protected void AcquireDisplay()
    {
        if (holdsDisplay) return;      // idempotent: a double Show must not stack two pauses
        holdsDisplay = true;

        if (PlaysDefaultOpenCloseSound) PlayUI(ProcSfx.UIOpen);

        if (!OwnsGameplay) return;

        GameManager gm = GameManager.instance;
        prevState = gm != null ? gm.currentState : GameState.Playing;
        if (gm != null)
        {
            gm.RequestPause();
            gm.SetGameState(GameState.Paused);
        }

        if (cachedHud == null) cachedHud = GameObject.Find("GameplayHUD");
        hudWasActive = cachedHud != null && cachedHud.activeSelf;   // visible ⇒ opened from gameplay
        if (cachedHud != null) cachedHud.SetActive(false);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);
    }

    /// <summary>Hand the screen back exactly as it was. Call from the screen's own Hide.</summary>
    protected void ReleaseDisplay()
    {
        if (!holdsDisplay) return;     // idempotent: a double Hide must not release a pause twice
        holdsDisplay = false;

        if (PlaysDefaultOpenCloseSound) PlayUI(ProcSfx.UIClose);

        if (!OwnsGameplay) return;

        GameManager gm = GameManager.instance;
        if (gm != null)
        {
            gm.ReleasePause();
            gm.SetGameState(prevState);
        }

        if (cachedHud != null) cachedHud.SetActive(hudWasActive);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);
    }

    /// <summary>
    /// Safety net: a screen destroyed while open would otherwise leave the game paused forever with
    /// the HUD hidden and no way to get it back.
    /// </summary>
    protected virtual void OnDestroy()
    {
        ReleaseDisplay();
    }

    // =====================================================================================
    // The one-frame Escape memory
    // =====================================================================================

    private static bool uiHeldPauseLastFrame;
    private static int memoryFrame = -1;

    /// <summary>
    /// Did ANY UI hold the pause on the previous frame?
    ///
    /// ⚠️ A screen that opens on a keypress must check this as well as `GameManager.IsUIPaused`.
    /// Script execution order is undefined, so on the frame another screen closes on Escape it may
    /// release its pause *before* this screen's `Update` runs — leaving Escape still down, nothing
    /// paused, and this screen opening instantly behind the one the player just dismissed.
    /// `ShopManager` used to carry a consumed-this-frame stamp for exactly that, but a stamp only
    /// ever protects the one screen that remembers to set it. A memory covers every screen and asks
    /// nothing of any of them.
    /// </summary>
    public static bool UIHeldPauseLastFrame { get { return uiHeldPauseLastFrame; } }

    /// <summary>
    /// Advance the memory. Frame-guarded, so it is correct no matter how many screens call it — and
    /// every screen with an `Update` should, so the memory keeps ticking when the screen that owns
    /// it happens not to be alive.
    /// </summary>
    protected static void TickUIPauseMemory()
    {
        if (memoryFrame == Time.frameCount) return;
        memoryFrame = Time.frameCount;
        GameManager gm = GameManager.instance;
        uiHeldPauseLastFrame = gm != null && gm.IsUIPaused;
    }

    // =====================================================================================
    // Aspect fitting
    //
    // ⚠️ THE TWO MODES ARE NOT INTERCHANGEABLE, and picking the wrong one is silently wrong.
    // =====================================================================================

    /// <summary>
    /// RESIZE the window to fit the canvas.
    ///
    /// Only safe when the window's content is anchored to its corners with insets, so it genuinely
    /// reflows into a smaller box — `RunMapScreen`'s chart does this.
    /// </summary>
    protected void FitWindowToCanvas(RectTransform window, float designW, float designH, float margin = 40f)
    {
        if (window == null) return;
        RectTransform parent = transform as RectTransform;
        if (parent == null) return;

        Rect r = parent.rect;
        if (r.width <= 1f || r.height <= 1f) return;   // not laid out yet

        window.sizeDelta = new Vector2(Mathf.Min(designW, r.width - margin),
                                       Mathf.Min(designH, r.height - margin));
    }

    /// <summary>
    /// Uniform SCALE factor, never above 1.
    ///
    /// Required when content sits at fixed offsets from the window centre — `BlompoScreen`,
    /// `SettingsScreen` and `ShopScreenUI` are all like this, and *resizing* them would overlap
    /// their own columns. Shrinking is only safe as a uniform scale.
    /// </summary>
    protected float FitScaleFor(float designW, float designH, float fill = 0.97f, float min = 0.4f)
    {
        RectTransform parent = transform as RectTransform;
        if (parent == null) return 1f;
        Rect r = parent.rect;
        if (r.width <= 1f || r.height <= 1f) return 1f;
        return Mathf.Clamp(Mathf.Min(r.width * fill / designW, r.height * fill / designH), min, 1f);
    }

    // =====================================================================================
    // Canvas
    // =====================================================================================

    /// <summary>
    /// The Canvas a screen belongs on: the root Screen-Space-Overlay one.
    ///
    /// ⚠️ NOT `FindFirstObjectByType&lt;Canvas&gt;()`. A gameplay scene carries a WORLD-SPACE Canvas
    /// per enemy health bar — 18 of them in SampleScene — and that call returns one of those. The
    /// character select shipped that way and built itself inside a health bar at 0.01 scale, so it
    /// rendered invisibly while its `Open` still reported success.
    /// </summary>
    public static Canvas FindRootCanvas()
    {
        Canvas[] all = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas fallback = null;
        foreach (Canvas c in all)
        {
            if (c == null) continue;
            if (fallback == null) fallback = c;
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        }
        return fallback;
    }

    // =====================================================================================
    // Helpers
    // =====================================================================================

    /// <summary>
    /// Fade a CanvasGroup on UNSCALED time.
    ///
    /// ⚠️ Every screen here pauses the game, so `Time.deltaTime` is 0 while one is up — a scaled
    /// fade never advances and the screen hangs at its starting alpha forever.
    /// </summary>
    protected static IEnumerator FadeGroup(CanvasGroup g, float from, float to, float duration)
    {
        if (g == null) yield break;
        if (duration <= 0f) { g.alpha = to; yield break; }

        float t = 0f;
        g.alpha = from;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        g.alpha = to;
    }

    // =====================================================================================
    // Sound — the UI vocabulary (see the UI family in ProcSfx)
    //
    // Six sounds sharing one voice, meaning carried by pitch motion. Firing open/close from here is
    // the point: it is how a new screen gets the vocabulary without inventing one, which is exactly
    // how the project ended up with sound on the pause screen and silence everywhere else.
    // =====================================================================================

    private AudioSource uiAudio;

    /// <summary>
    /// Whether this screen uses the generic open/close pair.
    ///
    /// ⚠️ **A screen with a BESPOKE open sound must return false, or it plays two.** The quest
    /// board's paper rustle and the pause screen's halt/release are that screen's signature and
    /// beat the generic pair — the generic one exists for screens that would otherwise be silent.
    /// </summary>
    protected virtual bool PlaysDefaultOpenCloseSound { get { return true; } }

    /// <summary>Moving a selection. Fires constantly, so it is deliberately the quietest.</summary>
    protected void PlayMove() { PlayUI(ProcSfx.UIMove); }

    /// <summary>Committing to something.</summary>
    protected void PlayConfirm() { PlayUI(ProcSfx.UIConfirm); }

    /// <summary>The player choosing to back out. NOT the same as <see cref="PlayRefuse"/>.</summary>
    protected void PlayCancel() { PlayUI(ProcSfx.UICancel); }

    /// <summary>The game saying no — can't afford it, slots full, already taken.</summary>
    protected void PlayRefuse() { PlayUI(ProcSfx.UIRefuse); }

    private void PlayUI(AudioClip clip)
    {
        if (clip == null) return;

        if (uiAudio == null)
        {
            // 2D and built in code, like the boss's source: a UI sound must be equally audible
            // wherever the player happens to be standing in the room behind the panel.
            uiAudio = gameObject.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false;
            uiAudio.spatialBlend = 0f;
        }

        SfxManager.PlayOn(uiAudio, clip);
    }

    /// <summary>
    /// A label in the DISPLAY face at a role's size. Prose (real sentences) should instead use
    /// <see cref="UIType.ApplyProse"/> — see `UIType` for the split.
    /// </summary>
    protected static TextMeshProUGUI AddLabel(Transform parent, string name, string text,
                                              TextRole role, Color color,
                                              TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        UIType.Apply(t, role);
        return t;
    }
}
