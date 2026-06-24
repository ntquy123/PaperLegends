using System;

[Serializable]
public class LuckyDrawItem
{
    public string type;
    public int amount;

    public bool IsCuli => string.Equals(type, "culi", StringComparison.OrdinalIgnoreCase);
    public int Quantity => amount;
    public int ItemId
    {
        get
        {
            if (int.TryParse(type, out var id))
                return id;
            return 0;
        }
    }
}

[Serializable]
public class LuckyDrawResult : LuckyDrawItem
{
}
