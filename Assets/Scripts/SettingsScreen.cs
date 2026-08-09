using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// The settings screen.
//
// THEME: Apparatus (FlatUI.Apparatus) — smoked glass and etched, self-illuminated linework. See the
// theme's own header for why: settings is the one screen that is not inside the fiction, so it is
// the machine's control panel rather than another room in the Oxidation District. Its light is
// EMITTED BY THE CONTENT, its motion is a single travelling scan sweep, and its corner marks are
// calibration crosshairs instead of rivets or stars.
//
// ⚠️ EVERY ROW ON THIS SCREEN CHANGES SOMETHING. Values live in GameSettings, which names the
// consumer for each one. Do not add a control without a consumer — a slider that moves and does
// nothing is worse than an absent feature, because the player then stops trusting the ones that
// work.
//
// House pattern: entirely procedural, self-instantiating under the root Canvas, no prefab and no
// art files. Opened by PauseScreen, and built to be openable from the main menu later via Open().
public class SettingsScreen : MonoBehaviour
{
    private static SettingsScreen instance;

    private static readonly FlatUI.Theme T = FlatUI.Apparatus;

    private const float WIN_W = 1240f;
    private const float WIN_H = 940f;
    private const int CHAMFER = 10;

    private const float CONTENT_TOP = 304f;      // from the window's centre
    private const float ROW_H = 44f;
    private const float SECTION_H = 46f;
    private const float SECTION_GAP = 14f;

    private const float LABEL_X = -350f;         // left-aligned, spans -540..-160
    private const float LABEL_W = 380f;
    private const float CTRL_X = -120f;          // left edge of every control
    private const float TRACK_W = 360f;
    private const float VALUE_X = 420f;          // right-aligned, spans 320..520
    private const float VALUE_W = 200f;

    private enum RowKind { Slider, Toggle, Cycle }

    private class Row
    {
        public RowKind kind;
        public string label, hint;
        public float y;
        public System.Func<bool> isEnabled;      // null = always live

        public TextMeshProUGUI labelText, valueText;
        public RectTransform selectMark;

        // Slider
        public System.Func<float> getF;
        public System.Action<float> setF;
        public RectTransform fill, handle;
        public readonly List<Image> ticks = new List<Image>();

        // Toggle
        public System.Func<bool> getB;
        public System.Action<bool> setB;
        public RectTransform knob;
        public Image switchTrack, switchFill;

        // Cycle
        public System.Func<int> getI;
        public System.Action<int> setI;
        public string[] names;
        public TextMeshProUGUI leftArrow, rightArrow;
    }

    private readonly List<Row> rows = new List<Row>();
    private int selected;

    private RectTransform window, sweep;
    private CanvasGroup group;
    private TMP_FontAsset font;
    private AudioSource audioSource;
    private TextMeshProUGUI hintLabel;

    private bool isOpen;
    private GameObject cachedHud;
    private bool hudWasActive;
    private float cursorY, cursorTargetY;
    private RectTransform cursor;
    private System.Action onClosed;

    // Set while the pointer is dragging a slider track, so a drag that wanders off the track keeps
    // controlling the row it started on.
    private Row dragging;

    // Laid out top-down as rows are added, exactly like the forge's LayoutSections — so inserting a
    // setting never means recomputing anyone's Y by hand.
    private float layoutY;

    // ---- entry points ----------------------------------------------------------------------------

    // `onClosed` runs when the player leaves. PauseScreen uses it to bring itself back.
    public static void Open(System.Action onClosed = null)
    {
        EnsureInstance();
        if (instance == null)
        {
            Debug.LogWarning("SettingsScreen: no Canvas, cannot open.");
            onClosed?.Invoke();
            return;
        }
        instance.onClosed = onClosed;
        instance.Show();
    }

    public static bool IsOpen => instance != null && instance.isOpen;

