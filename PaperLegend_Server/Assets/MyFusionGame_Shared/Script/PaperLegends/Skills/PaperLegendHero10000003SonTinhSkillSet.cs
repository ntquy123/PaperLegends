using System.Collections.Generic;
using UnityEngine;

public sealed class PaperLegendHero10000003SonTinhSkillSet : IPaperLegendHeroSkillSet
{
    public const int HeroId = 10000003;

    private const float WaveDirectionInputTimeoutSeconds = 5f;

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
        if (character == null || skillLevel <= 0)
            return false;

        // Only Song Day has gameplay for now. Slots 2-4 are configured placeholders.
        return slot == 1;
    }

    public bool CanUpgradeSkill(PaperLegendCharacterNetworkHandler character, int slot, int currentSkillLevel)
    {
        return character != null && slot >= 1 && slot <= PaperLegendHeroSkillRegistry.MaxSkillSlots;
    }

    public bool TryUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel)
    {
        if (character == null || slot != 1)
            return false;

        character.ServerArmHero10000003WavePush(skillLevel, WaveDirectionInputTimeoutSeconds);
        Debug.Log($"[PaperLegends][Skill] Son Tinh player={character.PlayerId} armed Song Day level={skillLevel}. Waiting for next flick input direction.");
        return true;
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
            CreateSkill(1, PaperLegendHeroSkillId.Hero10000003WavePush, "Song Day", "After casting, the next flick direction releases a wave that pushes paper heroes hit by the wave."),
            CreateSkill(2, PaperLegendHeroSkillId.Hero10000003ReservedSkill2, "Reserved Skill 2", "Reserved Son Tinh skill slot."),
            CreateSkill(3, PaperLegendHeroSkillId.Hero10000003ReservedSkill3, "Reserved Skill 3", "Reserved Son Tinh skill slot."),
            CreateSkill(4, PaperLegendHeroSkillId.Hero10000003ReservedSkill4, "Reserved Skill 4", "Reserved Son Tinh skill slot.")
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
