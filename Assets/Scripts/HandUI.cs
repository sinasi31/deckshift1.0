using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandUI : MonoBehaviour
{
    [Header("UI Ayarları")]
    public GameObject cardUIPrefab;
    public Transform handContainer;

    [Header("Animasyon Ayarları")]
    public Transform drawPilePosition;
    public float cardFlySpeed = 80f;
    public float dealDelay = 0.03f;

    [Header("Ses Efektleri")]
    public AudioClip drawSound; // Buraya ses dosyasını sürükleyeceksin
    [Range(0f, 1f)] public float soundVolume = 0.5f;
    private AudioSource audioSource;

    private List<GameObject> activeGhosts = new List<GameObject>();
    private Coroutine animateCoroutine;
    private Coroutine popCoroutine;

    private void Awake()
    {
        // Objenin üzerinde AudioSource var mı diye bakar, yoksa ekler
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        DeckManager.OnHandChanged += UpdateHandDisplay;
        DeckManager.OnCardPlayed += HandleCardPlayed;
    }

    private void OnDisable()
    {
        DeckManager.OnHandChanged -= UpdateHandDisplay;
        DeckManager.OnCardPlayed -= HandleCardPlayed;
    }

    private void Start()
    {
        StartCoroutine(StartWithDelay());
    }

    private IEnumerator StartWithDelay()
    {
        yield return new WaitForSeconds(0.2f);
        UpdateHandDisplay(true);
    }

    public void UpdateHandDisplay(bool animate)
    {
        if (animateCoroutine != null)
        {
            StopCoroutine(animateCoroutine);
            animateCoroutine = null;
        }

        // Hayalet Temizliği
        foreach (GameObject ghost in activeGhosts)
        {
            if (ghost != null) Destroy(ghost);
        }
        activeGhosts.Clear();

        // Kart Temizliği
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in handContainer)
            toDestroy.Add(child.gameObject);
        foreach (GameObject go in toDestroy)
            Destroy(go);

        if (DeckManager.instance == null) return;

        List<RuntimeCard> currentHand = DeckManager.instance.GetCurrentHand();
        List<CardUI> createdCards = new List<CardUI>();

        // Kartları Yarat
        for (int i = 0; i < currentHand.Count; i++)
        {
            GameObject cardObj = Instantiate(cardUIPrefab, handContainer);
            CardUI ui = cardObj.GetComponent<CardUI>();

            if (ui != null)
            {
                ui.Setup(currentHand[i], i);
                createdCards.Add(ui);
            }

            CanvasGroup cg = cardObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = cardObj.AddComponent<CanvasGroup>();

            if (!animate || drawPilePosition == null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
            }
            else
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(handContainer.GetComponent<RectTransform>());

        if (animate && drawPilePosition != null && createdCards.Count > 0)
        {
            animateCoroutine = StartCoroutine(SafeAnimateRoutine(createdCards));
        }
    }

    private IEnumerator SafeAnimateRoutine(List<CardUI> targets)
    {
        yield return new WaitForEndOfFrame();

        foreach (CardUI targetUI in targets)
        {
            if (targetUI == null) continue;

            CanvasGroup targetCG = targetUI.GetComponent<CanvasGroup>();
            if (targetCG == null) targetCG = targetUI.gameObject.AddComponent<CanvasGroup>();

            // --- SESİ BURADA ÇALIYORUZ ---
            if (drawSound != null && audioSource != null)
            {
                // Pitch'i hafifçe değiştiriyoruz (0.9 ile 1.1 arası) ki ses doğal gelsin
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(drawSound, soundVolume);
            }
            // -----------------------------

            // Ghost Yarat
            GameObject ghost = Instantiate(cardUIPrefab, handContainer.parent);
            ghost.transform.position = drawPilePosition.position;
            ghost.transform.localScale = targetUI.transform.localScale;
            activeGhosts.Add(ghost);

            CardUI ghostUI = ghost.GetComponent<CardUI>();
            if (ghostUI != null) ghostUI.Setup(targetUI.GetCard(), -1);

            Destroy(ghost.GetComponent<CardHover>());
            CanvasGroup ghostCG = ghost.GetComponent<CanvasGroup>();
            if (!ghostCG) ghostCG = ghost.AddComponent<CanvasGroup>();
            ghostCG.blocksRaycasts = false;
            ghostCG.alpha = 1f;

            float timer = 0f;
            while (ghost != null && targetUI != null && timer < 0.5f)
            {
                timer += Time.deltaTime;
                Vector3 targetPos = targetUI.transform.position;

                if (Vector3.Distance(ghost.transform.position, targetPos) < 0.5f)
                    break;

                ghost.transform.position = Vector3.Lerp(ghost.transform.position, targetPos, Time.deltaTime * cardFlySpeed);
                yield return null;
            }

            if (ghost != null)
            {
                activeGhosts.Remove(ghost);
                Destroy(ghost);
            }

            targetCG.alpha = 1f;
            targetCG.blocksRaycasts = true;

            yield return new WaitForSeconds(dealDelay);
        }
    }

    public void AnimateCardFromHand(int index) { }

    private void HandleCardPlayed(int index)
    {
        if (index < 0 || index >= handContainer.childCount) return;
        Transform source = handContainer.GetChild(index);
        if (source == null) return;

        // Spawn a ghost outside the hand container so UpdateHandDisplay cleanup doesn't touch it
        GameObject ghost = Instantiate(cardUIPrefab, handContainer.parent);
        ghost.transform.position = source.position;
        ghost.transform.localScale = source.localScale;

        CardUI sourceUI = source.GetComponent<CardUI>();
        CardUI ghostUI = ghost.GetComponent<CardUI>();
        if (ghostUI != null && sourceUI != null)
            ghostUI.Setup(sourceUI.GetCard(), -1);

        Destroy(ghost.GetComponent<CardHover>());
        CanvasGroup cg = ghost.GetComponent<CanvasGroup>();
        if (cg == null) cg = ghost.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        if (popCoroutine != null) StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(CardPlayPopRoutine(ghost));
    }

    private IEnumerator CardPlayPopRoutine(GameObject ghost)
    {
        if (ghost == null) yield break;

        RectTransform rt = ghost.GetComponent<RectTransform>();
        CanvasGroup cg = ghost.GetComponent<CanvasGroup>();
        Vector3 startScale = ghost.transform.localScale;
        Vector2 startPos = rt.anchoredPosition;

        // Quick scale punch up
        float t = 0f;
        while (t < 0.07f)
        {
            if (ghost == null) yield break;
            t += Time.unscaledDeltaTime;
            ghost.transform.localScale = startScale * Mathf.Lerp(1f, 1.25f, t / 0.07f);
            yield return null;
        }

        // Fade out while drifting up
        t = 0f;
        while (t < 0.15f)
        {
            if (ghost == null) yield break;
            t += Time.unscaledDeltaTime;
            float p = t / 0.15f;
            ghost.transform.localScale = startScale * Mathf.Lerp(1.25f, 0.85f, p);
            if (cg != null) cg.alpha = Mathf.Lerp(1f, 0f, p);
            rt.anchoredPosition = startPos + Vector2.up * (40f * p);
            yield return null;
        }

        if (ghost != null) Destroy(ghost);
        popCoroutine = null;
    }
}