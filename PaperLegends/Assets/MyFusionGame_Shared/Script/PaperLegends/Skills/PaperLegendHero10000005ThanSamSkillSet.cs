using System.Collections.Generic;
using UnityEngine;

public sealed class PaperLegendHero10000005ThanSamSkillSet : IPaperLegendHeroSkillSet
{
    public const int HeroId = 10000005;
    private const int UltimateSlot = 4;
    private const int UltimateMaxLevel = 3;

    public int HeroModelId => HeroId;

    public List<PaperLegendHeroSkillData> BuildSkillList(int heroLevel, IReadOnlyList<PaperLegendHeroSkillData> apiSkills)
    {
        return PaperLegendHeroSkillRegistry.BuildFixedSlotSkillList(
            apiSkills,
            CreateFallbackSkills(),
            PaperLegendHeroSkillRegistry.MaxSkillSlots);
    }

    public bool CanUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel)
    {
        return character != null && slot == UltimateSlot && skillLevel > 0;
    }

    public bool CanUpgradeSkill(PaperLegendCharacterNetworkHandler character, int slot, int currentSkillLevel)
    {
        if (character == null || slot < 1 || slot > PaperLegendHeroSkillRegistry.MaxSkillSlots)
            return false;

        if (slot == UltimateSlot)
            return currentSkillLevel < UltimateMaxLevel;

        return true;
    }

    public bool TryUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel)
    {
        if (character == null || slot != UltimateSlot)
            return false;

        bool casted = character.ServerTryCastHero10000005ThunderStorm(skillLevel);
        if (casted)
            Debug.Log($"[PaperLegends][Skill] Than Sam player={character.PlayerId} cast Thunder Storm level={skillLevel}.");

        return casted;
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
            CreateSkill(1, PaperLegendHeroSkillId.Hero10000005ReservedSkill1, "Reserved Skill 1", "Reserved gameplay slot for Than Sam."),
            CreateSkill(2, PaperLegendHeroSkillId.Hero10000005ReservedSkill2, "Reserved Skill 2", "Reserved gameplay slot for Than Sam."),
            CreateSkill(3, PaperLegendHeroSkillId.Hero10000005ReservedSkill3, "Reserved Skill 3", "Reserved gameplay slot for Than Sam."),
            CreateSkill(4, PaperLegendHeroSkillId.Hero10000005ThunderStorm, "Thunder Storm", "Select an area, channel for 1 second, then call lightning strikes for 4/5/6 seconds by skill level.")
        };
    }

    private static PaperLegendHeroSkillData CreateSkill(int slot, PaperLegendHeroSkillId skillId, string name, string description)
    {
        return new PaperLegendHeroSkillData
        {
            slot = slot,
            code = ((int)skillId).ToString(),
            name = name,
            description = description,
            isPassive = false,
            isActive = true
        };
    }
}
