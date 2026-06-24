using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Unity.VisualScripting;
using UnityEngine;

public class QuickMatchServer : NetworkBehaviour
{
    public static QuickMatchServer Instance { get; private set; }
    private WeakReference<NetworkRunner>? runnerReference;

    [Serializable]
    public struct QuickMatchTicket : INetworkStruct
    {
        public NetworkString<_64> SessionName;

        public QuickMatchTicket(NetworkString<_64> sessionName)
        {
            SessionName = sessionName;
        }

        public QuickMatchTicket(SessionInfo sessionInfo)
        {
            SessionName = sessionInfo.Name;
        }

        public bool IsValid => SessionName != null;

        public override string ToString() => SessionName.ToString();
    }

    [Serializable]
    public struct QuickMatchPlayerState : INetworkStruct
    {
        public int PlayerId;
        public int Status;
    }

    public static class QuickMatchPlayerStatusCodes
    {
        public const int Waiting = 0;
        public const int Ready = 1;
        public const int Cancelled = 2;
    }

    [SerializeField]
    private float readyTimeoutSeconds = 30f;

    [Header("Quick Match Flow")]
    [SerializeField]
    private bool enableReadyPhase = false;

    [SerializeField]
    private int expectedMatchPlayers = 0;

    [SerializeField]
    private int expectedRealPlayers = 0;

    [Header("Paper Legends Character Selection")]
    [SerializeField]
    private bool enablePaperLegendCharacterSelection = true;

    [SerializeField, Min(1f)]
    private float paperLegendCharacterSelectionSeconds = 40f;

    [SerializeField]
    private int[] paperLegendSelectableCharacterIds = new[] { 1, 2, 3, 4 };

    private readonly Dictionary<PlayerRef, bool> readyStates = new();
    private readonly Dictionary<PlayerRef, int> playerUserIds = new();
    private readonly Dictionary<PlayerRef, QuickMatchPlayerState> quickMatchPlayerStates = new();
    private readonly List<PlayerRef> lastMatchPlayers = new();
    private readonly Dictionary<int, PlayerRef> paperLegendSelectionPlayerRefsById = new();
    private readonly Dictionary<int, int> paperLegendSelectionModelsByPlayerId = new();
    private readonly Dictionary<int, int> paperLegendSelectionModelOwners = new();
    private readonly List<int> paperLegendSelectionAvailableModelIds = new();
    private readonly List<int> paperLegendSelectionBotModelIds = new();
    private readonly List<int> paperLegendSelectionRealPlayerIds = new();
    private Coroutine readyCountdownRoutine;
    private Coroutine paperLegendSelectionCountdownRoutine;
    private bool readyPhaseActive;
    private bool matchStarted;
    private bool paperLegendCharacterSelectionActive;
    private bool waitingForAssignedRealPlayers;
    private float waitingForAssignedRealPlayersSinceRealtime = -1f;
    private float paperLegendSelectionEndsAtRealtime = -1f;
    private QuickMatchTicket paperLegendSelectionTicket;

    public bool IsReadyPhaseEnabled => enableReadyPhase;
    public bool IsWaitingForAssignedRealPlayers => waitingForAssignedRealPlayers;
    public float WaitingForAssignedRealPlayersDurationSeconds =>
        waitingForAssignedRealPlayersSinceRealtime < 0f
            ? 0f
            : Mathf.Max(0f, Time.realtimeSinceStartup - waitingForAssignedRealPlayersSinceRealtime);

    public void SetExpectedPlayerCount(int count)
    {
        expectedMatchPlayers = Mathf.Max(0, count);
        Debug.Log($"[QuickMatch] Expected player count set to {expectedMatchPlayers}.");
    }

    public void SetExpectedRealPlayerCount(int count)
    {
        expectedRealPlayers = Mathf.Max(0, count);
        Debug.Log($"[QuickMatch] Expected real player count set to {expectedRealPlayers}.");
    }

    [Networked, Capacity(16)]
    public NetworkLinkedList<QuickMatchPlayerState> QuickMatchPlayers => default;

    public static event Action<QuickMatchTicket>? OnClientMatchReady;
    public static event Action<QuickMatchTicket>? OnClientMatchStarting;
    public static event Action? OnClientQueueCancelled;
    public static event Action? OnClientExitQueue;
    public static event Action<PlayerRef, int, int>? OnClientPlayerReadyStatusChanged;
    public static event Action<string>? OnClientInitializationFailed;
    public static event System.Action<string, string, float>? OnClientPaperLegendCharacterSelectionStarted;
    public static event System.Action<int, int, int, int, float>? OnClientPaperLegendCharacterSelectionUpdated;
    public static event System.Action<string>? OnClientPaperLegendCharacterSelectionCompleted;
    public static event System.Action<int, string>? OnClientPaperLegendCharacterSelectionRejected;

