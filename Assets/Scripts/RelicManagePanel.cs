using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Stage 2 of the slot-relic redesign (RelicRedesign.md): a calm, paused screen to inspect
// the loadout and SELL a relic for gold. Built procedurally (house style, RelicUISprites),
// self-instantiated under the main Canvas — no scene/prefab wiring. Opens from a loadout-bar
// click (RelicSlotHover) or the toggle hotkey (default I, set by RelicHUD).
public class RelicManagePanel : MonoBehaviour
{
    public static RelicManagePanel instance;
    private static KeyCode toggleKey = KeyCode.I;

    // Frame of the last open/close. RelicHUD checks this so the keypress that closes the panel
    // (which reactivates the HUD mid-frame) can't be re-read by the HUD to instantly reopen.
    public static int LastToggleFrame { get; private set; }

    private CanvasGroup group;
    private RectTransform window;
    private Transform slotRow;
    private TMP_Text countText, detailName, detailDesc;
    private GameObject sellButtonGo;
    private TMP_Text sellLabel;
    private TMP_FontAsset font;

    private readonly List<Image> selectRings = new List<Image>();
    private readonly List<RelicData> cellRelics = new List<RelicData>();
    private RelicData selected;
    private bool isOpen;
    private GameObject cachedHud;   // cached while active in Show(); Find() can't see it once hidden

    private const float WIN_W = 680f, WIN_H = 470f;
    private const float CELL = 104f;

    public static void SetToggleKey(KeyCode k) => toggleKey = k;

    public static void Open()
    {
        EnsureInstance();
        if (instance != null) instance.Show();
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        Canvas canvas = FindRootCanvas();
        if (canvas == null) { Debug.LogWarning("RelicManagePanel: no Canvas found in scene."); return; }
        GameObject go = new GameObject("RelicManagePanel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<RelicManagePanel>();
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

    private void Update()
    {
        // Close via the same toggle key while open (opening is handled by RelicHUD, which is
        // inactive while this panel is up).
        if (isOpen && Input.GetKeyDown(toggleKey)) Hide();
    }

    // ---- construction ----
    private void Build()
    {
        font = ResolveFont();

        RectTransform root = GetComponent<RectTransform>();
        Stretch(root);
        group = gameObject.AddComponent<CanvasGroup>();

        // Full-screen dim; clicking it (outside the window) closes.
        Image backdrop = AddImage(transform, "Backdrop", null, new Color(0f, 0f, 0f, 0.82f), true);
        Stretch(backdrop.rectTransform);
        Button backBtn = backdrop.gameObject.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(Hide);

        // Window
        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIN_W, WIN_H));
        Image winBg = window.gameObject.AddComponent<Image>();
        winBg.sprite = RelicUISprites.Panel();
        winBg.type = Image.Type.Sliced;
        winBg.color = new Color(0.11f, 0.12f, 0.15f, 0.98f);
        winBg.raycastTarget = true;   // blocks the backdrop-close behind it
        Image winFrame = AddImage(window, "Frame", RelicUISprites.Frame(), new Color(0.82f, 0.66f, 0.32f, 1f), false);
        winFrame.type = Image.Type.Sliced;
        Stretch(winFrame.rectTransform);

        // Header
        AddText(window, "Title", new Vector2(0f, 1f), new Vector2(30f, -22f), new Vector2(320f, 44f),
            "RELICS", 32f, FontStyles.Bold, new Color(0.95f, 0.86f, 0.6f), TextAlignmentOptions.TopLeft);
        countText = AddText(window, "Count", new Vector2(1f, 1f), new Vector2(-64f, -30f), new Vector2(120f, 32f),
            "0 / 5", 22f, FontStyles.Bold, new Color(0.8f, 0.83f, 0.9f), TextAlignmentOptions.TopRight);

        // Close X (top-right corner)
        RectTransform closeRt = AddPoint(window, "Close", new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(34f, 34f));
        Image closeBg = closeRt.gameObject.AddComponent<Image>();
        closeBg.sprite = RelicUISprites.Panel();
        closeBg.type = Image.Type.Sliced;
        closeBg.color = new Color(0.6f, 0.2f, 0.2f, 1f);
        Button closeBtn = closeRt.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeBg;
        closeBtn.onClick.AddListener(Hide);
        AddText(closeRt, "X", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f),
            "X", 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);

