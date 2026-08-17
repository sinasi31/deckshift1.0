public class ReturnAnchorAction : CardAction
{
    public override CardActionType ActionType => CardActionType.ReturnAnchor;

    // No declared flags, for the same reason as Jump and Stagger: the return is a single-frame
    // reposition with nothing to restore, and declaring PlayerVelocity would let a live Dash or
    // Comet Dive Block it. This is an escape hatch — refusing it is exactly when the player most
    // needs it to work.
    //
    // The one real conflict is a live Phase bubble, which is anchored in world space and would drag
    // the player back out of the return. That is handled properly rather than by refusal:
    // PlayerController.OnTeleported re-anchors the bubble.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        return player.TryReturnAnchor(out keepCardInHand);
    }
}
