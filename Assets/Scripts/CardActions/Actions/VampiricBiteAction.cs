using System.Collections;
using UnityEngine;

public class VampiricBiteAction : CardAction
{
    public override CardActionType ActionType => CardActionType.VampiricBite;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        player.PerformVampiricBite(value);
        return true;
    }
}
