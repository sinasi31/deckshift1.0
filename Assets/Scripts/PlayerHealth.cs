using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public bool isInvincible = false;

    [Header("Audio")]
    [SerializeField] AudioClip hurtSound;
    [SerializeField] AudioClip deathSound;
    [SerializeField] float deathVolume = 1f;

    private float currentHealth;
    private bool isDead = false;
    private float baseMaxHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public float HealthPercent => maxHealth > 0 ? currentHealth / maxHealth : 0f;

    // The unmodified max HP, captured before any relic touches it. Relic passives are always
    // recomputed from THIS (see RelicManager.RecomputePassives) so selling a relic reverses it
    // exactly, regardless of what order relics were gained or sold in.
    public float BaseMaxHealth => baseMaxHealth;

    public event System.Action<float> OnDamaged;
    public event System.Action OnDied;
    public event System.Action<Vector2> OnKnockback;
    public event System.Action OnFallRespawn;

    private Rigidbody2D rb;
    private Animator animator;
    private AudioSource audioSource;
    private PlayerController playerController;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
        playerController = GetComponent<PlayerController>();
        baseMaxHealth = maxHealth;
    }

    // Applied by relic passives (RelicManager.RecomputePassives). Clamp-only, deliberately:
    // healing on a capacity GAIN would let the player equip Glass Heart (halved max HP), take
    // damage, then SELL it for a free ~50 HP refill. Never drops below 1, so equipping a
    // max-HP-reducing relic can't kill you outright.
    public void SetMaxHealth(float newMax)
    {
        if (isDead) return;

        maxHealth = Mathf.Max(1f, newMax);
        currentHealth = Mathf.Clamp(currentHealth, 1f, maxHealth);
    }

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (isInvincible || isDead) return;

        currentHealth = Mathf.Max(currentHealth - damage, 0f);

        SfxManager.PlayOn(audioSource, hurtSound);

        if (animator != null) animator.SetTrigger("InjuredFront");

        Debug.Log($"Hasar Alındı! Kalan Can: {currentHealth}");

        OnDamaged?.Invoke(damage);

        if (currentHealth <= 0)
        {
            // Phoenix Cog: once per run, a lethal hit leaves you at 1 HP and erupts instead.
            if (RelicManager.instance != null && RelicManager.instance.TryConsumePhoenixCog())
            {
                currentHealth = 1f;
                RelicManager.instance.PhoenixBlast(transform.position);
                StartCoroutine(GrantInvincibility(1.5f));   // mercy window so 1 HP isn't instant death
                return;
            }

            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // Adrenaline slow-mo scales Time.timeScale/fixedDeltaTime and restores them in a
        // coroutine that dies with the scene load below — so a death during slow-mo would
        // leave the whole game at 40% speed forever. Reset defensively on every death;
        // outside Adrenaline these are already 1 / 0.02 (audit_report.md Critical #3).
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (playerController.currentState == PlayerState.CometDiving)
            playerController.EndCometDive();

        Debug.Log("💀 Oyuncu Öldü! Ses Çalınıyor...");

        if (deathSound != null)
        {
            if (Camera.main != null)
                SfxManager.PlayAtPoint(deathSound, Camera.main.transform.position, deathVolume);
            else
                SfxManager.PlayAtPoint(deathSound, transform.position, deathVolume);
        }

        if (animator != null) animator.SetBool("IsDead", true);
        if (rb != null) rb.simulated = false;

        OnDied?.Invoke();

        StartCoroutine(WaitAndReload());
    }

    public void Kill() => Die();

    private IEnumerator WaitAndReload()
    {
        yield return new WaitForSeconds(1.5f);
        SceneManager.LoadScene("GameOverScene");
    }

    public void ApplyKnockback(Vector2 knockbackForce)
    {
        OnKnockback?.Invoke(knockbackForce);
        StartCoroutine(KnockbackRoutine(knockbackForce));
    }

    private IEnumerator KnockbackRoutine(Vector2 knockbackForce)
    {
        if (playerController.currentState == PlayerState.CometDiving)
            playerController.EndCometDive();
        playerController.ChangeState(PlayerState.KnockedBack);
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(knockbackForce, ForceMode2D.Impulse);
        yield return new WaitForSeconds(0.2f);
        if (playerController.currentState == PlayerState.KnockedBack)
            playerController.ChangeState(PlayerState.Jumping);
    }

    public void FallAndRespawn()
    {
        if (playerController.currentState == PlayerState.CometDiving)
            playerController.EndCometDive();
        rb.linearVelocity = Vector2.zero;
        transform.position = playerController.currentRoomEntryPoint;
        OnFallRespawn?.Invoke();
    }

    public IEnumerator GrantInvincibility(float duration)
    {
        isInvincible = true;
        yield return new WaitForSeconds(duration);
        isInvincible = false;
    }
}
