using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Where the fine 1-unit tick marks are drawn inside a segmented bar.
public enum BarPipMode
{
    None,           // never
    ActiveSegment,  // the segment currently draining — when you're low, that IS your last segment
    FinalReserve,   // always the first segment (the last 10 you own), as a permanent reserve marking
    AllSegments     // every segment, all the time
}

public enum BarNumberPlacement { CenteredOnBar, RightOfBar, LeftOfBar }

// The SHAPE of a HUD bar. Health and Shift share a single one of these, so they are physically
// incapable of drifting to different lengths — they are meant to read as the same object in two
// colours. Keeping a private copy of the size on each bar is exactly how they ended up 144px and
// 262px once already.
[System.Serializable]
public class BarGeometry
{
    [Header("Size (panel-local pixels)")]
    public float width = 262f;
    public float height = 26f;
    [Tooltip("Gold frame thickness in pixels. Rendered at exactly this size regardless of bar size.")]
    public float frameThickness = 4f;

    [Header("Number")]
    public BarNumberPlacement numberPlacement = BarNumberPlacement.RightOfBar;
    public float numberSize = 23f;
    [Tooltip("Font size of the \" / max\" part. 0 = same as numberSize.")]
    public float maxNumberSize = 13f;
    [Tooltip("Distance from the bar edge when the number sits beside it.")]
    public float numberGap = 14f;
}

// The per-resource part: colour, and whether the bar is segmented. ResourcePanelHUD holds two of
// these (health + Shift) alongside one shared BarGeometry.
[System.Serializable]
public class BarStyle
{
    [Header("Segments")]
    [Tooltip("Off = one continuous bar (health). On = divided into cells of unitsPerSegment (Shift).")]
    public bool segmented = false;
    [Tooltip("How much of the resource one segment holds. Shift uses 10 per segment.")]
    public int unitsPerSegment = 10;
    [Tooltip("Gap between segments in pixels — the dark track shows through, so the cells read as separate.")]
    public float segmentGap = 3f;
    [Tooltip("Fine 1-unit tick marks. ActiveSegment = the cell currently draining (when you're low that's your last one). FinalReserve = always the leftmost cell.")]
    public BarPipMode pipMode = BarPipMode.ActiveSegment;

    [Header("Colour")]
    public Color fill = new Color(0.49f, 0.42f, 0.93f);
    [Tooltip("Unlit colour of an EMPTY cell. Segments must stay visible when drained or you can't tell a max of 40 from a max of 100 — keep this readable against the track.")]
    public Color empty = new Color(0.17f, 0.15f, 0.24f);
    [Tooltip("Pale trail left behind for a moment when the value drops, so you can see the size of the spend / hit.")]
    public Color chip = new Color(0.80f, 0.76f, 1f);
    [Tooltip("Fill colour once at or below lowThreshold. The bar pulses between this and the normal fill.")]
    public Color low = new Color(1f, 0.40f, 0.52f);
    [Tooltip("Value at or below which the bar warns. For Shift this is one segment (10).")]
    public float lowThreshold = 10f;
    public bool warnWhenLow = true;

    [Header("Number")]
    public bool showMax = true;
    public Color numberColor = Color.white;
    public Color maxNumberColor = new Color(0.72f, 0.70f, 0.80f);
}

// One procedural HUD resource bar: a dark recessed track inside a thin outline, built from FlatUI
// in the LOADOUT theme. House pattern — every part is generated in code, no prefabs, no art files.
//
// Loadout is deliberately shared with the relic bar rather than given its own material. Both are
// PERMANENT overlays that are on screen at the same time, and two co-visible HUD elements in
// different materials would read as a mistake. The "each screen gets its own material" rule is
// about places you visit one at a time, not about the persistent HUD.
//
// Same governing principle as the relic sockets: the chrome recedes, because on a bar the COLOUR
// is the information — red is health, blue is Shift.
//
// SEGMENTED MODE (Shift): the bar is a FIXED length divided into cells of `unitsPerSegment`.
// Raising maxShift adds cells inside the same length instead of making the bar longer, so the HUD
// never reflows. A max that isn't a whole number of segments leaves a proportionally NARROWER runt
// cell at the end — the value-to-pixel mapping stays linear, so a full-looking cell is always worth
// a full segment and the bar never lies about how much is left.
//
// This is a plain class, not a MonoBehaviour: ResourcePanelHUD owns the instances and drives them.
public class ResourceBarUI
{
    const float CHIP_HOLD = 0.22f;   // seconds the pale trail lingers before it starts catching up
    const float CHIP_SPEED = 0.85f;  // fraction of the full bar the trail closes per second
    const float PULSE_RATE = 5.0f;   // radians/sec of the low-resource pulse

