using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

// Deckshift -> Rebuild Resource Panel HUD
//
// One-click install for ResourcePanelHUD: finds the painted stat panel in the open scene, adds the
// component, and wires every reference (player, font, heart / crystal icons, the legacy displays it
// replaces). Written as a menu command so the designer never has to hand-drag references — the same
// reason LevelTextImporter and PrefabOverrideAuditor exist.
//
// Safe to re-run: it re-wires an existing component rather than adding a second one.
public static class ResourcePanelHUDSetup
{
    const string HEART_PATH = "Assets/Art/heart.png";
    const string CRYSTAL_PATH = "Assets/Art/shift_crystal.png";

    [MenuItem("Deckshift/Rebuild Resource Panel HUD")]
    public static void Rebuild()
    {
        Transform panel = FindInScene("ResourcePanel");
        if (panel == null)
        {
            EditorUtility.DisplayDialog("Resource Panel HUD",
                "Couldn't find a GameObject named \"ResourcePanel\" in the open scene.\n\n" +
                "Open Assets/Scenes/SampleScene.unity (it lives under Canvas/GameplayHUD) and run this again.",
                "OK");
            return;
        }

        var hud = panel.GetComponent<ResourcePanelHUD>();
        if (hud == null) hud = Undo.AddComponent<ResourcePanelHUD>(panel.gameObject);
        Undo.RecordObject(hud, "Wire Resource Panel HUD");

        // --- player ---
        if (hud.playerController == null)
            hud.playerController = Object.FindFirstObjectByType<PlayerController>();

        // --- displays this replaces / adopts ---
        Transform legacyHealth = panel.Find("Health_Background");
        Transform legacyShift = panel.Find("JumpText");
        Transform gold = panel.Find("GoldDisplay");
        if (legacyHealth != null) hud.legacyHealthDisplay = legacyHealth.gameObject;
        if (legacyShift != null) hud.legacyShiftText = legacyShift.gameObject;
        if (gold != null) hud.goldDisplay = gold.gameObject;

        // --- font: match whatever the panel already uses ---
        if (hud.font == null && legacyShift != null)
        {
            var tmp = legacyShift.GetComponent<TextMeshProUGUI>();
            if (tmp != null) hud.font = tmp.font;
        }

        // --- icons ---
        if (hud.heartIcon == null) hud.heartIcon = LoadSprite(HEART_PATH);
        if (hud.shiftIcon == null) hud.shiftIcon = LoadSprite(CRYSTAL_PATH);

        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;

        string report =
            $"Installed on: {Path(panel)}\n\n" +
            $"Player:        {Describe(hud.playerController)}\n" +
            $"Font:          {Describe(hud.font)}\n" +
            $"Heart icon:    {Describe(hud.heartIcon)}\n" +
            $"Shift icon:    {Describe(hud.shiftIcon)}\n" +
            $"Legacy health: {Describe(hud.legacyHealthDisplay)}  (hidden on play)\n" +
            $"Legacy Shift:  {Describe(hud.legacyShiftText)}  (hidden on play)\n" +
            $"Gold display:  {Describe(hud.goldDisplay)}  (moved to row 3)\n\n" +
            "The painted panel background is switched off at runtime (Hide Panel Background).\n\n" +
            "Save the scene, then press Play. The bars are built at runtime, so they won't show " +
            "in edit mode.";

        Debug.Log("[ResourcePanelHUD] " + report, hud);
        EditorUtility.DisplayDialog("Resource Panel HUD", report, "OK");
    }

    private static Sprite LoadSprite(string path)
    {
        // These PNGs import as sprite sheets (spriteMode: Multiple), so the Sprite is a sub-asset —
        // LoadAssetAtPath<Sprite> returns null on them.
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    private static Transform FindInScene(string name)
    {
        return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .FirstOrDefault(t => t.name == name);
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        for (Transform c = t.parent; c != null; c = c.parent) p = c.name + "/" + p;
        return p;
    }

    private static string Describe(Object o) => o != null ? o.name : "<MISSING>";
}