    // Ghi chÃƒÂº: ThiÃ¡ÂºÂ¿t lÃ¡ÂºÂ­p lÃ¡ÂºÂ¡i trÃ¡ÂºÂ¡ng thÃƒÂ¡i hÃƒÂ ng chÃ¡Â»Â khi object server Ã„â€˜Ã†Â°Ã¡Â»Â£c spawn vÃ¡Â»â€ºi quyÃ¡Â»Ân Ã„â€˜iÃ¡Â»Âu khiÃ¡Â»Æ’n.
    public override void Spawned()
    {
        base.Spawned();
        Instance = this;
        if (!RoomRunnerHelper.EnsureRunnerDontDestroyOnLoad(this, gameObject, ref runnerReference))
        {
            Debug.LogWarning($"[QuickMatch] {nameof(QuickMatchServer)} could not be marked DontDestroyOnLoad through the runner. Check room configuration.");
        }
        Debug.Log($"[QuickMatch] Spawned on {(Object.HasStateAuthority ? "server" : "client")} with objectId={Object.Id}.");

        if (Object.HasStateAuthority)
        {
            readyStates.Clear();
            readyPhaseActive = false;
            matchStarted = false;
            expectedRealPlayers = Mathf.Clamp(expectedRealPlayers, 0, Mathf.Max(expectedMatchPlayers, 0));
            playerUserIds.Clear();
            QuickMatchPlayers.Clear();
            quickMatchPlayerStates.Clear();
            lastMatchPlayers.Clear();
            StopPaperLegendCharacterSelection();
            if (!PaperLegendRuntimeState.IsPaperLegendMatch)
                PaperLegendRuntimeState.ClearCharacterSelections();
            ResetAssignedPlayersWaitingState();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Ghi chÃƒÂº: Khi client gÃ¡Â»Â­i yÃƒÂªu cÃ¡ÂºÂ§u tÃƒÂ¬m trÃ¡ÂºÂ­n, lÃ†Â°u thÃƒÂ´ng tin ngÃ†Â°Ã¡Â»Âi chÃ†Â¡i vÃƒÂ  kiÃ¡Â»Æ’m tra Ã„â€˜iÃ¡Â»Âu kiÃ¡Â»â€¡n mÃ¡Â»Å¸ giai Ã„â€˜oÃ¡ÂºÂ¡n sÃ¡ÂºÂµn sÃƒÂ ng.
    internal void HandlePlayerRequestQuickMatch(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        TrackPlayer(player);
        SyncReadyStatesWithRoom();
        TryBeginReadyPhase();
    }

    // Ghi chÃƒÂº: XÃ¡Â»Â­ lÃƒÂ½ trÃ¡ÂºÂ¡ng thÃƒÂ¡i sÃ¡ÂºÂµn sÃƒÂ ng cÃ¡Â»Â§a tÃ¡Â»Â«ng ngÃ†Â°Ã¡Â»Âi chÃ†Â¡i, hÃ¡Â»Â§y hoÃ¡ÂºÂ·c hoÃƒÂ n tÃ¡ÂºÂ¥t khi Ã„â€˜Ã¡Â»Â§ Ã„â€˜iÃ¡Â»Âu kiÃ¡Â»â€¡n.
    internal void HandlePlayerConfirmReady(PlayerRef player, bool ready, int userId)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (!readyStates.ContainsKey(player))
        {
            return;
        }

        readyStates[player] = ready;
        if (ready)
        {
            TrackPlayerUserId(player, userId);
        }
        else
        {
            UntrackPlayerUserId(player);
        }
        BroadcastReadyStatus(player);

        if (!ready)
        {
            CancelReadyPhase();
            return;
        }

        if (AllPlayersReady())
        {
            Debug.Log("All clients are ready.");
            CompleteReadyPhase();
        }
        else
            Debug.Log("Not enough players are ready yet.");
    }

    // Ghi chÃƒÂº: KiÃ¡Â»Æ’m tra phÃƒÂ²ng Ã„â€˜ÃƒÂ£ Ã„â€˜Ã¡Â»Â§ ngÃ†Â°Ã¡Â»Âi vÃƒÂ  phÃƒÂ¡t lÃ¡Â»â€¡nh hiÃ¡Â»Æ’n thÃ¡Â»â€¹ giao diÃ¡Â»â€¡n xÃƒÂ¡c nhÃ¡ÂºÂ­n sÃ¡ÂºÂµn sÃƒÂ ng tÃ¡Â»â€ºi tÃ¡Â»Â«ng ngÃ†Â°Ã¡Â»Âi chÃ†Â¡i.
    private void TryBeginReadyPhase()
    {
        if (matchStarted)
        {
            Debug.Log("[QuickMatch] TryBeginReadyPhase skipped: match already started (reconnecting player?)");
            return;
        }

        if (readyPhaseActive)
        {
            Debug.Log("[QuickMatch] TryBeginReadyPhase skipped: readyPhaseActive=true");
            return;
        }

        if (Runner == null)
        {
            Debug.LogWarning("[QuickMatch] TryBeginReadyPhase skipped: Runner is null");
            return;
        }

        var sessionInfo = Runner.SessionInfo;
        if (!sessionInfo.IsValid)
        {
            Debug.LogWarning("[QuickMatch] TryBeginReadyPhase skipped: SessionInfo is not valid");
            return;
        }

        int expectedPlayers = ResolveExpectedPlayerCount(sessionInfo);
        if (expectedPlayers <= 0)
        {
            Debug.LogWarning($"[QuickMatch] TryBeginReadyPhase skipped: expectedPlayers={expectedPlayers} (expectedMatchPlayers={expectedMatchPlayers}, MaxPlayers={sessionInfo.MaxPlayers}, PlayerCount={sessionInfo.PlayerCount})");
            return;
        }

        int currentPlayers = GetCurrentPlayerCount();
        int registeredPlayers = GetRegisteredPlayerCount();
        int expectedRealPlayerCount = ResolveExpectedRealPlayerCount(expectedPlayers);
        Debug.Log($"[QuickMatch] TryBeginReadyPhase: currentPlayers={currentPlayers}, expectedPlayers={expectedPlayers}, expectedRealPlayers={expectedRealPlayerCount}, readyStates={readyStates.Count}, registered={registeredPlayers}");

        if (registeredPlayers < expectedRealPlayerCount)
        {
            MarkWaitingForAssignedRealPlayers();
            Debug.Log($"[QuickMatch] Waiting for assigned real players ({registeredPlayers}/{expectedRealPlayerCount}) before adding bots. connected={currentPlayers}");
            return;
        }

        ResetAssignedPlayersWaitingState();

        if (registeredPlayers < expectedPlayers)
        {
            Debug.Log($"[QuickMatch] Real players registered enough ({registeredPlayers}/{expectedRealPlayerCount}) but total is not full ({registeredPlayers}/{expectedPlayers}). Starting with {expectedPlayers - registeredPlayers} bot(s).");
            StartMatchImmediately();
            return;
        }

        if (!enableReadyPhase)
        {
            Debug.Log($"[QuickMatch] Room '{sessionInfo.Name}' reached full registered capacity ({registeredPlayers}/{expectedPlayers}). Starting match immediately.");
            StartMatchImmediately();
            return;
        }

        readyPhaseActive = true;
        Debug.Log($"[QuickMatch] Room '{sessionInfo.Name}' reached full registered capacity ({registeredPlayers}/{expectedPlayers}). Initiating ready phase.");

        StartReadyCountdown();
        RPC_BeginReadyPhase(expectedPlayers, readyTimeoutSeconds);
    }

    // Ghi chÃƒÂº: BÃ¡ÂºÂ¯t Ã„â€˜Ã¡ÂºÂ§u Ã„â€˜Ã¡ÂºÂ¿m ngÃ†Â°Ã¡Â»Â£c thÃ¡Â»Âi gian chÃ¡Â»Â tÃ¡ÂºÂ¥t cÃ¡ÂºÂ£ ngÃ†Â°Ã¡Â»Âi chÃ†Â¡i xÃƒÂ¡c nhÃ¡ÂºÂ­n.
    private void StartReadyCountdown()
    {
        StopReadyCountdown();

        if (readyTimeoutSeconds <= 0f)
        {
            return;
        }

        readyCountdownRoutine = StartCoroutine(ReadyCountdownRoutine());
    }

    // Ghi chÃƒÂº: VÃƒÂ²ng lÃ¡ÂºÂ·p theo dÃƒÂµi thÃ¡Â»Âi gian chÃ¡Â»Â, hÃ¡ÂºÂ¿t hÃ¡ÂºÂ¡n sÃ¡ÂºÂ½ tÃ¡Â»Â± Ã„â€˜Ã¡Â»â„¢ng hÃ¡Â»Â§y quÃƒÂ¡ trÃƒÂ¬nh.
    private IEnumerator ReadyCountdownRoutine()
    {
        float remaining = readyTimeoutSeconds;
        while (readyPhaseActive && remaining > 0f)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }

        if (!readyPhaseActive)
        {
            yield break;
        }

        Debug.LogWarning("[QuickMatch] Ready phase timed out. Cancelling quick match.");
        CancelReadyPhase();
    }

