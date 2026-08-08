using UnityEngine;
using UnityEngine.EventSystems;

// Attached to a relic slot cell. Relays pointer enter/exit to the shared RelicTooltip (a filled
// slot shows its relic's tooltip; an empty slot does nothing on hover). On click it runs an
// optional action — the swap screen passes one to select a sacrifice; the HUD bar leaves it null,
// which defaults to opening the Manage panel.
public class RelicSlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private RelicData relic;
    private RelicTooltip tooltip;
    private System.Action onClick;
    private RectTransform rt;

    public void Set(RelicData relic, RelicTooltip tooltip, System.Action onClick = null)
    {
        this.relic = relic;
        this.tooltip = tooltip;
        this.onClick = onClick;
        rt = GetComponent<RectTransform>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (relic != null && tooltip != null) tooltip.Show(relic, rt);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Hide();
        if (onClick != null) onClick();
        else RelicManagePanel.Open();   // HUD-bar default
    }
}
