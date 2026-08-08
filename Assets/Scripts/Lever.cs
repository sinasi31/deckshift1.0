using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Lever : MonoBehaviour, IInteractable
{
    // AC Switch 01.controller uses a single Bool parameter "IsOn" (confirmed from YAML).
    // States are Off / Turn On / On / Turn Of — "IsOn" is the only parameter.
    [SerializeField] string animatorBoolParam = "IsOn";
    [SerializeField] bool startOn = false;
    // Public so the level importer can wire persistent listeners (Gate.Open/Close).
    public UnityEvent OnFlippedOn;
    public UnityEvent OnFlippedOff;
    [SerializeField] AudioClip flipSound;
    [SerializeField] GameObject prompt;

    [Header("Crusher cooldown mode")]
    [Tooltip("If set, the lever becomes a momentary switch: it fires this crusher, drops, shows a " +
             "cooldown clock, then rises back up when the crusher is rearmed. Leave empty for a plain toggle.")]
    [SerializeField] CrusherTrap crusher;

    [Header("Cooldown clock")]
    [Tooltip("World-space height of the clock above the lever.")]
    [SerializeField] float clockHeight = 0.95f;
    [Tooltip("World-space diameter of the clock.")]
    [SerializeField] float clockSize = 0.55f;
    [SerializeField] Color clockFillColor = new Color(0.95f, 0.85f, 0.3f, 0.95f);
    [SerializeField] Color clockBackColor = new Color(0.05f, 0.05f, 0.05f, 0.7f);
    [SerializeField] Color clockRingColor = new Color(0f, 0f, 0f, 0.85f);

    private bool isOn;
    private bool cooling;               // momentary lever is down + on cooldown
    private Animator animator;
    private AudioSource audioSource;

    private Canvas clockCanvas;
    private Image clockFill;
    private const float CLOCK_CANVAS_SCALE = 0.01f;
    private static Sprite cachedDisc;

    void Awake()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        BuildClock();
    }

    void Start()
    {
        isOn = startOn;
        if (animator != null) animator.SetBool(animatorBoolParam, isOn);
        HideClock();
    }

    void Update()
    {
        if (!cooling || crusher == null) return;

        clockFill.fillAmount = crusher.CooldownRemaining01;

        if (crusher.IsReady)
        {
            cooling = false;
            isOn = false;
            if (animator != null) animator.SetBool(animatorBoolParam, false);   // lever rises back up
            // NOTE: intentionally no OnFlippedOff.Invoke() here — in crusher mode the lever drives the
            // crusher directly, and re-firing that event would re-trigger a slam on the rise.
            HideClock();
        }
    }

    // Called by PlayerController.CheckInteraction() when the player presses E.
    public void Interact()
    {
        // Momentary crusher lever: drop, fire, then auto-rise when the crusher rearms.
        if (crusher != null)
        {
            if (cooling || !crusher.IsReady) return;   // still cooling down — ignore the pull

            isOn = true;
            if (animator != null) animator.SetBool(animatorBoolParam, true);   // lever drops
            PlayFlip();
            crusher.Activate();   // drives the crusher directly (no UnityEvent — avoids double/re-fire)
            cooling = true;
            ShowClock();
            return;
        }

        // Plain toggle (original behavior) for any other lever.
        isOn = !isOn;
        if (animator != null) animator.SetBool(animatorBoolParam, isOn);
        PlayFlip();
        if (isOn) OnFlippedOn.Invoke();
        else OnFlippedOff.Invoke();
    }

    private void PlayFlip()
    {
        if (flipSound == null) return;
        if (audioSource != null) SfxManager.PlayOn(audioSource, flipSound);
        else SfxManager.PlayAtPoint(flipSound, transform.position);
    }

    public string GetInteractText()
    {
        if (crusher != null) return cooling ? "" : "Pull Lever";
        return isOn ? "Turn Off" : "Turn On";
    }

    // Mirrors SimpleInteract's prompt pattern exactly.
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null)
            prompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null)
            prompt.SetActive(false);
    }

    // ---- Cooldown clock: a procedural world-space radial timer above the lever ----
    private void BuildClock()
    {
        GameObject go = new GameObject("CooldownClock");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, clockHeight, -0.1f);
        go.transform.localScale = Vector3.one * CLOCK_CANVAS_SCALE;

        clockCanvas = go.AddComponent<Canvas>();
        clockCanvas.renderMode = RenderMode.WorldSpace;
        clockCanvas.overrideSorting = true;
        clockCanvas.sortingOrder = 50;
        go.AddComponent<CanvasScaler>();

        RectTransform root = go.GetComponent<RectTransform>();
        float px = clockSize / CLOCK_CANVAS_SCALE;
        root.sizeDelta = new Vector2(px, px);

        MakeDisc("Ring", root, clockRingColor, 1.14f);   // outline (behind, slightly larger)
        MakeDisc("Back", root, clockBackColor, 1f);      // dark background disc

        clockFill = MakeDisc("Fill", root, clockFillColor, 0.86f);
        clockFill.type = Image.Type.Filled;
        clockFill.fillMethod = Image.FillMethod.Radial360;
        clockFill.fillOrigin = (int)Image.Origin360.Top;
        clockFill.fillClockwise = true;
        clockFill.fillAmount = 1f;
    }

    private Image MakeDisc(string name, RectTransform parent, Color color, float scale)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one * scale;

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = GetDiscSprite();
        img.raycastTarget = false;
        return img;
    }

    private void ShowClock()
    {
        if (clockCanvas == null) return;
        clockFill.fillAmount = 1f;
        clockCanvas.gameObject.SetActive(true);
    }

    private void HideClock()
    {
        if (clockCanvas != null) clockCanvas.gameObject.SetActive(false);
    }

    // A crisp white filled disc, tinted per-Image. 1 sprite, cached and reused.
    private static Sprite GetDiscSprite()
    {
        if (cachedDisc != null) return cachedDisc;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size / 2f;
        Vector2 c = new Vector2(r, r);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.Clamp01((r - d) / 1.5f);   // crisp edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        cachedDisc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedDisc;
    }
}
