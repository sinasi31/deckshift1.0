using UnityEngine;

/// Add to Player prefab alongside the Cainos PlayerController.
public class JacePlayerHealth : MonoBehaviour
{
    [Header("Stats")]
    public int   maxHealth             = 5;
    public float invincibilityDuration = 1.2f;

    public System.Action<int, int> OnHealthChanged;
    public System.Action           OnDeath;

    public int CurrentHealth { get; private set; }

    private float          invincibilityTimer;
    private SpriteRenderer sr;
    private bool           isDead;

    void Start()
    {
        CurrentHealth = maxHealth;
        sr = GetComponent<SpriteRenderer>();
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    void Update()
    {
        if (invincibilityTimer > 0f)
        {
            invincibilityTimer -= Time.deltaTime;
            if (sr != null)
                sr.enabled = Mathf.Sin(invincibilityTimer * 25f) > 0f;
            if (invincibilityTimer <= 0f && sr != null)
                sr.enabled = true;
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead || invincibilityTimer > 0f) return;
        CurrentHealth      = Mathf.Max(0, CurrentHealth - amount);
        invincibilityTimer = invincibilityDuration;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (CurrentHealth == 0) Die();
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        OnDeath?.Invoke();
        JaceGameManager.Instance?.GameOver();
    }
}
