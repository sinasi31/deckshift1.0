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

        // RelicIcon builds the rarity chip (glow + plate + framed art) + pop-in procedurally.
        RelicIcon styler = icon.GetComponent<RelicIcon>();
        if (styler == null) styler = icon.AddComponent<RelicIcon>();
        styler.Build(relic);
    }
}
