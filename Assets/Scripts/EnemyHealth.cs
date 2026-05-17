using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    [Header("Can Ayarları")]
    public float maxHealth = 30f;

    [Header("Head Bounce")]
    public bool canBeHeadBounced = true;

    [Header("Health Bar")]
    public GameObject healthBarPrefab;
    public Vector2 headBarOffset = new Vector2(0f, 1f);

    [Header("Stun")]
    [SerializeField] private SpriteRenderer[] stunSpriteRenderers = new SpriteRenderer[0];
    [SerializeField] private SkinnedMeshRenderer[] stunSkinnedRenderers = new SkinnedMeshRenderer[0];

    [Header("Efektler")]
    public GameObject damagePopupPrefab;

    private float currentHealth;

    // Polled by AI scripts — they return early while this is true.
    public bool IsStunned { get; private set; }

    private Color[] stunSpriteOriginalColors;
    private Color[] stunSkinnedOriginalColors;
    private Coroutine stunRoutineRef;

    // Damage flash — SpriteRenderer and SkinnedMeshRenderer both supported
    private SpriteRenderer flashSpriteRenderer;
    private SkinnedMeshRenderer flashSkinnedRenderer;
    private Color flashSpriteOriginalColor;
    private Color flashSkinnedOriginalColor;
    private bool flashSkinnedHasColor;

    private EnemyHealthBar healthBar;

    void Start()
    {
        currentHealth = maxHealth;

        stunSpriteOriginalColors = new Color[stunSpriteRenderers.Length];
        for (int i = 0; i < stunSpriteRenderers.Length; i++)
        {
            if (stunSpriteRenderers[i] != null)
                stunSpriteOriginalColors[i] = stunSpriteRenderers[i].color;
        }

        stunSkinnedOriginalColors = new Color[stunSkinnedRenderers.Length];
        for (int i = 0; i < stunSkinnedRenderers.Length; i++)
        {
            if (stunSkinnedRenderers[i] != null && stunSkinnedRenderers[i].material.HasProperty("_Color"))
                stunSkinnedOriginalColors[i] = stunSkinnedRenderers[i].material.color;
        }

        // Flash renderer: SkinnedMeshRenderer first (AeroBat etc.), fallback SpriteRenderer
        flashSkinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (flashSkinnedRenderer != null)
        {
            flashSkinnedHasColor = flashSkinnedRenderer.material.HasProperty("_Color");
            if (flashSkinnedHasColor) flashSkinnedOriginalColor = flashSkinnedRenderer.material.color;
        }

        flashSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (flashSpriteRenderer != null) flashSpriteOriginalColor = flashSpriteRenderer.color;

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
            Die();
    }

    private IEnumerator FlashRoutine()
    {
        if (flashSkinnedRenderer != null && flashSkinnedHasColor)
            flashSkinnedRenderer.material.SetColor("_Color", Color.white);

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

    // Calling Stun while already stunned extends the duration (cancels old timer, starts fresh).
    public void Stun(float duration)
    {
        if (stunRoutineRef != null)
            StopCoroutine(stunRoutineRef);
        stunRoutineRef = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        IsStunned = true;
        for (int i = 0; i < stunSpriteRenderers.Length; i++)
        {
            if (stunSpriteRenderers[i] != null) stunSpriteRenderers[i].color = Color.blue;
        }
        for (int i = 0; i < stunSkinnedRenderers.Length; i++)
        {
            if (stunSkinnedRenderers[i] != null && stunSkinnedRenderers[i].material.HasProperty("_Color"))
                stunSkinnedRenderers[i].material.SetColor("_Color", Color.blue);
        }
        Debug.Log($"{gameObject.name} DONDU!");

        yield return new WaitForSeconds(duration);

        IsStunned = false;
        for (int i = 0; i < stunSpriteRenderers.Length; i++)
        {
            if (stunSpriteRenderers[i] != null) stunSpriteRenderers[i].color = stunSpriteOriginalColors[i];
        }
        for (int i = 0; i < stunSkinnedRenderers.Length; i++)
        {
            if (stunSkinnedRenderers[i] != null && stunSkinnedRenderers[i].material.HasProperty("_Color"))
                stunSkinnedRenderers[i].material.SetColor("_Color", stunSkinnedOriginalColors[i]);
        }
        stunRoutineRef = null;
        Debug.Log($"{gameObject.name} ÇÖZÜLDÜ!");
    }
}
