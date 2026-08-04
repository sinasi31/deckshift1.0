using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The card offer. Opened by a CardChest found in a level — this is now the ONLY way cards enter a
// deck outside the shop.
//
// WHY IT REPLACED THE REWARD SCREEN (designer, 2026-08-09): a screen that appeared after every room
// and demanded you take one of three cards is a toll, not a reward. It grew the deck whether or not
// you wanted it to, and a deck you didn't choose is a deck you don't reach for. Cards are now loot
// you find, and finding loot is optional by nature.
//
// ⚠️ SKIPPING PAYS NOTHING, ON PURPOSE. The obvious instinct is to hand out gold or scrap for
// declining, so the choice "feels rewarded" either way. The designer's reasoning is better: a small
// deck is stronger than a large one — every card you decline makes the cards you kept come up more
// often. Skipping is already the reward. Paying for it a second time would make taking a card the
// mistake.
//
// THE LOOK is deliberately the quietest screen in the game. Iron dresses a workbench, Arcane dresses
// a ritual, the Marketplace dresses a stall — each has a material because each is a PLACE. This is
// not a place, it is three objects on a dark surface, and the objects are full-colour painted card
// art. So the chrome gets out of the way: near-black ground, a warm pool of light welling up from
// the open chest below, brass hairlines, and no ornament competing with the cards.
public class CardChestScreen : MonoBehaviour
{
    private const int OFFER_COUNT = 3;

    // How far the visible card sits above its own RectTransform — see the note in Populate.
    private const float CARD_VISUAL_OFFSET_Y = -320f;
    // Comfortably wider than the 200px card plus the 1.2x hover-flip zoom, so a card turning over
    // never touches its neighbours.
    private const float CARD_SPACING = 300f;

    private static readonly Color GROUND = new Color(0.045f, 0.038f, 0.034f, 0.965f);
    private static readonly Color BRASS = new Color(0.72f, 0.58f, 0.31f, 1f);
    private static readonly Color BRASS_DIM = new Color(0.72f, 0.58f, 0.31f, 0.34f);
    private static readonly Color TEXT_BRIGHT = new Color(0.95f, 0.93f, 0.89f, 1f);
    private static readonly Color TEXT_MUTED = new Color(0.56f, 0.52f, 0.46f, 1f);

    private static CardChestScreen instance;

    private CanvasGroup group;
    private RectTransform row;
    private readonly List<GameObject> spawnedCards = new List<GameObject>();
    private System.Action onClosed;
    private bool isOpen;

    // Opens an offer. `onClosed` runs whether the player takes a card or skips — the chest uses it
    // to mark itself spent either way, because a chest you can reopen until you like the offer is
    // not a choice.
    public static void Open(System.Action onClosed)
    {
        if (instance == null) instance = Build();
        if (instance == null)
        {
            // No Canvas: never strand the player in a paused state with no screen.
            Debug.LogWarning("CardChestScreen: no Canvas found, offer skipped.");
            onClosed?.Invoke();
            return;
        }
        if (instance.isOpen) return;

        instance.onClosed = onClosed;
        instance.Show();
    }

    private static CardChestScreen Build()
    {
        Canvas canvas = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
        if (canvas == null) return null;

        GameObject go = new GameObject("CardChestScreen", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        Stretch(rt);

        CardChestScreen screen = go.AddComponent<CardChestScreen>();
        screen.BuildUI(rt);
        go.SetActive(false);
        return screen;
    }

    private void BuildUI(RectTransform root)
    {
        group = gameObject.AddComponent<CanvasGroup>();

        // Backdrop. Raycast-blocking so nothing behind the screen can be clicked.
        Image bg = AddImage(root, "Ground", null, GROUND);
        Stretch(bg.rectTransform);
        bg.raycastTarget = true;

        // The chest's own light, welling up from the bottom centre. This is the one piece of
        // atmosphere: it says the cards are lying in something open, without drawing a box.
        Image pool = AddImage(root, "ChestLight", FlatUI.SoftGlow(), new Color(0.85f, 0.62f, 0.28f, 0.11f));
        RectTransform prt = pool.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0f);
        prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1500f, 900f);
        prt.anchoredPosition = new Vector2(0f, 120f);

        // Slow dust turning in the light. Not embers — nothing here is burning.
        UIEmberField.Attach(root, 26, new Color(0.85f, 0.72f, 0.48f, 0.5f), UIEmberField.Settings.Dust);

        TextMeshProUGUI title = AddText(root, "Title", "A CARD FOR THE DECK", TEXT_BRIGHT, 38f);
        title.characterSpacing = 10f;
        Anchor(title.rectTransform, 0.5f, 0.855f, 900f, 60f);

        TextMeshProUGUI sub = AddText(root, "Sub", "Take one, or leave them. A smaller deck draws better.", TEXT_MUTED, 21f);
        Anchor(sub.rectTransform, 0.5f, 0.805f, 1100f, 40f);

        Image rule = AddImage(root, "Rule", FlatUI.FadedRule(), BRASS_DIM);
        Anchor(rule.rectTransform, 0.5f, 0.775f, 760f, 2f);

        // The cards sit in a plain row; CardUI brings its own size, hover-flip and readable back.
        GameObject rowGO = new GameObject("Row", typeof(RectTransform));
        row = rowGO.GetComponent<RectTransform>();
        row.SetParent(root, false);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(1200f, 340f);
        row.anchoredPosition = new Vector2(0f, 40f);

