using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The character select — theme **VIGIL**.
//
// A cold stone hall of alcoves with the roster standing dormant in them. One warm lamp travels
// along the row to whoever you are on; that character wakes and starts breathing while the rest
// stay frozen in the dark.
//
// ⚠️ THE INVERSION IS THE LIGHT. Every other screen in this game is an evenly lit surface that
// marks its selection with COLOUR — a red ring, a cyan plate, a brighter sigil. This one leaves the
// whole hall dark and marks the selection by LIGHTING IT. Light direction is the one axis the
// theme table had spare, and it costs no hue at all, which matters because the palette budget is
// nearly spent (orange, violet, tan, amber, frost, arc-cyan and wax red are all claimed).
//
// ⚠️ THE SECOND SIGNAL IS MOTION, AND IT IS FREE. Unselected rigs have `animator.speed = 0`, so
// they are literally statues; the chosen one is the only moving thing on screen. Same idea as the
// quest board, where hovering a slip STOPS its sway — there stillness marks the choice, here it
// marks everything else.
public class CharacterSelectScreen : MonoBehaviour
{
    private static CharacterSelectScreen instance;

    public static bool IsOpen => instance != null && instance.isOpen;

    private bool isOpen;
    private System.Action onConfirmed;

    private readonly List<CharacterData> roster = new List<CharacterData>();
    private readonly List<Alcove> alcoves = new List<Alcove>();
    private int index;

    private RectTransform content;
    private CanvasGroup group;
    private RectTransform lamp;          // the travelling light
    private RectTransform lampGlow;
    private TextMeshProUGUI nameText, traitName, traitBody, hint;
    private RectTransform deckRow;
    private Button beginButton;

    private float lampX;                 // eased toward the selected alcove
    private float flare;                 // confirm burst
    private int openedFrame = -999;

    // ---- palette -----------------------------------------------------------------------------
    // Cold, nearly colourless stone; the ONLY warmth on screen is the lamp. That contrast is what
    // makes a lit alcove read as chosen without a single accent colour.
    private static readonly Color Stone     = new Color(0.055f, 0.058f, 0.072f, 1f);
    private static readonly Color StoneDeep = new Color(0.028f, 0.030f, 0.040f, 1f);
    private static readonly Color StoneEdge = new Color(0.135f, 0.140f, 0.165f, 1f);
    private static readonly Color Lamp      = new Color(1f, 0.80f, 0.52f, 1f);
    private static readonly Color TextLit   = new Color(0.97f, 0.92f, 0.84f, 1f);
    private static readonly Color TextDim   = new Color(0.42f, 0.44f, 0.52f, 1f);

    private const int PORTRAIT_W = 260, PORTRAIT_H = 380;
    private const float ALCOVE_W = 300f, ALCOVE_H = 430f;
    private const float ALCOVE_GAP = 44f;
    // Tuned together: at ROW_Y 128 / info 36 there was ~170px of dead black between the ledges and
    // the name, and the screen read as two unrelated halves rather than one hall.
    private const float ROW_Y = 110f;
    private const float INFO_Y = 110f;

    private class Alcove
    {
        public CharacterData data;
        public CharacterStagePortrait portrait;
        public RectTransform root;
        public Image niche, floor, rim;
        public Image torch, flame;          // the diegetic light — flame burns only on the chosen one
        public RawImage figure;
        public TextMeshProUGUI label;
        public float lit;                // 0..1, eased
        public float x;
    }

    // Real game art (see VigilArt). Null is a supported state — every use is guarded and the screen
    // falls back to the procedural shapes it had before.
    private VigilArt art;
    private static Sprite wallSprite;
    private static Sprite[] flameFrames;

    /// <summary>
    /// The torch flame, sliced out of the pack's FX sheet.
    ///
    /// ⚠️ **`TX FX Torch Flame` is an UNSLICED 128x128 ANIMATION SHEET, not one flame.** Unity
    /// reports it as a single sprite, so using it whole draws a grid of ~36 tiny flames — which is
    /// exactly what the first pass rendered above each torch and it looked like specks of dust.
    ///
    /// Measured by blitting it to a RenderTexture and reading the alpha (the source is not
    /// readable): six clean empty columns, and six uneven horizontal bands whose coverage rises to
    /// the middle and tapers away — a flame animation growing and shrinking. **Row 2 (y 42–64) is
    /// the one band with content in all six columns** (0.17–0.21 coverage), so that row is a clean
    /// six-frame cycle of full-size flames. Cycling one known-good row beats slicing all 36 and
    /// hoping the grid is exact — a misaligned frame would read as the flame sliding sideways.
    /// </summary>
    private static Sprite[] FlameFrames(Sprite sheet)
    {
        if (flameFrames != null) return flameFrames;
        if (sheet == null) return null;

        Texture2D tex = sheet.texture;
        const int COLS = 6;
        float fw = tex.width / (float)COLS;

        flameFrames = new Sprite[COLS];
        for (int c = 0; c < COLS; c++)
            flameFrames[c] = Sprite.Create(tex, new Rect(c * fw, 42f, fw, 22f),
                                           new Vector2(0.5f, 0.15f), 100f);
        return flameFrames;
    }

