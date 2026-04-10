using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Animator animator;

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
    private bool isGrounded;

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // GÜNCELLENDİ: Artık kendi üzerimizde değil, child objedeki (PF Skeleton) Animator'ı arıyoruz.
        animator = GetComponentInChildren<Animator>();
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
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetKey(KeyCode.Space))
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
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
        animator.SetFloat("MovingBlend", movingBlend);

        // GÜNCELLENDİ: Yeni paket "SpeedVertical" kullanıyor
        animator.SetFloat("SpeedVertical", rb.linearVelocity.y);

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
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
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

        if (moveInput > 0 && transform.localScale.x < 0) { Flip(); }
        else if (moveInput < 0 && transform.localScale.x > 0) { Flip(); }
    }

    private void ChangeState(PlayerState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
    }

    private void Flip()
    {
        Vector3 newScale = transform.localScale;
        newScale.x *= -1;
        transform.localScale = newScale;
    }

    public bool ExecuteAction(CardActionType type, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;

        switch (type)
        {
            case CardActionType.Jump:
                if (audioSource != null && leapSound != null)
                {
                    audioSource.PlayOneShot(leapSound);
                }
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
                rb.AddForce(new Vector2(0f, value), ForceMode2D.Impulse);
                if (leapEffectPrefab != null)
                {
                    Vector3 spawnPos = transform.position + new Vector3(0f, -0.8f, 0f);
                    Instantiate(leapEffectPrefab, spawnPos, Quaternion.identity);
                }
                ChangeState(PlayerState.Jumping);
                return true;

            case CardActionType.VampiricBite:
                PerformVampiricBite(value);
                return true;

            case CardActionType.Phase:
                PerformPhase(value);
                return true;
            case CardActionType.Stagger:
                PerformStagger();
                return true;

            case CardActionType.DashForward:
            case CardActionType.DashBackward:
                if (currentState != PlayerState.Dashing)
                {
                    int direction = (type == CardActionType.DashForward) ? 1 : -1;
                    direction *= (int)Mathf.Sign(transform.localScale.x);
                    StartCoroutine(PerformDash(value, direction));
                    return true;
                }
                return false;

            case CardActionType.WallCling:
                StartCoroutine(ActivateWallCling(value));
                return true;

            case CardActionType.DrawCards:
                if (DeckManager.instance != null)
                {
                    for (int i = 0; i < Mathf.RoundToInt(value); i++)
                        DeckManager.instance.DrawCard();
                }
                return true;

            case CardActionType.GainJumpCharges:
                AddShift(Mathf.RoundToInt(value));
                return true;

            case CardActionType.PlatformCreate:
                if (platformPrefab == null) return false;
                if (audioSource != null && createPlatformSound != null)
                {
                    audioSource.PlayOneShot(createPlatformSound);
                }
                Vector2 spawnPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Instantiate(platformPrefab, spawnPosition, Quaternion.identity);
                return true;

            case CardActionType.Fireball:
                PerformFireball(value);
                return true;

            case CardActionType.Portal:
                return TryPlacePortal(out keepCardInHand);

            case CardActionType.GlassWail:
                PerformGlassWail(value);
                return true;

            case CardActionType.CometDive:
                if (!isGrounded)
                {
                    PerformCometDive();
                    return true;
                }
                else
                {
                    Debug.Log("Comet Dive için havada olmalısın!");
                    return false;
                }

            case CardActionType.Adrenaline:
                UseAdrenaline(value);
                return true;
        }
        return false;
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
            currentShift--;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            rb.AddForce(new Vector2(0f, jumpForce), ForceMode2D.Impulse);
            ChangeState(PlayerState.Jumping);
        }
    }

    private IEnumerator PerformDash(float dashDistance, int direction)
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
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void PerformWallJump()
    {
        Flip();
        rb.linearVelocity = new Vector2(wallJumpForce.x * transform.localScale.x, wallJumpForce.y);
        ChangeState(PlayerState.Jumping);
    }

    private IEnumerator ActivateWallCling(float duration)
    {
        canWallCling = true;
        yield return new WaitForSeconds(duration);
        canWallCling = false;
    }

    private bool WallCheck()
    {
        return Physics2D.Raycast(wallCheck.position, Vector2.right * transform.localScale.x, wallCheckDistance, groundLayer);
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = IsGroundedCheck() ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (wallCheck != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + (Vector3.right * transform.localScale.x * wallCheckDistance));
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
            FallAndRespawn();
    }

    private void FallAndRespawn()
    {
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

        Quaternion fireballRotation = (transform.localScale.x < 0) ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
        GameObject fireballInstance = Instantiate(fireballPrefab, firePoint.position, fireballRotation);

        Fireball fireballScript = fireballInstance.GetComponent<Fireball>();
        if (fireballScript != null)
            fireballScript.damage = damageFromCard;
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

    private bool TryPlacePortal(out bool keepCard)
    {
        keepCard = false;
        if (portalPrefab == null) return false;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

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

            SpendShift(finalCost);

            GameObject p2 = Instantiate(portalPrefab, mousePos, Quaternion.identity);
            Portal secondPortal = p2.GetComponent<Portal>();

            firstPortalInstance.Link(secondPortal);
            firstPortalInstance = null;

            keepCard = false;
            return true;
        }
    }

    private void PerformVampiricBite(float damageAmount)
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

    private void PerformGlassWail(float stunDuration)
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

    private void PerformPhase(float duration)
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

    private void PerformCometDive()
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
        }
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

    private void UseAdrenaline(float value)
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

    private void PerformStagger()
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
}