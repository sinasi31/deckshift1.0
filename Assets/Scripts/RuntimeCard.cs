public class RuntimeCard
{
    public CardData cardData;
    public int currentUses;
    public bool isInfinite = false;
    public bool isSelected = false;

    // Blompo's blessing. At most ONE per card (enforced in CardEnhancements.CanApplyTo).
    // Deliberately lives here and NOT on CardData — CardData is a shared asset, so writing an
    // enhancement there would upgrade the card in every future run and dirty the asset on disk.
    public CardEnhancement enhancement = CardEnhancement.None;

    public RuntimeCard(CardData data)
    {
        cardData = data;
        currentUses = data.maxUses;
        isInfinite = false;
        isSelected = false;
        enhancement = CardEnhancement.None;
    }
}
