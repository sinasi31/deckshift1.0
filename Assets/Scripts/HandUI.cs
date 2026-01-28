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

    // Havada kalan hayaletleri takip etmek için liste
    private List<GameObject> activeGhosts = new List<GameObject>();

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
    }

    private void OnDisable()
    {
        DeckManager.OnHandChanged -= UpdateHandDisplay;
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
        StopAllCoroutines();

        // Hayalet Temizliği
        foreach (GameObject ghost in activeGhosts)
        {
            if (ghost != null) Destroy(ghost);
        }
        activeGhosts.Clear();

        // Kart Temizliği
        while (handContainer.childCount > 0)
        {
            DestroyImmediate(handContainer.GetChild(0).gameObject);
        }

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
            StartCoroutine(SafeAnimateRoutine(createdCards));
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
}