// Class để parse response JSON
using System.Collections.Generic;

[System.Serializable]
public class RoomResponse
{
    public int roomId;
    public int userId;
    public int createId;
    public string roomName;
    public string message;
    public int port;
    public int bet;
    public int maxPlayer;
    public int maxRound;
    public int rounds;
    public int mapId;

    public int GetMaxRound(int fallback = 5)
    {
        if (maxRound > 0)
            return maxRound;

        if (rounds > 0)
            return rounds;

        return fallback;
    }
}

[System.Serializable]
public class RoomData
{
    public int id;
    public string roomName;
    public string createPlayerName;
    public int createId;
    public int bet;
    public int mapId;
    public int maxRound;
    public int rounds;
    public int maxPlayer;
    public int maxPlayers;
    public int currentPlayers;

    public int GetMaxPlayers()
    {
        return maxPlayer != 0 ? maxPlayer : maxPlayers;
    }

    public int GetMaxRound(int fallback = 5)
    {
        if (maxRound > 0)
            return maxRound;

        if (rounds > 0)
            return rounds;

        return fallback;
    }
}

[System.Serializable]
public class RoomListWrapper
{
    public List<RoomData> rooms;
}

[System.Serializable]
public class UserRoom
{
    public int id;
    public int roomId;
    public int userId;
    public string joinedAt;
    public PlayerSchema player;
}

[System.Serializable]
public class UserRoomListWrapper
{
    public List<UserRoom> users;
}


[System.Serializable]
public class UserList
{
    public List<UserRoom> users;
}
[System.Serializable]
public class Wrapper
{
    public List<int> ids;
}

[System.Serializable]
public class CreateRoomRequest
{
    public int userId;
    public int bet;
    public int maxPlayer;
    public int mapId;
    public int maxRound;
    public int rounds;
    public string sessionName;
    public string roomName;
}

[System.Serializable]
public class LeaveRoomRequest
{
    public int userId;
    public int roomId;
}
