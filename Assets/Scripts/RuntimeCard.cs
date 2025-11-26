/*
Bu script'i Unity'de hiçbir objeye eklemeyeceksin.
Bu, 'DeckManager'ýn kullanacaðý basit bir veri taþýyýcý sýnýftýr.
*/
public class RuntimeCard
{
    public CardData cardData; // Þablon (Adý, hasarý, maliyeti, maks. kullanýmý)
    public int currentUses;   // O anki kullaným hakký (5, 4, 3...)

    // Yeni bir RuntimeCard oluþtururken bu 'constructor'ý kullanacaðýz
    public RuntimeCard(CardData data)
    {
        cardData = data;
        currentUses = data.maxUses; // O anki kullaným hakkýný, þablondaki maks. hakka eþitle
    }
}