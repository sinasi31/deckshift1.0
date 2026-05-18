using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Prefab yapısı: boş GameObject + bu script. Canvas ve UI child'ları Awake'te otomatik oluşturulur.
// EnemyHealth.healthBarPrefab alanına sürükle.
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Font (Inspector'dan ver)")]
    public TMP_FontAsset healthFont;

    private Transform followTarget;
    private Vector3 worldOffset;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Image fillImmediate;
    private Image fillDelayed;
    private TextMeshProUGUI healthText;

    private float targetFill = 1f;
    private float delayedFill = 1f;
    private float fadeTimer = 0f;
    private bool isDamaged = false;
    private float currentHP, maxHP;

    const float CANVAS_SCALE = 0.01f;
    const float BAR_HEIGHT_PX = 16f;
    const float DELAYED_SPEED = 2f;
    const float FADE_DELAY = 3f;
    const float FADE_SPEED = 2f;

    void Awake()
    {
        BuildCanvas();
    }

    void Start()
    {
        SettingsMenu.OnShowNumbersChanged += SetNumbersVisible;
        SetNumbersVisible(PlayerPrefs.GetInt("ShowEnemyNumbers", 1) == 1);
    }

    void OnDestroy()
    {
        SettingsMenu.OnShowNumbersChanged -= SetNumbersVisible;
    }

    void LateUpdate()
    {
        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;

        // Alt katman (delayed fill) mevcut HP'ye smooth lerp ile yaklaşır
        if (!Mathf.Approximately(delayedFill, targetFill))
        {
            delayedFill = Mathf.Lerp(delayedFill, targetFill, Time.deltaTime * DELAYED_SPEED);
            if (Mathf.Abs(delayedFill - targetFill) < 0.001f) delayedFill = targetFill;
            fillDelayed.fillAmount = delayedFill;
        }

        // Son hasardan FADE_DELAY saniye sonra yavaşça kaybol
        if (isDamaged)
        {
            fadeTimer -= Time.deltaTime;
            if (fadeTimer <= 0f) isDamaged = false;
        }
        else if (canvasGroup.alpha > 0f)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.deltaTime * FADE_SPEED);
        }
    }

    public void Initialize(Transform enemy, Vector2 offset, float worldWidth)
    {
        followTarget = enemy;
        worldOffset = new Vector3(offset.x, offset.y, -0.1f);
        SetBarWidth(worldWidth);

        // Düşmanın sprite sorting layer'ına göre canvas sıralamasını ayarla
        SpriteRenderer enemySR = enemy.GetComponentInChildren<SpriteRenderer>();
        if (enemySR != null)
        {
            canvas.sortingLayerID = enemySR.sortingLayerID;
            canvas.sortingOrder = enemySR.sortingOrder + 10;
        }
        else
        {
            SkinnedMeshRenderer enemySMR = enemy.GetComponentInChildren<SkinnedMeshRenderer>();
            if (enemySMR != null)
            {
                canvas.sortingLayerID = enemySMR.sortingLayerID;
                canvas.sortingOrder = enemySMR.sortingOrder + 10;
            }
        }

        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;
    }

    public void SetHealth(float current, float max)
    {
        currentHP = current;
        maxHP = max;
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        // Üst katman anında düşer
        targetFill = ratio;
        fillImmediate.fillAmount = ratio;
        Debug.Log($"[EnemyHealthBar] SetHealth called: current={current}, max={max}, ratio={ratio}, fillImmediate.fillAmount(after assignment)={fillImmediate.fillAmount}, fillImmediate.type={fillImmediate.type}, fillImmediate.sprite={(fillImmediate.sprite == null ? "NULL" : fillImmediate.sprite.name)}");

        // Tam canda bar görünmez kalsın
        if (current < max)
        {
            canvasGroup.alpha = 1f;
            fadeTimer = FADE_DELAY;
            isDamaged = true;
        }

        UpdateText();
    }

    void SetBarWidth(float worldWidth)
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(worldWidth / CANVAS_SCALE, BAR_HEIGHT_PX);
    }

    void UpdateText()
    {
        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(currentHP)} / {Mathf.CeilToInt(maxHP)}";
    }

    void SetNumbersVisible(bool visible)
    {
        if (healthText != null)
            healthText.gameObject.SetActive(visible);
    }

    void BuildCanvas()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        gameObject.AddComponent<CanvasScaler>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;

        RectTransform rootRT = GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(120f, BAR_HEIGHT_PX);
        transform.localScale = Vector3.one * CANVAS_SCALE;

        // 1px siyah border
        Image border = MakeChildImage("Border", rootRT, Color.black);
        FillRect(border.rectTransform, 0f);

        // Alt katman: açık kırmızı/turuncu (delayed — hasar boyutunu gösterir)
        fillDelayed = MakeChildImage("FillDelayed", rootRT, new Color(0.9f, 0.35f, 0.15f));
        SetFillImage(fillDelayed);
        FillRect(fillDelayed.rectTransform, 1f);

        // Üst katman: koyu kırmızı (immediate — gerçek HP)
        fillImmediate = MakeChildImage("FillImmediate", rootRT, new Color(0.8f, 0.133f, 0.133f));
        SetFillImage(fillImmediate);
        FillRect(fillImmediate.rectTransform, 1f);

        // Sayı metni — bar'ın üstünde ortalanmış
        GameObject textGO = MakeChild("HealthText", rootRT);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, 1f);
        textRT.anchorMax = new Vector2(1f, 1f);
        textRT.pivot = new Vector2(0.5f, 0f);
        textRT.sizeDelta = new Vector2(0f, 24f);
        textRT.anchoredPosition = new Vector2(0f, 4f);

        healthText = textGO.AddComponent<TextMeshProUGUI>();
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.fontSize = 14f;
        healthText.color = Color.white;
        healthText.enableWordWrapping = false;
        if (healthFont != null) healthText.font = healthFont;
    }

    static Image MakeChildImage(string name, RectTransform parent, Color color)
    {
        GameObject go = MakeChild(name, parent);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static GameObject MakeChild(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void FillRect(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void SetFillImage(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
    }
}
