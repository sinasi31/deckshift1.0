using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    internal Rigidbody2D rb;
    private Animator animator;
    internal Camera mainCamera;
    private CardActionExecutor cardActionExecutor;

    [Header("Visual Settings")]
    public GameObject visualModel; // YENİ: Hiyerarşideki PF Skeleton objesini buraya sürükleyeceğiz!

    [Header("Jump Feel Settings")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Durumlar")]
    public bool isPeeking = false;

    [Header("Physics Checks")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public Transform ceilingCheck;
    public float ceilingCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Air Settings")]
    public int maxAirJumps = 1;
    private int currentAirJumps = 0;

    public GameObject platformPrefab;
    private bool isPhasing = false;
    private float verticalInput;
    private Vector3 originalScale;
    private Vector3 currentRoomEntryPoint;

    [Header("Fall Settings")]
    public float fallDamage = 20f;

    private float _headBounceCooldown;

    [Header("Gold Settings")]
    public int currentGold = 0;
    public event System.Action<int> OnGoldChanged;

    [Header("Audio Settings")]
    public AudioClip hurtSound;
    public AudioSource audioSource;
    public AudioClip dashSound;
    public AudioClip fireballCastSound;
    public AudioClip cometDiveSound;
    public AudioClip phaseSound;
    public AudioClip adrenalineSound;
    public AudioClip createPlatformSound;
    public AudioClip vampireBiteSound;
    public AudioClip glassVailSound;
    public AudioClip jumpSound;
    public AudioClip leapSound;
    public AudioClip deathSound;
    public float deathVolume = 1f;
    public AudioClip spendSound;
    public AudioClip warningSoundClip;
    public float soundVolume = 1f;

    [Header("VFX Settings")]
    public GameObject biteEffectPrefab;
    public GameObject leapEffectPrefab;
    public TrailRenderer diveTrail;
    public GameObject diveImpactPrefab;
    public float diveSpeed = 25f;
    private bool isDiving = false;
    public GameObject dashEffectPrefab;

    [Header("Adrenaline VFX")]
    public GameObject ghostPrefab;
    public float adrenalineSpeedMult = 2f;
    public float ghostDelay = 0.05f;

    private float ghostTimer;
    private bool isAdrenalineActive = false;
    private float defaultMoveSpeed;

    [Header("Portal Settings")]
    public GameObject portalPrefab;
    public float portalMaxRange = 10f;
    public int portalCost = 2;
    private Portal firstPortalInstance;

    [Header("Wall Settings")]
    public Transform wallCheck;
    public float wallCheckDistance = 0.5f;
    public float wallSlideSpeed = 2f;
    public Vector2 wallJumpForce = new Vector2(10f, 15f);
    private bool isWallDetected;
    private bool canWallCling = false;
    private bool isWallSliding;

    [Header("Quest Tracking")]
    private bool tookDamageThisRoom = false;
    public bool TookDamageThisRoom { get { return tookDamageThisRoom; } }

    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    private float moveInput;

    [Header("Health Settings")]
    public float maxHealth = 100f;
    private float currentHealth;
    public bool isInvincible = false;
    public float CurrentHealth { get { return currentHealth; } }
    public float MaxHealth { get { return maxHealth; } }

    private bool isDead = false;

    [Header("Jump Settings")]
    public float defaultJumpForce = 10f;
    private bool freeAirJumpUsed = false;

    [Header("Shift Settings")]
    public int maxShift = 3;
    public int currentShift;
    public int GetCurrentShift() { return currentShift; }

    [Header("Comet Dive Settings")]
    public float cometSpeed = 25f;
    public float cometDamage = 40f;
    public float cometRadius = 3f;
    public GameObject cometImpactEffect;

    [Header("Adrenaline Card Settings")]
    public float adrenalineDuration = 3f;
    public float slowMotionFactor = 0.4f;
    public float speedBoostMultiplier = 1.5f;

    public PlayerState currentState;
    internal bool isGrounded;

    [Header("Combat Settings")]
    public GameObject fireballPrefab;
    public Transform firePoint;
    public float wailRange = 10f;
    public float biteRange = 1.5f;
    public float biteHealAmount = 10f;
    public LayerMask enemyLayer;

    [Header("Interaction Settings")]
    public float interactionRange = 2f;
    public LayerMask interactableLayer;

    [Header("Stagger Settings")]
    public int staggerCount = 0;
    public int maxStaggerUses = 3;
    public float staggerJumpForce = 5f;
    public float staggerDamage = 5f;
    public float staggerRadius = 2f;
    public GameObject staggerEffect;

    // Gravity reversal state
    private bool isGravityReversed = false;
    private float originalGravityScale;
    private Coroutine gravityReversalCoroutine;
    private float visualRotationZ = 0f;
    private Vector3 originalVisualLocalPos;
    private float originalVisualScaleX;
    internal bool isFacingRight = true;

    [Header("Gravity Reversal")]
    // Tune in Play mode: feet should just touch the ceiling when flipped
    [SerializeField] private float visualFlipYOffset = 2.0f;

    // Cached renderer for warning flash (same approach as EnemyHealth)
    private SkinnedMeshRenderer playerSkinnedRenderer;
    private bool playerRendererHasColor;
    private Color playerRendererOriginalColor;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        mainCamera = Camera.main;
        cardActionExecutor = GetComponent<CardActionExecutor>();

        // Cache SkinnedMeshRenderer for gravity-reversal warning flash
        playerSkinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (playerSkinnedRenderer != null)
        {
            playerRendererHasColor = playerSkinnedRenderer.material.HasProperty("_Color");
            if (playerRendererHasColor)
                playerRendererOriginalColor = playerSkinnedRenderer.material.color;
        }

        if (visualModel != null)
        {
            originalVisualLocalPos = visualModel.transform.localPosition;
            originalVisualScaleX = visualModel.transform.localScale.x;
        }
    }

    void Start()
    {
        originalScale = transform.localScale;
        currentHealth = maxHealth;
        currentShift = maxShift;
        ChangeState(PlayerState.Idle);
    }

    void Update()
    {
        if (isPeeking)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        if (isDead) return;

        bool wasGrounded = isGrounded;
        isGrounded = IsGroundedCheck();
        if (isGrounded)
        {
            currentAirJumps = 0;
            freeAirJumpUsed = false;
        }
        isWallDetected = WallCheck();

        if (isGrounded && !wasGrounded)
        {
            currentAirJumps = 0;
        }

        if (GameManager.instance != null && GameManager.instance.currentState == GameState.Paused)
        {
            moveInput = 0;
            verticalInput = 0;
            return;
        }

        HandleCardInput();

        if (Input.GetKeyDown(KeyCode.E))
        {
            CheckInteraction();
        }

        if (currentState == PlayerState.InCannon) return;

        if (isPhasing)
        {
            moveInput = Input.GetAxisRaw("Horizontal");
            verticalInput = Input.GetAxisRaw("Vertical");
        }
        else
        {
            if (Input.GetButtonDown("Jump"))
            {
                HandleJumpInput();
            }

            if (currentState == PlayerState.Idle || currentState == PlayerState.Running || currentState == PlayerState.Jumping)
                moveInput = Input.GetAxisRaw("Horizontal");
            else
                moveInput = 0;

            verticalInput = 0;
        }

        // --- BETTER JUMP MANTIĞI ---
        // gravitySign flips fall/low-jump-cut direction when gravity is reversed.
        // Fall check: velocity dot gravitySign < 0 means moving against gravity (falling).
        // Low-jump cut: velocity dot gravitySign > 0 means rising; multiplied force sign follows.
        float gravitySign = isGravityReversed ? -1f : 1f;
        if (rb.linearVelocity.y * gravitySign < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime * gravitySign;
        }
        else if (rb.linearVelocity.y * gravitySign > 0 && !Input.GetKey(KeyCode.Space))
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime * gravitySign;
        }

        if (!isPhasing)
        {
            HandleStateTransitions();
            UpdateAnimations();
        }
    }

    private void HandleCardInput()
    {
        if (DeckManager.instance == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) DeckManager.instance.SelectCard(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) DeckManager.instance.SelectCard(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) DeckManager.instance.SelectCard(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) DeckManager.instance.SelectCard(3);

        if (Input.GetMouseButtonDown(0))
        {
            DeckManager.instance.TryCastSelectedCard();
        }

        if (Input.GetMouseButtonDown(1))
        {
            DeckManager.instance.DeselectCard();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            DeckManager.instance.ReloadHand();
        }
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        OnGoldChanged?.Invoke(currentGold);

        if (QuestSystem.instance != null)
        {
            QuestSystem.instance.ReportEvent(QuestType.GoldAccumulate, amount);
        }
    }

    public bool TrySpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            OnGoldChanged?.Invoke(currentGold);
            if (spendSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(spendSound, soundVolume);
            }
            return true;
        }
        return false;
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        // GÜNCELLENDİ: Yeni paket "MovingBlend" kullanıyor (0.0 Durma, 1.0 Koşma)
        float movingBlend = (Mathf.Abs(moveInput) > 0.1f) ? 1.0f : 0.0f;
        animator.SetFloat("MoveBlendX", movingBlend);

        // GÜNCELLENDİ: Yeni paket "SpeedVertical" kullanıyor
        animator.SetFloat("VelocityY", rb.linearVelocity.y);

        animator.SetBool("IsGrounded", isGrounded);
    }

    private void HandleJumpInput()
    {
        if (currentState == PlayerState.WallSliding)
        {
            PerformWallJump();
        }
        else if (isGrounded)
        {
            PerformJump(defaultJumpForce);
        }
        else
        {
            bool hasWings = SkillManager.instance != null && SkillManager.instance.HasSkill(SkillType.SpectralWings);

            if (hasWings && !freeAirJumpUsed)
            {
                freeAirJumpUsed = true;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
                rb.AddForce(new Vector2(0f, defaultJumpForce), ForceMode2D.Impulse);
                ChangeState(PlayerState.Jumping);

                if (audioSource != null && jumpSound != null) audioSource.PlayOneShot(jumpSound);
                Debug.Log("SPECTRAL WINGS: Bedava Zıplama!");
                return;
            }

            if (currentAirJumps < maxAirJumps && currentShift > 0)
            {
                currentAirJumps++;
                PerformJump(defaultJumpForce);
            }
        }
    }

    private void FixedUpdate()
    {
        if (isPhasing)
        {
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, verticalInput * moveSpeed);
        }
        else if (currentState == PlayerState.WallSliding)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Clamp(rb.linearVelocity.y, -wallSlideSpeed, float.MaxValue));
        }
        else if (currentState != PlayerState.Dashing && currentState != PlayerState.KnockedBack && currentState != PlayerState.CometDiving)
        {
            if (isGrounded)
            {
                rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
            }
            else
            {
                // Havadayken yatay hızı koru, ama input ile biraz kontrol ver
                float airControl = 0.7f;
                float targetX = moveInput * moveSpeed;
                float newX = Mathf.Lerp(rb.linearVelocity.x, targetX, airControl * Time.fixedDeltaTime * 5f);
                rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
            }
        }
    }

    private void HandleStateTransitions()
    {
        if (canWallCling && !isGrounded && isWallDetected && moveInput != 0)
            ChangeState(PlayerState.WallSliding);
        else if (currentState == PlayerState.WallSliding && (!isWallDetected || moveInput == 0))
            ChangeState(PlayerState.Jumping);

        if (isGrounded && (currentState == PlayerState.Jumping || currentState == PlayerState.KnockedBack || currentState == PlayerState.WallSliding))
            ChangeState(PlayerState.Idle);

        if (isGrounded && currentState != PlayerState.Dashing)
        {
            if (moveInput != 0) { ChangeState(PlayerState.Running); }
            else { ChangeState(PlayerState.Idle); }
        }

        if (moveInput > 0 && !isFacingRight) { Flip(); }
        else if (moveInput < 0 && isFacingRight) { Flip(); }
    }

    internal void ChangeState(PlayerState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
    }

    // Applies isFacingRight to visualModel.localScale.x, accounting for the 180° Z
    // rotation that reverses the visual X axis during gravity reversal.
    private void ApplyVisualFacing()
    {
        if (visualModel == null) return;
        float sign = (isFacingRight ? 1f : -1f) * (isGravityReversed ? -1f : 1f);
        visualModel.transform.localScale = new Vector3(
            originalVisualScaleX * sign,
            visualModel.transform.localScale.y,
            visualModel.transform.localScale.z);
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        ApplyVisualFacing();
    }

    public bool ExecuteAction(CardActionType type, float value, out bool keepCardInHand)
    {
        return cardActionExecutor.TryExecute(type, value, out keepCardInHand);
    }

    private void PerformJump(float jumpForce)
    {
        if (currentShift > 0)
        {
            if (audioSource != null && jumpSound != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(jumpSound);
                audioSource.pitch = 1f;
            }
            if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())
                currentShift--;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            float jumpDir = isGravityReversed ? -1f : 1f;
            rb.AddForce(new Vector2(moveInput * jumpForce * 1f, jumpDir * jumpForce), ForceMode2D.Impulse);
            ChangeState(PlayerState.Jumping);
        }
    }

    internal IEnumerator PerformDash(float dashDistance, int direction)
    {
        if (audioSource != null && dashSound != null)
        {
            audioSource.PlayOneShot(dashSound);
        }
        if (dashEffectPrefab != null)
        {
            Instantiate(dashEffectPrefab, transform.position, Quaternion.identity);
        }

        PlayerState stateBeforeDash = currentState;
        ChangeState(PlayerState.Dashing);
        isInvincible = true;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(direction * dashDistance, 0);
        yield return new WaitForSeconds(0.3f);
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = originalGravity;
        isInvincible = false;

        if (isGrounded) ChangeState(PlayerState.Idle);
        else ChangeState(PlayerState.Jumping);
    }

    public void ApplyKnockback(Vector2 knockbackForce)
    {
        StartCoroutine(KnockbackRoutine(knockbackForce));
    }

    private IEnumerator KnockbackRoutine(Vector2 knockbackForce)
    {
        ChangeState(PlayerState.KnockedBack);
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(knockbackForce, ForceMode2D.Impulse);
        yield return new WaitForSeconds(0.2f);
        if (currentState == PlayerState.KnockedBack)
            ChangeState(PlayerState.Jumping);
    }

    public void TakeDamage(float damage)
    {
        if (isInvincible || isDead) { return; }
        if (RelicManager.instance != null)
        {
            RelicManager.instance.OnPlayerTakeDamage();
        }

        if (CameraShake.instance != null)
            CameraShake.instance.Shake(0.2f, 0.3f);

        tookDamageThisRoom = true;
        currentHealth = Mathf.Max(currentHealth - damage, 0f);

        if (hurtSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hurtSound);
        }

        // GÜNCELLENDİ: Yeni paket hasar yeme animasyonu tetikleyicisi
        if (animator != null) animator.SetTrigger("InjuredFront");

        Debug.Log($"Hasar Alındı! Kalan Can: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void OnNewRoomEnter()
    {
        tookDamageThisRoom = false;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("💀 Oyuncu Öldü! Ses Çalınıyor...");

        if (deathSound != null)
        {
            if (Camera.main != null)
                AudioSource.PlayClipAtPoint(deathSound, Camera.main.transform.position, deathVolume);
            else
                AudioSource.PlayClipAtPoint(deathSound, transform.position, deathVolume);
        }

        // GÜNCELLENDİ: Yeni paket ölüm animasyonu tetikleyicisi
        if (animator != null) animator.SetBool("IsDead", true);

        if (rb != null) rb.simulated = false;

        StartCoroutine(WaitAndReload());
    }

    private IEnumerator WaitAndReload()
    {
        yield return new WaitForSeconds(1.5f);
        SceneManager.LoadScene("GameOverScene");
    }

    public bool IsGroundedCheck()
    {
        if (isGravityReversed)
        {
            if (ceilingCheck == null) return false;
            return Physics2D.OverlapCircle(ceilingCheck.position, ceilingCheckRadius, groundLayer);
        }
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void PerformWallJump()
    {
        Flip();
        rb.linearVelocity = new Vector2(wallJumpForce.x * (isFacingRight ? 1f : -1f), wallJumpForce.y);
        ChangeState(PlayerState.Jumping);
    }

    internal IEnumerator ActivateWallCling(float duration)
    {
        canWallCling = true;
        yield return new WaitForSeconds(duration);
        canWallCling = false;
    }

    private bool WallCheck()
    {
        return Physics2D.Raycast(wallCheck.position, Vector2.right * (isFacingRight ? 1f : -1f), wallCheckDistance, groundLayer);
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = IsGroundedCheck() ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (ceilingCheck != null)
        {
            Gizmos.color = isGravityReversed ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(ceilingCheck.position, ceilingCheckRadius);
        }
        if (wallCheck != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + (Vector3.right * (isFacingRight ? 1f : -1f) * wallCheckDistance));
        }
        if (firePoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(firePoint.position, biteRange);
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, cometRadius);
    }

    public void SetCurrentEntryPoint(Vector3 entryPoint)
    {
        currentRoomEntryPoint = entryPoint;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("DeathZone"))
        {
            FallAndRespawn();
            return;
        }

        if (currentState == PlayerState.CometDiving) return;

        if (rb.linearVelocity.y < -0.1f)
        {
            if (RelicManager.instance == null || !RelicManager.instance.HasRelic("PogoBoots")) return;
            EnemyHealth eHealth = other.GetComponentInParent<EnemyHealth>();
            if (eHealth != null)
            {
                float enemyTopY = other.bounds.center.y + other.bounds.extents.y * 0.5f;
                Debug.Log($"[HeadBounce Trigger] {other.gameObject.name}, playerY: {transform.position.y:F2}, enemyTopY: {enemyTopY:F2}, velocity.y: {rb.linearVelocity.y:F2}");

                if (transform.position.y > enemyTopY)
                    TriggerHeadBounce(eHealth);
            }
        }
    }

    private void FallAndRespawn()
    {
        if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())
            TakeDamage(fallDamage);
        if (currentHealth > 0)
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = currentRoomEntryPoint;
        }
    }

    private void PerformFireball(float damageFromCard)
    {
        if (audioSource != null && fireballCastSound != null)
        {
            audioSource.PlayOneShot(fireballCastSound);
        }
        if (fireballPrefab == null || firePoint == null) return;

        Quaternion fireballRotation = !isFacingRight ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
        GameObject fireballInstance = Instantiate(fireballPrefab, firePoint.position, fireballRotation);

        Fireball fireballScript = fireballInstance.GetComponent<Fireball>();
        if (fireballScript != null)
            fireballScript.damage = damageFromCard;
    }

    internal IEnumerator FireballCastRoutine(float damageFromCard)
    {
        // Clip: "Pixel Character - Attack Cast", 30 frames at 30fps = 1.0s total.
        // OnAttackCast animation event fires at t=0.361s (frame ~10.8) — the exact frame
        // Cainos designed for projectile release, and sits at ~36% of the clip.
        float spawnDelay = 0.36f;

        // After spawning, hold IsAttacking true briefly so the animation finishes
        // its follow-through before the layer returns to idle. The Cast state exits
        // on its own ExitTime at ~0.8s, so this only prevents re-triggering.
        float clearAttackingDelay = 0.15f;

        if (animator != null)
        {
            animator.SetInteger("AttackAction", 14);
            animator.SetBool("IsAttacking", true);
        }

        yield return new WaitForSeconds(spawnDelay);

        PerformFireball(damageFromCard);

        yield return new WaitForSeconds(clearAttackingDelay);

        if (animator != null)
            animator.SetBool("IsAttacking", false);
    }

    public void AddShift(int amount)
    {
        currentShift = Mathf.Min(currentShift + amount, maxShift);
    }

    public void ResetShiftToMax()
    {
        currentShift = maxShift;
    }

    public void SpendShift(int amount)
    {
        if (amount <= 0) return;
        currentShift = Mathf.Max(0, currentShift - amount);
    }

    internal bool TryPlacePortal(out bool keepCard)
    {
        keepCard = false;
        if (portalPrefab == null) return false;
        if (mainCamera == null) return false;

        Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);

        if (firstPortalInstance == null)
        {
            GameObject p1 = Instantiate(portalPrefab, mousePos, Quaternion.identity);
            firstPortalInstance = p1.GetComponent<Portal>();
            firstPortalInstance.spriteRenderer.color = Color.gray;
            firstPortalInstance.ShowRangeCircle(portalMaxRange);

            keepCard = true;
            return true;
        }
        else
        {
            float distance = Vector2.Distance(firstPortalInstance.transform.position, mousePos);
            if (distance > portalMaxRange)
            {
                keepCard = true;
                return false;
            }

            int finalCost = portalCost;

            if (SkillManager.instance != null && SkillManager.instance.HasSkill(SkillType.KineticDiscount))
            {
                finalCost = Mathf.Max(0, finalCost - 1);
            }

            if (currentShift < finalCost)
            {
                keepCard = true;
                return false;
            }

            if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())
                SpendShift(finalCost);

            GameObject p2 = Instantiate(portalPrefab, mousePos, Quaternion.identity);
            Portal secondPortal = p2.GetComponent<Portal>();

            firstPortalInstance.Link(secondPortal);
            firstPortalInstance = null;

            keepCard = false;
            return true;
        }
    }

    internal void PerformVampiricBite(float damageAmount)
    {
        if (audioSource != null && vampireBiteSound != null)
        {
            audioSource.PlayOneShot(vampireBiteSound);
        }

        Collider2D hitEnemy = Physics2D.OverlapCircle(firePoint.position, biteRange, enemyLayer);

        if (hitEnemy != null)
        {
            EnemyHealth targetHealth = hitEnemy.GetComponent<EnemyHealth>();

            if (targetHealth != null)
            {
                targetHealth.TakeDamage(damageAmount);
                Heal(biteHealAmount);
                if (biteEffectPrefab != null)
                {
                    GameObject vfx = Instantiate(biteEffectPrefab, hitEnemy.transform.position, Quaternion.identity);
                    Destroy(vfx, 1.0f);
                }
            }
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    internal void PerformGlassWail(float stunDuration)
    {
        if (audioSource != null && glassVailSound != null)
        {
            audioSource.PlayOneShot(glassVailSound);
        }

        EnemyHealth[] allEnemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);

        if (CameraShake.instance != null)
            CameraShake.instance.Shake(0.5f, 0.5f);

        foreach (EnemyHealth enemy in allEnemies)
        {
            enemy.Stun(stunDuration);
        }
    }

    internal void PerformPhase(float duration)
    {
        if (audioSource != null && phaseSound != null)
        {
            audioSource.PlayOneShot(phaseSound);
        }
        StartCoroutine(PhaseRoutine(duration));
    }

    private IEnumerator PhaseRoutine(float duration)
    {
        isPhasing = true;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        int playerLayer = LayerMask.NameToLayer("Player");
        int groundLayer = LayerMask.NameToLayer("Ground");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        Physics2D.IgnoreLayerCollision(playerLayer, groundLayer, true);
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);

        // GÜNCELLENDİ: SkinnedMeshRenderer'larda direkt materyal rengi değiştirmek shader'a bağlı olduğu için 
        // şimdilik alfa değişimi kodunu yorum satırı yaptık. Gerekirse ileride çözeriz.
        // if (spriteRenderer != null) ... 

        yield return new WaitForSeconds(duration);

        Physics2D.IgnoreLayerCollision(playerLayer, groundLayer, false);
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);

        rb.gravityScale = originalGravity;

        // if (spriteRenderer != null) ...

        isPhasing = false;
    }

    public void IncreaseMaxShift(int amount)
    {
        maxShift += amount;
        currentShift += amount;
    }

    public void EnterCannon(Transform cannonTransform)
    {
        ChangeState(PlayerState.InCannon);

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // GÜNCELLENDİ: Artık SpriteRenderer yerine yeni görsel modeli (visualModel) açıp kapatıyoruz.
        if (visualModel != null) visualModel.SetActive(false);

        transform.position = cannonTransform.position;
        transform.SetParent(cannonTransform);
    }

    internal void PerformCometDive()
    {
        if (audioSource != null && cometDiveSound != null)
        {
            audioSource.PlayOneShot(cometDiveSound);
        }
        ChangeState(PlayerState.CometDiving);
        rb.linearVelocity = new Vector2(0, -cometSpeed);

        if (diveTrail != null) diveTrail.emitting = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState == PlayerState.CometDiving)
        {
            bool isGround = (groundLayer.value & (1 << collision.gameObject.layer)) > 0;
            bool isEnemy = (enemyLayer.value & (1 << collision.gameObject.layer)) > 0;

            if (isGround || isEnemy)
            {
                CometImpact();
            }
            return;
        }

        if (RelicManager.instance == null || !RelicManager.instance.HasRelic("PogoBoots")) return;
        EnemyHealth eHealth = collision.gameObject.GetComponentInParent<EnemyHealth>();
        if (eHealth != null)
        {
            ContactPoint2D contact = collision.GetContact(0);
            Debug.Log($"[HeadBounce] Collision: {collision.gameObject.name}, normal.y: {contact.normal.y:F2}, velocity.y: {rb.linearVelocity.y:F2}, canBounce: {eHealth.canBeHeadBounced}");

            if (contact.normal.y > 0.7f)
                TriggerHeadBounce(eHealth);
        }
    }

    private void TriggerHeadBounce(EnemyHealth eHealth)
    {
        if (!eHealth.canBeHeadBounced) return;
        if (Time.time < _headBounceCooldown) return;

        _headBounceCooldown = Time.time + 0.3f;
        eHealth.TakeDamage(8f);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * defaultJumpForce * 0.7f, ForceMode2D.Impulse);
        AddShift(1);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.1f, 0.2f);
    }

    private void CometImpact()
    {
        ChangeState(PlayerState.Idle);

        if (diveTrail != null) diveTrail.emitting = false;

        if (diveImpactPrefab != null)
        {
            Instantiate(diveImpactPrefab, transform.position, Quaternion.identity);
        }

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, cometRadius, enemyLayer);

        foreach (Collider2D enemy in hitEnemies)
        {
            EnemyHealth eHealth = enemy.GetComponent<EnemyHealth>();
            if (eHealth != null)
            {
                eHealth.TakeDamage(cometDamage);
            }
        }

        if (CameraShake.instance != null) CameraShake.instance.Shake(0.3f, 0.5f);
        if (cometImpactEffect != null) Instantiate(cometImpactEffect, transform.position, Quaternion.identity);
    }

    internal void UseAdrenaline(float value)
    {
        if (audioSource != null && adrenalineSound != null)
        {
            audioSource.PlayOneShot(adrenalineSound);
        }

        float healthPercentage = currentHealth / maxHealth;

        if (CameraShake.instance != null) CameraShake.instance.Shake(0.1f, 0.5f);

        if (healthPercentage > 0.5f)
        {
            StartCoroutine(AdrenalineSlowMoRoutine());
        }
        else
        {
            Heal(value);
            StartCoroutine(AdrenalineSpeedBoostRoutine());
        }
    }

    private IEnumerator AdrenalineSlowMoRoutine()
    {
        Time.timeScale = slowMotionFactor;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(adrenalineDuration);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private IEnumerator AdrenalineSpeedBoostRoutine()
    {
        isAdrenalineActive = true;

        // GÜNCELLENDİ: SkinnedMesh rengi şimdilik değiştirilmiyor
        // if (spriteRenderer != null) spriteRenderer.color = Color.red;

        float originalSpeed = moveSpeed;
        moveSpeed *= speedBoostMultiplier;

        yield return new WaitForSeconds(adrenalineDuration);

        moveSpeed = originalSpeed;

        // if (spriteRenderer != null) spriteRenderer.color = Color.white;
        isAdrenalineActive = false;
    }

    public void LaunchFromCannon(Vector2 forceVector)
    {
        transform.SetParent(null);
        transform.localScale = originalScale;
        transform.rotation = Quaternion.identity;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        // GÜNCELLENDİ: Görsel modeli geri aç
        if (visualModel != null) visualModel.SetActive(true);

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.AddForce(forceVector, ForceMode2D.Impulse);
        ChangeState(PlayerState.Jumping);
    }

    internal void PerformStagger()
    {
        staggerCount++;
        Debug.Log($"STAGGER KULLANILDI! ({staggerCount}/{maxStaggerUses})");

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        rb.AddForce(Vector2.up * staggerJumpForce, ForceMode2D.Impulse);

        Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position, staggerRadius, enemyLayer);
        foreach (Collider2D enemy in enemies)
        {
            EnemyHealth eHealth = enemy.GetComponent<EnemyHealth>();
            if (eHealth != null)
            {
                eHealth.TakeDamage(staggerDamage);
            }
        }

        if (staggerEffect != null) Instantiate(staggerEffect, transform.position, Quaternion.identity);

        if (staggerCount >= maxStaggerUses)
        {
            Debug.Log("KALBİN DAYANAMADI! ÖLÜYORSUN...");
            Die();
        }
    }

    private void CheckInteraction()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRange, interactableLayer);

        foreach (Collider2D hit in hits)
        {
            IInteractable interactable = hit.GetComponent<IInteractable>();

            if (interactable != null)
            {
                interactable.Interact();
                return;
            }
        }
    }

    // --- Floor is Lava card (ReverseGravity) ---

    internal void StartGravityReversal()
    {
        // Stop any existing effect so re-plays refresh the timer instead of stacking
        if (gravityReversalCoroutine != null)
            StopCoroutine(gravityReversalCoroutine);
        gravityReversalCoroutine = StartCoroutine(GravityReversalRoutine());
    }

    private IEnumerator GravityReversalRoutine()
    {
        bool wasAlreadyReversed = isGravityReversed;

        if (!wasAlreadyReversed)
        {
            // First activation: flip gravity, rotate visual upside-down
            isGravityReversed = true;
            ApplyVisualFacing();
            originalGravityScale = rb.gravityScale;
            rb.gravityScale = -originalGravityScale;
            yield return StartCoroutine(LerpVisualTransform(0f, 180f, originalVisualLocalPos.y, originalVisualLocalPos.y + visualFlipYOffset, 0.15f));
            // Wait until 0.5s before the 5s mark (5.0 - 0.5 - 0.15 initial rotation = 4.35s)
            yield return new WaitForSeconds(4.35f);
        }
        else
        {
            // Re-play while already reversed: gravity and visual already set, just restart timer
            yield return new WaitForSeconds(4.5f);
        }

        // Warning at t=4.5s: sound + visual strobe
        if (warningSoundClip != null && audioSource != null)
            audioSource.PlayOneShot(warningSoundClip, soundVolume);
        yield return StartCoroutine(WarningFlashRoutine());

        // t=5.0s: gravity snaps back instantly, visual lerps back
        rb.gravityScale = originalGravityScale;
        isGravityReversed = false;
        ApplyVisualFacing();
        yield return StartCoroutine(LerpVisualTransform(180f, 0f, originalVisualLocalPos.y + visualFlipYOffset, originalVisualLocalPos.y, 0.15f));

        gravityReversalCoroutine = null;
    }

    private IEnumerator LerpVisualTransform(float fromZ, float toZ, float fromY, float toY, float duration)
    {
        if (visualModel == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            visualRotationZ = Mathf.LerpAngle(fromZ, toZ, t);
            float newY = Mathf.Lerp(fromY, toY, t);
            visualModel.transform.localRotation = Quaternion.Euler(0f, 0f, visualRotationZ);
            visualModel.transform.localPosition = new Vector3(originalVisualLocalPos.x, newY, originalVisualLocalPos.z);
            yield return null;
        }
        // Snap to exact targets to avoid float drift
        visualRotationZ = toZ;
        visualModel.transform.localRotation = Quaternion.Euler(0f, 0f, visualRotationZ);
        visualModel.transform.localPosition = new Vector3(originalVisualLocalPos.x, toY, originalVisualLocalPos.z);
    }

    private IEnumerator WarningFlashRoutine()
    {
        // 3 rapid on/off cycles ≈ 0.5s total
        for (int i = 0; i < 3; i++)
        {
            SetPlayerFlashColor(Color.white);
            yield return new WaitForSeconds(0.083f);
            SetPlayerFlashColor(playerRendererOriginalColor);
            yield return new WaitForSeconds(0.083f);
        }
    }

    private void SetPlayerFlashColor(Color color)
    {
        if (playerSkinnedRenderer != null && playerRendererHasColor)
            playerSkinnedRenderer.material.SetColor("_Color", color);
    }
}