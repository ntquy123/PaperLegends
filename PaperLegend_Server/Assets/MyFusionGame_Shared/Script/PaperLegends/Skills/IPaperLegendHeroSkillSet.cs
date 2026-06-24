using System.Collections.Generic;
using UnityEngine;

public interface IPaperLegendHeroSkillSet
{
    int HeroModelId { get; }

    List<PaperLegendHeroSkillData> BuildSkillList(int heroLevel, IReadOnlyList<PaperLegendHeroSkillData> apiSkills);
    bool CanUpgradeSkill(PaperLegendCharacterNetworkHandler character, int slot, int currentSkillLevel);
    bool CanUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel);
    bool TryUseSkill(PaperLegendCharacterNetworkHandler character, int slot, int skillLevel);
    int ModifyExperienceReward(PaperLegendCharacterNetworkHandler character, int amount, PaperLegendExperienceSource source);
    void OnHeroConfigured(PaperLegendCharacterNetworkHandler character);
    void OnHeroLevelChanged(PaperLegendCharacterNetworkHandler character, int oldLevel, int newLevel);
}
