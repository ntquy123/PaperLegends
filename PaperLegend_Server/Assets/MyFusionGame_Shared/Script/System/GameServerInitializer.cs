#if UNITY_SERVER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;





public class GameServerInitializer : MonoBehaviour
{
    public static GameServerInitializer? Instance { get; private set; }

    [Header("SYSTEM CONFIG")]
    //public GameObject GameSessionHost;
    public GameObject GameOnlineUI;

    [Header("Spawn Prefabs")]
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private NetworkObject ringBallPrefab;
    [SerializeField] private NetworkObject ballPlayerPrefab;
    [SerializeField, Tooltip("Collider prefab for lightly chipped ball state.")] private GameObject chippedBallColliderPrefab;
    [SerializeField, Tooltip("Collider prefab for cracked ball state.")] private GameObject crackedBallColliderPrefab;
    [SerializeField, Tooltip("Collider prefab for heavily damaged ball state.")] private GameObject shatteredBallColliderPrefab;
    [SerializeField] private NetworkObject bananaPeelPrefab;

    [Header("Paper Legends")]
    [SerializeField, Tooltip("Enable Paper Legends rules on the dedicated server. When enabled, the server uses paper hero battle logic instead of legacy marble logic.")]
    private bool usePaperLegendRules = true;

    [SerializeField, Tooltip("Server-side paper character catalog. Maps modelId to the matching collider/network prefab for easier maintenance.")]
    private PaperLegendCharacterServerCatalog paperLegendCharacterServerCatalog;

    [SerializeField, Tooltip("Scene-level paper character model overrides. Use for quick tests or when no catalog is assigned.")]
    private PaperLegendCharacterModelSpawnEntry[] paperLegendCharacterModels;

    [SerializeField, Tooltip("Paper Legends match host prefab. Handles kill limit, respawn, end game, and match results. A fallback is created if this is not assigned.")]
    private NetworkObject paperLegendMatchHostPrefab;

    [SerializeField, Tooltip("Experience pickup prefab spawned on the map. If not assigned, EXP pickups are skipped.")]
    private NetworkObject paperLegendExperiencePickupPrefab;

    [SerializeField, Tooltip("Initial/respawn points for Paper Legends. Scene objects tagged paper_legend_spawn are preferred when present.")]
    private Transform[] paperLegendSpawnPoints;

    [SerializeField, Tooltip("Respawn points after death. If empty, Paper Legend Spawn Points are reused.")]
    private Transform[] paperLegendRespawnPoints;

    [SerializeField, Tooltip("EXP pickup spawn points. If empty, fallback positions around the play area are used.")]
    private Transform[] paperLegendExperienceSpawnPoints;

    [SerializeField, Min(1), Tooltip("Maximum Paper Legends match slots. FFA currently defaults to 4 players.")]
    private int paperLegendMaxPlayers = PaperLegendRuntimeState.DefaultFreeForAllPlayers;

    [SerializeField, Min(1), Tooltip("Minimum real players required to start. Missing slots are filled with bots.")]
    private int paperLegendMinRealPlayers = PaperLegendRuntimeState.DefaultMinRealPlayers;

    [SerializeField, Tooltip("Automatically fill missing match slots with bots after matchmaking.")]
    private bool paperLegendFillBots = true;

    [SerializeField, Tooltip("Enable Paper Legends bot AI. Bots flick toward targets and try to land on opponents.")]
    private bool paperLegendEnableBotAi = true;

    [SerializeField, Min(1), Tooltip("Kill count required to win the FFA match. For example, 15 means the first player to 15 kills wins.")]
    private int paperLegendKillLimit = 15;

    [SerializeField, Min(0f), Tooltip("Respawn delay after a character is eliminated, in seconds.")]
    private float paperLegendRespawnDelaySeconds = 5f;

    [SerializeField, Tooltip("Enable base objective win condition. FFA usually relies on kill limit, so this is normally disabled.")]
    private bool paperLegendEnableBaseObjectiveWin = false;

    [SerializeField, Min(0), Tooltip("Number of EXP pickups active on the map. Set to 0 to disable EXP pickups.")]
    private int paperLegendExperiencePickupCount = 8;

    [SerializeField, Min(1), Tooltip("EXP granted when collecting one pickup.")]
    private int paperLegendExperiencePerPickup = 30;

    [SerializeField, Min(0f), Tooltip("EXP pickup respawn time after collection, in seconds.")]
    private float paperLegendExperiencePickupRespawnSeconds = 10f;

    [SerializeField, Min(0.1f), Tooltip("Fallback spawn radius around the map center when no spawn point is configured.")]
    private float paperLegendFallbackSpawnRadius = 2.5f;

    [SerializeField, Min(0f), Tooltip("Extra spawn height to keep character colliders from clipping into the ground.")]
    private float paperLegendSpawnHeightOffset = 0.35f;

    private bool _isLoadingMap;
    private Scene _loadedGameScene;
    private bool _hasLoadedGameScene;

    //public NetworkObject PlayerPrefab => playerPrefab;
    public NetworkObject RingBallPrefab => ringBallPrefab;
   // public NetworkObject BallPlayerPrefab => ballPlayerPrefab;
   // public NetworkObject BananaPeelPrefab => bananaPeelPrefab;

    public int[] GetPaperLegendCharacterModelIds()
    {
        var characterModels = ResolvePaperLegendCharacterModels();
        if (characterModels == null || characterModels.Length == 0)
            return Array.Empty<int>();

        return characterModels
            .Where(IsSelectablePaperLegendCharacterModel)
            .Select(entry => entry.modelId)
            .Distinct()
            .ToArray();
    }

    private PaperLegendCharacterModelSpawnEntry[] ResolvePaperLegendCharacterModels()
    {
        if (paperLegendCharacterServerCatalog != null && paperLegendCharacterServerCatalog.HasAnyModelId)
            return paperLegendCharacterServerCatalog.CharacterModels;

        return paperLegendCharacterModels ?? Array.Empty<PaperLegendCharacterModelSpawnEntry>();
    }

    private bool IsSelectablePaperLegendCharacterModel(PaperLegendCharacterModelSpawnEntry entry)
    {
        return entry.modelId > 0 && entry.characterPrefab != null;
    }

    private bool TryResolveConfiguredPaperLegendSpawnPoints(
        string sceneName,
        out Transform[] spawnPoints,
        out string error)
    {
        spawnPoints = Array.Empty<Transform>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            error = $"[PaperLegends][Spawn] Missing sceneName. Cannot resolve '{SceneLogicConfig.PaperLegendSpawnTag}' from SceneLogicConfig.";
            return false;
        }

        var serverLauncher = ServerLauncher.Instance;
        if (serverLauncher == null)
        {
            error = $"[PaperLegends][Spawn] ServerLauncher is missing. Cannot load SceneLogicConfig host template for map '{sceneName}'.";
            return false;
        }

        if (!serverLauncher.TryCreateSessionHost(sceneName, out var host) || host == null)
        {
            error = $"[PaperLegends][Spawn] Map host template for '{sceneName}' was not created from SceneLogicConfig.";
            return false;
        }

        var configuredPoints = host.PaperLegendSpawnPoints?
            .Where(point => point != null)
            .ToArray() ?? Array.Empty<Transform>();

        if (configuredPoints.Length < SceneLogicConfig.PaperLegendSpawnPointCount)
        {
            error = $"[PaperLegends][Spawn] SceneLogicConfig for map '{sceneName}' must define at least {SceneLogicConfig.PaperLegendSpawnPointCount} spawn point(s) with tag '{SceneLogicConfig.PaperLegendSpawnTag}', but only {configuredPoints.Length} were configured.";
            return false;
        }

        spawnPoints = configuredPoints;

