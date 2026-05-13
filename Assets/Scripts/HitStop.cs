using UnityEngine;
using System.Collections;

public class HitStop : MonoBehaviour
{
    public static HitStop instance;
    private bool isWaiting = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    public void Stop(float duration)
    {
        if (isWaiting) return;

        // Zaman� durdur
        Time.timeScale = 0.0f;
        StartCoroutine(Wait(duration));
    }

    private IEnumerator Wait(float duration)
    {
        isWaiting = true;

        // Ger�ek d�nyada bekle (Oyun dondu�u i�in WaitForSecondsRealtime �art)
        yield return new WaitForSecondsRealtime(duration);

        // Zaman� devam ettir
        Time.timeScale = 1.0f;
        isWaiting = false;
    }
}