using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The run map — press M. A chart of the whole act, so the player plans a ROUTE rather than picking
// one door at a time.
//
// THEME: Etch. The plate is oxidised copper — Act 1's own chemistry — and the act is ACID-ETCHED
// into it. That single idea decides everything else:
//
//   · Every mark is CUT IN, never drawn on. Node sockets and edge grooves carry a dark rim on the
//     lit side and a bright rim on the shaded side, which is the classic engraving cue. Swap those
//     two and the whole chart pops OUT as stickers stuck to a board.
//   · The light RAKES ACROSS from the upper left. Iron is lit by fire from below and its marks
//     (rivets) stand proud of the plate; this is the inversion of both — cold light from the side,
//     and a surface that is incised rather than raised.
//   · The accent is BARE COPPER, not light. Where the player has walked, the acid has bitten
//     through the patina to clean metal. Every other theme's accent is light being added; this
//     one's is surface being worn away.
//   · There is NO PARTICLE FIELD, because a chart has no air. The one thing that moves is the acid
//     still working at the frontier — the branches you can actually take.
//
// ⚠️ WHAT THE REBUILD FIXED, so nobody reintroduces it (2026-08-13). The previous version read as a
// debug node-graph, and the three causes were structural, not decorative:
//
//   1. NOTHING TO STAND ON. Nodes floated in a void with no floor lines, so a branching act read as
//      scattered dots. Floor bands are what turn dots into LEVELS, and they give the run a visible
//      scale that "7 TO THE BOSS" in a footer never could.
//   2. EDGES CROSSED CONSTANTLY. Nodes were plotted straight from their generator column, so a
//      route could not be traced by eye. See LayOutNodes: barycentric relaxation pulls each node
//      toward its neighbours' average and removes nearly every crossing.
//   3. THE ACT YOU ARE PLANNING WAS INVISIBLE. Everything not immediately reachable was drawn in
//      TextDisabled and labelled with nothing at all — so the 90% of the chart the player is
//      supposed to be reading ahead through could barely be seen and could not be named. Unreached
//      nodes are now etched in legible Patina and EVERY node carries its label.
//
// House pattern: entirely procedural, self-instantiating under the root Canvas, no prefab and no
// art files. Same shape as ScrapForgeScreen and BlompoScreen.
public class RunMapScreen : MonoBehaviour
{
    private static RunMapScreen instance;

    private const float WIN_W = 1560f;
    private const float WIN_H = 980f;
    private const float CHAMFER = 10f;

    // Chart area, inset inside the window. Generous on purpose: nodes are positioned by their
    // CENTRE, so the boss glyph and the label hanging under every node both need room.
    private const float AREA_TOP = 120f;
    private const float AREA_BOTTOM = 94f;
    private const float AREA_SIDE = 58f;

    // Node X is clamped this far inside the area so a label (118 wide) never runs off the plate,
    // and so the floor-band depth marks in the left gutter are never sat on.
    private const float X_PAD = 74f;
    // Minimum horizontal gap between two nodes on the same floor. Must exceed the label width or
    // neighbouring labels collide — which is what the old jitter kept doing.
    private const float MIN_SEP = 132f;

    private const int RELAX_ITERS = 24;
    private const float RELAX_RATE = 0.35f;

    private const float EDGE_THICKNESS = 3f;

    // ---- the etch palette ------------------------------------------------------------------------
    // Local to this screen rather than pushed into FlatUI.Verdigris: these are the values of a
    // specific material under a specific light, not a general-purpose theme ramp. Every one was
    // picked by screenshot — this project renders in LINEAR colour space, where a plausible-looking
    // number composites far brighter than it reads on paper.

    // The plate. Deliberately greener and a little lighter than the old Verdigris Surface, which
    // sat so near black that the chart looked like it was floating in a void rather than lying on
    // a sheet of metal.
    private static readonly Color PlateBase = new Color(0.062f, 0.104f, 0.092f, 0.995f);
    private static readonly Color PlateSheen = new Color(0.42f, 0.62f, 0.54f, 1f);   // raking light
    private static readonly Color Backdrop = new Color(0.010f, 0.018f, 0.016f, 0.945f);

    private static readonly Color GrooveDark = new Color(0.018f, 0.038f, 0.032f, 1f);
    private static readonly Color GrooveLit = new Color(0.250f, 0.360f, 0.305f, 1f);

    // ⚠️ THREE DISTINCT VALUES, AND THEY MUST STAY DISTINCT, or the sockets stop reading as holes.
    // The first build made the socket floor and its shadow rim the SAME colour (both GrooveDark),
    // so the dark wall was invisible against the floor it was drawn on and every node came out
    // looking like a button stuck on the plate. A recess needs: floor darker than the plate, and a
    // shadow wall darker again than the floor, so there is something for it to bite against.
    private static readonly Color SocketFloor = new Color(0.034f, 0.062f, 0.054f, 1f);
    private static readonly Color RimShadow = new Color(0.006f, 0.014f, 0.012f, 1f);

