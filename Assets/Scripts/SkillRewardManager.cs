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
            SkillType.Recycle,
            SkillType.VampiricAura,
            SkillType.KineticDiscount,
            SkillType.MaxShiftBonus,
            SkillType.InfinitySeal
        };

        // Zaten sahip olunan "Unique" skilleri havuzdan çýkar (MaxShift ve Infinity hariç, onlar birikebilir)
        if (SkillManager.instance.HasSkill(SkillType.Recycle)) pool.Remove(SkillType.Recycle);
        if (SkillManager.instance.HasSkill(SkillType.VampiricAura)) pool.Remove(SkillType.VampiricAura);
        if (SkillManager.instance.HasSkill(SkillType.KineticDiscount)) pool.Remove(SkillType.KineticDiscount);

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

                // Butonun içindeki Textleri bul ve yaz (Basit buton yapýsý varsayýyorum)
                // Eðer özel bir SkillUI prefabýn varsa CardUI gibi Setup yapabilirsin.
                // Þimdilik butonun altýndaki Text (TMP)'yi deðiþtiriyoruz:
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
            case SkillType.MaxShiftBonus:
                GameManager.instance.player.IncreaseMaxShift(1);
                break;

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
            case SkillType.Recycle: return "<b>RECYCLE</b>\n\n+1 Shift when you kill an enemy.";
            case SkillType.VampiricAura: return "<b>VAMPIRIC AURA</b>\n\nHeal +5 HP when you kill an enemy.";
            case SkillType.KineticDiscount: return "<b>KINETIC DISCOUNT</b>\n\nAll cards cost -1 Shift.";
            case SkillType.MaxShiftBonus: return "<b>HIGH VOLTAGE</b>\n\nIncrease Max Shift by +1.";
            case SkillType.InfinitySeal: return "<b>INFINITY SEAL</b>\n\nMake a random card in your hand INFINITE.";
            default: return "Unknown Skill";
        }
    }
}