    // ⚠️ Measured on screen, not computed. The pack wall is lit for a torch-lit room; at anything
    // near full value it becomes a bright grey field, flattens the screen and destroys the one thing
    // this theme has — the lamp being the only light in it.
    private static readonly Color WallTint = new Color(0.34f, 0.34f, 0.39f, 1f);

    // ------------------------------------------------------------------ open / close

    public static void Open(System.Action onConfirmed)
    {
        if (instance == null)
        {
            Canvas canvas = FindRootCanvas();
            if (canvas == null)
            {
                // ⚠️ Never strand the player on a dead menu button. Same rule the run map follows:
                // if the screen cannot be shown, do the thing it was going to do.
                Debug.LogWarning("CharacterSelectScreen: no Canvas — starting the run without a pick.");
                onConfirmed?.Invoke();
                return;
            }

            var go = new GameObject("CharacterSelectScreen", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            instance = go.AddComponent<CharacterSelectScreen>();
            instance.Build();
        }
        instance.Show(onConfirmed);
    }

    /// <summary>
    /// The Canvas this screen belongs on — the root Screen-Space-Overlay one.
    ///
    /// ⚠️ This used to be `FindFirstObjectByType&lt;Canvas&gt;()`, which is only safe in a scene that
    /// happens to contain exactly one Canvas. It worked in the main menu and was silently broken
    /// everywhere else: a gameplay scene carries a WORLD-SPACE Canvas per enemy health bar (18 of
    /// them in SampleScene), and `FindFirstObjectByType` returned one of those. The screen then
    /// built itself inside a health bar at 0.01 scale — measured 76.7x24 units — so it rendered
    /// invisibly while `Open` still reported success and armed its confirm callback.
    ///
    /// Same helper `ShopScreenUI` already uses; keep the two in step.
    /// </summary>
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

    private void Show(System.Action cb)
    {
        onConfirmed = cb;
        isOpen = true;
        openedFrame = Time.frameCount;
        content.gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // Start on the last character played, so a repeat run is one keypress.
        index = 0;
        for (int i = 0; i < roster.Count; i++)
            if (roster[i] == CharacterSelection.Chosen) index = i;

        lampX = alcoves.Count > 0 ? alcoves[index].x : 0f;
        flare = 0f;
        RefreshInfo();
        StartCoroutine(FadeIn());
    }

    private void Hide()
    {
        isOpen = false;
        content.gameObject.SetActive(false);
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 2.2f;
            group.alpha = Mathf.Clamp01(t);
            yield return null;
        }
        group.alpha = 1f;
    }

    // ------------------------------------------------------------------ build

    private void Build()
    {
        var rt = GetComponent<RectTransform>();
        Stretch(rt);

        content = NewRect(rt, "Content");
        Stretch(content);
        group = content.gameObject.AddComponent<CanvasGroup>();

        art = Resources.Load<VigilArt>("VigilArt");

        BuildHall();
        BuildAlcoves();
        BuildLamp();
        BuildInfo();

        Title();
    }

    private void Title()
    {
        // House voice: the game does not take itself too seriously, and a row of figures waiting
        // their turn in the dark is funnier read as a queue than as a solemn rite.
        var t = AddText(content, "Title", "WHO'S UP?", 40f, TextDim, TextAlignmentOptions.Center);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        t.rectTransform.pivot = new Vector2(0.5f, 1f);
        t.rectTransform.anchoredPosition = new Vector2(0f, -52f);
        t.rectTransform.sizeDelta = new Vector2(900f, 52f);
        t.characterSpacing = 12f;
    }

