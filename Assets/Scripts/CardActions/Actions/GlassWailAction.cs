using System.Collections;
using UnityEngine;

public class GlassWailAction : CardAction
{
    public override CardActionType ActionType => CardActionType.GlassWail;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        player.PerformGlassWail(value);
        return true;
    }
}
