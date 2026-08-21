using UnityEngine;

/// <summary>
/// Real game art the character select is built from.
///
/// ⚠️ **THE NAME IS HISTORICAL AND ONLY `wallTexture` IS STILL CONSUMED.** This was authored for
/// theme **Vigil** — a hall of stone alcoves with a torch over each one — which the designer
/// rejected and which `CharacterSelectScreen` has since replaced with **Marquee**. Marquee is a
/// poster, not a room, so it wants a dark textured ground and nothing else: the alcove, plinth,
/// torch, flame, banner, beam, floor and grime slots below are **unused today**.
///
/// They are kept rather than deleted for two reasons. The class name and the asset path
/// (`Assets/Resources/VigilArt.asset`) are loaded by name at runtime, so renaming them is a live
/// risk to a lookup for zero visible benefit; and the references are already resolved, so a future
/// pass that wants a prop back gets it for free. **Do not read this file as a description of what
/// the screen currently looks like.**
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
