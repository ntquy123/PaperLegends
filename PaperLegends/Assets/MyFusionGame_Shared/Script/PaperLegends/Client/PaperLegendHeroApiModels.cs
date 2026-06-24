using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

[Serializable]
public class PaperLegendHeroListResponse
{
    public int count;
    public List<PaperLegendHeroData> heroes = new List<PaperLegendHeroData>();
}

[Serializable]
public class PaperLegendHeroData
{
    public string id;
    public string code;
    public string name;
    public string role;
    public string description;

    public int hp;
    public int attack;
    public int defense;
    public float speed;

    public float weight;
    public float bounce;
    public float friction;
    public float flickForce;

    public string modelId;
    public string prefabAddress;
    public string iconUrl;
    public string portraitUrl;
    public string selectionImageUrl;
    public int sortOrder;
    public bool isActive;

    public List<PaperLegendHeroSkillData> skills = new List<PaperLegendHeroSkillData>();

    public int ResolveModelIdInt()
    {
        if (int.TryParse(modelId, out int value))
            return value;

        if (int.TryParse(code, out value))
            return value;

        return 0;
    }
}

[Serializable]
public class PaperLegendHeroSkillData
{
    public string id;
    public string heroId;
    public int slot;
    public string code;
    public string name;
    public string description;
    public float cooldown;
    public int manaCost;
    public float damage;
    public float range;
    public float duration;
    public JToken config;
    public string iconUrl;
    public string vfxCode;
    public string sfxCode;
    public bool isPassive;
    public bool isActive;

    public int ResolveSkillIdInt()
    {
        if (int.TryParse(code, out int value))
            return value;

        if (int.TryParse(id, out value))
            return value;

        return 0;
    }
}
