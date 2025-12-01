/*
Bu script'i Unity'de hiçbir objeye ekleme.
Sadece veri taþýyýcýdýr.
*/
public class RuntimeCard
{
    public CardData cardData; // Þablon
    public int currentUses;   // Kalan kullaným hakký

    // --- EKSÝK OLAN KISIM BURASIYDI ---
    public bool isInfinite = false; // Kart sonsuz mu?
    // ----------------------------------

    public RuntimeCard(CardData data)
    {
        cardData = data;
        currentUses = data.maxUses;
        isInfinite = false; // Baþlangýçta sonsuz deðil
    }
}