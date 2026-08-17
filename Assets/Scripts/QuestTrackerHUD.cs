using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The live quest tracker — the contracts you're currently carrying, top-right, over gameplay.
//
// THEME: Bulletin, the same material as QuestBoardScreen, because these ARE the slips off that
// board. You took a contract down off the wall and it is in your pocket, so it should be the same
// piece of paper: pale sheet, brass tack, ink text, wax seal when it's done. Nothing else in the
// game's HUD is made of paper, so a glance at the corner of the screen tells you what these are
// before you've read a word.
//
// ⚠️ IT IS A HUD, SO IT IS DELIBERATELY QUIETER THAN THE BOARD. The Loadout theme's rule applies:
// a permanent overlay cannot compete with the game behind it the way a modal panel can. So these are
// narrow strips rather than full slips, the paper is held under full opacity, the sway is about a
// third of the board's, and there is no grain, no fold and no perforation — all of that detail is
// legible at 356px and is just noise at 300.
//
// House pattern: fully procedural. It ignores the old `questRowContainer` / `questRowPrefab`
// Inspector fields (kept only so an existing scene object doesn't error), builds its own rows, and
// needs no prefab. QuestRowPrefab.prefab and QuestTrackerRow.cs were deleted with this rewrite.
public class QuestTrackerHUD : MonoBehaviour
{
    private static readonly FlatUI.Theme T = FlatUI.Bulletin;
    private static readonly Color WAX = new Color(0.596f, 0.135f, 0.125f, 1f);

    // Layout, in canvas reference units. Anchored to the TOP-RIGHT edge, because with the canvas
    // width varying by aspect ratio anything at a screen edge must be anchored to that edge.
    private const float ROW_W = 300f;
    private const float ROW_H = 104f;   // room for the requirement line under the title
    private const float ROW_GAP = 10f;
    private const float MARGIN_X = 26f;
    // Pinned right to the top edge. The relic bar sits top-CENTRE and the tracker is top-RIGHT, so
    // there is nothing up here to clear — the old 150 inset just left the corner looking unused.
    private const float MARGIN_Y = 24f;

    // The pin sits near the TOP-LEFT corner, and everything rotates about it — a strip pinned by one
    // corner, rather than the board's slips which hang from a tack at their top centre. Same idea,
    // different object: a note you shoved onto a nail, not a poster somebody hung straight.
    private static readonly Vector2 PIN = new Vector2(0.10f, 0.86f);

    private class Row
    {
        public QuestSystem.ActiveQuest quest;
        public RectTransform rt, shadow;
        public CanvasGroup cg;
        public TextMeshProUGUI title, count, info;
        public Image fill, edgeFlag, seal;
        public string requirement;   // the quest's own description, shown on the info line
        public float baseAngle, swaySpeed, swayPhase, swayAmp;
        public float fillShown, fillTarget;
        public float jolt;          // decaying shake, driven on progress and on a break
        public bool retiring;
    }

    private readonly List<Row> rows = new List<Row>();
    private TMP_FontAsset font;
    private RectTransform root;
    private RectTransform slipLayer;

    // Legacy Inspector fields. The scene's QuestTracker object still has them wired; they are no
    // longer read. Left in place so the existing component doesn't lose its serialized data in a way
    // that looks like corruption — delete them once the scene object has been cleaned up.
    [SerializeField, HideInInspector] private Transform questRowContainer;
    [SerializeField, HideInInspector] private GameObject questRowPrefab;

    private void Start()
    {
        font = FlatUI.UIFont();
        root = GetComponent<RectTransform>();

        // Anchor the tracker itself to the top-right corner and give it a real size, whatever the
        // scene had it set to. It used to rely on a hand-placed container.
        root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-MARGIN_X, -MARGIN_Y);
        root.sizeDelta = new Vector2(ROW_W + 40f, 600f);

        // The old prefab-based container would otherwise draw its leftovers behind the new rows.
        if (questRowContainer != null && questRowContainer != transform)
            questRowContainer.gameObject.SetActive(false);