    // Ghi chÃƒÂº: DÃ¡Â»Â«ng quÃƒÂ¡ trÃƒÂ¬nh Ã„â€˜Ã¡ÂºÂ¿m ngÃ†Â°Ã¡Â»Â£c khi khÃƒÂ´ng cÃƒÂ²n cÃ¡ÂºÂ§n thiÃ¡ÂºÂ¿t.
    private void StopReadyCountdown()
    {
        if (readyCountdownRoutine != null)
        {
            StopCoroutine(readyCountdownRoutine);
            readyCountdownRoutine = null;
        }
    }

    // Ghi chÃƒÂº: KiÃ¡Â»Æ’m tra xem toÃƒÂ n bÃ¡Â»â„¢ ngÃ†Â°Ã¡Â»Âi chÃ†Â¡i trong phÃƒÂ²ng Ã„â€˜ÃƒÂ£ xÃƒÂ¡c nhÃ¡ÂºÂ­n sÃ¡ÂºÂµn sÃƒÂ ng hay chÃ†Â°a.
    private bool AllPlayersReady()
    {
        if (Runner == null)
        {
            return false;
        }

        int trackedPlayers = readyStates.Count;
        if (trackedPlayers <= 0)
        {
            return false;
        }

        var sessionInfo = Runner.SessionInfo;
        int requiredPlayers = trackedPlayers;

        if (sessionInfo.IsValid)
        {
            if (sessionInfo.PlayerCount > 0)
            {
                requiredPlayers = Math.Min(requiredPlayers, sessionInfo.PlayerCount);
            }

            if (sessionInfo.MaxPlayers > 0)
            {
                requiredPlayers = Math.Min(requiredPlayers, sessionInfo.MaxPlayers);
            }
        }

        if (requiredPlayers <= 0)
        {
            return false;
        }

        int readyCount = readyStates.Count(pair => pair.Value);
        if (readyCount < requiredPlayers)
        {
            return false;
        }

        return readyStates.All(pair => pair.Value);
    }

    private void TrackPlayer(PlayerRef player)
    {
        if (!readyStates.ContainsKey(player))
        {
            readyStates[player] = false;
        }
    }

    private void SyncReadyStatesWithRoom()
    {
        if (Runner == null)
        {
            return;
        }

        foreach (var activePlayer in Runner.ActivePlayers)
        {
            if (!IsClientPlayer(activePlayer))
            {
                continue;
            }

            TrackPlayer(activePlayer);
        }
    }

    private bool IsClientPlayer(PlayerRef player)
    {
        if (Runner == null)
        {
            return !player.IsNone;
        }

        // In dedicated GameMode.Server, LocalPlayer is the synthetic server actor (thÃ†Â°Ã¡Â»Âng id=1024).
        // Exclude it from quick-match client counts and ready-phase logic.
        if (Runner.IsServer && player == Runner.LocalPlayer)
        {
            return false;
        }

        return !player.IsNone;
    }

    private bool IsRoomFull(SessionInfo sessionInfo)
    {
        if (!sessionInfo.IsValid)
        {
            return false;
        }

        int expectedPlayers = ResolveExpectedPlayerCount(sessionInfo);
        if (expectedPlayers <= 0)
        {
            return false;
        }

        return GetCurrentPlayerCount() >= expectedPlayers;
    }

    private int GetCurrentPlayerCount()
    {
        int registeredPlayers = GetRegisteredPlayerCount();
        int trackedPlayers = readyStates.Count;

        if (Runner == null)
        {
            return Mathf.Max(registeredPlayers, trackedPlayers);
        }

        int activePlayers = Runner.ActivePlayers.Count(IsClientPlayer);
        return Mathf.Max(activePlayers, Mathf.Max(registeredPlayers, trackedPlayers));
    }

    private int GetRegisteredPlayerCount()
    {
        if (quickMatchPlayerStates.Count > 0)
        {
            var uniquePlayers = new HashSet<int>();
            foreach (var state in quickMatchPlayerStates.Values)
            {
                if (state.PlayerId > 0 && state.Status != QuickMatchPlayerStatusCodes.Cancelled)
                {
                    uniquePlayers.Add(state.PlayerId);
                }
            }

            if (uniquePlayers.Count > 0)
            {
                return uniquePlayers.Count;
            }
        }

        if (playerUserIds.Count > 0)
        {
            return playerUserIds.Values.Distinct().Count();
        }

        return 0;
    }

    private int ResolveExpectedPlayerCount(SessionInfo sessionInfo)
    {
        int expected = expectedMatchPlayers;

#if UNITY_SERVER
        int envMaxPlayers = GetEnvInt("MAX_PLAYERS", 0);
        if (envMaxPlayers > 0)
        {
            expected = envMaxPlayers;
        }
#endif

        if (expected <= 0 && sessionInfo.IsValid && sessionInfo.MaxPlayers > 0)
        {
            expected = sessionInfo.MaxPlayers;
        }

        if (expected <= 0 && sessionInfo.IsValid && sessionInfo.PlayerCount > 0)
        {
            expected = sessionInfo.PlayerCount;
        }

        if (expected <= 0)
        {
            Debug.LogWarning("[QuickMatch] Expected player count is not configured. Check MAX_PLAYERS or session PlayerCount/MaxPlayers.");
        }

        return expected;
    }

    public int GetResolvedExpectedPlayerCount()
    {
        var sessionInfo = Runner != null ? Runner.SessionInfo : default;
        return ResolveExpectedPlayerCount(sessionInfo);
    }

    private int ResolveExpectedRealPlayerCount(int expectedPlayers)
    {
        int resolved = expectedRealPlayers;

        if (resolved <= 0)
        {
            resolved = expectedPlayers;
        }

        if (expectedPlayers > 0)
        {
            resolved = Mathf.Min(resolved, expectedPlayers);
        }

        return Mathf.Max(1, resolved);
    }

    private int GetEnvInt(string key, int defaultValue)
    {
        string value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return int.TryParse(value.Trim(), out var parsed) ? parsed : defaultValue;
    }

