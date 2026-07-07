using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Procedural presentation layer for the end-of-level card reward screen (house style — sprites are
// generated in code and cached statically, like ChestOpenVFX / RelicIcon / EnemyHealthBar; no art, no
// prefab). Driven by RewardManager, which auto-adds this to the RewardScreen GameObject, so there is
// NO Inspector wiring required. Colours/tunables are public so the component CAN be added to the scene
// object by hand if the designer ever wants to tweak them (GetOrAdd reuses an existing one).
//
// It never touches the card transforms (CardUI owns each slot's scale/selection-lift) or the blocked
// CardTemplate prefab. The staggered "deal" reveal is done with a per-slot CanvasGroup + glow auras we
// own; atmosphere sits behind everything; decorations parent UNDER each card slot so they follow it.
public class RewardScreenFX : MonoBehaviour
{
    [Header("Atmosphere")]
    public Color centerGlowColor = new Color(1f, 0.82f, 0.42f, 1f);   // warm gold ambience behind the cards
    [Range(0f, 1f)] public float centerGlowAlpha = 0.16f;
    public Color vignetteColor = new Color(0f, 0f, 0f, 1f);
    [Range(0f, 1f)] public float vignetteStrength = 0.55f;
    public Color moteColor = new Color(1f, 0.9f, 0.62f, 1f);
    public int moteCount = 18;

    [Header("Cards")]
    public Color cardGlowColor = new Color(1f, 0.85f, 0.5f, 1f);      // soft halo behind each offered card
    [Range(0f, 1f)] public float cardGlowAlpha = 0.34f;
    public Color bonusColor = new Color(0.46f, 0.92f, 0.52f, 1f);     // shift-green for the "+1 SHIFT" card
    public float bonusBadgeYOffset = 82f;                             // how far the "+1 SHIFT" badge sits above the card

    [Header("Text")]
    public Color titleAccentColor = new Color(1f, 0.82f, 0.42f, 1f);
    public string subtitle = "Choose one to add to your deck";

    [Header("Timing")]
    public float fadeInTime = 0.22f;
    public float cardStagger = 0.09f;
    public float cardRevealTime = 0.26f;
    public float fadeOutTime = 0.24f;

    // --- runtime ---
    private CanvasGroup rootCg;
    private RectTransform backLayer;                 // atmosphere container (behind title + cards)
    private readonly List<RectTransform> motes = new List<RectTransform>();
    private readonly List<float> moteSpeed = new List<float>();
    private readonly List<float> motePhase = new List<float>();
    private RectTransform titleAccent;

    private readonly List<GameObject> transient = new List<GameObject>();  // rebuilt each intro
    private readonly List<Image> cardGlows = new List<Image>();
    private readonly List<CanvasGroup> slotGroups = new List<CanvasGroup>();
    private Image bonusGlow;
    private RectTransform bonusBadge;
    private bool atmosphereBuilt;
    private TMP_FontAsset font;

    private static Sprite glowSprite, ringSprite, pillSprite, vignetteSprite;

    // Called by RewardManager right after the offered cards are placed and the screen is shown.
    public void PlayIntro(IList<CardUI> slots, int activeCount, int bonusIndex)
    {
        StopAllCoroutines();
        EnsureFont();
        BuildAtmosphere();
        RebuildCardDecorations(slots, activeCount, bonusIndex);

        if (rootCg == null) rootCg = GetComponent<CanvasGroup>();
        StartCoroutine(IntroRoutine(activeCount));
    }

    // Called by RewardManager after the card is granted; fires onDone once the screen has faded out.
    public void PlaySelect(int index, Action onDone)
    {
        if (rootCg != null) rootCg.blocksRaycasts = false;
        StartCoroutine(SelectRoutine(index, onDone));
    }

    // ---------------------------------------------------------------- intro / select

    private IEnumerator IntroRoutine(int activeCount)
    {
        if (rootCg != null) { rootCg.alpha = 0f; rootCg.blocksRaycasts = true; }
        foreach (CanvasGroup g in slotGroups) if (g != null) g.alpha = 0f;
        foreach (Image g in cardGlows) if (g != null) SetAlpha(g, 0f);
        if (bonusBadge != null) bonusBadge.localScale = Vector3.zero;

        // Screen + atmosphere fade in.
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            if (rootCg != null) rootCg.alpha = Mathf.Clamp01(t / fadeInTime);
            yield return null;
        }
        if (rootCg != null) rootCg.alpha = 1f;

        // Deal the cards in one by one: fade the slot in while its halo pops behind it.
        for (int i = 0; i < activeCount; i++)
        {
            StartCoroutine(RevealCard(i));
            if (cardStagger > 0f)
            {
                float s = 0f;
                while (s < cardStagger) { s += Time.unscaledDeltaTime; yield return null; }
            }
        }
    }

    private IEnumerator RevealCard(int i)
    {
        CanvasGroup g = i < slotGroups.Count ? slotGroups[i] : null;
        Image glow = i < cardGlows.Count ? cardGlows[i] : null;
        bool isBonus = bonusBadge != null && bonusGlow == glow;
        float glowTarget = (isBonus ? 1f : cardGlowAlpha);

        float t = 0f;
        while (t < cardRevealTime)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / cardRevealTime);
            if (g != null) g.alpha = n;
            if (glow != null) SetAlpha(glow, glowTarget * n);
            float pop = Mathf.Max(0f, EaseOutBack(n));
            if (glow != null) glow.transform.localScale = Vector3.one * pop;
            if (isBonus && bonusBadge != null) bonusBadge.localScale = Vector3.one * pop;
            yield return null;
        }
        if (g != null) g.alpha = 1f;
        if (glow != null) glow.transform.localScale = Vector3.one;
        if (isBonus && bonusBadge != null) bonusBadge.localScale = Vector3.one;
    }

    private IEnumerator SelectRoutine(int index, Action onDone)
    {
        // Burst on the chosen slot: an expanding ring + a quick flash.
        RectTransform slot = ChosenSlot(index);
        if (slot != null)
        {
            Color burst = (bonusBadge != null && index == bonusIndexCache) ? bonusColor : cardGlowColor;
            StartCoroutine(BurstRoutine(slot, burst));
        }

        // Small beat so the burst reads, then fade the whole screen away (covers the room swap).
        float hold = 0.12f, t = 0f;
        while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }

        t = 0f;
        float startA = rootCg != null ? rootCg.alpha : 1f;
        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            if (rootCg != null) rootCg.alpha = Mathf.Lerp(startA, 0f, Mathf.Clamp01(t / fadeOutTime));
            yield return null;
        }
        if (rootCg != null) { rootCg.alpha = 1f; rootCg.blocksRaycasts = true; }  // reset for next time
        onDone?.Invoke();
    }

    private IEnumerator BurstRoutine(RectTransform slot, Color color)
    {
        float w = slot.rect.width > 1f ? slot.rect.width : 160f;
        Image ring = MakeChildImage(slot, "SelectRing", GetRingSprite(), color, w * 1.3f, w * 1.3f);
        ring.transform.SetAsLastSibling();
        Image flash = MakeChildImage(slot, "SelectFlash", GetGlowSprite(), color, w * 1.6f, w * 1.6f);
        flash.transform.SetAsLastSibling();
        transient.Add(ring.gameObject); transient.Add(flash.gameObject);

        float dur = 0.4f, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.1f, n);
            SetAlpha(ring, (1f - n) * 0.9f);
            SetAlpha(flash, (1f - n) * 0.7f);
            flash.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.4f, n);
            yield return null;
        }
    }

    private RectTransform ChosenSlot(int index)
    {
        // slotGroups are index-aligned to the active card slots.
        if (index >= 0 && index < slotGroups.Count && slotGroups[index] != null)
            return slotGroups[index].GetComponent<RectTransform>();
        return null;
    }

    // ---------------------------------------------------------------- build

    private int bonusIndexCache = -1;

    private void BuildAtmosphere()
    {
        if (atmosphereBuilt) return;
        atmosphereBuilt = true;

        GameObject back = new GameObject("RewardFX_Atmosphere", typeof(RectTransform));
        backLayer = back.GetComponent<RectTransform>();
        backLayer.SetParent(transform, false);
        Stretch(backLayer);
        backLayer.SetAsFirstSibling();   // behind the title + card container

        // Vignette darkens the edges so the cards read as the focal point.
        Image vig = MakeChildImage(backLayer, "Vignette", GetVignetteSprite(),
            new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, vignetteStrength), 0f, 0f);
        Stretch(vig.rectTransform);

        // Warm radial glow centred on the card row.
        Image cg = MakeChildImage(backLayer, "CenterGlow", GetGlowSprite(),
            new Color(centerGlowColor.r, centerGlowColor.g, centerGlowColor.b, centerGlowAlpha), 1400f, 900f);
        cg.rectTransform.anchoredPosition = new Vector2(0f, -40f);

        // Drifting motes.
        motes.Clear(); moteSpeed.Clear(); motePhase.Clear();
        for (int i = 0; i < moteCount; i++)
        {
            float sz = UnityEngine.Random.Range(4f, 11f);
            Image m = MakeChildImage(backLayer, "Mote", GetGlowSprite(), moteColor, sz, sz);
            RectTransform rt = m.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-900f, 900f), UnityEngine.Random.Range(-520f, 520f));
            SetAlpha(m, UnityEngine.Random.Range(0.25f, 0.7f));
            motes.Add(rt);
            moteSpeed.Add(UnityEngine.Random.Range(14f, 40f));
            motePhase.Add(UnityEngine.Random.Range(0f, 6.28f));
        }

        // Title accent bar + subtitle. The scene title ("Your Rewards") is 70pt in a strip at the very
        // top, so it visually reaches down to ~-95px — the accent/subtitle sit BELOW that to stay readable.
        Image acc = MakeChildImage(backLayer, "TitleAccent", GetGlowSprite(), titleAccentColor, 520f, 22f);
        acc.rectTransform.anchorMin = acc.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        acc.rectTransform.pivot = new Vector2(0.5f, 1f);
        acc.rectTransform.anchoredPosition = new Vector2(0f, -104f);
        titleAccent = acc.rectTransform;

        if (!string.IsNullOrEmpty(subtitle))
        {
            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform));
            RectTransform st = subGo.GetComponent<RectTransform>();
            st.SetParent(backLayer, false);
            st.anchorMin = st.anchorMax = new Vector2(0.5f, 1f);
            st.pivot = new Vector2(0.5f, 1f);
            st.sizeDelta = new Vector2(700f, 40f);
            st.anchoredPosition = new Vector2(0f, -122f);
            TextMeshProUGUI sub = subGo.AddComponent<TextMeshProUGUI>();
            if (font != null) sub.font = font;
            sub.text = subtitle;
            sub.fontSize = 26f;
            sub.alignment = TextAlignmentOptions.Center;
            sub.color = new Color(0.88f, 0.89f, 0.93f, 0.9f);
            sub.raycastTarget = false;
        }

        BuildViewDeckButton();
    }

    // A clickable "View Deck" button (bottom-centre). Opens the full-deck popup — this is the player's
    // way to inspect their deck now that the GameplayHUD (with its pile buttons) is hidden for clarity.
    private void BuildViewDeckButton()
    {
        GameObject go = new GameObject("ViewDeckButton", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);           // direct child of the screen → in front of the panel, clickable
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 70f);
        rt.sizeDelta = new Vector2(220f, 58f);

        Image pill = go.AddComponent<Image>();
        pill.sprite = GetPillSprite();
        pill.type = Image.Type.Sliced;
        pill.color = new Color(0.13f, 0.13f, 0.16f, 0.95f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = pill;
        btn.onClick.AddListener(() => { if (DeckViewUI.instance != null) DeckViewUI.instance.ShowFullDeck(); });

        // Soft gold rim.
        Image rim = MakeChildImage(rt, "Rim", GetPillSprite(), titleAccentColor, 220f, 58f);
        rim.type = Image.Type.Sliced;
        rim.rectTransform.sizeDelta = new Vector2(228f, 66f);
        SetAlpha(rim, 0.28f);
        rim.transform.SetAsFirstSibling();

        GameObject t = new GameObject("Label", typeof(RectTransform));
        RectTransform trt = t.GetComponent<RectTransform>();
        trt.SetParent(rt, false);
        Stretch(trt);
        TextMeshProUGUI txt = t.AddComponent<TextMeshProUGUI>();
        if (font != null) txt.font = font;
        txt.text = "VIEW DECK";
        txt.fontSize = 24f;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.92f, 0.92f, 0.96f, 1f);
        txt.raycastTarget = false;
    }

    // Rebuilt every time the screen opens (bonus index and active count can change).
    private void RebuildCardDecorations(IList<CardUI> slots, int activeCount, int bonusIndex)
    {
        for (int i = 0; i < transient.Count; i++) if (transient[i] != null) Destroy(transient[i]);
        transient.Clear();
        cardGlows.Clear();
        slotGroups.Clear();
        bonusGlow = null; bonusBadge = null;
        bonusIndexCache = bonusIndex;

        for (int i = 0; i < activeCount && i < slots.Count; i++)
        {
            CardUI slot = slots[i];
            if (slot == null) continue;
            RectTransform slotRt = slot.GetComponent<RectTransform>();
            bool isBonus = (i == bonusIndex);

            // Per-slot CanvasGroup lets us fade the whole card in without fighting CardUI's transform.
            CanvasGroup cg = slot.GetComponent<CanvasGroup>();
            if (cg == null) cg = slot.gameObject.AddComponent<CanvasGroup>();
            slotGroups.Add(cg);

            // Halo behind the card (child of the slot so it follows the selection-lift).
            float w = slotRt != null && slotRt.rect.width > 1f ? slotRt.rect.width : 160f;
            float h = slotRt != null && slotRt.rect.height > 1f ? slotRt.rect.height : 240f;
            Color gc = isBonus ? bonusColor : cardGlowColor;
            Image glow = MakeChildImage(slotRt, "CardGlow", GetGlowSprite(), gc, w * 1.55f, h * 1.35f);
            glow.transform.SetAsFirstSibling();
            SetAlpha(glow, 0f);
            transient.Add(glow.gameObject);
            cardGlows.Add(glow);
            if (isBonus) bonusGlow = glow;

            if (isBonus) BuildBonusBadge(slotRt, w);
        }
    }

    private void BuildBonusBadge(RectTransform slot, float slotWidth)
    {
        GameObject go = new GameObject("BonusBadge", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(slot, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, bonusBadgeYOffset);
        rt.sizeDelta = new Vector2(Mathf.Max(120f, slotWidth * 0.9f), 46f);
        transient.Add(go);
        bonusBadge = rt;

        // Soft glow behind the pill.
        Image glow = MakeChildImage(rt, "BadgeGlow", GetGlowSprite(), bonusColor, rt.sizeDelta.x * 1.5f, rt.sizeDelta.y * 2.4f);
        SetAlpha(glow, 0.5f);

        // Rounded pill plate (dark green so the light label pops).
        Image pill = MakeChildImage(rt, "Pill", GetPillSprite(), new Color(0.08f, 0.16f, 0.09f, 0.96f), rt.sizeDelta.x, rt.sizeDelta.y);
        pill.type = Image.Type.Sliced;

        // Label.
        GameObject txtGo = new GameObject("Label", typeof(RectTransform));
        RectTransform trt = txtGo.GetComponent<RectTransform>();
        trt.SetParent(rt, false);
        Stretch(trt);
        TextMeshProUGUI txt = txtGo.AddComponent<TextMeshProUGUI>();
        if (font != null) txt.font = font;
        txt.text = "+1 SHIFT";
        txt.fontSize = 26f;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.85f, 1f, 0.86f, 1f);
        txt.raycastTarget = false;
    }

    // ---------------------------------------------------------------- idle motion

    private void Update()
    {
        float ut = Time.unscaledTime;

        // Motes drift upward and wrap.
        for (int i = 0; i < motes.Count; i++)
        {
            RectTransform m = motes[i];
            if (m == null) continue;
            Vector2 p = m.anchoredPosition;
            p.y += moteSpeed[i] * Time.unscaledDeltaTime;
            p.x += Mathf.Sin(ut * 0.6f + motePhase[i]) * 8f * Time.unscaledDeltaTime;
            if (p.y > 560f) { p.y = -560f; p.x = UnityEngine.Random.Range(-900f, 900f); }
            m.anchoredPosition = p;
        }

        // Title accent shimmer.
        if (titleAccent != null)
        {
            Image img = titleAccent.GetComponent<Image>();
            if (img != null) SetAlpha(img, 0.5f + 0.25f * Mathf.Sin(ut * 2f));
        }

        // Bonus card halo breathes + badge bobs so the +1 SHIFT reads as special.
        if (bonusGlow != null) SetAlpha(bonusGlow, 0.75f + 0.25f * Mathf.Sin(ut * 3.2f));
        if (bonusBadge != null && bonusBadge.localScale.x > 0.99f)
            bonusBadge.anchoredPosition = new Vector2(0f, bonusBadgeYOffset + Mathf.Sin(ut * 3.5f) * 4f);
    }

    // ---------------------------------------------------------------- helpers

    private void EnsureFont()
    {
        if (font != null) return;
        TextMeshProUGUI existing = GetComponentInChildren<TextMeshProUGUI>(true);
        if (existing != null) font = existing.font;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        Color c = img.color; c.a = a; img.color = c;
    }

    private Image MakeChildImage(RectTransform parent, string n, Sprite sprite, Color color, float w, float h)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        if (w > 0f || h > 0f) rt.sizeDelta = new Vector2(w, h);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    // --- procedural sprites (cached + shared) ---

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

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, outer = c - 1f, band = s * 0.09f, inner = outer - band, feather = 2f;
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float ao = Mathf.Clamp01((outer - d) / feather);
                float ai = Mathf.Clamp01((d - inner) / feather);
                float a = Mathf.Min(ao, ai);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return ringSprite;
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
                float a = Mathf.Clamp01((d - 0.45f) / 0.55f); a *= a;                // clear centre, dark edges
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return vignetteSprite;
    }

    // Rounded-rect pill for the bonus badge (9-slice so it stretches cleanly to any width).
    private static Sprite GetPillSprite()
    {
        if (pillSprite != null) return pillSprite;
        int s = 64; float radius = s * 0.5f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdge(x, y, s, radius);
                float a = Mathf.Clamp01(d);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        pillSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return pillSprite;
    }

    private static float RoundedRectEdge(int x, int y, int s, float radius)
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
}
