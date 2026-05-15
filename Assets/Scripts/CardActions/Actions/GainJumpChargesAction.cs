using System.Collections;
using UnityEngine;

public class GainJumpChargesAction : CardAction
{
    public override CardActionType ActionType => CardActionType.GainJumpCharges;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        player.AddShift(Mathf.RoundToInt(value));
        return true;
    }
}
