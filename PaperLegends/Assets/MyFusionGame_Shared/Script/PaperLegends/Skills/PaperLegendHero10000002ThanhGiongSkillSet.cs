using System.Collections.Generic;
using UnityEngine;

public sealed class PaperLegendHero10000002ThanhGiongSkillSet : IPaperLegendHeroSkillSet
{
    public const int HeroId = 10000002;
    public const int ChildFormId = 1000000201;
    public const int HorseFormId = 1000000202;

    private const float ChildExperienceMultiplier = 1.25f;
    private const float FirstVoiceRadius = 4.25f;
    private const float FirstVoiceBaseHorizontalImpulse = 6.5f;
    private const float FirstVoiceHorizontalImpulsePerLevel = 1.15f;
    private const float FirstVoiceBaseUpwardImpulse = 1.35f;
    private const float FirstVoiceUpwardImpulsePerLevel = 0.28f;

    private static readonly Vector3 ChildScale = Vector3.one;
    private static readonly Vector3 HorseScale = new Vector3(1.45f, 1.45f, 1.45f);

    public int HeroModelId => HeroId;

    public List<PaperLegendHeroSkillData> BuildSkillList(int heroLevel, IReadOnlyList<PaperLegendHeroSkillData> apiSkills)
    {
        return heroLevel >= 10
            ? ResolveSkillList(apiSkills, CreateHorseFormFallbackSkills())
            : ResolveSkillList(apiSkills, CreateChildFormFallbackSkills());
    }

