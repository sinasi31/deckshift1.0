using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Keeps CardCatalogue in sync with the CardData assets, automatically. Twin of
// RelicCatalogueBuilder — same failure, same remedy: authoring the asset is the only step.
public class CardCatalogueBuilder : AssetPostprocessor
{
    private const string CataloguePath = "Assets/Resources/CardCatalogue.asset";

    [MenuItem("Deckshift/Rebuild Card Catalogue")]
    public static void RebuildMenu()
    {
        int n = Rebuild();
        Debug.Log($"[CardCatalogue] {n} card(s) catalogued at {CataloguePath}.");
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                               string[] movedTo, string[] movedFrom)
    {
        if (Touches(imported) || Touches(deleted) || Touches(movedTo) || Touches(movedFrom))
            Rebuild();
    }

    private static bool Touches(string[] paths)
    {
        foreach (string p in paths)
        {
            if (!p.EndsWith(".asset")) continue;
            // A deleted asset can no longer be loaded, so fall back to the folder convention.
            if (p.StartsWith("Assets/Cards/")) return true;
            if (AssetDatabase.LoadAssetAtPath<CardData>(p) != null) return true;
        }
        return false;
    }

    private static int Rebuild()
    {
        var cards = new List<CardData>();
        foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
        {
            var c = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null) cards.Add(c);
        }
        // Stable order so the asset doesn't churn in git on every rebuild.
        cards.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        foreach (var c in cards)
            if (string.IsNullOrEmpty(c.cardName))
                Debug.LogWarning($"[CardCatalogue] '{c.name}' has an empty cardName — it will show blank in UI.", c);

        Directory.CreateDirectory(Path.GetDirectoryName(CataloguePath));
        var cat = AssetDatabase.LoadAssetAtPath<CardCatalogue>(CataloguePath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<CardCatalogue>();
            AssetDatabase.CreateAsset(cat, CataloguePath);
        }

        bool changed = cat.all.Count != cards.Count;
        if (!changed)
            for (int i = 0; i < cards.Count; i++)
                if (cat.all[i] != cards[i]) { changed = true; break; }

        if (changed)
        {
            cat.all = cards;
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
        }
        return cards.Count;
    }
}
