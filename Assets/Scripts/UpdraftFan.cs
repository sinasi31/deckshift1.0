using UnityEngine;

public class UpdraftFan : MonoBehaviour
{
    [Header("Ayarlar")]
    public float liftForce = 20f;
    public float maxUpwardSpeed = 10f;

    [Header("Görsel Efekt")]
    public ParticleSystem windParticles; // Inspector'dan buraya WindEffect'i sürükle

    private BoxCollider2D fanCollider;

    private void Awake()
    {
        fanCollider = GetComponent<BoxCollider2D>();
    }

    private void Start()
    {
        // Particle System'ýn þeklini (Shape) Collider boyutuna otomatik eþitle
        if (windParticles != null && fanCollider != null)
        {
            var shape = windParticles.shape;

            // Box þeklini seç
            shape.shapeType = ParticleSystemShapeType.Box;

            // Collider boyutunu al (X ve Y)
            // Not: Z eksenini 1 yapýyoruz çünkü 2D oynuyoruz
            shape.scale = new Vector3(fanCollider.size.x, fanCollider.size.y, 1f);

            // Collider offset'ini de ayarla (Eðer merkezi kaydýrdýysan)
            windParticles.transform.localPosition = fanCollider.offset;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        Rigidbody2D rb = other.attachedRigidbody;

        if (rb != null)
        {
            if (rb.linearVelocity.y < maxUpwardSpeed)
            {
                rb.AddForce(Vector2.up * liftForce);
            }
        }
    }
}