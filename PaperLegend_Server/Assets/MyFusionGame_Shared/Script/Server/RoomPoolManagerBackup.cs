//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Linq;
//using System.Net;
//using System.Reflection;
//using System.Threading;
//using ExitGames.Client.Photon;
//using Fusion;
//using Fusion.Photon.Realtime;
//using Fusion.Sockets;
//using Unity.VisualScripting;
//using UnityEngine;
//using UnityEngine.SceneManagement;

//public class RoomPoolManager : MonoBehaviour, INetworkRunnerCallbacks
//{
//    public static RoomPoolManager? Instance { get; private set; }

//    [SerializeField]
//    private int _targetEmptyRooms = 1;

//    public int _maxPlayersPerRoom = 2; // điều chỉnh người chơi cần vào phòng mới có thể vào game.

//    [SerializeField]
//    private float _idleShutdownSeconds = 180f;

//    [SerializeField]
//    private ServerConfig? _serverConfig;

//    [SerializeField]
//    int netWorldBuildIndex = 1;
//    private readonly Dictionary<NetworkRunner, RoomEntry> _rooms = new();
//    private readonly HashSet<NetworkRunner> _shutdownInProgress = new();
//    private readonly Queue<PlayerRef> _quickMatchQueue = new();
//    private readonly HashSet<PlayerRef> _queuedQuickMatchPlayers = new();
//    private readonly Dictionary<NetworkRunner, QuickMatchServerCallbacks> _quickMatchServerCallbacks = new();
//    private readonly Dictionary<NetworkRunner, HashSet<int>> _leaveRoomRequests = new();
//    private static readonly MethodInfo? SetSceneNameInternalMethod = typeof(Scene).GetMethod("SetNameInternal", BindingFlags.NonPublic | BindingFlags.Instance);
//    private static bool _sceneRenameWarningLogged;

//    public readonly struct RoomPoolStatistics
//    {
//        public RoomPoolStatistics(string photonRegion, int totalOnlinePlayers, int totalRooms, int occupiedRooms, int fullRooms)
//        {
//            PhotonRegion = photonRegion;
//            TotalOnlinePlayers = totalOnlinePlayers;
//            TotalRooms = totalRooms;
//            OccupiedRooms = occupiedRooms;
//            FullRooms = fullRooms;
//        }

//        public string PhotonRegion { get; }
//        public int TotalOnlinePlayers { get; }
//        public int TotalRooms { get; }
//        public int OccupiedRooms { get; }
//        public int FullRooms { get; }
//    }

//    [SerializeField]
//    private int _maxConcurrentPlayers = 18;

//    private int _currentOnlinePlayers;

//    private AppSettings _customPhotonSettings = null!;
//    private ushort _basePort;
//    private string _roomPrefix = "Room";
//    private bool _initialised;
//    private bool _isCreatingRooms;
//    private bool _lastRoomCreationSucceeded = true;
//    private int _nextPortOffset;
//    private int _nextRoomIndex;
//    private string _resolvedPublicIpAddress = "0.0.0.0";
//    private Dictionary<string, SessionProperty>? _sessionProperties;
//    private int _matchRoomPropertyValue = (int)TypeMatchGid.MatchRoom;

//    private Coroutine? _topUpRoutine;

//    //[SerializeField]
//    //private NetworkPrefabRef _quickMatchClientPrefab;

//    //[SerializeField]
//    //private NetworkPrefabRef _quickMatchPlayerControllerPrefab;

//    [SerializeField]
//    private NetworkPrefabRef _matchGameNetworkPrefab;

//    private class RoomEntry
//    {
//        public int Index { get; set; }
//        public NetworkRunner Runner = null!;
//        public string Name = string.Empty;
//        public int PlayerCount;
//        public DateTime LastEmptyUtc;
//        public ushort Port;
//        public bool IsReserved;
//        public NetworkObject? QuickMatchClientInstance;
//        public SceneRef NetworkSceneRef;
//        public Scene NetworkScene;
//        public HashSet<int> ProcessedLeaveRequests = new();
//    }

//    public void Set_matchGameNetworPrefab(NetworkPrefabRef prefab)
//    {
//        _matchGameNetworkPrefab = prefab;

//        if (!_matchGameNetworkPrefab.IsValid)
//        {
//            Debug.LogWarning("  prefab reference is not valid. Ensure a _matchGameNetworkPrefab instance exists in the loaded scene.");
//        }
//    }

//    /*   public void SetQuickMatchPlayerControllerPrefab(NetworkPrefabRef prefab)
//       {
//           _quickMatchPlayerControllerPrefab = prefab;

//           foreach (var callback in _quickMatchServerCallbacks.Values)
//           {
//               if (callback != null)
//               {
//                   callback.SetPlayerControllerPrefab(prefab);
//               }
//           }
//       } */

//    private void Awake()
//    {
//        if (Instance != null && Instance != this)
//        {
//            Debug.LogWarning("⚠️ Multiple RoomPoolManager instances detected. Replacing the previous instance.");
//        }

//        Instance = this;
//    }

//    public RoomPoolStatistics GetStatisticsSnapshot()
//    {
//        var photonRegion = !string.IsNullOrWhiteSpace(_customPhotonSettings?.FixedRegion)
//            ? _customPhotonSettings.FixedRegion
//            : "asia";
//        var totalOnlinePlayers = Volatile.Read(ref _currentOnlinePlayers);

//        int totalRooms;
//        int occupiedRooms;
//        int fullRooms;

