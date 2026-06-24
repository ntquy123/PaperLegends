[System.Serializable]
public class MarketItemSchema {
    public int playerId;    // sellerId
    public string playerName;
    public int itemId;
    public int seq;
    public int level;
    public string description;
    public int Price;
    public int IsSolded;
    public ItemSchema item;
    public PlayerInfor player;
}
public class PlayerInfor
{
    public string PlayerName;
}