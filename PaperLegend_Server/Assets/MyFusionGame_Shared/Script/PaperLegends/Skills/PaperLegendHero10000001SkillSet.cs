using System.Collections.Generic;
using UnityEngine;

public sealed class PaperLegendHero10000001SkillSet : IPaperLegendHeroSkillSet
{
    public const int HeroId = 10000001;

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
                character.ServerArmHero10000001DistanceDamage();
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} armed hero 10000001 skill 1: distance landing damage level={skillLevel}.");
                return true;

            case 2:
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} used hero 10000001 skill 2 placeholder level={skillLevel}.");
                return true;

            case 3:
                character.ServerArmHero10000001FlickBoost();
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} armed hero 10000001 skill 3: flick boost level={skillLevel}.");
                return true;

            case 4:
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} used hero 10000001 skill 4 placeholder level={skillLevel}.");
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
                code = ((int)PaperLegendHeroSkillId.Hero10000001DistanceLandingDamage).ToString(),
                name = "Distance Landing Damage",
                description = "The farther the paper hero travels before landing on a target, the higher the damage. Max x4.",
                isActive = true
            },
            new PaperLegendHeroSkillData
            {
                slot = 2,
                code = ((int)PaperLegendHeroSkillId.Hero10000001ReservedSkill2).ToString(),
                name = "Reserved Skill 2",
                description = "Placeholder for hero 10000001 skill 2.",
                isActive = true
            },
            new PaperLegendHeroSkillData
            {
                slot = 3,
                code = ((int)PaperLegendHeroSkillId.Hero10000001FlickForceBoost).ToString(),
                name = "Flick Force Boost",
                description = "Boosts the next flick force and lets the camera look farther.",
                isActive = true
            },
            new PaperLegendHeroSkillData
            {
                slot = 4,
                code = ((int)PaperLegendHeroSkillId.Hero10000001ReservedSkill4).ToString(),
                name = "Reserved Skill 4",
                description = "Placeholder for hero 10000001 skill 4.",
                isActive = true
            }
        };
    }
}
