using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and repairs `Assets/Resources/UIType.asset`, the settings asset <see cref="UIType"/>
/// loads at runtime.
///
/// Same shape as `RelicCatalogueBuilder`: a menu item plus a check on load, so the asset cannot go
/// quietly missing and take the project's typography back to the font census with it. Font
/// references are resolved by **path**, and only filled in when they are empty — so if the designer
/// deliberately points a slot at a different face, a rebuild will not stomp it.
/// </summary>
public static class UITypeBuilder
{
    private const string AssetPath   = "Assets/Resources/UIType.asset";
    private const string DisplayPath = "Assets/LevelEfeVrl/Sprites/CCBattleScarred-Regular SDF.asset";
    private const string ProsePath   = "Assets/Cainos/Common/Font/Pixie SDF.asset";

    [MenuItem("Deckshift/Rebuild UI Type")]
    public static void Rebuild()
    {
        UITypeSettings s = Ensure(true);
        if (s == null) return;

        Selection.activeObject = s;
        EditorGUIUtility.PingObject(s);
        Debug.Log("[UIType] display = " + Name(s.displayFont) + "   prose = " + Name(s.bodyFont) +
                  "\n" + AssetPath);
    }

    [InitializeOnLoadMethod]
    private static void EnsureOnLoad()
    {
        // Deferred: the asset database is not necessarily ready during InitializeOnLoad itself.
        EditorApplication.delayCall += () => Ensure(false);
    }

    private static UITypeSettings Ensure(bool verbose)
    {
        var settings = AssetDatabase.LoadAssetAtPath<UITypeSettings>(AssetPath);

        if (settings == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, AssetPath)));

            settings = ScriptableObject.CreateInstance<UITypeSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            if (verbose) Debug.Log("[UIType] created " + AssetPath);
        }

        bool changed = false;

        if (settings.displayFont == null)
        {
            settings.displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayPath);
            changed |= settings.displayFont != null;
            if (settings.displayFont == null && verbose)
                Debug.LogWarning("[UIType] display face not found at " + DisplayPath);
        }

        if (settings.bodyFont == null)
        {
            settings.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProsePath);
            changed |= settings.bodyFont != null;
            if (settings.bodyFont == null && verbose)
                Debug.LogWarning("[UIType] prose face not found at " + ProsePath);
        }

        if (changed)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        return settings;
    }

    private static string Name(Object o) { return o != null ? o.name : "<missing>"; }
}