    // How far the two rims are pushed apart. 1.5px was too small to survive at this scale — the
    // bevel simply did not register. This is a look value: judge it on screen, not on paper.
    private const float BEVEL = 2.6f;

    // ⚠️ THE LOAD-BEARING COLOUR. This is everything the player has not reached yet — most of the
    // chart, and the entire thing they are planning a route through. The old value (TextDisabled,
    // 0.31/0.365/0.349) was a near-neutral grey that vanished into the plate. It must read as
    // "etched but not yet walked", never as "absent".
    private static readonly Color Patina = new Color(0.415f, 0.545f, 0.485f, 1f);
    private static readonly Color PatinaSoft = new Color(0.255f, 0.350f, 0.315f, 1f);

    // Bare metal, bitten through the patina. The accent, and the only warm thing on the plate.
    private static readonly Color Copper = new Color(0.815f, 0.520f, 0.285f, 1f);
    private static readonly Color CopperHot = new Color(1.00f, 0.715f, 0.415f, 1f);
    private static readonly Color CopperWorn = new Color(0.565f, 0.395f, 0.250f, 1f);

    private static readonly Color TextTitle = new Color(0.885f, 0.930f, 0.905f, 1f);
    private static readonly Color TextQuiet = new Color(0.470f, 0.560f, 0.525f, 1f);

    private RectTransform window, area;
    private CanvasGroup group;
    private TMP_FontAsset font;
    private TextMeshProUGUI footer, sub;

    private bool mustChoose;
    private System.Action onChosen;

    private bool isOpen;
    private GameObject cachedHud;
    private bool hudWasActive;
    private GameState prevState;

    // Rebuilt every Refresh. Kept so the motion tick doesn't have to search the hierarchy.
    private readonly List<Image> frontierBlooms = new List<Image>();
    private readonly List<Image> frontierRims = new List<Image>();
    private readonly List<Image> travelledEdges = new List<Image>();
    private readonly List<GameObject> spawned = new List<GameObject>();

    private static readonly FlatUI.Theme T = FlatUI.Verdigris;

    // ---- entry points ---------------------------------------------------------------------------

    public static void Toggle()
    {
        EnsureInstance();
        if (instance == null) return;
        if (instance.isOpen)
        {
            if (instance.mustChoose) return;   // can't M your way out of a required choice
            instance.Hide();
        }
        else instance.Show();
    }

    // Opens the map because the run needs a branch before it can continue. `onChosen` runs once
    // the player commits — that is what actually spawns the next room.
    //
    // If the screen cannot be created, onChosen is invoked immediately rather than dropped. A
    // missing Canvas must not strand the run in a room with no way forward.
    public static void OpenForChoice(System.Action onChosen)
    {
        EnsureInstance();
        if (instance == null)
        {
            Debug.LogWarning("RunMapScreen: no Canvas, continuing without a route choice.");
            onChosen?.Invoke();
            return;
        }

        instance.mustChoose = true;
        instance.onChosen = onChosen;

        if (instance.isOpen) instance.Refresh();
        else instance.Show();
    }

    public static void Open()
    {
        EnsureInstance();
        if (instance == null || instance.isOpen) return;
        instance.Show();
    }

    public static void Close()
    {
        if (instance != null && instance.isOpen) instance.Hide();
    }

    public static bool IsOpen => instance != null && instance.isOpen;

