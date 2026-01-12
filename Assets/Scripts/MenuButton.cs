using UnityEngine;
using UnityEngine.EventSystems; // Mouse olaylarýný algýlamak için ÞART!
using System.Collections;

public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Ayarlar")]
    public float buyumeOrani = 1.1f; // Buton %10 büyüsün
    public float hiz = 0.1f;         // Animasyon hýzý (küçükse hýzlý)

    private Vector3 orjinalBoyut;
    private Coroutine currentRoutine;

    private void Start()
    {
        // Oyun baþlayýnca butonun normal boyutunu hafýzaya al
        orjinalBoyut = transform.localScale;
    }

    // Mouse üstüne gelince çalýþýr
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(Boyutlandir(orjinalBoyut * buyumeOrani));
    }

    // Mouse gidince çalýþýr
    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(Boyutlandir(orjinalBoyut));
    }

    // Yumuþak geçiþ saðlayan fonksiyon
    IEnumerator Boyutlandir(Vector3 hedefBoyut)
    {
        float sure = 0;
        Vector3 baslangic = transform.localScale;

        while (sure < hiz)
        {
            sure += Time.unscaledDeltaTime; // Menüde zaman durursa bile çalýþsýn
            transform.localScale = Vector3.Lerp(baslangic, hedefBoyut, sure / hiz);
            yield return null;
        }
        transform.localScale = hedefBoyut;
    }
}
