using System.Collections;
using UnityEngine;

public class WallClingAction : CardAction
{
    public override CardActionType ActionType => CardActionType.WallCling;
    public override bool IsCoroutine => true;

    public override IEnumerator ExecuteCoroutine(PlayerController player, float value)
    {
        yield return player.StartCoroutine(player.ActivateWallCling(value));
    }
}
