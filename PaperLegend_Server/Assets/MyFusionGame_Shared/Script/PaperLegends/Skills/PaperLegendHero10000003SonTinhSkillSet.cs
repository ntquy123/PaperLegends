using System.Collections.Generic;
using UnityEngine;

public sealed class PaperLegendHero10000003SonTinhSkillSet : IPaperLegendHeroSkillSet
{
    public const int HeroId = 10000003;

    private const float DirectionInputTimeoutSeconds = 5f;

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

        return slot == 1 || slot == 2;
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
                character.ServerArmHero10000003WaterBurst(skillLevel, DirectionInputTimeoutSeconds);
                Debug.Log($"[PaperLegends][Skill] Son Tinh player={character.PlayerId} armed Water Burst level={skillLevel}. Waiting for next swipe direction.");
                return true;

            case 2:
                character.ServerArmHero10000003WavePush(skillLevel, DirectionInputTimeoutSeconds);
                Debug.Log($"[PaperLegends][Skill] Son Tinh player={character.PlayerId} armed Wave Push level={skillLevel}. Waiting for next flick input direction.");
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
            CreateSkill(1, PaperLegendHeroSkillId.Hero10000003WaterBurst, "Water Burst", "After casting, swipe a direction to erupt 3 water bursts in a line. The second and third bursts deal increased damage.", 10f, 7f),
            CreateSkill(2, PaperLegendHeroSkillId.Hero10000003WavePush, "Wave Push", "After casting, the next flick direction releases a wave that pushes paper heroes hit by the wave.", 0f, 8f),
            CreateSkill(3, PaperLegendHeroSkillId.Hero10000003ReservedSkill3, "Reserved Skill 3", "Reserved Son Tinh skill slot."),
            CreateSkill(4, PaperLegendHeroSkillId.Hero10000003ReservedSkill4, "Reserved Skill 4", "Reserved Son Tinh skill slot.")
        };
    }

    private static PaperLegendHeroSkillData CreateSkill(int slot, PaperLegendHeroSkillId skillId, string name, string description, float damage = 0f, float cooldown = 0f)
    {
        return new PaperLegendHeroSkillData
        {
            slot = slot,
            code = ((int)skillId).ToString(),
            name = name,
            description = description,
            cooldown = cooldown,
            damage = damage,
            damageLevel1 = damage,
            damageLevel2 = damage > 0f ? damage + 3f : 0f,
            damageLevel3 = damage > 0f ? damage + 6f : 0f,
            damageLevel4 = damage > 0f ? damage + 9f : 0f,
            isPassive = false,
            isActive = true
        };
    }
}
