using Fusion;
using Fusion.Addons.Physics;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System;
using Fusion.Sockets;
using Unity.VisualScripting;
using Fusion.Photon.Realtime;

//using TMPro.Examples;
#if !UNITY_SERVER
using DG.Tweening;
#endif

//Đối với class có implement INetworkRunnerCallbacks, 
//    nó nên nằm trên MonoBehaviour (hoặc một lớp C# thuần túy được khởi tạo bởi MonoBehaviour quản lý) thay vì NetworkBehaviour.
public class GameManagerNetWork : MonoBehaviour, INetworkRunnerCallbacks
{
    public static GameManagerNetWork Instance;
 
    [Header("LOGIN CONFIG")]
    public LoginUserModel loginUserModel;

    [Header("NET WORK CONFIG")]
    public GameObject networkManagerPrefab;
    public NetworkObjectManager serverRPC;
    public NetworkRunner runner;
    private Coroutine loadServerRPCCoroutine;
    private Coroutine resetRunnerCoroutine;
    private Coroutine manualShutdownCoroutine;
    private GameObject runnerHostObject;
    private QuickMatchReconnectHandler reconnectHandler;
    private readonly HashSet<string> pendingClientSetupScenes = new HashSet<string>();
    private Coroutine clientSetupNotifyRoutine;

    private int currentRoomId = -1;
    public string currentQuickMatchId = null;
    public string currentQuickMatchResultId = null;
    private bool currentIsHost = false;

    private bool isFocused = true;
    private float timeSinceUnfocused = 0f;
    private float timeoutDuration = 60f; // 60 giây
    private bool hasLeftRoom = false;
    private bool manualShutdownRequested = false;
    private float nextServerRpcResolveTime = 0f;

    //private void Awake()
    //{
    //    Instance = this;
    //}
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureRunnerHost();
            reconnectHandler = new QuickMatchReconnectHandler(this);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    private void Start()
    {
        //DOTween.Init();
        StartCoroutine(CheckInternetRoutine());
    }
    private IEnumerator CheckInternetRoutine()
    {
        while (true)
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                //Debug.LogWarning("🚫 Không có kết nối Internet!");
                //PopupHelper.Instance.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_network_false"));
                //if (!QuickMatchHandler.Instance.SuppressNetworkPopup)
                //  PopupHelper.Instance.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_network_false"));
            }


