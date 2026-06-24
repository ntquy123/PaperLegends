#if UNITY_SERVER
using Fusion;
using Fusion.Addons.Physics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
 



public class GameSessionNetWork_Host : NetworkBehaviour
{
    private const float BananaSlipCinematicDuration = 4.5f;
    private const float AbandonedRoomTimeoutSeconds = 90f;
    private const float SoloConnectedSurvivorTimeoutSeconds = 30f;
    [SerializeField, Tooltip("Thời gian tối đa cho mỗi người chơi bắn lượt thi. Hết giờ mà chưa bắn sẽ bị chốt 0 điểm.")]
    private float examShotTimeoutSeconds = 15f;
    [SerializeField, Tooltip("Khoảng chờ thêm sau khi server đã nhận client thả nút bắn nhưng gói ShotData tới trễ do mạng yếu.")]
    private float examShotCommitGraceSeconds = 4f;
    private const int MinBotOnlyTurnsBeforeForcedLoss = 1;
    private const int MaxBotOnlyTurnsBeforeForcedLoss = 2;
    public static GameSessionNetWork_Host Instance;
    [Header("PLAYER CONFIG")]
    [SerializeField] private float moveSpeed = 1f; // Tốc độ di chuyển

    [Header("GAME CONFIG")]
    private bool isProcessingEndGame;
    private float lastMatchProgressRealtime;
    public float LastMatchProgressRealtime => lastMatchProgressRealtime;
    public float SecondsSinceLastMatchProgress => Mathf.Max(0f, Time.realtimeSinceStartup - lastMatchProgressRealtime);

    public void MarkMatchProgress(string reason = null)
    {
        _ = reason;
        lastMatchProgressRealtime = Time.realtimeSinceStartup;
    }
    public bool IsProcessingEndGame => isProcessingEndGame;
    public bool HasBroadcastGameOverResults { get; private set; }
    private readonly HashSet<int> pendingGameOverAckPlayerIds = new HashSet<int>();
    private readonly HashSet<int> receivedGameOverAckPlayerIds = new HashSet<int>();
    private readonly HashSet<int> pendingDisconnectReadyPlayerIds = new HashSet<int>();
    private readonly HashSet<int> receivedDisconnectReadyPlayerIds = new HashSet<int>();
    public bool AreAllClientsGameOverAcked => pendingGameOverAckPlayerIds.Count == 0;
    public bool AreAllClientsReadyToDisconnect => pendingDisconnectReadyPlayerIds.Count == 0;
    public BoxCollider playArea; // Vùng vòng tròn, kiểm tra viên bi có nằm trong không
    public Transform playAreaGuard; // Vòng bảo vệ bao quanh PlayArea
    public Transform SpawnPlayerPoint;// vùng sapwm player lúc ban đầu
    public Transform SpawnBallPoint;// vùng sapwm culi lúc ban đầu
    public Transform ExamMain;
    public Transform StartPointMain;// đường thẳng mức để tính điểm thi
    //public Transform StartPoint; 

    [Header("HORIZONTAL MOVE LIMIT")]
    [SerializeField, Min(0f), Tooltip("Khoảng cách tối đa nhân vật được dịch trái/phải quanh ExamMain khi đang thi.")]
    private float examHorizontalMoveLimit = 2f;
    [SerializeField, Min(0f), Tooltip("Khoảng cách tối đa nhân vật được dịch trái/phải quanh StartPointMain khi tới lượt bắn.")]
    private float startPointHorizontalMoveLimit = 2f;
    public float ExamHorizontalMoveLimit => Mathf.Max(0f, examHorizontalMoveLimit);
    public float StartPointHorizontalMoveLimit => Mathf.Max(0f, startPointHorizontalMoveLimit);


    // Container dùng để chứa tạm các viên bi không hoạt động
    public Transform InactiveBallContainer;

    public bool IsGameEnded
    {
        get
        {
            var manager = NetworkObjectManager.Instance;
            return manager != null && manager.IsGameEnded;
        }
    }

    private void MarkGameEnded()
    {
        StopExamShotTimeoutWatchdog("game ended");

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("⚠️ Không thể đánh dấu kết thúc trận đấu vì NetworkObjectManager chưa được khởi tạo.");
            return;
        }

        if (!manager.HasStateAuthority)
        {
            Debug.LogWarning("⚠️ Bỏ qua yêu cầu đánh dấu kết thúc trận đấu vì không có quyền StateAuthority.");
            return;
        }

        if (manager.IsGameEnded)
            return;

