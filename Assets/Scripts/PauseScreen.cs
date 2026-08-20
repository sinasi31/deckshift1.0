using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

// The pause screen — Escape.
//
// ══ DUST SHEET — the first screen built in SALVAGE ═══════════════════════════════════════════════
//
// A sheet of canvas thrown over the frozen world, hung from a rope on wooden pegs.
//
// ⚠️ IT IS THE ONLY SOFT SCREEN IN THE GAME, AND THAT IS THE WHOLE POINT. Under Salvage every screen
// obeys the same five laws (scale, light, palette, two accents, wear) and differs only by WHAT THE
// OBJECT IS. Everything else coming is rigid — planks, a notice board, an anvil, a banner, paper on
// a grate. Pause is cloth. That single structural difference tells them apart with no new colour
// spent, which is exactly what the old nine-theme "pick a material and invert something" rule kept
// failing to do (it invented smoked glass, brass and frost, and burned a hue on each).
//
// Why cloth is the RIGHT object here rather than a nice one:
//   · Pause is not a place you travel to, so it must not be a panel you opened. A sheet does not
//     replace the room — it hangs in front of it, and the frozen game stays dimly visible past its
//     edges. No other screen in the game shows you the world behind it.
//   · Cloth has a motion vocabulary nothing else here owns: it DROPS, swings on its pins and settles.
//     On resume it is PULLED AWAY rather than faded out — a far better exit than an alpha ramp, and
//     it says "the world was always still there" in a way no dissolve can.
//   · The sound already fits. ProcSfx.PauseHalt is the one sound in the game defined by being CHOKED
//     — a damper clamping the ring away in 180ms. That is literally what cloth does to a sound.
//
// ⚠️ THE CONTENT IS PARENTED TO THE SHEET, so the text swings with the cloth it is printed on. Same
// lesson as the quest board's slips: the rotation PIVOT carries the metaphor, and here the pivot is
// the rope. Content that stayed level while the sheet swung would read as a texture behind a window.
//
// It is also the run's STATUS READOUT, which is the real reason it earns the whole frame. Several of
// these numbers — the exhausted count, the next Stagger price — are visible nowhere else in the game.
//
// House pattern: entirely procedural, self-bootstrapping, no prefab and no art files.
//
// ⚠️ THE ROOT STAYS ACTIVE; only its CONTENT child is toggled. Update() has to run to catch the
// Escape that OPENS the screen, and a deactivated GameObject does not get Update.
public class PauseScreen : MonoBehaviour
{
    private static PauseScreen instance;

    // ---- layout, in the canvas's 1920x1080 reference space, all centre-anchored -------------------

    // ⚠️ 1400 IS SET BY THE NARROWEST SUPPORTED ASPECT, NOT BY TASTE. Every CanvasScaler here matches
    // on HEIGHT, so the canvas is always 1080 tall and only WIDTH flexes: 1440 at 4:3, 1920 at 16:9,
    // 2560 at 21:9. A sheet wider than 1440 has its hanging edges cut off by the screen at 4:3, and
    // those edges are most of what makes it read as an object rather than a background. Widest
    // content is the menu hit plate at x -580, so 700 of half-width clears it by 120px.
    private const float SHEET_W = 1400f;
    private const float SHEET_TOP = 420f;
    private const float SHEET_BOTTOM = -340f;
    private const float ROPE_Y = 432f;

    // Where the sheet is pinned, as fractions across its width. Five pegs: an odd count so one sits
    // dead centre under the title, and the two outermost are inset so the corners hang free.
    private static readonly float[] Pegs = { 0.06f, 0.28f, 0.50f, 0.72f, 0.94f };

    private const float TITLE_Y = 318f;
    private const float SUB_Y = 266f;
    private const float TITLE_RULE_Y = 236f;
    private const float HEADER_Y = 194f;
    private const float HEADER_RULE_Y = 176f;
    private const float LIST_TOP = 128f;