//        lock (_rooms)
//        {
//            totalRooms = _rooms.Count;
//            occupiedRooms = 0;
//            fullRooms = 0;

//            foreach (var entry in _rooms.Values)
//            {
//                if (entry.PlayerCount > 0)
//                {
//                    occupiedRooms++;
//                }

//                if (entry.PlayerCount >= _maxPlayersPerRoom)
//                {
//                    fullRooms++;
//                }
//            }
//        }

//        return new RoomPoolStatistics(photonRegion, totalOnlinePlayers, totalRooms, occupiedRooms, fullRooms);
//    }

//    public IEnumerator InitialisePool(AppSettings photonSettings, ushort basePort, string roomPrefix, string? sessionPropertiesConfig = null)
//    {
//        if (photonSettings == null) throw new ArgumentNullException(nameof(photonSettings));

//        //ResolvePublicIpAddress();

//        _customPhotonSettings = photonSettings;

//        if (string.IsNullOrWhiteSpace(_customPhotonSettings.FixedRegion))
//        {
//            _customPhotonSettings.FixedRegion = "asia";
//        }
//        _basePort = basePort;
//        _roomPrefix = string.IsNullOrWhiteSpace(roomPrefix) ? "Room" : roomPrefix;
//        _sessionProperties = ParseSessionProperties(sessionPropertiesConfig);
//        _matchRoomPropertyValue = ExtractMatchRoomValue(_sessionProperties);
//        _rooms.Clear();
//        _shutdownInProgress.Clear();
//        _currentOnlinePlayers = 0;
//        _nextPortOffset = 0;
//        _nextRoomIndex = 0;

//        Debug.Log($"🏁 RoomPoolManager.InitialisePool() with basePort={_basePort}, targetEmptyRooms={_targetEmptyRooms}, idleShutdown={_idleShutdownSeconds}s");

//        yield return EnsureMinimumEmptyRoomsCoroutine(forceLog: true);

//        _initialised = true;
//        LogPoolStatus("Initial pool ready");
//    }

//    public static Dictionary<string, SessionProperty>? ParseSessionProperties(string? sessionPropertiesConfig)
//    {
//        if (string.IsNullOrWhiteSpace(sessionPropertiesConfig))
//        {
//            return null;
//        }

//        var parsedProperties = new Dictionary<string, SessionProperty>(StringComparer.Ordinal);

//        var entries = sessionPropertiesConfig.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

//        foreach (var entry in entries)
//        {
//            var kvp = entry.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
//            if (kvp.Length != 2)
//            {
//                continue;
//            }

//            var key = kvp[0].Trim();
//            var value = kvp[1].Trim();

//            if (string.IsNullOrEmpty(key))
//            {
//                continue;
//            }

//            SessionProperty sessionProperty;
//            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
//            {
//                sessionProperty = (SessionProperty)intValue;
//            }
//            else if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
//            {
//                sessionProperty = (SessionProperty)floatValue;
//            }
//            else if (bool.TryParse(value, out var boolValue))
//            {
//                sessionProperty = (SessionProperty)boolValue;
//            }
//            else
//            {
//                sessionProperty = (SessionProperty)value;
//            }

//            parsedProperties[key] = sessionProperty;
//        }

//        return parsedProperties.Count > 0 ? parsedProperties : null;
//    }

//    private static int ExtractMatchRoomValue(Dictionary<string, SessionProperty>? properties)
//    {
//        if (properties != null &&
//            properties.TryGetValue("MatchRoom", out var matchRoomValue) &&
//            int.TryParse(matchRoomValue.ToString(), out var parsed))
//        {
//            return parsed;
//        }

//        return (int)TypeMatchGid.MatchRoom;
//    }

//    private void ResolvePublicIpAddress()
//    {
//        if (_serverConfig == null)
//        {
//            Debug.LogError("Server config asset is not assigned to RoomPoolManager. Defaulting public IP to 0.0.0.0.");
//            _resolvedPublicIpAddress = "0.0.0.0";
//            return;
//        }

//        var configuredIp = "";

//        if (string.IsNullOrWhiteSpace(configuredIp))
//        {
//            Debug.LogWarning("Public IP address is empty in the assigned ServerConfig. Defaulting to 0.0.0.0.");
//            _resolvedPublicIpAddress = "0.0.0.0";
//            return;
//        }

//        configuredIp = configuredIp.Trim();

//        if (!IPAddress.TryParse(configuredIp, out _))
//        {
//            Debug.LogError($"Invalid public IP address '{configuredIp}' configured in ServerConfig. Defaulting to 0.0.0.0.");
//            _resolvedPublicIpAddress = "0.0.0.0";
//            return;
//        }

//        _resolvedPublicIpAddress = configuredIp;
//    }

//    private void Update()
//    {
//        if (!_initialised)
//        {
//            return;
//        }

//        var roomCount = _rooms.Count;
//        var anyRoomHasPlayers = false;

//        foreach (var entry in _rooms.Values)
//        {
//            if (entry.PlayerCount > 0)
//            {
//                anyRoomHasPlayers = true;
//                break;
//            }
//        }

//        if (roomCount <= _targetEmptyRooms || anyRoomHasPlayers)
//        {
//            return;
//        }

//        var emptyRooms = new List<NetworkRunner>();
//        var utcNow = DateTime.UtcNow;

//        foreach (var kvp in _rooms)
//        {
//            var entry = kvp.Value;