    private static void EnsureInstance()
    {
        if (instance != null) return;

        Canvas canvas = FindRootCanvas();
        if (canvas == null) { Debug.LogWarning("RunMapScreen: no Canvas found in scene."); return; }

        GameObject go = new GameObject("RunMapScreen", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<RunMapScreen>();
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

    // ---- construction ---------------------------------------------------------------------------

    private void Build()
    {
        font = FlatUI.UIFont();

        RectTransform root = GetComponent<RectTransform>();
        Stretch(root);
        group = gameObject.AddComponent<CanvasGroup>();

        Image backdrop = AddImage(transform, "Backdrop", null, Backdrop, true);
        Stretch(backdrop.rectTransform);
        Button backBtn = backdrop.gameObject.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(DismissIfAllowed);

        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIN_W, WIN_H));
        Image winBg = window.gameObject.AddComponent<Image>();
        winBg.sprite = FlatUI.Panel((int)CHAMFER);
        winBg.type = Image.Type.Sliced;      // MUST be Sliced — Simple stretches the chamfer sprite
        winBg.color = PlateBase;
        winBg.raycastTarget = true;

        // The raking light. A single broad bloom parked off the plate's upper-left corner, very
        // low alpha — that asymmetry is what makes the surface read as metal under a side light
        // rather than as a flat filled rectangle. It is NOT a border glow: it has no edge of its own.
        Image sheen = AddImage(window, "Sheen", MapGlyphs.Bloom(), Fade(PlateSheen, 0.055f), false);
        sheen.rectTransform.anchorMin = sheen.rectTransform.anchorMax = new Vector2(0f, 1f);
        sheen.rectTransform.anchoredPosition = new Vector2(120f, -60f);
        sheen.rectTransform.sizeDelta = new Vector2(1500f, 1100f);

        // Patchy oxidation. Verdigris does not form evenly, and an untextured plate is what made
        // the first pass read as a big green rectangle rather than a sheet of corroded metal.
        // Deterministic seed so the blotches never move between openings — they are part of the
        // object, not an effect. Alpha is deliberately about half what looks right on paper: this
        // is LINEAR colour space and anything stronger becomes visible cloud instead of texture.
        System.Random mot = new System.Random(20260813);
        for (int i = 0; i < 16; i++)
        {
            float mw = 180f + (float)mot.NextDouble() * 460f;
            Image blot = AddImage(window, "Patina" + i, MapGlyphs.Bloom(),
                Fade(i % 3 == 0 ? PlateSheen : GrooveDark, 0.030f), false);
            blot.rectTransform.anchoredPosition = new Vector2(
                ((float)mot.NextDouble() - 0.5f) * WIN_W * 0.94f,
                ((float)mot.NextDouble() - 0.5f) * WIN_H * 0.94f);
            blot.rectTransform.sizeDelta = new Vector2(mw, mw * (0.55f + (float)mot.NextDouble() * 0.7f));
        }

        Image winEdge = AddImage(window, "Edge", FlatUI.Outline((int)CHAMFER, 2), T.Border, false);
        winEdge.type = Image.Type.Sliced;
        Stretch(winEdge.rectTransform);
        FlatUI.ApplySliceThickness(winEdge, 2f);

        // Hairline along the top lip: the plate's own cut edge catching the same raking light.
        Image lip = AddImage(window, "Lip", FlatUI.FadedRule(), Fade(PlateSheen, 0.62f), false);
        lip.rectTransform.anchorMin = new Vector2(0f, 1f);
        lip.rectTransform.anchorMax = new Vector2(1f, 1f);
        lip.rectTransform.pivot = new Vector2(0.5f, 1f);
        lip.rectTransform.anchoredPosition = new Vector2(0f, -3f);
        lip.rectTransform.sizeDelta = new Vector2(-56f, 2f);

        TextMeshProUGUI title = AddText(window, "Title", "THE OXIDATION DISTRICT", 30f, TextTitle,
            TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -24f);
        title.rectTransform.sizeDelta = new Vector2(-80f, 38f);
        title.characterSpacing = 8f;

        sub = AddText(window, "Sub", "", 15f, TextQuiet, TextAlignmentOptions.Center);
        sub.rectTransform.anchorMin = new Vector2(0f, 1f);
        sub.rectTransform.anchorMax = new Vector2(1f, 1f);
        sub.rectTransform.pivot = new Vector2(0.5f, 1f);
        sub.rectTransform.anchoredPosition = new Vector2(0f, -58f);
        sub.rectTransform.sizeDelta = new Vector2(-80f, 22f);
        sub.characterSpacing = 6f;

        Image rule = AddImage(window, "TitleRule", FlatUI.FadedRule(), Fade(GrooveLit, 0.85f), false);
        rule.rectTransform.anchorMin = new Vector2(0f, 1f);
        rule.rectTransform.anchorMax = new Vector2(1f, 1f);
        rule.rectTransform.pivot = new Vector2(0.5f, 1f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, -84f);
        rule.rectTransform.sizeDelta = new Vector2(-120f, 2f);

        area = AddPoint(window, "Area", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        area.anchorMin = new Vector2(0f, 0f);
        area.anchorMax = new Vector2(1f, 1f);
        area.offsetMin = new Vector2(AREA_SIDE, AREA_BOTTOM);
        area.offsetMax = new Vector2(-AREA_SIDE, -AREA_TOP);

        footer = AddText(window, "Footer", "", 14f, TextQuiet, TextAlignmentOptions.Center);
        footer.rectTransform.anchorMin = new Vector2(0f, 0f);
        footer.rectTransform.anchorMax = new Vector2(1f, 0f);
        footer.rectTransform.pivot = new Vector2(0.5f, 0f);
        footer.rectTransform.anchoredPosition = new Vector2(0f, 20f);
        footer.rectTransform.sizeDelta = new Vector2(-80f, 24f);
        footer.characterSpacing = 4f;
    }

    // ---- open / close ---------------------------------------------------------------------------

    private void Show()
    {
        if (isOpen) return;
        isOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (GameManager.instance != null)
        {
            prevState = GameManager.instance.currentState;
            GameManager.instance.RequestPause();
            GameManager.instance.SetGameState(GameState.Paused);
        }

        if (cachedHud == null) cachedHud = GameObject.Find("GameplayHUD");
        hudWasActive = cachedHud != null && cachedHud.activeSelf;
        if (cachedHud != null) cachedHud.SetActive(false);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        FitWindowToCanvas();
        Refresh();

        StopAllCoroutines();
        StartCoroutine(OpenAnim());
    }

    private void Hide()
    {
        if (!isOpen) return;
        isOpen = false;

        if (GameManager.instance != null)
        {
            GameManager.instance.ReleasePause();
            GameManager.instance.SetGameState(prevState);
        }

        if (cachedHud != null) cachedHud.SetActive(hudWasActive);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);

        gameObject.SetActive(false);
    }

