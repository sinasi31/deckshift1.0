using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    public static CameraShake instance;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    /// <summary>
    /// Ekraný sallar.
    /// </summary>
    /// <param name="duration">Ne kadar sürecek? (örn: 0.1s)</param>
    /// <param name="magnitude">Ne kadar þiddetli olacak? (örn: 0.2)</param>
    public void Shake(float duration, float magnitude)
    {
        StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        Vector3 originalPos = transform.position; // Kameranýn orijinal yerini kaydet
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            // Rastgele bir x ve y ofseti belirle
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            // Kamerayý titret
            transform.position = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);

            elapsed += Time.deltaTime;

            yield return null; // Bir sonraki kareyi bekle
        }
        transform.position = originalPos;
    }
}