    private static void EnsureInstance()
    {
        if (instance != null) return;

        Canvas canvas = FindRootCanvas();
        if (canvas == null) return;

        GameObject go = new GameObject("SettingsScreen", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<SettingsScreen>();
        instance.Build();
        go.SetActive(false);
    }

    private static Canvas FindRootCanvas()
    {
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas fallback = null;
        foreach (Canvas c in all)
        {
            if (c == null) continue;
            if (fallback == null) fallback = c;
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        }
        return fallback;
    }

    // ---- construction ----------------------------------------------------------------------------

    private void Build()
    {
        font = FlatUI.UIFont();
        Stretch(GetComponent<RectTransform>());
        group = gameObject.AddComponent<CanvasGroup>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        Image backdrop = AddImage(transform, "Backdrop", null, T.Backdrop, true);
        Stretch(backdrop.rectTransform);
        Button back = backdrop.gameObject.AddComponent<Button>();
        back.transition = Selectable.Transition.None;
        back.onClick.AddListener(Hide);

        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero,
                          new Vector2(WIN_W, WIN_H));
        Image winBg = window.gameObject.AddComponent<Image>();
        winBg.sprite = FlatUI.Panel(CHAMFER);
        winBg.type = Image.Type.Sliced;          // ⚠️ Simple stretches the 26px plate into a blob
        winBg.color = T.Surface;
        winBg.raycastTarget = true;

        Image edge = AddImage(window, "Edge", FlatUI.Outline(CHAMFER, 2), T.Border, false);
        edge.type = Image.Type.Sliced;
        Stretch(edge.rectTransform);
        FlatUI.ApplySliceThickness(edge, 2f);

        BuildCalibrationMarks();
        BuildSweep();
        BuildTitle();
        BuildRows();
        BuildFooter();

        SetSelected(0, false);
    }

    // Four crosshairs just inside the corners. Deliberately dim: they are the panel saying what
    // KIND of object it is, not decoration competing with the controls.
    private void BuildCalibrationMarks()
    {
        Color c = T.EdgeLight;
        c.a = 0.32f;
        Vector2[] at =
        {
            new Vector2(-WIN_W * 0.5f + 30f,  WIN_H * 0.5f - 30f),
            new Vector2( WIN_W * 0.5f - 30f,  WIN_H * 0.5f - 30f),
            new Vector2(-WIN_W * 0.5f + 30f, -WIN_H * 0.5f + 30f),
            new Vector2( WIN_W * 0.5f - 30f, -WIN_H * 0.5f + 30f),
        };
        for (int i = 0; i < at.Length; i++)
        {
            Image m = AddImage(window, "Mark" + i, FlatUI.CalibrationMark(), c, false);
            m.rectTransform.sizeDelta = new Vector2(18f, 18f);
            m.rectTransform.anchoredPosition = at[i];
        }
    }

    private void BuildSweep()
    {
        Color c = T.Accent;
        c.a = 0.10f;
        Image s = AddImage(window, "Sweep", FlatUI.SweepLine(), c, false);
        s.rectTransform.sizeDelta = new Vector2(WIN_W - 40f, 90f);
        sweep = s.rectTransform;
    }

    private void BuildTitle()
    {
        TextMeshProUGUI title = AddText(window, "Title", "SETTINGS", 34f, T.TextBright,
                                        TextAlignmentOptions.Center);
        title.rectTransform.sizeDelta = new Vector2(WIN_W - 120f, 44f);
        title.rectTransform.anchoredPosition = new Vector2(0f, WIN_H * 0.5f - 56f);
        title.characterSpacing = 14f;

        TextMeshProUGUI sub = AddText(window, "Sub", "CALIBRATION", 14f, T.TextMuted,
                                      TextAlignmentOptions.Center);
        sub.rectTransform.sizeDelta = new Vector2(WIN_W - 120f, 20f);
        sub.rectTransform.anchoredPosition = new Vector2(0f, WIN_H * 0.5f - 96f);
        sub.characterSpacing = 9f;

        Image rule = AddImage(window, "TitleRule", FlatUI.FadedRule(), T.Border, false);
        rule.rectTransform.sizeDelta = new Vector2(WIN_W - 200f, 1f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, WIN_H * 0.5f - 124f);
    }

