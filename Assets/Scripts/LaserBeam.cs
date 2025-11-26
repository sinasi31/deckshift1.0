using System.Collections;
using UnityEngine;

public class LaserBeam : MonoBehaviour
{
    [Header("Zamanlama Ayarlarý")]
    public float warningTime = 1.0f;  // Lazerin aktif olmadan önceki uyarý süresi
    public float activeTime = 2.0f;   // Lazerin açýk kalýp hasar verdiði süre
    public float offTime = 3.0f;      // Lazerin kapalý kaldýðý süre

    [Header("Hasar Ayarlarý")]
    public float damagePerSecond = 50f; // Saniye baþýna vereceði hasar

    [Header("Görsel Referanslar")]
    private SpriteRenderer beamSprite;
    private Collider2D beamCollider;

    // Awake fonksiyonu, component referanslarýný almak için en iyi yerdir.
    private void Awake()
    {
        // Bu script'in eklendiði objenin üzerindeki component'larý bul
        beamSprite = GetComponent<SpriteRenderer>();
        beamCollider = GetComponent<Collider2D>();
    }

    // Oyun baþladýðýnda lazer döngüsünü baþlat
    private void Start()
    {
        // Baþlangýçta lazerin kapalý olduðundan emin ol
        beamSprite.enabled = false;
        beamCollider.enabled = false;

        // Sonsuz lazer döngüsünü baþlatan Coroutine'i çaðýr
        StartCoroutine(LaserCycleRoutine());
    }

    private IEnumerator LaserCycleRoutine()
    {
        // Bu döngü, obje yok edilene kadar sonsuza dek devam edecek
        while (true)
        {
            // --- UYARI FAZI ---
            beamSprite.color = Color.yellow; // Uyarý rengi (isteðe baðlý)
            beamSprite.enabled = true;
            yield return new WaitForSeconds(warningTime);

            // --- AKTÝF (ATEÞ) FAZI ---
            beamSprite.color = Color.red; // Aktif rengi
            beamCollider.enabled = true; // Hasar vermek için collider'ý aç
            yield return new WaitForSeconds(activeTime);

            // --- KAPALI FAZI ---
            beamSprite.enabled = false;
            beamCollider.enabled = false;
            yield return new WaitForSeconds(offTime);
        }
    }

    // Bu fonksiyon, bir obje lazerin trigger alanýnda DURDUÐU SÜRECE her frame çalýþýr.
    // Bu, saniye baþýna hasar vermek için mükemmeldir.
    private void OnTriggerStay2D(Collider2D other)
    {
        // Temas eden oyuncu mu?
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            // Hasarý, saniye baþýna hasar * geçen süre olarak uygula.
            // Bu, hasarýn akýcý olmasýný saðlar.
            player.TakeDamage(damagePerSecond * Time.deltaTime);
        }
    }
}