using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Paper Legends/Hero Config Catalog", fileName = "HeroConfigCatalog")]
public sealed class HeroConfigCatalog : ScriptableObject
{
    public const string DefaultResourcesPath = "PaperLegends/HeroConfigCatalog";

    public HeroConfig[] heroes = new HeroConfig[0];

    private static HeroConfigCatalog _defaultCatalog;
    private readonly Dictionary<int, HeroConfig> _lookup = new Dictionary<int, HeroConfig>();
    private bool _lookupBuilt;

    public IReadOnlyList<HeroConfig> Heroes => heroes;

    public static HeroConfigCatalog LoadDefault()
    {
        if (_defaultCatalog == null)
            _defaultCatalog = Resources.Load<HeroConfigCatalog>(DefaultResourcesPath);

        return _defaultCatalog;
    }

    public static HeroConfig ResolveHero(int heroId)
    {
        HeroConfigCatalog catalog = LoadDefault();
        return catalog != null && catalog.TryGetHero(heroId, out HeroConfig hero) ? hero : null;
    }

    public static SkillConfig ResolveSkill(int skillId)
    {
        HeroConfigCatalog catalog = LoadDefault();
        return catalog != null ? catalog.FindSkill(skillId) : null;
    }

    public static Sprite ResolveSkillIcon(int skillId)
    {
        // Skill icons are Addressables now; keep this API as a null-safe legacy fallback.
        // Use PaperLegendHeroAddressables.LoadSkillIconSpriteRoutine for runtime loading.
        return null;
    }

    public static AssetReferenceSprite ResolveSkillIconReference(int skillId)
    {
        SkillConfig skill = ResolveSkill(skillId);
        return skill != null ? skill.iconRef : null;
    }

    public static AssetReferenceSprite ResolveHeroIconReference(int heroId)
    {
        HeroConfig hero = ResolveHero(heroId);
        return hero != null ? hero.avatarIconRef : null;
    }

    public static AssetReferenceSprite ResolveHeroPortraitReference(int heroId)
    {
        HeroConfig hero = ResolveHero(heroId);
        return hero != null ? hero.avatarFullRef : null;
    }

    public bool TryGetHero(int heroId, out HeroConfig hero)
    {
        BuildLookupIfNeeded();
        return _lookup.TryGetValue(heroId, out hero);
    }

    public SkillConfig FindSkill(int skillId)
    {
        if (heroes == null || skillId <= 0)
            return null;

        for (int i = 0; i < heroes.Length; i++)
        {
            HeroConfig hero = heroes[i];
            if (hero == null)
                continue;

            SkillConfig skill = hero.GetSkillById(skillId);
            if (skill != null)
                return skill;
        }

        return null;
    }

    private void BuildLookupIfNeeded()
    {
        if (_lookupBuilt)
            return;

        _lookupBuilt = true;
        _lookup.Clear();

        if (heroes == null)
            return;

        for (int i = 0; i < heroes.Length; i++)
        {
            HeroConfig hero = heroes[i];
            if (hero == null)
                continue;

            int heroId = hero.ResolveHeroIdInt();
            if (heroId > 0)
                _lookup[heroId] = hero;
        }
    }
}
