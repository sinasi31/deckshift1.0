using System.Collections.Generic;
using UnityEngine;

// Bu satýr, Unity'nin "Create" menüsüne yeni bir seçenek ekler.
[CreateAssetMenu(fileName = "Yeni Kart Veritabaný", menuName = "Oyunum/Kart Veritabaný")]
public class CardDatabase : ScriptableObject
{
    // Oyunda bulunabilecek tüm özel kartlarýn listesi.
    // Buraya Inspector'dan sürükleyip býrakarak kart ekleyeceðiz.
    public List<CardData> tumOzelKartlar;
}