using UnityEngine;
using Unity.Cinemachine; // Cinemachine kütüphanesi şart

public class CameraPeek : MonoBehaviour
{
    [Header("Peek Ayarları")]
    public KeyCode peekKey = KeyCode.LeftControl; // Hangi tuşla bakılacak?
    public float peekSpeed = 20f;                 // Kamera ne kadar hızlı kaysın?
    public float maxPeekDistance = 15f;           // Oyuncudan en fazla ne kadar uzağa bakabilsin?

    private CinemachineCamera vCam;
    private CinemachineConfiner2D confiner;
    private Transform originalTarget;
    private GameObject peekTargetObj;
    private PlayerController player;

    void Start()
    {
        player = GetComponent<PlayerController>();
        vCam = FindFirstObjectByType<CinemachineCamera>();

        peekTargetObj = new GameObject("CameraPeekTarget");
        peekTargetObj.transform.position = transform.position;

        if (vCam != null)
        {
            originalTarget = vCam.Follow;
            confiner = vCam.GetComponent<CinemachineConfiner2D>(); // --- YENİ EKLENDİ ---
        }
    }

    void Update()
    {
        if (vCam == null || player == null) return;

        // 1. Oyuncu havada süzülürken hile yapamasın diye, sadece yere basıyorken açsın
        bool isGrounded = Physics2D.OverlapCircle(player.groundCheck.position, player.groundCheckRadius, player.groundLayer);

        if (Input.GetKeyDown(peekKey) && isGrounded)
        {
            StartPeek();
        }
        else if (Input.GetKeyUp(peekKey) || (player.isPeeking && !isGrounded))
        {
            // Tuşu bırakırsa VEYA haritaya bakarken altındaki platform çökerse hemen kamerayı geri ver
            StopPeek();
        }

        if (player.isPeeking)
        {
            HandlePeekMovement();
        }
    }

    void StartPeek()
    {
        player.isPeeking = true;
        peekTargetObj.transform.position = transform.position; // Görünmez hedefi oyuncunun yanına çek
        vCam.Follow = peekTargetObj.transform;                 // Kameraya "Artık bunu takip et" de
    }

    void StopPeek()
    {
        if (!player.isPeeking) return;

        player.isPeeking = false;
        vCam.Follow = originalTarget; // Kamerayı tekrar oyuncuya bağla
    }

    void HandlePeekMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 moveDir = new Vector3(horizontal, vertical, 0).normalized;
        peekTargetObj.transform.position += moveDir * peekSpeed * Time.deltaTime;

        // 1. OYUNCUDAN UZAKLAŞMA SINIRI (Mevcut sınırımız)
        float distanceToPlayer = Vector2.Distance(transform.position, peekTargetObj.transform.position);
        if (distanceToPlayer > maxPeekDistance)
        {
            Vector3 limitDir = (peekTargetObj.transform.position - transform.position).normalized;
            peekTargetObj.transform.position = transform.position + limitDir * maxPeekDistance;
        }

        // --- 2. YENİ: KAMERADAN KOPMA SINIRI (Duvar Çözümü) ---
        // Eğer kamera duvara çarparsa, hedef noktanın ekranın dışına doğru yürümesini engelle.
        Vector2 cameraPos = new Vector2(vCam.transform.position.x, vCam.transform.position.y);
        Vector2 targetPos = new Vector2(peekTargetObj.transform.position.x, peekTargetObj.transform.position.y);

        float distanceToCamera = Vector2.Distance(cameraPos, targetPos);

        // Hedef kameranın merkezinden 1 birimden fazla uzaklaşamaz
        float maxDistanceFromCamera = 1.0f;

        if (distanceToCamera > maxDistanceFromCamera)
        {
            Vector2 limitDir = (targetPos - cameraPos).normalized;
            Vector2 clampedPos = cameraPos + limitDir * maxDistanceFromCamera;
            peekTargetObj.transform.position = new Vector3(clampedPos.x, clampedPos.y, peekTargetObj.transform.position.z);
        }
        // -------------------------------------------------------
    }
}