using System;

[Serializable]
public class LuckyDrawAfterMatchReward
{
    public string rewardType;
    public string itemName;
    public int itemId;
    public int ringBall;
    public int exp;
    public bool isRare;
    public int luckyRate;

    public bool IsItem => string.Equals(rewardType, "item", StringComparison.OrdinalIgnoreCase);
    public bool IsStats => string.Equals(rewardType, "stats", StringComparison.OrdinalIgnoreCase);
}

[Serializable]
public class LuckyDrawAfterMatchRequest
{
    public string playerId;
}