        // Slot row (horizontal, centred under the header)
        RectTransform rowRt = AddPoint(window, "SlotRow", new Vector2(0.5f, 1f), new Vector2(0f, -76f),
            new Vector2(WIN_W - 40f, CELL));
        rowRt.pivot = new Vector2(0.5f, 1f);
        HorizontalLayoutGroup hlg = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        slotRow = rowRt;

        // Divider under the slots
        Image div = AddImage(window, "Divider", RelicUISprites.White(), new Color(1f, 1f, 1f, 0.12f), false);
        RectTransform drt = div.rectTransform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 1f);
        drt.pivot = new Vector2(0.5f, 1f);
        drt.sizeDelta = new Vector2(WIN_W - 64f, 2f);
        drt.anchoredPosition = new Vector2(0f, -208f);

        // Detail area (lower)
        detailName = AddText(window, "DetailName", new Vector2(0f, 1f), new Vector2(34f, -224f), new Vector2(WIN_W - 68f, 34f),
            "", 24f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft);
        // Narrow column on the left so a long description never runs under the sell button.
        detailDesc = AddText(window, "DetailDesc", new Vector2(0f, 1f), new Vector2(34f, -262f), new Vector2(WIN_W - 300f, 150f),
            "", 17f, FontStyles.Normal, new Color(0.82f, 0.84f, 0.9f), TextAlignmentOptions.TopLeft);
        detailDesc.enableWordWrapping = true;

        // Sell button (bottom-right)
        sellButtonGo = BuildButton(window, "SellButton", new Vector2(1f, 0f), new Vector2(-34f, 34f),
            new Vector2(250f, 56f), new Color(0.86f, 0.68f, 0.28f), out sellLabel, DoSell);

        gameObject.SetActive(false);
    }

    // ---- open / close ----
    private void Show()
    {
        if (isOpen) return;
        isOpen = true;
        selected = null;
        LastToggleFrame = Time.frameCount;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (GameManager.instance != null)
        {
            GameManager.instance.RequestPause();                       // stops physics (timeScale 0)
            GameManager.instance.SetGameState(GameState.Paused);       // blocks player input (jump/move/cards)
        }
        if (cachedHud == null) cachedHud = GameObject.Find("GameplayHUD");   // active now, cache for Hide
        if (cachedHud != null) cachedHud.SetActive(false);
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        RebuildSlots();
        UpdateDetail();
        StopAllCoroutines();
        StartCoroutine(OpenAnim());
    }

    private void Hide()
    {
        if (!isOpen) return;
        isOpen = false;
        LastToggleFrame = Time.frameCount;

        if (GameManager.instance != null)
        {
            GameManager.instance.ReleasePause();
            GameManager.instance.SetGameState(GameState.Playing);
        }
        if (cachedHud != null) cachedHud.SetActive(true);
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);

        gameObject.SetActive(false);
    }

    private IEnumerator OpenAnim()
    {
        float t = 0f; const float dur = 0.2f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            group.alpha = n;
            window.localScale = Vector3.one * (0.9f + 0.1f * EaseOutBack(n));
            yield return null;
        }
        group.alpha = 1f;
        window.localScale = Vector3.one;
    }

    // ---- slot grid ----
    private void RebuildSlots()
    {
        for (int c = slotRow.childCount - 1; c >= 0; c--)
            Destroy(slotRow.GetChild(c).gameObject);
        selectRings.Clear();
        cellRelics.Clear();

        var owned = RelicManager.instance != null ? RelicManager.instance.OwnedRelics : null;
        int count = owned != null ? owned.Count : 0;
        int slots = RelicManager.MaxSlots;

        for (int i = 0; i < slots; i++)
        {
            RectTransform cell = AddPoint(slotRow, $"Slot{i}", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CELL, CELL));
            LayoutElement le = cell.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = CELL;

            // Selection ring (hidden until selected).
            Image ring = AddImage(cell, "Ring", RelicUISprites.Frame(), new Color(1f, 1f, 1f, 0.95f), false);
            ring.type = Image.Type.Sliced;
            RectTransform ringRt = ring.rectTransform;
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(CELL + 6f, CELL + 6f);
            ring.enabled = false;
            selectRings.Add(ring);

            RelicData relic = i < count ? owned[i] : null;
            cellRelics.Add(relic);

            if (relic != null)
            {
                GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                RectTransform irt = icon.GetComponent<RectTransform>();
                irt.SetParent(cell, false);
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(CELL - 8f, CELL - 8f);
                icon.AddComponent<RelicIcon>().Build(relic);

                // Transparent hit target + button to select this relic.
                Image hit = AddImage(cell, "Hit", null, new Color(0, 0, 0, 0), true);
                hit.rectTransform.anchorMin = hit.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                hit.rectTransform.sizeDelta = new Vector2(CELL, CELL);
                Button b = hit.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                RelicData captured = relic;
                b.onClick.AddListener(() => Select(captured));
            }
            else
            {
                Image fill = AddImage(cell, "EmptyFill", RelicUISprites.Panel(), new Color(0.12f, 0.13f, 0.16f, 0.5f), false);
                fill.type = Image.Type.Sliced;
                fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                fill.rectTransform.sizeDelta = new Vector2(CELL - 8f, CELL - 8f);
                Image frame = AddImage(cell, "EmptyFrame", RelicUISprites.Frame(), new Color(0.45f, 0.48f, 0.55f, 0.5f), false);
                frame.type = Image.Type.Sliced;
                frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                frame.rectTransform.sizeDelta = new Vector2(CELL - 8f, CELL - 8f);
            }
        }

        if (countText != null) countText.text = $"{count} / {slots}";
        RefreshRings();
    }

    private void Select(RelicData relic)
    {
        selected = relic;
        UpdateDetail();
        RefreshRings();
    }

    private void RefreshRings()
    {
        for (int i = 0; i < selectRings.Count; i++)
        {
            bool on = selected != null && cellRelics[i] == selected;
            selectRings[i].enabled = on;
            if (on) selectRings[i].color = RelicUISprites.RarityColor(selected.rarity);
        }
    }

    private void UpdateDetail()
    {
        if (selected == null)
        {
            detailName.text = "";
            detailDesc.text = "Select a relic to inspect it, or sell it for gold.";
            if (sellButtonGo != null) sellButtonGo.SetActive(false);
            return;
        }

        detailName.text = string.IsNullOrEmpty(selected.relicName) ? selected.relicID : selected.relicName;
        detailName.color = RelicUISprites.RarityColor(selected.rarity);
        detailDesc.text = string.IsNullOrEmpty(selected.description) ? "-" : selected.description;

        int value = RelicManager.instance != null ? RelicManager.instance.SellValueFor(selected) : 0;
        if (sellLabel != null) sellLabel.text = $"SELL: {value} GOLD";
        if (sellButtonGo != null) sellButtonGo.SetActive(true);
    }

    private void DoSell()
    {
        if (selected == null || RelicManager.instance == null) return;
        RelicManager.instance.SellRelic(selected);
        selected = null;
        RebuildSlots();
        UpdateDetail();
    }

    // ---- small UGUI builders ----
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private RectTransform AddPoint(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private TMP_Text AddText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = AddPoint(parent, name, anchor, pos, size);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    private GameObject BuildButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        Color color, out TMP_Text label, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rt = AddPoint(parent, name, anchor, pos, size);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = RelicUISprites.Panel();
        bg.type = Image.Type.Sliced;
        bg.color = color;
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.normalColor = Color.white;
        b.colors = cb;
        b.onClick.AddListener(onClick);

        label = AddText(rt, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, size,
            name, 20f, FontStyles.Bold, new Color(0.12f, 0.1f, 0.05f), TextAlignmentOptions.Center);
        return rt.gameObject;
    }

    private TMP_FontAsset ResolveFont()
    {
        TMP_Text any = FindAnyObjectByType<TMP_Text>();
        if (any != null && any.font != null) return any.font;
        return TMP_Settings.defaultFontAsset;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