    /// <summary>
    /// The hall, built from the game's OWN dungeon art rather than flat colour.
    ///
    /// ⚠️ **THE WALL IS ONE SEAMLESS 8x8 PICTURE, NOT A REPEATING TILE.** `TX Tileable - Dungeon
    /// Wall` is a 256x256 block the artist drew to tile as a whole; its 64 sub-sprites are pieces of
    /// that picture, and tiling any single one repeats a fragment and reads as a checkerboard. This
    /// is the same mistake that made generated rooms never look like the hand-made ones — the level
    /// importer held six of the sixty-four pieces and scattered them. So the WHOLE texture is used as
    /// one tiled sprite: correct by construction, and one Image instead of ~500 cells.
    ///
    /// ⚠️ **It is tinted DEEP.** Vigil is near-black with one warm lamp, and the pack wall is lit for
    /// a torch-lit room. Left at full value it is a bright grey field that flattens the whole screen
    /// and kills the only thing this theme has — the lamp being the sole light. Tint measured on
    /// screen, not computed (see the linear-colour-space rule).
    /// </summary>
    private void BuildHall()
    {
        // Base coat behind everything, so a missing texture still gives stone rather than nothing.
        Image back = AddImage(content, "Hall", FlatUI.Pixel(), Stone, true);
        Stretch(back.rectTransform);

        if (art != null && art.wallTexture != null)
        {
            Texture2D tex = art.wallTexture;
            if (wallSprite == null)
                wallSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                           new Vector2(0.5f, 0.5f), 100f);

            Image wall = AddImage(content, "WallTiles", wallSprite, WallTint, false);
            wall.type = Image.Type.Tiled;      // ⚠️ never Simple — Simple stretches one block over the screen
            Stretch(wall.rectTransform);

            // Grime, in the outer margins the alcoves never occupy.
            //
            // ⚠️ NEAR NATIVE SIZE. The sprite is 38x27; the first pass blew it to 420x420 — a 15x
            // enlargement — and a stain that big stopped reading as a stain and became angular
            // shapes that looked like broken masonry floating in mid-air. Pixel art enlarged past
            // ~2x stops being the thing it is a picture of.
            if (art.wallDirt != null)
            {
                float[] gx = { -820f, 800f, -700f, 880f };
                float[] gy = {  300f, 210f, -140f,-260f };
                for (int i = 0; i < gx.Length; i++)
                {
                    Image d = AddImage(content, "Grime" + i, art.wallDirt,
                                       new Color(1f, 1f, 1f, 0.05f), false);
                    d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    d.rectTransform.sizeDelta = new Vector2(76f, 54f);
                    d.rectTransform.anchoredPosition = new Vector2(gx[i], gy[i]);
                }
            }

            // A timber across the ceiling — one horizontal that stops the top being an empty void.
            if (art.beam != null)
            {
                Image b = AddImage(content, "Beam", art.beam, new Color(0.30f, 0.28f, 0.30f, 1f), false);
                b.type = Image.Type.Tiled;
                b.rectTransform.anchorMin = new Vector2(0f, 1f);
                b.rectTransform.anchorMax = new Vector2(1f, 1f);
                b.rectTransform.pivot = new Vector2(0.5f, 1f);
                b.rectTransform.offsetMin = new Vector2(0f, -40f);
                b.rectTransform.offsetMax = new Vector2(0f, -8f);
            }

            // The floor the row stands on, so the alcoves are in a place rather than on a field.
            if (art.floor != null)
            {
                Image f = AddImage(content, "Floor", art.floor, new Color(0.26f, 0.25f, 0.27f, 1f), false);
                f.type = Image.Type.Tiled;
                f.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                f.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                f.rectTransform.pivot = new Vector2(0.5f, 1f);
                f.rectTransform.sizeDelta = new Vector2(0f, 26f);
                f.rectTransform.anchoredPosition = new Vector2(0f, ROW_Y - ALCOVE_H * 0.5f + 30f);
            }
        }

        // Darkening from the top so the eye falls to the row. Drawn OVER the wall — the texture
        // needs to fade into black at the ceiling, not sit at uniform value behind a gradient.
        Image ceiling = AddImage(content, "Ceiling", FlatUI.VerticalFade(), StoneDeep, false);
        ceiling.rectTransform.anchorMin = new Vector2(0f, 0.42f);
        ceiling.rectTransform.anchorMax = new Vector2(1f, 1f);
        ceiling.rectTransform.offsetMin = ceiling.rectTransform.offsetMax = Vector2.zero;

