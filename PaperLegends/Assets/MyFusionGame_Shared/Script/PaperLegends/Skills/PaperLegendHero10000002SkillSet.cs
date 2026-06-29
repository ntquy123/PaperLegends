using System.Collections.Generic;
using UnityEngine;

public sealed class PaperLegendHero10000002SkillSet : IPaperLegendHeroSkillSet
{
    public const int HeroId = 10000002;

    private static readonly float[] FlickSpeedBonusByLevel = { 0f, 0.10f, 0.15f, 0.20f, 0.25f };
    private static readonly float[] LastStandCooldownByLevel = { 0f, 60f, 50f, 40f, 30f };

    public int HeroModelId => HeroId;

    public static float ResolveLastStandCooldownSeconds(int skillLevel)
    {
        int index = Mathf.Clamp(skillLevel, 0, LastStandCooldownByLevel.Length - 1);
        return LastStandCooldownByLevel[index];
    }

    public static float ResolveFlickSpeedBonus(int skillLevel)
    {
        int index = Mathf.Clamp(skillLevel, 0, FlickSpeedBonusByLevel.Length - 1);
        return FlickSpeedBonusByLevel[index];
    }

    public List<PaperLegendHeroSkillData> BuildSkillList(int heroLevel, IReadOnlyList<PaperLegendHeroSkillData> apiSkills)
    {
        return PaperLegendHeroSkillRegistry.BuildFixedSlotSkillList(apiSkills, CreateFallbackSkills(), PaperLegendHeroSkillRegistry.MaxSkillSlots);
    }

    public bool CanUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel)
    {
        if (character == null || skillLevel <= 0 || slot < 1 || slot > PaperLegendHeroSkillRegistry.MaxSkillSlots)
            return false;

        if (slot == 3)
            return false;

        if (slot == 4)
            return character.CanActivateHero10000002LastStand();

        return true;
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
                character.ServerArmHero10000002ShoveStun(skillLevel);
                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} armed hero 10000002 skill 2: shove stun level={skillLevel}.");
                return true;

            case 3:
                return false;

            case 4:
                if (!character.ServerTryActivateHero10000002LastStand(manualTrigger: true))
                    return false;

                Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} activated hero 10000002 skill 4: last stand level={skillLevel}.");
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
                code = ((int)PaperLegendHeroSkillId.Hero10000002ShoveStun).ToString(),
                name = "skill_11400022_name",
                description = "skill_11400022_description",
                isActive = true
            },
            new PaperLegendHeroSkillData
            {
                slot = 3,
                code = ((int)PaperLegendHeroSkillId.Hero10000002FlickSpeedBoost).ToString(),
                name = "skill_11400023_name",
                description = "skill_11400023_description",
                isPassive = true,
                isActive = true
            },
            new PaperLegendHeroSkillData
            {
                slot = 4,
                code = ((int)PaperLegendHeroSkillId.Hero10000002LastStand).ToString(),
                name = "skill_11400024_name",
                description = "skill_11400024_description",
                cooldown = 60f,
                isActive = true
            }
        };
    }
}
