using UnityEngine;
using System.Collections;

public class HitStop : MonoBehaviour
{
    public static HitStop instance;
    private bool isWaiting = false;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    public void Stop(float duration)
    {
        if (isWaiting) return;

        // Zamaný durdur
        Time.timeScale = 0.0f;
        StartCoroutine(Wait(duration));
    }

    private IEnumerator Wait(float duration)
    {
        isWaiting = true;

        // Gerçek dünyada bekle (Oyun donduðu için WaitForSecondsRealtime þart)
        yield return new WaitForSecondsRealtime(duration);

        // Zamaný devam ettir
        Time.timeScale = 1.0f;
        isWaiting = false;
    }
}