        manager.StatusLoading = StatusLoadingGame.EndGame;
    }
 
    [Header("FLOOD CONFIG")]
    public GameObject WaterPrefab;
    public bool FloodEnabled = false; // enable or disable flooding
    [Header("WATER FALLBACK CONFIG")]
    public Transform WaterObject;
    [SerializeField] private bool enableServerWaterFallback = true;
    [SerializeField] private float waterFallbackCheckInterval = 0.1f;
    private float _nextWaterFallbackCheckTime;
    private bool _waterObjectResolveFailureLogged;
    private bool _waterObjectMissingColliderLogged;
    private readonly HashSet<int> _waterHitTriggeredPlayers = new HashSet<int>();

    [Header("DATA CONFIG")]
    //public float spawnRadius = 10f;
    private float minDistanceBetweenPlayers = 2.5f;
    //public LayerMask groundLayer; // layer của mặt đất
   // public Transform spawnParent;
    public Terrain TerrainGround;
    private List<Vector3> occupiedPositions = new List<Vector3>();
  
   // private bool moveLeft = false;
    //private bool moveRight = false;
    public List<Transform> LstLocationExam = new List<Transform>();
    // 3 điểm tập kết sau khi thi xong: người chưa tới lượt đứng chờ tại đây, không chồng lên mức bắn.
    public List<Transform> LstLocationGatherPoint = new List<Transform>();
    public List<Transform> LstLocationStartPoint = new List<Transform>();
    public List<Transform> PaperLegendSpawnPoints = new List<Transform>();
    public List<Transform> BananaSpawnPoints = new List<Transform>();
    public List<GameObject> ActiveBananaPeels = new List<GameObject>();
    private IReadOnlyList<TurnOrderEntry> TurnOrderListInternal => GetTurnOrderSnapshot();
    public IReadOnlyList<TurnOrderEntry> TurnOrderList => TurnOrderListInternal;

    private readonly Dictionary<int, Coroutine> activeServerMovements = new Dictionary<int, Coroutine>();
    private readonly Dictionary<int, Coroutine> activeWindBlowSkills = new Dictionary<int, Coroutine>();
    private readonly HashSet<int> playersMovedToStartPointForTurn = new HashSet<int>();
    private readonly HashSet<int> processingRegularShotStoppedPlayerIds = new HashSet<int>();
    private readonly Dictionary<int, int> grazeHitUseOrderByPlayer = new Dictionary<int, int>();
    private readonly Dictionary<int, int> catAnTienUseOrderByPlayer = new Dictionary<int, int>();
    private readonly HashSet<int> counteredGrazeHitPlayers = new HashSet<int>();
    private readonly HashSet<int> counteredCatAnTienPlayers = new HashSet<int>();
    private readonly Dictionary<int, bool> pendingCatAnTienCounteredBroadcast = new Dictionary<int, bool>();
    private int combatSkillUseSequence;

    /// <summary>
    /// Theo dõi số lần timeout liên tiếp của mỗi người chơi.
    /// Khi đạt 2 lần liên tiếp sẽ bị kick khỏi phòng và xử thua.
    /// </summary>
    private readonly Dictionary<int, int> consecutiveTimeoutCounts = new Dictionary<int, int>();
    private const int MaxConsecutiveTimeouts = 2;
    private const float AllBallsStopLinearThreshold = 0.05f;
    private const float AllBallsStopAngularThreshold = 1f;
    private const float AllBallsResidualLinearThreshold = 0.1f;
    private const float AllBallsResidualAngularThreshold = 2f;
    private float lastAllBallsStopLogTime;
    private bool lastAllBallsStoppedState = true;

    private void LogExamStateSnapshot(string context)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        var players = manager.players;
        var sb = new System.Text.StringBuilder();
        sb.Append($"[HOST][ExamScore] {context} players={players.Length} :: ");

        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (info.playerId == 0)
                continue;

            sb.Append($"pid={info.playerId} status={info.statusPlayer} hold={info.isHolding} destroy={info.isDestroy} dist={info.distance:F3} score={info.scoreExam:F2}; ");
        }

        Debug.Log(sb.ToString());
    }

    [Header("Wind Blow Skill")]
    [SerializeField] private float windBlowForce = 0.4f;
    [SerializeField] private float windBlowMinVelocity = 0.02f;
    [SerializeField] private float windBlowMinAngular = 0.2f;
    [SerializeField] private float windBlowReachDistance = 0.6f;
    [SerializeField] private float windBlowAnimationDelay = 3f;
    private readonly Dictionary<int, Coroutine> _activeBananaSlipRoutines = new Dictionary<int, Coroutine>();
    private Coroutine _bananaSlipSequenceRoutine;
    private Coroutine examOrderRoutine;
    private Coroutine examScoreRoutine;
    private Coroutine examShotTimeoutRoutine;
    private Coroutine betDeductionRoutine;
    private Coroutine abandonedRoomMonitorRoutine;
    private Coroutine abandonedRoomResolutionRoutine;
    private bool hasProcessedGameStart;
    private int pendingSoloSurvivorPlayerId;
    private readonly List<int> pendingDisconnectedAlivePlayerIds = new List<int>();
    private float pendingSoloSurvivorElapsed;
    private bool isBotOnlyResolutionArmed;
    private int botOnlyCompletedTurns;
    private int botOnlyTurnsBeforeForcedLoss;
    private readonly HashSet<int> examShotStartedPlayerIds = new HashSet<int>();
    private readonly Dictionary<int, float> pendingExamShotCommitRealtimeByPlayer = new Dictionary<int, float>();
 
    private List<TurnOrderEntry> GetTurnOrderSnapshot()
    {
        var snapshot = new List<TurnOrderEntry>();
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return snapshot;

        var players = manager.players;
        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (info.playerId == 0)
                continue;

            snapshot.Add(new TurnOrderEntry(info.playerId, info.turnOrder));
        }

        snapshot.Sort((a, b) => a.turnOrder.CompareTo(b.turnOrder));
        return snapshot;
    }

    public List<int> GetPlayerIdSnapshot()
    {
        return GetTurnOrderSnapshot()
            .Select(entry => entry.playerId)
            .Where(id => id > 0)
            .ToList();
    }

    private bool TryGetNetworkPlayerInfo(int playerId, out PlayerInfoStruct info, out int index)
    {
        info = default;
        index = -1;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return false;

        var players = manager.players;
        for (int i = 0; i < players.Length; i++)
        {
            var candidate = players.Get(i);
            if (candidate.playerId == playerId)
            {
                info = candidate;
                index = i;
                return true;
            }
        }

        return false;
    }

    private static bool IsServerExamPhaseActive(NetworkObjectManager manager)
    {
        return manager != null &&
               manager.HasStateAuthority &&
               manager.StatusLoading == StatusLoadingGame.isExam &&
               !manager.IsExamScoreReady;
    }

    private void ApplyTurnOrderToPlayer(int playerId, int turnOrder)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager != null && manager.HasStateAuthority && TryGetNetworkPlayerInfo(playerId, out var info, out var index))
        {
            if (info.turnOrder != turnOrder)
            {
                info.turnOrder = turnOrder;
                manager.players.Set(index, info);
            }
        }

        var playerGO = GetPlayerObject(playerId);
        if (playerGO != null)
        {
            var handler = playerGO.GetComponent<PlayerNetworkHandler>();
            var model = handler.PlayerModel;
            if (model.turnOrder != turnOrder)
            {
                model.turnOrder = turnOrder;
                handler.PlayerModel = model;
            }
        }

        var server = NetworkObjectManager.Instance;
        if (server != null && server.HasStateAuthority)
        {
            var players = server.players;
            for (int i = 0; i < players.Length; i++)
            {
                var candidate = players.Get(i);
                if (candidate.playerId == playerId)
                {
                    if (candidate.turnOrder != turnOrder)
                    {
                        candidate.turnOrder = turnOrder;
                        server.players.Set(i, candidate);
                    }
                    break;
                }
            }
        }
    }
    // danh sách id người chơi hết thời gian trong lượt thi
    public List<int> ExamTimeoutPlayers = new();
    // lưu kết quả kết thúc trận để gửi cho client
    public List<OverGameRequest> LastOverGameResults = new();
    private readonly Dictionary<BallDamageKey, float> pendingBallDamages = new();
    private readonly HashSet<int> pendingBotLeavePlayerIds = new();
   // [Header("Animator CONFIG")]
   // public Animator fingerAnimator;
   // public Animator animatorPlayer;
    [Header("MAP VIEW")]
    public GameObject playerArrowPrefab;
    private List<Vector3> usedPositions = new List<Vector3>();
    //public bool IsStartedGame { get; set; }
    //public bool isReadyToShoot = false;

    private NetworkObject RingBallPrefab => GameServerInitializer.Instance != null ? GameServerInitializer.Instance.RingBallPrefab : null;
  
    private void Awake()
    {
        Instance = this;
        HasBroadcastGameOverResults = false;
        lastMatchProgressRealtime = Time.realtimeSinceStartup;
        if (NetworkObjectManager.Instance != null && InactiveBallContainer != null)
        {
            NetworkObjectManager.Instance.SetInactiveBallContainer(InactiveBallContainer);
        }

        InitializeRingBallsFromScene();
    }

    private void FixedUpdate()
    {
        TickServerWaterFallback();
    }

    public override void FixedUpdateNetwork()
    {
        TickServerWaterFallback();
    }

    private void TickServerWaterFallback()
    {
        if (!enableServerWaterFallback)
            return;

        if (Time.time < _nextWaterFallbackCheckTime)
            return;

        _nextWaterFallbackCheckTime = Time.time + Mathf.Max(0.02f, waterFallbackCheckInterval);
        TryResolveWaterObject(logFailure: true);
        EvaluateWaterFallbackForActiveBalls();
    }

    private void InitializeRingBallsFromScene()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        manager.ringBalls.RefreshFromScene(playArea);
    }

    public void OnGameStartExamPhase()
    {
        if (hasProcessedGameStart)
            return;

        hasProcessedGameStart = true;
        ExamTimeoutPlayers.Clear();
        examShotStartedPlayerIds.Clear();
        pendingExamShotCommitRealtimeByPlayer.Clear();
        PrepareAllActivePlayersForExamPhase();

        EnsureBetDeduction();
        EnsureAbandonedRoomMonitor();
        StartExamShotTimeoutWatchdog();

        // ─── Bot Exam: đặt trạng thái và tự động bắn thi cho bot ───
        var botCtrl = BotPlayerController.Instance;
        if (botCtrl != null && botCtrl.HasBots)
        {
            StartCoroutine(TriggerBotExamShots(botCtrl));
        }
    }

    private IEnumerator TriggerBotExamShots(BotPlayerController botCtrl)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            yield break;

        // Đặt statusPlayer = ShootExam và isHolding = true cho tất cả bot
        for (int i = 0; i < manager.players.Length; i++)
        {
            var info = manager.players.Get(i);
            if (info.playerId == 0 || !botCtrl.IsBotPlayer(info.playerId))
                continue;

            info.statusPlayer = StatusPlayer.ShootExam;
            info.isHolding = true;
            manager.players.Set(i, info);

            var playerObject = manager.GetPlayerObject(info.playerId);
            var handler = playerObject != null ? playerObject.GetComponent<PlayerNetworkHandler>() : null;
            if (handler != null)
            {
                handler.PlayerModel = info;
                handler.CurrentAnimState = CharacterAnimState.SitToShoot;
            }

            var ballObj = manager.GetActiveBallObject(info.playerId);
            if (ballObj != null && ballObj.TryGetComponent<BallServerController>(out var ballCtrl))
            {
                ballCtrl.IsHolding = 1;
            }
        }

        // Chờ 1 giây để đảm bảo đồng bộ xong
        yield return new WaitForSeconds(1f);

        // Trigger exam shot cho từng bot
        foreach (var botId in botCtrl.BotIds)
        {
            StartCoroutine(botCtrl.ExecuteBotExamShot(botId));
        }
    }

    private void PrepareAllActivePlayersForExamPhase()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
            return;

        manager.IsExamScoreReady = false;

        for (int i = 0; i < manager.players.Length; i++)
        {
            var info = manager.players.Get(i);
            if (info.playerId <= 0 ||
                info.isDestroy ||
                info.statusPlayer == StatusPlayer.Destroy ||
                info.statusPlayer == StatusPlayer.WaitingDestroy)
            {
                continue;
            }

            info.statusPlayer = StatusPlayer.ShootExam;
            info.isHolding = true;
            info.distance = 0f;
            info.scoreExam = 0f;
            manager.players.Set(i, info);

            var playerObject = manager.GetPlayerObject(info.playerId);
            var handler = playerObject != null ? playerObject.GetComponent<PlayerNetworkHandler>() : null;
            if (handler != null)
            {
                handler.PlayerModel = info;
                handler.CurrentAnimState = CharacterAnimState.SitToShoot;
            }

            var ballObj = manager.GetActiveBallObject(info.playerId);
            if (ballObj != null && ballObj.TryGetComponent<BallServerController>(out var ballCtrl))
            {
                ballCtrl.IsHolding = 1;
                ballCtrl.hasBeenShoot = 0;
            }
        }

        LogExamStateSnapshot("Prepared all active players for exam phase");
    }

    private void StartExamShotTimeoutWatchdog()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (examShotTimeoutRoutine != null)
        {
            StopCoroutine(examShotTimeoutRoutine);
            examShotTimeoutRoutine = null;
        }

        examShotTimeoutRoutine = StartCoroutine(ExamShotTimeoutWatchdogRoutine());
    }

    private void StopExamShotTimeoutWatchdog(string reason)
    {
        if (examShotTimeoutRoutine != null)
        {
            StopCoroutine(examShotTimeoutRoutine);
            examShotTimeoutRoutine = null;
            Debug.Log($"[HOST][ExamTimeout] Dừng watchdog lượt thi: {reason}");
        }

        var manager = NetworkObjectManager.Instance;
        if (manager != null && manager.HasStateAuthority && manager.StatusLoading == StatusLoadingGame.isExam)
            manager.StopTurnTimer(reason);
    }

    private IEnumerator ExamShotTimeoutWatchdogRoutine()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
        {
            examShotTimeoutRoutine = null;
            yield break;
        }

        float timeoutSeconds = Mathf.Max(1f, examShotTimeoutSeconds);
        manager.StartTurnTimer(timeoutSeconds);
        Debug.Log($"[HOST][ExamTimeout] Bắt đầu timer lượt thi {timeoutSeconds:0.#} giây.");

        float endTime = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup < endTime)
        {
            if (IsGameEnded ||
                manager.StatusLoading != StatusLoadingGame.isExam ||
                manager.IsExamScoreReady)
            {
                examShotTimeoutRoutine = null;
                yield break;
            }

            if (AreAllExamPlayersFinished(false))
            {
                examShotTimeoutRoutine = null;
                manager.StopTurnTimer("all exam players finished before timeout");
                StartExamScoreResolution();
                yield break;
            }

            yield return null;
        }

        examShotTimeoutRoutine = null;
        ApplyExamTimeoutsForPlayersStillWaiting();
    }

    private void ApplyExamTimeoutsForPlayersStillWaiting()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null ||
            !manager.HasStateAuthority ||
            manager.StatusLoading != StatusLoadingGame.isExam ||
            manager.IsExamScoreReady ||
            IsGameEnded)
        {
            return;
        }

        var timedOutPlayerIds = new List<int>();
        var players = manager.players;
        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (!ShouldApplyServerExamTimeout(info))
                continue;

            timedOutPlayerIds.Add(info.playerId);
        }

        if (timedOutPlayerIds.Count == 0)
        {
            float retryDelay = GetPendingExamShotCommitRetryDelay();
            if (retryDelay > 0f && examShotTimeoutRoutine == null)
            {
                Debug.Log($"[HOST][ExamTimeout] Hết timer thi nhưng còn shot pending do mạng yếu, chờ thêm {retryDelay:0.00}s trước khi chốt timeout.");
                examShotTimeoutRoutine = StartCoroutine(DelayedExamTimeoutRecheck(retryDelay));
                return;
            }

            Debug.Log("[HOST][ExamTimeout] Hết timer thi nhưng không còn người chơi nào đang giữ bi chưa bắn.");
            return;
        }

        foreach (int playerId in timedOutPlayerIds)
        {
            var handler = GetPlayerObject(playerId)?.GetComponent<PlayerNetworkHandler>();
            if (handler != null)
            {
                ApplyExamTimeoutScore(handler);
            }
            else
            {
                ApplyExamTimeoutScore(playerId);
            }

            RegisterExamTimeoutPlayer(playerId);
        }

        Debug.Log($"[HOST][ExamTimeout] Chốt 0 điểm do quá {examShotTimeoutSeconds:0.#}s chưa bắn: {string.Join(", ", timedOutPlayerIds)}");
        LogExamStateSnapshot("After server exam timeout watchdog");

        if (AreAllExamPlayersFinished(true))
        {
            Debug.Log("[HOST][ExamTimeout] Tất cả người chơi đã hoàn tất/timeout lượt thi sau watchdog.");
            StartExamScoreResolution();
        }
    }

    private bool ShouldApplyServerExamTimeout(PlayerInfoStruct info)
    {
        if (info.playerId <= 0 ||
            info.isDestroy ||
            info.statusPlayer == StatusPlayer.Destroy ||
            info.statusPlayer == StatusPlayer.WaitingDestroy ||
            info.statusPlayer == StatusPlayer.StartPoint)
        {
            return false;
        }

        if (info.statusPlayer != StatusPlayer.ShootExam && !info.isHolding)
            return false;

        if (HasExamShotStarted(info.playerId, info))
            return false;

        if (IsExamShotCommitGraceActive(info.playerId))
            return false;

        return true;
    }

    private IEnumerator DelayedExamTimeoutRecheck(float delaySeconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, delaySeconds));
        examShotTimeoutRoutine = null;
        ApplyExamTimeoutsForPlayersStillWaiting();
    }

    private bool IsExamShotCommitGraceActive(int playerId)
    {
        if (!pendingExamShotCommitRealtimeByPlayer.TryGetValue(playerId, out float committedAt))
            return false;

        float graceSeconds = Mathf.Max(0f, examShotCommitGraceSeconds);
        if (graceSeconds <= 0f)
            return false;

        return Time.realtimeSinceStartup - committedAt < graceSeconds;
    }

    private float GetPendingExamShotCommitRetryDelay()
    {
        float retryDelay = 0f;
        float graceSeconds = Mathf.Max(0f, examShotCommitGraceSeconds);
        if (graceSeconds <= 0f || pendingExamShotCommitRealtimeByPlayer.Count == 0)
            return 0f;

        foreach (var kvp in pendingExamShotCommitRealtimeByPlayer)
        {
            int playerId = kvp.Key;
            if (!TryGetNetworkPlayerInfo(playerId, out var info, out _))
                continue;

            if (!ShouldRemainWaitingForExamShotData(info))
                continue;

            float remaining = kvp.Value + graceSeconds - Time.realtimeSinceStartup;
            retryDelay = Mathf.Max(retryDelay, remaining);
        }

        return Mathf.Max(0f, retryDelay);
    }

    private bool ShouldRemainWaitingForExamShotData(PlayerInfoStruct info)
    {
        if (info.playerId <= 0 ||
            info.isDestroy ||
            info.statusPlayer == StatusPlayer.Destroy ||
            info.statusPlayer == StatusPlayer.WaitingDestroy ||
            info.statusPlayer == StatusPlayer.StartPoint)
        {
            return false;
        }

        if (info.statusPlayer != StatusPlayer.ShootExam && !info.isHolding)
            return false;

        return !HasExamShotStarted(info.playerId, info);
    }

    private bool HasExamShotStarted(int playerId, PlayerInfoStruct info)
    {
        if (examShotStartedPlayerIds.Contains(playerId))
            return true;

        if (!info.isHolding)
            return true;

        var ballObj = GetActiveBallObject(playerId);
        var ballCtrl = ballObj != null ? ballObj.GetComponent<BallServerController>() : null;
        if (ballCtrl == null)
            return false;

        return ballCtrl.hasBeenShoot == 1 || ballCtrl.IsHolding == 0;
    }

    private void MarkExamShotStarted(int playerId)
    {
        if (playerId <= 0)
            return;

        var manager = NetworkObjectManager.Instance;
        if (IsServerExamPhaseActive(manager) ||
            (TryGetNetworkPlayerInfo(playerId, out var info, out _) &&
             info.statusPlayer == StatusPlayer.ShootExam))
        {
            pendingExamShotCommitRealtimeByPlayer.Remove(playerId);
            examShotStartedPlayerIds.Add(playerId);
        }
    }

    private void EnsureBetDeduction()
    {
        if (betDeductionRoutine != null)
            return;

        betDeductionRoutine = StartCoroutine(DeductStartingBetsRoutine());
    }

    private IEnumerator DeductStartingBetsRoutine()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
        {
            betDeductionRoutine = null;
            yield break;
        }

        int maxPlayers = Mathf.Max(manager.rpgRoomModel.MaxPlayer, 1);
        int betPerPlayer = manager.rpgRoomModel.betCount / maxPlayers;

        if (betPerPlayer <= 0)
        {
            betDeductionRoutine = null;
            yield break;
        }

        var players = GetTurnOrderSnapshot();
        var botCtrl = BotPlayerController.Instance;
        var transactions = players
            .Where(p => p.playerId > 0 && (botCtrl == null || !botCtrl.IsBotPlayer(p.playerId)))
            .Select(p => new APIManager.BetDeductionEntry
            {
                userId = p.playerId,
                ringBall = betPerPlayer,
                money = 0,
                description = "Deduct bet at match start",
                eventType = "game_start"
            })
            .ToList();

        if (transactions.Count == 0)
        {
            betDeductionRoutine = null;
            yield break;
        }

        manager.RpcShowMesByUser($"Đã trừ {betPerPlayer} bi cược của mỗi người chơi khi bắt đầu trận.");

        bool success = false;
        var api = APIManager.Instance;
        if (api != null)
        {
            yield return StartCoroutine(api.RunTask(
                api.DeductBetsOnGameStartAsync(transactions),
                result => success = result));
        }
        else
        {
            Debug.LogWarning("⚠️ APIManager chưa sẵn sàng để trừ bi cược khi bắt đầu trận.");
        }

        if (api != null && !success)
        {
            Debug.LogWarning("⚠️ Không thể đồng bộ trừ bi cược khi bắt đầu trận.");
        }

        betDeductionRoutine = null;
    }

    private void EnsureAbandonedRoomMonitor()
    {
        if (abandonedRoomMonitorRoutine != null)
            StopCoroutine(abandonedRoomMonitorRoutine);

        abandonedRoomMonitorRoutine = StartCoroutine(MonitorAbandonedRoomRoutine());
    }

    private IEnumerator MonitorAbandonedRoomRoutine()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
        {
            abandonedRoomMonitorRoutine = null;
            yield break;
        }

        float emptyDuration = 0f;

        while (manager.StatusLoading != StatusLoadingGame.EndGame &&
               manager.StatusLoading >= StatusLoadingGame.isExam)
        {
            if (!manager.TryGetRoomRunner(out var runner, logError: false) || runner == null || !runner.IsRunning || runner.IsShutdown)
            {
                abandonedRoomMonitorRoutine = null;
                yield break;
            }

            UpdateBotOnlyResolutionTracking();

            int activeCount = runner.ActivePlayers.Count();
            if (activeCount <= 0)
            {
                emptyDuration += Time.deltaTime;
                if (emptyDuration >= AbandonedRoomTimeoutSeconds)
                {
                    HandleAbandonedRoomDueToNoPlayers();
                    abandonedRoomMonitorRoutine = null;
                    yield break;
                }
            }
            else
            {
                emptyDuration = 0f;
            }

            if (TryHandleSoloConnectedSurvivorTimeout(Time.deltaTime))
            {
                abandonedRoomMonitorRoutine = null;
                yield break;
            }

            yield return null;
        }

        ResetSoloConnectedSurvivorTracking();
        abandonedRoomMonitorRoutine = null;
    }

    public void ForceAbandonedRoomDueToNoPlayers()
    {
        HandleAbandonedRoomDueToNoPlayers();
    }

    private void HandleAbandonedRoomDueToNoPlayers()
    {
        if (abandonedRoomResolutionRoutine != null)
            return;

        abandonedRoomResolutionRoutine = StartCoroutine(ResolveAbandonedRoomRoutine());
    }

    private IEnumerator ResolveAbandonedRoomRoutine()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
        {
            abandonedRoomResolutionRoutine = null;
            yield break;
        }

        if (isProcessingEndGame)
        {
            abandonedRoomResolutionRoutine = null;
            yield break;
        }

        isProcessingEndGame = true;
        MarkGameEnded();
        manager.StatusLoading = StatusLoadingGame.EndGame;

        var players = manager.GetOrderedPlayerInfos();
        if (players.Count == 0)
        {
            isProcessingEndGame = false;
            abandonedRoomResolutionRoutine = null;
            yield break;
        }

        bool noActivePlayers = true;
        if (manager.TryGetRoomRunner(out var runner, logError: false) && runner != null)
        {
            noActivePlayers = runner.ActivePlayers.Count() <= 0;
        }

        bool drawForAll = noActivePlayers && players.Count > 1;

        int winnerId = players
            .OrderBy(p => p.turnOrder)
            .Select(p => p.playerId)
            .FirstOrDefault();

        int maxPlayers = Mathf.Max(manager.rpgRoomModel.MaxPlayer, 1);
        int betPerPlayer = manager.rpgRoomModel.betCount / maxPlayers;
        int rounds = (manager.TurnCount / maxPlayers) + 1;

        float maxLevel = players.Max(p => p.level);
        float minLevel = players.Min(p => p.level);
        float levelGap = maxLevel - minLevel;
        float winCoef = 1.5f + levelGap * 0.05f;
        float loseCoef = 0.5f + levelGap * 0.02f;

        LastOverGameResults.Clear();
        var postPayload = new List<OverGameRequest>();

        foreach (var info in players)
        {
            bool isWinner = !drawForAll && info.playerId == winnerId;
            bool isDraw = drawForAll;
            int marblesWon = isWinner ? manager.rpgRoomModel.betCount : 0;
            int marblesLost = isWinner || isDraw ? 0 : betPerPlayer;
            int expGained = 0;
            if (!isDraw && betPerPlayer > 0)
            {
                expGained = Mathf.RoundToInt(betPerPlayer * (isWinner ? winCoef : loseCoef));
            }

            var result = new OverGameRequest
            {
                playerId = info.playerId,
                tunrOrder = info.turnOrder,
                typeMatchGid = (int)manager.rpgRoomModel.TypeMatch,
                StatusWin = isDraw ? (int)StatusWin.Dickens : (isWinner ? (int)StatusWin.Win : (int)StatusWin.Lose),
                rounds = rounds,
                MapGame = manager.rpgRoomModel.gameScene.Value,
                MaxPlayer = manager.rpgRoomModel.MaxPlayer,
                marbBet = betPerPlayer,
                marblesWon = marblesWon,
                marblesLost = marblesLost,
                expGained = expGained,
                playerName = info.fullname.ToString(),
                description = isDraw ? "Auto-finish: phòng trống, xử lý hòa" : "Auto-finish: phòng không còn người chơi",
                avatarUrl = info.avatarUrl.ToString()
            };

            LastOverGameResults.Add(result);
            postPayload.Add(result);
        }

        if (APIManager.Instance != null && postPayload.Count > 0)
        {
            yield return StartCoroutine(APIManager.Instance.RunTask(
                APIManager.Instance.PostOverGame(postPayload),
                null));
        }

        yield return SyncBallDamageUpdates();

        if (LastOverGameResults.Count > 0)
        {
            var botCtrl = BotPlayerController.Instance;
            var realPlayerIds = LastOverGameResults.Select(x => x.playerId)
                .Where(pid => botCtrl == null || !botCtrl.IsBotPlayer(pid));
            BeginAwaitClientGameOverAcks(realPlayerIds);
            BeginAwaitClientDisconnectReadiness(realPlayerIds);
            string json = JsonHelper.ToJson(LastOverGameResults);
            manager.RpcShowOverGameResult(json);
            HasBroadcastGameOverResults = true;
        }

        isProcessingEndGame = false;
        abandonedRoomResolutionRoutine = null;
    }

    private bool TryHandleSoloConnectedSurvivorTimeout(float deltaTime)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null || manager.StatusLoading == StatusLoadingGame.EndGame || isProcessingEndGame)
        {
            ResetSoloConnectedSurvivorTracking();
            return false;
        }

        if (!TryGetSoloConnectedSurvivor(out int survivorPlayerId, out var disconnectedAlivePlayerIds))
        {
            ResetSoloConnectedSurvivorTracking();
            return false;
        }

        bool trackingChanged = pendingSoloSurvivorPlayerId != survivorPlayerId ||
                              pendingDisconnectedAlivePlayerIds.Count != disconnectedAlivePlayerIds.Count ||
                              !pendingDisconnectedAlivePlayerIds.SequenceEqual(disconnectedAlivePlayerIds);

        if (trackingChanged)
        {
            pendingSoloSurvivorPlayerId = survivorPlayerId;
            pendingSoloSurvivorElapsed = 0f;
            pendingDisconnectedAlivePlayerIds.Clear();
            pendingDisconnectedAlivePlayerIds.AddRange(disconnectedAlivePlayerIds);
            Debug.LogWarning($"⏳ [HOST][CheckEndGame] Chỉ còn 1 người chơi còn kết nối/đủ điều kiện (pid={survivorPlayerId}). Chờ {SoloConnectedSurvivorTimeoutSeconds:0}s để các player [{string.Join(", ", disconnectedAlivePlayerIds)}] reconnect trước khi xử thua.");
            return false;
        }

        pendingSoloSurvivorElapsed += Mathf.Max(0f, deltaTime);
        if (pendingSoloSurvivorElapsed < SoloConnectedSurvivorTimeoutSeconds)
            return false;

        Debug.LogWarning($"⌛ [HOST][CheckEndGame] Hết {SoloConnectedSurvivorTimeoutSeconds:0}s chờ reconnect cho các player [{string.Join(", ", pendingDisconnectedAlivePlayerIds)}]. Tiến hành xử thua và kết thúc trận cho pid={pendingSoloSurvivorPlayerId} thắng.");

        foreach (int playerId in pendingDisconnectedAlivePlayerIds.ToList())
        {
            ForceDestroyPlayerForReconnectTimeout(playerId);
        }

        ResetSoloConnectedSurvivorTracking();
        CheckEndGame();
        return IsGameEnded || isProcessingEndGame;
    }

    private bool TryGetSoloConnectedSurvivor(out int survivorPlayerId, out List<int> disconnectedAlivePlayerIds)
    {
        survivorPlayerId = 0;
        disconnectedAlivePlayerIds = new List<int>();

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return false;

        var alivePlayerIds = manager.GetOrderedPlayerInfos()
            .Where(IsEligibleTurnPlayer)
            .Select(info => info.playerId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (alivePlayerIds.Count <= 1)
            return false;

        var connectedAlivePlayerIds = new List<int>();
        foreach (int playerId in alivePlayerIds)
        {
            if (IsPlayerConnectedToCurrentMatch(playerId))
                connectedAlivePlayerIds.Add(playerId);
            else if (!IsBotPlayer(playerId))
                disconnectedAlivePlayerIds.Add(playerId);
        }

        if (connectedAlivePlayerIds.Count != 1 || disconnectedAlivePlayerIds.Count == 0)
            return false;

        survivorPlayerId = connectedAlivePlayerIds[0];
        disconnectedAlivePlayerIds.Sort();
        return true;
    }

    private bool IsPlayerConnectedToCurrentMatch(int playerId)
    {
        if (playerId <= 0)
            return false;

        if (IsBotPlayer(playerId))
            return true;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.TryGetRoomRunner(out var runner, logError: false) || runner == null || !runner.IsRunning || runner.IsShutdown)
            return false;

        var quickMatchServer = QuickMatchServer.Instance;
        if (quickMatchServer != null && quickMatchServer.Runner == runner && quickMatchServer.TryGetPlayerRefByUserId(playerId, out var playerRef) && !playerRef.IsNone)
            return runner.ActivePlayers.Contains(playerRef);

        var playerObject = manager.GetPlayerObject(playerId);
        var handler = playerObject != null ? playerObject.GetComponent<PlayerNetworkHandler>() : null;
        if (handler != null && handler.Object != null && !handler.Object.InputAuthority.IsNone)
            return runner.ActivePlayers.Contains(handler.Object.InputAuthority);

        return false;
    }

    private bool IsBotPlayer(int playerId)
    {
        return playerId > 0 && BotPlayerController.Instance != null && BotPlayerController.Instance.IsBotPlayer(playerId);
    }

    private void ForceDestroyPlayerForReconnectTimeout(int playerId)
    {
        if (playerId <= 0)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
            return;

        if (!TryGetNetworkPlayerInfo(playerId, out var info, out var index))
            return;

        if (info.isDestroy || info.statusPlayer == StatusPlayer.Destroy || info.statusPlayer == StatusPlayer.WaitingDestroy)
            return;

        string playerName = info.fullname.ToString();
        info.score = 0;
        info.statusPlayer = StatusPlayer.Destroy;
        info.isDestroy = true;
        manager.players.Set(index, info);

        var playerGO = GetPlayerObject(playerId);
        if (playerGO != null)
        {
            var handler = playerGO.GetComponent<PlayerNetworkHandler>();
            if (handler != null)
            {
                var model = handler.PlayerModel;
                model.score = 0;
                model.statusPlayer = StatusPlayer.Destroy;
                model.isDestroy = true;
                handler.PlayerModel = model;
            }
        }

        consecutiveTimeoutCounts.Remove(playerId);
        ApplyDefeatAnimation(playerId);
        RemoveDestroyedPlayerRepresentationImmediately(playerId, "reconnect_timeout");
        ScheduleBotAutoLeaveIfNeeded(playerId, "reconnect_timeout");

        if (!string.IsNullOrWhiteSpace(playerName))
            manager.RpcShowMesByUser($"{playerName} đã bị xử thua do không reconnect lại trận trong 30 giây.");
    }

    private void ResetSoloConnectedSurvivorTracking()
    {
        pendingSoloSurvivorPlayerId = 0;
        pendingSoloSurvivorElapsed = 0f;
        pendingDisconnectedAlivePlayerIds.Clear();
    }

    private void UpdateBotOnlyResolutionTracking()
    {
        if (IsGameEnded || !TryGetBotOnlyResolutionCandidates(out var aliveBotPlayerIds))
        {
            ResetBotOnlyResolutionTracking();
            return;
        }

        if (isBotOnlyResolutionArmed)
            return;

        isBotOnlyResolutionArmed = true;
        botOnlyCompletedTurns = 0;
        botOnlyTurnsBeforeForcedLoss = UnityEngine.Random.Range(
            MinBotOnlyTurnsBeforeForcedLoss,
            MaxBotOnlyTurnsBeforeForcedLoss + 1);

        Debug.Log(
            $"🤖 [HOST][BotOnly] Không còn client thật trong trận; còn BOT [{string.Join(", ", aliveBotPlayerIds)}]. " +
            $"Sẽ xử thua ngẫu nhiên 1 BOT sau {botOnlyTurnsBeforeForcedLoss} lượt BOT hoàn tất.");
    }

    private bool TryGetBotOnlyResolutionCandidates(out List<int> aliveBotPlayerIds)
    {
        aliveBotPlayerIds = new List<int>();

        var manager = NetworkObjectManager.Instance;
        var botController = BotPlayerController.Instance;
        if (manager == null || botController == null)
            return false;

        bool hasRealParticipant = false;
        bool hasConnectedRealParticipant = false;
        bool hasAliveRealParticipant = false;

        foreach (var info in manager.GetOrderedPlayerInfos())
        {
            if (info.playerId <= 0)
                continue;

            if (botController.IsBotPlayer(info.playerId))
            {
                if (IsEligibleTurnPlayer(info))
                    aliveBotPlayerIds.Add(info.playerId);

                continue;
            }

            hasRealParticipant = true;
            hasConnectedRealParticipant |= IsPlayerConnectedToCurrentMatch(info.playerId);
            hasAliveRealParticipant |= IsEligibleTurnPlayer(info);
        }

        return hasRealParticipant &&
               !hasConnectedRealParticipant &&
               !hasAliveRealParticipant &&
               aliveBotPlayerIds.Count == 2;
    }

    private void ResetBotOnlyResolutionTracking()
    {
        isBotOnlyResolutionArmed = false;
        botOnlyCompletedTurns = 0;
        botOnlyTurnsBeforeForcedLoss = 0;
    }

    private bool TryResolveBotOnlyMatchAfterCompletedTurn(int completedPlayerId)
    {
        UpdateBotOnlyResolutionTracking();

        if (!isBotOnlyResolutionArmed || !IsBotPlayer(completedPlayerId))
            return false;

        if (!TryGetBotOnlyResolutionCandidates(out var aliveBotPlayerIds))
        {
            ResetBotOnlyResolutionTracking();
            return false;
        }

        botOnlyCompletedTurns++;
        if (botOnlyCompletedTurns < botOnlyTurnsBeforeForcedLoss)
        {
            Debug.Log(
                $"🤖 [HOST][BotOnly] BOT {completedPlayerId} hoàn tất lượt {botOnlyCompletedTurns}/{botOnlyTurnsBeforeForcedLoss}; " +
                "tiếp tục mô phỏng lượt còn lại.");
            return false;
        }

        int loserPlayerId = aliveBotPlayerIds[UnityEngine.Random.Range(0, aliveBotPlayerIds.Count)];
        string loserName = TryGetNetworkPlayerInfo(loserPlayerId, out var loserInfo, out _)
            ? loserInfo.fullname.ToString()
            : loserPlayerId.ToString();

        Debug.Log(
            $"🤖 [HOST][BotOnly] Đủ {botOnlyCompletedTurns} lượt sau khi client rời trận. " +
            $"Xử thua ngẫu nhiên BOT {loserName} (pid={loserPlayerId}) để kết thúc trận.");

        BotPlayerController.Instance?.CancelBotTurn(loserPlayerId, "bot-only resolution after all real clients left");
        SetPlayerStatus(loserPlayerId, StatusPlayer.Destroy);
        ResetBotOnlyResolutionTracking();
        return IsGameEnded;
    }

    public void SetPlayAreaGuardActive(bool active)
    {
        if (playAreaGuard == null)
            return;

        if (playAreaGuard.gameObject.activeSelf != active)
            playAreaGuard.gameObject.SetActive(active);
    }

    public void RefreshBananaSpawnPointsFromScene()
    {
        // Xóa danh sách cũ để đảm bảo dữ liệu chuẩn bị lấy lại từ scene là mới nhất.
        BananaSpawnPoints.Clear();

        // Tìm tất cả GameObject trong scene có gán tag "BananaSpawn" làm vị trí spawn chuối.
        var spawnObjects = GameObject.FindGameObjectsWithTag("BananaSpawn");
        if (spawnObjects == null || spawnObjects.Length == 0)
        {
            // Nếu không tìm thấy gì thì thoát sớm, giữ danh sách rỗng.
            return;
        }

        // Sắp xếp danh sách theo trục X để các chỉ số ổn định giữa các lần đồng bộ.
        Array.Sort(spawnObjects, (a, b) =>
        {
            if (a == null && b == null)
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            return a.transform.position.x.CompareTo(b.transform.position.x);
        });

        // Thêm từng transform hợp lệ vào danh sách điểm spawn, tránh thêm trùng lặp.
        foreach (var obj in spawnObjects)
        {
            if (obj == null)
                continue;

            var transform = obj.transform;
            if (transform != null && !BananaSpawnPoints.Contains(transform))
                BananaSpawnPoints.Add(transform);
        }

        // Khi đổi danh sách spawn thì cũng reset danh sách các banana đang bật.
        ActiveBananaPeels.Clear();
    }

    public void RegisterBallDamage(int playerId, int itemId, int seq, float damageDelta)
    {
        if (damageDelta <= 0f || playerId <= 0 || itemId <= 0 || seq < 0)
            return;

        var key = new BallDamageKey(playerId, itemId, seq);
        if (pendingBallDamages.TryGetValue(key, out var current))
            pendingBallDamages[key] = current + damageDelta;
        else
            pendingBallDamages[key] = damageDelta;
    }

    public void SetBananaPeelsActiveByIndices(IReadOnlyList<int> activeIndices)
    {
        // ...existing code...
        // Chuyển danh sách chỉ số được yêu cầu bật sang HashSet để kiểm tra nhanh và tránh trùng.
        var indices = new HashSet<int>();
        if (activeIndices != null)
        {
            foreach (var index in activeIndices)
            {
                // Chỉ nhận các chỉ số hợp lệ nằm trong phạm vi danh sách spawn.
                if (index >= 0 && index < BananaSpawnPoints.Count)
                    indices.Add(index);
            }
        }

        // Reset danh sách object đang bật để chuẩn bị cập nhật lại theo yêu cầu mới.
        ActiveBananaPeels.Clear();

        // Duyệt từng điểm spawn và bật/tắt tương ứng với tập chỉ số đã chọn.
        for (int i = 0; i < BananaSpawnPoints.Count; i++)
        {
            var point = BananaSpawnPoints[i];
            if (point == null)
                continue;

            var go = point.gameObject;
            if (go == null)
                continue;

            // Nếu chỉ số nằm trong tập đã chọn thì bật, ngược lại tắt.
            bool shouldBeActive = indices.Contains(i);
            if (go.activeSelf != shouldBeActive)
                go.SetActive(shouldBeActive);

            // Lưu lại các object đang bật để phục vụ các logic khác.
            if (shouldBeActive)
                ActiveBananaPeels.Add(go);
        }
    }

    public void CheckTurnLimitGameOver()
    {
        var server = NetworkObjectManager.Instance;
        int currentRound = server.TurnCount / server.rpgRoomModel.MaxPlayer;
        if (currentRound >= server.rpgRoomModel.MaxRound && !IsGameEnded)
        {
            var snapshot = GetTurnOrderSnapshot();
            var topPlayer = snapshot
                .Select(e =>
                {
                    var go = GetPlayerObject(e.playerId);
                    if (go == null)
                        return null;

                    var handler = go.GetComponent<PlayerNetworkHandler>();
                    return handler == null
                        ? null
                        : new { e.playerId, handler.PlayerModel.score, handler };
                })
                .Where(x => x != null)
                .OrderByDescending(x => x.score)
                .FirstOrDefault();

            if (topPlayer != null)
            {
                int remain = server.ringBalls.Count;
                if (remain > 0)
                    AddScorePlayer(topPlayer.playerId, remain);
            }

            MarkGameEnded();
            HandleEndGame(0);
        }
    }

    public void HandelNextTurn(int completedPlayerId = 0)
    {
 
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("⚠️ [HOST] Không thể chuyển lượt vì NetworkObjectManager chưa được khởi tạo.");
            return;
        }

        if (!manager.isActiveAndEnabled || !manager.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("⚠️ [HOST] NetworkObjectManager không hoạt động, bỏ qua yêu cầu chuyển lượt.");
            return;
        }

        if (manager.rpgRoomModel.MaxPlayer <= 0)
        {
            Debug.LogWarning("⚠️ [HOST] Không thể chuyển lượt vì số lượng người chơi không hợp lệ.");
            return;
        }

        manager.RPC_ToggleMiniCamera(false);
        ResetTurnCombatSkillState("next_turn");

        int currentTurnOrder = ResolveCurrentTurnOrderForAdvance(manager, completedPlayerId);
        RepairEligibleTurnOrdersIfNeeded(manager, ref currentTurnOrder, completedPlayerId);

        if (!TryGetNextEligibleTurnOrder(currentTurnOrder, out var nextTurnOrder))
        {
            Debug.LogWarning("⚠️ [HOST] Không tìm thấy người chơi hợp lệ cho lượt tiếp theo.");
            CheckEndGame();
            return;
        }

        if (completedPlayerId > 0 &&
            TryGetNetworkPlayerInfo(completedPlayerId, out var completedInfo, out _) &&
            IsAliveTurnPlayer(completedInfo) &&
            completedInfo.turnOrder == nextTurnOrder)
        {
            Debug.LogWarning($"⚠️ [HOST] Chặn chuyển lượt quay lại chính player {completedPlayerId}. currentOrder={currentTurnOrder}, nextOrder={nextTurnOrder}. Kiểm tra lại danh sách lượt.");
            CheckEndGame();
            return;
        }

        manager.currentPlayerIndex = nextTurnOrder;

       // manager.StatusLoading = StatusLoadingGame.None;

       

        CheckTurnLimitGameOver();
        if (IsGameEnded)
        {
            Debug.Log("🏁 [HOST] Game đã kết thúc sau khi kiểm tra giới hạn lượt. Hủy chuyển lượt tiếp theo.");
            return;
        }

        manager.StatusLoading = StatusLoadingGame.NextTurn;
        manager.TurnCount++;
        manager.RoundCount++;
        manager.PrepareTurnTimerForPlayerReady("next_turn");
        StartCurrentTurnPlayerAtStartPointIfNeeded("next_turn");

        TryStartBotTurnForCurrentPlayer("next_turn");
    }

    public void HandlePlayerTurnTimeout(int playerId)
    {
        if (IsGameEnded)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        var playerGO = GetPlayerObject(playerId);
        if (playerGO == null)
            return;

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
            return;

        if (handler.PlayerModel.statusPlayer == StatusPlayer.ShootExam)
        {
            if (!ShouldApplyServerExamTimeout(handler.PlayerModel))
            {
                Debug.Log($"[HOST][ExamTimeout] Bỏ qua timeout client cho player {playerId} vì đã bắt đầu bắn thi hoặc không còn hợp lệ.");
                return;
            }

            HandleExamTurnTimeout(handler);
            return;
        }

        if (!manager.IsYourTurn(playerId))
            return;

        handler.CurrentAnimState = CharacterAnimState.Sleeping;

        // Tăng bộ đếm timeout liên tiếp
        if (!consecutiveTimeoutCounts.ContainsKey(playerId))
            consecutiveTimeoutCounts[playerId] = 0;
        consecutiveTimeoutCounts[playerId]++;

        int timeoutCount = consecutiveTimeoutCounts[playerId];
        Debug.Log($"⏱️ [HOST] Người chơi {playerId} timeout lần {timeoutCount}/{MaxConsecutiveTimeouts}");

        // Nếu timeout liên tiếp >= MaxConsecutiveTimeouts => kick người chơi
        if (timeoutCount >= MaxConsecutiveTimeouts)
        {
            Debug.Log($"🚫 [HOST] Người chơi {playerId} bị kick do timeout {timeoutCount} lần liên tiếp!");
            KickPlayerForConsecutiveTimeout(playerId);
            return;
        }

        HandelNextTurn(playerId);
    }

    private void TryStartBotTurnForCurrentPlayer(string reason)
    {
        if (IsGameEnded)
            return;

        var manager = NetworkObjectManager.Instance;
        var botCtrl = BotPlayerController.Instance;
        if (manager == null || botCtrl == null)
            return;

        var currentEntry = manager.GetOrderedPlayerInfos()
            .FirstOrDefault(x => x.turnOrder == manager.currentPlayerIndex);
        if (currentEntry.playerId == 0 || !botCtrl.IsBotPlayer(currentEntry.playerId))
            return;

        var playerObj = manager.GetPlayerObject(currentEntry.playerId);
        var handler = playerObj != null ? playerObj.GetComponent<PlayerNetworkHandler>() : null;
        if (handler == null)
        {
            Debug.LogWarning($"🤖 [BOT] Không thể bắt đầu lượt bot {currentEntry.playerId} ({reason}) vì thiếu PlayerNetworkHandler.");
            return;
        }

        var model = handler.PlayerModel;
        if (model.isDestroy || model.statusPlayer == StatusPlayer.Destroy || model.statusPlayer == StatusPlayer.WaitingDestroy)
        {
            Debug.Log($"🤖 [BOT] Bỏ qua bot {currentEntry.playerId} ({reason}) vì đã bị loại.");
            HandelNextTurn(currentEntry.playerId);
            return;
        }

        //botCtrl.CancelBotTurn(currentEntry.playerId, $"restart:{reason}:{model.statusPlayer}");

        if (model.statusPlayer == StatusPlayer.ShootExam)
        {
            Debug.Log($"🤖 [BOT] Lượt của bot {currentEntry.playerId} ({reason}) ở phase thi, bắt đầu auto exam shot.");
            StartCoroutine(botCtrl.ExecuteBotExamShot(currentEntry.playerId));
            return;
        }

        Debug.Log($"🤖 [BOT] Lượt của bot {currentEntry.playerId} ({reason}), bắt đầu auto-shot.");
        StartCoroutine(botCtrl.ExecuteBotTurnShot(currentEntry.playerId));
    }

    /// <summary>
    /// Kick người chơi ra khỏi phòng do timeout liên tiếp.
    /// Áp dụng logic thua giống như đầu hàng (surrender).
    /// Nếu không còn ai trong phòng thì kết thúc game luôn.
    /// </summary>
    private void KickPlayerForConsecutiveTimeout(int playerId)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        var playerGO = GetPlayerObject(playerId);
        if (playerGO == null)
            return;

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
            return;

        var model = handler.PlayerModel;
        string playerName = model.fullname.ToString();
        int score = model.score;

        // Đánh dấu người chơi bị loại (thua) - giống logic surrender
        model.score = 0;
        model.statusPlayer = StatusPlayer.Destroy;
        model.isDestroy = true;
        handler.PlayerModel = model;

        // Cập nhật vào NetworkArray
        if (manager.HasStateAuthority && TryGetNetworkPlayerInfo(playerId, out var netInfo, out var netIndex))
        {
            netInfo.score = 0;
            netInfo.statusPlayer = StatusPlayer.Destroy;
            netInfo.isDestroy = true;
            manager.players.Set(netIndex, netInfo);
        }

        ApplyDefeatAnimation(playerId);
        RemoveDestroyedPlayerRepresentationImmediately(playerId, "timeout");
        ScheduleBotAutoLeaveIfNeeded(playerId, "timeout");

        // Xoá bộ đếm timeout
        consecutiveTimeoutCounts.Remove(playerId);

        // Thông báo cho tất cả client
        BroadcastImpactAnnouncement(playerName, "noti_player_eliminated_timeout", 1.6f);
        manager.RpcShowMesByUser($"{playerName} đã bị loại do không hành động 2 lượt liên tiếp!");

        // Đếm số người chơi còn sống
        var snapshot = GetTurnOrderSnapshot();
        int alive = snapshot.Count(t =>
        {
            var go = GetPlayerObject(t.playerId);
            if (go == null)
                return false;

            var h = go.GetComponent<PlayerNetworkHandler>();
            if (h == null)
                return false;

            var m = h.PlayerModel;
            return !m.isDestroy && m.statusPlayer != StatusPlayer.Destroy && m.statusPlayer != StatusPlayer.WaitingDestroy;
        });

        bool endGame = alive <= 1;

        if (!endGame)
        {
            // Trả lại bi đã ăn nếu có
            if (score > 0)
                AddRingBalls(score);

            manager.isContinueTurn = true;
            manager.RpcShowPlayerList_Online();

            // Chuyển sang lượt tiếp theo
            HandelNextTurn(playerId);
        }
        else
        {
            // Nếu chỉ còn 1 người => người đó thắng
            if (alive == 1)
            {
                var totalScore = manager.rpgRoomModel.betCount;
                var survivor = snapshot.FirstOrDefault(t =>
                {
                    var go = GetPlayerObject(t.playerId);
                    if (go == null)
                        return false;

                    var h = go.GetComponent<PlayerNetworkHandler>();
                    if (h == null)
                        return false;

                    var m = h.PlayerModel;
                    return !m.isDestroy && m.statusPlayer != StatusPlayer.Destroy && m.statusPlayer != StatusPlayer.WaitingDestroy;
                });

                if (survivor != null)
                {
                    AddScorePlayer(survivor.playerId, totalScore);
                    manager.RpcShowPlayerList_Online();
                }
            }

            MarkGameEnded();

            // alive == 0 => không còn ai, vẫn gọi HandleEndGame để hiển thị kết quả
            // alive == 1 => còn 1 người thắng, kết thúc bình thường
            HandleEndGame(playerId);
        }
    }

    /// <summary>
    /// Reset bộ đếm timeout liên tiếp khi người chơi thực hiện hành động (bắn bi).
    /// </summary>
    public void ResetConsecutiveTimeout(int playerId)
    {
        if (playerId <= 0)
            return;

        if (consecutiveTimeoutCounts.ContainsKey(playerId))
        {
            consecutiveTimeoutCounts.Remove(playerId);
            Debug.Log($"✅ [HOST] Reset bộ đếm timeout cho người chơi {playerId}");
        }
    }

    public void NotifyPlayerShotCommitted(int playerId)
    {
        if (!CanAcceptShotAction(playerId, out var manager))
            return;

        ResetConsecutiveTimeout(playerId);
        if (IsPlayerInExamPhase(playerId))
        {
            // RPC_NotifyShotCommitted được gửi ngay khi client thả nút để chạy animation.
            // Khi mạng yếu, RPC_RequestShot có thể tới sau animation event; nếu đánh dấu
            // examShotStarted ở đây thì server sẽ từ chối cú bắn thật và bi vẫn nằm trên tay.
            pendingExamShotCommitRealtimeByPlayer[playerId] = Time.realtimeSinceStartup;
            MarkMatchProgress($"ExamShotCommittedPending pid={playerId}");
            return;
        }

        manager.StopTurnTimerForPlayerAction(playerId, "shot committed");
    }

    public void NotifyPlayerShotStarted(int playerId)
    {
        if (!CanAcceptShotAction(playerId, out var manager))
            return;

        ResetConsecutiveTimeout(playerId);
        bool isExamShot = IsPlayerInExamPhase(playerId);
        if (isExamShot)
            MarkExamShotStarted(playerId);
        else
            manager.StopTurnTimerForPlayerAction(playerId, "shot started");

        SyncPlayerInfoState(playerId, info =>
        {
            info.isHolding = false;
            return info;
        });
        MarkMatchProgress($"ShotStarted pid={playerId}");
    }

    public bool CanAcceptPlayerShotAction(int playerId)
    {
        return CanAcceptShotAction(playerId, out _);
    }

    public bool CanAcceptPlayerMovementAction(int playerId, PlayerMovementRequestType requestType)
    {
        if (requestType != PlayerMovementRequestType.MoveToPlayArea)
            return true;

        var manager = NetworkObjectManager.Instance;
        if (playerId <= 0 || manager == null || !manager.HasStateAuthority || IsGameEnded)
            return false;

        if (!TryGetNetworkPlayerInfo(playerId, out var info, out _))
            return false;

        if (info.isDestroy ||
            info.statusPlayer == StatusPlayer.Destroy ||
            info.statusPlayer == StatusPlayer.WaitingDestroy ||
            info.statusPlayer == StatusPlayer.ShootExam ||
            IsServerExamPhaseActive(manager))
        {
            return false;
        }

        return manager.IsYourTurn(playerId);
    }

    private bool CanAcceptShotAction(int playerId, out NetworkObjectManager manager)
    {
        manager = NetworkObjectManager.Instance;
        if (playerId <= 0 || manager == null || !manager.HasStateAuthority || IsGameEnded)
            return false;

        if (!TryGetNetworkPlayerInfo(playerId, out var info, out _))
            return false;

        if (info.isDestroy ||
            info.statusPlayer == StatusPlayer.Destroy ||
            info.statusPlayer == StatusPlayer.WaitingDestroy)
        {
            return false;
        }

        if (IsServerExamPhaseActive(manager))
        {
            if (info.statusPlayer == StatusPlayer.StartPoint)
                return false;

            if (info.statusPlayer == StatusPlayer.ShootExam &&
                !examShotStartedPlayerIds.Contains(playerId))
            {
                return true;
            }

            return info.isHolding && !HasExamShotStarted(playerId, info);
        }

        if (info.statusPlayer == StatusPlayer.ShootExam)
            return true;

        return manager.IsYourTurn(playerId);
    }

    private bool IsPlayerInExamPhase(int playerId)
    {
        var manager = NetworkObjectManager.Instance;
        if (IsServerExamPhaseActive(manager) &&
            TryGetNetworkPlayerInfo(playerId, out var activeInfo, out _) &&
            activeInfo.playerId > 0 &&
            !activeInfo.isDestroy &&
            activeInfo.statusPlayer != StatusPlayer.Destroy &&
            activeInfo.statusPlayer != StatusPlayer.WaitingDestroy)
        {
            return true;
        }

        return TryGetNetworkPlayerInfo(playerId, out var info, out _) &&
            info.statusPlayer == StatusPlayer.ShootExam;
    }

    private void ApplyExamTimeoutScore(PlayerNetworkHandler handler)
    {
        if (handler == null)
            return;

        var model = handler.PlayerModel;
        pendingExamShotCommitRealtimeByPlayer.Remove(model.playerId);
        model.statusPlayer = StatusPlayer.StartPoint;
        model.isHolding = false;
        model.scoreExam = 0f;
        model.distance = 0f;
        handler.PlayerModel = model;
        handler.CurrentAnimState = CharacterAnimState.None;

        var manager = NetworkObjectManager.Instance;
        if (manager != null &&
            manager.HasStateAuthority &&
            TryGetNetworkPlayerInfo(model.playerId, out var info, out var index))
        {
            info.statusPlayer = StatusPlayer.StartPoint;
            info.isHolding = false;
            info.scoreExam = 0f;
            info.distance = 0f;
            manager.players.Set(index, info);
        }

        var ballObj = GetActiveBallObject(model.playerId);
        if (ballObj != null && ballObj.TryGetComponent<BallServerController>(out var ballCtrl))
        {
            ballCtrl.IsHolding = 0;
        }

        manager?.RpcShowPlayerList_Online();
    }

    private void ApplyExamTimeoutScore(int playerId)
    {
        if (playerId <= 0)
            return;

        pendingExamShotCommitRealtimeByPlayer.Remove(playerId);

        var manager = NetworkObjectManager.Instance;
        if (manager == null ||
            !manager.HasStateAuthority ||
            !TryGetNetworkPlayerInfo(playerId, out var info, out var index))
        {
            return;
        }

        info.statusPlayer = StatusPlayer.StartPoint;
        info.isHolding = false;
        info.scoreExam = 0f;
        info.distance = 0f;
        manager.players.Set(index, info);

        var ballObj = GetActiveBallObject(playerId);
        if (ballObj != null && ballObj.TryGetComponent<BallServerController>(out var ballCtrl))
            ballCtrl.IsHolding = 0;

        manager.RpcShowPlayerList_Online();
    }

    private void HandleExamTurnTimeout(PlayerNetworkHandler handler)
    {
        if (handler == null)
            return;

        int playerId = handler.PlayerModel.playerId;
        Debug.Log($"⏱️ [HOST][ExamScore] Player {playerId} hết giờ lượt thi, chốt 0 điểm.");

        ApplyExamTimeoutScore(handler);
        RegisterExamTimeoutPlayer(playerId);
        LogExamStateSnapshot($"Exam timeout pid={playerId}");

        if (AreAllExamPlayersFinished(true))
        {
            Debug.Log("[HOST][ExamScore] Tất cả người chơi đã hoàn tất bài thi sau timeout – bắt đầu chờ bi dừng để chấm điểm.");
            StartExamScoreResolution();
        }
    }

    private void RegisterExamTimeoutPlayer(int playerId)
    {
        if (playerId <= 0)
            return;

        if (!ExamTimeoutPlayers.Contains(playerId))
            ExamTimeoutPlayers.Add(playerId);
    }


    public void DespawnActiveBananaPeels(NetworkRunner runner)
    {
        for (int i = 0; i < ActiveBananaPeels.Count; i++)
        {
            var peel = ActiveBananaPeels[i];
            if (peel == null)
                continue;

            if (peel.activeSelf)
                peel.SetActive(false);
        }

        ActiveBananaPeels.Clear();
    }

    private bool TryConsumeBanana(GameObject peel)
    {
        if (peel == null)
            return false;

        int spawnIndex = -1;
        for (int i = 0; i < BananaSpawnPoints.Count; i++)
        {
            var point = BananaSpawnPoints[i];
            if (point != null && point.gameObject == peel)
            {
                spawnIndex = i;
                break;
            }
        }

        ActiveBananaPeels.Remove(peel);

        //var bananaPeel = peel.GetComponent<BananaPeel>();
        //if (bananaPeel != null)
        //    return bananaPeel.RequestConsume();

        if (!peel.activeSelf)
            return false;

        peel.SetActive(false);

        var manager = NetworkObjectManager.Instance;
        if (manager != null && manager.HasStateAuthority && spawnIndex >= 0)
            manager.NotifyBananaConsumed(spawnIndex);

        return true;
    }

    public void HandleBananaSlip(PlayerNetworkHandler handler, GameObject peel)
    {
        // Không có handler (player) thì không thể xử lý tiếp.
        if (handler == null)
            return;

        var model = handler.PlayerModel;
        // Người chơi đã bị loại thì bỏ qua, tránh gọi coroutine thừa.
        if (model.statusPlayer == StatusPlayer.Destroy || model.statusPlayer == StatusPlayer.WaitingDestroy)
            return;

        // Thử đánh dấu đã giẫm lên bẫy; nếu không còn hoạt động thì thôi.
        bool consumed = TryConsumeBanana(peel);

        if (!consumed)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager != null)
        {
            // Cấm tiếp tục lượt hiện tại và báo HUD để debug nhanh.
            manager.isContinueTurn = false;
            string playerName = model.fullname.ToString();
            if (string.IsNullOrWhiteSpace(playerName))
                playerName = $"Người chơi {model.playerId}";

            manager.RpcShowMesByUser($"{playerName} bị trượt vỏ chuối!");
        }

        // Đặt trạng thái animation sang trượt và bắt đầu chuỗi coroutine kết thúc lượt.
        var botCtrl = BotPlayerController.Instance;
        if (botCtrl != null && botCtrl.IsBotPlayer(model.playerId))
        {
            botCtrl.CancelBotTurn(model.playerId, "bot giẫm vỏ chuối nên phải hủy coroutine auto-shot hiện tại");
        }

        handler.CurrentAnimState = CharacterAnimState.Slipping;
        StartBananaSlipResetRoutine(handler);
        StartBananaSlipSequence(handler);
    }

    private void StartBananaSlipResetRoutine(PlayerNetworkHandler handler)
    {
        if (handler == null)
            return;

        int playerId = handler.PlayerModel.playerId;
        if (_activeBananaSlipRoutines.TryGetValue(playerId, out var routine) && routine != null)
            StopCoroutine(routine);

        Coroutine newRoutine = StartCoroutine(ResetBananaSlipAnimation(handler, playerId));
        _activeBananaSlipRoutines[playerId] = newRoutine;
    }

    private IEnumerator ResetBananaSlipAnimation(PlayerNetworkHandler handler, int playerId)
    {
        // Chờ hết thời lượng cinematic để đảm bảo client nhìn thấy hiệu ứng té.
        yield return new WaitForSeconds(BananaSlipCinematicDuration);

        // Sau khi kết thúc cinematic, nếu vẫn đang ở trạng thái trượt thì trả về Idle.
        if (handler != null && handler.CurrentAnimState == CharacterAnimState.Slipping)
            handler.CurrentAnimState = CharacterAnimState.HurtAfterSlip;

        // Xoá routine khỏi danh sách để lần trượt sau còn chạy được.
        _activeBananaSlipRoutines.Remove(playerId);
    }

    private void StartBananaSlipSequence(PlayerNetworkHandler handler)
    {
        // Nếu đang có coroutine chuyển lượt từ lần trượt trước thì huỷ, đảm bảo chỉ còn một luồng.
        if (_bananaSlipSequenceRoutine != null)
            StopCoroutine(_bananaSlipSequenceRoutine);

        _bananaSlipSequenceRoutine = StartCoroutine(HandleBananaSlipSequence(handler));
    }

    private IEnumerator HandleBananaSlipSequence(PlayerNetworkHandler handler)
    {
        // Đợi hiệu ứng trượt chạy xong.
        yield return new WaitForSeconds(BananaSlipCinematicDuration);
        Debug.Log("Trượt vỏ chuối.. chuyển sang lượt kế tiếp");
        // Khi cinematic kết thúc, buộc host chuyển sang người chơi kế tiếp.
        HandelNextTurn(handler.PlayerModel.playerId);
        _bananaSlipSequenceRoutine = null;
    }


    public void AddScorePlayer(int playerId, int score)
    {
        if (score == 0)
            return;

        var playerGO = GetPlayerObject(playerId);
        if (playerGO == null)
            return;

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
            return;

        var model = handler.PlayerModel;
        bool hadScore = model.score > 0;
        model.score += score;
        handler.PlayerModel = model;
        SyncPlayerInfoState(playerId, info =>
        {
            info.score = model.score;
            info.combo = model.combo;
            info.statusPlayer = model.statusPlayer;
            info.isDestroy = model.isDestroy;
            info.isHolding = model.isHolding;
            return info;
        });

        //if (!hadScore && model.score > 0)
        //{
        //    SkillManager.Instance?.CheckAddKillPermissionIcon(model.playerId, model.score);
        //}
    }

    private bool TryGetFinalPlayerInfo(PlayerInfoStruct baseInfo, out PlayerInfoStruct finalInfo)
    {
        finalInfo = baseInfo;
        if (baseInfo.playerId == 0)
            return false;

        var handler = GetPlayerObject(baseInfo.playerId)?.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
            return true;

        var model = handler.PlayerModel;
        finalInfo.level = model.level;
        finalInfo.fullname = model.fullname;
        finalInfo.avatarUrl = model.avatarUrl;
        finalInfo.RingBall = model.RingBall;
        finalInfo.score = model.score;
        finalInfo.scoreExam = model.scoreExam;
        finalInfo.combo = model.combo;
        finalInfo.statusPlayer = model.statusPlayer;
        finalInfo.distance = model.distance;
        finalInfo.isDestroy = model.isDestroy;
        finalInfo.isHolding = model.isHolding;
        finalInfo.turnOrder = model.turnOrder;
        return true;
    }

    private List<PlayerInfoStruct> GetFinalOrderedPlayerInfos()
    {
        var manager = NetworkObjectManager.Instance;
        var orderedPlayers = new List<PlayerInfoStruct>();
        if (manager == null)
            return orderedPlayers;

        foreach (var player in manager.GetOrderedPlayerInfos())
        {
            if (TryGetFinalPlayerInfo(player, out var finalInfo))
                orderedPlayers.Add(finalInfo);
        }

        orderedPlayers.Sort((a, b) => a.turnOrder.CompareTo(b.turnOrder));
        return orderedPlayers;
    }

    private void SyncPlayerInfoState(int playerId, Func<PlayerInfoStruct, PlayerInfoStruct> mutate)
    {
        if (mutate == null)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
            return;

        if (!TryGetNetworkPlayerInfo(playerId, out var info, out var index))
            return;

        info = mutate(info);
        manager.players.Set(index, info);

        var playerGO = GetPlayerObject(playerId);
        var handler = playerGO != null ? playerGO.GetComponent<PlayerNetworkHandler>() : null;
        if (handler != null)
            handler.PlayerModel = info;
    }

    private void RemoveDestroyedPlayerBallsImmediately(int playerId, string reason = null)
    {
        if (playerId <= 0)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
            return;

        if (!manager.TryGetRoomRunner(out var runner) || runner == null || !runner.IsRunning)
            return;

        var balls = manager.GetPlayerBalls(playerId)?.Where(ball => ball != null).ToList();
        if (balls == null || balls.Count == 0)
            return;

        foreach (var ball in balls)
        {
            manager.RemovePlayerBall(playerId, ball);
            runner.Despawn(ball);
        }

        manager.ClearPlayerBalls(playerId);
        Debug.Log($"🎱 [HOST] Đã loại bỏ ngay bi của playerId={playerId}, reason={reason ?? "destroyed"}");
    }

    private void HideDestroyedPlayerBallsImmediately(int playerId, string reason = null)
    {
        if (playerId <= 0)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
            return;

        var balls = manager.GetPlayerBalls(playerId)?.Where(ball => ball != null).ToList();
        if (balls == null || balls.Count == 0)
            return;

        foreach (var ball in balls)
        {
            var controller = ball.GetComponent<BallServerController>();
            if (controller == null)
                continue;

            controller.IsHolding = 0;
            controller.hasBeenShoot = 0;
            controller.RpcSetActive(0);
        }

        manager.SetActiveBallIndex(playerId, -1);
        manager.SetCurrentBallIndex(playerId, -1);
        Debug.Log($"🎱 [HOST] Đã ẩn bi của playerId={playerId}, reason={reason ?? "destroyed"}");
    }

    private void RemoveDestroyedPlayerRepresentationImmediately(int playerId, string reason = null)
    {
        if (playerId <= 0)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
            return;

        HideDestroyedPlayerBallsImmediately(playerId, reason);

        var playerObject = manager.GetPlayerObject(playerId);
        if (playerObject == null)
            return;

        var handler = playerObject.GetComponent<PlayerNetworkHandler>();
        handler?.RefreshDestroyedPresentationState();
        Debug.Log($"🧍 [HOST] Đã chuyển playerId={playerId} sang trạng thái ẩn, reason={reason ?? "destroyed"}");
    }

    private void TrySetPlayerAnimState(int playerId, CharacterAnimState state)
    {
        if (playerId <= 0)
            return;

        var playerObject = GetPlayerObject(playerId);
        if (playerObject == null)
            return;

        var handler = playerObject.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
            return;

        handler.CurrentAnimState = state;
    }

    private void ApplyDefeatAnimation(int playerId)
    {
        TrySetPlayerAnimState(playerId, CharacterAnimState.LoseEmotion);
    }

    private bool IsAliveTurnPlayer(PlayerInfoStruct info)
    {
        if (info.playerId <= 0)
            return false;

        if (info.isDestroy ||
            info.statusPlayer == StatusPlayer.Destroy ||
            info.statusPlayer == StatusPlayer.WaitingDestroy)
        {
            return false;
        }

        return true;
    }

    private bool IsEligibleTurnPlayer(PlayerInfoStruct info)
    {
        return IsAliveTurnPlayer(info) && info.turnOrder >= 0;
    }

    private int ResolveCurrentTurnOrderForAdvance(NetworkObjectManager manager, int completedPlayerId)
    {
        if (manager == null)
            return -1;

        if (completedPlayerId > 0 &&
            TryGetNetworkPlayerInfo(completedPlayerId, out var completedInfo, out _) &&
            completedInfo.turnOrder >= 0)
        {
            return completedInfo.turnOrder;
        }

        return manager.currentPlayerIndex;
    }

    private void RepairEligibleTurnOrdersIfNeeded(NetworkObjectManager manager, ref int currentTurnOrder, int completedPlayerId)
    {
        if (manager == null || !manager.HasStateAuthority)
            return;

        var alivePlayers = manager.GetOrderedPlayerInfos()
            .Where(IsAliveTurnPlayer)
            .ToList();

        if (alivePlayers.Count == 0)
            return;

        bool hasInvalidOrder = alivePlayers.Any(p => p.turnOrder < 0);
        bool hasDuplicateOrder = alivePlayers
            .Where(p => p.turnOrder >= 0)
            .GroupBy(p => p.turnOrder)
            .Any(group => group.Count() > 1);

        if (!hasInvalidOrder && !hasDuplicateOrder)
            return;

        int currentPlayerId = completedPlayerId;
        if (currentPlayerId <= 0)
        {
            int currentTurnOrderSnapshot = currentTurnOrder;
            var currentInfo = alivePlayers.FirstOrDefault(p => p.turnOrder == currentTurnOrderSnapshot);
            currentPlayerId = currentInfo.playerId;
        }

        var repairedOrder = alivePlayers
            .OrderBy(p => p.turnOrder < 0 ? int.MaxValue : p.turnOrder)
            .ThenBy(p => p.playerId)
            .ToList();

        for (int order = 0; order < repairedOrder.Count; order++)
        {
            ApplyTurnOrderToPlayer(repairedOrder[order].playerId, order);
        }

        if (currentPlayerId > 0 &&
            TryGetNetworkPlayerInfo(currentPlayerId, out var updatedCurrentInfo, out _) &&
            updatedCurrentInfo.turnOrder >= 0)
        {
            currentTurnOrder = updatedCurrentInfo.turnOrder;
        }

        Debug.LogWarning($"[HOST][TurnOrderRepair] Đã sửa turnOrder trước khi chuyển lượt. completedPlayer={completedPlayerId}, currentOrder={currentTurnOrder}, alive={repairedOrder.Count}");
        manager.RpcShowPlayerList_Online();
    }

    private bool TryGetNextEligibleTurnOrder(int currentTurnOrder, out int nextTurnOrder)
    {
        nextTurnOrder = -1;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return false;

        var ordered = manager.GetOrderedPlayerInfos()
            .Where(IsEligibleTurnPlayer)
            .OrderBy(p => p.turnOrder)
            .ToList();

        if (ordered.Count == 0)
            return false;

        if (ordered.Count == 1)
        {
            nextTurnOrder = ordered[0].turnOrder;
            return nextTurnOrder != currentTurnOrder;
        }

        foreach (var info in ordered)
        {
            if (info.turnOrder > currentTurnOrder)
            {
                nextTurnOrder = info.turnOrder;
                return true;
            }
        }

        nextTurnOrder = ordered[0].turnOrder;
        return true;
    }

    private void ScheduleBotAutoLeaveIfNeeded(int playerId, string reason = null)
    {
        if (playerId <= 0)
            return;

        var botController = BotPlayerController.Instance;
        if (botController == null || !botController.IsBotPlayer(playerId))
            return;

        if (!pendingBotLeavePlayerIds.Add(playerId))
            return;

        StartCoroutine(HandleBotAutoLeaveRoom(playerId, reason));
    }

    private IEnumerator HandleBotAutoLeaveRoom(int playerId, string reason)
    {
        float delay = UnityEngine.Random.Range(3f, 8f);
        yield return new WaitForSeconds(delay);

        pendingBotLeavePlayerIds.Remove(playerId);

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
            yield break;

        if (!TryGetNetworkPlayerInfo(playerId, out var playerInfo, out var playerIndex))
            yield break;

        string playerName = playerInfo.fullname.ToString();
        bool isDestroyed = playerInfo.isDestroy || playerInfo.statusPlayer == StatusPlayer.Destroy || playerInfo.statusPlayer == StatusPlayer.WaitingDestroy;
        if (!isDestroyed)
            yield break;

        if (manager.GetPlayerObject(playerId) == null)
            yield break;

        Debug.Log($"🤖 [BOT] Auto leave playerId={playerId}, name={playerName}, reason={reason ?? "destroyed"}, delay={delay:F2}s");

        var botController = BotPlayerController.Instance;
        botController?.CancelBotTurn(playerId, $"Auto leave: {reason ?? "destroyed"}");

        RemoveDestroyedPlayerBallsImmediately(playerId, $"bot_auto_leave:{reason ?? "destroyed"}");

        var runner = manager.Runner;
        var playerObject = manager.GetPlayerObject(playerId);
        if (playerObject != null)
        {
            manager.UnregisterPlayerObject(playerId, playerObject);
            if (runner != null && runner.IsRunning)
                runner.Despawn(playerObject);
        }

        if (playerIndex >= 0)
        {
            playerInfo.isDestroy = true;
            playerInfo.statusPlayer = StatusPlayer.Destroy;
            manager.players.Set(playerIndex, playerInfo);
        }

        // if (APIManager.Instance != null && manager.rpgRoomModel.roomId > 0)
        // {
        //     bool apiSuccess = false;
        //     yield return StartCoroutine(APIManager.Instance.RunTask(
        //         APIManager.Instance.LeaveRoomAsync(manager.rpgRoomModel.roomId, playerId),
        //         result => apiSuccess = result));

        //     Debug.Log($"🤖 [BOT] LeaveRoomAsync bot={playerId} room={manager.rpgRoomModel.roomId} success={apiSuccess}");
        // }
         Debug.Log($"🤖 [BOT] Skip leaveRooms API for bot={playerId}; chỉ dọn room local.");

        manager.RpcNotifyPlayerLeftInChat(playerName);
        manager.RpcShowPlayerList_Online();
    }

    public void SetPlayerStatus(int playerId, StatusPlayer status)
    {
        var manager = NetworkObjectManager.Instance;
        bool wasCurrentTurnPlayer = status == StatusPlayer.Destroy &&
                                    manager != null &&
                                    manager.HasStateAuthority &&
                                    manager.IsYourTurn(playerId);

        var playerGO = GetPlayerObject(playerId);
        var handler = playerGO != null ? playerGO.GetComponent<PlayerNetworkHandler>() : null;
        PlayerInfoStruct model = handler != null ? handler.PlayerModel : default;

        if (handler != null)
        {
            model.statusPlayer = status;

            if (status == StatusPlayer.Destroy)
            {
                model.score = 0;
                model.isDestroy = true;
            }

            handler.PlayerModel = model;
        }

        if (manager != null && manager.HasStateAuthority && TryGetNetworkPlayerInfo(playerId, out var info, out var index))
        {
            info.statusPlayer = status;
            if (status == StatusPlayer.Destroy)
            {
                info.score = 0;
                info.isDestroy = true;
            }
            else if (handler != null)
            {
                info.score = model.score;
                info.isDestroy = model.isDestroy;
            }
            manager.players.Set(index, info);
        }

        if (status == StatusPlayer.Destroy)
        {
            RemoveDestroyedPlayerRepresentationImmediately(playerId, "status_destroy");
            ScheduleBotAutoLeaveIfNeeded(playerId, "status_destroy");
            NetworkObjectManager.Instance?.CheckEndGame();
            TryAdvanceTurnAfterCurrentPlayerDestroyed(playerId, wasCurrentTurnPlayer, "status_destroy");
        }
 
    }

    private void TryAdvanceTurnAfterCurrentPlayerDestroyed(int playerId, bool wasCurrentTurnPlayer, string reason)
    {
        if (!wasCurrentTurnPlayer || playerId <= 0)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority || IsGameEnded)
            return;

        if (!TryGetNextEligibleTurnOrder(manager.currentPlayerIndex, out _))
        {
            CheckEndGame();
            return;
        }

        BotPlayerController.Instance?.CancelBotTurn(playerId, $"Current player destroyed: {reason}");
        manager.isContinueTurn = true;
        manager.RpcShowPlayerList_Online();
        Debug.Log($"[HOST][TurnAdvance] playerId={playerId} bị loại khi đang giữ lượt ({reason}) -> chuyển lượt ngay.");
        HandelNextTurn(playerId);
    }

    private const float DefaultChamCatSkillRadius = 0.5f;

    public void ResetTurnCombatSkillState(string reason = null)
    {
        grazeHitUseOrderByPlayer.Clear();
        catAnTienUseOrderByPlayer.Clear();
        counteredGrazeHitPlayers.Clear();
        counteredCatAnTienPlayers.Clear();
        pendingCatAnTienCounteredBroadcast.Clear();
        combatSkillUseSequence = 0;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
            return;

        for (int i = 0; i < manager.players.Length; i++)
        {
            var info = manager.players.Get(i);
            if (info.playerId <= 0 || info.isCatAnTienActive == 0)
                continue;

            info.isCatAnTienActive = 0;
            manager.players.Set(i, info);

            var playerGO = GetPlayerObject(info.playerId);
            var handler = playerGO != null ? playerGO.GetComponent<PlayerNetworkHandler>() : null;
            if (handler != null)
                handler.PlayerModel = info;
        }

        Debug.Log($"[HOST][SkillCounter] Reset turn combat skills. reason={reason ?? "unknown"}");
    }

    public bool HandleSkillUsageSync(int playerId, int skillId)
    {
        if (playerId <= 0)
            return false;

        if (ClientGameplayBridge.Skill.IsGrazeHitSkillId(skillId))
            return HandleGrazeHitSkill(playerId);

        if (skillId == (int)EffectPlayerType.CatAnTienSkill)
        {
            if (pendingCatAnTienCounteredBroadcast.TryGetValue(playerId, out bool isCountered))
            {
                pendingCatAnTienCounteredBroadcast.Remove(playerId);
                return isCountered;
            }

            return false;
        }

        return false;
    }

    private bool HandleGrazeHitSkill(int playerId)
    {
        int order = ++combatSkillUseSequence;
        grazeHitUseOrderByPlayer[playerId] = order;
        counteredGrazeHitPlayers.Remove(playerId);

        bool countered = HasEarlierEffectiveCatAnTienOpponent(playerId, order);
        if (countered)
            counteredGrazeHitPlayers.Add(playerId);

        Debug.Log($"[HOST][GrazeHit] pid={playerId} order={order} countered={countered}");
        return countered;
    }

    private bool RegisterCatAnTienSkillUse(int playerId)
    {
        int order = ++combatSkillUseSequence;
        catAnTienUseOrderByPlayer[playerId] = order;
        counteredCatAnTienPlayers.Remove(playerId);

        bool countered = HasEarlierEffectiveGrazeHitAttacker(playerId, order);
        if (countered)
            counteredCatAnTienPlayers.Add(playerId);

        pendingCatAnTienCounteredBroadcast[playerId] = countered;
        Debug.Log($"[HOST][CatAnTien] pid={playerId} order={order} countered={countered}");
        return countered;
    }

    private bool HasEarlierEffectiveCatAnTienOpponent(int playerId, int grazeOrder)
    {
        foreach (var kvp in catAnTienUseOrderByPlayer)
        {
            int opponentId = kvp.Key;
            int catOrder = kvp.Value;
            if (opponentId == playerId || catOrder >= grazeOrder || counteredCatAnTienPlayers.Contains(opponentId))
                continue;

            if (TryGetNetworkPlayerInfo(opponentId, out var info, out _) &&
                !info.isDestroy &&
                info.statusPlayer != StatusPlayer.Destroy &&
                info.statusPlayer != StatusPlayer.WaitingDestroy &&
                info.isCatAnTienActive == 1)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasEarlierEffectiveGrazeHitAttacker(int defenderId, int catOrder)
    {
        int attackerId = ResolveCurrentTurnPlayerId();
        if (attackerId <= 0 || attackerId == defenderId)
            return false;

        return grazeHitUseOrderByPlayer.TryGetValue(attackerId, out int grazeOrder) &&
               grazeOrder < catOrder &&
               !counteredGrazeHitPlayers.Contains(attackerId);
    }

    private int ResolveCurrentTurnPlayerId()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return 0;

        var current = manager.GetOrderedPlayerInfos()
            .FirstOrDefault(info => info.turnOrder == manager.currentPlayerIndex);
        return current.playerId;
    }

    public bool IsCatAnTienEffectiveAgainstAttacker(int defenderId, int attackerId)
    {
        if (defenderId <= 0 || attackerId <= 0)
            return false;

        if (counteredCatAnTienPlayers.Contains(defenderId))
            return false;

        if (!catAnTienUseOrderByPlayer.TryGetValue(defenderId, out int catOrder))
            return true;

        if (grazeHitUseOrderByPlayer.TryGetValue(attackerId, out int grazeOrder) &&
            !counteredGrazeHitPlayers.Contains(attackerId) &&
            grazeOrder < catOrder)
        {
            return false;
        }

        return true;
    }

    public float GetChamCatSkillRadius()
    {
        return ClientGameplayBridge.Skill.GetChamCatRadius(DefaultChamCatSkillRadius);
    }

    public bool CanUseChamCatOnTarget(int attackerId, int targetId, float? radiusOverride = null)
    {
        if (attackerId == 0 || targetId == 0 || attackerId == targetId)
            return false;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.IsYourTurn(attackerId))
            return false;

        var attackerGO = GetPlayerObject(attackerId);
        var targetGO = GetPlayerObject(targetId);
        var attackerBall = GetActiveBallObject(attackerId);
        var targetBall = GetActiveBallObject(targetId);
        if (attackerGO == null || targetGO == null || attackerBall == null || targetBall == null)
            return false;

        var attackerHandler = attackerGO.GetComponent<PlayerNetworkHandler>();
        var targetHandler = targetGO.GetComponent<PlayerNetworkHandler>();
        if (attackerHandler == null || targetHandler == null)
            return false;

        var attackerModel = attackerHandler.PlayerModel;
        if (attackerModel.isDestroy || attackerModel.statusPlayer == StatusPlayer.Destroy || attackerModel.statusPlayer == StatusPlayer.WaitingDestroy)
            return false;

        if (attackerModel.statusPlayer == StatusPlayer.ShootExam || attackerModel.statusPlayer == StatusPlayer.StartPoint)
            return false;

        if (attackerModel.score <= 0)
            return false;

        var targetModel = targetHandler.PlayerModel;
        if (targetModel.isDestroy || targetModel.statusPlayer == StatusPlayer.Destroy || targetModel.statusPlayer == StatusPlayer.WaitingDestroy)
            return false;

        float radius = radiusOverride ?? GetChamCatSkillRadius();
        float distance = Vector3.Distance(attackerBall.transform.position, targetBall.transform.position);
        return distance <= radius;
    }

    public bool TryGetChamCatTarget(int attackerId, out int targetId, float? radiusOverride = null)
    {
        targetId = -1;
        if (attackerId == 0)
            return false;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.IsYourTurn(attackerId))
            return false;

        var attackerGO = GetPlayerObject(attackerId);
        if (attackerGO == null)
            return false;

        var attackerHandler = attackerGO.GetComponent<PlayerNetworkHandler>();
        if (attackerHandler == null)
            return false;

        var attackerModel = attackerHandler.PlayerModel;
        if (attackerModel.isDestroy || attackerModel.statusPlayer == StatusPlayer.Destroy || attackerModel.statusPlayer == StatusPlayer.WaitingDestroy)
            return false;

        if (attackerModel.statusPlayer == StatusPlayer.ShootExam || attackerModel.statusPlayer == StatusPlayer.StartPoint)
            return false;

        if (attackerModel.score <= 0)
            return false;

        float radius = radiusOverride ?? GetChamCatSkillRadius();
        targetId = GetNearestEnemyPlayerId(attackerId, radius);
        return targetId != -1;
    }

    public bool TryResolveChamCatTarget(int attackerId, int requestedTargetId, out int targetId, bool allowNearestFallback = true, float? radiusOverride = null)
    {
        targetId = -1;

        if (requestedTargetId > 0 && CanUseChamCatOnTarget(attackerId, requestedTargetId, radiusOverride))
        {
            targetId = requestedTargetId;
            return true;
        }

        if (!allowNearestFallback)
            return false;

        return TryGetChamCatTarget(attackerId, out targetId, radiusOverride);
    }

    public bool HandleChamCatSkill(int attackerId, int requestedTargetId = 0, bool allowNearestFallback = true)
    {
        if (!TryResolveChamCatTarget(attackerId, requestedTargetId, out int targetId, allowNearestFallback))
            return false;

        var targetGO = GetPlayerObject(targetId);
        if (targetGO == null)
            return false;

        var targetHandler = targetGO.GetComponent<PlayerNetworkHandler>();
        if (targetHandler == null)
            return false;

        var targetModel = targetHandler.PlayerModel;
        int targetScore = targetModel.score;

        SetPlayerStatus(targetId, StatusPlayer.Destroy);

        if (targetScore > 0)
        {
            AddScorePlayer(attackerId, targetScore);
        }

        NetworkObjectManager.Instance.isContinueTurn = true;

        NetworkObjectManager.Instance?.RpcShowPlayerList_Online();
        return true;
    }

    public void HandleCatAnTienSkill(int playerId)
    {

        if (playerId == 0)
            return;

        var playerGO = GetPlayerObject(playerId);
        if (playerGO == null)
            return;

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
            return;

        var model = handler.PlayerModel;
        bool isCountered = RegisterCatAnTienSkillUse(playerId);
        model.isCatAnTienActive = isCountered ? 0 : 1;
        handler.PlayerModel = model;

        if (NetworkObjectManager.Instance != null && NetworkObjectManager.Instance.HasStateAuthority &&
            TryGetNetworkPlayerInfo(playerId, out var info, out var index))
        {
            info.isCatAnTienActive = model.isCatAnTienActive;
            NetworkObjectManager.Instance.players.Set(index, info);
        }

    }

    public void HandleBananaJumpSkill(int playerId)
    {
        if (playerId == 0)
        {
            Debug.LogWarning("[BananaJump][Server] HandleBananaJumpSkill called with invalid playerId (0).");
            return;
        }

        Debug.Log($"[BananaJump][Server] Processing Banana Jump skill for player {playerId}.");

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.IsYourTurn(playerId))
        {
            Debug.LogWarning($"[BananaJump][Server] Cannot process Banana Jump skill for player {playerId}. Manager missing or not player's turn.");
            return;
        }

        var playerGO = GetPlayerObject(playerId);
        if (playerGO == null)
        {
            Debug.LogWarning($"[BananaJump][Server] Cannot process Banana Jump skill for player {playerId} because player object is missing.");
            return;
        }

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
        {
            Debug.LogWarning($"[BananaJump][Server] Cannot process Banana Jump skill for player {playerId} because PlayerNetworkHandler is missing.");
            return;
        }

        bool activated = handler.TryActivateBananaJumpSkill();
        Debug.Log($"[BananaJump][Server] TryActivateBananaJumpSkill result for player {playerId}: {activated}.");
    }

    public void HandleWindBlowSkill(int playerId)
    {
        if (playerId == 0)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.IsYourTurn(playerId))
        {
            Debug.LogWarning($"[WindBlow][Server] Player {playerId} cannot use Wind Blow because it is not their turn.");
            return;
        }

        var playerGO = GetPlayerObject(playerId);
        if (playerGO == null)
        {
            Debug.LogWarning($"[WindBlow][Server] Player object not found for {playerId}.");
            return;
        }

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
        {
            Debug.LogWarning($"[WindBlow][Server] Player {playerId} missing PlayerNetworkHandler.");
            return;
        }

        var ballObj = GetActiveBallObject(playerId);
        if (ballObj == null)
        {
            Debug.LogWarning($"[WindBlow][Server] Player {playerId} has no active ball.");
            return;
        }

        var ballCtrl = ballObj.GetComponent<BallServerController>();
        if (ballCtrl == null)
        {
            Debug.LogWarning($"[WindBlow][Server] Ball controller missing for player {playerId}.");
            return;
        }

        if (!IsBallStillMoving(ballCtrl))
        {
            Debug.LogWarning($"[WindBlow][Server] Ball already stopped for player {playerId}.");
            return;
        }

        StopWindBlowRoutine(playerId);
        var routine = StartCoroutine(HandleWindBlowRoutine(playerId, handler, ballCtrl));
        activeWindBlowSkills[playerId] = routine;
    }

    public void HandleHuSkill(int attackerId, int skillLevel)
    {
        if (attackerId == 0)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        var ordered = manager.GetOrderedPlayerInfos();
        if (ordered == null || ordered.Count == 0)
            return;

        int currentOrder = manager.currentPlayerIndex;
        var data = ordered.FirstOrDefault(t => t.turnOrder == currentOrder);
        int targetId = data.playerId;
        if (targetId == 0 || targetId == attackerId)
            return;

        var attackerObj = manager.GetPlayerObject(attackerId);
        var targetObj = manager.GetPlayerObject(targetId);
        if (attackerObj == null || targetObj == null)
            return;

        var attackerHandler = attackerObj.GetComponent<PlayerNetworkHandler>();
        var targetHandler = targetObj.GetComponent<PlayerNetworkHandler>();
        if (attackerHandler == null || targetHandler == null)
            return;

        var targetModel = targetHandler.PlayerModel;
        if (targetModel.isDestroy || targetModel.statusPlayer == StatusPlayer.Destroy || targetModel.statusPlayer == StatusPlayer.WaitingDestroy)
            return;

        Vector3 behindDirection = -targetObj.transform.forward;
        if (behindDirection.sqrMagnitude <= Mathf.Epsilon)
            behindDirection = -(targetObj.transform.rotation * Vector3.forward);

        float distance = 0.85f;
        Vector3 sideOffset = targetObj.transform.right * UnityEngine.Random.Range(-0.2f, 0.2f);
        Vector3 teleportPosition = targetObj.transform.position + behindDirection.normalized * distance + sideOffset;
        StartCoroutine(attackerHandler.TeleportToTarget(teleportPosition, targetObj.transform));

        attackerHandler.CurrentAnimState = CharacterAnimState.Hu;
        ApplyHuRotation(targetHandler, skillLevel);
        manager.RpcPlayHuFeedback(attackerId, targetId, skillLevel);
    }

    private void ApplyHuRotation(PlayerNetworkHandler targetHandler, int skillLevel)
    {
        if (targetHandler == null)
            return;

        if (targetHandler.Object == null || !targetHandler.Object.IsValid)
            return;

        if (targetHandler.Object.InputAuthority == PlayerRef.None)
        {
            Debug.LogWarning($"⚠️ [Hu][Server] Không thể xoay vì player {targetHandler.PlayerModel.playerId} chưa có InputAuthority.");
            return;
        }

        int level = Mathf.Clamp(skillLevel, 1, 3);
        float maxAngle = 2f + (level - 1) * 2f;
        float yawAngle = UnityEngine.Random.Range(maxAngle * 0.5f, maxAngle);
        float yawSign = UnityEngine.Random.value > 0.5f ? 1f : -1f;
        float pitchAngle = UnityEngine.Random.Range(maxAngle * 0.5f, maxAngle);

        Vector3 baseForward = targetHandler.transform.forward;
        if (baseForward.sqrMagnitude <= Mathf.Epsilon)
            baseForward = targetHandler.transform.rotation * Vector3.forward;

        Quaternion yawRotation = Quaternion.AngleAxis(yawAngle * yawSign, Vector3.up);
        Quaternion pitchRotation = Quaternion.AngleAxis(-pitchAngle, targetHandler.transform.right);
        Vector3 lookDirection = yawRotation * (pitchRotation * baseForward);
        Vector3 origin = targetHandler.transform.position + Vector3.up * 1.5f;
        Vector3 lookAtTarget = origin + lookDirection.normalized * 5f;
        targetHandler.RPC_RequestRotateSightingPoint(lookAtTarget);
    }

    private void StopWindBlowRoutine(int playerId)
    {
        if (activeWindBlowSkills.TryGetValue(playerId, out var running) && running != null)
        {
            StopCoroutine(running);
        }

        activeWindBlowSkills.Remove(playerId);
    }

    private bool IsBallStillMoving(BallServerController ballCtrl)
    {
        if (ballCtrl == null || ballCtrl.hasBeenShoot != 1)
            return false;

        var rb = ballCtrl.GetComponent<NetworkRigidbody3D>()?.Rigidbody;
        if (rb == null)
            return false;

        return rb.linearVelocity.magnitude > windBlowMinVelocity || rb.angularVelocity.magnitude > windBlowMinAngular;
    }

    private IEnumerator HandleWindBlowRoutine(int playerId, PlayerNetworkHandler handler, BallServerController ballCtrl)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            yield break;

        Vector3 targetPosition = ballCtrl.transform.position;

        bool ShouldAbort()
        {
            return !manager.IsYourTurn(playerId) || !IsBallStillMoving(ballCtrl);
        }

        yield return handler.ProgressMoveToTargetForSkill(targetPosition, ShouldAbort);

        if (ShouldAbort())
        {
            if (handler.HasStateAuthority)
                handler.CurrentAnimState = CharacterAnimState.Idle;
            StopWindBlowRoutine(playerId);
            yield break;
        }

        float distanceToBall = Vector3.Distance(handler.transform.position, ballCtrl.transform.position);
        if (distanceToBall > windBlowReachDistance)
        {
            if (handler.HasStateAuthority)
                handler.CurrentAnimState = CharacterAnimState.Idle;
            StopWindBlowRoutine(playerId);
            yield break;
        }

        if (handler.HasStateAuthority)
            handler.CurrentAnimState = CharacterAnimState.BlowWind;

        yield return new WaitForSeconds(windBlowAnimationDelay);

        if (ShouldAbort())
        {
            if (handler.HasStateAuthority)
                handler.CurrentAnimState = CharacterAnimState.Idle;
            StopWindBlowRoutine(playerId);
            yield break;
        }

        ApplyWindBlowForce(ballCtrl, handler);

        if (handler.HasStateAuthority)
            handler.CurrentAnimState = CharacterAnimState.Idle;

        StopWindBlowRoutine(playerId);
    }

    private void ApplyWindBlowForce(BallServerController ballCtrl, PlayerNetworkHandler handler)
    {
        var rb = ballCtrl.GetComponent<NetworkRigidbody3D>()?.Rigidbody;
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.maxAngularVelocity = 50f;

        Vector3 direction = rb.linearVelocity.sqrMagnitude > 0.0001f
            ? rb.linearVelocity.normalized
            : (ballCtrl.transform.position - handler.transform.position).normalized;

        rb.AddForce(direction * windBlowForce, ForceMode.Impulse);
    }

    public int GetNearestEnemyPlayerId(int currentId, float radius = 1.5f)
    {
        if (currentId == 0)
            return -1;

        var currentBall = GetActiveBallObject(currentId);
        if (currentBall == null)
            return -1;

        Vector3 myPos = currentBall.transform.position;
        float minDist = float.MaxValue;
        int targetId = -1;

        foreach (var entry in GetTurnOrderSnapshot())
        {
            if (entry.playerId == currentId)
                continue;

            var playerGO = GetPlayerObject(entry.playerId);
            var ball = GetActiveBallObject(entry.playerId);
            if (playerGO == null || ball == null)
                continue;

            var handler = playerGO.GetComponent<PlayerNetworkHandler>();
            var model = handler.PlayerModel;
            if (model.isDestroy || model.statusPlayer == StatusPlayer.Destroy || model.statusPlayer == StatusPlayer.WaitingDestroy)
                continue;

            float distance = Vector3.Distance(myPos, ball.transform.position);
            if (distance <= radius && distance < minDist)
            {
                minDist = distance;
                targetId = entry.playerId;
            }
        }

        return targetId;
    }
    private NetworkObject GetPlayerObject(int playerId)
    {
        return NetworkObjectManager.Instance?.GetPlayerObject(playerId);
    }

    private NetworkObject GetActiveBallObject(int playerId)
    {
        return NetworkObjectManager.Instance?.GetActiveBallObject(playerId);
    }

    public void AddTurnOrderEntry(int playerId, int turnOrder)
    {
        if (playerId == 0)
            return;

        ApplyTurnOrderToPlayer(playerId, turnOrder);
    }

    public void UpdateTurnOrderEntry(int playerId, int turnOrder)
    {
        if (playerId == 0)
            return;

        ApplyTurnOrderToPlayer(playerId, turnOrder);
    }

 

    //void Update()
    //{
        
    //    //if(NetworkObjectManager.Instance != null && NetworkObjectManager.Instance.HotsReady == 1)
    //    //  HandleMovement(); // Xử lý di chuyển
    //}
    #region [ =============================  SETTING GAME ===========================]
 
    //  public IEnumerator SettingMoveForExam(NetworkObjectManager serverRPC)
    //{
    //    yield return StartCoroutine(MoveForExam(serverRPC));
    //   // serverRPC.RpcHotsSetupComplete();
    //}
 

    private bool PlaceOnGround(NetworkObject networkObject, Terrain terrain, float offsetY = 0.1f)
    {
        if (!networkObject.HasStateAuthority)
        {
            Debug.LogWarning("Không có quyền chỉnh vị trí trên networkObject này.");
            return false;
        }

        // Kiểm tra nếu Terrain không hợp lệ
        if (terrain == null)
        {
            Debug.LogWarning("Không có Terrain hợp lệ.");
            return false;
        }

        Transform objTransform = networkObject.transform;
        Vector3 origin = objTransform.position;

        // Sử dụng Terrain.SampleHeight để lấy độ cao của Terrain tại vị trí X, Z của đối tượng
        float terrainHeight = terrain.SampleHeight(origin);

        // Kiểm tra nếu độ cao của terrain hợp lệ (lớn hơn 0)
        if (terrainHeight > 0)
        {
            // Đảm bảo rằng đối tượng không bị di chuyển quá cao hoặc quá thấp
            terrainHeight = Mathf.Max(terrainHeight, 0);  // Tránh giá trị âm hoặc không hợp lệ

            // Kiểm tra nếu đối tượng đã lơ lửng
            if (origin.y > terrainHeight)
            {
                // Di chuyển đối tượng xuống mặt đất, cộng thêm offsetY để tránh bị lún vào mặt đất
                Vector3 newPos = new Vector3(origin.x, terrainHeight + offsetY, origin.z);

                // Cập nhật vị trí của đối tượng mà không cần sử dụng Rigidbody
                objTransform.position = newPos;

                Debug.Log("Đã di chuyển đối tượng lên mặt đất");
                return true;
            }
            else
            {
                Debug.Log("Đối tượng đã nằm trên mặt đất.");
            }
        }
        else
        {
            Debug.LogWarning("Không tìm thấy mặt đất dưới đối tượng.");
        }

        return false;
    }






    #endregion

    #region [ ============================ EXAM STEP =======================]

    public void DetermineTurnOrder()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("⚠️ [HOST] Không tìm thấy NetworkObjectManager để xác định lượt thi.");
            return;
        }

        var finishedPlayers = manager.GetExamCompletedPlayers();
        if (finishedPlayers == null || finishedPlayers.Count == 0)
        {
            Debug.LogWarning("⚠️ [HOST] Không có người chơi nào hoàn thành bài thi để sắp xếp lượt.");
            return;
        }

        var sortedPlayers = finishedPlayers
            .OrderBy(p => p.distance < 0f)
            .ThenBy(p => Mathf.Abs(p.distance))
            .ToList();

        var timedOutSet = new HashSet<int>(ExamTimeoutPlayers);
        var timedOutPlayers = sortedPlayers
            .Where(p => timedOutSet.Contains(p.playerId))
            .ToList();
        var normalPlayers = sortedPlayers
            .Where(p => !timedOutSet.Contains(p.playerId))
            .ToList();

        if (timedOutPlayers.Count > 0)
        {
            var timeoutOrder = ExamTimeoutPlayers
                .Select((id, index) => new { id, index })
                .ToDictionary(x => x.id, x => x.index);

            timedOutPlayers = timedOutPlayers
                .OrderBy(p => timeoutOrder.TryGetValue(p.playerId, out var order) ? order : int.MaxValue)
                .ToList();
        }

        sortedPlayers = normalPlayers
            .Concat(timedOutPlayers)
            .ToList();

        var assignedPlayerIds = new HashSet<int>();
        for (int order = 0; order < sortedPlayers.Count; order++)
        {
            var playerInfo = sortedPlayers[order];
            assignedPlayerIds.Add(playerInfo.playerId);
            ApplyTurnOrderToPlayer(playerInfo.playerId, order);
        }

        var players = manager.players;
        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (info.playerId == 0 || assignedPlayerIds.Contains(info.playerId))
                continue;

            if (info.turnOrder != -1)
            {
                info.turnOrder = -1;
                players.Set(i, info);
            }
        }

        Debug.Log("👑 [HOST] Đã xác định thứ tự lượt chơi dựa trên danh sách đã hoàn thành.");
       // NetworkObjectManager.Instance.RpcFinishOrderPlayer();
    }
 
    public void StartExamOrderResolution()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (examOrderRoutine != null)
        {
            StopCoroutine(examOrderRoutine);
            examOrderRoutine = null;
        }

        examOrderRoutine = StartCoroutine(HandleExamOrderResolutionRoutine());
    }

    private void CancelExamResolutionRoutines(string reason)
    {
        StopExamShotTimeoutWatchdog(reason);

        if (examScoreRoutine != null)
        {
            StopCoroutine(examScoreRoutine);
            examScoreRoutine = null;
            Debug.Log($"[HOST][ExamScore] Dừng examScoreRoutine: {reason}");
        }

        if (examOrderRoutine != null)
        {
            StopCoroutine(examOrderRoutine);
            examOrderRoutine = null;
            Debug.Log($"[HOST][ExamScore] Dừng examOrderRoutine: {reason}");
        }
    }

    private IEnumerator HandleExamOrderResolutionRoutine()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
        {
            yield break;
        }

        manager.ClientsReady = 0;
        manager.RpcSetStartPointEffectActive(false);

        DetermineTurnOrder();

        const float preIndicatorDelay = 1f;
        const float indicatorSpeed = 1f;
        const float indicatorDelay = 1f;
        const float postIndicatorDelay = 2f;
        float indicatorDuration = indicatorSpeed * 2f + indicatorDelay;

        yield return new WaitForSeconds(preIndicatorDelay);

        var orderedPlayers = manager.GetOrderedPlayerInfos();
        foreach (var entry in orderedPlayers)
        {
            manager.RpcShowExamTurnOrder(entry.playerId, entry.turnOrder + 1);
        }

        yield return new WaitForSeconds(indicatorDuration + postIndicatorDelay);

        if (gameObject.activeInHierarchy)
        {
            yield return StartCoroutine(ArrangePlayersForGatherPoint());
        }

        SetCurrentTurnToFirstEligiblePlayer(manager);
        manager.RPC_FinishedOrderExam();
        manager.StatusLoading = StatusLoadingGame.StartTurn;
        StartCurrentTurnPlayerAtStartPointIfNeeded("exam_order_finished");

        examOrderRoutine = null;
    }
 
