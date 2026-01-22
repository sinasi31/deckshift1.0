public class RuntimeCard
{
    public CardData cardData;
    public int currentUses;
    public bool isInfinite = false;
    public bool isSelected = false;

    public RuntimeCard(CardData data)
    {
        cardData = data;
        currentUses = data.maxUses;
        isInfinite = false;
        isSelected = false;
    }
}