using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.SceneManagement;
using Fusion;
using ExitGames.Client.Photon;
using UnityEngine.EventSystems;
using UnityEngine.Animations.Rigging;
using Unity.VisualScripting;
using UnityEngine.Networking;

public class GameInitializer : MonoBehaviour
{
    public static GameInitializer Instance;
    [Header("SYSTEM CONFIG")]
    [SerializeField, Tooltip("Prefab model nhân vật hiển thị ở client")] public GameObject PlayerModelVisual;
    [SerializeField, Tooltip("Prefab model viên bi hiển thị ở client")] public GameObject BallModelVisual;
    [SerializeField, Tooltip("Mesh viên bi sứt nhẹ")] public Mesh BallModelVisualChipped;
    [SerializeField, Tooltip("Mesh viên bi nứt")] public Mesh BallModelVisualCracked;
    [SerializeField, Tooltip("Mesh viên bi móp nặng")] public Mesh BallModelVisualShattered;
    public GameObject GameSessionOffline;
    public GameObject GameOfflineUI;
    [Header("Physics Material Config")]
    [SerializeField] private PhysicsMaterial groundPhysicsMaterial;
    public PhysicsMaterial GroundPhysicsMaterial => groundPhysicsMaterial;
    [SerializeField] private PhysicsMaterial ballPhysicsMaterial;
    public PhysicsMaterial BallPhysicsMaterial => ballPhysicsMaterial;
    [Header("Impact VFX")]
    [SerializeField] private GameObject heavyImpactVfxPrefab;
    public GameObject HeavyImpactVfxPrefab => heavyImpactVfxPrefab;
    [SerializeField] private GameObject waterSplashVfxPrefab;
    public GameObject WaterSplashVfxPrefab => waterSplashVfxPrefab;
 
    [Header("Server Initializer")]
    //[SerializeField]
    //private GameServerInitializer serverInitializer;
    [Header("Aim Constraint Config")]
    public Vector3 mirrorOffset = new Vector3(0f, 0f, -1f);

