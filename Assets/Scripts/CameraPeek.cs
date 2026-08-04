using UnityEngine;

public class CameraPeek : MonoBehaviour
{
    public static CameraPeek instance;

    [Header("Peek Settings")]
    // Peeking exists so the player can SCOUT before spending Shift — see what is down a shaft or
    // past a gap and decide whether the route is worth it. At 3 units it barely cleared the
    // player's own body and showed nothing they could not already see, so it did not answer any
    // question worth pressing a key for. 9 reaches roughly a screen out, which is far enough to
    // read the next chamber. The camera still clamps to the room's CameraBounds, so this can never
    // show the outside of the level however far it is pushed.
    [SerializeField] float maxOffset = 9f;
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
