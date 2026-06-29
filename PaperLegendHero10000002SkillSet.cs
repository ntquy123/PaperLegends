using System.Collections.Generic;
using UnityEngine;

public sealed class PaperLegendHero10000002SkillSet : IPaperLegendHeroSkillSet
{
    public const int HeroId = 10000002;

    public int HeroModelId => HeroId;

    public List<PaperLegendHeroSkillData> BuildSkillList(int heroLevel, IReadOnlyList<PaperLegendHeroSkillData> apiSkills)
    {
        return PaperLegendHeroSkillRegistry.BuildFixedSlotSkillList(apiSkills, CreateFallbackSkills(), PaperLegendHeroSkillRegistry.MaxSkillSlots);
    }

    public bool CanUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel)
    {
        return character != null && skillLevel > 0 && slot >= 1 && slot <= PaperLegendHeroSkillRegistry.MaxSkillSlots;
    }

    public bool CanUpgradeSkill(PaperLegendCharacterNetworkHandler character, int slot, int currentSkillLevel)
    {
        return character != null && slot >= 1 && slot <= PaperLegendHeroSkillRegistry.MaxSkillSlots;
    }

    public bool TryUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel)
    {
        if (character == null)
            return false;

        switch (Mathf.Clamp(slot, 1, PaperLegendHeroSkillRegistry.MaxSkillSlots))
        {
            case 1:
                character.ServerArmHero10000002ForwardSlide();
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} armed hero 10000002 skill 1: forward slide level={skillLevel}.");
                return true;

            case 2:
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} used hero 10000002 skill 2 placeholder level={skillLevel}.");
                return true;

            case 3:
                character.ServerArmHero10000002ShoveStun(skillLevel);
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} armed hero 10000002 skill 3: shove stun level={skillLevel}.");
                return true;

            case 4:
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} used hero 10000002 skill {slot} placeholder level={skillLevel}.");
                return true;
        }

        return false;
    }

    public int ModifyExperienceReward(PaperLegendCharacterNetworkHandler character, int amount, PaperLegendExperienceSource source)
    {
        return amount;
    }

    public void OnHeroConfigured(PaperLegendCharacterNetworkHandler character)
    {
    }

    public void OnHeroLevelChanged(PaperLegendCharacterNetworkHandler character, int oldLevel, int newLevel)
    {
    }

    private static List<PaperLegendHeroSkillData> CreateFallbackSkills()
    {
        return new List<PaperLegendHeroSkillData>
        {
            new PaperLegendHeroSkillData
            {
                slot = 1,
                code = ((int)PaperLegendHeroSkillId.Hero10000002ForwardSlide).ToString(),
                name = "skill_11400021_name",
                description = "skill_11400021_description",
                isActive = true
            },
            new PaperLegendHeroSkillData
            {
                slot = 2,
                code = ((int)PaperLegendHeroSkillId.Hero10000002ReservedSkill2).ToString(),
                name = "skill_11400022_name",
                description = "skill_11400022_description",
                isActive = true
            },
            new PaperLegendHeroSkillData
            {
                slot = 3,
                code = ((int)PaperLegendHeroSkillId.Hero10000002ShoveStun).ToString(),
                name = "skill_11400023_name",
                description = "skill_11400023_description",
                isActive = true
            },
            new PaperLegendHeroSkillData
            {
                slot = 4,
                code = ((int)PaperLegendHeroSkillId.Hero10000002ReservedSkill4).ToString(),
                name = "skill_11400024_name",
                description = "skill_11400024_description",
                isActive = true
            }
        };
    }
}
