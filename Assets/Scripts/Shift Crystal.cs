using UnityEngine;

public class ShiftCrystal : MonoBehaviour
{
    [Header("Ayarlar")]
    public int shiftAmount = 1;
    public bool destroyOnCollect = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // SADECE OYUNCU DOKUNURSA ALINSIN
        if (other.CompareTag("Player"))
        {
            CollectCrystal();
        }

        // NOT: Buradaki "else if (other.GetComponent<Fireball>..." kýsmýný sildik.
        // Artýk Fireball buna deðince hiçbir þey olmamýþ gibi yoluna devam edecek.
    }

    private void CollectCrystal()
    {
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            GameManager.instance.player.AddShift(shiftAmount);
            Debug.Log("Shift Kristali alýndý!");
        }

        // Ses efekti vs. buraya

        if (destroyOnCollect)
        {
            Destroy(gameObject);
        }
    }
}