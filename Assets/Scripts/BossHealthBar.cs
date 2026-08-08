using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A big, screen-anchored boss health bar (top-center), themed for the Oxidation District:
// a chunky black+bronze frame, segment notches, a glossy bevel, verdigris/acid fill, a styled
// boss name, and plenty of juice — a fill-up intro when the fight starts, a white flash + shake
// on every hit, a delayed "damage chunk" drain, and a low-HP danger pulse.
//
// Built procedurally (prefab = empty GameObject + this script; UI is created in Awake). Spawned by
// the boss in its Start(), bound to an EnemyHealth, and removes itself when the boss dies.
public class BossHealthBar : MonoBehaviour
{
    [Header("Identity")]
    public string bossName = "The Moss Knight";
    [Tooltip("Font for the boss name (assign CCBattleScarred SDF or Pixie SDF; TMP default if empty).")]
    public TMP_FontAsset nameFont;

    [Header("Layout (reference 1920x1080)")]
    public float barWidth = 900f;
    public float barHeight = 34f;
    [Tooltip("Distance from the top of the screen to the bar.")]
    public float topOffset = 54f;
    [Tooltip("Number of notch segments across the bar.")]
    public int segmentCount = 10;

    [Header("Colors")]
    public Color fillColor = new Color(0.42f, 0.74f, 0.33f);       // HP — verdigris green
    public Color delayedColor = new Color(0.83f, 0.95f, 0.55f);    // trailing lost-HP chunk — pale acid
    public Color warnColor = new Color(0.95f, 0.35f, 0.15f);       // low-HP / flash tint
    public Color backgroundColor = new Color(0.06f, 0.09f, 0.06f);
    public Color frameColor = new Color(0.34f, 0.3f, 0.18f);       // oxidized bronze
    public Color outlineColor = new Color(0.02f, 0.03f, 0.02f);
    public Color nameTopColor = new Color(0.96f, 0.98f, 0.88f);
    public Color nameBottomColor = new Color(0.55f, 0.76f, 0.45f);

    private const float INTRO_DURATION = 0.7f;
    private const float DELAYED_SPEED = 0.5f;
    private const float CHUNK_HOLD = 0.35f;
    private const float FLASH_DECAY = 7f;
    private const float SHAKE_DECAY = 26f;
    private const float FADE_SPEED = 3f;
    private const int OUTLINE = 2;
    private const int FRAME = 3;

    private EnemyHealth health;
    private bool subscribed;

    private CanvasGroup canvasGroup;
    private RectTransform panel;
    private Vector2 panelBasePos;
    private Image fillImmediate;
    private Image fillDelayed;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI nameShadow;

    private float displayRatio = 1f;
    private float delayedFill = 1f;
    private float chunkHoldTimer;
    private float flash;
    private float shake;
    private bool intro = true;
    private float introT;
    private bool dying;

    private static Sprite cachedWhiteSprite;

    void Awake()
    {
        BuildUI();
    }

    // Called by the boss right after it wakes. Binds the bar to the boss's health.
    public void Initialize(EnemyHealth bossHealth, string displayName)
    {
        health = bossHealth;
        if (!string.IsNullOrEmpty(displayName)) bossName = displayName;
        SetName(bossName);

        if (health != null && !subscribed)
        {
            health.OnDamaged += HandleDamaged;
            subscribed = true;
        }
        intro = true;
        introT = 0f;
    }

    void OnDestroy()
    {
        if (health != null && subscribed) health.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged()
    {
        flash = 1f;
        shake = 7f;
        chunkHoldTimer = CHUNK_HOLD;
    }

    void Update()
    {
        if (dying)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.deltaTime * FADE_SPEED);
            if (canvasGroup.alpha <= 0f) Destroy(gameObject);
            return;
        }

        // health == null means the boss GameObject was destroyed (it died) — bow out.
        if (health == null)
        {
            dying = true;
            return;
        }

