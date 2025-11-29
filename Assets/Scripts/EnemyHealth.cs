using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Listeler için gerekli

public class EnemyHealth : MonoBehaviour
{
    [Header("Can Ayarlarý")]
    public float maxHealth = 30f;
    private float currentHealth;

    [Header("Stun (Sersemletme) Ayarlarý")]
    [Tooltip("Stun yediðinde devre dýþý býrakýlacak scriptleri buraya sürükle (Örn: PatrolEnemy, Turret).")]
    public List<MonoBehaviour> scriptsToDisable; // Durdurulacak scriptler listesi

    private SpriteRenderer enemySprite;
    private Color originalColor;
    private bool isStunned = false;

    void Start()
    {
        currentHealth = maxHealth;

        enemySprite = GetComponent<SpriteRenderer>();
        if (enemySprite != null) originalColor = enemySprite.color;
    }

    // --- HASAR BÖLÜMÜ ---
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} hasar aldý! Kalan Can: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} öldü!");
        Destroy(gameObject);
    }

    // --- STUN BÖLÜMÜ ---
    public void Stun(float duration)
    {
        if (isStunned) return;

        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;

        // 1. Scriptleri KAPAT
        foreach (var script in scriptsToDisable)
        {
            if (script != null) script.enabled = false;
        }

        // 2. Görsel Efekt (Mavi yap)
        if (enemySprite != null) enemySprite.color = Color.blue;
        Debug.Log($"{gameObject.name} DONDU!");

        // 3. Bekle
        yield return new WaitForSeconds(duration);

        // 4. Scriptleri AÇ
        foreach (var script in scriptsToDisable)
        {
            if (script != null) script.enabled = true;
        }

        // 5. Rengi Düzelt
        if (enemySprite != null) enemySprite.color = originalColor;
        isStunned = false;
        Debug.Log($"{gameObject.name} ÇÖZÜLDÜ!");
    }
}