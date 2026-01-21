using UnityEngine;
using Unity.Cinemachine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake instance;

    private CinemachineCamera cinemachineCamera;
    private CinemachineBasicMultiChannelPerlin perlinComponent;

    private float shakeTimer;

    // YENÝ: Bu çarpan sayesinde koddan gelen küçük sayýlarý (1, 2 gibi) devasa sarsýntýlara çevireceðiz.
    [Header("Sarsýntý Ayarlarý")]
    public float shakeMultiplier = 5f; // Bunu Inspector'dan arttýrabilirsin (örn: 10 yap)

    private void Awake()
    {
        if (instance == null) instance = this;

        cinemachineCamera = GetComponent<CinemachineCamera>();
        perlinComponent = GetComponent<CinemachineBasicMultiChannelPerlin>();
    }

    public void Shake(float intensity, float time)
    {
        if (perlinComponent != null)
        {
            // Gelen þiddeti çarpan ile büyüt
            perlinComponent.AmplitudeGain = intensity * shakeMultiplier;

            shakeTimer = time;
        }
    }

    private void Update()
    {
        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;

            if (shakeTimer <= 0f)
            {
                if (perlinComponent != null)
                {
                    perlinComponent.AmplitudeGain = 0f;
                }
            }
        }
    }
}