    private readonly BarGeometry geo;
    private readonly BarStyle style;
    private readonly RectTransform root;

    private readonly List<Cell> cells = new List<Cell>();
    private TextMeshProUGUI numberText, numberShadow;

    private float current, max = 1f;
    private float chipValue, chipHold;
    private float pulseT;
    private float builtMax = float.NaN;
    private int shownCur = -1, shownMax = -1;

    public RectTransform Root => root;

    private class Cell
    {
        public float start, capacity;
        public Image chip, fill;
        public GameObject pips;
    }

    public ResourceBarUI(RectTransform parent, string name, BarGeometry geo, BarStyle style, TMP_FontAsset font)
    {
        this.geo = geo;
        this.style = style;

        var go = new GameObject(name, typeof(RectTransform));
        root = go.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(geo.width, geo.height);

        BuildNumber(font);
        // The chrome is laid out on the first SetValue — cell widths depend on max, so building
        // here with a placeholder would just be thrown away.
    }

    // ---------------------------------------------------------------- public API

    public void SetPosition(Vector2 topLeft)
    {
        root.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
    }

    public void SetValue(float value, float maximum)
    {
        maximum = Mathf.Max(1f, maximum);
        if (!Mathf.Approximately(maximum, builtMax)) Rebuild(maximum);

        value = Mathf.Clamp(value, 0f, maximum);
        if (value < current) chipHold = CHIP_HOLD;   // spent / took a hit: hold the trail, then drain it
        else chipValue = Mathf.Max(chipValue, value); // gained: no trail, the chip just tracks up

        current = value;
        max = maximum;
        ApplyFills();
        ApplyText();
    }

    // Driven from ResourcePanelHUD.Update with unscaled time so the trail and pulse keep animating
    // through HitStop freezes and slow-mo.
    public void Tick(float dt)
    {
        bool dirty = false;

        if (chipValue > current)
        {
            if (chipHold > 0f) chipHold -= dt;
            else
            {
                chipValue = Mathf.MoveTowards(chipValue, current, max * CHIP_SPEED * dt);
                dirty = true;
            }
        }

        if (style.warnWhenLow && current > 0f && current <= style.lowThreshold)
        {
            pulseT += dt * PULSE_RATE;
            dirty = true;
        }
        else if (pulseT != 0f)
        {
            pulseT = 0f;
            dirty = true;
        }

        if (dirty) ApplyFills();
    }

    // ---------------------------------------------------------------- build

    // Lays the cells out for a given max. Called on construction and whenever max changes
    // (IncreaseMaxShift, a max-HP relic), because the cell widths are derived from it.
    private void Rebuild(float maximum)
    {
        builtMax = maximum;
        max = Mathf.Max(1f, maximum);

        // Wipe the previous chrome — a rebuild replaces the whole track, cells included. The number
        // texts are left alone (they're rebuilt only if the style changes).
        cells.Clear();
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (child.name == "Shadow" || child.name == "Track" || child.name == "Frame" || child.name == "Cells")
            {
                child.SetParent(null, false);   // unparent first: Destroy is deferred to end of frame
                Object.Destroy(child.gameObject);
            }
        }

        float ft = geo.frameThickness;
        float innerW = Mathf.Max(1f, geo.width - ft * 2f);
        float innerH = Mathf.Max(1f, geo.height - ft * 2f);

        // 0. drop shadow — the HUD has no panel behind it any more, so each bar lifts itself off
        //    whatever the level happens to be rendering underneath.
        Image shadow = MakeImage(root, "Shadow", RelicUISprites.SoftShadow(), new Color(0f, 0f, 0f, 0.55f));
        var srt = shadow.rectTransform;
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.pivot = new Vector2(0.5f, 0.5f);
        srt.offsetMin = new Vector2(-7f, -11f);
        srt.offsetMax = new Vector2(7f, 3f);
        shadow.type = Image.Type.Sliced;
        shadow.pixelsPerUnitMultiplier = 1f;
        shadow.transform.SetAsFirstSibling();

        // 1. recessed track (behind everything; visible through the segment gaps)
        Image track = MakeImage(root, "Track", FlatUI.Panel(5), FlatUI.Loadout.Surface);
        Stretch(track.rectTransform);
        track.type = Image.Type.Sliced;
        FlatUI.ApplySliceThickness(track, Mathf.Max(2f, ft * 0.8f));
        track.transform.SetSiblingIndex(1);

