#if UNITY_SERVER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

[Serializable]
public struct PaperLegendCharacterModelSpawnEntry
{
    [Tooltip("Character/model id, currently resolved from PlayerInfoStruct.playerbody.")]
    public int modelId;
    [Tooltip("Fusion NetworkObject prefab for this paper character model.")]
    public NetworkObject characterPrefab;
}

[Serializable]
public struct PaperLegendServerMatchConfig
{
    public PaperLegendCharacterModelSpawnEntry[] CharacterModels;
    public NetworkObject MatchHostPrefab;
    public NetworkObject ExperiencePickupPrefab;
    public Transform[] SpawnPoints;
    public Transform[] RespawnPoints;
    public Transform[] ExperienceSpawnPoints;
    public int MaxPlayers;
    public int MinRealPlayers;
    public bool FillBots;
    public bool EnableBotAi;
    public int KillLimit;
    public float RespawnDelaySeconds;
    public bool EnableBaseObjectiveWin;
    public int ExperiencePickupCount;
    public int ExperiencePerPickup;
    public float ExperiencePickupRespawnSeconds;
    public float FallbackSpawnRadius;
    public float SpawnHeightOffset;
}

public sealed class PaperLegendGameServerInitializer : MonoBehaviour
{
    public GameInitializationReport LastInitializationReport { get; private set; } =
        GameInitializationReport.Failure(GameInitializationFailureReason.Unknown, "PaperLegends initialization has not started.");

    public IEnumerator InitializeGameOnline(
        NetworkRunner runner,
        NetworkObjectManager manager,
        Scene loadedScene,
        PaperLegendServerMatchConfig config)
    {
        SetReport(GameInitializationReport.Failure(GameInitializationFailureReason.Unknown, "Starting PaperLegends server initialization."));
        PaperLegendRuntimeState.SetPaperLegendMatch(true);

        if (runner == null)
        {
            SetReport(GameInitializationReport.Failure(GameInitializationFailureReason.MissingRunner, "Missing NetworkRunner for PaperLegends initialization."));
            yield break;
        }

        if (manager == null)
        {
            SetReport(GameInitializationReport.Failure(GameInitializationFailureReason.MissingManager, "Missing NetworkObjectManager for PaperLegends initialization."));
            yield break;
        }

        if (!HasAnyCharacterModelPrefab(config.CharacterModels))
        {
            SetReport(GameInitializationReport.Failure(GameInitializationFailureReason.SpawnFailed, "PaperLegends server character catalog has no assigned model prefabs."));
            yield break;
        }

        EnsureRunnerPhysicsSupport(runner);

        int maxPlayers = ResolveMaxPlayers(manager, config.MaxPlayers);
        int minRealPlayers = PaperLegendRuntimeState.ResolveMinRealPlayers(config.MinRealPlayers, maxPlayers);
        ApplyRoomSettings(manager, maxPlayers);

        var quickMatchServer = ResolveQuickMatchServer(runner);
        var realPlayerIds = CollectRealPlayerIds(quickMatchServer);
        manager.SetExpectedClientReadyCount(realPlayerIds.Count);

        if (realPlayerIds.Count < minRealPlayers)
        {
            SetReport(GameInitializationReport.Failure(
                GameInitializationFailureReason.DataLoadFailed,
                $"PaperLegends requires at least {minRealPlayers} real player(s), found {realPlayerIds.Count}."));
            yield break;
        }

        List<PlayerInfoStruct> players = null;
        string playerLoadError = string.Empty;
        yield return LoadPlayers(realPlayerIds, result => players = result, error => playerLoadError = error);

        if (players == null || players.Count == 0)
        {
            SetReport(GameInitializationReport.Failure(
                GameInitializationFailureReason.DataLoadFailed,
                string.IsNullOrWhiteSpace(playerLoadError) ? "Failed to load PaperLegends player data." : playerLoadError));
            yield break;
        }

        ApplySelectedCharacterModels(players);

        if (config.FillBots && players.Count < maxPlayers)
        {
            string botError = string.Empty;
            yield return FillBots(players, maxPlayers, error => botError = error);
            if (!string.IsNullOrEmpty(botError))
            {
                SetReport(GameInitializationReport.Failure(GameInitializationFailureReason.DataLoadFailed, botError));
                yield break;
            }
        }

        if (players.Count < maxPlayers)
        {
            SetReport(GameInitializationReport.Failure(
                GameInitializationFailureReason.DataLoadFailed,
                $"PaperLegends could not fill all {maxPlayers} FFA slots."));
            yield break;
        }

        Dictionary<int, PaperLegendHeroData> heroStatsByModelId = null;
        yield return LoadHeroStats(players.Select(ResolveCharacterModelId), result => heroStatsByModelId = result);

        SyncPlayersToNetworkManager(manager, players, maxPlayers);

        var matchHost = ResolveOrSpawnMatchHost(runner, loadedScene, config.MatchHostPrefab);
        if (matchHost == null)
            Debug.LogWarning("[PaperLegends] Match host could not be created. Kill limit and end-game handling will be disabled for this match.");

        if (matchHost != null)
        {
            matchHost.ConfigureFreeForAllRules(
                config.KillLimit > 0 ? config.KillLimit : 15,
                config.EnableBaseObjectiveWin);
            matchHost.ConfigureSpawnPoints(ResolveConfiguredRespawnPoints(config));
        }

        var authorityMap = BuildPlayerAuthorityMap(quickMatchServer, players.Select(p => p.playerId));
        bool spawned = SpawnPaperCharacters(runner, manager, loadedScene, players, maxPlayers, authorityMap, config, heroStatsByModelId);
        if (!spawned)
            yield break;

        SpawnExperiencePickups(runner, loadedScene, config);

        SetReport(GameInitializationReport.SuccessReport("PaperLegends server initialization completed."));
    }

