using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class PaperLegendMatchNetworkHost : NetworkBehaviour
{
    public static PaperLegendMatchNetworkHost Instance { get; private set; }

    [Header("Win Conditions")]
    [SerializeField, Min(1)] private int killLimit = 15;
    [SerializeField] private bool enableBaseObjectiveWin = false;
    [SerializeField, Min(1)] private int baseMaxHealth = 100;

    [Header("Progression")]
    [SerializeField, Min(0)] private int killExperienceReward = 120;
    [SerializeField, Min(0)] private int assistExperienceReward = 45;
    [SerializeField, Min(0f)] private float assistRadius = 3.5f;
    [SerializeField] private bool grantAssistExperienceByProximity = true;

    [Header("Spawn")]
    [SerializeField, Min(0f)] private float respawnOccupancyRadius = 1.1f;
    [SerializeField, Min(1)] private int safeRespawnCandidateCount = 3;
    [SerializeField, Min(0f)] private float respawnKillerAvoidRadius = 6f;
    [SerializeField, Min(0f)] private float respawnDeathPositionAvoidRadius = 5f;
    [SerializeField, Min(0f)] private float respawnReuseAvoidRadius = 1.5f;

    [Networked] public int NetTeamAKills { get; private set; }
    [Networked] public int NetTeamBKills { get; private set; }
    [Networked] public int NetFaction1Kills { get; private set; }
    [Networked] public int NetFaction2Kills { get; private set; }
    [Networked] public int NetFaction3Kills { get; private set; }
    [Networked] public int NetFaction4Kills { get; private set; }
    [Networked] public int NetTeamABaseHealth { get; private set; }
    [Networked] public int NetTeamBBaseHealth { get; private set; }
    [Networked] public PaperLegendTeam NetWinningTeam { get; private set; }
    [Networked] public int NetWinningPlayerId { get; private set; }
    [Networked] public int NetWinningFactionId { get; private set; }
    [Networked] public NetworkBool NetIsMatchEnded { get; private set; }
    [Networked] public int NetCurrentKillLimit { get; private set; }
    [Networked] public NetworkBool NetBaseObjectiveWinEnabled { get; private set; }

    private int _teamAKills;
    private int _teamBKills;
    private int _faction1Kills;
    private int _faction2Kills;
    private int _faction3Kills;
    private int _faction4Kills;
    private int _teamABaseHealth;
    private int _teamBBaseHealth;
    private PaperLegendTeam _winningTeam;
    private int _winningPlayerId;
    private int _winningFactionId;
    private bool _isMatchEnded;
    private int _currentKillLimit;
    private bool _baseObjectiveWinEnabled;

    public int TeamAKills => UsesNetworkState ? NetTeamAKills : _teamAKills;
    public int TeamBKills => UsesNetworkState ? NetTeamBKills : _teamBKills;
    public int Faction1Kills => UsesNetworkState ? NetFaction1Kills : _faction1Kills;
    public int Faction2Kills => UsesNetworkState ? NetFaction2Kills : _faction2Kills;
    public int Faction3Kills => UsesNetworkState ? NetFaction3Kills : _faction3Kills;
    public int Faction4Kills => UsesNetworkState ? NetFaction4Kills : _faction4Kills;
    public int TeamABaseHealth => UsesNetworkState ? NetTeamABaseHealth : _teamABaseHealth;
    public int TeamBBaseHealth => UsesNetworkState ? NetTeamBBaseHealth : _teamBBaseHealth;
    public PaperLegendTeam WinningTeam => UsesNetworkState ? NetWinningTeam : _winningTeam;
    public int WinningPlayerId => UsesNetworkState ? NetWinningPlayerId : _winningPlayerId;
    public int WinningFactionId => UsesNetworkState ? NetWinningFactionId : _winningFactionId;
    public bool IsMatchEnded => UsesNetworkState ? NetIsMatchEnded : _isMatchEnded;
    public int CurrentKillLimit => UsesNetworkState ? NetCurrentKillLimit : _currentKillLimit;
    public bool BaseObjectiveWinEnabled => UsesNetworkState ? NetBaseObjectiveWinEnabled : _baseObjectiveWinEnabled;

    private bool UsesNetworkState => Object != null && Object.IsValid;

    private readonly List<PaperLegendCharacterNetworkHandler> _players =
        new List<PaperLegendCharacterNetworkHandler>();
    private readonly List<Transform> _spawnPoints = new List<Transform>();
    private readonly Dictionary<int, Transform> _lastRespawnPointByPlayerId = new Dictionary<int, Transform>();
    private bool _isBroadcastingEndGame;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple PaperLegendMatchNetworkHost instances detected.");
            return;
        }

        Instance = this;
    }

    public override void Spawned()
    {
        if (!CanWriteMatchState())
            return;

        ResetMatchState();
    }

    public void InitializeLocalFallback()
    {
        ResetMatchState();
    }

    public void ConfigureFreeForAllRules(int targetKillLimit, bool allowBaseObjectiveWin)
    {
        if (!CanWriteMatchState())
            return;

        killLimit = Mathf.Max(1, targetKillLimit);
        enableBaseObjectiveWin = allowBaseObjectiveWin;
        SetCurrentKillLimit(killLimit);
        SetBaseObjectiveWinEnabled(enableBaseObjectiveWin);
    }

    public void ConfigureSpawnPoints(IEnumerable<Transform> spawnPoints)
    {
        if (!CanWriteMatchState())
            return;

        _spawnPoints.Clear();
        if (spawnPoints == null)
            return;

        foreach (var point in spawnPoints)
        {
            if (point != null && !_spawnPoints.Contains(point))
                _spawnPoints.Add(point);
        }

        Debug.Log($"[PaperLegends][Spawn] Match host configured with {_spawnPoints.Count} spawn point(s).");
    }

    public bool TryResolveRespawnPose(
        PaperLegendCharacterNetworkHandler requester,
        out Vector3 position,
        out Quaternion rotation)
    {
        return TryResolveRespawnPose(
            requester,
            null,
            false,
            default,
            out position,
            out rotation);
    }

    public bool TryResolveRespawnPose(
        PaperLegendCharacterNetworkHandler requester,
        PaperLegendCharacterNetworkHandler killer,
        bool hasDeathPosition,
        Vector3 deathPosition,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        if (!CanWriteMatchState())
            return false;

        PruneNullPlayers();

        var validPoints = _spawnPoints.Where(point => point != null).ToList();
        if (validPoints.Count == 0)
            return false;

        Transform safestPoint = ResolveSafestRespawnPoint(validPoints, requester, killer, hasDeathPosition, deathPosition);
        if (safestPoint == null)
            return false;

        position = safestPoint.position;
        rotation = safestPoint.rotation;
        if (requester != null)
            _lastRespawnPointByPlayerId[requester.PlayerId] = safestPoint;

        return true;
    }

    public void RegisterPlayer(PaperLegendCharacterNetworkHandler player)
    {
        if (player == null || _players.Contains(player))
            return;

        _players.Add(player);
    }

    public void UnregisterPlayer(PaperLegendCharacterNetworkHandler player)
    {
        if (player == null)
            return;

        _players.Remove(player);
    }

    public bool ReportLandingKill(
        PaperLegendCharacterNetworkHandler attacker,
        PaperLegendCharacterNetworkHandler victim)
    {
        return ReportCharacterElimination(attacker, victim);
    }

    public bool ReportCharacterElimination(
        PaperLegendCharacterNetworkHandler attacker,
        PaperLegendCharacterNetworkHandler victim)
    {
        if (!CanWriteMatchState() || IsMatchEnded)
            return false;

        if (attacker == null || victim == null || attacker == victim)
            return false;

        if (!attacker.IsAlive || !victim.IsAlive)
            return false;

        if (attacker.IsSameFaction(victim))
            return false;

        var assistants = ResolveAssistPlayers(attacker, victim);

        attacker.ServerAddKill();
        attacker.ServerGrantExperience(killExperienceReward, PaperLegendExperienceSource.Kill);

        for (int i = 0; i < assistants.Count; i++)
            assistants[i].ServerGrantExperience(assistExperienceReward, PaperLegendExperienceSource.Assist);

        victim.ServerEliminate(attacker);
        GameScoreManager.Instance?.RegisterKill(attacker, victim);

        AddFactionKill(attacker.FactionId);

        if (attacker.Team == PaperLegendTeam.TeamA)
            SetTeamAKills(TeamAKills + 1);
        else if (attacker.Team == PaperLegendTeam.TeamB)
            SetTeamBKills(TeamBKills + 1);

        CheckKillLimit(attacker);
        return true;
    }

    public void ReportScoreLimitWinner(PaperLegendCharacterNetworkHandler winner)
    {
        if (!CanWriteMatchState() || IsMatchEnded || winner == null)
            return;

        EndMatch(winner);
    }

    public void ApplyBaseDamage(PaperLegendTeam targetTeam, int damage)
    {
        if (!CanWriteMatchState() || IsMatchEnded)
            return;

        if (!enableBaseObjectiveWin)
            return;

        damage = Mathf.Max(0, damage);
        if (damage == 0)
            return;

        if (targetTeam == PaperLegendTeam.TeamA)
        {
            SetTeamABaseHealth(Mathf.Max(0, TeamABaseHealth - damage));
            if (TeamABaseHealth == 0)
                EndMatch(PaperLegendTeam.TeamB);
        }
        else if (targetTeam == PaperLegendTeam.TeamB)
        {
            SetTeamBBaseHealth(Mathf.Max(0, TeamBBaseHealth - damage));
            if (TeamBBaseHealth == 0)
                EndMatch(PaperLegendTeam.TeamA);
        }
    }

    public IReadOnlyList<PaperLegendCharacterNetworkHandler> GetRegisteredPlayers()
    {
        PruneNullPlayers();
        return _players;
    }

    public int GetFactionKillCount(int factionId)
    {
        switch (factionId)
        {
            case 1:
                return Faction1Kills;
            case 2:
                return Faction2Kills;
            case 3:
                return Faction3Kills;
            case 4:
                return Faction4Kills;
            default:
                return 0;
        }
    }

    private void AddFactionKill(int factionId)
    {
        switch (factionId)
        {
            case 1:
                SetFaction1Kills(Faction1Kills + 1);
                break;
            case 2:
                SetFaction2Kills(Faction2Kills + 1);
                break;
            case 3:
                SetFaction3Kills(Faction3Kills + 1);
                break;
            case 4:
                SetFaction4Kills(Faction4Kills + 1);
                break;
        }
    }

    private List<PaperLegendCharacterNetworkHandler> ResolveAssistPlayers(
        PaperLegendCharacterNetworkHandler attacker,
        PaperLegendCharacterNetworkHandler victim)
    {
        var assistants = new List<PaperLegendCharacterNetworkHandler>();
        if (!grantAssistExperienceByProximity || assistExperienceReward <= 0 || assistRadius <= 0f)
            return assistants;

        if (victim == null)
            return assistants;

        PruneNullPlayers();
        float assistSqrRadius = assistRadius * assistRadius;
        Vector3 victimPosition = victim.transform.position;

        for (int i = 0; i < _players.Count; i++)
        {
            var candidate = _players[i];
            if (candidate == null || candidate == attacker || candidate == victim || !candidate.IsAlive)
                continue;

            if (candidate.IsSameFaction(victim))
                continue;

            float sqrDistance = (candidate.transform.position - victimPosition).sqrMagnitude;
            if (sqrDistance <= assistSqrRadius)
                assistants.Add(candidate);
        }

        return assistants;
    }

    private Transform ResolveSafestRespawnPoint(
        IReadOnlyList<Transform> validPoints,
        PaperLegendCharacterNetworkHandler requester,
        PaperLegendCharacterNetworkHandler killer,
        bool hasDeathPosition,
        Vector3 deathPosition)
    {
        if (validPoints == null || validPoints.Count == 0)
            return null;

        var candidates = new List<RespawnCandidate>(validPoints.Count);
        for (int i = 0; i < validPoints.Count; i++)
        {
            Transform point = validPoints[i];
            if (point == null)
                continue;

            RespawnCandidate candidate = EvaluateRespawnPoint(point, requester, killer, hasDeathPosition, deathPosition);
            candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return null;

        candidates.Sort(CompareRespawnCandidates);

        int preferredCount = Mathf.Clamp(safeRespawnCandidateCount, 1, candidates.Count);
        var preferredCandidates = new List<RespawnCandidate>(preferredCount);
        for (int i = 0; i < preferredCount; i++)
            preferredCandidates.Add(candidates[i]);

        RespawnCandidate selected = SelectRespawnCandidate(preferredCandidates);
        if (selected.Point == null)
            return null;

        Debug.Log($"[PaperLegends][Respawn] Selected respawn point '{selected.Point.name}' for player={requester?.PlayerId ?? 0}: nearest={selected.NearestPlayerDistance:0.00}, killer={selected.KillerDistance:0.00}, deathSpot={selected.DeathPositionDistance:0.00}, occupied={selected.IsOccupied}, nearKiller={selected.IsNearKiller}, nearDeathSpot={selected.IsNearDeathPosition}, reused={selected.IsRecentlyUsedByRequester}, candidates={candidates.Count}.");
        return selected.Point;
    }

    private static RespawnCandidate SelectRespawnCandidate(IReadOnlyList<RespawnCandidate> preferredCandidates)
    {
        if (preferredCandidates == null || preferredCandidates.Count == 0)
            return default;

        for (int i = 0; i < preferredCandidates.Count; i++)
        {
            var candidate = preferredCandidates[i];
            if (!candidate.IsOccupied
                && !candidate.IsNearKiller
                && !candidate.IsNearDeathPosition
                && !candidate.IsRecentlyUsedByRequester)
                return candidate;
        }

        for (int i = 0; i < preferredCandidates.Count; i++)
        {
            var candidate = preferredCandidates[i];
            if (!candidate.IsOccupied
                && !candidate.IsNearKiller
                && !candidate.IsNearDeathPosition)
                return candidate;
        }

        for (int i = 0; i < preferredCandidates.Count; i++)
        {
            var candidate = preferredCandidates[i];
            if (!candidate.IsOccupied && !candidate.IsNearKiller)
                return candidate;
        }

        for (int i = 0; i < preferredCandidates.Count; i++)
        {
            var candidate = preferredCandidates[i];
            if (!candidate.IsOccupied)
                return candidate;
        }

        return preferredCandidates[0];
    }

    private RespawnCandidate EvaluateRespawnPoint(
        Transform point,
        PaperLegendCharacterNetworkHandler requester,
        PaperLegendCharacterNetworkHandler killer,
        bool hasDeathPosition,
        Vector3 deathPosition)
    {
        var candidate = new RespawnCandidate
        {
            Point = point,
            NearestPlayerDistance = float.PositiveInfinity,
            KillerDistance = float.PositiveInfinity,
            DeathPositionDistance = float.PositiveInfinity,
            PreviousRespawnDistance = float.PositiveInfinity,
            TotalPlayerDistance = 0f,
            AlivePlayerCount = 0,
            IsOccupied = false
        };

        if (point == null)
            return candidate;

        float sqrRadius = respawnOccupancyRadius * respawnOccupancyRadius;
        Vector3 pointPosition = point.position;

        for (int i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            if (player == null || player == requester || !player.IsAlive)
                continue;

            float distance = ResolveRespawnDistanceToPlayer(pointPosition, player);
            candidate.NearestPlayerDistance = Mathf.Min(candidate.NearestPlayerDistance, distance);
            candidate.TotalPlayerDistance += distance;
            candidate.AlivePlayerCount++;

            if (respawnOccupancyRadius > 0f && distance * distance <= sqrRadius)
                candidate.IsOccupied = true;
        }

        if (candidate.AlivePlayerCount == 0)
            candidate.NearestPlayerDistance = 9999f;

        if (killer != null && killer != requester && killer.IsAlive)
        {
            candidate.KillerDistance = ResolveRespawnDistanceToPlayer(pointPosition, killer);
            candidate.IsNearKiller = respawnKillerAvoidRadius > 0f
                && candidate.KillerDistance <= respawnKillerAvoidRadius;
        }

        if (hasDeathPosition)
        {
            candidate.DeathPositionDistance = ResolveHorizontalDistance(pointPosition, deathPosition);
            candidate.IsNearDeathPosition = respawnDeathPositionAvoidRadius > 0f
                && candidate.DeathPositionDistance <= respawnDeathPositionAvoidRadius;
        }

        if (requester != null
            && _lastRespawnPointByPlayerId.TryGetValue(requester.PlayerId, out Transform previousPoint)
            && previousPoint != null)
        {
            candidate.PreviousRespawnDistance = ResolveHorizontalDistance(pointPosition, previousPoint.position);
            candidate.IsRecentlyUsedByRequester = respawnReuseAvoidRadius > 0f
                && candidate.PreviousRespawnDistance <= respawnReuseAvoidRadius;
        }

        return candidate;
    }

    private static int CompareRespawnCandidates(RespawnCandidate left, RespawnCandidate right)
    {
        if (left.IsOccupied != right.IsOccupied)
            return left.IsOccupied ? 1 : -1;

        if (left.IsNearKiller != right.IsNearKiller)
            return left.IsNearKiller ? 1 : -1;

        if (left.IsNearDeathPosition != right.IsNearDeathPosition)
            return left.IsNearDeathPosition ? 1 : -1;

        if (left.IsRecentlyUsedByRequester != right.IsRecentlyUsedByRequester)
            return left.IsRecentlyUsedByRequester ? 1 : -1;

        int nearestComparison = right.NearestPlayerDistance.CompareTo(left.NearestPlayerDistance);
        if (nearestComparison != 0)
            return nearestComparison;

        int killerComparison = right.KillerDistance.CompareTo(left.KillerDistance);
        if (killerComparison != 0)
            return killerComparison;

        int deathSpotComparison = right.DeathPositionDistance.CompareTo(left.DeathPositionDistance);
        if (deathSpotComparison != 0)
            return deathSpotComparison;

        float leftAverage = left.AlivePlayerCount > 0 ? left.TotalPlayerDistance / left.AlivePlayerCount : left.NearestPlayerDistance;
        float rightAverage = right.AlivePlayerCount > 0 ? right.TotalPlayerDistance / right.AlivePlayerCount : right.NearestPlayerDistance;
        return rightAverage.CompareTo(leftAverage);
    }

    private static float ResolveRespawnDistanceToPlayer(Vector3 pointPosition, PaperLegendCharacterNetworkHandler player)
    {
        if (player == null)
            return float.PositiveInfinity;

        Vector3 centerDelta = player.transform.position - pointPosition;
        centerDelta.y = 0f;
        float centerDistance = centerDelta.magnitude;

        Bounds bounds = player.GetWorldBounds();
        Vector3 closestPoint = bounds.ClosestPoint(pointPosition);
        Vector3 boundsDelta = closestPoint - pointPosition;
        boundsDelta.y = 0f;
        float boundsDistance = boundsDelta.magnitude;

        return Mathf.Min(centerDistance, boundsDistance);
    }

    private static float ResolveHorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 delta = a - b;
        delta.y = 0f;
        return delta.magnitude;
    }

    private struct RespawnCandidate
    {
        public Transform Point;
        public float NearestPlayerDistance;
        public float KillerDistance;
        public float DeathPositionDistance;
        public float PreviousRespawnDistance;
        public float TotalPlayerDistance;
        public int AlivePlayerCount;
        public bool IsOccupied;
        public bool IsNearKiller;
        public bool IsNearDeathPosition;
        public bool IsRecentlyUsedByRequester;
    }

    private void CheckKillLimit(PaperLegendCharacterNetworkHandler attacker)
    {
        if (attacker != null && attacker.KillCount >= killLimit)
        {
            EndMatch(attacker);
            return;
        }

        if (!enableBaseObjectiveWin)
            return;

        if (TeamAKills >= killLimit)
            EndMatch(PaperLegendTeam.TeamA);
        else if (TeamBKills >= killLimit)
            EndMatch(PaperLegendTeam.TeamB);
    }

    private void EndMatch(PaperLegendCharacterNetworkHandler winner)
    {
        if (winner == null || IsMatchEnded)
            return;

        SetWinningPlayerId(winner.PlayerId);
        SetWinningFactionId(winner.FactionId);
        SetWinningTeam(winner.Team);
        SetIsMatchEnded(true);
        Debug.Log($"[PaperLegends] Match ended. Winner player={WinningPlayerId}, faction={WinningFactionId}, team={WinningTeam}");
        BeginEndGameBroadcast(WinningPlayerId);
    }

    private void EndMatch(PaperLegendTeam winningTeam)
    {
        if (IsMatchEnded)
            return;

        SetWinningTeam(winningTeam);
        SetWinningPlayerId(0);
        SetWinningFactionId(0);
        SetIsMatchEnded(true);
        Debug.Log($"[PaperLegends] Match ended. Winner: {winningTeam}");
        BeginEndGameBroadcast(WinningPlayerId);
    }

    private void BeginEndGameBroadcast(int winnerPlayerId)
    {
        if (!CanWriteMatchState() || _isBroadcastingEndGame)
            return;

        StartCoroutine(EndGameBroadcastRoutine(winnerPlayerId));
    }

    private IEnumerator EndGameBroadcastRoutine(int winnerPlayerId)
    {
        _isBroadcastingEndGame = true;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[PaperLegends][GameOver] Cannot broadcast results because NetworkObjectManager is missing.");
            _isBroadcastingEndGame = false;
            yield break;
        }

        if (manager.HasStateAuthority)
            manager.StatusLoading = StatusLoadingGame.EndGame;

        var results = BuildEndGameResults(winnerPlayerId, manager);
        if (results.Count == 0)
        {
            Debug.LogWarning("[PaperLegends][GameOver] Cannot broadcast results because no player results were built.");
            _isBroadcastingEndGame = false;
            yield break;
        }

