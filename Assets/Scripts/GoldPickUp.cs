using UnityEngine;

public class GoldPickup : MonoBehaviour
{
    [Header("Ayarlar")]
    public int goldAmount = 10; // Bu altýn kaç para veriyor?

    [Header("Ses Ayarlarý")]
    public AudioClip goldSound;
    [Range(0f, 1f)] public float soundVolume = 0.5f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 1. Oyuncuyu bul
            PlayerController player = other.GetComponent<PlayerController>();

            // Eðer oyuncu scripti varsa parayý ekle
            if (player != null)
            {
                player.AddGold(goldAmount);
            }

            // 2. Sesi çal (Obje yok olsa bile çalar)
            PlaySound();

            // 3. Altýný yok et
            Destroy(gameObject);
        }
    }

    private void PlaySound()
    {
        if (goldSound != null)
        {
            Vector3 soundPos = transform.position;
            soundPos.z = Camera.main.transform.position.z;
            AudioSource.PlayClipAtPoint(goldSound, soundPos, soundVolume);
        }
    }
}