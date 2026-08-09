using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

// The pause screen — Escape.
//
// THEME: Halt (FlatUI.Halt), and it is the only screen here with no window plate.
//
// Every other screen in the game is a place you have gone to inside the world — a workbench, a
// grove, a market stall — so each of them is a panel sitting on top of the game. Pause is not
// somewhere you go. It is the world itself being stopped, and it should therefore take the WHOLE
// frame rather than politely occupying a rectangle of it. That single structural choice is what
// separates this from every other screen before a colour is picked.
//
// What the material says (see FlatUI.Halt for the palette rationale): frost creeping in from the
// four edges rather than light from above or below; hairline fractures, because it stopped hard;
// and motes hanging DEAD STILL, each still dragging the streak it had when the clock stopped,
// shivering sub-pixel against it. The suspended particle field is the signature — it communicates
// "time is held" before the player has read a word.
//
// It is also the run's STATUS READOUT, which is the real reason it earns its space. Four buttons on
// a dark rectangle is a pause menu; this is the one screen that can afford to show everything at
// once, and several of these numbers (exhausted count, the next Stagger price) are visible nowhere
// else in the game.
//
// House pattern: entirely procedural, self-bootstrapping, no prefab and no art files — same shape
// as RunMapScreen and ScrapForgeScreen.
//
// ⚠️ THE ROOT STAYS ACTIVE; only its CONTENT child is toggled. Update() has to run to catch the
// Escape that OPENS the screen, and a deactivated GameObject does not get Update.
public class PauseScreen : MonoBehaviour
{
    private static PauseScreen instance;

    private static readonly FlatUI.Theme T = FlatUI.Halt;

    // Layout, in the canvas's 1920x1080 reference space. Everything is anchored to the centre.
    private const float TITLE_Y = 300f;
    private const float SUB_Y = 240f;
    private const float TITLE_RULE_Y = 210f;
    private const float HEADER_Y = 170f;
    private const float HEADER_RULE_Y = 150f;
    private const float LIST_TOP = 108f;

    private const float MENU_X = -250f;          // centre of the menu column
    private const float MENU_W = 340f;
    private const float MENU_STEP = 56f;
    private const float BRACKET_X = -438f;

    private const float STAT_LABEL_X = 175f;     // spans  80..270
    private const float STAT_LABEL_W = 190f;
    private const float STAT_VALUE_X = 375f;     // spans 275..475
    private const float STAT_VALUE_W = 200f;
    private const float STAT_STEP = 38f;
    private const float BAR_X = 262f;            // bar spans 262..372, value takes the rest
    private const float BAR_W = 110f;
    private const float BAR_VALUE_W = 108f;

    private const float FOOTER_Y = -300f;

    // A menu row. `confirmLabel` non-null makes the entry two-step: the first activation ARMS it and
    // the second commits. Throwing a run away on a single keypress next to RESUME is a trap.
    private class Entry
    {
        public string label;
        public string confirmLabel;
        public System.Action action;
        public TextMeshProUGUI text;
        public bool armed;
        public float armedUntil;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private int selected;

    private RectTransform content, bracket, highlight;
    private CanvasGroup group;
    private TMP_FontAsset font;
    private AudioSource audioSource;

    private TextMeshProUGUI subtitle;
    private TextMeshProUGUI vFloor, vGold, vScrap, vRelics, vDeck, vExhaust, vStagger, vRecall;
    private TextMeshProUGUI vHealth, vShift;
    private Image barHealth, barShift;

    private bool isOpen;
    private bool wasUIPaused;
    private float bracketY, bracketTargetY;

    // A sub-screen (settings / how-to-play) currently borrowing the display. While one is up this
    // screen hides its own content but KEEPS its pause held, and watches for the panel closing
    // itself — which is how both of the old panels dismiss, so we don't have to rewire them.
    private GameObject subPanel;

    // A procedural sub-screen (SettingsScreen) has the display. Unlike `subPanel` it tells us when
    // it closes, so there is nothing to poll — but Update must still stand down while it is up.
    private bool subScreenOpen;

    private GameObject cachedHud;
    private bool hudWasActive;

    // ---- suspended motes -------------------------------------------------------------------------
    // Not UIEmberField: that class is a drift simulator, and the entire point of these is that they
    // do NOT drift. A mote here is a frozen streak with the dot still at its leading end.

    private struct Mote
    {
        public RectTransform rt;
        public Image streak, dot;
        public Vector2 basePos;
        public float fx, fy, px, py, amp;      // shiver
        public float breatheSpeed, breathePhase;
        public float streakAlpha, dotAlpha;
    }

