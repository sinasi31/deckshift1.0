using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Animator animator;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    public GameObject platformPrefab;
    private SpriteRenderer spriteRenderer;
    private bool isPhasing = false;        
    private float verticalInput;
    private Vector3 originalScale;
    [Header("Fall Settings")]
    public float fallDamage = 20f;
    private Vector3 currentRoomEntryPoint;

    [Header("Economy")]
    public int currentGold = 0;
    public void AddGold(int amount)
    {
        currentGold += amount;
        Debug.Log($"Altýn kazanýldý: {amount}. Toplam: {currentGold}");
    }
    public bool TrySpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            Debug.Log($"Altýn harcandý: {amount}. Kalan: {currentGold}");
            // TODO: UI güncellemesi
            return true;
        }
        else
        {
            Debug.Log("Yetersiz Bakiye!");
            return false;
        }
    }
    [Header("VFX Settings")]
    public GameObject biteEffectPrefab; // Hazýrladýðýmýz kýrmýzý kalýbý buraya koyacaðýz.
    public GameObject leapEffectPrefab; // --- YENÝ EKLENEN: Leap efekti prefabý ---

    [Header("Portal Settings")]
    public GameObject portalPrefab; // Portal prefabýný buraya sürükleyeceðiz
    public float portalMaxRange = 10f; // Ýki portal arasý maksimum mesafe
    public int portalCost = 2;
    private Portal firstPortalInstance; // Sahnedeki ilk portalý aklýmýzda tutacaðýz

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

    [Header("Jump Settings")]
    public float defaultJumpForce = 10f;

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
    public float adrenalineDuration = 3f;  // Etki ne kadar sürsün?
    public float slowMotionFactor = 0.4f; // Zaman ne kadar yavaþlasýn? (0.5 = yarým hýz)
    public float speedBoostMultiplier = 1.5f; // Düþük canda ne kadar hýzlanalým?

    public PlayerState currentState;
    private bool isGrounded;

    [Header("Combat Settings")]
    public GameObject fireballPrefab;
    public Transform firePoint;
    public float wailRange = 10f; // Çýðlýk menzili
    // VAMPIRIC BITE 
    public float biteRange = 1.5f; // Isýrma menzili (kýsa olmalý)
    public float biteHealAmount = 10f; // Isýrýnca kaç can geleceði
    public LayerMask enemyLayer; // Düþmanlarý tanýmak için katman maskesi


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        originalScale = transform.localScale;
        currentHealth = maxHealth;
        currentShift = maxShift; // Güncellendi
        ChangeState(PlayerState.Idle);
    }

    void Update()
    {
        if (GameManager.instance != null && GameManager.instance.currentState == GameState.Paused)
        {
            moveInput = 0;
            verticalInput = 0;
            return;
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

        isGrounded = IsGroundedCheck();
        isWallDetected = WallCheck();

        if (!isPhasing)
        {
            HandleStateTransitions();
            UpdateAnimations();
        }
    }
    private void UpdateAnimations()
    {
        if (animator == null) return;
        animator.SetFloat("Speed", Mathf.Abs(moveInput));
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetFloat("yVelocity", rb.linearVelocity.y);
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
        else if (currentState != PlayerState.Dashing && currentState != PlayerState.KnockedBack)
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
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
                rb.AddForce(new Vector2(0f, value), ForceMode2D.Impulse);
                // 2. YENÝ EKLEME: Sadece efekti oluþturuyoruz
                if (leapEffectPrefab != null)
                {
                    // Ayaklarýn hizasý için Y ekseninde -0.8f aþaðý iniyoruz
                    Vector3 spawnPos = transform.position + new Vector3(0f, -0.8f, 0f);
                    Instantiate(leapEffectPrefab, spawnPos, Quaternion.identity);
                }
                ChangeState(PlayerState.Jumping);
                return true;
            case CardActionType.VampiricBite:
                PerformVampiricBite(value);
                return true;

            case CardActionType.Phase:
                PerformPhase(value); // value = kaç saniye süreceði
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
                    Debug.Log("Sadece havadayken Comet Dive atabilirsin!");
                    return false; // Kart harcanmaz
                }

            case CardActionType.Adrenaline: // Ýsim deðiþti
                UseAdrenaline(value);
                return true;
        }
        return false;
    }
    private void PerformJump(float jumpForce)
    {
        if (currentShift > 0)
        {
            currentShift--;
            Debug.Log($"Jumped! Shift remaining: {currentShift}");

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            rb.AddForce(new Vector2(0f, jumpForce), ForceMode2D.Impulse);
            ChangeState(PlayerState.Jumping);
        }
        else
        {
            Debug.LogWarning("Tried to jump, but no Shift left!");
        }
    }

    private IEnumerator PerformDash(float dashDistance, int direction)
    {
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
        if (isInvincible) { return; }
        if (CameraShake.instance != null)
            CameraShake.instance.Shake(0.2f, 0.3f);
        // ------------------------

        tookDamageThisRoom = true;
        currentHealth = Mathf.Max(currentHealth - damage, 0f);
        if (currentHealth <= 0) { Die(); }
    }

    public void OnNewRoomEnter()
    {
        tookDamageThisRoom = false;
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        SceneManager.LoadScene("GameOverScene");
    }

    private bool IsGroundedCheck()
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
        if (fireballPrefab == null || firePoint == null) return;

        Quaternion fireballRotation = (transform.localScale.x < 0) ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
        GameObject fireballInstance = Instantiate(fireballPrefab, firePoint.position, fireballRotation);

        Fireball fireballScript = fireballInstance.GetComponent<Fireball>();
        if (fireballScript != null)
            fireballScript.damage = damageFromCard;
    }

    // --- "JUMP CHARGE" -> "SHIFT" OLARAK GÜNCELLENDÝ ---
    public void AddShift(int amount)
    {
        currentShift = Mathf.Min(currentShift + amount, maxShift);
        Debug.Log($"Gained {amount} shift! Current: {currentShift}");
    }

    public void ResetShiftToMax()
    {
        currentShift = maxShift;
    }

    // --- BU FONKSÝYON EKSÝKTÝ, EKLENDÝ ---
    public void SpendShift(int amount)
    {
        if (amount <= 0) return;
        currentShift = Mathf.Max(0, currentShift - amount);
        Debug.Log($"Spent {amount} shift. Remaining: {currentShift}");
    }
    // --- ÝMZA DEÐÝÞTÝ: 'out bool keepCard' eklendi ---
    private bool TryPlacePortal(out bool keepCard)
    {
        keepCard = false;
        if (portalPrefab == null) return false;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // --- DURUM 1: ÝLK PORTAL (Ayný kalýyor) ---
        if (firstPortalInstance == null)
        {
            GameObject p1 = Instantiate(portalPrefab, mousePos, Quaternion.identity);
            firstPortalInstance = p1.GetComponent<Portal>();
            firstPortalInstance.spriteRenderer.color = Color.gray;

            // --- YENÝ EKLENEN SATIR ---
            // Ýlk portalý koyduðumuz an menzil halkasýný göster!
            firstPortalInstance.ShowRangeCircle(portalMaxRange);
            // --------------------------

            Debug.Log("Ýlk Portal yerleþtirildi...");
            keepCard = true;
            return true;
        }
        // --- DURUM 2: ÝKÝNCÝ PORTAL (BURAYI GÜNCELLÝYORUZ) ---
        else
        {
            // Mesafe kontrolü
            float distance = Vector2.Distance(firstPortalInstance.transform.position, mousePos);
            if (distance > portalMaxRange)
            {
                Debug.LogWarning("Mesafe çok uzak!");
                keepCard = true;
                return false;
            }

            // --- YENÝ: ÝNDÝRÝM HESAPLAMA ---
            int finalCost = portalCost; // Varsayýlan maliyet (2)

            // Eðer "Discount" skilli varsa maliyeti 1 düþür
            if (SkillManager.instance != null && SkillManager.instance.HasSkill(SkillType.KineticDiscount))
            {
                finalCost = Mathf.Max(0, finalCost - 1);
                Debug.Log($"Portal maliyetine indirim uygulandý! Yeni maliyet: {finalCost}");
            }
            // -------------------------------

            // Shift kontrolü (finalCost üzerinden)
            if (currentShift < finalCost)
            {
                Debug.LogWarning($"Yeterli Shift yok! {finalCost} gerekiyor.");
                keepCard = true;
                return false;
            }

            // Shift harca (finalCost kadar)
            SpendShift(finalCost);

            // Portalý koy
            GameObject p2 = Instantiate(portalPrefab, mousePos, Quaternion.identity);
            Portal secondPortal = p2.GetComponent<Portal>();

            firstPortalInstance.Link(secondPortal);
            firstPortalInstance = null;

            Debug.Log("Portal baðlantýsý kuruldu!");
            keepCard = false;
            return true;
        }
    }
    // --- YENÝ: ISIRMA FONKSÝYONU ---
    private void PerformVampiricBite(float damageAmount)
    {
        Collider2D hitEnemy = Physics2D.OverlapCircle(firePoint.position, biteRange, enemyLayer);

        if (hitEnemy != null)
        {
            // --- YENÝ VE TEK KONTROL ---
            // Isýrdýðým þeyin Caný (EnemyHealth) var mý?
            EnemyHealth targetHealth = hitEnemy.GetComponent<EnemyHealth>();

            if (targetHealth != null)
            {
                targetHealth.TakeDamage(damageAmount); // Hasar ver
                Heal(biteHealAmount); // Can çal
                // --- BURAYA YAPIÞTIR ---
                if (biteEffectPrefab != null)
                {
                    Instantiate(biteEffectPrefab, hitEnemy.transform.position, Quaternion.identity);
                }
                // -----------------------
                Debug.Log("Bir þey ýsýrýldý!");
            }
            // --- BÝTÝÞ ---
        }
        else
        {
            Debug.Log("Isýracak kimse yok!");
        }
    }

    // --- YENÝ: ÝYÝLEÞME FONKSÝYONU ---
    public void Heal(float amount)
    {
        // Caný artýr ama Maksimum Caný geçmesin
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"Ýyileþildi! Güncel Can: {currentHealth}");

        // UI otomatik olarak Update() içinde güncellendiði için baþka bir þey yapmana gerek yok.
    }
    private void PerformGlassWail(float stunDuration)
    {
        EnemyHealth[] allEnemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);

        Debug.Log($"GLOBAL Glass Wail kullanýldý! {allEnemies.Length} düþman dondu.");
        if (CameraShake.instance != null)
            CameraShake.instance.Shake(0.5f, 0.5f);

        foreach (EnemyHealth enemy in allEnemies)
        {
            enemy.Stun(stunDuration);
        }

        // TODO: Buraya tüm ekraný kaplayan bir beyaz flaþ efekti eklersen çok havalý olur.
    }
    private void PerformPhase(float duration)
    {
        StartCoroutine(PhaseRoutine(duration));
    }

    private IEnumerator PhaseRoutine(float duration)
    {
        isPhasing = true; // Kontrolleri uçuþ moduna al

        // 1. Yerçekimini KAPAT (Havada asýlý kalsýn)
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero; // Düþmeyi durdur

        // 2. Katmanlarý Belirle (Unity'deki isimlerin AYNI olmasý lazým)
        int playerLayer = LayerMask.NameToLayer("Player");
        int groundLayer = LayerMask.NameToLayer("Ground");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        // 3. Çarpýþmalarý KAPAT (Duvar ve Düþman içinden geç)
        Physics2D.IgnoreLayerCollision(playerLayer, groundLayer, true);
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);

        // 4. Görseli Þeffaf Yap
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 0.4f; // %40 Görünürlük
            spriteRenderer.color = color;
        }

        Debug.Log("HAYALET MODU AÇIK: Uçabilir ve içinden geçebilirsin.");

        // --- SÜRE BOYUNCA BEKLE ---
        yield return new WaitForSeconds(duration);

        // --- BÝTÝÞ ---

        // 5. Çarpýþmalarý GERÝ AÇ
        Physics2D.IgnoreLayerCollision(playerLayer, groundLayer, false);
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);

        // 6. Yerçekimini GERÝ AÇ
        rb.gravityScale = originalGravity;

        // 7. Görseli Düzelt
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 1f; // Tam Görünürlük
            spriteRenderer.color = color;
        }

        isPhasing = false; // Normal kontrollere dön
        Debug.Log("HAYALET MODU KAPANDI.");
    }
    public void IncreaseMaxShift(int amount)
    {
        maxShift += amount;
        currentShift += amount;
        // UI otomatik güncellenecek
    }
    public void EnterCannon(Transform cannonTransform)
    {
        ChangeState(PlayerState.InCannon);

        rb.linearVelocity = Vector2.zero; 
        rb.bodyType = RigidbodyType2D.Kinematic; 

        
        if (GetComponent<SpriteRenderer>() != null)
            GetComponent<SpriteRenderer>().enabled = false;

        transform.position = cannonTransform.position;
        
        transform.SetParent(cannonTransform);
    }
    private void PerformCometDive()
    {
        ChangeState(PlayerState.CometDiving);

        // Hareketi dondur ve hýzla aþaðý it
        rb.linearVelocity = new Vector2(0, -cometSpeed);

        // Ýstersen havada kýsa bir an asýlý kalýp sonra düþebilir (daha havalý olur)
        // Ama þimdilik direkt düþürüyoruz.
    }

    // Yere çarpmayý algýlamak için OnCollisionEnter2D'yi güncelliyoruz
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Debug satýrýný açýk býrakalým ki neye çarptýðýný görelim
        // Debug.Log($"Çarpýlan Obje: {collision.gameObject.name} - Layer: {LayerMask.LayerToName(collision.gameObject.layer)}");

        // Eðer Comet Dive modundaysak
        if (currentState == PlayerState.CometDiving)
        {
            // 1. Kontrol: Çarptýðýmýz þey ZEMÝN mi?
            bool isGround = (groundLayer.value & (1 << collision.gameObject.layer)) > 0;

            // 2. Kontrol: Çarptýðýmýz þey DÜÞMAN mý? (Yeni ekledik)
            bool isEnemy = (enemyLayer.value & (1 << collision.gameObject.layer)) > 0;

            // Eðer Zemin VEYA Düþman ise patlat!
            if (isGround || isEnemy)
            {
                Debug.Log("Comet Dive: Hedefe (Yer/Düþman) çakýldýk! PATLIYOR!");
                CometImpact();
            }
            else
            {
                Debug.LogWarning($"Comet Dive ile bir þeye çarptýk ama Layer listemizde yok. Çarpýlan: {LayerMask.LayerToName(collision.gameObject.layer)}");
            }
        }

        // Buranýn altýna senin varsa eski hasar alma kodlarýn (TakeDamage) gelebilir.
    }

    private void CometImpact()
    {
        // Çarpma anýnda durumu normale çevir
        ChangeState(PlayerState.Idle);

        // 1. Alan Hasarý Kontrolü
        // Gizmos'ta gördüðün sarý çemberin içinde "Enemy" layer'ý arýyoruz.
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, cometRadius, enemyLayer);

        Debug.Log($"Comet Impact Alaný: {cometRadius} birim. Bulunan Obje Sayýsý: {hitEnemies.Length}");

        foreach (Collider2D enemy in hitEnemies)
        {
            // Bulunan objenin adýný yazdýr
            Debug.Log($"Vurulan Düþman: {enemy.name}");

            EnemyHealth eHealth = enemy.GetComponent<EnemyHealth>();
            if (eHealth != null)
            {
                eHealth.TakeDamage(cometDamage);
                Debug.Log($"{enemy.name} hasar aldý: {cometDamage}");
            }
            else
            {
                Debug.LogError($"HATA: {enemy.name} üzerinde 'EnemyHealth' scripti bulunamadý!");
            }
        }

        // 2. Efekt ve Kamera
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.3f, 0.5f);
        if (cometImpactEffect != null) Instantiate(cometImpactEffect, transform.position, Quaternion.identity);
    }
    private void UseAdrenaline(float value)
    {
        float healthPercentage = currentHealth / maxHealth;

        // Efekt (Varsa ekle)
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.1f, 0.5f);

        if (healthPercentage > 0.5f)
        {
            // --- DURUM A: YÜKSEK CAN (OVERDOSE / BULLET TIME) ---
            // "Adrenalin duyularýný keskinleþtirir, zaman yavaþlar."
            Debug.Log("Adrenaline: Bullet Time Modu!");
            StartCoroutine(AdrenalineSlowMoRoutine());
        }
        else
        {
            // --- DURUM B: DÜÞÜK CAN (EMERGENCY STIM) ---
            // "Kalbine iðne saplanmýþ gibi kendine getirir."
            Debug.Log("Adrenaline: Acil Durum Ýyileþmesi!");

            // 1. Ýyileþme (Kartýn 'value' deðeri kadar can ver)
            Heal(value);

            // 2. Hýzlanma (Kaçmak için)
            StartCoroutine(AdrenalineSpeedBoostRoutine());
        }
    }

    // Zamaný Yavaþlatma Coroutine'i
    private IEnumerator AdrenalineSlowMoRoutine()
    {
        // Zamaný yavaþlat
        Time.timeScale = slowMotionFactor;
        // Fizik hesaplamalarýný da yavaþlat ki karakterler titremesin
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // Gerçek dünyada 'duration' kadar bekle (Oyun zamaný yavaþladýðý için WaitForSecondsRealtime kullanýyoruz)
        yield return new WaitForSecondsRealtime(adrenalineDuration);

        // Zamaný normale döndür
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        Debug.Log("Adrenaline etkisi geçti.");
    }

    // Hýzlandýrma Coroutine'i
    private IEnumerator AdrenalineSpeedBoostRoutine()
    {
        float originalSpeed = moveSpeed;
        moveSpeed *= speedBoostMultiplier; // Hýzý artýr

        yield return new WaitForSeconds(adrenalineDuration);

        moveSpeed = originalSpeed; // Hýzý normale döndür
    }


    public void LaunchFromCannon(Vector2 forceVector)
    {
        transform.SetParent(null);
        transform.localScale = originalScale;
        transform.rotation = Quaternion.identity;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        if (GetComponent<SpriteRenderer>() != null)
            GetComponent<SpriteRenderer>().enabled = true;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.AddForce(forceVector, ForceMode2D.Impulse);
        ChangeState(PlayerState.Jumping);
    }
}