    private static bool _initialDataPrepared;
    private static bool _languagesLoaded;
    private static bool _isBootstrapping;
    private static bool _noInternetPopupShown;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        _initialDataPrepared = false;
        _languagesLoaded = false;
        _isBootstrapping = false;
        _noInternetPopupShown = false;
    }

    public static bool TryGetSkillLevelVfxColor(int level, out Color color)
    {
        if (level >= 10)
        {
            color = Color.red;
            return true;
        }

        if (level >= 8)
        {
            color = Color.yellow;
            return true;
        }

        if (level >= 5)
        {
            color = Color.green;
            return true;
        }

        color = Color.clear;
        return false;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        //if (serverInitializer == null)
        //{
        //    serverInitializer = GetComponent<GameServerInitializer>();
        //}
        DOTween.SetTweensCapacity(3000, 500);
    }

    private void Start()
    {
        //Đây là hàm đầu tiên sẽ được gọi lúc mở game chạy
        if (!_isBootstrapping)
        {
            _isBootstrapping = true;
            StartCoroutine(BootstrapRoutine());
        }
    }

    private IEnumerator BootstrapRoutine()
    {
        Debug.Log($"[GameInitializer] BootstrapRoutine bắt đầu ở scene '{SceneManager.GetActiveScene().name}'.");
        yield return WaitForLoadingManagerReady();

        if (!LoadingManager.TryGetInstance(out var loadingManager) || !loadingManager.isActiveAndEnabled)
        {
            Debug.LogWarning($"GameInitializer: LoadingManager chưa sẵn sàng, bỏ qua bootstrap hiện tại. {LoadingManager.GetDiagnosticsSummary()}");
            _isBootstrapping = false;
            yield break;
        }

        Debug.Log($"[GameInitializer] LoadingManager đã sẵn sàng: {LoadingManager.GetDiagnosticsSummary()}");
        loadingManager.StartLoadingLocal();
        _isBootstrapping = true;

        yield return EnsureManagersReady();

        // Check internet thật sự (ping server) trước khi làm bất cứ gì
        bool hasInternet = false;
        yield return CheckInternetConnectivity(result => hasInternet = result);
        if (!hasInternet)
        {
            _isBootstrapping = false;
            yield break;
        }



        if (!EnsureInternetAvailable())
        {
            _isBootstrapping = false;
            yield break;
        }

      

        if (!_initialDataPrepared)
        {
            yield return StartCoroutine(AddressablesHelper.DownloadInitialData(progress =>
            {
                UpdateBootstrapProgress(progress, "Đang tải dữ liệu...");
            }));
            _initialDataPrepared = true;
        }
        else
        {
            UpdateBootstrapProgress(1f, "Đang tải dữ liệu...");
            yield return null;
        }

        if (!EnsureInternetAvailable())
        {
            _isBootstrapping = false;
            yield break;
        }

        yield return LoadLanguagesWithTimeout();

        PlayerPrefs.GetString("language", "vi");
        FinishBootstrapLoading();
        LoadMenuScene();

        _isBootstrapping = false;
    }

    private bool EnsureInternetAvailable()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            return true;
        }

        ShowNoInternetPopup();
        return false;
    }

    private void ShowNoInternetPopup()
    {
        if (_noInternetPopupShown)
        {
            return;
        }

        _noInternetPopupShown = true;
        FinishBootstrapLoading();

        string message = "Không có kết nối Internet.";
        if (LocalizationManager.Instance != null)
        {
            message = LocalizationManager.Instance.GetText("noti_network_false");
        }

        if (PopupHelper.Instance != null)
        {
            PopupHelper.Instance.ShowPopupOut(message, ExitGame);
            return;
        }

        Debug.LogWarning("GameInitializer: PopupHelper chưa sẵn sàng để hiển thị thông báo mất kết nối.");
    }

    private void ExitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>
    /// Kiểm tra kết nối internet thật sự bằng cách ping server.
    /// Application.internetReachability chỉ check adapter (WiFi/data bật/tắt),
    /// không phát hiện được WiFi bật nhưng không ra được internet.
    /// </summary>
    private IEnumerator CheckInternetConnectivity(System.Action<bool> onResult)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            ShowNoInternetPopup();
            onResult?.Invoke(false);
            yield break;
        }

        // Ping thật sự để xác nhận có kết nối internet
        using (var req = UnityWebRequest.Head("https://clients3.google.com/generate_204"))
        {
            req.timeout = 8;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success && req.responseCode == 204)
            {
                onResult?.Invoke(true);
                yield break;
            }
        }

        // Fallback: thử lần 2 với endpoint khác
        using (var req = UnityWebRequest.Head("https://www.google.com"))
        {
            req.timeout = 8;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onResult?.Invoke(true);
                yield break;
            }
        }

        Debug.LogWarning("GameInitializer: Không thể kết nối internet (có WiFi/data nhưng không ra được mạng).");
        ShowNoInternetPopup();
        onResult?.Invoke(false);
    }

    private static IEnumerator EnsureManagersReady()
    {
        const float timeout = 5f;
        float elapsed = 0f;

        while ((APIManager.Instance == null || LocalizationManager.Instance == null) && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static IEnumerator WaitForLoadingManagerReady()
    {
        const float timeout = 5f;
        float elapsed = 0f;
        float nextLogAt = 0f;

        Debug.Log($"[GameInitializer] Bắt đầu chờ LoadingManager. {LoadingManager.GetDiagnosticsSummary()}");

        while (!LoadingManager.HasActiveInstance && elapsed < timeout)
        {
            if (elapsed >= nextLogAt)
            {
                Debug.LogWarning($"[GameInitializer] Đang chờ LoadingManager... elapsed={elapsed:F1}s/{timeout:F1}s | {LoadingManager.GetDiagnosticsSummary()}");
                nextLogAt += 1f;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.Log($"[GameInitializer] Kết thúc chờ LoadingManager sau {elapsed:F1}s. activeReady={LoadingManager.HasActiveInstance}. {LoadingManager.GetDiagnosticsSummary()}");
    }

    private void UpdateBootstrapProgress(float progress, string message)
    {
        if (LoadingManager.TryGetInstance(out var loadingManager) && loadingManager.isActiveAndEnabled)
        {
            loadingManager.UpdateProgress(progress, message);
        }
    }

    private void FinishBootstrapLoading()
    {
        UpdateBootstrapProgress(1f, "Hoàn tất");
    }

    private void LoadMenuScene()
    {
        const string menuSceneName = "Menu";

        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && string.Equals(activeScene.name, menuSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LoadingManager.LoadScene(menuSceneName);
    }

    private static IEnumerator WaitForTaskWithTimeout<T>(System.Threading.Tasks.Task<T> task, float timeout)
    {
        float elapsed = 0f;
        while (!task.IsCompleted && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator LoadLanguagesWithTimeout()
    {
        if (_languagesLoaded || APIManager.Instance == null || LocalizationManager.Instance == null)
        {
            yield break;
        }

        bool receivedLanguages = false;
        var task = APIManager.Instance.GetLanguagesAsync();

        if (LoadingManager.HasActiveInstance)
        {
            yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(task, langs =>
            {
                if (langs == null || langs.Count == 0)
                {
                    Debug.LogWarning("GameInitializer: Không nhận được dữ liệu ngôn ngữ từ server, sử dụng dữ liệu cục bộ.");
                    return;
                }

                LocalizationManager.Instance.SetLanguages(langs);
                receivedLanguages = true;
                _languagesLoaded = true;
            }, 7f));
        }
        else
        {
            yield return WaitForTaskWithTimeout(task, 7f);

            if (!task.IsCompleted)
            {
                Debug.LogWarning("GameInitializer: Timeout khi tải ngôn ngữ từ server, sử dụng dữ liệu cục bộ.");
            }
            else if (task.IsFaulted)
            {
                Debug.LogError("GameInitializer: Lỗi tải ngôn ngữ từ server: " + task.Exception);
            }
            else if (task.Result == null || task.Result.Count == 0)
            {
                Debug.LogWarning("GameInitializer: Không nhận được dữ liệu ngôn ngữ từ server, sử dụng dữ liệu cục bộ.");
            }
            else
            {
                LocalizationManager.Instance.SetLanguages(task.Result);
                receivedLanguages = true;
                _languagesLoaded = true;
            }
        }

        if (!receivedLanguages)
        {
            LocalizationManager.Instance.LoadLanguage(PlayerPrefs.GetString("language", "vi"));
        }
    }

    public IEnumerator InitializeGameOffline()
    {


        // Tạo phiên chơi offline
        var hostGO = Instantiate(GameSessionOffline);
        var host = hostGO.GetComponent<GameSessionOffline>();

        // Gán tham chiếu bằng tag
        host.playArea = GameObject.FindWithTag("PlayArea")?.GetComponent<BoxCollider>();
        host.SpawnPlayerPoint = GameObject.FindWithTag("SpawnPoint")?.transform;
        host.SpawnBallPoint = GameObject.FindWithTag("SpawnBallPoint")?.transform;
        host.ExamMain = GameObject.FindWithTag("ExamPoint")?.transform;
        host.StartPointMain = GameObject.FindWithTag("StartPointMain")?.transform;
        //host.StartPoint = GameObject.FindWithTag("StartPoint")?.transform;
        host.TerrainGround = GameObject.FindGameObjectWithTag("Ground")?.GetComponent<Terrain>();

        var examLocation_1 = GameObject.FindWithTag("ExamPoint_1")?.transform;
        var examLocation_2 = GameObject.FindWithTag("ExamPoint_2")?.transform;
        host.LstLocationExam.Add(examLocation_1);
        host.LstLocationExam.Add(examLocation_2);
        var StartLocation_1 = GameObject.FindWithTag("StartPoint_1")?.transform;
        var StartLocation_2 = GameObject.FindWithTag("StartPoint_2")?.transform;
        host.LstLocationStartPoint.Add(StartLocation_1);
        host.LstLocationStartPoint.Add(StartLocation_2);

        Log.Info("Game Initialized successfully");
        yield break;

    }

}