        float max = health.maxHealth;
        float ratio = max > 0f ? Mathf.Clamp01(health.CurrentHealth / max) : 0f;

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.deltaTime * FADE_SPEED);

        if (intro)
        {
            // Dramatic fill-up as the boss appears.
            introT += Time.deltaTime / INTRO_DURATION;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(introT), 3f);   // ease-out cubic
            displayRatio = Mathf.Lerp(0f, ratio, e);
            delayedFill = displayRatio;
            if (introT >= 1f) { intro = false; displayRatio = ratio; }
        }
        else
        {
            displayRatio = ratio;   // real HP snaps instantly
            if (delayedFill > displayRatio)
            {
                if (chunkHoldTimer > 0f) chunkHoldTimer -= Time.deltaTime;
                else delayedFill = Mathf.MoveTowards(delayedFill, displayRatio, Time.deltaTime * DELAYED_SPEED);
            }
            else delayedFill = displayRatio;
        }

        fillImmediate.fillAmount = displayRatio;
        fillDelayed.fillAmount = Mathf.Max(delayedFill, displayRatio);

        // Low-HP danger pulse + white hit flash, composited onto the fill color.
        float danger = displayRatio < 0.3f ? (1f - displayRatio / 0.3f) : 0f;
        float pulse = danger > 0f ? (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f)) * danger : 0f;
        if (flash > 0f) flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * FLASH_DECAY);

        Color c = Color.Lerp(fillColor, warnColor, pulse * 0.6f);
        c = Color.Lerp(c, Color.white, flash);
        fillImmediate.color = c;

        if (nameText != null)
        {
            Color nc = Color.Lerp(Color.white, warnColor, pulse * 0.6f);
            nameText.color = nc;
        }

        // Hit shake on the bar.
        if (shake > 0f)
        {
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * SHAKE_DECAY);
            panel.anchoredPosition = panelBasePos + new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * shake;
        }
        else if (panel.anchoredPosition != panelBasePos)
        {
            panel.anchoredPosition = panelBasePos;
        }
    }

    private void SetName(string n)
    {
        string up = string.IsNullOrEmpty(n) ? "" : n.ToUpperInvariant();
        if (nameText != null) nameText.text = up;
        if (nameShadow != null) nameShadow.text = up;
    }

    void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;

        RectTransform rootRT = GetComponent<RectTransform>();

        // Panel = outermost outline (near-black).
        GameObject panelGO = MakeChild("BarPanel", rootRT);
        panel = panelGO.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.sizeDelta = new Vector2(barWidth, barHeight);
        panel.anchoredPosition = new Vector2(0f, -topOffset);
        panelBasePos = panel.anchoredPosition;
        AddImage(panelGO, outlineColor);

        // Bronze frame inside the outline.
        Image frame = MakeChildImage("Frame", panel, frameColor);
        FillRect(frame.rectTransform, OUTLINE);

        // Content area inside the frame.
        Image inner = MakeChildImage("Inner", panel, backgroundColor);
        FillRect(inner.rectTransform, OUTLINE + FRAME);
        RectTransform content = inner.rectTransform;

        // Delayed (pale acid) chunk, then the immediate (verdigris) real-HP layer on top.
        fillDelayed = MakeChildImage("FillDelayed", content, delayedColor);
        SetFillImage(fillDelayed);
        FillRect(fillDelayed.rectTransform, 0f);

        fillImmediate = MakeChildImage("FillImmediate", content, fillColor);
        SetFillImage(fillImmediate);
        FillRect(fillImmediate.rectTransform, 0f);

        // Glossy bevel: a bright strip across the top, a soft shadow across the bottom.
        Image gloss = MakeChildImage("Gloss", content, new Color(1f, 1f, 1f, 0.12f));
        RectTransform gr = gloss.rectTransform;
        gr.anchorMin = new Vector2(0f, 0.55f);
        gr.anchorMax = new Vector2(1f, 1f);
        gr.offsetMin = Vector2.zero;
        gr.offsetMax = Vector2.zero;

        Image shadeBottom = MakeChildImage("ShadeBottom", content, new Color(0f, 0f, 0f, 0.22f));
        RectTransform sb = shadeBottom.rectTransform;
        sb.anchorMin = new Vector2(0f, 0f);
        sb.anchorMax = new Vector2(1f, 0.3f);
        sb.offsetMin = Vector2.zero;
        sb.offsetMax = Vector2.zero;

        // Segment notches.
        int segs = Mathf.Max(1, segmentCount);
        for (int i = 1; i < segs; i++)
        {
            float f = (float)i / segs;
            Image tick = MakeChildImage("Tick", content, new Color(0f, 0f, 0f, 0.45f));
            RectTransform tr = tick.rectTransform;
            tr.anchorMin = new Vector2(f, 0f);
            tr.anchorMax = new Vector2(f, 1f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(2f, 0f);
            tr.anchoredPosition = Vector2.zero;
        }

        // Boss name (shadow clone behind, gradient face in front), centered below the bar.
        nameShadow = MakeName("BossNameShadow", panel, new Vector2(2f, -2f));
        nameShadow.color = new Color(0f, 0f, 0f, 0.8f);
        nameShadow.enableVertexGradient = false;

        nameText = MakeName("BossName", panel, Vector2.zero);
        nameText.enableVertexGradient = true;
        nameText.colorGradient = new VertexGradient(nameTopColor, nameTopColor, nameBottomColor, nameBottomColor);

        SetName(bossName);
    }

    private TextMeshProUGUI MakeName(string name, RectTransform parent, Vector2 pixelOffset)
    {
        GameObject go = MakeChild(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 30f);
        rt.anchoredPosition = new Vector2(pixelOffset.x, -5f + pixelOffset.y);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        t.fontSize = 26f;
        t.characterSpacing = 6f;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        if (nameFont != null) t.font = nameFont;
        return t;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = GetWhiteSprite();
        img.raycastTarget = false;
        return img;
    }

    private static Image MakeChildImage(string name, RectTransform parent, Color color)
    {
        return AddImage(MakeChild(name, parent), color);
    }

    private static GameObject MakeChild(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void FillRect(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetFillImage(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
    }

    private static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        }
        return cachedWhiteSprite;
    }
}
