using System;
using System.Collections.Generic;

[Serializable]
public class MarketOrderBoardEntry
{
    public int price;
    public int quantityBuy;
    public int quantitySell;
}

[Serializable]
public class MarketOrderBoardOrder
{
    public int price;
    public int count;
}

[Serializable]
public class MarketOrderBoardResponse
{
    public List<MarketOrderBoardOrder> sellingOrders;
    public List<MarketOrderBoardOrder> buyOrders;
}

[Serializable]
public class PlaceBuyRequestOrderRequest
{
    public int playerId;
    public int itemId;
    public int price;
    public int quantity;
}
