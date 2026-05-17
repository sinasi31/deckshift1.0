using System.Collections;
using UnityEngine;

public class FireballAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Fireball;
    public override bool IsCoroutine => true;
    public override ConflictFlags ModifiedState => ConflictFlags.AnimatorAttackState;

    public override IEnumerator ExecuteCoroutine(PlayerController player, float value)
    {
        yield return player.StartCoroutine(player.FireballCastRoutine(value));
    }
}
