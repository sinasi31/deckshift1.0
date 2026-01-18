using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SlotReel : MonoBehaviour
{
    [Header("Ayarlar")]
    public RectTransform stripTransform;
    public float spinSpeed = 2000f;

    // Elle girmene gerek yok, kod otomatik hesaplayacak
    private float symbolHeight;
    private int totalSymbolsInStrip; // Kopyalar DAHÝL toplam sayý
    private int realSymbolCount; // Kopyalar HARÝÇ ana sembol sayýsý
    private bool isSpinning = false;

    private void Start()
    {
        // Layout'un oturduðundan emin olalým
        Canvas.ForceUpdateCanvases();
        InitializeReel();
    }

    // Reel ölçümlerini yapan fonksiyon
    private void InitializeReel()
    {
        if (stripTransform.childCount == 0) return;

        // 1. Sembol yüksekliðini ilk elemandan otomatik al
        RectTransform firstChild = stripTransform.GetChild(0).GetComponent<RectTransform>();
        symbolHeight = firstChild.rect.height;

        // Eðer Layout Element kullanýyorsan ve rect.height 0 geliyorsa, LayoutElement'ten almayý dene
        if (symbolHeight <= 0)
        {
            var layoutEle = firstChild.GetComponent<LayoutElement>();
            if (layoutEle != null) symbolHeight = layoutEle.preferredHeight;
        }

        // 2. Toplam sembol sayýsýný al
        totalSymbolsInStrip = stripTransform.childCount;

        // Varsayým: Listenin sonuna 3 tane kopya ekledin.
        // O zaman gerçek sembol sayýn: Toplam - 3
        // Eðer kopya eklemediysen bu sayý hatalý olur! Lütfen en az 3 kopya ekle.
        int copyCount = 3;
        realSymbolCount = totalSymbolsInStrip - copyCount;

        // Baþlangýç pozisyonunu sýfýrla (En üste hizala)
        stripTransform.anchoredPosition = new Vector2(stripTransform.anchoredPosition.x, 0);
    }

    public void Spin(int resultIndex, float duration)
    {
        if (isSpinning) return;

        // Emin olmak için tekrar ölç (Bazen oyun baþýnda UI geç yüklenir)
        InitializeReel();

        StartCoroutine(SpinRoutine(resultIndex, duration));
    }

    private IEnumerator SpinRoutine(int resultIndex, float duration)
    {
        isSpinning = true;
        float elapsed = 0f;

        // Bir tam turun uzunluðu (Piksel cinsinden)
        // Sadece gerçek sembollerin boyu kadar döneceðiz, kopyalar tampon bölge olacak.
        float loopHeight = realSymbolCount * symbolHeight;

        // --- 1. DÖNME AÞAMASI ---
        while (elapsed < duration)
        {
            // Þeridi YUKARI doðru kaydýr (Unity UI'da aþaðý dizildiði için, görmek için yukarý itmeliyiz)
            float moveAmount = spinSpeed * Time.unscaledDeltaTime;
            Vector2 currentPos = stripTransform.anchoredPosition;
            currentPos.y += moveAmount;

            // Loop Kontrolü: Eðer þerit çok yukarý çýktýysa, baþa sar.
            // loopHeight kadar yukarý çýktýðýmýzda, 1. sembol ile kopyasý ayný yere gelmiþ demektir.
            if (currentPos.y >= loopHeight)
            {
                currentPos.y -= loopHeight; // Geriye ýþýnla
            }

            stripTransform.anchoredPosition = currentPos;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // --- 2. HEDEF HESAPLAMA ---
        // resultIndex: 0 (Kuru Kafa), 1 (Sembol1)...
        // Hedef Pozisyon: Index * Yükseklik
        float targetY = resultIndex * symbolHeight;

        // Þu anki pozisyonumuz
        float currentY = stripTransform.anchoredPosition.y;

        // Hedefimiz þu anki konumun gerisinde mi kaldý? (Örn: Biz 500'deyiz, Hedef 200)
        // O zaman hedefe bir tur boyu ekleyelim ki ileri doðru gidip duralým (Hedef 200 + LoopHeight)
        // Veya hedef çok ilerideyse (Biz 500'deyiz, Hedef 800), direkt oraya gidelim.

        // En temiz yöntem: En az 1 tur daha döndürüp öyle duralým.
        float finalTargetY = targetY;
        while (finalTargetY < currentY + symbolHeight) // Biraz mesafe býrak
        {
            finalTargetY += loopHeight;
        }

        // --- 3. YERÝNE OTURTMA (EASING) ---
        float t = 0;
        float durationStop = 0.5f; // Durma süresi
        Vector2 startPos = stripTransform.anchoredPosition;
        Vector2 endPos = new Vector2(startPos.x, finalTargetY);

        while (t < durationStop)
        {
            t += Time.unscaledDeltaTime;
            float progress = t / durationStop;

            // SmoothStep (Yumuþak duruþ) formülü
            progress = progress * progress * (3f - 2f * progress);

            stripTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, progress);
            yield return null;
        }

        // --- 4. SON AYAR (MODULO) ---
        // Durduðumuz yer muhtemelen çok büyük bir sayý (örn: Y=5400).
        // Bunu tekrar 0 ile loopHeight arasýna çekelim ki bir sonraki spin bozulmasýn.
        float normalizedY = finalTargetY % loopHeight;
        stripTransform.anchoredPosition = new Vector2(stripTransform.anchoredPosition.x, normalizedY);

        isSpinning = false;
    }
}