using UnityEngine;

public class CameraPeek : MonoBehaviour
{
    public static CameraPeek instance;

    [Header("Peek Settings")]
    [SerializeField] float maxOffset = 3f;
    [SerializeField] float lerpDuration = 0.3f;
    [SerializeField] KeyCode peekKey = KeyCode.LeftControl;

    public Vector2 peekOffset { get; private set; }

    private Camera cam;
    private PlayerHealth playerHealth;

    void Awake()
    {
        instance = this;
        cam = Camera.main;
    }

    void Update()
    {
        if (playerHealth == null && GameManager.instance != null && GameManager.instance.player != null)
            playerHealth = GameManager.instance.player.GetComponent<PlayerHealth>();

        bool inputBlocked =
            (GameManager.instance != null && GameManager.instance.currentState == GameState.Paused) ||
            (HandUIDrawer.instance != null && HandUIDrawer.instance.isLocked) ||
            (playerHealth != null && playerHealth.IsDead);

        Vector2 target = Vector2.zero;

        if (!inputBlocked && Input.GetKey(peekKey))
        {
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 mousePos = Input.mousePosition;
            Vector2 norm = new Vector2(
                (mousePos.x - screenCenter.x) / screenCenter.x,
                (mousePos.y - screenCenter.y) / screenCenter.y
            );
            norm.x = Mathf.Clamp(norm.x, -1f, 1f);
            norm.y = Mathf.Clamp(norm.y, -1f, 1f);
            target = norm * maxOffset;
        }

        float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / (lerpDuration / 3f));
        peekOffset = Vector2.Lerp(peekOffset, target, t);
    }
}
