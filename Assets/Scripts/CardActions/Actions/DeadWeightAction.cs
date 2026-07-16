public class DeadWeightAction : CardAction
{
    public override CardActionType ActionType => CardActionType.DeadWeight;

    // Dead Weight cannot be played — returning false rides the normal "failed play"
    // path: no Shift spent, no charge lost, card stays in hand. Its only effect is
    // BEING in hand when a combat room ends: DeckManager.OnRoomEnd pays out
    // actionValue Shift per copy. Recall discards it like any card and forfeits that.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        return false;
    }
}