#endregion
    
    #region [========================== GAME PLAY STEP ==========================]
    
    void AddRingBalls(int amount)
    {
        if (amount <= 0) return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("⚠️ Không tìm thấy NetworkObjectManager để thêm bi.");
            return;
        }

        var runner = manager.Runner;
        if (runner == null && !manager.TryGetRoomRunner(out runner, logError: false))
        {
            runner = null;
        }

        if (runner == null || !runner.IsRunning || runner.IsShutdown)
        {
            Debug.LogWarning("⚠️ Không tìm thấy NetworkRunner để spawn thêm bi.");
            return;
        }

        if (playArea == null)
        {
            Debug.LogWarning("⚠️ PlayArea chưa được cấu hình để spawn thêm bi.");
            return;
        }

        var initializer = GameServerInitializer.Instance;
        if (initializer == null)
        {
            Debug.LogWarning("⚠️ Không tìm thấy GameServerInitializer để spawn thêm bi.");
            return;
        }

        var ringPrefab = initializer.RingBallPrefab;
        if (ringPrefab == null)
        {
            Debug.LogWarning("⚠️ RingBallPrefab chưa được gán trên GameServerInitializer.");
            return;
        }

        MarbleSpawnData[] dataList = initializer.GenerateMarbleSpawnData(playArea, amount);
        NormalizeReturnedRingBallSpawnData(dataList);
        bool addedAny = false;

        for (int idx = 0; idx < dataList.Length; idx++)
        {
            var data = dataList[idx];
            var marble = runner.Spawn(ringPrefab, data.Position, data.Rotation, null);
            if (marble != null)
            {
                int materialIndex = -1;
                //if (materialCateyes != null && materialCateyes.Count > 0)
                  //  materialIndex = UnityEngine.Random.Range(0, materialCateyes.Count);

                var handler = marble.GetComponent<RingBallNetworkHandler>();
                //if (handler != null)
                //    handler.MaterialIndex = materialIndex;

                StabilizeReturnedRingBall(marble);
                manager.ringBalls.Add(marble, playArea);
                addedAny = true;
            }
        }

        if (addedAny)
            manager.RpcNotifyRingBallCollectionChanged();

    }

    private void NormalizeReturnedRingBallSpawnData(MarbleSpawnData[] dataList)
    {
        if (dataList == null || dataList.Length == 0 || playArea == null)
            return;

        for (int i = 0; i < dataList.Length; i++)
        {
            var data = dataList[i];
            data.Position = ResolveStableRingBallPosition(data.Position, null);
            dataList[i] = data;
        }
    }

    private void StabilizeReturnedRingBall(NetworkObject marble)
    {
        if (marble == null || playArea == null)
            return;

        Vector3 stablePosition = ResolveStableRingBallPosition(marble.transform.position, marble);
        marble.transform.position = stablePosition;

        if (TryGetRingBallRigidbody(marble, out var rb))
        {
            rb.position = stablePosition;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.Sleep();
        }
    }

    private Vector3 ResolveStableRingBallPosition(Vector3 position, NetworkObject marble)
    {
        position = ClampPointInsidePlayArea(position, 0.03f);

        float radius = ResolveRingBallWorldRadius(marble);
        float targetY = position.y;

        if (TerrainGround != null)
        {
            targetY = TerrainGround.SampleHeight(position) + TerrainGround.transform.position.y + radius + 0.002f;
        }
        else
        {
            targetY = Mathf.Max(targetY, playArea.bounds.min.y + radius + 0.002f);
        }

        position.y = targetY;
        return ClampPointInsidePlayArea(position, 0.03f);
    }

    private Vector3 ClampPointInsidePlayArea(Vector3 position, float margin)
    {
        if (playArea == null)
            return position;

        Vector3 local = playArea.transform.InverseTransformPoint(position) - playArea.center;
        Vector3 halfSize = playArea.size * 0.5f;
        float safeMarginX = Mathf.Min(Mathf.Max(0f, margin), Mathf.Max(0f, halfSize.x - 0.001f));
        float safeMarginY = Mathf.Min(Mathf.Max(0f, margin), Mathf.Max(0f, halfSize.y - 0.001f));
        float safeMarginZ = Mathf.Min(Mathf.Max(0f, margin), Mathf.Max(0f, halfSize.z - 0.001f));

        local.x = Mathf.Clamp(local.x, -halfSize.x + safeMarginX, halfSize.x - safeMarginX);
        local.y = Mathf.Clamp(local.y, -halfSize.y + safeMarginY, halfSize.y - safeMarginY);
        local.z = Mathf.Clamp(local.z, -halfSize.z + safeMarginZ, halfSize.z - safeMarginZ);

        return playArea.transform.TransformPoint(local + playArea.center);
    }

    private float ResolveRingBallWorldRadius(NetworkObject marble)
    {
        SphereCollider sphere = marble != null ? marble.GetComponent<SphereCollider>() : null;
        if (sphere == null)
            return 0.0125f;

        Vector3 scale = marble.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return Mathf.Max(0.005f, sphere.radius * maxScale);
    }

    private bool TryGetRingBallRigidbody(NetworkObject marble, out Rigidbody rb)
    {
        rb = null;
        if (marble == null)
            return false;

        if (marble.TryGetComponent<NetworkRigidbody3D>(out var networkRigidbody) && networkRigidbody.Rigidbody != null)
        {
            rb = networkRigidbody.Rigidbody;
            return true;
        }

        return marble.TryGetComponent(out rb) && rb != null;
    }

    void SpawnMarbles(int totalAmount)
    {
        var ringPrefab = RingBallPrefab;
        if (ringPrefab == null)
        {
            Debug.LogWarning("⚠️ RingBallPrefab chưa được cấu hình cho SpawnMarbles.");
            return;
        }

        Collider areaCollider = playArea.GetComponent<Collider>();
        if (areaCollider == null)
        {
            Debug.LogError("⚠️ PlayArea cần có Collider để tính giới hạn spawn!");
            return;
        }

        Vector3 areaMin = areaCollider.bounds.min;
        Vector3 areaMax = areaCollider.bounds.max;
        float spawnHeight = areaMax.y + 0.2f;

        // Xác định kích thước vùng spawn
        float width = areaMax.x - areaMin.x;
        float depth = areaMax.z - areaMin.z;

        // Tính toán số hàng và cột tối ưu
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(totalAmount));
        float cellSizeX = width / gridSize;
        float cellSizeZ = depth / gridSize;

        List<Vector3> spawnPositions = new List<Vector3>();

        // Tạo lưới vị trí spawn
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                if (spawnPositions.Count >= totalAmount) break;

                // Tính toán vị trí trung tâm của ô lưới
                float x = areaMin.x + (i + 0.5f) * cellSizeX;
                float z = areaMin.z + (j + 0.5f) * cellSizeZ;

                // Đảm bảo không vượt quá ranh giới vùng
                x = Mathf.Clamp(x, areaMin.x, areaMax.x);
                z = Mathf.Clamp(z, areaMin.z, areaMax.z);

                // Thêm một độ lệch ngẫu nhiên nhỏ để trông tự nhiên hơn
                float randomOffsetX = UnityEngine.Random.Range(-cellSizeX * 0.3f, cellSizeX * 0.3f);
                float randomOffsetZ = UnityEngine.Random.Range(-cellSizeZ * 0.3f, cellSizeZ * 0.3f);

                spawnPositions.Add(new Vector3(x + randomOffsetX, spawnHeight, z + randomOffsetZ));
            }
        }

        // Xáo trộn danh sách vị trí để tạo sự ngẫu nhiên
        spawnPositions = spawnPositions.OrderBy(p => UnityEngine.Random.value).ToList();

        // Spawn bi theo vị trí hợp lệ
        for (int i = 0; i < totalAmount; i++)
        {
            // Lấy một material ngẫu nhiên từ danh sách materialCateye
           // Material materialRandom = materialCateyes[UnityEngine.Random.Range(0, materialCateyes.Count)];

            // Tìm đối tượng con "Cateye" và gán material ngẫu nhiên vào
            //var cateyeRenderer = ringPrefab.transform.Find("Cateye")?.GetComponent<Renderer>();
            //if (cateyeRenderer != null)
               // cateyeRenderer.material = materialRandom;

            // Tạo góc quay ngẫu nhiên cho viên bi (quay trên mọi trục)
            Quaternion randomRotation = Quaternion.Euler(UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f));

            // Instantiate đối tượng RingBallPrefab tại vị trí spawnPositions[i] với góc quay ngẫu nhiên
            Instantiate(ringPrefab, spawnPositions[i], randomRotation);
        }
    }


    //IEnumerator MoveForExam(NetworkObjectManager serverRPC)
    //{
    //    // Hiển thị thông báo