    // Ghi chÃƒÂº: GÃ¡Â»Â­i cÃ¡ÂºÂ­p nhÃ¡ÂºÂ­t sÃ¡Â»â€˜ ngÃ†Â°Ã¡Â»Âi sÃ¡ÂºÂµn sÃƒÂ ng tÃ¡Â»â€ºi tÃ¡Â»Â«ng client.
    private void BroadcastReadyStatus(PlayerRef readyPlayer)
    {
        int readyCount = readyStates.Count(pair => pair.Value);
        int totalPlayers = readyStates.Count;

        var players = new List<PlayerRef>(readyStates.Keys);
        foreach (var player in players)
        {
            RPC_PlayerReadyStatus(player, readyPlayer, readyCount, totalPlayers);
        }
    }

    // Ghi chÃƒÂº: HÃ¡Â»Â§y giai Ã„â€˜oÃ¡ÂºÂ¡n sÃ¡ÂºÂµn sÃƒÂ ng vÃƒÂ  thÃƒÂ´ng bÃƒÂ¡o cho tÃ¡ÂºÂ¥t cÃ¡ÂºÂ£ client khi cÃƒÂ³ ngÃ†Â°Ã¡Â»Âi hÃ¡Â»Â§y hoÃ¡ÂºÂ·c hÃ¡ÂºÂ¿t thÃ¡Â»Âi gian.
    private void CancelReadyPhase()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (!readyPhaseActive && readyStates.Count == 0)
        {
            return;
        }

        readyPhaseActive = false;
        StopReadyCountdown();

        lastMatchPlayers.Clear();

        var players = new List<PlayerRef>(readyStates.Keys);
        foreach (var player in players)
        {
            RPC_ReadyPhaseCancelled(player);
        }

