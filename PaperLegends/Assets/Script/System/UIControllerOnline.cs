#if !UNITY_SERVER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using DG.Tweening;
using System;
using Fusion;
//using Unity.Services.Authentication;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.EventSystems;

public class UIControllerOnline : MonoBehaviour
{
    // Singleton dùng để các hệ thống khác truy cập UI online hiện tại.
    public static UIControllerOnline Instance;

    [Header("SYSTEM / BET SETUP")]
    // Nhóm cấu hình đặt cược và kỹ năng đã chọn trước khi vào trận online.
    public TMP_InputField inputField; // Ô nhập số bi cược.
    //public Button plusButton;       // Nút +
    //public Button minusButton;      // Nút -
    public Button startButton; // Nút bắt đầu.
    public GameObject UIBet; // Panel đặt cược.
    public List<SkillType> selectedSkills = new List<SkillType>(); // Danh sách kỹ năng người chơi đã chọn.
    private int betAmount = 1; // Số bi cược tối thiểu.

    [Header("PREFAB / CANVAS ROOT")]
    // Nhóm prefab và canvas root dùng để sinh UI runtime.
    public GameObject playerItemPrefab; // Prefab hiển thị mỗi người chơi.
    public GameObject messagePrefab; // Prefab TMP_Text thông báo.
    public Transform canvasTransform; // Canvas để chứa text/UI sinh runtime.
    [Header("HERO PICK UI")]
    [Tooltip("Optional reference to a HeroInfoUI component to show the picked hero (MOBA-like).")]
    public HeroInfoUI heroInfoUI;
    [SerializeField, Tooltip("Paper Legends: automatically bind heroInfoUI to the local paper character during gameplay.")]
    private bool autoRefreshPaperLegendHeroInfo = true;
    [SerializeField, Min(0.05f), Tooltip("Paper Legends hero info refresh interval in seconds.")]
    private float paperLegendHeroInfoRefreshInterval = 0.2f;

    [Header("PLAYER LIST PANELS")]
    // Nhóm panel chứa danh sách người chơi và thông tin người chơi trong trận.
    public Transform playerListPanel; // Panel chứa danh sách người chơi.
    public Transform InforListPanel; // Panel chứa thông tin người chơi trong game như số culi.

    [Header("PAPER LEGENDS SCORE UI")]
    [SerializeField, Tooltip("Text showing local Paper Legends kill count.")]
    private TextMeshProUGUI paperLegendKillText;
    [SerializeField, Tooltip("Text showing local Paper Legends death count.")]
    private TextMeshProUGUI paperLegendDeathText;
    [SerializeField, Min(0.1f), Tooltip("Paper Legends scoreboard refresh interval in seconds.")]
    private float paperLegendScoreboardRefreshInterval = 0.5f;

    [Header("MAIN ACTION UI")]
    // Nhóm nút thao tác chính trong trận như vòng bi, đầu hàng, xem map, ẩn người chơi.
    [SerializeField] private TextMeshProUGUI ringBallText;
    [SerializeField] private Button ringBallButton;
    [SerializeField] private Button surrenderButton;
    [SerializeField] private Button viewMapButton;
    [SerializeField] private OnlineMap2DController map2DController;
    [SerializeField] private Button hidePlayerButton;
    [SerializeField] private Button actionEmoteMenuButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitGameButton;
    [SerializeField] private GameObject damagedBallNotice;
    [SerializeField, Tooltip("Thời gian làm mới kiểm tra bi hư hỏng (giây).")]
    private float damagedBallNoticeRefreshInterval = 0.5f;

    [Header("CAMERA UI")]
    // Nhóm nút điều khiển góc nhìn camera khi chơi online.
    [SerializeField] private Button cameraSwitchButton;

    [Header("BALL SWITCH UI")]
    // Nhóm UI đổi bi, gồm nút đổi bi và cấu hình popup chọn bi.
    [SerializeField] private Button nextBallButton;
    [SerializeField, Tooltip("Ảnh nút đổi bi ở trạng thái bình thường.")]
    private Sprite nextBallButtonNormalSprite;
    [SerializeField, Tooltip("Ảnh nút đổi bi khi popup chọn bi đang mở.")]
    private Sprite nextBallButtonActiveSprite;
    [SerializeField, Tooltip("Kích thước mỗi thẻ bi trong popup đổi bi.")]
    private Vector2 ballSwitchCardSize = new Vector2(86f, 86f);
    [SerializeField, Tooltip("Khoảng cách giữa các thẻ bi trong popup đổi bi.")]
    private float ballSwitchCardSpacing = 18f;
    [SerializeField, Tooltip("Độ lệch popup đổi bi so với nút đổi bi.")]
    private Vector2 ballSwitchPopupOffset = new Vector2(0f, 122f);
    [SerializeField, Tooltip("Độ trễ stagger giữa từng thẻ bi.")]
    private float ballSwitchStaggerDelay = 0.055f;
    [SerializeField, Tooltip("Đường kính tối thiểu của thẻ bi khi mở popup.")]
    private float ballSwitchMinCardDiameter = 156f;
    [SerializeField, Tooltip("Độ cong của layout thẻ bi theo cung phía trên.")]
    private float ballSwitchArcHeight = 64f;

    [Header("SELECTED BALL INFO UI")]
    // Nhóm hiển thị thông tin viên bi đang được chọn.
    [SerializeField] private TextMeshProUGUI selectedBallNameText;
    [SerializeField] private Image selectedBallImage;
    [SerializeField] private TextMeshProUGUI selectedBallStatText;

    [Header("BALL HIT ANNOUNCEMENT UI")]
    // Nhóm hiệu ứng UI khi bắn trúng bi người chơi khác.
    [SerializeField, Tooltip("Ảnh thông báo ĐÃ TRÚNG, kéo Image UI vào đây.")]
    private Image ballHitAnnouncementImage;
    [SerializeField, Tooltip("Ảnh shockwave khi bắn trúng. Có thể để trống để dùng shockwave runtime.")]
    private Image ballHitShockwaveImage;
    [SerializeField, Tooltip("Target rung nhẹ khi trúng bi. Để trống sẽ rung canvas UI.")]
    private Transform ballHitScreenShakeTarget;
    [SerializeField, Tooltip("Vị trí bắt đầu của ảnh thông báo, tính từ tâm màn hình.")]
    private Vector2 ballHitAnnouncementStartOffset = new Vector2(420f, 160f);
    [SerializeField, Tooltip("Vị trí dừng của ảnh thông báo, tính từ tâm màn hình.")]
    private Vector2 ballHitAnnouncementTargetOffset = new Vector2(420f, 0f);

    [Header("BALL SKILL UI")]
    // Nhóm icon kỹ năng bi và tooltip mô tả kỹ năng.
    [SerializeField] private Image ballSkillImages;
    [SerializeField] private Button ballSkillButtons;
    [SerializeField] private GameObject ballSkillTooltipRoot;
    [SerializeField] private TextMeshProUGUI ballSkillTooltipText;
    [SerializeField, Range(0.3f, 0.5f)] private float ballSkillTooltipHoldDuration = 0.35f;
    [SerializeField] private float ballSkillTooltipIconScale = 1.12f;
    [SerializeField] private Vector2 ballSkillTooltipOffset = new Vector2(0f, 96f);

    // Cache chat/emote: tên object và state runtime để bật/tắt theo cấp người chơi.
    private const int ChatUnlockLevel = 10;
    private const string ChatButtonObjectName = "ShowChatButton";
    private const string ChatButtonStateTextName = "ChatButtonStateText";
    private const string ActionEmoteMenuButtonObjectName = "ActionEmoteMenuButton";
    private Button chatButton;
    private ActionEmotePopupUI activeActionEmotePopup;
    private TextMeshProUGUI chatButtonStateText;
 
    [Header("GAME OVER UI")]
    // Nhóm UI kết quả sau khi trận đấu kết thúc.
    public GameObject UIGameOVer;
    public TextMeshProUGUI ResultBall;
    public TextMeshProUGUI ResultExp;

    [Header("GAMEPLAY HUD UI")]
    // Nhóm HUD chính trong lúc chơi, gồm vùng ẩn UI, joystick và combo.
    // public GameObject Hand;
    [Tooltip("Vùng ẩn UI cho chế độ xem")]
    public GameObject ZoneUINeedToHide; // ẩn khi ở chế độ xem 
    public GameObject UIMove;
    public GameObject UIJoystick;
    [Tooltip("Sprites dùng để hiển thị hiệu ứng combo, được sắp xếp từ x1 đến x6 và lớn hơn (x6+).")]
    public Sprite[] comboSprites;

    [Header("SHOOT ANGLE CONFIG")]
    // Nhóm cấu hình góc bắn, hiện dùng nội bộ để tính góc bắn khi thả joystick.
    // cho góc bắn không phải thanh lực tạm thời chưa dùng
    private Slider shootAngleSlider;
    [Tooltip("Góc bắn nhỏ nhất (độ).")] private float minShootAngle = 15f;
    [Tooltip("Góc bắn lớn nhất (độ).")] private float maxShootAngle = 70f;
    [Tooltip("Góc bắn mặc định (độ).")] private float defaultShootAngle = 35f;
    [Tooltip("Ẩn shootAngleSlider sau khi thả nút bắn (giây).")] private float shootAngleSliderAutoHideDelay = 2f;

    [Header("TUTORIAL UI")]
    // Nhóm UI hỗ trợ hướng dẫn trong chế độ online.
    public TutorialControllerOnline tutorialController;
    public GameObject BackgroundMesage;
    public TextMeshProUGUI showMes;

    [Header("TURN / TIMER UI")]
    // Nhóm text hiển thị lượt chơi, thời gian và vùng di chuyển.
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI currentTurnText;
    public GameObject moveRangeIndicator; // Vùng sáng di chuyển.
    public float moveDistance = 1.5f; // Tối đa 3 gang tay (~1.5 mét).

    // State hiển thị người chơi/camera: lưu trạng thái toggle của các nút trong HUD.
    private bool isHidePlayer = false;
    private bool useSecondFirstPersonCamera = false;
    private int consecutiveTimeouts = 0;
    private float nextDamagedBallNoticeRefreshTime;
    private bool hasTimerExpired;
    private bool hasLocalPlayerShot;
    private int? lastRemainingTicks;
    private int lastDisplayedSeconds = -1;
    private int lastBeepSecond = -1;
    private int lastLocalBallIndex = -1;

    // Cache dữ liệu bi local: lưu danh sách bi, coroutine load skill và thông tin bi đang chọn.
    private readonly List<BallPhysicsItem> localBallPhysicsItems = new();
    private Coroutine loadLocalBallSkillsRoutine;
    private Coroutine hideShootAngleSliderRoutine;

    // State nút thoát sau khi bị loại: điều khiển hiển thị và animation nhấp nháy.
    private Tween exitGameButtonBlinkTween;
    private bool lastExitGameButtonVisible;

    // State popup đổi bi: quản lý popup, animation, sprite và trạng thái nút đổi bi.
    private GameObject activeBallSwitchPopup;
    private Sequence ballSwitchPopupSequence;
    private bool isBallSwitchPopupAnimating;
    private Image nextBallButtonImage;
    private Sprite nextBallButtonInitialSprite;
    private int selectedBallInfoItemId = -1;
    private bool nextBallButtonDefaultActive = true;
    private bool hasCachedNextBallButtonDefaultActive;
    private bool lastZoneUiVisibilityForBallSwitch = true;
    private static Sprite ballSwitchCircleSprite;
    private static Sprite ballSwitchArcSprite;

    // State kỹ năng bi: quản lý tooltip giữ icon, scale icon và trạng thái tooltip runtime.
    private Coroutine ballSkillHoldRoutine;
    private Tween ballSkillIconScaleTween;
    private Vector3 ballSkillIconInitialScale = Vector3.one;
    private bool ballSkillTooltipVisible;
    private bool ballSkillTooltipRegistered;
    private bool ownsRuntimeBallSkillTooltip;

    // State di chuyển local: lưu vị trí bắt đầu để tính vùng di chuyển hợp lệ.
    private Vector3 startPosition;

    // Callback timeout: cho hệ thống gameplay đăng ký khi người chơi hết giờ.
    public System.Action OnLoseByTimeout;
    public System.Action OnTimeOut;

    // Cache avatar người chơi: tránh tải lại ảnh đại diện nhiều lần trong cùng trận.
    private readonly Dictionary<int, Texture2D> playerAvatarTextures = new();
    private readonly Dictionary<int, Sprite> playerAvatarSprites = new();
    private readonly Dictionary<int, Coroutine> playerAvatarLoaders = new();
    private readonly Dictionary<int, Coroutine> playerAvatarPreloaders = new();
    private bool hasRequestedAvatarGuidSync;
    private Coroutine roomAvatarPreloadRoutine;
    private bool roomAvatarPreloadCompleted;
    private static readonly Dictionary<int, Texture2D> preloadedMatchAvatarTextures = new();

    // State thông báo va chạm/hit: quản lý popup, shockwave, shake và motion blur.
    private Coroutine impactAnnouncementRoutine;
    private GameObject activeImpactAnnouncementObject;
    private GameObject activeDestroyPermissionAnnouncementObject;
    private Sequence destroyPermissionAnnouncementSequence;
    private Sequence ballHitAnnouncementSequence;
    private Sequence ballHitShockwaveSequence;
    private Tween ballHitScreenShakeTween;
    private bool warnedSharedBallHitImage;
    private GameObject activeRuntimeBallHitShockwaveObject;
    private readonly List<GameObject> activeBallHitMotionBlurObjects = new();
    private RectTransform activeBallHitScreenShakeRect;
    private Vector2 activeBallHitScreenShakeAnchoredPosition;
    private Transform activeBallHitScreenShakeTransform;
    private Vector3 activeBallHitScreenShakeLocalPosition;
    private float lastBallPlayerHitKengTime = -999f;

    // Tham số animation thông báo va chạm: thời lượng rơi, rung, giữ và ẩn hiệu ứng.
    private const float ImpactAnnouncementFallDuration = 0.55f;
    private const float ImpactAnnouncementShakeDuration = 0.28f;
    private const float ImpactAnnouncementFadeDuration = 0.3f;
    private const float DestroyPermissionPopDuration = 0.18f;
    private const float DestroyPermissionSettleDuration = 0.1f;
    private const float DestroyPermissionHoldDuration = 0.5f;
    private const float DestroyPermissionFadeDuration = 0.35f;
    private const float BallHitAnnouncementDropDuration = 0.16f;
    private const float BallHitAnnouncementStopDuration = 0.08f;
    private const float BallHitAnnouncementFreezeDuration = 3f;
    private const float BallHitAnnouncementHideDuration = 0.18f;
    private const float BallPlayerHitKengDedupeSeconds = 0.25f;
    private const string DestroyPermissionUnlockedText = "Đã mở quyền tiêu diệt!";
    private static readonly Color ImpactAnnouncementStartColor = Color.white;
    private static readonly Color ImpactAnnouncementEndColor = new Color(0.95f, 0.2f, 0.2f, 1f);
    private static readonly Color DestroyPermissionTextColor = new Color(1f, 0.86f, 0.22f, 1f);
    private static Sprite ballHitShockwaveSprite;
    private readonly Dictionary<int, PaperLegendHeroData> paperLegendHeroInfoDataByModelId = new Dictionary<int, PaperLegendHeroData>();
    private readonly HashSet<int> paperLegendHeroInfoLoadsInProgress = new HashSet<int>();
    private readonly HashSet<int> paperLegendHeroInfoLoadAttempted = new HashSet<int>();
    private readonly Dictionary<int, Sprite> paperLegendHeroIconSpritesByModelId = new Dictionary<int, Sprite>();
    private readonly HashSet<int> paperLegendHeroIconLoadsInProgress = new HashSet<int>();
    private float nextPaperLegendHeroInfoRefreshTime;
    private float nextPaperLegendScoreboardRefreshTime;
    private int lastPaperLegendHeroInfoModelId = -1;
    private bool lastPaperLegendHeroInfoHadApiData;
    private bool warnedPaperLegendHeroInfoApiMissing;

    #region UI MANAGER
    private void Awake()
    {
        Instance = this;
        ResolveHeroInfoUiReference();
        if (timerText != null)
            timerText.text = "0";
    }

    private void Update()
    {
        RefreshLocalBallSkillUiState();
        RefreshPaperLegendHeroInfoIfNeeded();
        RefreshPaperLegendScoreboardIfNeeded();
        SyncNextBallVisibilityWithZone();
        UpdateExitGameButtonState();

        if (damagedBallNotice == null || damagedBallNoticeRefreshInterval <= 0f)
            return;

        if (Time.time < nextDamagedBallNoticeRefreshTime)
            return;

        nextDamagedBallNoticeRefreshTime = Time.time + damagedBallNoticeRefreshInterval;
        UpdateDamagedBallNotice();
    }

    private void OnDestroy()
    {
        StopActiveImpactAnnouncement();
        StopDestroyPermissionUnlockedAnnouncement();
        StopBallPlayerHitAnnouncement();
        StopExitGameButtonBlink();
        HideBallSwitchPopup(false);
        ClearAvatarCache();
        if (Instance == this)
        {
            Instance = null;
        }
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
        if (cameraSwitchButton != null)
            cameraSwitchButton.onClick.RemoveListener(OnClickSwitchFirstPersonCamera);
        if (nextBallButton != null)
            nextBallButton.onClick.RemoveListener(OnClickSwitchBall);
        if (exitGameButton != null)
            exitGameButton.onClick.RemoveListener(OnClickExitGameAfterDestroyed);

        StopBallSkillHoldRoutine();
        HideBallSkillTooltip(false);
        ballSkillIconScaleTween?.Kill();
        if (ownsRuntimeBallSkillTooltip && ballSkillTooltipRoot != null)
            Destroy(ballSkillTooltipRoot);

        UnregisterActionAnimationButtons();
    }
    void Start()
    {
        //inputField.characterValidation = TMP_InputField.CharacterValidation.Integer; // Chỉ nhập số
      //  inputField.onValueChanged.AddListener(ValidateInput);
       // startButton.onClick.AddListener(PlaceBet);
        ConfigureShootAngleSlider();
        ConfigureShootAngleSliderVisibilityByJoystick();
        RegisterSettingsButton();
        RegisterCameraSwitchButton();
        RegisterExitGameButton();
        RegisterNextBallButton();
        BindBallSkillUi();
        RegisterBallSkillTooltip();
        InitializeBallHitAnnouncementUi();
        ResolveHeroInfoUiReference();
        StartLoadLocalBallSkills();
    }

    private void BindBallSkillUi()
    {
        var ballSkillManager = BallActiveSkillManager.EnsureInstance();
        if (ballSkillManager != null)
            ballSkillManager.BindUi(ballSkillImages, ballSkillButtons);
    }

    private void StartLoadLocalBallSkills()
    {
        if (loadLocalBallSkillsRoutine != null)
            StopCoroutine(loadLocalBallSkillsRoutine);

        loadLocalBallSkillsRoutine = StartCoroutine(LoadLocalBallSkillsRoutine());
    }

    private IEnumerator LoadLocalBallSkillsRoutine()
    {
        int userId = 0;
        float timeout = 5f;
        while (timeout > 0f)
        {
            userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
            if (userId > 0 && APIManager.Instance != null)
                break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        if (userId <= 0 || APIManager.Instance == null)
            yield break;

        List<PlayerBallPhysics> physicsData = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetBallPhysicsAsync(new List<int> { userId }),
            result => physicsData = result));

        var local = physicsData != null ? physicsData.FirstOrDefault(x => x.playerId == userId) : null;
        localBallPhysicsItems.Clear();
        if (local?.physics != null)
            localBallPhysicsItems.AddRange(local.physics);

        var ballSkillManager = BallActiveSkillManager.Instance;
        if (ballSkillManager != null)
            ballSkillManager.SetLocalBallSkills(local != null ? local.physics : null);