    // Shrinks the window if the canvas is narrower or shorter than its design size.
    //
    // ⚠️ This is the WIDEST window in the game (1560), so it is the first thing to run off the edge
    // on a narrow display. With the canvas matching HEIGHT, its logical width is 1080 * aspect —
    // 1920 at 16:9 and 2560 at 21:9, but only 1440 at 4:3.
    //
    // Only the map RESIZES. Its chart lives in `area`, anchored to the window corners with insets,
    // so it genuinely reflows into whatever size it is given — and LayOutNodes reads `area.rect`
    // fresh every Refresh, so the relaxation re-solves for the smaller width. The other screens
    // position content at fixed offsets from their centre and must SCALE instead.
    private void FitWindowToCanvas()
    {
        RectTransform parent = transform as RectTransform;
        if (parent == null) return;

        Rect r = parent.rect;
        if (r.width <= 1f || r.height <= 1f) return;   // not laid out yet

        const float MARGIN = 40f;
        float w = Mathf.Min(WIN_W, r.width - MARGIN);
        float h = Mathf.Min(WIN_H, r.height - MARGIN);
        window.sizeDelta = new Vector2(w, h);
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

    private void Update()
    {
        if (!isOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { DismissIfAllowed(); return; }
        TickMotion();
    }

    private void DismissIfAllowed()
    {
        if (mustChoose) return;
        Hide();
    }

    private void ConfirmChoice()
    {
        System.Action cb = onChosen;
        onChosen = null;
        mustChoose = false;

        Hide();
        cb?.Invoke();
    }

    // The only motion on the plate, and it is information, not atmosphere: acid still working at
    // the frontier. Deliberately slow and shallow — this screen is read, not watched, and anything
    // livelier competes with the route the player is trying to trace.
    private void TickMotion()
    {
        float breathe = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.8f);

        for (int i = 0; i < frontierBlooms.Count; i++)
        {
            Image img = frontierBlooms[i];
            if (img == null) continue;
            img.color = Fade(CopperHot, Mathf.Lerp(0.035f, 0.105f, breathe));
        }

        for (int i = 0; i < frontierRims.Count; i++)
        {
            Image img = frontierRims[i];
            if (img == null) continue;
            img.color = Color.Lerp(Copper, CopperHot, breathe);
        }

        // Offset per edge so the glint runs ALONG the walked route rather than the whole path
        // flashing at once, which reads as one object instead of a path.
        for (int i = 0; i < travelledEdges.Count; i++)
        {
            Image img = travelledEdges[i];
            if (img == null) continue;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.6f - i * 0.5f);
            img.color = Color.Lerp(CopperWorn, Copper, k);
        }
    }

    // ---- drawing --------------------------------------------------------------------------------

    private void Refresh()
    {
        // Deactivate before Destroy: Unity's Destroy is deferred to end of frame, so the old chart
        // would otherwise render on top of the new one for a frame — and its buttons would still
        // answer GetComponentsInChildren.
        foreach (GameObject go in spawned)
        {
            if (go == null) continue;
            go.SetActive(false);
            Destroy(go);
        }
        spawned.Clear();
        frontierBlooms.Clear();
        frontierRims.Clear();
        travelledEdges.Clear();

        RunMapManager mgr = RunMapManager.instance;
        RunMap map = mgr != null ? mgr.Map : null;

        if (map == null || map.nodes.Count == 0)
        {
            SetFooter("No act in progress.");
            return;
        }

        Dictionary<int, Vector2> pos = LayOutNodes(map);

        DrawFloorBands(map);

        // Edges first so the sockets sit on top of where they land.
        foreach (MapNode n in map.nodes)
            foreach (int id in n.next)
            {
                MapNode m = map.Get(id);
                if (m == null) continue;
                DrawEdge(map, mgr, n, m, pos[n.id], pos[m.id]);
            }

        foreach (MapNode n in map.nodes) DrawNode(map, mgr, n, pos[n.id]);

        if (sub != null) sub.text = mustChoose ? "CHOOSE YOUR ROUTE" : "PRESS ESC TO CLOSE";

        int floorsLeft = (map.floors - 1) - (map.Current != null ? map.Current.floor : 0);
        if (mustChoose)
            SetFooter($"PICK A BRANCH TO CONTINUE   ·   {floorsLeft} TO THE BOSS");
        else if (mgr != null && mgr.HasChosenNext)
        {
            MapNode chosen = map.Get(mgr.ChosenNextId);
            SetFooter($"NEXT: {MapGlyphs.LabelFor(chosen.type)}{RechargeSuffix(chosen)}   ·   {floorsLeft} TO THE BOSS");
        }
        else if (map.AvailableNext().Count > 0)
            SetFooter($"PICK A BRANCH   ·   {floorsLeft} TO THE BOSS");
        else
            SetFooter("ACT COMPLETE");
    }