        // ⚠️ NO LAYOUT GROUP HERE, DELIBERATELY. The cards need a manual Y correction (see Populate)
        // and a layout group would overwrite anchoredPosition on its next rebuild, silently undoing
        // it — the offer would look right until the first time anything dirtied the layout.

        BuildSkipButton(root);
    }

    private void BuildSkipButton(RectTransform root)
    {
        GameObject go = new GameObject("Skip", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        Anchor(rt, 0.5f, 0.145f, 300f, 62f);

        Image plate = go.AddComponent<Image>();
        plate.sprite = FlatUI.Panel(5);
        plate.type = Image.Type.Sliced;
        plate.color = new Color(0.11f, 0.096f, 0.084f, 1f);

        Image frame = AddImage(rt, "Frame", FlatUI.Outline(5, 1), BRASS_DIM);
        frame.type = Image.Type.Sliced;
        Stretch(frame.rectTransform);

        // Deliberately understated: skipping is a legitimate, often correct choice, but the screen
        // should not SELL it. No glow, no payout, no "+N" — just a way out.
        TextMeshProUGUI label = AddText(rt, "Label", "LEAVE THEM", TEXT_MUTED, 24f);
        label.characterSpacing = 6f;
        Stretch(label.rectTransform);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = plate;
        btn.onClick.AddListener(() => Close());
    }

    private void Show()
    {
        isOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        Populate();

        if (GameManager.instance != null) GameManager.instance.RequestPause();

        // Same housekeeping the reward screen used to do: the hand drawer would otherwise slide up
        // behind the offer and absorb clicks meant for the cards.
        GameObject hud = GameObject.Find("GameplayHUD");
        if (hud != null) hud.SetActive(false);
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);
    }

    private void Populate()
    {
        foreach (GameObject go in spawnedCards) if (go != null) Destroy(go);
        spawnedCards.Clear();

        GameObject prefab = ResolveCardPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("CardChestScreen: no CardUI prefab available, nothing to offer.");
            return;
        }

        // CardPool excludes Stagger and is rebuilt from the assets, so a newly authored card is
        // offerable the moment it exists. Duplicates across offers are prevented; duplicates with
        // cards already in the deck are FINE — a second Fireball is a real build choice.
        List<CardData> pool = CardPool.Offerable();
        int count = Mathf.Min(OFFER_COUNT, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(0, pool.Count);
            CardData data = pool[pick];
            pool.RemoveAt(pick);

            GameObject cardGO = Instantiate(prefab, row);
            cardGO.transform.localScale = Vector3.one;

            // ⚠️ THE CARD PREFAB DOES NOT DRAW WHERE ITS RECT IS. Its root is a 200x100 stub with a
            // bottom pivot, while CardArt is a 200x300 child centred on that stub — so the visible
            // card sits 320px ABOVE its own rect. Measured, not guessed: dropped straight into a
            // centred row the cards landed at the top of the screen, on top of the title. The hand
            // doesn't hit this because its drawer is positioned to compensate; any NEW screen laying
            // these out has to correct for it.
            RectTransform crt = (RectTransform)cardGO.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * CARD_SPACING,
                                               CARD_VISUAL_OFFSET_Y);

            spawnedCards.Add(cardGO);

            CardUI ui = cardGO.GetComponent<CardUI>();
            if (ui != null) ui.Setup(new RuntimeCard(data), -1);   // -1: no [n] key hint, it isn't in hand

            Button btn = cardGO.GetComponent<Button>();
            if (btn != null)
            {
                CardData taken = data;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => Take(taken));
            }
        }
    }

    // ⚠️ Borrowed from whatever scene object already owns it rather than serialized here. This
    // screen self-instantiates and has no Inspector, and "the system exists in code but isn't in the
    // scene" is this project's most recurrent bug — a field to wire is a field to forget.
    private static GameObject ResolveCardPrefab()
    {
        HandUI hand = Object.FindFirstObjectByType<HandUI>(FindObjectsInactive.Include);
        if (hand != null && hand.cardUIPrefab != null) return hand.cardUIPrefab;

        DeckViewUI deck = Object.FindFirstObjectByType<DeckViewUI>(FindObjectsInactive.Include);
        if (deck != null && deck.cardUIPrefab != null) return deck.cardUIPrefab;

        return null;
    }

    private void Take(CardData data)
    {
        if (!isOpen) return;
        if (data != null && DeckManager.instance != null)
            DeckManager.instance.AddCardToDeck(data);
        Close();
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        foreach (GameObject go in spawnedCards) if (go != null) Destroy(go);
        spawnedCards.Clear();

        if (GameManager.instance != null) GameManager.instance.ReleasePause();

        GameObject hud = GameObject.Find("GameplayHUD");
        if (hud == null)
        {
            // GameplayHUD was switched OFF, so Find can't see it — reach it through the Canvas.
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform t = c.transform.Find("GameplayHUD");
                if (t != null) { hud = t.gameObject; break; }
            }
        }
        if (hud != null) hud.SetActive(true);
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);

        gameObject.SetActive(false);

        System.Action done = onClosed;
        onClosed = null;
        done?.Invoke();
    }

    // --- construction helpers -------------------------------------------------------------------

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(x, y);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
    }

    private static Image AddImage(RectTransform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite != null ? sprite : FlatUI.Pixel();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI AddText(RectTransform parent, string name, string text, Color color, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = FlatUI.UIFont();
        if (f != null) t.font = f;
        t.text = text;
        t.color = color;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }
}