//            if (entry.PlayerCount == 0)
//            {
//                var idleSeconds = (utcNow - entry.LastEmptyUtc).TotalSeconds;
//                if (_idleShutdownSeconds > 0 && idleSeconds >= _idleShutdownSeconds)
//                {
//                    emptyRooms.Add(entry.Runner);
//                }
//            }
//        }

//        foreach (var runner in emptyRooms)
//        {
//            if (_shutdownInProgress.Add(runner))
//            {
//                StartCoroutine(ShutdownRoomCoroutine(runner));
//            }
//        }
//    }

//    private IEnumerator EnsureMinimumEmptyRoomsCoroutine(bool forceLog = false)
//    {
//        while (_isCreatingRooms)
//        {
//            yield return null;
//        }

//        _isCreatingRooms = true;

//        try
//        {
//            int roomsToCreate = _targetEmptyRooms - CountPlayerRoomsNotFull();

//            for (int i = 0; i < roomsToCreate; i++)
//            {
//                yield return CreateRoomCoroutine();

//                if (!_lastRoomCreationSucceeded)
//                {
//                    break;
//                }
//            }

//            if (forceLog && _lastRoomCreationSucceeded)
//            {
//                LogPoolStatus("Ensured minimum empty rooms");
//            }
//        }
//        finally
//        {
//            _isCreatingRooms = false;
//        }
//    }

//    private IEnumerator CreateRoomCoroutine()
//    {
//        _lastRoomCreationSucceeded = false;

//        //var roomName = GenerateRoomName();
//        var roomName = _roomPrefix;
//        var port = (ushort)(_basePort + _nextPortOffset);
//        _nextPortOffset++;

//        Debug.Log($"➕ Creating room '{roomName}' on port {port}");

//        var go = new GameObject($"Runner_{roomName}");
//        DontDestroyOnLoad(go);

//        var runner = go.AddComponent<NetworkRunner>();
//        runner.ProvideInput = false;

//        var sceneManager = go.AddComponent<NetworkSceneManagerDefault>();

//        runner.AddCallbacks(this);

//        QuickMatchServerCallbacks? quickMatchCallbacks = null;
//        var sessionProperties = _sessionProperties == null
//    ? null
//    : new Dictionary<string, SessionProperty>(_sessionProperties);

//        var customSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
//        customSettings.FixedRegion = "asia";
//        customSettings.AppVersion = PhotonAppSettings.Global.AppSettings.AppVersion;

//        var args = new StartGameArgs
//        {
//            GameMode = GameMode.Server,
//            SessionName = roomName,
//            //CustomLobbyName = "DefaultLobby",
//            // Server lắng nghe trên tất cả các interface nội bộ
//            Address = NetAddress.CreateFromIpPort("0.0.0.0", 27015),
//            // Server báo cáo địa chỉ công cộng cho Client kết nối trên VPS
//            //CustomPublicAddress = NetAddress.CreateFromIpPort(_resolvedPublicIpAddress, port),
//            SceneManager = sceneManager,
//            // Scene = SceneRef.FromIndex(netWorldBuildIndex),
//            PlayerCount = _maxPlayersPerRoom,
//            CustomPhotonAppSettings = customSettings,
//            SessionProperties = sessionProperties
//            //ObjectProvider = objectProvider
//        };

//        string propertiesLog = args.SessionProperties != null
//               ? string.Join(", ", args.SessionProperties.Select(kvp => $"{kvp.Key}={kvp.Value}"))
//               : "None";
//        Debug.Log($"StartGameArgs:AppVersion ={customSettings.AppVersion}, Region='{customSettings.FixedRegion}',SessionProperties: {propertiesLog}");

//        var startTask = runner.StartGame(args);
//        while (!startTask.IsCompleted)
//        {
//            yield return null;
//        }

//        var result = startTask.Result;

//        if (result.Ok)
//        {
//            var roomIndex = _nextRoomIndex++;
//            var entry = new RoomEntry
//            {
//                Index = roomIndex,
//                Runner = runner,
//                Name = roomName,
//                PlayerCount = 0,
//                LastEmptyUtc = DateTime.UtcNow,
//                Port = port,
//                QuickMatchClientInstance = null,
//                NetworkSceneRef = default,
//                NetworkScene = default
//            };

//            var spawnFailed = false;
//            NetworkObject? matchGameNetworkObject = null;
//            if (_matchGameNetworkPrefab.IsValid)
//            {
//                try
//                {
//                    var serverAuthorityPlayer = GetServerAuthorityPlayer(runner);
//                    matchGameNetworkObject = runner.Spawn(
//                        _matchGameNetworkPrefab,
//                        Vector3.zero,
//                        Quaternion.identity,
//                        serverAuthorityPlayer
//                    );
//                    Debug.Log($"🏷️ Spawned MatchGameNetWorkPrefab for '{roomName}' with authority '{serverAuthorityPlayer}'.");
//                    if (matchGameNetworkObject != null)
//                    {
//                        var spawnedGo = matchGameNetworkObject.gameObject;
//                        if (spawnedGo != null)
//                        {
//                            spawnedGo.name = $"{roomName}_{spawnedGo.name}";
//                        }
//                    }
//                }
//                catch (Exception ex)
//                {
//                    spawnFailed = true;
//                    Debug.LogError($"❌ Failed to spawn MatchGameNetWorkPrefab for room '{roomName}': {ex.Message}");
//                }

//                if (!spawnFailed && matchGameNetworkObject == null)
//                {
//                    spawnFailed = true;
//                    Debug.LogError($"❌ MatchGameNetWorkPrefab spawn returned null NetworkObject in room '{roomName}'.");
//                }
//            }
//            else
//            {
//                spawnFailed = true;
//                Debug.LogError("❌ MatchGameNetWorkPrefab reference is not valid. Ensure it is assigned in RoomPoolManager.");
//            }

