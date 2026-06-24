[System.Serializable]
public class EquipEffectRequest
{
    public int playerId;
    public int oldEffectId;
    public int newEffectId;
}

[System.Serializable]
public class EquipEffectResponse
{
    public int playerId;
    public int oldEffectId;
    public int newEffectId;
    public string message;
}