    private void SetReport(GameInitializationReport report)
    {
        LastInitializationReport = report;
    }

    private int ResolveMaxPlayers(NetworkObjectManager manager, int configuredMaxPlayers)
    {
        int maxPlayers = PaperLegendRuntimeState.ResolveMaxPlayers(configuredMaxPlayers);
        int capacity = manager != null ? manager.players.Length : PaperLegendRuntimeState.DefaultFreeForAllPlayers;
        if (capacity > 0)
            maxPlayers = Mathf.Min(maxPlayers, capacity);

        return Mathf.Max(1, maxPlayers);
    }

    private void ApplyRoomSettings(NetworkObjectManager manager, int maxPlayers)
    {
        if (manager == null || !manager.HasStateAuthority)
            return;

        var room = manager.rpgRoomModel;
        room.MaxPlayer = maxPlayers;
        room.betCount = 0;
        manager.rpgRoomModel = room;
    }

    private List<int> CollectRealPlayerIds(QuickMatchServer quickMatchServer)
    {
        var ids = new List<int>();
        if (quickMatchServer == null)
        {
            Debug.LogWarning("[PaperLegends] QuickMatchServer not found; cannot collect assigned real players.");
            return ids;
        }

        quickMatchServer.PruneInactiveRegisteredPlayers();
        bool requireReady = quickMatchServer.IsReadyPhaseEnabled;

        foreach (var state in quickMatchServer.QuickMatchPlayers)
        {
            if (state.PlayerId <= 0)
                continue;

            if (requireReady && state.Status != QuickMatchServer.QuickMatchPlayerStatusCodes.Ready)
                continue;

            if (!requireReady && state.Status == QuickMatchServer.QuickMatchPlayerStatusCodes.Cancelled)
                continue;

            if (!ids.Contains(state.PlayerId))
                ids.Add(state.PlayerId);
        }

        return ids;
    }