    private void BuildRows()
    {
        // The selection cursor is built first so every row's widgets draw over it.
        cursor = AddPoint(window, "Cursor", new Vector2(0.5f, 0.5f), Vector2.zero,
                          new Vector2(WIN_W - 96f, 38f));
        Image cu = cursor.gameObject.AddComponent<Image>();
        cu.sprite = FlatUI.Panel(5);
        cu.type = Image.Type.Sliced;
        // ⚠️ 0.03, not the 0.065 that looks right on paper. The project renders in LINEAR colour
        // space, so a small alpha of a bright, saturated colour composites far brighter than the
        // number suggests: 0.065 of this cyan over the panel surface landed near 0.36 sRGB on
        // screen and filled the selected row with a solid teal slab. Pick these by screenshot.
        cu.color = new Color(T.Accent.r, T.Accent.g, T.Accent.b, 0.03f);
        cu.raycastTarget = false;
        FlatUI.ApplySliceThickness(cu, 5f);

        layoutY = CONTENT_TOP;

        AddSection("AUDIO");
        AddSlider("MASTER VOLUME", "Everything at once. Music and effects both sit under this.",
                  () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        AddSlider("MUSIC", "Background music only.",
                  () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
        AddSlider("SOUND EFFECTS", "Everything that isn't music.",
                  () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);

        AddSection("GAME FEEL");
        AddSlider("SCREEN SHAKE", "How hard the camera kicks on impacts. Turn it down if it's too much.",
                  () => GameSettings.ScreenShake, v => GameSettings.ScreenShake = v);
        AddSlider("FREEZE FRAMES", "The tiny pause on every hit. At zero, hits play through smoothly.",
                  () => GameSettings.HitStopStrength, v => GameSettings.HitStopStrength = v);
        AddToggle("DAMAGE NUMBERS", "Show the damage dealt floating off each enemy you hit.",
                  () => GameSettings.DamageNumbers, v => GameSettings.DamageNumbers = v);
        AddToggle("ENEMY HEALTH NUMBERS", "Show exact HP on enemy health bars. The bars stay either way.",
                  () => GameSettings.EnemyHealthNumbers, v => GameSettings.EnemyHealthNumbers = v);
        AddToggle("CARD AIM PREVIEW", "Show where a selected card will land before you play it.",
                  () => GameSettings.CardAimPreview, v => GameSettings.CardAimPreview = v);

        AddSection("VIDEO");
        AddCycle("DISPLAY MODE", "Fullscreen, borderless window, or a plain window.",
                 GameSettings.DisplayModeNames,
                 () => GameSettings.DisplayMode, v => GameSettings.DisplayMode = v, null);
        AddToggle("VSYNC", "Matches the frame rate to your monitor. Prevents tearing.",
                  () => GameSettings.VSync, v => GameSettings.VSync = v);
        AddCycle("FRAME CAP", "Maximum frames per second. Only applies with VSync off.",
                 GameSettings.FrameCapNames,
                 () => GameSettings.FrameCap, v => GameSettings.FrameCap = v,
                 () => !GameSettings.VSync);
    }

    private void AddSection(string name)
    {
        if (rows.Count > 0) layoutY -= SECTION_GAP;

        TextMeshProUGUI h = AddText(window, "Section_" + name, name, 15f, T.Accent,
                                    TextAlignmentOptions.Left);
        h.rectTransform.sizeDelta = new Vector2(LABEL_W, 22f);
        h.rectTransform.anchoredPosition = new Vector2(LABEL_X, layoutY - 10f);
        h.characterSpacing = 11f;

        Image rule = AddImage(window, "SectionRule_" + name, FlatUI.FadedRule(), T.BorderSoft, false);
        rule.rectTransform.sizeDelta = new Vector2(WIN_W - 200f, 1f);
        rule.rectTransform.anchoredPosition = new Vector2(20f, layoutY - 30f);

        layoutY -= SECTION_H;
    }

    // Common furniture for any row: the label, the value readout, a selection tick, and a
    // full-width hit plate that makes hovering select it.
    private Row BeginRow(RowKind kind, string label, string hint, System.Func<bool> isEnabled)
    {
        Row r = new Row { kind = kind, label = label, hint = hint, y = layoutY, isEnabled = isEnabled };
        int index = rows.Count;

        r.labelText = AddText(window, "L_" + label, label, 17f, T.TextBody, TextAlignmentOptions.Left);
        r.labelText.rectTransform.sizeDelta = new Vector2(LABEL_W, 26f);
        r.labelText.rectTransform.anchoredPosition = new Vector2(LABEL_X, r.y);
        r.labelText.characterSpacing = 3f;

        r.valueText = AddText(window, "V_" + label, "", 17f, T.Accent, TextAlignmentOptions.Right);
        r.valueText.rectTransform.sizeDelta = new Vector2(VALUE_W, 26f);
        r.valueText.rectTransform.anchoredPosition = new Vector2(VALUE_X, r.y);

        // A small etched tick left of the selected row, in place of the pause screen's solid bar —
        // this panel's language is measuring marks, not bracketing.
        r.selectMark = AddPoint(window, "Tick_" + label, new Vector2(0.5f, 0.5f),
                                new Vector2(LABEL_X - LABEL_W * 0.5f - 18f, r.y), new Vector2(9f, 2f));
        Image tick = r.selectMark.gameObject.AddComponent<Image>();
        tick.sprite = FlatUI.Pixel();
        tick.color = T.Accent;
        tick.raycastTarget = false;
        tick.enabled = false;

        Image hit = AddImage(window, "Hit_" + label, null, new Color(0f, 0f, 0f, 0f), true);
        hit.rectTransform.sizeDelta = new Vector2(WIN_W - 96f, ROW_H - 4f);
        hit.rectTransform.anchoredPosition = new Vector2(0f, r.y);
        PauseEntryHover hov = hit.gameObject.AddComponent<PauseEntryHover>();
        hov.onEnter = () => SetSelected(index, true);
        // Clicking a toggle row anywhere flips it; sliders and cycles have their own hit targets and
        // must NOT respond to a stray click on the label, which would be an accidental change.
        if (kind == RowKind.Toggle) hov.onClick = () => Adjust(index, +1);

        rows.Add(r);
        layoutY -= ROW_H;
        return r;
    }

    private void AddSlider(string label, string hint, System.Func<float> get, System.Action<float> set)
    {
        Row r = BeginRow(RowKind.Slider, label, hint, null);
        r.getF = get;
        r.setF = set;

        RectTransform track = AddPoint(window, "Track_" + label, new Vector2(0.5f, 0.5f),
                                       new Vector2(CTRL_X + TRACK_W * 0.5f, r.y),
                                       new Vector2(TRACK_W, 26f));
        Image trackHit = track.gameObject.AddComponent<Image>();
        trackHit.color = new Color(0f, 0f, 0f, 0f);
        trackHit.raycastTarget = true;

        // Graduated scale: eleven ticks, taller at the ends and the midpoint, which is what makes
        // the control read as an instrument rather than as a progress bar.
        for (int i = 0; i <= 10; i++)
        {
            bool major = i == 0 || i == 5 || i == 10;
            Image t = AddImage(track, "Tick" + i, FlatUI.Pixel(),
                               new Color(T.EdgeLight.r, T.EdgeLight.g, T.EdgeLight.b, major ? 0.55f : 0.28f),
                               false);
            t.rectTransform.sizeDelta = new Vector2(1f, major ? 11f : 6f);
            t.rectTransform.anchoredPosition = new Vector2(-TRACK_W * 0.5f + TRACK_W * i / 10f, -7f);
            r.ticks.Add(t);
        }

        Image rail = AddImage(track, "Rail", FlatUI.Pixel(),
                              new Color(1f, 1f, 1f, 0.08f), false);
        rail.rectTransform.sizeDelta = new Vector2(TRACK_W, 3f);
        rail.rectTransform.anchoredPosition = new Vector2(0f, 5f);

        Image fill = AddImage(rail.rectTransform, "Fill", FlatUI.Pixel(), T.Accent, false);
        fill.rectTransform.anchorMin = new Vector2(0f, 0f);
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.rectTransform.anchoredPosition = Vector2.zero;
        r.fill = fill.rectTransform;

        // The handle is a blade, not a knob — it points AT a graduation.
        RectTransform handle = AddPoint(track, "Handle", new Vector2(0.5f, 0.5f), Vector2.zero,
                                        new Vector2(3f, 20f));
        Image hi = handle.gameObject.AddComponent<Image>();
        hi.sprite = FlatUI.Pixel();
        hi.color = T.TextBright;
        hi.raycastTarget = false;
        r.handle = handle;

        int index = rows.Count - 1;
        SettingsSliderDrag drag = track.gameObject.AddComponent<SettingsSliderDrag>();
        drag.track = track;
        drag.onSet = v => { SetSelected(index, false); SetSliderValue(index, v, true); };
    }

    private void AddToggle(string label, string hint, System.Func<bool> get, System.Action<bool> set)
    {
        Row r = BeginRow(RowKind.Toggle, label, hint, null);
        r.getB = get;
        r.setB = set;

        RectTransform sw = AddPoint(window, "Switch_" + label, new Vector2(0.5f, 0.5f),
                                    new Vector2(CTRL_X + 34f, r.y), new Vector2(68f, 24f));
        Image bg = sw.gameObject.AddComponent<Image>();
        bg.sprite = FlatUI.Panel(5);
        bg.type = Image.Type.Sliced;
        bg.color = T.SurfaceRaised;
        bg.raycastTarget = true;
        FlatUI.ApplySliceThickness(bg, 5f);
        r.switchTrack = bg;

        Image swFill = AddImage(sw, "SwitchFill", FlatUI.Panel(5), T.Accent, false);
        swFill.type = Image.Type.Sliced;
        Stretch(swFill.rectTransform);
        FlatUI.ApplySliceThickness(swFill, 5f);
        r.switchFill = swFill;

        RectTransform knob = AddPoint(sw, "Knob", new Vector2(0.5f, 0.5f), Vector2.zero,
                                      new Vector2(26f, 16f));
        Image ki = knob.gameObject.AddComponent<Image>();
        ki.sprite = FlatUI.Panel(5);
        ki.type = Image.Type.Sliced;
        ki.color = T.TextBright;
        ki.raycastTarget = false;
        FlatUI.ApplySliceThickness(ki, 4f);
        r.knob = knob;

        int index = rows.Count - 1;
        Button b = sw.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { SetSelected(index, false); Adjust(index, +1); });
    }

    private void AddCycle(string label, string hint, string[] names, System.Func<int> get,
                          System.Action<int> set, System.Func<bool> isEnabled)
    {
        Row r = BeginRow(RowKind.Cycle, label, hint, isEnabled);
        r.getI = get;
        r.setI = set;
        r.names = names;

        int index = rows.Count - 1;
        r.leftArrow = AddArrow(label + "_L", "<", CTRL_X + 12f, r.y, index, -1);
        r.rightArrow = AddArrow(label + "_R", ">", CTRL_X + TRACK_W - 12f, r.y, index, +1);

        // A cycle's chosen option reads in the CONTROL area, between its arrows, not over in the
        // value column — the arrows have to bracket the thing they change or they look unrelated.
        r.valueText.rectTransform.sizeDelta = new Vector2(TRACK_W - 60f, 26f);
        r.valueText.rectTransform.anchoredPosition = new Vector2(CTRL_X + TRACK_W * 0.5f, r.y);
        r.valueText.alignment = TextAlignmentOptions.Center;
    }

    private TextMeshProUGUI AddArrow(string name, string glyph, float x, float y, int index, int dir)
    {
        TextMeshProUGUI t = AddText(window, "Arrow_" + name, glyph, 20f, T.TextMuted,
                                    TextAlignmentOptions.Center);
        t.rectTransform.sizeDelta = new Vector2(26f, 26f);
        t.rectTransform.anchoredPosition = new Vector2(x, y);
        t.raycastTarget = true;

        Button b = t.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { SetSelected(index, false); Adjust(index, dir); });
        return t;
    }

