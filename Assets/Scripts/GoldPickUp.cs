using UnityEngine;

public class GoldPickup : MonoBehaviour
{
    public int goldAmount = 10; // Kaç altýn verecek?
    public bool destroyOnPickup = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Sadece oyuncu toplasýn (Fireball toplamasýn)
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.AddGold(goldAmount);
                // Buraya "Ching!" ses efekti ekleyebilirsin.
                if (destroyOnPickup) Destroy(gameObject);
            }
        }
    }
}