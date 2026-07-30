using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Stage 3 of the slot-relic redesign (RelicRedesign.md): the acquire-when-full decision.
// When a relic is granted while all 5 slots are full, RelicManager.TryGrantRelic opens this:
// the incoming relic is shown up top, the current loadout below as click-to-sell targets.
// "Take it" sells the chosen relic (gold in) then adds the incoming and runs the caller's
// onAcquired (e.g. the shop charges gold ONLY here); "Leave it" declines at no cost.
//
// Robust to being opened either on top of an already-paused panel (shop / slot machine) or
// during live gameplay (chest / debug): it saves and restores the prior game state and only
// touches the HUD/hand-drawer when it was opened from gameplay (HUD visible).
public class RelicSwapScreen : MonoBehaviour
{
    public static RelicSwapScreen instance;

    private CanvasGroup group;
    private RectTransform window;
    private RectTransform incomingChipHolder;
    private Image incomingFrame;
    private TMP_Text newRelicLabel, incomingName, incomingDesc, sacrificeInfo;
    private Transform slotRow;
    private Button takeButton;
    private RelicTooltip tooltip;
    private TMP_FontAsset font;

    private readonly List<Image> selectRings = new List<Image>();
    private readonly List<RelicData> cellRelics = new List<RelicData>();

    private RelicData incoming;
    private RelicData sacrifice;
    private System.Action onAcquired;
    private bool isOpen;

    // Restore-state bookkeeping.
    private GameState prevState;
    private GameObject cachedHud;
    private bool hudWasActive;

    private const float WIN_W = 720f, WIN_H = 540f, CELL = 92f;

