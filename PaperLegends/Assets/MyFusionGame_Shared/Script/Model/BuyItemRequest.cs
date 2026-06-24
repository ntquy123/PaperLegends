[System.Serializable]
public enum ShopPurchaseCurrency
{
    Money = 1,
    RingBall = 2
}

[System.Serializable]
public class BuyItemRequest
{
    public int playerId;
    public int itemId;
    public int currencyType;
}
