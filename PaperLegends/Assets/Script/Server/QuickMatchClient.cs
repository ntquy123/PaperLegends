using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class QuickMatchClient : MonoBehaviour
{
    public static QuickMatchClient? Instance { get; private set; }

    public static event Action? OnSearching;
    public static event Action<QuickMatchServer.QuickMatchTicket>? OnMatchReady;
    public static event Action? OnQueueCancelled;
    public static event Action<QuickMatchServer.QuickMatchTicket>? OnMatchStarting;
    public static event Action? OnExitedQueue;
    public static event Action<PlayerRef, int, int>? OnPlayerReadyStatusChanged;
    public static event Action? OnRequestQuickMatchCommand;
    public static event Action<bool>? OnConfirmReadyCommand;

    [Header("Matchmaking CONFIG")]
    [SerializeField]
    private TextMeshProUGUI timerText;
    [SerializeField]
    private GameObject findMatchPanel; // thanh trạng thái tìm trận có hiển thị đếm thời gian
    [SerializeField]
    private Button cancelSearchBtn;
    [SerializeField]
    private Button startSearchBtn;
    [SerializeField]
    private TMP_Text startSearchStatusText;
    [SerializeField, Range(0f, 1f)]
    private float startSearchDisabledAlpha = 0.5f;
    private CanvasGroup startSearchCanvasGroup;
    private CanvasGroup cancelSearchCanvasGroup;
    [SerializeField, Range(0f, 1f)]
    private float cancelSearchDisabledAlpha = 0.5f;
    [SerializeField]
    private float cancelSearchCooldownSeconds = 5f;
    private float waitOpponentTimeout = 30f; // thời gian tối đa chờ ghép người chơi trước khi fallback sang AI
    private int quickMatchBetRequirement = 7; // yêu cầu đặt bi ván này
    private int MaxPlayer = 3; // số lượng tối đa người chơi

    [Header("Paper Legends Queue")]
    [SerializeField] private bool usePaperLegendQuickMatch = true;
    [SerializeField, Min(1)] private int paperLegendMaxPlayers = PaperLegendRuntimeState.DefaultFreeForAllPlayers;
    [SerializeField, Min(0)] private int paperLegendBetRequirement = 0;
    [SerializeField] private PaperLegendCharacterSelectionClient paperLegendCharacterSelectionClient;
    [Header("Matchmaking Ready Prompt")]
    [SerializeField]
    private GameObject quickMatchReadyPrompt; //popup hiển thị đã ghép được trận và kèm danh sách người chơi đã sẵn sàng
    [SerializeField]
    private Button quickMatchReadyAcceptButton; //button click sẵn sàng để chuẩn bị vào game
    //[SerializeField]
    //private Button quickMatchReadyDeclineButton;
    [SerializeField]
    private TMP_Text quickMatchReadyCountdownText;
    [SerializeField]
    private float quickMatchReadyTimeout = 100000f;
    [SerializeField]
    [Tooltip("Giới hạn tối đa cho thời gian đếm ngược popup ready (giây).")]
    private int quickMatchReadyCountdownLimitSeconds = 600000;
    [SerializeField]
    private Transform quickMatchReadyPlayerGrid;
    [SerializeField]
    private GameObject quickMatchReadyPlayerItemPrefab;
    //[SerializeField]
    //private GridLayoutGroup quickMatchReadyGridLayout;

    private QuickMatchServer.QuickMatchTicket _pendingSession;
    private QuickMatchServer? cachedQuickMatchServer;
    private QuickMatchState _state = QuickMatchState.Idle;
    private int quickMatchReadyConfirmedCount;
    private int quickMatchReadyTotalPlayers;
    private float quickMatchReadyCountdownRemaining;
    private float quickMatchReadyDefaultTimeout;
    private float elapsedTime;
    private bool quickMatchTimerActive;
    // Cờ đánh dấu client đã gửi phản hồi chấp nhận/từ chối popup ready. Khi true, mọi hành
    // động/đếm ngược liên quan đến popup sẽ dừng lại để tránh gửi trùng lặp lên server.
    private bool quickMatchReadyResponseSent;
    private Coroutine quickMatchReadyCountdownRoutine;
    private bool uiCallbacksRegistered;
    private bool serverCallbacksRegistered;
    private Coroutine clientSetupMonitorRoutine;
    private readonly Dictionary<int, ReadyPlayerDisplay> readyPlayerDisplaysById = new();
    private readonly Dictionary<PlayerRef, ReadyPlayerDisplay> readyPlayerDisplaysByRef = new();
    private readonly Dictionary<int, string> readyPlayerGuidCache = new();
    private Coroutine allPlayersReadyRoutine;
    private bool allPlayersReadyVisualsApplied;
    private bool hasSubmittedAvatarGuid;
    private bool hasRequestedAvatarGuidSync;
    private bool socketCallbacksRegistered;
    private bool queueWaitingForTicket;
    private bool queueJoinRequested;
    private bool queueCancelRequested;
    private bool queueReadyPromptActive;
    private bool queueReadySelectionMade;
    private bool queueReadyAccepted;
    private bool awaitingReadyConfirmation;
    private bool queuedReadyAccepted;
    private bool queueLoadingPending;
    private bool matchLoadingStarted;
    private bool matchLoadingCompleted;
    private bool paperLegendSelectionActive;
    private float matchLoadingProgress;
    private string deferredMatchLoadingStage;
    private string pendingMatchProposalId;
    private bool awaitingMatchConfirmation;
    private bool matchConfirmed;
    private string activeResultMatchId;
    private string queueDeferredFailureReason;
    private string queueFailureReason;
    private WebSocketHelper.MatchTicketMessage pendingMatchTicket;
    private int queueRequiredPlayers;
    private Coroutine quickMatchRoutine;
    private Coroutine cancelMatchQueueRoutine;
    private Coroutine keepNetworkRoutine;
    private Coroutine preloadAvatarsRoutine;
    private int quickMatchRequestVersion;
    private const int CancelMatchQueueRetryCount = 6;
    private const float CancelMatchQueueRetryDelaySeconds = 0.75f;
    private const float QuickMatchReadyPromptSeconds = 20f;
    private readonly Dictionary<string, (float target, string text)> matchLoadingStageSteps = new(StringComparer.OrdinalIgnoreCase)
    {
        { "SERVER_CREATING", (0.2f, "Đang khởi tạo server...") },
        { "MATCH_READY", (0.25f, "Đã ghép đôi, đang chuẩn bị vào trận...") }
    };

    // Thuộc tính đọc trạng thái hiện tại của client (Idle, Searching, Ready, ...)
    public QuickMatchState State => _state;

    private static bool IsTicketExpired(WebSocketHelper.MatchTicketMessage ticket, out long nowUnixMs)
    {
        nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (ticket == null || ticket.deadlineMs <= 0)
        {
            return false;
        }

        return nowUnixMs >= ticket.deadlineMs;
    }

    private static string BuildTicketDebugLog(WebSocketHelper.MatchTicketMessage ticket)
    {
        if (ticket == null)
        {
            return "<null ticket>";
        }

        return $"matchId={ticket.matchId}, session={ticket.sessionName}, region={ticket.region}, hostPort={ticket.hostPort}, deadlineMs={ticket.deadlineMs}";
    }

    // Lớp phụ trợ quản lý phần tử UI đại diện từng người chơi trong popup ready
    private class ReadyPlayerDisplay
    {
        public int PlayerId;
        public PlayerRef PlayerRef;
        public GameObject Root;
        public RawImage RawImage;
        public Image Image;
        public CanvasGroup CanvasGroup;
        public Texture2D Texture;
        public Sprite Sprite;
        public bool IsReady;
        public bool HighlightAllReady;

        // Gán lại texture đại diện (ảnh avatar) cho item hiển thị người chơi và cập nhật sprite tương ứng
        public void ApplyTexture(Texture2D texture)
        {
            Texture = texture;

            if (RawImage != null)
            {
                RawImage.texture = texture;
            }
            else if (Image != null)
            {
                if (Sprite != null)
                {
                    UnityEngine.Object.Destroy(Sprite);
                    Sprite = null;
                }

                if (texture != null)
                {
                    Sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    Image.sprite = Sprite;
                }
                else
                {
                    Image.sprite = null;
                }
            }
        }

        // Đánh dấu người chơi đã sẵn sàng và cập nhật hiệu ứng hiển thị
        public void SetReadyState(bool ready)
        {
            IsReady = ready;
            UpdateVisual();
        }

        // Bật/tắt hiệu ứng nổi bật khi tất cả người chơi đã sẵn sàng
        public void SetHighlightAllReady(bool highlight)
        {
            HighlightAllReady = highlight;
            UpdateVisual();
        }

        // Điều chỉnh màu sắc/độ mờ dựa trên trạng thái ready hoặc highlight
        private void UpdateVisual()
        {
            var color = HighlightAllReady ? new Color(1f, 0.35f, 0.35f, 1f) : Color.white;
            float alpha = (IsReady || HighlightAllReady) ? 1f : 0.35f;
            color.a = alpha;

            if (RawImage != null)
            {
                RawImage.color = color;
            }

            if (Image != null)
            {
                Image.color = color;
            }

            if (CanvasGroup != null)
            {
                CanvasGroup.alpha = alpha;
            }
        }
    }

    // Khởi tạo singleton, lưu giá trị timeout mặc định và ẩn popup ghép trận khi mới vào
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Another QuickMatchClient instance was created. Destroying the new instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        PaperLegendRuntimeState.SetPaperLegendMatch(usePaperLegendQuickMatch);
        quickMatchReadyTimeout = QuickMatchReadyPromptSeconds;
        quickMatchReadyDefaultTimeout = ClampReadyTimeout(quickMatchReadyTimeout);

        if (quickMatchReadyPrompt != null)
        {
            quickMatchReadyPrompt.SetActive(false);
        }

        if (paperLegendCharacterSelectionClient == null)
        {
            paperLegendCharacterSelectionClient = FindObjectOfType<PaperLegendCharacterSelectionClient>(true);
        }

        if (startSearchStatusText == null && startSearchBtn != null)
        {
            startSearchStatusText = startSearchBtn.GetComponentInChildren<TMP_Text>(true);
        }

        if (startSearchBtn != null)
        {
            startSearchCanvasGroup = startSearchBtn.GetComponent<CanvasGroup>();
        }

        if (cancelSearchBtn != null)
        {
            cancelSearchCanvasGroup = cancelSearchBtn.GetComponent<CanvasGroup>();
            if (cancelSearchCanvasGroup == null)
            {
                cancelSearchCanvasGroup = cancelSearchBtn.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    // Xóa tham chiếu singleton khi object bị hủy
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    // Đăng ký callback UI và server mỗi khi component được bật
    private void OnEnable()
    {
        RegisterUiCallbacks();
        RegisterServerCallbacks();
        RegisterSocketCallbacks();
        UpdateStartSearchAvailability(showNotification: false);
    }

    // Hủy đăng ký callback, dừng coroutine và dọn trạng thái khi component bị tắt
    private void OnDisable()
    {
        UnregisterUiCallbacks();
        UnregisterServerCallbacks();
        UnregisterSocketCallbacks();

        StopQuickMatchReadyCountdown();
        quickMatchReadyResponseSent = false;
        cachedQuickMatchServer = null;
        queueReadyPromptActive = false;
        queueReadySelectionMade = false;
        queueReadyAccepted = false;
        awaitingReadyConfirmation = false;
        awaitingMatchConfirmation = false;
        matchConfirmed = false;
        pendingMatchProposalId = null;
        queuedReadyAccepted = false;
        queueLoadingPending = false;
        queueLoadingPending = false;
        queueRequiredPlayers = 0;
        queueDeferredFailureReason = null;
        if (clientSetupMonitorRoutine != null)
        {
            StopCoroutine(clientSetupMonitorRoutine);
            clientSetupMonitorRoutine = null;
        }

        if (preloadAvatarsRoutine != null)
        {
            StopCoroutine(preloadAvatarsRoutine);
            preloadAvatarsRoutine = null;
        }

        ClearLocalAvatarGuid(ResolveNetworkObjectManager());
    }

    // Cập nhật bộ đếm thời gian hiển thị khi đang tìm trận
    private void Update()
    {
        if (quickMatchTimerActive)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimerText(elapsedTime);
        }
    }

    // Tìm hoặc tạo tham chiếu GameManagerNetWork hiện tại trên scene
    private GameManagerNetWork ResolveNetworkManager()
    {
        var instance = GameManagerNetWork.Instance;
        if (instance == null)
        {
            instance = FindObjectOfType<GameManagerNetWork>(true);
            if (instance != null)
            {
                GameManagerNetWork.Instance = instance;
            }
        }

        return instance;
    }

    // Tìm NetworkObjectManager hợp lệ để đồng bộ thông tin avatar người chơi
    private NetworkObjectManager ResolveNetworkObjectManager()
    {
        if (NetworkObjectManager.Instance != null &&
            NetworkObjectManager.Instance.Object != null &&
            NetworkObjectManager.Instance.Object.IsValid)
        {
            return NetworkObjectManager.Instance;
        }

        var candidates = FindObjectsOfType<NetworkObjectManager>();
        foreach (var candidate in candidates)
        {
            if (candidate == null || candidate.Object == null || !candidate.Object.IsValid)
            {
                continue;
            }

            NetworkObjectManager.Instance = candidate;
            return candidate;
        }

        return null;
    }

    // Lấy thông tin người chơi local (id + token) nếu có đăng nhập
    private bool TryGetLocalPlayerGuid(out int playerId, out string guid)
    {
        playerId = 0;
        guid = null;

        var login = GameManagerNetWork.Instance?.loginUserModel;
        if (login == null)
        {
            return false;
        }

        playerId = login.UserId;
        guid = login.Token;
        return playerId > 0 && !string.IsNullOrEmpty(guid);
    }

    // Đảm bảo client local đã gửi guid avatar của mình lên server
    private void EnsureLocalAvatarGuidRegistered(NetworkObjectManager manager)
    {
        if (manager == null || hasSubmittedAvatarGuid)
        {
            return;
        }

        if (!TryGetLocalPlayerGuid(out var playerId, out var guid))
        {
            return;
        }

        readyPlayerGuidCache[playerId] = guid;
        manager.RpcSubmitPlayerAvatarGuid(playerId, guid);
        hasSubmittedAvatarGuid = true;
    }

    // Gửi yêu cầu đồng bộ danh sách guid avatar từ server xuống
    private void RequestAvatarGuidSync(NetworkObjectManager manager)
    {
        if (manager == null || hasRequestedAvatarGuidSync)
        {
            return;
        }

        manager.RpcRequestPlayerAvatarGuidSync();
        hasRequestedAvatarGuidSync = true;
    }

    // Đồng bộ cache guid avatar local theo dữ liệu manager đang giữ
    private void SyncReadyPlayerGuids(NetworkObjectManager manager)
    {
        if (manager == null)
        {
            return;
        }

        var lookup = manager.PlayerAvatarGuids;
        if (lookup == null)
        {
            return;
        }

        foreach (var pair in lookup)
        {
            if (pair.Key <= 0 || string.IsNullOrEmpty(pair.Value))
            {
                continue;
            }

            readyPlayerGuidCache[pair.Key] = pair.Value;
        }
    }

    // Xóa guid avatar local khi rời popup ready hoặc thoát component
    private void ClearLocalAvatarGuid(NetworkObjectManager manager)
    {
        if (manager == null || !hasSubmittedAvatarGuid)
        {
            hasSubmittedAvatarGuid = false;
            return;
        }

        if (TryGetLocalPlayerGuid(out var playerId, out _))
        {
            manager.RpcClearPlayerAvatarGuid(playerId);
            readyPlayerGuidCache.Remove(playerId);
        }

        hasSubmittedAvatarGuid = false;
        ResetAvatarGuidSyncState();
    }

    // Reset cờ theo dõi tiến trình sync guid avatar
    private void ResetAvatarGuidSyncState()
    {
        hasRequestedAvatarGuidSync = false;
    }

    public void StartQuickMatch(string playerName)
    {
        if (quickMatchRoutine != null)
        {
            StopCoroutine(quickMatchRoutine);
        }

        quickMatchRoutine = StartCoroutine(StartQuickMatchRoutine(playerName));
    }

    // Chuẩn bị runner, kết nối Photon và tìm QuickMatchServer để bắt đầu quy trình ghép trận
    public IEnumerator StartQuickMatchRoutine(string playerName)
    {
        quickMatchRequestVersion++;
        StopCancelMatchQueueRetry();

        // Bật màn hình loading nếu có cấu hình sẵn để báo hiệu đang xử lý ghép trận
        if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
        {
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        }

        PaperLegendRuntimeState.SetPaperLegendMatch(usePaperLegendQuickMatch);
        int betRequirement = GetEffectiveQuickMatchBetRequirement();
        int requiredPlayers = GetEffectiveQuickMatchMaxPlayers();

        if (ShouldCheckRingBallForQuickMatch() && !HasEnoughRingBall(betRequirement, showNotification: true))
        {
            if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            {
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            }
            ClearQuickMatchRoutineHandle();
            yield break;
        }
        // Ghi log bước vào coroutine tìm trận, kèm tên người chơi để dễ debug
        Debug.Log($"[QuickMatch] StartQuickMatchRoutine entered for player '{playerName}'. paperLegends={usePaperLegendQuickMatch}, requiredPlayers={requiredPlayers}, bet={betRequirement}");

        // Biến lưu thông báo lỗi (nếu có) trong quá trình khởi tạo để xử lý tập trung
        string errorMessage = null;

        if (!RequestQuickMatchInternal())
        {
            if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            {
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            }
            ClearQuickMatchRoutineHandle();
            yield break;
        }

        queueCancelRequested = false;
        queueFailureReason = null;
        pendingMatchTicket = null;
        paperLegendSelectionActive = false;
        ClearActiveResultMatch();
        queueReadyPromptActive = false;
        queueReadySelectionMade = false;
        queueReadyAccepted = false;
        awaitingReadyConfirmation = false;
        queueLoadingPending = false;
        queueDeferredFailureReason = null;
        pendingMatchProposalId = null;
        awaitingMatchConfirmation = false;
        matchConfirmed = false;
        ResetMatchLoadingState();

        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (userId <= 0)
        {
            errorMessage = "Invalid userId for quick match.";
        }

        if (errorMessage == null && APIManager.Instance == null)
        {
            errorMessage = "APIManager is not ready.";
        }

        if (errorMessage == null)
        {
            WebSocketHelper.Instance?.Connect(userId);
            var joinTask = APIManager.Instance.JoinMatchQueueAsync(userId, betRequirement, (int)TypeMatchGid.MatchRandomRank, "asia", requiredPlayers);
            APIManager.QueueJoinResponse joinResponse = null;
            yield return StartCoroutine(APIManager.Instance.RunTask(joinTask, result => joinResponse = result));

            if (joinResponse == null)
            {
                errorMessage = "Không thể vào hàng chờ.";
            }
            else if (joinResponse.status != "QUEUED" && joinResponse.status != "ALREADY_QUEUED")
            {
                errorMessage = string.IsNullOrEmpty(joinResponse.message) ? "Không thể vào hàng chờ." : joinResponse.message;
            }
            else
            {
                queueJoinRequested = true;
                queueWaitingForTicket = true;
            }
        }

        if (errorMessage == null && queueCancelRequested)
        {
            if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            {
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            }
            ClearQuickMatchRoutineHandle();
            yield break;
        }

        if (errorMessage == null)
        {
            if (findMatchPanel != null)
            {
                findMatchPanel.SetActive(true);
            }

            if (startSearchBtn != null)
            {
                startSearchBtn.gameObject.SetActive(false);
            }

            ShowCancelButtonWithCooldown();

            elapsedTime = 0f;
            UpdateTimerText(elapsedTime);
            quickMatchTimerActive = true;
            HideQuickMatchReadyPrompt();

            if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            {
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            }
        }

        float queueWaitElapsed = 0f;
        bool forceStartRequested = false;

        while (errorMessage == null && queueWaitingForTicket && pendingMatchTicket == null && string.IsNullOrEmpty(queueFailureReason))
        {
            if (queueCancelRequested)
            {
                break;
            }

            if (usePaperLegendQuickMatch &&
                (paperLegendSelectionActive || matchConfirmed || matchLoadingStarted || queueLoadingPending))
            {
                yield return null;
                continue;
            }

            queueWaitElapsed += Time.deltaTime;

            // Hết thời gian chờ → yêu cầu backend tạo trận ngay với bot lấp chỗ trống
            if (!forceStartRequested && queueWaitElapsed >= waitOpponentTimeout)
            {
                forceStartRequested = true;
                Debug.Log($"[QuickMatch] ⏱️ Hết thời gian chờ ({waitOpponentTimeout}s), yêu cầu ghép trận với bot...");

                var forceTask = APIManager.Instance.ForceStartMatchAsync(userId, betRequirement, (int)TypeMatchGid.MatchRandomRank, "asia", requiredPlayers);
                APIManager.ForceStartResponse forceResponse = null;
                yield return StartCoroutine(APIManager.Instance.RunTask(forceTask, result => forceResponse = result));

                if (forceResponse != null && forceResponse.status == "OK")
                {
                    Debug.Log("[QuickMatch] ✅ Force-start thành công, chờ ticket từ server...");
                }
                else
                {
                    Debug.LogWarning("[QuickMatch] ⚠️ Force-start thất bại, tiếp tục chờ ticket...");
                }
            }

            // Sau force-start, chờ thêm tối đa 15s cho ticket
            if (forceStartRequested && queueWaitElapsed >= waitOpponentTimeout + 15f)
            {
                Debug.LogWarning("[QuickMatch] ❌ Đã chờ quá lâu sau force-start, hủy tìm trận.");
                errorMessage = "Không thể tạo trận đấu. Vui lòng thử lại.";
                break;
            }

            yield return null;
        }

        if (errorMessage == null && awaitingReadyConfirmation)
        {
            float waitSeconds = QuickMatchReadyPromptSeconds;
            float waited = 0f;
            while (awaitingReadyConfirmation
                   && waited < waitSeconds
                   && !queueCancelRequested
                   && string.IsNullOrEmpty(queueFailureReason))
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (awaitingReadyConfirmation && !queueCancelRequested && string.IsNullOrEmpty(queueFailureReason))
            {
                errorMessage = "Người chơi chưa xác nhận sẵn sàng.";
            }
            else if (queueReadySelectionMade && !queueReadyAccepted)
            {
                errorMessage = "Người chơi đã hủy xác nhận sẵn sàng.";
            }
        }
        else if (errorMessage == null && queueReadySelectionMade && !queueReadyAccepted)
        {
            errorMessage = "Người chơi đã hủy xác nhận sẵn sàng.";
        }

        if (errorMessage == null && !string.IsNullOrEmpty(queueFailureReason))
        {
            errorMessage = queueFailureReason;
        }

        if (errorMessage == null && queueCancelRequested)
        {
            if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            {
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            }
            ClearQuickMatchRoutineHandle();
            yield break;
        }

        var matchTicket = pendingMatchTicket;
        if (errorMessage == null && matchTicket == null)
        {
            errorMessage = "Không nhận được ticket từ server.";
        }

        if (errorMessage == null && IsTicketExpired(matchTicket, out var ticketCheckNowUnixMs))
        {
            errorMessage = "Ticket vào trận đã hết hạn. Vui lòng tìm trận lại.";
            Debug.LogWarning($"[QuickMatch] Match ticket expired before StartGame (nowMs={ticketCheckNowUnixMs}): {BuildTicketDebugLog(matchTicket)}");
        }

        queueWaitingForTicket = false;

        if (errorMessage == null)
        {
            quickMatchTimerActive = false;
            if (findMatchPanel != null)
            {
                findMatchPanel.SetActive(false);
            }
        }

        if (errorMessage == null)
        {
            if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            {
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            }

            queuedReadyAccepted = true;
        }

        if (errorMessage == null)
        {
            BeginMatchLoadingSequence();
            UpdateMatchLoadingProgress(0.3f, "Đang chuẩn bị kết nối phòng...");
            
            // Thêm delay để đợi server khởi động hoàn toàn
            // Server cần thời gian để connect tới Photon Cloud và tạo room
            Debug.Log("[QuickMatch] Waiting for server to fully initialize...");
            const float serverInitDelay = 3f; // Đợi 3 giây để server khởi động
            float waited = 0f;
            while (waited < serverInitDelay)
            {
                waited += Time.deltaTime;
                yield return null;
                if (queueCancelRequested)
                {
                    ClearQuickMatchRoutineHandle();
                    yield break;
                }
            }
            UpdateMatchLoadingProgress(0.35f, "Server đang sẵn sàng...");
        }

        // Lấy tham chiếu GameManagerNetWork để thao tác runner; thử nhiều lần nếu chưa sẵn sàng
        var networkManager = ResolveNetworkManager();
        if (networkManager == null)
        {
            const float managerResolveTimeout = 3f; // thời gian tối đa chờ tìm được GameManagerNetWork
            float managerResolveElapsed = 0f; // thời gian đã trôi qua khi chờ

            while (networkManager == null && managerResolveElapsed < managerResolveTimeout)
            {
                managerResolveElapsed += Time.deltaTime; // cộng dồn thời gian chờ mỗi frame
                yield return null; // nhường khung hình để tránh khóa ứng dụng
                if (queueCancelRequested)
                {
                    ClearQuickMatchRoutineHandle();
                    yield break;
                }
                networkManager = ResolveNetworkManager(); // thử lấy lại tham chiếu sau mỗi frame
            }
        }

        // Log kết quả có lấy được GameManagerNetWork hay không
        Debug.Log(networkManager != null
            ? "[QuickMatch] GameManagerNetWork.Instance acquired successfully."
            : "[QuickMatch] GameManagerNetWork.Instance is null!");

        // Nếu vẫn không có manager thì lưu lại lỗi để dừng xử lý
        if (networkManager == null)
        {
            errorMessage = "GameManagerNetWork instance is not available.";
        }

        // Khai báo runner và sceneManager sẽ dùng cho Photon
        NetworkRunner runner = null;
        NetworkSceneManagerDefault sceneManager = null;

        if (errorMessage == null)
        {
            // Mở kết nối tới Photon và nhận runner từ GameManagerNetWork
            runner = networkManager.OpenConnectToPhotonServer();
            Debug.Log(runner != null
                ? $"[QuickMatch] NetworkRunner obtained. IsRunning={runner.IsRunning}"
                : "[QuickMatch] Failed to obtain NetworkRunner from GameManagerNetWork.");

            if (runner == null)
            {
                // Nếu không lấy được runner thì ghi nhận lỗi
                errorMessage = "Failed to create or retrieve the network runner.";
            }
            else
            {
                // Tìm sẵn NetworkSceneManagerDefault trên runner để dùng load scene
                sceneManager = runner != null
                    ? runner.GetComponent<NetworkSceneManagerDefault>()
                    : null;

                // Nếu runner chưa có component quản lý scene thì bổ sung
                if (sceneManager == null && runner != null)
                {
                    sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
                    Debug.Log("[QuickMatch] NetworkSceneManagerDefault created alongside NetworkRunner instance.");
                }

                // Trường hợp vẫn chưa tìm thấy thì gắn vào chính GameManagerNetWork để đảm bảo có sceneManager
                if (sceneManager == null)
                {
                    sceneManager = networkManager.GetComponent<NetworkSceneManagerDefault>();
                    if (sceneManager == null)
                    {
                        sceneManager = networkManager.gameObject.AddComponent<NetworkSceneManagerDefault>();
                        Debug.Log("[QuickMatch] NetworkSceneManagerDefault created on GameManagerNetWork fallback host.");
                    }
                }
            }
        }

        if (errorMessage == null && runner != null && !runner.IsRunning)
        {
            // Sao chép cấu hình Photon và cố định region để tối ưu kết nối
            var customSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
            customSettings.FixedRegion = string.IsNullOrWhiteSpace(matchTicket?.region) ? "asia" : matchTicket.region;
            customSettings.AppVersion = PhotonAppSettings.Global.AppSettings.AppVersion;
            Debug.Log(
                $"[QuickMatch] Starting NetworkRunner. Player='{playerName}', Region='{customSettings.FixedRegion}', Mode={GameMode.Client}");

            var startArgs = new StartGameArgs
            {
                GameMode = GameMode.Client,
                MatchmakingMode = MatchmakingMode.FillRoom,
                SessionName = matchTicket.sessionName,
                SceneManager = sceneManager,
                CustomPhotonAppSettings = customSettings,
               // ConnectionToken = BuildMatchConnectionToken(matchTicket),
                EnableClientSessionCreation = false,
            };

            // Tạo chuỗi mô tả các thuộc tính trong Dictionary
            //In log mới
            Debug.Log($"StartGameArgs:AppVersion ={customSettings.AppVersion},  Player='{playerName}', Region='{customSettings.FixedRegion}', Mode={GameMode.Client}, SessionName='{startArgs.SessionName ?? "<none>"}' ");
            // Bắt đầu quá trình StartGame
            UpdateMatchLoadingProgress(0.45f, "Đang kết nối Photon...");
            
            // Retry logic để xử lý trường hợp ServerFull khi nhiều client join đồng thời
            const int maxRetries = 3;
            const float retryDelaySeconds = 2f;
            bool startGameSuccess = false;
            string lastErrorReason = null;
            
            for (int retryAttempt = 0; retryAttempt < maxRetries && !startGameSuccess; retryAttempt++)
            {
                if (retryAttempt > 0)
                {
                    // Shutdown runner cũ và tạo runner mới cho mỗi lần retry
                    // vì NetworkRunner không thể reuse sau khi fail
                    Debug.Log($"[QuickMatch] Retry attempt {retryAttempt}/{maxRetries} - recreating NetworkRunner...");
                    
                    if (networkManager != null)
                    {
                        networkManager.ResetRunner();
                        runner = null;
                        sceneManager = null;
                    }
                    
                    // Chờ một chút để server sẵn sàng
                    float retryDelay = 0f;
                    while (retryDelay < retryDelaySeconds)
                    {
                        retryDelay += Time.deltaTime;
                        yield return null;
                        if (queueCancelRequested)
                        {
                            ClearQuickMatchRoutineHandle();
                            yield break;
                        }
                    }
                    
                    // Tạo runner mới
                    runner = networkManager.OpenConnectToPhotonServer();
                    if (runner == null)
                    {
                        lastErrorReason = "Failed to create new NetworkRunner for retry.";
                        break;
                    }
                    
                    // Gắn lại SceneManager cho runner mới
                    sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
                    if (sceneManager == null)
                    {
                        sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
                    }
                    
                    // Cập nhật lại startArgs với sceneManager mới
                    startArgs.SceneManager = sceneManager;
                    
                    Debug.Log($"[QuickMatch] New runner created for retry {retryAttempt}");
                }
                
                var startTask = runner.StartGame(startArgs);
                const float startTimeout = 25f; // timeout tối đa cho StartGame
                float startElapsed = 0f; // thời gian đã chờ

                while (!startTask.IsCompleted && startElapsed < startTimeout)
                {
                    startElapsed += Time.deltaTime; // tăng thời gian chờ
                    yield return null; // đợi frame tiếp theo
                    if (queueCancelRequested)
                    {
                        ClearQuickMatchRoutineHandle();
                        yield break;
                    }
                }

                if (!startTask.IsCompleted)
                {
                    // Nếu hết thời gian mà StartGame chưa xong thì báo lỗi
                    lastErrorReason = "Time out! không thể vào phòng game";
                    if (retryAttempt < maxRetries - 1)
                    {
                        if (networkManager != null)
                        {
                            Debug.LogWarning("[QuickMatch] StartGame timeout detected. Resetting runner before retry to tránh giữ slot cũ.");
                            networkManager.ResetRunner();
                            runner = null;
                            sceneManager = null;
                        }

                        Debug.LogWarning($"[QuickMatch] StartGame timeout, will retry...");
                        continue;
                    }
                    break;
                }
                
                var startResult = startTask.Result; // kết quả StartGame từ Fusion
                if (!startResult.Ok)
                {
                    // Nếu StartGame trả về lỗi, kiểm tra có phải ServerFull không để retry
                    bool isServerFull = startResult.ShutdownReason == ShutdownReason.GameIsFull;
                    lastErrorReason = $"{startResult.ShutdownReason}";
                    if (!string.IsNullOrEmpty(startResult.ErrorMessage))
                    {
                        lastErrorReason += $" ({startResult.ErrorMessage})";
                    }
                    
                    if (isServerFull && retryAttempt < maxRetries - 1)
                    {
                        if (networkManager != null)
                        {
                            Debug.LogWarning("[QuickMatch] GameIsFull detected. Resetting runner before retry to tránh duplicate client cũ.");
                            networkManager.ResetRunner();
                            runner = null;
                            sceneManager = null;
                        }

                        Debug.LogWarning($"[QuickMatch] Server is full, will retry... ({retryAttempt + 1}/{maxRetries})");
                        continue;
                    }
                    else
                    {
                        break; // Lỗi khác hoặc hết lượt retry
                    }
                }
                else
                {
                    // Log thành công và thông tin phòng
                    Debug.Log($"🚀 StartGame thành công. Vào phòng: {runner.SessionInfo?.Name ?? startArgs.SessionName}");

                    UpdateMatchLoadingProgress(0.6f, "Đang vào phòng...");
                    // Log chi tiết runner sau khi StartGame thành công
                    Debug.Log($"[QuickMatch] NetworkRunner StartGame succeeded. Session='{startArgs.SessionName}', Players={startArgs.PlayerCount}, IsRunning={runner.IsRunning}");
                    const float runnerReadyTimeout = 10f; // thời gian chờ runner ở trạng thái running
                    float runnerReadyElapsed = 0f; // thời gian đã chờ

                    while (!runner.IsRunning && runnerReadyElapsed < runnerReadyTimeout)
                    {
                        runnerReadyElapsed += Time.deltaTime; // cộng dồn thời gian chờ
                        yield return null; // đợi tới frame kế
                        if (queueCancelRequested)
                        {
                            ClearQuickMatchRoutineHandle();
                            yield break;
                        }
                    }

                    if (!runner.IsRunning)
                    {
                        // Nếu runner vẫn không chạy sau timeout thì xem như lỗi
                        lastErrorReason = "Network runner is not running after StartGame succeeded.";
                        break;
                    }
                    else
                    {
                        UpdateMatchLoadingProgress(0.7f, "Đang chuẩn bị dữ liệu...");
                        EnsureKeepNetworkStarted();
                        startGameSuccess = true;
                    }
                }
            }
            
            // Nếu sau tất cả retry vẫn thất bại
            if (!startGameSuccess && lastErrorReason != null)
            {
                errorMessage = $"Failed to start runner: {lastErrorReason}";
            }
        }

        if (errorMessage == null)
        {
            // Lấy lại tham chiếu runner từ GameManagerNetWork để đảm bảo đang dùng chung instance
            runner = networkManager != null ? networkManager.runner : null;
            Debug.Log(runner != null
                ? $"[QuickMatch] Using GameManagerNetWork runner reference. IsRunning={runner.IsRunning}, PlayerCount={runner.SessionInfo?.PlayerCount}"
                : "[QuickMatch] GameManagerNetWork runner reference is null after start.");
        }

        QuickMatchServer quickMatchServerInstance = null; // biến tạm giữ QuickMatchServer tìm thấy trong scene
        if (errorMessage == null && runner != null)
        {
            const float serverSearchTimeout = 25f; // thời gian tối đa tìm QuickMatchServer
            float serverSearchElapsed = 0f; // thời gian đã chờ tìm server

            while (serverSearchElapsed < serverSearchTimeout && quickMatchServerInstance == null)
            {
                var candidates = FindObjectsOfType<QuickMatchServer>(); // tìm tất cả QuickMatchServer có trong scene
                foreach (var candidate in candidates)
                {
                    if (candidate == null || candidate.Runner == null || candidate.Object == null || !candidate.Object.IsValid)
                    {
                        continue; // bỏ qua server không hợp lệ hoặc chưa spawn xong
                    }

                    if (candidate.Runner == runner)
                    {
                        quickMatchServerInstance = candidate; // lưu lại server cùng runner hiện tại
                        cachedQuickMatchServer = candidate; // cache để dùng về sau
                        break;
                    }
                }

                if (quickMatchServerInstance == null)
                {
                    serverSearchElapsed += Time.deltaTime; // cộng dồn thời gian chờ tìm server
                    yield return null; // chờ frame tiếp theo
                    if (queueCancelRequested)
                    {
                        ClearQuickMatchRoutineHandle();
                        yield break;
                    }
                }
            }

            if (quickMatchServerInstance == null)
            {
                // Nếu quá thời gian vẫn không tìm thấy server thì coi như lỗi
                errorMessage = "Không tìm thấy QuickMatchServer trong 25 giây.";
                Debug.LogWarning("[QuickMatch] QuickMatchServer instance not found within timeout window.");
            }
            else
            {
                try
                {
                    int loginUserId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0; // lấy userId đang đăng nhập
                    quickMatchServerInstance.NotifyClientEnteredGame(loginUserId); // báo server rằng client đã vào phòng
                    Debug.Log("[QuickMatch] QuickMatchServer located. Notifying server about client entry.");

                    var networkObjectManager = ResolveNetworkObjectManager(); // tìm serverRPC đã spawn
                    if (networkObjectManager != null)
                    {
                        if (GameManagerNetWork.Instance != null)
                        {
                            GameManagerNetWork.Instance.serverRPC = networkObjectManager; // gán serverRPC vào GameManagerNetWork để dùng chung
                            Debug.Log("[QuickMatch] thiết lập server serverRPC thành công !");
                        }
                        else
                        {
                            Debug.LogWarning("[QuickMatch] GameManagerNetWork.Instance is missing while assigning NetworkObjectManager.");
                        }

                        EnsureLocalAvatarGuidRegistered(networkObjectManager); // gửi guid avatar local lên server nếu chưa có
                        RequestAvatarGuidSync(networkObjectManager); // yêu cầu đồng bộ guid avatar của người chơi khác
                        UpdateMatchLoadingProgress(0.85f, "Đang đồng bộ dữ liệu...");
                    }
                    else
                    {
                        Debug.LogWarning("[QuickMatch] Không tìm thấy serverRPC");
                    }
                }
                catch (Exception ex)
                {
                    // Bắt lỗi bất ngờ khi thông báo lên QuickMatchServer
                    errorMessage = $"Không thể thông báo QuickMatchServer: {ex.Message}";
                    Debug.LogException(ex);
                }
            }
        }

        if (errorMessage != null)
        {
            // Khi có lỗi: log cảnh báo, hiện thông báo UI và hủy tìm trận
            Debug.LogWarning($"Failed to start quick match: {errorMessage}");
            ResetMatchLoadingState(true);
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_quickmatch_start_failed"), false);
            if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            {
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            }

            CancelQuickMatch();
            ClearQuickMatchRoutineHandle();
            yield break; // kết thúc coroutine vì đã gặp lỗi
        }

        GameManagerNetWork.Instance.currentQuickMatchId = matchTicket.sessionName;
        SetActiveResultMatch(matchTicket.matchId);

        // Ẩn màn hình loading khi đã hoàn tất các bước kết nối
        if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
        {
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
        }

        CompleteMatchLoadingSequence();
        ClearQuickMatchRoutineHandle();
    }

    // Resend ACK once after short delay to reduce chance of dropped ACK
    private IEnumerator ResendMatchAckCoroutine(string matchId, int playerId)
    {
        yield return new WaitForSeconds(0.2f);
        if (!matchConfirmed)
        {
            Debug.Log($"[QuickMatch] Resending Match ACK for {matchId} to player {playerId}");
            WebSocketHelper.Instance?.SendMatchAck(matchId, playerId);
        }
    }

    private Dictionary<string, SessionProperty> BuildSessionProperties()
    {
        return new Dictionary<string, SessionProperty>
        {
            { "MatchRoom", (SessionProperty)(int)TypeMatchGid.MatchRandomRank }
        };
    }

    private static byte[] BuildMatchConnectionToken(WebSocketHelper.MatchTicketMessage matchTicket)
    {
        if (matchTicket == null || string.IsNullOrWhiteSpace(matchTicket.joinToken))
        {
            Debug.LogWarning("[QuickMatch] Match ticket is missing joinToken; connecting without ConnectionToken.");
            return null;
        }

        var tokenBytes = Encoding.UTF8.GetBytes(matchTicket.joinToken);
        const int fusionConnectionTokenMaxBytes = 128;
        if (tokenBytes.Length > fusionConnectionTokenMaxBytes)
        {
            Debug.LogWarning($"[QuickMatch] joinToken length {tokenBytes.Length} exceeds Fusion limit {fusionConnectionTokenMaxBytes}. Connecting without ConnectionToken to avoid token truncation.");
            return null;
        }

        return tokenBytes;
    }

    private bool HasEnoughRingBall(int requiredBet, bool showNotification)
    {
        int ringBall = UserInfoHandler.Instance?.PlayerInventory?.RingBall ?? 0;
        if (ringBall >= requiredBet)
        {
            return true;
        }

        if (!showNotification)
        {
            return false;
        }

        string message = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText("noti_ringball_not_enough")
            : "Not enough RingBall.";
        NotificationHelper.Instance?.ShowNotification(message, false);
        return false;
    }

    private int GetEffectiveQuickMatchBetRequirement()
    {
        return usePaperLegendQuickMatch ? Mathf.Max(0, paperLegendBetRequirement) : quickMatchBetRequirement;
    }

    private int GetEffectiveQuickMatchMaxPlayers()
    {
        return usePaperLegendQuickMatch
            ? PaperLegendRuntimeState.DefaultFreeForAllPlayers
            : Mathf.Max(1, MaxPlayer);
    }

    private bool ShouldCheckRingBallForQuickMatch()
    {
        return !usePaperLegendQuickMatch && GetEffectiveQuickMatchBetRequirement() > 0;
    }

    public void UpdateStartSearchAvailability(bool showNotification)
    {
        bool hasEnough = !ShouldCheckRingBallForQuickMatch()
            || HasEnoughRingBall(GetEffectiveQuickMatchBetRequirement(), showNotification);

        if (startSearchStatusText == null && startSearchBtn != null)
        {
            startSearchStatusText = startSearchBtn.GetComponentInChildren<TMP_Text>(true);
        }

        if (startSearchBtn != null)
        {
            startSearchBtn.interactable = hasEnough;
            if (startSearchCanvasGroup != null)
            {
                startSearchCanvasGroup.alpha = hasEnough ? 1f : startSearchDisabledAlpha;
            }
            else
            {
                var image = startSearchBtn.GetComponent<Image>();
                if (image != null)
                {
                    var color = image.color;
                    color.a = hasEnough ? 1f : startSearchDisabledAlpha;
                    image.color = color;
                }
            }
        }

        if (startSearchStatusText != null)
        {
            startSearchStatusText.text = hasEnough ? "Tìm trận" : "Hết bi cược";
            startSearchStatusText.color = hasEnough ? Color.white : Color.red;
        }
    }

 

    // Hàm public để UI gọi tìm trận nhanh, ủy quyền vào logic nội bộ
    public bool RequestQuickMatch() => RequestQuickMatchInternal();

    // Hàm public để gửi trạng thái sẵn sàng lên server
    public void ConfirmReady(bool ready) => ConfirmReadyInternal(ready);

    // Hủy quá trình tìm trận, reset UI và trạng thái đếm thời gian
    public void CancelQuickMatch()
    {
        ClearActiveResultMatch();
        ResetMatchLoadingState(true);
        ResetQuickMatchReadyTimeout();
        SetState(QuickMatchState.Idle);
        _pendingSession = default;
        pendingMatchTicket = null;
        queueFailureReason = null;
        paperLegendSelectionActive = false;
        queueCancelRequested = true;
        queueWaitingForTicket = false;
        queueReadyPromptActive = false;
        queueReadySelectionMade = false;
        queueReadyAccepted = false;
        awaitingReadyConfirmation = false;
        queuedReadyAccepted = false;
        if (GameManagerNetWork.Instance != null)
        {
            GameManagerNetWork.Instance.currentQuickMatchId = null;
            GameManagerNetWork.Instance.currentQuickMatchResultId = null;
        }
        if (quickMatchRoutine != null)
        {
            StopCoroutine(quickMatchRoutine);
            quickMatchRoutine = null;
        }

        try
        {
            var quickMatchServer = TryGetCachedQuickMatchServer();
            int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
            quickMatchServer?.NotifyClientQueueStatus(userId, QuickMatchServer.QuickMatchPlayerStatusCodes.Cancelled);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        RequestCancelMatchQueue(force: true);

        if (findMatchPanel != null)
        {
            findMatchPanel.SetActive(false);
        }

        if (startSearchBtn != null)
        {
            startSearchBtn.gameObject.SetActive(true);
        }

        UpdateStartSearchAvailability(showNotification: false);

        if (cancelSearchBtn != null)
        {
            cancelSearchBtn.gameObject.SetActive(false);
        }

        elapsedTime = 0f;
        UpdateTimerText(elapsedTime);
        quickMatchTimerActive = false;
        HideQuickMatchReadyPrompt();
        quickMatchReadyResponseSent = false;
    }

    public void HandleMatchRoomClosedBeforeGameplay()
    {
        Time.timeScale = 1f;
        ClearActiveResultMatch();
        ResetMatchLoadingState(true);
        ResetQuickMatchReadyTimeout();
        SetState(QuickMatchState.Idle);
        _pendingSession = default;
        pendingMatchTicket = null;
        queueFailureReason = null;
        paperLegendSelectionActive = false;
        queueCancelRequested = true;
        queueWaitingForTicket = false;
        queueReadyPromptActive = false;
        queueReadySelectionMade = false;
        queueReadyAccepted = false;
        awaitingReadyConfirmation = false;
        queuedReadyAccepted = false;
        awaitingMatchConfirmation = false;
        matchConfirmed = false;
        pendingMatchProposalId = null;

        if (quickMatchRoutine != null)
        {
            StopCoroutine(quickMatchRoutine);
            quickMatchRoutine = null;
        }

        if (findMatchPanel != null)
        {
            findMatchPanel.SetActive(false);
        }

        if (startSearchBtn != null)
        {
            startSearchBtn.gameObject.SetActive(true);
        }

        UpdateStartSearchAvailability(showNotification: false);

        if (cancelSearchBtn != null)
        {
            cancelSearchBtn.gameObject.SetActive(false);
        }

        elapsedTime = 0f;
        UpdateTimerText(elapsedTime);
        quickMatchTimerActive = false;
        HideQuickMatchReadyPrompt();
        quickMatchReadyResponseSent = false;
        cachedQuickMatchServer = null;
        RequestCancelMatchQueue(force: true);
    }

    private void SetActiveResultMatch(string matchId)
    {
        activeResultMatchId = string.IsNullOrWhiteSpace(matchId) ? null : matchId.Trim();

        if (GameManagerNetWork.Instance != null)
        {
            GameManagerNetWork.Instance.currentQuickMatchResultId = activeResultMatchId;
        }
    }

    public void ClearActiveResultMatch()
    {
        activeResultMatchId = null;

        if (GameManagerNetWork.Instance != null)
        {
            GameManagerNetWork.Instance.currentQuickMatchResultId = null;
        }
    }

    public bool ShouldAcceptMatchFinished(string matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(activeResultMatchId))
        {
            return false;
        }

        if (!string.Equals(activeResultMatchId, matchId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var networkManager = GameManagerNetWork.Instance;
        return matchLoadingCompleted ||
               networkManager != null &&
               (!string.IsNullOrEmpty(networkManager.currentQuickMatchId) ||
                networkManager.IsRunnerActive ||
                networkManager.IsReconnecting ||
                networkManager.WillAttemptReconnect);
    }

    private void RequestCancelMatchQueue(bool force = false)
    {
        if (!force && !queueJoinRequested)
        {
            return;
        }

        if (APIManager.Instance == null)
        {
            return;
        }

        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (userId <= 0)
        {
            return;
        }

        StopCancelMatchQueueRetry();
        cancelMatchQueueRoutine = StartCoroutine(CancelMatchQueueWithRetriesCoroutine(userId, quickMatchRequestVersion));
    }

    private IEnumerator CancelMatchQueueWithRetriesCoroutine(int userId, int requestVersion)
    {
        for (int attempt = 1; attempt <= CancelMatchQueueRetryCount; attempt++)
        {
            if (requestVersion != quickMatchRequestVersion)
                break;

            if (APIManager.Instance == null)
                break;

            APIManager.QueueCancelResponse response = null;
            var task = APIManager.Instance.CancelMatchQueueAsync(userId);
            yield return StartCoroutine(APIManager.Instance.RunTask(task, result => response = result));

            if (response != null)
            {
                Debug.Log($"[QuickMatch] Cancel queue sync attempt {attempt}/{CancelMatchQueueRetryCount}: {response.status}");
            }

            if (attempt < CancelMatchQueueRetryCount)
                yield return new WaitForSeconds(CancelMatchQueueRetryDelaySeconds);
        }

        queueJoinRequested = false;
        cancelMatchQueueRoutine = null;
    }

    private void StopCancelMatchQueueRetry()
    {
        if (cancelMatchQueueRoutine == null)
            return;

        StopCoroutine(cancelMatchQueueRoutine);
        cancelMatchQueueRoutine = null;
    }

    // Khôi phục thời gian chờ ready về giá trị mặc định
    private void ResetQuickMatchReadyTimeout()
    {
        quickMatchReadyTimeout = ClampReadyTimeout(quickMatchReadyDefaultTimeout);
    }

    // Đảm bảo thời gian đếm ngược không vượt quá giới hạn cho phép
    private float ClampReadyTimeout(float seconds)
    {
        if (seconds <= 0f)
        {
            return 0f;
        }

        if (quickMatchReadyCountdownLimitSeconds > 0)
        {
            return Mathf.Min(seconds, quickMatchReadyCountdownLimitSeconds);
        }

        return seconds;
    }

    private void BeginMatchLoadingSequence()
    {
        if (matchLoadingStarted || matchLoadingCompleted || LoadingManager.Instance == null)
        {
            return;
        }

        matchLoadingStarted = true;
        matchLoadingProgress = 0f;
        queueLoadingPending = false;
        LoadingManager.Instance.StartLoadingLocalPersistent();
        UpdateMatchLoadingProgress(0.05f, "Đang chuẩn bị vào trận...");

        if (!string.IsNullOrEmpty(deferredMatchLoadingStage))
        {
            var stage = deferredMatchLoadingStage;
            deferredMatchLoadingStage = null;
            ApplyMatchLoadingStage(stage);
        }
    }

    private void UpdateMatchLoadingProgress(float progress, string text)
    {
        if (!matchLoadingStarted || LoadingManager.Instance == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(progress);
        if (clamped <= matchLoadingProgress)
        {
            return;
        }

        matchLoadingProgress = clamped;
        LoadingManager.Instance.UpdateProgress(clamped, text);
    }

    private void ApplyMatchLoadingStage(string stage)
    {
        if (string.IsNullOrEmpty(stage))
        {
            return;
        }

        if (!matchLoadingStarted)
        {
            deferredMatchLoadingStage = stage;
            return;
        }

        if (matchLoadingStageSteps.TryGetValue(stage, out var step))
        {
            UpdateMatchLoadingProgress(step.target, step.text);
            if (string.Equals(stage, "SERVER_CREATING", StringComparison.OrdinalIgnoreCase))
            {
                EnsurePreloadMatchAvatars();
            }
            return;
        }

        Debug.Log($"[QuickMatch] Unhandled loading stage: {stage}");
    }

    private void EnsurePreloadMatchAvatars()
    {
        if (preloadAvatarsRoutine != null || APIManager.Instance == null)
        {
            return;
        }

        preloadAvatarsRoutine = StartCoroutine(PreloadMatchAvatarsRoutine());
    }

    private IEnumerator PreloadMatchAvatarsRoutine()
    {
        UIControllerOnline.ClearPreloadedMatchAvatars();

        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (userId <= 0 || APIManager.Instance == null)
        {
            preloadAvatarsRoutine = null;
            yield break;
        }

        PlayerRoomApiResponse roomResponse = null;
        var roomTask = APIManager.Instance.GetCurrentPlayerRoomAsync(userId);
        yield return StartCoroutine(APIManager.Instance.RunTask(roomTask, result => roomResponse = result));

        int roomId = roomResponse?.room?.id ?? roomResponse?.roomUser?.roomId ?? 0;
        if (roomId <= 0)
        {
            preloadAvatarsRoutine = null;
            yield break;
        }

        List<UserRoom> users = null;
        var usersTask = APIManager.Instance.GetUsersInRoomAsync(roomId);
        yield return StartCoroutine(APIManager.Instance.RunTask(usersTask, result => users = result));

        if (users == null || users.Count == 0)
        {
            preloadAvatarsRoutine = null;
            yield break;
        }

        foreach (var user in users)
        {
            if (user?.player == null)
            {
                continue;
            }

            var providerType = ResolveProviderType(user.player.ProviderType);
            var avatarUrl = user.player.AvatarUrl;
            var guid = user.player.IdAccount;

            var playerId = user.userId;
            if (playerId <= 0)
            {
                continue;
            }

            var avatarService = AvatarService.EnsureInstance();
            Texture2D texture = null;
            string errorMessage = null;
            bool isDone = false;

            bool allowStorageFallback = providerType != AuthenticationProviderType.GooglePlayGames &&
                                        providerType != AuthenticationProviderType.Google;

            avatarService.LoadAvatar(new AvatarService.AvatarRequest(providerType, avatarUrl, guid, "avatars", allowStorageFallback),
                downloaded =>
                {
                    texture = downloaded;
                    isDone = true;
                },
                error =>
                {
                    errorMessage = error;
                    isDone = true;
                });

            while (!isDone)
            {
                yield return null;
            }

            if (texture == null)
            {
                Debug.LogWarning($"[QuickMatch] Tải avatar preload thất bại cho player {playerId}: {errorMessage}");
                continue;
            }

            UIControllerOnline.CachePreloadedMatchAvatar(playerId, texture);
        }

        preloadAvatarsRoutine = null;
    }

    private static AuthenticationProviderType ResolveProviderType(string providerType)
    {
        if (!string.IsNullOrEmpty(providerType) && Enum.TryParse(providerType, true, out AuthenticationProviderType parsed))
        {
            return parsed;
        }

        return AuthenticationProviderType.Anonymous;
    }

    private void CompleteMatchLoadingSequence()
    {
        if (!matchLoadingStarted)
        {
            return;
        }

        UpdateMatchLoadingProgress(0.95f, "Đang tải bản đồ...");
        matchLoadingStarted = false;
        matchLoadingCompleted = true;
        deferredMatchLoadingStage = null;
        pendingMatchTicket = null;
    }

    private void ResetMatchLoadingState(bool hideLoading = false)
    {
        matchLoadingStarted = false;
        matchLoadingCompleted = false;
        matchLoadingProgress = 0f;
        deferredMatchLoadingStage = null;

        if (hideLoading && LoadingManager.Instance != null)
        {
            LoadingManager.Instance.FinishLoading();
        }
    }

    private void EnsureKeepNetworkStarted()
    {
        if (LoadingManager.Instance == null)
        {
            Debug.LogWarning("[QuickMatch] LoadingManager instance is missing; cannot start KeepNetwork.");
            return;
        }

        LoadingManager.Instance.EnsureKeepNetworkStarted();
    }

    public void BeginReconnectLoadingSequence()
    {
        BeginMatchLoadingSequence();
    }

    public void EnsureKeepNetworkForReconnect()
    {
        EnsureKeepNetworkStarted();
    }

    public void SyncAvatarGuidForReconnect(NetworkObjectManager manager)
    {
        EnsureLocalAvatarGuidRegistered(manager);
        RequestAvatarGuidSync(manager);
        SyncReadyPlayerGuids(manager);
    }

    // Được server gọi để bắt đầu giai đoạn confirm ready
    public void HandleReadyPhaseStarted(int totalPlayers, float countdownSeconds) => BeginReadyPhase(totalPlayers, countdownSeconds);

    // Được server gọi khi giai đoạn ready bị hủy
    public void HandleReadyPhaseCancelled() => CancelReadyPhase();

    // Bắt đầu giai đoạn xác nhận sẵn sàng và hiển thị popup đếm ngược
    private void BeginReadyPhase(int totalPlayers, float countdownSeconds)
    {
        if (startSearchBtn != null)
        {
            startSearchBtn.gameObject.SetActive(false);
        }

        quickMatchTimerActive = false;

        quickMatchReadyTotalPlayers = totalPlayers;
        quickMatchReadyConfirmedCount = 0;

        quickMatchReadyTimeout = ClampReadyTimeout(QuickMatchReadyPromptSeconds);

        if (!TryConsumeReadyPrompt())
        {
            return;
        }

        SetState(QuickMatchState.MatchReady);
        HideQuickMatchReadyPrompt(false);
        queuedReadyAccepted = false;
        MarkLocalPlayerReady();
        ConfirmReadyInternal(true);
    }

    // Dừng giai đoạn ready và đưa client trở lại trạng thái chờ
    private void CancelReadyPhase()
    {
        quickMatchTimerActive = false;
        ResetQuickMatchReadyTimeout();
        SetState(QuickMatchState.Idle);
        CancelQuickMatch();
    }

    // Gửi yêu cầu tìm trận nếu đang rảnh, đồng thời phát sự kiện cho UI/Server
    private bool RequestQuickMatchInternal()
    {
        if (_state != QuickMatchState.Idle)
        {
            Debug.LogWarning("Quick match cannot be requested while another request is active.");
            return false;
        }

        SetState(QuickMatchState.Searching);
        RaiseSearching();
        OnRequestQuickMatchCommand?.Invoke();
        return true;
    }

    // Gửi tín hiệu đã/không sẵn sàng lên server và cập nhật trạng thái client
    private void ConfirmReadyInternal(bool ready)
    {
        if (!ready)
        {
            SetState(QuickMatchState.Idle);
        }

        OnConfirmReadyCommand?.Invoke(ready);

        var quickMatchServer = ResolveQuickMatchServer();
        if (quickMatchServer == null)
        {
            Debug.LogWarning("[QuickMatch] Unable to send ready confirmation because the QuickMatchServer reference is missing.");
            return;
        }

        try
        {
            int userId = GameManagerNetWork.Instance.loginUserModel.UserId;
            quickMatchServer.NotifyClientReadyState(ready, userId);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    // Nhận thông báo đã ghép được trận và phát sự kiện cho UI
    internal void HandleMatchReady(QuickMatchServer.QuickMatchTicket ticket)
    {
        _pendingSession = ticket;
        SetState(QuickMatchState.MatchReady);
        RaiseMatchReady(ticket);
    }

    // Nhận tín hiệu trận sắp bắt đầu và chuyển sang trạng thái vào trận
    internal void HandleMatchStarting(QuickMatchServer.QuickMatchTicket ticket)
    {
        _pendingSession = ticket;
        SetState(QuickMatchState.EnteringMatch);
        RaiseMatchStarting(ticket);
    }

    // Server báo hàng chờ bị hủy, cập nhật trạng thái và phát sự kiện
    internal void HandleQueueCancelled()
    {
        SetState(QuickMatchState.Searching);
        RaiseQueueCancelled();
        RaiseSearching();
    }

    // Server báo người chơi đã ra khỏi hàng chờ, đặt trạng thái về Idle
    internal void HandleExitQueue()
    {
        SetState(QuickMatchState.Idle);
        RaiseQueueCancelled();
        RaiseExitedQueue();
    }

    // Cập nhật UI theo trạng thái sẵn sàng từng người chơi từ server
    internal void HandlePlayerReadyStatus(PlayerRef readyPlayer, int readyCount, int totalPlayers)
    {
        RaisePlayerReadyStatus(readyPlayer, readyCount, totalPlayers);
    }

    // Lưu lại trạng thái nội bộ của client
    private void SetState(QuickMatchState newState)
    {
        _state = newState;
    }

    // Đăng ký sự kiện nội bộ để cập nhật giao diện nhanh
    private void RegisterUiCallbacks()
    {
        if (uiCallbacksRegistered)
        {
            return;
        }

        OnSearching += HandleQuickMatchSearching;
        OnMatchReady += HandleQuickMatchReady;
        OnQueueCancelled += HandleQuickMatchQueueCancelled;
        OnMatchStarting += HandleQuickMatchStarting;
        OnExitedQueue += HandleQuickMatchExited;
        OnPlayerReadyStatusChanged += HandleQuickMatchPlayerReadyStatusChanged;

        // Ready prompt is disabled; no UI callbacks are required.
        //if (quickMatchReadyDeclineButton != null)
        //{
        //    quickMatchReadyDeclineButton.onClick.AddListener(OnQuickMatchReadyDeclined);
        //}

        uiCallbacksRegistered = true;
    }

    // Hủy đăng ký các sự kiện UI khi component bị disable
    private void UnregisterUiCallbacks()
    {
        if (!uiCallbacksRegistered)
        {
            return;
        }

        OnSearching -= HandleQuickMatchSearching;
        OnMatchReady -= HandleQuickMatchReady;
        OnQueueCancelled -= HandleQuickMatchQueueCancelled;
        OnMatchStarting -= HandleQuickMatchStarting;
        OnExitedQueue -= HandleQuickMatchExited;
        OnPlayerReadyStatusChanged -= HandleQuickMatchPlayerReadyStatusChanged;

        // Ready prompt is disabled; no UI callbacks are required.
        //if (quickMatchReadyDeclineButton != null)
        //{
        //    quickMatchReadyDeclineButton.onClick.RemoveListener(OnQuickMatchReadyDeclined);
        //}

        uiCallbacksRegistered = false;
    }

    // Đăng ký sự kiện từ QuickMatchServer để nhận thông báo thời gian thực
    private void RegisterServerCallbacks()
    {
        if (serverCallbacksRegistered)
        {
            return;
        }

        QuickMatchServer.OnClientMatchReady += HandleServerMatchReady;
        QuickMatchServer.OnClientMatchStarting += HandleServerMatchStarting;
        QuickMatchServer.OnClientQueueCancelled += HandleServerQueueCancelled;
        QuickMatchServer.OnClientExitQueue += HandleServerExitQueue;
        QuickMatchServer.OnClientPlayerReadyStatusChanged += HandleServerPlayerReadyStatusChanged;
        QuickMatchServer.OnClientInitializationFailed += HandleServerInitializationFailed;
        QuickMatchServer.OnClientPaperLegendCharacterSelectionStarted += HandleServerPaperLegendCharacterSelectionStarted;
        QuickMatchServer.OnClientPaperLegendCharacterSelectionUpdated += HandleServerPaperLegendCharacterSelectionUpdated;
        QuickMatchServer.OnClientPaperLegendCharacterSelectionCompleted += HandleServerPaperLegendCharacterSelectionCompleted;
        QuickMatchServer.OnClientPaperLegendCharacterSelectionRejected += HandleServerPaperLegendCharacterSelectionRejected;

        serverCallbacksRegistered = true;
    }

    private void RegisterSocketCallbacks()
    {
        if (socketCallbacksRegistered)
        {
            return;
        }

        WebSocketHelper.OnQueueUpdate += HandleQueueUpdateReceived;
        WebSocketHelper.OnMatchFound += HandleMatchFoundReceived;
        WebSocketHelper.OnMatchConfirmed += HandleMatchConfirmedReceived;
        WebSocketHelper.OnMatchTicket += HandleMatchTicketReceived;
        WebSocketHelper.OnMatchFailed += HandleMatchFailedReceived;
        WebSocketHelper.OnMatchCancelled += HandleMatchCancelledReceived;
        WebSocketHelper.OnQueueCancelled += HandleQueueCancelledReceived;
        WebSocketHelper.OnQueueBlocked += HandleQueueBlockedReceived;
        WebSocketHelper.OnMatchLoading += HandleMatchLoadingReceived;
        WebSocketHelper.OnPaperLegendCharacterSelectionStart += HandleSocketPaperLegendCharacterSelectionStarted;
        WebSocketHelper.OnPaperLegendCharacterSelectionComplete += HandleSocketPaperLegendCharacterSelectionCompleted;

        socketCallbacksRegistered = true;
    }

    // Hủy đăng ký sự kiện từ server khi không còn cần thiết
    private void UnregisterServerCallbacks()
    {
        if (!serverCallbacksRegistered)
        {
            return;
        }

        QuickMatchServer.OnClientMatchReady -= HandleServerMatchReady;
        QuickMatchServer.OnClientMatchStarting -= HandleServerMatchStarting;
        QuickMatchServer.OnClientQueueCancelled -= HandleServerQueueCancelled;
        QuickMatchServer.OnClientExitQueue -= HandleServerExitQueue;
        QuickMatchServer.OnClientPlayerReadyStatusChanged -= HandleServerPlayerReadyStatusChanged;
        QuickMatchServer.OnClientInitializationFailed -= HandleServerInitializationFailed;
        QuickMatchServer.OnClientPaperLegendCharacterSelectionStarted -= HandleServerPaperLegendCharacterSelectionStarted;
        QuickMatchServer.OnClientPaperLegendCharacterSelectionUpdated -= HandleServerPaperLegendCharacterSelectionUpdated;
        QuickMatchServer.OnClientPaperLegendCharacterSelectionCompleted -= HandleServerPaperLegendCharacterSelectionCompleted;
        QuickMatchServer.OnClientPaperLegendCharacterSelectionRejected -= HandleServerPaperLegendCharacterSelectionRejected;

        serverCallbacksRegistered = false;
    }

    private void UnregisterSocketCallbacks()
    {
        if (!socketCallbacksRegistered)
        {
            return;
        }

        WebSocketHelper.OnQueueUpdate -= HandleQueueUpdateReceived;
        WebSocketHelper.OnMatchFound -= HandleMatchFoundReceived;
        WebSocketHelper.OnMatchConfirmed -= HandleMatchConfirmedReceived;
        WebSocketHelper.OnMatchTicket -= HandleMatchTicketReceived;
        WebSocketHelper.OnMatchFailed -= HandleMatchFailedReceived;
        WebSocketHelper.OnMatchCancelled -= HandleMatchCancelledReceived;
        WebSocketHelper.OnQueueCancelled -= HandleQueueCancelledReceived;
        WebSocketHelper.OnQueueBlocked -= HandleQueueBlockedReceived;
        WebSocketHelper.OnMatchLoading -= HandleMatchLoadingReceived;
        WebSocketHelper.OnPaperLegendCharacterSelectionStart -= HandleSocketPaperLegendCharacterSelectionStarted;
        WebSocketHelper.OnPaperLegendCharacterSelectionComplete -= HandleSocketPaperLegendCharacterSelectionCompleted;

        socketCallbacksRegistered = false;
    }

    // Callback từ server: chuyển tiếp sự kiện match ready
    private void HandleServerMatchReady(QuickMatchServer.QuickMatchTicket ticket)
    {
        HandleMatchReady(ticket);
    }

    // Callback từ server: chuyển tiếp sự kiện trận bắt đầu
    private void HandleServerMatchStarting(QuickMatchServer.QuickMatchTicket ticket)
    {
        HandleMatchStarting(ticket);
    }

    // Callback từ server: chuyển tiếp sự kiện hàng chờ bị hủy
    private void HandleServerQueueCancelled()
    {
        HandleQueueCancelled();
    }

    // Callback từ server: chuyển tiếp sự kiện người chơi rời hàng chờ
    private void HandleServerExitQueue()
    {
        HandleExitQueue();
    }

    // Callback từ server: cập nhật trạng thái ready của từng người chơi
    private void HandleServerPlayerReadyStatusChanged(PlayerRef readyPlayer, int readyCount, int totalPlayers)
    {
        HandlePlayerReadyStatus(readyPlayer, readyCount, totalPlayers);
    }

    // Server thông báo khởi tạo thất bại: hiển thị thông báo và reset trạng thái
    private void HandleServerInitializationFailed(string localizationKey)
    {
        string message = localizationKey;

        if (LocalizationManager.Instance != null)
        {
            var localized = LocalizationManager.Instance.GetText(localizationKey);
            if (!string.IsNullOrEmpty(localized))
            {
                message = localized;
            }
        }

        NotificationHelper.Instance?.ShowNotification(message, false);
        StopQuickMatchReadyCountdown();
        SetState(QuickMatchState.Idle);
        CancelQuickMatch();
    }

    private void HandleServerPaperLegendCharacterSelectionStarted(string playerIdsCsv, string selectableModelIdsCsv, float countdownSeconds)
    {
        if (!usePaperLegendQuickMatch)
            return;

        quickMatchTimerActive = false;
        if (findMatchPanel != null)
            findMatchPanel.SetActive(false);

        if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);

        HideQuickMatchReadyPrompt(false);

        var selectionClient = ResolvePaperLegendCharacterSelectionClient();
        if (selectionClient == null)
        {
            Debug.LogWarning("[QuickMatch][PaperLegends] Character selection UI client is not assigned.");
            return;
        }

        selectionClient.BeginSelection(ResolvePaperLegendSelectionMatchId(), playerIdsCsv, selectableModelIdsCsv, countdownSeconds);
    }

    private void HandleServerPaperLegendCharacterSelectionUpdated(int playerId, int modelId, int selectedCount, int totalCount, float remainingSeconds)
    {
        ResolvePaperLegendCharacterSelectionClient()?.ApplySelectionUpdate(playerId, modelId, selectedCount, selectedCount, totalCount, remainingSeconds, true);
    }

    private void HandleServerPaperLegendCharacterSelectionCompleted(string selectionsCsv)
    {
        ResolvePaperLegendCharacterSelectionClient()?.CompleteSelection(selectionsCsv);
        BeginMatchLoadingSequence();
        UpdateMatchLoadingProgress(0.35f, "Đã chọn tướng, đang tải map...");
    }

    private void HandleServerPaperLegendCharacterSelectionRejected(int modelId, string reason)
    {
        ResolvePaperLegendCharacterSelectionClient()?.RejectSelection(modelId, reason);
        Debug.LogWarning($"[QuickMatch][PaperLegends] Character modelId={modelId} rejected: {reason}");
    }

    private PaperLegendCharacterSelectionClient ResolvePaperLegendCharacterSelectionClient()
    {
        if (paperLegendCharacterSelectionClient != null)
            return paperLegendCharacterSelectionClient;

        if (PaperLegendCharacterSelectionClient.Instance != null)
        {
            paperLegendCharacterSelectionClient = PaperLegendCharacterSelectionClient.Instance;
            return paperLegendCharacterSelectionClient;
        }

        paperLegendCharacterSelectionClient = FindObjectOfType<PaperLegendCharacterSelectionClient>(true);
        return paperLegendCharacterSelectionClient;
    }

    private string ResolvePaperLegendSelectionMatchId()
    {
        if (pendingMatchTicket != null && !string.IsNullOrWhiteSpace(pendingMatchTicket.matchId))
            return pendingMatchTicket.matchId;

        if (!string.IsNullOrWhiteSpace(activeResultMatchId))
            return activeResultMatchId;

        if (!string.IsNullOrWhiteSpace(pendingMatchProposalId))
            return pendingMatchProposalId;

        return GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.currentQuickMatchResultId : string.Empty;
    }

    private void HandleQueueUpdateReceived(WebSocketHelper.QueueUpdateMessage message)
    {
        if (!queueWaitingForTicket && pendingMatchTicket == null)
        {
            return;
        }

        queueRequiredPlayers = message.required;
        Debug.Log($"[QuickMatch] Queue update {message.current}/{message.required} (bucket={message.bucket}).");

        if (message.required > 0 && message.current >= message.required)
        {
            queueLoadingPending = true;
        }
    }

    private void HandleMatchFoundReceived(WebSocketHelper.MatchFoundMessage message)
    {
        if (!queueWaitingForTicket && pendingMatchTicket == null && !queueJoinRequested && _state != QuickMatchState.Searching)
        {
            return;
        }

        if (!queueWaitingForTicket)
        {
            queueWaitingForTicket = true;
        }

        if (!queueJoinRequested)
        {
            queueJoinRequested = true;
        }

        queueRequiredPlayers = message.required;
        quickMatchTimerActive = false;
        queueReadyPromptActive = false;
        queueReadySelectionMade = true;
        queueReadyAccepted = true;
        awaitingReadyConfirmation = false;
        queueLoadingPending = false;
        quickMatchReadyResponseSent = false;
        quickMatchReadyTotalPlayers = message.required;
        quickMatchReadyConfirmedCount = 0;
        ResetQuickMatchReadyTimeout();
        pendingMatchProposalId = message.matchId;
        awaitingMatchConfirmation = true;
        matchConfirmed = false;

        Debug.Log("[QuickMatch] Match found; auto-accepting matchmaking confirmation.");

        if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
        {
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
        }

        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (userId > 0)
        {
            WebSocketHelper.Instance?.SendMatchAck(message.matchId, userId);
            // Retry once after a short delay to increase reliability in case the first send gets dropped
            StartCoroutine(ResendMatchAckCoroutine(message.matchId, userId));
        }

        Debug.Log($"[QuickMatch] Match found: {message.matchId}");

        HideQuickMatchReadyPrompt(false);
    }

    private void HandleMatchConfirmedReceived(WebSocketHelper.MatchConfirmedMessage message)
    {
        if (!queueWaitingForTicket && pendingMatchTicket == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(pendingMatchProposalId) &&
            !string.Equals(pendingMatchProposalId, message.matchId, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[QuickMatch] Ignoring match confirmation for unexpected match {message.matchId}.");
            return;
        }

        awaitingMatchConfirmation = false;
        matchConfirmed = true;

        if (!usePaperLegendQuickMatch)
        {
            queueLoadingPending = true;

            if (!matchLoadingStarted)
            {
                BeginMatchLoadingSequence();
            }
        }

        Debug.Log($"[QuickMatch] Match confirmed: {message.matchId}");
    }

    private void HandleSocketPaperLegendCharacterSelectionStarted(WebSocketHelper.PaperLegendCharacterSelectionStartMessage message)
    {
        if (!usePaperLegendQuickMatch || message == null)
            return;

        if (!queueWaitingForTicket)
            queueWaitingForTicket = true;

        paperLegendSelectionActive = true;
        queueFailureReason = null;
        queueLoadingPending = false;
        quickMatchTimerActive = false;
        awaitingMatchConfirmation = false;
        matchConfirmed = true;
        pendingMatchProposalId = message.matchId;

        if (findMatchPanel != null)
            findMatchPanel.SetActive(false);

        if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);

        HideQuickMatchReadyPrompt(false);
        Debug.Log($"[QuickMatch][PaperLegends] Character selection started: {message.matchId}, total={message.totalPlayers}, real={message.realPlayerCount}, bots={message.botCount}");
    }

    private void HandleSocketPaperLegendCharacterSelectionCompleted(WebSocketHelper.PaperLegendCharacterSelectionCompleteMessage message)
    {
        if (!usePaperLegendQuickMatch || message == null)
            return;

        paperLegendSelectionActive = false;
        queueLoadingPending = true;

        if (!matchLoadingStarted)
            BeginMatchLoadingSequence();

        ApplyMatchLoadingStage("SERVER_CREATING");
        Debug.Log($"[QuickMatch][PaperLegends] Character selection completed: {message.matchId}");
    }

    private void HandleMatchLoadingReceived(WebSocketHelper.MatchLoadingMessage message)
    {
        if (!queueWaitingForTicket && pendingMatchTicket == null && !matchLoadingStarted)
        {
            return;
        }

        if (!matchConfirmed)
        {
            deferredMatchLoadingStage = message.stage;
            return;
        }

        if (!matchLoadingStarted)
        {
            BeginMatchLoadingSequence();
        }

        ApplyMatchLoadingStage(message.stage);

        Debug.Log($"[QuickMatch] Match loading: {message.matchId} ({message.stage})");
    }

    private void HandleMatchTicketReceived(WebSocketHelper.MatchTicketMessage message)
    {
        if (!queueWaitingForTicket)
        {
            return;
        }

        if (IsTicketExpired(message, out var nowUnixMs))
        {
            queueFailureReason = "Ticket vào trận đã hết hạn. Vui lòng tìm trận lại.";
            queueWaitingForTicket = false;
            queueJoinRequested = false;
            awaitingMatchConfirmation = false;
            matchConfirmed = false;
            pendingMatchTicket = null;
            Debug.LogWarning($"[QuickMatch] Ignored expired match ticket (nowMs={nowUnixMs}): {BuildTicketDebugLog(message)}");
            return;
        }

        pendingMatchTicket = message;
        paperLegendSelectionActive = false;
        queueWaitingForTicket = false;
        queueJoinRequested = false;
        awaitingMatchConfirmation = false;
        matchConfirmed = true;

        long remainingMs = message.deadlineMs > 0 ? message.deadlineMs - nowUnixMs : -1;
        Debug.Log($"[QuickMatch] Match ticket received: {BuildTicketDebugLog(message)}, remainingMs={remainingMs}");
    }

    private void HandleMatchFailedReceived(WebSocketHelper.MatchFailedMessage message)
    {
        if (!queueWaitingForTicket)
        {
            return;
        }

        var failureReason = !string.IsNullOrEmpty(message.reason) ? message.reason : "Match failed.";
        if (!string.IsNullOrEmpty(message.detail))
            failureReason = $"{failureReason}: {message.detail}";

        if (queueReadyPromptActive && !queueReadySelectionMade)
        {
            queueDeferredFailureReason = failureReason;
            Debug.LogWarning($"[QuickMatch] Match failed deferred until ready selection: {failureReason}");
            return;
        }

        queueFailureReason = failureReason;
        paperLegendSelectionActive = false;
        queueWaitingForTicket = false;
        queueJoinRequested = false;
        awaitingMatchConfirmation = false;
        matchConfirmed = false;
        Debug.LogWarning($"[QuickMatch] Match failed: {queueFailureReason}");
    }

    private void HandleMatchCancelledReceived(WebSocketHelper.MatchCancelledMessage message)
    {
        if (!queueWaitingForTicket && pendingMatchTicket == null && !awaitingMatchConfirmation)
        {
            return;
        }

        queueFailureReason = string.IsNullOrEmpty(message.reason) ? "Match cancelled." : message.reason;
        paperLegendSelectionActive = false;
        queueWaitingForTicket = false;
        queueJoinRequested = false;
        awaitingMatchConfirmation = false;
        matchConfirmed = false;
        pendingMatchProposalId = null;
        ResetMatchLoadingState(true);

        Debug.LogWarning($"[QuickMatch] Match cancelled: {queueFailureReason}");
    }

    private void HandleQueueCancelledReceived(WebSocketHelper.QueueCancelledMessage message)
    {
        if (!queueWaitingForTicket || queueCancelRequested)
        {
            return;
        }

        queueFailureReason = "Queue cancelled.";
        queueWaitingForTicket = false;
        queueJoinRequested = false;
        Debug.LogWarning("[QuickMatch] Queue cancelled by server.");
    }

    private void HandleQueueBlockedReceived(WebSocketHelper.QueueBlockedMessage message)
    {
        if (!queueWaitingForTicket)
        {
            return;
        }

        queueFailureReason = message.reason ?? "Queue blocked.";
        queueWaitingForTicket = false;
        queueJoinRequested = false;
        Debug.LogWarning($"[QuickMatch] Queue blocked: {queueFailureReason}");
    }

    private void ShowCancelButtonWithCooldown()
    {
        if (cancelSearchBtn == null)
        {
            return;
        }

        cancelSearchBtn.gameObject.SetActive(true);
        StartCoroutine(EnableCancelAfterDelay());
    }

    private IEnumerator EnableCancelAfterDelay()
    {
        SetCancelButtonInteractable(false);

        float delay = Mathf.Max(0f, cancelSearchCooldownSeconds);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        SetCancelButtonInteractable(true);
    }

    private void SetCancelButtonInteractable(bool interactable)
    {
        if (cancelSearchBtn == null)
        {
            return;
        }

        cancelSearchBtn.interactable = interactable;
        if (cancelSearchCanvasGroup != null)
        {
            cancelSearchCanvasGroup.alpha = interactable ? 1f : cancelSearchDisabledAlpha;
            cancelSearchCanvasGroup.blocksRaycasts = interactable;
            cancelSearchCanvasGroup.interactable = interactable;
        }
    }

    // Khi bắt đầu tìm trận: bật panel tìm trận và reset đồng hồ đếm
    private void HandleQuickMatchSearching()
    {
        if (findMatchPanel != null)
        {
            findMatchPanel.SetActive(true);
        }

        HideQuickMatchReadyPrompt(false);
        quickMatchReadyResponseSent = false;

        if (startSearchBtn != null)
        {
            startSearchBtn.gameObject.SetActive(false);
        }

        ShowCancelButtonWithCooldown();

        if (!quickMatchTimerActive)
        {
            elapsedTime = 0f;
            UpdateTimerText(elapsedTime);
        }

        quickMatchTimerActive = true;
    }

    // Nhận thông báo đã có phòng sẵn sàng: hiển thị popup confirm
    private void HandleQuickMatchReady(QuickMatchServer.QuickMatchTicket ticket)
    {
        quickMatchTimerActive = false;
        Debug.Log($"✅ Quick match ready: {ticket.SessionName}");
        if (!TryConsumeReadyPrompt())
        {
            return;
        }

        HideQuickMatchReadyPrompt(false);
        MarkLocalPlayerReady();
        ConfirmReadyInternal(true);
    }

    // Khi hàng chờ bị hủy: quay lại UI tìm trận và giữ đồng hồ chạy
    private void HandleQuickMatchQueueCancelled()
    {
        if (findMatchPanel != null && !findMatchPanel.activeSelf)
        {
            findMatchPanel.SetActive(true);
        }

        HideQuickMatchReadyPrompt();
        quickMatchReadyResponseSent = false;

        if (startSearchBtn != null)
        {
            startSearchBtn.gameObject.SetActive(false);
        }

        ShowCancelButtonWithCooldown();

        quickMatchTimerActive = true;
    }

    // Trận đang chuyển cảnh: dừng timer và chờ toàn bộ ready trước khi ẩn popup
    private void HandleQuickMatchStarting(QuickMatchServer.QuickMatchTicket ticket)
    {
        quickMatchTimerActive = false;
        Debug.Log($"🚀 Entering match: {ticket.SessionName}");
        if (allPlayersReadyRoutine == null)
        {
            allPlayersReadyRoutine = StartCoroutine(AllPlayersReadyDelayRoutine());
        }
    }

    // Khi người chơi thoát hàng chờ: xóa thông tin avatar cục bộ và reset UI
    private void HandleQuickMatchExited()
    {
        ClearLocalAvatarGuid(ResolveNetworkObjectManager());
        CancelQuickMatch();
    }

    // Cập nhật nhãn đồng hồ tìm trận theo mm:ss
    private void UpdateTimerText(float time)
    {
        if (timerText == null)
        {
            return;
        }

        int totalSeconds = Mathf.FloorToInt(time);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:D2}:{seconds:D2}";
    }

    // Bắt đầu theo dõi việc client đã load xong scene game hay chưa
    public void BeginClientSetupMonitor()
    {
        var serverRpc = GameManagerNetWork.Instance.serverRPC;
        if (serverRpc == null)
        {
            Debug.LogWarning("Cannot monitor client setup – server RPC reference is null.");
            return;
        }

        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("QuickMatchClient is disabled; cannot start client setup monitor.");
            return;
        }

        if (clientSetupMonitorRoutine != null)
        {
            StopCoroutine(clientSetupMonitorRoutine);
        }

        clientSetupMonitorRoutine = StartCoroutine(MonitorClientSceneLoaded(serverRpc));
    }

    // Coroutine chờ scene game load xong rồi báo cho server
    private IEnumerator MonitorClientSceneLoaded(NetworkObjectManager serverRpc)
    {
        var roomSetting = serverRpc.GetRoomSetting();
        string expectedSceneName = roomSetting.gameScene.ToString();

        while (true)
        {
            if (serverRpc == null)
            {
                Debug.LogWarning("Server RPC reference lost while monitoring client setup.");
                break;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.isLoaded && activeScene.name == expectedSceneName)
            {
                Debug.LogWarning("[CLIENT ]Đã tải xong map game");
                yield return new WaitForSeconds(2f);
                serverRpc.RpcClientSetupComplete();
                break;
            }

            yield return null;
        }

        clientSetupMonitorRoutine = null;
    }

    // Mở popup confirm ready, phát âm thanh và dựng danh sách người chơi
    private void ShowQuickMatchReadyPrompt()
    {
        HideQuickMatchReadyPrompt(false);
    }

    // Ẩn popup ready và hủy các coroutine liên quan
    private void HideQuickMatchReadyPrompt(bool isActiveReadyPrompt = false)
    {
        ClearReadyPlayerDisplays();

        if (quickMatchReadyPrompt != null)
        {
            quickMatchReadyPrompt.SetActive(isActiveReadyPrompt);
        }

        StopQuickMatchReadyCountdown();
    }

    // Dựng danh sách item người chơi trong popup ready sử dụng avatar mặc định của prefab
    private void BuildReadyPlayerDisplays()
    {
        if (quickMatchReadyPlayerGrid == null || quickMatchReadyPlayerItemPrefab == null)
        {
            Debug.LogWarning("[QuickMatch] Ready player grid or item prefab is not assigned. Skipping avatar setup.");
            return;
        }

        allPlayersReadyVisualsApplied = false;

        var playerStates = GatherReadyPlayerStates();
        foreach (var playerState in playerStates)
        {
            var display = CreateReadyPlayerDisplay(playerState.PlayerId);
            if (display != null)
            {
                display.SetReadyState(playerState.Status == QuickMatchServer.QuickMatchPlayerStatusCodes.Ready);
            }
        }

        UpdateReadyDisplaysHighlight(false);
    }

    // Thu thập danh sách người chơi sẽ hiển thị trong popup ready cùng trạng thái
    private List<QuickMatchServer.QuickMatchPlayerState> GatherReadyPlayerStates()
    {
        var players = new List<QuickMatchServer.QuickMatchPlayerState>();

        var manager = ResolveNetworkObjectManager();
        if (manager != null)
        {
            var ordered = manager.GetOrderedPlayerInfos();
            if (ordered != null)
            {
                foreach (var info in ordered)
                {
                    if (info.playerId > 0 && !players.Any(p => p.PlayerId == info.playerId))
                    {
                        players.Add(new QuickMatchServer.QuickMatchPlayerState
                        {
                            PlayerId = info.playerId,
                            Status = QuickMatchServer.QuickMatchPlayerStatusCodes.Waiting
                        });
                    }
                }
            }
        }

        var runner = GameManagerNetWork.Instance?.runner;
        if (runner != null)
        {
            foreach (var player in runner.ActivePlayers)
            {
                if (runner.TryGetPlayerObject(player, out var playerObject) && playerObject != null)
                {
                    var handler = playerObject.GetComponent<PlayerNetworkHandler>();
                    if (handler != null)
                    {
                        int playerId = handler.PlayerModel.playerId;
                        if (playerId > 0 && !players.Any(p => p.PlayerId == playerId))
                        {
                            players.Add(new QuickMatchServer.QuickMatchPlayerState
                            {
                                PlayerId = playerId,
                                Status = QuickMatchServer.QuickMatchPlayerStatusCodes.Waiting
                            });
                        }
                    }
                }
            }
        }

        var quickMatchServer = ResolveQuickMatchServer();
        if (quickMatchServer != null && quickMatchServer.QuickMatchPlayers.Count > 0)
        {
            for (int i = 0; i < quickMatchServer.QuickMatchPlayers.Count; i++)
            {
                var state = quickMatchServer.QuickMatchPlayers[i];
                if (state.PlayerId > 0 && state.Status != QuickMatchServer.QuickMatchPlayerStatusCodes.Cancelled &&
                    !players.Any(p => p.PlayerId == state.PlayerId))
                {
                    players.Add(state);
                }
            }
        }

        var login = GameManagerNetWork.Instance?.loginUserModel;
        if (players.Count == 0 && login != null && login.UserId > 0)
        {
            players.Add(new QuickMatchServer.QuickMatchPlayerState
            {
                PlayerId = login.UserId,
                Status = QuickMatchServer.QuickMatchPlayerStatusCodes.Waiting
            });
        }

        return players;
    }

    // Dọn toàn bộ item hiển thị và reset trạng thái ready khi đóng popup
    private void ClearReadyPlayerDisplays()
    {
        if (allPlayersReadyRoutine != null)
        {
            StopCoroutine(allPlayersReadyRoutine);
            allPlayersReadyRoutine = null;
        }

        foreach (var entry in readyPlayerDisplaysById.Values)
        {
            if (entry == null)
            {
                continue;
            }

            if (entry.Sprite != null)
            {
                Destroy(entry.Sprite);
                entry.Sprite = null;
            }

            // Texture belongs to AvatarService.avatarCache — do NOT Destroy it here.
            entry.Texture = null;

            if (entry.Root != null)
            {
                Destroy(entry.Root);
            }
        }

        readyPlayerDisplaysById.Clear();
        readyPlayerDisplaysByRef.Clear();
        allPlayersReadyVisualsApplied = false;
    }

    // Điều chỉnh trạng thái highlight cho tất cả item ready
    private void UpdateReadyDisplaysHighlight(bool highlightAll)
    {
        foreach (var entry in readyPlayerDisplaysById.Values)
        {
            entry?.SetHighlightAllReady(highlightAll);
        }

        allPlayersReadyVisualsApplied = highlightAll;
    }

    // Tạo hoặc tái sử dụng item ready cho một player cụ thể
    private ReadyPlayerDisplay CreateReadyPlayerDisplay(int playerId)
    {
        if (playerId <= 0)
        {
            return null;
        }

        if (readyPlayerDisplaysById.TryGetValue(playerId, out var existing))
        {
            return existing;
        }

        var instance = Instantiate(quickMatchReadyPlayerItemPrefab, quickMatchReadyPlayerGrid);
        instance.SetActive(true);

        var display = new ReadyPlayerDisplay
        {
            PlayerId = playerId,
            Root = instance,
            CanvasGroup = instance.GetComponent<CanvasGroup>()
        };

        if (display.CanvasGroup == null)
        {
            display.CanvasGroup = instance.AddComponent<CanvasGroup>();
        }

        var avatarTransform = instance.transform.Find("AvatarPlayer") ?? instance.transform.Find("Avatar");
        if (avatarTransform != null)
        {
            display.RawImage = avatarTransform.GetComponent<RawImage>();
            display.Image = avatarTransform.GetComponent<Image>();
        }

        if (display.RawImage == null && display.Image == null)
        {
            display.RawImage = instance.GetComponentInChildren<RawImage>(true);
            if (display.RawImage == null)
            {
                display.Image = instance.GetComponentInChildren<Image>(true);
            }
        }

        display.SetHighlightAllReady(false);
        display.SetReadyState(false);

        var playerRef = ResolvePlayerRef(playerId);
        if (!playerRef.IsNone)
        {
            display.PlayerRef = playerRef;
            readyPlayerDisplaysByRef[playerRef] = display;
        }

        readyPlayerDisplaysById[playerId] = display;
        return display;
    }

    // Đảm bảo người chơi local được đánh dấu ready trên UI và đồng bộ PlayerRef
    private void MarkLocalPlayerReady()
    {
        int loginUserId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (loginUserId <= 0)
        {
            return;
        }

        if (readyPlayerDisplaysById.TryGetValue(loginUserId, out var display))
        {
            display.SetReadyState(true);

            if (display.PlayerRef.IsNone)
            {
                var resolved = ResolvePlayerRef(loginUserId);
                if (!resolved.IsNone)
                {
                    display.PlayerRef = resolved;
                    readyPlayerDisplaysByRef[resolved] = display;
                }
            }

            UpdateReadyDisplaysHighlight(allPlayersReadyVisualsApplied);
        }
    }

    // Tìm item hiển thị ứng với một PlayerRef cụ thể
    private ReadyPlayerDisplay ResolveReadyPlayerDisplay(PlayerRef player)
    {
        if (player.IsNone)
        {
            return null;
        }

        if (readyPlayerDisplaysByRef.TryGetValue(player, out var display))
        {
            return display;
        }

        int playerId = GetPlayerIdFromRef(player);
        if (playerId > 0 && readyPlayerDisplaysById.TryGetValue(playerId, out display))
        {
            display.PlayerRef = player;
            readyPlayerDisplaysByRef[player] = display;
            return display;
        }

        return null;
    }

    // Tra cứu playerId từ PlayerRef thông qua runner hoặc NetworkObjectManager
    private int GetPlayerIdFromRef(PlayerRef player)
    {
        var runner = GameManagerNetWork.Instance?.runner;
        if (runner != null && runner.TryGetPlayerObject(player, out var playerObject) && playerObject != null)
        {
            var handler = playerObject.GetComponent<PlayerNetworkHandler>();
            if (handler != null)
            {
                return handler.PlayerModel.playerId;
            }
        }

        var manager = ResolveNetworkObjectManager();
        if (manager != null)
        {
            var ordered = manager.GetOrderedPlayerInfos();
            foreach (var info in ordered)
            {
                if (ResolvePlayerRef(info.playerId) == player)
                {
                    return info.playerId;
                }
            }
        }

        return 0;
    }

    // Tìm PlayerRef tương ứng với playerId nếu đã spawn trên mạng
    private PlayerRef ResolvePlayerRef(int playerId)
    {
        if (playerId <= 0)
        {
            return PlayerRef.None;
        }

        var manager = ResolveNetworkObjectManager();
        var playerObject = manager?.GetPlayerObject(playerId);
        if (playerObject != null)
        {
            if (!playerObject.InputAuthority.IsNone)
            {
                return playerObject.InputAuthority;
            }

            if (!playerObject.StateAuthority.IsNone)
            {
                return playerObject.StateAuthority;
            }
        }

        var runner = GameManagerNetWork.Instance?.runner;
        if (runner != null)
        {
            foreach (var player in runner.ActivePlayers)
            {
                if (runner.TryGetPlayerObject(player, out var candidate) && candidate != null)
                {
                    var handler = candidate.GetComponent<PlayerNetworkHandler>();
                    if (handler != null && handler.PlayerModel.playerId == playerId)
                    {
                        return player;
                    }
                }
            }
        }

        return PlayerRef.None;
    }

    // Chờ thêm vài giây sau khi tất cả đã ready trước khi đóng popup
    private IEnumerator AllPlayersReadyDelayRoutine()
    {
        const float waitSeconds = 3f;
        float elapsed = 0f;

        if (!allPlayersReadyVisualsApplied)
        {
            UpdateReadyDisplaysHighlight(true);
        }

        while (elapsed < waitSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        FinalizeAllPlayersReady();
    }

    // Khi mọi người đã ready đủ lâu thì reset và ẩn popup
    private void FinalizeAllPlayersReady()
    {
        if (quickMatchReadyPrompt != null && !quickMatchReadyPrompt.activeSelf)
        {
            return;
        }

        allPlayersReadyRoutine = null;
        ResetQuickMatchReadyTimeout();
        quickMatchReadyResponseSent = false;
        HideQuickMatchReadyPrompt();
    }

    // Sự kiện khi người chơi bấm nút sẵn sàng trên popup
    private void OnQuickMatchReadyAccepted()
    {
        HandleReadyPromptSelection(true);
    }

    // Xử lý chung cho việc đồng ý hoặc từ chối popup ready
    private void HandleReadyPromptSelection(bool accepted)
    {
        if (!TryConsumeReadyPrompt())
        {
            return;
        }

        if (queueReadyPromptActive)
        {
            queueReadySelectionMade = true;
            queueReadyAccepted = accepted;
            queueReadyPromptActive = false;
            awaitingReadyConfirmation = false;
            if (!string.IsNullOrEmpty(queueDeferredFailureReason))
            {
                queueFailureReason = queueDeferredFailureReason;
                queueDeferredFailureReason = null;
                queueWaitingForTicket = false;
                queueJoinRequested = false;
            }
            queueLoadingPending = false;
            HideQuickMatchReadyPrompt();
            return;
        }

        if (accepted)
        {
            Debug.Log("Đã sẵn sàng");
            MarkLocalPlayerReady();
        }
        else
        {
            HideQuickMatchReadyPrompt();
        }

        ConfirmReadyInternal(accepted);
    }

    // Đảm bảo mỗi client chỉ phản hồi popup một lần
    // Hàm đảm bảo chỉ xử lý sự kiện bấm nút ready một lần: nếu đã phản hồi trước đó thì bỏ qua,
    // ngược lại đánh dấu đã gửi và dừng bộ đếm.
    private bool TryConsumeReadyPrompt()
    {
        if (quickMatchReadyResponseSent)
        {
            return false;
        }

        quickMatchReadyResponseSent = true;
        StopQuickMatchReadyCountdown();
        return true;
    }

    // Khởi động đếm ngược cho popup ready
    // Chuẩn bị và khởi động coroutine đếm ngược khi popup hiển thị. Mỗi lần mở popup ta reset
    // giá trị còn lại rồi bắt đầu coroutine mới.
    private void StartQuickMatchReadyCountdown()
    {
        StopQuickMatchReadyCountdown();

        if (quickMatchReadyTimeout <= 0f)
        {
            return;
        }

        quickMatchReadyTimeout = ClampReadyTimeout(quickMatchReadyTimeout);
        quickMatchReadyCountdownRemaining = quickMatchReadyTimeout;
        UpdateQuickMatchReadyCountdownLabel();
        quickMatchReadyCountdownRoutine = StartCoroutine(QuickMatchReadyCountdownRoutine());
    }

    // Dừng đếm ngược popup ready nếu đang chạy
    // Dừng coroutine đếm ngược hiện tại (nếu có) nhằm tránh việc coroutine cũ tiếp tục chạy khi
    // popup đã bị đóng hoặc khi người chơi đã phản hồi.
    private void StopQuickMatchReadyCountdown()
    {
        if (quickMatchReadyCountdownRoutine != null)
        {
            StopCoroutine(quickMatchReadyCountdownRoutine);
            quickMatchReadyCountdownRoutine = null;
        }
    }

    // Coroutine giảm thời gian còn lại và tự động từ chối khi hết giờ
    // Coroutine chịu trách nhiệm cập nhật đếm ngược mỗi frame và auto từ chối khi hết giờ.
    // - Trong vòng lặp: giảm thời gian còn lại theo deltaTime, cập nhật UI, dừng nếu người chơi
    //   đã phản hồi (quickMatchReadyResponseSent được set true bởi TryConsumeReadyPrompt).
    // - Sau vòng lặp: đảm bảo giá trị còn lại không âm, cập nhật UI lần cuối, nếu chưa phản hồi
    //   thì tự động chọn "từ chối" để thông báo lên server.
    private IEnumerator QuickMatchReadyCountdownRoutine()
    {
        int previousSeconds = Mathf.CeilToInt(Mathf.Max(quickMatchReadyCountdownRemaining, 0f));

        // Lặp cho đến khi hết thời gian hoặc người chơi đã phản hồi popup.
        while (quickMatchReadyCountdownRemaining > 0f && !quickMatchReadyResponseSent)
        {
            // Trừ thời gian dựa trên thời gian frame hiện tại.
            quickMatchReadyCountdownRemaining -= Time.deltaTime;

            int currentSeconds = Mathf.CeilToInt(Mathf.Max(quickMatchReadyCountdownRemaining, 0f));
            if (currentSeconds != previousSeconds)
            {
                // Cập nhật lại text đếm ngược hiển thị ra popup khi qua mốc giây mới.
                UpdateQuickMatchReadyCountdownLabel();
                previousSeconds = currentSeconds;
            }

            yield return null;
        }

        // Khi thoát vòng lặp, clamp về 0 để tránh hiển thị số âm và cập nhật UI lần cuối.
        quickMatchReadyCountdownRemaining = Mathf.Max(quickMatchReadyCountdownRemaining, 0f);
        UpdateQuickMatchReadyCountdownLabel();

        // Nếu người chơi chưa phản hồi và thời gian đã hết, tự động coi như từ chối.
        if (!quickMatchReadyResponseSent && quickMatchReadyCountdownRemaining <= 0f)
        {
            HandleReadyPromptSelection(false);
        }
    }

    // Reset lại bộ đếm sẵn sàng và trạng thái hiển thị trước khi build danh sách
    private void ResetQuickMatchReadyStatus()
    {
        quickMatchReadyConfirmedCount = 0;
        quickMatchReadyTotalPlayers = 0;
        quickMatchReadyTimeout = ClampReadyTimeout(quickMatchReadyTimeout);
        quickMatchReadyCountdownRemaining = quickMatchReadyTimeout;
        allPlayersReadyVisualsApplied = false;
        UpdateQuickMatchReadyCountdownLabel();
    }

    // Cập nhật text hiển thị đếm ngược và số người đã sẵn sàng
    private void UpdateQuickMatchReadyCountdownLabel()
    {
        if (quickMatchReadyCountdownText == null)
        {
            return;
        }

        int seconds = Mathf.CeilToInt(Mathf.Max(quickMatchReadyCountdownRemaining, 0f));
        if (quickMatchReadyTotalPlayers > 0)
        {
            quickMatchReadyCountdownText.text = $"{seconds}s • Ready {quickMatchReadyConfirmedCount}/{quickMatchReadyTotalPlayers}";
        }
        else
        {
            quickMatchReadyCountdownText.text = $"{seconds}s";
        }
    }

    // Phản ứng khi có người chơi báo ready, cập nhật số liệu và hiệu ứng
    private void HandleQuickMatchPlayerReadyStatusChanged(PlayerRef readyPlayer, int readyCount, int totalPlayers)
    {
        if (quickMatchReadyPrompt != null && !quickMatchReadyPrompt.activeSelf)
        {
            return;
        }

        quickMatchReadyConfirmedCount = readyCount;

        // Một số tình huống server có thể chưa đồng bộ đủ số lượng người chơi về client
        // (ví dụ khi một client chưa kịp được TrackPlayer trên server). Nếu sử dụng trực tiếp
        // totalPlayers được gửi xuống, client sẽ hiểu nhầm rằng tất cả đã sẵn sàng và đóng
        // popup sớm. Thay vì ghi đè, chúng ta chỉ cập nhật khi giá trị lớn hơn để luôn giữ
        // lại ngưỡng kỳ vọng ban đầu của giai đoạn ready.
        if (totalPlayers > quickMatchReadyTotalPlayers)
        {
            quickMatchReadyTotalPlayers = totalPlayers;
        }
        Debug.Log($"👥 Player {readyPlayer} ready ({readyCount}/{totalPlayers}).");

        var display = ResolveReadyPlayerDisplay(readyPlayer);
        display?.SetReadyState(true);

        int readyTarget = quickMatchReadyTotalPlayers > 0 ? quickMatchReadyTotalPlayers : totalPlayers;
        bool allReady = readyTarget > 0 && readyCount >= readyTarget;
        UpdateReadyDisplaysHighlight(allReady);

        if (allReady)
        {
            BeginMatchLoadingSequence();
            if (allPlayersReadyRoutine == null)
            {
                allPlayersReadyRoutine = StartCoroutine(AllPlayersReadyDelayRoutine());
            }
        }
        else if (allPlayersReadyRoutine != null)
        {
            StopCoroutine(allPlayersReadyRoutine);
            allPlayersReadyRoutine = null;
        }

        UpdateQuickMatchReadyCountdownLabel();
    }

    // Tìm QuickMatchServer tương ứng với runner hiện tại (có cache)
    private QuickMatchServer? ResolveQuickMatchServer()
    {
        if (cachedQuickMatchServer != null && cachedQuickMatchServer.Object != null && cachedQuickMatchServer.Object.IsValid)
        {
            return cachedQuickMatchServer;
        }

        var targetRunner = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.runner : null;
        var candidates = FindObjectsOfType<QuickMatchServer>();
        foreach (var candidate in candidates)
        {
            if (candidate == null || candidate.Runner == null || candidate.Object == null || !candidate.Object.IsValid)
            {
                continue;
            }

            if (targetRunner == null || candidate.Runner == targetRunner)
            {
                cachedQuickMatchServer = candidate;
                break;
            }
        }

        return cachedQuickMatchServer;
    }

    private QuickMatchServer? TryGetCachedQuickMatchServer()
    {
        if (cachedQuickMatchServer != null && cachedQuickMatchServer.Object != null && cachedQuickMatchServer.Object.IsValid)
        {
            return cachedQuickMatchServer;
        }

        return null;
    }

    private void ClearQuickMatchRoutineHandle()
    {
        quickMatchRoutine = null;
    }

 

    // Kích hoạt sự kiện báo UI/server rằng client đang tìm trận
    private void RaiseSearching()
    {
        OnSearching?.Invoke();
    }

    // Kích hoạt sự kiện đã tìm được trận với thông tin ticket
    private void RaiseMatchReady(QuickMatchServer.QuickMatchTicket ticket)
    {
        OnMatchReady?.Invoke(ticket);
    }

    // Kích hoạt sự kiện hàng chờ bị hủy
    private void RaiseQueueCancelled()
    {
        OnQueueCancelled?.Invoke();
    }

    // Kích hoạt sự kiện trận chuẩn bị bắt đầu
    private void RaiseMatchStarting(QuickMatchServer.QuickMatchTicket ticket)
    {
        OnMatchStarting?.Invoke(ticket);
    }

    // Kích hoạt sự kiện người chơi đã rời hàng chờ
    private void RaiseExitedQueue()
    {
        OnExitedQueue?.Invoke();
    }

    // Kích hoạt sự kiện cập nhật số người đã sẵn sàng
    private void RaisePlayerReadyStatus(PlayerRef player, int readyCount, int totalPlayers)
    {
        OnPlayerReadyStatusChanged?.Invoke(player, readyCount, totalPlayers);
    }
}