    public static void Open(RelicData incoming, System.Action onAcquired)
    {
        if (incoming == null) return;
        EnsureInstance();
        if (instance == null) return;
        if (instance.isOpen)   // a swap is already up — don't stack; drop the extra grant
        {
            Debug.LogWarning("RelicSwapScreen already open; ignoring a second grant.");
            return;
        }
        instance.Show(incoming, onAcquired);
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        Canvas canvas = FindRootCanvas();
        if (canvas == null) { Debug.LogWarning("RelicSwapScreen: no Canvas found in scene."); return; }
        GameObject go = new GameObject("RelicSwapScreen", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<RelicSwapScreen>();
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

    // ---- construction ----
    private void Build()
    {
        font = ResolveFont();

        Stretch(GetComponent<RectTransform>());
        group = gameObject.AddComponent<CanvasGroup>();

        Image backdrop = AddImage(transform, "Backdrop", null, new Color(0f, 0f, 0f, 0.86f), true);
        Stretch(backdrop.rectTransform);   // no click-to-close: this is a forced decision

        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIN_W, WIN_H));
        Image winBg = window.gameObject.AddComponent<Image>();
        winBg.sprite = RelicUISprites.StonePanel(); winBg.type = Image.Type.Sliced;
        winBg.color = new Color(0.8f, 0.78f, 0.82f, 1f); winBg.raycastTarget = true;
        Image winFrame = AddImage(window, "Frame", RelicUISprites.GoldBorder(), Color.white, false);
        winFrame.type = Image.Type.Sliced; Stretch(winFrame.rectTransform);
        RelicUISprites.AddGemStuds(window, WIN_W, WIN_H, RelicUISprites.GemColor(Rarity.Common));   // ruby studs like the HUD panel

        AddText(window, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(640f, 40f),
            "LOADOUT FULL", 30f, FontStyles.Bold, new Color(0.95f, 0.86f, 0.6f), TextAlignmentOptions.Top);
        AddText(window, "Subtitle", new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(660f, 26f),
            "Sell a relic to make room, or leave the new one.", 16f, FontStyles.Normal,
            new Color(0.78f, 0.8f, 0.88f), TextAlignmentOptions.Top);

        // Incoming relic sub-panel.
        RectTransform inc = AddPoint(window, "Incoming", new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(640f, 132f));
        inc.pivot = new Vector2(0.5f, 1f);
        Image incBg = inc.gameObject.AddComponent<Image>();
        incBg.sprite = RelicUISprites.StonePanel(); incBg.type = Image.Type.Sliced;
        incBg.color = new Color(0.95f, 0.9f, 0.82f, 1f);   // warmer stone sets the incoming apart
        incomingFrame = AddImage(inc, "IncFrame", RelicUISprites.GoldBorder(), Color.white, false);
        incomingFrame.type = Image.Type.Sliced; Stretch(incomingFrame.rectTransform);

        newRelicLabel = AddText(inc, "NewLabel", new Vector2(0f, 1f), new Vector2(30f, -16f), new Vector2(260f, 22f),
            "NEW RELIC", 14f, FontStyles.Bold, new Color(0.95f, 0.86f, 0.6f), TextAlignmentOptions.TopLeft);
        incomingChipHolder = AddPoint(inc, "Chip", new Vector2(0f, 0.5f), new Vector2(78f, -8f), new Vector2(96f, 96f));
        // Name/description column starts well clear of the icon AND its glow aura (the glow
        // extends ~1.4x the icon, to ~x=140), so the name no longer blends into the symbol.
        incomingName = AddText(inc, "IncName", new Vector2(0f, 1f), new Vector2(196f, -30f), new Vector2(424f, 32f),
            "", 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft);
        incomingDesc = AddText(inc, "IncDesc", new Vector2(0f, 1f), new Vector2(196f, -66f), new Vector2(424f, 60f),
            "", 15f, FontStyles.Normal, new Color(0.82f, 0.84f, 0.9f), TextAlignmentOptions.TopLeft);
        incomingDesc.enableWordWrapping = true;

        // Divider + loadout label
        Image div = AddImage(window, "Divider", RelicUISprites.White(), new Color(1f, 1f, 1f, 0.12f), false);
        div.rectTransform.anchorMin = div.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        div.rectTransform.pivot = new Vector2(0.5f, 1f);
        div.rectTransform.sizeDelta = new Vector2(WIN_W - 108f, 2f);
        div.rectTransform.anchoredPosition = new Vector2(0f, -230f);
        AddText(window, "LoadoutLabel", new Vector2(0f, 1f), new Vector2(52f, -244f), new Vector2(520f, 24f),
            "YOUR RELICS  (click one to sell)", 15f, FontStyles.Bold, new Color(0.78f, 0.8f, 0.88f), TextAlignmentOptions.TopLeft);

        // Slot row
        RectTransform rowRt = AddPoint(window, "SlotRow", new Vector2(0.5f, 1f), new Vector2(0f, -276f), new Vector2(WIN_W - 60f, CELL));
        rowRt.pivot = new Vector2(0.5f, 1f);
        HorizontalLayoutGroup hlg = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        slotRow = rowRt;

        sacrificeInfo = AddText(window, "SacrificeInfo", new Vector2(0.5f, 1f), new Vector2(0f, -384f), new Vector2(640f, 26f),
            "", 16f, FontStyles.Italic, new Color(0.85f, 0.7f, 0.4f), TextAlignmentOptions.Top);

        // Buttons — inset off the border/studs.
        BuildButton(window, "LeaveButton", new Vector2(0f, 0f), new Vector2(48f, 42f), new Vector2(216f, 54f),
            new Color(0.4f, 0.42f, 0.48f), "LEAVE IT", new Color(1f, 1f, 1f), DoLeave, out _);
        takeButton = BuildButton(window, "TakeButton", new Vector2(1f, 0f), new Vector2(-48f, 42f), new Vector2(216f, 54f),
            new Color(0.42f, 0.72f, 0.34f), "TAKE IT", new Color(0.08f, 0.12f, 0.06f), DoTake, out _);

        // Shared hover tooltip so the player can read what each sell candidate does.
        GameObject tipGo = new GameObject("RelicTooltip", typeof(RectTransform));
        tipGo.transform.SetParent(transform, false);
        tooltip = tipGo.AddComponent<RelicTooltip>();
        tooltip.Build(font);

        gameObject.SetActive(false);
    }

