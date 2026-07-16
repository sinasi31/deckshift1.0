public class FreefallBladeAction : CardAction
{
    public override CardActionType ActionType => CardActionType.FreefallBlade;

    // Always plays, hit or not (designer 2026-07-15): the swing spends the charge even
    // into empty air — unlike Vampiric Bite's no-cost whiff refusal.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        return player.PerformFreefallBlade(value);
    }
}