#if UNITY_SERVER
        var legacyHost = GameSessionNetWork_Host.Instance;
        if (legacyHost != null)
        {
            legacyHost.LastOverGameResults.Clear();
            legacyHost.LastOverGameResults.AddRange(results);
            legacyHost.BeginAwaitClientGameOverAcks(results.Select(x => x.playerId));
            legacyHost.BeginAwaitClientDisconnectReadiness(results.Select(x => x.playerId));
        }
#endif

        string json = JsonHelper.ToJson(results);
        Debug.Log($"[PaperLegends][GameOver] Broadcasting results. winnerPlayerId={winnerPlayerId}, count={results.Count}, payload={json.Length} chars.");
        manager.RpcShowOverGameResult(json);

#if UNITY_SERVER
        yield return PostEndGameResultsToApi(results);
#endif

        _isBroadcastingEndGame = false;
    }

    private List<OverGameRequest> BuildEndGameResults(int winnerPlayerId, NetworkObjectManager manager)
    {
        PruneNullPlayers();

        var results = new List<OverGameRequest>();
        var charactersByPlayerId = new Dictionary<int, PaperLegendCharacterNetworkHandler>();
        for (int i = 0; i < _players.Count; i++)
        {
            var character = _players[i];
            if (character == null || character.PlayerId <= 0 || charactersByPlayerId.ContainsKey(character.PlayerId))
                continue;

            charactersByPlayerId.Add(character.PlayerId, character);
        }

        var orderedInfos = manager != null ? manager.GetOrderedPlayerInfos() : new List<PlayerInfoStruct>();
        if (orderedInfos.Count == 0)
        {
            orderedInfos = _players
                .Where(player => player != null && player.PlayerId > 0)
                .OrderBy(player => player.FactionId)
                .Select(player => new PlayerInfoStruct
                {
                    playerId = player.PlayerId,
                    turnOrder = Mathf.Max(0, player.FactionId - 1),
                    score = player.KillCount,
                    fullname = $"Player {player.PlayerId}",
                    avatarUrl = string.Empty
                })
                .ToList();
        }

        int maxPlayers = ResolveMaxPlayers(manager, orderedInfos.Count);
        int typeMatchGid = manager != null ? (int)manager.rpgRoomModel.TypeMatch : (int)TypeMatchGid.MatchRandomNormal;
        string mapGame = manager != null ? manager.rpgRoomModel.gameScene.Value : string.Empty;
        int rounds = Mathf.Max(1, CurrentKillLimit);

        for (int i = 0; i < orderedInfos.Count; i++)
        {
            var info = orderedInfos[i];
            if (info.playerId <= 0)
                continue;

            charactersByPlayerId.TryGetValue(info.playerId, out var character);
            bool isWinner = info.playerId == winnerPlayerId;
            int killCount = character != null ? character.KillCount : Mathf.Max(0, info.score);
            int deathCount = character != null ? character.DeathCount : 0;
            int expGained = character != null ? character.TotalExperience : 0;

            results.Add(new OverGameRequest
            {
                playerId = info.playerId,
                tunrOrder = info.turnOrder >= 0 ? info.turnOrder : i,
                typeMatchGid = typeMatchGid,
                StatusWin = isWinner ? (int)StatusWin.Win : (int)StatusWin.Lose,
                rounds = rounds,
                MapGame = mapGame,
                MaxPlayer = maxPlayers,
                marbBet = 0,
                marblesWon = killCount,
                marblesLost = deathCount,
                expGained = expGained,
                playerName = ResolvePlayerName(info, character),
                description = isWinner
                    ? $"Paper Legends FFA winner: first to {CurrentKillLimit} kills."
                    : $"Paper Legends FFA ended. Winner playerId={winnerPlayerId}.",
                avatarUrl = info.avatarUrl.ToString()
            });
        }

        return results
            .OrderByDescending(result => result.StatusWin == (int)StatusWin.Win)
            .ThenBy(result => result.tunrOrder)
            .ToList();
    }

    private static int ResolveMaxPlayers(NetworkObjectManager manager, int fallbackCount)
    {
        if (manager != null && manager.rpgRoomModel.MaxPlayer > 0)
            return manager.rpgRoomModel.MaxPlayer;

        return Mathf.Max(1, fallbackCount);
    }

    private static string ResolvePlayerName(PlayerInfoStruct info, PaperLegendCharacterNetworkHandler character)
    {
        string name = info.fullname.ToString();
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        int playerId = character != null ? character.PlayerId : info.playerId;
        return playerId > 0 ? $"Player {playerId}" : "Player";
    }

