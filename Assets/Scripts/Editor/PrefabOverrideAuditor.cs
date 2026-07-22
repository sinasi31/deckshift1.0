using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Design-time safety net: finds prefab-instance overrides that have silently diverged from their
// source prefab. Run via the menu:
//   Deckshift → Audit Prefab Overrides
//
// WHY THIS EXISTS (2026-07-22): two separate bugs in one day were both "a prefab-instance override
// quietly disagreeing with a correct source prefab":
//   1. SampleScene's Player overrode PlayerController.warningSoundClip to NULL, silencing the
//      gravity-reversal warning even though Player.prefab had the clip assigned.
//   2. hub.prefab pinned the entry-door's local Z with an override, so it would NOT have followed
//      the project-wide fix later applied to Assets/Prefabs/GirisNoktasi.prefab.
// Both are invisible in normal use — the prefab looks right, so you debug the code instead.
//
// The audit reports only the two HIGH-SIGNAL categories, so the output stays readable:
//   NULLED  — the override blanks an object reference the source actually has. Almost always a bug.
//   PINNED  — the override just repeats the source's CURRENT value. Harmless today, but the
//             instance is frozen and will not follow future edits to the prefab.
// Everything else (genuine intentional tweaks) is counted but not listed.
public static class PrefabOverrideAuditor
{
    private const string MenuPath = "Deckshift/Audit Prefab Overrides";

    // Overrides on an instance ROOT's own transform/name are how you place and label a thing in a
    // level — never interesting. Child transforms ARE interesting (that is bug #2 above).
    private static readonly HashSet<string> RootNoisePaths = new HashSet<string>
    {
        "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
        "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
        "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z",
        "m_Name", "m_RootOrder",
    };

    private class Finding
    {
        public string Where;        // human-readable location
        public string What;         // Component.property
        public string Detail;       // source vs instance
        public Object Context;      // click-to-ping target
    }

    [MenuItem(MenuPath)]
    public static void Audit()
    {
        RunAudit();
    }

