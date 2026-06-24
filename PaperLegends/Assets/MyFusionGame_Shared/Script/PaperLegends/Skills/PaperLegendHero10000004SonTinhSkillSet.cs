using System.Collections.Generic;

public sealed class PaperLegendHero10000004SonTinhSkillSet : IPaperLegendHeroSkillSet
{
    public const int HeroId = 10000004;

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
        return false;
    }

    public bool CanUpgradeSkill(PaperLegendCharacterNetworkHandler character, int slot, int currentSkillLevel)
    {
        return character != null && slot >= 1 && slot <= PaperLegendHeroSkillRegistry.MaxSkillSlots;
    }

    public bool TryUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel)
    {
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
            CreateSkill(1, PaperLegendHeroSkillId.Hero10000004ReservedSkill1, "Reserved Skill 1", "Reserved gameplay slot for hero 10000004."),
            CreateSkill(2, PaperLegendHeroSkillId.Hero10000004ReservedSkill2, "Reserved Skill 2", "Reserved gameplay slot for hero 10000004."),
            CreateSkill(3, PaperLegendHeroSkillId.Hero10000004ReservedSkill3, "Reserved Skill 3", "Reserved gameplay slot for hero 10000004."),
            CreateSkill(4, PaperLegendHeroSkillId.Hero10000004ReservedSkill4, "Reserved Skill 4", "Reserved gameplay slot for hero 10000004.")
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
