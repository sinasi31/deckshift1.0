using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// The run map — press M. A chart of the whole act, so the player plans a ROUTE rather than picking
// one door at a time.
//
// THEME: Verdigris (FlatUI.Verdigris), and it is deliberately the odd one out. Every other screen
// dresses a PLACE and renders it with light on material — the Forge lit from below, Blompo lit from
// above, the Marketplace by lamplight. A map is not a place: you are not standing in it, you are
// reading it. So this screen inverts the lighting model entirely — flat, matte, unlit, with no glow
// source and NO PARTICLE FIELD, because a chart has no air. What moves instead is the INFORMATION:
// branches you can take pulse, and the route you have walked shimmers along its length.
//
// The material is oxidised copper, Act 1's own chemistry, and it pays off the accent: the travelled
// route is drawn in warm BARE copper, as if the patina had been worn back to metal by walking it.
// Every other theme's accent is light being added; this one's is surface being worn away.
//
// House pattern: entirely procedural, self-instantiating under the root Canvas, no prefab and no
// art files. Same shape as ScrapForgeScreen and BlompoScreen.
public class RunMapScreen : MonoBehaviour
{
    private static RunMapScreen instance;

    private const float WIN_W = 1100f;
    private const float WIN_H = 820f;
    private const float CHAMFER = 10f;

    // Map drawing area, inset inside the window. The insets are generous on purpose: nodes are
    // positioned by their CENTRE, so the boss glyph (76px) and the labels hanging under each
    // reachable node both need room or they collide with the title rule and the footer.
    private const float AREA_TOP = 132f;
    private const float AREA_BOTTOM = 106f;
    private const float AREA_SIDE = 62f;

    private const float EDGE_THICKNESS = 3f;

    private RectTransform window, area;
    private CanvasGroup group;
    private TMP_FontAsset font;
    private TextMeshProUGUI footer;

    private bool isOpen;
    private GameObject cachedHud;
    private bool hudWasActive;
    private GameState prevState;

    // Rebuilt every Refresh. Kept so the pulse/shimmer tick doesn't have to search the hierarchy.
    private readonly List<Image> availableMarks = new List<Image>();
    private readonly List<Image> travelledEdges = new List<Image>();
    private readonly List<GameObject> spawned = new List<GameObject>();

    private static readonly FlatUI.Theme T = FlatUI.Verdigris;

    // ---- entry points ---------------------------------------------------------------------------

