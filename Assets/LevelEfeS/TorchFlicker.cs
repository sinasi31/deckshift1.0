using UnityEngine;
using UnityEngine.Rendering.Universal;

public class TorchFlicker : MonoBehaviour
{
    Light2D light2D;

    public float minIntensity = 0.8f;
    public float maxIntensity = 1.05f;
    public float flickerSpeed = 6f;

    void Awake()
    {
        light2D = GetComponent<Light2D>();
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
        light2D.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }
}
