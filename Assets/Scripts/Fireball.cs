using UnityEngine;
using UnityEngine.UI;

public class Fireball : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 15f;
    public float lifeTime = 3f;

    [Header("VFX")]
    public GameObject explosionEffect;
    public GameObject trailEffect;
    // How large the VFX appear in world units. Tune this in the Inspector.
    // The pixel art sprites are 64px; at 0.03 scale they become ~1.9 world units wide.
    [Range(0.005f, 0.1f)] public float vfxWorldScale = 0.03f;

    public float damage = 10f;

    // The card that fired this, stamped at spawn.
    //
    // ⚠️ WITHOUT THIS, EVERY DAMAGE-TIME BLESSING SILENTLY DOES NOTHING ON FIREBALL — which is the
    // main attack card. DeckManager.AttributedCard is live only for the duration of ExecuteAction,
    // and a projectile lands whole seconds after that has returned and cleared it. So the shot
    // carries its own source and re-installs it around the hit. Any future delayed damage source
    // (a lingering pool, a summon) must do the same.
    [HideInInspector] public RuntimeCard sourceCard;

    private Rigidbody2D rb;
    private bool hasHit = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        rb.linearVelocity = transform.right * speed;
        Destroy(gameObject, lifeTime);

        if (trailEffect != null)
            SpawnUIVFX(trailEffect, transform, Vector3.zero, 0f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        if (other.GetComponent<Portal>() != null) return;

        IDamageable targetHealth = other.GetComponentInParent<IDamageable>();

        if (targetHealth != null)
        {
            hasHit = true;

            // Re-install this shot's card so the blessings on it apply, and so a kill is credited
            // to it. Cleared again immediately — a stale attribution would hand the next spike or
            // pogo bounce in the game a Grudge bonus it never earned.
            RuntimeCard prev = DeckManager.instance != null ? DeckManager.instance.AttributedCard : null;
            if (DeckManager.instance != null) DeckManager.instance.AttributedCard = sourceCard;

            // Route through the relic modifier (Whetstone / Midas Recoil / Glass Heart).
            float dealt = RelicManager.instance != null
                ? RelicManager.instance.ModifyPlayerDamage(damage, targetHealth as EnemyHealth)
                : damage;
            targetHealth.TakeDamage(dealt);

            if (DeckManager.instance != null) DeckManager.instance.AttributedCard = prev;
            if (CameraShake.instance != null)
                CameraShake.instance.Shake(0.15f, 0.5f);
            CreateExplosionEffect();
            Destroy(gameObject);
            return;
        }
        if (other.isTrigger) return;

        // Wall check
        if (other.gameObject.GetComponent<PlayerController>() == null)
        {
            hasHit = true;
            CreateExplosionEffect();
            Destroy(gameObject);
        }
    }

    void CreateExplosionEffect()
    {
        if (explosionEffect != null)
            SpawnUIVFX(explosionEffect, null, transform.position, 0.75f);
    }

    // Wraps a UI-based VFX prefab in a temporary World Space Canvas so it renders in the game world.
    // parent=null  -> free in scene at worldPos, destroyed after destroyAfter seconds.
    // parent set   -> child of that transform at localOffset, lives until parent is destroyed.
    void SpawnUIVFX(GameObject vfxPrefab, Transform parent, Vector3 localOrWorldPos, float destroyAfter)
    {
        GameObject canvasGO = new GameObject("VFX_Canvas");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        RectTransform rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(64f, 64f);

        if (parent != null)
        {
            canvasGO.transform.SetParent(parent);
            canvasGO.transform.localPosition = localOrWorldPos;
        }
        else
        {
            canvasGO.transform.position = localOrWorldPos;
        }

        canvasGO.transform.localScale = Vector3.one * vfxWorldScale;

        Instantiate(vfxPrefab, canvasGO.transform);

        if (destroyAfter > 0f)
            Destroy(canvasGO, destroyAfter);
    }
}