//    Vector3 examPosition = ExamMain.position; // Vị trí thi đấu
//    Vector3 startPosition = StartPointMain.transform.position; // Vị trí xuất phát


//    float spacing = 0.8f; // Khoảng cách tối thiểu giữa các người chơi
//    List<float> usedXPositions = new List<float>(); // Lưu các vị trí X đã dùng,  
//    var players =  serverRPC.players;
//    for (int i = 0; i < players.Length; i++)
//    {
//        var player = players.Get(i);
//        Vector3 finalPosition;
//        float randomX;
//        do
//        {
//            randomX = examPosition.x + Random.Range(-spacing * players.Length, spacing * players.Length);
//        }
//        while (usedXPositions.Exists(x => Mathf.Abs(x - randomX) < spacing));

//        usedXPositions.Add(randomX);

//        // Xác định vị trí mới nhưng chỉ thay đổi X, giữ nguyên Y và Z
//        var playerbody = GetPlayerObject(player.playerId);
//        var scriptHandle = playerbody.GetComponent<PlayerNetworkHandler>();
//        finalPosition = new Vector3(randomX, playerbody.transform.position.y, examPosition.z);

//        // step di chuyển đến vị trí thi và nhìn vào mức
//        yield return StartCoroutine(MovePlayerToPosition(playerbody, finalPosition, startPosition));
//        // step ngồi xuông
//        scriptHandle.CurrentAnimState = CharacterAnimState.SitToShoot;
//        // Cập nhật trạng thái
//        player.statusPlayer = StatusPlayer.ShootExam;
//        player.isHolding = true;
//       // var FingerPosition = playerbody.transform.Find("FingerPosition").gameObject.transform.position;
//        //player.FingerPosition = FingerPosition;
//        players.Set(i, player);
//    }
//    yield return null;
//}
 
    //private IEnumerator MovePlayerToPosition(NetworkObject playerbody, Vector3 targetPosition, Vector3 lookAtPosition)
    //{
    //    float moveDuration = 1.5f;
    //    float elapsedTime = 0f;
    //    Vector3 startPosition = playerbody.transform.position;
    //    Vector3 direction = (targetPosition - startPosition).normalized;
    //    float distance = Vector3.Distance(startPosition, targetPosition);

    //    var controller = playerbody.GetComponent<NetworkCharacterController>();
    //    var scriptHandle = playerbody.GetComponent<PlayerNetworkHandler>();

    //    while (elapsedTime < moveDuration)
    //    {
    //        controller.Move(direction * (distance / moveDuration) * Time.deltaTime);
    //        elapsedTime += Time.deltaTime;
    //        yield return null;
    //    }

    //    // Đảm bảo nhân vật đến đúng vị trí cuối cùng
    //    controller.Teleport(targetPosition, playerbody.transform.rotation);

    //    // Xoay mặt về hướng startPosition (chỉ quay theo trục Y)
    //    //Vector3 directionToStart = (lookAtPosition - playerbody.transform.position).normalized;
    //    //directionToStart.y = 0;
    //    if (scriptHandle != null)
    //    {
    //        scriptHandle.RPC_RequestRotateSightingPoint(lookAtPosition);
    //    }
    //    //playerbody.transform.rotation = Quaternion.LookRotation(directionToStart);
    //}

    private void StopExistingServerMovement(int playerId)
    {
        if (activeServerMovements.TryGetValue(playerId, out var running) && running != null)
        {
            Debug.Log($"⏹️ [SERVER] Dừng coroutine di chuyển cũ của player {playerId}");
            StopCoroutine(running);
        }

        activeServerMovements.Remove(playerId);
    }

    private Transform ResolveLookTarget(PlayerMovementRequestType requestType)
    {
        switch (requestType)
        {
            case PlayerMovementRequestType.TeleportExam:
                return StartPointMain;
            case PlayerMovementRequestType.TeleportStartPoint:
            case PlayerMovementRequestType.TeleportGatherPoint:
            case PlayerMovementRequestType.MoveToPlayArea:
                return playArea != null ? playArea.transform : null;
            default:
                return null;
        }
    }

    private bool ShouldFacePlayAreaAfterMovement(PlayerMovementRequestType requestType)
    {
        return requestType == PlayerMovementRequestType.TeleportStartPoint
               || requestType == PlayerMovementRequestType.TeleportGatherPoint
               || requestType == PlayerMovementRequestType.MoveToPlayArea;
    }

    private bool TryBuildLookAtRotation(Vector3 originPosition, Transform lookTarget, out Quaternion rotation)
    {
        rotation = Quaternion.identity;

        if (lookTarget == null)
            return false;

        Vector3 direction = lookTarget.position - originPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        return true;
    }

    private Quaternion? ResolveFinalMovementRotation(
        PlayerNetworkHandler handler,
        Vector3 fallbackPosition,
        PlayerMovementRequestType requestType,
        Transform lookTarget,
        Quaternion? configuredRotation)
    {
        if (ShouldFacePlayAreaAfterMovement(requestType))
        {
            Vector3 originPosition = handler != null ? handler.transform.position : fallbackPosition;
            if (TryBuildLookAtRotation(originPosition, lookTarget, out Quaternion lookRotation))
                return lookRotation;

            if (TryBuildLookAtRotation(fallbackPosition, lookTarget, out lookRotation))
                return lookRotation;
        }

        return configuredRotation;
    }

    private IEnumerator ExecuteServerMovementRoutine(int playerId, Vector3 targetPosition, PlayerMovementRequestType requestType, Quaternion? targetRotation = null)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
        {
            Debug.LogError($"❌ [SERVER] Không tìm thấy NetworkObjectManager khi xử lý di chuyển cho player {playerId}");
            yield break;
        }

        var playerGO = manager.GetPlayerObject(playerId);
        if (playerGO == null)
        {
            Debug.LogError($"❌ [SERVER] Không tìm thấy đối tượng player {playerId} trong NetworkObjectManager");
            yield break;
        }

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
        {
            Debug.LogError($"❌ [SERVER] Player {playerId} thiếu PlayerNetworkHandler, không thể di chuyển");
            yield break;
        }

        Debug.Log($"🚀 [SERVER] Bắt đầu coroutine di chuyển player {playerId} đến {targetPosition} (type={requestType})");

        if (requestType == PlayerMovementRequestType.TeleportStartPoint)
            playersMovedToStartPointForTurn.Add(playerId);
        else if (requestType == PlayerMovementRequestType.TeleportGatherPoint)
            playersMovedToStartPointForTurn.Remove(playerId);

        var lookTarget = ResolveLookTarget(requestType);
        if (requestType == PlayerMovementRequestType.MoveToPlayArea)
        {
            yield return handler.ProgressMoveToTarget(targetPosition, lookTarget);
        }
        else if (requestType == PlayerMovementRequestType.TeleportGatherPoint)
        {
            yield return handler.TeleportToTarget(targetPosition, lookTarget, CharacterAnimState.Idle, false, 0.15f);
        }
        else
        {
            yield return handler.TeleportToTarget(targetPosition, lookTarget);
        }

        Quaternion? finalRotation = ResolveFinalMovementRotation(handler, targetPosition, requestType, lookTarget, targetRotation);
        if (finalRotation.HasValue)
        {
            ApplyServerRotation(handler, finalRotation.Value);
        }
        yield break;
    }

    private void ApplyServerRotation(PlayerNetworkHandler handler, Quaternion rotation)
    {
        if (handler == null || !handler.HasStateAuthority)
            return;

        handler.TargetRotation = rotation;
        handler.transform.rotation = rotation;
    }

    private IEnumerator TrackMovementCoroutine(int playerId, Vector3 targetPosition, PlayerMovementRequestType requestType, Quaternion? targetRotation = null)
    {
        yield return ExecuteServerMovementRoutine(playerId, targetPosition, requestType, targetRotation);
        activeServerMovements.Remove(playerId);
        Debug.Log($"✅ [SERVER] Hoàn thành coroutine di chuyển player {playerId}");
    }

    public void StartServerControlledMovement(int playerId, Vector3 targetPosition, PlayerMovementRequestType requestType, Quaternion? targetRotation = null)
    {
        StopExistingServerMovement(playerId);
        var routine = StartCoroutine(TrackMovementCoroutine(playerId, targetPosition, requestType, targetRotation));
        activeServerMovements[playerId] = routine;
        Debug.Log($"▶️ [SERVER] Khởi động coroutine di chuyển mới cho player {playerId}");
    }

    private Transform GetLocationByOrder(List<Transform> locations, int order)
    {
        if (locations == null || locations.Count == 0)
            return null;

        int index = Mathf.Clamp(order, 0, locations.Count - 1);
        return locations[index];
    }

    public IEnumerator ArrangePlayersForExamStart()
    {
        if (LstLocationExam == null || LstLocationExam.Count == 0)
            yield break;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            yield break;

        var playersBuffer = manager.players;
        if (playersBuffer.Length == 0)
            yield break;

        List<PlayerInfoStruct> activePlayers = new List<PlayerInfoStruct>();
        for (int i = 0; i < playersBuffer.Length; i++)
        {
            var info = playersBuffer.Get(i);
            if (info.playerId != 0)
            {
                activePlayers.Add(info);
            }
        }

        if (activePlayers.Count == 0)
            yield break;

        // Trộn ngẫu nhiên danh sách người chơi để đảm bảo phân bổ vị trí ngẫu nhiên.
        for (int i = activePlayers.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (activePlayers[i], activePlayers[swapIndex]) = (activePlayers[swapIndex], activePlayers[i]);
        }

        int maxAssignments = Mathf.Min(activePlayers.Count, LstLocationExam.Count);
        for (int i = 0; i < maxAssignments; i++)
        {
            var target = LstLocationExam[i];
            if (target == null)
                continue;

            yield return ExecuteServerMovementRoutine(activePlayers[i].playerId, target.position, PlayerMovementRequestType.TeleportExam);
        }
    }

    private List<TurnOrderEntry> GetAliveTurnEntriesByOrder()
    {
        var result = new List<TurnOrderEntry>();
        var ordered = TurnOrderList.ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            if (!TryGetNetworkPlayerInfo(entry.playerId, out var info, out _))
                continue;

            if (!IsEligibleTurnPlayer(info))
                continue;

            result.Add(new TurnOrderEntry(info.playerId, info.turnOrder));
        }

        result.Sort((a, b) => a.turnOrder.CompareTo(b.turnOrder));
        return result;
    }

    private bool TryResolveLocationForTurnOrder(
        List<Transform> locations,
        TurnOrderEntry entry,
        HashSet<int> usedOrders,
        ref int fallbackOrder,
        out Transform target,
        out int mappedOrder,
        out bool duplicate,
        out bool invalid)
    {
        target = null;
        mappedOrder = entry != null ? entry.turnOrder : -1;
        duplicate = false;
        invalid = locations == null || locations.Count == 0 || mappedOrder < 0 || mappedOrder >= locations.Count;

        if (locations == null || locations.Count == 0 || entry == null)
            return false;

        if (!invalid)
            duplicate = !usedOrders.Add(mappedOrder);

        if (invalid || duplicate)
        {
            while (fallbackOrder < locations.Count && usedOrders.Contains(fallbackOrder))
            {
                fallbackOrder++;
            }

            mappedOrder = Mathf.Clamp(fallbackOrder, 0, locations.Count - 1);
            duplicate = usedOrders.Contains(mappedOrder);
            usedOrders.Add(mappedOrder);
        }

        target = GetLocationByOrder(locations, mappedOrder);
        return target != null;
    }

    public IEnumerator ArrangePlayersForGatherPoint()
    {
        List<Transform> gatherLocations = LstLocationGatherPoint != null && LstLocationGatherPoint.Count > 0
            ? LstLocationGatherPoint
            : LstLocationStartPoint;

        if (gatherLocations == null || gatherLocations.Count == 0)
        {
            Debug.LogWarning("[HOST][GatherPoint] Chưa cấu hình LstLocationGatherPoint hoặc LstLocationStartPoint, bỏ qua bước tập kết sau thi.");
            yield break;
        }

        if (gatherLocations == LstLocationStartPoint)
            Debug.LogWarning("[HOST][GatherPoint] Chưa cấu hình LstLocationGatherPoint, tạm fallback sang LstLocationStartPoint.");

        var ordered = GetAliveTurnEntriesByOrder();
        if (ordered.Count == 0)
            yield break;

        var usedOrders = new HashSet<int>();
        int fallbackOrder = 0;

        foreach (var entry in ordered)
        {
            if (!TryResolveLocationForTurnOrder(gatherLocations, entry, usedOrders, ref fallbackOrder, out var target, out var mappedOrder, out var duplicate, out var invalid))
                continue;

            Debug.Log($"[HOST][GatherPoint] pid={entry.playerId} turnOrder={entry.turnOrder} mappedIndex={mappedOrder}/{gatherLocations.Count} duplicate={duplicate} invalid={invalid}");
            yield return ExecuteServerMovementRoutine(entry.playerId, target.position, PlayerMovementRequestType.TeleportGatherPoint, target.rotation);
        }
    }

    public IEnumerator ArrangePlayersForStartPoint()
    {
        if (LstLocationStartPoint == null || LstLocationStartPoint.Count == 0)
            yield break;

        var ordered = GetAliveTurnEntriesByOrder();
        if (ordered.Count == 0)
            yield break;

        var usedOrders = new HashSet<int>();
        int fallbackOrder = 0;

        foreach (var entry in ordered)
        {
            if (!TryResolveLocationForTurnOrder(LstLocationStartPoint, entry, usedOrders, ref fallbackOrder, out var target, out var mappedOrder, out var duplicate, out var invalid))
                continue;

            Debug.Log($"[HOST][StartPoint] pid={entry.playerId} turnOrder={entry.turnOrder} mappedIndex={mappedOrder}/{LstLocationStartPoint.Count} duplicate={duplicate} invalid={invalid}");
            yield return ExecuteServerMovementRoutine(entry.playerId, target.position, PlayerMovementRequestType.TeleportStartPoint, target.rotation);
        }
    }

    private void SetCurrentTurnToFirstEligiblePlayer(NetworkObjectManager manager)
    {
        if (manager == null)
            return;

        var firstPlayer = manager.GetOrderedPlayerInfos()
            .Where(IsEligibleTurnPlayer)
            .OrderBy(info => info.turnOrder)
            .FirstOrDefault();

        if (firstPlayer.playerId <= 0)
            return;

        manager.currentPlayerIndex = firstPlayer.turnOrder;
        Debug.Log($"[HOST][TurnStart] Đặt lượt đầu sau thi: pid={firstPlayer.playerId}, order={firstPlayer.turnOrder}");
    }

    private Transform ResolveStartPointForTurnOrder(int turnOrder)
    {
        return StartPointMain;
    }

    private void StartCurrentTurnPlayerAtStartPointIfNeeded(string reason)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
            return;

        var currentInfo = manager.GetOrderedPlayerInfos()
            .FirstOrDefault(info => info.turnOrder == manager.currentPlayerIndex);

        if (!IsEligibleTurnPlayer(currentInfo) || currentInfo.statusPlayer != StatusPlayer.StartPoint)
            return;

        var botController = BotPlayerController.Instance;
        if (botController != null && botController.IsBotPlayer(currentInfo.playerId))
            return;

        Transform target = ResolveStartPointForTurnOrder(currentInfo.turnOrder);
        if (target == null)
        {
            Debug.LogWarning($"[HOST][StartPoint] Không có điểm bắn cho player {currentInfo.playerId}, reason={reason}");
            return;
        }

        Debug.Log($"[HOST][StartPoint] Đưa player tới lượt vào mức bắn: pid={currentInfo.playerId}, order={currentInfo.turnOrder}, reason={reason}");
        StartServerControlledMovement(currentInfo.playerId, target.position, PlayerMovementRequestType.TeleportStartPoint, target.rotation);
    }

    private void StartPlayerBackToGatherPointIfNeeded(int playerId, string reason)
    {
        if (playerId <= 0 || LstLocationGatherPoint == null || LstLocationGatherPoint.Count == 0)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.HasStateAuthority)
            return;

        if (!TryGetNetworkPlayerInfo(playerId, out var info, out _) || !IsEligibleTurnPlayer(info))
            return;

        Transform target = GetLocationByOrder(LstLocationGatherPoint, info.turnOrder);
        if (target == null)
            return;

        Debug.Log($"[HOST][GatherPoint] Đưa player vừa bắn về tập kết: pid={playerId}, order={info.turnOrder}, reason={reason}");
        StartServerControlledMovement(playerId, target.position, PlayerMovementRequestType.TeleportGatherPoint, target.rotation);
    }



    /// <summary>
    /// hàm xử lý khi đè vào button bắn và buông ra
    /// </summary>
    /// 

    public IEnumerator EndTurn()
    {
        ClientGameplayBridge.Camera.StopSlowMotion();
        var players = NetworkObjectManager.Instance.players;
        if (IsGameEnded) yield break;
        NetworkObjectManager.Instance.currentPlayerIndex = (NetworkObjectManager.Instance.currentPlayerIndex + 1) % players.Length; // Chuyển sang người tiếp theo
    }
    public int CheckOutBall()
    {
        int ScoreTotal = 0;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || playArea == null)
            return ScoreTotal;

        // 1) Xóa các bi đã rời khỏi vùng chơi khỏi bộ sưu tập hiện tại
        var removedBalls = manager.ringBalls.RemoveOutside(playArea);
        ScoreTotal = removedBalls.Count;

        if (ScoreTotal > 0)
        {
            Debug.Log($"Đã ra đạn: {ScoreTotal}");
            // 2) Thông báo cho client và despawn các bi nằm ngoài vòng
            HandleRemovedRingBalls(removedBalls);
            manager.isContinueTurn = true;
        }

        return ScoreTotal;
    }

    private int ReturnOutsideRingBallsToRingForQuay()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null || playArea == null)
            return 0;

        var outsideBalls = manager.ringBalls.RemoveOutside(playArea);
        int returnedCount = outsideBalls.Count;
        if (returnedCount <= 0)
            return 0;

        Debug.Log($"[HOST][Quay] Người chơi quậy, thu hồi {returnedCount} bi vừa ra vòng và đặt lại vào vòng.");
        HandleRemovedRingBalls(outsideBalls);
        AddRingBalls(returnedCount);
        return returnedCount;
    }

    private void HandleRemovedRingBalls(List<NetworkObject> removedBalls)
    {
        //  Debug.Log("👑[HOST] đã ra đạn");
        if (removedBalls == null || removedBalls.Count == 0)
            return;

        if (!NetworkObjectManager.Instance.TryGetRoomRunner(out var runner))
            return;
        bool changed = removedBalls.Any(ball => ball != null);

        foreach (var ball in removedBalls)
        {
            if (ball == null)
                continue;
                runner.Despawn(ball);
        }

        if (changed)
            NetworkObjectManager.Instance.RpcNotifyRingBallCollectionChanged();
    }
    private int GetPlayerIndexById(int playerId)
    {
        for (int i = 0; i < NetworkObjectManager.Instance.players.Length; i++)
        {
            if (NetworkObjectManager.Instance.players.Get(i).playerId == playerId)
                return i;
        }
        return -1;
    }
    bool IsInsideCube(Vector3 position)
    {
        // Cần đảm bảo playArea có BoxCollider, KHÔNG PHẢI Renderer
        BoxCollider cubeCollider = playArea.GetComponent<BoxCollider>();

        if (cubeCollider == null)
        {
            // Xử lý lỗi: không tìm thấy collider hoặc kích thước không xác định
            Debug.LogError("playArea thiếu BoxCollider!");
            return false;
        }

        // Lấy kích thước và vị trí của collider
        Vector3 cubeCenter = cubeCollider.bounds.center; // Tốt nhất nên dùng bounds.center của Collider
        Vector3 cubeSize = cubeCollider.bounds.size;

        // Kiểm tra xem vị trí có nằm trong giới hạn của cube không
        return (position.x >= cubeCenter.x - cubeSize.x / 2 && position.x <= cubeCenter.x + cubeSize.x / 2) &&
               (position.y >= cubeCenter.y - cubeSize.y / 2 && position.y <= cubeCenter.y + cubeSize.y / 2) &&
               (position.z >= cubeCenter.z - cubeSize.z / 2 && position.z <= cubeCenter.z + cubeSize.z / 2);
    }
    public IEnumerator ShootBall(int playerId, Rigidbody rb, Vector3 direction, float force, Vector3 spin, float shootAngle)
    {
        if (rb == null)
        {
            Debug.LogError("[HOST] ShootBall called with null Rigidbody");
            yield break;
        }

        if (!NetworkObjectManager.Instance.TryGetRoomRunner(out var runner) || runner == null)
        {
            Debug.LogError("[HOST] ShootBall called when runner is null");
            yield break;
        }

        // ⚙️ Cấu hình Rigidbody để đảm bảo xoáy hoạt động
        rb.isKinematic = false;
        rb.collisionDetectionMode = force >= 3f
            ? CollisionDetectionMode.ContinuousDynamic
            : CollisionDetectionMode.Continuous;
        rb.maxAngularVelocity = 50f;
        Debug.Log($"[HOST][ShootBall] rb state before force kin={rb.isKinematic} vel={rb.linearVelocity} pos={rb.position}");

        // 🎯 Lực bắn chính
        Vector3 shotDirection = GetShotDirection(direction, shootAngle);
        Vector3 right = Vector3.Cross(Vector3.up, shotDirection).normalized;

        float backSpin = Vector3.Dot(spin, shotDirection);
        float sideSpin = Vector3.Dot(spin, right);
        float contactOffset = 0.08f;
        Vector3 contactOffsetWorld = (-Vector3.up * backSpin + right * sideSpin) * contactOffset;

        rb.AddForceAtPosition(shotDirection * force, rb.worldCenterOfMass + contactOffsetWorld, ForceMode.Impulse);

        // 🌀 Xử lý Spin (Xì-đê)
        Vector3 torque = Vector3.zero;
        float torqueScale = force * 0.05f;

        if (Mathf.Abs(backSpin) > 0.01f)
            torque += right * (-backSpin) * torqueScale;

        if (Mathf.Abs(sideSpin) > 0.01f)
            torque += Vector3.up * sideSpin * torqueScale;

        if (torque.sqrMagnitude > 0.0001f)
            rb.AddTorque(torque, ForceMode.Impulse);

        // ⏪ XỬ LÝ XÌ-ĐÊ (ĐÃ SỬA)
        if (backSpin < -0.01f)
        {
            float minPullDelay = 0.08f;
            float maxPullDelay = 0.2f;
            float normalizedForce = Mathf.Clamp01(force / 10f);
            float pullDelay = Mathf.Lerp(minPullDelay, maxPullDelay, normalizedForce); // Force càng mạnh thì thời gian càng lâu
            float backSpinMagnitude = Mathf.Clamp01(Mathf.Abs(backSpin));
            float pullbackForce = force * Mathf.Lerp(0.15f, 0.45f, backSpinMagnitude);

            // ✅ GHI NHỚ velocity lúc bắt đầu
            Vector3 initialVelocity = rb.linearVelocity;
            float initialSpeed = initialVelocity.magnitude;

            Debug.Log($"[HOST] Chuẩn bị xì-đê | InitialSpeed: {initialSpeed} | PullbackForce: {pullbackForce} | Delay: {pullDelay}");

            // ✅ CHỜ ĐỦNG THỜI GIAN (trong FixedUpdate context của Fusion)
            float timeElapsed = 0f;
            while (timeElapsed < pullDelay)
            {
                if (rb == null)
                    yield break;

                timeElapsed += runner.DeltaTime;  // ← Dùng Server Time, KHÔNG phải Time.deltaTime!
                yield return null;
            }

            // ✅ NGƯNG BI TRƯỚC KHI KÉO LÙI ĐỂ TẠO HIỆU ỨNG XÌ-ĐÊ RÕ HƠN
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();

            // ✅ Áp dụng lực kéo lùi theo hướng bắn ban đầu
            Vector3 pullDirection = shotDirection;
            rb.AddForce(-pullDirection * pullbackForce, ForceMode.Impulse);

            Debug.Log($"[HOST] ✅ Áp dụng xì-đê thành công!");
            Debug.Log($"  - Initial Speed: {initialSpeed}");
            Debug.Log($"  - Pull Direction: {-pullDirection}");
            Debug.Log($"  - Pull Force:  {pullbackForce}");
            //thông báo sự kiện server vẫn còn người chơi để tránh auto shutdown server khi không có ai chơi
            MarkMatchProgress($"Shot pid={playerId}");
            yield break;
        }

        Debug.Log($"[HOST] shot ball | Force: {force} | Spin:  {spin} | AngularVel: {rb.angularVelocity}");

        yield break;
    }

    private Vector3 GetShotDirection(Vector3 baseDirection, float shootAngle)
    {
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(baseDirection, Vector3.up);
        if (horizontalDirection.sqrMagnitude < 0.0001f)
        {
            horizontalDirection = new Vector3(baseDirection.x, 0f, baseDirection.z);
            if (horizontalDirection.sqrMagnitude < 0.0001f)
                horizontalDirection = Vector3.forward;
        }

        horizontalDirection.Normalize();
        if (Mathf.Abs(shootAngle) < 0.01f)
            return horizontalDirection;

        Vector3 right = Vector3.Cross(Vector3.up, horizontalDirection).normalized;
        return Quaternion.AngleAxis(shootAngle, right) * horizontalDirection;
    }
    public bool CheckIfBallInRing2P(int PlayerId)
    {
        var ballObj = GetActiveBallObject(PlayerId);
        if (ballObj == null)
            return false;

        Vector3 pos = ballObj.transform.position;
        return IsInsidePlayArea(playArea, pos);
    }
    private bool IsInsidePlayArea(BoxCollider playArea, Vector3 ballPosition)
    {
        // Chuyển vị trí viên bi về không gian local của playArea
        Vector3 localPos = playArea.transform.InverseTransformPoint(ballPosition) - playArea.center;

        // Lấy kích thước của BoxCollider
        Vector3 halfSize = playArea.size / 2;

        // Kiểm tra xem vị trí có nằm trong phạm vi không
        return (localPos.x >= -halfSize.x && localPos.x <= halfSize.x) &&
               (localPos.y >= -halfSize.y && localPos.y <= halfSize.y) &&
               (localPos.z >= -halfSize.z && localPos.z <= halfSize.z);
    }
    private IEnumerator CheckBallsStopped()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f); // Chờ một khoảng thời gian để kiểm tra lại

            if (AllBallsStopped()) // Nếu tất cả viên bi đều đứng yên                  
            {
                Debug.Log("🛑 Tất cả viên bi đã dừng!");
                yield break; // Kết thúc Coroutine
            }
        }
    }
    public bool AllBallsStopped()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return true;

        var ballsToCheck = new List<NetworkObject>();
        var seenIds = new HashSet<NetworkId>();

        void AddBallIfNeeded(NetworkObject ball)
        {
            if (ball == null)
                return;

            if (!seenIds.Add(ball.Id))
                return;

            ballsToCheck.Add(ball);
        }

        foreach (var ringBall in manager.ringBalls)
        {
            AddBallIfNeeded(ringBall);
        }

        var players = manager.players;
        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (info.playerId <= 0 ||
                info.isHolding ||
                info.isDestroy ||
                info.statusPlayer == StatusPlayer.Destroy ||
                info.statusPlayer == StatusPlayer.WaitingDestroy)
            {
                continue;
            }

            AddBallIfNeeded(GetActiveBallObject(info.playerId));
        }

        List<string> movingReasons = null;

        foreach (var ball in ballsToCheck)
        {
            if (IsBallMovingForStopCheck(ball, out var reason))
            {
                movingReasons ??= new List<string>();
                movingReasons.Add(reason);
            }
        }

        if (movingReasons != null)
        {
            float now = Time.time;
            if (now - lastAllBallsStopLogTime > 0.5f)
            {
                Debug.Log($"[HOST] AllBallsStopped=FALSE moving={movingReasons.Count} :: {string.Join(" | ", movingReasons)}");
                lastAllBallsStopLogTime = now;
            }

            lastAllBallsStoppedState = false;
            return false;
        }

        if (!lastAllBallsStoppedState)
        {
            Debug.Log($"[HOST] AllBallsStopped=TRUE (tất cả viên bi đã đứng yên, checked={ballsToCheck.Count})");
        }

        lastAllBallsStoppedState = true;
        return true;
    }

    private bool IsBallMovingForStopCheck(NetworkObject ball, out string reason)
    {
        reason = string.Empty;

        if (ball == null)
            return false;

        var netRb = ball.GetComponent<NetworkRigidbody3D>();
        if (netRb == null || netRb.Rigidbody == null)
            return false;

        var rb = netRb.Rigidbody;
        if (rb.isKinematic || rb.IsSleeping())
            return false;

        float lin = rb.linearVelocity.magnitude;
        float ang = rb.angularVelocity.magnitude;

        int pid = 0;
        int holding = -1;
        int hasShoot = -1;

        if (ball.TryGetComponent<BallServerController>(out var ballCtrl))
        {
            pid = ballCtrl.playerId;
            holding = ballCtrl.IsHolding;
            hasShoot = ballCtrl.hasBeenShoot;

            if (ballCtrl.IsHolding == 1)
                return false;

            // hasBeenShoot=0 nghĩa là bi đã hoàn tất chu kỳ bắn; cho phép dư chấn nhỏ để tránh bị kẹt vòng lặp chờ.
            if (ballCtrl.hasBeenShoot == 0 &&
                lin <= AllBallsResidualLinearThreshold &&
                ang <= AllBallsResidualAngularThreshold)
            {
                return false;
            }
        }

        if (lin > AllBallsStopLinearThreshold || ang > AllBallsStopAngularThreshold)
        {
            reason = $"{ball.name} p={pid} v={lin:F3}/{AllBallsStopLinearThreshold} w={ang:F3}/{AllBallsStopAngularThreshold} shoot={hasShoot} hold={holding} sleep={rb.IsSleeping()} kin={rb.isKinematic}";
            return true;
        }

        return false;
    }

    public void HandleBallStopped(PlayerNetworkHandler handler, Vector3 ballPosition)
    {
        int playerId = handler != null ? handler.PlayerModel.playerId : 0;
        HandleBallStopped(handler, playerId, ballPosition);
    }

    public void HandleBallStopped(PlayerNetworkHandler handler, int playerId, Vector3 ballPosition)
    {
        MarkMatchProgress($"BallStop pid={playerId}");
        if (handler == null && playerId > 0)
        {
            handler = GetPlayerObject(playerId)?.GetComponent<PlayerNetworkHandler>();
        }

        if (handler == null)
        {
            if (TryRecoverExamPlayerFinish(playerId, ballPosition))
            {
                Debug.LogWarning($"[HOST] Khôi phục hoàn tất bài thi cho player {playerId} khi callback dừng bi không có PlayerNetworkHandler.");
            }
            else
            {
                Debug.LogWarning($"[HOST] Bỏ qua HandleBallStopped vì không resolve được PlayerNetworkHandler cho playerId={playerId}.");
            }

            return;
        }

        //if (!GameManagerNetWork.Instance.ValidateNetworkObjects())
        //    return;

        if (IsServerExamPhaseActive(NetworkObjectManager.Instance))
        {
            if (ShouldTreatStoppedBallAsExamShot(playerId))
                HandleExamShotStopped(handler, ballPosition);
            return;
        }

        if (handler.PlayerModel.statusPlayer == StatusPlayer.StartPoint)
        {
            Debug.Log($"[HOST] [BOT] Đã bắn xong ở StartPoint, kiểm tra luật/quậy/thua/điểm như bắn thường cho playerId={playerId}.");
            StartRegularShotStoppedRoutineIfNeeded(handler, playerId);
            return;
        }

        if (handler.PlayerModel.statusPlayer == StatusPlayer.ShootExam)
        {
            HandleExamShotStopped(handler, ballPosition);
        }
        else
        {
            StartRegularShotStoppedRoutineIfNeeded(handler, playerId);
        }
    }

    private void StartRegularShotStoppedRoutineIfNeeded(PlayerNetworkHandler handler, int playerId)
    {
        if (handler == null)
            return;

        if (playerId <= 0)
            playerId = handler.PlayerModel.playerId;

        if (playerId <= 0)
            return;

        if (!processingRegularShotStoppedPlayerIds.Add(playerId))
        {
            Debug.LogWarning($"[HOST][ShotStop] Bỏ qua callback dừng bi trùng cho playerId={playerId}.");
            return;
        }

        StartCoroutine(HandleRegularShotStoppedRoutine(handler, playerId));
    }

    private bool ShouldTreatStoppedBallAsExamShot(int playerId)
    {
        var manager = NetworkObjectManager.Instance;
        if (!IsServerExamPhaseActive(manager))
            return false;

        if (!TryGetNetworkPlayerInfo(playerId, out var info, out _))
            return false;

        if (info.isDestroy ||
            info.statusPlayer == StatusPlayer.Destroy ||
            info.statusPlayer == StatusPlayer.WaitingDestroy)
        {
            return false;
        }

        if (info.statusPlayer == StatusPlayer.StartPoint && !info.isHolding)
            return false;

        return true;
    }

    private void HandleExamShotStopped(PlayerNetworkHandler handler, Vector3 ballPosition)
    {
        MarkExamFinishedForPlayer(handler);
        Debug.Log($"🧍 [SERVER] {handler.PlayerModel.playerId} đã thi xong");
        LogExamStateSnapshot($"After HandleExamShotStopped pid={handler.PlayerModel.playerId}");

        var manager = NetworkObjectManager.Instance;
        if (manager == null || manager.IsExamScoreReady)
            return;

        if (AreAllExamPlayersFinished(true))
        {
            Debug.Log("[HOST][ExamScore] Tất cả người chơi đã báo thi xong – bắt đầu chờ bi dừng để chấm điểm.");
            StartExamScoreResolution();
        }
    }

    private static float CalculateExamRawScore(float distanceToLine)
    {
        float absDistance = Mathf.Abs(distanceToLine);
        return distanceToLine < 0f
            ? -Mathf.Clamp(absDistance, 1f, 10f)
            : Mathf.Clamp(10f - absDistance, 1f, 10f);
    }

    private static float RoundExamScore(float score, int decimals)
    {
        float factor = Mathf.Pow(10f, decimals);
        return Mathf.Round(score * factor) / factor;
    }

    private static float CalculateExamScore(float distanceToLine, int decimals = 1)
    {
        return RoundExamScore(CalculateExamRawScore(distanceToLine), decimals);
    }

    private void StartExamScoreResolution()
    {
        if (!gameObject.activeInHierarchy)
            return;

        StopExamShotTimeoutWatchdog("exam score resolution started");

        if (examScoreRoutine != null)
        {
            StopCoroutine(examScoreRoutine);
            examScoreRoutine = null;
        }

        examScoreRoutine = StartCoroutine(HandleExamScoreResolutionRoutine());
    }

    private IEnumerator HandleExamScoreResolutionRoutine()
    {
        RecoverPendingExamPlayersFromRestingBalls();
        LogExamStateSnapshot("Start waiting balls stop");

        int waitCount = 0;
        while (!AllBallsStopped())
        {
            waitCount++;
            if (waitCount % 4 == 0)
            {
                Debug.Log($"[HOST][ExamScore] Chờ bi dừng (check {waitCount}, mỗi 0.5s)");
                LogExamStateSnapshot($"Waiting check={waitCount}");
            }

            RecoverPendingExamPlayersFromRestingBalls();
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("[HOST][ExamScore] Tất cả bi đã đứng yên. Bắt đầu tính điểm.");
        RecoverPendingExamPlayersFromRestingBalls();
        LogExamStateSnapshot("Before ApplyExamScoresForAllPlayers");
        ApplyExamScoresForAllPlayers();
        examScoreRoutine = null;
    }

    private bool AreAllExamPlayersFinished(bool verbose = false)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return false;

        RecoverPendingExamPlayersFromRestingBalls();

        var players = manager.players;
        int activePlayers = 0;
        int finishedPlayers = 0;

        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (info.playerId == 0)
                continue;

            if (info.isDestroy ||
                info.statusPlayer == StatusPlayer.Destroy ||
                info.statusPlayer == StatusPlayer.WaitingDestroy)
            {
                continue;
            }

            activePlayers++;
            if (info.statusPlayer == StatusPlayer.StartPoint)
                finishedPlayers++;
        }

        if (verbose)
        {
            Debug.Log($"[HOST][ExamScore] AreAllExamPlayersFinished? active={activePlayers} finished={finishedPlayers}");
        }

        return activePlayers > 0 && finishedPlayers == activePlayers;
    }

    private void ApplyExamScoresForAllPlayers()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null || manager.IsExamScoreReady)
            return;

        RecoverPendingExamPlayersFromRestingBalls();

        if (!AreAllExamPlayersFinished(true))
        {
            Debug.LogWarning("[HOST] ApplyExamScoresForAllPlayers bị bỏ qua vì vẫn còn người chơi chưa hoàn tất bài thi sau bước recovery.");
            LogExamStateSnapshot("ApplyExamScoresForAllPlayers blocked - not finished");
            return;
        }

        float startPointZ = StartPointMain != null ? StartPointMain.position.z : 0f;
        var players = manager.players;
        bool updatedAny = false;

        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (info.playerId == 0)
                continue;

            if (info.statusPlayer != StatusPlayer.StartPoint &&
                info.statusPlayer != StatusPlayer.ShootExam)
                continue;

            if (ExamTimeoutPlayers.Contains(info.playerId))
                continue;

            var ball = GetActiveBallObject(info.playerId);
            if (ball == null)
                continue;

            var handler = GetPlayerObject(info.playerId)?.GetComponent<PlayerNetworkHandler>();
            if (handler != null)
            {
                UpdateExamScoreForPlayer(handler, ball.transform.position, true);
            }
            else
            {
                float distanceToLine = ball.transform.position.z - startPointZ;
                float scoreExam = CalculateExamScore(distanceToLine);
                info.distance = distanceToLine;
                info.statusPlayer = StatusPlayer.StartPoint;
                info.scoreExam = scoreExam;
                players.Set(i, info);
            }

            updatedAny = true;
        }

        if (!updatedAny)
            Debug.Log("[HOST][ExamScore] Không có điểm thi mới cần tính, có thể toàn bộ người chơi đã timeout.");

        ApplyExamScorePrecisionForTies();
        manager.IsExamScoreReady = true;
        manager.RpcShowPlayerList_Online();
        var finishedPlayers = manager.GetExamCompletedPlayers();
        int maxPlayers = manager.rpgRoomModel.MaxPlayer;
        if (maxPlayers > 0 && finishedPlayers.Count == maxPlayers)
        {
            Debug.Log($"👑 [SERVER] Tất cả đã thi xong");
            StartExamOrderResolution();
        }
    }

    private void RecoverPendingExamPlayersFromRestingBalls()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null || manager.IsExamScoreReady)
            return;

        var players = manager.players;
        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (info.playerId == 0)
                continue;

            if (info.isDestroy ||
                info.statusPlayer != StatusPlayer.ShootExam ||
                info.isHolding)
            {
                continue;
            }

            if (!examShotStartedPlayerIds.Contains(info.playerId))
                continue;

            var ball = GetActiveBallObject(info.playerId);
            if (!IsBallStoppedForExamRecovery(ball))
                continue;

            if (TryRecoverExamPlayerFinish(info.playerId, ball.transform.position))
            {
                Debug.Log($"[HOST] Recovery: đánh dấu hoàn tất bài thi cho player {info.playerId} vì bi đã đứng yên nhưng chưa nhận callback stop.");
            }
        }
    }

    private bool TryRecoverExamPlayerFinish(int playerId, Vector3 ballPosition)
    {
        if (playerId <= 0)
            return false;

        if (!TryGetNetworkPlayerInfo(playerId, out var info, out var index))
            return false;

        if (info.isDestroy ||
            info.statusPlayer == StatusPlayer.Destroy ||
            info.statusPlayer == StatusPlayer.WaitingDestroy)
        {
            return false;
        }

        if (info.statusPlayer != StatusPlayer.ShootExam && info.statusPlayer != StatusPlayer.StartPoint)
            return false;

        float startPointZ = StartPointMain != null ? StartPointMain.position.z : 0f;
        float distanceToLine = ballPosition.z - startPointZ;
        float scoreExam = CalculateExamScore(distanceToLine);

        info.statusPlayer = StatusPlayer.StartPoint;
        info.isHolding = false;
        info.distance = distanceToLine;
        info.scoreExam = scoreExam;

        var manager = NetworkObjectManager.Instance;
        if (manager != null && manager.HasStateAuthority)
        {
            manager.players.Set(index, info);
        }

        var handler = GetPlayerObject(playerId)?.GetComponent<PlayerNetworkHandler>();
        if (handler != null)
        {
            var model = handler.PlayerModel;
            model.statusPlayer = StatusPlayer.StartPoint;
            model.isHolding = false;
            model.distance = distanceToLine;
            model.scoreExam = scoreExam;
            handler.PlayerModel = model;
            handler.CurrentAnimState = CharacterAnimState.None;
        }

        var ballObj = GetActiveBallObject(playerId);
        if (ballObj != null && ballObj.TryGetComponent<BallServerController>(out var ballCtrl))
        {
            ballCtrl.IsHolding = 0;
        }


        Debug.Log($"[HOST][ExamScore] Recovery finish pid={playerId} dist={distanceToLine:F3} score={scoreExam:F2}");
        LogExamStateSnapshot($"Recovery success pid={playerId}");

        return true;
    }

    private static bool IsBallStoppedForExamRecovery(NetworkObject ball)
    {
        if (ball == null)
            return false;

        var ballCtrl = ball.GetComponent<BallServerController>();
        if (ballCtrl != null)
        {
            if (ballCtrl.IsHolding == 1 || ballCtrl.hasBeenShoot == 1)
                return false;
        }

        var netRb = ball.GetComponent<NetworkRigidbody3D>();
        if (netRb == null || netRb.Rigidbody == null)
            return true;

        var velocity = netRb.Rigidbody.linearVelocity;
        var angularVelocity = netRb.Rigidbody.angularVelocity;
        return velocity.magnitude <= 0.05f && angularVelocity.magnitude <= 1f;
    }

    private void MarkExamFinishedForPlayer(PlayerNetworkHandler handler)
    {
        if (handler == null)
            return;

        var model = handler.PlayerModel;
        pendingExamShotCommitRealtimeByPlayer.Remove(model.playerId);
        model.statusPlayer = StatusPlayer.StartPoint;
        model.isHolding = false;
        handler.PlayerModel = model;
        handler.CurrentAnimState = CharacterAnimState.None;

        var manager = NetworkObjectManager.Instance;
        if (manager != null &&
            manager.HasStateAuthority &&
            TryGetNetworkPlayerInfo(model.playerId, out var info, out var index))
        {
            info.statusPlayer = StatusPlayer.StartPoint;
            info.isHolding = false;
            manager.players.Set(index, info);
        }

        var ballObj = GetActiveBallObject(model.playerId);
        if (ballObj != null && ballObj.TryGetComponent<BallServerController>(out var ballCtrl))
            ballCtrl.IsHolding = 0;
    }

    private void UpdateExamScoreForPlayer(PlayerNetworkHandler handler, Vector3 ballPosition, bool markFinished)
    {
        float startPointZ = StartPointMain != null ? StartPointMain.position.z : 0f;
        float distanceToLine = ballPosition.z - startPointZ;
        float scoreExam = CalculateExamScore(distanceToLine);

        var model = handler.PlayerModel;
        if (markFinished)
        {
            model.statusPlayer = StatusPlayer.StartPoint;
        }

        model.distance = distanceToLine;
        model.scoreExam = scoreExam;
        handler.PlayerModel = model;
        handler.CurrentAnimState = CharacterAnimState.None;

        var manager = NetworkObjectManager.Instance;
        if (manager != null &&
            manager.HasStateAuthority &&
            TryGetNetworkPlayerInfo(model.playerId, out var info, out var index))
        {
            info.distance = distanceToLine;
            if (markFinished)
                info.statusPlayer = StatusPlayer.StartPoint;
            info.scoreExam = scoreExam;
            manager.players.Set(index, info);
        }
    }

    private void ApplyExamScorePrecisionForTies()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        var players = manager.players;
        var tiedPlayerIds = new HashSet<int>();
        var groupedScores = new Dictionary<float, List<int>>();

        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (info.playerId == 0)
                continue;

            if (info.statusPlayer != StatusPlayer.StartPoint &&
                info.statusPlayer != StatusPlayer.ShootExam)
                continue;

            float baseScore = CalculateExamScore(info.distance, 1);
            if (!groupedScores.TryGetValue(baseScore, out var list))
            {
                list = new List<int>();
                groupedScores[baseScore] = list;
            }

            list.Add(info.playerId);
        }

        foreach (var group in groupedScores.Values)
        {
            if (group.Count <= 1)
                continue;

            foreach (var playerId in group)
            {
                tiedPlayerIds.Add(playerId);
            }
        }

        if (tiedPlayerIds.Count == 0)
            return;

        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (!tiedPlayerIds.Contains(info.playerId))
                continue;

            float preciseScore = CalculateExamScore(info.distance, 2);
            if (Mathf.Approximately(info.scoreExam, preciseScore))
                continue;

            info.scoreExam = preciseScore;
            players.Set(i, info);

            var handler = GetPlayerObject(info.playerId)?.GetComponent<PlayerNetworkHandler>();
            if (handler == null)
                continue;

            var model = handler.PlayerModel;
            model.scoreExam = preciseScore;
            handler.PlayerModel = model;
        }
    }

    private IEnumerator HandleRegularShotStoppedRoutine(PlayerNetworkHandler handler, int resolvingPlayerId)
    {
        try
        {
            yield return HandleRegularShotStoppedCoreRoutine(handler);
        }
        finally
        {
            processingRegularShotStoppedPlayerIds.Remove(resolvingPlayerId);
        }
    }

    private IEnumerator HandleRegularShotStoppedCoreRoutine(PlayerNetworkHandler handler)
    {
        Debug.Log("🧍[SERVER] Tiến hành xử lý game sau khi bắn xong...");

        var server = NetworkObjectManager.Instance;
        if (server == null)
            yield break;

        yield return WaitForMovingRingBallsInsideArea();

        int scoreTotal = 0;
        server.isContinueTurn = false;

        var modelupdate = handler.PlayerModel;
        bool wasPower = modelupdate.statusPlayer == StatusPlayer.Power;
        bool wasStartPointShot = playersMovedToStartPointForTurn.Remove(modelupdate.playerId) ||
                                 modelupdate.statusPlayer == StatusPlayer.StartPoint;
        modelupdate.statusPlayer = StatusPlayer.Normal;

        bool shouldTogglePowerVfx = false;
        bool shouldNotifyOtherPower = false;
        float otherPlayersPowerValue = 0f;
        bool shouldShowMessage = false;
        string message = string.Empty;
        bool shouldShowCombo = false;
        bool shouldResetPowerGauge = false;
        string eliminationMessage = string.Empty;
        bool shouldShowEliminationMessage = false;
        bool shouldShowDestroyPermissionUnlocked = false;
       // bool shouldDelayNextTurnForQuayMessage = false;

        bool isQuay = CheckIfBallInRing2P(modelupdate.playerId);
        //Kiểm tra xem có vi phạm luật chơi là quậy không nếu có thì người chơi bị thua
        if (isQuay)
        {
            int previousScore = modelupdate.score;
            int returnedShotScore = ReturnOutsideRingBallsToRingForQuay();
            if (previousScore > 0)
                AddRingBalls(previousScore);

            if (returnedShotScore > 0 || previousScore > 0)
            {
                Debug.Log($"[HOST][Quay] Rollback điểm/bi cho playerId={modelupdate.playerId}: scoreTruocDo={previousScore}, biVuaRa={returnedShotScore}.");
            }

            modelupdate.score = 0;
            modelupdate.isDestroy = true;
            modelupdate.statusPlayer = StatusPlayer.Destroy;
            handler.PlayerModel = modelupdate;

            if (server.HasStateAuthority && TryGetNetworkPlayerInfo(modelupdate.playerId, out var info, out var index))
            {
                info.score = 0;
                info.isDestroy = true;
                info.statusPlayer = StatusPlayer.Destroy;
                server.players.Set(index, info);
            }

            ApplyDefeatAnimation(modelupdate.playerId);
            RemoveDestroyedPlayerRepresentationImmediately(modelupdate.playerId, "quay");
            ScheduleBotAutoLeaveIfNeeded(modelupdate.playerId, "quay");

            BroadcastImpactAnnouncement(modelupdate.fullname.ToString(), "noti_player_eliminated_by_messing", 1.6f);
            server.RpcShowMesByUser($"{modelupdate.fullname} đã quậy");
            //shouldDelayNextTurnForQuayMessage = true;
            CheckEndGame();
        }
        else
        {
            if (wasPower || modelupdate.score > 0)
            {
                shouldTogglePowerVfx = true;
                scoreTotal += CheckRemovePlayer(out shouldResetPowerGauge, out eliminationMessage);
                shouldShowEliminationMessage = !string.IsNullOrWhiteSpace(eliminationMessage);
            }

            bool hadDestroyPermissionBeforeScore = modelupdate.score > 0;
            int outBallScore = CheckOutBall();
            scoreTotal += outBallScore;

            if (scoreTotal > 0)
            {
                modelupdate.score += scoreTotal;
                modelupdate.combo += 1;
                handler.PlayerModel = modelupdate;
                SyncPlayerInfoState(modelupdate.playerId, info =>
                {
                    info.score = modelupdate.score;
                    info.combo = modelupdate.combo;
                    info.statusPlayer = modelupdate.statusPlayer;
                    info.isDestroy = modelupdate.isDestroy;
                    return info;
                });

                message = "+" + scoreTotal;
                shouldShowMessage = true;
                shouldShowCombo = true;
                shouldNotifyOtherPower = true;
                otherPlayersPowerValue = scoreTotal * 0.3f;
                shouldShowDestroyPermissionUnlocked = outBallScore > 0 &&
                                                       !hadDestroyPermissionBeforeScore &&
                                                       modelupdate.score > 0;
            }
            else
            {
                modelupdate.combo = 0;
                handler.PlayerModel = modelupdate;
                SyncPlayerInfoState(modelupdate.playerId, info =>
                {
                    info.combo = modelupdate.combo;
                    info.statusPlayer = modelupdate.statusPlayer;
                    info.isDestroy = modelupdate.isDestroy;
                    return info;
                });

                if (!server.isContinueTurn)
                {

                  // CircularButton.Instance.SetPower(0.4f);

                }
            }
        }

        CheckEndGame();
        bool isGameEnded = IsGameEnded;

        if (!isGameEnded && TryResolveBotOnlyMatchAfterCompletedTurn(modelupdate.playerId))
            isGameEnded = IsGameEnded;

        bool shouldStopPowerEffect = !isGameEnded && wasPower;

        if (shouldTogglePowerVfx || shouldNotifyOtherPower || shouldShowMessage || shouldShowCombo || shouldStopPowerEffect || shouldResetPowerGauge || shouldShowEliminationMessage)
        {
            server.RpcHandleRegularShotFeedback(
                modelupdate.playerId,
                shouldTogglePowerVfx,
                false,
                shouldNotifyOtherPower,
                otherPlayersPowerValue,
                shouldShowMessage,
                message,
                shouldShowCombo,
                modelupdate.combo,
                shouldShowDestroyPermissionUnlocked,
                shouldStopPowerEffect,
                shouldResetPowerGauge,
                shouldShowEliminationMessage,
                eliminationMessage);
        }

        if (isGameEnded)
            yield break;

        // if (shouldDelayNextTurnForQuayMessage)
        //     yield return new WaitForSeconds(1.25f);

        if (server.isContinueTurn)
        {
            server.HandleContinueTurn();
        }
        else
        {
           if (handler.HasStateAuthority && AnimatorController.Instance != null)
                handler.IdleAnimIndex = UnityEngine.Random.Range(0, AnimatorController.Instance.IdleAnimationCount);

            handler.CurrentAnimState = CharacterAnimState.Idle;
            if (wasStartPointShot)
                StartPlayerBackToGatherPointIfNeeded(modelupdate.playerId, "completed_start_point_turn");

            Debug.Log("🧍[SERVER] Chuyển lượt kế tiếp");
            HandelNextTurn(modelupdate.playerId);
        }
    }

    private IEnumerator WaitForMovingRingBallsInsideArea()
    {
        if (playArea == null)
            yield break;

        while (true)
        {
            bool hasMovingRingBallInside = false;

            foreach (var ringBall in GameObject.FindGameObjectsWithTag("RingBall"))
            {
                if (ringBall == null)
                    continue;

                if (!IsInsidePlayArea(playArea, ringBall.transform.position))
                    continue;

                var netRb = ringBall.GetComponent<NetworkRigidbody3D>();
                if (netRb != null && netRb.Rigidbody != null)
                {
                    var velocity = netRb.Rigidbody.linearVelocity;
                    var angularVelocity = netRb.Rigidbody.angularVelocity;

                    if (velocity.magnitude > 0.05f || angularVelocity.magnitude > 1f)
                    {
                        hasMovingRingBallInside = true;
                        break;
                    }
                }
                else
                {
                    var rb = ringBall.GetComponent<Rigidbody>();
                    if (rb != null && (rb.linearVelocity.magnitude > 0.05f || rb.angularVelocity.magnitude > 1f))
                    {
                        hasMovingRingBallInside = true;
                        break;
                    }
                }
            }

            if (!hasMovingRingBallInside)
                yield break;

            yield return new WaitForSeconds(0.1f);
        }
    }

    public bool GetIsHolding(int playerId)
    {
        var players = NetworkObjectManager.Instance.players;

        for (int i = 0; i < players.Length; i++)
        {
            if (players.Get(i).playerId == playerId)
            {
                return players.Get(i).isHolding;
            }
        }

        return false;
    }
 
    public int CheckRemovePlayer(out bool shouldResetPowerGauge, out string eliminationMessage)
    {
        int scoreTotal = 0;
        var snapshot = GetTurnOrderSnapshot();
        List<string> eliminatedMessages = new List<string>();
        shouldResetPowerGauge = false;

        foreach (var item in snapshot)
        {
            var playerBody = GetPlayerObject(item.playerId);
            if (playerBody == null)
                continue;

            var script = playerBody.GetComponent<PlayerNetworkHandler>();
            if (script == null)
                continue;

            var model = script.PlayerModel;
            if (model.statusPlayer == StatusPlayer.WaitingDestroy)
            {
                if (model.score > 0)
                    scoreTotal += model.score;

                SetPlayerStatus(item.playerId, StatusPlayer.Destroy);
                NetworkObjectManager.Instance.isContinueTurn = true;
                shouldResetPowerGauge = true;
                string playerName = model.fullname.ToString();
                eliminatedMessages.Add($"{playerName} Đã bị loại");
                BroadcastImpactAnnouncement(playerName, "noti_player_eliminated", 1.45f);
            }

        }

        // Không đánh dấu EndGame ở đây để tránh chặn CheckEndGame()/HandleEndGame().
        // Flow kết thúc trận cần chạy trọn vẹn để build payload, broadcast RPC
        // cho client thật và bỏ qua BOT trong cơ chế ACK/disconnect-ready.

        eliminationMessage = string.Join("\n", eliminatedMessages.Where(message => !string.IsNullOrWhiteSpace(message)));
        return scoreTotal;
    }

    public int CheckRemovePlayer()
    {
        return CheckRemovePlayer(out _, out _);
    }

    public void CheckEndGame()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        if (IsGameEnded)
        {
            bool hasPendingEndGameBroadcast = !isProcessingEndGame &&
                                             !HasBroadcastGameOverResults &&
                                             (LastOverGameResults == null || LastOverGameResults.Count == 0);

            if (!hasPendingEndGameBroadcast)
            {
                return;
            }

            Debug.LogWarning("⚠️ [HOST] Trận đã bị đánh dấu EndGame trước khi build/broadcast kết quả. Tiếp tục chạy CheckEndGame để hoàn tất flow gửi kết quả cho client thật.");
        }

        bool isOutOfRingBall = manager.ringBalls.Count == 0;
        var snapshot = GetTurnOrderSnapshot();

        int alive = snapshot.Count(t =>
        {
            var go = GetPlayerObject(t.playerId);
            if (go == null)
                return false;

            var handler = go.GetComponent<PlayerNetworkHandler>();
            if (handler == null)
                return false;

            var model = handler.PlayerModel;
            return !model.isDestroy && model.statusPlayer != StatusPlayer.Destroy && model.statusPlayer != StatusPlayer.WaitingDestroy;
        });

        if (alive <= 1)
        {
            bool isExamPhase = manager.StatusLoading == StatusLoadingGame.isExam;

            if (isExamPhase && examScoreRoutine != null)
            {
                Debug.LogWarning("[HOST][ExamScore] StartExamOrderResolution bị chặn vì examScoreRoutine vẫn đang chạy (chưa chấm điểm xong).");
                return;
            }

            if (isExamPhase && !manager.IsExamScoreReady)
            {
                Debug.LogWarning("[HOST][ExamScore] StartExamOrderResolution bị chặn vì IsExamScoreReady=false (chưa chấm điểm xong).");
                return;
            }

            {
                var survivor = snapshot.FirstOrDefault(t =>
                {
                    var go = GetPlayerObject(t.playerId);
                    if (go == null)
                        return false;

                    var handler = go.GetComponent<PlayerNetworkHandler>();
                    if (handler == null)
                        return false;

                    var model = handler.PlayerModel;
                    return !model.isDestroy && model.statusPlayer != StatusPlayer.Destroy && model.statusPlayer != StatusPlayer.WaitingDestroy;
                });

                if (survivor != null)
                {
                    var go = GetPlayerObject(survivor.playerId);
                    var handler = go.GetComponent<PlayerNetworkHandler>();
                    var model = handler.PlayerModel;
                    int remain = manager.ringBalls.Count;
                    if (remain > 0)
                    {
                        model.score += remain;
                        handler.PlayerModel = model;
                        SyncPlayerInfoState(survivor.playerId, info =>
                        {
                            info.score = model.score;
                            return info;
                        });
                    }

                    int target = manager.rpgRoomModel.betCount;
                    if (model.score < target)
                        AddScorePlayer(survivor.playerId, target - model.score);
                }
            }

            CancelExamResolutionRoutines("end game triggered with <= 1 alive player");
            MarkGameEnded();
            HandleEndGame(0);
            return;
        }

        if (isOutOfRingBall)
        {
            if (!NetworkObjectManager.Instance.isContinueTurn)
            {
                MarkGameEnded();
                HandleEndGame(0);
            }
            else
            {
                bool hasScore = snapshot.Any(t =>
                {
                    var go = GetPlayerObject(t.playerId);
                    if (go == null)
                        return false;

                    var handler = go.GetComponent<PlayerNetworkHandler>();
                    if (handler == null)
                        return false;

                    var model = handler.PlayerModel;
                    return !model.isDestroy &&
                           model.statusPlayer != StatusPlayer.Destroy &&
                           model.statusPlayer != StatusPlayer.WaitingDestroy &&
                           model.score > 0;
                });

                if (!hasScore)
                {
                    MarkGameEnded();
                    HandleEndGame(0);
                }
            }
        }
    }


    public void HandleEndGame(int playerIdSurrender)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("⚠️ Không thể kết thúc trận đấu vì NetworkObjectManager chưa được khởi tạo.");
            return;
        }

        CancelExamResolutionRoutines("HandleEndGame");
        MarkGameEnded();

        if (!manager.HasStateAuthority || isProcessingEndGame)
            return;

        HasBroadcastGameOverResults = false;
        StartCoroutine(HandleEndGameAndBroadcastRoutine(playerIdSurrender));
    }

    private bool ShouldAwaitClientGameOverSignal(int playerId)
    {
        if (playerId <= 0)
            return false;

        var botCtrl = BotPlayerController.Instance;
        if (botCtrl != null && botCtrl.IsBotPlayer(playerId))
        {
            Debug.Log($"🤖 [HOST] Bỏ qua playerId={playerId} trong danh sách chờ ACK/disconnect GameOver vì đây là BOT.");
            return false;
        }

        if (!IsPlayerConnectedToCurrentMatch(playerId))
        {
            Debug.Log($"ℹ️ [HOST] Bỏ qua playerId={playerId} trong danh sách chờ ACK/disconnect GameOver vì client đã rời trận.");
            return false;
        }

        return true;
    }

    private bool TryGetHostLocalPlayerId(out int hostLocalPlayerId)
    {
        hostLocalPlayerId = 0;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.TryGetRoomRunner(out var runner, logError: false) || runner == null || !runner.IsRunning || runner.IsShutdown)
            return false;

        if (runner.LocalPlayer.IsNone)
            return false;

        var quickMatchServer = QuickMatchServer.Instance;
        if (quickMatchServer == null || quickMatchServer.Runner != runner)
            return false;

        foreach (var result in LastOverGameResults)
        {
            if (result == null || result.playerId <= 0)
                continue;

            if (quickMatchServer.TryGetPlayerRefByUserId(result.playerId, out var playerRef) && playerRef == runner.LocalPlayer)
            {
                hostLocalPlayerId = result.playerId;
                return true;
            }
        }

        return false;
    }

    private void AutoCompleteHostLocalGameOverSignals()
    {
        if (!TryGetHostLocalPlayerId(out int hostLocalPlayerId) || hostLocalPlayerId <= 0)
            return;

        if (pendingGameOverAckPlayerIds.Contains(hostLocalPlayerId))
        {
            Debug.Log($"🏠 [HOST] Tự hoàn tất ACK GameOver cho host-local playerId={hostLocalPlayerId}.");
            pendingGameOverAckPlayerIds.Remove(hostLocalPlayerId);
            receivedGameOverAckPlayerIds.Add(hostLocalPlayerId);
        }

        if (pendingDisconnectReadyPlayerIds.Contains(hostLocalPlayerId))
        {
            Debug.Log($"🏠 [HOST] Tự hoàn tất disconnect-ready cho host-local playerId={hostLocalPlayerId}.");
            pendingDisconnectReadyPlayerIds.Remove(hostLocalPlayerId);
            receivedDisconnectReadyPlayerIds.Add(hostLocalPlayerId);
        }
    }

    public void BeginAwaitClientGameOverAcks(IEnumerable<int> playerIds)
    {
        pendingGameOverAckPlayerIds.Clear();
        receivedGameOverAckPlayerIds.Clear();

        if (playerIds == null)
        {
            Debug.LogWarning("⚠️ [HOST] Không có danh sách người chơi để chờ xác nhận GameOver.");
            return;
        }

        foreach (var playerId in playerIds)
        {
            if (!ShouldAwaitClientGameOverSignal(playerId))
                continue;

            pendingGameOverAckPlayerIds.Add(playerId);
        }

        AutoCompleteHostLocalGameOverSignals();

        Debug.Log($"📨 [HOST] Bắt đầu chờ ACK GameOver từ {pendingGameOverAckPlayerIds.Count} client(s): [{string.Join(", ", pendingGameOverAckPlayerIds)}]");
    }

    public void BeginAwaitClientDisconnectReadiness(IEnumerable<int> playerIds)
    {
        pendingDisconnectReadyPlayerIds.Clear();
        receivedDisconnectReadyPlayerIds.Clear();

        if (playerIds == null)
        {
            Debug.LogWarning("⚠️ [HOST] Không có danh sách người chơi để chờ tín hiệu sẵn sàng disconnect sau GameOver.");
            return;
        }

        foreach (var playerId in playerIds)
        {
            if (!ShouldAwaitClientGameOverSignal(playerId))
                continue;

            pendingDisconnectReadyPlayerIds.Add(playerId);
        }

        AutoCompleteHostLocalGameOverSignals();

        Debug.Log($"📨 [HOST] Bắt đầu chờ tín hiệu sẵn sàng disconnect từ {pendingDisconnectReadyPlayerIds.Count} client(s): [{string.Join(", ", pendingDisconnectReadyPlayerIds)}]");
    }

    public void RegisterClientGameOverAck(int playerId)
    {
        if (playerId <= 0)
        {
            Debug.LogWarning("⚠️ [HOST] Nhận ACK GameOver với playerId không hợp lệ.");
            return;
        }

        if (!pendingGameOverAckPlayerIds.Contains(playerId))
        {
            if (receivedGameOverAckPlayerIds.Contains(playerId))
            {
                Debug.Log($"ℹ️ [HOST] Client {playerId} gửi ACK GameOver lặp lại.");
                return;
            }

            Debug.LogWarning($"⚠️ [HOST] Nhận ACK GameOver ngoài danh sách chờ từ client {playerId}.");
            receivedGameOverAckPlayerIds.Add(playerId);
            return;
        }

        pendingGameOverAckPlayerIds.Remove(playerId);
        receivedGameOverAckPlayerIds.Add(playerId);

        Debug.Log($"✅ [HOST] Client {playerId} đã ACK GameOver. Còn chờ {pendingGameOverAckPlayerIds.Count} client(s).");

        if (pendingGameOverAckPlayerIds.Count == 0)
        {
            Debug.Log("✅ [HOST] Đã nhận đủ ACK GameOver từ tất cả client.");
        }
    }

    public void RegisterClientReadyToDisconnect(int playerId)
    {
        if (playerId <= 0)
        {
            Debug.LogWarning("⚠️ [HOST] Nhận tín hiệu disconnect-ready với playerId không hợp lệ.");
            return;
        }

        if (!pendingDisconnectReadyPlayerIds.Contains(playerId))
        {
            if (receivedDisconnectReadyPlayerIds.Contains(playerId))
            {
                Debug.Log($"ℹ️ [HOST] Client {playerId} gửi disconnect-ready lặp lại.");
                return;
            }

            Debug.LogWarning($"⚠️ [HOST] Nhận disconnect-ready ngoài danh sách chờ từ client {playerId}.");
            receivedDisconnectReadyPlayerIds.Add(playerId);
            return;
        }

        pendingDisconnectReadyPlayerIds.Remove(playerId);
        receivedDisconnectReadyPlayerIds.Add(playerId);

        Debug.Log($"✅ [HOST] Client {playerId} đã sẵn sàng disconnect sau GameOver. Còn chờ {pendingDisconnectReadyPlayerIds.Count} client(s).");

        if (pendingDisconnectReadyPlayerIds.Count == 0)
        {
            Debug.Log("✅ [HOST] Đã nhận đủ tín hiệu disconnect-ready từ tất cả client.");
        }
    }

    private IEnumerator HandleEndGameAndBroadcastRoutine(int playerIdSurrender)
    {
        isProcessingEndGame = true;
        Debug.Log("📦 [HOST] HandleEndGameAndBroadcastRoutine bắt đầu. Đang tính kết quả...");

        // Phase 1: Tính kết quả (nhanh, đồng bộ) — KHÔNG chờ API.
        BuildEndGameResults(playerIdSurrender);

        Debug.Log("📦 [HOST] Kết quả đã tính xong. Gửi RPC cho client TRƯỚC khi gọi API...");

        try
        {
            var manager = NetworkObjectManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("⚠️ Không thể gửi kết quả kết thúc trận đấu vì NetworkObjectManager chưa được khởi tạo.");
                yield break;
            }

            var results = LastOverGameResults ?? new List<OverGameRequest>();
            if (results.Count == 0)
            {
                Debug.LogWarning("⚠️ Không có dữ liệu kết thúc trận đấu để gửi cho client.");
            }

            BeginAwaitClientGameOverAcks(results.Select(x => x.playerId));
            BeginAwaitClientDisconnectReadiness(results.Select(x => x.playerId));

            string json = JsonHelper.ToJson(results);
            Debug.Log($"📤 [HOST] Gửi RpcShowOverGameResult tới toàn bộ client. Payload size: {json.Length} ký tự.");
            manager.RpcShowOverGameResult(json);
            HasBroadcastGameOverResults = true;
        }
        finally
        {
            // Phase 2 hoàn thành — RPC đã gửi. Bây giờ gọi API (chậm, không block client).
        }

        // Phase 3: Gọi API lưu kết quả + sync ball damage (chạy SAU khi RPC đã gửi).
        Debug.Log("📦 [HOST] RPC đã gửi. Bắt đầu gọi API PostOverGame + SyncBallDamageUpdates...");
        yield return PostEndGameResultsToApi(playerIdSurrender);

        isProcessingEndGame = false;
    }


    /// <summary>
    /// Phase 1: Tính toán kết quả trận đấu (đồng bộ, nhanh).
    /// Gọi TRƯỚC khi gửi RPC để client nhận kết quả ngay lập tức.
    /// </summary>
    private void BuildEndGameResults(int playerIdSurrender)
    {
        LastOverGameResults.Clear();
        MarkGameEnded();
        var server = NetworkObjectManager.Instance;
        if (server == null)
            return;

        var orderedPlayers = GetFinalOrderedPlayerInfos();
        if (orderedPlayers.Count == 0)
            return;

        int maxPlayers = Mathf.Max(server.rpgRoomModel.MaxPlayer, 1);
        int rounds = (server.TurnCount / maxPlayers) + 1;

        float maxLevel = orderedPlayers.Max(player => (float)player.level);
        float minLevel = orderedPlayers.Min(player => (float)player.level);
        float levelGap = maxLevel - minLevel;

        float winCoef = 1.5f + levelGap * 0.05f;
        float loseCoef = 0.5f + levelGap * 0.02f;

        int betByPlayer = server.rpgRoomModel.betCount / maxPlayers;

        foreach (var current in orderedPlayers)
        {
            bool isSurrender = playerIdSurrender > 0 && playerIdSurrender == current.playerId;
            bool isWin = current.score > betByPlayer && !isSurrender;
            bool isDraw = current.score == betByPlayer && !isSurrender;

            int expGain = Mathf.RoundToInt(betByPlayer * (isWin ? winCoef : loseCoef));
            if (isDraw)
                expGain = Mathf.RoundToInt(betByPlayer);
            if (isSurrender)
                expGain = Mathf.RoundToInt(betByPlayer * loseCoef);

            var data = new OverGameRequest
            {
                playerId = current.playerId,
                tunrOrder = current.turnOrder,
                typeMatchGid = (int)server.rpgRoomModel.TypeMatch,
                StatusWin = isSurrender ? (int)StatusWin.Surrender : (isWin ? (int)StatusWin.Win : (isDraw ? (int)StatusWin.Dickens : (int)StatusWin.Lose)),
                rounds = rounds,
                MapGame = server.rpgRoomModel.gameScene.Value,
                MaxPlayer = server.rpgRoomModel.MaxPlayer,
                marbBet = betByPlayer,
                marblesWon = isSurrender ? 0 : current.score,
                marblesLost = isSurrender ? betByPlayer : Mathf.Max(betByPlayer - current.score, 0),
                expGained = expGain,
                playerName = current.fullname.ToString(),
                description = "Online match",
                avatarUrl = current.avatarUrl.ToString()
            };

            LastOverGameResults.Add(data);

            if (!isWin && !isDraw)
                TrySetPlayerAnimState(current.playerId, CharacterAnimState.LoseEmotion);
        }
    }

    /// <summary>
    /// Phase 3: Gửi kết quả lên API backend (chậm, gọi SAU khi RPC đã gửi cho client).
    /// </summary>
    private IEnumerator PostEndGameResultsToApi(int playerIdSurrender)
    {
        if (APIManager.Instance == null)
            yield break;

        var postPayload = LastOverGameResults
            .Where(r => r.playerId > 0)
            .ToList();
        if (postPayload.Count > 0)
        {
            var postTask = APIManager.Instance.PostOverGame(postPayload);
            yield return StartCoroutine(APIManager.Instance.RunTask(postTask, null));
        }

        yield return SyncBallDamageUpdates();
    }

    // Giữ lại HandleEndGameRoutine cho backward compatibility (ResolveAbandonedRoomRoutine dùng flow riêng)
    public IEnumerator HandleEndGameRoutine(int playerIdSurrender)
    {
        BuildEndGameResults(playerIdSurrender);
        yield return PostEndGameResultsToApi(playerIdSurrender);
    }

    public void ProcessSurrender(int playerId)
    {
        var serverRPC = NetworkObjectManager.Instance;
        var surrenderGO = GetPlayerObject(playerId);
        if (surrenderGO == null) return;
        var surrenderHandler = surrenderGO.GetComponent<PlayerNetworkHandler>();
        if (surrenderHandler == null) return;
        var model = surrenderHandler.PlayerModel;

        string playerName = model.fullname.ToString();

        int score = model.score;

        // Đánh dấu người chơi bị loại
        model.score = 0;
        model.statusPlayer = StatusPlayer.Destroy;
        model.isDestroy = true;
        surrenderHandler.PlayerModel = model;

        ApplyDefeatAnimation(playerId);
        RemoveDestroyedPlayerRepresentationImmediately(playerId, "surrender");
        ScheduleBotAutoLeaveIfNeeded(playerId, "surrender");

        var snapshot = GetTurnOrderSnapshot();
        int alive = snapshot.Count(t =>
        {
            var go = GetPlayerObject(t.playerId);
            if (go == null)
                return false;

            var handler = go.GetComponent<PlayerNetworkHandler>();
            if (handler == null)
                return false;

            var playerModel = handler.PlayerModel;
            return !playerModel.isDestroy && playerModel.statusPlayer != StatusPlayer.Destroy && playerModel.statusPlayer != StatusPlayer.WaitingDestroy;
        });

        bool endGame = alive <= 1;

        if (!endGame)
        {
            if (score > 0)
                AddRingBalls(score);

            NetworkObjectManager.Instance.isContinueTurn = true;
            //CircularButton.Instance.SetPower(0.3f);
        }
        else
        {
            var totalScore = NetworkObjectManager.Instance.rpgRoomModel.betCount;
            var survivor = snapshot.FirstOrDefault(t =>
            {
                var go = GetPlayerObject(t.playerId);
                if (go == null)
                    return false;

                var handler = go.GetComponent<PlayerNetworkHandler>();
                if (handler == null)
                    return false;

                var playerModel = handler.PlayerModel;
                return !playerModel.isDestroy && playerModel.statusPlayer != StatusPlayer.Destroy && playerModel.statusPlayer != StatusPlayer.WaitingDestroy;
            });

            if (survivor != null)
            {
                //Người thắng cuối cùng do đối thủ đầu hàng + full điểm
                AddScorePlayer(survivor.playerId, totalScore);
                serverRPC.RpcShowPlayerList_Online();
            }

            MarkGameEnded();
        }

        NetworkObjectManager.Instance?.RpcNotifyPlayerSurrender(playerId, playerName, endGame);

        if (endGame)
        {
            HandleEndGame(playerId);
        }
        else
        {
            CheckEndGame();
        }
    }

    private void BroadcastImpactAnnouncement(string title, string localizationKey, float displayDuration = 1.4f)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(localizationKey))
            return;

        NetworkObjectManager.Instance?.RpcShowImpactAnnouncement(title, localizationKey, displayDuration);
    }

    private IEnumerator SyncBallDamageUpdates()
    {
        if (APIManager.Instance == null || pendingBallDamages.Count == 0)
            yield break;

        var payload = pendingBallDamages
            .Where(pair => pair.Value > 0f)
            .Select(pair => new PlayerItemDamageUpdateEntry
            {
                playerId = pair.Key.playerId,
                itemId = pair.Key.itemId,
                seq = pair.Key.seq,
                damage = pair.Value
            })
            .ToList();

        if (payload.Count == 0)
            yield break;

        bool success = false;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.UpdatePlayerItemDamageAsync(payload),
            result => success = result));

        if (success)
            pendingBallDamages.Clear();
    }

    private readonly struct BallDamageKey : IEquatable<BallDamageKey>
    {
        public readonly int playerId;
        public readonly int itemId;
        public readonly int seq;

        public BallDamageKey(int playerId, int itemId, int seq)
        {
            this.playerId = playerId;
            this.itemId = itemId;
            this.seq = seq;
        }

        public bool Equals(BallDamageKey other)
        {
            return playerId == other.playerId && itemId == other.itemId && seq == other.seq;
        }

        public override bool Equals(object obj)
        {
            return obj is BallDamageKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = playerId;
                hash = (hash * 397) ^ itemId;
                hash = (hash * 397) ^ seq;
                return hash;
            }
        }
    }

    private void PlayerFellIntoWater(int playerId)
    {
        StartCoroutine(PlayerFellIntoWaterRoutine(playerId));
    }

    public bool HandleBallWaterHit(int playerId, Vector3 ballPosition, GameObject sourceObject = null, string source = null)
    {
        if (playerId <= 0)
            return false;

        if (_waterHitTriggeredPlayers.Contains(playerId))
            return true;

        if (WaterObject == null && !TryResolveWaterObject(logFailure: true))
            return false;

        if (!IsPointInsideWaterCollider(ballPosition))
            return false;

        if (!CanApplyWaterElimination(playerId, out _))
            return false;

        _waterHitTriggeredPlayers.Add(playerId);

        string sourceName = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
        string objectName = sourceObject != null ? sourceObject.name : "null";
        Debug.Log($"[HOST][WaterHitConfirmed] pid={playerId} source={sourceName} obj={objectName} pos={ballPosition} water={GetHierarchyPath(WaterObject)}");

        NetworkObjectManager.Instance?.RpcPlayWaterSplashVfx(playerId, ResolveWaterSplashPosition(ballPosition));
        PlayerFellIntoWater(playerId);
        return true;
    }

    private void EvaluateWaterFallbackForActiveBalls()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        var ordered = manager.GetOrderedPlayerInfos();
        for (int i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            int playerId = entry.playerId;
            if (playerId <= 0)
                continue;

            var playerObject = manager.GetPlayerObject(playerId);
            var handler = playerObject != null ? playerObject.GetComponent<PlayerNetworkHandler>() : null;
            if (handler == null)
                continue;

            var model = handler.PlayerModel;
            if (model.isDestroy || model.statusPlayer == StatusPlayer.Destroy || model.statusPlayer == StatusPlayer.WaitingDestroy)
                continue;

            var ballObj = manager.GetActiveBallObject(playerId);
            var ballCtrl = ballObj != null ? ballObj.GetComponent<BallServerController>() : null;
            if (ballCtrl == null)
                continue;

            if (ballCtrl.hasBeenShoot != 1)
            {
                _waterHitTriggeredPlayers.Remove(playerId);
                continue;
            }

            if (_waterHitTriggeredPlayers.Contains(playerId))
                continue;

            Vector3 ballPos = ballCtrl.transform.position;
            if (!IsPointInsideWaterCollider(ballPos))
                continue;

            HandleBallWaterHit(playerId, ballPos, WaterObject != null ? WaterObject.gameObject : null, "fallback");
        }
    }

    private Vector3 ResolveWaterSplashPosition(Vector3 ballPosition)
    {
        if (WaterObject == null && !TryResolveWaterObject(logFailure: false))
            return ballPosition;

        var colliders = WaterObject.GetComponentsInChildren<Collider>(true);
        Collider bestCollider = null;
        float bestDistanceSqr = float.MaxValue;

        for (int c = 0; c < colliders.Length; c++)
        {
            var collider = colliders[c];
            if (collider == null || !collider.enabled)
                continue;

            if (IsPointInsideCollider(collider, ballPosition))
            {
                bestCollider = collider;
                break;
            }

            var closest = collider.ClosestPoint(ballPosition);
            float distanceSqr = (closest - ballPosition).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestCollider = collider;
            }
        }

        if (bestCollider == null)
            return ballPosition;

        var splashPosition = ballPosition;
        splashPosition.y = bestCollider.bounds.max.y + 0.02f;
        return splashPosition;
    }

    private bool IsPointInsideWaterCollider(Vector3 point)
    {
        if (WaterObject == null && !TryResolveWaterObject(logFailure: false))
            return false;

        var colliders = WaterObject.GetComponentsInChildren<Collider>(true);
        for (int c = 0; c < colliders.Length; c++)
        {
            var collider = colliders[c];
            if (collider == null || !collider.enabled)
                continue;

            if (IsPointInsideCollider(collider, point))
                return true;
        }

        return false;
    }

    private static bool IsPointInsideCollider(Collider collider, Vector3 point)
    {
        if (collider == null || !collider.bounds.Contains(point))
            return false;

        var closest = collider.ClosestPoint(point);
        return (closest - point).sqrMagnitude <= 0.0001f;
    }

    public bool TryResolveWaterObject(bool logFailure = false)
    {
        if (WaterObject != null)
        {
            var existingCollider = WaterObject.GetComponentInChildren<Collider>(true);
            if (existingCollider == null)
            {
                if (logFailure && !_waterObjectMissingColliderLogged)
                {
                    Debug.LogError($"❌ [HOST][WaterConfig] WaterObject đã gán nhưng không có Collider: {GetHierarchyPath(WaterObject)}");
                    _waterObjectMissingColliderLogged = true;
                }

                return false;
            }

            _waterObjectResolveFailureLogged = false;
            _waterObjectMissingColliderLogged = false;
            return true;
        }

        var waterObject = FindWaterObjectByTag();
        if (waterObject != null)
        {
            WaterObject = waterObject.transform;
            _waterObjectResolveFailureLogged = false;
            _waterObjectMissingColliderLogged = false;
            Debug.Log($"[HOST][WaterConfig] WaterObject auto-resolved by tag 'Water': {GetHierarchyPath(WaterObject)}");
            return true;
        }

        if (logFailure && !_waterObjectResolveFailureLogged)
        {
            Debug.LogError($"❌ [HOST][WaterConfig] Không tìm thấy GameObject tag 'Water' để gán WaterObject. Candidates by name: {BuildWaterCandidateLog()}");
            _waterObjectResolveFailureLogged = true;
        }

        return false;
    }

    private static GameObject FindWaterObjectByTag()
    {
        try
        {
            var taggedObjects = GameObject.FindGameObjectsWithTag("Water");
            if (taggedObjects != null && taggedObjects.Length > 0)
            {
                var withCollider = taggedObjects.FirstOrDefault(go => go != null && go.GetComponentInChildren<Collider>(true) != null);
                if (withCollider != null)
                {
                    return withCollider;
                }

                return taggedObjects.FirstOrDefault(go => go != null);
            }
        }
        catch (UnityException ex)
        {
            Debug.LogError($"❌ [HOST][WaterConfig] Tag 'Water' chưa được định nghĩa hoặc không hợp lệ: {ex.Message}");
        }

        var transforms = UnityEngine.Object.FindObjectsOfType<Transform>(true);
        var taggedTransform = transforms
            .Where(t => t != null && HasUnityTag(t.gameObject, "Water"))
            .OrderByDescending(t => t.GetComponentInChildren<Collider>(true) != null)
            .FirstOrDefault();

        return taggedTransform != null ? taggedTransform.gameObject : null;
    }

    private static bool HasUnityTag(GameObject target, string tag)
    {
        if (target == null)
        {
            return false;
        }

        try
        {
            return target.CompareTag(tag);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private static string BuildWaterCandidateLog()
    {
        var transforms = UnityEngine.Object.FindObjectsOfType<Transform>(true);
        var candidates = transforms
            .Where(t => t != null && t.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0)
            .Take(8)
            .Select(t => $"{GetHierarchyPath(t)} tag={SafeGetTag(t.gameObject)} active={t.gameObject.activeInHierarchy} hasCollider={t.GetComponentInChildren<Collider>(true) != null}");

        var result = string.Join(" | ", candidates);
        return string.IsNullOrWhiteSpace(result) ? "<none>" : result;
    }

    private static string SafeGetTag(GameObject target)
    {
        if (target == null)
        {
            return "<null>";
        }

        try
        {
            return target.tag;
        }
        catch (UnityException)
        {
            return "<invalid>";
        }
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        var names = new List<string>();
        var current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private bool CanApplyWaterElimination(int playerId, out PlayerInfoStruct model)
    {
        model = default;

        if (!TryGetWaterPlayerModel(playerId, out model))
            return false;

        if (model.isDestroy ||
            model.statusPlayer == StatusPlayer.Destroy ||
            model.statusPlayer == StatusPlayer.WaitingDestroy)
        {
            return false;
        }

        var manager = NetworkObjectManager.Instance;
        bool isActiveExamPhase = IsServerExamPhaseActive(manager);
        return model.statusPlayer != StatusPlayer.ShootExam || !isActiveExamPhase;
    }

    private bool TryGetWaterPlayerModel(int playerId, out PlayerInfoStruct model)
    {
        model = default;

        var manager = NetworkObjectManager.Instance;
        var playerGO = GetPlayerObject(playerId);
        if (playerGO != null)
        {
            var handler = playerGO.GetComponent<PlayerNetworkHandler>();
            if (handler != null)
            {
                model = handler.PlayerModel;
                return true;
            }
        }

        if (manager != null && TryGetNetworkPlayerInfo(playerId, out var info, out _))
        {
            model = new PlayerInfoStruct
            {
                playerId = info.playerId,
                fullname = info.fullname,
                statusPlayer = info.statusPlayer,
                score = info.score,
                isDestroy = info.isDestroy
            };
            return true;
        }

        return false;
    }

    private IEnumerator PlayerFellIntoWaterRoutine(int playerId)
    {
        if (!CanApplyWaterElimination(playerId, out var model))
            yield break;

        SetPlayerStatus(playerId, StatusPlayer.Destroy);
        if (NetworkObjectManager.Instance != null)
        {
            string playerName = $"Người chơi {model.fullname}";
            NetworkObjectManager.Instance.RpcShowMesByUser($"{playerName} bị rơi xuống nước!");
        }
        yield return new WaitForSeconds(2f);
        CheckEndGame();
    }

    public void HandleExamTimeout(int playerId)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        if (!ExamTimeoutPlayers.Contains(playerId))
            ExamTimeoutPlayers.Add(playerId);

        var snapshot = GetTurnOrderSnapshot();
        if (snapshot.Count == 0)
            return;

        ExamTimeoutPlayers = ExamTimeoutPlayers
            .Where(id => snapshot.Any(entry => entry.playerId == id))
            .ToList();

        var rnd = new System.Random();
        ExamTimeoutPlayers = ExamTimeoutPlayers.OrderBy(x => rnd.Next()).ToList();

        var activePlayerIds = snapshot
            .Where(entry => !ExamTimeoutPlayers.Contains(entry.playerId))
            .Select(entry => entry.playerId)
            .ToList();

        foreach (var id in ExamTimeoutPlayers)
        {
            if (!activePlayerIds.Contains(id))
                activePlayerIds.Add(id);
        }

        for (int i = 0; i < activePlayerIds.Count; i++)
        {
            int pid = activePlayerIds[i];
            ApplyTurnOrderToPlayer(pid, i);
        }

        manager.RpcShowPlayerList_Online();
    }
 
 

    #region[=========================== MOVE ==========================]
    #region [===================== MOVE TO START POINT =============================]
    //public IEnumerator MoveToStartPointWithTeleport(int playerId, List<Vector3> occupiedPositions, float minDistance = 0.5f)
    //{
    //    Vector3 startPointPos = StartPoint.transform.position;
    //    float minZOffset = -0.5f;
    //    float maxZOffset = -0.5f;

    //    var playerObject = GetPlayerObject(playerId);
    //    if (playerObject == null) yield break;

    //    NetworkCharacterController controller = playerObject.GetComponent<NetworkCharacterController>();
    //    if (controller == null || !controller.HasStateAuthority) yield break;

    //    Vector3 newPosition = Vector3.zero;
    //    bool isValidPosition = false;
    //    int maxAttempts = 20;

    //    while (!isValidPosition && maxAttempts-- > 0)
    //    {
    //        float randomX = Random.Range(startPointPos.x - 5f, startPointPos.x + 5f);
    //        float randomZ = startPointPos.z + Random.Range(minZOffset, maxZOffset);
    //        float y = playerObject.transform.position.y;

    //        newPosition = new Vector3(randomX, y, randomZ);

    //        isValidPosition = true;
    //        foreach (var pos in occupiedPositions)
    //        {
    //            if (Vector3.Distance(newPosition, pos) < minDistance)
    //            {
    //                isValidPosition = false;
    //                break;
    //            }
    //        }
    //    }

    //    if (isValidPosition)
    //    {
    //        occupiedPositions.Add(newPosition);

    //        // ✅ Tính hướng xoay mặt về playArea
    //        Quaternion lookRotation = Quaternion.identity;
    //        Vector3 lookDir = playArea.transform.position - newPosition;
    //        if (lookDir != Vector3.zero)
    //            lookRotation = Quaternion.LookRotation(lookDir);

    //        controller.Teleport(newPosition, lookRotation);

    //        // ✅ Chờ 1 frame để đảm bảo client sync (nếu cần)
    //        yield return null;
    //    }
    //    //step sau khi di chuyển quay mặt về mức

    //}

    #endregion
    //private void HandleMovement()
    //{
    //    // Xác định phạm vi di chuyển hợp lý
    //    var playerToUpdate = GameManagerNetWork.Instance.GetCurrentPlayerGame();
    //    if (playerToUpdate.playerId <= 0)
    //        return; // không có dữ liệu hợp lệ

    //    var playerbody = GetPlayerObject(playerToUpdate.playerId);
    //    if (playerbody != null)
    //    {
    //        if (playerToUpdate.statusPlayer == StatusPlayer.ShootExam)
    //        {

    //            float leftLimit = ExamMain.position.x - 2f;   // Giới hạn qua trái 3f từ targetPosition
    //            float rightLimit = ExamMain.position.x + 2f;  // Giới hạn qua phải 3f từ targetPosition
    //            if (moveLeft && playerbody.transform.position.x > leftLimit)
    //                playerbody.transform.position += Vector3.left * moveSpeed * Time.deltaTime;

    //            if (moveRight && playerbody.transform.position.x < rightLimit)
    //                 playerbody.transform.position += Vector3.right * moveSpeed * Time.deltaTime;
    //        }
    //        else
    //        {
    //            float leftLimit = StartPointMain.position.x - 2f;   // Giới hạn qua trái 3f từ targetPosition
    //            float rightLimit = StartPointMain.position.x + 2f;  // Giới hạn qua phải 3f từ targetPosition
    //            if (moveRight && playerbody.transform.position.x > leftLimit)
    //                playerbody.transform.position += Vector3.left * moveSpeed * Time.deltaTime;

    //            if (moveLeft && playerbody.transform.position.x < rightLimit)
    //                playerbody.transform.position += Vector3.right * moveSpeed * Time.deltaTime;
    //        }
    //    }
    //}

    //public void StartMoveLeft()
    //{
    //    Debug.Log("Move Left");
    //    moveLeft = true;
    //}

    //public void StopMoveLeft()
    //{
    //    moveLeft = false;
    //}

    //public void StartMoveRight()
    //{
    //    moveRight = true;
    //}

    //public void StopMoveRight()
    //{
    //    moveRight = false;
    //}


    #endregion

    // Kiểm tra xem có kẻ địch nào ở gần người chơi hiện tại hay không
    // Mặc định bán kính kiểm tra là 0.75f (~1.5 gang tay)
    public bool IsEnemyNearPlayer(int playerId, float radius)
    {
        if (playerId == 0)
            return false;

        var currentBall = GetActiveBallObject(playerId);
        if (currentBall == null)
            return false;

        Vector3 myPos = currentBall.transform.position;
        foreach (var entry in GetTurnOrderSnapshot())
        {
            if (entry.playerId == playerId)
                continue;

            var playerGO = GetPlayerObject(entry.playerId);
            var ball = GetActiveBallObject(entry.playerId);
            if (playerGO == null || ball == null)
                continue;

            var handler = playerGO.GetComponent<PlayerNetworkHandler>();
            var model = handler.PlayerModel;
            if (model.isDestroy || model.statusPlayer == StatusPlayer.Destroy || model.statusPlayer == StatusPlayer.WaitingDestroy)
                continue;

            float distance = Vector3.Distance(myPos, ball.transform.position);
            if (distance <= radius)
                return true;
        }
        return false;
    }

    //public bool IsEnemyNearCurrentPlayer(float radius)
    //{
    //    int currentId = GameManagerNetWork.Instance.loginUserModel.UserId;
    //    return IsEnemyNearPlayer(currentId, radius);
    //}

    //public int GetNearestEnemyPlayerId(float radius = 1.5f)
    //{
    //    int currentId = GameManagerNetWork.Instance.loginUserModel.UserId;
    //    return GetNearestEnemyPlayerId(currentId, radius);
    //}

 
}
#endregion
#endif
