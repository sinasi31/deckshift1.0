using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TutorialMenu : MonoBehaviour
{
    [Header("Slaytlar")]
    public GameObject[] slides; // Unity'de hazýrladýðýn panel sayfalarý (Sayfa1, Sayfa2...)
    public TextMeshProUGUI pageNumberText;

    [Header("Butonlar")]
    public Button nextButton;
    public Button prevButton;

    private int currentIndex = 0;

    private void OnEnable()
    {
        // Panel açýlýnca ilk sayfaya dön
        currentIndex = 0;
        UpdateSlides();
    }

    public void NextSlide()
    {
        if (currentIndex < slides.Length - 1)
        {
            currentIndex++;
            UpdateSlides();
        }
    }

    public void PrevSlide()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            UpdateSlides();
        }
    }

    private void UpdateSlides()
    {
        // Tüm slaytlarý kapat, sadece aktif olaný aç
        for (int i = 0; i < slides.Length; i++)
        {
            slides[i].SetActive(i == currentIndex);
        }

        // Buton durumlarýný ayarla (Ýlk sayfadaysa Geri butonu pasif olsun vb.)
        if (prevButton) prevButton.interactable = (currentIndex > 0);
        if (nextButton) nextButton.interactable = (currentIndex < slides.Length - 1);

        // Sayfa numarasý yazdýr
        if (pageNumberText)
            pageNumberText.text = $"{currentIndex + 1} / {slides.Length}";
    }
}