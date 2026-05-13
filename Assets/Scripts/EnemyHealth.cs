using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyHealth : MonoBehaviour
{
    [Header("Can Ayarları")]
    public float maxHealth = 30f;

    [Header("Head Bounce")]
    public bool canBeHeadBounced = true;

    [Header("Health Bar")]
    public GameObject healthBarPrefab;
    public Vector2 headBarOffset = new Vector2(0f, 1f);

    [Header("Stun (Sersemletme) Ayarları")]
    [Tooltip("Stun yediğinde devre dışı bırakılacak scriptleri buraya sürükle (Örn: PatrolEnemy, Turret).")]
    public List<MonoBehaviour> scriptsToDisable;

    [Header("Efektler")]
    public GameObject damagePopupPrefab;

    private float currentHealth;

    // Stun için orijinal renk
    private SpriteRenderer enemySprite;
    private Color originalColor;
    private bool isStunned = false;

    // Hasar flash — SpriteRenderer ve SkinnedMeshRenderer desteklenir
    private SpriteRenderer flashSpriteRenderer;
    private SkinnedMeshRenderer flashSkinnedRenderer;
    private Color flashSpriteOriginalColor;
    private Color flashSkinnedOriginalColor;
    private bool flashSkinnedHasColor;

    private EnemyHealthBar healthBar;

    void Start()
    {
        currentHealth = maxHealth;

        // Stun renk referansı (root SpriteRenderer)
        enemySprite = GetComponent<SpriteRenderer>();
        if (enemySprite != null) originalColor = enemySprite.color;

        // Flash renderer: SkinnedMeshRenderer öncelikli (AeroBat gibi), yoksa SpriteRenderer
        flashSkinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (flashSkinnedRenderer != null)
        {
            flashSkinnedHasColor = flashSkinnedRenderer.material.HasProperty("_Color");
            if (flashSkinnedHasColor) flashSkinnedOriginalColor = flashSkinnedRenderer.material.color;
        }

        flashSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (flashSpriteRenderer != null) flashSpriteOriginalColor = flashSpriteRenderer.color;

        // Health bar spawn
        if (healthBarPrefab != null)
        {
            GameObject barGO = Instantiate(healthBarPrefab);
            healthBar = barGO.GetComponent<EnemyHealthBar>();
            if (healthBar != null)
            {
                Collider2D col = GetComponent<Collider2D>();
                float barWidth = col != null ? col.bounds.size.x * 1.2f : 1f;
                healthBar.Initialize(transform, headBarOffset, barWidth);
                healthBar.SetHealth(currentHealth, maxHealth);
            }
        }
    }

    // --- HASAR BÖLÜMÜ ---
    public void TakeDamage(float damage, Transform damageSource = null)
    {
        currentHealth -= damage;
        if (HitStop.instance != null) HitStop.instance.Stop(0.15f);
        Debug.Log($"{gameObject.name} hasar aldı! Kalan Can: {currentHealth}");

        StartCoroutine(FlashRoutine());

        ShieldEnemy shield = GetComponent<ShieldEnemy>();
        if (shield != null && damageSource != null)
        {
            if (shield.IsBlocking(damageSource.position))
            {
                Debug.Log("BLOKLANDI! (Metal Sesi Çal)");
                return;
            }
        }

        if (damagePopupPrefab != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            popup.GetComponent<DamagePopup>().Setup(Mathf.RoundToInt(damage));
        }

        healthBar?.SetHealth(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator FlashRoutine()
    {
        // SkinnedMeshRenderer (AeroBat vb.) beyaz flash
        if (flashSkinnedRenderer != null && flashSkinnedHasColor)
            flashSkinnedRenderer.material.SetColor("_Color", Color.white);

        // SpriteRenderer beyaz flash
        if (flashSpriteRenderer != null)
            flashSpriteRenderer.color = Color.white;

        yield return new WaitForSeconds(0.1f);

        if (flashSkinnedRenderer != null && flashSkinnedHasColor)
            flashSkinnedRenderer.material.SetColor("_Color", flashSkinnedOriginalColor);

        if (flashSpriteRenderer != null)
            flashSpriteRenderer.color = flashSpriteOriginalColor;
    }

    private void Die()
    {
        if (healthBar != null) Destroy(healthBar.gameObject);

        if (RelicManager.instance != null)
            RelicManager.instance.OnEnemyKilled();

        if (QuestSystem.instance != null)
        {
            QuestSystem.instance.ReportEvent(QuestType.KillEnemy, 1);

            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null && !player.IsGroundedCheck())
                QuestSystem.instance.ReportEvent(QuestType.AirKill, 1);
        }

        Debug.Log($"{gameObject.name} öldü!");

        if (SkillManager.instance != null && SkillManager.instance.HasSkill(SkillType.Overclock))
        {
            if (DeckManager.instance != null)
            {
                DeckManager.instance.isNextCardFree = true;
                Debug.Log("OVERCLOCK AKTİF: Sonraki kart bedava!");
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

        foreach (var script in scriptsToDisable)
            if (script != null) script.enabled = false;

        if (enemySprite != null) enemySprite.color = Color.blue;
        Debug.Log($"{gameObject.name} DONDU!");

        yield return new WaitForSeconds(duration);

        foreach (var script in scriptsToDisable)
            if (script != null) script.enabled = true;

        if (enemySprite != null) enemySprite.color = originalColor;
        isStunned = false;
        Debug.Log($"{gameObject.name} ÇÖZÜLDÜ!");
    }
}
