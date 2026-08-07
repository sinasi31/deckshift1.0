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

    // Deep rock, in three steps of recession.
    //
    // ⚠️ THE TINT MUST NEUTRALISE HUE, NOT JUST DARKEN. Measured average colour of the sheet:
    //
    //     surface tiles  (162/144/154)   0.27, 0.25, 0.24   luma 0.25   R-B 0.028  (near neutral)
    //     interior tiles (153/185/101/49) 0.24, 0.18, 0.16  luma 0.19   R-B 0.077  (strongly BROWN)
    //
    // The interior tiles are inherently browner AND darker than the surface stone. The hand-made
    // rooms hide this because their interiors are only 1-2 cells deep — you see a thin brown line
    // and read it as shadow under a ledge. Across a mass 8 cells deep it becomes a brown slab
    // against grey stone, which is the "tiles don't belong" the designer reported. Simply
    // darkening made it worse: a darker brown is still brown.
    //
    // So each tint below is source-relative: it divides out the brown and multiplies back the
    // SURFACE tiles' hue ratio at a lower luma, so deep rock is the same stone in shadow rather
    // than a different material.
    //
    // ⚠️ AND DON'T GO AS DARK AS YOU THINK. These render through Sprite-Lit-Default under a
    // 0.5-intensity global Light2D, so the scene already halves them. A 0.42 tint measured ~0.21
    // on screen and the mass came out a flat black hole.
    //
    // Three steps rather than one because a single cut-off at depth 3 left a hard horizontal seam
    // where the grey stopped and the dark began. Rock should recede, not change material.
    public static readonly Color[] DeepTints =
    {
        new Color(0.77f, 0.91f, 0.98f, 1f),   // depth 3 — just inside the face
        new Color(0.67f, 0.79f, 0.85f, 1f),   // depth 4
        new Color(0.58f, 0.68f, 0.74f, 1f),   // depth 5+ — the core of a mass
    };

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

    // ---- flattened deep-rock textures -----------------------------------------------------------
    //
    // The deep tiles carry small light-grey brick details drawn at DIFFERENT positions in each
    // tile. Repeat them across a mass and they never line up, so the wall reads as scattered brick
    // fragments instead of one surface — the designer's "if they were somehow in-sync it might be
    // better". Syncing them is not possible without redrawing the set.
    //
    // But deep rock should not have legible detail in the first place: it is metres of stone in
    // shadow. So instead of trying to align the bricks, this DELETES them — it compresses each
    // tile's value range toward its own dark base, which erases the bright brick faces while
    // keeping the faint mottling that stops a fill looking like flat colour. No detail, no
    // repetition artefact.
    //
    // Written as new PNG assets rather than modifying the pack, so the originals (and every
    // hand-made room using them) are untouched.
    private const string FlatTextureFolder = VariantFolder + "/Textures";

    // 0 = perfectly flat, 1 = original. Low on purpose: at 0.35 the bricks were still legible
    // enough to tile visibly.
    private const float DetailRetention = 0.16f;

    private static Sprite FlattenSprite(Sprite src, string outName)
    {
        string outPath = FlatTextureFolder + "/" + outName + ".png";

        Rect r = src.textureRect;
        int w = (int)r.width, h = (int)r.height;
        Color[] px = src.texture.GetPixels((int)r.x, (int)r.y, w, h);

        // The tile's own base tone: the 25th-percentile luminance, so bright brick faces don't drag
        // it up and the darkest mortar lines don't drag it down.
        var lums = new List<float>(px.Length);
        foreach (var p in px) if (p.a > 0.5f) lums.Add(0.299f * p.r + 0.587f * p.g + 0.114f * p.b);
        if (lums.Count == 0) return null;
        lums.Sort();
        float baseLum = lums[Mathf.Clamp(lums.Count / 4, 0, lums.Count - 1)];

        var outPx = new Color[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            Color p = px[i];
            if (p.a <= 0.5f) { outPx[i] = p; continue; }
            float lum = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
            // Pull每 pixel toward the base tone, keeping a sliver of the original variation.
            float target = baseLum + (lum - baseLum) * DetailRetention;
            float k = lum > 0.001f ? target / lum : 1f;
            outPx[i] = new Color(p.r * k, p.g * k, p.b * k, p.a);
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(outPx);
        tex.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);

        var imp = AssetImporter.GetAtPath(outPath) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;       // pixel art — bilinear would smear it
            imp.spritePixelsPerUnit = src.pixelsPerUnit;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
    }

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

        // Deep rock: flatten each source ONCE (the brick detail is what tiles badly), then make
        // one tinted Tile per recession step pointing at that flattened sprite.
        var flattened = new Dictionary<string, Sprite>();
        bool[] restore = PrepareReadable(DeepSources);
        foreach (string src in DeepSources)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(src);
            if (tile == null || tile.sprite == null) { missing.Add(src); continue; }
            string flatName = Path.GetFileNameWithoutExtension(src) + " Flat";
            Sprite s = FlattenSprite(tile.sprite, flatName);
            if (s != null) flattened[src] = s;
        }
        RestoreReadable(DeepSources, restore);

        for (int step = 0; step < DeepTints.Length; step++)
            foreach (string src in DeepSources)
            {
                Sprite flat;
                if (!flattened.TryGetValue(src, out flat)) continue;
                MakeVariant(src, "Deep" + (step + 1), DeepTints[step], ref made, flat);
            }

        foreach (string src in PlatformSources) if (!MakeVariant(src, "Lit", PlatformTint, ref made)) missing.Add(src);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"Generated/updated {made} tile variant(s) in {VariantFolder}.";
        if (missing.Count > 0) msg += "\n\nMISSING SOURCES:\n  " + string.Join("\n  ", missing.ToArray());
        Debug.Log("[TileVariantGenerator] " + msg);
    }

    // Sprite pixels can only be read when the source texture is marked readable, which the pack's
    // textures are not. Flip it for the duration and put it back — leaving it on doubles the
    // texture's memory cost at runtime.
    private static bool[] PrepareReadable(string[] tilePaths)
    {
        var was = new bool[tilePaths.Length];
        for (int i = 0; i < tilePaths.Length; i++)
        {
            var t = AssetDatabase.LoadAssetAtPath<Tile>(tilePaths[i]);
            if (t == null || t.sprite == null) continue;
            var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t.sprite.texture)) as TextureImporter;
            if (imp == null) continue;
            was[i] = imp.isReadable;
            if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
        }
        return was;
    }

    private static void RestoreReadable(string[] tilePaths, bool[] was)
    {
        for (int i = 0; i < tilePaths.Length; i++)
        {
            if (was[i]) continue;
            var t = AssetDatabase.LoadAssetAtPath<Tile>(tilePaths[i]);
            if (t == null || t.sprite == null) continue;
            var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t.sprite.texture)) as TextureImporter;
            if (imp != null && imp.isReadable) { imp.isReadable = false; imp.SaveAndReimport(); }
        }
    }

    // Returns false if the source tile could not be loaded. `spriteOverride` swaps in a different
    // sprite (used for the flattened deep-rock art) while keeping the source's collision settings.
    private static bool MakeVariant(string sourcePath, string suffix, Color tint, ref int made,
                                    Sprite spriteOverride = null)
    {
        var src = AssetDatabase.LoadAssetAtPath<Tile>(sourcePath);
        if (src == null) return false;

        string outPath = Path.Combine(VariantFolder,
            Path.GetFileNameWithoutExtension(sourcePath) + " " + suffix + ".asset").Replace("\\", "/");

        var existing = AssetDatabase.LoadAssetAtPath<Tile>(outPath);
        Tile t = existing != null ? existing : ScriptableObject.CreateInstance<Tile>();

        t.sprite = spriteOverride != null ? spriteOverride : src.sprite;
        t.color = tint;
        // Collision must not change. NOTE: the flattened sprites are Single-mode with no custom
        // physics shape, so a Sprite collider would differ from the original — force Grid, which
        // is what a solid rock cell wants anyway.
        t.colliderType = spriteOverride != null ? Tile.ColliderType.Grid : src.colliderType;
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