        // ⚠️ THE SCENE OBJECT STILL CARRIES A VerticalLayoutGroup AND A ContentSizeFitter from the
        // old prefab-driven tracker, and they do not politely ignore procedural children — they
        // relaid every slip AND every slip's SHADOW as separate list items, which spaced the rows
        // at 84+4+84+4 = 176 instead of 94 and shoved them sideways. Rows here are pivoted at their
        // pin and rotated every frame, so a layout group can never own them.
        //
        // Both are killed rather than worked around, and the slips are then built into a dedicated
        // child layer that has no layout components at all — so this stays correct even if someone
        // re-adds one to the root later.
        LayoutGroup legacyLayout = GetComponent<LayoutGroup>();
        if (legacyLayout != null) legacyLayout.enabled = false;
        ContentSizeFitter legacyFitter = GetComponent<ContentSizeFitter>();
        if (legacyFitter != null) legacyFitter.enabled = false;

        GameObject layerGO = new GameObject("Slips", typeof(RectTransform));
        slipLayer = layerGO.GetComponent<RectTransform>();
        slipLayer.SetParent(transform, false);
        slipLayer.anchorMin = slipLayer.anchorMax = new Vector2(0.5f, 1f);
        slipLayer.pivot = new Vector2(0.5f, 1f);
        slipLayer.anchoredPosition = Vector2.zero;
        slipLayer.sizeDelta = new Vector2(ROW_W, 600f);

        if (QuestSystem.instance == null)
        {
            Debug.LogWarning("QuestTrackerHUD: QuestSystem.instance is null. Tracker will not function.");
            return;
        }

        QuestSystem.instance.OnQuestAccepted += AddRow;
        QuestSystem.instance.OnQuestProgress += UpdateRow;
        QuestSystem.instance.OnQuestCompleted += CompleteRow;