        // 2. the segment cells
        var cellsRoot = new GameObject("Cells", typeof(RectTransform)).GetComponent<RectTransform>();
        cellsRoot.SetParent(root, false);
        Stretch(cellsRoot);
        cellsRoot.SetSiblingIndex(2);

        int segCount = style.segmented
            ? Mathf.Max(1, Mathf.CeilToInt(max / Mathf.Max(1, style.unitsPerSegment)))
            : 1;
        float gap = style.segmented ? style.segmentGap : 0f;
        float usable = Mathf.Max(1f, innerW - gap * (segCount - 1));

        float x = ft;
        for (int i = 0; i < segCount; i++)
        {
            float start = style.segmented ? i * style.unitsPerSegment : 0f;
            float capacity = style.segmented
                ? Mathf.Min(style.unitsPerSegment, max - start)
                : max;
            float w = usable * (capacity / max);

            var holder = new GameObject("Segment" + i, typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(cellsRoot, false);
            holder.anchorMin = holder.anchorMax = new Vector2(0f, 1f);
            holder.pivot = new Vector2(0f, 1f);
            holder.sizeDelta = new Vector2(w, innerH);
            holder.anchoredPosition = new Vector2(x, -ft);

            var cell = new Cell { start = start, capacity = Mathf.Max(0.0001f, capacity) };

            // Unlit socket: the same cell shape as the fill, just dark. Without it a drained
            // segment is indistinguishable from the track and the bar stops showing your capacity.
            //
            // Cells are FLAT colour blocks now (the old ones carried a baked bevel). On a bar the
            // colour IS the information — red health, blue Shift — so any shading on top of it is
            // noise competing with the one thing the bar has to communicate.
            Image socket = MakeImage(holder, "Socket", FlatUI.Pixel(), style.empty);
            Stretch(socket.rectTransform);

            cell.chip = MakeImage(holder, "Chip", FlatUI.Pixel(), style.chip);
            Stretch(cell.chip.rectTransform);
            AsFill(cell.chip);

            cell.fill = MakeImage(holder, "Fill", FlatUI.Pixel(), style.fill);
            Stretch(cell.fill.rectTransform);
            AsFill(cell.fill);

            cell.pips = BuildPips(holder, w, innerH, Mathf.RoundToInt(capacity));
            cells.Add(cell);

            x += w + gap;
        }

        // 3. outline, drawn over the cells so they sit INSIDE it.
        //
        // Rendered at 2px regardless of geo.frameThickness, which stays the LAYOUT inset. The old
        // gold frame was drawn at the full 4px, which on a 26px bar was a sixth of the whole thing
        // in chrome. Decoupling them leaves a couple of pixels of dark track showing between the
        // outline and the cells, which reads as recessed rather than as a heavy border.
        Image frame = MakeImage(root, "Frame", FlatUI.Outline(5, 2), FlatUI.Loadout.Border);
        Stretch(frame.rectTransform);
        frame.type = Image.Type.Sliced;
        frame.fillCenter = false;
        FlatUI.ApplySliceThickness(frame, 2f);
        frame.transform.SetSiblingIndex(3);

        // keep the number on top of the chrome
        if (numberShadow != null) numberShadow.transform.SetAsLastSibling();
        if (numberText != null) numberText.transform.SetAsLastSibling();

        chipValue = Mathf.Min(chipValue, max);
        ApplyFills();
        ApplyText();
    }

    // `capacity` hairlines' worth of subdivision — capacity-1 lines. A runt cell gets only as many
    // ticks as it actually holds, so a tick is always worth exactly 1 unit.
    private GameObject BuildPips(RectTransform parent, float w, float h, int capacity)
    {
        var go = new GameObject("Pips", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt);

        if (style.pipMode != BarPipMode.None && capacity > 1)
        {
            for (int k = 1; k < capacity; k++)
            {
                Image line = MakeImage(rt, "Pip" + k, FlatUI.Pixel(), new Color(0f, 0f, 0f, 0.42f));
                var lrt = line.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(1f, h * 0.62f);
                lrt.anchoredPosition = new Vector2(w * k / capacity, 0f);
            }
        }

        go.SetActive(false);
        return go;
    }

