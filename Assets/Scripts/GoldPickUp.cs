using UnityEngine;

public class GoldPickup : MonoBehaviour
{
    [Header("Rastgele Altýn Aralýðý")]
    public int minGold = 15; // En az kaç gelsin?
    public int maxGold = 45; // En fazla kaç gelsin?

    [Header("Ayarlar")]
    public bool destroyOnPickup = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Sadece oyuncu toplasýn
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                int randomAmount = Random.Range(minGold, maxGold + 1);

                player.AddGold(randomAmount);

                // Efekt veya ses buraya eklenebilir
                if (destroyOnPickup) Destroy(gameObject);
            }
        }
    }
}