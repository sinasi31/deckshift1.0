using UnityEngine;

[RequireComponent(typeof(JaceDensityObject))]
[RequireComponent(typeof(Rigidbody2D))]
public class JaceHeavyObjectDamage : MonoBehaviour
{
    [Header("Damage")]
    public int   damageAmount     = 3;
    public float minSpeedToDamage = 4f;

    private JaceDensityObject densityObj;
    private Rigidbody2D       rb;

    void Awake()
    {
        densityObj = GetComponent<JaceDensityObject>();
        rb         = GetComponent<Rigidbody2D>();
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (densityObj.State != JaceDensityState.Heavied) return;
        if (rb.linearVelocity.magnitude < minSpeedToDamage) return;

        JaceDroneEnemy enemy = col.gameObject.GetComponent<JaceDroneEnemy>();
        if (enemy != null)
            enemy.TakeDamage(damageAmount);
    }
}
