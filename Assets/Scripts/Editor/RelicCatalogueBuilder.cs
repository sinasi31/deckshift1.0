using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Keeps RelicCatalogue in sync with the RelicData assets, automatically.
//
// The point is that nobody has to remember. The shop and the chests fell 15 and 13 relics behind
// respectively simply because adding a relic asset did not add it to anything — and a missing entry
// is invisible from inside the game, so it survives every playtest. Regenerating from the assets
// makes the roster the single source of truth.
public class RelicCatalogueBuilder : AssetPostprocessor
{
    private const string CataloguePath = "Assets/Resources/RelicCatalogue.asset";

    [MenuItem("Deckshift/Rebuild Relic Catalogue")]
    public static void RebuildMenu()
    {
        int n = Rebuild();
        Debug.Log($"[RelicCatalogue] {n} relic(s) catalogued at {CataloguePath}.");
    }

    // Rebuild whenever a RelicData asset is added, deleted, moved or re-imported.
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                               string[] movedTo, string[] movedFrom)
    {
        if (TouchesRelic(imported) || TouchesRelic(deleted) || TouchesRelic(movedTo) || TouchesRelic(movedFrom))
            Rebuild();
    }

    private static bool TouchesRelic(string[] paths)
    {
        foreach (string p in paths)
        {
            if (!p.EndsWith(".asset")) continue;
            // A deleted asset can no longer be loaded, so fall back to the folder convention.
            if (p.StartsWith("Assets/Relics/")) return true;
            if (AssetDatabase.LoadAssetAtPath<RelicData>(p) != null) return true;
        }
        return false;
    }

    private static int Rebuild()
    {
        var relics = new List<RelicData>();
        foreach (string guid in AssetDatabase.FindAssets("t:RelicData"))
        {
            var r = AssetDatabase.LoadAssetAtPath<RelicData>(AssetDatabase.GUIDToAssetPath(guid));
            if (r != null) relics.Add(r);
        }
        // Stable order so the asset does not churn in git on every rebuild.
        relics.Sort((a, b) => string.CompareOrdinal(a.relicID, b.relicID));

        var duplicateIDs = new Dictionary<string, int>();
        foreach (var r in relics)
        {
            if (string.IsNullOrEmpty(r.relicID))
            {
                Debug.LogWarning($"[RelicCatalogue] '{r.name}' has an EMPTY relicID — HasRelic() can never match it.", r);
                continue;
            }
            duplicateIDs.TryGetValue(r.relicID, out int c);
            duplicateIDs[r.relicID] = c + 1;
        }
        foreach (var kv in duplicateIDs)
            if (kv.Value > 1)
                Debug.LogWarning($"[RelicCatalogue] relicID '{kv.Key}' is used by {kv.Value} assets — " +
                                 "HasRelic() and the owned-filter treat them as the same relic.");

        Directory.CreateDirectory(Path.GetDirectoryName(CataloguePath));
        var cat = AssetDatabase.LoadAssetAtPath<RelicCatalogue>(CataloguePath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<RelicCatalogue>();
            AssetDatabase.CreateAsset(cat, CataloguePath);
        }

        // Only write when something actually changed, so this can run on every import cheaply.
        bool changed = cat.all.Count != relics.Count;
        if (!changed)
            for (int i = 0; i < relics.Count; i++)
                if (cat.all[i] != relics[i]) { changed = true; break; }

        if (changed)
        {
            cat.all = relics;
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
        }
        return relics.Count;
    }
}
