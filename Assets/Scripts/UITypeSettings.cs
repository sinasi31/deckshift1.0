using TMPro;
using UnityEngine;

/// <summary>
/// The two font assets <see cref="UIType"/> uses, held in an asset so they can be loaded at runtime.
///
/// ⚠️ **This exists because neither font lives in a `Resources/` folder** — Pixie ships inside the
/// Cainos pack (`Assets/Cainos/Common/Font/`) and CCBattleScarred sits in `Assets/LevelEfeVrl/
/// Sprites/`. Moving either one to make `Resources.Load` work would risk a pack reimport putting it
/// back, so the references are carried here instead. Same pattern as `RelicCatalogue` and
/// `CardCatalogue`, and it is rebuilt the same way (**Deckshift → Rebuild UI Type**).
///
/// Must live at `Assets/Resources/UIType.asset` — `UIType` loads it by that name.
/// </summary>
public class UITypeSettings : ScriptableObject
{
    [Tooltip("Titles, headings, menu items, buttons, stat labels, numbers. The game's voice.")]
    public TMP_FontAsset displayFont;

    [Tooltip("Running sentences only — descriptions, barks, card rules. Needs true lowercase.")]
    public TMP_FontAsset bodyFont;
}