    private IEnumerator LoadPlayers(List<int> realPlayerIds, Action<List<PlayerInfoStruct>> onLoaded, Action<string> onError)
    {
        if (APIManager.Instance == null)
        {
            onError?.Invoke("APIManager is not ready for PaperLegends player load.");
            yield break;
        }

        PlayerInfoStruct[] playerList = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetListPlayerGameById(realPlayerIds),
            result => playerList = result));

        if (playerList == null || playerList.Length == 0)
        {
            onError?.Invoke("PaperLegends player API returned no players.");
            yield break;
        }

        var byId = playerList
            .Where(p => p.playerId > 0)
            .GroupBy(p => p.playerId)
            .ToDictionary(g => g.Key, g => g.First());

        var ordered = new List<PlayerInfoStruct>();
        foreach (int id in realPlayerIds)
        {
            if (byId.TryGetValue(id, out var info))
                ordered.Add(info);
        }

        onLoaded?.Invoke(ordered);
    }

    private IEnumerator FillBots(List<PlayerInfoStruct> players, int maxPlayers, Action<string> onError)
    {
        int botsNeeded = maxPlayers - players.Count;
        if (botsNeeded <= 0)
            yield break;

        EnsureBotController();

        List<BotPlayerData> botList = new List<BotPlayerData>();
        if (APIManager.Instance != null)
        {
            yield return StartCoroutine(APIManager.Instance.RunTask(
                APIManager.Instance.GetBotPlayersAsync(botsNeeded),
                result => botList = result ?? new List<BotPlayerData>()));
        }
        else
        {
            Debug.LogWarning("[PaperLegends][BOT] APIManager is not ready; using local fallback bot profiles.");
        }

        if (botList.Count < botsNeeded)
            Debug.LogWarning($"[PaperLegends][BOT] Bot API returned {botList.Count}/{botsNeeded}; filling the rest with local PaperLegends bot profiles.");

        var realPlayers = players.Where(info => info.playerId > 0).ToList();
        for (int i = 0; i < botsNeeded; i++)
        {
            BotPlayerData botData = i < botList.Count && botList[i] != null
                ? botList[i]
                : CreateLocalPaperLegendBotData(players, i);

            if (botData.id == 0 || players.Any(info => info.playerId == botData.id))
                botData = CreateLocalPaperLegendBotData(players, i);

            botData.Level = GetRandomBotLevel(realPlayers);
            var botInfo = BotPlayerController.CreateBotPlayerInfo(botData);
            if (PaperLegendRuntimeState.TryDequeueReservedBotCharacterModel(out int botCharacterModelId))
                botInfo.playerbody = (PlayerBodyType)botCharacterModelId;
            BotPlayerController.Instance?.RegisterBot(botInfo.playerId);
            players.Add(botInfo);
            Debug.Log($"[PaperLegends][BOT] Added bot '{botInfo.fullname}' (ID={botInfo.playerId}, modelId={(int)botInfo.playerbody}).");
        }
    }

    private BotPlayerData CreateLocalPaperLegendBotData(IReadOnlyList<PlayerInfoStruct> existingPlayers, int botIndex)
    {
        int botId = -100000 - botIndex;
        while (existingPlayers != null && existingPlayers.Any(player => player.playerId == botId))
            botId--;

        return new BotPlayerData
        {
            id = botId,
            PlayerName = $"Paper Bot {botIndex + 1}",
            Level = 1,
            RingBall = 0,
            AvatarUrl = string.Empty,
            ProviderType = "BOT",
            IdAccount = $"paper_legend_bot_{Mathf.Abs(botId)}",
            friendCode = $"PLBOT{Mathf.Abs(botId)}"
        };
    }

    private void ApplySelectedCharacterModels(List<PlayerInfoStruct> players)
    {
        if (players == null)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            var playerInfo = players[i];
            if (!PaperLegendRuntimeState.TryGetSelectedCharacterModel(playerInfo.playerId, out int selectedModelId))
                continue;

            playerInfo.playerbody = (PlayerBodyType)selectedModelId;
            players[i] = playerInfo;
            Debug.Log($"[PaperLegends][CharacterSelect] Player {playerInfo.playerId} selected modelId={selectedModelId}.");
        }
    }

    private void SyncPlayersToNetworkManager(NetworkObjectManager manager, List<PlayerInfoStruct> players, int maxPlayers)
    {
        var playersArray = manager.players;
        for (int i = 0; i < playersArray.Length; i++)
            playersArray.Set(i, default);

        int count = Mathf.Min(maxPlayers, Mathf.Min(players.Count, playersArray.Length));
        for (int i = 0; i < count; i++)
        {
            var info = players[i];
            info.turnOrder = i;
            info.statusPlayer = StatusPlayer.Normal;
            info.isDestroy = false;
            playersArray.Set(i, info);
        }
    }

    private PaperLegendMatchNetworkHost ResolveOrSpawnMatchHost(NetworkRunner runner, Scene loadedScene, NetworkObject matchHostPrefab)
    {
        if (PaperLegendMatchNetworkHost.Instance != null)
            return PaperLegendMatchNetworkHost.Instance;

        if (matchHostPrefab == null)
        {
            Debug.LogWarning("[PaperLegends] MatchHostPrefab is not assigned. Creating a server-only local match host fallback.");

            var fallbackObject = new GameObject("PaperLegendMatchHost_LocalFallback");
            if (loadedScene.IsValid())
                SceneManager.MoveGameObjectToScene(fallbackObject, loadedScene);

            fallbackObject.AddComponent<NetworkObject>();
            var fallbackHost = fallbackObject.AddComponent<PaperLegendMatchNetworkHost>();
            fallbackHost.InitializeLocalFallback();
            return fallbackHost;
        }

        var spawned = SpawnWithServerAuthority(runner, matchHostPrefab, Vector3.zero, Quaternion.identity, PlayerRef.None);
        if (spawned == null)
            return null;

        MoveNetworkObjectToGameScene(runner, spawned, loadedScene);
        return spawned.GetComponent<PaperLegendMatchNetworkHost>();
    }

    private bool SpawnPaperCharacters(
        NetworkRunner runner,
        NetworkObjectManager manager,
        Scene loadedScene,
        List<PlayerInfoStruct> players,
        int maxPlayers,
        Dictionary<int, PlayerRef> authorityMap,
        PaperLegendServerMatchConfig config,
        Dictionary<int, PaperLegendHeroData> heroStatsByModelId)
    {
        var assignedSpawnPoints = BuildRandomPointOrder(config.SpawnPoints, maxPlayers);
        var assignedRespawnPoints = BuildRandomPointOrder(ResolveConfiguredRespawnPoints(config), maxPlayers);

        for (int i = 0; i < maxPlayers; i++)
        {
            var playerInfo = players[i];
            ResolveSpawnPose(i, maxPlayers, config, assignedSpawnPoints, out Vector3 spawnPosition, out Quaternion spawnRotation);
            ResolveRespawnPose(i, maxPlayers, config, assignedRespawnPoints, assignedSpawnPoints, out Vector3 respawnPosition, out Quaternion respawnRotation);
            NetworkObject characterPrefab = ResolveCharacterPrefab(playerInfo, config, out int characterModelId);
            if (characterPrefab == null)
            {
                SetReport(GameInitializationReport.Failure(
                    GameInitializationFailureReason.SpawnFailed,
                    $"PaperLegends server character prefab is missing for modelId={characterModelId}. Configure this modelId in PaperLegendCharacterServerCatalog."));
                return false;
            }

            bool isBotPlayer = IsBotPlayer(playerInfo.playerId);
            PlayerRef authority = PlayerRef.None;
            if (!isBotPlayer)
            {
                if (!authorityMap.TryGetValue(playerInfo.playerId, out authority) || authority.IsNone)
                {
                    SetReport(GameInitializationReport.Failure(
                        GameInitializationFailureReason.SpawnFailed,
                        $"PaperLegends could not resolve Fusion input authority for real playerId={playerInfo.playerId}. The character would not accept client input."));
                    return false;
                }
            }

            Debug.Log($"[PaperLegends][Spawn] Spawning character slot={i + 1}, playerId={playerInfo.playerId}, bot={isBotPlayer}, modelId={characterModelId}, inputAuthority={authority}.");
            var spawned = SpawnWithServerAuthority(runner, characterPrefab, spawnPosition, spawnRotation, authority);
            if (spawned == null)
            {
                SetReport(GameInitializationReport.Failure(GameInitializationFailureReason.SpawnFailed, $"Failed to spawn PaperLegends character for player {playerInfo.playerId} with modelId={characterModelId}."));
                return false;
            }

            MoveNetworkObjectToGameScene(runner, spawned, loadedScene);
            spawned.name = $"PaperLegend_Player_{i + 1}_{playerInfo.playerId}";
            if (!authority.IsNone)
                runner.SetPlayerObject(authority, spawned);

            Debug.Log($"[PaperLegends][Spawn] Spawned '{spawned.name}' with inputAuthority={spawned.InputAuthority}, stateAuthority={spawned.StateAuthority}.");

            var handler = spawned.GetComponent<PaperLegendCharacterNetworkHandler>();
            if (handler == null)
            {
                SetReport(GameInitializationReport.Failure(GameInitializationFailureReason.SpawnFailed, "PaperLegends character prefab is missing PaperLegendCharacterNetworkHandler."));
                return false;
            }

            handler.ConfigureIdentity(playerInfo.playerId, PaperLegendTeam.None, i + 1, characterModelId);
            if (TryGetHeroStats(heroStatsByModelId, characterModelId, out PaperLegendHeroData heroStats))
            {
                handler.ConfigureCombatStats(heroStats.hp, heroStats.attack, heroStats.speed);
                handler.ConfigureHeroSkillStats(heroStats);
                handler.ConfigurePaperPhysicsStats(heroStats.weight, heroStats.bounce, heroStats.friction, heroStats.flickForce);
            }

            handler.ConfigureRespawnPoint(
                respawnPosition,
                respawnRotation,
                config.RespawnDelaySeconds > 0f ? config.RespawnDelaySeconds : 5f);
            manager.RegisterPlayerObject(playerInfo.playerId, spawned);

            if (config.EnableBotAi && isBotPlayer)
                PaperLegendBotFlickController.Ensure().RegisterBotCharacter(handler);
        }

        return true;
    }

    private NetworkObject ResolveCharacterPrefab(PlayerInfoStruct playerInfo, PaperLegendServerMatchConfig config, out int characterModelId)
    {
        characterModelId = ResolveCharacterModelId(playerInfo);

        if (config.CharacterModels != null)
        {
            for (int i = 0; i < config.CharacterModels.Length; i++)
            {
                var entry = config.CharacterModels[i];
                if (entry.modelId == characterModelId && entry.characterPrefab != null)
                    return entry.characterPrefab;
            }
        }

        return null;
    }

    private static int ResolveCharacterModelId(PlayerInfoStruct playerInfo)
    {
        return Mathf.Max(0, (int)playerInfo.playerbody);
    }

    private static bool HasAnyCharacterModelPrefab(PaperLegendCharacterModelSpawnEntry[] entries)
    {
        return ResolveFirstCharacterModelPrefab(entries) != null;
    }

    private static NetworkObject ResolveFirstCharacterModelPrefab(PaperLegendCharacterModelSpawnEntry[] entries)
    {
        if (entries == null)
            return null;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].characterPrefab != null)
                return entries[i].characterPrefab;
        }

        return null;
    }

    private void SpawnExperiencePickups(NetworkRunner runner, Scene loadedScene, PaperLegendServerMatchConfig config)
    {
        int count = Mathf.Max(0, config.ExperiencePickupCount);
        if (count == 0)
            return;

        if (config.ExperiencePickupPrefab == null)
        {
            Debug.LogWarning("[PaperLegends][XP] Experience pickup prefab is not assigned; map pickups will not spawn.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            ResolveExperiencePickupPose(i, count, config, out Vector3 position, out Quaternion rotation);

            var spawned = runner.Spawn(config.ExperiencePickupPrefab, position, rotation, PlayerRef.None);
            if (spawned == null)
            {
                Debug.LogWarning($"[PaperLegends][XP] Failed to spawn experience pickup #{i + 1}.");
                continue;
            }

            MoveNetworkObjectToGameScene(runner, spawned, loadedScene);
            spawned.name = $"PaperLegend_ExperiencePickup_{i + 1}";

            var pickup = spawned.GetComponent<PaperLegendExperiencePickup>();
            if (pickup != null)
            {
                pickup.Configure(
                    Mathf.Max(1, config.ExperiencePerPickup),
                    Mathf.Max(0f, config.ExperiencePickupRespawnSeconds));
            }
            else
            {
                Debug.LogWarning("[PaperLegends][XP] Experience pickup prefab is missing PaperLegendExperiencePickup.");
            }
        }
    }

    private void ResolveSpawnPose(
        int slotIndex,
        int maxPlayers,
        PaperLegendServerMatchConfig config,
        Transform[] assignedSpawnPoints,
        out Vector3 position,
        out Quaternion rotation)
    {
        if (assignedSpawnPoints != null && slotIndex < assignedSpawnPoints.Length && assignedSpawnPoints[slotIndex] != null)
        {
            var point = assignedSpawnPoints[slotIndex];
            position = point.position;
            rotation = SanitizeRotation(point.rotation);
            return;
        }

        Vector3 center = Vector3.zero;
        float radius = config.FallbackSpawnRadius > 0f ? config.FallbackSpawnRadius : 2.5f;

        var legacyHost = GameSessionNetWork_Host.Instance;
        if (legacyHost != null && legacyHost.playArea != null)
        {
            Bounds bounds = legacyHost.playArea.bounds;
            center = bounds.center;
            radius = Mathf.Max(0.75f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.65f);
        }

        float angle = Mathf.PI * 2f * slotIndex / Mathf.Max(1, maxPlayers);
        Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        position = center + direction * radius;
        position.y += config.SpawnHeightOffset > 0f ? config.SpawnHeightOffset : 0.35f;
        rotation = SanitizeRotation(Quaternion.LookRotation(direction.sqrMagnitude > 0f ? -direction : Vector3.forward, Vector3.up));
    }

    private void ResolveRespawnPose(
        int slotIndex,
        int maxPlayers,
        PaperLegendServerMatchConfig config,
        Transform[] assignedRespawnPoints,
        Transform[] assignedSpawnPoints,
        out Vector3 position,
        out Quaternion rotation)
    {
        if (assignedRespawnPoints != null
            && slotIndex < assignedRespawnPoints.Length
            && assignedRespawnPoints[slotIndex] != null)
        {
            var point = assignedRespawnPoints[slotIndex];
            position = point.position;
            rotation = SanitizeRotation(point.rotation);
            return;
        }

        ResolveSpawnPose(slotIndex, maxPlayers, config, assignedSpawnPoints, out position, out rotation);
    }

    private static Transform[] ResolveConfiguredRespawnPoints(PaperLegendServerMatchConfig config)
    {
        if (HasAnyTransform(config.RespawnPoints))
            return config.RespawnPoints;

        return config.SpawnPoints;
    }

    private static Transform[] BuildRandomPointOrder(Transform[] points, int count)
    {
        count = Mathf.Max(0, count);
        if (count == 0 || !HasAnyTransform(points))
            return Array.Empty<Transform>();

        var validPoints = points
            .Where(point => point != null)
            .Distinct()
            .OrderBy(_ => Random.value)
            .ToList();

        if (validPoints.Count == 0)
            return Array.Empty<Transform>();

        var assigned = new Transform[count];
        for (int i = 0; i < count; i++)
            assigned[i] = validPoints[i % validPoints.Count];

        return assigned;
    }

    private static bool HasAnyTransform(Transform[] points)
    {
        return points != null && points.Any(point => point != null);
    }

    private void ResolveExperiencePickupPose(
        int index,
        int count,
        PaperLegendServerMatchConfig config,
        out Vector3 position,
        out Quaternion rotation)
    {
        if (config.ExperienceSpawnPoints != null
            && index < config.ExperienceSpawnPoints.Length
            && config.ExperienceSpawnPoints[index] != null)
        {
            var point = config.ExperienceSpawnPoints[index];
            position = point.position;
            rotation = SanitizeRotation(point.rotation);
            return;
        }

        Vector3 center = Vector3.zero;
        float radius = config.FallbackSpawnRadius > 0f ? config.FallbackSpawnRadius * 0.55f : 1.5f;

        var legacyHost = GameSessionNetWork_Host.Instance;
        if (legacyHost != null && legacyHost.playArea != null)
        {
            Bounds bounds = legacyHost.playArea.bounds;
            center = bounds.center;
            radius = Mathf.Max(0.75f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.35f);
        }

        float angle = Mathf.PI * 2f * index / Mathf.Max(1, count);
        Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        position = center + direction * radius;
        position.y += config.SpawnHeightOffset > 0f ? config.SpawnHeightOffset : 0.35f;
        rotation = Quaternion.identity;
    }

    private Dictionary<int, PlayerRef> BuildPlayerAuthorityMap(QuickMatchServer quickMatchServer, IEnumerable<int> playerIds)
    {
        var map = new Dictionary<int, PlayerRef>();
        if (quickMatchServer == null || playerIds == null)
            return map;

        foreach (int playerId in playerIds)
        {
            if (playerId <= 0 || map.ContainsKey(playerId))
                continue;

            if (quickMatchServer.TryGetPlayerRefByUserId(playerId, out var playerRef) && !playerRef.IsNone)
            {
                map[playerId] = playerRef;
                Debug.Log($"[PaperLegends][Authority] Resolved playerId={playerId} -> playerRef={playerRef}.");
            }
            else
            {
                Debug.LogWarning($"[PaperLegends][Authority] Could not resolve playerRef for real playerId={playerId}.");
            }
        }

        return map;
    }

    private IEnumerator LoadHeroStats(IEnumerable<int> modelIds, Action<Dictionary<int, PaperLegendHeroData>> onComplete)
    {
        var result = new Dictionary<int, PaperLegendHeroData>();
        var distinctModelIds = modelIds != null
            ? modelIds.Where(modelId => modelId > 0).Distinct().ToList()
            : new List<int>();

        if (distinctModelIds.Count == 0 || APIManager.Instance == null)
        {
            if (APIManager.Instance == null)
                Debug.LogWarning("[PaperLegends][HeroStats] APIManager is not ready; using character prefab combat defaults.");

            onComplete?.Invoke(result);
            yield break;
        }

        var loadTask = APIManager.Instance.GetPaperLegendHeroesByModelIdsAsync(distinctModelIds);
        const float timeoutSeconds = 6f;
        float elapsed = 0f;
        while (!loadTask.IsCompleted && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!loadTask.IsCompleted)
        {
            Debug.LogWarning("[PaperLegends][HeroStats] Hero API timed out; using character prefab combat defaults.");
            onComplete?.Invoke(result);
            yield break;
        }

        if (loadTask.IsFaulted)
        {
            Debug.LogWarning($"[PaperLegends][HeroStats] Hero API failed: {loadTask.Exception?.GetBaseException().Message}");
            onComplete?.Invoke(result);
            yield break;
        }

        PaperLegendHeroListResponse response = loadTask.Result;
        if (response?.heroes != null)
        {
            foreach (var hero in response.heroes)
            {
                if (hero == null)
                    continue;

                int modelId = hero.ResolveModelIdInt();
                if (modelId > 0 && !result.ContainsKey(modelId))
                    result.Add(modelId, hero);
            }
        }

        Debug.Log($"[PaperLegends][HeroStats] Loaded combat stats for {result.Count}/{distinctModelIds.Count} selected hero model(s).");
        onComplete?.Invoke(result);
    }

    private static bool TryGetHeroStats(Dictionary<int, PaperLegendHeroData> statsByModelId, int modelId, out PaperLegendHeroData stats)
    {
        stats = null;
        return statsByModelId != null && modelId > 0 && statsByModelId.TryGetValue(modelId, out stats) && stats != null;
    }

    private QuickMatchServer ResolveQuickMatchServer(NetworkRunner runner)
    {
        if (QuickMatchServer.Instance != null)
            return QuickMatchServer.Instance;

        var servers = FindObjectsOfType<QuickMatchServer>();
        foreach (var server in servers)
        {
            if (server != null && server.Runner == runner)
                return server;
        }

        return null;
    }

    private NetworkObject SpawnWithServerAuthority(NetworkRunner runner, NetworkObject prefab, Vector3 position, Quaternion rotation, PlayerRef requestedAuthority)
    {
        if (runner == null || prefab == null)
            return null;

        var spawned = runner.Spawn(prefab, position, SanitizeRotation(rotation), requestedAuthority);
        if (spawned != null)
            TryEnablePhysics(spawned);

        return spawned;
    }

    private bool IsBotPlayer(int playerId)
    {
        if (playerId <= 0)
            return true;

        return BotPlayerController.Instance != null && BotPlayerController.Instance.IsBotPlayer(playerId);
    }

    private void MoveNetworkObjectToGameScene(NetworkRunner runner, NetworkObject networkObject, Scene loadedScene)
    {
        if (networkObject == null)
            return;

        runner?.MoveToRunnerScene(networkObject.gameObject);

        if (loadedScene.IsValid() && networkObject.gameObject.scene != loadedScene)
            SceneManager.MoveGameObjectToScene(networkObject.gameObject, loadedScene);
    }

    private void EnsureRunnerPhysicsSupport(NetworkRunner runner)
    {
        if (runner == null || runner.gameObject == null)
            return;

        var runnerPhysics = runner.gameObject.GetComponent<RunnerSimulatePhysics3D>();
        if (runnerPhysics == null)
            runnerPhysics = runner.gameObject.AddComponent<RunnerSimulatePhysics3D>();

        if (runnerPhysics.ClientPhysicsSimulation == ClientPhysicsSimulation.Disabled)
            runnerPhysics.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateForward;
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
            ApplyGravity(rigidbody);
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

    private static Quaternion SanitizeRotation(Quaternion rotation)
    {
        float sqrMagnitude = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
        if (float.IsNaN(sqrMagnitude) || float.IsInfinity(sqrMagnitude) || sqrMagnitude <= 0.0001f)
            return Quaternion.identity;

        float magnitude = Mathf.Sqrt(sqrMagnitude);
        return new Quaternion(rotation.x / magnitude, rotation.y / magnitude, rotation.z / magnitude, rotation.w / magnitude);
    }

    private void EnsureBotController()
    {
        if (BotPlayerController.Instance != null)
            return;

        var go = new GameObject("BotPlayerController");
        go.AddComponent<BotPlayerController>();
        DontDestroyOnLoad(go);
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
}
#endif