        foreach (QuestSystem.ActiveQuest quest in QuestSystem.instance.activeQuests)
            if (!quest.isCompleted) AddRow(quest);
    }

    private void OnDestroy()
    {
        if (QuestSystem.instance == null) return;
        QuestSystem.instance.OnQuestAccepted -= AddRow;
        QuestSystem.instance.OnQuestProgress -= UpdateRow;
        QuestSystem.instance.OnQuestCompleted -= CompleteRow;
    }

    // ---- construction --------------------------------------------------------------------------

    private void AddRow(QuestSystem.ActiveQuest quest)
    {
        if (quest == null || quest.data == null) return;
        if (Find(quest) != null) return;

        Row r = new Row();
        r.quest = quest;
        r.baseAngle = Random.Range(-1.6f, -0.4f);   // always tips the same way: hung off one nail
        r.swaySpeed = Random.Range(0.30f, 0.50f);
        r.swayPhase = Random.Range(0f, Mathf.PI * 2f);
        r.swayAmp = Random.Range(0.10f, 0.22f);

        // Shadow first so it sits behind. Light rakes from the left on this material, so it falls
        // to the lower right — the same direction as every shadow on the board.
        //
        // ⚠️ THE SHADOW MUST BE ANCHORED EXACTLY LIKE THE SLIP. AddImage anchors to the parent's
        // CENTRE while AddPoint anchors to its TOP, so the two shared an anchoredPosition but
        // measured it from different origins — every shadow rendered ~300px below its slip as a
        // free-floating black rectangle in the middle of the screen. Two objects that track each
        // other by position must agree on their anchors first.
        Image sh = AddImage(slipLayer, "RowShadow", FlatUI.Panel(4), new Color(0f, 0f, 0f, 0.40f), false);
        sh.type = Image.Type.Sliced;
        sh.rectTransform.anchorMin = sh.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        sh.rectTransform.pivot = PIN;
        sh.rectTransform.sizeDelta = new Vector2(ROW_W, ROW_H);
        r.shadow = sh.rectTransform;

        RectTransform rt = AddPoint(slipLayer, "Quest_" + quest.data.name, Vector2.zero,
                                    new Vector2(ROW_W, ROW_H));
        rt.pivot = PIN;
        r.rt = rt;
        r.cg = rt.gameObject.AddComponent<CanvasGroup>();

        Image paper = AddImage(rt, "Paper", FlatUI.Panel(4), T.SurfaceRaised, false);
        paper.type = Image.Type.Sliced;
        Stretch(paper.rectTransform);

        // A wax-red flag down the left edge, shown only while the oath is broken in the current
        // room. It is the one thing on this HUD that changes colour, so it can't be missed.
        r.edgeFlag = AddImage(rt, "BrokenFlag", FlatUI.Pixel(), WAX, false);
        r.edgeFlag.rectTransform.anchorMin = new Vector2(0f, 0f);
        r.edgeFlag.rectTransform.anchorMax = new Vector2(0f, 1f);
        r.edgeFlag.rectTransform.sizeDelta = new Vector2(5f, -12f);
        r.edgeFlag.rectTransform.anchoredPosition = new Vector2(4.5f, 0f);
        r.edgeFlag.gameObject.SetActive(false);

        r.title = AddText(rt, "Title", quest.data.questName, 18f, T.TextBright, TextAlignmentOptions.Left);
        r.title.fontStyle = FontStyles.Bold;
        r.title.enableAutoSizing = true;
        r.title.fontSizeMin = 13f;
        r.title.fontSizeMax = 18f;
        // ⚠️ The title is INDENTED clear of the tack. The tack sits at the row's top-left corner and
        // the title is left-aligned on the same line, so the default full-width box ran the first
        // two characters straight underneath it.
        r.title.rectTransform.sizeDelta = new Vector2(168f, 26f);
        r.title.rectTransform.anchoredPosition = new Vector2(-16f, 34f);

        r.count = AddText(rt, "Count", "", 17f, T.TextBody, TextAlignmentOptions.Right);
        r.count.fontStyle = FontStyles.Bold;
        r.count.rectTransform.sizeDelta = new Vector2(70f, 26f);
        r.count.rectTransform.anchoredPosition = new Vector2(110f, 34f);

        // WHAT THE CONTRACT ACTUALLY ASKS OF YOU. Taken straight from QuestData.description rather
        // than derived from the type, so it can never drift from what the board says and editing a
        // quest's text updates the HUD for free.
        //
        // It doubles as the break warning: when the oath is broken this room the line is REPLACED by
        // "BROKEN THIS ROOM" in wax red, because at that moment the requirement is not the thing the
        // player needs to read — and swapping keeps the strip one line shorter than carrying both.
        r.requirement = string.IsNullOrEmpty(quest.data.description) ? "" : quest.data.description;
        r.info = AddText(rt, "Info", r.requirement, 13f, T.TextMuted, TextAlignmentOptions.TopLeft);
        r.info.enableAutoSizing = true;
        r.info.fontSizeMin = 10f;
        r.info.fontSizeMax = 13f;
        r.info.enableWordWrapping = true;
        r.info.overflowMode = TextOverflowModes.Ellipsis;
        r.info.rectTransform.sizeDelta = new Vector2(268f, 34f);
        r.info.rectTransform.anchoredPosition = new Vector2(0f, 2f);

        Image track = AddImage(rt, "Track", FlatUI.Panel(2), new Color(0.42f, 0.37f, 0.30f, 0.45f), false);
        track.type = Image.Type.Sliced;
        track.rectTransform.sizeDelta = new Vector2(264f, 6f);
        track.rectTransform.anchoredPosition = new Vector2(0f, -38f);

        // Left-anchored so a fill of zero has no width, rather than collapsing about its centre.
        r.fill = AddImage(track.rectTransform, "Fill", FlatUI.Panel(2), T.TextBright, false);
        r.fill.type = Image.Type.Sliced;
        r.fill.rectTransform.anchorMin = new Vector2(0f, 0f);
        r.fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        r.fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        r.fill.rectTransform.anchoredPosition = Vector2.zero;
        r.fill.rectTransform.sizeDelta = Vector2.zero;

        // Built hidden; stamped on completion.
        r.seal = AddImage(rt, "Seal", FlatUI.WaxSeal(), WAX, false);
        r.seal.rectTransform.sizeDelta = new Vector2(62f, 62f);
        r.seal.rectTransform.anchoredPosition = new Vector2(104f, -4f);
        r.seal.rectTransform.localEulerAngles = new Vector3(0f, 0f, -11f);
        r.seal.gameObject.SetActive(false);

        Image tackShadow = AddImage(rt, "TackShadow", FlatUI.SoftGlow(), new Color(0f, 0f, 0f, 0.38f), false);
        tackShadow.rectTransform.sizeDelta = new Vector2(26f, 26f);
        tackShadow.rectTransform.anchoredPosition = PinLocal() + new Vector2(4f, -3f);

        Image tack = AddImage(rt, "Tack", FlatUI.PinTack(), new Color(0.82f, 0.63f, 0.30f, 1f), false);
        tack.rectTransform.sizeDelta = new Vector2(17f, 17f);
        tack.rectTransform.anchoredPosition = PinLocal();

        rows.Add(r);
        SyncRow(r, true);
        Layout();

        StartCoroutine(DropIn(r));
    }

    // The pivot expressed as an offset from the rect's centre, which is where children are placed.
    private static Vector2 PinLocal()
    {
        return new Vector2(ROW_W * (PIN.x - 0.5f), ROW_H * (PIN.y - 0.5f));
    }

    // ---- state ---------------------------------------------------------------------------------

    private Row Find(QuestSystem.ActiveQuest q)
    {
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].quest == q) return rows[i];
        return null;
    }

    private void UpdateRow(QuestSystem.ActiveQuest quest)
    {
        Row r = Find(quest);
        if (r == null) { AddRow(quest); return; }
        SyncRow(r, false);
        r.jolt = 1f;
    }

    private void SyncRow(Row r, bool immediate)
    {
        int target = Mathf.Max(1, r.quest.data.targetAmount);
        int current = Mathf.Clamp(r.quest.currentAmount, 0, r.quest.data.targetAmount);

        r.count.text = current + " / " + r.quest.data.targetAmount;
        r.fillTarget = (float)current / target;
        if (immediate) r.fillShown = r.fillTarget;
    }

    private void CompleteRow(QuestSystem.ActiveQuest quest)
    {
        Row r = Find(quest);
        if (r == null) return;
        rows.Remove(r);
        StartCoroutine(Retire(r));
    }

    // ---- life ----------------------------------------------------------------------------------

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;
        QuestSystem qs = QuestSystem.instance;

        for (int i = 0; i < rows.Count; i++)
        {
            Row r = rows[i];
            if (r.rt == null || r.retiring) continue;

            // Live oath state. This is the only place in the game that tells you an oath is already
            // lost for the room you are STANDING IN — the board can only ever tell you afterwards,
            // and finding out at the exit door would feel like the game hid it from you.
            bool broken = qs != null && qs.IsOathBroken(r.quest.data);
            if (r.edgeFlag.gameObject.activeSelf != broken)
            {
                r.edgeFlag.gameObject.SetActive(broken);
                r.title.color = broken ? WAX : T.TextBright;
                r.info.text = broken ? "BROKEN THIS ROOM" : r.requirement;
                r.info.color = broken ? WAX : T.TextMuted;
                if (broken) r.jolt = 1f;
            }

            // Fill eases toward its target, so a streak collapsing DRAINS visibly instead of
            // snapping to zero. Watching the bar run backwards is the punishment landing.
            r.fillShown = Mathf.MoveTowards(r.fillShown, r.fillTarget, dt * 1.6f);
            r.fill.rectTransform.sizeDelta = new Vector2(264f * r.fillShown, 0f);
            r.fill.color = broken ? WAX : T.TextBright;

            r.jolt = Mathf.Max(0f, r.jolt - dt * 3.2f);
            float shake = r.jolt * r.jolt * 2.6f * Mathf.Sin(now * 42f + r.swayPhase);

            float sway = Mathf.Sin(now * r.swaySpeed + r.swayPhase) * r.swayAmp;
            float angle = r.baseAngle + sway + shake;

            r.rt.localEulerAngles = new Vector3(0f, 0f, angle);
            if (r.shadow != null)
            {
                r.shadow.localEulerAngles = new Vector3(0f, 0f, angle);
                r.shadow.anchoredPosition = r.rt.anchoredPosition + new Vector2(5f, -4f);
            }
        }
    }

    // Rows are positioned by hand rather than by a LayoutGroup: they are rotated and pivoted at
    // their pin, and a layout group would fight both every frame.
    private void Layout()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            Vector2 at = RowRest(i);
            rows[i].rt.anchoredPosition = at;
            if (rows[i].shadow != null) rows[i].shadow.anchoredPosition = at + new Vector2(5f, -4f);
        }
    }

    // ⚠️ anchoredPosition places the PIVOT, not the rect's centre — and this pivot is up in the
    // top-left corner (that's the pin). Positioning rows at x = 0 therefore hung 90% of each strip
    // to the RIGHT of the anchor and pushed them 74px off the edge of the screen, cutting the
    // progress counts in half. This backs the pivot offset out so the strip itself lands where the
    // layout means it to.
    private static Vector2 RowRest(int index)
    {
        float cx = -(0.5f - PIN.x) * ROW_W;
        float cy = -(0.5f - PIN.y) * ROW_H;
        return new Vector2(cx, cy - ROW_H * 0.5f - index * (ROW_H + ROW_GAP));
    }

    private IEnumerator DropIn(Row r)
    {
        const float DUR = 0.26f;
        float t = 0f;
        Vector2 rest = r.rt.anchoredPosition;
        while (t < DUR)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / DUR);
            float e = 1f - Mathf.Pow(1f - n, 3f);
            r.cg.alpha = n;
            r.rt.anchoredPosition = rest + new Vector2(Mathf.Lerp(46f, 0f, e), 0f);
            yield return null;
        }
        r.cg.alpha = 1f;
        r.rt.anchoredPosition = rest;
        r.jolt = 0.7f;
    }

    // Completion: the seal is stamped, then the slip comes OFF the pin and falls away. A contract
    // that simply faded out would read as the tracker forgetting about it; this reads as the job
    // being finished and the paper being taken down.
    private IEnumerator Retire(Row r)
    {
        r.retiring = true;

        r.fill.rectTransform.sizeDelta = new Vector2(264f, 0f);
        r.fill.color = WAX;
        r.count.color = WAX;
        r.edgeFlag.gameObject.SetActive(false);
        r.title.color = T.TextBright;
        r.info.text = r.requirement;
        r.info.color = T.TextMuted;

        r.seal.gameObject.SetActive(true);
        float t = 0f;
        const float STAMP = 0.18f;
        while (t < STAMP)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / STAMP);
            float k = Mathf.Lerp(2.2f, 1f, n * n);
            r.seal.rectTransform.localScale = new Vector3(k, k, 1f);
            r.seal.color = new Color(WAX.r, WAX.g, WAX.b, n);
            yield return null;
        }
        r.seal.rectTransform.localScale = Vector3.one;
        SfxManager.PlayOn(GetOrMakeAudio(), ProcSfx.WaxStamp, 0.55f);

        // Beat, so the completed state is legible before the paper leaves.
        float hold = 0f;
        while (hold < 0.75f) { hold += Time.unscaledDeltaTime; yield return null; }

        // Off the pin: it swings out and drops.
        Vector2 from = r.rt.anchoredPosition;
        float fallT = 0f;
        const float FALL = 0.5f;
        while (fallT < FALL)
        {
            fallT += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(fallT / FALL);
            r.rt.anchoredPosition = from + new Vector2(26f * n, -260f * n * n);
            r.rt.localEulerAngles = new Vector3(0f, 0f, r.baseAngle - 26f * n);
            r.cg.alpha = 1f - n;
            if (r.shadow != null)
            {
                r.shadow.anchoredPosition = r.rt.anchoredPosition + new Vector2(5f, -4f);
                r.shadow.localEulerAngles = r.rt.localEulerAngles;
            }
            yield return null;
        }

        if (r.shadow != null) Destroy(r.shadow.gameObject);
        if (r.rt != null) Destroy(r.rt.gameObject);
        Layout();
    }

    private AudioSource audioSource;
    private AudioSource GetOrMakeAudio()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        return audioSource;
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private RectTransform AddPoint(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
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
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
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
