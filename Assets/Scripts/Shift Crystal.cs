using UnityEngine;

public class ShiftCrystal : MonoBehaviour
{
    [Header("Oynanış")]
    public int shiftAmount = 1;
    public bool destroyOnCollect = true; 

    [Header("Animasyon")]
    public float floatSpeed = 1.5f;
    public float floatHeight = 0.15f;
    
    public float pulseSpeed = 2f;
    
    [Range(0f, 0.5f)] // Unity'de yanlışlıkla çok açma diye kilitledim
    public float pulsePercentage = 0.1f; // %10 Büyüme (İdeal oran)

    [Header("Efektler")]
    public GameObject collectEffect;
    public AudioClip collectSound;

    private Vector3 startPos;
    private Vector3 initialScale;

    void Start()
    {
        startPos = transform.position;
        initialScale = transform.localScale;
    }

    void Update()
    {
        // YÜZME
        float newY = startPos.y + (Mathf.Sin(Time.time * floatSpeed) * floatHeight);
        transform.position = new Vector3(startPos.x, newY, startPos.z);

        // NEFES ALMA (ÇARPMA İŞLEMİ)
        // Objen 0.06 da olsa, 100 de olsa sadece %10 büyür.
        float wave = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f; // 0-1 arası dalga
        float multiplier = 1f + (wave * pulsePercentage); // 1.0 - 1.1 arası çarpan
        
        transform.localScale = initialScale * multiplier; 
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) Collect();
    }

    private void Collect()
    {
        // 1. Shift ekle
        if (GameManager.instance != null && GameManager.instance.player != null)
            GameManager.instance.player.AddShift(shiftAmount);

        // 2. Efekt oluştur
        if (collectEffect != null) 
            Instantiate(collectEffect, transform.position, Quaternion.identity);
        
        // 3. SESİ ÇAL (DÜZELTME BURADA)
        if (collectSound != null) 
        {
            // Sesi objenin olduğu yerde değil, KAMERANIN olduğu yerde çalıyoruz.
            // Böylece mesafe 0 oluyor ve ses KISILMIYOR.
            AudioSource.PlayClipAtPoint(collectSound, Camera.main.transform.position);
        }

        // 4. Yok et
        if (destroyOnCollect) 
            Destroy(gameObject);
    }
}