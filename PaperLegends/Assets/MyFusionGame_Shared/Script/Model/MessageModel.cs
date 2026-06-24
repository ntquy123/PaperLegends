[System.Serializable]
public class MessageModel
{
    public int senderId;
    public string PlayerName;
    public int seqMess;
    public int receiverId;
    public string status;
    public string message;
    public int itemId;
    public int seqId;
    public string createdAt;
    public int ringBallReward = 0;
    public int moneyReward = 0;
    public int itemRewardId = 0;
    public SenderInfo sender;
}

[System.Serializable]
public class SenderInfo
{
    public int id;
    public string PlayerName;
    public int Level;
    public int Exp;
    public int Body;
    public int RingBall;
    public int Money;
    public int TalentPoint;
    public string IdAccount;
    public bool IsActive;
}
