using System;
using System.Collections.Generic;

[Serializable]
public class PlayerRankLeaderboardResponse
{
    public List<PlayerRankLeaderboardEntry> leaderboard;
    public PlayerRankLeaderboardEntry playerRank;
}
