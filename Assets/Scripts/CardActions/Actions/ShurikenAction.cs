using UnityEngine;

public class ShurikenAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Shuriken;

    // No declared flags, for the same reason FireballAction declares none: an attack must never be
    // refused by the conflict system, and claiming AnimatorAttackState would Block rapid throws —
    // which on a ten-charge card is the whole way you play it.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        player.ThrowShuriken(value);
        return true;
    }
}
