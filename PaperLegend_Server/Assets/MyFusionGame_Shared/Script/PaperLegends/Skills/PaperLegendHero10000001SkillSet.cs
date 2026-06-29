using System.Collections.Generic;
using UnityEngine;

public sealed class PaperLegendHero10000001SkillSet : IPaperLegendHeroSkillSet
{
    public const int HeroId = 10000001;
    public const int Skill4MaxLevel = 3;

    public int HeroModelId => HeroId;

    public List<PaperLegendHeroSkillData> BuildSkillList(int heroLevel, IReadOnlyList<PaperLegendHeroSkillData> apiSkills)
    {
        return PaperLegendHeroSkillRegistry.BuildFixedSlotSkillList(apiSkills, CreateFallbackSkills(), PaperLegendHeroSkillRegistry.MaxSkillSlots);
    }

    public bool CanUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel)
    {
        if (character == null || skillLevel <= 0 || slot < 1 || slot > PaperLegendHeroSkillRegistry.MaxSkillSlots)
            return false;

        if (slot == 1)
            return false;

        return true;
    }

    public bool CanUpgradeSkill(PaperLegendCharacterNetworkHandler character, int slot, int currentSkillLevel)
    {
        if (character == null || slot < 1 || slot > PaperLegendHeroSkillRegistry.MaxSkillSlots)
            return false;

        if (slot == 4)
            return currentSkillLevel < Skill4MaxLevel;

        return true;
    }

    public bool TryUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel)
    {
        if (character == null)
            return false;

        switch (Mathf.Clamp(slot, 1, PaperLegendHeroSkillRegistry.MaxSkillSlots))
        {
            case 1:
                return false;

            case 2:
                character.ServerArmHero10000001PaperArrow();
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} armed hero 10000001 skill 2: paper arrow level={skillLevel}.");
                return true;

            case 3:
                character.ServerArmHero10000001FlickBoost();
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} armed hero 10000001 skill 3: flick boost level={skillLevel}.");
                return true;

            case 4:
                character.ServerArmHero10000001EdgeBounce();
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} armed hero 10000001 skill 4: edge bounce rebound level={skillLevel}, maxRebounds={Mathf.Clamp(skillLevel, 1, Skill4MaxLevel)}.");
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
                description = "Passive. The farther the paper hero travels before landing on a target, the higher the damage. Max x4.",
                isPassive = true,
                isActive = true
            },
            new PaperLegendHeroSkillData
            {
                slot = 2,
                code = ((int)PaperLegendHeroSkillId.Hero10000001PaperArrow).ToString(),
                name = "Paper Arrow",
                description = "After casting, the next swipe shoots a paper arrow forward. When it stops, it slows enemies in the area by 30% and deals light damage.",
                damage = 10f,
                damageLevel1 = 10f,
                damageLevel2 = 13f,
                damageLevel3 = 16f,
                damageLevel4 = 19f,
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
                code = ((int)PaperLegendHeroSkillId.Hero10000001EdgeBounceRebound).ToString(),
                name = "Lat Mep Nay Lai",
                description = "Next flick: each landing that does not consume all rebounds bounces again in the travel direction, even when pinning an enemy. Level 1-3 grants 1-3 extra bounces.",
                isActive = true
            }
        };
    }
}