    // ---- open / close ----
    private void Show(RelicData incomingRelic, System.Action acquiredCallback)
    {
        isOpen = true;
        incoming = incomingRelic;
        onAcquired = acquiredCallback;
        sacrifice = null;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // Save + apply state. Pause counter is nesting-safe; game state is saved/restored so a
        // shop-context open returns to Paused, a gameplay-context open returns to Playing.
        prevState = GameManager.instance != null ? GameManager.instance.currentState : GameState.Playing;
        if (GameManager.instance != null)
        {
            GameManager.instance.RequestPause();
            GameManager.instance.SetGameState(GameState.Paused);
        }
        if (cachedHud == null) cachedHud = GameObject.Find("GameplayHUD");
        hudWasActive = cachedHud != null && cachedHud.activeSelf;   // visible ⇒ opened from gameplay
        if (cachedHud != null) cachedHud.SetActive(false);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        RebuildIncoming();
        RebuildSlots();
        RefreshTake();

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
            GameManager.instance.SetGameState(prevState);       // back to whatever it was
        }
        if (cachedHud != null) cachedHud.SetActive(hudWasActive);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);

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

    // ---- content ----
    private void RebuildIncoming()
    {
        for (int c = incomingChipHolder.childCount - 1; c >= 0; c--)
            Destroy(incomingChipHolder.GetChild(c).gameObject);

        GameObject chip = new GameObject("IncomingIcon", typeof(RectTransform), typeof(Image));
        RectTransform crt = chip.GetComponent<RectTransform>();
        crt.SetParent(incomingChipHolder, false);
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(96f, 96f);
        chip.AddComponent<RelicIcon>().Build(incoming);

        Color rc = RelicUISprites.RarityColor(incoming.rarity);
        newRelicLabel.color = rc;   // frame stays gold; rarity reads through the label, name, and the chip's gems
        incomingName.text = string.IsNullOrEmpty(incoming.relicName) ? incoming.relicID : incoming.relicName;
        incomingName.color = rc;
        incomingDesc.text = string.IsNullOrEmpty(incoming.description) ? "-" : incoming.description;
    }

    private void RebuildSlots()
    {
        for (int c = slotRow.childCount - 1; c >= 0; c--)
            Destroy(slotRow.GetChild(c).gameObject);
        selectRings.Clear();
        cellRelics.Clear();

        var owned = RelicManager.instance != null ? RelicManager.instance.OwnedRelics : null;
        int count = owned != null ? owned.Count : 0;

        for (int i = 0; i < RelicManager.MaxSlots; i++)
        {
            RectTransform cell = AddPoint(slotRow, $"Slot{i}", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CELL, CELL));
            LayoutElement le = cell.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = CELL;

            Image ring = AddImage(cell, "Ring", RelicUISprites.Frame(), Color.white, false);
            ring.type = Image.Type.Sliced;
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ring.rectTransform.sizeDelta = new Vector2(CELL + 6f, CELL + 6f);
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

                // Transparent hit-target on the cell itself (RelicIcon's graphics are non-raycast)
                // + a hover relay: hover shows the tooltip, click selects this relic as the sacrifice.
                Image hit = cell.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                hit.raycastTarget = true;
                RelicData captured = relic;
                cell.gameObject.AddComponent<RelicSlotHover>().Set(captured, tooltip, () => Select(captured));
            }
        }
        RefreshRings();
    }

    private void Select(RelicData relic)
    {
        sacrifice = relic;
        RefreshRings();
        RefreshTake();
    }

    private void RefreshRings()
    {
        for (int i = 0; i < selectRings.Count; i++)
        {
            bool on = sacrifice != null && cellRelics[i] == sacrifice;
            selectRings[i].enabled = on;
            if (on) selectRings[i].color = RelicUISprites.RarityColor(sacrifice.rarity);
        }
    }

    private void RefreshTake()
    {
        bool ready = sacrifice != null;
        if (takeButton != null) takeButton.interactable = ready;   // disabled state auto-dims it
        if (sacrificeInfo != null)
        {
            if (ready)
            {
                int v = RelicManager.instance != null ? RelicManager.instance.SellValueFor(sacrifice) : 0;
                string nm = string.IsNullOrEmpty(sacrifice.relicName) ? sacrifice.relicID : sacrifice.relicName;
                sacrificeInfo.text = $"Selling {nm} for +{v} gold";
            }
            else sacrificeInfo.text = "";
        }
    }

    private void DoTake()
    {
        if (sacrifice == null || incoming == null || RelicManager.instance == null) return;

        RelicManager.instance.SellRelic(sacrifice);   // frees a slot (5→4), credits gold
        RelicManager.instance.AddRelic(incoming);      // fills it back (4→5)

        System.Action cb = onAcquired;
        Hide();
        cb?.Invoke();   // caller finalizes (e.g. shop charges gold, marks item sold)
    }

    private void DoLeave()
    {
        Hide();   // declined — onAcquired is never called, nothing charged
    }

    // ---- small UGUI builders (house style — self-contained per panel) ----
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
        img.sprite = sprite; img.color = color; img.raycastTarget = raycast;
        return img;
    }

    private TMP_Text AddText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = AddPoint(parent, name, anchor, pos, size);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = fontSize; t.fontStyle = style; t.color = color; t.alignment = align;
        t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }

    private Button BuildButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        Color color, string labelText, Color labelColor, UnityEngine.Events.UnityAction onClick, out TMP_Text label)
    {
        RectTransform rt = AddPoint(parent, name, anchor, pos, size);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = RelicUISprites.Panel(); bg.type = Image.Type.Sliced; bg.color = color;
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        b.onClick.AddListener(onClick);
        label = AddText(rt, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, size,
            labelText, 20f, FontStyles.Bold, labelColor, TextAlignmentOptions.Center);
        return b;
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
