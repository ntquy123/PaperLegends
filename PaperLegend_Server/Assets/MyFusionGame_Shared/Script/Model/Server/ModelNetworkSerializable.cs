

using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
//public class RpgSetttingsGameModel: MonoBehaviour
//{
//    public RpgRoomModel rpgRoomModel;
//    public MarbleSpawnData[] spawnDataList;
//    public PlayerInfoStruct[] players;

//}

[System.Serializable]
public struct MarbleSpawnData : INetworkStruct

{
    public Vector3 Position;
    public Quaternion Rotation;
}

[System.Serializable]
public struct RpgRoomModel : INetworkStruct
{
    public NetworkString<_64> gameScene;
    public TypeMatchGid TypeMatch;
    public int roomId;
    public int betCount;
    public int MaxPlayer;
    public int MaxRound;
    public TimeOfDay timeOfDay;
    public WeatherType weatherType;
}
 
[System.Serializable]
public struct PlayerInfoStruct : INetworkStruct
{
    public int playerId;
    public int level;
    public NetworkString<_64> fullname;
    public NetworkString<_256> avatarUrl;
    public float powerForce;
    public float spinForce;
    public float exactRatio;
    public int avatar;
    public ItemCode ball;
    public PlayerBodyType playerbody;
    public int RingBall;
    public NetworkString<_32> providerType;
    public NetworkString<_64> idAccount;
    public int score;
    public float scoreExam;
    public int combo;
    public StatusPlayer statusPlayer;
    public float distance;
    public bool isDestroy;
    public bool isHolding;
    public int turnOrder;
    //trang thai ky nang
    public int isCatAnTienActive;
   // public Vector3 FingerPosition;
}

[System.Serializable]
public struct OrderPlayerData : INetworkStruct

{
    public int playerId;
    public int seq;
}

[System.Serializable]
public class LoginUserModel
{
    public int UserId;
    public string Username;
    public string Token;
    public string FriendCode;
    public string AvatarUrl;
    public int Level;
    public int Exp;
    public int Body;
    public int RingBall;
    public int Money;
    public int TalentPoint;
    public bool IsActive;
    public string Email;
    public string ProviderType;
    public string AccessToken;
    public string RefreshToken;
    public string AccessTokenExpiresAt;
    public string RefreshTokenExpiresAt;
    public bool IsTutorialCompleted;
    public string CreatedAt;
    public string LastLoginAt;
}

[System.Serializable]
public class UserRoomListWrapperFix
{
    public List<PlayerSchema> players;
}
[System.Serializable]
public class TurnOrderEntry
{
    public int playerId;
    public int turnOrder;

    public TurnOrderEntry(int playerId, int turnOrder)
    {
        this.playerId = playerId;
        this.turnOrder = turnOrder;
    }
}

public struct HitInfo : INetworkStruct
{
    public float magnitude;
    public Vector3 point;
    public HitSurface surfaceType;
}

public enum HitSurface : byte
{
    None = 0,
    Ball = 1,
    Water = 2,
    Rock = 3,
    Tree = 4,
    Puddle = 5,
    Grass = 6,
    Swamp = 7
}

[System.Serializable]
public struct BallPhysicsStruct : INetworkStruct
{
    public int playerId;
    public NetworkString<_64> name;
    public int skillGenCode;
    public float Mass;
    public float GravityScale;
    public float Drag;
    public float Bounciness;
    public float Elasticity;
    public float ImpactResistance;
}
