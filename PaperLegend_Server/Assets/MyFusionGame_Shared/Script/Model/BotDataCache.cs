using System;
using System.Collections.Generic;

[Serializable]
public class BotPlayerData
{
    public int id;
    public string PlayerName;
    public int Level;
    public int RingBall;
    public string AvatarUrl;
    public string ProviderType;
    public string IdAccount;
    public string friendCode;
}

[Serializable]
public class BotPlayerListWrapper
{
    public List<BotPlayerData> players;
}
