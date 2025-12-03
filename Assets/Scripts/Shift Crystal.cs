using UnityEngine;

public class ShiftCrystal : MonoBehaviour
{
    [Header("Ayarlar")]
    public int shiftAmount = 1; // Kaç Shift verecek?
    public bool destroyOnCollect = true; // Alýnýnca yok olsun mu? (Genelde evet)

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. OYUNCU DOKUNURSA
        if (other.CompareTag("Player"))
        {
            CollectCrystal();
        }
        // 2. ATEÞ TOPU (FIREBALL) VURURSA
        else if (other.GetComponent<Fireball>() != null)
        {
            // Fireball'u da yok edelim ki içinden geçip gitmesin (Ýsteðe baðlý)
            Destroy(other.gameObject);
            CollectCrystal();
        }
    }

    private void CollectCrystal()
    {
        // Shift ver
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            GameManager.instance.player.AddShift(shiftAmount);
            Debug.Log("Shift Kristali alýndý!");
        }

        // Efekt (Ses/Partikül) buraya eklenebilir

        if (destroyOnCollect)
        {
            Destroy(gameObject);
        }
    }
}