    private void BuildNumber(TMP_FontAsset font)
    {
        numberShadow = MakeText(root, "NumberShadow", font, new Color(0f, 0f, 0f, 0.72f));
        numberText = MakeText(root, "Number", font, style.numberColor);

        foreach (var t in new[] { numberShadow, numberText })
        {
            var rt = t.rectTransform;
            switch (geo.numberPlacement)
            {
                case BarNumberPlacement.CenteredOnBar:
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(geo.width, geo.height);
                    rt.anchoredPosition = Vector2.zero;
                    t.alignment = TextAlignmentOptions.Center;
                    break;
                case BarNumberPlacement.RightOfBar:
                    rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.sizeDelta = new Vector2(160f, geo.height * 1.6f);
                    rt.anchoredPosition = new Vector2(geo.numberGap, 0f);
                    t.alignment = TextAlignmentOptions.Left;
                    break;
                case BarNumberPlacement.LeftOfBar:
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.sizeDelta = new Vector2(160f, geo.height * 1.6f);
                    rt.anchoredPosition = new Vector2(-geo.numberGap, 0f);
                    t.alignment = TextAlignmentOptions.Right;
                    break;
            }
            t.fontSize = geo.numberSize;
        }

        // 1px drop shadow — readable over both the fill and the dark track without relying on a TMP
        // outline material property (see CLAUDE.md: shader properties that don't exist fail silently).
        numberShadow.rectTransform.anchoredPosition += new Vector2(1.5f, -1.5f);
    }

    // ---------------------------------------------------------------- per-frame apply

    private void ApplyFills()
    {
        Color fillCol = style.fill;
        if (style.warnWhenLow && current > 0f && current <= style.lowThreshold)
            fillCol = Color.Lerp(style.fill, style.low, 0.5f + 0.5f * Mathf.Sin(pulseT));

        int active = ActiveCellIndex();
        bool low = current > 0f && current <= style.lowThreshold;

        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            float amount = Mathf.Clamp01((current - c.start) / c.capacity);
            c.fill.fillAmount = amount;
            c.chip.fillAmount = Mathf.Clamp01((chipValue - c.start) / c.capacity);
            c.fill.color = fillCol;

            bool showPips;
            switch (style.pipMode)
            {
                case BarPipMode.AllSegments: showPips = true; break;
                case BarPipMode.FinalReserve: showPips = i == 0; break;
                // Only the segment actually mid-drain — a brimming cell has nothing to count, so a
                // full bar stays clean and the ticks appear exactly when you start eating into one.
                case BarPipMode.ActiveSegment: showPips = i == active && (low || (amount > 0.001f && amount < 0.999f)); break;
                default: showPips = false; break;
            }
            if (c.pips != null && c.pips.activeSelf != showPips) c.pips.SetActive(showPips);
        }
    }

    // The cell the value is currently sitting in — the one being eaten. Exactly-full boundaries
    // count as the lower cell, so at 30/40 the third segment is active, not the empty fourth.
    private int ActiveCellIndex()
    {
        for (int i = cells.Count - 1; i >= 0; i--)
            if (current > cells[i].start) return i;
        return 0;
    }

    private void ApplyText()
    {
        if (numberText == null) return;

        // Rebuild only when a displayed digit actually changes — this runs every frame otherwise
        // and would churn four strings a frame for no visible difference.
        int curInt = Mathf.CeilToInt(current), maxInt = Mathf.CeilToInt(max);
        if (curInt == shownCur && maxInt == shownMax) return;
        shownCur = curInt; shownMax = maxInt;

        // Rich-text tags must be invariant: on a Turkish locale a float size would interpolate as
        // "<size=13,5>" and TMP would not parse it.
        string maxSize = (geo.maxNumberSize > 0f ? geo.maxNumberSize : geo.numberSize)
            .ToString("0.##", CultureInfo.InvariantCulture);
        string cur = curInt.ToString(CultureInfo.InvariantCulture);
        string mx = maxInt.ToString(CultureInfo.InvariantCulture);

        string hex = ColorUtility.ToHtmlStringRGB(style.maxNumberColor);
        numberText.text = style.showMax
            ? $"{cur}<size={maxSize}><color=#{hex}> / {mx}</color></size>"
            : cur;
        // The shadow drops the colour tag so it stays a flat silhouette behind the real text.
        numberShadow.text = style.showMax ? $"{cur}<size={maxSize}> / {mx}</size>" : cur;
    }

    // ---------------------------------------------------------------- small UGUI helpers

    private static Image MakeImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, TMP_FontAsset font, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.color = color;
        t.richText = true;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AsFill(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
    }
}