//            if (spawnFailed)
//            {
//                runner.RemoveCallbacks(this);

//                if (quickMatchCallbacks != null)
//                {
//                    runner.RemoveCallbacks(quickMatchCallbacks);

//                    if (quickMatchCallbacks is IDisposable disposable)
//                    {
//                        disposable.Dispose();
//                    }

//                    Destroy(quickMatchCallbacks);
//                }

//                Destroy(go);
//                yield break;
//            }

//            if (matchGameNetworkObject != null)
//            {
//                var matchGameGo = matchGameNetworkObject.gameObject;
//                DontDestroyOnLoad(matchGameGo);

//                try
//                {
//                    runner.MakeDontDestroyOnLoad(matchGameGo);
//                }
//                catch (Exception ex)
//                {
//                    Debug.LogWarning($"⚠️ Unable to mark '{matchGameGo.name}' as DontDestroyOnLoad via runner: {ex.Message}");
//                }
//            }

//            //var serverInitializerGo = new GameObject($"ServerInitializer_{roomName}");
//            //serverInitializerGo.transform.SetParent(go.transform, worldPositionStays: false);
//            //DontDestroyOnLoad(serverInitializerGo);
//            //serverInitializerGo.AddComponent<GameServerInitializer>();

//            entry.NetworkScene = default;
//            entry.NetworkSceneRef = default;

//            _rooms[runner] = entry;
//            //if (quickMatchCallbacks != null)
//            //{
//            //    quickMatchCallbacks.Initialise(entry.Index, entry.Name, quickMatchInstance, entry.NetworkScene);
//            //    _quickMatchServerCallbacks[runner] = quickMatchCallbacks;
//            //}

//            _lastRoomCreationSucceeded = true;
//            Debug.Log($"✅ Room '{roomName}' started on port {port}");
//            //  Debug.Log($"Runner={runner.name} mode={runner.GameMode} " +
//            //$"appId={PhotonAppSettings.Instance.AppSettings.AppIdFusion} " +
//            //$"region={runner.CloudRegion} lobby={runner.SessionInfo?.Lobby?.Name} " +
//            //$"room={runner.SessionInfo?.Name} players={runner.SessionInfo?.PlayerCount}");

//        }
//        else
//        {
//            Debug.LogError($"❌ Failed to start room '{roomName}': {result.ShutdownReason}");
//            runner.RemoveCallbacks(this);
//            if (quickMatchCallbacks != null)
//            {
//                runner.RemoveCallbacks(quickMatchCallbacks);
//                Destroy(quickMatchCallbacks);
//            }
//            Destroy(go);
//        }
//    }

//    private void AttachNetworkObjectToRoomScene(NetworkObject networkObject, RoomEntry entry, bool fallbackToDontDestroyOnLoad)
//    {
//        if (networkObject == null)
//        {
//            return;
//        }

//        var go = networkObject.gameObject;
//        var targetScene = entry.NetworkScene;

//        if (targetScene.IsValid() && targetScene.isLoaded)
//        {
//            try
//            {
//                SceneManager.MoveGameObjectToScene(go, targetScene);
//            }
//            catch (Exception ex)
//            {
//                Debug.LogWarning($"⚠️ Unable to move '{go.name}' into scene '{targetScene.name}': {ex.Message}");
//            }

//            go.transform.SetParent(null);
//        }
//        else if (fallbackToDontDestroyOnLoad)
//        {
//            DontDestroyOnLoad(go);

//            try
//            {
//                entry.Runner.MakeDontDestroyOnLoad(go);
//            }
//            catch (Exception ex)
//            {
//                Debug.LogWarning($"⚠️ Unable to mark '{go.name}' as DontDestroyOnLoad via runner: {ex.Message}");
//            }
//        }
//        else
//        {
//            Debug.LogError($"❌ Unable to attach '{go.name}' to the network scene for room '{entry.Name}' because the scene is not loaded.");
//        }
//    }

//    private static PlayerRef GetServerAuthorityPlayer(NetworkRunner runner)
//    {
//        if (runner == null)
//        {
//            return PlayerRef.None;
//        }

//        if (runner.GameMode == GameMode.Server)
//        {
//            var localPlayer = runner.LocalPlayer;
//            if (!localPlayer.IsNone)
//            {
//                return localPlayer;
//            }

//            var activePlayers = runner.ActivePlayers;
//            if (activePlayers.Any())
//            {
//                return activePlayers.First();
//            }

//            return PlayerRef.None;
//        }

//        return runner.LocalPlayer;
//    }

//    private static void TrySetSceneDisplayName(Scene scene, string newName)
//    {
//        if (!scene.IsValid() || string.IsNullOrWhiteSpace(newName))
//        {
//            return;
//        }

//        if (SetSceneNameInternalMethod == null)
//        {
//            if (!_sceneRenameWarningLogged)
//            {
//                Debug.LogWarning($"⚠️ Unable to rename scene '{scene.name}' to '{newName}' because the internal rename API is not available in this Unity version.");
//                _sceneRenameWarningLogged = true;
//            }

//            return;
//        }

//        try
//        {
//            SetSceneNameInternalMethod.Invoke(scene, new object[] { newName });
//        }
//        catch (Exception ex)
//        {
//            if (!_sceneRenameWarningLogged)
//            {
//                Debug.LogWarning($"⚠️ Failed to rename scene '{scene.name}' to '{newName}': {ex.Message}");
//                _sceneRenameWarningLogged = true;
//            }
//        }
//    }

