using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    internal Rigidbody2D rb;
    private Animator animator;
    internal Camera mainCamera;
    private CardActionExecutor cardActionExecutor;
    private PlayerHealth playerHealth;

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

    [Header("Swim Settings")]
    [Tooltip("Horizontal/vertical move speed while swimming. Lower than moveSpeed to simulate water resistance.")]
    public float swimSpeed = 5f;
    [Tooltip("Upward impulse applied when pressing Jump while swimming, used to break the surface and leap out.")]
    public float swimExitJumpForce = 9f;
    [Tooltip("How far BELOW the surface the player settles when NOT actively swimming up/down, so they sit in the water instead of bobbing on top. Hold Up to swim above this and out of the water.")]
    public float swimSurfaceOffset = 1.2f;
    [Tooltip("How quickly the player sinks to the idle rest depth when not pressing up/down. Higher = snappier settle.")]
    [SerializeField] private float swimSettleStrength = 8f;
    [Tooltip("Seconds the upward swim-jump pop is preserved, to help breach the surface and leave the water.")]
    [SerializeField] private float swimExitDuration = 0.35f;
    private bool isSwimming = false;
    private float swimCachedGravityScale;
    private int swimZoneCount = 0; // refcount so overlapping swim zones don't exit early
    private SwimZone currentSwimZone;
    private float swimExitTimer;
    private Vector3 originalScale;
    internal Vector3 currentRoomEntryPoint;

    private float _headBounceCooldown;

    [Header("Gold Settings")]
    public int currentGold = 0;
    public event System.Action<int> OnGoldChanged;

    [Header("Audio Settings")]
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
    public AudioClip spendSound;
    public AudioClip warningSoundClip;
    public float soundVolume = 1f;

    [Header("VFX Settings")]
    public GameObject biteEffectPrefab;
    public GameObject leapEffectPrefab;
    public TrailRenderer diveTrail;
    public GameObject dashEffectPrefab;
    [SerializeField] internal float dashImpulse = 18f;
    [SerializeField] internal float dashIFrameDuration = 0.15f;

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
    private Portal firstPortalInstance;

    [Header("Wall Settings")]
    public Transform wallCheck;
    public float wallCheckDistance = 0.5f;
    public float wallSlideSpeed = 2f;
    public Vector2 wallJumpForce = new Vector2(10f, 15f);
    private bool isWallDetected;
    private bool isWallSliding;

    [Header("Quest Tracking")]
    private bool tookDamageThisRoom = false;
    public bool TookDamageThisRoom { get { return tookDamageThisRoom; } }

    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    private float moveInput;

    [Header("Health Settings")]
    public float CurrentHealth => playerHealth.CurrentHealth;
    public float MaxHealth => playerHealth.MaxHealth;
    public bool IsDead => playerHealth.IsDead;
    public bool isInvincible
    {
        get => playerHealth.isInvincible;
        set => playerHealth.isInvincible = value;
    }

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
    public GameObject cometShockwaveEffect;
    [Range(0.005f, 0.1f)] public float cometVfxScale = 0.055f;

    [Header("Adrenaline Card Settings")]
    public float adrenalineDuration = 3f;
    public float slowMotionFactor = 0.4f;
    public float speedBoostMultiplier = 1.5f;
    public GameObject adrenalineAuraEffect;   // looping energy aura (AdrenalineAuraVFX), played for the buff duration

    public PlayerState currentState;
    internal bool isGrounded;

    [Header("Combat Settings")]
    public GameObject fireballPrefab;
    public Transform firePoint;
    public float wailRange = 10f;
    [SerializeField] internal float fireballCastDelay = 0.12f;
    public float biteRange = 1.5f;
    public float biteHealAmount = 10f;
    public LayerMask enemyLayer;
    public GameObject glassWailEffect;   // Glass Wail shockwave VFX (world-space ShockwaveVFX prefab; size set on the prefab)

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
    internal bool isGravityReversed = false;
    private float originalGravityScale;
    private Coroutine gravityReversalCoroutine;
    private float visualRotationZ = 0f;
    private Vector3 originalVisualLocalPos;
    private float originalVisualScaleX;
    internal bool isFacingRight = true;

    [Header("Gravity Reversal")]
    // Tune in Play mode: feet should just touch the ceiling when flipped
    [SerializeField] private float visualFlipYOffset = 2.0f;
    public GameObject gravityAuraEffect;        // looping anti-gravity aura prefab (GravityAuraVFX), played for the reversal duration
    private GameObject gravityAuraInstance;

    [Header("Phase Visual")]
    [SerializeField] internal SkinnedMeshRenderer[] phaseVisuals;
    private Coroutine phaseVisualCoroutine;
    private CapsuleCollider2D capsuleCollider;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        mainCamera = Camera.main;
        cardActionExecutor = GetComponent<CardActionExecutor>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        playerHealth = GetComponent<PlayerHealth>();

        if (visualModel != null)
        {
            originalVisualLocalPos = visualModel.transform.localPosition;
            originalVisualScaleX = visualModel.transform.localScale.x;
        }
    }

    void Start()
    {
        originalScale = transform.localScale;
        currentShift = maxShift;
        ChangeState(PlayerState.Idle);

        playerHealth.OnDamaged += (dmg) =>
        {
            tookDamageThisRoom = true;
            RelicManager.instance?.OnPlayerTakeDamage();
            CameraShake.instance?.Shake(0.2f, 0.3f);
        };

        // Phase toggles the global layer-collision matrix, which survives scene loads.
        // Dying mid-Phase kills PhaseRoutine before its cleanup runs, so the death
        // path must restore the matrix itself (audit_report.md Critical #2).
        playerHealth.OnDied += RestorePhaseLayerCollisions;

        if (visualModel != null)
        {
            var aer = visualModel.GetComponentInChildren<Cainos.CustomizablePixelCharacter.AnimationEventReceiver>(true);
            if (aer != null && aer.enabled)
            {
                aer.enabled = false;
                Debug.Log("[PlayerController] AnimationEventReceiver was enabled on startup, force-disabling.");
            }
        }
    }

    void Update()
    {
        if (isPeeking)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        if (playerHealth.IsDead) return;

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
        else if (isSwimming)
        {
            // Free 8-directional swim. Jump kicks upward to break the surface.
            moveInput = Input.GetAxisRaw("Horizontal");
            verticalInput = Input.GetAxisRaw("Vertical");

            if (Input.GetButtonDown("Jump")) PerformSwimJump();

            if (moveInput > 0 && !isFacingRight) Flip();
            else if (moveInput < 0 && isFacingRight) Flip();
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
        // Skip Better Jump while swimming: it manually applies Physics2D.gravity
        // regardless of gravityScale, which would pull the swimmer down.
        if (!isSwimming)
        {
            float gravitySign = isGravityReversed ? -1f : 1f;
            if (rb.linearVelocity.y * gravitySign < 0)
            {
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime * gravitySign;
            }
            else if (rb.linearVelocity.y * gravitySign > 0 && !Input.GetKey(KeyCode.Space))
            {
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime * gravitySign;
            }
        }

        // Swimming handles its own facing above and must not let ground-based
        // transitions (e.g. touching the water floor) knock it out of Swimming.
        if (!isPhasing && !isSwimming)
        {
            HandleStateTransitions();
            UpdateAnimations();
        }
        else if (isSwimming)
        {
            UpdateSwimAnimations();
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
            SfxManager.PlayOn(audioSource, spendSound, soundVolume);
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

    // Feeds the Cainos swim blend tree so it picks the forward / backward / up / down
    // swim clip based on actual movement. IsInWater (set in EnterWater) gates entry.
    private void UpdateSwimAnimations()
    {
        if (animator == null) return;

        animator.SetFloat("MoveBlendX", Mathf.Abs(moveInput) > 0.1f ? 1f : 0f);
        // VelocityX is signed by facing so "forward" stays positive regardless of direction.
        animator.SetFloat("VelocityX", rb.linearVelocity.x * (isFacingRight ? 1f : -1f));
        animator.SetFloat("VelocityY", rb.linearVelocity.y);
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

                SfxManager.PlayOn(audioSource, jumpSound);
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
        else if (isSwimming && currentState != PlayerState.Dashing && currentState != PlayerState.KnockedBack && currentState != PlayerState.CometDiving)
        {
            // Direct velocity control with water resistance. Overwriting velocity each
            // step keeps swimming responsive even if gravityScale gets changed elsewhere.
            float vx = moveInput * swimSpeed;
            float vy;

            if (swimExitTimer > 0f)
            {
                // Swim-jump pop in progress: preserve the upward velocity to breach and exit.
                swimExitTimer -= Time.fixedDeltaTime;
                vy = rb.linearVelocity.y;
            }
            else if (Mathf.Abs(verticalInput) > 0.01f)
            {
                // Actively swimming up or down. Holding Up carries the player to the
                // surface and out of the water — this is the normal way to exit.
                vy = verticalInput * swimSpeed;
            }
            else
            {
                // Idle: settle DOWN to a rest depth below the surface so the player sits
                // in the water instead of floating on top. Never pushes up, so it can't trap.
                float restY = (currentSwimZone != null ? currentSwimZone.SurfaceY : transform.position.y) - swimSurfaceOffset;
                vy = transform.position.y > restY
                    ? Mathf.Max(-swimSpeed, (restY - transform.position.y) * swimSettleStrength)
                    : 0f;
            }

            rb.linearVelocity = new Vector2(vx, vy);
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
        if (currentState == PlayerState.WallSliding && (!isWallDetected || moveInput == 0))
            ChangeState(PlayerState.Jumping);

        if (isGrounded && (currentState == PlayerState.Jumping || currentState == PlayerState.KnockedBack || currentState == PlayerState.WallSliding))
            ChangeState(PlayerState.Idle);

        if (isGrounded && currentState != PlayerState.Dashing && currentState != PlayerState.CometDiving)
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

    // === SWIMMING ===
    // Called by SwimZone when the player enters/exits swimmable water (Clear/Normal).
    // Hazard waters (Acid/Lava/Poison) use HazardZone and never call these.
    // Swimming is free (no Shift cost) and uses gravity-off, 8-directional control.
    public void EnterWater(SwimZone zone)
    {
        swimZoneCount++;
        if (isSwimming || playerHealth.IsDead || currentState == PlayerState.InCannon) return;

        isSwimming = true;
        currentSwimZone = zone;
        swimExitTimer = 0f;
        swimCachedGravityScale = rb.gravityScale; // restore exactly on exit (handles gravity reversal)
        rb.gravityScale = 0f;
        ChangeState(PlayerState.Swimming);

        // Drives the Cainos "Swim" state machine in AC Character.controller.
        if (animator != null) animator.SetBool("IsInWater", true);
    }

    public void ExitWater(SwimZone zone)
    {
        swimZoneCount = Mathf.Max(0, swimZoneCount - 1);
        if (!isSwimming || swimZoneCount > 0) return; // still inside another overlapping zone

        isSwimming = false;
        currentSwimZone = null;
        swimExitTimer = 0f;
        rb.gravityScale = swimCachedGravityScale;
        if (currentState == PlayerState.Swimming) ChangeState(PlayerState.Jumping);

        if (animator != null) animator.SetBool("IsInWater", false);
    }

    // Upward kick to break the surface and leap out of water. Free, no Shift cost.
    // Starts a brief window where the surface clamp is bypassed so the player can exit.
    private void PerformSwimJump()
    {
        swimExitTimer = swimExitDuration;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, swimExitJumpForce);
        SfxManager.PlayOn(audioSource, jumpSound, soundVolume);
    }

    // Detailed outcome of the most recent card play (Success / Failed / Blocked).
    // DeckManager only needs the bool; UI feedback for Blocked reads this later.
    public CardExecuteResult LastExecuteResult { get; private set; } = CardExecuteResult.Success;

    public bool ExecuteAction(CardActionType type, float value, out bool keepCardInHand)
    {
        LastExecuteResult = cardActionExecutor.TryExecute(type, value, out keepCardInHand);
        return LastExecuteResult == CardExecuteResult.Success;
    }

    private void PerformJump(float jumpForce)
    {
        if (currentShift > 0)
        {
            if (audioSource != null && jumpSound != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                SfxManager.PlayOn(audioSource, jumpSound);
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

    internal IEnumerator DashIFrames(float duration) => playerHealth.GrantInvincibility(duration);

    public void ApplyKnockback(Vector2 knockbackForce) => playerHealth.ApplyKnockback(knockbackForce);

    public void TakeDamage(float damage) => playerHealth.TakeDamage(damage);

    public void OnNewRoomEnter()
    {
        tookDamageThisRoom = false;
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
            playerHealth.FallAndRespawn();
            return;
        }

        if (currentState == PlayerState.CometDiving) return;

        bool fallingDown = isGravityReversed ? rb.linearVelocity.y > 0.1f : rb.linearVelocity.y < -0.1f;
        if (fallingDown)
        {
            if (RelicManager.instance == null || !RelicManager.instance.HasRelic("PogoBoots")) return;
            EnemyHealth eHealth = other.GetComponentInParent<EnemyHealth>();
            if (eHealth != null)
            {
                float enemyTopY = other.bounds.center.y + other.bounds.extents.y * 0.5f;
                float enemyBottomY = other.bounds.center.y - other.bounds.extents.y * 0.5f;
                Debug.Log($"[HeadBounce Trigger] {other.gameObject.name}, playerY: {transform.position.y:F2}, enemyTopY: {enemyTopY:F2}, velocity.y: {rb.linearVelocity.y:F2}");

                bool positionOk = isGravityReversed ? transform.position.y < enemyBottomY : transform.position.y > enemyTopY;
                if (positionOk)
                    TriggerHeadBounce(eHealth);
            }
        }
    }

    private void PerformFireball(float damageFromCard)
    {
        SfxManager.PlayOn(audioSource, fireballCastSound);
        if (fireballPrefab == null || firePoint == null) return;

        Quaternion fireballRotation = !isFacingRight ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
        GameObject fireballInstance = Instantiate(fireballPrefab, firePoint.position, fireballRotation);

        Fireball fireballScript = fireballInstance.GetComponent<Fireball>();
        if (fireballScript != null)
            fireballScript.damage = damageFromCard;
    }

    internal IEnumerator FireballCastRoutine(float damageFromCard)
    {
        if (animator != null)
        {
            animator.SetInteger("AttackAction", 14);
            animator.SetBool("IsAttacking", true);
        }

        yield return new WaitForSeconds(fireballCastDelay);

        PerformFireball(damageFromCard);

        yield return new WaitForSeconds(0.39f);

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

    internal bool TryPlacePortal(out bool keepCard, int shiftCost)
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

            int finalCost = shiftCost;

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
        SfxManager.PlayOn(audioSource, vampireBiteSound);

        // ~0 = all layers: avoids the AeroBat/MeleeEnemy Default-layer miss from enemyLayer mask.
        // GetComponentInParent finds EnemyHealth even when the collider is on a child.
        Collider2D[] hits = Physics2D.OverlapCircleAll(firePoint.position, biteRange, ~0);
        foreach (Collider2D hit in hits)
        {
            IDamageable targetHealth = hit.GetComponentInParent<IDamageable>();
            if (targetHealth == null) continue;

            targetHealth.TakeDamage(damageAmount);
            if (targetHealth is EnemyHealth) Heal(biteHealAmount);
            if (biteEffectPrefab != null)
                Instantiate(biteEffectPrefab, hit.transform.position, Quaternion.identity);  // BiteVFX self-destroys

            if (CameraShake.instance != null)
                CameraShake.instance.Shake(0.08f, 0.25f);   // chomp impact

            return; // one bite, one target
        }
    }

    public void Heal(float amount) => playerHealth.Heal(amount);

    internal void PerformGlassWail(float stunDuration)
    {
        SfxManager.PlayOn(audioSource, glassVailSound);

        EnemyHealth[] allEnemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);

        if (CameraShake.instance != null)
            CameraShake.instance.Shake(0.5f, 0.5f);

        // Wail origin: a shockwave ripples out from the player so the screen-wide stun reads visually.
        // World-space prefab (self-destroys), so spawn it directly rather than via the UI-canvas helper.
        if (glassWailEffect != null)
            Instantiate(glassWailEffect, transform.position, Quaternion.identity);

        foreach (EnemyHealth enemy in allEnemies)
        {
            enemy.Stun(stunDuration);
        }
    }

    internal IEnumerator PhaseRoutine(float duration)
    {
        isPhasing = true;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        int playerLayer = LayerMask.NameToLayer("Player");
        int groundLayerIndex = LayerMask.NameToLayer("Ground");
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");

        Physics2D.IgnoreLayerCollision(playerLayer, groundLayerIndex, true);
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIndex, true);

        // Pass duration + 1.5f so the pulse covers the maximum possible extension time.
        // PhaseRoutine always stops it explicitly; the extra headroom just prevents a
        // static mid-pulse tint if wall-stuck extension runs to the full 1s cap.
        phaseVisualCoroutine = StartCoroutine(PhaseVisualRoutine(duration + 1.5f));

        yield return new WaitForSeconds(duration);

        float extensionTime = 0f;
        while (IsCollidingWithGround() && extensionTime < 1f)
        {
            yield return new WaitForSeconds(0.1f);
            extensionTime += 0.1f;
        }
        if (IsCollidingWithGround())
        {
            float ejectDir = isGravityReversed ? -1f : 1f;
            transform.position += new Vector3(0, 0.5f * ejectDir, 0);
        }

        if (phaseVisualCoroutine != null) { StopCoroutine(phaseVisualCoroutine); phaseVisualCoroutine = null; }
        RestorePhaseVisuals();

        Physics2D.IgnoreLayerCollision(playerLayer, groundLayerIndex, false);
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIndex, false);

        rb.gravityScale = originalGravity;

        isPhasing = false;
    }

    private IEnumerator PhaseVisualRoutine(float duration)
    {
        if (phaseVisuals == null || phaseVisuals.Length == 0) yield break;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float pulse = (Mathf.Sin(Time.time * Mathf.PI * 4f) + 1f) * 0.5f;
            block.SetFloat("_Alpha", Mathf.Lerp(0.3f, 0.6f, pulse));
            for (int i = 0; i < phaseVisuals.Length; i++)
            {
                if (phaseVisuals[i] == null) continue;
                phaseVisuals[i].SetPropertyBlock(block);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Safety net: restore if PhaseRoutine doesn't stop this coroutine first.
        block.SetFloat("_Alpha", 1f);
        for (int i = 0; i < phaseVisuals.Length; i++)
        {
            if (phaseVisuals[i] == null) continue;
            phaseVisuals[i].SetPropertyBlock(block);
        }
    }

    private void RestorePhaseVisuals()
    {
        if (phaseVisuals == null) return;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetFloat("_Alpha", 1f);
        for (int i = 0; i < phaseVisuals.Length; i++)
        {
            if (phaseVisuals[i] == null) continue;
            phaseVisuals[i].SetPropertyBlock(block);
        }
    }

    private bool IsCollidingWithGround()
    {
        if (capsuleCollider == null) return false;
        Bounds b = capsuleCollider.bounds;
        // 0.9f shrink avoids a false positive from the player barely touching the floor normally
        return Physics2D.OverlapBox(b.center, b.size * 0.9f, 0f, groundLayer);
    }

    // Re-enables the layer pairs PhaseRoutine ignores. Runs unconditionally on death:
    // ignoring is only ever set by Phase, and clearing an already-clear pair is a no-op,
    // so this is safe whether or not a Phase was active when the player died.
    private void RestorePhaseLayerCollisions()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        Physics2D.IgnoreLayerCollision(playerLayer, LayerMask.NameToLayer("Ground"), false);
        Physics2D.IgnoreLayerCollision(playerLayer, LayerMask.NameToLayer("Enemy"), false);
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

    // Mirrors the dive's PlayerVelocity flag: true from StartCometDive until the first
    // EndCometDive. Needed because the fall-respawn path can call EndCometDive twice
    // for one dive (once at respawn, once when the still-diving state lands at spawn).
    private bool cometDiveWindowActive;

    internal void StartCometDive()
    {
        ChangeState(PlayerState.CometDiving);
        cometDiveWindowActive = true;
        // Manual flag: the dive holds PlayerVelocity until it lands (or is interrupted)
        // — an open-ended window the executor can't see via ManagedCoroutine.
        // ConflictFlags has no player-state flag, so PlayerVelocity is the whole claim.
        cardActionExecutor?.SetManualFlag(ConflictFlags.PlayerVelocity, true);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -cometSpeed);
        if (diveTrail != null) diveTrail.emitting = true;
        SfxManager.PlayOn(audioSource, cometDiveSound);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState == PlayerState.CometDiving)
        {
            bool isGround = (groundLayer.value & (1 << collision.gameObject.layer)) > 0;
            if (isGround)
                LandCometDive(collision.GetContact(0).point);
            return;
        }

        if (RelicManager.instance == null || !RelicManager.instance.HasRelic("PogoBoots")) return;
        EnemyHealth eHealth = collision.gameObject.GetComponentInParent<EnemyHealth>();
        if (eHealth != null)
        {
            ContactPoint2D contact = collision.GetContact(0);
            Debug.Log($"[HeadBounce] Collision: {collision.gameObject.name}, normal.y: {contact.normal.y:F2}, velocity.y: {rb.linearVelocity.y:F2}, canBounce: {eHealth.canBeHeadBounced}");

            bool normalFromBelow = isGravityReversed ? contact.normal.y < -0.7f : contact.normal.y > 0.7f;
            if (normalFromBelow)
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
        float bounceDir = isGravityReversed ? -1f : 1f;
        rb.AddForce(Vector2.up * defaultJumpForce * 0.7f * bounceDir, ForceMode2D.Impulse);
        AddShift(1);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.1f, 0.2f);
    }

    // Single choke point for ending the dive window — every exit path (LandCometDive,
    // knockback, death, fall-respawn) calls this. The guard clears the flag exactly
    // once per dive even when a path calls EndCometDive twice (fall-respawn does).
    internal void EndCometDive()
    {
        if (cometDiveWindowActive)
        {
            cometDiveWindowActive = false;
            cardActionExecutor?.SetManualFlag(ConflictFlags.PlayerVelocity, false);
        }
        if (diveTrail != null) diveTrail.emitting = false;
    }

    private void LandCometDive(Vector2 contactPoint)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, cometRadius, ~0);
        HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        foreach (Collider2D hit in hits)
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target != null && damaged.Add(target))
                target.TakeDamage(cometDamage);
        }

        // Offset up by half the canvas height so the bottom of the VFX sits on the surface
        // rather than the center, which would put half the explosion inside the ground.
        float halfCanvasWorld = 32f * cometVfxScale;
        Vector3 vfxPos = new Vector3(contactPoint.x, contactPoint.y + halfCanvasWorld, 0f);

        if (cometImpactEffect != null)
            SpawnUIVFX(cometImpactEffect, vfxPos, cometVfxScale, 0.75f);
        if (cometShockwaveEffect != null)
            SpawnUIVFX(cometShockwaveEffect, vfxPos, cometVfxScale, 0.75f);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.15f, 0.5f);

        EndCometDive();
        ChangeState(PlayerState.Idle);
    }

    public void FlashCardPlay()
    {
        StartCoroutine(CardPlayFlashRoutine());
    }

    private IEnumerator CardPlayFlashRoutine()
    {
        SpriteRenderer[] renderers = visualModel != null
            ? visualModel.GetComponentsInChildren<SpriteRenderer>()
            : GetComponentsInChildren<SpriteRenderer>();

        Color flashColor = new Color(1f, 0.85f, 0.2f); // gold
        Color[] original = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            original[i] = renderers[i].color;
            renderers[i].color = flashColor;
        }

        yield return new WaitForSecondsRealtime(0.08f);

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = original[i];
    }

    void SpawnUIVFX(GameObject vfxPrefab, Vector3 worldPos, float scale, float destroyAfter)
    {
        GameObject canvasGO = new GameObject("VFX_Canvas");

        UnityEngine.Canvas canvas = canvasGO.AddComponent<UnityEngine.Canvas>();
        canvas.renderMode = UnityEngine.RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        canvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 64f);
        canvasGO.transform.position = worldPos;
        canvasGO.transform.localScale = Vector3.one * scale;

        Instantiate(vfxPrefab, canvasGO.transform);
        Destroy(canvasGO, destroyAfter);
    }

    internal void UseAdrenaline(float value)
    {
        SfxManager.PlayOn(audioSource, adrenalineSound);

        float healthPercentage = playerHealth.HealthPercent;

        if (CameraShake.instance != null) CameraShake.instance.Shake(0.1f, 0.5f);

        // Energy aura for the whole buff (both branches last adrenalineDuration). Parent to the
        // player root (identity rotation) so sparks rise in world space; it self-destroys.
        if (adrenalineAuraEffect != null)
        {
            GameObject aura = Instantiate(adrenalineAuraEffect, transform);
            aura.transform.localPosition = Vector3.zero;
            AdrenalineAuraVFX auraVfx = aura.GetComponent<AdrenalineAuraVFX>();
            if (auraVfx != null) auraVfx.duration = adrenalineDuration;
        }

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
        cardActionExecutor?.SetManualFlag(ConflictFlags.TimeScale | ConflictFlags.MoveSpeed, true);
        Time.timeScale = slowMotionFactor;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(adrenalineDuration);

        cardActionExecutor?.SetManualFlag(ConflictFlags.TimeScale | ConflictFlags.MoveSpeed, false);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private IEnumerator AdrenalineSpeedBoostRoutine()
    {
        cardActionExecutor?.SetManualFlag(ConflictFlags.TimeScale | ConflictFlags.MoveSpeed, true);
        isAdrenalineActive = true;

        // GÜNCELLENDİ: SkinnedMesh rengi şimdilik değiştirilmiyor
        // if (spriteRenderer != null) spriteRenderer.color = Color.red;

        float originalSpeed = moveSpeed;
        moveSpeed *= speedBoostMultiplier;

        yield return new WaitForSeconds(adrenalineDuration);

        cardActionExecutor?.SetManualFlag(ConflictFlags.TimeScale | ConflictFlags.MoveSpeed, false);
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
        float jumpDir = isGravityReversed ? -1f : 1f;
        rb.AddForce(Vector2.up * staggerJumpForce * jumpDir, ForceMode2D.Impulse);

        Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position, staggerRadius, ~0);
        foreach (Collider2D enemy in enemies)
        {
            IDamageable target = enemy.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(staggerDamage);
            }
        }

        if (staggerEffect != null) Instantiate(staggerEffect, transform.position, Quaternion.identity);

        if (staggerCount >= maxStaggerUses)
        {
            Debug.Log("KALBİN DAYANAMADI! ÖLÜYORSUN...");
            playerHealth.Kill();
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
        // Stop any existing effect so re-plays refresh the timer instead of stacking.
        // Flags are cleared BEFORE StopCoroutine: stopping skips the old routine's
        // tail (its normal clear point), and the new routine re-sets them
        // synchronously inside StartCoroutine below — so there is never a window
        // with flags set but no live routine, and the clear can't stomp the new set.
        if (gravityReversalCoroutine != null)
        {
            cardActionExecutor?.SetManualFlag(ConflictFlags.GravityScale | ConflictFlags.VisualTransform, false);
            StopCoroutine(gravityReversalCoroutine);
            DestroyGravityAura();   // stopping the routine skips its normal cleanup; clear the aura too
        }
        gravityReversalCoroutine = StartCoroutine(GravityReversalRoutine());
    }

    private IEnumerator GravityReversalRoutine()
    {
        // Manual-flag pattern (same as Adrenaline): flags live exactly as long as this
        // routine. This line runs synchronously inside StartCoroutine, so on a replay
        // the clear in StartGravityReversal and this re-set happen in one call stack.
        cardActionExecutor?.SetManualFlag(ConflictFlags.GravityScale | ConflictFlags.VisualTransform, true);

        bool wasAlreadyReversed = isGravityReversed;

        if (!wasAlreadyReversed)
        {
            // First activation: flip gravity, rotate visual upside-down
            isGravityReversed = true;
            ApplyVisualFacing();
            originalGravityScale = rb.gravityScale;
            rb.gravityScale = -originalGravityScale;

            // Anti-gravity aura: parent to the player root (identity rotation) so motes
            // rise in world space rather than inheriting the visual's 180 deg flip.
            if (gravityAuraEffect != null)
            {
                gravityAuraInstance = Instantiate(gravityAuraEffect, transform);
                gravityAuraInstance.transform.localPosition = Vector3.zero;
            }

            yield return StartCoroutine(LerpVisualTransform(0f, 180f, originalVisualLocalPos.y, originalVisualLocalPos.y + visualFlipYOffset, 0.15f));
            // Wait until 0.5s before the 5s mark (5.0 - 0.5 - 0.15 initial rotation = 4.35s)
            yield return new WaitForSeconds(4.35f);
        }
        else
        {
            // Re-play while already reversed: gravity and visual already set, just restart timer.
            // UNREACHABLE since Block enforcement (CardActionExecutor.TryExecute): a
            // ReverseGravity play while the effect is active is refused upstream because its
            // GravityScale|VisualTransform flags overlap activeFlags. Kept deliberately in
            // case the policy later changes to allow same-card timer refresh.
            yield return new WaitForSeconds(4.5f);
        }

        // Warning at t=4.5s: sound + visual strobe
        SfxManager.PlayOn(audioSource, warningSoundClip, soundVolume);
        yield return StartCoroutine(WarningFlashRoutine());

        // t=5.0s: gravity snaps back instantly, visual lerps back
        rb.gravityScale = originalGravityScale;
        isGravityReversed = false;
        ApplyVisualFacing();
        yield return StartCoroutine(LerpVisualTransform(180f, 0f, originalVisualLocalPos.y + visualFlipYOffset, originalVisualLocalPos.y, 0.15f));

        DestroyGravityAura();

        // Cleared only after the visual lerp-back so VisualTransform stays honest
        // for the full effect, mirroring the set at the top of this routine.
        cardActionExecutor?.SetManualFlag(ConflictFlags.GravityScale | ConflictFlags.VisualTransform, false);
        gravityReversalCoroutine = null;
    }

    private void DestroyGravityAura()
    {
        if (gravityAuraInstance != null)
        {
            Destroy(gravityAuraInstance);
            gravityAuraInstance = null;
        }
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
        // Flash the sprite rig (the SkinnedMeshRenderer path was dead after the rig swap).
        // Capture each sprite's own color so the restore is honest even if they differ.
        SpriteRenderer[] sprites = visualModel != null
            ? visualModel.GetComponentsInChildren<SpriteRenderer>(true)
            : new SpriteRenderer[0];
        Color[] originals = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            originals[i] = sprites[i].color;

        Color warnColor = new Color(1f, 0.3f, 0.3f);   // red danger tint

        // 3 rapid on/off cycles ≈ 0.5s total
        for (int c = 0; c < 3; c++)
        {
            SetSpriteColors(sprites, warnColor, null);
            yield return new WaitForSeconds(0.083f);
            SetSpriteColors(sprites, default, originals);
            yield return new WaitForSeconds(0.083f);
        }

        // Guarantee restoration regardless of where the loop ended.
        SetSpriteColors(sprites, default, originals);
    }

    // Tint all sprites to a single color, or restore each to its captured original
    // when 'restore' is supplied.
    private void SetSpriteColors(SpriteRenderer[] sprites, Color flat, Color[] restore)
    {
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            sprites[i].color = restore != null ? restore[i] : flat;
        }
    }
}