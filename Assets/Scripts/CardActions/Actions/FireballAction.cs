using System.Collections;
using UnityEngine;

public class FireballAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Fireball;
    public override bool IsCoroutine => true;
    // No declared flags (design decision): declaring AnimatorAttackState would Block
    // rapid-fire casts for ~0.5s each. Overlapping casts only risk a cosmetic
    // animation cut — attacks must never be refused by the conflict system.

    public override IEnumerator ExecuteCoroutine(PlayerController player, float value)
    {
        yield return player.StartCoroutine(player.FireballCastRoutine(value));
    }
}
