using UnityEngine;
using System.Collections;

public class BreakableWall : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHP = 30f;
    [SerializeField] private GameObject breakVFX;
    [SerializeField] private AudioClip breakSound;
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private float headBarOffset = 1f;

    private float currentHP;
    private EnemyHealthBar healthBar;

    // Flash — SpriteRenderer first (standard dungeon props); SkinnedMeshRenderer fallback (Cainos-style rigs).
    private SpriteRenderer spriteRenderer;
    private Color spriteOriginalColor;

    private SkinnedMeshRenderer skinnedRenderer;
    private MaterialPropertyBlock propBlock;
    private Color skinnedOriginalColor = Color.white;

    private void Awake()
    {
        currentHP = maxHP;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
            spriteOriginalColor = spriteRenderer.color;

        skinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skinnedRenderer != null)
        {
            propBlock = new MaterialPropertyBlock();
            if (skinnedRenderer.sharedMaterial != null && skinnedRenderer.sharedMaterial.HasProperty("_Color"))
                skinnedOriginalColor = skinnedRenderer.sharedMaterial.GetColor("_Color");
        }
    }

    private void Start()
    {
        if (healthBarPrefab != null)
        {
            GameObject barGO = Instantiate(healthBarPrefab);
            healthBar = barGO.GetComponent<EnemyHealthBar>();
            if (healthBar != null)
            {
                Collider2D col = GetComponent<Collider2D>();
                float barWidth = col != null ? col.bounds.size.x * 1.2f : 1f;
                healthBar.Initialize(transform, new Vector2(0f, headBarOffset), barWidth);
                healthBar.SetHealth(currentHP, maxHP);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentHP <= 0f) return;

        currentHP -= damage;
        StartCoroutine(FlashRoutine());
        healthBar?.SetHealth(currentHP, maxHP);

        if (currentHP <= 0f)
            Break();
    }

    private IEnumerator FlashRoutine()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;

        if (skinnedRenderer != null)
        {
            skinnedRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", Color.white);
            skinnedRenderer.SetPropertyBlock(propBlock);
        }

        yield return new WaitForSeconds(0.08f);

        if (spriteRenderer != null)
            spriteRenderer.color = spriteOriginalColor;

        if (skinnedRenderer != null)
        {
            skinnedRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", skinnedOriginalColor);
            skinnedRenderer.SetPropertyBlock(propBlock);
        }
    }

    private void Break()
    {
        if (healthBar != null) Destroy(healthBar.gameObject);

        if (breakVFX != null)
            Instantiate(breakVFX, transform.position, Quaternion.identity);

        SfxManager.PlayAtPoint(breakSound, transform.position);

        CameraShake.instance?.Shake(0.1f, 0.15f);
        Destroy(gameObject);
    }
}
