using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// One quest slip on the board. Keeps the painted board/slot art; adds house-style juice (all procedural):
// a staggered EaseOutBack pop-in when the board opens, a gold hover highlight over the ACCEPT area, and
// a red "ACCEPTED" stamp slam + flash + dim when taken. No art / no extra Inspector wiring.
public class QuestPaper : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI Elemanları")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI rewardText;
    public Button acceptButton;

    private static Sprite glowSprite;

    private QuestData myData;
    private RectTransform rt;
    private CanvasGroup cg;
    private Image acceptHighlight;

    private bool accepted;
    private bool hovering;
    private bool popping;
    private float popT, popDelay;
    const float POP_DUR = 0.32f;

    public void Setup(QuestData data)
    {
        myData = data;
        if (titleText) titleText.text = data.questName;
        if (descText) descText.text = data.description;
        if (rewardText) rewardText.text = data.rewardText;

        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        Image rootImg = GetComponent<Image>();
        if (rootImg != null) rootImg.raycastTarget = true;   // whole slip is hoverable

        accepted = false;
        hovering = false;

        acceptButton.onClick.RemoveAllListeners();
        acceptButton.onClick.AddListener(OnAccept);
        acceptButton.interactable = true;

        // Blank the leftover "Button" label so it doesn't clutter the painted ACCEPT graphic.
        TextMeshProUGUI label = acceptButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = "";

        BuildHighlight();
        if (acceptHighlight != null) SetAlpha(acceptHighlight, 0f);

        // Staggered pop-in based on the slip's position on the board.
        popDelay = transform.GetSiblingIndex() * 0.09f;
        popT = 0f;
        popping = true;
        transform.localScale = Vector3.zero;
    }

    private void BuildHighlight()
    {
        if (acceptHighlight != null || acceptButton == null) return;
        RectTransform brt = acceptButton.GetComponent<RectTransform>();
        GameObject go = new GameObject("AcceptHighlight", typeof(RectTransform));
        RectTransform hrt = go.GetComponent<RectTransform>();
        hrt.SetParent(brt, false);
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
        hrt.offsetMin = new Vector2(-14f, -10f); hrt.offsetMax = new Vector2(14f, 10f);   // spill past the button
        acceptHighlight = go.AddComponent<Image>();
        acceptHighlight.sprite = GetGlowSprite();
        acceptHighlight.color = new Color(1f, 0.82f, 0.42f, 1f);
        acceptHighlight.raycastTarget = false;
        acceptHighlight.transform.SetAsFirstSibling();   // behind the (transparent) button graphic
    }

    public void OnPointerEnter(PointerEventData eventData) { if (!accepted) hovering = true; }
    public void OnPointerExit(PointerEventData eventData) { hovering = false; }

    // Click anywhere on the slip (not just the ACCEPT button) to take the quest. Clicks that land on
    // the ACCEPT button are consumed by the Button itself and don't reach here, so there's no double-fire
    // (and OnAccept is guarded regardless).
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        OnAccept();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (popping)
        {
            if (popDelay > 0f) { popDelay -= dt; transform.localScale = Vector3.zero; return; }
            popT += dt;
            float n = Mathf.Clamp01(popT / POP_DUR);
            transform.localScale = Vector3.one * Mathf.Max(0f, EaseOutBack(n));
            if (n >= 1f) { popping = false; transform.localScale = Vector3.one; }
            return;
        }

        if (accepted) return;

        // Hover: highlight the ACCEPT area + a subtle scale.
        if (acceptHighlight != null)
            SetAlpha(acceptHighlight, Mathf.MoveTowards(acceptHighlight.color.a, hovering ? 0.6f : 0f, dt * 4f));
        float target = hovering ? 1.03f : 1f;
        transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.one * target, dt * 0.6f);
    }

    void OnAccept()
    {
        if (accepted) return;
        accepted = true;
        hovering = false;

        acceptButton.interactable = false;
        if (acceptHighlight != null) SetAlpha(acceptHighlight, 0f);

        QuestSystem.instance.AcceptQuest(myData);
        StartCoroutine(StampRoutine());
    }

    private IEnumerator StampRoutine()
    {
        transform.localScale = Vector3.one;

        // Gold flash over the whole slip.
        Image flash = MakeImage(rt, "AcceptFlash", GetGlowSprite(), new Color(1f, 0.85f, 0.5f, 0.7f));
        flash.rectTransform.anchorMin = Vector2.zero; flash.rectTransform.anchorMax = Vector2.one;
        flash.rectTransform.offsetMin = Vector2.zero; flash.rectTransform.offsetMax = Vector2.zero;

        // Rotated red "ACCEPTED" stamp.
        TextMeshProUGUI stamp = MakeText(rt, "AcceptedStamp", "ACCEPTED", 40f);
        stamp.fontStyle = FontStyles.Bold;
        stamp.color = new Color(0.85f, 0.15f, 0.12f, 1f);
        stamp.rectTransform.anchorMin = stamp.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        stamp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        stamp.rectTransform.sizeDelta = new Vector2(260f, 80f);
        stamp.rectTransform.anchoredPosition = Vector2.zero;
        stamp.rectTransform.localEulerAngles = new Vector3(0f, 0f, -8f);

        float t = 0f, dur = 0.34f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.3f, 1f, EaseOutBack(n));   // slam down
            SetAlpha(flash, 0.7f * (1f - n));
            yield return null;
        }
        stamp.rectTransform.localScale = Vector3.one;
        Destroy(flash.gameObject);

        // Settle: dim the slip to read as "taken".
        float f = 0f;
        while (f < 0.2f)
        {
            f += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(1f, 0.72f, f / 0.2f);
            yield return null;
        }
        cg.alpha = 0.72f;
    }

    // ---------------------------------------------------------------- helpers

    private static void SetAlpha(Image img, float a) { if (img != null) { Color c = img.color; c.a = a; img.color = c; } }

    private Image MakeImage(Transform parent, string n, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI MakeText(Transform parent, string n, string text, float size)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (titleText != null) t.font = titleText.font;
        t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        return t;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    private static Sprite GetGlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, rad = c;
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rad;
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return glowSprite;
    }
}