    private Mote[] motes;
    private const int MOTE_COUNT = 44;

    // A pause screen's atmosphere must never compete with the text sitting on top of it. These
    // alphas look far too low written down and are correct on screen — the same lesson the ember
    // field's 0.085 -> 0.05 correction taught.
    //
    // The DOT is the loud half and the streak is a hint. Reversing that ratio is what turned the
    // first pass into rain.
    private const float MOTE_STREAK_ALPHA = 0.11f;
    private const float MOTE_DOT_ALPHA = 0.52f;

    // ---- bootstrap -------------------------------------------------------------------------------

    // ⚠️ Registered through SceneBootstrap, not called directly: RuntimeInitializeOnLoadMethod fires
    // once per PLAY SESSION, so a screen created here alone would vanish the first time the player
    // died and restarted (SampleScene -> GameOverScene -> SampleScene) and Escape would do nothing
    // for the rest of the session. That exact bug cost RunMapManager and ScrapHUD.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneBootstrap.Register(Create);
    }

    private static void Create()
    {
        if (instance != null) return;
        if (FindFirstObjectByType<PauseScreen>() != null) return;

        // Gameplay scenes only. The bootstrap re-runs on every scene load, and Escape must not
        // raise a run-status screen over the main menu or the game-over screen.
        if (FindFirstObjectByType<GameManager>() == null) return;

        Canvas canvas = FindRootCanvas();
        if (canvas == null) return;

        GameObject go = new GameObject("PauseScreen", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<PauseScreen>();
        instance.Build();
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

    public static bool IsOpen => instance != null && instance.isOpen;

    // ---- construction ----------------------------------------------------------------------------

    private void Build()
    {
        font = FlatUI.UIFont();

        RectTransform root = GetComponent<RectTransform>();
        Stretch(root);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        content = AddPoint(transform, "Content", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(content);
        group = content.gameObject.AddComponent<CanvasGroup>();

        // Backdrop. Raycast ON so nothing behind the screen can be clicked through it, and it is
        // deliberately NOT a dismiss button: the pause screen has an explicit RESUME and a click
        // anywhere would make the two destructive entries one stray click away from being lost.
        Image backdrop = AddImage(content, "Backdrop", null, T.Backdrop, true);
        Stretch(backdrop.rectTransform);

        BuildFrostEdges();
        BuildFractures();
        BuildMotes();

        BuildTitle();
        BuildMenu();
        BuildStatus();
        BuildDivider();
        BuildFooter();

        content.gameObject.SetActive(false);
    }

    // Frost creeping in from all four borders, plus a soft bloom at each corner so it reads as
    // spreading from the corners rather than as four straight bands.
    private void BuildFrostEdges()
    {
        const float THICK = 96f;
        Color rim = T.EdgeLight;
        rim.a = 0.085f;

        // Top / bottom use VerticalFade (opaque at the top). The bottom copy is MIRRORED with
        // localScale.y = -1 on a centred pivot rather than rotated 180 — a rotation about a
        // non-centred pivot would swing the strip off the screen.
        Image top = AddImage(content, "FrostTop", FlatUI.VerticalFade(), rim, false);
        top.rectTransform.anchorMin = new Vector2(0f, 1f);
        top.rectTransform.anchorMax = new Vector2(1f, 1f);
        top.rectTransform.sizeDelta = new Vector2(0f, THICK);
        top.rectTransform.anchoredPosition = new Vector2(0f, -THICK * 0.5f);

        Image bottom = AddImage(content, "FrostBottom", FlatUI.VerticalFade(), rim, false);
        bottom.rectTransform.anchorMin = new Vector2(0f, 0f);
        bottom.rectTransform.anchorMax = new Vector2(1f, 0f);
        bottom.rectTransform.sizeDelta = new Vector2(0f, THICK);
        bottom.rectTransform.anchoredPosition = new Vector2(0f, THICK * 0.5f);
        bottom.rectTransform.localScale = new Vector3(1f, -1f, 1f);

        Image left = AddImage(content, "FrostLeft", FlatUI.HorizontalFade(), rim, false);
        left.rectTransform.anchorMin = new Vector2(0f, 0f);
        left.rectTransform.anchorMax = new Vector2(0f, 1f);
        left.rectTransform.sizeDelta = new Vector2(THICK, 0f);
        left.rectTransform.anchoredPosition = new Vector2(THICK * 0.5f, 0f);

        Image right = AddImage(content, "FrostRight", FlatUI.HorizontalFade(), rim, false);
        right.rectTransform.anchorMin = new Vector2(1f, 0f);
        right.rectTransform.anchorMax = new Vector2(1f, 1f);
        right.rectTransform.sizeDelta = new Vector2(THICK, 0f);
        right.rectTransform.anchoredPosition = new Vector2(-THICK * 0.5f, 0f);
        right.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        Color corner = T.EdgeLight;
        corner.a = 0.05f;
        Vector2[] anchors = { Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 1f), Vector2.one };
        for (int i = 0; i < anchors.Length; i++)
        {
            Image g = AddImage(content, "FrostCorner" + i, FlatUI.SoftGlow(), corner, false);
            g.rectTransform.anchorMin = g.rectTransform.anchorMax = anchors[i];
            g.rectTransform.sizeDelta = new Vector2(560f, 560f);
            g.rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    // Hairline crazing in the frost, kept OUT IN THE MARGINS. Fixed strokes, so they are the same
    // every time — a crack that moved between openings would read as an animation, and these are
    // meant to be damage.
    //
    // ⚠️ TWO CONSTRAINTS LEARNED THE HARD WAY. They must stay near the edges, in the band the frost
    // already occupies, so they read as the frost cracking rather than as scratches floating over
    // the picture; and they must be much fainter than the motes, or the two effects collapse into
    // one look — the first pass had six long strokes at the same value as the mote streaks, and the
    // whole screen read as a dirty lens instead of as either idea.
    private void BuildFractures()
    {
        Color c = T.EdgeLight;
        c.a = 0.032f;

        float[,] f = {
            //  x,     y,    length, angle
            { -790f,  400f,  300f,  -28f },
            {  810f,  330f,  240f,   33f },
            { -840f, -330f,  260f,   16f },
            {  830f, -390f,  320f,  -21f },
        };

        for (int i = 0; i < f.GetLength(0); i++)
        {
            Image line = AddImage(content, "Fracture" + i, FlatUI.FadedRule(), c, false);
            line.rectTransform.sizeDelta = new Vector2(f[i, 2], 1f);
            line.rectTransform.anchoredPosition = new Vector2(f[i, 0], f[i, 1]);
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, f[i, 3]);
        }
    }

    private void BuildMotes()
    {
        GameObject field = new GameObject("Motes", typeof(RectTransform));
        RectTransform fieldRt = field.GetComponent<RectTransform>();
        fieldRt.SetParent(content, false);
        Stretch(fieldRt);

        // The root is stretched under the Canvas so its rect is the screen. It may still be zero on
        // the very first frame after Awake, hence the reference-resolution fallback.
        Rect r = ((RectTransform)transform).rect;
        float halfW = r.width > 1f ? r.width * 0.5f : 960f;
        float halfH = r.height > 1f ? r.height * 0.5f : 540f;

        Random.State prev = Random.state;
        Random.InitState(20260809);   // fixed layout: the field is scenery, not a lottery

        motes = new Mote[MOTE_COUNT];
        for (int i = 0; i < MOTE_COUNT; i++)
        {
            GameObject go = new GameObject("Mote", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(fieldRt, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            // ⚠️ THE STREAK IS SHORT AND THE DOT LEADS. The first pass ran 16-52px streaks with a
            // 3-6px dot, and the field read as RAIN — or worse, as scratches on the lens — because
            // a long thin line is a line first and a particle second. A mote has to read as a
            // POINT that happens to be smeared; the moment the smear is the bigger half, the
            // suspended-particle idea is gone and it just looks like the camera is dirty.
            float len = Random.Range(5f, 17f);
            float thick = 1f;

            // FadedRule fades at BOTH ends, which is exactly the shape of a motion smear, and the
            // dot sits at the leading end so the mote reads as travelling — while going nowhere.
            Image streak = AddImage(rt, "Streak", FlatUI.FadedRule(), Color.white, false);
            streak.rectTransform.sizeDelta = new Vector2(len, thick);
            streak.rectTransform.anchoredPosition = new Vector2(-len * 0.5f, 0f);

            float dotSize = Random.Range(4.5f, 8.5f);
            Image dot = AddImage(rt, "Dot", FlatUI.EmberDot(), Color.white, false);
            dot.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);

            motes[i].rt = rt;
            motes[i].streak = streak;
            motes[i].dot = dot;
            motes[i].basePos = new Vector2(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH));
            rt.anchoredPosition = motes[i].basePos;

            // A high-frequency tremble of about a pixel. Big enough to notice at the edge of vision,
            // far too small to read as travel — which is the whole trick.
            motes[i].fx = Random.Range(9f, 17f);
            motes[i].fy = Random.Range(9f, 17f);
            motes[i].px = Random.Range(0f, 6.28f);
            motes[i].py = Random.Range(0f, 6.28f);
            motes[i].amp = Random.Range(0.7f, 1.5f);

            motes[i].breatheSpeed = Random.Range(0.35f, 0.8f);
            motes[i].breathePhase = Random.Range(0f, 6.28f);

            float scale = Random.Range(0.55f, 1f);
            motes[i].streakAlpha = MOTE_STREAK_ALPHA * scale;
            motes[i].dotAlpha = MOTE_DOT_ALPHA * scale;
        }

        Random.state = prev;
    }

    private void BuildTitle()
    {
        Image glow = AddImage(content, "TitleGlow", FlatUI.SoftGlow(),
                              new Color(T.Accent.r, T.Accent.g, T.Accent.b, 0.055f), false);
        glow.rectTransform.sizeDelta = new Vector2(980f, 260f);
        glow.rectTransform.anchoredPosition = new Vector2(0f, TITLE_Y - 6f);

        TextMeshProUGUI title = AddText(content, "Title", "PAUSED", 78f, T.TextBright,
                                        TextAlignmentOptions.Center);
        title.rectTransform.sizeDelta = new Vector2(1200f, 96f);
        title.rectTransform.anchoredPosition = new Vector2(0f, TITLE_Y);
        title.characterSpacing = 22f;
        title.fontStyle = FontStyles.Bold;

        subtitle = AddText(content, "Subtitle", "", 17f, T.TextMuted, TextAlignmentOptions.Center);
        subtitle.rectTransform.sizeDelta = new Vector2(1200f, 26f);
        subtitle.rectTransform.anchoredPosition = new Vector2(0f, SUB_Y);
        subtitle.characterSpacing = 7f;

        Image rule = AddImage(content, "TitleRule", FlatUI.FadedRule(), T.Border, false);
        rule.rectTransform.sizeDelta = new Vector2(640f, 1f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, TITLE_RULE_Y);
    }

    private void BuildDivider()
    {
        // FadedRule is horizontal, so the column divider is one rotated 90 degrees. Safe here where
        // the frost edges were not: this rect has a fixed size and a centred pivot, so rotating it
        // spins it in place instead of swinging it out of the screen.
        Image d = AddImage(content, "Divider", FlatUI.FadedRule(), T.BorderSoft, false);
        d.rectTransform.sizeDelta = new Vector2(460f, 1f);
        d.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        d.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
    }

    private void BuildMenu()
    {
        AddColumnHeader("OPTIONS", MENU_X, MENU_W, TextAlignmentOptions.Left);

        // The selected row's plate and bracket are built BEFORE the labels so they sit behind them.
        highlight = AddPoint(content, "Highlight", new Vector2(0.5f, 0.5f), Vector2.zero,
                             new Vector2(MENU_W + 60f, 44f));
        Image hi = highlight.gameObject.AddComponent<Image>();
        hi.sprite = FlatUI.Panel(5);
        hi.type = Image.Type.Sliced;
        hi.color = new Color(T.Accent.r, T.Accent.g, T.Accent.b, 0.07f);
        hi.raycastTarget = false;
        FlatUI.ApplySliceThickness(hi, 5f);

        bracket = AddPoint(content, "Bracket", new Vector2(0.5f, 0.5f),
                           new Vector2(BRACKET_X, 0f), new Vector2(4f, 28f));
        Image br = bracket.gameObject.AddComponent<Image>();
        br.sprite = FlatUI.Pixel();
        br.color = T.Accent;
        br.raycastTarget = false;

        AddEntry("RESUME", null, Close);
        AddEntry("SETTINGS", null, OpenSettings);
        AddEntry("HOW TO PLAY", null, () => OpenSubPanel("TutorialPanel"));
        AddEntry("ABANDON RUN", "ABANDON RUN?  CONFIRM", AbandonRun);
        AddEntry("QUIT TO DESKTOP", "QUIT TO DESKTOP?  CONFIRM", QuitGame);

        SetSelected(0, false);
    }

    private void AddEntry(string label, string confirmLabel, System.Action action)
    {
        int index = entries.Count;
        float y = LIST_TOP - index * MENU_STEP;

        Entry e = new Entry { label = label, confirmLabel = confirmLabel, action = action };

        e.text = AddText(content, "Entry_" + label, label, 30f, T.TextMuted, TextAlignmentOptions.Left);
        e.text.rectTransform.sizeDelta = new Vector2(MENU_W, 44f);
        e.text.rectTransform.anchoredPosition = new Vector2(MENU_X, y);
        e.text.characterSpacing = 4f;

        // A transparent hit plate rather than a Button on the label: the label's own rect is only as
        // tall as its text, and a row you have to hit exactly feels broken next to keyboard nav.
        Image hit = AddImage(content, "Hit_" + label, null, new Color(0f, 0f, 0f, 0f), true);
        hit.rectTransform.sizeDelta = new Vector2(MENU_W + 60f, 46f);
        hit.rectTransform.anchoredPosition = new Vector2(MENU_X, y);

        PauseEntryHover hov = hit.gameObject.AddComponent<PauseEntryHover>();
        hov.onEnter = () => SetSelected(index, true);
        hov.onClick = () => Activate(index);

        entries.Add(e);
    }

    private void BuildStatus()
    {
        AddColumnHeader("THIS RUN", STAT_LABEL_X, STAT_LABEL_W, TextAlignmentOptions.Left);

        int row = 0;
        vFloor = AddStatRow("FLOOR", row++);
        vHealth = AddBarRow("HEALTH", row++, new Color(0.847f, 0.310f, 0.310f, 1f), out barHealth);
        vShift = AddBarRow("SHIFT", row++, T.Accent, out barShift);
        vGold = AddStatRow("GOLD", row++);
        vScrap = AddStatRow("SCRAP", row++);
        vRelics = AddStatRow("RELICS", row++);
        vDeck = AddStatRow("DECK", row++);
        vExhaust = AddStatRow("EXHAUSTED", row++);
        vRecall = AddStatRow("RECALL COST", row++);
        vStagger = AddStatRow("NEXT STAGGER", row++);
    }

    private void AddColumnHeader(string label, float x, float w, TextAlignmentOptions align)
    {
        TextMeshProUGUI h = AddText(content, "Header_" + label, label, 14f, T.TextMuted, align);
        h.rectTransform.sizeDelta = new Vector2(w, 20f);
        h.rectTransform.anchoredPosition = new Vector2(x, HEADER_Y);
        h.characterSpacing = 10f;

        Image rule = AddImage(content, "HeaderRule_" + label, FlatUI.FadedRule(), T.BorderSoft, false);
        rule.rectTransform.sizeDelta = new Vector2(w + 200f, 1f);
        rule.rectTransform.anchoredPosition = new Vector2(x + 100f, HEADER_RULE_Y);
    }

    private TextMeshProUGUI AddStatRow(string label, int row)
    {
        float y = LIST_TOP - row * STAT_STEP;

        TextMeshProUGUI l = AddText(content, "L_" + label, label, 15f, T.TextMuted,
                                    TextAlignmentOptions.Left);
        l.rectTransform.sizeDelta = new Vector2(STAT_LABEL_W, 24f);
        l.rectTransform.anchoredPosition = new Vector2(STAT_LABEL_X, y);
        l.characterSpacing = 4f;

        TextMeshProUGUI v = AddText(content, "V_" + label, "-", 17f, T.TextBody,
                                    TextAlignmentOptions.Right);
        v.rectTransform.sizeDelta = new Vector2(STAT_VALUE_W, 24f);
        v.rectTransform.anchoredPosition = new Vector2(STAT_VALUE_X, y);
        return v;
    }

    // Health and Shift are BOUNDED, so they get a fill; everything else is an unbounded count and
    // stays a number. Same rule the resource panel settled on.
    private TextMeshProUGUI AddBarRow(string label, int row, Color fill, out Image bar)
    {
        float y = LIST_TOP - row * STAT_STEP;

        TextMeshProUGUI l = AddText(content, "L_" + label, label, 15f, T.TextMuted,
                                    TextAlignmentOptions.Left);
        l.rectTransform.sizeDelta = new Vector2(STAT_LABEL_W, 24f);
        l.rectTransform.anchoredPosition = new Vector2(STAT_LABEL_X, y);
        l.characterSpacing = 4f;

        Image track = AddImage(content, "Track_" + label, FlatUI.Pixel(),
                               new Color(1f, 1f, 1f, 0.07f), false);
        track.rectTransform.sizeDelta = new Vector2(BAR_W, 7f);
        track.rectTransform.anchoredPosition = new Vector2(BAR_X + BAR_W * 0.5f, y);

        bar = AddImage(track.rectTransform, "Fill_" + label, FlatUI.Pixel(), fill, false);
        bar.rectTransform.anchorMin = new Vector2(0f, 0f);
        bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.anchoredPosition = Vector2.zero;
        bar.rectTransform.sizeDelta = new Vector2(BAR_W, 0f);

        // ⚠️ Wide enough for "100 / 100" at 17pt. The first pass gave this 70px and TMP wrapped the
        // health readout onto two lines, which pushed it out of its row.
        TextMeshProUGUI v = AddText(content, "V_" + label, "-", 17f, T.TextBody,
                                    TextAlignmentOptions.Right);
        v.rectTransform.sizeDelta = new Vector2(BAR_VALUE_W, 24f);
        v.rectTransform.anchoredPosition =
            new Vector2(STAT_VALUE_X + STAT_VALUE_W * 0.5f - BAR_VALUE_W * 0.5f, y);
        return v;
    }

    private void BuildFooter()
    {
        TextMeshProUGUI f = AddText(content, "Footer",
            "W / S  NAVIGATE          ENTER  SELECT          ESC  RESUME",
            13f, T.TextDisabled, TextAlignmentOptions.Center);
        f.rectTransform.sizeDelta = new Vector2(1400f, 22f);
        f.rectTransform.anchoredPosition = new Vector2(0f, FOOTER_Y);
        f.characterSpacing = 6f;
    }

    // ---- open / close ----------------------------------------------------------------------------

    private static readonly string[] Subtitles =
    {
        "TAKE YOUR TIME. IT ISN'T GOING ANYWHERE.",
        "EVERYTHING IS HOLDING VERY STILL.",
        "THE DISTRICT WILL WAIT.",
        "NOBODY MOVES.",
        "BREATHE. THE RUST CAN WAIT.",
    };
    private static int lastSubtitle = -1;

    private void Open()
    {
        if (isOpen) return;
        isOpen = true;

        content.gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (GameManager.instance != null)
        {
            GameManager.instance.RequestPause();
            GameManager.instance.SetGameState(GameState.Paused);
        }

        if (cachedHud == null) cachedHud = GameObject.Find("GameplayHUD");
        hudWasActive = cachedHud != null && cachedHud.activeSelf;
        if (cachedHud != null) cachedHud.SetActive(false);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        // Never the same line twice running — with a pool this small, plain randomness repeats
        // constantly, and a repeat is what makes a line feel canned.
        int pick = Random.Range(0, Subtitles.Length);
        if (Subtitles.Length > 1 && pick == lastSubtitle) pick = (pick + 1) % Subtitles.Length;
        lastSubtitle = pick;
        subtitle.text = Subtitles[pick];

        Refresh();
        SetSelected(0, false);

        if (audioSource != null) SfxManager.PlayOn(audioSource, ProcSfx.PauseHalt, 0.85f);

        StopAllCoroutines();
        StartCoroutine(OpenAnim());
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        CloseSubPanel();

        if (GameManager.instance != null)
        {
            GameManager.instance.ReleasePause();
            GameManager.instance.SetGameState(GameState.Playing);
        }

        if (cachedHud != null) cachedHud.SetActive(hudWasActive);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);

        if (audioSource != null) SfxManager.PlayOn(audioSource, ProcSfx.PauseRelease, 0.7f);

        StopAllCoroutines();
        content.gameObject.SetActive(false);
    }

    private IEnumerator OpenAnim()
    {
        const float dur = 0.20f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;   // the screen pauses the game; scaled time is frozen
            float k = Mathf.Clamp01(t / dur);
            group.alpha = k;
            // The content settles INWARD as the frost closes in, rather than popping outward like
            // the other screens' panels. Small, but it belongs to the same idea as everything else
            // on this screen.
            content.localScale = Vector3.one * Mathf.Lerp(1.015f, 1f, k * k);
            yield return null;
        }
        group.alpha = 1f;
        content.localScale = Vector3.one;
    }

    // ---- input -----------------------------------------------------------------------------------

    private void Update()
    {
        if (!isOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) && CanOpen()) Open();
            return;
        }

