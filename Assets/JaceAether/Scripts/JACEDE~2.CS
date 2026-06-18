using UnityEngine;

/// Controls: Hold Q = lighten, Hold E = weigh down. Mouse aims.
/// Add to Player alongside the Cainos PlayerController.
/// densityLayer is optional — leave as Nothing to auto-detect any JaceDensityObject in range.
public class JaceDensityAbility : MonoBehaviour
{
    [Header("Range & Targeting")]
    public float     range        = 5f;
    public LayerMask densityLayer;          // optional layer filter; Nothing = check all layers

    [Header("Lighten (Q)")]
    public float lightenMass    = 0.05f;
    public float lightenGravity = -1.2f;

    [Header("Weigh (E)")]
    public float heavyMass    = 60f;
    public float heavyGravity = 10f;

    [Header("Selection Indicator (optional)")]
    public GameObject selectionIndicatorPrefab;

    private JaceDensityObject currentTarget;
    private GameObject        indicator;
    private Camera            cam;
    private Animator          animator;
    private bool              isAbilityActive;

    void Awake()
    {
        cam      = Camera.main;
        animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (selectionIndicatorPrefab != null)
        {
            indicator = Instantiate(selectionIndicatorPrefab);
            indicator.SetActive(false);
        }
    }

    void Update()
    {
        FindTarget();
        HandleInput();
    }

    void FindTarget()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        // If densityLayer is Nothing (value 0), fall back to checking all layers so the
        // ability works even when the Inspector mask isn't configured.
        int mask = (densityLayer.value != 0) ? densityLayer.value : ~0;

        // OverlapCircleAll so a floor/wall hit doesn't hide the object behind it.
        Collider2D[] hits = Physics2D.OverlapCircleAll(mouseWorld, 0.8f, mask);

        JaceDensityObject obj = null;
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;   // never target self
            var candidate = h.GetComponent<JaceDensityObject>();
            if (candidate != null) { obj = candidate; break; }
        }

        if (obj != null)
        {
            float dist = Vector2.Distance(transform.position, obj.transform.position);
            if (dist <= range)
            {
                if (currentTarget != null && currentTarget != obj)
                    currentTarget.ResetDensity();

                currentTarget = obj;

                if (indicator != null)
                {
                    indicator.SetActive(true);
                    indicator.transform.position = currentTarget.transform.position;
                }
                return;
            }
        }

        ClearTarget();
    }

    void HandleInput()
    {
        if (currentTarget == null) { isAbilityActive = false; return; }

        if (Input.GetKey(KeyCode.Q))
        {
            currentTarget.Lighten(lightenMass, lightenGravity);
            isAbilityActive = true;
        }
        else if (Input.GetKey(KeyCode.E))
        {
            currentTarget.Weigh(heavyMass, heavyGravity);
            isAbilityActive = true;
        }
        else
        {
            currentTarget.ResetDensity();
            isAbilityActive = false;
        }
    }

    void LateUpdate()
    {
        if (animator == null) return;
        if (isAbilityActive)
        {
            animator.SetBool("IsAttacking", true);
            animator.SetInteger("AttackAction", 14);
        }
        else
        {
            animator.SetBool("IsAttacking", false);
        }
    }

    void ClearTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.ResetDensity();
            currentTarget = null;
        }
        if (indicator != null)
            indicator.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
