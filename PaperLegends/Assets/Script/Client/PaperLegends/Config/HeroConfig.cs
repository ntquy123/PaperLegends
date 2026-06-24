using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Paper Legends/Hero Config", fileName = "HeroConfig")]
public sealed class HeroConfig : ScriptableObject
{
    public string heroId;
    public string heroName;
    public string nameKey;

    [Header("Addressables")]
    public AssetReferenceSprite avatarIconRef;
    public AssetReferenceSprite avatarFullRef;
    public AssetReferenceT<Material> materialRef;
    public AssetReferenceGameObject prefabRef;

    public HeroAudioConfig audioConfig;
    public HeroVfxConfig vfxConfig;
    public SkillConfig[] skills = Array.Empty<SkillConfig>();

    public int ResolveHeroIdInt()
    {
        return int.TryParse(heroId, out int value) ? value : 0;
    }

    public SkillConfig GetSkillById(int skillId)
    {
        if (skills == null || skillId <= 0)
            return null;

        for (int i = 0; i < skills.Length; i++)
        {
            SkillConfig skill = skills[i];
            if (skill != null && skill.skillId == skillId)
                return skill;
        }

        return null;
    }

    public SkillConfig GetSkillBySlot(int slot)
    {
        if (skills == null)
            return null;

        for (int i = 0; i < skills.Length; i++)
        {
            SkillConfig skill = skills[i];
            if (skill != null && skill.slot == slot)
                return skill;
        }

        return null;
    }
}