//    //private NetworkObject? ResolveQuickMatchClient(RoomEntry entry)
//    //{
//    //    var targetScene = entry.NetworkScene;

//    //    if (!targetScene.IsValid() || !targetScene.isLoaded)
//    //    {
//    //        Debug.LogWarning($"⚠️ Cannot resolve quick match client for room '{entry.Name}' because the network scene is not loaded.");
//    //        return null;
//    //    }

//    //    var quickMatchObject = FindQuickMatchClientInScene(targetScene);

//    //    if (quickMatchObject == null)
//    //    {
//    //        Debug.LogWarning($"⚠️ Unable to locate a QuickMatchClient instance for room '{entry.Name}' in scene '{targetScene.name}'. Ensure the scene contains an active QuickMatchClient network object.");
//    //        return null;
//    //    }

//    //    return quickMatchObject;
//    //}

//    //private NetworkObject? FindQuickMatchClientInScene(Scene targetScene)
//    //{
//    //    foreach (var root in targetScene.GetRootGameObjects())
//    //    {
//    //        var quickMatch = root.GetComponentInChildren<QuickMatchClient>(includeInactive: true);

//    //        if (quickMatch == null)
//    //        {
//    //            continue;
//    //        }

//    //        var networkObject = quickMatch.Object != null && quickMatch.Object.IsValid
//    //            ? quickMatch.Object
//    //            : quickMatch.GetComponent<NetworkObject>();

//    //        if (networkObject != null && networkObject.IsValid)
//    //        {
//    //            return networkObject;
//    //        }
//    //    }

//    //    return null;
//    //}

//    private IEnumerator UnloadNetworkSceneCoroutine(NetworkRunner runner, RoomEntry entry)
//    {
//        var targetScene = entry.NetworkScene;
//        var sceneRef = entry.NetworkSceneRef;
//        var sceneLabel = targetScene.IsValid() ? targetScene.name : (sceneRef.IsValid ? sceneRef.ToString() : $"build index {netWorldBuildIndex}");
//        var unloadedViaRunner = false;

//        if (sceneRef.IsValid && runner != null && runner.SceneManager != null)
//        {
//            NetworkSceneAsyncOp unloadOperation;

//            try
//            {
//                unloadOperation = runner.SceneManager.UnloadScene(sceneRef);
//            }
//            catch (Exception ex)
//            {
//                Debug.LogWarning($"⚠️ Failed to request unload of network scene '{sceneLabel}' for room '{entry.Name}': {ex.Message}");
//                unloadOperation = default;
//            }

//            if (unloadOperation.IsValid)
//            {
//                while (!unloadOperation.IsDone)
//                {
//                    yield return null;
//                }

//                if (unloadOperation.Error == null)
//                {
//                    unloadedViaRunner = true;
//                }
//                else
//                {
//                    Debug.LogWarning($"⚠️ Unloading network scene '{sceneLabel}' for room '{entry.Name}' reported error: {unloadOperation.Error.Message}");
//                }
//            }
//        }

//        if (!unloadedViaRunner && targetScene.IsValid() && targetScene.isLoaded)
//        {
//            AsyncOperation? unloadAsync = null;

//            try
//            {
//                unloadAsync = SceneManager.UnloadSceneAsync(targetScene);
//            }
//            catch (Exception ex)
//            {
//                Debug.LogWarning($"⚠️ Unity unload of scene '{targetScene.name}' for room '{entry.Name}' failed: {ex.Message}");
//            }

//            if (unloadAsync != null)
//            {
//                while (!unloadAsync.isDone)
//                {
//                    yield return null;
//                }
//            }
//        }

//        entry.NetworkScene = default;
//        entry.NetworkSceneRef = default;
//    }

//    private IEnumerator ShutdownRoomCoroutine(NetworkRunner runner)
//    {
//        if (!_rooms.TryGetValue(runner, out var entry))
//        {
//            yield break;
//        }

//        var runnerToShutdown = entry.Runner;
//        QuickMatchServerCallbacks? quickMatchCallbacks = null;

//        if (_quickMatchServerCallbacks.TryGetValue(runnerToShutdown, out var storedCallbacks))
//        {
//            quickMatchCallbacks = storedCallbacks;
//            _quickMatchServerCallbacks.Remove(runnerToShutdown);
//        }

//        _rooms.Remove(runnerToShutdown);

//        var shutdownKey = runnerToShutdown;

//        Debug.Log($"♻️ Shutting down idle room '{entry.Name}' on port {entry.Port}");

//        try
//        {
//            if (entry.QuickMatchClientInstance != null && entry.QuickMatchClientInstance.IsValid && runnerToShutdown)
//            {
//                runnerToShutdown.Despawn(entry.QuickMatchClientInstance);
//                entry.QuickMatchClientInstance = null;
//            }

//            yield return UnloadNetworkSceneCoroutine(runnerToShutdown, entry);

//            if (runnerToShutdown)
//            {
//                var shutdownTask = runnerToShutdown.Shutdown();

//                while (!shutdownTask.IsCompleted)
//                {
//                    yield return null;
//                }

//                // Ensure Fusion has a frame to finalise runner state before we continue tearing down.
//                yield return null;
//            }

//            if (runnerToShutdown)
//            {
//                runnerToShutdown.RemoveCallbacks(this);
//            }