    private const float MENU_X = -330f;
    private const float MENU_W = 380f;
    private const float MENU_STEP = 54f;
    private const float CHALK_X = -556f;

    private const float STAT_LABEL_X = 214f;
    private const float STAT_LABEL_W = 200f;
    private const float STAT_VALUE_X = 432f;
    private const float STAT_VALUE_W = 200f;
    private const float STAT_STEP = 36f;
    private const float BAR_X = 318f;
    private const float BAR_W = 110f;
    private const float BAR_VALUE_W = 108f;

    // Below the hem, on the dark. The sheet's torn edge is the best thing on the screen and putting
    // the hint under it is what makes you look at it.
    private const float FOOTER_Y = -404f;

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

    private RectTransform content, sheet, cloth, chalk;
    private Image underline;
    private RectTransform printed;   // parent for everything drawn ON the cloth, so it swings with it
    private CanvasGroup group;
    private TMP_FontAsset font;
    private AudioSource audioSource;

    private TextMeshProUGUI subtitle;
    private TextMeshProUGUI vFloor, vGold, vScrap, vRelics, vDeck, vExhaust, vStagger, vRecall;
    private TextMeshProUGUI vHealth, vShift;
    private Image barHealth, barShift;

    private bool isOpen;
    private bool wasUIPaused;
    private float markY, markTargetY;

    // ---- the swing ------------------------------------------------------------------------------
    // A hung sheet is a pendulum, so it is integrated as one rather than lerped. Explicit integration
    // plus one very long frame (a domain reload, an editor stall) throws a spring off the screen, so
    // dt is clamped — the same guard the character-select figures needed.
    private float swingAngle, swingVel;
    private float dropY, dropVel;
    private const float SwingK = 26f, SwingDamp = 3.1f;
    private const float DropK = 78f, DropDamp = 11f;
    private const float MaxStep = 1f / 30f;

    // ---- dust ------------------------------------------------------------------------------------
    // ⚠️ NOT the old suspended frost motes. Those said "time is held", which was Halt's idea and a
    // good one for frost. Cloth says something different and more physical: dust is knocked OFF the
    // sheet when it drops, and it falls. It is dense for the first second and then thins to almost
    // nothing, so the screen calms down instead of fidgeting at you for as long as you leave it open.
    private struct Mote
    {
        public RectTransform rt;
        public Image img;
        public Vector2 pos;
        public float vx, vy, size, alpha, life, maxLife;
    }

    private Mote[] motes;
    private const int MOTE_COUNT = 40;
    private float dustBurst;

    private GameObject subPanel;
    private bool subScreenOpen;

    private GameObject cachedHud;
    private bool hudWasActive;

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

        // ⚠️ NOT opaque. Every other screen in the game blacks the world out; this one dims it to
        // roughly a quarter and lets it show past the sheet's edges, because "the world is still
        // there, you have just stopped" is the entire premise. Raycast ON so nothing behind can be
        // clicked, and deliberately NOT a dismiss button — two of these entries are destructive and
        // a click-anywhere-to-close would put them one stray click from being lost.
        Image backdrop = AddImage(content, "Backdrop", null, new Color(0.030f, 0.025f, 0.021f, 0.78f), true);
        Stretch(backdrop.rectTransform);

        BuildRope();
        BuildSheet();
        BuildDust();

        BuildTitle();
        BuildMenu();
        BuildStatus();
        BuildFooter();

