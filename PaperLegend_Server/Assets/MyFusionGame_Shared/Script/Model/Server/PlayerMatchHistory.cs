using System;

[Serializable]
public class PlayerMatchHistory
{
    public int playerId;
    public string transno;
    public int turnOrder;
    public int typeMatchGid;
    public int statusWin;
    public string mapGame;
    public int maxPlayer;
    public int rounds;
    public int marbBet;
    public int marblesWon;
    public int marblesLost;
    public int expGained;
    public int rankPoints;
    public string description;
    public string createdAt;
    public PlayerMatchHistoryPlayer player;
}

[Serializable]
public class PlayerMatchHistoryPlayer
{
    public int id;
    public string friendCode;
    public string PlayerName;
    public int Level;
    public int Exp;
    public int Body;
    public int RingBall;
    public int Money;
    public int TalentPoint;
    public string IdAccount;
    public bool IsActive;
    public string Email;
    public string ProviderType;
}
