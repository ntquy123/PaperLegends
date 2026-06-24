using System.Collections.Generic;

public interface ISkillHandler
{
    EffectPlayerType EffectType { get; }
    bool TryActivate(SkillManager manager, List<EffectPlayerSchema> activeEffects);
}

public class DefaultSkillHandler : ISkillHandler
{
    public EffectPlayerType EffectType => 0;

    public bool TryActivate(SkillManager manager, List<EffectPlayerSchema> activeEffects)
    {
        return true;
    }
}