        readyStates.Clear();
        QuickMatchPlayers.Clear();
        playerUserIds.Clear();
        quickMatchPlayerStates.Clear();
        ResetAssignedPlayersWaitingState();
    }

    // Ghi chÃƒÂº: Khi tÃ¡ÂºÂ¥t cÃ¡ÂºÂ£ Ã„â€˜ÃƒÂ£ sÃ¡ÂºÂµn sÃƒÂ ng, thÃƒÂ´ng bÃƒÂ¡o bÃ¡ÂºÂ¯t Ã„â€˜Ã¡ÂºÂ§u trÃ¡ÂºÂ­n vÃƒÂ  reset trÃ¡ÂºÂ¡ng thÃƒÂ¡i chuÃ¡ÂºÂ©n bÃ¡Â»â€¹.
    private void CompleteReadyPhase()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        readyPhaseActive = false;
        matchStarted = true;
        StopReadyCountdown();
        ResetAssignedPlayersWaitingState();

        var sessionInfo = Runner != null ? Runner.SessionInfo : default;
        var ticket = sessionInfo.IsValid ? new QuickMatchTicket(sessionInfo) : default;

        var players = new List<PlayerRef>(readyStates.Keys);
        lastMatchPlayers.Clear();
        lastMatchPlayers.AddRange(players);

        readyStates.Clear();
        BeginMatchStartOrCharacterSelection(players, ticket);
        //TriggerAllClientsReady();
    }

    private void StartMatchImmediately()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        readyPhaseActive = false;
        matchStarted = true;
        StopReadyCountdown();
        ResetAssignedPlayersWaitingState();

        var sessionInfo = Runner != null ? Runner.SessionInfo : default;
        var ticket = sessionInfo.IsValid ? new QuickMatchTicket(sessionInfo) : default;

        var players = new List<PlayerRef>();
        if (Runner != null)
        {
            players.AddRange(Runner.ActivePlayers.Where(IsClientPlayer));
        }

        if (players.Count == 0)
        {
            players.AddRange(readyStates.Keys);
        }

        if (players.Count == 0)
        {
            return;
        }

        lastMatchPlayers.Clear();
        lastMatchPlayers.AddRange(players);

        readyStates.Clear();
        BeginMatchStartOrCharacterSelection(players, ticket);
    }

    private void TrackPlayerUserId(PlayerRef player, int userId)
    {
        if (userId <= 0)
        {
            return;
        }

        playerUserIds[player] = userId;

        var status = QuickMatchPlayerStatusCodes.Waiting;
        if (quickMatchPlayerStates.TryGetValue(player, out var existingState) && existingState.PlayerId == userId)
        {
            status = existingState.Status;
        }

        UpsertQuickMatchPlayerState(player, userId, status);
    }

    private void UntrackPlayerUserId(PlayerRef player)
    {
        if (!playerUserIds.Remove(player))
        {
            return;
        }

        quickMatchPlayerStates.Remove(player);
        RebuildQuickMatchPlayers();
    }

    private void UpsertQuickMatchPlayerState(PlayerRef player, int userId, int status)
    {
        if (player.IsNone || userId <= 0)
        {
            return;
        }

        playerUserIds[player] = userId;
        quickMatchPlayerStates[player] = new QuickMatchPlayerState
        {
            PlayerId = userId,
            Status = status
        };

        RebuildQuickMatchPlayers();
    }

    private void RemoveQuickMatchPlayerState(PlayerRef player, bool markCancelled)
    {
        if (player.IsNone)
        {
            return;
        }

        if (markCancelled && quickMatchPlayerStates.TryGetValue(player, out var current))
        {
            current.Status = QuickMatchPlayerStatusCodes.Cancelled;
            quickMatchPlayerStates[player] = current;
        }
        else
        {
            quickMatchPlayerStates.Remove(player);
        }

        playerUserIds.Remove(player);
        RebuildQuickMatchPlayers();
    }

    private void RebuildQuickMatchPlayers()
    {
        QuickMatchPlayers.Clear();

        var uniqueStates = new Dictionary<int, QuickMatchPlayerState>();

        foreach (var kvp in quickMatchPlayerStates.Values)
        {
            int userId = kvp.PlayerId;
            if (userId <= 0)
            {
                continue;
            }

            uniqueStates[userId] = kvp;
        }

        foreach (var state in uniqueStates.Values)
        {
            QuickMatchPlayers.Add(state);
        }
    }

    public bool TryGetPlayerRefByUserId(int userId, out PlayerRef playerRef)
    {
        playerRef = PlayerRef.None;

        if (userId <= 0)
        {
            return false;
        }

        foreach (var kvp in playerUserIds)
        {
            if (kvp.Value == userId)
            {
                playerRef = kvp.Key;
                return true;
            }
        }

        return false;
    }

    private void BeginMatchStartOrCharacterSelection(List<PlayerRef> players, QuickMatchTicket ticket)
    {
        if (PaperLegendRuntimeState.IsPaperLegendMatch)
        {
            Debug.Log("[QuickMatch][PaperLegends] Skipping Fusion-side character selection; WEB_SERVER socket selection is already authoritative.");
            StartMatchForPlayers(players, ticket);
            return;
        }

        if (TryBeginPaperLegendCharacterSelection(players, ticket))
            return;

        StartMatchForPlayers(players, ticket);
    }

    private void StartMatchForPlayers(List<PlayerRef> players, QuickMatchTicket ticket)
    {
        if (players == null || players.Count == 0)
            return;

        foreach (var player in players)
        {
            RPC_StartMatch(player, ticket);
        }

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[QuickMatch][PaperLegends] NetworkObjectManager is missing; cannot load game map.");
            return;
        }

        manager.AllClientsAreReadyHandle();
    }

    private bool TryBeginPaperLegendCharacterSelection(List<PlayerRef> players, QuickMatchTicket ticket)
    {
        if (!enablePaperLegendCharacterSelection || !PaperLegendRuntimeState.IsPaperLegendMatch)
            return false;

        if (players == null || players.Count == 0)
            return false;

        StopPaperLegendCharacterSelection();
        PaperLegendRuntimeState.ClearCharacterSelections();

        paperLegendCharacterSelectionActive = true;
        paperLegendSelectionTicket = ticket;
        paperLegendSelectionEndsAtRealtime = Time.realtimeSinceStartup + Mathf.Max(1f, paperLegendCharacterSelectionSeconds);
        paperLegendSelectionPlayerRefsById.Clear();
        paperLegendSelectionModelsByPlayerId.Clear();
        paperLegendSelectionModelOwners.Clear();
        paperLegendSelectionAvailableModelIds.Clear();
        paperLegendSelectionBotModelIds.Clear();
        paperLegendSelectionRealPlayerIds.Clear();

        foreach (var player in players)
        {
            if (!TryGetUserIdForPlayer(player, out int userId))
            {
                Debug.LogWarning($"[QuickMatch][PaperLegends] Missing userId for playerRef={player}; skipping character selection entry.");
                continue;
            }

            if (!paperLegendSelectionPlayerRefsById.ContainsKey(userId))
            {
                paperLegendSelectionPlayerRefsById.Add(userId, player);
                paperLegendSelectionRealPlayerIds.Add(userId);
            }
        }

        if (paperLegendSelectionRealPlayerIds.Count == 0)
        {
            StopPaperLegendCharacterSelection();
            return false;
        }

        var sessionInfo = Runner != null ? Runner.SessionInfo : default;
        int expectedSlots = ResolveExpectedPlayerCount(sessionInfo);
        if (expectedSlots <= 0)
            expectedSlots = Mathf.Max(players.Count, expectedMatchPlayers);
        if (expectedSlots <= 0)
            expectedSlots = players.Count;

        expectedSlots = Mathf.Max(paperLegendSelectionRealPlayerIds.Count, expectedSlots);
        ResolvePaperLegendSelectableCharacterIds(expectedSlots, paperLegendSelectionAvailableModelIds);

        if (paperLegendSelectionAvailableModelIds.Count < expectedSlots)
        {
            Debug.LogWarning($"[QuickMatch][PaperLegends] Only {paperLegendSelectionAvailableModelIds.Count} selectable character model(s) configured for {expectedSlots} slot(s). Configure at least {expectedSlots} unique modelId entries to guarantee no duplicates.");
        }

        int botSlots = Mathf.Max(0, expectedSlots - paperLegendSelectionRealPlayerIds.Count);
        for (int i = 0; i < botSlots; i++)
        {
            int botModelId = PickAvailableCharacterModelId();
            int botOwnerId = -1000 - i;
            paperLegendSelectionBotModelIds.Add(botModelId);
            paperLegendSelectionModelOwners[botModelId] = botOwnerId;
        }

        string playerIdsCsv = BuildCsv(paperLegendSelectionRealPlayerIds);
        string modelIdsCsv = BuildCsv(paperLegendSelectionAvailableModelIds);
        float countdownSeconds = Mathf.Max(1f, paperLegendCharacterSelectionSeconds);

        foreach (var player in paperLegendSelectionPlayerRefsById.Values)
        {
            RPC_BeginPaperLegendCharacterSelection(player, playerIdsCsv, modelIdsCsv, countdownSeconds);
        }

        for (int i = 0; i < paperLegendSelectionBotModelIds.Count; i++)
        {
            BroadcastPaperLegendSelectionUpdate(-1000 - i, paperLegendSelectionBotModelIds[i]);
        }

        paperLegendSelectionCountdownRoutine = StartCoroutine(PaperLegendCharacterSelectionCountdownRoutine());
        Debug.Log($"[QuickMatch][PaperLegends] Character selection started for {paperLegendSelectionRealPlayerIds.Count} real player(s), {botSlots} bot slot(s), countdown={countdownSeconds:F0}s.");
        return true;
    }

    private IEnumerator PaperLegendCharacterSelectionCountdownRoutine()
    {
        while (paperLegendCharacterSelectionActive && Time.realtimeSinceStartup < paperLegendSelectionEndsAtRealtime)
        {
            if (AllRealPlayersSelectedCharacters())
                break;

            yield return null;
        }

        FinalizePaperLegendCharacterSelection();
    }

    private void FinalizePaperLegendCharacterSelection()
    {
        if (!paperLegendCharacterSelectionActive)
            return;

        foreach (int playerId in paperLegendSelectionRealPlayerIds)
        {
            if (paperLegendSelectionModelsByPlayerId.ContainsKey(playerId))
                continue;

            int modelId = PickAvailableCharacterModelId();
            paperLegendSelectionModelsByPlayerId[playerId] = modelId;
            paperLegendSelectionModelOwners[modelId] = playerId;
            BroadcastPaperLegendSelectionUpdate(playerId, modelId);
        }

        foreach (var pair in paperLegendSelectionModelsByPlayerId)
        {
            PaperLegendRuntimeState.SetSelectedCharacterModel(pair.Key, pair.Value);
        }

        PaperLegendRuntimeState.SetReservedBotCharacterModels(paperLegendSelectionBotModelIds);

        string selectionsCsv = BuildSelectionCsv();
        foreach (var player in paperLegendSelectionPlayerRefsById.Values)
        {
            RPC_PaperLegendCharacterSelectionCompleted(player, selectionsCsv);
        }

        var playersToStart = new List<PlayerRef>(lastMatchPlayers);
        var ticket = paperLegendSelectionTicket;
        StopPaperLegendCharacterSelection();

        Debug.Log($"[QuickMatch][PaperLegends] Character selection completed: {selectionsCsv}");
        StartMatchForPlayers(playersToStart, ticket);
    }

    private bool TrySelectPaperLegendCharacter(PlayerRef player, int userId, int modelId, out string reason)
    {
        reason = string.Empty;

        if (!paperLegendCharacterSelectionActive)
        {
            reason = "selection_not_active";
            return false;
        }

        if (userId <= 0 || !paperLegendSelectionPlayerRefsById.TryGetValue(userId, out var expectedPlayer) || expectedPlayer != player)
        {
            reason = "invalid_player";
            return false;
        }

        if (modelId <= 0 || !paperLegendSelectionAvailableModelIds.Contains(modelId))
        {
            reason = "invalid_character";
            return false;
        }

        if (paperLegendSelectionModelOwners.TryGetValue(modelId, out int ownerId) && ownerId != userId)
        {
            reason = "character_already_selected";
            return false;
        }

        if (paperLegendSelectionModelsByPlayerId.TryGetValue(userId, out int previousModelId))
        {
            if (previousModelId == modelId)
                return true;

            reason = "player_already_selected";
            return false;
        }

        paperLegendSelectionModelsByPlayerId[userId] = modelId;
        paperLegendSelectionModelOwners[modelId] = userId;
        BroadcastPaperLegendSelectionUpdate(userId, modelId);

        if (AllRealPlayersSelectedCharacters())
            FinalizePaperLegendCharacterSelection();

        return true;
    }

    private bool AllRealPlayersSelectedCharacters()
    {
        if (paperLegendSelectionRealPlayerIds.Count == 0)
            return false;

        foreach (int playerId in paperLegendSelectionRealPlayerIds)
        {
            if (!paperLegendSelectionModelsByPlayerId.ContainsKey(playerId))
                return false;
        }

        return true;
    }

    private void BroadcastPaperLegendSelectionUpdate(int playerId, int modelId)
    {
        int selectedCount = paperLegendSelectionModelsByPlayerId.Count + paperLegendSelectionBotModelIds.Count;
        int totalCount = paperLegendSelectionRealPlayerIds.Count + paperLegendSelectionBotModelIds.Count;
        float remainingSeconds = GetPaperLegendSelectionRemainingSeconds();

        foreach (var player in paperLegendSelectionPlayerRefsById.Values)
        {
            RPC_PaperLegendCharacterSelectionUpdated(player, playerId, modelId, selectedCount, totalCount, remainingSeconds);
        }
    }

    private void StopPaperLegendCharacterSelection()
    {
        if (paperLegendSelectionCountdownRoutine != null)
        {
            StopCoroutine(paperLegendSelectionCountdownRoutine);
            paperLegendSelectionCountdownRoutine = null;
        }

        paperLegendCharacterSelectionActive = false;
        paperLegendSelectionEndsAtRealtime = -1f;
        paperLegendSelectionPlayerRefsById.Clear();
        paperLegendSelectionModelsByPlayerId.Clear();
        paperLegendSelectionModelOwners.Clear();
        paperLegendSelectionAvailableModelIds.Clear();
        paperLegendSelectionBotModelIds.Clear();
        paperLegendSelectionRealPlayerIds.Clear();
        paperLegendSelectionTicket = default;
    }

    private float GetPaperLegendSelectionRemainingSeconds()
    {
        if (paperLegendSelectionEndsAtRealtime < 0f)
            return 0f;

        return Mathf.Max(0f, paperLegendSelectionEndsAtRealtime - Time.realtimeSinceStartup);
    }

    private bool TryGetUserIdForPlayer(PlayerRef player, out int userId)
    {
        if (playerUserIds.TryGetValue(player, out userId) && userId > 0)
            return true;

        if (quickMatchPlayerStates.TryGetValue(player, out var state) && state.PlayerId > 0)
        {
            userId = state.PlayerId;
            return true;
        }

        userId = 0;
        return false;
    }

    private void ResolvePaperLegendSelectableCharacterIds(int requiredSlots, List<int> results)
    {
        results.Clear();

#if UNITY_SERVER
        var initializer = GameServerInitializer.Instance;
        if (initializer != null)
            AddUniqueCharacterModelIds(results, initializer.GetPaperLegendCharacterModelIds());
#endif

        AddUniqueCharacterModelIds(results, paperLegendSelectableCharacterIds);

        foreach (PlayerBodyType bodyType in Enum.GetValues(typeof(PlayerBodyType)))
        {
            AddUniqueCharacterModelId(results, (int)bodyType);
        }

        int fallbackId = 1;
        while (results.Count < requiredSlots)
        {
            AddUniqueCharacterModelId(results, fallbackId);
            fallbackId++;
        }
    }

    private static void AddUniqueCharacterModelIds(List<int> results, IEnumerable<int> modelIds)
    {
        if (modelIds == null)
            return;

        foreach (int modelId in modelIds)
        {
            AddUniqueCharacterModelId(results, modelId);
        }
    }

    private static void AddUniqueCharacterModelId(List<int> results, int modelId)
    {
        if (modelId <= 0 || results.Contains(modelId))
            return;

        results.Add(modelId);
    }

    private int PickAvailableCharacterModelId()
    {
        var candidates = paperLegendSelectionAvailableModelIds
            .Where(modelId => !paperLegendSelectionModelOwners.ContainsKey(modelId))
            .ToList();

        if (candidates.Count > 0)
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];

        if (paperLegendSelectionAvailableModelIds.Count > 0)
            return paperLegendSelectionAvailableModelIds[UnityEngine.Random.Range(0, paperLegendSelectionAvailableModelIds.Count)];

        return 1;
    }

    private static string BuildCsv(IEnumerable<int> values)
    {
        return values == null ? string.Empty : string.Join(",", values);
    }

    private string BuildSelectionCsv()
    {
        var parts = new List<string>();
        foreach (var pair in paperLegendSelectionModelsByPlayerId)
        {
            parts.Add($"{pair.Key}:{pair.Value}");
        }

        for (int i = 0; i < paperLegendSelectionBotModelIds.Count; i++)
        {
            parts.Add($"{-1000 - i}:{paperLegendSelectionBotModelIds[i]}");
        }

        return string.Join(",", parts);
    }

    //private void TriggerAllClientsReady()
    //{
    //    NetworkObjectManager manager = null;

    //    if (NetworkObjectManager.Instance != null)
    //    {
    //        manager = NetworkObjectManager.Instance;
    //    }
    //    else if (GameManagerNetWork.Instance != null && GameManagerNetWork.Instance.serverRPC != null)
    //    {
    //        manager = GameManagerNetWork.Instance.serverRPC;
    //    }

    //    if (manager != null)
    //    {
    //        manager.AllClientsAreReadyHandle();
    //    }
    //    else
    //    {
    //        Debug.LogWarning("[QuickMatch] Unable to trigger game start because NetworkObjectManager reference is missing.");
    //    }
    //}

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Ghi chÃƒÂº: ThÃƒÂ´ng bÃƒÂ¡o client bÃ¡ÂºÂ¯t Ã„â€˜Ã¡ÂºÂ§u giai Ã„â€˜oÃ¡ÂºÂ¡n xÃƒÂ¡c nhÃ¡ÂºÂ­n sÃ¡ÂºÂµn sÃƒÂ ng vÃ¡Â»â€ºi sÃ¡Â»â€˜ ngÃ†Â°Ã¡Â»Âi chÃ†Â¡i vÃƒÂ  thÃ¡Â»Âi gian Ã„â€˜Ã¡ÂºÂ¿m ngÃ†Â°Ã¡Â»Â£c.
    private void RPC_BeginReadyPhase(int totalPlayers, float countdownSeconds)
    {
        if (Object.HasStateAuthority)
        {
            return;
        }
#if !UNITY_SERVER
        QuickMatchClient.Instance?.HandleReadyPhaseStarted(totalPlayers, countdownSeconds);
#endif
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BeginPaperLegendCharacterSelection(PlayerRef targetPlayer, string playerIdsCsv, string selectableModelIdsCsv, float countdownSeconds)
    {
        if (Object.HasStateAuthority || Runner == null || Runner.LocalPlayer != targetPlayer)
            return;

#if !UNITY_SERVER
        OnClientPaperLegendCharacterSelectionStarted?.Invoke(playerIdsCsv, selectableModelIdsCsv, countdownSeconds);
#endif
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PaperLegendCharacterSelectionUpdated(PlayerRef targetPlayer, int playerId, int modelId, int selectedCount, int totalCount, float remainingSeconds)
    {
        if (Object.HasStateAuthority || Runner == null || Runner.LocalPlayer != targetPlayer)
            return;

#if !UNITY_SERVER
        OnClientPaperLegendCharacterSelectionUpdated?.Invoke(playerId, modelId, selectedCount, totalCount, remainingSeconds);
#endif
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PaperLegendCharacterSelectionCompleted(PlayerRef targetPlayer, string selectionsCsv)
    {
        if (Object.HasStateAuthority || Runner == null || Runner.LocalPlayer != targetPlayer)
            return;

#if !UNITY_SERVER
        OnClientPaperLegendCharacterSelectionCompleted?.Invoke(selectionsCsv);
#endif
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PaperLegendCharacterSelectionRejected(PlayerRef targetPlayer, int modelId, string reason)
    {
        if (Object.HasStateAuthority || Runner == null || Runner.LocalPlayer != targetPlayer)
            return;

#if !UNITY_SERVER
        OnClientPaperLegendCharacterSelectionRejected?.Invoke(modelId, reason);
#endif
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ClientEnteredQuickMatch(int userId, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        var player = info.Source;

        if (player.IsNone)
        {
            Debug.LogWarning("[QuickMatch] Received quick match entry RPC but player reference was invalid.");
            return;
        }

        Debug.Log($"[QuickMatch] Player {player} entered the matchmaking room.");
        UpsertQuickMatchPlayerState(player, userId, QuickMatchPlayerStatusCodes.Waiting);
        HandlePlayerRequestQuickMatch(player);
    }

    public void NotifyClientEnteredGame(int userId)
    {
        if (Runner == null)
        {
            Debug.LogWarning("[QuickMatch] Cannot notify server of entry because runner is null.");
            return;
        }

        RPC_ClientEnteredQuickMatch(userId);
    }

    public void NotifyClientReadyState(bool ready, int userId)
    {
        if (Runner == null)
        {
            Debug.LogWarning("[QuickMatch] Cannot confirm ready because runner is null.");
            return;
        }

        //int userId = GameManagerNetWork.Instance != null && GameManagerNetWork.Instance.loginUserModel != null
        //    ? GameManagerNetWork.Instance.loginUserModel.UserId
        //    : 0;

        RPC_ClientReadyState(ready, userId);
    }

    public void NotifyPaperLegendCharacterSelected(int userId, int modelId)
    {
        if (Runner == null)
        {
            Debug.LogWarning("[QuickMatch][PaperLegends] Cannot submit character selection because runner is null.");
            return;
        }

        RPC_ClientPaperLegendCharacterSelected(userId, modelId);
    }

    public void NotifyClientQueueStatus(int userId, int status)
    {
        if (Runner == null)
        {
            Debug.LogWarning("[QuickMatch] Cannot update queue status because runner is null.");
            return;
        }

        RPC_ClientQueueStatus(userId, status);
    }

    public void NotifyInitializationFailed(string localizationKey)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (string.IsNullOrEmpty(localizationKey))
        {
            return;
        }

        Debug.LogWarning($"[QuickMatch] Initialization failed with key '{localizationKey}'. Notifying clients.");

        RPC_InitializationFailed(localizationKey);

        lastMatchPlayers.Clear();
    }

    private void MarkWaitingForAssignedRealPlayers()
    {
        if (waitingForAssignedRealPlayers)
        {
            return;
        }

        waitingForAssignedRealPlayers = true;
        waitingForAssignedRealPlayersSinceRealtime = Time.realtimeSinceStartup;
    }

    private void ResetAssignedPlayersWaitingState()
    {
        waitingForAssignedRealPlayers = false;
        waitingForAssignedRealPlayersSinceRealtime = -1f;
    }

    public bool TryStartWithConnectedPlayersAfterAssignedPlayerTimeout()
    {
        if (!Object.HasStateAuthority || !waitingForAssignedRealPlayers || matchStarted)
        {
            return false;
        }

        PruneInactiveRegisteredPlayers();

        int registeredPlayers = GetRegisteredPlayerCount();
        if (registeredPlayers <= 0)
        {
            return false;
        }

        Debug.LogWarning($"[QuickMatch] Assigned real-player wait timed out with {registeredPlayers}/{expectedRealPlayers} registered client(s). Starting match with connected clients and filling missing slots with bots.");
        expectedRealPlayers = registeredPlayers;
        ResetAssignedPlayersWaitingState();
        StartMatchImmediately();
        return true;
    }

    public void PruneInactiveRegisteredPlayers()
    {
        if (Runner == null)
        {
            return;
        }

        var activePlayers = new HashSet<PlayerRef>(Runner.ActivePlayers.Where(IsClientPlayer));
        var registeredRefs = quickMatchPlayerStates.Keys.ToList();

        foreach (var player in registeredRefs)
        {
            if (activePlayers.Contains(player))
            {
                continue;
            }

            Debug.LogWarning($"[QuickMatch] Removing inactive registered player {player} before timeout fallback start.");
            RemoveQuickMatchPlayerState(player, false);
            readyStates.Remove(player);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Ghi chÃƒÂº: GÃ¡Â»Â­i tÃƒÂ­n hiÃ¡Â»â€¡u cho client biÃ¡ÂºÂ¿t giai Ã„â€˜oÃ¡ÂºÂ¡n chÃ¡Â»Â Ã„â€˜ÃƒÂ£ bÃ¡Â»â€¹ hÃ¡Â»Â§y.
    private void RPC_ReadyPhaseCancelled(PlayerRef targetPlayer)
    {
        if (Object.HasStateAuthority || Runner == null || Runner.LocalPlayer != targetPlayer)
        {
            return;
        }
        #if !UNITY_SERVER
        QuickMatchClient.Instance?.HandleReadyPhaseCancelled();
        #endif
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Ghi chÃƒÂº: Ra lÃ¡Â»â€¡nh cho client bÃ¡ÂºÂ¯t Ã„â€˜Ã¡ÂºÂ§u trÃ¡ÂºÂ­n Ã„â€˜Ã¡ÂºÂ¥u vÃ¡Â»â€ºi vÃƒÂ© tÃ†Â°Ã†Â¡ng Ã¡Â»Â©ng.
    private void RPC_StartMatch(PlayerRef targetPlayer, QuickMatchTicket ticket)
    {
        if (Object.HasStateAuthority || Runner == null || Runner.LocalPlayer != targetPlayer)
        {
            return;
        }

        OnClientMatchStarting?.Invoke(ticket);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Ghi chÃƒÂº: BÃƒÂ¡o cho client rÃ¡ÂºÂ±ng hÃƒÂ ng chÃ¡Â»Â hiÃ¡Â»â€¡n tÃ¡ÂºÂ¡i Ã„â€˜ÃƒÂ£ bÃ¡Â»â€¹ hÃ¡Â»Â§y.
    private void RPC_QueueCancelled(PlayerRef targetPlayer)
    {
        if (Object.HasStateAuthority || Runner == null || Runner.LocalPlayer != targetPlayer)
        {
            return;
        }

        OnClientQueueCancelled?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Ghi chÃƒÂº: YÃƒÂªu cÃ¡ÂºÂ§u client thoÃƒÂ¡t khÃ¡Â»Âi hÃƒÂ ng chÃ¡Â»Â khi cÃ¡ÂºÂ§n thiÃ¡ÂºÂ¿t.
    private void RPC_ExitQueue(PlayerRef targetPlayer)
    {
        if (Object.HasStateAuthority || Runner == null || Runner.LocalPlayer != targetPlayer)
        {
            return;
        }

        OnClientExitQueue?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Ghi chÃƒÂº: CÃ¡ÂºÂ­p nhÃ¡ÂºÂ­t cho client vÃ¡Â»Â sÃ¡Â»â€˜ ngÃ†Â°Ã¡Â»Âi Ã„â€˜ÃƒÂ£ sÃ¡ÂºÂµn sÃƒÂ ng vÃƒÂ  ngÃ†Â°Ã¡Â»Âi vÃ¡Â»Â«a thay Ã„â€˜Ã¡Â»â€¢i trÃ¡ÂºÂ¡ng thÃƒÂ¡i.
    private void RPC_PlayerReadyStatus(PlayerRef targetPlayer, PlayerRef readyPlayer, int readyCount, int totalPlayers)
    {
        if (Object.HasStateAuthority || Runner == null || Runner.LocalPlayer != targetPlayer)
        {
            return;
        }

        OnClientPlayerReadyStatusChanged?.Invoke(readyPlayer, readyCount, totalPlayers);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_InitializationFailed(string localizationKey)
    {
        if (Object.HasStateAuthority)
        {
            return;
        }

#if !UNITY_SERVER
        OnClientInitializationFailed?.Invoke(localizationKey);
#endif
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ClientReadyState(bool ready, int userId, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        var player = info.Source;

        // NÃ¡ÂºÂ¿u source bÃ¡Â»â€¹ rÃ¡Â»â€”ng (thÃ†Â°Ã¡Â»Âng xÃ¡ÂºÂ£y ra khi host tÃ¡Â»Â± gÃ¡Â»Âi RPC), fallback vÃ¡Â»Â LocalPlayer cÃ¡Â»Â§a runner
        // Ã„â€˜Ã¡Â»Æ’ vÃ¡ÂºÂ«n ghi nhÃ¡ÂºÂ­n Ã„â€˜Ã†Â°Ã¡Â»Â£c thao tÃƒÂ¡c ready Ã„â€˜Ã¡ÂºÂ§u tiÃƒÂªn cÃ¡Â»Â§a chÃ¡Â»Â§ phÃƒÂ²ng.
        if (player.IsNone && Runner != null)
        {
            player = Runner.LocalPlayer;
        }

        if (player.IsNone)
        {
            Debug.LogWarning("[QuickMatch] Received ready confirmation but player reference was invalid.");
            return;
        }

        // Ã„ÂÃ¡ÂºÂ£m bÃ¡ÂºÂ£o server cÃƒÂ³ entry cho player trÃ†Â°Ã¡Â»â€ºc khi cÃ¡ÂºÂ­p nhÃ¡ÂºÂ­t cÃ¡Â»Â ready
        TrackPlayer(player);

        if (ready)
        {
            UpsertQuickMatchPlayerState(player, userId, QuickMatchPlayerStatusCodes.Ready);
        }
        else
        {
            UpsertQuickMatchPlayerState(player, userId, QuickMatchPlayerStatusCodes.Waiting);
        }

        Debug.Log($"Player {userId} is ready.");
        HandlePlayerConfirmReady(player, ready, userId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ClientPaperLegendCharacterSelected(int userId, int modelId, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        var player = info.Source;
        if (player.IsNone && Runner != null)
            player = Runner.LocalPlayer;

        if (player.IsNone)
        {
            Debug.LogWarning("[QuickMatch][PaperLegends] Received character selection but player reference was invalid.");
            return;
        }

        if (!TrySelectPaperLegendCharacter(player, userId, modelId, out string reason))
        {
            RPC_PaperLegendCharacterSelectionRejected(player, modelId, reason);
            Debug.LogWarning($"[QuickMatch][PaperLegends] Rejected character selection userId={userId}, modelId={modelId}, reason={reason}.");
            return;
        }

        Debug.Log($"[QuickMatch][PaperLegends] Accepted character selection userId={userId}, modelId={modelId}.");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ClientQueueStatus(int userId, int status, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        var player = info.Source;

        if (player.IsNone && Runner != null)
        {
            player = Runner.LocalPlayer;
        }

        if (player.IsNone)
        {
            Debug.LogWarning("[QuickMatch] Received queue status update but player reference was invalid.");
            return;
        }

        TrackPlayer(player);

        if (status == QuickMatchPlayerStatusCodes.Cancelled)
        {
            RemoveQuickMatchPlayerState(player, true);
            readyStates.Remove(player);
            if (readyPhaseActive)
            {
                CancelReadyPhase();
            }
            return;
        }

        UpsertQuickMatchPlayerState(player, userId, status);
    }
}
