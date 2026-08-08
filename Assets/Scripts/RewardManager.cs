using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardManager : MonoBehaviour
{
    public static RewardManager instance;

    [Header("References")]
    public GameObject rewardScreen;

    // Hidden while the reward screen is up (visual clarity). Optional — auto-found by name if unassigned.
    [SerializeField] private GameObject gameplayHUD;
    private GameObject ResolveHUD()
    {
        if (gameplayHUD == null)
        {
            GameObject g = GameObject.Find("GameplayHUD");
            if (g != null) gameplayHUD = g;
        }
        return gameplayHUD;
    }

    // --- DE����KL�K: Art�k Button listesi de�il, CardUI listesi tutuyoruz ---
    public List<CardUI> rewardCardSlots;
    // --- B�T�� ---

    private List<CardData> offeredCards = new List<CardData>();
    private int bonusCardIndex = -1;
    private bool isShowing = false;

    // Procedural presentation layer (atmosphere, staggered reveal, bonus badge, selection burst).
    // Auto-added to the reward screen so it needs no Inspector wiring.
    private RewardScreenFX fx;
    private RewardScreenFX GetFX()
    {
        if (fx != null) return fx;
        if (rewardScreen == null) return null;
        fx = rewardScreen.GetComponent<RewardScreenFX>();
        if (fx == null) fx = rewardScreen.AddComponent<RewardScreenFX>();
        return fx;
    }

    private void Awake()
    {
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        if (rewardScreen != null) rewardScreen.SetActive(false);
    }

    public void ShowRewardScreen()
    {
        if (isShowing) return;
        isShowing = true;

        offeredCards.Clear();
        bonusCardIndex = -1;

        // ⚠️ Was AchievementManager.GetAvailableCardPool(), which returned only the cards listed in
        // `defaultUnlockedCards` plus the reward cards of COMPLETED challenges. With one challenge
        // authored, DeadWeight / FreefallBlade / GlassParry could never be offered by anything —
        // they existed and were simply not in the game, with nothing to indicate it. Achievement
        // gating on cards is removed (designer 2026-08-09); AchievementManager still tracks and
        // saves challenges, it just no longer decides what exists.
        List<CardData> cardPool = CardPool.Offerable();
        GameManager.instance.SetGameState(GameState.Paused);

        // 3 Kart Se�
        for (int i = 0; i < 3; i++)
        {
            if (cardPool.Count == 0) break;
            int randomIndex = Random.Range(0, cardPool.Count);
            offeredCards.Add(cardPool[randomIndex]);
            cardPool.RemoveAt(randomIndex);
        }

        // Bonus �ans�
        if (offeredCards.Count > 0)
            bonusCardIndex = Random.Range(0, offeredCards.Count);

        // Kartlar� UI'a yerle�tir
        for (int i = 0; i < rewardCardSlots.Count; i++)
        {
            if (i < offeredCards.Count)
            {
                CardUI slot = rewardCardSlots[i];
                slot.gameObject.SetActive(true);

                CardData data = offeredCards[i];

                // --- G�RSEL KURULUM (En �nemli K�s�m) ---
                // CardUI'�n kendi Setup fonksiyonunu kullan�yoruz!
                // G�rsel olmas� i�in ge�ici bir RuntimeCard olu�turuyoruz.
                RuntimeCard visualCard = new RuntimeCard(data);

                // Bonus varsa a��klamas�n� g�ncelle (Sadece g�rsel i�in)
                if (i == bonusCardIndex)
                {
                    // Not: Bu kal�c� veriyi de�i�tirmez, sadece visualCard'� etkiler
                    // (CardUI scriptinde descriptionText'i description'dan ald���m�z� varsayarsak)
                    // Ancak CardData ScriptableObject oldu�u i�in a��klamay� kodla de�i�tirmek riskli olabilir.
                    // �imdilik bonusu g�stermek i�in basit bir y�ntem:
                    Debug.Log($"Kart {i} BONUSLU (+1 Shift)");
                }

                // Setup'� �a��r (Bu; resmi, frame'i, daireleri her �eyi ayarlar!)
                slot.Setup(visualCard, i + 1);

                // --- BUTON TIKLAMA OLAYI ---
                Button btn = slot.GetComponent<Button>();
                if (btn != null)
                {
                    int cardIndex = i;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => SelectCard(cardIndex));
                }
            }
            else
            {
                // Yeterli kart yoksa slotu kapat
                rewardCardSlots[i].gameObject.SetActive(false);
            }
        }

        rewardScreen.SetActive(true);
        if (GameManager.instance != null) GameManager.instance.RequestPause();

        // Hide the gameplay HUD for a clean presentation; the reward screen's "View Deck" button
        // keeps the deck reachable. Lock the hand drawer so it can't slide up behind the screen.
        GameObject hud = ResolveHUD();
        if (hud != null) hud.SetActive(false);
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        // Play the entrance choreography (fade-in, dealt reveal, +1 SHIFT badge on the bonus card).
        RewardScreenFX intro = GetFX();
        if (intro != null) intro.PlayIntro(rewardCardSlots, offeredCards.Count, bonusCardIndex);
    }

    public void SelectCard(int cardIndex)
    {
        if (!isShowing) return;   // ignore extra clicks while the selection burst / fade-out plays
        isShowing = false;

        CardData selectedCard = offeredCards[cardIndex];
        DeckManager.instance.AddCardToDeck(selectedCard);

        if (cardIndex == bonusCardIndex)
        {
            GameManager.instance.player.AddShift(1);
        }

        // Card is granted immediately; the visual close (burst + fade-out) is deferred to the FX,
        // which fires FinishReward when the screen has faded away. Falls back to instant close.
        RewardScreenFX outro = GetFX();
        if (outro != null) outro.PlaySelect(cardIndex, FinishReward);
        else FinishReward();
    }

    private void FinishReward()
    {
        rewardScreen.SetActive(false);

        // Restore the HUD + hand drawer, and close the deck view if the player left it open.
        if (gameplayHUD != null) gameplayHUD.SetActive(true);
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);
        if (DeckViewUI.instance != null) DeckViewUI.instance.CloseView();

        if (GameManager.instance != null) GameManager.instance.ReleasePause();
        GameManager.instance.SetGameState(GameState.Playing);

        // The run map goes here, between taking the reward and the next room existing. If the
        // player already planned a branch with M, or the act offers only one way on, this is
        // skipped entirely and the room spawns as before — a forced screen with a single button is
        // ceremony, not a decision.
        if (RunMapManager.instance != null && RunMapManager.instance.NeedsRouteChoice)
            RunMapScreen.OpenForChoice(() => LevelManager.instance.SpawnNextRoom());
        else
            LevelManager.instance.SpawnNextRoom();
    }
}