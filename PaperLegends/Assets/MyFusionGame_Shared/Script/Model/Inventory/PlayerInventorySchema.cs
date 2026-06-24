using System;
using System.Collections.Generic;

[System.Serializable]
public class ItemSchema
{
    public int id;
    public int seq;
    // For stackable items (e.g., gems), holds all available sequences
    public List<int> seqList = new List<int>();
    public StatusSold IsSolded;
    public string name;
    public string description;
    // Level player must reach to purchase this item
    public int Levelrequired;
    public int level;
    public int typeGid;
    public int price;
    public int priceByBall;
    public bool isLevelUp;
    public bool isOpen;
    public bool isOnMarket = false;
    public bool isCateye;
    public int rarityGid;
    public int locationGid;
    public float Mass;
    public float GravityScale;
    public float Drag;
    public float Bounciness;
    public float Elasticity;
    public float ImpactResistance;
    public ElementalType ElementType;
    public float damage;
    public int dailyPurchaseLimit;
    public int dailyPurchasedCount;
    public bool isDailyPurchaseLocked;
    public ActiveSkillSchema activeSkill;
    public int? SkillGid;
}

[System.Serializable]
public class ActiveSkillSchema
{
    public int GenCode;
    public string GenName;
    public string description;
    public int mana;
    public float cooldown;
}

[System.Serializable]
public class PlayerItemSchema
{
    public int playerId;
    public int itemId;
    public int seq;
    public int level;
    public string description;
    public float damage;
    public ItemSchema item;
}

[System.Serializable]
public class PlayerInventorySchema
{
    public int id;
    public string PlayerName;
    public int Level;
    public int Exp;
    public int Ball;
    public int? SeqBall;
    public int Body;
    public int Shirt;
    public int Pant;
    public int RingBall;
    public int Money;
    public int Hair;
    public int TalentPoint;
    public int GlassShard;
    public string IdAccount;
    public string createdAt;
    public string lastLoginAt;
    public List<ItemSchema> playerItems;
    public List<EquipPlayer> equippedItems;
    public int newmessage;
    public int newreqfriends;
}
[System.Serializable]
public class EquipPlayer
{
    public int locationId;
    public int id;
    public int seq;
    public string name;
    public string description;
    // Level player must reach to purchase this item
    public int Levelrequired;
    public int level;
    public int typeGid;
    public int price;
    public bool isLevelUp;
    public bool isOpen;
    public bool isOnMarket = false;
    public bool isCateye;
    public int rarityGid;
    public int locationGid;
    public float Mass;
    public float GravityScale;
    public float Drag;
    public float Bounciness;
    public float Elasticity;
    public float ImpactResistance;
    public float damage;
    public ElementalType element;
    public ActiveSkillSchema activeSkill;
    public int? SkillGid;
}
