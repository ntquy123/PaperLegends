using System;

[Serializable]
public class PlayerItemDamageUpdateEntry
{
    public int playerId;
    public int itemId;
    public int seq;
    public float damage;
}
