using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A reusable "press [E]" interaction prompt. Drop this prefab under any interactable and assign it
// to that object's prompt / interactionPopup field — the existing SetActive(true/false) on player
// enter/exit drives it. Everything (canvas, keycap sprite, letter) is built procedurally in Awake,
// matching the project's art-free house style (see EnemyHealthBar). Shows a beveled keyboard cap
// with the key letter, a soft drop shadow, a gentle float + breathing pulse, and a pop-in when shown.
public class InteractPrompt : MonoBehaviour
{
    [Header("Key")]
    public string key = "E";
    public TMP_FontAsset font;              // pixel font (Pixie SDF) — assigned on the prefab

    [Header("Look")]
    public Color keycapColor = new Color(0.96f, 0.95f, 0.88f, 1f);   // warm off-white cap
    public Color letterColor = new Color(0.15f, 0.13f, 0.11f, 1f);   // dark engraved letter
    public Color shadowColor = new Color(0f, 0f, 0f, 0.45f);
    [Tooltip("On-screen size of the keycap in world units.")]
    public float size = 0.7f;
    [Tooltip("Optional by-eye fine-tune on top of Midline centering (fraction of cap size). Positive = down. 0 = off.")]
    public float letterVerticalNudge = 0f;

    [Header("Motion")]
    public float bobAmplitude = 0.09f;      // vertical float, world units
    public float bobSpeed = 2.6f;
    public float pulseAmount = 0.05f;       // breathing scale, fraction
    public float pulseSpeed = 3.4f;
    public float popInTime = 0.14f;         // grow + fade when it appears

    const float CANVAS_SCALE = 0.01f;
    const int KEYCAP_PX = 72;               // canvas/keycap pixel footprint before CANVAS_SCALE

    private CanvasGroup group;
    private RectTransform keyRT;            // keycap + letter move together for the "press" feel
    private Vector3 baseLocalPos;
    private float popT;                     // 0..1 pop-in progress
    private float phase;                    // random so multiple prompts don't bob in lockstep

    private static Sprite keycapSprite;
    private static Sprite shadowSprite;

    void Awake()
    {
        Build();
        baseLocalPos = transform.localPosition;
        phase = Random.value * 10f;
    }

    void OnEnable()
    {
        // Restart the pop-in every time the prompt is shown.
        popT = 0f;
        if (group != null) group.alpha = 0f;
    }

    void Update()
    {
        popT = Mathf.Min(1f, popT + (popInTime > 0f ? Time.deltaTime / popInTime : 1f));
        float pop = EaseOutBack(popT);
        if (group != null) group.alpha = Mathf.Clamp01(popT * 1.4f);

        // Gentle float around the placed position.
        float t = Time.time * bobSpeed + phase;
        transform.localPosition = baseLocalPos + Vector3.up * (Mathf.Sin(t) * bobAmplitude);

        // Breathing pulse layered on top of the pop-in scale.
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed + phase) * pulseAmount;
        float s = CANVAS_SCALE * pop * pulse;
        transform.localScale = new Vector3(s, s, s);
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;          // draw above world + health bars
        gameObject.AddComponent<CanvasScaler>();

        group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;

        RectTransform rootRT = GetComponent<RectTransform>();
        float px = size / CANVAS_SCALE;
        rootRT.sizeDelta = new Vector2(px, px);
        transform.localScale = Vector3.one * CANVAS_SCALE;

        // Soft drop shadow, nudged down-right for a floating feel.
        Image shadow = MakeImage("Shadow", rootRT, shadowColor, GetShadowSprite());
        StretchWithOffset(shadow.rectTransform, new Vector2(px * 0.06f, -px * 0.08f), px * 0.06f);

        // The keycap + letter live under one child so they pop/press as a unit.
        GameObject keyGO = new GameObject("Key");
        keyRT = keyGO.AddComponent<RectTransform>();
        keyRT.SetParent(rootRT, false);
        Stretch(keyRT);

        Image cap = MakeImage("Cap", keyRT, keycapColor, GetKeycapSprite());
        Stretch(cap.rectTransform);

        GameObject letterGO = new GameObject("Letter");
        RectTransform letterRT = letterGO.AddComponent<RectTransform>();
        letterRT.SetParent(keyRT, false);
        // Center the letter on the cap. TextAlignmentOptions.Center centers on the font's LINE BOX
        // (ascender→descender), so a capital with no descender optically rides high and chasing it
        // with a manual nudge is fragile. Midline centers on the actual rendered GLYPH geometry, which
        // truly centers the letter. letterVerticalNudge remains as an optional by-eye fine-tune (0 = off).
        Stretch(letterRT);
        float letterNudge = px * letterVerticalNudge;
        letterRT.offsetMin = new Vector2(0f, -letterNudge);
        letterRT.offsetMax = new Vector2(0f, -letterNudge);

        TextMeshProUGUI label = letterGO.AddComponent<TextMeshProUGUI>();
        label.text = key;
        label.alignment = TextAlignmentOptions.Midline;
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 200f;
        label.fontStyle = FontStyles.Bold;
        label.color = letterColor;
        label.enableWordWrapping = false;
        if (font != null) label.font = font;
    }

    static Image MakeImage(string name, RectTransform parent, Color color, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void StretchWithOffset(RectTransform rt, Vector2 shift, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset) + shift;
        rt.offsetMax = new Vector2(-inset, -inset) + shift;
    }

    // Beveled keycap: rounded square, brighter top / darker bottom (fake depth), darker rim.
    // Luminance is baked into RGB and modulated by Image.color, so keycapColor stays tunable.
    static Sprite GetKeycapSprite()
    {
        if (keycapSprite != null) return keycapSprite;
        int s = 64;
        float radius = 14f;
        float rim = 2.5f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdgeDistance(x, y, s, radius);   // >0 inside, distance to edge
                if (d <= 0f) { tex.SetPixel(x, y, Color.clear); continue; }

                float ny = y / (float)(s - 1);                        // 0 bottom .. 1 top
                float shade = Mathf.Lerp(0.78f, 1f, ny);              // top face brighter
                if (ny > 0.72f) shade += 0.06f;                       // subtle top highlight
                if (d < rim) shade *= 0.62f;                          // darker beveled rim
                float a = Mathf.Clamp01(d);                           // 1px anti-aliased edge
                tex.SetPixel(x, y, new Color(shade, shade, shade, a));
            }
        tex.Apply();
        keycapSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return keycapSprite;
    }

    // Soft rounded blob for the drop shadow (white, tinted via Image.color).
    static Sprite GetShadowSprite()
    {
        if (shadowSprite != null) return shadowSprite;
        int s = 64;
        float radius = 16f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdgeDistance(x, y, s, radius);
                float a = Mathf.Clamp01(d / 6f);                      // soft feathered edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        shadowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return shadowSprite;
    }

    // Signed distance (in px) from a pixel to the nearest edge of a rounded square; >0 = inside.
    static float RoundedRectEdgeDistance(int x, int y, int s, float radius)
    {
        float half = s / 2f;
        float px = x + 0.5f - half;
        float py = y + 0.5f - half;
        float ax = Mathf.Abs(px) - (half - radius);
        float ay = Mathf.Abs(py) - (half - radius);
        float outside = Mathf.Sqrt(Mathf.Max(ax, 0f) * Mathf.Max(ax, 0f) + Mathf.Max(ay, 0f) * Mathf.Max(ay, 0f));
        float inside = Mathf.Min(Mathf.Max(ax, ay), 0f);
        return radius - (outside + inside);
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
