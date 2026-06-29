using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class PaperLegendHeroConfigAssetGenerator
{
    private const string ResourcesRoot = "Assets/Resources/PaperLegends";
    private const string HeroConfigRoot = ResourcesRoot + "/HeroConfigs";
    private const string AudioRoot = HeroConfigRoot + "/Audio";
    private const string VfxRoot = HeroConfigRoot + "/Vfx";
    private const string SkillRoot = HeroConfigRoot + "/Skills";
    private const string AddressableRoot = "Assets/AddressableAsset/PaperLegends";

    [MenuItem("Tools/Paper Legends/Create Default Hero Config Assets")]
    public static void CreateDefaultHeroConfigs()
    {
        EnsureFolders();

        HeroConfig hero10000001 = CreateHero10000001();
        HeroConfig thanhGiong = CreateHero10000002();
        HeroConfig sonTinh = CreateSonTinh();
        HeroConfig sonTinh10000004 = CreateSonTinh10000004();
        HeroConfig thanSam = CreateThanSam();

        HeroConfigCatalog catalog = LoadOrCreateAsset<HeroConfigCatalog>(
            ResourcesRoot + "/HeroConfigCatalog.asset");
        catalog.heroes = new[] { hero10000001, thanhGiong, sonTinh, sonTinh10000004, thanSam };
        EditorUtility.SetDirty(catalog);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PaperLegends][Config] Default hero config assets created or updated.");
    }

    [MenuItem("Tools/Paper Legends/Sync Paper Legends Addressables")]
    public static void SyncPaperLegendAddressables()
    {
        EnsureFolders();

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("[PaperLegends][Addressables] Addressable settings not found.");
            return;
        }

        AddressableAssetGroup group = settings.DefaultGroup;
        if (group == null)
        {
            Debug.LogWarning("[PaperLegends][Addressables] Default Addressables group not found.");
            return;
        }

        int syncedCount = 0;
        syncedCount += SyncAddressableFolder(settings, group, AddressableRoot + "/HeroIcons");
        syncedCount += SyncAddressableFolder(settings, group, AddressableRoot + "/HeroPortraits");
        syncedCount += SyncAddressableFolder(settings, group, AddressableRoot + "/HeroMaterial");
        syncedCount += SyncAddressableFolder(settings, group, AddressableRoot + "/HeroPrefabs");
        syncedCount += SyncAddressableFolder(settings, group, AddressableRoot + "/SkillIcons");

        AssetDatabase.SaveAssets();
        Debug.Log($"[PaperLegends][Addressables] Synced {syncedCount} Paper Legends addressable asset(s).");
    }

    private static HeroConfig CreateHero10000001()
    {
        const int heroId = 10000001;
        string heroFolder = SkillRoot + "/" + heroId;
        EnsureFolder(SkillRoot, heroId.ToString());

        HeroAudioConfig audio = LoadOrCreateAsset<HeroAudioConfig>(AudioRoot + "/HeroAudioConfig_10000001.asset");
        audio.heroId = heroId;
        EditorUtility.SetDirty(audio);

        HeroVfxConfig vfx = LoadOrCreateAsset<HeroVfxConfig>(VfxRoot + "/HeroVfxConfig_10000001.asset");
        vfx.heroId = heroId;
        EditorUtility.SetDirty(vfx);

        SkillConfig[] skills =
        {
            CreateSkill(heroFolder, heroId, 11400001, 1, "Distance Landing Damage", "Passive. The farther the paper hero travels before landing on a target, the higher the damage. Max x4.", true),
            CreateSkill(heroFolder, heroId, 11400002, 2, "Reserved Skill 2", "Reserved gameplay slot for hero 10000001.", false),
            CreateSkill(heroFolder, heroId, 11400003, 3, "Flick Force Boost", "Boosts the next flick force and lets the camera look farther.", false),
            CreateSkill(heroFolder, heroId, 11400004, 4, "Reserved Skill 4", "Reserved gameplay slot for hero 10000001.", false)
        };

        HeroConfig hero = LoadOrCreateAsset<HeroConfig>(HeroConfigRoot + "/HeroConfig_10000001.asset");
        hero.heroId = heroId.ToString();
        hero.heroName = "Hero 10000001";
        hero.nameKey = "hero_10000001_name";
        hero.audioConfig = audio;
        hero.vfxConfig = vfx;
        hero.skills = skills;
        EditorUtility.SetDirty(hero);
        return hero;
    }

    private static HeroConfig CreateHero10000002()
    {
        const int heroId = 10000002;
        string heroFolder = SkillRoot + "/" + heroId;
        EnsureFolder(SkillRoot, heroId.ToString());

        HeroAudioConfig audio = LoadOrCreateAsset<HeroAudioConfig>(AudioRoot + "/HeroAudioConfig_ThanhGiong.asset");
        audio.heroId = heroId;
        EditorUtility.SetDirty(audio);

        HeroVfxConfig vfx = LoadOrCreateAsset<HeroVfxConfig>(VfxRoot + "/HeroVfxConfig_ThanhGiong.asset");
        vfx.heroId = heroId;
        EditorUtility.SetDirty(vfx);

        SkillConfig[] skills =
        {
            CreateSkill(heroFolder, heroId, 11400021, 1, "Forward Slide", "After casting, the next 3 swipes on your hero slide forward instead of a paper flick.", false),
            CreateSkill(heroFolder, heroId, 11400022, 2, "Shove Stun", "After casting, if an enemy is nearby within 3 seconds, swipe on them to shove away and stun for 1-1.8 seconds.", false),
            CreateSkill(heroFolder, heroId, 11400023, 3, "Flick Speed Boost", "Passive. Increases paper flick flight speed by 10%, 15%, 20%, or 25% based on skill level.", true),
            CreateSkill(heroFolder, heroId, 11400024, 4, "Last Stand", "Become invincible for 5 seconds. Health cannot drop below 1. Cooldown starts after invincibility ends (60s to 30s by level). Also auto-triggers when health would reach 1.", false),
        };

        HeroConfig hero = LoadOrCreateAsset<HeroConfig>(HeroConfigRoot + "/HeroConfig_ThanhGiong.asset");
        hero.heroId = heroId.ToString();
        hero.heroName = "Hero 10000002";
        hero.nameKey = "hero_10000002_name";
        hero.audioConfig = audio;
        hero.vfxConfig = vfx;
        hero.skills = skills;
        EditorUtility.SetDirty(hero);
        return hero;
    }

    private static HeroConfig CreateSonTinh()
    {
        const int heroId = 10000003;
        string heroFolder = SkillRoot + "/" + heroId;
        EnsureFolder(SkillRoot, heroId.ToString());

        HeroAudioConfig audio = LoadOrCreateAsset<HeroAudioConfig>(AudioRoot + "/HeroAudioConfig_SonTinh.asset");
        audio.heroId = heroId;
        EditorUtility.SetDirty(audio);

        HeroVfxConfig vfx = LoadOrCreateAsset<HeroVfxConfig>(VfxRoot + "/HeroVfxConfig_SonTinh.asset");
        vfx.heroId = heroId;
        EditorUtility.SetDirty(vfx);

        SkillConfig[] skills =
        {
            CreateSkill(heroFolder, heroId, 11400031, 1, "Water Burst", "After casting, swipe a direction to erupt 3 water bursts in a line. Bursts deal 100%, 120%, then 150% damage.", false, 7f, 10f),
            CreateSkill(heroFolder, heroId, 11400032, 2, "Wave Push", "After casting, the next flick direction releases a wave that pushes paper heroes hit by the wave.", false, 8f),
            CreateSkill(heroFolder, heroId, 11400033, 3, "Reserved Skill 3", "Reserved gameplay slot for Son Tinh.", false),
            CreateSkill(heroFolder, heroId, 11400034, 4, "Reserved Skill 4", "Reserved gameplay slot for Son Tinh.", false)
        };

        HeroConfig hero = LoadOrCreateAsset<HeroConfig>(HeroConfigRoot + "/HeroConfig_SonTinh.asset");
        hero.heroId = heroId.ToString();
        hero.heroName = "Son Tinh";
        hero.nameKey = "hero_10000003_name";
        hero.audioConfig = audio;
        hero.vfxConfig = vfx;
        hero.skills = skills;
        EditorUtility.SetDirty(hero);
        return hero;
    }

    private static HeroConfig CreateSonTinh10000004()
    {
        const int heroId = 10000004;
        string heroFolder = SkillRoot + "/" + heroId;
        EnsureFolder(SkillRoot, heroId.ToString());

        HeroAudioConfig audio = LoadOrCreateAsset<HeroAudioConfig>(AudioRoot + "/HeroAudioConfig_SonTinh_10000004.asset");
        audio.heroId = heroId;
        EditorUtility.SetDirty(audio);

        HeroVfxConfig vfx = LoadOrCreateAsset<HeroVfxConfig>(VfxRoot + "/HeroVfxConfig_SonTinh_10000004.asset");
        vfx.heroId = heroId;
        EditorUtility.SetDirty(vfx);

        SkillConfig[] skills =
        {
            CreateSkill(heroFolder, heroId, 11400041, 1, "Reserved Skill 1", "Reserved gameplay slot for hero 10000004.", false),
            CreateSkill(heroFolder, heroId, 11400042, 2, "Reserved Skill 2", "Reserved gameplay slot for hero 10000004.", false),
            CreateSkill(heroFolder, heroId, 11400043, 3, "Reserved Skill 3", "Reserved gameplay slot for hero 10000004.", false),
            CreateSkill(heroFolder, heroId, 11400044, 4, "Reserved Skill 4", "Reserved gameplay slot for hero 10000004.", false)
        };

        HeroConfig hero = LoadOrCreateAsset<HeroConfig>(HeroConfigRoot + "/HeroConfig_SonTinh_10000004.asset");
        hero.heroId = heroId.ToString();
        hero.heroName = "Son Tinh";
        hero.nameKey = "hero_10000004_name";
        hero.audioConfig = audio;
        hero.vfxConfig = vfx;
        hero.skills = skills;
        EditorUtility.SetDirty(hero);
        return hero;
    }

    private static HeroConfig CreateThanSam()
    {
        const int heroId = 10000005;
        string heroFolder = SkillRoot + "/" + heroId;
        EnsureFolder(SkillRoot, heroId.ToString());

        HeroAudioConfig audio = LoadOrCreateAsset<HeroAudioConfig>(AudioRoot + "/HeroAudioConfig_ThanSam.asset");
        audio.heroId = heroId;
        EditorUtility.SetDirty(audio);

        HeroVfxConfig vfx = LoadOrCreateAsset<HeroVfxConfig>(VfxRoot + "/HeroVfxConfig_ThanSam.asset");
        vfx.heroId = heroId;
        EditorUtility.SetDirty(vfx);

        SkillConfig[] skills =
        {
            CreateSkill(heroFolder, heroId, 11400051, 1, "Reserved Skill 1", "Reserved gameplay slot for Than Sam.", false),
            CreateSkill(heroFolder, heroId, 11400052, 2, "Reserved Skill 2", "Reserved gameplay slot for Than Sam.", false),
            CreateSkill(heroFolder, heroId, 11400053, 3, "Reserved Skill 3", "Reserved gameplay slot for Than Sam.", false),
            CreateSkill(heroFolder, heroId, 11400054, 4, "Thunder Storm", "Select an area, channel for 1 second, then call lightning strikes for 4/5/6 seconds by skill level.", false)
        };

        HeroConfig hero = LoadOrCreateAsset<HeroConfig>(HeroConfigRoot + "/HeroConfig_ThanSam.asset");
        hero.heroId = heroId.ToString();
        hero.heroName = "Than Sam";
        hero.nameKey = "hero_10000005_name";
        hero.audioConfig = audio;
        hero.vfxConfig = vfx;
        hero.skills = skills;
        EditorUtility.SetDirty(hero);
        return hero;
    }

    private static SkillConfig CreateSkill(
        string folder,
        int heroId,
        int skillId,
        int slot,
        string skillName,
        string description,
        bool isPassive,
        float cooldown = 0f,
        float damage = 0f)
    {
        SkillConfig skill = LoadOrCreateAsset<SkillConfig>($"{folder}/SkillConfig_{skillId}_{SanitizeFileName(skillName)}.asset");
        skill.heroId = heroId;
        skill.skillId = skillId;
        skill.slot = slot;
        skill.skillName = skillName;
        skill.description = description;
        skill.isPassive = isPassive;
        skill.cooldown = Mathf.Max(0f, cooldown);
        skill.damage = Mathf.Max(0f, damage);
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "PaperLegends");
        EnsureFolder(ResourcesRoot, "HeroConfigs");
        EnsureFolder(HeroConfigRoot, "Audio");
        EnsureFolder(HeroConfigRoot, "Vfx");
        EnsureFolder(HeroConfigRoot, "Skills");
        EnsureFolder("Assets", "AddressableAsset");
        EnsureFolder("Assets/AddressableAsset", "PaperLegends");
        EnsureFolder(AddressableRoot, "HeroIcons");
        EnsureFolder(AddressableRoot, "HeroPortraits");
        EnsureFolder(AddressableRoot, "HeroMaterial");
        EnsureFolder(AddressableRoot, "HeroPrefabs");
        EnsureFolder(AddressableRoot, "SkillIcons");
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static int SyncAddressableFolder(AddressableAssetSettings settings, AddressableAssetGroup group, string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return 0;

        int syncedCount = 0;
        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
                continue;

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guids[i], group);
            if (entry == null)
                continue;

            entry.address = path;
            syncedCount++;
        }

        if (syncedCount > 0)
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, group, true);

        return syncedCount;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Skill";

        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');

        return value.Replace(' ', '_');
    }
}