//            if (quickMatchCallbacks != null)
//            {
//                if (runnerToShutdown)
//                {
//                    runnerToShutdown.RemoveCallbacks(quickMatchCallbacks);
//                }

//                if (quickMatchCallbacks)
//                {
//                    Destroy(quickMatchCallbacks);
//                }
//            }

//            AdjustOnlinePlayerCount(-entry.PlayerCount);

//            if (runnerToShutdown)
//            {
//                Destroy(runnerToShutdown.gameObject);

//                // Wait a frame so any pending operations triggered by Destroy() can complete
//                // before we signal that the shutdown is finished. This prevents late callbacks
//                // (such as runner.LoadScene) from observing a half-disposed runner instance.
//                yield return null;
//            }
//        }
//        finally
//        {
//            _shutdownInProgress.RemoveWhere(r => ReferenceEquals(r, shutdownKey) || r == null);
//        }

//        LogPoolStatus($"Room '{entry.Name}' shut down");

//        if (_topUpRoutine == null)
//        {
//            _topUpRoutine = StartCoroutine(TopUpRoomsCoroutine());
//        }
//    }

//    private IEnumerator TopUpRoomsCoroutine()
//    {
//        try
//        {
//            //yield return EnsureMinimumEmptyRoomsCoroutine();
//            yield return null;
//        }
//        finally
//        {
//            _topUpRoutine = null;
//        }
//    }

//    private int CountEmptyRooms()
//    {
//        return _rooms.Values.Count(r => r.PlayerCount == 0);
//    }
//    private int CountPlayerRoomsNotFull()
//    {
//        int TotalRoomNotFull = _rooms.Values.Count(r => r.PlayerCount <= _maxPlayersPerRoom);
//        return TotalRoomNotFull;
//    }
//    //private string GenerateRoomName()
//    //{
//    //    // var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
//    //    // return $"{_roomPrefix}_{guid}";
//    //    return _roomPrefix;
//    //}

//    private void LogPoolStatus(string context)
//    {
//        var status = _rooms.Count == 0
//            ? "(none)"
//            : string.Join(", ", _rooms.Values.Select(r => $"{r.Name}[players={r.PlayerCount},port={r.Port}]").ToArray());
//        Debug.Log($"📊 {context} | Rooms: {status}");
//    }

//    private void OnDestroy()
//    {
//        if (Instance == this)
//        {
//            Instance = null;
//        }

//        foreach (var runner in _rooms.Keys.ToList())
//        {
//            if (_rooms.TryGetValue(runner, out var entry) && entry.QuickMatchClientInstance != null && entry.QuickMatchClientInstance.IsValid)
//            {
//                runner.Despawn(entry.QuickMatchClientInstance);
//                entry.QuickMatchClientInstance = null;
//            }

//            runner.RemoveCallbacks(this);
//            if (_quickMatchServerCallbacks.TryGetValue(runner, out var quickMatchCallbacks))
//            {
//                runner.RemoveCallbacks(quickMatchCallbacks);
//                _quickMatchServerCallbacks.Remove(runner);
//                if (quickMatchCallbacks)
//                {
//                    Destroy(quickMatchCallbacks);
//                }
//            }

//            if (entry != null)
//            {
//                if (entry.NetworkScene.IsValid() && entry.NetworkScene.isLoaded)
//                {
//                    SceneManager.UnloadSceneAsync(entry.NetworkScene);
//                }

//                entry.NetworkScene = default;
//                entry.NetworkSceneRef = default;
//            }
//            _shutdownInProgress.Remove(runner);
//            runner.Shutdown();
//            Destroy(runner.gameObject);
//        }

//        _rooms.Clear();
//        _quickMatchServerCallbacks.Clear();
//        _shutdownInProgress.Clear();
//        _currentOnlinePlayers = 0;
//    }

//    internal bool TryEnqueueQuickMatchPlayer(PlayerRef player, out SessionInfo sessionInfo, out List<PlayerRef> players)
//    {
//        sessionInfo = default;
//        players = new List<PlayerRef>();

//        if (!_initialised)
//        {
//            return false;
//        }

//        if (!_queuedQuickMatchPlayers.Add(player))
//        {
//            return false;
//        }

//        _quickMatchQueue.Enqueue(player);

//        if (_topUpRoutine == null && CountPlayerRoomsNotFull() == 0)
//        {
//            _topUpRoutine = StartCoroutine(TopUpRoomsCoroutine());
//        }

//        return TryDequeueQuickMatchGroup(out sessionInfo, out players);
//    }

//    internal void RequeueQuickMatchPlayers(IEnumerable<PlayerRef> players)
//    {
//        foreach (var player in players)
//        {
//            if (_queuedQuickMatchPlayers.Add(player))
//            {
//                _quickMatchQueue.Enqueue(player);
//            }
//        }
//    }

//    internal bool TryAllocateQuickMatchGroup(out SessionInfo sessionInfo, out List<PlayerRef> players)
//    {
//        return TryDequeueQuickMatchGroup(out sessionInfo, out players);
//    }

//    private bool TryDequeueQuickMatchGroup(out SessionInfo sessionInfo, out List<PlayerRef> players)
//    {
//        sessionInfo = default;
//        players = new List<PlayerRef>();

//        if (_quickMatchQueue.Count < _maxPlayersPerRoom)
//        {
//            return false;
//        }

//        var availableRoom = _rooms.Values.FirstOrDefault(r => r.PlayerCount == 0 && !r.IsReserved);
//        if (availableRoom == null)
//        {
//            return false;
//        }

