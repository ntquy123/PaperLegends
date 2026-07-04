using Fusion;

[System.Serializable]
public struct PlayerScoreData : INetworkStruct
{
    public PlayerRef PlayerRef;
    public int PlayerId;
    public int Score;
    public int Kills;
    public int Assists;
    public int Deaths;
    public int ScoreReachedSequence;
}
