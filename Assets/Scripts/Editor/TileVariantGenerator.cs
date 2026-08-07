using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Generates recoloured copies of existing dungeon tiles.
//
// WHY THIS EXISTS. Two problems with the Cainos ground set as shipped:
//
//   1. DEEP INTERIORS. The set is a FACING tileset — edges and near-edge detail — and it was never
//      drawn to be a fill. Painted across a mass 8-10 cells deep it reads as repeating wallpaper.
//      An earlier attempt left those cells unpainted so the backdrop showed through, but that is
//      worse: solid rock then looks like open background, which is actively misleading while
//      playing and while peeking with Ctrl.
//
//   2. PLATFORMS BLEND INTO WALLS. Playtest feedback. Platform tiles and wall tiles are the same
//      value, so a ledge disappears against the mass behind it.
//
// Both are VALUE problems, not shape problems, so both are fixed by tinting rather than by new art.
//
// ⚠️ WHY DUPLICATES INSTEAD OF TINTING IN PLACE: every tile in the pack ships with
// `TileFlags.LockColor`, which makes Tilemap.color and Tilemap.SetColor no-ops on it. A duplicate
// Tile asset pointing at the SAME sprite with its own `color` renders tinted with no shader work,
// no texture edits, and no risk to the hand-made rooms that use the originals.
//
// Menu: Deckshift -> Generate Tile Variants.
public static class TileVariantGenerator
{
    public const string VariantFolder = "Assets/LevelGenerated/TileVariants";

    // Deep rock: pushed down and slightly cool so a mass reads as receding shadow rather than as
    // another lit surface.
    //
    // ⚠️ DON'T GO AS DARK AS IT LOOKS LIKE YOU SHOULD. These tiles render through
    // Sprite-Lit-Default with a global Light2D at 0.5 intensity, so the scene ALREADY halves them.
    // A 0.42 tint measured out at ~0.21 on screen and the deep mass came out a flat black hole,
    // which reads as a pit rather than as rock. Multiply your intended value by the light, then
    // pick the tint.
    public static readonly Color DeepTint = new Color(0.72f, 0.74f, 0.80f, 1f);

    // Platforms: lifted and warmed so a ledge separates from the wall behind it.
    public static readonly Color PlatformTint = new Color(1.14f, 1.10f, 1.00f, 1f);

    private static readonly string[] DeepSources =
    {
        "Assets/LevelSinasi/biseyler/TX Tileset - Dungeon Ground Extra_153.asset",
        "Assets/LevelSinasi/biseyler/TX Tileset - Dungeon Ground Extra_185.asset",
        "Assets/LevelSinasi/biseyler/TX Tileset - Dungeon Ground Extra_101.asset",
        "Assets/LevelSinasi/biseyler/TX Tileset - Dungeon Ground Extra_49.asset",
    };

    private const string CainosDir = "Assets/Cainos/Pixel Art Platformer - Dungeon/Tileset Pallete/TP Dungeon Ground/";
    private static readonly string[] PlatformSources =
    {
        CainosDir + "TX Tileset - Dungeon Ground_11.asset",
        CainosDir + "TX Tileset - Dungeon Ground Dirt_0.asset",
        CainosDir + "TX Tileset - Dungeon Ground Dirt_14.asset",
        CainosDir + "TX Tileset - Dungeon Ground Dirt_4.asset",
        CainosDir + "TX Tileset - Dungeon Ground_3.asset",
        CainosDir + "TX Tileset - Dungeon Ground_11.asset",
        CainosDir + "TX Tileset - Dungeon Ground Dirt_12.asset",
        CainosDir + "TX Tileset - Dungeon Ground Dirt_3.asset",
        CainosDir + "TX Tileset - Dungeon Ground_1.asset",
        CainosDir + "TX Tileset - Dungeon Ground_0.asset",
    };

    [MenuItem("Deckshift/Generate Tile Variants")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(VariantFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/LevelGenerated"))
                AssetDatabase.CreateFolder("Assets", "LevelGenerated");
            AssetDatabase.CreateFolder("Assets/LevelGenerated", "TileVariants");
        }

        int made = 0;
        var missing = new List<string>();
        foreach (string src in DeepSources) if (!MakeVariant(src, "Deep", DeepTint, ref made)) missing.Add(src);
        foreach (string src in PlatformSources) if (!MakeVariant(src, "Lit", PlatformTint, ref made)) missing.Add(src);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"Generated/updated {made} tile variant(s) in {VariantFolder}.";
        if (missing.Count > 0) msg += "\n\nMISSING SOURCES:\n  " + string.Join("\n  ", missing.ToArray());
        Debug.Log("[TileVariantGenerator] " + msg);
    }

    // Returns false if the source tile could not be loaded.
    private static bool MakeVariant(string sourcePath, string suffix, Color tint, ref int made)
    {
        var src = AssetDatabase.LoadAssetAtPath<Tile>(sourcePath);
        if (src == null) return false;

        string outPath = Path.Combine(VariantFolder,
            Path.GetFileNameWithoutExtension(sourcePath) + " " + suffix + ".asset").Replace("\\", "/");

        var existing = AssetDatabase.LoadAssetAtPath<Tile>(outPath);
        Tile t = existing != null ? existing : ScriptableObject.CreateInstance<Tile>();

        t.sprite = src.sprite;                 // same art, different value
        t.color = tint;
        t.colliderType = src.colliderType;     // collision must not change
        t.transform = src.transform;
        t.gameObject = src.gameObject;
        // Keep LockColor so the tile's own colour is authoritative and a Tilemap can't wash it out.
        t.flags = TileFlags.LockColor;

        if (existing == null) AssetDatabase.CreateAsset(t, outPath);
        else EditorUtility.SetDirty(t);

        made++;
        return true;
    }

    // Path of the variant for a source tile NAME (no folder, no extension), or null if absent.
    public static string VariantPath(string sourceFileName, string suffix)
    {
        string p = VariantFolder + "/" + sourceFileName + " " + suffix + ".asset";
        return AssetDatabase.LoadAssetAtPath<Tile>(p) != null ? p : null;
    }
}