//        sessionInfo = availableRoom.Runner.SessionInfo;
//        availableRoom.IsReserved = true;

//        for (int i = 0; i < _maxPlayersPerRoom && _quickMatchQueue.Count > 0; i++)
//        {
//            var queuedPlayer = _quickMatchQueue.Dequeue();
//            _queuedQuickMatchPlayers.Remove(queuedPlayer);
//            players.Add(queuedPlayer);
//        }

//        return players.Count == _maxPlayersPerRoom;
//    }

//    internal void ReleaseQuickMatchReservation(SessionInfo sessionInfo)
//    {
//        if (!sessionInfo.IsValid)
//        {
//            return;
//        }

//        foreach (var entry in _rooms.Values)
//        {
//            if (entry.Runner.SessionInfo.Name == sessionInfo.Name)
//            {
//                entry.IsReserved = false;
//                return;
//            }
//        }
//    }

//    private bool IsMatchRoomSession(SessionInfo sessionInfo)
//    {
//        if (sessionInfo == null || !sessionInfo.IsValid)
//        {
//            return false;
//        }

//        if (sessionInfo.Properties == null || !sessionInfo.Properties.TryGetValue("MatchRoom", out var matchRoomValue) || matchRoomValue == null)
//        {
//            return false;
//        }

//        return int.TryParse(matchRoomValue.ToString(), out var matchType) && matchType == _matchRoomPropertyValue;
//    }

//    #region INetworkRunnerCallbacks
//    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
//    {
//        if (_rooms.TryGetValue(runner, out var entry))
//        {
//            entry.IsReserved = false;
//            UpdateRoomPlayerCount(entry, runner.ActivePlayers.Count());
//            Debug.Log($"👥 Player joined room '{entry.Name}'. Count={entry.PlayerCount}");
//            if (entry.PlayerCount == 1 && IsMatchRoomSession(runner.SessionInfo))
//            {
//                runner.SessionInfo.IsVisible = false;
//                Debug.Log($"🚫 Room '{entry.Name}' is now hidden from random matchmaking (MatchRoom).");
//            }
//            if (entry.PlayerCount >= _maxPlayersPerRoom)
//            {
//                Debug.Log($"🚪 Room '{entry.Name}' is full.");
//            }

//            var emptyRoomCount = CountPlayerRoomsNotFull();
//            if (_topUpRoutine == null && emptyRoomCount < _targetEmptyRooms)
//            {
//                _topUpRoutine = StartCoroutine(TopUpRoomsCoroutine());
//            }
//            LogPoolStatus($"Player joined {entry.Name}");
//        }
//    }

//    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
//    {
//        if (_rooms.TryGetValue(runner, out var entry))
//        {
//            UpdateRoomPlayerCount(entry, Math.Max(0, runner.ActivePlayers.Count()));
//            if (entry.PlayerCount == 0)
//            {
//                entry.LastEmptyUtc = DateTime.UtcNow;
//            }

//            Debug.Log($"👤 Player left room '{entry.Name}'. Count={entry.PlayerCount}");
//            LogPoolStatus($"Player left {entry.Name}");

//            if (_topUpRoutine == null && CountPlayerRoomsNotFull() < _targetEmptyRooms)
//            {
//                _topUpRoutine = StartCoroutine(TopUpRoomsCoroutine());
//            }

//            TrySendLeaveRoomsBatch(runner, player, entry, nameof(OnPlayerLeft));
//        }
//    }

//    private void TrySendLeaveRoomsBatch(NetworkRunner runner, PlayerRef player, RoomEntry entry, string context)
//    {
//        if (APIManager.Instance == null)
//        {
//            Debug.LogWarning($"⚠️ APIManager chưa sẵn sàng, bỏ qua leaveRooms ({context}).");
//            return;
//        }

//        if (IsMatchInProgress())
//        {
//            Debug.Log($"⚠️ Bỏ qua leaveRooms ({context}) vì trận đấu đang diễn ra và người chơi có thể kết nối lại.");
//            return;
//        }

//        if (!TryBuildLeaveRoomsPayload(runner, player, out int roomId, out List<int> userIds))
//        {
//            Debug.LogWarning($"⚠️ Không thể lấy thông tin room/user để gọi leaveRooms ({context}).");
//            return;
//        }

//        var processedSet = GetProcessedLeaveRequestSet(runner, entry);
//        userIds = userIds.Where(id => processedSet.Add(id)).ToList();

//        if (userIds.Count == 0)
//        {
//            return;
//        }

//        StartCoroutine(SendLeaveRoomsBatch(roomId, userIds, context));
//    }

//    private HashSet<int> GetProcessedLeaveRequestSet(NetworkRunner runner, RoomEntry entry)
//    {
//        if (_leaveRoomRequests.TryGetValue(runner, out var trackedIds))
//        {
//            return trackedIds;
//        }

//        _leaveRoomRequests[runner] = entry.ProcessedLeaveRequests ?? new HashSet<int>();
//        return _leaveRoomRequests[runner];
//    }

//    private bool TryBuildLeaveRoomsPayload(NetworkRunner runner, PlayerRef player, out int roomId, out List<int> userIds)
//    {
//        roomId = 0;
//        userIds = null;

//        var serverManager = NetworkObjectManager.Instance;
//        if (serverManager != null)
//        {
//            roomId = serverManager.rpgRoomModel.roomId;
//        }

//        if (roomId <= 0)
//        {
//            return false;
//        }

//        if (!TryGetPlayerUserId(runner, player, out var userId))
//        {
//            return false;
//        }

