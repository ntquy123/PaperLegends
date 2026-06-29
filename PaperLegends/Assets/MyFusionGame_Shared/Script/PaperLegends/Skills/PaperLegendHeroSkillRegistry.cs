using System.Collections.Generic;
using UnityEngine;

public static class PaperLegendHeroSkillRegistry
{
    public const int MaxSkillSlots = 4;

    private static readonly IPaperLegendHeroSkillSet[] SkillSets =
    {
        new PaperLegendHero10000001SkillSet(),
        new PaperLegendHero10000002SkillSet(),
        new PaperLegendHero10000003SonTinhSkillSet(),
        new PaperLegendHero10000004SonTinhSkillSet(),
        new PaperLegendHero10000005ThanSamSkillSet()
    };

    private static readonly Dictionary<int, IPaperLegendHeroSkillSet> SkillSetByHeroId = CreateLookup();

    public static bool TryGetSkillSet(int heroModelId, out IPaperLegendHeroSkillSet skillSet)
    {
        return SkillSetByHeroId.TryGetValue(heroModelId, out skillSet);
    }

    public static List<PaperLegendHeroSkillData> BuildSkillList(int heroModelId, int heroLevel, IReadOnlyList<PaperLegendHeroSkillData> apiSkills)
    {
        if (TryGetSkillSet(heroModelId, out IPaperLegendHeroSkillSet skillSet))
            return skillSet.BuildSkillList(heroLevel, apiSkills);

        return BuildFixedSlotSkillList(apiSkills, null, MaxSkillSlots);
    }

    public static bool CanUseSkill(PaperLegendCharacterNetworkHandler character, int slot)
    {
        if (character == null)
            return false;

        if (!TryGetSkillSet(character.CharacterModelId, out IPaperLegendHeroSkillSet skillSet))
            return false;

        slot = Mathf.Clamp(slot, 1, MaxSkillSlots);
        return skillSet.CanUseSkill(character, slot, character.GetSkillLevel(slot));
    }

    public static bool CanUpgradeSkill(PaperLegendCharacterNetworkHandler character, int slot)
    {
        if (character == null)
            return false;

        if (!TryGetSkillSet(character.CharacterModelId, out IPaperLegendHeroSkillSet skillSet))
            return false;

        slot = Mathf.Clamp(slot, 1, MaxSkillSlots);
        return skillSet.CanUpgradeSkill(character, slot, character.GetSkillLevel(slot));
    }

    public static bool TryUseSkill(PaperLegendCharacterNetworkHandler character, int slot)
    {
        if (character == null)
            return false;

        if (!TryGetSkillSet(character.CharacterModelId, out IPaperLegendHeroSkillSet skillSet))
            return false;

        slot = Mathf.Clamp(slot, 1, MaxSkillSlots);
        int skillLevel = character.GetSkillLevel(slot);
        return skillSet.CanUseSkill(character, slot, skillLevel)
            && skillSet.TryUseSkill(character, slot, skillLevel);
    }

    public static int ModifyExperienceReward(PaperLegendCharacterNetworkHandler character, int amount, PaperLegendExperienceSource source)
    {
        amount = Mathf.Max(0, amount);
        if (character == null || amount <= 0)
            return amount;

        return TryGetSkillSet(character.CharacterModelId, out IPaperLegendHeroSkillSet skillSet)
            ? Mathf.Max(0, skillSet.ModifyExperienceReward(character, amount, source))
            : amount;
    }

    public static void NotifyHeroConfigured(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null)
            return;