    // Returns the same report it logs, so it can also be driven from automation/tests.
    public static string RunAudit()
    {
        var nulled = new List<Finding>();
        var pinned = new List<Finding>();
        var seen = new HashSet<string>();
        int instances = 0, otherOverrides = 0, assetsScanned = 0;

        // --- 1. The active scene -------------------------------------------------------------
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                    instances++;
                    ScanInstance(t.gameObject, "Scene '" + scene.name + "' → " + PathOf(t),
                                 nulled, pinned, seen, ref otherOverrides);
                }
            }
        }

        // --- 2. Every prefab asset that nests other prefabs (rooms, props) --------------------
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Retired folders are known-broken (missing nested prefabs) and only add console noise.
            if (path.Contains("Old Levels") || path.Contains("old_levels")) continue;
            if (path.Contains("/Cainos/")) continue;   // untouched vendor art

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            assetsScanned++;

            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject == go) continue;   // the asset root is not a nested instance
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                instances++;
                ScanInstance(t.gameObject, System.IO.Path.GetFileName(path) + " → " + PathOf(t),
                             nulled, pinned, seen, ref otherOverrides);
            }
        }

        return Report(nulled, pinned, instances, assetsScanned, otherOverrides);
    }

    private static void ScanInstance(GameObject instanceRoot, string where,
                                     List<Finding> nulled, List<Finding> pinned,
                                     HashSet<string> seen, ref int otherOverrides)
    {
        // --- Pass 1: NULLED, by comparing EFFECTIVE values against the source object. ------------
        // Deliberately NOT driven by GetPropertyModifications: the recorded modification list can
        // contain STALE entries Unity no longer applies (a real case: GenLevel3's AcidWater carries
        // an m_Materials.Array.data[0] = null record, but every material is actually assigned).
        // Trusting the record produced a false positive; reading the live value does not.
        foreach (var comp in instanceRoot.GetComponentsInChildren<Component>(true))
        {
            if (comp == null) continue;
            var src = PrefabUtility.GetCorrespondingObjectFromSource(comp);
            if (src == null) continue;

            var instSO = new SerializedObject(comp);
            var srcSO = new SerializedObject(src);
            var it = instSO.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (it.objectReferenceValue != null) continue;          // instance has a value -> fine
                if (InternalPath(it.propertyPath)) continue;

                var srcProp = srcSO.FindProperty(it.propertyPath);
                if (srcProp == null
                    || srcProp.propertyType != SerializedPropertyType.ObjectReference
                    || srcProp.objectReferenceValue == null) continue;   // source empty too -> fine

                string k = "N|" + comp.GetInstanceID() + "|" + it.propertyPath;
                if (!seen.Add(k)) continue;

                // Distinguish the two very different causes, because the FIX differs:
                //  (a) the reference was simply cleared      -> revert the property
                //  (b) the object it pointed at was DELETED  -> revert can't help; restore the
                //      child GameObject, or delete the now-useless leftover.
                var srcRef = srcProp.objectReferenceValue;
                bool targetStillExists = false;
                foreach (var ic in instanceRoot.GetComponentsInChildren<Component>(true))
                {
                    if (ic == null) continue;
                    if (PrefabUtility.GetCorrespondingObjectFromSource(ic) == srcRef)
                    { targetStillExists = true; break; }
                }

                nulled.Add(new Finding
                {
                    Where = where,
                    What = comp.GetType().Name + "." + it.propertyPath,
                    Detail = targetStillExists
                        ? "prefab has '" + srcRef.name + "', instance has NOTHING (reference cleared — revert it)"
                        : "prefab has '" + srcRef.name + "', but the instance DELETED that object — "
                          + "reverting won't help; restore the child or remove the leftover",
                    Context = instanceRoot,
                });
            }
        }

        // --- Pass 2: PINNED, from the modification records (value-identical overrides). ----------
        PropertyModification[] mods = PrefabUtility.GetPropertyModifications(instanceRoot);
        if (mods == null) return;

        foreach (var mod in mods)
        {
            if (mod == null || mod.target == null) continue;

            bool onInstanceRoot = IsPartOf(mod.target, instanceRoot);
            if (onInstanceRoot && RootNoisePath(mod.propertyPath)) continue;
            if (mod.propertyPath.StartsWith("m_LocalEulerAnglesHint")) continue;   // mirrors rotation

            // Dedupe: nested instances can surface the same modification twice.
            string key = "P|" + mod.target.GetInstanceID() + "|" + mod.propertyPath;
            if (!seen.Add(key)) continue;

            var sourceSO = new SerializedObject(mod.target);
            var sp = sourceSO.FindProperty(mod.propertyPath);
            if (sp == null) { otherOverrides++; continue; }

            string what = mod.target.GetType().Name + "." + mod.propertyPath;

            // PINNED is only meaningful on OUR OWN script fields. Unity's built-in components
            // (RectTransform especially) record value-identical overrides constantly as a normal
            // part of layout/serialization — reporting those buried the real findings 500:1.
            if (IsProjectScript(mod.target) && ValuesEqual(sp, mod))
            {
                pinned.Add(new Finding
                {
                    Where = where,
                    What = what,
                    Detail = "override repeats the prefab's current value (" + Describe(sp) + ")",
                    Context = instanceRoot,
                });
            }
            else
            {
                otherOverrides++;
            }
        }
    }

    private static bool RootNoisePath(string path)
    {
        return RootNoisePaths.Contains(path);
    }

    // Unity's own serialization plumbing — never a gameplay reference.
    private static bool InternalPath(string path)
    {
        return path == "m_Script" || path == "m_GameObject" || path == "m_ObjectHideFlags"
            || path == "m_CorrespondingSourceObject" || path == "m_PrefabInstance"
            || path == "m_PrefabAsset" || path == "m_StaticEditorFlags";
    }

    // "Is this one of OUR scripts?" — anything outside the UnityEngine/UnityEditor/TMPro
    // namespaces, i.e. a gameplay MonoBehaviour we wrote.
    private static bool IsProjectScript(Object target)
    {
        var ns = target.GetType().Namespace;
        if (string.IsNullOrEmpty(ns)) return true;               // global namespace = our code
        return !ns.StartsWith("UnityEngine") && !ns.StartsWith("UnityEditor")
            && !ns.StartsWith("TMPro") && !ns.StartsWith("Cainos");
    }

    // True if the modification target is a component/GameObject of the instance root itself
    // (as opposed to one of its children).
    private static bool IsPartOf(Object target, GameObject root)
    {
        var comp = target as Component;
        if (comp != null) return comp.gameObject.name == root.name;
        var go = target as GameObject;
        return go != null && go.name == root.name;
    }

    private static bool ValuesEqual(SerializedProperty sp, PropertyModification mod)
    {
        switch (sp.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                return sp.objectReferenceValue == mod.objectReference;
            case SerializedPropertyType.Float:
            {
                float f;
                if (!float.TryParse(mod.value, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
                    return false;
                return Mathf.Abs(sp.floatValue - f) < 1e-6f;
            }
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.Enum:
            {
                int i;
                if (!int.TryParse(mod.value, out i)) return false;
                return sp.intValue == i;
            }
            case SerializedPropertyType.Boolean:
                return sp.boolValue == (mod.value == "1");
            case SerializedPropertyType.String:
                return sp.stringValue == mod.value;
            default:
                return false;   // can't compare confidently -> don't claim it's redundant
        }
    }

    private static string Describe(SerializedProperty sp)
    {
        switch (sp.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                return sp.objectReferenceValue == null ? "none" : sp.objectReferenceValue.name;
            case SerializedPropertyType.Float:   return sp.floatValue.ToString("G6");
            case SerializedPropertyType.Boolean: return sp.boolValue.ToString();
            case SerializedPropertyType.String:  return "\"" + sp.stringValue + "\"";
            default:                             return sp.intValue.ToString();
        }
    }

    private const int MaxListed = 40;

    private static void AppendList(System.Text.StringBuilder sb, List<Finding> list)
    {
        int shown = Mathf.Min(list.Count, MaxListed);
        for (int i = 0; i < shown; i++)
        {
            sb.AppendLine("   • " + list[i].Where);
            sb.AppendLine("       " + list[i].What + "  —  " + list[i].Detail);
        }
        if (list.Count > shown)
            sb.AppendLine("   … and " + (list.Count - shown) + " more (showing first " + MaxListed + ").");
    }

    private static string PathOf(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    private static string Report(List<Finding> nulled, List<Finding> pinned,
                                 int instances, int assets, int other)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[Prefab Override Audit] " + instances + " prefab instances across the active scene + "
                      + assets + " prefab assets.");
        sb.AppendLine("   NULLED: " + nulled.Count + "   PINNED: " + pinned.Count
                      + "   (other intentional overrides: " + other + ")");
        sb.AppendLine();

        if (nulled.Count > 0)
        {
            sb.AppendLine("!! NULLED — the instance blanks a value the prefab HAS. Almost always a bug:");
            sb.AppendLine("   the prefab looks correct, so you end up debugging the code instead.");
            AppendList(sb, nulled);
            sb.AppendLine();
        }

        if (pinned.Count > 0)
        {
            sb.AppendLine("~  PINNED — the override just repeats the prefab's current value. Harmless now,");
            sb.AppendLine("   but this instance will NOT follow future changes to the prefab.");
            AppendList(sb, pinned);
            sb.AppendLine();
        }

        if (nulled.Count == 0 && pinned.Count == 0)
            sb.AppendLine("Clean — no nulled or pinned overrides found.");
        else
            sb.AppendLine("TO FIX: select the object, right-click the property's label in the Inspector, "
                          + "and choose 'Revert'. That makes it follow the prefab again.");

        if (nulled.Count > 0) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());

        // Individually clickable entries for the worst category.
        foreach (var f in nulled)
            Debug.LogWarning("[Override Audit] NULLED  " + f.Where + "  " + f.What + " — " + f.Detail, f.Context);

        return sb.ToString();
    }
}
