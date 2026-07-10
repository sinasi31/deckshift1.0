#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class DebugTools : MonoBehaviour
{
    private List<RelicData> allRelics = new List<RelicData>();
    private bool showPanel = false;
    private Vector2 scrollPos;

    private void Awake()
    {
        string[] guids = AssetDatabase.FindAssets("t:RelicData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(path);
            if (relic != null) allRelics.Add(relic);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            showPanel = !showPanel;
    }

    private void OnGUI()
    {
        if (!showPanel) return;

        GUILayout.BeginArea(new Rect(20, 20, 260, 400), GUI.skin.box);
        GUILayout.Label("DEBUG — Relic Ver", GUI.skin.box);

        scrollPos = GUILayout.BeginScrollView(scrollPos);

        foreach (RelicData relic in allRelics)
        {
            bool owned = RelicManager.instance != null && RelicManager.instance.HasRelic(relic.relicID);

            GUI.enabled = !owned;
            if (GUILayout.Button(owned ? $"{relic.relicName} ✓" : relic.relicName))
            {
                if (RelicManager.instance != null)
                    RelicManager.instance.TryGrantRelic(relic);   // full loadout -> Swap Screen
                else
                    Debug.LogWarning("[DebugTools] RelicManager sahnede bulunamadı.");
            }
            GUI.enabled = true;
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
#endif
