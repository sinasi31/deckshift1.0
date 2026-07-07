using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Procedural styling + juice for one quest-tracker entry (house style — sprites generated in code and
// cached statically, like RelicIcon / DeckViewUI). Added to each instantiated QuestRowPrefab by
// QuestTrackerHUD; it disables the prefab's plain visuals and builds a sleek card (accent stripe +
// quest marker + title + progress bar), then animates: EaseOutBack pop-in, a pulse on progress, and a
// green completion celebration with the reward floating up before the row fades out.
//
// The tracker container is a VerticalLayoutGroup (ChildControl off), so animations use localScale +
// CanvasGroup alpha only (layout-safe) — never anchoredPosition.
public class QuestTrackerRow : MonoBehaviour
{
    private static readonly Color CardColor   = new Color(0.13f, 0.12f, 0.10f, 0.93f);
    private static readonly Color FrameColor  = new Color(0.82f, 0.62f, 0.30f, 0.55f);
    private static readonly Color AccentColor = new Color(1f, 0.80f, 0.40f, 1f);   // amber/gold
    private static readonly Color TrackColor  = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color DoneColor   = new Color(0.46f, 0.88f, 0.42f, 1f); // completion green

    private static Sprite panelSprite, framePanelSprite, barSprite, diamondSprite;

    private CanvasGroup cg;
    private RectTransform rt;
    private Image accentStripe, marker, fill, flash;
    private TextMeshProUGUI titleT, countT;
    private TMP_FontAsset font;

    private float fillCurrent, fillTarget;
    private bool popping; private float popT; const float POP_DUR = 0.34f;
    private bool pulsing; private float pulseT; const float PULSE_DUR = 0.28f;
    private bool completing;

    public void Build(string title, int current, int target)
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        // Inherit the game font from the prefab's text, then retire the prefab's plain visuals.
        TextMeshProUGUI existing = GetComponentInChildren<TextMeshProUGUI>(true);
        if (existing != null) font = existing.font;
        Image rootImg = GetComponent<Image>();
        if (rootImg != null) rootImg.enabled = false;
        foreach (TextMeshProUGUI t in GetComponentsInChildren<TextMeshProUGUI>(true))
            t.gameObject.SetActive(false);

        rt.sizeDelta = new Vector2(236f, 74f);

        // Card background + frame.
        Image bg = MakeImage(rt, "Card", GetPanelSprite(), CardColor); bg.type = Image.Type.Sliced;
        Stretch(bg.rectTransform);
        Image frame = MakeImage(rt, "Frame", GetFramePanelSprite(), FrameColor); frame.type = Image.Type.Sliced;
        Stretch(frame.rectTransform);

        // Left accent stripe.
        accentStripe = MakeImage(rt, "Accent", GetPanelSprite(), AccentColor); accentStripe.type = Image.Type.Sliced;
        Place(accentStripe.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, -12f), new Vector2(9f, 0f));

        // Quest marker diamond.
        marker = MakeImage(rt, "Marker", GetDiamondSprite(), AccentColor);
        Place(marker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, 20f), new Vector2(22f, -16f));

        // Title.
        titleT = MakeText(rt, "TitleT", title, 19f, TextAlignmentOptions.TopLeft);
        titleT.fontStyle = FontStyles.Bold; titleT.color = new Color(0.97f, 0.94f, 0.86f, 1f);
        titleT.enableWordWrapping = false; titleT.overflowMode = TextOverflowModes.Ellipsis;
        Place(titleT.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-58f, 26f), new Vector2(42f, -10f));

        // Progress track + fill.
        Image track = MakeImage(rt, "Track", GetBarSprite(), TrackColor); track.type = Image.Type.Sliced;
        Place(track.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(-58f, 12f), new Vector2(42f, 16f));
        fill = MakeImage(track.rectTransform, "Fill", GetBarSprite(), AccentColor); fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
        Stretch(fill.rectTransform);

        // Count text (right).
        countT = MakeText(rt, "CountT", "", 17f, TextAlignmentOptions.Right);
        countT.fontStyle = FontStyles.Bold; countT.color = AccentColor;
        Place(countT.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(52f, 0f), new Vector2(-8f, 2f));

        // A flash overlay used for progress pulses + completion (built once, kept transparent).
        flash = MakeImage(rt, "Flash", GetPanelSprite(), new Color(1f, 1f, 1f, 0f)); flash.type = Image.Type.Sliced;
        Stretch(flash.rectTransform);

        int t2 = Mathf.Max(1, target);
        fillTarget = fillCurrent = Mathf.Clamp01(current / (float)t2);
        fill.fillAmount = fillCurrent;
        countT.text = current + "/" + target;

        cg.alpha = 0f;
        transform.localScale = Vector3.one * 0.6f;
        popping = true; popT = 0f;
    }

    public void SetProgress(int current, int target)
    {
        if (completing) return;
        int t = Mathf.Max(1, target);
        fillTarget = Mathf.Clamp01(current / (float)t);
        if (countT != null) countT.text = current + "/" + target;
        pulsing = true; pulseT = 0f;
    }

    public void PlayComplete(string rewardText)
    {
        if (completing) return;
        completing = true;
        StartCoroutine(CompleteRoutine(rewardText));
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (popping)
        {
            popT += dt;
            float n = Mathf.Clamp01(popT / POP_DUR);
            transform.localScale = Vector3.one * Mathf.Max(0f, EaseOutBack(n));
            cg.alpha = n;
            if (n >= 1f) { popping = false; transform.localScale = Vector3.one; cg.alpha = 1f; }
        }

        // Ease the fill toward its target.
        if (fill != null && !Mathf.Approximately(fillCurrent, fillTarget))
        {
            fillCurrent = Mathf.MoveTowards(fillCurrent, fillTarget, dt * 1.4f);
            fill.fillAmount = fillCurrent;
        }

        if (pulsing)
        {
            pulseT += dt;
            float n = Mathf.Clamp01(pulseT / PULSE_DUR);
            float bump = Mathf.Sin(n * Mathf.PI);                 // 0→1→0
            if (!popping) transform.localScale = Vector3.one * (1f + 0.08f * bump);
            if (flash != null) SetAlpha(flash, 0.35f * bump);
            if (n >= 1f) { pulsing = false; if (!popping) transform.localScale = Vector3.one; if (flash != null) SetAlpha(flash, 0f); }
        }
    }

    private IEnumerator CompleteRoutine(string rewardText)
    {
        popping = false; pulsing = false;
        transform.localScale = Vector3.one;

        // Recolour to "done" green + snap the bar full.
        fillTarget = fillCurrent = 1f;
        if (fill != null) { fill.fillAmount = 1f; fill.color = DoneColor; }
        if (accentStripe != null) accentStripe.color = DoneColor;
        if (marker != null) marker.color = DoneColor;
        if (countT != null) { countT.text = "DONE"; countT.color = DoneColor; }

        // Green flash.
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.unscaledDeltaTime;
            if (flash != null) SetAlpha(flash, Mathf.Lerp(0.55f, 0f, t / 0.35f));
            yield return null;
        }
        if (flash != null) SetAlpha(flash, 0f);

        // Reward floats up and fades.
        if (!string.IsNullOrEmpty(rewardText))
        {
            TextMeshProUGUI rw = MakeText(rt, "Reward", "+ " + rewardText, 20f, TextAlignmentOptions.Center);
            rw.fontStyle = FontStyles.Bold; rw.color = DoneColor;
            Place(rw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(220f, 30f), Vector2.zero);
            float e = 0f;
            while (e < 1.1f)
            {
                e += Time.unscaledDeltaTime;
                rw.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 34f, e / 1.1f));
                SetAlpha(rw, Mathf.Clamp01(1.6f - e / 1.1f * 1.6f));
                yield return null;
            }
        }
        else
        {
            float h = 0f; while (h < 0.7f) { h += Time.unscaledDeltaTime; yield return null; }
        }

        // Shrink + fade out (layout collapses the gap).
        float f = 0f, dur = 0.3f;
        while (f < dur)
        {
            f += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(f / dur);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, n);
            cg.alpha = 1f - n;
            yield return null;
        }
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- helpers

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // anchorMin/Max/pivot + size + anchoredPosition, in one call.
    private static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    private static void SetAlpha(Graphic g, float a) { if (g != null) { Color c = g.color; c.a = a; g.color = c; } }

    private Image MakeImage(Transform parent, string n, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI MakeText(Transform parent, string n, string text, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.alignment = align; t.raycastTarget = false;
        return t;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    // --- procedural sprites (cached + shared) ---

    private static Sprite GetPanelSprite()
    {
        if (panelSprite != null) return panelSprite;
        panelSprite = BuildRounded(48, 48 * 0.32f, -1f);
        return panelSprite;
    }

    private static Sprite GetFramePanelSprite()
    {
        if (framePanelSprite != null) return framePanelSprite;
        framePanelSprite = BuildRounded(48, 48 * 0.32f, 48 * 0.06f);
        return framePanelSprite;
    }

    private static Sprite GetBarSprite()
    {
        if (barSprite != null) return barSprite;
        barSprite = BuildRounded(32, 16f, -1f);   // fully rounded pill
        return barSprite;
    }

    private static Sprite GetDiamondSprite()
    {
        if (diamondSprite != null) return diamondSprite;
        int s = 32; Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = s / 2f; Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - half) / half, dy = Mathf.Abs(y + 0.5f - half) / half;
                float a = Mathf.Clamp01((1f - (dx + dy)) * s * 0.25f);   // |x|+|y|<=1 rhombus, soft edge
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        diamondSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return diamondSprite;
    }

    private static Sprite BuildRounded(int s, float radius, float border)
    {
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdge(x, y, s, radius);
                float a;
                if (border < 0f) a = Mathf.Clamp01(d);
                else { float o = Mathf.Clamp01(d / 1.5f); float i = Mathf.Clamp01((border - d) / 1.5f); a = d < 0f ? 0f : Mathf.Min(o, i); }
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        float b = radius + 2f;
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    private static float RoundedRectEdge(int x, int y, int s, float radius)
    {
        float half = s / 2f;
        float px = x + 0.5f - half, py = y + 0.5f - half;
        float ax = Mathf.Abs(px) - (half - radius), ay = Mathf.Abs(py) - (half - radius);
        float outside = Mathf.Sqrt(Mathf.Max(ax, 0f) * Mathf.Max(ax, 0f) + Mathf.Max(ay, 0f) * Mathf.Max(ay, 0f));
        float inside = Mathf.Min(Mathf.Max(ax, ay), 0f);
        return radius - (outside + inside);
    }
}