        RefreshLocalBallSkillUiState(true);
    }

    private void RefreshLocalBallSkillUiState(bool force = false)
    {
        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (userId <= 0 || !TryGetReadyNetworkManager(out var manager))
            return;

        int currentIndex = manager.GetCurrentBallIndex(userId);
        if (!force && currentIndex == lastLocalBallIndex)
            return;

        bool changedBall = currentIndex != lastLocalBallIndex;
        lastLocalBallIndex = currentIndex;
        var ballSkillManager = BallActiveSkillManager.Instance;
        if (ballSkillManager != null)
            ballSkillManager.SetCurrentBallIndex(currentIndex);

        if (changedBall)
            HideBallSkillTooltip(false);

        RefreshSelectedBallInfoUi(currentIndex);
    }

    private void RefreshSelectedBallInfoUi(int currentIndex)
    {
        if (selectedBallNameText == null && selectedBallImage == null && selectedBallStatText == null)
            return;

        var ballInfo = ResolveSelectedBallInfo(currentIndex);
        if (ballInfo == null)
        {
            if (selectedBallNameText != null)
                selectedBallNameText.text = string.Empty;
            if (selectedBallStatText != null)
                selectedBallStatText.text = string.Empty;
            if (selectedBallImage != null)
                selectedBallImage.gameObject.SetActive(false);
            selectedBallInfoItemId = -1;
            return;
        }

        if (selectedBallNameText != null)
        {
            selectedBallNameText.gameObject.SetActive(true);
            selectedBallNameText.text = ResolveLocalizedText(ballInfo.name);
        }

        if (selectedBallStatText != null)
        {
            selectedBallStatText.gameObject.SetActive(true);
            selectedBallStatText.richText = true;
            float speed = ItemVisualHelper.CalculateSpeedFromStats(
                ballInfo.Mass,
                ballInfo.GravityScale,
                ballInfo.Drag,
                ballInfo.Bounciness,
                ballInfo.Elasticity,
                ballInfo.ImpactResistance);
            float damagePercent = ItemVisualHelper.GetDamagePercent(ballInfo.ImpactResistance, Mathf.Max(ballInfo.damage, 0f));
            selectedBallStatText.text = ItemVisualHelper.BuildScaledStatInfo(
                ballInfo.Mass,
                speed,
                ballInfo.Bounciness,
                ballInfo.ImpactResistance,
                damagePercent);
        }

        RefreshSelectedBallImage(ballInfo.itemId);
    }

    private void RefreshCurrentSelectedBallInfoUi()
    {
        RefreshSelectedBallInfoUi(ResolveCurrentBallIndex());
    }

    private BallPhysicsItem ResolveSelectedBallInfo(int currentIndex)
    {
        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        var serverRpc = GameManagerNetWork.Instance?.serverRPC;
        var ballInfo = userId > 0 && serverRpc != null ? serverRpc.GetBallPhysics(userId) : null;
        BallPhysicsItem cachedInfo = currentIndex >= 0 && currentIndex < localBallPhysicsItems.Count
            ? localBallPhysicsItems[currentIndex]
            : null;

        BallPhysicsStruct data = default;
        BallServerController activeBall = null;
        bool hasNetworkInfo = ballInfo.HasValue;
        if (hasNetworkInfo)
        {
            data = ballInfo.Value.data;
            activeBall = ballInfo.Value.active;
        }

        if (cachedInfo == null && !hasNetworkInfo)
            return null;

        int skillId = hasNetworkInfo ? data.skillGenCode : cachedInfo?.activeSkill?.GenCode ?? 0;
        var activeSkill = skillId > 0
            ? ResolveActiveBallSkill(userId, currentIndex, skillId) ??
              cachedInfo?.activeSkill ??
              BallActiveSkillManager.Instance?.GetCurrentActiveSkill()
            : null;

        return new BallPhysicsItem
        {
            name = hasNetworkInfo && !string.IsNullOrWhiteSpace(data.name.ToString()) ? data.name.ToString() : cachedInfo?.name,
            itemId = activeBall != null ? activeBall.BallMaterialId : cachedInfo?.itemId ?? 0,
            seqItem = activeBall != null ? activeBall.BallItemSeq : cachedInfo?.seqItem ?? 0,
            SkillGid = skillId > 0 ? skillId : (int?)null,
            activeSkill = activeSkill,
            Mass = hasNetworkInfo ? data.Mass : cachedInfo?.Mass ?? 0f,
            GravityScale = hasNetworkInfo ? data.GravityScale : cachedInfo?.GravityScale ?? 0f,
            Drag = hasNetworkInfo ? data.Drag : cachedInfo?.Drag ?? 0f,
            Bounciness = hasNetworkInfo ? data.Bounciness : cachedInfo?.Bounciness ?? 0f,
            Elasticity = hasNetworkInfo ? data.Elasticity : cachedInfo?.Elasticity ?? 0f,
            ImpactResistance = hasNetworkInfo ? data.ImpactResistance : cachedInfo?.ImpactResistance ?? 0f,
            level = activeBall != null ? Mathf.Max(activeBall.BallLevel, 1) : Mathf.Max(cachedInfo?.level ?? 1, 1),
            isCateye = activeBall != null ? activeBall.HasCateye : cachedInfo != null && cachedInfo.isCateye,
            damage = activeBall != null ? activeBall.GetDamageAmount() : cachedInfo?.damage ?? 0f
        };
    }

    private void RefreshSelectedBallImage(int itemId)
    {
        if (selectedBallImage == null)
            return;

        selectedBallInfoItemId = itemId;
        if (itemId <= 0)
        {
            selectedBallImage.gameObject.SetActive(false);
            selectedBallImage.sprite = null;
            return;
        }

        selectedBallImage.gameObject.SetActive(true);
        selectedBallImage.preserveAspect = true;
        selectedBallImage.sprite = ItemVisualHelper.LoadSpriteByID(itemId);
        StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{itemId}.png", sprite =>
        {
            if (selectedBallImage != null && selectedBallInfoItemId == itemId && sprite != null)
                selectedBallImage.sprite = sprite;
        }));
    }

    private void RegisterBallSkillTooltip()
    {
        if (ballSkillTooltipRegistered)
            return;

        GameObject triggerTarget = ballSkillButtons != null
            ? ballSkillButtons.gameObject
            : ballSkillImages != null ? ballSkillImages.gameObject : null;
        if (triggerTarget == null)
            return;

        ballSkillTooltipRegistered = true;
        if (ballSkillImages != null)
            ballSkillIconInitialScale = ballSkillImages.transform.localScale;

        var trigger = triggerTarget.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = triggerTarget.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        AddBallSkillTrigger(trigger, EventTriggerType.PointerDown, OnBallSkillPointerDown);
        AddBallSkillTrigger(trigger, EventTriggerType.PointerUp, OnBallSkillPointerUp);
        AddBallSkillTrigger(trigger, EventTriggerType.PointerExit, OnBallSkillPointerExit);
        HideBallSkillTooltip(false);
    }

    private void AddBallSkillTrigger(EventTrigger trigger, EventTriggerType eventType, Action<BaseEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(data => callback?.Invoke(data));
        trigger.triggers.Add(entry);
    }

    private void OnBallSkillPointerDown(BaseEventData _)
    {
        if (ResolveCurrentBallSkill() == null)
            return;

        StopBallSkillHoldRoutine();
        ballSkillHoldRoutine = StartCoroutine(BallSkillHoldRoutine());
    }

    private void OnBallSkillPointerUp(BaseEventData _)
    {
        bool wasTooltipVisible = ballSkillTooltipVisible;
        StopBallSkillHoldRoutine();
        HideBallSkillTooltip(true);

        if (wasTooltipVisible)
            BallActiveSkillManager.Instance?.SuppressNextSkillClick(0.35f);
    }

    private void OnBallSkillPointerExit(BaseEventData _)
    {
        StopBallSkillHoldRoutine();
        HideBallSkillTooltip(true);
    }

    private IEnumerator BallSkillHoldRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Clamp(ballSkillTooltipHoldDuration, 0.3f, 0.5f));

        ballSkillHoldRoutine = null;
        if (ResolveCurrentBallSkill() == null)
            yield break;

        BallActiveSkillManager.Instance?.SuppressNextSkillClick(0.35f);
        ShowBallSkillTooltip();
        AnimateBallSkillIcon(true);
    }

    private void StopBallSkillHoldRoutine()
    {
        if (ballSkillHoldRoutine == null)
            return;

        StopCoroutine(ballSkillHoldRoutine);
        ballSkillHoldRoutine = null;
    }

    private ActiveSkillSchema ResolveCurrentBallSkill()
    {
        int currentIndex = ResolveCurrentBallIndex();
        if (currentIndex >= 0 && currentIndex < localBallPhysicsItems.Count)
        {
            var skill = localBallPhysicsItems[currentIndex]?.activeSkill;
            if (skill != null && skill.GenCode > 0)
                return skill;
        }

        return BallActiveSkillManager.Instance?.GetCurrentActiveSkill();
    }

    private int ResolveCurrentBallIndex()
    {
        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (userId > 0 && TryGetReadyNetworkManager(out var manager))
            return manager.GetCurrentBallIndex(userId);

        return lastLocalBallIndex;
    }

    private void ShowBallSkillTooltip()
    {
        var skill = ResolveCurrentBallSkill();
        if (skill == null)
            return;

        EnsureBallSkillTooltip();
        if (ballSkillTooltipRoot == null || ballSkillTooltipText == null)
            return;

        ballSkillTooltipVisible = true;
        ballSkillTooltipText.richText = true;
        ballSkillTooltipText.enableWordWrapping = true;
        ballSkillTooltipText.text = BuildBallSkillTooltipText(skill);
        PositionBallSkillTooltip();

        ballSkillTooltipRoot.SetActive(true);
        ballSkillTooltipRoot.transform.SetAsLastSibling();

        var group = ballSkillTooltipRoot.GetComponent<CanvasGroup>();
        if (group == null)
            group = ballSkillTooltipRoot.AddComponent<CanvasGroup>();
        group.DOKill();
        group.alpha = 0f;
        group.DOFade(1f, 0.12f).SetUpdate(true);
    }

    private void HideBallSkillTooltip(bool animate)
    {
        ballSkillTooltipVisible = false;
        AnimateBallSkillIcon(false);

        if (ballSkillTooltipRoot == null)
            return;

        var group = ballSkillTooltipRoot.GetComponent<CanvasGroup>();
        if (group == null)
            group = ballSkillTooltipRoot.AddComponent<CanvasGroup>();
        group.DOKill();

        if (!animate)
        {
            group.alpha = 0f;
            ballSkillTooltipRoot.SetActive(false);
            return;
        }

        group.DOFade(0f, 0.1f)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (ballSkillTooltipRoot != null)
                    ballSkillTooltipRoot.SetActive(false);
            });
    }

    private void AnimateBallSkillIcon(bool enlarged)
    {
        if (ballSkillImages == null)
            return;

        var target = ballSkillImages.transform;
        ballSkillIconScaleTween?.Kill();
        Vector3 targetScale = enlarged
            ? ballSkillIconInitialScale * Mathf.Max(ballSkillTooltipIconScale, 1f)
            : ballSkillIconInitialScale;
        ballSkillIconScaleTween = target.DOScale(targetScale, 0.12f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void EnsureBallSkillTooltip()
    {
        if (ballSkillTooltipRoot == null && ballSkillTooltipText != null)
            ballSkillTooltipRoot = ballSkillTooltipText.gameObject;

        if (ballSkillTooltipRoot != null && ballSkillTooltipText == null)
            ballSkillTooltipText = ballSkillTooltipRoot.GetComponentInChildren<TextMeshProUGUI>(true);

        if (ballSkillTooltipRoot != null && ballSkillTooltipText != null)
            return;

        if (ballSkillTooltipRoot == null)
        {
            Transform parent = canvasTransform != null ? canvasTransform : transform;
            ballSkillTooltipRoot = new GameObject("BallSkillTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            ballSkillTooltipRoot.transform.SetParent(parent, false);
            ownsRuntimeBallSkillTooltip = true;

            var rootRect = ballSkillTooltipRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.sizeDelta = new Vector2(380f, 178f);

            var background = ballSkillTooltipRoot.GetComponent<Image>();
            background.color = new Color(0.025f, 0.03f, 0.04f, 0.94f);
            background.raycastTarget = false;
        }

        var textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(ballSkillTooltipRoot.transform, false);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 14f);
        textRect.offsetMax = new Vector2(-18f, -14f);

        ballSkillTooltipText = textObj.GetComponent<TextMeshProUGUI>();
        ballSkillTooltipText.fontSize = 22f;
        ballSkillTooltipText.color = Color.white;
        ballSkillTooltipText.alignment = TextAlignmentOptions.TopLeft;
        ballSkillTooltipText.raycastTarget = false;
        if (ringBallText != null && ringBallText.font != null)
            ballSkillTooltipText.font = ringBallText.font;

        ballSkillTooltipRoot.SetActive(false);
    }

    private void PositionBallSkillTooltip()
    {
        if (ballSkillTooltipRoot == null)
            return;

        var rootRect = ballSkillTooltipRoot.GetComponent<RectTransform>();
        var parentRect = rootRect != null ? rootRect.parent as RectTransform : null;
        var sourceRect = ballSkillImages != null
            ? ballSkillImages.rectTransform
            : ballSkillButtons != null ? ballSkillButtons.GetComponent<RectTransform>() : null;

        if (rootRect == null || parentRect == null || sourceRect == null)
            return;

        var canvas = parentRect.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, sourceRect.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out var localPoint))
        {
            rootRect.anchoredPosition = localPoint + ballSkillTooltipOffset;
        }
    }

    private string BuildBallSkillTooltipText(ActiveSkillSchema skill)
    {
        string nameKey = !string.IsNullOrWhiteSpace(skill.GenName) ? skill.GenName : string.Empty;
        string skillName = !string.IsNullOrWhiteSpace(nameKey)
            ? ResolveLocalizedText(nameKey)
            : $"Skill {skill.GenCode}";
        string manaText = skill.mana > 0 ? skill.mana.ToString() : "--";
        string cooldownText = skill.cooldown > 0f ? $"{skill.cooldown:0.#}s" : "--";
        string description = !string.IsNullOrWhiteSpace(skill.description)
            ? ItemVisualHelper.ConvertSimpleHtmlToTmp(ResolveLocalizedText(skill.description))
            : string.Empty;

        string result = $"<b>{skillName}</b>\nMana: {manaText}   Cooldown: {cooldownText}";
        if (!string.IsNullOrWhiteSpace(description))
            result += "\n" + description;

        return result;
    }

    private static string ResolveLocalizedText(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        return LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(key)
            : key;
    }

    private void RegisterSettingsButton()
    {
        if (settingsButton == null)
            return;

        settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
        settingsButton.onClick.AddListener(OnSettingsButtonClicked);
    }

    private void RegisterCameraSwitchButton()
    {
        if (cameraSwitchButton == null)
            return;

        cameraSwitchButton.onClick.RemoveListener(OnClickSwitchFirstPersonCamera);
        cameraSwitchButton.onClick.AddListener(OnClickSwitchFirstPersonCamera);
        UpdateCameraSwitchButtonState();
    }

    private void RegisterExitGameButton()
    {
        if (exitGameButton == null)
            return;

        exitGameButton.onClick.RemoveListener(OnClickExitGameAfterDestroyed);
        exitGameButton.onClick.AddListener(OnClickExitGameAfterDestroyed);
        UpdateExitGameButtonState();
    }

    private void RegisterNextBallButton()
    {
        if (nextBallButton == null)
            return;

        CacheNextBallButtonDefaultActive();
        nextBallButton.onClick.RemoveListener(OnClickSwitchBall);
        nextBallButton.onClick.AddListener(OnClickSwitchBall);

        CacheNextBallButtonImage();
        ApplyNextBallButtonSprite(activeBallSwitchPopup != null);
        SyncNextBallVisibilityWithZone(true);
    }

    private void CacheNextBallButtonDefaultActive()
    {
        if (nextBallButton == null || hasCachedNextBallButtonDefaultActive)
            return;

        nextBallButtonDefaultActive = nextBallButton.gameObject.activeSelf;
        hasCachedNextBallButtonDefaultActive = true;
    }

    private void SyncNextBallVisibilityWithZone(bool force = false)
    {
        if (nextBallButton == null || ZoneUINeedToHide == null)
            return;

        CacheNextBallButtonDefaultActive();
        bool zoneVisible = ZoneUINeedToHide.activeInHierarchy;
        if (!force && zoneVisible == lastZoneUiVisibilityForBallSwitch)
            return;

        lastZoneUiVisibilityForBallSwitch = zoneVisible;
        bool shouldShowButton = zoneVisible && nextBallButtonDefaultActive;
        if (nextBallButton.gameObject.activeSelf != shouldShowButton)
            nextBallButton.gameObject.SetActive(shouldShowButton);

        if (!zoneVisible && activeBallSwitchPopup != null)
            HideBallSwitchPopup(false);

        if (!zoneVisible)
            HideBallSkillTooltip(false);
    }

    private void CacheNextBallButtonImage()
    {
        if (nextBallButton == null)
            return;

        nextBallButtonImage = nextBallButton.GetComponent<Image>();
        if (nextBallButtonImage == null)
            nextBallButtonImage = nextBallButton.targetGraphic as Image;

        if (nextBallButtonImage == null)
            return;

        if (nextBallButtonInitialSprite == null)
            nextBallButtonInitialSprite = nextBallButtonImage.sprite;

        if (nextBallButtonNormalSprite == null)
            nextBallButtonNormalSprite = nextBallButtonInitialSprite;
    }

    private void ApplyNextBallButtonSprite(bool isActive)
    {
        if (nextBallButton == null)
            return;

        if (nextBallButtonImage == null)
            CacheNextBallButtonImage();

        if (nextBallButtonImage == null)
            return;

        Sprite targetSprite = isActive ? nextBallButtonActiveSprite : nextBallButtonNormalSprite;
        if (targetSprite == null)
            targetSprite = nextBallButtonNormalSprite != null ? nextBallButtonNormalSprite : nextBallButtonInitialSprite;

        if (targetSprite != null)
            nextBallButtonImage.sprite = targetSprite;
    }

    private void OnSettingsButtonClicked()
    {
        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("UIControllerOnline: PopupHelper instance is not available.");
            return;
        }

        PopupHelper.Instance.ShowGameSettingsPopup();
    }

    private void ConfigureShootAngleSlider()
    {
        if (shootAngleSlider == null)
            return;

        shootAngleSlider.minValue = minShootAngle;
        shootAngleSlider.maxValue = maxShootAngle;
        shootAngleSlider.value = Mathf.Clamp(defaultShootAngle, minShootAngle, maxShootAngle);
        shootAngleSlider.gameObject.SetActive(false);
    }

    private void ConfigureShootAngleSliderVisibilityByJoystick()
    {
        if (UIJoystick == null)
            return;

        var trigger = UIJoystick.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = UIJoystick.AddComponent<EventTrigger>();

        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();
        trigger.triggers.RemoveAll(entry =>
            entry.eventID == EventTriggerType.PointerDown ||
            entry.eventID == EventTriggerType.PointerUp ||
            entry.eventID == EventTriggerType.PointerClick);

        AddEventTrigger(trigger, EventTriggerType.PointerDown, _ => ShowShootAngleSlider());
        AddEventTrigger(trigger, EventTriggerType.PointerClick, _ => ShowShootAngleSlider());
        AddEventTrigger(trigger, EventTriggerType.PointerUp, _ => HideShootAngleSliderWithDelay());
    }

    private void AddEventTrigger(EventTrigger trigger, EventTriggerType eventType, Action<BaseEventData> callback)
    {
        if (trigger == null || callback == null)
            return;

        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(data => callback.Invoke(data));
        trigger.triggers.Add(entry);
    }

    private void ShowShootAngleSlider()
    {
        if (shootAngleSlider == null)
            return;

        if (hideShootAngleSliderRoutine != null)
        {
            StopCoroutine(hideShootAngleSliderRoutine);
            hideShootAngleSliderRoutine = null;
        }

        shootAngleSlider.gameObject.SetActive(true);
    }

    private void HideShootAngleSliderWithDelay()
    {
        if (shootAngleSlider == null)
            return;

        if (hideShootAngleSliderRoutine != null)
            StopCoroutine(hideShootAngleSliderRoutine);

        hideShootAngleSliderRoutine = StartCoroutine(HideShootAngleSliderRoutine());
    }

    private IEnumerator HideShootAngleSliderRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, shootAngleSliderAutoHideDelay));
        if (shootAngleSlider != null)
            shootAngleSlider.gameObject.SetActive(false);
        hideShootAngleSliderRoutine = null;
    }

    public float GetShootAngle()
    {
        float minAngle = Mathf.Min(minShootAngle, maxShootAngle);
        float maxAngle = Mathf.Max(minShootAngle, maxShootAngle);
        float fallbackAngle = Mathf.Clamp(defaultShootAngle, minAngle, maxAngle);
        return shootAngleSlider != null ? Mathf.Clamp(shootAngleSlider.value, minAngle, maxAngle) : fallbackAngle;
    }

    private bool TryGetReadyNetworkManager(out NetworkObjectManager manager)
    {
        manager = NetworkObjectManager.Instance;
        return manager != null && manager.IsNetworkStateReady;
    }

    private List<PlayerInfoStruct> GetPlayersOrderedByTurn(bool descending = false)
    {
        if (!TryGetReadyNetworkManager(out var manager))
            return new List<PlayerInfoStruct>();

        var players = manager.GetOrderedPlayerInfos();
        if (descending)
            players.Sort((a, b) => b.turnOrder.CompareTo(a.turnOrder));
        return players;
    }

    private PlayerNetworkHandler GetPlayerHandler(int playerId)
    {
        if (!TryGetReadyNetworkManager(out var manager))
            return null;

        var playerGO = manager.GetPlayerObject(playerId);
        return playerGO != null ? playerGO.GetComponent<PlayerNetworkHandler>() : null;
    }

    private PlayerNetworkHandler GetLocalPlayerHandler()
    {
        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        return userId > 0 ? GetPlayerHandler(userId) : null;
    }

    public bool MoveCameraToCurrentFirstPersonView(PlayerNetworkHandler handler)
    {
        if (handler == null || CameraRotation.Instance == null)
            return false;

        if (useSecondFirstPersonCamera)
        {
            if (handler.FPPPositionCam2 == null || handler.PointPositionCam2 == null)
            {
                useSecondFirstPersonCamera = false;
                UpdateCameraSwitchButtonState();
            }
            else
            {
                CameraRotation.Instance.MoveCameraToFPPOnline(handler.FPPPositionCam2, handler.PointPositionCam2);
                UpdateCameraSwitchButtonState();
                return true;
            }
        }

        if (handler.FPPPosition == null || handler.PointPosition == null)
            return false;

        CameraRotation.Instance.MoveCameraToFPPOnline(handler.FPPPosition, handler.PointPosition);
        UpdateCameraSwitchButtonState();
        return true;
    }

    private void OnClickSwitchFirstPersonCamera()
    {
        var handler = GetLocalPlayerHandler();
        if (handler == null)
            return;

        bool targetCam2 = !useSecondFirstPersonCamera;
        if (targetCam2 && (handler.FPPPositionCam2 == null || handler.PointPositionCam2 == null))
        {
            ShowMesByUser("CAM2 hoặc PointPositionCam2 chưa được gán.");
            UpdateCameraSwitchButtonState();
            return;
        }

        useSecondFirstPersonCamera = targetCam2;
        MoveCameraToCurrentFirstPersonView(handler);
    }

    public void UpdateCameraSwitchButtonState()
    {
        if (cameraSwitchButton == null)
            return;

        var handler = GetLocalPlayerHandler();
        bool canSwitch = CameraRotation.Instance != null &&
                         handler != null &&
                         handler.FPPPosition != null &&
                         handler.PointPosition != null &&
                         handler.FPPPositionCam2 != null &&
                         handler.PointPositionCam2 != null;

        cameraSwitchButton.interactable = canSwitch;

        var img = cameraSwitchButton.GetComponent<Image>();
        if (img != null)
            img.color = canSwitch ? Color.white : Color.gray;

        var label = cameraSwitchButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = useSecondFirstPersonCamera ? "CAM2" : "CAM1";
    }

    private PlayerInfoStruct GetDisplayPlayerInfo(PlayerInfoStruct fallbackInfo, PlayerNetworkHandler handler)
    {
        if (handler != null)
            return handler.PlayerModel;

        return fallbackInfo;
    }

    private void EnsureAvatarGuidSyncRequested()
    {
        if (hasRequestedAvatarGuidSync)
            return;

        if (!TryGetReadyNetworkManager(out var manager))
            return;

        manager.RpcRequestPlayerAvatarGuidSync();
        hasRequestedAvatarGuidSync = true;
    }

    private bool TryGetAvatarGuidFromManager(int playerId, out string guid)
    {
        guid = null;

        if (!TryGetReadyNetworkManager(out var manager))
            return false;

        return manager.TryGetPlayerAvatarGuid(playerId, out guid);
    }

    private AuthenticationProviderType ResolveProviderType(string providerType)
    {
        if (!string.IsNullOrEmpty(providerType) && Enum.TryParse(providerType, true, out AuthenticationProviderType parsed))
        {
            return parsed;
        }

        return AuthenticationProviderType.Anonymous;
    }

    public void BeginPreloadRoomAvatars()
    {
        if (roomAvatarPreloadRoutine != null || roomAvatarPreloadCompleted)
        {
            Debug.Log($"[UI] BeginPreloadRoomAvatars: đã chạy hoặc đã hoàn tất (routine={roomAvatarPreloadRoutine != null}, completed={roomAvatarPreloadCompleted}).");
            return;
        }

        Debug.Log("[UI] BeginPreloadRoomAvatars: bắt đầu preload danh sách avatar phòng.");
        roomAvatarPreloadRoutine = StartCoroutine(PreloadRoomAvatarsRoutine());
    }

    public IEnumerator WaitForRoomAvatarPreload()
    {
        if (roomAvatarPreloadRoutine != null)
        {
            yield return roomAvatarPreloadRoutine;
        }
    }

    private IEnumerator PreloadRoomAvatarsRoutine()
    {
        roomAvatarPreloadCompleted = false;
        Debug.Log("[UI] PreloadRoomAvatarsRoutine: khởi tạo danh sách preload avatar.");

        var players = GetPlayersOrderedByTurn();
        if (players.Count == 0)
        {
            Debug.LogWarning("[UI] PreloadRoomAvatarsRoutine: không tìm thấy người chơi để preload.");
            roomAvatarPreloadRoutine = null;
            roomAvatarPreloadCompleted = false;
            yield break;
        }

        Debug.Log($"[UI] PreloadRoomAvatarsRoutine: tổng số người chơi={players.Count}.");
        EnsureAvatarGuidSyncRequested();

        foreach (var entry in players)
        {
            var handler = GetPlayerHandler(entry.playerId);
            if (handler == null)
            {
                Debug.LogWarning($"[UI] PreloadRoomAvatarsRoutine: không tìm thấy handler cho player {entry.playerId}.");
                continue;
            }

            var player = handler.PlayerModel;
            if (player.isDestroy || player.playerId <= 0)
            {
                Debug.LogWarning($"[UI] PreloadRoomAvatarsRoutine: bỏ qua player {entry.playerId} (isDestroy={player.isDestroy}, playerId={player.playerId}).");
                continue;
            }

            if (playerAvatarTextures.TryGetValue(player.playerId, out var cachedTexture) && cachedTexture != null)
            {
                Debug.Log($"[UI] PreloadRoomAvatarsRoutine: đã có cache texture cho player {player.playerId}, bỏ qua preload.");
                continue;
            }

            if (TryConsumePreloadedMatchAvatar(player.playerId, out var preloadedTexture) && preloadedTexture != null)
            {
                Debug.Log($"[UI] PreloadRoomAvatarsRoutine: dùng texture preload sẵn cho player {player.playerId}.");
                playerAvatarTextures[player.playerId] = preloadedTexture;
                continue;
            }

            if (playerAvatarPreloaders.ContainsKey(player.playerId))
            {
                Debug.Log($"[UI] PreloadRoomAvatarsRoutine: preload đã chạy cho player {player.playerId}, bỏ qua.");
                continue;
            }

            Debug.Log($"[UI] PreloadRoomAvatarsRoutine: bắt đầu coroutine preload cho player {player.playerId}.");
            var routine = StartCoroutine(PreloadPlayerAvatarRoutine(player));
            playerAvatarPreloaders[player.playerId] = routine;
            yield return routine;
        }

        playerAvatarPreloaders.Clear();
        roomAvatarPreloadRoutine = null;
        roomAvatarPreloadCompleted = true;
        Debug.Log("[UI] PreloadRoomAvatarsRoutine: hoàn tất preload avatar phòng.");
    }

    private IEnumerator PreloadPlayerAvatarRoutine(PlayerInfoStruct player)
    {
        int playerId = player.playerId;
        if (playerId <= 0)
        {
            Debug.LogWarning("[UI] PreloadPlayerAvatarRoutine: playerId không hợp lệ.");
            yield break;
        }

        var providerType = ResolveProviderType(player.providerType.ToString());
        var avatarUrl = player.avatarUrl.ToString();
        string guid = player.idAccount.ToString();

        yield return ResolveAvatarGuidRoutine(playerId, guid, resolved => guid = resolved);

        Debug.Log($"[UI] Preload avatar player {playerId}: avatarUrl='{avatarUrl}', guid='{guid}', providerType='{providerType}'.");

        if (string.IsNullOrEmpty(avatarUrl) && string.IsNullOrEmpty(guid))
        {
            if (!Application.isEditor)
            {
                Debug.LogWarning($"[UI] Không thể xác định nguồn avatar cho người chơi {playerId} (preload).");
            }
            playerAvatarPreloaders.Remove(playerId);
            yield break;
        }

        bool allowStorageFallback = providerType != AuthenticationProviderType.GooglePlayGames &&
                                    providerType != AuthenticationProviderType.Google;

        if (string.IsNullOrWhiteSpace(avatarUrl) && !string.IsNullOrWhiteSpace(guid))
        {
            allowStorageFallback = true;
        }

        Texture2D texture = null;
        string errorMessage = null;
        bool isDone = false;

        var avatarService = AvatarService.EnsureInstance();
        if (avatarService == null)
        {
            playerAvatarPreloaders.Remove(playerId);
            yield break;
        }

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

        playerAvatarPreloaders.Remove(playerId);

        if (texture == null)
        {
            if (!Application.isEditor)
            {
                Debug.LogWarning($"[UI] Không thể preload avatar cho người chơi {playerId}: {errorMessage}");
            }
            yield break;
        }

        playerAvatarTextures[playerId] = texture;
    }

    private IEnumerator ResolveAvatarGuidRoutine(int playerId, string firebaseUid, Action<string> onResolved)
    {
        string guid = firebaseUid;

        if (string.IsNullOrEmpty(guid) && TryGetAvatarGuidFromManager(playerId, out var managerGuid) && !string.IsNullOrEmpty(managerGuid))
        {
            guid = managerGuid;
        }
        else if (string.IsNullOrEmpty(guid))
        {
            const float waitTimeout = 2f;
            float waited = 0f;

            while (string.IsNullOrEmpty(guid) && waited < waitTimeout)
            {
                if (TryGetAvatarGuidFromManager(playerId, out managerGuid) && !string.IsNullOrEmpty(managerGuid))
                {
                    guid = managerGuid;
                    break;
                }

                waited += Time.deltaTime;
                yield return null;
            }
        }

        if (string.IsNullOrEmpty(guid))
        {
            int loginUserId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
            if (playerId == loginUserId)
            {
                guid = GameManagerNetWork.Instance?.loginUserModel?.Token;
            }
        }

        if (string.IsNullOrEmpty(guid) && APIManager.Instance != null)
        {
            var profileTask = APIManager.Instance.GetPlayerInventoryAsync(playerId);
            while (profileTask != null && !profileTask.IsCompleted)
            {
                yield return null;
            }

            if (profileTask != null && profileTask.Status == TaskStatus.RanToCompletion && profileTask.Result != null)
            {
                guid = profileTask.Result.IdAccount;
            }
            else if (profileTask != null && profileTask.IsFaulted)
            {
                Debug.LogWarning($"[UI] Không thể lấy GUID avatar cho người chơi {playerId}: {profileTask.Exception?.GetBaseException().Message}");
            }
        }

        onResolved?.Invoke(guid);
    }


    // hảm hiển thị thông báo trái = > phải
    public IEnumerator ShowTurnIndicatorRunTime(string message, float speed, float delay)
    {
        // Tạo chữ từ Prefab
        GameObject textObj = Instantiate(messagePrefab, canvasTransform);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        TextMeshProUGUI textMesh = textObj.GetComponent<TextMeshProUGUI>();

        if (textMesh == null || textRect == null)
        {
            Debug.LogError("Prefab thiếu TextMeshProUGUI hoặc RectTransform!");
            yield break; // Dừng lại nếu không có TextMeshProUGUI hoặc RectTransform
        }

        // Gán nội dung tin nhắn
        textMesh.text = message;

        // Đặt vị trí ban đầu (ngoài màn hình bên phải)
        float startX = Screen.width / 2 + moveDistance;
        float middleX = 0; // Vị trí giữa màn hình
        float endX = -Screen.width / 2 - moveDistance; // Rời khỏi màn hình bên trái

        textRect.anchoredPosition = new Vector2(startX, 0);

        // Đi vào màn hình (vị trí giữa) bằng DOTween
        Tween tween = textRect.DOAnchorPosX(middleX, speed);
        yield return tween.WaitForCompletion();

        // Giữ nguyên vị trí delay giây
        yield return new WaitForSeconds(delay);

        // Đi ra khỏi màn hình (vị trí bên trái)
        tween = textRect.DOAnchorPosX(endX, speed);
        yield return tween.WaitForCompletion();

        // Xóa object sau khi hoàn thành
        Destroy(textObj);
    }

    public void ShowTurnIndicator(string message,float speed, float delay)
    {
        // Tạo chữ từ Prefab
        //  BackgroundMesage.SetActive(true);
        //if (isYourTurn)
        //    SoundManager.Instance.PlayYourTurn();
        //else
        //    SoundManager.Instance.PlayOpponentTurn();
        GameObject textObj = Instantiate(messagePrefab, canvasTransform);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        TextMeshProUGUI textMesh = textObj.GetComponent<TextMeshProUGUI>();

        if (textMesh == null || textRect == null)
        {
            Debug.LogError("Prefab thiếu TextMeshProUGUI hoặc RectTransform!");
            return;
        }

        // Gán nội dung tin nhắn
        textMesh.text = message;

        // Đặt vị trí ban đầu (ngoài màn hình bên phải)
        float startX = Screen.width / 2 + moveDistance;
        float middleX = 0; // Vị trí giữa màn hình
        float endX = -Screen.width / 2 - moveDistance; // Rời khỏi màn hình bên trái

        textRect.anchoredPosition = new Vector2(startX, 0);

        // Chuỗi animation: Đi vào -> Dừng lại 2s -> Đi ra
        textRect.DOAnchorPosX(middleX, speed).SetEase(Ease.OutExpo) // Đi vào giữa
            .OnComplete(() =>
            {
                // Giữ nguyên vị trí 2 giây trước khi tiếp tục
                DOVirtual.DelayedCall(delay, () =>
                {
                    textRect.DOAnchorPosX(endX, speed).SetEase(Ease.InExpo) // Đi ra khỏi màn hình
                        .OnComplete(() => Destroy(textObj)); // Xóa object sau khi hoàn thành
                   // BackgroundMesage.SetActive(false);
                });
            });
    }

    private void InitializeBallHitAnnouncementUi()
    {
        if (ballHitAnnouncementImage != null)
        {
            ballHitAnnouncementImage.raycastTarget = false;
            ballHitAnnouncementImage.preserveAspect = true;
            ballHitAnnouncementImage.gameObject.SetActive(false);
            LogBallHitAnnouncementState("InitializeAfterHide");
        }
        else
        {
            Debug.LogWarning("[CLIENT][BallHitUI] ballHitAnnouncementImage chưa được gán trong UIControllerOnline Inspector.");
        }

        if (ballHitShockwaveImage != null && ballHitShockwaveImage == ballHitAnnouncementImage)
        {
            WarnSharedBallHitImage();
        }
        else if (ballHitShockwaveImage != null)
        {
            ballHitShockwaveImage.raycastTarget = false;
            ballHitShockwaveImage.gameObject.SetActive(false);
        }
    }

    // Populate the picked-hero UI with provided model.
    public void ShowPickedHero(HeroInfoModel model)
    {
        if (heroInfoUI == null || model == null)
            return;

        heroInfoUI.Populate(model);
    }

    private void ResolveHeroInfoUiReference()
    {
        if (heroInfoUI != null)
            return;

        heroInfoUI = GetComponentInChildren<HeroInfoUI>(true);
        if (heroInfoUI != null)
            return;

        heroInfoUI = FindObjectOfType<HeroInfoUI>(true);
    }

    private void RefreshPaperLegendHeroInfoIfNeeded()
    {
        if (!autoRefreshPaperLegendHeroInfo)
            return;

        if (!PaperLegendRuntimeState.IsPaperLegendMatch)
            return;

        ResolveHeroInfoUiReference();
        if (heroInfoUI == null)
            return;

        if (Time.unscaledTime < nextPaperLegendHeroInfoRefreshTime)
            return;

        nextPaperLegendHeroInfoRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, paperLegendHeroInfoRefreshInterval);

        if (!TryGetLocalPaperLegendHandler(out PaperLegendCharacterNetworkHandler handler))
            return;

        int modelId = handler.CharacterModelId;
        if (modelId <= 0)
            return;

        EnsurePaperLegendHeroInfoDataLoaded(modelId);
        PaperLegendHeroData heroData = ResolvePaperLegendHeroInfoData(modelId);
        bool hasApiData = heroData != null;

        if (!heroInfoUI.gameObject.activeSelf)
            heroInfoUI.gameObject.SetActive(true);

        if (lastPaperLegendHeroInfoModelId != modelId || lastPaperLegendHeroInfoHadApiData != hasApiData)
        {
            ShowPickedHero(BuildPaperLegendHeroInfoModel(handler, heroData));
            lastPaperLegendHeroInfoModelId = modelId;
            lastPaperLegendHeroInfoHadApiData = hasApiData;
        }

        RefreshPaperLegendHeroInfoRuntimeStats(handler, heroData);
    }

    private bool TryGetLocalPaperLegendHandler(out PaperLegendCharacterNetworkHandler handler)
    {
        handler = null;

        var gameManager = GameManagerNetWork.Instance;
        int playerId = gameManager != null && gameManager.loginUserModel != null
            ? gameManager.loginUserModel.UserId
            : 0;
        if (playerId <= 0)
            return false;

        var playerObject = NetworkObjectManager.Instance != null
            ? NetworkObjectManager.Instance.GetPlayerObject(playerId)
            : null;
        if (playerObject != null)
        {
            handler = playerObject.GetComponent<PaperLegendCharacterNetworkHandler>();
            if (handler != null)
                return true;
        }

        var paperHandlers = FindObjectsOfType<PaperLegendCharacterNetworkHandler>(true);
        for (int i = 0; i < paperHandlers.Length; i++)
        {
            var candidate = paperHandlers[i];
            if (candidate == null)
                continue;

            if (candidate.PlayerId == playerId)
            {
                handler = candidate;
                return true;
            }
        }

        for (int i = 0; i < paperHandlers.Length; i++)
        {
            var candidate = paperHandlers[i];
            if (candidate != null && candidate.HasInputAuthority)
            {
                handler = candidate;
                return true;
            }
        }

        return false;
    }

    private HeroInfoModel BuildPaperLegendHeroInfoModel(
        PaperLegendCharacterNetworkHandler handler,
        PaperLegendHeroData heroData)
    {
        int modelId = handler != null ? handler.CharacterModelId : 0;
        return new HeroInfoModel
        {
            avatarItemId = 0,
            avatarAddressable = modelId > 0 ? PaperLegendHeroAddressables.BuildHeroIconAddress(modelId) : string.Empty,
            nameKey = ResolvePaperLegendHeroDisplayName(modelId, heroData),
            level = handler != null ? Mathf.Max(1, handler.Level) : 1,
            currentExp = handler != null ? Mathf.Max(0, handler.CurrentExperience) : 0,
            maxExp = handler != null ? Mathf.Max(0, handler.ExperienceToNextLevel) : 0,
            hp = handler != null ? ResolvePaperLegendCurrentHp(handler) : 0,
            maxHp = handler != null ? Mathf.Max(0, handler.MaxHealth) : 0,
            attack = handler != null ? Mathf.Max(0, handler.AttackPower) : 0,
            defense = heroData != null ? Mathf.Max(0, heroData.defense) : 0,
            speed = handler != null ? Mathf.Max(0, handler.AttackSpeed) : 0,
            effects = null
        };
    }

    private void RefreshPaperLegendHeroInfoRuntimeStats(
        PaperLegendCharacterNetworkHandler handler,
        PaperLegendHeroData heroData)
    {
        if (heroInfoUI == null || handler == null)
            return;

        heroInfoUI.SetName(ResolvePaperLegendHeroDisplayName(handler.CharacterModelId, heroData));
        heroInfoUI.SetLevel(Mathf.Max(1, handler.Level));
        heroInfoUI.SetExp(Mathf.Max(0, handler.CurrentExperience), Mathf.Max(0, handler.ExperienceToNextLevel));
        heroInfoUI.SetStats(
            ResolvePaperLegendCurrentHp(handler),
            Mathf.Max(0, handler.MaxHealth),
            Mathf.Max(0, handler.AttackPower),
            heroData != null ? Mathf.Max(0, heroData.defense) : 0,
            Mathf.Max(0, handler.AttackSpeed));
    }

    private static float ResolvePaperLegendCurrentHp(PaperLegendCharacterNetworkHandler handler)
    {
        if (handler == null)
            return 0f;

        if (handler.CurrentHealth > 0f)
            return handler.CurrentHealth;

        return handler.IsAlive ? handler.MaxHealth : 0f;
    }

    private static string ResolvePaperLegendHeroDisplayName(int modelId, PaperLegendHeroData heroData)
    {
        if (heroData != null)
        {
            if (!string.IsNullOrWhiteSpace(heroData.name))
                return heroData.name;

            if (!string.IsNullOrWhiteSpace(heroData.code))
                return heroData.code;
        }

        return modelId > 0 ? $"Hero {modelId}" : "Paper Hero";
    }

    private PaperLegendHeroData ResolvePaperLegendHeroInfoData(int modelId)
    {
        if (modelId <= 0)
            return null;

        if (paperLegendHeroInfoDataByModelId.TryGetValue(modelId, out PaperLegendHeroData cached) && cached != null)
            return cached;

        var selectionClient = PaperLegendCharacterSelectionClient.Instance;
        if (selectionClient != null && selectionClient.HeroDataByModelId.TryGetValue(modelId, out PaperLegendHeroData selectedHero) && selectedHero != null)
        {
            paperLegendHeroInfoDataByModelId[modelId] = selectedHero;
            return selectedHero;
        }

        return null;
    }

    private void EnsurePaperLegendHeroInfoDataLoaded(int modelId)
    {
        if (modelId <= 0)
            return;

        if (ResolvePaperLegendHeroInfoData(modelId) != null)
            return;

        if (paperLegendHeroInfoLoadsInProgress.Contains(modelId))
            return;

        if (paperLegendHeroInfoLoadAttempted.Contains(modelId))
            return;

        if (APIManager.Instance == null)
        {
            if (!warnedPaperLegendHeroInfoApiMissing)
            {
                warnedPaperLegendHeroInfoApiMissing = true;
                Debug.LogWarning("[PaperLegends][HeroInfoUI] APIManager is missing; hero info UI will use network stats only.");
            }

            return;
        }

        StartCoroutine(LoadPaperLegendHeroInfoDataRoutine(modelId));
    }

    private IEnumerator LoadPaperLegendHeroInfoDataRoutine(int modelId)
    {
        paperLegendHeroInfoLoadsInProgress.Add(modelId);
        paperLegendHeroInfoLoadAttempted.Add(modelId);

        PaperLegendHeroListResponse response = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetPaperLegendHeroesByModelIdsAsync(new List<int> { modelId }),
            result => response = result));

        if (response != null && response.heroes != null)
        {
            for (int i = 0; i < response.heroes.Count; i++)
            {
                PaperLegendHeroData hero = response.heroes[i];
                if (hero == null)
                    continue;

                int resolvedModelId = hero.ResolveModelIdInt();
                if (resolvedModelId > 0)
                    paperLegendHeroInfoDataByModelId[resolvedModelId] = hero;
            }
        }

        paperLegendHeroInfoLoadsInProgress.Remove(modelId);
        lastPaperLegendHeroInfoHadApiData = false;
    }

    private void RefreshPaperLegendScoreboardIfNeeded()
    {
        if (!PaperLegendRuntimeState.IsPaperLegendMatch)
            return;

        if (Time.unscaledTime < nextPaperLegendScoreboardRefreshTime)
            return;

        nextPaperLegendScoreboardRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, paperLegendScoreboardRefreshInterval);
        UpdatePaperLegendLocalKillDeathText();
        ShowPaperLegendPlayerList();
    }

    private void UpdatePaperLegendLocalKillDeathText()
    {
        if (!TryGetLocalPaperLegendHandler(out PaperLegendCharacterNetworkHandler handler))
            return;

        if (paperLegendKillText != null)
        {
            paperLegendKillText.gameObject.SetActive(true);
            paperLegendKillText.text = $"{handler.KillCount}";
        }

        if (paperLegendDeathText != null)
        {
            paperLegendDeathText.gameObject.SetActive(true);
            paperLegendDeathText.text = $"{handler.DeathCount}";
        }
    }

    private void ShowPaperLegendPlayerList()
    {
        if (playerListPanel == null || playerItemPrefab == null)
            return;

        List<PaperLegendCharacterNetworkHandler> players = GetPaperLegendScoreboardPlayers();
        if (players.Count == 0)
            return;

        playerListPanel.gameObject.SetActive(true);

        for (int i = playerListPanel.childCount - 1; i >= 0; i--)
            Destroy(playerListPanel.GetChild(i).gameObject);

        int localPlayerId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        int topKillCount = players.Count > 0 ? players.Max(player => player.KillCount) : 0;

        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler player = players[i];
            if (player == null)
                continue;

            GameObject newItem = Instantiate(playerItemPrefab, playerListPanel);
            var itemUi = newItem.GetComponent<PlayerListItemUI>();
            if (itemUi == null)
            {
                Debug.LogWarning("[PaperLegends][UI] PlayerListItemUI is missing on playerItemPrefab.");
                continue;
            }

            ConfigurePaperLegendScoreboardItem(itemUi, player, i + 1, localPlayerId, topKillCount);
        }

        var rect = playerListPanel.GetComponent<RectTransform>();
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private List<PaperLegendCharacterNetworkHandler> GetPaperLegendScoreboardPlayers()
    {
        var result = new List<PaperLegendCharacterNetworkHandler>();
        var seenPlayerIds = new HashSet<int>();
        var handlers = FindObjectsOfType<PaperLegendCharacterNetworkHandler>(true);

        for (int i = 0; i < handlers.Length; i++)
        {
            PaperLegendCharacterNetworkHandler handler = handlers[i];
            if (handler == null || handler.PlayerId == 0)
                continue;

            if (!seenPlayerIds.Add(handler.PlayerId))
                continue;

            result.Add(handler);
        }

        result.Sort((left, right) =>
        {
            int killCompare = right.KillCount.CompareTo(left.KillCount);
            if (killCompare != 0)
                return killCompare;

            int deathCompare = left.DeathCount.CompareTo(right.DeathCount);
            if (deathCompare != 0)
                return deathCompare;

            int levelCompare = right.Level.CompareTo(left.Level);
            if (levelCompare != 0)
                return levelCompare;

            return left.PlayerId.CompareTo(right.PlayerId);
        });

        return result;
    }

    private void ConfigurePaperLegendScoreboardItem(
        PlayerListItemUI itemUi,
        PaperLegendCharacterNetworkHandler player,
        int rank,
        int localPlayerId,
        int topKillCount)
    {
        if (itemUi == null || player == null)
            return;

        SetupPaperLegendPlayerAvatar(itemUi, player.CharacterModelId);

        bool isLocalPlayer = player.PlayerId == localPlayerId;
        if (itemUi.PlayerNameText != null)
        {
            itemUi.PlayerNameText.text = ResolvePaperLegendPlayerDisplayName(player.PlayerId);
            itemUi.PlayerNameText.color = isLocalPlayer
                ? new Color32(173, 216, 230, 255)
                : Color.white;
        }

        if (itemUi.TurnOrderText != null)
            itemUi.TurnOrderText.text = rank.ToString();

        if (itemUi.LevelText != null)
            itemUi.LevelText.text = $"Lv {Mathf.Max(1, player.Level)}";

        if (itemUi.ScoreText != null)
            itemUi.ScoreText.text = $"K {player.KillCount} / D {player.DeathCount}";

        if (itemUi.AlwaysExamScoreText != null)
            itemUi.AlwaysExamScoreText.text = player.KillCount.ToString();

        if (itemUi.ScoreExamText != null)
            itemUi.ScoreExamText.text = player.DeathCount.ToString();

        if (itemUi.ExamUI != null)
            itemUi.ExamUI.SetActive(false);

        if (itemUi.ComboRoot != null)
            itemUi.ComboRoot.SetActive(false);

        bool isEliminated = !player.IsAlive;
        if (itemUi.EliminatedRoot != null)
            itemUi.EliminatedRoot.SetActive(isEliminated);

        if (itemUi.EliminatedText != null)
        {
            itemUi.EliminatedText.gameObject.SetActive(isEliminated);
            itemUi.EliminatedText.text = isEliminated ? "Respawning" : string.Empty;
        }

        if (itemUi.SkillListRoot != null)
        {
            for (int i = itemUi.SkillListRoot.childCount - 1; i >= 0; i--)
                Destroy(itemUi.SkillListRoot.GetChild(i).gameObject);
        }

        if (itemUi.FireRoot != null)
            itemUi.FireRoot.SetActive(topKillCount > 0 && player.KillCount == topKillCount && player.IsAlive);

        var informationRoot = itemUi.Information != null ? itemUi.Information : itemUi.gameObject;
        var canvasGroup = informationRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = informationRoot.AddComponent<CanvasGroup>();

        canvasGroup.alpha = isEliminated ? 0.55f : 1f;
        canvasGroup.interactable = !isEliminated;
        canvasGroup.blocksRaycasts = !isEliminated;

        var background = itemUi.BackgroundImage != null ? itemUi.BackgroundImage : itemUi.GetComponent<Image>();
        if (background != null)
            background.color = isLocalPlayer ? Color.white : new Color(1f, 1f, 1f, 0.82f);

        var button = itemUi.Button != null ? itemUi.Button : itemUi.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (player != null && CameraRotation.Instance != null)
                    CameraRotation.Instance.RotateCameraToPoint(player.transform.position);
            });
        }
    }

    private string ResolvePaperLegendPlayerDisplayName(int playerId)
    {
        var selectionClient = PaperLegendCharacterSelectionClient.Instance;
        if (selectionClient != null)
            return selectionClient.GetPlayerDisplayName(playerId);

        int localPlayerId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        var login = GameManagerNetWork.Instance?.loginUserModel;
        if (playerId == localPlayerId && login != null && !string.IsNullOrWhiteSpace(login.Username))
            return login.Username;

        return playerId <= 0 ? $"BOT {Mathf.Abs(playerId)}" : $"Player {playerId}";
    }

    private void SetupPaperLegendPlayerAvatar(PlayerListItemUI itemUi, int modelId)
    {
        if (itemUi == null)
            return;

        if (itemUi.AvatarRawImage != null)
            itemUi.AvatarRawImage.gameObject.SetActive(false);

        if (itemUi.AvatarImage == null || modelId <= 0)
            return;

        itemUi.AvatarImage.gameObject.SetActive(true);
        itemUi.AvatarImage.preserveAspect = true;

        if (paperLegendHeroIconSpritesByModelId.TryGetValue(modelId, out Sprite cachedSprite) && cachedSprite != null)
        {
            itemUi.AvatarImage.sprite = cachedSprite;
            itemUi.AvatarImage.color = Color.white;
            return;
        }

        itemUi.AvatarImage.sprite = null;
        itemUi.AvatarImage.color = Color.clear;

        if (!paperLegendHeroIconLoadsInProgress.Contains(modelId))
            StartCoroutine(LoadPaperLegendScoreboardHeroIconRoutine(modelId));
    }

    private IEnumerator LoadPaperLegendScoreboardHeroIconRoutine(int modelId)
    {
        paperLegendHeroIconLoadsInProgress.Add(modelId);

        Sprite loadedSprite = null;
        yield return PaperLegendHeroAddressables.LoadHeroIconSpriteRoutine(modelId, sprite => loadedSprite = sprite);

        if (loadedSprite != null)
            paperLegendHeroIconSpritesByModelId[modelId] = loadedSprite;

        paperLegendHeroIconLoadsInProgress.Remove(modelId);
    }

    public void PlayBallPlayerHitAnnouncement(float force = 1f)
    {
        Debug.Log($"[CLIENT][BallHitUI] PlayBallPlayerHitAnnouncement called. force={force:0.###}");

        StopBallPlayerHitAnnouncement();

        TryPlayBallPlayerHitKengOnce();

        PlayBallHitScreenShake(force);
        PlayBallHitShockwave(ballHitAnnouncementTargetOffset, force);

        if (ballHitAnnouncementImage == null)
        {
            Debug.LogWarning("[CLIENT][BallHitUI] Không hiện được thông báo trúng bi vì ballHitAnnouncementImage=null.");
            return;
        }

        var rect = ballHitAnnouncementImage.rectTransform;
        var group = EnsureCanvasGroup(ballHitAnnouncementImage.gameObject);
        LogBallHitAnnouncementState("BeforeShow");
        ConfigureBallHitCenterRect(rect, ballHitAnnouncementStartOffset);
        rect.localScale = Vector3.one * 1.28f;
        rect.localEulerAngles = new Vector3(0f, 0f, -18f);
        group.alpha = 0f;
        ballHitAnnouncementImage.gameObject.SetActive(true);
        ballHitAnnouncementImage.transform.SetAsLastSibling();
        LogBallHitAnnouncementState("AfterSetActive");
        WarnBallHitAnnouncementVisibilityIssues();

        CreateBallHitMotionBlur(rect, ballHitAnnouncementStartOffset, ballHitAnnouncementTargetOffset);

        ballHitAnnouncementSequence = DOTween.Sequence();
        ballHitAnnouncementSequence.SetUpdate(true);
        ballHitAnnouncementSequence.Append(group.DOFade(1f, 0.04f));
        ballHitAnnouncementSequence.Join(rect.DOAnchorPos(ballHitAnnouncementTargetOffset, BallHitAnnouncementDropDuration).SetEase(Ease.InExpo));
        ballHitAnnouncementSequence.Join(rect.DOScale(1f, BallHitAnnouncementDropDuration).SetEase(Ease.OutCubic));
        ballHitAnnouncementSequence.Join(rect.DOLocalRotate(new Vector3(0f, 0f, -7f), BallHitAnnouncementDropDuration).SetEase(Ease.OutCubic));
        ballHitAnnouncementSequence.Append(rect.DOShakeAnchorPos(BallHitAnnouncementStopDuration, new Vector2(18f, 7f), 13, 90f, false, true));
        ballHitAnnouncementSequence.Join(rect.DOPunchScale(Vector3.one * 0.18f, BallHitAnnouncementStopDuration, 5, 0.65f));
        ballHitAnnouncementSequence.AppendInterval(BallHitAnnouncementFreezeDuration);
        ballHitAnnouncementSequence.Append(group.DOFade(0f, BallHitAnnouncementHideDuration).SetEase(Ease.InQuad));
        ballHitAnnouncementSequence.Join(rect.DOAnchorPos(ballHitAnnouncementTargetOffset + new Vector2(52f, 18f), BallHitAnnouncementHideDuration).SetEase(Ease.InQuad));
        ballHitAnnouncementSequence.OnComplete(() =>
        {
            if (ballHitAnnouncementImage != null)
                ballHitAnnouncementImage.gameObject.SetActive(false);
            ballHitAnnouncementSequence = null;
            Debug.Log("[CLIENT][BallHitUI] Announcement animation complete, image hidden.");
        });
    }

    private void TryPlayBallPlayerHitKengOnce()
    {
        float now = Time.unscaledTime;
        if (now - lastBallPlayerHitKengTime < BallPlayerHitKengDedupeSeconds)
        {
            Debug.Log("[CLIENT][BallHitUI] Skip duplicate ballHitKengClip.");
            return;
        }

        lastBallPlayerHitKengTime = now;
        ClientGameplayBridge.Sound.PlayBallPlayerHitKeng();
    }

    private void LogBallHitAnnouncementState(string context)
    {
        if (ballHitAnnouncementImage == null)
        {
            Debug.LogWarning($"[CLIENT][BallHitUI] {context}: ballHitAnnouncementImage=NULL");
            return;
        }

        var imageObject = ballHitAnnouncementImage.gameObject;
        var rect = ballHitAnnouncementImage.rectTransform;
        var group = imageObject.GetComponent<CanvasGroup>();
        var rootCanvas = imageObject.GetComponentInParent<Canvas>(true);
        string spriteName = ballHitAnnouncementImage.sprite != null ? ballHitAnnouncementImage.sprite.name : "NULL";
        string groupAlpha = group != null ? group.alpha.ToString("0.###") : "none";
        string canvasName = rootCanvas != null ? rootCanvas.name : "NULL";
        bool sharesShockwaveImage = ballHitShockwaveImage != null && ballHitShockwaveImage == ballHitAnnouncementImage;

        Debug.Log($"[CLIENT][BallHitUI] {context}: path={GetTransformPath(imageObject.transform)}, activeSelf={imageObject.activeSelf}, activeInHierarchy={imageObject.activeInHierarchy}, imageEnabled={ballHitAnnouncementImage.enabled}, sprite={spriteName}, imageAlpha={ballHitAnnouncementImage.color.a:0.###}, canvasGroupAlpha={groupAlpha}, rectSize={rect.rect.size}, anchored={rect.anchoredPosition}, rootCanvas={canvasName}, sharesShockwaveImage={sharesShockwaveImage}, activeChain={GetActiveChain(imageObject.transform)}");
    }

    private void WarnBallHitAnnouncementVisibilityIssues()
    {
        if (ballHitAnnouncementImage == null)
            return;

        var imageObject = ballHitAnnouncementImage.gameObject;
        var rect = ballHitAnnouncementImage.rectTransform;
        var rootCanvas = imageObject.GetComponentInParent<Canvas>(true);

        if (!imageObject.activeInHierarchy)
            Debug.LogWarning($"[CLIENT][BallHitUI] Image đã SetActive(true) nhưng activeInHierarchy=false. Có parent đang inactive. activeChain={GetActiveChain(imageObject.transform)}");

        if (!ballHitAnnouncementImage.enabled)
            Debug.LogWarning("[CLIENT][BallHitUI] Component Image đang disabled, nên sprite không render.");

        if (ballHitAnnouncementImage.sprite == null)
            Debug.LogWarning("[CLIENT][BallHitUI] Image chưa có sprite, nên không có ảnh để hiển thị.");

        if (rect.rect.width <= 0.5f || rect.rect.height <= 0.5f)
            Debug.LogWarning($"[CLIENT][BallHitUI] RectTransform size quá nhỏ hoặc bằng 0: size={rect.rect.size}.");

        if (rootCanvas == null)
            Debug.LogWarning("[CLIENT][BallHitUI] Không tìm thấy Canvas cha cho ballHitAnnouncementImage.");
        else if (!rootCanvas.enabled)
            Debug.LogWarning($"[CLIENT][BallHitUI] Canvas cha '{rootCanvas.name}' đang disabled.");
    }

    private static string GetTransformPath(Transform target)
    {
        if (target == null)
            return "NULL";

        var names = new List<string>();
        var current = target;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static string GetActiveChain(Transform target)
    {
        if (target == null)
            return "NULL";

        var states = new List<string>();
        var current = target;
        while (current != null)
        {
            states.Add($"{current.name}:{current.gameObject.activeSelf}");
            current = current.parent;
        }

        states.Reverse();
        return string.Join(" > ", states);
    }

    private void StopBallPlayerHitAnnouncement()
    {
        if (ballHitAnnouncementSequence != null)
        {
            ballHitAnnouncementSequence.Kill();
            ballHitAnnouncementSequence = null;
        }

        if (ballHitShockwaveSequence != null)
        {
            ballHitShockwaveSequence.Kill();
            ballHitShockwaveSequence = null;
        }

        StopBallHitScreenShake();
        ClearBallHitMotionBlurObjects();

        if (ballHitAnnouncementImage != null)
        {
            ballHitAnnouncementImage.rectTransform.DOKill();
            ballHitAnnouncementImage.gameObject.SetActive(false);
        }

        if (ballHitShockwaveImage != null && ballHitShockwaveImage != ballHitAnnouncementImage)
        {
            ballHitShockwaveImage.rectTransform.DOKill();
            ballHitShockwaveImage.gameObject.SetActive(false);
        }

        if (activeRuntimeBallHitShockwaveObject != null)
        {
            Destroy(activeRuntimeBallHitShockwaveObject);
            activeRuntimeBallHitShockwaveObject = null;
        }
    }

    private void PlayBallHitShockwave(Vector2 anchoredPosition, float force)
    {
        Image shockwaveImage = ResolveBallHitShockwaveImage();
        bool isRuntimeShockwave = false;

        if (shockwaveImage == null)
        {
            var parent = ResolveBallHitEffectParent();
            if (parent == null)
                return;

            var shockwaveObject = new GameObject("BallHitShockwaveRuntime", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            shockwaveObject.transform.SetParent(parent, false);
            shockwaveImage = shockwaveObject.GetComponent<Image>();
            shockwaveImage.sprite = GetBallHitShockwaveSprite();
            shockwaveImage.color = new Color(1f, 0.92f, 0.25f, 0.45f);
            shockwaveImage.raycastTarget = false;
            activeRuntimeBallHitShockwaveObject = shockwaveObject;
            isRuntimeShockwave = true;
        }
        else if (shockwaveImage.sprite == null)
        {
            shockwaveImage.sprite = GetBallHitShockwaveSprite();
        }

        var rect = shockwaveImage.rectTransform;
        var group = EnsureCanvasGroup(shockwaveImage.gameObject);
        ConfigureBallHitCenterRect(rect, anchoredPosition);
        rect.sizeDelta = rect.sizeDelta == Vector2.zero ? new Vector2(180f, 180f) : rect.sizeDelta;
        rect.localScale = Vector3.one * 0.28f;
        rect.localEulerAngles = Vector3.zero;
        group.alpha = Mathf.Clamp01(0.55f + force * 0.002f);
        shockwaveImage.gameObject.SetActive(true);
        shockwaveImage.transform.SetAsLastSibling();
        if (ballHitAnnouncementImage != null)
            ballHitAnnouncementImage.transform.SetAsLastSibling();

        ballHitShockwaveSequence = DOTween.Sequence();
        ballHitShockwaveSequence.SetUpdate(true);
        ballHitShockwaveSequence.Append(rect.DOScale(2.35f, 0.34f).SetEase(Ease.OutExpo));
        ballHitShockwaveSequence.Join(group.DOFade(0f, 0.34f).SetEase(Ease.OutQuad));
        ballHitShockwaveSequence.OnComplete(() =>
        {
            if (isRuntimeShockwave)
            {
                if (activeRuntimeBallHitShockwaveObject != null)
                    Destroy(activeRuntimeBallHitShockwaveObject);
                activeRuntimeBallHitShockwaveObject = null;
            }
            else if (shockwaveImage != null)
            {
                shockwaveImage.gameObject.SetActive(false);
            }

            ballHitShockwaveSequence = null;
        });
    }

    private Image ResolveBallHitShockwaveImage()
    {
        if (ballHitShockwaveImage != null && ballHitShockwaveImage == ballHitAnnouncementImage)
        {
            WarnSharedBallHitImage();
            return null;
        }

        return ballHitShockwaveImage;
    }

    private void WarnSharedBallHitImage()
    {
        if (warnedSharedBallHitImage)
            return;

        warnedSharedBallHitImage = true;
        Debug.LogWarning("[CLIENT][BallHitUI] ballHitShockwaveImage đang gán cùng Image với ballHitAnnouncementImage. Hai tween sẽ đè nhau nên code sẽ bỏ qua shockwave Image này và dùng shockwave runtime riêng. Hãy để Ball Hit Shockwave Image trống hoặc gán một Image khác.");
    }

    private void PlayBallHitScreenShake(float force)
    {
        StopBallHitScreenShake();

        Transform target = ballHitScreenShakeTarget != null
            ? ballHitScreenShakeTarget
            : canvasTransform != null ? canvasTransform : transform;

        if (target == null)
            return;

        var rectTarget = target as RectTransform;
        if (rectTarget != null)
        {
            activeBallHitScreenShakeRect = rectTarget;
            activeBallHitScreenShakeAnchoredPosition = rectTarget.anchoredPosition;
            float strength = Mathf.Clamp(force * 0.02f, 8f, 22f);
            ballHitScreenShakeTween = rectTarget
                .DOShakeAnchorPos(0.18f, new Vector2(strength, strength * 0.55f), 14, 90f, false, true)
                .SetUpdate(true)
                .OnComplete(RestoreBallHitScreenShakeTarget);
            return;
        }

        activeBallHitScreenShakeTransform = target;
        activeBallHitScreenShakeLocalPosition = target.localPosition;
        float worldStrength = Mathf.Clamp(force * 0.0008f, 0.025f, 0.09f);
        ballHitScreenShakeTween = target
            .DOShakePosition(0.18f, worldStrength, 12, 90f, false, true)
            .SetUpdate(true)
            .OnComplete(RestoreBallHitScreenShakeTarget);
    }

    private void StopBallHitScreenShake()
    {
        if (ballHitScreenShakeTween != null)
        {
            ballHitScreenShakeTween.Kill();
            ballHitScreenShakeTween = null;
        }

        RestoreBallHitScreenShakeTarget();
    }

    private void RestoreBallHitScreenShakeTarget()
    {
        if (activeBallHitScreenShakeRect != null)
            activeBallHitScreenShakeRect.anchoredPosition = activeBallHitScreenShakeAnchoredPosition;
        else if (activeBallHitScreenShakeTransform != null)
            activeBallHitScreenShakeTransform.localPosition = activeBallHitScreenShakeLocalPosition;

        activeBallHitScreenShakeRect = null;
        activeBallHitScreenShakeTransform = null;
    }

    private void CreateBallHitMotionBlur(RectTransform sourceRect, Vector2 startPosition, Vector2 targetPosition)
    {
        if (sourceRect == null || ballHitAnnouncementImage == null || ballHitAnnouncementImage.sprite == null)
            return;

        Transform parent = sourceRect.parent;
        if (parent == null)
            return;

        for (int i = 0; i < 3; i++)
        {
            var blurObject = new GameObject($"BallHitMotionBlur_{i}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            blurObject.transform.SetParent(parent, false);
            blurObject.transform.SetSiblingIndex(sourceRect.GetSiblingIndex());
            activeBallHitMotionBlurObjects.Add(blurObject);

            var blurRect = blurObject.GetComponent<RectTransform>();
            blurRect.anchorMin = sourceRect.anchorMin;
            blurRect.anchorMax = sourceRect.anchorMax;
            blurRect.pivot = sourceRect.pivot;
            blurRect.sizeDelta = sourceRect.sizeDelta;
            blurRect.localEulerAngles = sourceRect.localEulerAngles;
            blurRect.localScale = Vector3.one * (1.08f + i * 0.05f);
            blurRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, 0.22f + i * 0.18f);

            var image = blurObject.GetComponent<Image>();
            image.sprite = ballHitAnnouncementImage.sprite;
            image.preserveAspect = ballHitAnnouncementImage.preserveAspect;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0.2f - i * 0.045f);

            var group = blurObject.GetComponent<CanvasGroup>();
            group.alpha = 1f;

            float duration = BallHitAnnouncementDropDuration + i * 0.025f;
            Sequence blurSequence = DOTween.Sequence();
            blurSequence.SetUpdate(true);
            blurSequence.Append(blurRect.DOAnchorPos(targetPosition + new Vector2(0f, 42f + i * 22f), duration).SetEase(Ease.InExpo));
            blurSequence.Join(group.DOFade(0f, duration).SetEase(Ease.OutQuad));
            blurSequence.OnComplete(() =>
            {
                activeBallHitMotionBlurObjects.Remove(blurObject);
                if (blurObject != null)
                    Destroy(blurObject);
            });
        }
    }

    private void ClearBallHitMotionBlurObjects()
    {
        for (int i = activeBallHitMotionBlurObjects.Count - 1; i >= 0; i--)
        {
            var blurObject = activeBallHitMotionBlurObjects[i];
            if (blurObject == null)
                continue;

            blurObject.transform.DOKill();
            Destroy(blurObject);
        }

        activeBallHitMotionBlurObjects.Clear();
    }

    private Transform ResolveBallHitEffectParent()
    {
        if (ballHitAnnouncementImage != null && ballHitAnnouncementImage.transform.parent != null)
            return ballHitAnnouncementImage.transform.parent;

        return canvasTransform != null ? canvasTransform : transform;
    }

    private static void ConfigureBallHitCenterRect(RectTransform rect, Vector2 offset)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        if (target == null)
            return null;

        var group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.AddComponent<CanvasGroup>();

        return group;
    }

    public void ShowImpactAnnouncement(string title, string localizationKey, float displayDuration = 1.4f)
    {
        StopActiveImpactAnnouncement();

        impactAnnouncementRoutine = StartCoroutine(ShowImpactAnnouncementRoutine(title, localizationKey, displayDuration));
    }

    public IEnumerator ShowImpactAnnouncementRunTime(string title, string localizationKey, float displayDuration = 1.4f)
    {
        StopActiveImpactAnnouncement();

        impactAnnouncementRoutine = StartCoroutine(ShowImpactAnnouncementRoutine(title, localizationKey, displayDuration));
        yield return impactAnnouncementRoutine;
    }

    private void StopActiveImpactAnnouncement()
    {
        if (impactAnnouncementRoutine != null)
        {
            StopCoroutine(impactAnnouncementRoutine);
            impactAnnouncementRoutine = null;
        }

        if (activeImpactAnnouncementObject != null)
        {
            activeImpactAnnouncementObject.transform.DOKill();
            Destroy(activeImpactAnnouncementObject);
            activeImpactAnnouncementObject = null;
        }
    }

    private void StopDestroyPermissionUnlockedAnnouncement()
    {
        destroyPermissionAnnouncementSequence?.Kill();
        destroyPermissionAnnouncementSequence = null;

        if (activeDestroyPermissionAnnouncementObject != null)
        {
            Destroy(activeDestroyPermissionAnnouncementObject);
            activeDestroyPermissionAnnouncementObject = null;
        }
    }

    public void ShowDestroyPermissionUnlockedAnnouncement()
    {
        StopDestroyPermissionUnlockedAnnouncement();

        Transform parent = canvasTransform != null ? canvasTransform : transform;
        GameObject textObj = messagePrefab != null
            ? Instantiate(messagePrefab, parent)
            : new GameObject("DestroyPermissionUnlockedAnnouncement", typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));

        if (messagePrefab == null)
            textObj.transform.SetParent(parent, false);

        activeDestroyPermissionAnnouncementObject = textObj;
        textObj.transform.SetAsLastSibling();

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        TextMeshProUGUI textMesh = textObj.GetComponent<TextMeshProUGUI>();
        CanvasGroup canvasGroup = EnsureCanvasGroup(textObj);

        if (textRect == null || textMesh == null || canvasGroup == null)
        {
            Debug.LogError("Prefab thiếu TextMeshProUGUI, RectTransform hoặc CanvasGroup!");
            StopDestroyPermissionUnlockedAnnouncement();
            return;
        }

        textMesh.text = DestroyPermissionUnlockedText;
        textMesh.enableWordWrapping = false;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = Mathf.Max(textMesh.fontSize, 52f);
        textMesh.fontStyle = FontStyles.Bold;
        textMesh.color = DestroyPermissionTextColor;
        textMesh.alpha = 1f;

        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, 120f);
        textRect.localScale = Vector3.zero;
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(textRect.rect.width, 760f));
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(textRect.rect.height, 120f));

        canvasGroup.alpha = 1f;

        destroyPermissionAnnouncementSequence = DOTween.Sequence().SetUpdate(true);
        destroyPermissionAnnouncementSequence
            .Append(textRect.DOScale(1.15f, DestroyPermissionPopDuration).SetEase(Ease.OutBack))
            .Append(textRect.DOScale(1f, DestroyPermissionSettleDuration).SetEase(Ease.OutQuad))
            .AppendInterval(DestroyPermissionHoldDuration)
            .Append(textRect.DOAnchorPosY(textRect.anchoredPosition.y + 90f, DestroyPermissionFadeDuration).SetEase(Ease.OutCubic))
            .Join(canvasGroup.DOFade(0f, DestroyPermissionFadeDuration).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                if (activeDestroyPermissionAnnouncementObject == textObj)
                    activeDestroyPermissionAnnouncementObject = null;

                destroyPermissionAnnouncementSequence = null;

                if (textObj != null)
                    Destroy(textObj);
            });
    }

    private IEnumerator ShowImpactAnnouncementRoutine(string title, string localizationKey, float displayDuration)
    {
        GameObject textObj = Instantiate(messagePrefab, canvasTransform);
        activeImpactAnnouncementObject = textObj;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        TextMeshProUGUI textMesh = textObj.GetComponent<TextMeshProUGUI>();

        if (textMesh == null || textRect == null)
        {
            Debug.LogError("Prefab thiếu TextMeshProUGUI hoặc RectTransform!");
            if (textObj != null)
                Destroy(textObj);
            activeImpactAnnouncementObject = null;
            impactAnnouncementRoutine = null;
            yield break;
        }

        var canvasGroup = textObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = textObj.AddComponent<CanvasGroup>();

        string localizedLine = localizationKey;
        if (!string.IsNullOrWhiteSpace(localizationKey) && LocalizationManager.Instance != null)
            localizedLine = LocalizationManager.Instance.GetText(localizationKey);

        bool hasTitle = !string.IsNullOrWhiteSpace(title);
        bool hasSubtitle = !string.IsNullOrWhiteSpace(localizedLine);
        textMesh.text = hasTitle && hasSubtitle
            ? $"{title}\n{localizedLine}"
            : hasTitle
                ? title
                : localizedLine;

        textMesh.enableWordWrapping = false;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = Mathf.Max(textMesh.fontSize, 48f);
        textMesh.color = ImpactAnnouncementStartColor;
        textMesh.alpha = 1f;

        textRect.localScale = Vector3.one * 1.05f;
        float startY = Screen.height * 0.5f + 280f;
        float targetY = 0f;
        textRect.anchoredPosition = new Vector2(0f, startY);
        canvasGroup.alpha = 1f;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(textRect.DOAnchorPosY(targetY, ImpactAnnouncementFallDuration).SetEase(Ease.InQuad));
        sequence.Join(textRect.DOScale(1f, ImpactAnnouncementFallDuration).SetEase(Ease.OutBack));
        sequence.Join(textMesh.DOColor(ImpactAnnouncementEndColor, ImpactAnnouncementFallDuration + ImpactAnnouncementShakeDuration));
        sequence.Append(textRect.DOShakeAnchorPos(ImpactAnnouncementShakeDuration, new Vector2(18f, 10f), 18, 90f, false, true));
        sequence.AppendInterval(Mathf.Max(0.1f, displayDuration));
        sequence.Append(canvasGroup.DOFade(0f, ImpactAnnouncementFadeDuration).SetEase(Ease.InQuad));
        sequence.Join(textRect.DOAnchorPosY(targetY - 20f, ImpactAnnouncementFadeDuration).SetEase(Ease.InQuad));

        yield return sequence.WaitForCompletion();

        if (textObj != null)
            Destroy(textObj);

        activeImpactAnnouncementObject = null;
        impactAnnouncementRoutine = null;
    }

    // hàm hiển thị điểm số
    public void ShowMesByUser( string mess)
    {
        RectTransform scoreRect = showMes.GetComponent<RectTransform>();
        TextMeshProUGUI scoreText = showMes.GetComponent<TextMeshProUGUI>();

        if (scoreRect == null || scoreText == null)
        {
            Debug.LogError("Prefab thiếu TextMeshProUGUI hoặc RectTransform!");
            return;
        }

        // Gán nội dung tin nhắn
        scoreText.text = mess;

        // Đặt vị trí ban đầu theo đối tượng cha
       // scoreRect.anchoredPosition = Vector2.zero;

        // Hiệu ứng bay lên và mờ dần
        scoreRect.DOAnchorPosY(scoreRect.anchoredPosition.y + 100, 1f)
            .SetEase(Ease.OutExpo);

        scoreText.DOFade(0, 2f)
            .SetEase(Ease.InExpo)
            .OnComplete(() =>
            {
                if (scoreText != null)
                {
                    scoreText.text = ""; // Chỉ ẩn chữ
                    scoreText.color = new Color(scoreText.color.r, scoreText.color.g, scoreText.color.b, 1); // Đặt lại alpha = 1 để tái sử dụng
                }
            });
    }

    // Hiển thị hiệu ứng combo ở giữa màn hình
    public void ShowComboEffect(int combo)
    {
        string[] comboTexts = new string[]
        {
            "Mở hàng!",
            "Trúng phát nữa!",
            "Ăn liên tiếp!",
            "Trúng như thần!",
            "Thánh bắn bi!",
            "Trùm sân đất!"
        };
        int index = Mathf.Clamp(combo - 1, 0, comboTexts.Length - 1);
        //string mess = $"x{combo} {comboTexts[index]}";
        string mess = $"{comboTexts[index]}";
        GameObject obj = Instantiate(messagePrefab, canvasTransform);
        var rect = obj.GetComponent<RectTransform>();
        var text = obj.GetComponent<TextMeshProUGUI>();
        text.text = mess;
        rect.anchoredPosition = Vector2.zero;
        CanvasGroup cg = obj.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        Sequence seq = DOTween.Sequence();
        seq.Append(cg.DOFade(1f, 0.2f));
        seq.Join(rect.DOAnchorPosY(150f, 1f).SetEase(Ease.OutBack));
        seq.AppendInterval(0.5f);
        seq.Append(cg.DOFade(0f, 0.5f));
        seq.OnComplete(() => Destroy(obj));
    }


    // UI cho người chơi lượt view
    public void UIforViewOnline()
    {   //hiện toàn bộ player

       // Hand.SetActive(false);
        ZoneUINeedToHide.SetActive(false);
        SyncNextBallVisibilityWithZone(true);
        UIMove.SetActive(false);
        UIJoystick.SetActive(false);
        UpdateDamagedBallNotice();
        UpdateExitGameButtonState();
        UpdateCameraSwitchButtonState();
    }
    //UI cho người chơi lượt chơi thường
    public void UIforPlayNormalOnline()
    {


        //ẩn UI cần
        ZoneUINeedToHide.SetActive(true);
        SyncNextBallVisibilityWithZone(true);
        // hiển thị tay
       // Hand.SetActive(true);
        // ẩn button di chuyển
        UIMove.SetActive(false);
        //hiện nút bắn
        UIJoystick.SetActive(true);
        TryStartLevelOneTutorial();
        UpdateDamagedBallNotice();
        UpdateExitGameButtonState();
        UpdateCameraSwitchButtonState();
    }
    //UI cho người chơi lượt thi hoặc ở mức
    public void UIforStartPointOnline()
    {
        ZoneUINeedToHide.SetActive(true);
        SyncNextBallVisibilityWithZone(true);
       // Hand.SetActive(true);
        UIMove.SetActive(true);
        UIJoystick.SetActive(true);
        TryStartLevelOneTutorial();
        UpdateDamagedBallNotice();
        UpdateExitGameButtonState();
        UpdateCameraSwitchButtonState();
    }

    private void TryStartLevelOneTutorial()
    {
        if (tutorialController == null)
        {
            return;
        }

        int level = GameManagerNetWork.Instance?.loginUserModel?.Level ?? 0;
        var serverRpc = GameManagerNetWork.Instance?.serverRPC;
        var status = serverRpc != null && serverRpc.TryGetStatusLoading(out var resolvedStatus)
            ? resolvedStatus
            : StatusLoadingGame.None;
        tutorialController.UpdateTutorialForExam(level, status);
    }
    public void ExitButton()
    {
        Time.timeScale = 1;
        UIGameOVer.gameObject.SetActive(false);
        GameOverManager.Instance.EndGamePopup.SetActive(false);
        ClearAvatarCache();
        if (GameManagerNetWork.Instance != null)
        {
            GameManagerNetWork.Instance.currentQuickMatchResultId = null;
        }
      //  bool isHost = GameManagerNetWork.Instance.serverRPC != null && GameManagerNetWork.Instance.serverRPC.HasStateAuthority;
       // if (isHost)
           // StartCoroutine(CheckServerConnection());

        GameManagerNetWork.Instance.CloseConnectToRunner();
        DayNightWeatherManager.Instance?.StopEnvironmentSound();
        LoadingManager.LoadScene("Menu");
    }
    public void ShowPlayerList_Online()
    {
        if (PaperLegendRuntimeState.IsPaperLegendMatch)
        {
            ShowPaperLegendPlayerList();
            return;
        }

        var players = GetPlayersOrderedByTurn();
        if (players.Count == 0)
        {
            return;
        }
        var serverRpc = GameManagerNetWork.Instance?.serverRPC;
        bool showExamScore = serverRpc != null &&
                             serverRpc.TryGetStatusLoading(out var status) &&
                             status == StatusLoadingGame.isExam &&
                             serverRpc.IsExamScoreReady;

        SkillManager.Instance.ShowSkillUsedList();
        // Xóa danh sách cũ trước khi hiển thị
        foreach (Transform child in playerListPanel)
        {
            Destroy(child.gameObject);
        }
        var validPlayerModels = players
            .Select(p => GetPlayerHandler(p.playerId))
            .Where(handler => handler != null && !handler.PlayerModel.isDestroy)
            .Select(handler => handler!.PlayerModel)
            .ToList();

        int maxScore = validPlayerModels.Count > 0 ? validPlayerModels.Max(pm => pm.score) : 0;

        // Lấy người chơi hiện tại theo chỉ số
        int currentIndex = TryGetReadyNetworkManager(out var manager)
            ? manager.currentPlayerIndex
            : -1;

        // Hiển thị từng người chơi
        foreach (var item in players)
        {
            var handler = GetPlayerHandler(item.playerId);
            var player = GetDisplayPlayerInfo(item, handler);
            GameObject newItem = Instantiate(playerItemPrefab, playerListPanel);
            var itemUi = newItem.GetComponent<PlayerListItemUI>();
            if (itemUi == null)
            {
                Debug.LogWarning("[UI] PlayerListItemUI chưa được gắn trên prefab danh sách người chơi.");
                continue;
            }

            SetupOnlinePlayerAvatar(itemUi, player);

            string playerName = player.fullname.ToString();
            int turnOrderDisplay = item.turnOrder + 1;
            if (itemUi.PlayerNameText != null)
            {
                itemUi.PlayerNameText.text = playerName;
                if (player.playerId == GameManagerNetWork.Instance.loginUserModel.UserId)
                {
                    itemUi.PlayerNameText.color = new Color32(173, 216, 230, 255);
                }
            }

            if (itemUi.TurnOrderText != null)
            {
                itemUi.TurnOrderText.text = turnOrderDisplay.ToString();
            }

            if (itemUi.AlwaysExamScoreText != null)
            {
                itemUi.AlwaysExamScoreText.text = player.scoreExam.ToString("0.0");
            }

            if (itemUi.LevelText != null)
            {
                itemUi.LevelText.text = $"Lv {Mathf.Max(1, player.level)}";
            }
            else if (itemUi.PlayerNameText != null)
            {
                itemUi.PlayerNameText.text = $"{playerName} (Lv {Mathf.Max(1, player.level)})";
            }

            if (itemUi.ScoreText != null)
            {
                itemUi.ScoreText.text = player.score.ToString();
            }

            bool isEliminated = player.isDestroy || handler == null;

            if (itemUi.EliminatedRoot != null)
                itemUi.EliminatedRoot.SetActive(isEliminated);

            if (itemUi.EliminatedText != null)
            {
                itemUi.EliminatedText.gameObject.SetActive(isEliminated);
                if (isEliminated)
                {
                    itemUi.EliminatedText.text = LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.GetText("noti_player_eliminated")
                        : "Eliminated";
                }
            }

            if (itemUi.ScoreExamText != null)
            {
                var defaultScoreColor = itemUi.ScoreExamText.color;
                if (showExamScore)
                {
                    itemUi.ExamUI.SetActive(true);
                    float scoreExam = item.scoreExam;
                    itemUi.ScoreExamText.text = scoreExam.ToString("0.0");
                    itemUi.ScoreExamText.color = scoreExam < 0f ? Color.red : defaultScoreColor;
                }
                else
                {
                    itemUi.ExamUI.SetActive(false);
                   // itemUi.ScoreExamText.text = player.score.ToString();
                    //itemUi.ScoreExamText.color = defaultScoreColor;
                }
            }

            var comboRoot = itemUi.ComboRoot;
            var comboImage = itemUi.ComboImage;
            if (comboRoot != null && comboImage != null)
            {
                bool isCurrentTurnPlayer = item.turnOrder == currentIndex;
                bool shouldShowCombo = isCurrentTurnPlayer
                    && manager != null
                    && manager.IsNetworkStateReady
                    && manager.isContinueTurn
                    && player.combo > 0
                    && comboSprites != null
                    && comboSprites.Length > 0;
                if (shouldShowCombo)
                {
                    int idx = Mathf.Clamp(player.combo - 1, 0, comboSprites.Length - 1);
                    comboImage.sprite = comboSprites[idx];
                    comboRoot.SetActive(true);
                }
                else
                {
                    comboImage.sprite = null;
                    comboRoot.SetActive(false);
                }
            }

            if (itemUi.SkillListRoot != null)
            {
                SkillManager.Instance.ShowSkillUsedList(player.playerId, itemUi.SkillListRoot);
            }

            if (player.isDestroy || handler == null)
            {
                var informationRoot = itemUi.Information != null ? itemUi.Information : newItem;
                var canvasGroup = informationRoot.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = informationRoot.AddComponent<CanvasGroup>();

                canvasGroup.alpha = 0.5f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (itemUi.FireRoot != null)
            {
                itemUi.FireRoot.SetActive(player.score > 0 && player.score == maxScore && !player.isDestroy);
            }

            if (item.turnOrder == currentIndex)
            {
                newItem.transform.localScale = Vector3.one * 1.2f;
                var img = itemUi.BackgroundImage != null ? itemUi.BackgroundImage : newItem.GetComponent<Image>();
                if (img != null) img.color = Color.white;
            }

            if (!player.isDestroy && handler != null)
            {
                bool canInteract = GameManagerNetWork.Instance.GetCurrentPlayerGame().statusPlayer != StatusPlayer.ShootExam;
                var btn = itemUi.Button != null ? itemUi.Button : newItem.GetComponent<Button>();
                if (canInteract && btn != null)
                {
                    var Ball = manager != null && manager.IsNetworkStateReady
                        ? manager.GetActiveBallObject(item.playerId)
                        : null;
                    if (Ball != null)
                    {
                        btn.onClick.AddListener(() =>
                        {
                            CameraRotation.Instance.RotateCameraToPoint(Ball.transform.position);
                            var ballController = Ball.GetComponent<BallServerController>();
                            var itemData = BuildActiveBallItemData(item.playerId, ballController);
                            if (itemData != null)
                            {
                                PopupHelper.Instance.ShowItemInfoPopup(itemData, ItemInfoPopupTab.OnlyView);
                            }
                        });
                    }
                }
            }
        }

        // Cập nhật layout UI
        LayoutRebuilder.ForceRebuildLayoutImmediate(playerListPanel.GetComponent<RectTransform>());
    }

    private void SetupOnlinePlayerAvatar(PlayerListItemUI itemUi, PlayerInfoStruct player)
    {
        if (itemUi == null || player.playerId <= 0)
        {
            return;
        }

#if UNITY_EDITOR
        Debug.Log($"[UI] Bỏ qua tải avatar cho player {player.playerId} khi chạy trong Editor.");
        return;
#endif

        var providerType = ResolveProviderType(player.providerType.ToString());

        var rawImage = itemUi.AvatarRawImage;
        var image = itemUi.AvatarImage;

        if (rawImage == null && image == null)
        {
            return;
        }

        if (TryConsumePreloadedMatchAvatar(player.playerId, out var preloadedTexture) && preloadedTexture != null)
        {
            playerAvatarTextures[player.playerId] = preloadedTexture;
            ApplyAvatarTexture(preloadedTexture, rawImage, image, player.playerId);
            return;
        }

        // Nếu avatar đã được gán sẵn và có trong cache thì dùng luôn
        bool hasAssignedAvatar = (rawImage != null && rawImage.texture != null) ||
                                 (image != null && image.sprite != null && image.sprite.texture != null);
        if (hasAssignedAvatar && playerAvatarTextures.TryGetValue(player.playerId, out var assignedTexture) && assignedTexture != null)
        {
            ApplyAvatarTexture(assignedTexture, rawImage, image, player.playerId);
            return;
        }

        if (playerAvatarLoaders.TryGetValue(player.playerId, out var running) && running != null)
        {
            StopCoroutine(running);
        }

        if (playerAvatarTextures.TryGetValue(player.playerId, out var cachedTexture) && cachedTexture != null)
        {
            ApplyAvatarTexture(cachedTexture, rawImage, image, player.playerId);
            playerAvatarLoaders.Remove(player.playerId);
            return;
        }

        var avatarService = AvatarService.EnsureInstance();
        if (avatarService == null)
        {
            return;
        }

        EnsureAvatarGuidSyncRequested();
        var firebaseUid = player.idAccount.ToString();
        var avatarUrl = player.avatarUrl.ToString();
        Debug.Log($"[UI] Tiến hành tải avatar cho player {player.playerId}.");
        var routine = StartCoroutine(LoadAndApplyPlayerAvatarRoutine(player.playerId, providerType, avatarUrl, firebaseUid, rawImage, image));
        playerAvatarLoaders[player.playerId] = routine;
    }

    private ItemSchema BuildActiveBallItemData(int playerId, BallServerController ballController)
    {
        if (ballController == null)
        {
            return null;
        }

        int itemId = ballController.BallMaterialId;
        if (itemId <= 0)
        {
            return null;
        }

        var itemData = new ItemSchema
        {
            id = itemId,
            level = Mathf.Max(ballController.BallLevel, 1),
            typeGid = (int)TypeItemGid.Culi,
            isCateye = ballController.HasCateye,
            locationGid = (int)LocationItemGid.Equipped,
            IsSolded = StatusSold.None,
            name = string.Empty,
            description = string.Empty,
            damage = ballController.GetDamageAmount()
        };

        var serverRpc = GameManagerNetWork.Instance?.serverRPC;
        var ballInfo = serverRpc != null ? serverRpc.GetBallPhysics(playerId) : null;
        if (ballInfo.HasValue)
        {
            var data = ballInfo.Value.data;
            itemData.name = data.name.ToString();
            itemData.Mass = data.Mass;
            itemData.GravityScale = data.GravityScale;
            itemData.Drag = data.Drag;
            itemData.Bounciness = data.Bounciness;
            itemData.Elasticity = data.Elasticity;
            itemData.ImpactResistance = data.ImpactResistance;
            if (data.skillGenCode > 0)
            {
                var activeSkill = ResolveActiveBallSkill(playerId, ballController.BallIndex, data.skillGenCode);
                itemData.SkillGid = data.skillGenCode;
                itemData.activeSkill = activeSkill ?? new ActiveSkillSchema
                {
                    GenCode = data.skillGenCode,
                    GenName = string.Empty,
                    description = string.Empty
                };
            }
        }

        return itemData;
    }

    private ActiveSkillSchema ResolveActiveBallSkill(int playerId, int ballIndex, int skillId)
    {
        if (skillId <= 0)
            return null;

        int localPlayerId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (playerId != localPlayerId)
            return null;

        var localSkill = BallActiveSkillManager.Instance?.GetLocalActiveSkill(ballIndex, skillId);
        if (localSkill == null)
            return null;

        return new ActiveSkillSchema
        {
            GenCode = localSkill.GenCode,
            GenName = localSkill.GenName,
            description = localSkill.description,
            mana = localSkill.mana,
            cooldown = localSkill.cooldown
        };
    }

    private IEnumerator LoadAndApplyPlayerAvatarRoutine(int playerId, AuthenticationProviderType providerType, string avatarUrl, string firebaseUid, RawImage rawImage, Image image)
    {
        string guid = firebaseUid;
        yield return ResolveAvatarGuidRoutine(playerId, guid, resolved => guid = resolved);

        bool allowStorageFallback = providerType != AuthenticationProviderType.GooglePlayGames &&
                                    providerType != AuthenticationProviderType.Google;

        if (string.IsNullOrWhiteSpace(avatarUrl) && !string.IsNullOrWhiteSpace(guid))
        {
            allowStorageFallback = true;
        }

        if (string.IsNullOrEmpty(avatarUrl) && string.IsNullOrEmpty(guid))
        {
            if (!Application.isEditor)
            {
                Debug.LogWarning($"[UI] Không thể xác định nguồn avatar cho người chơi {playerId}.");
            }
            playerAvatarLoaders.Remove(playerId);
            yield break;
        }

        Texture2D texture = null;
        string errorMessage = null;
        bool isDone = false;

        var avatarService = AvatarService.EnsureInstance();
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

        playerAvatarLoaders.Remove(playerId);

        if (texture == null)
        {
            if (!Application.isEditor)
            {
                Debug.LogWarning($"[UI] Không thể tải avatar cho người chơi {playerId}: {errorMessage}");
            }
            yield break;
        }

        playerAvatarTextures[playerId] = texture;
        ApplyAvatarTexture(texture, rawImage, image, playerId);
    }

    private void ApplyAvatarTexture(Texture2D texture, RawImage rawImage, Image image, int playerId)
    {
        if (texture == null)
        {
            return;
        }

        if (rawImage != null)
        {
            rawImage.texture = texture;
            rawImage.color = Color.white;
        }
        else if (image != null)
        {
            if (playerAvatarSprites.TryGetValue(playerId, out var existingSprite))
            {
                if (existingSprite == null || existingSprite.texture != texture)
                {
                    if (existingSprite != null)
                    {
                        Destroy(existingSprite);
                    }

                    existingSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    playerAvatarSprites[playerId] = existingSprite;
                }
            }
            else
            {
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                playerAvatarSprites[playerId] = sprite;
            }

            image.sprite = playerAvatarSprites[playerId];
            image.color = Color.white;
        }
    }

    public static void CachePreloadedMatchAvatar(int playerId, Texture2D texture)
    {
        if (playerId <= 0 || texture == null)
        {
            return;
        }

        preloadedMatchAvatarTextures[playerId] = texture;
    }

    public static void ClearPreloadedMatchAvatars()
    {
        // Textures belong to AvatarService.avatarCache — do NOT Destroy them here.
        preloadedMatchAvatarTextures.Clear();
    }

    private static bool TryConsumePreloadedMatchAvatar(int playerId, out Texture2D texture)
    {
        if (preloadedMatchAvatarTextures.TryGetValue(playerId, out texture) && texture != null)
        {
            preloadedMatchAvatarTextures.Remove(playerId);
            return true;
        }

        texture = null;
        return false;
    }

    public void ClearAvatarCache()
    {
        foreach (var loader in playerAvatarLoaders.Values)
        {
            if (loader != null)
            {
                StopCoroutine(loader);
            }
        }

        playerAvatarLoaders.Clear();

        foreach (var loader in playerAvatarPreloaders.Values)
        {
            if (loader != null)
            {
                StopCoroutine(loader);
            }
        }

        playerAvatarPreloaders.Clear();

        if (roomAvatarPreloadRoutine != null)
        {
            StopCoroutine(roomAvatarPreloadRoutine);
            roomAvatarPreloadRoutine = null;
        }
        roomAvatarPreloadCompleted = false;

        foreach (var sprite in playerAvatarSprites.Values)
        {
            if (sprite != null)
            {
                Destroy(sprite);
            }
        }

        playerAvatarSprites.Clear();

        // Textures belong to AvatarService.avatarCache — do NOT Destroy them here.
        playerAvatarTextures.Clear();
        ClearPreloadedMatchAvatars();
    }

 

    public void UpdateViewMapButtonState()
    {
        if (viewMapButton == null)
            return;

        viewMapButton.interactable = true;
        var img = viewMapButton.GetComponent<Image>();
        if (img != null)
            img.color = Color.white;
    }

    public void UpdateHidePlayerButtonState()
    {
        if (hidePlayerButton == null)
            return;

        int loginUserId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        bool interactable = loginUserId > 0 &&
                            TryGetReadyNetworkManager(out var manager) &&
                            manager.IsYourTurn(loginUserId);
        var serverRpc = GameManagerNetWork.Instance?.serverRPC;
        bool isExam = serverRpc != null &&
                      serverRpc.TryGetStatusLoading(out var status) &&
                      status == StatusLoadingGame.isExam;
        if(isExam)
        {
            hidePlayerButton.interactable = true;
            var img = hidePlayerButton.GetComponent<Image>();
            if (img != null)
                img.color = Color.white;
        }    
        else
        {
            hidePlayerButton.interactable = interactable;
            var img = hidePlayerButton.GetComponent<Image>();
            if (img != null)
                img.color = interactable ? Color.white : Color.gray;
        }    

    }

    private bool IsLocalPlayerEliminated()
    {
        var loginModel = GameManagerNetWork.Instance?.loginUserModel;
        if (loginModel == null)
        {
            return false;
        }

        var handler = GetPlayerHandler(loginModel.UserId);
        if (handler == null)
        {
            return false;
        }

        return handler.PlayerModel.isDestroy ||
               handler.PlayerModel.statusPlayer == StatusPlayer.Destroy;
    }

    private bool HasOtherAlivePlayers(int localPlayerId)
    {
        var players = GetPlayersOrderedByTurn();
        foreach (var player in players)
        {
            if (player.playerId <= 0 || player.playerId == localPlayerId)
                continue;

            var handler = GetPlayerHandler(player.playerId);
            if (handler == null)
                continue;

            var model = handler.PlayerModel;
            if (!model.isDestroy &&
                model.statusPlayer != StatusPlayer.Destroy &&
                model.statusPlayer != StatusPlayer.WaitingDestroy)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOnlineGameOver()
    {
        if (GameOverManager.Instance != null && GameOverManager.Instance.HasGameOverResults)
            return true;

        if (TryGetReadyNetworkManager(out var manager) && manager.IsGameEnded)
            return true;

        var serverRpc = GameManagerNetWork.Instance?.serverRPC;
        return serverRpc != null &&
               serverRpc.TryGetStatusLoading(out var status) &&
               status == StatusLoadingGame.EndGame;
    }

    private bool CanExitAfterDestroyed()
    {
        var loginModel = GameManagerNetWork.Instance?.loginUserModel;
        if (loginModel == null)
            return false;

        return IsLocalPlayerEliminated() &&
               !IsOnlineGameOver() &&
               HasOtherAlivePlayers(loginModel.UserId);
    }

    private void UpdateExitGameButtonState()
    {
        if (exitGameButton == null)
        {
            return;
        }

        bool shouldShow = CanExitAfterDestroyed();
        if (exitGameButton.gameObject.activeSelf != shouldShow)
        {
            exitGameButton.gameObject.SetActive(shouldShow);
        }

        exitGameButton.interactable = shouldShow;

        var image = exitGameButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = Color.white;
        }

        var canvasGroup = EnsureExitGameButtonCanvasGroup();
        if (canvasGroup != null && !shouldShow)
        {
            canvasGroup.alpha = 1f;
        }

        if (lastExitGameButtonVisible != shouldShow)
        {
            lastExitGameButtonVisible = shouldShow;
            if (shouldShow)
                StartExitGameButtonBlink();
            else
                StopExitGameButtonBlink();
        }
    }

    private CanvasGroup EnsureExitGameButtonCanvasGroup()
    {
        if (exitGameButton == null)
            return null;

        var canvasGroup = exitGameButton.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = exitGameButton.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.blocksRaycasts = exitGameButton.gameObject.activeSelf;
        canvasGroup.interactable = exitGameButton.interactable;
        return canvasGroup;
    }

    private void StartExitGameButtonBlink()
    {
        StopExitGameButtonBlink();

        var canvasGroup = EnsureExitGameButtonCanvasGroup();
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 1f;
        exitGameButtonBlinkTween = canvasGroup
            .DOFade(0.35f, 0.55f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopExitGameButtonBlink()
    {
        if (exitGameButtonBlinkTween != null)
        {
            exitGameButtonBlinkTween.Kill();
            exitGameButtonBlinkTween = null;
        }

        var canvasGroup = exitGameButton != null ? exitGameButton.GetComponent<CanvasGroup>() : null;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    private void OnClickExitGameAfterDestroyed()
    {
        if (!CanExitAfterDestroyed())
        {
            UpdateExitGameButtonState();
            return;
        }

        if (exitGameButton != null)
        {
            exitGameButton.interactable = false;
        }

        StartCoroutine(ExitGameAfterDestroyedRoutine());
    }

    private IEnumerator ExitGameAfterDestroyedRoutine()
    {
        int roomId = GameManagerNetWork.Instance?.serverRPC?.rpgRoomModel.roomId ?? 0;
        int playerId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        string matchId = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.currentQuickMatchResultId : null;

        if (APIManager.Instance != null && playerId > 0)
        {
            var earlyExitTask = APIManager.Instance.MarkMatchEarlyExitAsync(matchId, roomId, playerId);
            yield return StartCoroutine(APIManager.Instance.RunTask(earlyExitTask, _ => { }));
        }

        if (APIManager.Instance != null && roomId > 0 && playerId > 0)
        {
            var task = APIManager.Instance.MarkRoomUserLeftAsync(roomId, playerId);
            yield return StartCoroutine(APIManager.Instance.RunTask(task, _ => { }));
        }

        ExitButton();
    }

    public void ShowInforList_Online()
    {
        if (!GameManagerNetWork.Instance.CheckServerConnection())
            return;

        var playerDataInfor = GameManagerNetWork.Instance.GetCurrentPlayerGame();
        bool examTurn = playerDataInfor.statusPlayer == StatusPlayer.ShootExam;
        int countTotalRingBall = 0;
        var serverRpc = GameManagerNetWork.Instance.serverRPC;
        if (serverRpc != null)
            countTotalRingBall = serverRpc.rpgRoomModel.betCount;

        int countCurrentRingBall = 0;
        if (serverRpc != null)
        {
            foreach (var id in serverRpc.ringBalls.EnumerateIds())
            {
                if (id != default)
                    countCurrentRingBall++;
            }

            if (countCurrentRingBall == 0)
            {
                foreach (var ringBall in GameObject.FindGameObjectsWithTag("RingBall"))
                {
                    var netObj = ringBall.GetComponent<NetworkObject>();
                    if (netObj != null  && netObj.Id != default)
                        countCurrentRingBall++;
                }
            }
        }

        if (ringBallText != null)
        {
            ringBallText.text = countCurrentRingBall + "/" + countTotalRingBall;
        }

        SetupRingBallButton(ringBallButton);
        if (surrenderButton != null)
        {
            surrenderButton.onClick.RemoveAllListeners();
            surrenderButton.onClick.AddListener(SurrenderButton);
        }

        if (viewMapButton != null)
        {
            viewMapButton.onClick.RemoveAllListeners();
            viewMapButton.onClick.AddListener(onClickViewMap);
        }

        if (hidePlayerButton != null)
        {
            hidePlayerButton.onClick.RemoveAllListeners();
            hidePlayerButton.onClick.AddListener(HidePlayer);
        }

        RegisterNextBallButton();

        RegisterCameraSwitchButton();

        RegisterExitGameButton();

        UpdateViewMapButtonState();
        UpdateHidePlayerButtonState();
        UpdateCameraSwitchButtonState();
        UpdateExitGameButtonState();
        //UpdateChatButtonState(playerDataInfor.level);
        SetupActionAnimationButtons();

        if (examTurn)
        {
            if (ringBallButton != null)
            {
                ringBallButton.interactable = false;
                var img = ringBallButton.GetComponent<Image>();
                // if (img != null) img.color = Color.gray;
            }

            if (hidePlayerButton != null)
            {
                hidePlayerButton.interactable = false;
                var img = hidePlayerButton.GetComponent<Image>();
                if (img != null) img.color = Color.gray;
            }
        }
        UpdateDamagedBallNotice();
        // Đảm bảo UI cập nhật ngay lập tức
        if (InforListPanel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(InforListPanel.GetComponent<RectTransform>());
    }

    private void UpdateChatButtonState(int playerLevel)
    {
        var button = ResolveChatButton();
        if (button == null)
            return;

        if (!button.gameObject.activeSelf)
            button.gameObject.SetActive(true);

        bool canOpenChat = playerLevel > ChatUnlockLevel;
        button.interactable = canOpenChat;

        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = canOpenChat ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }

        var canvasGroup = button.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = button.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = canOpenChat ? 1f : 0.75f;
        canvasGroup.interactable = canOpenChat;
        canvasGroup.blocksRaycasts = canOpenChat;

        var stateText = ResolveChatButtonStateText(button);
        if (stateText != null)
        {
            stateText.text = canOpenChat ? "CHAT" : "chỉ cho cấp 10";
            stateText.color = canOpenChat ? Color.white : new Color(1f, 1f, 1f, 0.9f);
        }
    }

    private Button ResolveChatButton()
    {
        if (chatButton != null)
            return chatButton;

        var buttonTransform = transform.Find(ChatButtonObjectName);
        if (buttonTransform == null)
        {
            var allButtons = GetComponentsInChildren<Button>(true);
            foreach (var button in allButtons)
            {
                if (button != null && button.name == ChatButtonObjectName)
                {
                    chatButton = button;
                    break;
                }
            }
        }
        else
        {
            chatButton = buttonTransform.GetComponent<Button>();
        }

        return chatButton;
    }

    private TextMeshProUGUI ResolveChatButtonStateText(Button button)
    {
        if (button == null)
            return null;

        if (chatButtonStateText != null)
            return chatButtonStateText;

        var textTransform = button.transform.Find(ChatButtonStateTextName);
        if (textTransform != null)
        {
            chatButtonStateText = textTransform.GetComponent<TextMeshProUGUI>();
            if (chatButtonStateText != null)
                return chatButtonStateText;
        }

        var textObject = new GameObject(ChatButtonStateTextName, typeof(RectTransform));
        textObject.transform.SetParent(button.transform, false);

        var rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(0f, -18f);
        rectTransform.sizeDelta = new Vector2(0f, 32f);

        chatButtonStateText = textObject.AddComponent<TextMeshProUGUI>();
        chatButtonStateText.alignment = TextAlignmentOptions.Center;
        chatButtonStateText.enableWordWrapping = false;
        chatButtonStateText.fontSize = 18f;
        chatButtonStateText.raycastTarget = false;
        chatButtonStateText.text = string.Empty;

        if (ringBallText != null && ringBallText.font != null)
            chatButtonStateText.font = ringBallText.font;

        return chatButtonStateText;
    }

    private void SetupActionAnimationButtons()
    {
        actionEmoteMenuButton = ResolveOptionalButton(actionEmoteMenuButton, ActionEmoteMenuButtonObjectName);

        bool canTriggerAction = CanTriggerLocalActionAnimation();
        if (actionEmoteMenuButton != null)
        {
            actionEmoteMenuButton.onClick.RemoveAllListeners();
            actionEmoteMenuButton.onClick.AddListener(OnActionEmoteMenuButtonClicked);
            actionEmoteMenuButton.interactable = canTriggerAction;
        }
    }

    private void UnregisterActionAnimationButtons()
    {
        if (actionEmoteMenuButton != null)
            actionEmoteMenuButton.onClick.RemoveAllListeners();
        activeActionEmotePopup = null;
    }

    private Button ResolveOptionalButton(Button cachedButton, string objectName)
    {
        if (cachedButton != null)
            return cachedButton;

        var buttonTransform = transform.Find(objectName);
        if (buttonTransform != null)
            return buttonTransform.GetComponent<Button>();

        var allButtons = GetComponentsInChildren<Button>(true);
        foreach (var button in allButtons)
        {
            if (button != null && button.name == objectName)
                return button;
        }

        return null;
    }
    private void OnActionEmoteMenuButtonClicked()
    {
        if (!CanTriggerLocalActionAnimation())
            return;

        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("UIControllerOnline: PopupHelper instance is not available for action emote popup.");
            return;
        }

        if (activeActionEmotePopup != null)
        {
            PopupHelper.Instance.ClosePopup(activeActionEmotePopup.gameObject);
            activeActionEmotePopup = null;
            return;
        }

        activeActionEmotePopup = PopupHelper.Instance.ShowActionEmotePopup(
            TriggerLocalActionAnimation,
            () => activeActionEmotePopup = null);

        if (activeActionEmotePopup == null)
        {
            Debug.LogWarning("UIControllerOnline: Unable to show action emote popup.");
        }
    }

    private bool CanTriggerLocalActionAnimation()
    {
        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (userId <= 0)
            return false;

        var handler = GetPlayerHandler(userId);
        return handler != null && handler.IsNetworkStateReady && !handler.IsMarkedDestroyed;
    }

    private void TriggerLocalActionAnimation(CharacterAnimState animState)
    {
        if (!CanTriggerLocalActionAnimation())
            return;

        MovePlayerOnlineHandler.Instance?.RequestAnimState(animState);
    }

    private void UpdateDamagedBallNotice()
    {
        if (damagedBallNotice == null)
        {
            return;
        }

        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (userId <= 0)
        {
            damagedBallNotice.SetActive(false);
            return;
        }

        var ballObject = TryGetReadyNetworkManager(out var manager)
            ? manager.GetActiveBallObject(userId)
            : null;
        if (ballObject == null)
        {
            damagedBallNotice.SetActive(false);
            return;
        }

        var ballController = ballObject.GetComponent<BallServerController>();
        bool shouldShow = ballController != null && ballController.IsDamageStageActive();
        if (damagedBallNotice.activeSelf != shouldShow)
        {
            damagedBallNotice.SetActive(shouldShow);
        }
    }

    private void SetupRingBallButton(Button ringBallButton)
    {
        if (ringBallButton == null)
            return;

        ringBallButton.onClick.RemoveAllListeners();

        var playArea =   GameSessionClientLocal.Instance.playArea;
        if (playArea == null)
        {
            ringBallButton.interactable = false;
            return;
        }

        Vector3 targetPos = playArea.transform.position;
        ringBallButton.onClick.AddListener(() => onClickRingBallOnline(targetPos));
    }

    public void onClickViewMap()
    {
        if (map2DController == null)
            map2DController = GetComponentInChildren<OnlineMap2DController>(true);

        if (map2DController == null)
        {
            Debug.LogWarning("UIControllerOnline: OnlineMap2DController is not configured for viewMapButton.");
            return;
        }

        map2DController.Toggle();
    }

    public void HidePlayer()
    {
        if (!TryGetReadyNetworkManager(out _))
            return;

        var snapshot = GetPlayersOrderedByTurn();
        int myId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;

        if (!isHidePlayer)
        {
            foreach (var item in snapshot)
            {
                if (item.playerId == myId)
                    continue;
                var handler = GetPlayerHandler(item.playerId);
                if (handler != null)
                    SetPlayerVisible(handler.gameObject, false);
            }
            isHidePlayer = true;
        }
        else
        {
            foreach (var item in snapshot)
            {
                var handler = GetPlayerHandler(item.playerId);
                if (handler != null)
                    SetPlayerVisible(handler.gameObject, true);
            }
            isHidePlayer = false;
        }
    }

    public void OnClickSwitchBall()
    {
        SoundManager.Instance?.PlayNextBallButtonClick();

        if (activeBallSwitchPopup != null)
        {
            HideBallSwitchPopup(true);
            return;
        }

        ShowBallSwitchPopup();
    }

    private void ShowBallSwitchPopup()
    {
        int loginUserId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        var serverRpc = GameManagerNetWork.Instance?.serverRPC;
        if (loginUserId <= 0 || serverRpc == null)
            return;

        var entries = BuildBallSwitchEntries(loginUserId);
        if (entries.Count == 0)
        {
            ShowMesByUser("Không có bi để đổi.");
            return;
        }

        Transform parent = canvasTransform != null ? canvasTransform : transform;
        activeBallSwitchPopup = new GameObject("BallSwitchPopup", typeof(RectTransform), typeof(CanvasGroup));
        activeBallSwitchPopup.transform.SetParent(parent, false);

        Vector2 cardSize = GetBallSwitchCardDisplaySize();
        var arcPositions = BuildBallSwitchArcPositions(entries.Count, cardSize);
        Vector2 popupSize = CalculateBallSwitchPopupSize(arcPositions, cardSize);

        var popupRect = activeBallSwitchPopup.GetComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.sizeDelta = popupSize;
        popupRect.anchoredPosition = ResolveBallSwitchPopupPosition(parent, cardSize);

        var popupCanvasGroup = activeBallSwitchPopup.GetComponent<CanvasGroup>();
        popupCanvasGroup.alpha = 0f;
        popupCanvasGroup.interactable = false;
        popupCanvasGroup.blocksRaycasts = true;
        ApplyNextBallButtonSprite(true);

        int currentIndex = serverRpc.GetCurrentBallIndex(loginUserId);
        var cardRects = new List<RectTransform>();
        var cardGroups = new List<CanvasGroup>();

        CreateBallSwitchArcBackplate(activeBallSwitchPopup.transform, popupSize, cardSize);

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            bool isCurrent = entry.slotIndex == currentIndex;
            var card = CreateBallSwitchCard(entry, isCurrent, cardSize);
            card.transform.SetParent(activeBallSwitchPopup.transform, false);

            var rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = cardSize;
            rect.anchoredPosition = i < arcPositions.Count ? arcPositions[i] : Vector2.zero;
            rect.localScale = Vector3.one * 0.88f;

            var cardGroup = card.GetComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            cardGroup.interactable = false;
            cardGroup.blocksRaycasts = false;

            cardRects.Add(rect);
            cardGroups.Add(cardGroup);
        }

        PlayBallSwitchPopupAnimation(popupRect, popupCanvasGroup, cardRects, cardGroups, currentIndex);
    }

    private Vector2 GetBallSwitchCardDisplaySize()
    {
        float configuredDiameter = Mathf.Max(ballSwitchCardSize.x, ballSwitchCardSize.y);
        if (configuredDiameter <= 0f)
            configuredDiameter = 86f;

        float diameter = Mathf.Max(configuredDiameter, ballSwitchMinCardDiameter);
        return new Vector2(diameter, diameter);
    }

    private List<Vector2> BuildBallSwitchArcPositions(int count, Vector2 cardSize)
    {
        var positions = new List<Vector2>();
        if (count <= 0)
            return positions;

        float spacing = Mathf.Max(ballSwitchCardSpacing, cardSize.x * 0.18f);
        float horizontalStep = cardSize.x + spacing;
        float arcHeight = Mathf.Max(ballSwitchArcHeight, cardSize.y * 0.42f);

        for (int i = 0; i < count; i++)
        {
            float normalized = count == 1 ? 0f : Mathf.Lerp(-1f, 1f, i / (float)(count - 1));
            float x = normalized * horizontalStep * 0.82f;
            float y = arcHeight * Mathf.Sqrt(Mathf.Clamp01(1f - normalized * normalized));

            if (count == 2)
            {
                x = normalized * horizontalStep * 0.58f;
                y = arcHeight * 0.32f;
            }
            else if (count == 1)
            {
                y = arcHeight * 0.55f;
            }

            positions.Add(new Vector2(x, y));
        }

        return positions;
    }

    private Vector2 CalculateBallSwitchPopupSize(List<Vector2> positions, Vector2 cardSize)
    {
        if (positions == null || positions.Count == 0)
            return cardSize;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        Vector2 halfSize = cardSize * 0.5f;

        foreach (var position in positions)
        {
            minX = Mathf.Min(minX, position.x - halfSize.x);
            maxX = Mathf.Max(maxX, position.x + halfSize.x);
            minY = Mathf.Min(minY, position.y - halfSize.y);
            maxY = Mathf.Max(maxY, position.y + halfSize.y);
        }

        Vector2 padding = new Vector2(cardSize.x * 0.34f, cardSize.y * 0.36f);
        return new Vector2(maxX - minX + padding.x * 2f, maxY - minY + padding.y * 2f);
    }

    private void CreateBallSwitchArcBackplate(Transform parent, Vector2 popupSize, Vector2 cardSize)
    {
        var backplate = new GameObject("ArcBackplate", typeof(RectTransform), typeof(Image));
        backplate.transform.SetParent(parent, false);
        backplate.transform.SetAsFirstSibling();

        var rect = backplate.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(popupSize.x + cardSize.x * 0.16f, popupSize.y + cardSize.y * 0.08f);
        rect.anchoredPosition = new Vector2(0f, cardSize.y * 0.08f);

        var image = backplate.GetComponent<Image>();
        image.sprite = GetBallSwitchArcSprite();
        image.color = new Color(0.03f, 0.06f, 0.09f, 0.42f);
        image.raycastTarget = false;
    }

    private List<BallSwitchEntry> BuildBallSwitchEntries(int playerId)
    {
        var entries = new List<BallSwitchEntry>();
        var balls = TryGetReadyNetworkManager(out var manager)
            ? manager.GetPlayerBalls(playerId)
            : null;

        if (balls != null)
        {
            for (int i = 0; i < balls.Count && entries.Count < 3; i++)
            {
                var ball = balls[i];
                if (ball == null)
                    continue;

                var ballController = ball.GetComponent<BallServerController>();
                int itemId = ballController != null ? ballController.BallMaterialId : 0;
                int level = ballController != null ? Mathf.Max(ballController.BallLevel, 1) : 1;
                if (itemId <= 0 && i >= 0 && i < localBallPhysicsItems.Count)
                {
                    itemId = localBallPhysicsItems[i]?.itemId ?? 0;
                    level = Mathf.Max(localBallPhysicsItems[i]?.level ?? 1, 1);
                }

                if (itemId <= 0)
                    continue;

                entries.Add(new BallSwitchEntry
                {
                    slotIndex = i,
                    itemId = itemId,
                    level = level
                });
            }
        }

        if (entries.Count == 0 && localBallPhysicsItems.Count > 0)
        {
            for (int i = 0; i < localBallPhysicsItems.Count && entries.Count < 3; i++)
            {
                var item = localBallPhysicsItems[i];
                if (item == null || item.itemId <= 0)
                    continue;

                entries.Add(new BallSwitchEntry
                {
                    slotIndex = i,
                    itemId = item.itemId,
                    level = Mathf.Max(item.level, 1)
                });
            }
        }

        return entries;
    }

    private GameObject CreateBallSwitchCard(BallSwitchEntry entry, bool isCurrent, Vector2 cardSize)
    {
        var card = new GameObject($"BallSwitchCard_{entry.slotIndex}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));

        var circleSprite = GetBallSwitchCircleSprite();
        var background = card.GetComponent<Image>();
        background.sprite = circleSprite;
        background.color = isCurrent
            ? new Color(1f, 0.76f, 0.22f, 0.94f)
            : new Color(0.06f, 0.075f, 0.095f, 0.92f);

        var button = card.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;
        button.onClick.AddListener(() => OnBallSwitchCardClicked(entry.slotIndex));

        var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glow.transform.SetParent(card.transform, false);
        var glowRect = glow.GetComponent<RectTransform>();
        glowRect.anchorMin = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.sizeDelta = cardSize + new Vector2(cardSize.x * 0.22f, cardSize.y * 0.22f);
        glowRect.anchoredPosition = Vector2.zero;
        var glowImage = glow.GetComponent<Image>();
        glowImage.sprite = circleSprite;
        glowImage.color = isCurrent ? new Color(1f, 0.78f, 0.18f, 0.26f) : new Color(0.35f, 0.75f, 1f, 0.13f);
        glowImage.raycastTarget = false;
        glow.transform.SetAsFirstSibling();

        var ring = new GameObject("Ring", typeof(RectTransform), typeof(Image), typeof(Outline));
        ring.transform.SetParent(card.transform, false);
        var ringRect = ring.GetComponent<RectTransform>();
        ringRect.anchorMin = new Vector2(0.5f, 0.5f);
        ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.sizeDelta = cardSize - new Vector2(cardSize.x * 0.08f, cardSize.y * 0.08f);
        ringRect.anchoredPosition = Vector2.zero;
        var ringImage = ring.GetComponent<Image>();
        ringImage.sprite = circleSprite;
        ringImage.color = isCurrent ? new Color(1f, 0.92f, 0.58f, 0.28f) : new Color(0.55f, 0.75f, 1f, 0.16f);
        ringImage.raycastTarget = false;
        var ringOutline = ring.GetComponent<Outline>();
        ringOutline.effectColor = isCurrent ? new Color(1f, 0.86f, 0.28f, 0.62f) : new Color(0.55f, 0.82f, 1f, 0.35f);
        ringOutline.effectDistance = new Vector2(1.6f, 1.6f);

        var innerDisc = new GameObject("InnerDisc", typeof(RectTransform), typeof(Image));
        innerDisc.transform.SetParent(card.transform, false);
        var innerDiscRect = innerDisc.GetComponent<RectTransform>();
        innerDiscRect.anchorMin = new Vector2(0.5f, 0.5f);
        innerDiscRect.anchorMax = new Vector2(0.5f, 0.5f);
        innerDiscRect.pivot = new Vector2(0.5f, 0.5f);
        innerDiscRect.sizeDelta = cardSize - new Vector2(cardSize.x * 0.18f, cardSize.y * 0.18f);
        innerDiscRect.anchoredPosition = Vector2.zero;
        var innerDiscImage = innerDisc.GetComponent<Image>();
        innerDiscImage.sprite = circleSprite;
        innerDiscImage.color = new Color(0.02f, 0.025f, 0.035f, 0.48f);
        innerDiscImage.raycastTarget = false;

        var iconMask = new GameObject("IconMask", typeof(RectTransform), typeof(Image), typeof(Mask));
        iconMask.transform.SetParent(card.transform, false);
        var iconMaskRect = iconMask.GetComponent<RectTransform>();
        iconMaskRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconMaskRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconMaskRect.pivot = new Vector2(0.5f, 0.5f);
        iconMaskRect.sizeDelta = cardSize - new Vector2(cardSize.x * 0.2f, cardSize.y * 0.2f);
        iconMaskRect.anchoredPosition = Vector2.zero;
        var iconMaskImage = iconMask.GetComponent<Image>();
        iconMaskImage.sprite = circleSprite;
        iconMaskImage.color = Color.white;
        iconMaskImage.raycastTarget = false;
        var mask = iconMask.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(iconMask.transform, false);
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = iconMaskRect.sizeDelta * 1.16f;
        iconRect.anchoredPosition = Vector2.zero;
        var iconImage = icon.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.sprite = ItemVisualHelper.LoadSpriteByID(entry.itemId);
        LoadBallSwitchIconAsync(iconImage, entry.itemId);

        var levelTextObj = new GameObject("Level", typeof(RectTransform), typeof(TextMeshProUGUI));
        levelTextObj.transform.SetParent(card.transform, false);
        var levelRect = levelTextObj.GetComponent<RectTransform>();
        levelRect.anchorMin = new Vector2(0f, 0f);
        levelRect.anchorMax = new Vector2(1f, 0f);
        levelRect.pivot = new Vector2(0.5f, 0f);
        levelRect.sizeDelta = new Vector2(0f, Mathf.Max(24f, cardSize.y * 0.18f));
        levelRect.anchoredPosition = new Vector2(0f, cardSize.y * 0.04f);
        var levelText = levelTextObj.GetComponent<TextMeshProUGUI>();
        levelText.alignment = TextAlignmentOptions.Center;
        levelText.enableWordWrapping = false;
        levelText.fontSize = Mathf.Clamp(cardSize.y * 0.15f, 16f, 22f);
        levelText.raycastTarget = false;
        levelText.color = Color.white;
        levelText.text = $"Lv.{entry.level}";
        if (ringBallText != null && ringBallText.font != null)
            levelText.font = ringBallText.font;

        return card;
    }

    private static Sprite GetBallSwitchCircleSprite()
    {
        if (ballSwitchCircleSprite != null)
            return ballSwitchCircleSprite;

        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "BallSwitchCircleSprite";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f - 2f;
        float edgeSoftness = 2.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((radius - distance) / edgeSoftness + 0.5f);
                byte alphaByte = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alphaByte);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        ballSwitchCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        ballSwitchCircleSprite.name = "BallSwitchCircleSprite";
        return ballSwitchCircleSprite;
    }

    private static Sprite GetBallHitShockwaveSprite()
    {
        if (ballHitShockwaveSprite != null)
            return ballHitShockwaveSprite;

        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "BallHitShockwaveSprite";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float innerRadius = size * 0.31f;
        float outerRadius = size * 0.46f;
        float edgeSoftness = 4.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float outerAlpha = Mathf.Clamp01((outerRadius - distance) / edgeSoftness + 0.5f);
                float innerAlpha = Mathf.Clamp01((distance - innerRadius) / edgeSoftness + 0.5f);
                byte alphaByte = (byte)Mathf.RoundToInt(outerAlpha * innerAlpha * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alphaByte);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        ballHitShockwaveSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        ballHitShockwaveSprite.name = "BallHitShockwaveSprite";
        return ballHitShockwaveSprite;
    }

    private static Sprite GetBallSwitchArcSprite()
    {
        if (ballSwitchArcSprite != null)
            return ballSwitchArcSprite;

        const int width = 256;
        const int height = 128;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "BallSwitchArcSprite";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[width * height];
        float centerX = (width - 1) * 0.5f;
        float centerY = -height * 0.58f;
        float radius = width * 0.72f;
        float edgeSoftness = 7f;

        for (int y = 0; y < height; y++)
        {
            float verticalFade = Mathf.SmoothStep(0f, 1f, y / (height * 0.55f));
            for (int x = 0; x < width; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float circleAlpha = Mathf.Clamp01((radius - distance) / edgeSoftness + 0.5f);
                byte alphaByte = (byte)Mathf.RoundToInt(circleAlpha * verticalFade * 255f);
                pixels[y * width + x] = new Color32(255, 255, 255, alphaByte);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        ballSwitchArcSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), height);
        ballSwitchArcSprite.name = "BallSwitchArcSprite";
        return ballSwitchArcSprite;
    }

    private void LoadBallSwitchIconAsync(Image target, int itemId)
    {
        if (target == null || itemId <= 0)
            return;

        StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{itemId}.png", sprite =>
        {
            if (target != null && sprite != null)
                target.sprite = sprite;
        }));
    }

    private Vector2 ResolveBallSwitchPopupPosition(Transform parent, Vector2 cardSize)
    {
        Vector2 offset = ballSwitchPopupOffset + new Vector2(0f, Mathf.Max(0f, cardSize.y - 86f) * 0.38f);
        var parentRect = parent as RectTransform;
        if (parentRect == null || nextBallButton == null)
            return offset;

        var buttonRect = nextBallButton.GetComponent<RectTransform>();
        if (buttonRect == null)
            return offset;

        var canvas = parent.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, buttonRect.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out var localPoint))
            return localPoint + offset;

        return offset;
    }

    private void PlayBallSwitchPopupAnimation(RectTransform popupRect, CanvasGroup popupGroup, List<RectTransform> cardRects, List<CanvasGroup> cardGroups, int currentIndex)
    {
        HideBallSwitchTweensOnly();
        isBallSwitchPopupAnimating = true;
        popupGroup.alpha = 0f;
        popupRect.localScale = Vector3.one * 0.96f;

        ballSwitchPopupSequence = DOTween.Sequence();
        ballSwitchPopupSequence.SetUpdate(true);
        ballSwitchPopupSequence.Append(popupGroup.DOFade(1f, 0.12f));
        ballSwitchPopupSequence.Join(popupRect.DOScale(1f, 0.18f).SetEase(Ease.OutQuad));

        for (int i = 0; i < cardRects.Count; i++)
        {
            var rect = cardRects[i];
            var group = cardGroups[i];
            float delay = i * Mathf.Max(0f, ballSwitchStaggerDelay);
            Vector2 targetPos = rect.anchoredPosition;
            float jumpInOffset = Mathf.Max(28f, rect.sizeDelta.y * 0.2f);
            float bounceHeight = Mathf.Max(18f, rect.sizeDelta.y * 0.12f);
            rect.anchoredPosition = targetPos + new Vector2(0f, -jumpInOffset);
            bool isCurrent = i < cardRects.Count && GetBallSwitchSlotIndex(rect) == currentIndex;
            float targetScale = isCurrent ? 1.1f : 1f;

            ballSwitchPopupSequence.Insert(delay, group.DOFade(1f, 0.12f));
            ballSwitchPopupSequence.Insert(delay, rect.DOAnchorPos(targetPos + new Vector2(0f, bounceHeight), 0.24f).SetEase(Ease.OutBack, 1.45f));
            ballSwitchPopupSequence.Insert(delay + 0.22f, rect.DOAnchorPos(targetPos, 0.18f).SetEase(Ease.OutQuad));
            ballSwitchPopupSequence.Insert(delay, rect.DOScale(targetScale + 0.06f, 0.24f).SetEase(Ease.OutBack, 1.35f));
            ballSwitchPopupSequence.Insert(delay + 0.24f, rect.DOScale(targetScale, 0.16f).SetEase(Ease.OutQuad));
        }

        ballSwitchPopupSequence.OnComplete(() =>
        {
            isBallSwitchPopupAnimating = false;
            popupGroup.interactable = true;
            popupGroup.blocksRaycasts = true;

            for (int i = 0; i < cardGroups.Count; i++)
            {
                if (cardGroups[i] == null)
                    continue;

                cardGroups[i].interactable = true;
                cardGroups[i].blocksRaycasts = true;
            }

            for (int i = 0; i < cardRects.Count; i++)
            {
                var rect = cardRects[i];
                if (rect == null)
                    continue;

                float floatAmount = i % 2 == 0 ? 5f : 7f;
                rect.DOAnchorPosY(rect.anchoredPosition.y + floatAmount, 1.25f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        });
    }

    private int GetBallSwitchSlotIndex(RectTransform rect)
    {
        if (rect == null)
            return -1;

        string name = rect.gameObject.name;
        const string prefix = "BallSwitchCard_";
        if (name.StartsWith(prefix) && int.TryParse(name.Substring(prefix.Length), out int index))
            return index;

        return -1;
    }

    private void OnBallSwitchCardClicked(int slotIndex)
    {
        if (isBallSwitchPopupAnimating)
            return;

        int loginUserId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        var serverRpc = GameManagerNetWork.Instance?.serverRPC;
        if (loginUserId <= 0 || serverRpc == null)
            return;

        SoundManager.Instance?.PlayBallSwitchSelect();

        int currentIndex = serverRpc.GetCurrentBallIndex(loginUserId);
        if (slotIndex != currentIndex)
        {
            serverRpc.RpcSwitchToBallIndex(loginUserId, slotIndex);
            StartCoroutine(RefreshBallSkillAfterSwitch());

            var handler = GetPlayerHandler(loginUserId);
            if (handler != null && handler.FPPPosition != null && handler.PointPosition != null)
            {
                MovePlayerOnlineHandler.Instance?.RequestAnimState(CharacterAnimState.Changeball);
                handler.CurrentAnimState = CharacterAnimState.Changeball;
            }
        }

        HideBallSwitchPopup(true);
    }

    private void HideBallSwitchTweensOnly()
    {
        if (ballSwitchPopupSequence != null)
        {
            ballSwitchPopupSequence.Kill();
            ballSwitchPopupSequence = null;
        }

        if (activeBallSwitchPopup == null)
            return;

        activeBallSwitchPopup.transform.DOKill(true);
        var rects = activeBallSwitchPopup.GetComponentsInChildren<RectTransform>(true);
        foreach (var rect in rects)
        {
            if (rect != null)
                rect.DOKill(true);
        }

        var groups = activeBallSwitchPopup.GetComponentsInChildren<CanvasGroup>(true);
        foreach (var group in groups)
        {
            if (group != null)
                group.DOKill(true);
        }
    }

    private void HideBallSwitchPopup(bool animate)
    {
        if (activeBallSwitchPopup == null)
        {
            ApplyNextBallButtonSprite(false);
            return;
        }

        HideBallSwitchTweensOnly();
        isBallSwitchPopupAnimating = false;
        var popup = activeBallSwitchPopup;
        activeBallSwitchPopup = null;
        ApplyNextBallButtonSprite(false);

        if (!animate)
        {
            Destroy(popup);
            return;
        }

        var popupGroup = popup.GetComponent<CanvasGroup>();
        if (popupGroup != null)
        {
            popupGroup.interactable = false;
            popupGroup.blocksRaycasts = false;
            popupGroup.DOFade(0f, 0.12f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (popup != null)
                        Destroy(popup);
                });
        }
        else
        {
            Destroy(popup);
        }
    }

    private sealed class BallSwitchEntry
    {
        public int slotIndex;
        public int itemId;
        public int level;
    }

    public void SwitchToNextBallImmediate()
    {
        int loginUserId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (loginUserId <= 0 || GameManagerNetWork.Instance?.serverRPC == null)
            return;

        GameManagerNetWork.Instance.serverRPC.RpcSwitchToNextBall(loginUserId);
        StartCoroutine(RefreshBallSkillAfterSwitch());

        var handler = GetPlayerHandler(loginUserId);
        if (handler != null && handler.FPPPosition != null && handler.PointPosition != null)
        {

            // Bật animation đổi bi cho nhân vật hiện tại
            handler.CurrentAnimState = CharacterAnimState.Changeball;
            // Giữ nguyên góc nhìn thứ 1 sau khi đổi bi
            //CameraRotation.Instance.MoveCameraToFPPOnline(handler.FPPPosition, handler.PointPosition);
        }
    }

    private IEnumerator RefreshBallSkillAfterSwitch()
    {
        yield return new WaitForSeconds(0.15f);
        RefreshLocalBallSkillUiState(true);
    }

    public void ResetHiddenPlayers()
    {
        if (!TryGetReadyNetworkManager(out _) || isHidePlayer == false)
            return;

        var snapshot = GetPlayersOrderedByTurn();
        foreach (var item in snapshot)
        {
            var handler = GetPlayerHandler(item.playerId);
            if (handler != null)
                SetPlayerVisible(handler.gameObject, true);
        }

        isHidePlayer = false;
        UpdateHidePlayerButtonState();
    }

    private void SetPlayerVisible(GameObject playerObj, bool visible)
    {
        if (playerObj == null)
            return;

        var renderers = playerObj.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            r.enabled = visible;
    }
    public void onClickRingBallOnline(Vector3 targetPosition)
    {
        //var manager = GameManagerNetWork.Instance;
        //if (manager == null || manager.serverRPC == null)
        //    return;

        //  int playerId = manager.loginUserModel.UserId;
        MovePlayerOnlineHandler.Instance?.RotateSightingPoint(targetPosition);
       // manager.serverRPC.RpcRotatePlayerSightingPoint(playerId, targetPosition);
    }
    void ShowMoveRange()
    {
        moveRangeIndicator.SetActive(true);
        moveRangeIndicator.transform.position = startPosition;
        moveRangeIndicator.transform.localScale = new Vector3(moveDistance * 2, 1, moveDistance * 2);
    }
    //public void onClickPlayAgain()
    //{
    //    GameManager.Instance.ResetGame();
    //    GameManager.Instance.PlayGame();
    //}
    public void ShowMessage(string message, float speed, float duration)
    {
        // Tạo text mới từ prefab
        GameObject messageObject = Instantiate(messagePrefab, canvasTransform);
        TextMeshProUGUI messageText = messageObject.GetComponent<TextMeshProUGUI>();

        messageText.text = message;  // Đặt nội dung
        StartCoroutine(AnimateText(messageObject, speed, 2));
    }
    private IEnumerator AnimateText(GameObject messageObject, float speed, float pauseTime)
    {
        RectTransform rectTransform = messageObject.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = messageObject.AddComponent<CanvasGroup>();

        // Bắt đầu từ ngoài màn hình (bên trái)
        rectTransform.anchoredPosition = new Vector2(-Screen.width / 2, 0);
        rectTransform.localScale = Vector3.one * 0.8f; // Nhỏ hơn một chút
        canvasGroup.alpha = 0; // Ẩn ban đầu

        float elapsedTime = 0f;
        Vector2 targetPosition = new Vector2(0, 0); // Giữa màn hình

        // 🚀 **Di chuyển nhanh vào giữa + Fade in**
        while (elapsedTime < 0.5f)
        {
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetPosition, speed * Time.deltaTime);
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1, Time.deltaTime * 5); // Làm hiện dần
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, Vector3.one, Time.deltaTime * 3); // Phóng to nhẹ
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // ⏳ **Dừng lại 2 giây**
        yield return new WaitForSeconds(pauseTime);

 

        // Xóa text sau khi hoàn thành
        Destroy(messageObject);
    }
    public void ShowGameOverResults(List<OverGameRequest> results)
    {
        Time.timeScale = 0f;
        string message = "";
        foreach (var r in results)
        {
            message += $"{r.playerName}: +{r.marblesWon} bi, +{r.expGained} exp\n";
        }
        PopupHelper.Instance.ShowPopup(message, () => { ExitButton(); });
    }
    public void SurrenderButton()
    {
        StartCoroutine(SurrenderRoutine());
    }

    private IEnumerator SurrenderRoutine()
    {
        bool confirm = false;
        string TextQuestion = LocalizationManager.Instance.GetText("Do_you_want_to_surrender") + " ?";
        PopupHelper.Instance.ShowPopup(TextQuestion, () => { confirm = true; });
        yield return new WaitUntil(() => confirm);

        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null)
        {
            Debug.LogWarning("Không thể gửi yêu cầu đầu hàng vì serverRPC chưa được khởi tạo.");
            yield break;
        }

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        serverRPC.RpcProcessSurrender(playerId);
    }
    public void StartTurnCountdown()
    {
        ResetTurnCountdownState();
        RefreshTurnStartBallInfo();
    }
    public void StopTurnCountdown(bool playerDidAction)
    {
        if (playerDidAction)
        {
            // Nếu người chơi hành động trước khi hết giờ → reset penalty
            consecutiveTimeouts = 0;
            hasLocalPlayerShot = true;
        }
        hasTimerExpired = false;
    }

    public void UpdateTurnCounter(int turn, int maxTurn)
    {
        if (currentTurnText != null)
            currentTurnText.text = $"{turn} / {maxTurn}";
    }
    public void RenderTurnCountdown(NetworkRunner runner, TickTimer timer)
    {
        if (timerText == null || runner == null || !runner.IsRunning)
            return;

        int? remainingTicks = timer.RemainingTicks(runner);
        bool isRunning = remainingTicks.HasValue;
        bool wasRunning = lastRemainingTicks.HasValue;

        if (isRunning && (!wasRunning || remainingTicks.Value > lastRemainingTicks.Value))
        {
            ResetTurnCountdownState();
            RefreshTurnStartBallInfo();
        }

        lastRemainingTicks = remainingTicks;

        if (hasLocalPlayerShot && (IsLocalPlayerTurn() || IsExamPhaseActive()))
        {
            UpdateTimerText(0);
            return;
        }

        if (!isRunning)
        {
            if (wasRunning)
                HandleTurnExpired();

            UpdateTimerText(0);
            return;
        }

        float remainingSeconds = timer.RemainingTime(runner) ?? 0f;
        remainingSeconds = Mathf.Max(0f, remainingSeconds);

        int currentInt = Mathf.CeilToInt(remainingSeconds);
        if (currentInt <= 5 && currentInt != lastBeepSecond)
        {
            SoundManager.Instance.PlayBeepSound();
            lastBeepSecond = currentInt;
        }

        int displaySeconds = Mathf.CeilToInt(remainingSeconds);
        UpdateTimerText(displaySeconds);
    }

    private void UpdateTimerText(int displaySeconds)
    {
        if (timerText == null || displaySeconds == lastDisplayedSeconds)
            return;

        timerText.text = $"{displaySeconds}";
        lastDisplayedSeconds = displaySeconds;
    }

    private void ResetTurnCountdownState()
    {
        hasTimerExpired = false;
        hasLocalPlayerShot = false;
        lastBeepSecond = -1;
        lastDisplayedSeconds = -1;
        lastRemainingTicks = null;
    }

    private void RefreshTurnStartBallInfo()
    {
        RefreshLocalBallSkillUiState(true);
        RefreshCurrentSelectedBallInfoUi();
        UpdateDamagedBallNotice();
    }

    private void HandleTurnExpired()
    {
        if (hasTimerExpired)
            return;

        hasTimerExpired = true;

        if (!IsLocalPlayerTurn())
            return;

        consecutiveTimeouts++;

        if (consecutiveTimeouts >= 2)
            OnLoseByTimeout?.Invoke();
        else
            OnTimeOut?.Invoke();
    }

    private bool IsLocalPlayerTurn()
    {
        var loginModel = GameManagerNetWork.Instance?.loginUserModel;
        if (!TryGetReadyNetworkManager(out var manager) || loginModel == null)
            return false;

        return manager.IsYourTurn(loginModel.UserId);
    }

    private bool IsExamPhaseActive()
    {
        return TryGetReadyNetworkManager(out var manager) &&
               manager.StatusLoading == StatusLoadingGame.isExam;
    }

    #endregion
}
#endif