            yield return new WaitForSeconds(3f); // Kiểm tra mỗi 3 giây
        }
    }
    private void OnApplicationPause(bool pauseStatus)
    {
        isFocused = !pauseStatus;

        if (pauseStatus)
        {
            timeSinceUnfocused = 0f;
            return;
        }

        if (ShouldAttemptReconnect())
        {
            StartCoroutine(EnsureReconnectAfterResume());
        }
    }
    //void Update()
    //{
    //    if (!isFocused && !hasLeftRoom)
    //    {
    //        timeSinceUnfocused += Time.deltaTime;

    //        if (timeSinceUnfocused >= timeoutDuration)
    //        {
    //            Debug.Log("📴 Người chơi mất kết nối quá lâu → tự động thoát phòng");

    //            hasLeftRoom = true; // đảm bảo chỉ gọi 1 lần

    //            if (runner != null && runner.IsRunning)
    //            {
    //                APIManager.Instance.LeaveRoomAPI(serverRPC.rpgRoomModel.roomId, loginUserModel.UserId);
    //                CloseConnectToRunner();
    //            }
    //        }
    //    }
    //}


    //public PlayerInfoStruct GetCurrentPlayerGame()
    //{
    //    if(serverRPC != null)
    //    {
    //        var players = serverRPC.players;

    //        for (int i = 0; i < players.Length; i++)
    //        {
    //            var p = players.Get(i);
    //            if (p.playerId == loginUserModel.UserId)
    //                return p;
    //        }
    //    }    


    //    return default;
    //}
    public PlayerInfoStruct GetCurrentPlayerGame()
    {
        var playerGO = NetworkObjectManager.Instance?.GetPlayerObject(loginUserModel.UserId);
        if (playerGO == null)
            return default;

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        return handler.PlayerModel;
    }

    public PlayerInfoStruct GetPlayerGameById(int playerId)
    {
        if (serverRPC != null)
        {
            var players = serverRPC.players;

            for (int i = 0; i < players.Length; i++)
            {
                var p = players.Get(i);
                if (p.playerId == playerId)
                    return p;
            }
        }    

 

        return default;
    }
    public void CreateRoomAndConnect(int roomId, System.Action<bool> onComplete = null)
    {
        StartCoroutine(CreateRoomAndConnectCoroutine(roomId, onComplete));
    }

    private IEnumerator CreateRoomAndConnectCoroutine(int roomId, System.Action<bool> onComplete)
    {
        currentRoomId = roomId;
        currentIsHost = true;
        while (runner == null)
        {
            runner = OpenConnectToPhotonServer();
            yield return null; // đợi 1 frame
        }
        var sceneManager = GetOrAddSceneManager();
        if (sceneManager == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }
        var startArgs = new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = roomId.ToString(),
            SceneManager = sceneManager,
            CustomLobbyName = "",
            SessionProperties = new Dictionary<string, SessionProperty>
        {
            { "map", (SessionProperty)1 },
            { "mode", (SessionProperty)0 }
        }
        };

        var startTask = runner.StartGame(startArgs);
        while (!startTask.IsCompleted) yield return null;

        var startResult = startTask.Result;
        if (startResult.Ok)
        {
            Debug.Log("✅ Phòng đã được tạo!");
            // 💤 Đợi vài frame để đảm bảo mọi thứ đã được load
            yield return new WaitForSeconds(0.5f);

            // Hoặc đợi tới khi scene đã thực sự active
            while (!SceneManager.GetActiveScene().isLoaded)
                yield return null;

            //runner.Spawn(networkManagerPrefab, Vector3.zero, Quaternion.identity, runner.LocalPlayer);
            //StartCoroutine(LoadServerRPC(runner));
            runner.Spawn(networkManagerPrefab, Vector3.zero, Quaternion.identity, runner.LocalPlayer);

            bool rpcReady = false;
            yield return StartCoroutine(LoadServerRPC(runner, rpc =>
            {
                rpcReady = rpc != null;
            }, 60f));

            onComplete?.Invoke(rpcReady);
        }
        else
        {
            Debug.LogWarning($"❌ Lỗi khi tạo phòng: {startResult.ShutdownReason}");
            onComplete?.Invoke(false);
        }
    }
    public Task<bool> JoinRoomAndConnectAsync(int roomId)
    {
        var tcs = new TaskCompletionSource<bool>();
        try
        {
            StartCoroutine(JoinRoomAndConnectCoroutine(roomId, (success) =>
            {
                tcs.SetResult(success);
            }));
        }
        catch(Exception ex) {
            Debug.LogWarning("Khởi động game lỗi:" + ex.Message.ToString());
        }
        return tcs.Task;
    }
    // xử lý sau khi tạo phòng thì tham gia phòng ngay
    public Task<bool> JoinRoomByNameAsync(int roomId, string sessionName, Dictionary<string, SessionProperty> sessionProperties = null)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(JoinRoomByNameCoroutine(roomId, sessionName, sessionProperties, success => tcs.SetResult(success)));
        return tcs.Task;
    }

    private IEnumerator JoinRoomAndConnectCoroutine(int roomId, System.Action<bool> onComplete)
    {
        currentRoomId = roomId;
        currentIsHost = false;
        while (runner == null)
        {
            runner = OpenConnectToPhotonServer();
            yield return null; // đợi 1 frame
        }
        var sceneManager = GetOrAddSceneManager();
        if (sceneManager == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }
        var joinArgs = new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = roomId.ToString(),
            SceneManager = sceneManager
        };

        var startTask = runner.StartGame(joinArgs);

        // Thêm timeout khi chờ startTask hoàn thành
        float timeout = 10f;
        float elapsed = 0f;
        while (!startTask.IsCompleted && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!startTask.IsCompleted)
        {
            Debug.LogWarning("⏱ Timeout khi kết nối StartGame.");
            onComplete?.Invoke(false);
            yield break;
        }

        // Di chuyển kiểm tra runner.IsRunning xuống đây
        if (!runner.IsRunning)
        {
            Debug.LogWarning("⛔ Runner vẫn chưa chạy sau StartGame.");
            onComplete?.Invoke(false);
            yield break;
        }

        var startResult = startTask.Result;
        if (startResult.Ok)
        {
            Debug.Log("✅ Tham gia phòng thành công!");
            bool rpcReady = false;
            yield return StartCoroutine(LoadServerRPC(runner, rpc =>
            {
                rpcReady = rpc != null;
            }, 60f));

            if (rpcReady)
            {
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogWarning("⏰ Timeout khi chờ server RPC sau khi tham gia phòng.");
                onComplete?.Invoke(false);
            }

        }
        else
        {
            Debug.LogWarning($"❌ Lỗi: {startResult.ShutdownReason}");
            onComplete?.Invoke(false);
        }
    }
    // xử lý sau khi tạo phòng join phòng đó ngay
    private IEnumerator JoinRoomByNameCoroutine(int roomId, string sessionName, Dictionary<string, SessionProperty> sessionProperties, System.Action<bool> onComplete)
    {
        currentRoomId = roomId;
        currentIsHost = false;
        while (runner == null)
        {
            // mở kết nối đến photo
            runner = OpenConnectToPhotonServer();
            yield return null; // đợi 1 frame
        }

        var sceneManager = GetOrAddSceneManager();
        if (sceneManager == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        var customSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
        customSettings.FixedRegion = "asia";
        customSettings.AppVersion = PhotonAppSettings.Global.AppSettings.AppVersion;

        var startArgs = new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = sessionName,
            MatchmakingMode = MatchmakingMode.FillRoom,
            SceneManager = sceneManager,
            CustomPhotonAppSettings = customSettings,
            EnableClientSessionCreation = false,
            SessionProperties = sessionProperties
        };

        var startTask = runner.StartGame(startArgs);

        float timeout = 10f;
        float elapsed = 0f;
        while (!startTask.IsCompleted && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!startTask.IsCompleted)
        {
            Debug.LogWarning("⏱ Timeout khi kết nối StartGame.");
            onComplete?.Invoke(false);
            yield break;
        }

        if (!runner.IsRunning)
        {
            Debug.LogWarning("⛔ Runner vẫn chưa chạy sau StartGame.");
            onComplete?.Invoke(false);
            yield break;
        }

        var startResult = startTask.Result;
        if (startResult.Ok)
        {
            Debug.Log("✅ Tham gia phòng thành công!");
            bool rpcReady = false;
            yield return StartCoroutine(LoadServerRPC(runner, rpc =>
            {
                rpcReady = rpc != null;
            }, 60f));

            if (rpcReady)
            {
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogWarning("⏰ Timeout khi chờ server RPC sau khi tham gia phòng bằng tên.");
                onComplete?.Invoke(false);
            }

        }
        else
        {
            Debug.LogWarning($"❌ Lỗi: {startResult.ShutdownReason}");
            onComplete?.Invoke(false);
        }
    }

    public IEnumerator ReconnectQuickMatch(string sessionName)
    {
        reconnectHandler ??= new QuickMatchReconnectHandler(this);
        yield return StartCoroutine(reconnectHandler.ReconnectQuickMatch(sessionName));
    }

    internal IEnumerator UnloadMenuSceneIfLoaded()
    {
        const string menuSceneName = "Menu";
        var menuScene = SceneManager.GetSceneByName(menuSceneName);

        if (!menuScene.IsValid() || !menuScene.isLoaded)
        {
            yield break;
        }

        var unloadOperation = SceneManager.UnloadSceneAsync(menuScene);
        unloadOperation ??= SceneManager.UnloadSceneAsync(menuSceneName);

        if (unloadOperation != null)
        {
            while (!unloadOperation.isDone)
            {
                yield return null;
            }
        }
    }

    internal void EnsureReconnectSceneActive()
    {
        if (serverRPC == null)
        {
            TryResolveServerRPC(true);
        }

        if (serverRPC == null)
        {
            Debug.Log("không tìm thấy serverRPC");
            return;
        }

        var roomSetting = serverRPC.GetRoomSetting();
        if (roomSetting.roomId == 0)
        {
            Debug.Log("không tìm thấy thông tin phòng");
            return;
        }

        string sceneName = roomSetting.gameScene.ToString();
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.Log("không tìm thấy sceneName");
            return;
        }

        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.EnsureSceneActive(sceneName);
        }
        else
        {
            StartCoroutine(UnloadMenuSceneIfLoaded());
        }
    }


    // Mở kết nối đến Photon Server và RPC
    //public NetworkRunner OpenConnectToPhotonServer()
    //{
    //    if (runner == null)
    //    {
    //        runner = gameObject.AddComponent<NetworkRunner>();           
    //        runner.ProvideInput = true;
    //        gameObject.AddComponent<RunnerSimulatePhysics3D>();
    //        Debug.Log("✅ Runner đã được khởi tạo");
    //    }

    //    return runner;
    //}

    public NetworkRunner OpenConnectToPhotonServer()
    {
        if (Instance == null)
        {
            Instance = FindObjectOfType<GameManagerNetWork>();
            if (Instance == null)
            {
                Debug.LogError("❌ Không tìm thấy GameManagerNetWork khi mở kết nối Photon.");
                return null;
            }

            if (Instance != this)
            {
                return Instance.OpenConnectToPhotonServer();
            }
        }

        manualShutdownRequested = false;

        EnsureRunnerHost();

        if (runnerHostObject == null)
        {
            Debug.LogError("❌ Không thể tạo runner host object.");
            return null;
        }

        if (runner == null)
        {
            runner = runnerHostObject.GetComponent<NetworkRunner>();
        }

        // Nếu runner tồn tại nhưng đã bị tắt (sau lỗi/thoát phòng) → đảm bảo trạng thái sạch
        if (runner != null && !runner.IsRunning)
        {
            ResetRunner();
        }

        // Nếu sau khi reset vẫn chưa có runner → tạo mới
        if (runner == null)
        {
            if (runnerHostObject == null)
            {
                Debug.LogWarning("⚠️ Runner host bị mất trước khi tạo runner. Thử tạo lại host.");
                EnsureRunnerHost();
            }

            if (runnerHostObject == null)
            {
                Debug.LogError("❌ Không thể tạo NetworkRunner vì runner host object đang null.");
                return null;
            }

            runner = runnerHostObject.AddComponent<NetworkRunner>();
            Debug.Log("✅ Runner đã được tạo mới");
        }

        if (runner == null)
        {
            Debug.LogError("❌ Tạo NetworkRunner thất bại (runner vẫn null).");
            return null;
        }

        runner.ProvideInput = true;

        // Đảm bảo handler input chỉ được gắn 1 lần
        var inputHandler = FindObjectOfType<NetworkInputHandler>();
        if (inputHandler == null)
        {
            GameObject go = new GameObject("NetworkInputHandler");
            inputHandler = go.AddComponent<NetworkInputHandler>();
            DontDestroyOnLoad(go);
        }

        PaperLegendFlickInputCollector.EnsureExists();

        runner.RemoveCallbacks(inputHandler);
        runner.AddCallbacks(inputHandler);

        runner.RemoveCallbacks(this);
        runner.AddCallbacks(this);

        // Cấu hình vật lý nếu có
        var runnerPhysics = runnerHostObject.GetComponent<RunnerSimulatePhysics3D>();
        if (runnerPhysics == null)
        {
            runnerPhysics = runnerHostObject.AddComponent<RunnerSimulatePhysics3D>();
            Debug.Log("ℹ️ [CLIENT] Bổ sung RunnerSimulatePhysics3D để hỗ trợ NetworkRigidbody.");
        }

        EnsureClientPhysicsSimulation(runnerPhysics);

        return runner;
    }

    private void EnsureRunnerHost()
    {
        if (this == null || transform == null)
        {
            runnerHostObject = null;
            return;
        }

        if (runnerHostObject == null)
        {
            var existingHost = transform.Find("NetworkRunnerHost");
            if (existingHost != null)
            {
                runnerHostObject = existingHost.gameObject;
            }
            else
            {
                var globalHost = GameObject.Find("NetworkRunnerHost");
                if (globalHost != null)
                {
                    runnerHostObject = globalHost;
                }
            }

            if (runnerHostObject == null)
            {
                runnerHostObject = new GameObject("NetworkRunnerHost");
            }

            runnerHostObject.transform.SetParent(transform);
           // DontDestroyOnLoad(runnerHostObject);
        }
        else if (runnerHostObject.transform.parent != transform)
        {
            runnerHostObject.transform.SetParent(transform);
        }

        // Xóa các component runner/scene manager/physics cũ còn sót lại trên GameManager
        var legacyRunner = GetComponent<NetworkRunner>();
        if (legacyRunner != null)
        {
            if (runner == legacyRunner)
            {
                runner = null;
            }
            Destroy(legacyRunner);
        }

        foreach (var sim in GetComponents<RunnerSimulatePhysics3D>())
        {
            Destroy(sim);
        }

        foreach (var sceneManager in GetComponents<NetworkSceneManagerDefault>())
        {
            Destroy(sceneManager);
        }
    }

    public bool TryResolveServerRPC(bool force = false)
    {
        if (serverRPC != null)
        {
            return true;
        }

        if (!force && Time.unscaledTime < nextServerRpcResolveTime)
        {
            return false;
        }

        nextServerRpcResolveTime = Time.unscaledTime + 1f;

        if (NetworkObjectManager.Instance != null)
        {
            serverRPC = NetworkObjectManager.Instance;
            return true;
        }

        var objs = GameObject.FindGameObjectsWithTag("NetworkManager");
        foreach (var obj in objs)
        {
            var rpc = obj.GetComponent<NetworkObjectManager>();
            if (rpc != null)
            {
                serverRPC = rpc;
                return true;
            }
        }

        return false;
    }

    private static void EnsureClientPhysicsSimulation(RunnerSimulatePhysics3D runnerPhysics)
    {
        if (runnerPhysics == null)
            return;

        if (runnerPhysics.ClientPhysicsSimulation == ClientPhysicsSimulation.Disabled)
        {
            runnerPhysics.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateForward;
            Debug.Log("ℹ️ [CLIENT] Bật dự đoán vật lý cho RunnerSimulatePhysics3D (SimulateForward).");
        }
    }

    private NetworkRunner GetRunnerFromHost()
    {
        if (runnerHostObject == null)
        {
            return null;
        }

        return runnerHostObject.GetComponent<NetworkRunner>();
    }

    internal NetworkSceneManagerDefault GetOrAddSceneManager()
    {
        EnsureRunnerHost();

        if (runnerHostObject == null)
        {
            Debug.LogError("❌ Không thể tạo NetworkSceneManagerDefault vì runner host không tồn tại.");
            return null;
        }

        var sceneManager = runnerHostObject.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
        {
            sceneManager = runnerHostObject.AddComponent<NetworkSceneManagerDefault>();
        }

        return sceneManager;
    }
    public void ResetRunner()
    {
        ResetRunner(false);
    }

    public void ResetRunner(bool waitForShutdown)
    {
        if (waitForShutdown)
        {
            if (resetRunnerCoroutine != null)
            {
                StopCoroutine(resetRunnerCoroutine);
            }

            resetRunnerCoroutine = StartCoroutine(ResetRunnerRoutine());
            return;
        }

        ResetRunnerInternal();
    }

    public IEnumerator RecreateRunnerForReconnect()
    {
        var existingRunner = runner != null ? runner : GetRunnerFromHost();
        if (existingRunner != null)
        {
            if (existingRunner.IsRunning)
            {
                var shutdownTask = existingRunner.Shutdown();
                while (!shutdownTask.IsCompleted)
                {
                    yield return null;
                }
            }

            ResetRunnerInternal();

            // Chờ Unity hủy object xong trước khi tạo runner mới,
            // tránh lỗi "NetworkRunner should not be reused".
            yield return null;
        }

        runner = OpenConnectToPhotonServer();
    }

    private IEnumerator ResetRunnerRoutine()
    {
        var existingRunner = runner != null ? runner : GetRunnerFromHost();
        if (existingRunner != null && existingRunner.IsRunning)
        {
            var shutdownTask = existingRunner.Shutdown();
            while (!shutdownTask.IsCompleted)
            {
                yield return null;
            }
        }

        ResetRunnerInternal();
        resetRunnerCoroutine = null;
    }

    private void ResetRunnerInternal()
    {
        EnsureRunnerHost();

        var existingRunner = runner != null ? runner : GetRunnerFromHost();
        if (existingRunner != null)
        {
            if (existingRunner.IsRunning)
            {
                existingRunner.Shutdown();
            }

            var inputHandler = FindObjectOfType<NetworkInputHandler>();
            if (inputHandler != null)
            {
                existingRunner.RemoveCallbacks(inputHandler);
            }

            existingRunner.RemoveCallbacks(this);
        }

        if (runnerHostObject != null)
        {
            Destroy(runnerHostObject);
            runnerHostObject = null;
        }
        else if (existingRunner != null)
        {
            Destroy(existingRunner);
        }

        runner = null;
        serverRPC = null;

        Debug.Log("✅ Runner đã được reset và sẽ được tạo mới");
    }

    public void SetCurrentRoomState(int roomId, bool isHost = false)
    {
        currentRoomId = roomId;
        currentIsHost = isHost;
        if (roomId > 0)
        {
            currentQuickMatchId = null;
            currentQuickMatchResultId = null;
        }
    }
    public void SpawnServerRPC(Action<NetworkObjectManager> onReady)
    {
        runner.Spawn(networkManagerPrefab, Vector3.zero, Quaternion.identity, runner.LocalPlayer);
        StartCoroutine(LoadServerRPC(runner, onReady));
    }

    internal IEnumerator LoadServerRPC(NetworkRunner runner, Action<NetworkObjectManager> onReady, float timeoutSeconds = 30f, float pollIntervalSeconds = 0.5f)
    {
        float elapsed = 0f;

        while (elapsed < timeoutSeconds)
        {
            if (runner == null || runner.IsShutdown || !runner.IsRunning)
            {
                Debug.LogWarning("⛔ Runner không còn hoạt động trong khi chờ server RPC.");
                break;
            }

            // Check singleton first (set in NetworkObjectManager.Awake)
            if (NetworkObjectManager.Instance != null && NetworkObjectManager.Instance.IsNetworkStateReady)
            {
                serverRPC = NetworkObjectManager.Instance;
                Debug.Log("✅ Đã load server RPC thành công (via Instance)");
                onReady?.Invoke(serverRPC);
                yield break;
            }

            // Fallback: tag search
            var objs = GameObject.FindGameObjectsWithTag("NetworkManager");
            foreach (var obj in objs)
            {
                var rpc = obj.GetComponent<NetworkObjectManager>();
                if (rpc != null && rpc.IsNetworkStateReady)
                {
                    serverRPC = rpc;
                    Debug.Log("✅ Đã load server RPC thành công (via tag)");
                    onReady?.Invoke(serverRPC);
                    yield break;
                }
            }

            // Also try FindObjectsByType as last resort (same approach as QuickMatchServer search)
            var managers = FindObjectsByType<NetworkObjectManager>(FindObjectsSortMode.None);
            foreach (var mgr in managers)
            {
                if (mgr != null && mgr.IsNetworkStateReady)
                {
                    serverRPC = mgr;
                    Debug.Log("✅ Đã load server RPC thành công (via FindObjectsOfType)");
                    onReady?.Invoke(serverRPC);
                    yield break;
                }
            }

            yield return new WaitForSeconds(pollIntervalSeconds);
            elapsed += pollIntervalSeconds;
        }

        Debug.LogWarning($"⏰ Timeout {timeoutSeconds}s khi chờ NetworkObjectManager sẵn sàng.");
        onReady?.Invoke(null);
    }

    // Dừng coroutine và đóng kết nối
    public void CloseConnectToRunner()
    {
        Debug.Log($"🔌 [CLIENT] CloseConnectToRunner được gọi. Runner active: {IsRunnerActive}, manualShutdownRequested: {manualShutdownRequested}.");
        QuickMatchClient.Instance?.ClearActiveResultMatch();

        if (manualShutdownCoroutine != null)
        {
            Debug.Log("ℹ️ [CLIENT] CloseConnectToRunner bị bỏ qua vì manualShutdownCoroutine đang chạy.");
            return;
        }

        manualShutdownRequested = true;
        // Dừng coroutine nếu đang chạy
        if (loadServerRPCCoroutine != null)
        {
            StopCoroutine(loadServerRPCCoroutine);
            loadServerRPCCoroutine = null; // Đảm bảo ngừng coroutine
            Debug.Log("Đã dừng coroutine load server RPC.");
        }

        manualShutdownCoroutine = StartCoroutine(ShutdownRunnerRoutine());
    }

    private bool isReconnecting = false;

    public bool IsReconnecting => isReconnecting;

    public bool ManualShutdownRequested => manualShutdownRequested;

    public bool WillAttemptReconnect => !manualShutdownRequested && !isReconnecting
        && (currentRoomId > 0 || !string.IsNullOrEmpty(currentQuickMatchId));

    public bool IsRunnerActive => runner != null && runner.IsRunning && !runner.IsShutdown;

    public bool ShouldSuppressExpectedDisconnectPopup()
    {
        return manualShutdownRequested || HasGameOverResults() || IsGameStatusEndGame();
    }

    private bool HasGameOverResults()
    {
        return GameOverManager.Instance != null && GameOverManager.Instance.HasGameOverResults;
    }

    private bool IsGameStatusEndGame()
    {
        return TryReadStatusLoading(out var status) && status == StatusLoadingGame.EndGame;
    }

    private bool TryReadStatusLoading(out StatusLoadingGame status)
    {
        status = StatusLoadingGame.None;

        try
        {
            if (serverRPC != null && serverRPC.TryGetStatusLoading(out status))
            {
                return true;
            }

            var manager = NetworkObjectManager.Instance;
            if (manager != null && manager.TryGetStatusLoading(out status))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Disconnect] Không đọc được trạng thái game: {ex.Message}");
        }

        return false;
    }

    private void ClearMatchSessionTracking()
    {
        currentQuickMatchId = null;
        currentQuickMatchResultId = null;
        currentRoomId = -1;
    }

    public bool CheckServerConnection()
    {
        //if (serverRPC == null || runner == null || !runner.IsRunning)
        //{
        //    if (!QuickMatchHandler.Instance.SuppressNetworkPopup)
        //        Debug.Log("runner chưa kết nối");
        //    //if (!isReconnecting)
        //    //    StartCoroutine(ReconnectRoutine());
        //    return false;
        //}
        return true;
    }

    public bool ValidateNetworkObjects()
    {
        if (runner == null || serverRPC == null)
        {
           // if (!QuickMatchHandler.Instance.SuppressNetworkPopup)
                PopupHelper.Instance.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_network_false"));
            return false;
        }
        return true;
    }

    private bool ShouldAttemptReconnect(bool forceCheck = false)
    {
        if (manualShutdownRequested || isReconnecting)
            return false;

        bool hasSession = currentRoomId > 0 || !string.IsNullOrEmpty(currentQuickMatchId);
        if (!hasSession)
            return false;

        if (forceCheck)
            return true;

        if (runner == null || !runner.IsRunning || runner.IsShutdown)
            return true;

        if (serverRPC == null || serverRPC.Object == null)
            return true;

        return false;
    }

    private IEnumerator EnsureReconnectAfterResume()
    {
        yield return new WaitForSeconds(0.5f);

        if (ShouldAttemptReconnect())
        {
            yield return StartCoroutine(ReconnectRoutine());
        }
    }

    public IEnumerator WaitForStableConnection(float maxPing = 0.5f, float timeoutSeconds = 15f)
    {
        if (runner == null || !runner.IsRunning)
            yield break;

        float elapsed = 0f;
        PlayerRef localPlayer = runner.LocalPlayer;

        while (elapsed < timeoutSeconds)
        {
            double rtt = runner.GetPlayerRtt(localPlayer);
            if (rtt >= 0f && rtt <= maxPing)
                yield break;

            yield return new WaitForSeconds(1f);
            elapsed += 1f;
        }

        Debug.LogWarning("⛔ Timeout waiting for stable connection");
        //if (!QuickMatchHandler.Instance.SuppressNetworkPopup)
            //PopupHelper.Instance.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_network_false"));
    }

    private void TryReconnectIfNeeded(string context, bool forceCheck = false)
    {
        if (ShouldAttemptReconnect(forceCheck))
        {
            Debug.LogWarning($"{context} → kích hoạt quy trình reconnect.");
            StartCoroutine(ReconnectRoutine());
        }
    }

    private IEnumerator ReconnectRoutine()
    {
        const float reconnectTimeoutSeconds = 90f;
        isReconnecting = true;

        bool finished = false;
        var flowRoutine = StartCoroutine(ReconnectFlow(() => finished = true));

        float elapsed = 0f;
        while (!finished && elapsed < reconnectTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!finished)
        {
            if (flowRoutine != null)
            {
                StopCoroutine(flowRoutine);
            }
            HandleReconnectTimeout();
            yield break;
        }

        isReconnecting = false;
    }

    private IEnumerator ReconnectFlow(Action onComplete)
    {
        StartReconnectLoading();
        OpenConnectToPhotonServer();
        UpdateReconnectLoadingProgress(0.2f, "Đang kết nối lại...");

        if (currentRoomId > 0)
        {
            if (currentIsHost)
            {
                UpdateReconnectLoadingProgress(0.4f, "Đang khôi phục phòng...");
                yield return StartCoroutine(CreateRoomAndConnectCoroutine(currentRoomId, null));
            }
            else
            {
                UpdateReconnectLoadingProgress(0.4f, "Đang vào lại phòng...");
                yield return StartCoroutine(JoinRoomAndConnectCoroutine(currentRoomId, null));
            }
        }
        else if (!string.IsNullOrEmpty(currentQuickMatchId))
        {
            UpdateReconnectLoadingProgress(0.4f, "Đang vào lại quick match...");
            yield return StartCoroutine(ReconnectQuickMatch(currentQuickMatchId));
        }

        yield return new WaitForSeconds(2f);
        UpdateReconnectLoadingProgress(1f, "Hoàn tất");
        FinishReconnectLoading();
        onComplete?.Invoke();
    }

    private void HandleReconnectTimeout()
    {
        FinishReconnectLoading();
        isReconnecting = false;

        string message = "Mạng không ổn định. Vui lòng thử lại.";
        if (LocalizationManager.Instance != null)
        {
            string localized = LocalizationManager.Instance.GetText("noti_network_false");
            if (!string.IsNullOrWhiteSpace(localized))
            {
                message = localized;
            }
        }

        if (PopupHelper.Instance != null)
        {
            PopupHelper.Instance.ShowPopupOut(message, PopupHelper.Instance.ExitButton);
        }
        else
        {
            Debug.LogWarning(message);
            LoadingManager.LoadScene("Menu");
        }
    }

    private bool useQuickReconnectOverlay;

    private void StartReconnectLoading()
    {
        if (LoadingManager.Instance == null)
        {
            return;
        }

        useQuickReconnectOverlay = ShouldUseQuickReconnectOverlay();
        LoadingManager.Instance.ShowReconnectLoading("Mạng không ổn định đang tiến hành kết nối lại");

        if (!useQuickReconnectOverlay)
        {
            LoadingManager.Instance.StartLoadingLocalPersistent();
            LoadingManager.Instance.UpdateProgress(0.05f, "Đang chuẩn bị kết nối lại...");
        }
    }

    private void UpdateReconnectLoadingProgress(float progress, string text)
    {
        if (LoadingManager.Instance == null)
        {
            return;
        }

        if (useQuickReconnectOverlay)
        {
            return;
        }

        LoadingManager.Instance.UpdateProgress(progress, text);
    }

    private void FinishReconnectLoading()
    {
        if (LoadingManager.Instance == null)
        {
            return;
        }

        if (!useQuickReconnectOverlay)
        {
            LoadingManager.Instance.FinishLoading();
        }
        LoadingManager.Instance.HideReconnectLoading();
    }

    private void ShowImmediateReconnectLoading(string message)
    {
        if (LoadingManager.Instance == null)
        {
            return;
        }

        if (LoadingManager.Instance.UILoadingScreenPrefab != null)
        {
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            LoadingManager.Instance.ShowReconnectLoading(message);
        }
    }

    private bool ShouldUseQuickReconnectOverlay()
    {
        if (string.IsNullOrEmpty(currentQuickMatchId))
        {
            return false;
        }

        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return false;
        }

        var sceneName = activeScene.name;
        return !string.IsNullOrEmpty(sceneName)
            && !string.Equals(sceneName, "Menu", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sceneName, "PersistentScene", StringComparison.OrdinalIgnoreCase);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }


    // Callback khi scene được tải map game xong
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene == null || string.IsNullOrEmpty(scene.name))
        {
            return;
        }

        if (serverRPC != null)
        {
            var RoomSetting = serverRPC.GetRoomSetting();
            if (scene.name == RoomSetting.gameScene.ToString())
            {  
                Debug.Log("test client tải xong map1");
                serverRPC.RpcClientSetupComplete();
            }

            return;
        }

        if (!pendingClientSetupScenes.Add(scene.name))
        {
            return;
        }

        if (clientSetupNotifyRoutine != null)
        {
            StopCoroutine(clientSetupNotifyRoutine);
        }

        clientSetupNotifyRoutine = StartCoroutine(NotifyClientSetupWhenServerReady(scene.name));
    }

    private IEnumerator NotifyClientSetupWhenServerReady(string sceneName)
    {
        const float timeoutSeconds = 10f;
        float elapsed = 0f;

        while (elapsed < timeoutSeconds)
        {
            if (!TryResolveServerRPC(true))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
                continue;
            }

            if (serverRPC != null && sceneName == serverRPC.GetRoomSetting().gameScene.ToString())
            {
                Debug.Log($"test client tải xong map (late bind): {sceneName}");
                serverRPC.RpcClientSetupComplete();
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        pendingClientSetupScenes.Remove(sceneName);
        clientSetupNotifyRoutine = null;
    }
    //public void CheckLeaveRoom()
    //{
    //    if (!runner.IsRunning || runner.IsShutdown)
    //    {
    //        Debug.Log("🔌 Mất kết nối hoặc runner đã shutdown → tự động rời phòng.");
    //        runner.Shutdown();
    //    }
    //}
    #region [======================== GAME PLAY FOR QUICK MATCH ==========]

    #endregion
    #region [======================== GAME PLAY=======================]
    //public IEnumerator StartGameLoading(RpgRoomModel settings)
    //{
    //    if (!ValidateNetworkObjects())
    //        yield break;
    //    // Bước 4: Gửi settings phòng
    //        serverRPC.RpcLoadSettingRoom(settings);
    //        serverRPC.currentPlayerIndex = 0;
    //        // Bước 5: Gọi toàn bộ client load scene
    //        serverRPC.RpcLoadMapGame();
    //        // Bước 2: Gọi API và đợi kết quả
    //        PlayerInfoStruct[] playerList = null;
    //        yield return StartCoroutine(RunTask(GetListPlayerGame(settings.roomId), result => playerList = result));

    //        if (playerList == null || playerList.Length == 0)
    //        {
    //            Debug.LogError("❌ Không có người chơi nào trong phòng.");
    //            yield break;
    //        }
    //        else
    //        Debug.Log("✅ Đã tải xong list player");

    //    // Bước 3: Gửi danh sách player về tất cả client
    //    //serverRPC.players.Clear();
    //    foreach (var player in playerList)
    //    {
    //        serverRPC.RpcLoadSettingsPlayer(player);
    //        //Đợi 5s tải mạng
    //      //  yield return new WaitForSeconds(5f);
    //    }
    //    // Bước 8: Chờ host setup hoàn tất

    //     yield return StartCoroutine(WaitForAllClientsReady(playerList.Length, settings));


    //    // Bước 9: Chạy game
    //    // GameSessionNetWork_Host.Instance.PlayGameMain();

    //}
    //public IEnumerator WaitForAllClientsReady(int expectedClientCount, RpgRoomModel settings)
    //{
    //    if (!ValidateNetworkObjects())
    //        yield break;
    //    float timeout = 180f;
    //    float elapsed = 0f;

    //    while (serverRPC.GetClientReady() < expectedClientCount && elapsed < timeout)
    //    {
    //       // Debug.Log($"🌀 [WAITING] ClientsReady = {serverRPC.GetClientReady()} / {expectedClientCount} | Elapsed: {elapsed:F1}s");
    //        elapsed += Time.deltaTime;
    //        yield return null;
    //    }

    //    if (serverRPC.GetClientReady() >= expectedClientCount)
    //    {
    //        yield return new WaitForSeconds(3f);
    //        Debug.Log($"🌀 [WAITING] ClientsReady = {serverRPC.GetClientReady()} / {expectedClientCount} | Elapsed: {elapsed:F1}s");
    //        Debug.Log("✅ Tất cả client đã tải xong Map game. tiền hành cài đặt thông số...");
    //        // Bước 7: Host setup gameplay
    //       // yield return StartCoroutine(GameSessionNetWork_Host.Instance.SettingGameHost(settings,runner,serverRPC));
    //        yield return null; // Thoát hàm ngay lập tức
    //    }
    //    else
    //    {
    //        Debug.LogWarning($"⛔ Timeout sau {elapsed:F1}s: Không đủ client ready ({serverRPC.GetClientReady()} / {expectedClientCount}) - Có thể đã mất kết nối.");
    //       // ShowPopupExitGame("Không có người chơi sẵn sàng !");
    //        yield break;
    //    }
    //}

    //  void ShowPopupExitGame(string mess)
    //{
    //    PopupHelper.Instance.ShowPopup(mess, () => {
    //        ExitButton();
    //    });
    //}
    //  void ExitButton()
    //{
    //    Time.timeScale = 1;
    //    DayNightWeatherManager.Instance?.StopEnvironmentSound();
    //    LoadingManager.LoadScene("Menu");
    //}

    //public IEnumerator WaitForHostReady(int expectedClientCount, float timeoutSeconds = 5f)
    //{
    //    if (!ValidateNetworkObjects())
    //        yield break;
    //    float timer = timeoutSeconds;

    //    while (serverRPC.HotsReady < expectedClientCount && timer > 0f)
    //    {
    //        timer -= Time.deltaTime;
    //        Debug.Log($"⏳ [WAITING] ClientsReady = {serverRPC.HotsReady}, Expected = {expectedClientCount}, Time left: {timer:F1}s");
    //        yield return null;
    //    }

    //    if (serverRPC.HotsReady < expectedClientCount)
    //    {
    //        Debug.LogWarning("⛔ Timeout: Không đủ host ready.");
    //        yield break;
    //    }

    //    Debug.Log("✅ Host ready.");
    //}

    //public static IEnumerator RunTask<T>(Task<T> task, Action<T> onComplete)
    //{
    //    while (!task.IsCompleted)
    //        yield return null;

    //    if (task.IsFaulted)
    //        Debug.LogError("❌ Task lỗi: " + task.Exception);

    //    onComplete?.Invoke(task.Result);
    //}



    //async Task<PlayerInfoStruct[]> GetListPlayerGame(int roomId)
    //{
    //    try
    //    {
    //        List<UserRoom> result = await APIManager.Instance.GetUsersInRoomAsync(roomId);

    //        if (result == null || result.Count == 0)
    //        {
    //            Debug.LogError("❌ Không thể lấy danh sách người chơi hoặc danh sách trống!");
    //            return Array.Empty<PlayerInfoStruct>();
    //        }

    //        PlayerInfoStruct[] players = result.Select(x => new PlayerInfoStruct
    //        {
    //            playerId = x.player.id,
    //            level = x.player.Level,
    //            fullname = x.player.PlayerName,
    //            playerbody = (PlayerBodyType)x.player.Body,
    //            score = 0,
    //            combo = 0,
    //            ball = (ItemCode)x.player.Ball,
    //            RingBall = x.player.RingBall,
    //            avatar = 1,
    //            exactRatio = 1,
    //            powerForce = x.player.Level,
    //            statusPlayer = StatusPlayer.ShootExam,
    //            distance = 0,
    //            isDestroy = false,
    //            isHolding = true,
    //            //FingerPosition = Vector3.zero,
    //            turnOrder = 0,
    //        }).ToArray();

    //        return players;
    //    }
    //    catch (Exception e)
    //    {
    //        Debug.LogError($"❌ Lỗi khi lấy danh sách người chơi: {e.Message}");
    //        return Array.Empty<PlayerInfoStruct>();
    //    }
    //}

    // INetworkRunnerCallbacks implementation
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        if (ShouldSuppressExpectedDisconnectPopup())
        {
            Debug.Log($"[Disconnect] Expected disconnect, skipping reconnect/error popup. Reason: {reason}");
            ClearMatchSessionTracking();
            return;
        }

        if (manualShutdownRequested)
        {
            Debug.Log($"[Disconnect] Manual shutdown in progress, skipping reconnect. Reason: {reason}");
            return;
        }

        if (reason == NetDisconnectReason.Requested || reason == NetDisconnectReason.ByRemote)
        {
            Debug.LogWarning($"[Disconnect] Server closed the connection: {reason}");
            return;
        }

        string message;

        switch (reason)
        {
            case NetDisconnectReason.Timeout:
                message = "Mất kết nối do timeout. Kiểm tra kết nối mạng.";
                break;
            default:
                message = $"Ngắt kết nối: {reason}";
                break;
        }

        Debug.LogWarning($"[Disconnect] Reason: {reason}");
        ShowImmediateReconnectLoading(message);
        //PopupHelper.Instance.ShowPopup(message, ExitButton);
        TryReconnectIfNeeded($"[Disconnect] {reason}", true);
    }



    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (ShouldSuppressExpectedDisconnectPopup())
        {
            Debug.Log($"[Shutdown] Expected shutdown, skipping reconnect/error popup. Reason: {shutdownReason}");
            ClearMatchSessionTracking();
            return;
        }

        if (manualShutdownRequested)
        {
            Debug.Log($"[Shutdown] Manual shutdown completed: {shutdownReason}");
            return;
        }

        if (shutdownReason == ShutdownReason.DisconnectedByPluginLogic || shutdownReason == ShutdownReason.GameClosed)
        {
            Debug.Log("Server đã đóng phòng một cách chủ động.");

            // If game over results already shown (via RPC), just clean up
            if (HasGameOverResults())
            {
                ClearMatchSessionTracking();
                return;
            }

            if (ShouldWaitForGameOverResultsOnShutdown())
            {
                // RPC failed during gameplay - wait for WebSocket match:finished backup.
                StartCoroutine(WaitForGameOverResultsViaSocketFallback());
            }
            else
            {
                StartCoroutine(HandleRoomClosedBeforeGameplayRoutine(shutdownReason));
            }

            return;
        }

        Debug.LogWarning($"[Shutdown] Runner dừng do: {shutdownReason}");
        ShowImmediateReconnectLoading("Mất kết nối. Đang thử kết nối lại...");
        TryReconnectIfNeeded($"[Shutdown] {shutdownReason}", true);
    }

    private bool ShouldWaitForGameOverResultsOnShutdown()
    {
        if (GameOverManager.Instance != null && GameOverManager.Instance.HasGameOverResults)
        {
            return false;
        }

        StatusLoadingGame status;
        try
        {
            if (serverRPC == null || !serverRPC.TryGetStatusLoading(out status))
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[QuickMatch] Không đọc được trạng thái phòng khi shutdown: {ex.Message}");
            return false;
        }

        switch (status)
        {
            case StatusLoadingGame.isExam:
            case StatusLoadingGame.StartTurn:
            case StatusLoadingGame.ContinueTurn:
            case StatusLoadingGame.NextTurn:
            case StatusLoadingGame.EndTurn:
            case StatusLoadingGame.EndGame:
                return true;
            default:
                return false;
        }
    }

    private IEnumerator HandleRoomClosedBeforeGameplayRoutine(ShutdownReason shutdownReason)
    {
        Debug.LogWarning($"[QuickMatch] Phòng bị đóng trước khi gameplay bắt đầu. Lý do: {shutdownReason}");

        yield return null;

        Time.timeScale = 1f;
        QuickMatchClient.Instance?.HandleMatchRoomClosedBeforeGameplay();
        ResetRunnerInternal();
        currentQuickMatchId = null;
        currentQuickMatchResultId = null;
        currentRoomId = -1;

        var message = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText("noti_quickmatch_start_failed")
            : string.Empty;

        if (string.IsNullOrEmpty(message))
        {
            message = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText("noti_shutdown_host_closed")
                : "Match room was closed.";
        }

        ClientGameplayBridge.Popup.ShowPopupConfirm(message);
    }

    private IEnumerator WaitForGameOverResultsViaSocketFallback()
    {
        Debug.Log("⏳ [CLIENT] RPC game over không nhận được. Chờ kết quả từ WebSocket backup...");

        // Defer runner cleanup to next frame (we're still inside OnShutdown callback)
        yield return null;
        ResetRunnerInternal();

        float maxWait = 20f;
        float waited = 0f;
        bool apiAttempted = false;

        while (waited < maxWait)
        {
            if (GameOverManager.Instance != null && GameOverManager.Instance.HasGameOverResults)
            {
                Debug.Log("✅ [CLIENT] Đã nhận kết quả game over từ WebSocket/API backup.");
                currentQuickMatchId = null;
                currentQuickMatchResultId = null;
                currentRoomId = -1;
                yield break;
            }

            // After 5s of passive WebSocket wait, try fetching results from API
            if (!apiAttempted && waited >= 5f)
            {
                apiAttempted = true;
                Debug.Log("📊 [CLIENT] WebSocket backup chưa nhận được. Thử lấy kết quả từ API...");
                yield return TryFetchGameOverResultsFromAPI();

                if (GameOverManager.Instance != null && GameOverManager.Instance.HasGameOverResults)
                {
                    Debug.Log("✅ [CLIENT] Đã nhận kết quả game over từ API fallback.");
                    currentQuickMatchId = null;
                    currentQuickMatchResultId = null;
                    currentRoomId = -1;
                    yield break;
                }
            }

            yield return new WaitForSecondsRealtime(0.5f);
            waited += 0.5f;
        }

        Debug.LogWarning("⚠️ [CLIENT] Không nhận được kết quả game over sau 20 giây. Quay về menu.");
        currentQuickMatchId = null;
        currentQuickMatchResultId = null;
        currentRoomId = -1;
        ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_shutdown_host_closed"));
    }

    private IEnumerator TryFetchGameOverResultsFromAPI()
    {
        if (APIManager.Instance == null || loginUserModel == null)
            yield break;

        int playerId = loginUserModel.UserId;
        if (playerId <= 0)
            yield break;

        // Step 1: Get latest match for this player
        List<PlayerMatchHistory> latestMatches = null;
        var getHistoryTask = APIManager.Instance.GetPlayerMatchHistoriesAsync(playerId, 1, 1);
        yield return StartCoroutine(APIManager.Instance.RunTask(getHistoryTask, result => latestMatches = result));

        if (latestMatches == null || latestMatches.Count == 0)
        {
            Debug.Log("📊 [CLIENT] API fallback: Không tìm thấy lịch sử trận đấu.");
            yield break;
        }

        var latest = latestMatches[0];

        // Check if this match just ended (within last 3 minutes)
        if (!string.IsNullOrEmpty(latest.createdAt))
        {
            if (System.DateTime.TryParse(latest.createdAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out System.DateTime created))
            {
                double minutesAgo = (System.DateTime.UtcNow - created.ToUniversalTime()).TotalMinutes;
                if (minutesAgo > 3)
                {
                    Debug.Log($"📊 [CLIENT] API fallback: Trận gần nhất đã quá cũ ({minutesAgo:F1} phút trước).");
                    yield break;
                }
            }
        }

        if (string.IsNullOrEmpty(latest.transno))
        {
            Debug.Log("📊 [CLIENT] API fallback: Không có transno.");
            yield break;
        }

        // Step 2: Get ALL players' results by transNo
        List<PlayerMatchHistory> allResults = null;
        var getByTransNoTask = APIManager.Instance.GetMatchHistoriesByTransNoAsync(latest.transno);
        yield return StartCoroutine(APIManager.Instance.RunTask(getByTransNoTask, result => allResults = result));

        if (allResults == null || allResults.Count == 0)
        {
            Debug.Log("📊 [CLIENT] API fallback: Không lấy được kết quả theo transno.");
            yield break;
        }

        // Step 3: Convert PlayerMatchHistory → OverGameRequest
        var overGameResults = new List<OverGameRequest>();
        foreach (var h in allResults)
        {
            overGameResults.Add(new OverGameRequest
            {
                playerId = h.playerId,
                tunrOrder = h.turnOrder,
                typeMatchGid = h.typeMatchGid,
                StatusWin = h.statusWin,
                rounds = h.rounds,
                MapGame = h.mapGame,
                MaxPlayer = h.maxPlayer,
                marbBet = h.marbBet,
                marblesWon = h.marblesWon,
                marblesLost = h.marblesLost,
                expGained = h.expGained,
                playerName = h.player != null ? h.player.PlayerName : "",
                description = h.description,
                avatarUrl = ""
            });
        }

        if (overGameResults.Count > 0 && GameOverManager.Instance != null && !GameOverManager.Instance.HasGameOverResults)
        {
            Debug.Log($"📊 [CLIENT] API fallback thành công! Đã lấy {overGameResults.Count} kết quả game over.");
            ClientGameplayBridge.Match.ShowGameOverResults(overGameResults);
        }
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        if (manualShutdownRequested)
        {
            return;
        }

        ShowImmediateReconnectLoading("Không thể kết nối Photon. Đang thử kết nối lại...");
        TryReconnectIfNeeded($"[ConnectFailed] {reason}", true);
    }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("⚠️ Host migration detected - reinitializing runner");
        //StartCoroutine(HandleHostMigration(hostMigrationToken));
    }

    //private IEnumerator HandleHostMigration(HostMigrationToken hostMigrationToken)
    //{
    //    // Clean up the current runner and create a new one
    //    CloseConnectToRunner();

    //    runner = OpenConnectToPhotonServer();

    //    var sceneManager = GetOrAddSceneManager();
    //    if (sceneManager == null)
    //    {
    //        onComplete?.Invoke(false);
    //        yield break;
    //    }

    //    var startArgs = new StartGameArgs
    //    {
    //        HostMigrationToken = hostMigrationToken,
    //        GameMode = hostMigrationToken.GameMode,
    //        SceneManager = sceneManager,
    //        HostMigrationResume = FinishHostMigration
    //    };

    //    var task = runner.StartGame(startArgs);
    //    while (!task.IsCompleted)
    //        yield return null;

    //    if (task.Result.Ok)
    //    {
    //        Debug.Log("✅ Host migration completed");
    //        // reload serverRPC reference
    //        var obj = FindObjectOfType<NetworkObjectManager>();
    //        if (obj != null)
    //            serverRPC = obj;
    //    }
    //    else
    //    {
    //        Debug.LogError($"❌ Host migration failed: {task.Result.ShutdownReason}");
    //    }
    //}

    private IEnumerator ShutdownRunnerRoutine()
    {
        // Chờ trước khi shutdown runner.
        // Lý do: nếu CloseConnectToRunner được gọi từ trong Fusion RPC handler
        // (ví dụ: RpcShowOverGameResult → ShowGameOverResults → CloseConnectToRunner),
        // thì các RPC đã queue trong tick hiện tại (ACK GameOver, ReadyToDisconnect)
        // cần được flush trước khi runner bị shutdown.
        // Dùng WaitForSecondsRealtime vì Time.timeScale có thể = 0 (ShowGameOverResults),
        // nếu dùng yield return null thì Fusion không có thêm tick nào để flush RPC.
        yield return new WaitForSecondsRealtime(0.3f);

        var existingRunner = runner != null ? runner : GetRunnerFromHost();
        if (existingRunner != null && existingRunner.IsRunning)
        {
            var shutdownTask = existingRunner.Shutdown();
            while (!shutdownTask.IsCompleted)
            {
                yield return null;
            }
        }

        ResetRunnerInternal();
        manualShutdownCoroutine = null;
    }

    //private void FinishHostMigration(NetworkRunner resumedRunner)
    //{
    //    // Restore game state on network behaviours that need it
    //    foreach (var no in resumedRunner.GetResumeSnapshotNetworkObjects())
    //    {
    //        foreach (var behaviour in no.GetComponents<NetworkBehaviour>())
    //        {
    //            if (behaviour is IAfterHostMigration after)
    //            {
    //                after.AfterHostMigration();
    //            }
    //        }
    //    }
    //}


    #endregion
}
