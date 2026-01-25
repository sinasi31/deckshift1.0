using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyHealth : MonoBehaviour
{
    [Header("Can Ayarlarý")]
    public float maxHealth = 30f;
    private float currentHealth;
    private SpriteRenderer spriteRenderer;

    [Header("Stun (Sersemletme) Ayarlarý")]
    [Tooltip("Stun yediðinde devre dýþý býrakýlacak scriptleri buraya sürükle (Örn: PatrolEnemy, Turret).")]
    public List<MonoBehaviour> scriptsToDisable; 
    [Header("Efektler")]
    public GameObject damagePopupPrefab; 

    private SpriteRenderer enemySprite;
    private Color originalColor;
    private bool isStunned = false;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;

        enemySprite = GetComponent<SpriteRenderer>();
        if (enemySprite != null) originalColor = enemySprite.color;
    }

    // --- HASAR BÖLÜMÜ ---
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        HitStop.instance.Stop(0.05f);
        HitStop.instance.Stop(0.15f);
        Debug.Log($"{gameObject.name} hasar aldý! Kalan Can: {currentHealth}");
        if (spriteRenderer != null)
        {
            StartCoroutine(FlashRoutine());
        }

        if (damagePopupPrefab != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);

            popup.GetComponent<DamagePopup>().Setup(Mathf.RoundToInt(damage));
        }
        // ---------------------------------

        if (currentHealth <= 0)
        {
            Die();
        }
    }
    private System.Collections.IEnumerator FlashRoutine()
    {
        // Rengi Kýrmýzý (veya istediðin renk) yap
        spriteRenderer.color = Color.red;

        // Çok kýsa bekle (0.1 saniye)
        yield return new WaitForSeconds(0.1f);

        // Rengi normale (Beyaz) döndür
        spriteRenderer.color = Color.white;
    }
    private void Die()
    {
        if (QuestSystem.instance != null)
        {
            // 1. Normal Kill Görevleri için:
            QuestSystem.instance.ReportEvent(QuestType.KillEnemy, 1);

            // 2. Air Kill (Havada Öldürme) Kontrolü:
            // Oyuncuyu bul ve havada mý diye bak
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null && !player.IsGroundedCheck())
            {
                QuestSystem.instance.ReportEvent(QuestType.AirKill, 1);
            }
        }
        Debug.Log($"{gameObject.name} öldü!");

        if (SkillManager.instance != null)
        {
            // 1. RECYCLE: +1 Shift
            if (SkillManager.instance.HasSkill(SkillType.Recycle))
            {
                if (GameManager.instance != null && GameManager.instance.player != null)
                    GameManager.instance.player.AddShift(1);
            }

            if (SkillManager.instance.HasSkill(SkillType.VampiricAura))
            {
                if (GameManager.instance != null && GameManager.instance.player != null)
                    GameManager.instance.player.Heal(5); // 5 Can ver
            }
        }
        Destroy(gameObject);
    }
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
        if (enemySprite != null) enemySprite.color = Color.blue;
        Debug.Log($"{gameObject.name} DONDU!");
        yield return new WaitForSeconds(duration);

        foreach (var script in scriptsToDisable)
        {
            if (script != null) script.enabled = true;
        }

        if (enemySprite != null) enemySprite.color = originalColor;
        isStunned = false;
        Debug.Log($"{gameObject.name} ÇÖZÜLDÜ!");
    }
}