        content.gameObject.SetActive(false);
    }

    // A rope strung across the room, running off both edges of the screen. ⚠️ It must overhang the
    // canvas: a rope whose ends are visible is a prop floating in space, and at 21:9 the canvas is
    // 2560 wide, so this is sized against the widest supported aspect rather than against 1920.
    private void BuildRope()
    {
        const float SPAN = 2720f;
        const int SAG = 9;

        Sprite rope = SalvageSurfaces.RopeSpan(Salvage.Tex(SPAN), SAG);
        Image img = AddImage(content, "Rope", rope, Color.white, false);
        img.rectTransform.sizeDelta = new Vector2(SPAN, Salvage.Px(rope.rect.height));
        img.rectTransform.anchoredPosition = new Vector2(0f, ROPE_Y);
    }

    private void BuildSheet()
    {
        float h = SHEET_TOP - SHEET_BOTTOM;

        // ⚠️ THE PIVOT IS THE ROPE. Everything about this screen's motion — the drop, the swing, the
        // yank on resume — is a rotation about the line it hangs from. Pivoting at the centre makes
        // a swinging sheet look like a spinning card, which is the exact failure the quest board's
        // tack pivot exists to avoid.
        sheet = AddPoint(content, "Sheet", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        sheet.pivot = new Vector2(0.5f, 1f);
        sheet.sizeDelta = new Vector2(SHEET_W, h);
        sheet.anchoredPosition = new Vector2(0f, SHEET_TOP);

        Sprite s = SalvageSurfaces.Sheet(Salvage.Tex(SHEET_W), Salvage.Tex(h), Pegs);
        Image img = AddImage(sheet, "Cloth", s, Color.white, false);
        cloth = img.rectTransform;
        Stretch(cloth);

        // The pegs sit ON the rope, gripping the cloth. They belong to the sheet, not the rope, so
        // they travel with it as it swings — a peg that stayed put while the cloth moved would read
        // as the cloth having torn off.
        Sprite peg = SalvageSurfaces.Peg();
        for (int i = 0; i < Pegs.Length; i++)
        {
            Image p = AddImage(sheet, "Peg" + i, peg, Color.white, false);
            p.rectTransform.sizeDelta = new Vector2(Salvage.Px(peg.rect.width), Salvage.Px(peg.rect.height));
            p.rectTransform.anchorMin = p.rectTransform.anchorMax = new Vector2(Pegs[i], 1f);
            p.rectTransform.anchoredPosition = new Vector2(0f, -Salvage.Px(peg.rect.height) * 0.34f);
        }

        // Everything printed on the sheet hangs off it, so it swings with the cloth.
        RectTransform onCloth = AddPoint(sheet, "OnCloth", new Vector2(0.5f, 1f),
                                         new Vector2(0f, -SHEET_TOP), Vector2.zero);
        onCloth.sizeDelta = Vector2.zero;
        printed = onCloth;
    }


    private void BuildDust()
    {
        motes = new Mote[MOTE_COUNT];
        Sprite dot = Salvage.Pixel();

        for (int i = 0; i < MOTE_COUNT; i++)
        {
            RectTransform rt = AddPoint(content, "Dust" + i, new Vector2(0.5f, 0.5f), Vector2.zero,
                                        Vector2.one);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = dot;
            img.raycastTarget = false;
            img.color = new Color(0, 0, 0, 0);

            motes[i] = new Mote { rt = rt, img = img };
            RespawnMote(ref motes[i], true);
        }
    }

    private void RespawnMote(ref Mote m, bool anywhere)
    {
        // Dust comes off the CLOTH, so it starts inside the sheet's footprint, not the whole screen.
        float x = Random.Range(-SHEET_W * 0.5f, SHEET_W * 0.5f);
        float y = anywhere ? Random.Range(SHEET_BOTTOM, SHEET_TOP)
                           : Random.Range(SHEET_TOP - 180f, SHEET_TOP);

        m.pos = new Vector2(x, y);
        m.vx = Random.Range(-7f, 7f);
        m.vy = Random.Range(-26f, -9f);          // it falls; nothing here hangs still
        // Sized in WORLD pixels, so a dust speck is the size of a pixel of the game behind it.
        m.size = Salvage.Px(Random.value < 0.22f ? 2f : 1f);
        m.maxLife = Random.Range(2.2f, 5.5f);
        m.life = 0f;
        m.alpha = Random.Range(0.26f, 0.58f);
        m.rt.sizeDelta = new Vector2(m.size, m.size);
        m.rt.anchoredPosition = m.pos;
    }

    private void BuildTitle()
    {
        TextMeshProUGUI t = AddText(printed, "Title", "PAUSED", 62f, Salvage.TextBright,
                                    TextAlignmentOptions.Center);
        t.rectTransform.sizeDelta = new Vector2(900f, 76f);
        t.rectTransform.anchoredPosition = new Vector2(0f, TITLE_Y);
        t.characterSpacing = 16f;

        subtitle = AddText(printed, "Subtitle", "", 17f, Salvage.TextMuted, TextAlignmentOptions.Center);
        subtitle.rectTransform.sizeDelta = new Vector2(1100f, 26f);
        subtitle.rectTransform.anchoredPosition = new Vector2(0f, SUB_Y);
        subtitle.characterSpacing = 6f;

        // A chalk rule, scored across and fading at the ends. Chalk rather than a drawn line because
        // chalk is already this game's "someone wrote this here" — the exit marker uses the same
        // stroke sprite and the same colour.
        Image rule = AddImage(printed, "TitleRule", Parchment.Stroke(),
                              new Color(Salvage.Chalk.r, Salvage.Chalk.g, Salvage.Chalk.b, 0.30f), false);
        rule.rectTransform.sizeDelta = new Vector2(760f, 3f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, TITLE_RULE_Y);
    }

    private void BuildMenu()
    {
        AddColumnHeader("OPTIONS", MENU_X, MENU_W, TextAlignmentOptions.Left);

        // ⚠️ THE SELECTION IS MARKED IN CHALK, NOT LIT. Two earlier attempts lit the row instead —
        // a translucent accent plate (which needs a hue Salvage does not have to spend) and then a
        // "rubbed brighter" patch of cloth. The rubbed version was the better idea and still failed
        // on screen: a soft bright blob over a soft grey field has nothing to read as an EDGE, so it
        // came out looking like a lens flare or a blur artefact lying across the menu rather than
        // like wear. Chalk has a hard edge, it is already this game's "someone marked this" — the
        // exit arrow in the world is drawn with the same stroke sprite in the same colour — and it
        // costs no colour at all.
        underline = AddImage(printed, "Underline", Parchment.Stroke(), Salvage.Chalk, false);
        underline.rectTransform.sizeDelta = new Vector2(MENU_W - 40f, 3.4f);

        chalk = BuildChalkMark();

        AddEntry("RESUME", null, Close);
        AddEntry("SETTINGS", null, OpenSettings);
        AddEntry("HOW TO PLAY", null, () => OpenSubPanel("TutorialPanel"));
        AddEntry("ABANDON RUN", "ABANDON RUN?  CONFIRM", AbandonRun);
        AddEntry("QUIT TO DESKTOP", "QUIT TO DESKTOP?  CONFIRM", QuitGame);

        SetSelected(0, false);
    }

    // Two chalk strokes making a chevron. ⚠️ DRAWN, never typed as ">" — the display face is
    // CCBattleScarred and a glyph it happens not to carry renders as a blank or a box, which is the
    // same trap the character-select arrow hints hit.
    private RectTransform BuildChalkMark()
    {
        RectTransform mark = AddPoint(printed, "ChalkMark", new Vector2(0.5f, 0.5f),
                                      new Vector2(CHALK_X, 0f), new Vector2(28f, 30f));

        AddChalkStroke(mark, new Vector2(-3f, 7f), -38f, 22f, 3.4f);
        AddChalkStroke(mark, new Vector2(-3f, -7f), 38f, 22f, 3.4f);
        return mark;
    }

    private void AddChalkStroke(RectTransform parent, Vector2 pos, float angle, float len, float thick)
    {
        Image img = AddImage(parent, "Stroke", Parchment.Stroke(), Salvage.Chalk, false);
        img.rectTransform.sizeDelta = new Vector2(len, thick);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void AddEntry(string label, string confirmLabel, System.Action action)
    {
        int index = entries.Count;
        float y = LIST_TOP - index * MENU_STEP;

        Entry e = new Entry { label = label, confirmLabel = confirmLabel, action = action };

        e.text = AddText(printed, "Entry_" + label, label, 29f, Salvage.TextMuted,
                         TextAlignmentOptions.Left);
        e.text.rectTransform.sizeDelta = new Vector2(MENU_W, 44f);
        e.text.rectTransform.anchoredPosition = new Vector2(MENU_X, y);
        e.text.characterSpacing = 4f;

        // A transparent hit plate rather than a Button on the label: the label's own rect is only as
        // tall as its text, and a row you have to hit exactly feels broken next to keyboard nav.
        Image hit = AddImage(printed, "Hit_" + label, null, new Color(0f, 0f, 0f, 0f), true);
        hit.rectTransform.sizeDelta = new Vector2(MENU_W + 120f, 48f);
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
        vHealth = AddBarRow("HEALTH", row++, Salvage.Wound, out barHealth);
        vShift = AddBarRow("SHIFT", row++, Salvage.Shift, out barShift);
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
        TextMeshProUGUI h = AddText(printed, "Header_" + label, label, 14f, Salvage.TextMuted, align);
        h.rectTransform.sizeDelta = new Vector2(w, 20f);
        h.rectTransform.anchoredPosition = new Vector2(x, HEADER_Y);
        h.characterSpacing = 10f;

        Image rule = AddImage(printed, "HeaderRule_" + label, Parchment.Stroke(),
                              new Color(Salvage.Chalk.r, Salvage.Chalk.g, Salvage.Chalk.b, 0.16f), false);
        rule.rectTransform.sizeDelta = new Vector2(w + 220f, 2f);
        rule.rectTransform.anchoredPosition = new Vector2(x + 110f, HEADER_RULE_Y);
    }

    private TextMeshProUGUI AddStatRow(string label, int row)
    {
        float y = LIST_TOP - row * STAT_STEP;

        TextMeshProUGUI l = AddText(printed, "L_" + label, label, 15f, Salvage.TextMuted,
                                    TextAlignmentOptions.Left);
        l.rectTransform.sizeDelta = new Vector2(STAT_LABEL_W, 24f);
        l.rectTransform.anchoredPosition = new Vector2(STAT_LABEL_X, y);
        l.characterSpacing = 4f;

        TextMeshProUGUI v = AddText(printed, "V_" + label, "-", 17f, Salvage.TextBody,
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

        TextMeshProUGUI l = AddText(printed, "L_" + label, label, 15f, Salvage.TextMuted,
                                    TextAlignmentOptions.Left);
        l.rectTransform.sizeDelta = new Vector2(STAT_LABEL_W, 24f);
        l.rectTransform.anchoredPosition = new Vector2(STAT_LABEL_X, y);
        l.characterSpacing = 4f;

        Image track = AddImage(printed, "Track_" + label, Salvage.Pixel(),
                               new Color(0f, 0f, 0f, 0.30f), false);
        track.rectTransform.sizeDelta = new Vector2(BAR_W, 7f);
        track.rectTransform.anchoredPosition = new Vector2(BAR_X + BAR_W * 0.5f, y);

        bar = AddImage(track.rectTransform, "Fill_" + label, Salvage.Pixel(), fill, false);
        bar.rectTransform.anchorMin = new Vector2(0f, 0f);
        bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.anchoredPosition = Vector2.zero;
        bar.rectTransform.sizeDelta = new Vector2(BAR_W, 0f);

        // ⚠️ Wide enough for "100 / 100" at 17pt. An earlier pass gave this 70px and TMP wrapped the
        // health readout onto two lines, which pushed it out of its row.
        TextMeshProUGUI v = AddText(printed, "V_" + label, "-", 17f, Salvage.TextBody,
                                    TextAlignmentOptions.Right);
        v.rectTransform.sizeDelta = new Vector2(BAR_VALUE_W, 24f);
        v.rectTransform.anchoredPosition =
            new Vector2(STAT_VALUE_X + STAT_VALUE_W * 0.5f - BAR_VALUE_W * 0.5f, y);
        return v;
    }

    // ⚠️ NOT on the cloth. The footer hangs below the torn hem, on the dark, which is what makes you
    // notice the hem is torn — and it keeps the sheet's bottom edge as the last thing on the sheet.
    private void BuildFooter()
    {
        TextMeshProUGUI f = AddText(content, "Footer",
            "W / S  NAVIGATE          ENTER  SELECT          ESC  RESUME",
            13f, Salvage.TextFaint, TextAlignmentOptions.Center);
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

        // ⚠️ Re-arm the group by hand. Opening can interrupt CloseAnim mid-yank, and that coroutine
        // drops raycasts on its first frame and only restores them at its end — so a screen opened
        // during a close would come up looking perfect and ignoring every click.
        group.blocksRaycasts = true;
        group.interactable = true;

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

        // The throw. It comes in from above the rope, overshoots, and swings itself out.
        dropY = 620f;
        dropVel = 0f;
        swingAngle = Random.Range(0.7f, 1.5f) * (Random.value < 0.5f ? -1f : 1f);
        swingVel = 0f;
        dustBurst = 1f;
        for (int i = 0; i < motes.Length; i++) RespawnMote(ref motes[i], false);

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
        StartCoroutine(CloseAnim());
    }

    // ⚠️ THE SHEET IS PULLED AWAY, NOT FADED OUT, and the pause is released BEFORE it finishes.
    // A dissolve says the screen was an image laid over the game; whipping the cloth up off the rope
    // says the game was behind it the whole time, which is the one thing this screen exists to say.
    // The game is therefore already running for the ~0.2s the sheet takes to clear — that is the
    // point, not a compromise — so raycasts are dropped on the first frame or the player's first
    // click after resuming would be eaten by a sheet halfway off the screen.
    private IEnumerator CloseAnim()
    {
        const float dur = 0.19f;

        group.blocksRaycasts = false;
        group.interactable = false;

        float startY = sheet.anchoredPosition.y;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            sheet.anchoredPosition = new Vector2(0f, startY + 780f * k * k);   // accelerating away
            sheet.localRotation = Quaternion.Euler(0f, 0f, swingAngle * (1f - k) + k * 3.5f);
            group.alpha = 1f - k * k;
            yield return null;
        }

        // ⚠️ Open() may have run during the yank (Escape mashed). Deactivating unconditionally would
        // switch off a screen that is supposed to be up, leaving a paused game with no menu on it.
        if (!isOpen) content.gameObject.SetActive(false);

        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;
    }

    private IEnumerator OpenAnim()
    {
        const float dur = 0.16f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;   // the screen pauses the game; scaled time is frozen
            group.alpha = Mathf.Clamp01(t / dur);
            yield return null;
        }
        group.alpha = 1f;
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
        TickCloth();
        TickDust();
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
        // instantly behind the screen the player just dismissed. A one-frame memory covers every
        // Escape-handling screen and needs nothing from any of them.
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
        markTargetY = LIST_TOP - index * MENU_STEP;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e.armed) continue;                        // armed entries keep the warning colour
            e.text.color = i == selected ? Salvage.TextBright : Salvage.TextMuted;
        }

        if (!playSound) markY = markTargetY;
        else if (changed && audioSource != null)
        {
            SfxManager.PlayOn(audioSource, ProcSfx.PauseTick, 0.5f);
            // Nudging the row nudges the sheet. Tiny, but it is the difference between cloth and a
            // picture of cloth: touching a hung thing moves it.

            swingVel += Random.Range(-1.5f, 1.5f);
        }
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
            // ⚠️ Wound, not an accent. Salvage has exactly two accents and neither of them can say
            // "this will end your run" — danger is the one place a third colour is permitted.
            e.text.color = Salvage.Wound;
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
        e.text.color = entries.IndexOf(e) == selected ? Salvage.TextBright : Salvage.TextMuted;
    }

    private void TickArmTimeout()
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].armed && Time.unscaledTime > entries[i].armedUntil) Disarm(entries[i]);
    }

    // The sheet is a pendulum on a spring, integrated on unscaled time because the game is frozen.
    private void TickCloth()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);

        dropVel += (-DropK * dropY - DropDamp * dropVel) * dt;
        dropY += dropVel * dt;
        if (Mathf.Abs(dropY) < 0.05f && Mathf.Abs(dropVel) < 0.05f) { dropY = 0f; dropVel = 0f; }

        swingVel += (-SwingK * swingAngle - SwingDamp * swingVel) * dt;
        swingAngle += swingVel * dt;

        // A breath of air on it, forever. Without this the sheet eventually goes dead still and
        // stops being cloth; it is deliberately tiny — a permanent overlay must not compete with
        // the game behind it, and this one is over frozen gameplay.
        float idle = Mathf.Sin(Time.unscaledTime * 0.9f) * 0.16f
                   + Mathf.Sin(Time.unscaledTime * 0.37f) * 0.10f;

        sheet.anchoredPosition = new Vector2(0f, SHEET_TOP + dropY);
        sheet.localRotation = Quaternion.Euler(0f, 0f, swingAngle + idle);
    }

    private void TickDust()
    {
        if (motes == null) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);

        dustBurst = Mathf.Max(0f, dustBurst - dt * 0.55f);

        for (int i = 0; i < motes.Length; i++)
        {
            Mote m = motes[i];
            if (m.rt == null) continue;

            m.life += dt;
            m.pos += new Vector2(m.vx, m.vy) * dt;

            // Drifting sideways as it falls, because dust in still air does not fall straight.
            m.vx += Mathf.Sin(Time.unscaledTime * 1.3f + i) * 3f * dt;

            float k = Mathf.Clamp01(m.life / m.maxLife);
            float fade = 1f - k * k;

            // Only a fraction stay alive once the burst has settled — the screen calms down.
            float ceiling = 0.42f + dustBurst * 0.58f;

            Color c = Salvage.Torch;
            c.a = m.alpha * fade * ceiling;
            m.img.color = c;
            m.rt.anchoredPosition = m.pos;

            if (m.life >= m.maxLife || m.pos.y < SHEET_BOTTOM - 40f) RespawnMote(ref m, false);
            motes[i] = m;
        }
    }

    private void TickSelectionVisual()
    {
        // Framerate-independent ease, on unscaled time because the game is frozen.
        float k = 1f - Mathf.Exp(-24f * Time.unscaledDeltaTime);
        markY = Mathf.Lerp(markY, markTargetY, k);

        chalk.anchoredPosition = new Vector2(CHALK_X, markY);
        // The underline sits just under the label's baseline, indented like a hand-drawn rule.
        underline.rectTransform.anchoredPosition = new Vector2(MENU_X - 14f, markY - 23f);
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
            vStagger.color = lethal ? Salvage.Wound : Salvage.TextBody;
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
            vExhaust.color = exhausted > 0 ? Salvage.Torch : Salvage.TextBody;
            // Denominated in Shift, so it carries Shift's colour. That is the whole discipline:
            // cyan means "this is Shift", everywhere in the game, and nothing else ever borrows it.
            vRecall.text = dm.currentRecallCost + " SHIFT";
            vRecall.color = Salvage.Shift;
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
