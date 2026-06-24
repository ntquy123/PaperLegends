using System.Collections.Generic;
using UnityEngine;

public class OfflineObjectManager : MonoBehaviour
{
    public List<PlayerInfoStruct> players = new List<PlayerInfoStruct>();
    public List<BallPhysicsStruct> ballPhysicsList = new List<BallPhysicsStruct>();
    public int currentPlayerIndex;
    public int TurnCount;
    public RpgRoomModel rpgRoomModel;

    public void Initialize(List<PlayerInfoStruct> playerData, List<BallPhysicsStruct> physicsData, RpgRoomModel settings)
    {
        players = playerData;
        ballPhysicsList = physicsData;
        rpgRoomModel = settings;
        currentPlayerIndex = 0;
        TurnCount = 0;
    }

    public BallPhysicsStruct GetBallPhysics(int playerId)
    {
        return ballPhysicsList.Find(p => p.playerId == playerId);
    }
}
