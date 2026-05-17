using UnityEngine;

public class PortalAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Portal;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        return player.TryPlacePortal(out keepCardInHand, Mathf.RoundToInt(value));
    }
}