    private void BuildFooter()
    {
        Image rule = AddImage(window, "FooterRule", FlatUI.FadedRule(), T.BorderSoft, false);
        rule.rectTransform.sizeDelta = new Vector2(WIN_W - 200f, 1f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, -WIN_H * 0.5f + 122f);

        // The hint line. One shared strip that describes the SELECTED row, rather than a caption
        // under every setting — eleven permanent explanation lines would bury the controls, and the
        // player only needs the one they are looking at.
        hintLabel = AddText(window, "Hint", "", 15f, T.TextMuted, TextAlignmentOptions.Center);
        hintLabel.rectTransform.sizeDelta = new Vector2(WIN_W - 160f, 44f);
        hintLabel.rectTransform.anchoredPosition = new Vector2(0f, -WIN_H * 0.5f + 92f);

        TextMeshProUGUI reset = AddText(window, "Reset", "RESET TO DEFAULTS", 14f, T.TextMuted,
                                        TextAlignmentOptions.Center);
        reset.rectTransform.sizeDelta = new Vector2(260f, 28f);
        reset.rectTransform.anchoredPosition = new Vector2(-WIN_W * 0.5f + 190f, -WIN_H * 0.5f + 48f);
        reset.characterSpacing = 5f;
        reset.raycastTarget = true;
        Button rb = reset.gameObject.AddComponent<Button>();
        rb.transition = Selectable.Transition.None;
        rb.onClick.AddListener(DoReset);

        TextMeshProUGUI foot = AddText(window, "Footer",
            "W / S  SELECT          A / D  ADJUST          ESC  BACK",
            13f, T.TextDisabled, TextAlignmentOptions.Center);
        foot.rectTransform.sizeDelta = new Vector2(WIN_W - 160f, 22f);
        foot.rectTransform.anchoredPosition = new Vector2(0f, -WIN_H * 0.5f + 48f);
        foot.characterSpacing = 6f;
    }

