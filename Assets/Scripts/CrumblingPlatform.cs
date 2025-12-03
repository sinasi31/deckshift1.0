using UnityEngine;
using System.Collections;

public class CrumblingPlatform : MonoBehaviour
{
    [Header("Zamanlama")]
    public float timeToFall = 0.5f;  // Bastýktan kaç saniye sonra düþecek?
    public float respawnTime = 2.0f; // Düþtükten kaç saniye sonra geri gelecek?

    private Collider2D myCollider;
    private SpriteRenderer mySprite;
    private bool isCrumbling = false;

    void Start()
    {
        myCollider = GetComponent<Collider2D>();
        mySprite = GetComponent<SpriteRenderer>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Sadece oyuncu üstüne basarsa (ve zaten düþmüyorsa)
        if (collision.gameObject.CompareTag("Player") && !isCrumbling)
        {
            // Oyuncunun platformun ÜSTÜNDE olduðunu kontrol etmek iyi olur
            // (Kafasýný çarparsa düþmesin diye). 
            // Þimdilik basit tutalým, dokununca düþsün.
            StartCoroutine(CrumbleRoutine());
        }
    }

    private IEnumerator CrumbleRoutine()
    {
        isCrumbling = true;

        // 1. Titreme Efekti (Opsiyonel - Rengi grileþtirerek uyaralým)
        if (mySprite != null) mySprite.color = Color.gray;

        // Düþmeden önceki bekleme süresi
        yield return new WaitForSeconds(timeToFall);

        // 2. Platformu Kapat (Yok etme, sadece devre dýþý býrak)
        if (myCollider != null) myCollider.enabled = false;
        if (mySprite != null) mySprite.enabled = false;

        // Geri gelme süresi
        yield return new WaitForSeconds(respawnTime);

        // 3. Platformu Geri Getir
        if (myCollider != null) myCollider.enabled = true;
        if (mySprite != null)
        {
            mySprite.enabled = true;
            mySprite.color = Color.white; // Rengi düzelt
        }

        isCrumbling = false;
    }
}