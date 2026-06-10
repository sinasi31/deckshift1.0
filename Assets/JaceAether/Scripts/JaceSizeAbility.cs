using UnityEngine;

/// Hold R = grow, Hold F = shrink. Mouse-hover targeting — same feel as JaceDensityAbility.
/// Size changes persist when you release the key or move the mouse away.
/// Add to Player alongside JaceDensityAbility.
public class JaceSizeAbility : MonoBehaviour
{
    [Header("Range")]
    public float range = 5f;

    [Header("Speed (scale units per second)")]
    public float growSpeed   = 1f;
    public float shrinkSpeed = 1f;

    [Header("Selection Indicator (optional — can share the same prefab as density)")]
    public GameObject selectionIndicatorPrefab;

    private JaceSizeObject currentTarget;
    private GameObject     indicator;
    private Camera         cam;
    private Animator       animator;
    private bool           isAbilityActive;

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

        Vector2      mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits       = Physics2D.OverlapCircleAll(mouseWorld, 0.8f);

        JaceSizeObject obj = null;
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;
            var candidate = h.GetComponent<JaceSizeObject>();
            if (candidate != null) { obj = candidate; break; }
        }

        if (obj != null && Vector2.Distance(transform.position, obj.transform.position) <= range)
        {
            currentTarget = obj;
            if (indicator != null)
            {
                indicator.SetActive(true);
                indicator.transform.position = currentTarget.transform.position;
            }
            return;
        }

        // Size is kept on lost target — only clear the selection UI, not the object's size.
        currentTarget = null;
        if (indicator != null) indicator.SetActive(false);
    }

    void HandleInput()
    {
        if (currentTarget == null) { isAbilityActive = false; return; }

        if (Input.GetKey(KeyCode.R))
        {
            currentTarget.Grow(growSpeed * Time.deltaTime);
            isAbilityActive = true;
        }
        else if (Input.GetKey(KeyCode.F))
        {
            currentTarget.Shrink(shrinkSpeed * Time.deltaTime);
            isAbilityActive = true;
        }
        else
        {
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
