using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and repairs `Assets/Resources/VigilArt.asset` — the real game art the character select
/// is dressed with. Menu: **Deckshift → Rebuild Vigil Art**.
///
/// Same shape as `UITypeBuilder` and `RelicCatalogueBuilder`: resolve by path/name, fill only EMPTY
/// slots so a deliberate designer override is never stomped, and check on load so the asset cannot
/// go quietly missing.
/// </summary>
public static class VigilArtBuilder
{
    private const string AssetPath = "Assets/Resources/VigilArt.asset";
    private const string PackTex   = "Assets/Cainos/Pixel Art Platformer - Dungeon/Texture/";

    private const string WallTex   = PackTex + "TX Tileable - Dungeon Wall.png";
    private const string PropsTex  = PackTex + "TX Dungeon Props.png";
    private const string DirtTex   = PackTex + "TX Dungeon Wall Dirt.png";
    private const string FlameTex  = PackTex + "FX/TX FX Torch Flame.png";

    [MenuItem("Deckshift/Rebuild Vigil Art")]
    public static void Rebuild()
    {
        VigilArt a = Ensure(true);
        if (a == null) return;
        Selection.activeObject = a;
        EditorGUIUtility.PingObject(a);
    }

    [InitializeOnLoadMethod]
    private static void EnsureOnLoad()
    {
        EditorApplication.delayCall += () => Ensure(false);
    }

    private static VigilArt Ensure(bool verbose)
    {
        var art = AssetDatabase.LoadAssetAtPath<VigilArt>(AssetPath);
        if (art == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, AssetPath)));
            art = ScriptableObject.CreateInstance<VigilArt>();
            AssetDatabase.CreateAsset(art, AssetPath);
            if (verbose) Debug.Log("[VigilArt] created " + AssetPath);
        }

        bool changed = false;

        if (art.wallTexture == null)
        {
            art.wallTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WallTex);
            changed |= art.wallTexture != null;
        }

        changed |= Fill(ref art.wallDirt, DirtTex,  "TX Dungeon Wall Dirt - 03");
        changed |= Fill(ref art.pillar,   PropsTex, "TX Dungeon Props - Pillar 01 A");
        changed |= Fill(ref art.recess,   PropsTex, "TX Dungeon Props - Wall Cave 01 A");
        changed |= Fill(ref art.plinth,   PropsTex, "TX Dungeon Props - Stage 01");
        changed |= Fill(ref art.torch,    PropsTex, "TX Dungeon Props - Torch 01");
        changed |= Fill(ref art.banner,   PropsTex, "TX Dungeon Props - Banner 01 B");
        changed |= Fill(ref art.beam,     PropsTex, "TX Dungeon Props - Beam 01");
        changed |= Fill(ref art.floor,    PropsTex, "TX Dungeon Props - Platform 01");
        changed |= Fill(ref art.flame,    FlameTex, "TX FX Torch Flame");

        if (changed)
        {
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
        }

        if (verbose)
        {
            Debug.Log("[VigilArt] wall=" + N(art.wallTexture) + " dirt=" + N(art.wallDirt) +
                      " pillar=" + N(art.pillar) + " recess=" + N(art.recess) + " plinth=" + N(art.plinth) +
                      " torch=" + N(art.torch) + " flame=" + N(art.flame) + " banner=" + N(art.banner) +
                      " beam=" + N(art.beam) + " floor=" + N(art.floor) + "\n" + AssetPath);
        }

        return art;
    }

    /// <summary>Fills an empty sprite slot from a named sub-sprite of a sheet. Never overwrites.</summary>
    private static bool Fill(ref Sprite slot, string sheetPath, string spriteName)
    {
        if (slot != null) return false;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
        {
            var s = o as Sprite;
            if (s != null && s.name == spriteName) { slot = s; return true; }
        }
        Debug.LogWarning("[VigilArt] '" + spriteName + "' not found in " + sheetPath);
        return false;
    }

    private static string N(Object o) { return o != null ? o.name : "<missing>"; }
}
