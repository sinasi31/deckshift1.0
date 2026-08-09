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

        // Scaled by the player's Freeze Frames setting, at the one chokepoint all 8 callers go
        // through. At 0 we must return BEFORE touching timeScale — a zero-length freeze would still
        // set timeScale to 0 and only restore it a frame later, which is a visible hitch.
        duration *= GameSettings.HitStopStrength;
        if (duration <= 0f) return;

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