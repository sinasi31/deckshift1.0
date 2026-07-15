using UnityEngine;

public class ShiftCrystal : MonoBehaviour
{
    [Header("Ayarlar")]
    public int shiftAmount = 1;
    public bool destroyOnCollect = true;

    [Header("Ses")]
    // Played when the player collects the crystal. Uses PlayClipAtPoint because
    // the object is destroyed on collect, so a local AudioSource would be cut off.
    [SerializeField] private AudioClip collectSound;
    [SerializeField, Range(0f, 1f)] private float collectVolume = 1f;

    // Scrap Magnet relic pulls the crystal toward the player when in range (no-op without it).
    private void Update()
    {
        ScrapMagnet.Attract(transform);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // SADECE OYUNCU DOKUNURSA ALINSIN
        if (other.CompareTag("Player"))
        {
            CollectCrystal();
        }

        // NOT: Buradaki "else if (other.GetComponent<Fireball>..." k�sm�n� sildik.
        // Art�k Fireball buna de�ince hi�bir �ey olmam�� gibi yoluna devam edecek.
    }

    private void CollectCrystal()
    {
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            GameManager.instance.player.AddShift(shiftAmount);
            Debug.Log("Shift Kristali al�nd�!");
        }

        // Play the pickup sound at the crystal's position (survives object destroy).
        SfxManager.PlayAtPoint(collectSound, transform.position, collectVolume);

        if (destroyOnCollect)
        {
            Destroy(gameObject);
        }
    }
}