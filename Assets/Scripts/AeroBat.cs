using UnityEngine;
using System.Collections;

public class AeroBat : MonoBehaviour
{
    [Header("Hareket Ayarları")]
    public float detectRange = 9f;   // Oyuncuyu görme mesafesi
    public float diveSpeed = 7f;     // Dalış hızı (DÜŞÜRÜLDÜ)
    public float returnSpeed = 3f;   // Geri dönüş hızı
    public float hoverHeight = 0.5f; // Havada sallanma miktarı

    [Header("Saldırı Hazırlığı (Nerf)")]
    public float windUpTime = 0.8f;  // Saldırmadan önce kaç saniye beklesin? (TEPKİ SÜRESİ)
    public GameObject alertIcon;     // Tepesinde çıkacak "!" objesi

    private Vector3 startPos;
    private Transform player;

    // Durum Kontrolü
    private bool isIdle = true;      // Devriye/Bekleme modu
    private bool isPreparing = false;// "!" çıktığı an
    private bool isDiving = false;   // Saldırı anı
    private bool isReturning = false;// Yerine dönme anı

    private Vector3 diveTarget;      // Nereye dalacak?

    private void Start()
    {
        startPos = transform.position;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        // Başlangıçta ünlem kapalı olsun
        if (alertIcon != null) alertIcon.SetActive(false);
    }

    private void Update()
    {
        if (player == null) return;

        // 1. GERİ DÖNÜŞ
        if (isReturning)
        {
            ReturnToPerch();
        }
        // 2. DALIŞ (SALDIRI)
        else if (isDiving)
        {
            DiveAttack();
        }
        // 3. HAZIRLIK (ÜNLEM ÇIKAN AN)
        else if (isPreparing)
        {
            // Hazırlanırken sadece oyuncuya dön, hareket etme
            LookAtPlayer();
        }
        // 4. BEKLEME (IDLE)
        else if (isIdle)
        {
            IdleHover();
            CheckForPlayer();
        }
    }

    // --- IDLE MODU ---
    void IdleHover()
    {
        // Olduğu yerde hafifçe süzülür
        float hoverY = Mathf.Sin(Time.time * 2f) * hoverHeight;
        transform.position = startPos + Vector3.up * hoverY;
    }

    void CheckForPlayer()
    {
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist < detectRange)
        {
            // Hemen saldırma! Hazırlık sürecini başlat.
            StartCoroutine(PrepareAttackRoutine());
        }
    }

    // --- HAZIRLIK MODU (YENİ) ---
    IEnumerator PrepareAttackRoutine()
    {
        isIdle = false;
        isPreparing = true;

        // 1. Ünlemi Göster
        if (alertIcon != null)
        {
            alertIcon.SetActive(true);
            // İstersen burada bir ses çal: "EnemyAlertSound"
        }

        // 2. Bekle (Oyuncuya kaçma fırsatı ver)
        yield return new WaitForSeconds(windUpTime);

        // 3. Ünlemi Kapat ve Hedefi Kilitle
        if (alertIcon != null) alertIcon.SetActive(false);

        diveTarget = player.position; // O an oyuncu neredeyse oraya kilitle

        // Oyuncunun biraz arkasına geçsin ki tam üstüne düşmesin (Opsiyonel zorluk)
        // diveTarget += (player.position - transform.position).normalized * 2f; 

        isPreparing = false;
        isDiving = true;
    }

    // --- DALIŞ MODU ---
    void DiveAttack()
    {
        // Hedefe doğru uç
        transform.position = Vector3.MoveTowards(transform.position, diveTarget, diveSpeed * Time.deltaTime);

        // Hedefe vardı mı? (Veya çok yaklaştı mı?)
        if (Vector3.Distance(transform.position, diveTarget) < 0.2f)
        {
            isDiving = false;
            isReturning = true;
        }
    }

    // --- DİĞERLERİ ---
    void ReturnToPerch()
    {
        // Yüzünü yuvaya dön
        FaceTarget(startPos);

        transform.position = Vector3.MoveTowards(transform.position, startPos, returnSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, startPos) < 0.1f)
        {
            isReturning = false;
            isIdle = true;
        }
    }

    void LookAtPlayer()
    {
        FaceTarget(player.position);
    }

    void FaceTarget(Vector3 target)
    {
        Vector3 scale = transform.localScale;
        if (target.x < transform.position.x) scale.x = -Mathf.Abs(scale.x); // Sola bak
        else scale.x = Mathf.Abs(scale.x); // Sağa bak
        transform.localScale = scale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(15);
                // Vurunca geri sekme efekti
                pc.ApplyKnockback((other.transform.position - transform.position).normalized * 5f);
            }

            // Çarpar çarpmaz eve dön
            StopAllCoroutines(); // Hazırlığı iptal et
            if (alertIcon != null) alertIcon.SetActive(false);
            isPreparing = false;
            isDiving = false;
            isReturning = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        if (isDiving) Gizmos.DrawLine(transform.position, diveTarget);
    }
}