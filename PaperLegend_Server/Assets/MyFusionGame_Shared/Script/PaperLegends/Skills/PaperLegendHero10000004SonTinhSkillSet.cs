using System.Collections.Generic;
using UnityEngine;

public sealed class PaperLegendHero10000004SonTinhSkillSet : IPaperLegendHeroSkillSet
{
    public const int HeroId = 10000004;
    public const float LandingPinCritChance = 0.25f;

    private static readonly float[] PaperSpeedMultiplierByLevel = { 0f, 1.5f, 2f, 2.5f, 3f };
    private static readonly float[] LandingPinCritMultiplierByLevel = { 0f, 2f, 3f, 4f, 4f };
    private static readonly float[] PinDodgeChanceByLevel = { 0f, 0.15f, 0.20f, 0.25f, 0.30f };

    public int HeroModelId => HeroId;

    public static float ResolvePaperSpeedMultiplier(int skillLevel)
    {
        int index = Mathf.Clamp(skillLevel, 0, PaperSpeedMultiplierByLevel.Length - 1);
        return PaperSpeedMultiplierByLevel[index];
    }

    public static float ResolveLandingPinCritMultiplier(int skillLevel)
    {
        int index = Mathf.Clamp(skillLevel, 0, LandingPinCritMultiplierByLevel.Length - 1);
        return LandingPinCritMultiplierByLevel[index];
    }

    public static float ResolvePinDodgeChance(int skillLevel)
    {
        int index = Mathf.Clamp(skillLevel, 0, PinDodgeChanceByLevel.Length - 1);
        return PinDodgeChanceByLevel[index];
    }

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

        if (slot == 3 || slot == 4)
            return false;

        return slot >= 1 && slot <= PaperLegendHeroSkillRegistry.MaxSkillSlots;
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
                character.ServerArmHero10000004HomingSword(skillLevel);
                Debug.Log($"[PaperLegends][Skill] Hero 10000004 player={character.PlayerId} armed Homing Sword level={skillLevel}. Swipe on the ground to launch.");
                return true;

            case 2:
                if (character.TryGetPendingSkillTargetPlayerId(out _))
                    return character.ServerTryCastHero10000004RushPaperSpeed(skillLevel);

                character.ServerArmHero10000004RushPaperSpeed(skillLevel);
                Debug.Log($"[PaperLegends][Skill] Hero 10000004 player={character.PlayerId} armed Rush Paper Speed level={skillLevel}. Tap an enemy to dash.");
                return true;

            case 3:
            case 4:
                return false;
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
            CreateSkill(1, PaperLegendHeroSkillId.Hero10000004ReservedSkill1, "Homing Sword", "Tap to arm, then swipe on the ground to launch a sword. When it lands, it scans a small area and strikes the nearest enemy; otherwise it disappears.", 8f, 7f),
            CreateSkill(2, PaperLegendHeroSkillId.Hero10000004ReservedSkill2, "Rush Paper Speed", "Tap an enemy to instantly dash near them from any distance. For 5 seconds your paper flick becomes much faster to fly and fall, up to x3 speed at max level. No damage.", 0f, 9f),
            CreatePassiveSkill(3, PaperLegendHeroSkillId.Hero10000004ReservedSkill3, "Pin Dodge", "Passive. While an enemy is pinning you, each damage tick has a 15% to 30% chance to dodge, quickly sliding just far enough to escape the pin without losing health."),
            CreatePassiveSkill(4, PaperLegendHeroSkillId.Hero10000004ReservedSkill4, "Pin Critical Strike", "Passive. While pinning an enemy, each damage burst has a 25% chance to critically strike for x2, x3, or x4 attack damage by skill level. Crits show orange damage numbers.")
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
            damageLevel2 = damage > 0f ? damage + 2f : 0f,
            damageLevel3 = damage > 0f ? damage + 4f : 0f,
            damageLevel4 = damage > 0f ? damage + 6f : 0f,
            isPassive = false,
            isActive = true
        };
    }

    private static PaperLegendHeroSkillData CreatePassiveSkill(int slot, PaperLegendHeroSkillId skillId, string name, string description)
    {
        return new PaperLegendHeroSkillData
        {
            slot = slot,
            code = ((int)skillId).ToString(),
            name = name,
            description = description,
            cooldown = 0f,
            damage = 0f,
            isPassive = true,
            isActive = true
        };
    }
}
