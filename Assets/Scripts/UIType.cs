using TMPro;
using UnityEngine;

/// <summary>
/// The size scale. Roles, not numbers — a screen asks for a Title, it does not pick 48.
///
/// Every screen currently invents its own sizes (the pause screen alone uses 13, 14, 15, 17, 30 and
/// 78), which is why nothing lines up between screens and why "make the body text bigger" has never
/// been a single edit.
/// </summary>
public enum TextRole
{
    /// <summary>The one big word on a screen that takes the whole frame. Pause's "PAUSED".</summary>
    Hero,
    /// <summary>A normal screen title — "CONTRACTS", "THE OXIDATION DISTRICT".</summary>
    Title,
    /// <summary>Section headings and menu items.</summary>
    Heading,
    /// <summary>Stat labels, column headers, button text.</summary>
    Label,
    /// <summary>Running text: descriptions, barks, card rules.</summary>
    Body,
    /// <summary>Hints, footers, fine print.</summary>
    Caption,
}

/// <summary>
/// Deckshift's typography, stated in one place.
///
/// ── Why this exists ──────────────────────────────────────────────────────────────────────────
///
/// The UI font used to be decided by **census**: `FlatUI.UIFont()` counted every `TMP_Text` in the
/// scene and returned whichever font was most common. That is not a decision, it is an emergent
/// property — it does a full `FindObjectsByType` per call, it can legitimately answer differently in
/// MainMenu than in SampleScene, and any screen that forgot to call it silently fell out of the
/// system (the character select shipped in Liberation Sans exactly this way).
///
/// ── The two faces, and the split (designer, 2026-08-16) ──────────────────────────────────────
///
/// **Display = CCBattleScarred.** Rough, inked, heavy, essentially all-caps. This is the game's
/// voice and it keeps every title, heading, menu item, button, stat label and number.
///
/// **Prose = Pixie.** Clean, true lowercase, readable small. It takes ONLY real sentences —
/// contract descriptions, card rules, shopkeeper barks, trait blurbs.
///
/// The split was measured on the quest board, which is the screen that discriminates because it is
/// the one with sentences in it. In the display face a contract reads
/// `CLEAR 4 ROOMS IN A ROW WITHOUT PLAYING STAGGER.` — every contract shouts. In the prose face it
/// reads `Clear 4 rooms in a row without playing Stagger.`, which is what the Bulletin theme is
/// *about*: someone wrote this and pinned it up. The pause screen, by contrast, barely
/// discriminates at all — it is 30 labels and numbers and almost no prose, so do not judge a type
/// decision on it.
///
/// ⚠️ **Prose text is auto-compensated by `ProseScale`.** Pixie has a smaller cap height, so at
/// equal nominal pt it renders visibly smaller than the display face. `SizeFor` applies the factor
/// so a Body is the same optical size whichever face draws it — never hand-tune a size to
/// compensate, or the two faces drift apart again.
///
/// ⚠️ **Pixie is thin on LIGHT grounds.** On the Bulletin theme's pale paper it wants a darker ink
/// than the value you would pick for the same text on a dark plate. See the linear-colour-space
/// note in the `deckshift-ui` skill: measure it, don't compute it.
///
/// ── Migration policy ─────────────────────────────────────────────────────────────────────────
///
/// ⚠️ **Do NOT retrofit every screen at once.** New screens use this immediately; existing ones
/// migrate when they are already being touched for another reason. `FlatUI.UIFont()` now delegates
/// here and returns the DISPLAY face, which is exactly what the census was already resolving to —
/// so wiring this in changed nothing on screen, and each screen's prose can move over one at a time.
/// </summary>
public static class UIType
{
    /// <summary>
    /// Multiplier applied to prose sizes so Pixie matches the display face optically. Measured by
    /// eye against CCBattleScarred at 15–19pt on the pause and quest screens.
    /// </summary>
    public const float ProseScale = 1.18f;

    private static UITypeSettings settings;
    private static bool searched;

    private static UITypeSettings Settings()
    {
        if (settings != null) return settings;
        if (!searched)
        {
            searched = true;
            settings = Resources.Load<UITypeSettings>("UIType");
            if (settings == null)
                Debug.LogWarning("UIType: no Assets/Resources/UIType.asset — falling back to the old " +
                                 "font census. Run Deckshift → Rebuild UI Type to create it.");
        }
        return settings;
    }

    /// <summary>
    /// The display face: titles, headings, menu items, buttons, stat labels, numbers.
    /// Falls back to the legacy census so a missing settings asset degrades instead of breaking.
    /// </summary>
    public static TMP_FontAsset Display()
    {
        UITypeSettings s = Settings();
        if (s != null && s.displayFont != null) return s.displayFont;
        return LegacyCensus();
    }

    /// <summary>
    /// The prose face: running sentences only. Falls back to the display face, because a screen
    /// rendered entirely in the display face is today's look — correct, if shouty — whereas a screen
    /// rendered in TMP's default Liberation Sans is a visible bug.
    /// </summary>
    public static TMP_FontAsset Prose()
    {
        UITypeSettings s = Settings();
        if (s != null && s.bodyFont != null) return s.bodyFont;
        return Display();
    }

    /// <summary>Point size for a role. <paramref name="prose"/> applies <see cref="ProseScale"/>.</summary>
    public static float SizeFor(TextRole role, bool prose = false)
    {
        float b;
        switch (role)
        {
            case TextRole.Hero:    b = 64f; break;
            case TextRole.Title:   b = 48f; break;
            case TextRole.Heading: b = 34f; break;
            case TextRole.Label:   b = 24f; break;
            case TextRole.Body:    b = 19f; break;
            default:               b = 16f; break;   // Caption
        }
        return prose ? Mathf.Round(b * ProseScale) : b;
    }

    /// <summary>Set a label in the DISPLAY face at a role's size.</summary>
    public static void Apply(TMP_Text t, TextRole role)
    {
        if (t == null) return;
        TMP_FontAsset f = Display();
        if (f != null) t.font = f;
        t.fontSize = SizeFor(role, false);
    }

    /// <summary>Set running text in the PROSE face, size auto-compensated.</summary>
    public static void ApplyProse(TMP_Text t, TextRole role = TextRole.Body)
    {
        if (t == null) return;
        TMP_FontAsset f = Prose();
        if (f != null) t.font = f;
        t.fontSize = SizeFor(role, true);
    }

    /// <summary>
    /// The pre-2026-08-16 behaviour: whichever font the scene uses most.
    ///
    /// ⚠️ Kept ONLY as a fallback for a missing settings asset. Do not call it directly and do not
    /// reintroduce it as the primary path — it is a full scene scan per call, and it makes the
    /// project's typography an accident of which screens happen to be instantiated.
    /// </summary>
    private static TMP_FontAsset LegacyCensus()
    {
        var counts = new System.Collections.Generic.Dictionary<TMP_FontAsset, int>();
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || t.font == null) continue;
            counts.TryGetValue(t.font, out int n);
            counts[t.font] = n + 1;
        }

        TMP_FontAsset best = null;
        int bestCount = 0;
        foreach (var kv in counts)
        {
            if (kv.Value > bestCount ||
                (kv.Value == bestCount && best != null && kv.Key.GetInstanceID() < best.GetInstanceID()))
            {
                best = kv.Key;
                bestCount = kv.Value;
            }
        }

        return best != null ? best : TMP_Settings.defaultFontAsset;
    }
}
