using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SkillRewardManager : MonoBehaviour
{
    public static SkillRewardManager instance;

    [Header("UI Referanslarý")]
    public GameObject skillRewardPanel; // Panel objesi
    public List<Button> skillButtons;   // 3 adet buton

    // Rastgele seçim için havuz
    private List<SkillType> offeredSkills = new List<SkillType>();

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void Start()
    {
        if (skillRewardPanel != null) skillRewardPanel.SetActive(false);
    }

    public void OpenSkillSelection()
    {
        // 1. Oyunu Durdur
        GameManager.instance.SetGameState(GameState.Paused);
        Time.timeScale = 0f;
        skillRewardPanel.SetActive(true);

        // 2. Rastgele 3 Farklý Skill Seç
        offeredSkills.Clear();
        List<SkillType> pool = new List<SkillType>()
        {
            SkillType.InfinitySeal
        };

        // Havuzdan rastgele 3 tane çek
        for (int i = 0; i < 3; i++)
        {
            if (pool.Count == 0) break;
            int r = Random.Range(0, pool.Count);
            offeredSkills.Add(pool[r]);
            pool.RemoveAt(r);
        }

        // 3. Butonlarý Doldur
        for (int i = 0; i < skillButtons.Count; i++)
        {
            if (i < offeredSkills.Count)
            {
                skillButtons[i].gameObject.SetActive(true);
                SkillType skill = offeredSkills[i];

                TextMeshProUGUI btnText = skillButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = GetSkillDescription(skill);
                }

                int index = i;
                skillButtons[i].onClick.RemoveAllListeners();
                skillButtons[i].onClick.AddListener(() => SelectSkill(index));
            }
            else
            {
                skillButtons[i].gameObject.SetActive(false);
            }
        }
    }

    public void SelectSkill(int index)
    {
        SkillType selected = offeredSkills[index];
        ApplySkillEffect(selected);

        // Ekraný Kapat ve Devam Et
        skillRewardPanel.SetActive(false);
        Time.timeScale = 1f;
        GameManager.instance.SetGameState(GameState.Playing);
    }

    private void ApplySkillEffect(SkillType type)
    {
        Debug.Log($"SEÇÝLEN ÖDÜL: {type}");

        switch (type)
        {

            case SkillType.InfinitySeal:
                // Eldeki rastgele bir kartý sonsuz yap
                List<RuntimeCard> hand = DeckManager.instance.GetCurrentHand();
                if (hand.Count > 0)
                {
                    int r = Random.Range(0, hand.Count);
                    hand[r].isInfinite = true;
                    // UI Güncellemesini tetiklemek için el deðiþikliði bildirimi
                    // (DeckManager.cs içinde public static Action OnHandChanged var)
                    // Ancak dýþarýdan tetikleyemeyiz, basitçe log atalým, oyuncu kart oynayýnca düzelir.
                    Debug.Log("Bir kart SONSUZ oldu!");
                }
                else
                {
                    // Elde kart yoksa boþa gitmesin
                    GameManager.instance.player.IncreaseMaxShift(1);
                }
                break;

            default:
                // Diðerleri pasif skilldir, Manager'a kaydet
                SkillManager.instance.UnlockSkill(type);
                break;
        }
    }

    private string GetSkillDescription(SkillType type)
    {
        switch (type)
        {
            case SkillType.InfinitySeal: return "<b>INFINITY SEAL</b>\n\nMake a random card in your hand INFINITE.";
            case SkillType.EchoChamber: return "<b>ECHO CHAMBER</b>\n\nCards have a 50% chance to cast <color=purple>TWICE</color>.";
            case SkillType.SpectralWings: return "<b>SPECTRAL WINGS</b>\n\nGrants an extra Mid-Air Jump that costs <color=green>0 SHIFT</color>.";
            case SkillType.Overclock: return "<b>OVERCLOCK</b>\n\nKilling an enemy makes your next card cost <color=yellow>0 SHIFT</color>.";
            default: return "Unknown Skill";
        }
    }
}