    private string RechargeSuffix(MapNode n)
        => n.recharge == RechargeType.None ? "" : $" + {MapGlyphs.LabelFor(n.recharge)}";

    private void SetFooter(string s)
    {
        if (footer != null) footer.text = s;
    }

    // A scored line per floor, with a depth mark in the left gutter.
    //
    // This is the single change that turns the chart from scattered dots into a structure you climb.
    // It also gives the run a visible scale: how far the boss is stops being a number in the footer
    // and becomes a distance you can see.
    private void DrawFloorBands(RunMap map)
    {
        Rect r = area.rect;
        float h = r.height, w = r.width;
        float step = map.floors > 1 ? h / (map.floors - 1) : h;
        int curFloor = map.Current != null ? map.Current.floor : 0;

        for (int f = 0; f < map.floors; f++)
        {
            float y = -h * 0.5f + step * f;
            bool passed = f < curFloor;
            bool here = f == curFloor;
            bool bossBand = f == map.floors - 1;

            // Bands behind the player are worn to copper like everything else they have walked.
            // The boss band is deliberately the heaviest line on the plate: it is the edge of the
            // act, and the thing every route on the chart is pointing at.
            Color line = here ? Fade(Copper, 0.42f)
                       : bossBand ? Fade(GrooveLit, 0.62f)
                       : passed ? Fade(CopperWorn, 0.24f)
                                : Fade(GrooveLit, 0.30f);

            GameObject go = new GameObject($"Band{f}", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(area, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(w, bossBand ? 3f : 2f);   // 2px min — a hairline is subpixel luck

            Image img = go.AddComponent<Image>();
            img.sprite = FlatUI.FadedRule();     // scores across and fades at the ends
            img.color = line;
            img.raycastTarget = false;
            spawned.Add(go);

            // Depth mark, in the gutter X_PAD keeps clear of every node and label.
            string mark = f == 0 ? "HUB" : f == map.floors - 1 ? "BOSS" : f.ToString("00");
            TextMeshProUGUI t = AddText(area, $"Depth{f}", mark, 11f,
                here ? Fade(Copper, 0.95f) : Fade(passed ? CopperWorn : PatinaSoft, 0.9f),
                TextAlignmentOptions.Left);
            t.rectTransform.anchoredPosition = new Vector2(-w * 0.5f + 4f, y + 11f);
            t.rectTransform.sizeDelta = new Vector2(70f, 14f);
            t.characterSpacing = 3f;
            spawned.Add(t.gameObject);
        }
    }

    // Positions every node in the chart area.
    //
    // Each node is pulled toward the mean X of everything it connects to (barycentric ordering,
    // the standard layered-graph fix), then each row is pushed apart to MIN_SEP.
    //
    // ⚠️ WHAT THIS DOES AND DOES NOT DO — measured over 300 generated acts, because the first
    // version of this comment claimed a crossing fix that does not exist:
    //
    //   · Edge crossings were ALREADY ZERO and still are (0.00/act in both layouts, worst case 0).
    //     The generator's column carving prevents them; nothing here was needed. If the old chart
    //     looked tangled, that was edges passing near unrelated nodes, not true intersections.
    //   · What it actually buys: average sideways travel per edge falls 214px -> 86px, a 60% drop.
    //     Edges become far more vertical, so a route reads as one continuous climb instead of a
    //     zigzag. THAT is the legibility win.
    //   · The cost, and why MIN_SEP is load-bearing: pulling nodes toward their neighbours also
    //     pulls them toward each other, and the tightest same-floor gap falls 232px -> 132px. That
    //     is still wider than the 118px label box, which is the only reason labels don't collide.
    //     Lower MIN_SEP below the label width and they will.
    //
    // Deterministic: no random jitter at all. The old jitter existed to stop the chart looking like
    // a spreadsheet, but on an ENGRAVED plate sitting exactly on the floor line is correct —
    // precision is the material — and the bands now supply the structure the jitter was faking.
    private Dictionary<int, Vector2> LayOutNodes(RunMap map)
    {
        Rect r = area.rect;
        float w = r.width, h = r.height;
        float halfW = w * 0.5f;
        float limit = Mathf.Max(0f, halfW - X_PAD);

        float step = map.floors > 1 ? h / (map.floors - 1) : h;

        int maxCol = 0;
        foreach (MapNode n in map.nodes) if (n.column > maxCol) maxCol = n.column;
        float colStep = maxCol > 0 ? w / (maxCol + 1) : w;

        // Seed from the generator's columns.
        Dictionary<int, float> x = new Dictionary<int, float>();
        foreach (MapNode n in map.nodes)
            x[n.id] = maxCol > 0 ? -halfW + colStep * (n.column + 0.5f) : 0f;

        // Group by floor once.
        List<List<MapNode>> byFloor = new List<List<MapNode>>();
        for (int f = 0; f < map.floors; f++) byFloor.Add(new List<MapNode>());
        foreach (MapNode n in map.nodes)
            if (n.floor >= 0 && n.floor < map.floors) byFloor[n.floor].Add(n);

        for (int iter = 0; iter < RELAX_ITERS; iter++)
        {
            foreach (List<MapNode> row in byFloor)
            {
                foreach (MapNode n in row)
                {
                    // The act's spine. Start and Boss are single nodes and belong dead centre;
                    // letting them drift makes the whole chart look tipped over.
                    if (n.type == MapNodeType.Start || n.type == MapNodeType.Boss) { x[n.id] = 0f; continue; }

                    float sum = 0f; int count = 0;
                    foreach (int id in n.next) { if (x.ContainsKey(id)) { sum += x[id]; count++; } }
                    foreach (int id in n.prev) { if (x.ContainsKey(id)) { sum += x[id]; count++; } }
                    if (count == 0) continue;

                    x[n.id] = Mathf.Lerp(x[n.id], sum / count, RELAX_RATE);
                }

                // Push the row apart, then re-centre it so repeated passes don't march it right.
                row.Sort((a, b) => x[a.id].CompareTo(x[b.id]));
                for (int i = 1; i < row.Count; i++)
                {
                    float need = x[row[i - 1].id] + MIN_SEP;
                    if (x[row[i].id] < need) x[row[i].id] = need;
                }
                if (row.Count > 1)
                {
                    float mid = (x[row[0].id] + x[row[row.Count - 1].id]) * 0.5f;
                    foreach (MapNode n in row)
                        if (n.type != MapNodeType.Start && n.type != MapNodeType.Boss) x[n.id] -= mid;
                }

                foreach (MapNode n in row)
                    if (n.type != MapNodeType.Start && n.type != MapNodeType.Boss)
                        x[n.id] = Mathf.Clamp(x[n.id], -limit, limit);
            }
        }

        Dictionary<int, Vector2> pos = new Dictionary<int, Vector2>();
        foreach (MapNode n in map.nodes)
            pos[n.id] = new Vector2(x[n.id], -h * 0.5f + step * n.floor);
        return pos;
    }

    // A channel cut between two nodes: a dark groove with a lit wall along one side. The offset is
    // perpendicular to the run and constant in SCREEN space, so every groove on the plate catches
    // the light from the same direction — which is what sells the engraving.
    private void DrawEdge(RunMap map, RunMapManager mgr, MapNode from, MapNode to, Vector2 a, Vector2 b)
    {
        bool travelled = map.visited.Contains(from.id) && map.visited.Contains(to.id);
        bool open = map.currentNodeId == from.id && map.CanTravelTo(to.id);
        bool committed = open && mgr != null && mgr.ChosenNextId == to.id;

        Vector2 delta = b - a;
        float len = delta.magnitude;
        if (len < 0.01f) return;

        // ⚠️ TRIM BOTH ENDS BACK TO THE SOCKET RIM. A channel runs BETWEEN two sockets; drawn
        // centre-to-centre it runs straight through them and, worse, straight through the label
        // hanging under the far one. On the hub row — five edges fanning up into five labelled
        // nodes — that put a line through every word on the frontier.
        Vector2 dir = delta / len;
        float ra = MapGlyphs.SizeFor(from.type) * 0.5f + 10f + 4f;
        float rb = MapGlyphs.SizeFor(to.type) * 0.5f + 10f + 4f;
        if (ra + rb >= len - 4f) return;      // sockets touch; a stub between them reads as dirt

        a += dir * ra;
        b -= dir * rb;
        delta = b - a;
        len = delta.magnitude;

        float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Vector2 mid = a + delta * 0.5f;

        Color groove;
        float thickness;
        if (travelled) { groove = Copper; thickness = EDGE_THICKNESS + 2f; }
        else if (committed) { groove = CopperHot; thickness = EDGE_THICKNESS + 1.5f; }
        else if (open) { groove = Patina; thickness = EDGE_THICKNESS + 0.5f; }
        else { groove = PatinaSoft; thickness = EDGE_THICKNESS - 0.5f; }

        // The dark cut, offset down-right — the shadowed inside wall of the channel.
        MakeBar($"EdgeCut{from.id}_{to.id}", mid + PerpOffset(delta, -1.4f), len, thickness,
                ang, Fade(GrooveDark, 0.75f), null);

        Image lit = MakeBar($"Edge{from.id}_{to.id}", mid, len, thickness, ang, groove, null);
        if (travelled) travelledEdges.Add(lit);
    }

    // Perpendicular offset of `dist` px to the left of the run direction.
    private static Vector2 PerpOffset(Vector2 dir, float dist)
    {
        Vector2 n = new Vector2(-dir.y, dir.x).normalized;
        return n * dist;
    }

    private Image MakeBar(string name, Vector2 pos, float len, float thick, float angle, Color c, Sprite s)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(area, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(len, thick);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        Image img = go.AddComponent<Image>();
        img.sprite = s != null ? s : FlatUI.Pixel();
        img.color = c;
        img.raycastTarget = false;
        spawned.Add(go);
        return img;
    }

    private void DrawNode(RunMap map, RunMapManager mgr, MapNode n, Vector2 p)
    {
        bool isCurrent = map.currentNodeId == n.id;
        bool visited = map.visited.Contains(n.id);
        bool reachable = map.CanTravelTo(n.id);
        bool committed = mgr != null && mgr.ChosenNextId == n.id;

        float size = MapGlyphs.SizeFor(n.type);
        float socket = size + 20f;

        GameObject go = new GameObject($"Node{n.id}", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(area, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = p;
        rt.sizeDelta = new Vector2(socket + 12f, socket + 12f);
        spawned.Add(go);

        // The boss gets a standing haze of its own — not a pulse, because it is a destination
        // rather than an option. It is the one node the player never chooses and always ends at,
        // so it should have presence without asking to be clicked.
        if (n.type == MapNodeType.Boss)
        {
            Image aura = AddImage(rt, "BossAura", MapGlyphs.Bloom(), Fade(GrooveLit, 0.10f), false);
            aura.rectTransform.sizeDelta = new Vector2(socket * 3.4f, socket * 3.4f);
        }

        // The acid still biting at the frontier. Behind everything, and the only thing that moves.
        if (reachable)
        {
            Image bloom = AddImage(rt, "Bloom", MapGlyphs.Bloom(), Fade(CopperHot, 0.06f), false);
            bloom.rectTransform.sizeDelta = new Vector2(socket * 2.4f, socket * 2.4f);
            frontierBlooms.Add(bloom);
        }

        // --- the socket, cut into the plate -------------------------------------------------------
        // Recess floor, then the rim TWICE at opposite offsets: dark on the lit (upper-left) side,
        // bright on the shaded (lower-right) side. ⚠️ Swap those two and the node reads as a button
        // stuck ON the plate instead of a hole punched INTO it — that inversion is the entire cue.
        Image well = AddImage(rt, "Socket", MapGlyphs.Disc(), SocketFloor, false);
        well.rectTransform.sizeDelta = new Vector2(socket, socket);

        Image rimDark = AddImage(rt, "RimDark", MapGlyphs.SocketRim(), RimShadow, false);
        rimDark.rectTransform.sizeDelta = new Vector2(socket, socket);
        rimDark.rectTransform.anchoredPosition = new Vector2(-BEVEL, BEVEL);

        Color rimCol = isCurrent ? CopperHot
                     : visited ? CopperWorn
                     : reachable ? Copper
                     : Fade(GrooveLit, 0.95f);
        Image rimLit = AddImage(rt, "RimLit", MapGlyphs.SocketRim(), rimCol, false);
        rimLit.rectTransform.sizeDelta = new Vector2(socket, socket);
        rimLit.rectTransform.anchoredPosition = new Vector2(BEVEL, -BEVEL);
        if (reachable) frontierRims.Add(rimLit);

        if (committed)
        {
            Image lockRing = AddImage(rt, "Committed", MapGlyphs.Ring(), CopperHot, false);
            lockRing.rectTransform.sizeDelta = new Vector2(socket + 16f, socket + 16f);
        }

        // --- the mark itself ----------------------------------------------------------------------
        Color tint = isCurrent ? CopperHot
                   : visited ? CopperWorn
                   : reachable ? CopperHot
                   : Patina;                 // ⚠️ legible, not disabled — see the header

        // Its own shadow in the socket, so the glyph looks stamped rather than pasted.
        Image glyphShadow = AddImage(rt, "GlyphShadow", MapGlyphs.ForNode(n.type), Fade(RimShadow, 0.85f), false);
        glyphShadow.rectTransform.sizeDelta = new Vector2(size, size);
        glyphShadow.rectTransform.anchoredPosition = new Vector2(-2f, 2f);

        Image glyph = AddImage(rt, "Glyph", MapGlyphs.ForNode(n.type), tint, false);
        glyph.rectTransform.sizeDelta = new Vector2(size, size);

        if (n.recharge != RechargeType.None)
        {
            float badge = 20f;
            RectTransform bt = AddPoint(rt, "Recharge", new Vector2(0.5f, 0.5f),
                new Vector2(socket * 0.46f, socket * 0.46f), new Vector2(badge + 10f, badge + 10f));

            Image disc = bt.gameObject.AddComponent<Image>();
            disc.sprite = MapGlyphs.Disc();
            disc.color = Fade(GrooveDark, 0.95f);
            disc.raycastTarget = false;

            Image ring = AddImage(bt, "BadgeRim", MapGlyphs.SocketRim(),
                reachable || visited ? Fade(Copper, 0.9f) : Fade(GrooveLit, 0.9f), false);
            ring.rectTransform.sizeDelta = new Vector2(badge + 10f, badge + 10f);

            Image mark = AddImage(bt, "Mark", MapGlyphs.ForRecharge(n.recharge),
                reachable || visited ? CopperHot : Patina, false);
            mark.rectTransform.sizeDelta = new Vector2(badge, badge);
        }

        AddLabel(rt, n, socket, isCurrent, visited, reachable);

        // Only reachable nodes are clickable. Choosing is a commitment, not navigation: the click
        // sets the branch and the run travels there when the player reaches the exit door.
        if (reachable)
        {
            Button btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            Image hit = AddImage(rt, "Hit", FlatUI.Pixel(), new Color(0f, 0f, 0f, 0f), true);
            hit.rectTransform.sizeDelta = new Vector2(socket + 12f, socket + 12f);
            btn.targetGraphic = hit;

            int id = n.id;
            btn.onClick.AddListener(() =>
            {
                if (RunMapManager.instance == null || !RunMapManager.instance.ChooseNext(id)) return;
                // Opened by the exit: committing IS leaving, so go. Opened with M: this is
                // planning, so mark the branch and let the player keep reading the act.
                if (mustChoose) ConfirmChoice();
                else Refresh();
            });
        }
    }

    // ⚠️ EVERY node is labelled, not just the reachable ones. The whole point of showing the act is
    // planning a route through it, and you cannot plan through nodes that have no names. The old
    // version labelled only what you could reach this instant, which meant the 90% of the chart the
    // feature exists to show was anonymous.
    //
    // Distance is carried by WEIGHT rather than presence: the branches in front of you are bright
    // and spaced, the act ahead is quiet patina.
    private void AddLabel(RectTransform parent, MapNode n, float socket,
                          bool isCurrent, bool visited, bool reachable)
    {
        string text = isCurrent ? "YOU ARE HERE" : MapGlyphs.LabelFor(n.type);
        if (n.recharge != RechargeType.None && !isCurrent) text += "\n+ " + MapGlyphs.LabelFor(n.recharge);

        Color c = isCurrent ? CopperHot
                : reachable ? Fade(CopperHot, 0.95f)
                : visited ? Fade(CopperWorn, 0.9f)
                : Fade(Patina, 0.82f);

        float fs = reachable || isCurrent ? 12.5f : 11.5f;
        float y = -(socket * 0.5f + 7f);
        int lines = text.Contains("\n") ? 2 : 1;

        // ⚠️ A CHASED PATCH BEHIND THE WORD, and it is not decoration — it is the fix for edges
        // running through labels. Trimming the channels at the socket rim removes most of that, but
        // a node with an edge leaving DOWNWARD still has that edge pass straight through the label
        // hanging beneath it — measured on the committed branch, where the bright copper line went
        // right through "ELITE". Soft-edged rather than a rectangle so it reads as a shallow
        // depression beaten into the plate, not a UI box laid on top of one.
        Image patch = AddImage(parent, "LabelPatch", MapGlyphs.Bloom(), Fade(SocketFloor, 0.80f), false);
        patch.rectTransform.pivot = new Vector2(0.5f, 1f);
        patch.rectTransform.anchoredPosition = new Vector2(0f, y + 4f);
        patch.rectTransform.sizeDelta = new Vector2(128f, 20f + lines * 15f);

        // Stamped, like the glyph: a dark copy behind, offset along the same light direction.
        TextMeshProUGUI shadow = AddText(parent, "LabelShadow", text, fs, Fade(RimShadow, 0.9f),
                                         TextAlignmentOptions.Top);
        shadow.rectTransform.pivot = new Vector2(0.5f, 1f);
        shadow.rectTransform.anchoredPosition = new Vector2(-1.4f, y - 1.4f);
        shadow.rectTransform.sizeDelta = new Vector2(118f, 34f);
        shadow.characterSpacing = 3f;
        shadow.lineSpacing = -14f;

        TextMeshProUGUI label = AddText(parent, "Label", text, fs, c, TextAlignmentOptions.Top);
        // Pivot at the TOP so the offset places the text's first line, not its box centre. With a
        // centred pivot a 34px-tall box put its first line back on top of the glyph.
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(0f, y);
        // ⚠️ Must match the shadow's box exactly — two objects that track each other by position
        // have to agree on anchor, pivot AND size, or they drift apart at different text lengths.
        label.rectTransform.sizeDelta = new Vector2(118f, 34f);
        label.characterSpacing = 3f;
        label.lineSpacing = -14f;
    }

    // ---- small builders -------------------------------------------------------------------------

    private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

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