    public bool CanUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel)
    {
        if (character == null || skillLevel <= 0)
            return false;

        if (character.Level < 10)
            return slot == 2;

        return slot >= 1 && slot <= 4;
    }

    public bool CanUpgradeSkill(PaperLegendCharacterNetworkHandler character, int slot, int currentSkillLevel)
    {
        if (character == null)
            return false;

        int maxSlot = character.Level >= 10 ? 4 : 2;
        return slot >= 1 && slot <= maxSlot;
    }

    public bool TryUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel)
    {
        if (character == null)
            return false;

        if (character.Level < 10)
        {
            if (slot == 1)
                return false;

            if (slot == 2)
                return CastFirstVoice(character, skillLevel);
        }

        string formName = character.Level >= 10 ? "Phu Dong Thien Vuong" : "Cau Be Giong";
        Debug.Log($"[PaperLegends][Skill] player={character.PlayerId} used Thanh Giong slot={slot}, level={skillLevel}, form={formName}. Gameplay is reserved for a later pass.");
        return true;
    }

    public int ModifyExperienceReward(PaperLegendCharacterNetworkHandler character, int amount, PaperLegendExperienceSource source)
    {
        if (character == null || amount <= 0)
            return amount;

        if (character.Level >= 10 || character.GetSkillLevel(1) <= 0)
            return amount;

        return Mathf.CeilToInt(amount * ChildExperienceMultiplier);
    }

    public void OnHeroConfigured(PaperLegendCharacterNetworkHandler character)
    {
        ApplyForm(character, character != null ? character.Level : 1);
    }

    public void OnHeroLevelChanged(PaperLegendCharacterNetworkHandler character, int oldLevel, int newLevel)
    {
        if (character == null)
            return;

        bool oldHorseForm = oldLevel >= 10;
        bool newHorseForm = newLevel >= 10;
        if (oldHorseForm == newHorseForm && character.HeroFormId > 0)
            return;

        ApplyForm(character, newLevel);
    }

    private static void ApplyForm(PaperLegendCharacterNetworkHandler character, int level)
    {
        if (character == null)
            return;

        bool horseForm = level >= 10;
        int formId = horseForm ? HorseFormId : ChildFormId;
        Vector3 scale = horseForm ? HorseScale : ChildScale;
        character.ServerApplyHeroForm(formId, formId, scale);
    }

    private static List<PaperLegendHeroSkillData> ResolveSkillList(
        IReadOnlyList<PaperLegendHeroSkillData> apiSkills,
        List<PaperLegendHeroSkillData> fallbackSkills)
    {
        List<PaperLegendHeroSkillData> result = new List<PaperLegendHeroSkillData>(fallbackSkills.Count);
        for (int i = 0; i < fallbackSkills.Count; i++)
        {
            PaperLegendHeroSkillData fallback = fallbackSkills[i];
            int slot = fallback != null ? fallback.slot : i + 1;
            int skillId = fallback != null ? fallback.ResolveSkillIdInt() : 0;
            PaperLegendHeroSkillData apiSkill = FindSkillByCode(apiSkills, skillId);
            result.Add(PaperLegendHeroSkillRegistry.CloneWithSlot(apiSkill ?? fallback, slot));
        }

        return result;
    }

    private static PaperLegendHeroSkillData FindSkillByCode(IReadOnlyList<PaperLegendHeroSkillData> apiSkills, int skillId)
    {
        if (apiSkills == null || skillId <= 0)
            return null;

        for (int i = 0; i < apiSkills.Count; i++)
        {
            PaperLegendHeroSkillData skill = apiSkills[i];
            if (skill != null && skill.ResolveSkillIdInt() == skillId)
                return skill;
        }

        return null;
    }

    private static List<PaperLegendHeroSkillData> CreateChildFormFallbackSkills()
    {
        return new List<PaperLegendHeroSkillData>
        {
            CreateSkill(1, PaperLegendHeroSkillId.Hero10000002ChildSkill1, "An Bao Nhieu Cung Khong Du", "Passive. While Thanh Giong is level 1-9, experience gained is increased by 25%.", true),
            CreateSkill(2, PaperLegendHeroSkillId.Hero10000002ChildSkill2, "Tieng Noi Dau Tien", "Thanh Giong shouts: Xin vua ren ngua sat! Nearby enemies are knocked away.")
        };
    }

    private static List<PaperLegendHeroSkillData> CreateHorseFormFallbackSkills()
    {
        return new List<PaperLegendHeroSkillData>
        {
            CreateSkill(1, PaperLegendHeroSkillId.Hero10000002HorseSkill1, "Phu Dong Skill 1", "Reserved horse-form skill unlocked at level 10."),
            CreateSkill(2, PaperLegendHeroSkillId.Hero10000002HorseSkill2, "Phu Dong Skill 2", "Reserved horse-form skill unlocked at level 10."),
            CreateSkill(3, PaperLegendHeroSkillId.Hero10000002HorseSkill3, "Phu Dong Skill 3", "Reserved horse-form skill unlocked at level 10."),
            CreateSkill(4, PaperLegendHeroSkillId.Hero10000002HorseSkill4, "Phu Dong Skill 4", "Reserved horse-form skill unlocked at level 10.")
        };
    }

    private static PaperLegendHeroSkillData CreateSkill(int slot, PaperLegendHeroSkillId skillId, string name, string description, bool isPassive = false)
    {
        return new PaperLegendHeroSkillData
        {
            slot = slot,
            code = ((int)skillId).ToString(),
            name = name,
            description = description,
            isPassive = isPassive,
            isActive = !isPassive
        };
    }

    private static bool CastFirstVoice(PaperLegendCharacterNetworkHandler character, int skillLevel)
    {
        if (character == null)
            return false;

        Vector3 origin = character.GetWorldBounds().center;
        float horizontalImpulse = FirstVoiceBaseHorizontalImpulse + FirstVoiceHorizontalImpulsePerLevel * Mathf.Max(0, skillLevel - 1);
        float upwardImpulse = FirstVoiceBaseUpwardImpulse + FirstVoiceUpwardImpulsePerLevel * Mathf.Max(0, skillLevel - 1);
        int affected = 0;

        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host != null ? host.GetRegisteredPlayers() : null;
        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                PaperLegendCharacterNetworkHandler target = players[i];
                if (target == null || target == character)
                    continue;

                if (target.ServerApplyRadialKnockback(character, origin, FirstVoiceRadius, horizontalImpulse, upwardImpulse))
                    affected++;
            }
        }

        character.ServerDispatchSkillEvent(PaperLegendHeroSkillId.Hero10000002ChildSkill2, 2, origin, FirstVoiceRadius);
        Debug.Log($"[PaperLegends][Skill] Thanh Giong first voice player={character.PlayerId}, level={skillLevel}, affected={affected}, radius={FirstVoiceRadius:0.00}.");
        return true;
    }
}