        // SettingsScreen owns the display and will call us back; it also handles its own Escape,
        // so we must not act on that key while it is up.
        if (subScreenOpen) return;

        // The legacy How To Play panel owns the display. Watch for it dismissing itself — it closes
        // via its own button, so polling activeSelf works without rewiring it.
        if (subPanel != null)
        {
            if (!subPanel.activeSelf) { subPanel = null; SetContentVisible(true); }
            else if (Input.GetKeyDown(KeyCode.Escape)) CloseSubPanel();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }

        HandleNavigation();
        if (!isOpen) return;   // RESUME closes us mid-frame; don't tick a screen that is gone

        TickArmTimeout();
        TickMotes();
        TickSelectionVisual();
    }

    // Sampled every frame, whatever state this screen is in — hence LateUpdate rather than the tail
    // of Update, which several branches return before reaching.
    private void LateUpdate()
    {
        wasUIPaused = GameManager.instance != null && GameManager.instance.IsUIPaused;
    }

    private bool CanOpen()
    {
        GameManager gm = GameManager.instance;
        if (gm == null) return false;

        // Something else already owns the screen (shop, map, forge, Blompo, chest, quest board...).
        // One check instead of a list of IsOpen flags that would fall behind — see IsUIPaused.
        if (gm.IsUIPaused) return false;

        // ⚠️ AND it must not have owned the screen LAST frame either. Script execution order is
        // undefined, so on the frame the shop closes on Escape it may release its pause before this
        // Update runs — leaving Escape still down, no UI paused, and the pause screen opening
        // instantly behind the screen the player just dismissed. ShopManager carries a
        // consumed-this-frame stamp for exactly that reason, but a stamp only protects the ONE
        // screen that remembers to set it; every other Escape-handling screen had the same hole.
        // A one-frame memory covers all of them and needs nothing from any of them.
        if (wasUIPaused) return false;

        // No pausing your way out of a death. The game-over flow owns the moment.
        if (gm.player != null)
        {
            PlayerHealth ph = gm.player.GetComponent<PlayerHealth>();
            if (ph != null && ph.IsDead) return false;
        }
        return true;
    }

    private void HandleNavigation()
    {
        int dir = 0;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dir = 1;
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dir = -1;

        if (dir != 0)
        {
            int next = (selected + dir + entries.Count) % entries.Count;
            SetSelected(next, true);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
            Activate(selected);
    }

    private void SetSelected(int index, bool playSound)
    {
        if (entries.Count == 0) return;
        index = Mathf.Clamp(index, 0, entries.Count - 1);

        bool changed = index != selected;

        // Moving off an armed entry disarms it. Arming has to be a deliberate, held state — an
        // "are you sure" that survives you looking elsewhere is not a confirmation.
        if (changed) Disarm(entries[selected]);

        selected = index;
        bracketTargetY = LIST_TOP - index * MENU_STEP;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e.armed) continue;                        // armed entries keep the accent colour
            e.text.color = i == selected ? T.TextBright : T.TextMuted;
        }

        if (!playSound) bracketY = bracketTargetY;
        else if (changed && audioSource != null)
            SfxManager.PlayOn(audioSource, ProcSfx.PauseTick, 0.5f);
    }

    private void Activate(int index)
    {
        if (index < 0 || index >= entries.Count) return;
        SetSelected(index, false);

        Entry e = entries[index];

        if (e.confirmLabel != null && !e.armed)
        {
            e.armed = true;
            e.armedUntil = Time.unscaledTime + 4f;
            e.text.text = e.confirmLabel;
            e.text.color = T.Accent;
            if (audioSource != null) SfxManager.PlayOn(audioSource, ProcSfx.PauseTick, 0.9f);
            return;
        }

        Disarm(e);
        e.action?.Invoke();
    }

    private void Disarm(Entry e)
    {
        if (e == null || !e.armed) return;
        e.armed = false;
        e.text.text = e.label;
        e.text.color = entries.IndexOf(e) == selected ? T.TextBright : T.TextMuted;
    }

    private void TickArmTimeout()
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].armed && Time.unscaledTime > entries[i].armedUntil) Disarm(entries[i]);
    }

    private void TickSelectionVisual()
    {
        // Framerate-independent ease, on unscaled time because the game is frozen.
        float k = 1f - Mathf.Exp(-24f * Time.unscaledDeltaTime);
        bracketY = Mathf.Lerp(bracketY, bracketTargetY, k);

        bracket.anchoredPosition = new Vector2(BRACKET_X, bracketY);
        highlight.anchoredPosition = new Vector2(MENU_X, bracketY);
    }

    private void TickMotes()
    {
        if (motes == null) return;
        float t = Time.unscaledTime;

        for (int i = 0; i < motes.Length; i++)
        {
            Mote m = motes[i];
            if (m.rt == null) continue;

            // The tremble. Position is always base + offset, never accumulated, so the field can
            // never drift away from where it was placed no matter how long the game sits paused.
            Vector2 shiver = new Vector2(Mathf.Sin(t * m.fx + m.px), Mathf.Sin(t * m.fy + m.py)) * m.amp;
            m.rt.anchoredPosition = m.basePos + shiver;

            float breathe = 0.72f + 0.28f * Mathf.Sin(t * m.breatheSpeed + m.breathePhase);
            Color c = T.EdgeLight;

            c.a = m.streakAlpha * breathe;
            m.streak.color = c;
            c.a = m.dotAlpha * breathe;
            m.dot.color = c;
        }
    }

    // ---- content ---------------------------------------------------------------------------------

    private void Refresh()
    {
        PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
        PlayerHealth ph = p != null ? p.GetComponent<PlayerHealth>() : null;
        DeckManager dm = DeckManager.instance;
        RelicManager rm = RelicManager.instance;
        RunMapManager map = RunMapManager.instance;

        if (map != null && map.HasMap && map.CurrentNode != null)
            vFloor.text = map.CurrentNode.floor + " / " + (map.Map.floors - 1);
        else
            vFloor.text = "-";

        if (ph != null)
        {
            float frac = ph.MaxHealth > 0f ? Mathf.Clamp01(ph.CurrentHealth / ph.MaxHealth) : 0f;
            barHealth.rectTransform.sizeDelta = new Vector2(BAR_W * frac, 0f);
            vHealth.text = Mathf.CeilToInt(ph.CurrentHealth) + " / " + Mathf.CeilToInt(ph.MaxHealth);
        }

        if (p != null)
        {
            float frac = p.maxShift > 0 ? Mathf.Clamp01((float)p.GetCurrentShift() / p.maxShift) : 0f;
            barShift.rectTransform.sizeDelta = new Vector2(BAR_W * frac, 0f);
            vShift.text = p.GetCurrentShift() + " / " + p.maxShift;

            vGold.text = p.currentGold.ToString();
            vScrap.text = p.currentScrap.ToString();

            // Mirrors the Stagger card's own rule: the price turns red once it is more than you
            // have left, because that is the run's actual death condition and this is the only
            // place outside the card itself it can be read.
            float cost = p.NextStaggerCost;
            bool lethal = ph != null && cost >= ph.CurrentHealth;
            vStagger.text = Mathf.CeilToInt(cost) + " HP";
            vStagger.color = lethal ? new Color(0.902f, 0.290f, 0.290f, 1f) : T.TextBody;
        }

        vRelics.text = rm != null
            ? rm.OwnedRelics.Count + " / " + RelicManager.MaxSlots
            : "-";

        if (dm != null)
        {
            int deck = dm.GetDrawPile().Count + dm.GetCurrentHand().Count + dm.GetDiscardPile().Count;
            int exhausted = dm.GetExhaustPile().Count;
            vDeck.text = deck.ToString();
            vExhaust.text = exhausted.ToString();
            vExhaust.color = exhausted > 0 ? FlatUI.Ember : T.TextBody;
            vRecall.text = dm.currentRecallCost + " SHIFT";
        }
    }

    // ---- sub-panels ------------------------------------------------------------------------------

    // SettingsScreen is a proper procedural screen with its own callback, so it needs none of the
    // activeSelf polling the legacy panel below does.
    private void OpenSettings()
    {
        subScreenOpen = true;
        SetContentVisible(false);
        SettingsScreen.Open(() =>
        {
            subScreenOpen = false;
            SetContentVisible(true);
        });
    }

    // How To Play still uses its pre-existing panel. It is next on the list to be rebuilt; until
    // then the pause screen hands the display over and takes it back, rather than this screen being
    // blocked on it.
    private void OpenSubPanel(string objectName)
    {
        Canvas canvas = FindRootCanvas();
        Transform found = canvas != null ? canvas.transform.Find(objectName) : null;
        if (found == null)
        {
            Debug.LogWarning("PauseScreen: no '" + objectName + "' under the Canvas.");
            return;
        }

        subPanel = found.gameObject;
        SetContentVisible(false);
        subPanel.SetActive(true);
        subPanel.transform.SetAsLastSibling();
    }

    private void CloseSubPanel()
    {
        if (subPanel == null) return;
        subPanel.SetActive(false);
        subPanel = null;
        SetContentVisible(true);
    }

    // Hides the pause screen's own furniture WITHOUT releasing the pause or deactivating the root —
    // Update has to keep running to notice the sub-panel closing.
    private void SetContentVisible(bool visible)
    {
        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }

    // ---- actions ---------------------------------------------------------------------------------

    private void AbandonRun()
    {
        if (GameManager.instance != null) GameManager.instance.ReleasePause();
        // Belt and braces before a scene load: an unbalanced pause anywhere would leave the menu
        // frozen. This is the same deliberate bypass the old PauseMenu.LoadMenu did.
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
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

// Pointer relay for a menu row. Hover SELECTS rather than merely highlighting, so the mouse and the
// keyboard drive the same single selection instead of disagreeing about which row is live.
public class PauseEntryHover : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public System.Action onEnter;
    public System.Action onClick;

    public void OnPointerEnter(PointerEventData e) { onEnter?.Invoke(); }
    public void OnPointerClick(PointerEventData e) { onClick?.Invoke(); }
}
