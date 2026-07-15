using UnityEngine;

public class GoldPickup : MonoBehaviour
{
    [Header("Ayarlar")]
    public int goldAmount = 10; // Bu alt�n ka� para veriyor?

    [Header("Ses Ayarlar�")]
    public AudioClip goldSound;
    [Range(0f, 1f)] public float soundVolume = 0.5f;

    // Scrap Magnet relic pulls the coin toward the player when in range (no-op without it).
    private void Update()
    {
        ScrapMagnet.Attract(transform);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 1. Oyuncuyu bul
            PlayerController player = other.GetComponent<PlayerController>();

            // E�er oyuncu scripti varsa paray� ekle
            if (player != null)
            {
                player.AddGold(goldAmount);
            }

            // 2. Sesi �al (Obje yok olsa bile �alar)
            PlaySound();

            // 3. Alt�n� yok et
            Destroy(gameObject);
        }
    }

    private void PlaySound()
    {
        if (goldSound != null)
        {
            Vector3 soundPos = transform.position;
            soundPos.z = Camera.main.transform.position.z;
            SfxManager.PlayAtPoint(goldSound, soundPos, soundVolume);
        }
    }
}