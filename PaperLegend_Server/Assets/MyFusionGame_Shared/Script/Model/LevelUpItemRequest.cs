[System.Serializable]
public class LevelUpItemRequest
{
    public int playerId;
    public int itemId;
    public int seq;
    public float successRate;
    public System.Collections.Generic.List<UpgradeMaterial> materials;
}