    public static void Toggle()
    {
        EnsureInstance();
        if (instance == null) return;
        if (instance.isOpen) instance.Hide();
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

        Image backdrop = AddImage(transform, "Backdrop", null, T.Backdrop, true);
        Stretch(backdrop.rectTransform);
        Button backBtn = backdrop.gameObject.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(Hide);

        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIN_W, WIN_H));
        Image winBg = window.gameObject.AddComponent<Image>();
        winBg.sprite = FlatUI.Panel((int)CHAMFER);
        winBg.type = Image.Type.Sliced;
        winBg.color = T.Surface;
        winBg.raycastTarget = true;

        Image winEdge = AddImage(window, "Edge", FlatUI.Outline((int)CHAMFER, 2), T.Border, false);
        // MUST be Sliced. AddImage leaves the default Simple, which stretches this 26px outline
        // sprite across the whole 1040x780 window — it renders as an enormous soft octagon glow
        // hanging outside the panel, which is exactly what the first build did.
        winEdge.type = Image.Type.Sliced;
        Stretch(winEdge.rectTransform);
        FlatUI.ApplySliceThickness(winEdge, 2f);

        // The one lit element allowed: a hairline along the top lip, so the plate still reads as a
        // physical sheet rather than a flat rectangle. NOT a glow — it has no falloff into the panel.
        Image lip = AddImage(window, "Lip", FlatUI.FadedRule(), T.EdgeLight, false);
        lip.rectTransform.anchorMin = new Vector2(0f, 1f);
        lip.rectTransform.anchorMax = new Vector2(1f, 1f);
        lip.rectTransform.pivot = new Vector2(0.5f, 1f);
        lip.rectTransform.anchoredPosition = new Vector2(0f, -3f);
        lip.rectTransform.sizeDelta = new Vector2(-56f, 1f);

        TextMeshProUGUI title = AddText(window, "Title", "THE OXIDATION DISTRICT", 30f, T.TextBright,
            TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -26f);
        title.rectTransform.sizeDelta = new Vector2(-80f, 38f);
        title.characterSpacing = 8f;

        TextMeshProUGUI sub = AddText(window, "Sub", "CHOOSE YOUR ROUTE", 15f, T.TextMuted,
            TextAlignmentOptions.Center);
        sub.rectTransform.anchorMin = new Vector2(0f, 1f);
        sub.rectTransform.anchorMax = new Vector2(1f, 1f);
        sub.rectTransform.pivot = new Vector2(0.5f, 1f);
        sub.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        sub.rectTransform.sizeDelta = new Vector2(-80f, 22f);
        sub.characterSpacing = 6f;

        Image rule = AddImage(window, "TitleRule", FlatUI.FadedRule(), T.BorderSoft, false);
        rule.rectTransform.anchorMin = new Vector2(0f, 1f);
        rule.rectTransform.anchorMax = new Vector2(1f, 1f);
        rule.rectTransform.pivot = new Vector2(0.5f, 1f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, -88f);
        rule.rectTransform.sizeDelta = new Vector2(-120f, 1f);

        // The chart itself.
        area = AddPoint(window, "Area", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        area.anchorMin = new Vector2(0f, 0f);
        area.anchorMax = new Vector2(1f, 1f);
        area.offsetMin = new Vector2(AREA_SIDE, AREA_BOTTOM);
        area.offsetMax = new Vector2(-AREA_SIDE, -AREA_TOP);

        footer = AddText(window, "Footer", "", 14f, T.TextMuted, TextAlignmentOptions.Center);
        footer.rectTransform.anchorMin = new Vector2(0f, 0f);
        footer.rectTransform.anchorMax = new Vector2(1f, 0f);
        footer.rectTransform.pivot = new Vector2(0.5f, 0f);
        footer.rectTransform.anchoredPosition = new Vector2(0f, 22f);
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

        if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); return; }

        TickMotion();
    }

    // Motion lives in the information, not the atmosphere — see the header. Branches you can take
    // breathe; the route already walked carries a slow travelling shimmer.
    private void TickMotion()
    {
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.0f);

        for (int i = 0; i < availableMarks.Count; i++)
        {
            Image img = availableMarks[i];
            if (img == null) continue;
            Color c = img.color;
            c.a = Mathf.Lerp(0.35f, 1f, pulse);
            img.color = c;
        }

        for (int i = 0; i < travelledEdges.Count; i++)
        {
            Image img = travelledEdges[i];
            if (img == null) continue;
            // Offset per edge so the shimmer runs ALONG the route instead of the whole path
            // flashing at once, which reads as a single object rather than a path.
            float phase = Time.unscaledTime * 1.9f - i * 0.55f;
            float k = 0.5f + 0.5f * Mathf.Sin(phase);
            Color c = T.Accent;
            c.a = Mathf.Lerp(0.55f, 0.95f, k);
            img.color = c;
        }
    }

    // ---- drawing --------------------------------------------------------------------------------

    private void Refresh()
    {
        // Deactivate before Destroy: Unity's Destroy is deferred to end of frame, so the old chart
        // would otherwise still render on top of the new one for a frame.
        foreach (GameObject go in spawned)
        {
            if (go == null) continue;
            go.SetActive(false);
            Destroy(go);
        }
        spawned.Clear();
        availableMarks.Clear();
        travelledEdges.Clear();

        RunMapManager mgr = RunMapManager.instance;
        RunMap map = mgr != null ? mgr.Map : null;

        if (map == null || map.nodes.Count == 0)
        {
            SetFooter("No act in progress.");
            return;
        }

        Dictionary<int, Vector2> pos = LayOutNodes(map);

        // Edges first so nodes sit on top of them.
        foreach (MapNode n in map.nodes)
        {
            foreach (int id in n.next)
            {
                MapNode m = map.Get(id);
                if (m == null) continue;
                DrawEdge(map, mgr, n, m, pos[n.id], pos[m.id]);
            }
        }

        foreach (MapNode n in map.nodes) DrawNode(map, mgr, n, pos[n.id]);

        int floorsLeft = (map.floors - 1) - (map.Current != null ? map.Current.floor : 0);
        if (mgr != null && mgr.HasChosenNext)
        {
            MapNode chosen = map.Get(mgr.ChosenNextId);
            SetFooter($"NEXT: {MapGlyphs.LabelFor(chosen.type)}{RechargeSuffix(chosen)}   ·   {floorsLeft} TO THE BOSS");
        }
        else if (map.AvailableNext().Count > 0)
        {
            SetFooter($"PICK A BRANCH   ·   {floorsLeft} TO THE BOSS");
        }
        else
        {
            SetFooter("ACT COMPLETE");
        }
    }

    private string RechargeSuffix(MapNode n)
    {
        return n.recharge == RechargeType.None ? "" : $" + {MapGlyphs.LabelFor(n.recharge)}";
    }

    private void SetFooter(string s)
    {
        if (footer != null) footer.text = s;
    }

    // Positions every node in the chart area. Columns are spread across the full width and floors
    // stack bottom-to-top, with a small DETERMINISTIC jitter per node so the act reads as a drawn
    // route rather than a spreadsheet — seeded by node id so it never moves between openings.
    private Dictionary<int, Vector2> LayOutNodes(RunMap map)
    {
        Dictionary<int, Vector2> pos = new Dictionary<int, Vector2>();

        Rect r = area.rect;
        float w = r.width, h = r.height;

        int maxCol = 0;
        foreach (MapNode n in map.nodes) if (n.column > maxCol) maxCol = n.column;

        float colStep = maxCol > 0 ? w / (maxCol + 1) : w;
        float floorStep = map.floors > 1 ? h / (map.floors - 1) : h;

        foreach (MapNode n in map.nodes)
        {
            float x = maxCol > 0 ? -w * 0.5f + colStep * (n.column + 0.5f) : 0f;
            float y = -h * 0.5f + floorStep * n.floor;

            // Start and Boss are single nodes and belong dead centre — they are the act's spine.
            if (n.type == MapNodeType.Start || n.type == MapNodeType.Boss)
            {
                pos[n.id] = new Vector2(0f, y);
                continue;
            }

            System.Random rng = new System.Random(n.id * 7919 + map.seed);
            // Horizontal jitter is kept tighter than vertical: sideways wander is what pushes two
            // nodes on the same floor close enough for their labels to collide, while vertical
            // wander costs nothing.
            float jx = ((float)rng.NextDouble() - 0.5f) * colStep * 0.20f;
            float jy = ((float)rng.NextDouble() - 0.5f) * floorStep * 0.28f;

            pos[n.id] = new Vector2(x + jx, y + jy);
        }

        return pos;
    }

    // A line between two nodes, drawn as a thin rotated rect.
    private void DrawEdge(RunMap map, RunMapManager mgr, MapNode from, MapNode to, Vector2 a, Vector2 b)
    {
        bool travelled = map.visited.Contains(from.id) && map.visited.Contains(to.id);
        bool open = map.currentNodeId == from.id && map.CanTravelTo(to.id);
        bool committed = open && mgr != null && mgr.ChosenNextId == to.id;

        Color c;
        float thickness = EDGE_THICKNESS;

        if (travelled) { c = T.Accent; thickness = EDGE_THICKNESS + 1.5f; }
        else if (committed) { c = T.Accent; thickness = EDGE_THICKNESS + 1f; }
        else if (open) { c = T.TextBody; }
        else { c = T.BorderSoft; thickness = EDGE_THICKNESS - 1f; }

        Vector2 delta = b - a;
        float len = delta.magnitude;
        if (len < 0.01f) return;

        GameObject go = new GameObject($"Edge{from.id}_{to.id}", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(area, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = a + delta * 0.5f;
        rt.sizeDelta = new Vector2(len, thickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        Image img = go.AddComponent<Image>();
        img.sprite = FlatUI.Pixel();
        img.color = c;
        img.raycastTarget = false;

        spawned.Add(go);
        if (travelled) travelledEdges.Add(img);
    }

    private void DrawNode(RunMap map, RunMapManager mgr, MapNode n, Vector2 p)
    {
        bool isCurrent = map.currentNodeId == n.id;
        bool visited = map.visited.Contains(n.id);
        bool reachable = map.CanTravelTo(n.id);
        bool committed = mgr != null && mgr.ChosenNextId == n.id;

        float size = MapGlyphs.SizeFor(n.type);

        GameObject go = new GameObject($"Node{n.id}", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(area, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = p;
        rt.sizeDelta = new Vector2(size + 26f, size + 26f);
        spawned.Add(go);

        // A pulsing halo marks every branch the player may actually take. It is the only thing on
        // the chart that moves on its own, which is what makes "where can I go" answerable at a
        // glance without reading anything.
        if (reachable)
        {
            Image halo = AddImage(rt, "Halo", MapGlyphs.Ring(), T.TextBright, false);
            halo.rectTransform.sizeDelta = new Vector2(size + 22f, size + 22f);
            availableMarks.Add(halo);
        }

        if (committed)
        {
            Image lockRing = AddImage(rt, "Committed", MapGlyphs.Ring(), T.Accent, false);
            lockRing.rectTransform.sizeDelta = new Vector2(size + 34f, size + 34f);
        }

        Color tint;
        if (isCurrent) tint = T.Accent;
        else if (visited) tint = new Color(T.Accent.r, T.Accent.g, T.Accent.b, 0.55f);
        else if (reachable) tint = T.TextBright;
        else tint = T.TextDisabled;

        Image glyph = AddImage(rt, "Glyph", MapGlyphs.ForNode(n.type), tint, false);
        glyph.rectTransform.sizeDelta = new Vector2(size, size);

        // Recharge attachment: a small badge clipped to the node's upper right. None of these
        // appear until their room prefab is assigned on LevelManager — the map refuses to draw a
        // room it cannot spawn.
        if (n.recharge != RechargeType.None)
        {
            float badge = 20f;
            RectTransform bt = AddPoint(rt, "Recharge", new Vector2(0.5f, 0.5f),
                new Vector2(size * 0.48f, size * 0.48f), new Vector2(badge + 8f, badge + 8f));

            Image disc = bt.gameObject.AddComponent<Image>();
            disc.sprite = FlatUI.Panel(5);
            disc.type = Image.Type.Sliced;
            disc.color = T.SurfaceRaised;
            disc.raycastTarget = false;

            Image mark = AddImage(bt, "Mark", MapGlyphs.ForRecharge(n.recharge),
                reachable || visited ? T.TextBright : T.TextMuted, false);
            mark.rectTransform.sizeDelta = new Vector2(badge, badge);
        }

        // Only reachable nodes are clickable. Choosing is a commitment, not navigation, so the
        // click sets the branch and the run travels there when the player reaches the exit door.
        if (reachable)
        {
            Button btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            Image hit = AddImage(rt, "Hit", FlatUI.Pixel(), new Color(0f, 0f, 0f, 0f), true);
            hit.rectTransform.sizeDelta = new Vector2(size + 26f, size + 26f);
            btn.targetGraphic = hit;

            int id = n.id;
            btn.onClick.AddListener(() =>
            {
                if (RunMapManager.instance != null && RunMapManager.instance.ChooseNext(id)) Refresh();
            });

            AddHoverLabel(rt, n, size);
        }
        else if (isCurrent)
        {
            // BELOW the node. Above it collides with the hanging label of whichever branch sits
            // directly overhead, which on the hub row is almost always one of them.
            TextMeshProUGUI here = AddText(rt, "Here", "YOU ARE HERE", 11f, T.Accent, TextAlignmentOptions.Center);
            here.rectTransform.pivot = new Vector2(0.5f, 1f);
            here.rectTransform.anchoredPosition = new Vector2(0f, -(size * 0.5f + 10f));
            here.rectTransform.sizeDelta = new Vector2(160f, 16f);
            here.characterSpacing = 3f;
        }
    }

    // The node's promise, spelled out under it. Present on every reachable branch rather than on
    // hover only: the whole point of the map is comparing branches at a glance, and a label you
    // have to hover to read cannot be compared with anything.
    private void AddHoverLabel(RectTransform parent, MapNode n, float size)
    {
        string text = MapGlyphs.LabelFor(n.type);
        if (n.recharge != RechargeType.None) text += "\n+ " + MapGlyphs.LabelFor(n.recharge);

        TextMeshProUGUI label = AddText(parent, "Label", text, 12f, T.TextBody, TextAlignmentOptions.Top);
        // Pivot at the TOP so the offset positions the text's first line, not its box centre. With
        // a centred pivot a 34px-tall box put its first line back on top of the glyph.
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -(size * 0.5f + 9f));
        // Narrower than it looks like it needs to be. Neighbouring nodes on a floor sit about one
        // column apart MINUS their jitter, so a 150px label ran into its neighbour's on any floor
        // that filled up.
        label.rectTransform.sizeDelta = new Vector2(118f, 34f);
        label.characterSpacing = 3f;
        label.lineSpacing = -14f;
    }

    // ---- small builders -------------------------------------------------------------------------

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
