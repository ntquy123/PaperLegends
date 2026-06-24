using System.Collections.Generic;

[System.Serializable]
public class EffectPlayerSchema
{
    public int playerId;
    public int effectId;
    public int power;
    public int spin;
    public int level;
    public bool isPassive;
    public int charges;
    public string description;
    public int parentId;
    public bool IsActive;
    public bool IsEquiped;
    public SysMasGeneral sysMasGeneral;
    public PlayerData player;
}
[System.Serializable]
public class SysMasGeneral
{
    public string GenName;
    public string description;
    public int ParentCode;
}
[System.Serializable]
public class PlayerData
{
    public int TalentPoint;
    public int Level;
}
[System.Serializable]
public class EffectPlayerListWrapper
{
    public List<EffectPlayerSchema> effects;
}