#if UNITY_SERVER
    private IEnumerator PostEndGameResultsToApi(List<OverGameRequest> results)
    {
        if (APIManager.Instance == null || results == null || results.Count == 0)
            yield break;

        var botController = BotPlayerController.Instance;
        var postPayload = results
            .Where(result => result != null
                && result.playerId > 0
                && (botController == null || !botController.IsBotPlayer(result.playerId)))
            .ToList();

        if (postPayload.Count == 0)
            yield break;

        var postTask = APIManager.Instance.PostOverGame(postPayload);
        yield return StartCoroutine(APIManager.Instance.RunTask(postTask, _ => { }));
    }
#endif

    private void ResetMatchState()
    {
        SetTeamAKills(0);
        SetTeamBKills(0);
        SetFaction1Kills(0);
        SetFaction2Kills(0);
        SetFaction3Kills(0);
        SetFaction4Kills(0);
        SetTeamABaseHealth(baseMaxHealth);
        SetTeamBBaseHealth(baseMaxHealth);
        SetWinningTeam(PaperLegendTeam.None);
        SetWinningPlayerId(0);
        SetWinningFactionId(0);
        SetIsMatchEnded(false);
        SetCurrentKillLimit(Mathf.Max(1, killLimit));
        SetBaseObjectiveWinEnabled(enableBaseObjectiveWin);
    }

    private void SetTeamAKills(int value)
    {
        if (UsesNetworkState) NetTeamAKills = value;
        else _teamAKills = value;
    }

    private void SetTeamBKills(int value)
    {
        if (UsesNetworkState) NetTeamBKills = value;
        else _teamBKills = value;
    }

    private void SetFaction1Kills(int value)
    {
        if (UsesNetworkState) NetFaction1Kills = value;
        else _faction1Kills = value;
    }

    private void SetFaction2Kills(int value)
    {
        if (UsesNetworkState) NetFaction2Kills = value;
        else _faction2Kills = value;
    }

    private void SetFaction3Kills(int value)
    {
        if (UsesNetworkState) NetFaction3Kills = value;
        else _faction3Kills = value;
    }

    private void SetFaction4Kills(int value)
    {
        if (UsesNetworkState) NetFaction4Kills = value;
        else _faction4Kills = value;
    }

    private void SetTeamABaseHealth(int value)
    {
        if (UsesNetworkState) NetTeamABaseHealth = value;
        else _teamABaseHealth = value;
    }

    private void SetTeamBBaseHealth(int value)
    {
        if (UsesNetworkState) NetTeamBBaseHealth = value;
        else _teamBBaseHealth = value;
    }

    private void SetWinningTeam(PaperLegendTeam value)
    {
        if (UsesNetworkState) NetWinningTeam = value;
        else _winningTeam = value;
    }

    private void SetWinningPlayerId(int value)
    {
        if (UsesNetworkState) NetWinningPlayerId = value;
        else _winningPlayerId = value;
    }

    private void SetWinningFactionId(int value)
    {
        if (UsesNetworkState) NetWinningFactionId = value;
        else _winningFactionId = value;
    }

    private void SetIsMatchEnded(bool value)
    {
        if (UsesNetworkState) NetIsMatchEnded = value;
        else _isMatchEnded = value;
    }

    private void SetCurrentKillLimit(int value)
    {
        if (UsesNetworkState) NetCurrentKillLimit = value;
        else _currentKillLimit = value;
    }

    private void SetBaseObjectiveWinEnabled(bool value)
    {
        if (UsesNetworkState) NetBaseObjectiveWinEnabled = value;
        else _baseObjectiveWinEnabled = value;
    }

    private bool CanWriteMatchState()
    {
        return !UsesNetworkState || HasStateAuthority;
    }

    private void PruneNullPlayers()
    {
        for (int i = _players.Count - 1; i >= 0; i--)
        {
            if (_players[i] == null)
                _players.RemoveAt(i);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
