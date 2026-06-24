using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Fusion;
using System.Threading.Tasks;

public class LoadingManager : MonoBehaviour
{
    [Header("SYSTEM CONFIG")]
    public static LoadingManager Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        nextScene = null;
    }
    public Slider loadingSlider;
    public TextMeshProUGUI loadingText;
    public static string nextScene; // Lưu tên Scene tiếp theo
    public GameObject UILoading;
    public GameObject UILoadingScreenPrefab;
    public TextMeshProUGUI loadingMapNameText;
    [Header("RECONNECT LOADING")]
    public TextMeshProUGUI reconnectDescriptionText;
    [Header("LOADING BACKGROUND")]
    public GameObject LoadingMapInfoPanel;
    public Image loadingBackgroundImage;
    public List<LoadingBackgroundConfig> loadingBackgrounds = new();
    private Coroutine ensureSceneActiveRoutine;
    private GameMapId? _pendingMapId;
    private Sprite defaultLoadingBackgroundSprite;
    private Coroutine keepNetworkRoutine;
    [SerializeField] private float keepNetworkStopTimeoutSeconds = 10f;

    [System.Serializable]
    public class LoadingBackgroundConfig
    {
        public GameMapId mapId;
        public Sprite backgroundSprite;
    }

    private static bool IsUsableInstance(LoadingManager manager)
    {
        return manager != null && manager.gameObject != null;
    }

    private static LoadingManager ResolveInstance()
    {
        if (IsUsableInstance(Instance))
        {
            return Instance;
        }

        LoadingManager[] candidates = FindObjectsOfType<LoadingManager>();
        LoadingManager fallback = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (!IsUsableInstance(candidate))
            {
                continue;
            }

            if (candidate.isActiveAndEnabled)
            {
                return candidate;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }
        }

        return fallback;
    }

    private static string DescribeInstance(LoadingManager manager)
    {
        if (!IsUsableInstance(manager))
        {
            return "instance=null";
        }

        string sceneName = manager.gameObject.scene.IsValid()
            ? manager.gameObject.scene.name
            : "<invalid-scene>";

        return $"name={manager.name}, activeSelf={manager.gameObject.activeSelf}, activeInHierarchy={manager.gameObject.activeInHierarchy}, enabled={manager.enabled}, scene={sceneName}";
    }

    public static string GetDiagnosticsSummary()
    {
        var resolvedInstance = ResolveInstance();
        string activeSceneName = SceneManager.GetActiveScene().IsValid()
            ? SceneManager.GetActiveScene().name
            : "<invalid-scene>";

        return $"stored={DescribeInstance(Instance)} | resolved={DescribeInstance(resolvedInstance)} | sceneCount={SceneManager.sceneCount} | activeScene={activeSceneName}";
    }

    private void Awake()
    {
        CacheDefaultLoadingBackgroundSprite();

        if (Instance == null || !IsUsableInstance(Instance))
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }

        if (Instance == this)
        {
            DontDestroyOnLoad(gameObject);
            return;
        }

        if (!Instance.isActiveAndEnabled && isActiveAndEnabled)
        {
            Debug.LogWarning($"[LoadingManager] Replacing inactive instance during Awake. old={DescribeInstance(Instance)} | new={DescribeInstance(this)}");
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        Debug.Log($"[LoadingManager] OnDestroy: {DescribeInstance(this)}");
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static bool HasActiveInstance
    {
        get
        {
            return TryGetInstance(out var manager) && manager.isActiveAndEnabled;
        }
    }

    public static bool TryGetInstance(out LoadingManager manager)
    {
        Instance = ResolveInstance();
        manager = Instance;
        return manager != null;
    }

    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("⚠️ LoadingManager: nextScene is null or empty!");
            return;
        }

        nextScene = sceneName;

        if (!TryGetInstance(out var manager))
        {
            Debug.LogWarning($"⚠️ LoadingManager chưa được khởi tạo, chuyển scene trực tiếp: {nextScene}");
            SceneManager.LoadScene(nextScene, LoadSceneMode.Single);
            return;
        }

        Debug.Log($"⏳ Bắt đầu tải scene: {nextScene}");
        manager.StartCoroutine(manager.LoadSceneAsync(nextScene)); // 🟢 Gọi từ instance
    }

    IEnumerator LoadSceneAsync(string sceneName)
    {
        Instance.UILoading.SetActive(true);
        loadingSlider.value = 0;
        loadingText.text = $"Loading... 0%";
        yield return new WaitForSeconds(0.5f); // Giả lập delay load cho mượt

        // 🔴 **Bước 1: Unload toàn bộ scene cũ (trừ PersistentScene)**
        int initialSceneCount = SceneManager.sceneCount;
        for (int i = 0; i < initialSceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name != "PersistentScene") // Giữ lại scene quan trọng
            {
                // Không cố gắng unload scene cuối cùng để tránh cảnh báo của Unity
                if (SceneManager.sceneCount <= 1)
                {
                    Debug.LogWarning($"🚫 Bỏ qua unload scene cuối cùng: {scene.name}");
                    break;
                }
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        yield return new WaitForSeconds(1.5f); // Cho Unity có thời gian giải phóng bộ nhớ

        // 🟢 **Bước 2: Load scene mới theo dạng Additive**

        //AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        if (operation == null)
        {
            Debug.LogError("❌ LoadSceneAsync thất bại, kiểm tra lại tên Scene!");
            yield break;
        }

        operation.allowSceneActivation = false; // Chờ đủ 100% mới chuyển Scene

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f); // Tiến trình tải
            loadingSlider.value = progress;
            loadingText.text = $"Loading... {progress * 100:F0}%";

            if (operation.progress >= 0.9f)
            {
                 
                yield return new WaitForSeconds(1f); // Chờ một chút cho mượt

                // 🔴 **Bước 3: Ẩn màn hình loading**
                UILoading.SetActive(false);
                operation.allowSceneActivation = true; // Kích hoạt scene mới
            }

            yield return null;
        }
    }
    //public void HideSceneByName(string sceneName)
    //{
    //    if (string.IsNullOrEmpty(sceneName))
    //        return;

    //    var targetScene = SceneManager.GetSceneByName(sceneName);
    //    if (!targetScene.IsValid())
    //        return;

    //    foreach (var rootObject in targetScene.GetRootGameObjects())
    //    {
    //        if (rootObject != null && rootObject.activeSelf)
    //        {
    //            rootObject.SetActive(false);
    //        }
    //    }
    //    Debug.Log($"✅ Đã ẩn Scene {sceneName}");
    //}
    public void HideSceneByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        // 1. Kiểm tra Scene có đang tải không
        var targetScene = SceneManager.GetSceneByName(sceneName);
        if (!targetScene.IsValid() || !targetScene.isLoaded)
            return;

        // 2. Thực hiện việc giải phóng bộ nhớ (Unload Scene)
        SceneManager.UnloadSceneAsync(targetScene);

        Debug.Log($"✅ Đã giải phóng bộ nhớ và Unload Scene: {sceneName}");
    }

    public void EnsureSceneActive(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        if (ensureSceneActiveRoutine != null)
        {
            StopCoroutine(ensureSceneActiveRoutine);
            ensureSceneActiveRoutine = null;
        }

        ensureSceneActiveRoutine = StartCoroutine(EnsureSceneActiveRoutine(sceneName));
    }

    public void EnsureKeepNetworkStarted()
    {
        if (keepNetworkRoutine != null)
        {
            return;
        }

        keepNetworkRoutine = StartCoroutine(KeepNetworkRoutine());
    }

    public void StopKeepNetwork()
    {
        if (keepNetworkRoutine == null)
        {
            return;
        }

        StopCoroutine(keepNetworkRoutine);
        keepNetworkRoutine = null;
    }

    private IEnumerator KeepNetworkRoutine()
    {
        Debug.Log("[QuickMatch] KeepNetwork started after Photon connection success.");
        float nullManagerElapsed = 0f;
        float invalidRunnerElapsed = 0f;

        while (true)
        {
            yield return new WaitForSeconds(5f);
            var manager = NetworkObjectManager.Instance;
            if (manager == null)
            {
                nullManagerElapsed += Time.unscaledDeltaTime;
                if (keepNetworkStopTimeoutSeconds > 0f && nullManagerElapsed >= keepNetworkStopTimeoutSeconds)
                {
                    Debug.LogWarning("[QuickMatch] Stop KeepNetwork: NetworkObjectManager missing too long.");
                    keepNetworkRoutine = null;
                    yield break;
                }
                continue;
            }

            nullManagerElapsed = 0f;

            var runner = manager.Runner;
            if (runner == null || !runner.IsRunning || runner.IsShutdown)
            {
                invalidRunnerElapsed += Time.unscaledDeltaTime;
                if (keepNetworkStopTimeoutSeconds > 0f && invalidRunnerElapsed >= keepNetworkStopTimeoutSeconds)
                {
                    Debug.LogWarning("[QuickMatch] Stop KeepNetwork: NetworkRunner not running.");
                    keepNetworkRoutine = null;
                    yield break;
                }
                continue;
            }

            invalidRunnerElapsed = 0f;
            manager.RpcKeepAlive();
        }
    }

    private IEnumerator EnsureSceneActiveRoutine(string sceneName)
    {
        const float activationTimeout = 20f;
        float elapsed = 0f;

        while (elapsed < activationTimeout)
        {
            var loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                SceneManager.SetActiveScene(loadedScene);
                HideSceneByName("Menu");
                ensureSceneActiveRoutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning($"⚠️ Không thể kích hoạt scene '{sceneName}' trong khoảng thời gian cho phép.");
        HideSceneByName("Menu");
        ensureSceneActiveRoutine = null;
    }
    #region [=================== GAME ONLINE ==================]
    public void FinishLoading()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        _loadingLocked = false;
        _progress = 1f;
        if (loadingSlider != null)
            loadingSlider.value = _progress;
        if (loadingText != null)
            loadingText.text = "Hoàn tất 100%";
        if (_timeoutRoutine != null)
        {
            StopCoroutine(_timeoutRoutine);
            _timeoutRoutine = null;
        }
        if (UILoading != null)
            UILoading.SetActive(false);
        if (UILoadingScreenPrefab != null)
            UILoadingScreenPrefab.SetActive(false);
    }    

    public void ShowReconnectLoading(string message)
    {
        if (UILoadingScreenPrefab == null)
        {
            return;
        }

        UILoadingScreenPrefab.SetActive(true);
        if (reconnectDescriptionText != null && !string.IsNullOrWhiteSpace(message))
        {
            reconnectDescriptionText.text = message;
        }
    }

    public void HideReconnectLoading()
    {
        if (UILoadingScreenPrefab == null)
        {
            return;
        }

        UILoadingScreenPrefab.SetActive(false);
    }
    private readonly Dictionary<StatusLoadingGame, (float target, string text)> _progressSteps = new()
    {
        { StatusLoadingGame.DownloadData, (0.1f, "Đang tải dữ liệu...") },
        { StatusLoadingGame.LoadMapGame, (0.25f, "Đang tải bản đồ...") },
        { StatusLoadingGame.isExam, (1f, "Hoàn tất") }
    };

    private float _progress = 0f;
    private Coroutine _timeoutRoutine;
    private bool _loadingLocked;

    public void StartLoading()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        _loadingLocked = false;
        if (UILoading != null)
            UILoading.SetActive(true);
        ApplyLoadingMapInfo();
        if (loadingSlider != null)
            loadingSlider.value = 0f;
        if (loadingText != null)
            loadingText.text = "Đang tải... 0%";
        _progress = 0f;

        if (_timeoutRoutine != null)
            StopCoroutine(_timeoutRoutine);
        _timeoutRoutine = StartCoroutine(TimeoutCheck());
    }
    public void StartLoadingLocal()
    {
        StartLoadingLocalInternal(false);
    }

    public void StartLoadingLocalPersistent()
    {
        StartLoadingLocalInternal(true);
    }

    private void StartLoadingLocalInternal(bool lockLoading)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        _loadingLocked = lockLoading;
        if (UILoading != null)
            UILoading.SetActive(true);
        ApplyLoadingMapInfo();
        if (loadingSlider != null)
            loadingSlider.value = 0f;
        if (loadingText != null)
            loadingText.text = "Đang tải... 0%";
        _progress = 0f;

        if (_timeoutRoutine != null)
            StopCoroutine(_timeoutRoutine);
        _timeoutRoutine = StartCoroutine(TimeoutCheckLocal());
    }

    public void UpdateProgress(StatusLoadingGame status)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (_progressSteps.TryGetValue(status, out var step))
        {
            StartCoroutine(AnimateProgress(step.target, step.text));
        }
    }

    public void UpdateProgress(float progress, string text)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StartCoroutine(AnimateProgress(progress, text));
    }

    private IEnumerator AnimateProgress(float target, string text)
    {
        while (_progress < target)
        {
            _progress = Mathf.MoveTowards(_progress, target, Time.deltaTime * 0.5f);
            if (loadingSlider != null)
                loadingSlider.value = _progress;
            if (loadingText != null)
                loadingText.text = $"{text} {_progress * 100:F0}%";
            yield return null;
        }

        if (Mathf.Approximately(target, 1f) && !_loadingLocked)
        {
            yield return new WaitForSeconds(0.5f);
            if (UILoading != null)
                UILoading.SetActive(false);
        }
    }

    public void SetLoadingBackground(GameMapId mapId)
    {
        if (loadingBackgroundImage == null)
        {
            return;
        }

        CacheDefaultLoadingBackgroundSprite();
        Sprite targetSprite = ResolveLoadingBackgroundSprite(mapId);

        loadingBackgroundImage.sprite = targetSprite;
        loadingBackgroundImage.gameObject.SetActive(targetSprite != null);
    }

    public void SetLoadingMapInfo(GameMapId mapId)
    {
        if (LoadingMapInfoPanel != null)
        {
            LoadingMapInfoPanel.SetActive(true);
        }

        _pendingMapId = mapId;
        ApplyLoadingMapInfo();
    }

    private void CacheDefaultLoadingBackgroundSprite()
    {
        if (defaultLoadingBackgroundSprite == null && loadingBackgroundImage != null)
        {
            defaultLoadingBackgroundSprite = loadingBackgroundImage.sprite;
        }
    }

    private Sprite ResolveLoadingBackgroundSprite(GameMapId mapId)
    {
        Sprite targetSprite = FindConfiguredLoadingBackground(mapId);
        if (targetSprite != null)
        {
            return targetSprite;
        }

        if (defaultLoadingBackgroundSprite != null)
        {
            return defaultLoadingBackgroundSprite;
        }

        targetSprite = FindConfiguredLoadingBackground(GameMapId.HometownHouse);
        if (targetSprite != null)
        {
            return targetSprite;
        }

        for (int i = 0; i < loadingBackgrounds.Count; i++)
        {
            Sprite fallbackSprite = loadingBackgrounds[i]?.backgroundSprite;
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }
        }

        return loadingBackgroundImage != null ? loadingBackgroundImage.sprite : null;
    }

    private Sprite FindConfiguredLoadingBackground(GameMapId mapId)
    {
        for (int i = 0; i < loadingBackgrounds.Count; i++)
        {
            var config = loadingBackgrounds[i];
            if (config != null && config.mapId == mapId && config.backgroundSprite != null)
            {
                return config.backgroundSprite;
            }
        }

        return null;
    }

    private void ApplyLoadingMapInfo()
    {
        if (!_pendingMapId.HasValue)
        {
            return;
        }
        
        SetLoadingBackground(_pendingMapId.Value);
        SetLoadingMapNameText(_pendingMapId.Value);
    }

    private void SetLoadingMapNameText(GameMapId mapId)
    {
        if (loadingMapNameText == null)
        {
            return;
        }

        var mapKey = mapId.ToString();
        var localizedName = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(mapKey)
            : mapKey;
        loadingMapNameText.text = localizedName;
        loadingMapNameText.gameObject.SetActive(!string.IsNullOrEmpty(localizedName));
    }

    private IEnumerator TimeoutCheck()
    {
        float timeout = 180f;
        float elapsed = 0f;
        while (elapsed < timeout && _progress < 1f)
        {
            if (GameManagerNetWork.Instance == null || GameManagerNetWork.Instance.serverRPC == null)
            {
                Debug.LogWarning("⛔ MẤT KẾT NỐI: serverRPC null");
                ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_loading_lost_connection"));
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_progress < 1f)
        {
            Debug.LogWarning("⛔ TIMEOUT: Không hoàn tất tải trong 180s");
            ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_loading_timeout"));
            Instance.UILoading.SetActive(false);
        }
    }

    private IEnumerator TimeoutCheckLocal()
    {
        float timeout = 180f;
        float elapsed = 0f;
        while (elapsed < timeout && _progress < 1f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_progress < 1f)
        {
            Debug.LogWarning("⛔ TIMEOUT: Không hoàn tất tải trong 180s");
            ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_loading_timeout_local"));
            Instance.UILoading.SetActive(false);
        }
    }

    public IEnumerator ShowLoadingProgress()
    {
        Instance.UILoading.SetActive(true);
        float progress = 0f;
        float timeout = 180f;
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.deltaTime;

            if (GameManagerNetWork.Instance == null ||
                GameManagerNetWork.Instance.serverRPC == null)
            {
                //string shutdownReason = "Không kết nối được tới Host";
                Debug.LogWarning("⛔ MẤT KẾT NỐI: serverRPC null");
                ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_sync_lost_connection"));
            
                yield break;
            }

            StatusLoadingGame status = GameManagerNetWork.Instance.serverRPC.StatusLoading;
            if (_progressSteps.TryGetValue(status, out var step))
            {
                progress = Mathf.MoveTowards(progress, step.target, Time.deltaTime * 0.5f);
                loadingSlider.value = progress;
                loadingText.text = $"{step.text} {progress * 100:F0}%";

                if (status == StatusLoadingGame.isExam && Mathf.Approximately(progress, step.target))
                    break;
            }

            if (elapsed > timeout)
            {
                Debug.LogWarning("⛔ TIMEOUT: Không nhận được trạng thái isDoneSyncData từ Host");
                ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_sync_timeout"));
                yield break;
            }

            yield return null;
        }

        yield return new WaitForSeconds(0.5f);
        Instance.UILoading.SetActive(false);
    }


    public IEnumerator RunTaskWithTimeout<T>(System.Threading.Tasks.Task<T> task, System.Action<T> onComplete, float timeout = 10f)
    {
        Instance.UILoadingScreenPrefab.SetActive(true);
        float timer = 0f;
        while (!task.IsCompleted && timer < timeout)
        {
            timer += UnityEngine.Time.deltaTime;
            yield return null;
        }

        Instance.UILoadingScreenPrefab.SetActive(false);

        if (!task.IsCompleted)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("network_noti_false"), false);
            yield break;
        }

        if (task.IsFaulted)
            UnityEngine.Debug.LogError("❌ Task lỗi: " + task.Exception);

        onComplete?.Invoke(task.Result);
    }

    public IEnumerator RunTaskWithTimeout(System.Collections.IEnumerator routine, float timeout = 10f)
    {
        Instance.UILoadingScreenPrefab.SetActive(true);
        bool finished = false;
        System.Collections.IEnumerator Wrapper()
        {
            yield return routine;
            finished = true;
        }

        var c = Instance.StartCoroutine(Wrapper());
        float timer = 0f;
        while (!finished && timer < timeout)
        {
            timer += UnityEngine.Time.deltaTime;
            yield return null;
        }

        if (!finished)
        {
            Instance.StopCoroutine(c);
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("network_noti_false"), false);
        }

        Instance.UILoadingScreenPrefab.SetActive(false);
    }


    //public IEnumerator LoadRemainingProgressAndSettingHost(RpgRoomModel setting)
    //{
    //    float fakeProgress = 0f;



    //    // Gọi song song 2 tiến trình:
    //    // - Chạy SettingGameHost
    //    // - Loading progress đến 80%
    //    Coroutine settingHostRoutine = StartCoroutine(GameSessionNetWork_Host.Instance.SettingGameHost(setting));

    //    // Giai đoạn 1: Loading từ 0% đến 80% trong lúc SettingGameHost chạy
    //    while (fakeProgress < 0.8f)
    //    {
    //        fakeProgress += Time.deltaTime * 0.4f;
    //        loadingSlider.value = fakeProgress;
    //        loadingText.text = $"Loading... {fakeProgress * 100:F0}%";
    //        yield return null;
    //    }

    //    // Đảm bảo chính xác dừng tại 80%
    //    fakeProgress = 0.8f;
    //    loadingSlider.value = fakeProgress;
    //    loadingText.text = $"Loading... 80%";

    //    // Chờ SettingGameHost chạy xong (nếu chưa xong)
    //    yield return settingHostRoutine;

    //    Debug.Log("Đã gọi SettingGameHost. Chờ các client báo ready...");

    //    // Giai đoạn 2: Chờ tất cả client báo ready (clientsReady == 1)
    //    while (GameManagerNetWork.Instance.serverRPC.ClientsReady != 1)
    //    {
    //        yield return null;
    //    }

    //    Debug.Log("Tất cả client ready. Bắt đầu fill progress 80-100%");

    //    // Giai đoạn 3: Tăng từ 80% đến 100%
    //    while (fakeProgress < 1f)
    //    {
    //        fakeProgress += Time.deltaTime * 0.2f;
    //        loadingSlider.value = fakeProgress;
    //        loadingText.text = $"Loading... {fakeProgress * 100:F0}%";
    //        yield return null;
    //    }

    //    loadingSlider.value = 1f;
    //    loadingText.text = $"Loading... 100%";

    //    yield return new WaitForSeconds(0.5f);
    //    Instance.UILoading.SetActive(false);

    //    Debug.Log("Tắt UI Loading. Sẵn sàng vào game!");
    //}



    //public IEnumerator FakeLoadRemainingProgress()
    //{
    //    float fakeProgress = 0f;

    //    // Giai đoạn 1: Load đến 80%
    //    while (fakeProgress < 0.8f)
    //    {
    //        fakeProgress += Time.deltaTime * 0.4f; // Tăng tốc độ để đạt 80% nhanh hơn
    //        loadingSlider.value = fakeProgress;
    //        loadingText.text = $"Loading... {fakeProgress * 100:F0}%";
    //        yield return null;
    //    }

    //    loadingSlider.value = 0.8f;
    //    loadingText.text = $"Loading... 80%";

    //    // Giai đoạn 2: Chờ clientsReady == 1
    //    while (GameManagerNetWork.Instance.serverRPC.ClientsReady != 1)
    //    {
    //        yield return null;
    //    }

    //    // Giai đoạn 3: Load từ 80% đến 100%
    //    while (fakeProgress < 1f)
    //    {
    //        fakeProgress += Time.deltaTime * 0.2f;
    //        loadingSlider.value = fakeProgress;
    //        loadingText.text = $"Loading... {fakeProgress * 100:F0}%";
    //        yield return null;
    //    }

    //    loadingSlider.value = 1f;
    //    loadingText.text = $"Loading... 100%";

    //    yield return new WaitForSeconds(0.5f);
    //    Instance.UILoading.SetActive(false);
    //}


    #endregion

}
