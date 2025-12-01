using System.Collections.Generic;
using UnityEngine;

public class SkillManager : MonoBehaviour
{
    public static SkillManager instance;

    // Oyuncunun sahip olduðu aktif skillerin listesi
    private List<SkillType> unlockedSkills = new List<SkillType>();

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    // Yeni bir skill öðrenildiðinde çaðrýlýr
    public void UnlockSkill(SkillType skill)
    {
        if (!unlockedSkills.Contains(skill))
        {
            unlockedSkills.Add(skill);
            Debug.Log($"YENÝ SKILL ÖÐRENÝLDÝ: {skill}");
            // TODO: Ýstersen burada ekrana "Ancient Knowledge Unlocked!" yazýsý çýkarabilirsin.
        }
    }

    // Diðer scriptler "Oyuncuda bu skill var mý?" diye sormak için bunu kullanacak
    public bool HasSkill(SkillType skill)
    {
        return unlockedSkills.Contains(skill);
    }
}