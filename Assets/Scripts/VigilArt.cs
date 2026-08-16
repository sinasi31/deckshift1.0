using UnityEngine;

/// <summary>
/// The real game art the character select (theme **Vigil**) is built from.
///
/// ⚠️ **This exists for the same reason `UITypeSettings` does: none of this art lives in a
/// `Resources/` folder.** It all ships inside `Assets/Cainos/Pixel Art Platformer - Dungeon/`, and
/// moving it to make `Resources.Load` work would be undone by the next pack reimport. So the
/// references are carried here instead, and rebuilt by **Deckshift → Rebuild Vigil Art**.
///
/// Must live at `Assets/Resources/VigilArt.asset` — `CharacterSelectScreen` loads it by that name.
///
/// ⚠️ **Every field is optional and the screen degrades per-field.** If this asset is missing, or a
/// slot is empty, the screen falls back to the procedural shapes it used before — a dressing pass
/// must never be able to leave the player unable to pick a character. Same rule as
/// `CharacterAppearance.Apply` catching everything.
/// </summary>
public class VigilArt : ScriptableObject
{
    [Header("Wall")]
    [Tooltip("TX Tileable - Dungeon Wall. ⚠️ The WHOLE 256x256 texture is one seamless 8x8 block — " +
             "that is what 'tileable' means here. Tiling any single 32px sub-sprite would repeat one " +
             "piece of a larger picture and read as a checkerboard.")]
    public Texture2D wallTexture;

    [Tooltip("A grime/stain overlay broken across the wall so it isn't mechanically regular.")]
    public Sprite wallDirt;

    [Header("Alcove")]
    [Tooltip("Pillar 01 A — flanks each alcove.")]
    public Sprite pillar;

    [Tooltip("Wall Cave 01 A — the dark recess a figure stands in.")]
    public Sprite recess;

    [Tooltip("Stage 01 — the plinth each figure stands on.")]
    public Sprite plinth;

    [Header("Light — the diegetic part")]
    [Tooltip("Torch 01 — the bracket. Present on EVERY alcove, lit on only one.")]
    public Sprite torch;

    [Tooltip("TX FX Torch Flame — the flame itself. Its alpha is driven by how lit the alcove is.")]
    public Sprite flame;

    [Header("Dressing")]
    [Tooltip("Banner 01 B — hung between alcoves for vertical rhythm.")]
    public Sprite banner;

    [Tooltip("Beam 01 — a timber across the ceiling.")]
    public Sprite beam;

    [Tooltip("Platform 01 — the floor strip the row stands on.")]
    public Sprite floor;

    /// <summary>True if there is enough here to dress the hall at all.</summary>
    public bool HasWall { get { return wallTexture != null; } }
}
