using System;
using System.Collections.Generic;

[Serializable]
public class MarketSearchPopupValues
{
    public string ItemName = string.Empty;
    public int? LevelFrom;
    public int? LevelTo;
    public List<int> RarityGids = new();

    public MarketSearchPopupValues Clone()
    {
        return new MarketSearchPopupValues
        {
            ItemName = ItemName,
            LevelFrom = LevelFrom,
            LevelTo = LevelTo,
            RarityGids = RarityGids != null ? new List<int>(RarityGids) : new List<int>()
        };
    }
}
