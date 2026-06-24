[System.Serializable]
public class LuckyDrawResponse
{
    public string type;
    public int amount;

    // Helper properties to mimic old field semantics
    public bool IsCuli => string.Equals(type, "culi", System.StringComparison.OrdinalIgnoreCase);

    public int Quantity => amount;

    // For rewards that reference an item by id in the type string, try parse it
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
[System.Serializable]
public class LuckyDrawRequest
{
    public int playerId;
}