        if (TryGetSkillSet(character.CharacterModelId, out IPaperLegendHeroSkillSet skillSet))
            skillSet.OnHeroConfigured(character);
    }

    public static void NotifyHeroLevelChanged(PaperLegendCharacterNetworkHandler character, int oldLevel, int newLevel)
    {
        if (character == null || oldLevel == newLevel)
            return;

        if (TryGetSkillSet(character.CharacterModelId, out IPaperLegendHeroSkillSet skillSet))
            skillSet.OnHeroLevelChanged(character, oldLevel, newLevel);
    }

    internal static List<PaperLegendHeroSkillData> BuildFixedSlotSkillList(
        IReadOnlyList<PaperLegendHeroSkillData> apiSkills,
        IReadOnlyList<PaperLegendHeroSkillData> fallbackSkills,
        int slotCount)
    {
        slotCount = Mathf.Clamp(slotCount, 0, MaxSkillSlots);
        PaperLegendHeroSkillData[] bySlot = new PaperLegendHeroSkillData[slotCount];

        FillSlotSkills(apiSkills, bySlot);
        FillMissingSlotSkills(fallbackSkills, bySlot);

        List<PaperLegendHeroSkillData> result = new List<PaperLegendHeroSkillData>(slotCount);
        for (int i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] != null)
                result.Add(CloneWithSlot(bySlot[i], i + 1));
        }

        return result;
    }

    internal static PaperLegendHeroSkillData FindSkillByCodeOrSlot(
        IReadOnlyList<PaperLegendHeroSkillData> apiSkills,
        PaperLegendHeroSkillId skillId,
        int slot)
    {
        if (apiSkills == null)
            return null;

        int resolvedSkillId = (int)skillId;
        for (int i = 0; i < apiSkills.Count; i++)
        {
            PaperLegendHeroSkillData skill = apiSkills[i];
            if (skill == null)
                continue;

            if (skill.ResolveSkillIdInt() == resolvedSkillId)
                return skill;
        }

        for (int i = 0; i < apiSkills.Count; i++)
        {
            PaperLegendHeroSkillData skill = apiSkills[i];
            if (skill != null && skill.slot == slot)
                return skill;
        }

        return null;
    }

    internal static PaperLegendHeroSkillData CloneWithSlot(PaperLegendHeroSkillData source, int slot)
    {
        if (source == null)
            return null;

        return new PaperLegendHeroSkillData
        {
            id = source.id,
            heroId = source.heroId,
            slot = slot,
            code = source.code,
            name = source.name,
            description = source.description,
            cooldown = source.cooldown,
            manaCost = source.manaCost,
            damage = source.damage,
            damageLevel1 = source.damageLevel1,
            damageLevel2 = source.damageLevel2,
            damageLevel3 = source.damageLevel3,
            damageLevel4 = source.damageLevel4,
            range = source.range,
            duration = source.duration,
            config = source.config,
            iconUrl = source.iconUrl,
            vfxCode = source.vfxCode,
            sfxCode = source.sfxCode,
            isPassive = source.isPassive,
            isActive = source.isActive
        };
    }

    private static Dictionary<int, IPaperLegendHeroSkillSet> CreateLookup()
    {
        Dictionary<int, IPaperLegendHeroSkillSet> lookup = new Dictionary<int, IPaperLegendHeroSkillSet>();
        for (int i = 0; i < SkillSets.Length; i++)
        {
            IPaperLegendHeroSkillSet skillSet = SkillSets[i];
            if (skillSet == null || skillSet.HeroModelId <= 0)
                continue;

            lookup[skillSet.HeroModelId] = skillSet;
        }

        return lookup;
    }

    private static void FillSlotSkills(IReadOnlyList<PaperLegendHeroSkillData> source, PaperLegendHeroSkillData[] bySlot)
    {
        if (source == null || bySlot == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            PaperLegendHeroSkillData skill = source[i];
            if (skill == null)
                continue;

            int slot = ResolveSlot(skill, i, bySlot.Length);
            if (slot >= 1 && slot <= bySlot.Length && bySlot[slot - 1] == null)
                bySlot[slot - 1] = skill;
        }
    }

    private static void FillMissingSlotSkills(IReadOnlyList<PaperLegendHeroSkillData> source, PaperLegendHeroSkillData[] bySlot)
    {
        if (source == null || bySlot == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            PaperLegendHeroSkillData skill = source[i];
            if (skill == null)
                continue;

            int slot = ResolveSlot(skill, i, bySlot.Length);
            if (slot >= 1 && slot <= bySlot.Length && bySlot[slot - 1] == null)
                bySlot[slot - 1] = skill;
        }
    }

    private static int ResolveSlot(PaperLegendHeroSkillData skill, int index, int slotCount)
    {
        if (skill != null && skill.slot >= 1 && skill.slot <= slotCount)
            return skill.slot;

        return Mathf.Clamp(index + 1, 1, Mathf.Max(1, slotCount));
    }
}
