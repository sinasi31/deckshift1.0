using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake instance;

    private float shakeIntensity;
    private float shakeTimer;
    private float shakeDuration;
    public Vector2 shakeOffset;

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

    public void Shake(float intensity, float duration)
    {
        shakeIntensity = intensity;
        shakeDuration = duration;
        shakeTimer = duration;
    }

    private void Update()
    {
        if (shakeTimer > 0)
        {
            // Proportional fade-out: full intensity at start, zero at end
            float currentIntensity = shakeIntensity * (shakeTimer / shakeDuration);
            shakeOffset = Random.insideUnitCircle * currentIntensity;
            shakeTimer -= Time.unscaledDeltaTime;

            if (shakeTimer <= 0f)
                shakeOffset = Vector2.zero;
        }
    }
}