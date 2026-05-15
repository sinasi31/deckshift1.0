using System.Collections;
using UnityEngine;

public class DrawCardsAction : CardAction
{
    public override CardActionType ActionType => CardActionType.DrawCards;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        if (DeckManager.instance != null)
        {
            for (int i = 0; i < Mathf.RoundToInt(value); i++)
                DeckManager.instance.DrawCard();
        }
        return true;
    }
}
