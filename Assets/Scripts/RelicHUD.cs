using UnityEngine;
using UnityEngine.UI;

public class RelicHUD : MonoBehaviour
{
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private Transform iconContainer;

    private void Start()
    {
        if (RelicManager.instance == null) return;

        // Populate icons for relics already owned before this HUD existed
        // (handles scene reload or HUD initialising after a relic was granted).
        foreach (RelicData relic in RelicManager.instance.OwnedRelics)
            AddIcon(relic);

        RelicManager.instance.OnRelicAdded += AddIcon;
    }

    private void OnDestroy()
    {
        if (RelicManager.instance != null)
            RelicManager.instance.OnRelicAdded -= AddIcon;
    }

    private void AddIcon(RelicData relic)
    {
        if (iconPrefab == null || iconContainer == null) return;

        GameObject icon = Instantiate(iconPrefab, iconContainer);

        Image img = icon.GetComponentInChildren<Image>();
        if (img == null) return;

        // Leave the prefab's default sprite intact when relicArt is unassigned,
        // so the slot is visible even for relics without art yet.
        if (relic.relicArt != null)
            img.sprite = relic.relicArt;
    }
}
