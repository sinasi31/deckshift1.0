using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Can Ayarlarý")]
    public float maxHealth = 30f;
    private float currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} hasar aldý! Kalan Can: {currentHealth}");

        // TODO: Kýrmýzý yanýp sönme efekti (Hit Effect) buraya eklenebilir.

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} öldü!");

        // Buraya patlama efekti, ses, loot düþürme vb. eklenebilir.

        Destroy(gameObject);
    }
}