    // ---- open / close ----------------------------------------------------------------------------

    private void Show()
    {
        if (isOpen) return;
        isOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (GameManager.instance != null) GameManager.instance.RequestPause();

        // Hides the HUD itself rather than relying on whoever opened it. PauseScreen has already
        // hidden it, but this screen is also opened straight from the main menu and will be opened
        // from elsewhere later; a screen that only looks right via one entry point is a trap.
        if (cachedHud == null) cachedHud = GameObject.Find("GameplayHUD");
        hudWasActive = cachedHud != null && cachedHud.activeSelf;
        if (cachedHud != null) cachedHud.SetActive(false);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        RefreshAll();
        SetSelected(0, false);

        StopAllCoroutines();
        StartCoroutine(OpenAnim());
    }

    private void Hide()
    {
        if (!isOpen) return;
        isOpen = false;
        dragging = null;

        if (GameManager.instance != null) GameManager.instance.ReleasePause();

        if (cachedHud != null) cachedHud.SetActive(hudWasActive);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);

        StopAllCoroutines();
        gameObject.SetActive(false);

        System.Action cb = onClosed;
        onClosed = null;
        cb?.Invoke();
    }

    private IEnumerator OpenAnim()
    {
        const float dur = 0.16f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;   // the screen pauses the game; scaled time is frozen
            float k = Mathf.Clamp01(t / dur);
            group.alpha = k;
            window.localScale = Vector3.one * Mathf.Lerp(0.985f, 1f, k);
            yield return null;
        }
        group.alpha = 1f;
        window.localScale = Vector3.one;
    }

    // ---- input -----------------------------------------------------------------------------------

    private void Update()
    {
        if (!isOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); return; }

        int dir = 0;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dir = 1;
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dir = -1;
        if (dir != 0) SetSelected(NextEnabled(selected, dir), true);

        // Sliders repeat while held — nudging a volume 5% at a time with 20 discrete presses is
        // the kind of thing that makes a menu feel cheap.
        int adjust = 0;
        bool held = rows.Count > 0 && rows[selected].kind == RowKind.Slider;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) adjust = 1;
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) adjust = -1;
        else if (held && (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))) adjust = 1;
        else if (held && (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))) adjust = -1;
        if (adjust != 0) Adjust(selected, adjust);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
            Adjust(selected, +1);

        TickCursor();
        TickSweep();
    }

    // Skips rows that are currently greyed out (Frame Cap with VSync on), so the keyboard never
    // parks on a control that ignores input.
    private int NextEnabled(int from, int dir)
    {
        for (int step = 1; step <= rows.Count; step++)
        {
            int i = (from + dir * step + rows.Count * step) % rows.Count;
            if (RowEnabled(rows[i])) return i;
        }
        return from;
    }

    private bool RowEnabled(Row r) { return r.isEnabled == null || r.isEnabled(); }

    private void SetSelected(int index, bool playSound)
    {
        if (rows.Count == 0) return;
        index = Mathf.Clamp(index, 0, rows.Count - 1);
        bool changed = index != selected;
        selected = index;
        cursorTargetY = rows[index].y;
        if (!playSound) cursorY = cursorTargetY;
        else if (changed && audioSource != null)
            SfxManager.PlayOn(audioSource, ProcSfx.PauseTick, 0.5f);

        hintLabel.text = rows[index].hint;
        RefreshAll();
    }

    private void Adjust(int index, int dir)
    {
        if (index < 0 || index >= rows.Count) return;
        Row r = rows[index];
        if (!RowEnabled(r)) return;

        switch (r.kind)
        {
            case RowKind.Slider:
                SetSliderValue(index, Mathf.Clamp01(r.getF() + dir * 0.05f), false);
                break;

            case RowKind.Toggle:
                r.setB(!r.getB());
                if (audioSource != null) SfxManager.PlayOn(audioSource, ProcSfx.PauseTick, 0.8f);
                RefreshAll();
                break;

            case RowKind.Cycle:
                int n = r.names.Length;
                r.setI(((r.getI() + dir) % n + n) % n);
                if (audioSource != null) SfxManager.PlayOn(audioSource, ProcSfx.PauseTick, 0.8f);
                RefreshAll();
                break;
        }
    }

    // `fromDrag` suppresses the tick while the pointer is sweeping the track, which would otherwise
    // fire on every frame of the drag and machine-gun.
    private void SetSliderValue(int index, float value, bool fromDrag)
    {
        Row r = rows[index];
        float before = r.getF();
        r.setF(Mathf.Clamp01(value));

        if (!fromDrag && !Mathf.Approximately(before, r.getF()) && audioSource != null)
            SfxManager.PlayOn(audioSource, ProcSfx.PauseTick, 0.45f);

        RefreshAll();
    }

    private void DoReset()
    {
        GameSettings.ResetToDefaults();
        if (audioSource != null) SfxManager.PlayOn(audioSource, ProcSfx.PauseHalt, 0.35f);
        RefreshAll();
    }

    // ---- visual state ----------------------------------------------------------------------------

    // Redraws every row from GameSettings rather than tracking widget state locally. Settings are
    // cheap to read and this makes it impossible for the panel to disagree with the actual values —
    // which matters because rows affect each other (VSync greys out Frame Cap) and because RESET
    // changes all eleven at once.
    private void RefreshAll()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            Row r = rows[i];
            bool live = RowEnabled(r);
            bool sel = i == selected;

            r.labelText.color = !live ? T.TextDisabled : (sel ? T.TextBright : T.TextBody);
            r.selectMark.GetComponent<Image>().enabled = sel && live;

            Color accent = live ? T.Accent : T.TextDisabled;

            switch (r.kind)
            {
                case RowKind.Slider:
                {
                    float v = r.getF();
                    r.fill.sizeDelta = new Vector2(TRACK_W * v, 0f);
                    r.handle.anchoredPosition = new Vector2(-TRACK_W * 0.5f + TRACK_W * v, 5f);
                    r.valueText.text = Mathf.RoundToInt(v * 100f) + "%";
                    r.valueText.color = accent;
                    r.fill.GetComponent<Image>().color = accent;

                    // Graduations the value has passed brighten, so the scale reads as filled.
                    for (int k = 0; k < r.ticks.Count; k++)
                    {
                        bool major = k == 0 || k == 5 || k == 10;
                        bool passed = v >= k / 10f - 0.001f;
                        Color c = passed ? T.Accent : T.EdgeLight;
                        c.a = major ? (passed ? 0.75f : 0.55f) : (passed ? 0.45f : 0.28f);
                        r.ticks[k].color = c;
                    }
                    break;
                }

                case RowKind.Toggle:
                {
                    bool on = r.getB();
                    r.knob.anchoredPosition = new Vector2(on ? 18f : -18f, 0f);
                    r.knob.GetComponent<Image>().color = live ? (on ? T.Surface : T.TextMuted)
                                                              : T.TextDisabled;
                    // ⚠️ 0.55, not full. At alpha 1 in linear space these are solid slabs of
                    // saturated cyan, and five of them stacked pulled the eye straight off the
                    // sliders — the loudest thing on a settings panel should not be whichever
                    // control happens to be a switch. The bright knob still carries the state.
                    Color f = accent;
                    f.a = on ? 0.55f : 0f;
                    r.switchFill.color = f;
                    r.valueText.text = on ? "ON" : "OFF";
                    r.valueText.color = on ? accent : T.TextMuted;
                    break;
                }

                case RowKind.Cycle:
                {
                    r.valueText.text = r.names[Mathf.Clamp(r.getI(), 0, r.names.Length - 1)];
                    r.valueText.color = accent;
                    Color arrow = live ? (sel ? T.TextBright : T.TextMuted) : T.TextDisabled;
                    r.leftArrow.color = arrow;
                    r.rightArrow.color = arrow;
                    break;
                }
            }
        }
    }

    private void TickCursor()
    {
        float k = 1f - Mathf.Exp(-24f * Time.unscaledDeltaTime);
        cursorY = Mathf.Lerp(cursorY, cursorTargetY, k);
        cursor.anchoredPosition = new Vector2(0f, cursorY);
    }

    // The scan sweep: one line of light crossing the plate top to bottom, then a long dark pause
    // before the next pass. The pause is what keeps it a measuring instrument rather than a loading
    // bar — a continuously cycling line reads as "busy", and this panel is idle by definition.
    private void TickSweep()
    {
        const float period = 7f;
        const float travel = 2.2f;      // seconds of the period the line is actually visible

        float t = Time.unscaledTime % period;
        Image img = sweep.GetComponent<Image>();

        if (t > travel)
        {
            Color hidden = img.color;
            hidden.a = 0f;
            img.color = hidden;
            return;
        }

        float k = t / travel;
        float half = WIN_H * 0.5f;
        sweep.anchoredPosition = new Vector2(0f, Mathf.Lerp(half, -half, k));

        // Fade in and out at the ends so the line doesn't pop into existence at the panel edge.
        Color c = T.Accent;
        c.a = 0.10f * Mathf.Min(Mathf.Clamp01(k / 0.15f), Mathf.Clamp01((1f - k) / 0.15f));
        img.color = c;
    }

    // ---- small builders --------------------------------------------------------------------------

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private RectTransform AddPoint(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        Image img = go.AddComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, string text, float size, Color color,
                                    TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }
}

// Click-and-drag on a slider track. Handles the press as well as the drag so a single click
// anywhere on the track jumps the value there — having to grab a 3px handle exactly would be
// miserable.
public class SettingsSliderDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public RectTransform track;
    public System.Action<float> onSet;

    public void OnPointerDown(PointerEventData e) { Apply(e); }
    public void OnDrag(PointerEventData e) { Apply(e); }

    private void Apply(PointerEventData e)
    {
        if (track == null || onSet == null) return;
        Vector2 local;
        // Camera is null for a Screen Space Overlay canvas — passing e.pressEventCamera here would
        // be null anyway, but being explicit documents the assumption.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, null, out local))
            return;

        Rect r = track.rect;
        onSet(Mathf.Clamp01((local.x - r.xMin) / r.width));
    }
}
