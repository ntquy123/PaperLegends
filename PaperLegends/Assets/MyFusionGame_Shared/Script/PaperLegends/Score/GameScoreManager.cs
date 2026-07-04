using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class GameScoreManager : NetworkBehaviour
{
    public const int MaxTrackedPlayers = 4;

    public static GameScoreManager Instance { get; private set; }

    [Header("Score Rules")]
    [SerializeField, Min(1)] private int targetScore = 30;
    [SerializeField, Min(0)] private int killScore = 2;
    [SerializeField, Min(0)] private int assistScore = 1;
    [SerializeField, Min(0)] private int drumCaptureScore = 3;
    [SerializeField, Min(0)] private int leaderKillBaseBonus = 1;
    [SerializeField, Min(1)] private int leaderKillScoreGapStep = 5;
    [SerializeField, Min(0)] private int leaderKillMaxGapBonus = 4;

    [Header("Assist")]
    [SerializeField, Min(0.1f)] private float assistWindowSeconds = 5f;

    [Header("Drum War")]
    [SerializeField, Min(1)] private int drumWarActivationScore = 24;

    [Networked, Capacity(MaxTrackedPlayers)]
    public NetworkArray<PlayerScoreData> PlayerScores => default;

    [Networked, OnChangedRender(nameof(OnScoreStateChanged))] public PlayerRef CurrentLeader { get; private set; }
    [Networked] public int CurrentLeaderPlayerId { get; private set; }
    [Networked] public int ScoreRevision { get; private set; }
    [Networked] public int ScoreEventSequence { get; private set; }
    [Networked] public NetworkBool IsGameEnded { get; private set; }
    [Networked, OnChangedRender(nameof(OnScoreStateChanged))] public NetworkBool IsDrumWarActive { get; private set; }
    [Networked] public PlayerRef Winner { get; private set; }
    [Networked] public int WinnerPlayerId { get; private set; }
    [Networked] public int TargetScore { get; private set; }
    [Networked] public int DrumWarActivationScore { get; private set; }
    [Networked] public NetworkBool DrumObjectiveIsActive { get; private set; }
    [Networked] public NetworkBool DrumObjectiveIsWarning { get; private set; }
    [Networked] public int DrumObjectiveCapturingPlayerId { get; private set; }
    [Networked] public float DrumObjectiveCaptureProgress01 { get; private set; }
    [Networked] public int DrumObjectiveAlertTick { get; private set; }

    private readonly Dictionary<int, PaperLegendCharacterNetworkHandler> _charactersByPlayerId =
        new Dictionary<int, PaperLegendCharacterNetworkHandler>();

    private readonly Dictionary<int, List<DamageRecord>> _damageHistoryByVictimId =
        new Dictionary<int, List<DamageRecord>>();

    public int KillScore => killScore;
    public int AssistScore => assistScore;
    public int DrumCaptureScore => drumCaptureScore;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PaperLegends][Score] Multiple GameScoreManager instances detected.");
            return;
        }

        Instance = this;
    }

    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        ResetScoreState();
        RegisterExistingCharacters();
    }

    public void RegisterPlayer(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null || !HasStateAuthority)
            return;

        int playerId = character.PlayerId;
        if (playerId <= 0)
            return;

        _charactersByPlayerId[playerId] = character;
        EnsureScoreEntry(character.Object != null ? character.Object.InputAuthority : PlayerRef.None, playerId);
    }

    public void UnregisterPlayer(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null || !HasStateAuthority)
            return;

        if (character.PlayerId > 0)
            _charactersByPlayerId.Remove(character.PlayerId);
    }

    public void RecordDamage(PaperLegendCharacterNetworkHandler attacker, PaperLegendCharacterNetworkHandler victim)
    {
        if (!HasStateAuthority || attacker == null || victim == null || attacker == victim)
            return;

        if (attacker.PlayerId <= 0 || victim.PlayerId <= 0)
            return;

        RecordDamage(attacker.Object != null ? attacker.Object.InputAuthority : PlayerRef.None, attacker.PlayerId, victim.PlayerId);
    }

    public void RecordDamage(PlayerRef attacker, PlayerRef victim)
    {
        if (!TryFindScoreIndex(attacker, 0, out int attackerIndex) ||
            !TryFindScoreIndex(victim, 0, out int victimIndex))
        {
            return;
        }

        int attackerPlayerId = PlayerScores.Get(attackerIndex).PlayerId;
        int victimPlayerId = PlayerScores.Get(victimIndex).PlayerId;
        RecordDamage(attacker, attackerPlayerId, victimPlayerId);
    }

    public void RegisterKill(PaperLegendCharacterNetworkHandler attacker, PaperLegendCharacterNetworkHandler victim)
    {
        if (!HasStateAuthority || attacker == null || victim == null)
            return;

        RegisterKill(
            attacker.Object != null ? attacker.Object.InputAuthority : PlayerRef.None,
            attacker.PlayerId,
            victim.Object != null ? victim.Object.InputAuthority : PlayerRef.None,
            victim.PlayerId);
    }

    public void RegisterKill(PlayerRef attacker, PlayerRef victim)
    {
        if (!HasStateAuthority)
            return;

        if (!TryFindScoreIndex(attacker, 0, out int attackerIndex) ||
            !TryFindScoreIndex(victim, 0, out int victimIndex))
        {
            return;
        }

        PlayerScoreData attackerData = PlayerScores.Get(attackerIndex);
        PlayerScoreData victimData = PlayerScores.Get(victimIndex);
        RegisterKill(attacker, attackerData.PlayerId, victim, victimData.PlayerId);
    }

    public void RegisterAssist(PaperLegendCharacterNetworkHandler assistPlayer, PaperLegendCharacterNetworkHandler victim)
    {
        if (!HasStateAuthority || assistPlayer == null || victim == null)
            return;

        RegisterAssist(
            assistPlayer.Object != null ? assistPlayer.Object.InputAuthority : PlayerRef.None,
            assistPlayer.PlayerId,
            victim.Object != null ? victim.Object.InputAuthority : PlayerRef.None,
            victim.PlayerId);
    }

    public void RegisterAssist(PlayerRef assistPlayer, PlayerRef victim)
    {
        if (!HasStateAuthority)
            return;

        if (!TryFindScoreIndex(assistPlayer, 0, out int assistIndex) ||
            !TryFindScoreIndex(victim, 0, out int victimIndex))
        {
            return;
        }

        PlayerScoreData assistData = PlayerScores.Get(assistIndex);
        PlayerScoreData victimData = PlayerScores.Get(victimIndex);
        RegisterAssist(assistPlayer, assistData.PlayerId, victim, victimData.PlayerId);
    }

    public void CaptureDrum(PaperLegendCharacterNetworkHandler player)
    {
        if (!HasStateAuthority || player == null)
            return;

        CaptureDrum(player.Object != null ? player.Object.InputAuthority : PlayerRef.None, player.PlayerId);
    }

    public void CaptureDrum(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        if (!TryFindScoreIndex(player, 0, out int index))
            return;

        CaptureDrum(player, PlayerScores.Get(index).PlayerId);
    }

    public bool TryGetScore(PlayerRef player, out PlayerScoreData scoreData)
    {
        if (TryFindScoreIndex(player, 0, out int index))
        {
            scoreData = PlayerScores.Get(index);
            return true;
        }

        scoreData = default;
        return false;
    }

    public bool TryGetScoreByPlayerId(int playerId, out PlayerScoreData scoreData)
    {
        if (TryFindScoreIndex(PlayerRef.None, playerId, out int index))
        {
            scoreData = PlayerScores.Get(index);
            return true;
        }

        scoreData = default;
        return false;
    }

    public void SetDrumObjectiveState(
        bool isActive,
        bool isWarning,
        int capturingPlayerId,
        float captureProgress01,
        int alertTick)
    {
        if (!HasStateAuthority)
            return;

        DrumObjectiveIsActive = isActive;
        DrumObjectiveIsWarning = isWarning;
        DrumObjectiveCapturingPlayerId = capturingPlayerId > 0 ? capturingPlayerId : 0;
        DrumObjectiveCaptureProgress01 = Mathf.Clamp01(captureProgress01);
        DrumObjectiveAlertTick = Mathf.Max(0, alertTick);
    }

    public List<PlayerScoreData> GetOrderedScores()
    {
        var scores = new List<PlayerScoreData>(MaxTrackedPlayers);
        for (int i = 0; i < PlayerScores.Length; i++)
        {
            PlayerScoreData data = PlayerScores.Get(i);
            if (data.PlayerId > 0 || data.PlayerRef != PlayerRef.None)
                scores.Add(data);
        }

        scores.Sort((left, right) =>
        {
            int scoreComparison = right.Score.CompareTo(left.Score);
            if (scoreComparison != 0)
                return scoreComparison;

            int sequenceComparison = left.ScoreReachedSequence.CompareTo(right.ScoreReachedSequence);
            if (sequenceComparison != 0)
                return sequenceComparison;

            return left.PlayerId.CompareTo(right.PlayerId);
        });

        return scores;
    }

    private void ResetScoreState()
    {
        TargetScore = Mathf.Max(1, targetScore);
        CurrentLeader = PlayerRef.None;
        CurrentLeaderPlayerId = 0;
        Winner = PlayerRef.None;
        WinnerPlayerId = 0;
        IsGameEnded = false;
        IsDrumWarActive = false;
        DrumObjectiveIsActive = false;
        DrumObjectiveIsWarning = false;
        DrumObjectiveCapturingPlayerId = 0;
        DrumObjectiveCaptureProgress01 = 0f;
        DrumObjectiveAlertTick = 0;
        ScoreRevision = 0;
        ScoreEventSequence = 0;
        DrumWarActivationScore = Mathf.Clamp(drumWarActivationScore, 1, TargetScore);
        _damageHistoryByVictimId.Clear();

        for (int i = 0; i < PlayerScores.Length; i++)
            PlayerScores.Set(i, default);
    }

    private void RegisterExistingCharacters()
    {
        var host = PaperLegendMatchNetworkHost.Instance;
        if (host != null)
        {
            IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host.GetRegisteredPlayers();
            for (int i = 0; i < players.Count; i++)
                RegisterPlayer(players[i]);
        }

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        List<PlayerInfoStruct> orderedInfos = manager.GetOrderedPlayerInfos();
        for (int i = 0; i < orderedInfos.Count; i++)
        {
            PlayerInfoStruct info = orderedInfos[i];
            if (info.playerId > 0)
                EnsureScoreEntry(PlayerRef.None, info.playerId);
        }
    }

    private void RecordDamage(PlayerRef attackerRef, int attackerPlayerId, int victimPlayerId)
    {
        if (!HasStateAuthority || attackerPlayerId <= 0 || victimPlayerId <= 0 || attackerPlayerId == victimPlayerId)
            return;

        if (!_damageHistoryByVictimId.TryGetValue(victimPlayerId, out List<DamageRecord> records))
        {
            records = new List<DamageRecord>(MaxTrackedPlayers);
            _damageHistoryByVictimId[victimPlayerId] = records;
        }

        float now = GetServerTimeSeconds();
        for (int i = records.Count - 1; i >= 0; i--)
        {
            if (now - records[i].TimeSeconds > assistWindowSeconds)
                records.RemoveAt(i);
        }

        int existingIndex = records.FindIndex(record => record.AttackerPlayerId == attackerPlayerId);
        var damageRecord = new DamageRecord
        {
            Attacker = attackerRef,
            AttackerPlayerId = attackerPlayerId,
            TimeSeconds = now
        };

        if (existingIndex >= 0)
            records[existingIndex] = damageRecord;
        else
            records.Add(damageRecord);
    }

    private void RegisterKill(PlayerRef attackerRef, int attackerPlayerId, PlayerRef victimRef, int victimPlayerId)
    {
        if (IsGameEnded || attackerPlayerId <= 0 || victimPlayerId <= 0 || attackerPlayerId == victimPlayerId)
            return;

        EnsureScoreEntry(attackerRef, attackerPlayerId);
        EnsureScoreEntry(victimRef, victimPlayerId);

        bool killedLeader = IsCurrentLeader(victimRef, victimPlayerId);
        int leaderBonus = killedLeader ? CalculateLeaderKillBonus(attackerPlayerId, victimPlayerId) : 0;
        AddScore(attackerRef, attackerPlayerId, killScore + leaderBonus, killDelta: 1, assistDelta: 0, deathDelta: 0);

        List<DamageRecord> assists = ResolveAssistRecords(attackerPlayerId, victimPlayerId);
        for (int i = 0; i < assists.Count; i++)
            RegisterAssist(assists[i].Attacker, assists[i].AttackerPlayerId, victimRef, victimPlayerId);

        AddScore(victimRef, victimPlayerId, 0, killDelta: 0, assistDelta: 0, deathDelta: 1);
        _damageHistoryByVictimId.Remove(victimPlayerId);

        Debug.Log($"[PaperLegends][Score] kill attacker={attackerPlayerId} victim={victimPlayerId} leaderBonus={leaderBonus} assists={assists.Count}");
    }

    private void RegisterAssist(PlayerRef assistRef, int assistPlayerId, PlayerRef victimRef, int victimPlayerId)
    {
        if (IsGameEnded || assistPlayerId <= 0 || victimPlayerId <= 0 || assistPlayerId == victimPlayerId)
            return;

        EnsureScoreEntry(assistRef, assistPlayerId);
        EnsureScoreEntry(victimRef, victimPlayerId);
        AddScore(assistRef, assistPlayerId, assistScore, killDelta: 0, assistDelta: 1, deathDelta: 0);

        Debug.Log($"[PaperLegends][Score] assist player={assistPlayerId} victim={victimPlayerId}");
    }

    private void CaptureDrum(PlayerRef playerRef, int playerId)
    {
        if (IsGameEnded || playerId <= 0)
            return;

        EnsureScoreEntry(playerRef, playerId);
        AddScore(playerRef, playerId, drumCaptureScore, killDelta: 0, assistDelta: 0, deathDelta: 0);
        Debug.Log($"[PaperLegends][Score] drum captured by player={playerId}");
    }

    private void AddScore(PlayerRef playerRef, int playerId, int scoreDelta, int killDelta, int assistDelta, int deathDelta)
    {
        if (!TryFindScoreIndex(playerRef, playerId, out int index))
            return;

        PlayerScoreData data = PlayerScores.Get(index);
        int oldScore = data.Score;

        data.Score = Mathf.Max(0, data.Score + Mathf.Max(0, scoreDelta));
        data.Kills = Mathf.Max(0, data.Kills + Mathf.Max(0, killDelta));
        data.Assists = Mathf.Max(0, data.Assists + Mathf.Max(0, assistDelta));
        data.Deaths = Mathf.Max(0, data.Deaths + Mathf.Max(0, deathDelta));

        if (data.Score != oldScore)
            data.ScoreReachedSequence = NextScoreEventSequence();

        PlayerScores.Set(index, data);
        BumpScoreRevision();
        RefreshLeader();
        CheckDrumWarActivation(data);
        CheckTargetScore(data);
    }

    private void CheckDrumWarActivation(PlayerScoreData changedData)
    {
        if (IsDrumWarActive || changedData.Score < DrumWarActivationScore)
            return;

        IsDrumWarActive = true;
        BumpScoreRevision();
        Debug.Log($"[PaperLegends][Score] Drum War phase started by player={changedData.PlayerId} score={changedData.Score}/{TargetScore}.");
    }

    private int CalculateLeaderKillBonus(int attackerPlayerId, int leaderPlayerId)
    {
        int attackerScore = TryGetScoreByPlayerId(attackerPlayerId, out PlayerScoreData attackerData) ? attackerData.Score : 0;
        int leaderScore = TryGetScoreByPlayerId(leaderPlayerId, out PlayerScoreData leaderData) ? leaderData.Score : 0;
        int scoreGap = Mathf.Max(0, leaderScore - attackerScore);
        int gapBonus = leaderKillScoreGapStep > 0 ? scoreGap / leaderKillScoreGapStep : 0;
        gapBonus = Mathf.Clamp(gapBonus, 0, leaderKillMaxGapBonus);
        return leaderKillBaseBonus + gapBonus;
    }

    private List<DamageRecord> ResolveAssistRecords(int attackerPlayerId, int victimPlayerId)
    {
        var assists = new List<DamageRecord>(MaxTrackedPlayers);
        if (!_damageHistoryByVictimId.TryGetValue(victimPlayerId, out List<DamageRecord> records))
            return assists;

        float now = GetServerTimeSeconds();
        for (int i = 0; i < records.Count; i++)
        {
            DamageRecord record = records[i];
            if (record.AttackerPlayerId <= 0 || record.AttackerPlayerId == attackerPlayerId)
                continue;

            if (now - record.TimeSeconds <= assistWindowSeconds)
                assists.Add(record);
        }

        return assists;
    }

    private void RefreshLeader()
    {
        PlayerScoreData best = default;
        bool hasBest = false;

        for (int i = 0; i < PlayerScores.Length; i++)
        {
            PlayerScoreData candidate = PlayerScores.Get(i);
            if (candidate.PlayerId <= 0 && candidate.PlayerRef == PlayerRef.None)
                continue;

            if (!hasBest || IsBetterLeaderCandidate(candidate, best))
            {
                best = candidate;
                hasBest = true;
            }
        }

        CurrentLeader = hasBest ? best.PlayerRef : PlayerRef.None;
        CurrentLeaderPlayerId = hasBest ? best.PlayerId : 0;
    }

    private static bool IsBetterLeaderCandidate(PlayerScoreData candidate, PlayerScoreData best)
    {
        if (candidate.Score != best.Score)
            return candidate.Score > best.Score;

        if (candidate.ScoreReachedSequence != best.ScoreReachedSequence)
            return candidate.ScoreReachedSequence < best.ScoreReachedSequence;

        return candidate.PlayerId < best.PlayerId;
    }

    private void CheckTargetScore(PlayerScoreData changedData)
    {
        if (IsGameEnded || changedData.Score < TargetScore)
            return;

        IsGameEnded = true;
        Winner = changedData.PlayerRef;
        WinnerPlayerId = changedData.PlayerId;
        BumpScoreRevision();

        if (_charactersByPlayerId.TryGetValue(changedData.PlayerId, out PaperLegendCharacterNetworkHandler winnerCharacter))
            PaperLegendMatchNetworkHost.Instance?.ReportScoreLimitWinner(winnerCharacter);

        Debug.Log($"[PaperLegends][Score] match ended by score. winner={WinnerPlayerId} score={changedData.Score}/{TargetScore}");
    }

    private bool IsCurrentLeader(PlayerRef playerRef, int playerId)
    {
        if (playerId > 0 && playerId == CurrentLeaderPlayerId)
            return true;

        return playerRef != PlayerRef.None && playerRef == CurrentLeader;
    }

    private bool EnsureScoreEntry(PlayerRef playerRef, int playerId)
    {
        if (playerId <= 0 && playerRef == PlayerRef.None)
            return false;

        if (TryFindScoreIndex(playerRef, playerId, out _))
            return true;

        for (int i = 0; i < PlayerScores.Length; i++)
        {
            PlayerScoreData existing = PlayerScores.Get(i);
            if (existing.PlayerId > 0 || existing.PlayerRef != PlayerRef.None)
                continue;

            var data = new PlayerScoreData
            {
                PlayerRef = playerRef,
                PlayerId = playerId,
                Score = 0,
                Kills = 0,
                Assists = 0,
                Deaths = 0,
                ScoreReachedSequence = NextScoreEventSequence()
            };

            PlayerScores.Set(i, data);
            BumpScoreRevision();
            RefreshLeader();
            return true;
        }

        Debug.LogWarning($"[PaperLegends][Score] Cannot track player={playerId}; capacity={MaxTrackedPlayers} is full.");
        return false;
    }

    private bool TryFindScoreIndex(PlayerRef playerRef, int playerId, out int index)
    {
        for (int i = 0; i < PlayerScores.Length; i++)
        {
            PlayerScoreData data = PlayerScores.Get(i);
            if (data.PlayerId <= 0 && data.PlayerRef == PlayerRef.None)
                continue;

            if (playerId > 0 && data.PlayerId == playerId)
            {
                index = i;
                return true;
            }

            if (playerRef != PlayerRef.None && data.PlayerRef == playerRef)
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    private int NextScoreEventSequence()
    {
        ScoreEventSequence++;
        return ScoreEventSequence;
    }

    private void BumpScoreRevision()
    {
        ScoreRevision++;
    }

    private float GetServerTimeSeconds()
    {
        return Time.time;
    }

    private void OnScoreStateChanged()
    {
        // Hook for Fusion OnChangedRender. UI scripts poll ScoreRevision/leader from this network state.
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private struct DamageRecord
    {
        public PlayerRef Attacker;
        public int AttackerPlayerId;
        public float TimeSeconds;
    }
}