        // Banners hang BETWEEN alcoves, in the gaps — vertical rhythm in the one band the layout
        // leaves empty at any roster size.
        if (art != null && art.banner != null)
        {
            int n = Mathf.Max(1, CharacterRoster.All.Count);
            float step = ALCOVE_W + ALCOVE_GAP;
            for (int i = 0; i <= n; i++)
            {
                float x = -(n * 0.5f) * step + i * step;
                Image bn = AddImage(content, "Banner" + i, art.banner,
                                    new Color(0.34f, 0.20f, 0.20f, 0.85f), false);
                bn.rectTransform.anchorMin = bn.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                bn.rectTransform.pivot = new Vector2(0.5f, 1f);
                bn.rectTransform.sizeDelta = new Vector2(64f, 126f);
                bn.rectTransform.anchoredPosition = new Vector2(x, ROW_Y + ALCOVE_H * 0.5f + 34f);
            }
        }
    }

    private void BuildAlcoves()
    {
        roster.Clear();
        foreach (CharacterData c in CharacterRoster.All) if (c != null) roster.Add(c);

        int n = Mathf.Max(1, roster.Count);
        float step = ALCOVE_W + ALCOVE_GAP;
        float first = -(n - 1) * 0.5f * step;

        for (int i = 0; i < n; i++)
        {
            var a = new Alcove { data = roster[i], x = first + i * step };

            a.root = NewRect(content, "Alcove_" + a.data.characterName);
            a.root.anchorMin = a.root.anchorMax = new Vector2(0.5f, 0.5f);
            a.root.pivot = new Vector2(0.5f, 0.5f);
            a.root.anchoredPosition = new Vector2(a.x, ROW_Y);
            a.root.sizeDelta = new Vector2(ALCOVE_W, ALCOVE_H);

            // The niche: a recess cut into the wall, darker than the wall around it. Still the
            // procedural arch — it is the hit target and the shape that reads at any roster size,
            // and the pack has no alcove sprite this tall.
            a.niche = AddImage(a.root, "Niche", ArchSprite(), StoneDeep, true);
            a.niche.type = Image.Type.Simple;
            Stretch(a.niche.rectTransform);

            // Real cave-mouth art INSIDE the arch, so the recess is cut stone rather than a shape.
            if (art != null && art.recess != null)
            {
                Image rec = AddImage(a.root, "Recess", art.recess, new Color(0.16f, 0.16f, 0.19f, 0.9f), false);
                rec.rectTransform.anchorMin = rec.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                rec.rectTransform.pivot = new Vector2(0.5f, 0f);
                rec.rectTransform.sizeDelta = new Vector2(ALCOVE_W * 0.92f, 150f);
                rec.rectTransform.anchoredPosition = new Vector2(0f, 26f);
            }

            // A hairline of catch-light around the arch — without it the recess has no edge at all
            // against a wall this dark, and the row reads as flat rectangles.
            a.rim = AddImage(a.root, "Rim", ArchRimSprite(), StoneEdge, false);
            Stretch(a.rim.rectTransform);

            // ⚠️ NO PILLARS HERE, AND THAT IS A DECISION. `Pillar 01 A` was tried both stretched
            // (a 3x vertical smear of a carved column) and TILED, and tiling an ATLASED sprite
            // repeats the pillar's capital rather than its shaft — both read as brackets stuck to
            // the wall at chest height, not as columns. The arch, banners, plinth and torch already
            // carry the architecture, so forcing one more prop to be something it is not drawn as
            // would have cost more than it paid. If a real column is wanted later it needs a
            // purpose-drawn shaft sprite, not this one repeated.

            a.portrait = CharacterStagePortrait.Create(a.data, i, PORTRAIT_W, PORTRAIT_H);
            a.figure = AddRaw(a.root, "Figure", a.portrait != null ? a.portrait.Texture : null);
            // ⚠️ A RawImage with no texture draws a SOLID WHITE QUAD, which tinted by the shadow
            // colour came out as a big blue slab where a character should be. A half-authored
            // character must leave an empty alcove, not a coloured box.
            if (a.portrait == null) a.figure.enabled = false;
            a.figure.rectTransform.anchorMin = a.figure.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            a.figure.rectTransform.pivot = new Vector2(0.5f, 0f);
            a.figure.rectTransform.anchoredPosition = new Vector2(0f, 42f);
            a.figure.rectTransform.sizeDelta = new Vector2(PORTRAIT_W, PORTRAIT_H);

            // The ledge each figure stands on, so they are in the alcove rather than floating.
            // A real stone plinth where we have one; the flat bar is the fallback.
            if (art != null && art.plinth != null)
            {
                a.floor = AddImage(a.root, "Plinth", art.plinth, new Color(0.34f, 0.33f, 0.36f, 1f), false);
                a.floor.rectTransform.anchorMin = a.floor.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                a.floor.rectTransform.pivot = new Vector2(0.5f, 0f);
                a.floor.rectTransform.sizeDelta = new Vector2(ALCOVE_W * 0.80f, 52f);
                a.floor.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            }
            else
            {
                a.floor = AddImage(a.root, "Ledge", FlatUI.Pixel(), StoneEdge, false);
                a.floor.rectTransform.anchorMin = new Vector2(0.08f, 0f);
                a.floor.rectTransform.anchorMax = new Vector2(0.92f, 0f);
                a.floor.rectTransform.pivot = new Vector2(0.5f, 0f);
                a.floor.rectTransform.offsetMin = new Vector2(0f, 30f);
                a.floor.rectTransform.offsetMax = new Vector2(0f, 34f);
            }

            // ⚠️ THE TORCH IS THE POINT OF THIS PASS. Vigil's inversion is that light marks the
            // selection, and until now that light had no SOURCE — it was a glow sprite floating in
            // the dark. Every alcove now carries a real bracket, and only the chosen one burns, so
            // the light is a thing in the room instead of a UI effect. The flame's alpha and scale
            // are driven by `a.lit` in Update, which means the fire travels the row with the
            // selection rather than cutting.
            if (art != null && art.torch != null)
            {
                // 8x35 native. Held near 2x — pixel art pushed much past that stops reading as the
                // object it is a picture of (the 15x grime stain in the first pass proved it).
                a.torch = AddImage(a.root, "Torch", art.torch, new Color(0.34f, 0.32f, 0.32f, 1f), false);
                a.torch.rectTransform.anchorMin = a.torch.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                a.torch.rectTransform.pivot = new Vector2(0.5f, 1f);
                a.torch.rectTransform.sizeDelta = new Vector2(18f, 78f);
                a.torch.rectTransform.anchoredPosition = new Vector2(0f, -4f);

                Sprite[] frames = FlameFrames(art.flame);
                if (frames != null && frames[0] != null)
                {
                    a.flame = AddImage(a.root, "Flame", frames[0], new Color(1f, 1f, 1f, 0f), false);
                    a.flame.rectTransform.anchorMin = a.flame.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    a.flame.rectTransform.pivot = new Vector2(0.5f, 0f);   // grows UPWARD from the bracket
                    a.flame.rectTransform.sizeDelta = new Vector2(52f, 56f);
                    a.flame.rectTransform.anchoredPosition = new Vector2(0f, -10f);
                }
            }

            a.label = AddText(a.root, "Name", a.data.characterName.ToUpperInvariant(), 22f, TextDim,
                              TextAlignmentOptions.Center);
            a.label.rectTransform.anchorMin = a.label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            a.label.rectTransform.pivot = new Vector2(0.5f, 1f);
            a.label.rectTransform.anchoredPosition = new Vector2(0f, 22f);
            a.label.rectTransform.sizeDelta = new Vector2(ALCOVE_W, 30f);
            a.label.characterSpacing = 6f;

            int captured = i;
            var btn = a.root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = a.niche;
            btn.onClick.AddListener(() =>
            {
                // Clicking the one you are already on confirms it — the second click of a
                // double-click lands on the same alcove, so picking and starting is one gesture.
                if (index == captured) Confirm();
                else index = captured;
            });

            a.lit = 0f;
            alcoves.Add(a);
        }
    }

    private void BuildLamp()
    {
        // Drawn UNDER the figures so the light is in the alcove with them, not a wash over the top.
        lampGlow = NewRect(content, "LampGlow");
        lampGlow.anchorMin = lampGlow.anchorMax = new Vector2(0.5f, 0.5f);
        // ⚠️ Kept close to the alcove it belongs to. At 2.1x/2.0x this spilled a soft oval halfway
        // down the screen and over the info panel, which reads as a vignette on the whole page
        // rather than as a lamp lighting one niche — the light stops being a POSITION.
        lampGlow.sizeDelta = new Vector2(ALCOVE_W * 1.45f, ALCOVE_H * 1.25f);
        var glow = lampGlow.gameObject.AddComponent<Image>();
        glow.sprite = FlatUI.SoftGlow();
        glow.color = new Color(Lamp.r, Lamp.g, Lamp.b, 0.20f);
        glow.raycastTarget = false;
        lampGlow.SetSiblingIndex(2);

        // The beam: a shaft of light falling into the niche from above.
        lamp = NewRect(content, "Beam");
        lamp.anchorMin = lamp.anchorMax = new Vector2(0.5f, 0.5f);
        lamp.sizeDelta = new Vector2(ALCOVE_W * 0.92f, ALCOVE_H * 1.25f);
        var beam = lamp.gameObject.AddComponent<Image>();
        beam.sprite = BeamSprite();
        beam.color = new Color(Lamp.r, Lamp.g, Lamp.b, 0.13f);
        beam.raycastTarget = false;
        lamp.SetSiblingIndex(3);

        // Dust hanging in the shaft. Warm, slow, no twinkle — it is lit air, not sparks.
        UIEmberField.Attach(lamp, 26, new Color(1f, 0.88f, 0.66f, 1f), UIEmberField.Settings.Dust);
    }

    private void BuildInfo()
    {
        var panel = NewRect(content, "Info");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, INFO_Y);
        panel.sizeDelta = new Vector2(1000f, 250f);

        nameText = AddText(panel, "Name", "", 52f, TextLit, TextAlignmentOptions.Center);
        nameText.rectTransform.anchorMin = nameText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        nameText.rectTransform.pivot = new Vector2(0.5f, 1f);
        nameText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        nameText.rectTransform.sizeDelta = new Vector2(1000f, 62f);
        nameText.characterSpacing = 8f;

        var rule = AddImage(panel, "Rule", FlatUI.FadedRule(), new Color(Lamp.r, Lamp.g, Lamp.b, 0.30f), false);
        rule.rectTransform.anchorMin = rule.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rule.rectTransform.pivot = new Vector2(0.5f, 1f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        rule.rectTransform.sizeDelta = new Vector2(520f, 2f);

        traitName = AddText(panel, "TraitName", "", 26f, Lamp, TextAlignmentOptions.Center);
        traitName.rectTransform.anchorMin = traitName.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        traitName.rectTransform.pivot = new Vector2(0.5f, 1f);
        traitName.rectTransform.anchoredPosition = new Vector2(0f, -74f);
        traitName.rectTransform.sizeDelta = new Vector2(900f, 32f);

        traitBody = AddText(panel, "TraitBody", "", 21f, TextDim, TextAlignmentOptions.Center);
        traitBody.rectTransform.anchorMin = traitBody.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        traitBody.rectTransform.pivot = new Vector2(0.5f, 1f);
        traitBody.rectTransform.anchoredPosition = new Vector2(0f, -108f);
        traitBody.rectTransform.sizeDelta = new Vector2(900f, 30f);

        // ⚠️ The starting deck is drawn with the REAL card faces. It is the single biggest
        // difference between two characters, and a list of names would make the screen a spec
        // sheet — a player who has played before recognises those cards on sight.
        deckRow = NewRect(panel, "Deck");
        deckRow.anchorMin = deckRow.anchorMax = new Vector2(0.5f, 1f);
        deckRow.pivot = new Vector2(0.5f, 1f);
        deckRow.anchoredPosition = new Vector2(0f, -148f);
        deckRow.sizeDelta = new Vector2(1000f, 116f);

        hint = AddText(content, "Hint", "← →  CHOOSE     ENTER  BEGIN     ESC  BACK",
                       18f, new Color(TextDim.r, TextDim.g, TextDim.b, 0.75f), TextAlignmentOptions.Center);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hint.rectTransform.pivot = new Vector2(0.5f, 0f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, 12f);
        hint.rectTransform.sizeDelta = new Vector2(900f, 24f);
        hint.characterSpacing = 6f;
    }

    // ------------------------------------------------------------------ per-frame

    private void Update()
    {
        if (!isOpen) return;

        HandleKeys();

        // ⚠️ THE PANEL IS DERIVED FROM `index`, NOT PUSHED AT IT. `RefreshInfo` self-guards on
        // `lastShown`, so calling it every frame is free — and it is the only way the name, trait and
        // starting deck cannot fall out of step with the lamp. They already had: the alcove CLICK
        // handler sets `index` directly and never refreshed, so clicking a character moved the light
        // onto them while the panel kept describing the previous one. Requiring every mutation site
        // to remember is the same losing pattern as a hand-kept list of `IsOpen` flags.
        RefreshInfo();

        // ⚠️ Unscaled throughout. This screen can be reached with the game paused behind it, and on
        // scaled time every one of these would sit frozen at its resting value.
        float dt = Time.unscaledDeltaTime;

        // The lamp SLIDES rather than cutting. The travel is the whole conceit — you are carrying
        // a light down a row of alcoves, and a hard cut would just be a highlight moving.
        float targetX = alcoves.Count > 0 ? alcoves[Mathf.Clamp(index, 0, alcoves.Count - 1)].x : 0f;
        lampX = Mathf.Lerp(lampX, targetX, 1f - Mathf.Exp(-9f * dt));

        if (lamp != null) lamp.anchoredPosition = new Vector2(lampX, ROW_Y + 40f);
        if (lampGlow != null) lampGlow.anchoredPosition = new Vector2(lampX, ROW_Y);

        if (flare > 0f) flare = Mathf.Max(0f, flare - dt * 1.6f);
        float flarePunch = 1f + flare * 1.4f;
        if (lampGlow != null)
        {
            lampGlow.localScale = Vector3.one * flarePunch;
            var img = lampGlow.GetComponent<Image>();
            img.color = new Color(Lamp.r, Lamp.g, Lamp.b, 0.16f + flare * 0.55f);
        }

        for (int i = 0; i < alcoves.Count; i++)
        {
            Alcove a = alcoves[i];

            // How lit an alcove is falls off with its distance from the lamp, so a neighbour catches
            // a little spill. A hard on/off would make the light a spotlight cursor rather than a
            // lamp in a room.
            float d = Mathf.Abs(a.x - lampX) / (ALCOVE_W + ALCOVE_GAP);
            float want = Mathf.Clamp01(1f - d * 1.15f);
            a.lit = Mathf.Lerp(a.lit, want, 1f - Mathf.Exp(-10f * dt));

            float k = a.lit * a.lit;                       // squared: the falloff should be steep
            if (a.figure != null)
            {
                float v = Mathf.Lerp(0.20f, 1f, k);
                // Shadowed figures go COLD as well as dark. Pure darkening reads as a turned-down
                // brightness slider; a blue shift reads as "out of the lamplight".
                a.figure.color = new Color(v * 0.86f, v * 0.90f, Mathf.Lerp(0.34f, 1f, k), 1f);
                a.figure.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.97f, 1.0f, k);
            }
            if (a.niche != null)
                a.niche.color = Color.Lerp(StoneDeep, new Color(0.13f, 0.115f, 0.10f, 1f), k * 0.9f);
            if (a.rim != null)
                a.rim.color = Color.Lerp(StoneEdge, Lamp, k * 0.55f);
            if (a.label != null)
                a.label.color = Color.Lerp(TextDim, TextLit, k);

            // The torch: cold iron when dark, catching its own firelight when lit.
            if (a.torch != null)
                a.torch.color = Color.Lerp(new Color(0.30f, 0.29f, 0.31f, 1f),
                                           new Color(1f, 0.86f, 0.62f, 1f), k * 0.75f);

            // The flame. ⚠️ It must actually ANIMATE — a fire held on one frame at a steady alpha
            // reads as a decal of a fire. It cycles the sheet's six frames, and each torch runs on
            // its own offset so the row doesn't flicker in unison like a strobe. The small scale
            // wobble on top keeps it from looping visibly at six frames.
            if (a.flame != null)
            {
                Sprite[] frames = flameFrames;
                float t = Time.unscaledTime;
                if (frames != null && frames.Length > 0)
                {
                    int off = i * 2;                                    // per-torch phase offset
                    int f = (Mathf.FloorToInt(t * 12f) + off) % frames.Length;
                    if (frames[f] != null) a.flame.sprite = frames[f];
                }
                float wob = 1f + 0.06f * Mathf.Sin(t * 9.1f + i * 2.3f);
                // Tinted toward the lamp colour: the sheet's flame is near-white at its core, which
                // on its own reads as a candle rather than a torch.
                a.flame.color = new Color(1f, 0.86f, 0.62f, k * 0.95f);
                a.flame.rectTransform.localScale = new Vector3(1f, wob, 1f) * Mathf.Lerp(0.75f, 1f, k);
            }

            // Only the chosen figure BREATHES. Everything else is a statue.
            if (a.portrait != null) a.portrait.SetAwake(i == index);
        }
    }

    private void HandleKeys()
    {
        // ⚠️ IGNORE KEYS ON THE FRAME THIS OPENED. The main menu's PLAY button can be activated with
        // Enter or Space, and that same press is still down when this screen's first Update runs —
        // so the screen would confirm the default character instantly and flash past. Exactly the
        // trap PauseScreen has to guard against with Escape, arriving from the other direction.
        if (Time.frameCount <= openedFrame + 1) return;

        int move = 0;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) move = 1;
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) move = -1;

        if (move != 0 && alcoves.Count > 0)
        {
            index = Mathf.Clamp(index + move, 0, alcoves.Count - 1);
            RefreshInfo();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space)) Confirm();

        if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); }
    }

    private int lastShown = -1;

    private void RefreshInfo()
    {
        if (index == lastShown || index < 0 || index >= roster.Count) return;
        lastShown = index;

        CharacterData c = roster[index];
        nameText.text = c.characterName.ToUpperInvariant();
        traitName.text = c.traitName.ToUpperInvariant();
        traitBody.text = c.traitDescription;

        BuildDeckRow(c);
    }

    private void BuildDeckRow(CharacterData c)
    {
        for (int i = deckRow.childCount - 1; i >= 0; i--)
        {
            var child = deckRow.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        const float H = 112f;
        float w = H / CardFace.ASPECT;
        int n = c.startingDeck != null ? c.startingDeck.Count : 0;
        float gap = 10f;
        float first = -(n - 1) * 0.5f * (w + gap);

        for (int i = 0; i < n; i++)
        {
            CardData card = c.startingDeck[i];
            if (card == null) continue;

            var cell = NewRect(deckRow, "Card" + i);
            cell.anchorMin = cell.anchorMax = new Vector2(0.5f, 0.5f);
            cell.pivot = new Vector2(0.5f, 0.5f);
            cell.anchoredPosition = new Vector2(first + i * (w + gap), 0f);
            cell.sizeDelta = new Vector2(w, H);
            CardFace.Build(cell, new RuntimeCard(card));
        }
    }

    private void Confirm()
    {
        if (!isOpen || index < 0 || index >= roster.Count) return;

        CharacterSelection.Chosen = roster[index];
        flare = 1f;
        StartCoroutine(ConfirmRoutine());
    }

    // The lamp flares, the hall washes out, and the run begins. Deliberately short — this is the
    // last thing between the player and playing.
    private IEnumerator ConfirmRoutine()
    {
        isOpen = false;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 2.6f;
            group.alpha = 1f - Mathf.Clamp01(t) * 0.15f;
            yield return null;
        }

        System.Action cb = onConfirmed;
        onConfirmed = null;
        Hide();
        cb?.Invoke();
    }

    // ------------------------------------------------------------------ procedural art

    private static Sprite arch, archRim, beamSprite;

    // A recess with an arched top — the silhouette that says "this is a niche in a wall" rather
    // than "this is a card".
    private static Sprite ArchSprite() { return arch != null ? arch : (arch = BuildArch(false)); }
    private static Sprite ArchRimSprite() { return archRim != null ? archRim : (archRim = BuildArch(true)); }

    private static Sprite BuildArch(bool rimOnly)
    {
        const int W = 128, H = 184;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        float radius = W * 0.5f;
        float springLine = H - radius;      // where the straight sides give way to the arch

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float dxEdge;
                if (y <= springLine) dxEdge = Mathf.Min(x, W - 1 - x);
                else
                {
                    float dx = x - radius, dy = y - springLine;
                    dxEdge = radius - Mathf.Sqrt(dx * dx + dy * dy);
                }

                float a;
                if (rimOnly)
                {
                    // A band hugging the inside of the opening.
                    a = Mathf.Clamp01(1f - Mathf.Abs(dxEdge - 3f) / 3.0f);
                }
                else
                {
                    a = Mathf.Clamp01(dxEdge / 2f);
                    // Deeper toward the back of the recess, so it reads as having depth.
                    a *= Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(dxEdge / 26f));
                }

                tex.SetPixel(x, y, a <= 0.004f ? new Color(0, 0, 0, 0) : new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
    }

    // A shaft of light: brightest at the top, spreading and fading downward.
    private static Sprite BeamSprite()
    {
        if (beamSprite != null) return beamSprite;

        const int W = 128, H = 192;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        for (int y = 0; y < H; y++)
        {
            float v = y / (float)(H - 1);          // 0 bottom, 1 top
            float spread = Mathf.Lerp(0.98f, 0.40f, v);
            float strength = Mathf.Pow(v, 0.75f);
            for (int x = 0; x < W; x++)
            {
                float u = Mathf.Abs(x / (float)(W - 1) - 0.5f) * 2f;
                float across = Mathf.Clamp01(1f - u / spread);
                float a = across * across * strength;
                tex.SetPixel(x, y, a <= 0.003f ? new Color(0, 0, 0, 0) : new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        beamSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        return beamSprite;
    }

    // ------------------------------------------------------------------ small builders

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static Image AddImage(Transform parent, string name, Sprite s, Color c, bool raycast)
    {
        var rt = NewRect(parent, name);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var img = rt.gameObject.AddComponent<Image>();
        if (s != null) img.sprite = s;
        img.color = c;
        img.raycastTarget = raycast;
        return img;
    }

    private static RawImage AddRaw(Transform parent, string name, Texture tex)
    {
        var rt = NewRect(parent, name);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI AddText(Transform parent, string name, string text, float size,
                                           Color c, TextAlignmentOptions align)
    {
        var rt = NewRect(parent, name);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = c;
        t.alignment = align;
        t.raycastTarget = false;

        // ⚠️ Every other screen in the game routes its text through this. Missing it here is why the
        // select screen rendered in TMP's default Liberation Sans while the rest of the game is in
        // the pixel face — an opt-in convention that silently drops any screen that forgets it.
        TMP_FontAsset font = FlatUI.UIFont();
        if (font != null) t.font = font;

        return t;
    }

    private void OnDestroy()
    {
        foreach (Alcove a in alcoves)
            if (a.portrait != null) Destroy(a.portrait.gameObject);
        if (instance == this) instance = null;
    }
}