//        userIds = new List<int> { userId };
//        return true;
//    }

//    private bool TryGetPlayerUserId(NetworkRunner runner, PlayerRef player, out int userId)
//    {
//        userId = 0;

//        var playerObject = runner != null ? runner.GetPlayerObject(player) : null;
//        if (playerObject != null)
//        {
//            var handler = playerObject.GetComponent<PlayerNetworkHandler>();
//            if (handler != null)
//            {
//                var model = handler.PlayerModel;
//                if (model.playerId > 0)
//                {
//                    userId = model.playerId;
//                    return true;
//                }
//            }
//        }

//        Debug.LogWarning($"⚠️ Không thể xác định userId cho PlayerRef {player} để gửi leaveRooms.");
//        return false;
//    }

//    private IEnumerator SendLeaveRoomsBatch(int roomId, List<int> userIds, string context)
//    {
//        bool success = false;
//        yield return StartCoroutine(APIManager.Instance.RunTask(
//            APIManager.Instance.LeaveRoomsBatchAsync(roomId, userIds),
//            result => success = result));

//        if (!success)
//        {
//            Debug.LogWarning($"⚠️ leaveRoomsBatch ({context}) không thành công cho room {roomId}.");
//        }
//    }

//    private bool IsMatchInProgress()
//    {
//        var serverManager = NetworkObjectManager.Instance;
//        if (serverManager == null)
//        {
//            return false;
//        }

//        var status = serverManager.StatusLoading;
//        return status != StatusLoadingGame.EndGame && status >= StatusLoadingGame.isExam;
//    }

//    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
//    {
//        if (_shutdownInProgress.Contains(runner))
//        {
//            return;
//        }

//        if (_rooms.Remove(runner, out var entry))
//        {
//            AdjustOnlinePlayerCount(-entry.PlayerCount);
//            Debug.LogWarning($"⚠️ Runner for room '{entry.Name}' shutdown due to {shutdownReason}");
//            if (_quickMatchServerCallbacks.TryGetValue(runner, out var quickMatchCallbacks))
//            {
//                _quickMatchServerCallbacks.Remove(runner);

//                if (runner)
//                {
//                    runner.RemoveCallbacks(quickMatchCallbacks);
//                }

//                if (quickMatchCallbacks)
//                {
//                    Destroy(quickMatchCallbacks);
//                }
//            }

//            if (entry.QuickMatchClientInstance != null && entry.QuickMatchClientInstance.IsValid && runner)
//            {
//                try
//                {
//                    runner.Despawn(entry.QuickMatchClientInstance);
//                }
//                catch (Exception ex)
//                {
//                    Debug.LogWarning($"⚠️ Failed to despawn quick match client during shutdown of room '{entry.Name}': {ex.Message}");
//                }

//                entry.QuickMatchClientInstance = null;
//            }

//            if (isActiveAndEnabled)
//            {
//                StartCoroutine(UnloadNetworkSceneCoroutine(runner, entry));
//            }
//            else
//            {
//                if (entry.NetworkScene.IsValid() && entry.NetworkScene.isLoaded)
//                {
//                    SceneManager.UnloadSceneAsync(entry.NetworkScene);
//                }

//                entry.NetworkScene = default;
//                entry.NetworkSceneRef = default;
//            }

//            Destroy(runner.gameObject);
//            _shutdownInProgress.Remove(runner);
//            if (_topUpRoutine == null)
//            {
//                _topUpRoutine = StartCoroutine(TopUpRoomsCoroutine());
//            }
//            LogPoolStatus($"Runner shutdown {entry.Name}");
//        }

//        _leaveRoomRequests.Remove(runner);
//    }

//    public void OnConnectedToServer(NetworkRunner runner)
//    {
//    }

//    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
//    {
//    }

//    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
//    {
//        if (_currentOnlinePlayers >= _maxConcurrentPlayers)
//        {
//            Debug.LogError("quá tải server");
//            request.Refuse();
//            return;
//        }

//        request.Accept();
//    }

//    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
//    {
//    }

//    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
//    {
//    }

//    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
//    {
//    }

//    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
//    {
//    }

//    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
//    {
//    }

//    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
//    {
//    }

//    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
//    {
//    }

//    public void OnSceneLoadDone(NetworkRunner runner)
//    {
//    }

//    public void OnSceneLoadStart(NetworkRunner runner)
//    {
//    }

//    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
//    {
//    }

//    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
//    {
//    }

//    public void OnInput(NetworkRunner runner, NetworkInput input)
//    {
//    }

//    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
//    {
//    }

//    public void OnResourceLoadFailed(NetworkRunner runner, object resourceKey, NetworkObject obj)
//    {
//    }

//    public void OnResourceLoadSuccess(NetworkRunner runner, object resourceKey, NetworkObject obj)
//    {
//    }

//    public void OnRpcMessageReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data)
//    {
//    }
//    #endregion

//    private void UpdateRoomPlayerCount(RoomEntry entry, int newCount)
//    {
//        var previous = entry.PlayerCount;
//        entry.PlayerCount = newCount;

//        var delta = newCount - previous;
//        if (delta != 0)
//        {
//            AdjustOnlinePlayerCount(delta);
//        }
//    }

//    private void AdjustOnlinePlayerCount(int delta)
//    {
//        if (delta == 0)
//        {
//            return;
//        }

//        _currentOnlinePlayers = Mathf.Max(0, _currentOnlinePlayers + delta);
//        Debug.Log($"🌐 Total online players: {_currentOnlinePlayers}");
//    }
//}