        Debug.Log($"[PaperLegends][Spawn] Loaded {spawnPoints.Length} spawn point(s) from SceneLogicConfig for map '{sceneName}'.");
        return true;
    }

    public GameInitializationReport LastInitializationReport { get; private set; } = GameInitializationReport.Failure(GameInitializationFailureReason.Unknown, "Initialization has not started.");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GameServerInitializer instances detected. Replacing the previous instance.");
        }

        Instance = this;
        PaperLegendRuntimeState.SetPaperLegendMatch(usePaperLegendRules);
    }

    private void SetInitializationReport(GameInitializationReport report)
    {
        LastInitializationReport = report;
    }

    private bool EnsureRunnerConnected(NetworkRunner runner, NetworkObjectManager manager, string context)
    {
        if (runner == null)
        {
            string message = $"NetworkRunner could not be resolved while {context}.";
            Debug.LogError(message);
            SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.MissingRunner, message));
            manager?.ReportInitializationFailure("noti_network_false");
            return false;
        }

        if (!runner.IsRunning || runner.IsShutdown)
        {
            string message = $"Runner disconnected while {context}.";
            Debug.LogError(message);
            SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.ConnectionLost, message));
            manager?.ReportInitializationFailure("noti_network_false");
            return false;
        }

        return true;
    }
    public IEnumerator InitializeGameOnline(NetworkRunner runnerReference, NetworkObjectManager manager)
    {
        SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.Unknown, "Server initialization is running..."));

        if (runnerReference == null)
        {
            string message = "NetworkRunner could not be resolved for server initialization.";
            Debug.LogError(message);
            SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.MissingRunner, message));
            yield break;
        }

        if (!EnsureRunnerConnected(runnerReference, manager, "starting initialization"))
            yield break;

        EnsureRunnerPhysicsSupport(runnerReference);

        if (manager == null)
        {
            string message = "NetworkObjectManager could not be resolved for the current room.";
            Debug.LogError(message);
            SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.MissingManager, message));
            yield break;
        }

        yield return LoadMapGameRoutine(runnerReference, manager);

        if (!manager.IsServerSceneLoadCompleted)
        {
            string message = "Game map has not finished loading. Initialization stopped.";
            Debug.LogWarning(message);
            SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.SceneLoadFailed, message));
            yield break;
        }

        if (!EnsureRunnerConnected(runnerReference, manager, "after map load"))
            yield break;

        if (usePaperLegendRules || PaperLegendRuntimeState.IsPaperLegendMatch)
        {
            PaperLegendRuntimeState.SetPaperLegendMatch(true);

            var paperLegendInitializer = GetComponent<PaperLegendGameServerInitializer>();
            if (paperLegendInitializer == null)
                paperLegendInitializer = gameObject.AddComponent<PaperLegendGameServerInitializer>();

            var rawSceneName = manager.rpgRoomModel.gameScene.ToString();
            var paperLegendSceneName = GameMapHelper.TryParseMapId(rawSceneName, out var paperLegendMapId)
                ? GameMapHelper.ToSceneName(paperLegendMapId)
                : rawSceneName;
            if (!TryResolveConfiguredPaperLegendSpawnPoints(paperLegendSceneName, out var paperLegendSceneSpawnPoints, out var spawnConfigError))
            {
                Debug.LogError(spawnConfigError);
                SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.SceneLoadFailed, spawnConfigError));
                manager?.ReportInitializationFailure("noti_map_config_failed");
                yield break;
            }

            var paperLegendConfig = new PaperLegendServerMatchConfig
            {
                CharacterModels = ResolvePaperLegendCharacterModels(),
                MatchHostPrefab = paperLegendMatchHostPrefab,
                ExperiencePickupPrefab = paperLegendExperiencePickupPrefab,
                SpawnPoints = paperLegendSceneSpawnPoints,
                RespawnPoints = paperLegendSceneSpawnPoints,
                ExperienceSpawnPoints = paperLegendExperienceSpawnPoints,
                MaxPlayers = paperLegendMaxPlayers,
                MinRealPlayers = paperLegendMinRealPlayers,
                FillBots = paperLegendFillBots,
                EnableBotAi = paperLegendEnableBotAi,
                KillLimit = paperLegendKillLimit,
                RespawnDelaySeconds = paperLegendRespawnDelaySeconds,
                EnableBaseObjectiveWin = paperLegendEnableBaseObjectiveWin,
                ExperiencePickupCount = paperLegendExperiencePickupCount,
                ExperiencePerPickup = paperLegendExperiencePerPickup,
                ExperiencePickupRespawnSeconds = paperLegendExperiencePickupRespawnSeconds,
                FallbackSpawnRadius = paperLegendFallbackSpawnRadius,
                SpawnHeightOffset = paperLegendSpawnHeightOffset
            };

            yield return paperLegendInitializer.InitializeGameOnline(
                runnerReference,
                manager,
                _hasLoadedGameScene ? _loadedGameScene : SceneManager.GetActiveScene(),
                paperLegendConfig);

            SetInitializationReport(paperLegendInitializer.LastInitializationReport);
            yield break;
        }

        var sceneName = manager.rpgRoomModel.gameScene.ToString();
        var serverLauncher = ServerLauncher.Instance;

        GameSessionNetWork_Host? host = null;
        bool usingPreconfiguredHost = false;

        if (serverLauncher != null && serverLauncher.TryCreateSessionHost(sceneName, out var sessionHost))
        {
            host = sessionHost;
            usingPreconfiguredHost = true;
        }
        else
        {
            host = GameSessionNetWork_Host.Instance;
        }

        if (host == null)
        {
            string message = "GameSessionNetWork_Host is not initialized.";
            Debug.LogError(message);
            SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.SpawnFailed, message));
            yield break;
        }

        var mapManager = MapSceneConfigManager.Instance;
        if (mapManager != null)
        {
            if (mapManager.TryGetConfig(sceneName, out var config))
            {
                if (!usingPreconfiguredHost && !config.Apply(host, manager))
                {
                    const string message = "Map configuration failed. Game initialization stopped.";
                    Debug.LogWarning(message);
                    SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.SceneLoadFailed, message));
                    yield break;
                }
            }
            else
            {
                Debug.LogWarning($"MapSceneConfig not found for map {sceneName}.");
            }
        }
        else
        {
            Debug.LogWarning("MapSceneConfigManager is not ready.");
        }

        bool playersLoaded = false;
        string playerLoadError = string.Empty;
        yield return LoadAndAssignPlayers(runnerReference, manager, (success, error) =>
        {
            playersLoaded = success;
            playerLoadError = error;
        });
        if (!playersLoaded)
        {
            string message = string.IsNullOrEmpty(playerLoadError)
                ? "Could not load player data."
                : playerLoadError;
            Debug.LogWarning(message);
            SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.DataLoadFailed, message));
            yield break;
        }

        if (!EnsureRunnerConnected(runnerReference, manager, "syncing players"))
            yield break;

        bool spawnedPlayers = false;
        string spawnPlayersError = string.Empty;
        yield return SpawnPlayers(runnerReference, host, manager, (success, error) =>
        {
            spawnedPlayers = success;
            spawnPlayersError = error;
        });
        if (!spawnedPlayers)
        {
            string message = string.IsNullOrEmpty(spawnPlayersError)
                ? "Could not spawn players."
                : spawnPlayersError;
            Debug.LogWarning(message);
            SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.SpawnFailed, message));
            yield break;
        }

        if (!EnsureRunnerConnected(runnerReference, manager, "spawning characters"))
            yield break;

        if (!TrySpawnRingBalls(runnerReference, host, manager))
        {
            SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.SpawnFailed, "Could not spawn ring balls."));
            yield return new WaitForSeconds(2f);
            manager.RpcLoadMenuGame();
            yield break;
        }

        ActivateBananaPeelSpawns(host, manager);

        bool spawnedBalls = false;
        string spawnBallsError = string.Empty;
        yield return SpawnPlayerBalls(runnerReference, host, manager, (success, error) =>
        {
            spawnedBalls = success;
            spawnBallsError = error;
        });

        if (!spawnedBalls)
        {
            string message = string.IsNullOrEmpty(spawnBallsError)
                ? "Could not spawn player balls."
                : spawnBallsError;
            Debug.LogWarning(message);
            SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.SpawnFailed, message));
            yield break;
        }

        if (!EnsureRunnerConnected(runnerReference, manager, "spawn culi"))
            yield break;

        bool hasBallsForAll = true;
        foreach (var info in manager.GetOrderedPlayerInfos())
        {
            var list = manager.GetPlayerBalls(info.playerId);
            if (list == null || list.Count == 0)
            {
                hasBallsForAll = false;
                break;
            }
        }

        if (!hasBallsForAll)
        {
            const string message = "Could not spawn enough culi for every player.";
            Debug.LogWarning(message);
            SetInitializationReport(GameInitializationReport.Failure(GameInitializationFailureReason.SpawnFailed, message));
            yield break;
        }

        if (host != null)
        {
            yield return host.ArrangePlayersForExamStart();
        }

        Debug.Log("Game initialized successfully.");
        SetInitializationReport(GameInitializationReport.SuccessReport("Server initialization completed."));
    }

    private Transform[] ResolvePaperLegendSceneSpawnPoints()
    {
        var taggedPoints = FindSceneTransformsByTag(SceneLogicConfig.PaperLegendSpawnTag);
        if (taggedPoints.Length > 0)
        {
            if (taggedPoints.Length < SceneLogicConfig.PaperLegendSpawnPointCount)
            {
                Debug.LogWarning($"[PaperLegends][Spawn] Chi tim thay {taggedPoints.Length}/{SceneLogicConfig.PaperLegendSpawnPointCount} diem '{SceneLogicConfig.PaperLegendSpawnTag}' trong scene.");
            }

            return taggedPoints;
        }

        var host = GameSessionNetWork_Host.Instance;
        if (host != null && host.PaperLegendSpawnPoints != null && host.PaperLegendSpawnPoints.Count > 0)
            return FilterAssignedTransforms(host.PaperLegendSpawnPoints);

        return Array.Empty<Transform>();
    }

    private static Transform[] FindSceneTransformsByTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return Array.Empty<Transform>();

        try
        {
            return GameObject.FindGameObjectsWithTag(tag)
                .Where(obj => obj != null)
                .Select(obj => obj.transform)
                .Distinct()
                .OrderBy(transform => transform.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (UnityException ex)
        {
            Debug.LogWarning($"[PaperLegends][Spawn] Khong tim thay tag '{tag}' trong scene hoac TagManager: {ex.Message}");
            return Array.Empty<Transform>();
        }
    }

    private static Transform[] FindIndexedSceneTransforms(string tagPrefix, int count)
    {
        if (string.IsNullOrWhiteSpace(tagPrefix) || count <= 0)
            return Array.Empty<Transform>();

        var points = new List<Transform>();
        for (int i = 1; i <= count; i++)
        {
            string tag = $"{tagPrefix}{i}";
            try
            {
                var target = GameObject.FindWithTag(tag);
                if (target != null)
                    AddUniqueTransform(points, target.transform);
            }
            catch (UnityException ex)
            {
                Debug.LogWarning($"[PaperLegends][Spawn] Khong tim thay tag '{tag}' trong scene hoac TagManager: {ex.Message}");
            }
        }

        return points.ToArray();
    }

    private static Transform[] FilterAssignedTransforms(IEnumerable<Transform> transforms)
    {
        if (transforms == null)
            return Array.Empty<Transform>();

        var result = new List<Transform>();
        foreach (var transform in transforms)
            AddUniqueTransform(result, transform);

        return result.ToArray();
    }

    private static void AddUniqueTransform(List<Transform> targets, Transform transform)
    {
        if (targets == null || transform == null || targets.Contains(transform))
            return;

        targets.Add(transform);
    }

    private IEnumerator LoadAndAssignPlayers(NetworkRunner runner, NetworkObjectManager manager, Action<bool, string> onCompleted)
    {
        var listUserId = new List<int>();
        var quickMatchServer = ResolveQuickMatchServer(runner);
        bool requireReadyStatus = quickMatchServer != null && quickMatchServer.IsReadyPhaseEnabled;

        if (quickMatchServer != null)
        {
            quickMatchServer.PruneInactiveRegisteredPlayers();

            foreach (var playerState in quickMatchServer.QuickMatchPlayers)
            {
                if (requireReadyStatus && playerState.Status != QuickMatchServer.QuickMatchPlayerStatusCodes.Ready)
                {
                    continue;
                }

                if (!requireReadyStatus && playerState.Status == QuickMatchServer.QuickMatchPlayerStatusCodes.Cancelled)
                {
                    continue;
                }

                if (playerState.PlayerId > 0 && !listUserId.Contains(playerState.PlayerId))
                    listUserId.Add(playerState.PlayerId);
            }
        }
        else
        {
            string warning = "QuickMatchServer not found; cannot read the ready player list.";
            Debug.LogWarning(warning);
        }

        manager?.SetExpectedClientReadyCount(listUserId.Count);

        if (listUserId.Count == 0)
        {
            string message = "No valid players available for data loading.";
            Debug.LogWarning(message);
            manager?.ReportInitializationFailure("noti_network_false");
            onCompleted?.Invoke(false, message);
            yield break;
        }

        var roomName = runner != null && runner.SessionInfo != null ? runner.SessionInfo.Name : string.Empty;
        if (string.IsNullOrWhiteSpace(roomName))
        {
            const string roomNameError = "Could not resolve roomName for room registration.";
            Debug.LogWarning(roomNameError);
            manager?.ReportInitializationFailure("noti_network_false");
            onCompleted?.Invoke(false, roomNameError);
            yield break;
        }

        PlayerInfoStruct[] playerList = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetListPlayerGameById(listUserId),
            result => playerList = result));

        if (playerList == null || playerList.Length == 0)
        {
            string message = "Could not load player data.";
            Debug.LogWarning(message);
            manager?.ReportInitializationFailure("noti_network_false");
            onCompleted?.Invoke(false, message);
            yield break;
        }

        var playersArray = manager.players;
        for (int i = 0; i < playersArray.Length; i++)
        {
            playersArray.Set(i, default);
        }

        int syncCount = Mathf.Min(playersArray.Length, playerList.Length);
        for (int i = 0; i < syncCount; i++)
        {
            playersArray.Set(i, playerList[i]);
        }

        if (playerList.Length > playersArray.Length)
        {
            Debug.LogWarning($"Player count ({playerList.Length}) exceeds configured capacity ({playersArray.Length}). Only syncing the first {syncCount} players.");
        }

        // ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚ÂÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚ÂÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚ÂÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ Bot Fill: ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Ân bot vÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â o slot trÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œng nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¿u thiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¿u ngÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â°ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âi ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚ÂÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚ÂÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚ÂÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬
        int realPlayerCount = syncCount;
        int maxPlayers = manager.rpgRoomModel.MaxPlayer;
        if (realPlayerCount < maxPlayers && realPlayerCount > 0)
        {
            EnsureBotController();
            int botsNeeded = Mathf.Min(maxPlayers, playersArray.Length) - realPlayerCount;
            var realPlayers = Enumerable.Range(0, realPlayerCount)
                .Select(playersArray.Get)
                .Where(info => info.playerId != 0)
                .ToList();

            List<BotPlayerData> botList = null;
            yield return StartCoroutine(APIManager.Instance.RunTask(
                APIManager.Instance.GetBotPlayersAsync(botsNeeded),
                result => botList = result));

            if (botList != null && botList.Count > 0)
            {
                int addedBotCount = 0;
                for (int i = 0; i < botList.Count && (realPlayerCount + i) < playersArray.Length; i++)
                {
                    botList[i].Level = GetRandomBotLevel(realPlayers);
                    var botInfo = BotPlayerController.CreateBotPlayerInfo(botList[i]);
                    playersArray.Set(realPlayerCount + i, botInfo);
                    BotPlayerController.Instance?.RegisterBot(botInfo.playerId);
                    addedBotCount++;
                    Debug.Log($"[BOT] Added bot '{botInfo.fullname}' (ID={botInfo.playerId}, level={botInfo.level}) to slot {realPlayerCount + i}.");
                }

                if (addedBotCount > 0 && manager.HasStateAuthority && realPlayerCount > 0)
                {
                    NormalizeRoomBetCount(manager, realPlayerCount + addedBotCount, "bot fill");
                }
            }
            else
            {
                Debug.LogWarning("[BOT] Failed to load bot list from API.");
            }
        }

        NormalizeRoomBetCount(manager, manager.GetOrderedPlayerInfos().Count, "load players");

        onCompleted?.Invoke(true, string.Empty);
    }

    private void EnsureBotController()
    {
        if (BotPlayerController.Instance == null)
        {
            var go = new GameObject("BotPlayerController");
            go.AddComponent<BotPlayerController>();
            DontDestroyOnLoad(go);
        }
    }

    private int GetRandomBotLevel(IReadOnlyList<PlayerInfoStruct> realPlayers)
    {
        if (realPlayers == null || realPlayers.Count == 0)
            return 1;

        var anchorPlayer = realPlayers[Random.Range(0, realPlayers.Count)];
        int anchorLevel = Mathf.Max(anchorPlayer.level, 1);
        int minLevel = Mathf.Max(1, anchorLevel - 3);
        int maxLevel = Mathf.Max(minLevel, anchorLevel + 3);
        return Random.Range(minLevel, maxLevel + 1);
    }

    private int NormalizeRoomBetCount(NetworkObjectManager manager, int totalPlayers, string reason)
    {
        if (manager == null)
            return 0;

        int normalizedTotalPlayers = Mathf.Max(totalPlayers, 0);
        int currentTotalBet = Mathf.Max(manager.rpgRoomModel.betCount, 0);
        if (normalizedTotalPlayers <= 0)
            return currentTotalBet;

        int configuredBetPerPlayer = 0;
        var serverLauncher = ServerLauncher.Instance;
        if (serverLauncher != null)
        {
            configuredBetPerPlayer = Mathf.Max(serverLauncher.BetPerPlayer, 0);
        }

        if (configuredBetPerPlayer <= 0)
        {
            int roomCapacity = Mathf.Max(manager.rpgRoomModel.MaxPlayer, 1);
            configuredBetPerPlayer = Mathf.Max(currentTotalBet / roomCapacity, 0);
        }

        if (configuredBetPerPlayer <= 0)
            return currentTotalBet;

        int normalizedTotalBet = configuredBetPerPlayer * normalizedTotalPlayers;
        if (normalizedTotalBet == currentTotalBet)
            return normalizedTotalBet;

        if (manager.HasStateAuthority)
        {
            var roomModel = manager.rpgRoomModel;
            roomModel.betCount = normalizedTotalBet;
            manager.rpgRoomModel = roomModel;
        }

        Debug.Log($"[ROOM] Normalized total bet from {currentTotalBet} to {normalizedTotalBet} ({configuredBetPerPlayer}/player x {normalizedTotalPlayers} players) at {reason}.");
        return normalizedTotalBet;
    }

    private IEnumerator SpawnPlayers(NetworkRunner runner, GameSessionNetWork_Host host, NetworkObjectManager manager, Action<bool, string> onCompleted)
    {
        if (playerPrefab == null)
        {
            string message = "Player prefab not found.";
            Log.Error(message);
            onCompleted?.Invoke(false, message);
            yield break;
        }

        yield return new WaitForSeconds(5f);
        Time.timeScale = 1f;

        var quickMatchServer = ResolveQuickMatchServer(runner);
        var playerInfos = manager.GetOrderedPlayerInfos();
        if (playerInfos.Count == 0)
        {
            string message = "Player list is empty after sync.";
            Debug.LogWarning(message);
            manager.ReportInitializationFailure("noti_network_false");
            onCompleted?.Invoke(false, message);
            yield break;
        }

        Debug.Log("Finished loading players.");

        var ids = playerInfos.Select(p => p.playerId).ToList();
        var authorityMap = BuildPlayerAuthorityMap(quickMatchServer, ids);

        int spawnedCount = 0;
        foreach (var playerInfo in playerInfos)
        {
            var playerId = playerInfo.playerId;
            var spawnPoint = host.SpawnPlayerPoint;
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            authorityMap.TryGetValue(playerId, out var playerAuthority);
            var spawnedPlayer = SpawnWithServerAuthority(runner, playerPrefab, spawnPos, spawnRot, playerAuthority);
            if (spawnedPlayer == null)
            {
                string message = "Could not spawn player.";
                Log.Error(message);
                onCompleted?.Invoke(false, message);
                yield break;
            }

            spawnedCount++;

            EnsureNetworkObjectInGameScene(runner, spawnedPlayer);

            spawnedPlayer.name = "Player_" + playerId;
            var handler = spawnedPlayer.GetComponent<PlayerNetworkHandler>();
            if (handler != null)
            {
                handler.PlayerModel = playerInfo;
            }

            if (handler != null)
                yield return new WaitUntil(() => handler.IsSpawned == 1);

            if (host.TerrainGround != null)
                PlaceOnGround(spawnedPlayer, host.TerrainGround);
        }

        yield return new WaitForSeconds(2f);
        bool success = spawnedCount == playerInfos.Count && spawnedCount > 0;
        string resultMessage = success ? string.Empty : "Could not spawn all characters.";
        onCompleted?.Invoke(success, resultMessage);
    }

    private bool TrySpawnRingBalls(NetworkRunner runner, GameSessionNetWork_Host host, NetworkObjectManager manager)
    {
        if (ringBallPrefab == null)
        {
            Log.Error("RingBallPrefab is not assigned.");
            return false;
        }

        if (host == null)
        {
            Debug.LogWarning("Host not found for ring marble spawn.");
            return false;
        }

        if (host.playArea == null)
        {
            Debug.LogWarning("No valid PlayArea found for ring marble spawn.");
            return false;
        }
        var setting = manager.rpgRoomModel;
        Debug.Log($"[HOST] Syncing culi data inside ring. Count: {setting.betCount}.");
        MarbleSpawnData[] spawnDataList = GenerateMarbleSpawnData(host.playArea, setting.betCount);
        AdjustMarbleSpawnData(spawnDataList, host.playArea, host.TerrainGround);

        bool checkSpawnBall = SpawnMarbles(runner, host, manager, spawnDataList);
        if (!checkSpawnBall)
        {
            Debug.LogWarning("Network error while spawning ring marbles.");
            manager.ReportInitializationFailure("noti_network_false");
            return false;
        }
        // TÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡m thÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âi khÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â´ng dÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¹ng vÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ gÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢y ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©c chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¿.NghÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©a lÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â  chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Ân ngÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â«u nhiÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªn 2 viÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªn ring ball vÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â  nhÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢n mass lÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªn x4 (targetRigidbody.mass *= 4f).
        //ApplyHeavyWeightToRandomRingBalls(manager, 2, 4f);

        MoveRingBallsIntoPlayArea(host, manager);

        return true;
    }

    private void ActivateBananaPeelSpawns(GameSessionNetWork_Host host, NetworkObjectManager manager)
    {
        /*
         * HÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â m nÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â y chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¹u trÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ch nhiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¡m bÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­t ngÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â«u nhiÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªn mÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢t vÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â i bÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â«y chuÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œi trong map vÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â 
         * phÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡t tÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡n trÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ng thÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡i ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ cho mÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âi client. Quy trÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬nh chung nhÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â° sau:
         *  1. ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã¢â‚¬Å“ng bÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ lÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡i danh sÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ch cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡c ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢m spawn (trÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªn host) ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¯c chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¯n dÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¯ liÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¡u cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­p nhÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­t.
         *  2. ChÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Ân ngÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â«u nhiÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªn tÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œi ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œa 3 vÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¹ trÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­ spawn khÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£ dÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¥ng.
         *  3. BÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­t GameObject tÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â°ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ng ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©ng trÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªn host vÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â  lÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â°u lÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡i chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â° sÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œ ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£ chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Ân.
         *  4. GÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âi NetworkObjectManager.SyncBananaSpawnActivation ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ gÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­i chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â° sÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œ ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¿n tÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¥t cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£ client.
         */

        // NÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¿u host chÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â°a ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â°ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£c truyÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Ân vÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â o thÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ khÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â´ng lÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â m gÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£ ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ trÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡nh lÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Âi null.
        if (host == null)
            return;

        // CÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­p nhÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­t lÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡i danh sÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ch cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡c ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢m spawn chuÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œi trong scene dÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â±a trÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªn tag "BananaSpawn".
        host.RefreshBananaSpawnPointsFromScene();

        // KiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢m tra nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¿u sau khi cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­p nhÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­t mÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â  vÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â«n khÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â´ng cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢m spawn nÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â o thÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ ghi log vÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â  thoÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡t.
        if (host.BananaSpawnPoints == null || host.BananaSpawnPoints.Count == 0)
        {
            Debug.LogWarning("BananaSpawn position not found in scene.");
            return;
        }

        // ChÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â° bÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­t tÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œi ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œa 3 ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢m spawn cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¹ng lÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âºc hoÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â·c ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­t hÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡n nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¿u tÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¢ng sÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œ ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢m spawn nhÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â hÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡n.
        int activationCount = Mathf.Min(5, host.BananaSpawnPoints.Count);
        if (activationCount <= 0)
            return;

        // TÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­p hÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£p tÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡m ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ lÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â°u cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡c chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â° sÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œ ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â°ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£c chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Ân ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£m bÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£o khÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â´ng bÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¹ trÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¹ng nhau.
        var selectedIndices = new HashSet<int>();
        while (selectedIndices.Count < activationCount)
        {
            // LÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¥y ngÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â«u nhiÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªn mÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢t chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â° sÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œ trong danh sÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ch cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡c ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢m spawn hiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¡n cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³.
            int index = Random.Range(0, host.BananaSpawnPoints.Count);
            // HashSet sÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â½ tÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â± loÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡i bÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â giÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ trÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¹ trÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¹ng nÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªn ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£m bÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£o sÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œ lÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â°ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£ng phÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â§n tÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­ ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âºng yÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªu cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â§u.
            selectedIndices.Add(index);
        }

        // SÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¯p xÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¿p lÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡i cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡c chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â° sÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œ theo thÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â© tÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â± tÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ng dÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â§n giÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âºp viÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¡c sync ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¢n ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¹nh hÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡n.
        var orderedIndices = selectedIndices.OrderBy(i => i).ToList();

        // BÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­t/tÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¯t cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡c GameObject Banana tÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â°ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ng ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©ng vÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Âºi cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡c chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â° sÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œ ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£ chÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Ân.
        host.SetBananaPeelsActiveByIndices(orderedIndices);

        // NÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¿u cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ manager thÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã¢â‚¬Å“ng bÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ trÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ng thÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡i cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡c ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¹Ã…â€œiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢m spawn sang cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡c client khÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡c.
        if (manager != null)
            manager.SyncBananaSpawnActivation(orderedIndices);
    }

    private IEnumerator SpawnPlayerBalls(NetworkRunner runner, GameSessionNetWork_Host host, NetworkObjectManager manager, Action<bool, string> onCompleted)
    {
        string message = "";
        if (ballPlayerPrefab == null)
        {
              message = "BallPlayerPrefab is not assigned.";
            Log.Error(message);
            onCompleted?.Invoke(false, message);
            yield break;
        }

        var turnOrderSnapshot = manager.GetOrderedPlayerInfos()
            .Select(p => new TurnOrderEntry(p.playerId, p.turnOrder))
            .ToList();
        List<int> ids = turnOrderSnapshot.Select(t => t.playerId).ToList();

        List<PlayerBallPhysics> physicsData = null;
        if (ids.Count > 0)
        {
            yield return StartCoroutine(APIManager.Instance.RunTask(
                APIManager.Instance.GetBallPhysicsAsync(ids),
                r => physicsData = r));
        }

        if (physicsData == null)
            physicsData = new List<PlayerBallPhysics>();

        // Fallback: thÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªm default ball physics cho bot nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¿u API khÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â´ng trÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚ÂºÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£ vÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â dÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¯ liÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¡u
        var botCtrl = BotPlayerController.Instance;
        if (botCtrl != null)
        {
            foreach (var id in ids)
            {
                if (botCtrl.IsBotPlayer(id))
                {
                    var existing = physicsData.FirstOrDefault(p => p.playerId == id);
                    if (existing == null || existing.physics == null || existing.physics.Count == 0)
                    {
                        if (existing != null) physicsData.Remove(existing);
                        physicsData.Add(BotPlayerController.CreateDefaultBotBallPhysics(id));
                        Debug.Log($"[BOT] Using default ball physics for bot {id}.");
                    }
                }
            }
        }

        if (physicsData.Count == 0)
        {
              message = "Could not load player culi data.";
            Log.Error(message);
            onCompleted?.Invoke(false, message);
            yield break;
        }

        List<BallPhysicsStruct> allBallPhysics = new List<BallPhysicsStruct>();
        foreach (var info in turnOrderSnapshot)
        {
            var pdata = physicsData.FirstOrDefault(p => p.playerId == info.playerId);
            if (pdata != null)
            {
                foreach (var item in pdata.physics)
                {
                    var bp = new BallPhysicsStruct
                    {
                        playerId = info.playerId,
                        name = item.name,
                        skillGenCode = item?.activeSkill?.GenCode ?? 0,
                        Mass = item?.Mass ?? 0f,
                        GravityScale = item?.GravityScale ?? 0f,
                        Drag = item?.Drag ?? 0f,
                        Bounciness = item?.Bounciness ?? 0f,
                        Elasticity = item?.Elasticity ?? 0f,
                        ImpactResistance = item?.ImpactResistance ?? 0f
                    };
                    allBallPhysics.Add(bp);
                }
            }
        }

        if (manager.HasStateAuthority)
        {
            manager.CacheBallPhysicsData(allBallPhysics);
        }

        var quickMatchServer = ResolveQuickMatchServer(runner);
        var authorityMap = BuildPlayerAuthorityMap(quickMatchServer, ids);

        if (host.InactiveBallContainer == null)
        {
            var containerGO = new GameObject("InactiveBallContainer");
            host.InactiveBallContainer = containerGO.transform;
        }

        if (manager != null && host.InactiveBallContainer != null)
        {
            manager.SetInactiveBallContainer(host.InactiveBallContainer);
        }

        int spawnedSuccess = 0;
        bool spawnFailed = false;
        string failureMessage = string.Empty;
        foreach (var item in turnOrderSnapshot)
        {
            var playerGO = manager.GetPlayerObject(item.playerId);
            if (playerGO == null)
            {
                Debug.LogError($"playerGO not found for ID {item.playerId}.");
                continue;
            }

            var handler = playerGO.GetComponent<PlayerNetworkHandler>();
            var playerData = handler != null ? handler.PlayerModel : default;
            var items = physicsData.FirstOrDefault(p => p.playerId == playerData.playerId)?.physics ?? new List<BallPhysicsItem>();
            int count = Mathf.Min(items.Count, 3);
            for (int idx = 0; idx < count; idx++)
            {
                Vector3 spawnPos = host.SpawnBallPoint != null ? host.SpawnBallPoint.position : Vector3.zero;
                authorityMap.TryGetValue(item.playerId, out var playerAuthority);
                var spawnedBall = SpawnWithServerAuthority(runner, ballPlayerPrefab, spawnPos, Quaternion.identity, playerAuthority);
                if (spawnedBall == null)
                {
                    ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_network_false"));
                    failureMessage = "Could not spawn player culi.";
                    spawnFailed = true;
                    break;
                }

                EnsureNetworkObjectInGameScene(runner, spawnedBall);

                var info = items[idx];
                var ballCtrl = spawnedBall.GetComponent<BallServerController>();
                if (ballCtrl != null)
                {
                    ballCtrl.playerId = playerData.playerId;
                    ballCtrl.BallIndex = idx;
                    ballCtrl.BallMaterialId = info.itemId;
                    ballCtrl.BallItemSeq = info.seqItem;
                    ballCtrl.BallLevel = info.level;
                    ballCtrl.HasCateye = info.isCateye;
                    if (info.level >= 10)
                        ballCtrl.RefreshClientLevel10Vfx();
                    ballCtrl.ConfigureDamageColliders(chippedBallColliderPrefab, crackedBallColliderPrefab, shatteredBallColliderPrefab);
                    ballCtrl.RpcApplyPhysics(info.Mass, info.GravityScale, info.Drag, info.Bounciness, info.Elasticity, info.ImpactResistance);
                    var rbNet = ballCtrl.GetComponent<NetworkRigidbody3D>();
                    var rbPhys = rbNet != null ? rbNet.Rigidbody : null;
                    Debug.Log($"[HOST][BallPhysics] pid={playerData.playerId} idx={idx} mass={info.Mass:F2} g={info.GravityScale:F2} drag={info.Drag:F2} bounce={info.Bounciness:F2} elast={info.Elasticity:F2} resist={info.ImpactResistance:F2} rbKinematic={rbPhys?.isKinematic} vel={rbPhys?.linearVelocity}");
                    if (info.damage > 0f)
                        ballCtrl.ApplyInitialDamage(info.damage);

                    float timeout = 3f;
                    float timer = 0f;
                    while (ballCtrl.IsSpawned != 1 && timer < timeout)
                    {
                        yield return null;
                        timer += Time.deltaTime;
                    }

                    if (ballCtrl.IsSpawned != 1)
                    {
                        ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_network_false"));
                        failureMessage = "Culi sync failed.";
                        spawnFailed = true;
                        break;
                    }

                    bool active = idx == 0;
                    ballCtrl.RpcSetActive(active ? 1 : 0);
                    ballCtrl.IsHolding = active ? 1 : 0;
                    if (active && rbPhys != null)
                    {
                        rbPhys.isKinematic = true; // start in hand
                    }
                    if (!active && host.InactiveBallContainer != null)
                        spawnedBall.transform.SetParent(host.InactiveBallContainer);
                    else if (active)
                        spawnedBall.transform.SetParent(null);
                    manager.RegisterPlayerBall(playerData.playerId, spawnedBall, idx, active);
                }

            }

            if (spawnFailed)
                break;

            spawnedSuccess++;
        }

        if (spawnFailed)
        {
            if (string.IsNullOrEmpty(failureMessage))
                failureMessage = "Could not complete culi spawn.";

            onCompleted?.Invoke(false, failureMessage);
            yield break;
        }

        bool success = spawnedSuccess == turnOrderSnapshot.Count;
        message = success ? string.Empty : "Could not spawn culi for every player.";
        onCompleted?.Invoke(success, message);
    }

    private QuickMatchServer ResolveQuickMatchServer(NetworkRunner runner)
    {
        var quickMatchServer = QuickMatchServer.Instance;
        if (quickMatchServer != null)
            return quickMatchServer;

        var servers = FindObjectsOfType<QuickMatchServer>();
        foreach (var server in servers)
        {
            if (server != null && server.Runner == runner)
            {
                return server;
            }
        }

        return null;
    }

    private Dictionary<int, PlayerRef> BuildPlayerAuthorityMap(QuickMatchServer quickMatchServer, IEnumerable<int> playerIds)
    {
        var map = new Dictionary<int, PlayerRef>();

        if (quickMatchServer == null || playerIds == null)
            return map;

        foreach (var playerId in playerIds)
        {
            if (playerId <= 0 || map.ContainsKey(playerId))
                continue;

            if (quickMatchServer.TryGetPlayerRefByUserId(playerId, out var playerRef) && !playerRef.IsNone)
            {
                map[playerId] = playerRef;
            }
            else if (BotPlayerController.Instance == null || !BotPlayerController.Instance.IsBotPlayer(playerId))
            {
                Debug.LogWarning($"PlayerRef not found for user {playerId}.");
            }
        }

        return map;
    }

    public MarbleSpawnData[] GenerateMarbleSpawnData(BoxCollider playArea, int totalAmount)
    {
        MarbleSpawnData[] result = new MarbleSpawnData[Mathf.Max(totalAmount, 0)];
        if (playArea == null)
        {
            Debug.LogError("PlayArea needs a Collider to calculate spawn bounds.");
            return result;
        }

        if (totalAmount <= 0)
            return result;

        var areaBounds = playArea.bounds;
        float spawnHeight = areaBounds.min.y + 0.1f;

        float width = areaBounds.size.x;
        float depth = areaBounds.size.z;

        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(totalAmount));
        float cellSizeX = width / gridSize;
        float cellSizeZ = depth / gridSize;

        List<Vector3> positionsList = new List<Vector3>();
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                if (positionsList.Count >= totalAmount)
                    break;

                float x = areaBounds.min.x + (i + 0.5f) * cellSizeX;
                float z = areaBounds.min.z + (j + 0.5f) * cellSizeZ;

                x = Mathf.Clamp(x, areaBounds.min.x, areaBounds.max.x);
                z = Mathf.Clamp(z, areaBounds.min.z, areaBounds.max.z);

                float offsetX = Random.Range(-cellSizeX * 0.3f, cellSizeX * 0.3f);
                float offsetZ = Random.Range(-cellSizeZ * 0.3f, cellSizeZ * 0.3f);

                positionsList.Add(new Vector3(x + offsetX, spawnHeight, z + offsetZ));
            }
        }

        positionsList = positionsList.OrderBy(p => Random.value).ToList();

        for (int i = 0; i < positionsList.Count; i++)
        {
            var pos = positionsList[i];
            var rot = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            result[i] = new MarbleSpawnData { Position = pos, Rotation = rot };
        }

        return result;
    }

    private void AdjustMarbleSpawnData(MarbleSpawnData[] dataList, BoxCollider playArea, Terrain terrain)
    {
        if (dataList == null || dataList.Length == 0 || playArea == null)
            return;

        var bounds = playArea.bounds;
        float margin = Mathf.Min(bounds.size.x, bounds.size.z) * 0.02f;
        margin = Mathf.Clamp(margin, 0f, 0.5f);

        Vector3 terrainPosition = terrain != null ? terrain.transform.position : Vector3.zero;

        for (int i = 0; i < dataList.Length; i++)
        {
            var data = dataList[i];
            var position = data.Position;

            position.x = Mathf.Clamp(position.x, bounds.min.x + margin, bounds.max.x - margin);
            position.z = Mathf.Clamp(position.z, bounds.min.z + margin, bounds.max.z - margin);

            float groundY = bounds.min.y;
            if (terrain != null)
            {
                float sampled = terrain.SampleHeight(position);
                if (!float.IsNaN(sampled))
                {
                    sampled = Mathf.Clamp(sampled + terrainPosition.y, bounds.min.y, bounds.max.y);
                    groundY = Mathf.Max(groundY, sampled);
                }
            }

            groundY = Mathf.Clamp(groundY, bounds.min.y, bounds.max.y);
            position.y = Mathf.Min(bounds.max.y, groundY + 0.05f);

            data.Position = position;
            dataList[i] = data;
        }
    }

    private bool SpawnMarbles(NetworkRunner runner, GameSessionNetWork_Host host, NetworkObjectManager manager, MarbleSpawnData[] spawnDataList)
    {
        if (ringBallPrefab == null)
        {
            Log.Error("RingBallPrefab is not assigned.");
            return false;
        }

        try
        {
            manager.ringBalls.Clear();
            for (int idx = 0; idx < spawnDataList.Length; idx++)
            {
                var data = spawnDataList[idx];
                var marble = SpawnWithServerAuthority(runner, ringBallPrefab, data.Position, data.Rotation, runner.LocalPlayer);
                if (marble != null)
                {
                    EnsureNetworkObjectInGameScene(runner, marble);
                    TryDisablePhysics(marble);
                    int materialIndex = -1;
                   // if (host.materialCateyes != null && host.materialCateyes.Count > 0)
                     //   materialIndex = Random.Range(0, host.materialCateyes.Count);

                    var ringHandler = marble.GetComponent<RingBallNetworkHandler>();
                    //if (ringHandler != null)
                    //    ringHandler.MaterialIndex = materialIndex;

                    manager.ringBalls.Add(marble, host.playArea);
                }
            }

            manager.RpcNotifyRingBallCollectionChanged();

            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error: " + ex.Message);
            return false;
        }
    }
    private void MoveRingBallsIntoPlayArea(GameSessionNetWork_Host host, NetworkObjectManager manager)
    {
        if (host == null || manager == null)
            return;

        var playArea = host.playArea;
        if (playArea == null)
            return;

        Bounds bounds = playArea.bounds;
        Terrain terrain = host.TerrainGround;

        const float margin = 0.05f;

        foreach (var ringBall in manager.ringBalls)
        {
            if (ringBall == null)
                continue;

            if (!ringBall.HasStateAuthority)
                continue;

            Transform transform = ringBall.transform;
            Vector3 position = transform.position;

            position.x = Mathf.Clamp(position.x, bounds.min.x + margin, bounds.max.x - margin);
            position.z = Mathf.Clamp(position.z, bounds.min.z + margin, bounds.max.z - margin);

            float groundY = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);

            if (terrain != null)
            {
                float sampledHeight = terrain.SampleHeight(position) + terrain.transform.position.y;
                if (!float.IsNaN(sampledHeight))
                {
                    groundY = Mathf.Clamp(sampledHeight + margin, bounds.min.y, bounds.max.y);
                }
            }

            position.y = groundY;
            transform.position = position;

            TryEnablePhysics(ringBall);
        }
    }

    private bool PlaceOnGround(NetworkObject networkObject, Terrain terrain, float offsetY = 0.1f)
    {
        if (networkObject == null)
            return false;

        if (!networkObject.HasStateAuthority)
        {
            Debug.LogWarning("No authority to update this network object position.");
            return false;
        }

        if (terrain == null)
        {
            Debug.LogWarning("No valid terrain found.");
            return false;
        }

        Transform objTransform = networkObject.transform;
        Vector3 origin = objTransform.position;
        float terrainHeight = terrain.SampleHeight(origin);

        if (terrainHeight > 0)
        {
            terrainHeight = Mathf.Max(terrainHeight, 0);
            if (origin.y > terrainHeight)
            {
                Vector3 newPos = new Vector3(origin.x, terrainHeight + offsetY, origin.z);
                objTransform.position = newPos;
                Debug.Log("Object moved to the ground surface.");
                return true;
            }
            else
            {
                Debug.Log("Object is already on the ground surface.");
            }
        }
        else
        {
            Debug.LogWarning("No ground found below the object.");
        }

        return false;
    }

    private IEnumerator LoadMapGameRoutine(NetworkRunner runner, NetworkObjectManager manager)
    {
        if (_isLoadingMap)
        {
            Debug.LogWarning("[HOST] A map load is already running. Ignoring duplicate request.");
            yield break;
        }

        _isLoadingMap = true;
        manager.IsServerSceneLoadCompleted = false;
        _hasLoadedGameScene = false;
        _loadedGameScene = default;

        if (manager.HasStateAuthority && manager.StatusLoading != StatusLoadingGame.LoadMapGame)
        {
            manager.StatusLoading = StatusLoadingGame.LoadMapGame;
        }

        if (GameOnlineUI != null)
        {
            GameOnlineUI.SetActive(false);
        }

        if (!EnsureSceneManagerReady(runner))
        {
            Debug.LogError("[HOST] Cannot load map because NetworkSceneManager is destroyed or not ready.");
            _isLoadingMap = false;
            yield break;
        }

        var activeSceneManager = runner.SceneManager;
        var managerTypeName = activeSceneManager != null ? activeSceneManager.GetType().Name : "null";
        Debug.Log($"[HOST] Runner.SceneManager: {managerTypeName} (UnityNull={IsUnityNull(activeSceneManager)}). Starting map load...");

        var sceneName = manager.rpgRoomModel.gameScene.ToString();
        var loadOperation = runner.LoadScene(sceneName, LoadSceneMode.Additive, LocalPhysicsMode.None, setActiveOnLoad: true);
        if (!loadOperation.IsValid)
        {
            Debug.LogError("[HOST] LoadScene failed for the scene build index.");
            _isLoadingMap = false;
            yield break;
        }

        while (!loadOperation.IsDone)
        {
            yield return null;
        }

        yield return WaitForSceneManagerIdle(runner);

        if (loadOperation.Error != null)
        {
            Debug.LogError($"[HOST] Scene load failed: {loadOperation.Error.Message}");
        }
        else
        {
            Debug.Log("[HOST] Game map loaded successfully.");
            manager.IsServerSceneLoadCompleted = true;
 
            var loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid())
            {
                SceneManager.SetActiveScene(loadedScene);
                _loadedGameScene = loadedScene;
                _hasLoadedGameScene = true;

                HideSceneByName("Menu");

                if (manager != null)
                {
                    manager.RequestClientSceneActivation(sceneName);
                }

                ApplySharedSceneConfigs(sceneName);
            }
            else
            {
                _hasLoadedGameScene = false;
                Debug.LogWarning($"[HOST] Could not find scene '{sceneName}' after loading.");
            }
        }

        _isLoadingMap = false;
    }

    private void EnsureNetworkObjectInGameScene(NetworkRunner runner, NetworkObject networkObject)
    {
        if (networkObject == null)
            return;

        if (runner != null)
        {
            runner.MoveToRunnerScene(networkObject.gameObject);

            if (runner.IsServer)
            {
                TryEnablePhysics(networkObject);
            }
        }

        if (!_hasLoadedGameScene || !_loadedGameScene.IsValid())
            return;

        var targetScene = _loadedGameScene;
        var currentScene = networkObject.gameObject.scene;
        if (currentScene == targetScene)
            return;

        SceneManager.MoveGameObjectToScene(networkObject.gameObject, targetScene);
    }

 
    private NetworkObject SpawnWithServerAuthority(NetworkRunner runner, NetworkObject prefab, Vector3 position, Quaternion rotation, PlayerRef requestedAuthority)
    {
        if (runner == null || prefab == null)
            return null;

        EnsureRunnerPhysicsSupport(runner);

        PlayerRef finalAuthority = requestedAuthority;

        if (runner.IsServer)
        {
            if (finalAuthority.IsNone)
            {
                finalAuthority = runner.LocalPlayer;

                if (finalAuthority.IsNone)
                {
                    finalAuthority = PlayerRef.FromIndex(0);
                }
            }
        }

        // GÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡n PlayerRef hÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£p lÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¡ vÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â o lÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¡nh Spawn.
        var spawned = runner.Spawn(prefab, position, rotation, finalAuthority);  

        if (runner != null && runner.IsServer && spawned != null)
        {
            TryEnablePhysics(spawned);
        }

        return spawned;
    }

    private void EnsureRunnerPhysicsSupport(NetworkRunner runner)
    {
        if (runner == null)
            return;

        var runnerObject = runner.gameObject;
        if (runnerObject == null)
            return;

        //Debug.Log("KiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â»ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢m tra AutoSimulation: " + Physics.autoSimulation);

        var runnerPhysics = runnerObject.GetComponent<RunnerSimulatePhysics3D>();
        if (runnerPhysics == null)
        {
            runnerPhysics = runnerObject.AddComponent<RunnerSimulatePhysics3D>();
            Debug.Log("[HOST] Added RunnerSimulatePhysics3D for NetworkRigidbody support.");
        }

        EnsureClientPhysicsSimulation(runnerPhysics);
    }

    private void TryDisablePhysics(NetworkObject networkObject)
    {
        if (networkObject == null)
            return;

        if (networkObject.TryGetComponent<NetworkRigidbody3D>(out var networkRigidbody))
        {
            DisablePhysics(networkRigidbody.Rigidbody);
            return;
        }

        if (networkObject.TryGetComponent<Rigidbody>(out var rigidbody))
        {
            DisablePhysics(rigidbody);
        }
    }

    private void TryEnablePhysics(NetworkObject networkObject)
    {
        if (networkObject == null)
            return;

        if (networkObject.TryGetComponent<NetworkRigidbody3D>(out var networkRigidbody))
        {
            ApplyGravity(networkRigidbody.Rigidbody);
            return;
        }

        if (networkObject.TryGetComponent<Rigidbody>(out var rigidbody))
        {
            ApplyGravity(rigidbody);
        }
    }

    private void ApplyHeavyWeightToRandomRingBalls(NetworkObjectManager manager, int selectionCount, float massMultiplier)
    {
        if (manager == null || manager.ringBalls == null || manager.ringBalls.Count == 0)
            return;

        var candidates = manager.ringBalls.Where(ball => ball != null).ToList();
        if (candidates.Count == 0 || selectionCount <= 0 || massMultiplier <= 0f)
            return;

        int pickCount = Mathf.Min(selectionCount, candidates.Count);
        for (int i = 0; i < pickCount; i++)
        {
            int randomIndex = Random.Range(0, candidates.Count);
            var selected = candidates[randomIndex];
            candidates.RemoveAt(randomIndex);

            Rigidbody targetRigidbody = null;
            if (selected.TryGetComponent<NetworkRigidbody3D>(out var networkRigidbody) && networkRigidbody.Rigidbody != null)
            {
                targetRigidbody = networkRigidbody.Rigidbody;
            }
            else
            {
                selected.TryGetComponent(out targetRigidbody);
            }

            if (targetRigidbody != null)
            {
                targetRigidbody.mass *= massMultiplier;
            }
        }
    }

    private static void EnsureClientPhysicsSimulation(RunnerSimulatePhysics3D runnerPhysics)
    {
        if (runnerPhysics == null)
            return;

        if (runnerPhysics.ClientPhysicsSimulation == ClientPhysicsSimulation.Disabled)
        {
            runnerPhysics.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateForward;
            Debug.Log("[HOST] Enabled RunnerSimulatePhysics3D client physics simulation: SimulateForward.");
        }
    }

    private static void ApplyGravity(Rigidbody rigidbody)
    {
        if (rigidbody == null)
            return;

        rigidbody.isKinematic = false;
        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        rigidbody.useGravity = true;
        rigidbody.WakeUp();
    }

    private static void DisablePhysics(Rigidbody rigidbody)
    {
        if (rigidbody == null)
            return;

        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.Sleep();
    }
 

    internal static void HideSceneByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        var targetScene = SceneManager.GetSceneByName(sceneName);
        if (!targetScene.IsValid())
            return;

        foreach (var rootObject in targetScene.GetRootGameObjects())
        {
            if (rootObject != null && rootObject.activeSelf)
            {
                rootObject.SetActive(false);
            }
        }
    }

    private IEnumerator WaitForSceneManagerIdle(NetworkRunner runner)
    {
        const float timeout = 10f;
        float elapsed = 0f;

        while (true)
        {
            if (runner == null)
                yield break;

            var sceneManager = runner.SceneManager as NetworkSceneManagerDefault;
            if (sceneManager == null || IsUnityNull(sceneManager))
                yield break;

            if (!sceneManager.IsBusy)
                yield break;

            yield return null;
            elapsed += Time.deltaTime;

            if (elapsed >= timeout)
            {
                Debug.LogWarning($"[HOST] SceneManager is still busy after {timeout} seconds. Continuing sync to avoid a hang.");
                yield break;
            }
        }
    }

    private bool EnsureSceneManagerReady(NetworkRunner runner)
    {
        if (runner == null)
        {
            Debug.LogError("Runner is missing; cannot ensure SceneManager.");
            return false;
        }

        var sceneManager = runner.SceneManager;

        if (sceneManager is UnityEngine.Object unityObject && unityObject == null)
        {
            Debug.LogWarning("Runner.SceneManager references a destroyed object. Reassigning it.");
            sceneManager = null;
        }

        NetworkSceneManagerDefault resolvedManager = null;

        if (sceneManager == null)
        {
            resolvedManager = runner.GetComponent<NetworkSceneManagerDefault>();
            if (IsUnityNull(resolvedManager))
            {
                resolvedManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
                Debug.LogWarning("NetworkSceneManagerDefault was destroyed; creating a new component.");
            }

            if (!AssignSceneManagerToRunner(runner, resolvedManager))
            {
                Debug.LogError("Cannot assign a new NetworkSceneManager to the runner.");
                return false;
            }

            sceneManager = runner.SceneManager;
        }
        else if (sceneManager is NetworkSceneManagerDefault defaultManager)
        {
            resolvedManager = defaultManager;
        }

        if (resolvedManager != null)
        {
            EnsureSceneManagerInitialized(runner, resolvedManager);
        }

        if (runner.SceneManager == null || IsUnityNull(runner.SceneManager))
        {
            Debug.LogError("Runner has no valid SceneManager after recovery attempts.");
            return false;
        }

        Debug.Log($"[HOST] Runner.SceneManager is ready: {runner.SceneManager.GetType().Name} (UnityNull={IsUnityNull(runner.SceneManager)}).");
        return true;
    }

    private static bool IsUnityNull(object obj)
    {
        if (obj is UnityEngine.Object unityObject)
        {
            return unityObject == null;
        }

        return obj == null;
    }

    private void EnsureSceneManagerInitialized(NetworkRunner runner, NetworkSceneManagerDefault manager)
    {
        if (runner == null || manager == null)
            return;

        if (manager.Runner == runner)
            return;

        if (manager.Runner != null && manager.Runner != runner)
        {
            Debug.LogWarning("NetworkSceneManagerDefault references another runner. Shutting it down before reinitialization.");
            manager.Shutdown();
        }

        Debug.LogWarning("NetworkSceneManagerDefault is not initialized or lost its runner reference. Reinitializing it.");
        manager.Initialize(runner);
    }

    private bool AssignSceneManagerToRunner(NetworkRunner runner, NetworkSceneManagerDefault sceneManager)
    {
        if (runner == null || IsUnityNull(sceneManager))
            return false;

        try
        {
            var runnerType = typeof(NetworkRunner);
            var property = runnerType.GetProperty("SceneManager", BindingFlags.Instance | BindingFlags.Public);

            if (property != null)
            {
                var setter = property.GetSetMethod(true);
                if (setter != null)
                {
                    setter.Invoke(runner, new object[] { sceneManager });
                    Debug.Log("Assigned new SceneManager through the NetworkRunner internal setter.");
                    return true;
                }
            }

            var field = runnerType.GetField("_sceneManager", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(runner, sceneManager);
                Debug.Log("Assigned new SceneManager through the private _sceneManager field.");
                return true;
            }

            Debug.LogWarning("Could not find a reflection path to assign SceneManager on NetworkRunner.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error while assigning SceneManager to runner: {ex}");
        }

        return false;
    }

    private void ApplySharedSceneConfigs(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("Cannot apply SceneLogicConfig because sceneName is invalid.");
            return;
        }

        var serverLauncher = ServerLauncher.Instance ?? FindObjectOfType<ServerLauncher>();
        if (serverLauncher == null)
        {
            Debug.LogWarning("ServerLauncher not found for SceneLogicConfig lookup.");
            return;
        }

        var configs = serverLauncher.GetSceneConfigsForScene(sceneName);
        if (configs == null || configs.Count == 0)
        {
            Debug.LogWarning($"No matching SceneLogicConfig found for scene '{sceneName}'.");
            return;
        }

        foreach (var config in configs)
        {
            if (config == null)
            {
                continue;
            }

            config.ApplyToScene();
        }
    }
}
public readonly struct GameInitializationReport
{
    public bool Success { get; }
    public GameInitializationFailureReason FailureReason { get; }
    public string Message { get; }
    public bool IsConnectionIssue => FailureReason == GameInitializationFailureReason.ConnectionLost || FailureReason == GameInitializationFailureReason.RunnerNotRunning;

    public GameInitializationReport(bool success, GameInitializationFailureReason failureReason, string? message)
    {
        Success = success;
        FailureReason = failureReason;
        Message = message ?? string.Empty;
    }

    public static GameInitializationReport SuccessReport(string? message = null)
    {
        return new GameInitializationReport(true, GameInitializationFailureReason.None, message ?? string.Empty);
    }

    public static GameInitializationReport Failure(GameInitializationFailureReason reason, string? message = null)
    {
        return new GameInitializationReport(false, reason, message ?? string.Empty);
    }

    public override string ToString()
    {
        return Success
            ? $"Success: {Message}"
            : $"Failure({FailureReason}): {Message}";
    }
}
#endif


