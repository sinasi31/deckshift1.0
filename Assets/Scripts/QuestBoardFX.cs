using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Procedural presentation layer for the quest board (house style — sprites generated in code + cached,
// like RewardScreenFX / DeckViewUI). QuestSystem auto-adds this to the QuestBoardOverlay, so there's NO
// Inspector wiring and the painted board sprite / layout are untouched. On each open it fades in a modal
// dim + vignette + a warm torch-glow behind the board + drifting dust motes, and drops the board in with
// a bouncy EaseOutBack settle. Runs on unscaled time (the board pauses the game).
public class QuestBoardFX : MonoBehaviour
{
    [Header("Atmosphere")]
    public Color dimColor = new Color(0f, 0f, 0f, 0.62f);
    public Color vignetteColor = new Color(0f, 0f, 0f, 0.82f);
    public Color glowColor = new Color(1f, 0.72f, 0.34f, 1f);
    [Range(0f, 1f)] public float glowAlpha = 0.16f;
    public Color moteColor = new Color(1f, 0.85f, 0.55f, 1f);
    public int moteCount = 20;

    [Header("Entrance")]
    public float fadeTime = 0.2f;
    public float boardDropTime = 0.4f;

    private bool built;
    private RectTransform boardPanel;      // the painted board (overlay's original child)
    private CanvasGroup boardCG;
    private RectTransform backLayer;
    private Image dim, vignette, glow;
    private readonly List<RectTransform> motes = new List<RectTransform>();
    private readonly List<float> moteSpeed = new List<float>();
    private readonly List<float> motePhase = new List<float>();

    private static Sprite glowSprite, vignetteSprite;

    private void OnEnable()
    {
        if (!built) Build();
        StopAllCoroutines();
        StartCoroutine(IntroRoutine());
    }

    private void Build()
    {
        built = true;

        // The overlay's existing child is the painted board Panel — capture it before inserting the
        // backdrop behind it.
        if (transform.childCount > 0) boardPanel = transform.GetChild(0) as RectTransform;
        if (boardPanel != null)
        {
            boardCG = boardPanel.GetComponent<CanvasGroup>();
            if (boardCG == null) boardCG = boardPanel.gameObject.AddComponent<CanvasGroup>();
        }

        // Backdrop layer, behind the board.
        GameObject back = new GameObject("QuestBoardFX_Atmosphere", typeof(RectTransform));
        backLayer = back.GetComponent<RectTransform>();
        backLayer.SetParent(transform, false);
        Center(backLayer, 40f, 40f);
        backLayer.SetAsFirstSibling();

        // Oversized so they cover the screen regardless of the overlay's odd rect / any resolution.
        dim = MakeImage(backLayer, "Dim", null, dimColor, true);          // modal: blocks clicks behind
        Center(dim.rectTransform, 4000f, 2600f);
        vignette = MakeImage(backLayer, "Vignette", GetVignetteSprite(), vignetteColor, false);
        Center(vignette.rectTransform, 3400f, 2200f);
        glow = MakeImage(backLayer, "BoardGlow", GetGlowSprite(), new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha), false);
        Center(glow.rectTransform, 1700f, 1100f);

        // Dust motes.
        for (int i = 0; i < moteCount; i++)
        {
            float sz = Random.Range(3f, 9f);
            Image m = MakeImage(backLayer, "Mote", GetGlowSprite(), moteColor, false);
            Center(m.rectTransform, sz, sz);
            m.rectTransform.anchoredPosition = new Vector2(Random.Range(-720f, 720f), Random.Range(-440f, 440f));
            SetAlpha(m, Random.Range(0.2f, 0.6f));
            motes.Add(m.rectTransform);
            moteSpeed.Add(Random.Range(10f, 32f));
            motePhase.Add(Random.Range(0f, 6.28f));
        }
    }

    private IEnumerator IntroRoutine()
    {
        // Reset.
        if (dim != null) SetAlpha(dim, 0f);
        if (vignette != null) SetAlpha(vignette, 0f);
        if (glow != null) SetAlpha(glow, 0f);
        if (boardCG != null) boardCG.alpha = 0f;

        // Fade the atmosphere in.
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / fadeTime);
            if (dim != null) SetAlpha(dim, dimColor.a * n);
            if (vignette != null) SetAlpha(vignette, vignetteColor.a * n);
            if (glow != null) SetAlpha(glow, glowAlpha * n);
            yield return null;
        }
        if (dim != null) SetAlpha(dim, dimColor.a);
        if (vignette != null) SetAlpha(vignette, vignetteColor.a);
        if (glow != null) SetAlpha(glow, glowAlpha);

        // Drop the board in: bouncy scale + a small rotation settle + fade.
        if (boardPanel != null)
        {
            float d = 0f;
            while (d < boardDropTime)
            {
                d += Time.unscaledDeltaTime;
                float n = Mathf.Clamp01(d / boardDropTime);
                float s = Mathf.Lerp(0.82f, 1f, EaseOutBack(n));
                boardPanel.localScale = new Vector3(s, s, 1f);
                boardPanel.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-3.5f, 0f, EaseOut(n)));
                if (boardCG != null) boardCG.alpha = Mathf.Clamp01(n * 1.5f);
                yield return null;
            }
            boardPanel.localScale = Vector3.one;
            boardPanel.localEulerAngles = Vector3.zero;
            if (boardCG != null) boardCG.alpha = 1f;
        }
    }

    private void Update()
    {
        float ut = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        for (int i = 0; i < motes.Count; i++)
        {
            RectTransform m = motes[i];
            if (m == null) continue;
            Vector2 p = m.anchoredPosition;
            p.y += moteSpeed[i] * dt;
            p.x += Mathf.Sin(ut * 0.7f + motePhase[i]) * 7f * dt;
            if (p.y > 470f) { p.y = -470f; p.x = Random.Range(-720f, 720f); }
            m.anchoredPosition = p;
        }

        // Gentle torch-like pulse on the board glow.
        if (glow != null)
            SetAlpha(glow, glowAlpha * (0.8f + 0.2f * Mathf.Sin(ut * 2.3f) + 0.06f * Mathf.Sin(ut * 7.1f)));
    }

    // ---------------------------------------------------------------- helpers

    private static void Center(RectTransform rt, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void SetAlpha(Image img, float a) { if (img != null) { Color c = img.color; c.a = a; img.color = c; } }

    private Image MakeImage(Transform parent, string n, Sprite sprite, Color color, bool raycast)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = raycast;
        return img;
    }

    private static float EaseOut(float t) { float p = 1f - t; return 1f - p * p * p; }
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

    private static Sprite GetVignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;
        int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, rad = c;
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rad;   // 0 centre, 1 edge
                float a = Mathf.Clamp01((d - 0.42f) / 0.58f); a *= a;                // clear centre, dark edges
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return vignetteSprite;
    }
}
