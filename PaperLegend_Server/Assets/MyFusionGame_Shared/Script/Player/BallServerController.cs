
using Fusion;
using Fusion.Addons.Physics;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
#if !UNITY_SERVER
using DG.Tweening;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static Fusion.Sockets.NetBitBuffer;
#endif


[System.Serializable]
public struct ShotParams : INetworkStruct
{
    public Vector3 direction;
    public Vector3 spin;
    public float force;
    public float shootAngle;
}



public class BallServerController : NetworkBehaviour
{
    public static BallServerController Instance;
    private bool networkStateReady;
    private bool CanAccessNetworkedState
    {
        get
        {
            var networkObject = Object;
            return networkStateReady && networkObject != null && networkObject.IsValid;
        }
    }
    [Header("DATA CONFIG")]
    [Networked] public int playerId { get; set; }
    [Networked] public int IsSpawned { get; set; }
    [Networked] public int IsHolding { get; set; }
    [Networked, OnChangedRender(nameof(OnHeldPositionChanged))] public Vector3 HeldPosition { get; set; }
    [Networked, OnChangedRender(nameof(OnBallHit))] public HitInfo LastHitInfo { get; set; }
    [Networked, OnChangedRender(nameof(OnBallHit))] public float LastHitDamage { get; set; }
    [Networked, OnChangedRender(nameof(OnMaterialChanged))] public int BallMaterialId { get; set; }
    [Networked] public int BallItemSeq { get; set; }
    [Networked, OnChangedRender(nameof(OnCateyeChanged))] public bool HasCateye { get; set; }
    [Networked, OnChangedRender(nameof(OnBallLevelChanged))] public int BallLevel { get; set; }
    [Networked] public int BallIndex { get; set; }
    [Networked, OnChangedRender(nameof(OnActiveChanged))] public int IsActive { get; set; }
    [Networked, OnChangedRender(nameof(OnDamageChanged))] public float CurrentImpactResistance { get; set; }
    [Networked, OnChangedRender(nameof(OnDamageChanged))] public Vector3 DamagePoint { get; set; }
    [Networked, OnChangedRender(nameof(OnShotParamsChanged))] public ShotParams ShotData { get; set; }
    //private GameObject Level10VFXPrefab;
    private GameObject vipVFXInstance;
    private Rigidbody rb;
    private Vector3 spawnRestPosition;
    private bool spawnRestPositionInitialized;
    private int cachedPlayerId;
    private bool didRegisterBall;
    [SerializeField] private Transform InterpolationTarget;
#if !UNITY_SERVER
    [Header("RENDER CONFIG")]
    private AsyncOperationHandle<Material> mainMaterialHandle;
    private AsyncOperationHandle<Material> cateyeMaterialHandle;
    private AsyncOperationHandle<GameObject> vfxHandle;
    public Transform cameraFollowBall;
    private Vector3 vel = Vector3.zero;
    private Vector3 cameraFollowVelocity = Vector3.zero;
    private GameObject ballModelVisualInstance;
    private Transform _ballVisualAnchor;
    private GameObject nameArrow; // Đối tượng hiển thị mũi tên tên người chơi trên đầu viên bi
    private static readonly Dictionary<int, GameObject> NameArrowsByPlayer = new Dictionary<int, GameObject>();
    private Vector3 _lastRenderPosition;
    private Vector3 _smoothedRenderVelocity;
    private bool _hasRenderSample;
    [SerializeField, Tooltip("Thời gian nội suy mượt của visual ở client proxy")] private float _renderSmoothTime = 0.045f;
    [SerializeField, Tooltip("Tốc độ lerp xoay visual")] private float _renderRotationLerpSpeed = 22f;
    private bool _hasCenteredVisual;
    private Vector3 _centeredVisualLocalPosition = Vector3.zero;
    private Vector3 _baseVisualScale = Vector3.one;
    private float _tweenDuration = 0.1f; // Tùy chỉnh độ mượt
    private Coroutine _moveCoroutine;
    private Tween _ballMoveTween;
    private float _nextArrowRefreshTime;
    private const float ArrowRefreshInterval = 0.15f;
    private Coroutine shotGroundAudioRoutine;
    private float shotGroundAudioSpeed;
    private const float ShotGroundProbeOffset = 0.35f;
    private const float ShotGroundContactTolerance = 0.12f;
    private Vector3 _lastLocalCollisionSamplePosition;
    private Vector3 _localCollisionSampleVelocity;
    private bool _hasLocalCollisionSample;
    private float _lastLocalCollisionAudioTime = -999f;
    private Vector3 _lastLocalCollisionAudioPoint;
    private HitSurface _lastLocalCollisionAudioSurface = HitSurface.None;
    private float _lastServerHitFeedbackTime = -999f;
    private Vector3 _lastServerHitFeedbackPoint;
    private HitSurface _lastServerHitFeedbackSurface = HitSurface.None;
    private static readonly Collider[] LocalCollisionProbeHits = new Collider[24];
    private static readonly Dictionary<ulong, float> LocalCollisionAudioTimeByPair = new Dictionary<ulong, float>();
    private static readonly Dictionary<ulong, float> LocalCollisionActivePairTimeByPair = new Dictionary<ulong, float>();
    private const float LocalCollisionMinAudibleForce = 0.08f;
    private const float LocalCollisionPairDedupeSeconds = 0.18f;
    private const float LocalCollisionActivePairMemorySeconds = 0.12f;
    private const float ServerHitFeedbackDedupeSeconds = 0.08f;
    private const float LocalServerHitSuppressionSeconds = 0.85f;
    private const float LocalServerHitSuppressionDistanceSqr = 1.44f;
    private const float LocalBallCollisionProbePadding = 0.03f;
        // Biến kiểm soát bật/tắt hiệu ứng slow motion (debug)

#endif
#if UNITY_SERVER

#endif
    private bool EnableSlowMotion = false; // Đặt true để bật lại
    [Networked, OnChangedRender(nameof(OnShootBall))] public int hasBeenShoot { get; set; } = 0;

    // Bán kính phát hiện đối thủ (m)
    [SerializeField, Tooltip("Bán kính phát hiện đối thủ (m)")]
    private float slowMotionRadius = 0.45f;

    // Khoảng cách phát hiện slow motion (m)
    [SerializeField, Tooltip("Khoảng cách phát hiện slow motion (m)")]
    private float slowMotionDetectionRange = 0.5f;
    [SerializeField, Tooltip("Thời gian dự đoán trước va chạm để kích hoạt slow motion")]
    private float slowMotionPredictionTime = 0.6f;
    [SerializeField, Tooltip("Thời gian tối thiểu để kích hoạt slow motion trước khi va chạm (cinematic lead time)")]
    private float slowMotionMinLeadTime = 2.5f;
    [SerializeField, Tooltip("Tốc độ tối thiểu để bắt đầu dự đoán slow motion")]
    private float slowMotionMinSpeed = 1.0f;
    [SerializeField, Tooltip("Khoảng cách dự đoán tối đa cho slow motion")]
    private float slowMotionMaxPredictionDistance = 8f;
    [SerializeField, Tooltip("Thời gian chờ trước khi tắt slow motion nếu mất dự đoán")]
    private float slowMotionPredictionGraceTime = 0.6f;
    [SerializeField, Tooltip("Giữ slow motion thêm sau khi xác nhận va chạm")]
    private float slowMotionPostHitHoldTime = 1.5f;
    [SerializeField, Tooltip("Tổng thời gian slow motion tối thiểu (từ lúc dự đoán đến khi kết thúc)")]
    private float slowMotionMinCinematicDuration = 4.5f;
    [SerializeField, Tooltip("Số nạn nhân tối đa xử lý trong một lần dự đoán slow motion")]
    private int slowMotionVictimLimit = 3;
    [SerializeField, Tooltip("Tần suất kiểm tra dự đoán slow motion (giây, realtime)")]
    private float slowMotionLoopDelay = 0.02f;

    private Coroutine slowMotionCoroutine;
    private Coroutine heartbeatCoroutine;
    private const float heartbeatDistance = 1f;
    private bool slowMoPredictionActive;
    private int slowMoTriggerId;
    private float slowMoLastPredictionTime;
    private float slowMoLastConfirmedHitTime = -999f;
    private readonly HashSet<int> slowMoPredictedVictims = new HashSet<int>();
    private WaitForSecondsRealtime slowMotionLoopYield;

#if !UNITY_SERVER
    private int activeSlowMoTriggerId;
    private readonly HashSet<int> activeSlowMoVictims = new HashSet<int>();
#endif

    private float stoppedTime = 0f;
    private const float minVelocity = 0.03f;
    private const float minAngular = 1f;
    private const float requiredStopDuration = 0.2f;
    private float nearStopBrakeTime;
    private const float NearStopBrakeLinearThreshold = 0.18f;
    private const float NearStopBrakeVerticalThreshold = 0.12f;
    private const float NearStopBrakeAngularThreshold = 6f;
    private const float NearStopBrakeRampSeconds = 0.6f;
    private const float NearStopBrakeLinearDecelMin = 0.55f;
    private const float NearStopBrakeLinearDecelMax = 2.4f;
    private const float NearStopBrakeAngularDecelMin = 12f;
    private const float NearStopBrakeAngularDecelMax = 55f;
    private const float NearStopForceStopDelay = 0.35f;
    private const float NearStopForceStopVelocity = 0.04f;
    private const float NearStopForceStopAngular = 1.2f;
    private const string ProtectiveShieldTag = "ProtectiveShield";

    private PlayerInfoStruct CurrentUser;
    private NetworkObject _networkObject;
    private bool _hasRequestedAuthority = false;
    private BallMeshDeformer meshDeformer;
    private float initialImpactResistance;
    private float initialAngularDrag;
    private float initialLinearDamping;
    private Vector3 lastDamagePoint;
    private float lastSyncedResistance = -1f;
    private Vector3 lastSyncedDamagePoint;
    private bool damageInitialized = false;
    private PhysicsBallHelper physicsHelper;
    private bool _hasLoggedMissingOwnerHandler;
    private Collider[] _cachedAllColliders;
#if !UNITY_SERVER
    private Renderer[] _cachedVisualRenderers;
    private DG.Tweening.Sequence _scaleSkillEffectSequence;
    private DG.Tweening.Sequence _groundPinSkillVisualSequence;
    private readonly List<GameObject> _scaleSkillEffectObjects = new List<GameObject>();
    private bool _hasBaseVisualScale;
#endif
    private SphereCollider _sphereCollider;
    private float _baseColliderRadius = -1f;
    private float _currentScaleMultiplier = 1f;
#if UNITY_SERVER
    private readonly Dictionary<int, int> activeBallSkillUseCountBySkill = new Dictionary<int, int>();
    private bool groundPinSkillArmed;
    private bool groundPinSkillShotActive;
    private bool groundPinSkillHasLeftGround;
    private bool groundPinSkillTriggered;
    private bool groundPinSkillConstraintsSaved;
    private float groundPinSkillShotElapsed;
    private float groundPinSkillSpinElapsed;
    private Vector3 groundPinSkillPinnedPosition;
    private Vector3 groundPinSkillSpinAxis = Vector3.up;
    private RigidbodyConstraints groundPinSkillOriginalConstraints;
    private const float GroundPinSkillMinFlightSeconds = 0.08f;
    private const float GroundPinSkillFallbackGroundSeconds = 0.22f;
    private const float GroundPinSkillSpinDuration = 0.38f;
    private const float GroundPinSkillInitialAngularSpeed = 55f;
    private const float GroundPinSkillGroundProbeOffset = 0.45f;
    private const float GroundPinSkillGroundContactTolerance = 0.14f;
#endif

    private enum BallDamageStage
    {
        Pristine,
        Chipped,
        Cracked,
        Shattered
    }

    [SerializeField, Tooltip("Ngưỡng % độ bền còn lại để chuyển sang trạng thái sứt nhẹ")]
    private float chippedThreshold = 0.7f;
    [SerializeField, Tooltip("Ngưỡng % độ bền còn lại để chuyển sang trạng thái nứt nhiều")]
    private float crackedThreshold = 0.4f;
    [SerializeField, Tooltip("Ngưỡng % độ bền còn lại để chuyển sang trạng thái móp nặng")]
    private float shatteredThreshold = 0.15f;
    [SerializeField, Tooltip("Ngưỡng lực va chạm tối thiểu để tính hư hỏng")]
    private float damageImpactThreshold = 0.6f;
    [SerializeField, Tooltip("Hệ số nền để chuyển tốc độ va chạm thành sát thương (k trong công thức damage = k * v^2)")]
    private float baseImpactDamage = 0.1f;

    private BallDamageStage _currentDamageStage = BallDamageStage.Pristine;

#if UNITY_SERVER
    [Header("Damage Collider Prefabs (Server)")]
    [SerializeField] private GameObject chippedColliderPrefab;
    [SerializeField] private GameObject crackedColliderPrefab;
    [SerializeField] private GameObject shatteredColliderPrefab;
    private GameObject _spawnedDamageCollider;
    private GameObject _currentColliderPrefab;
#endif

#if !UNITY_SERVER
    private Mesh _currentVisualMesh;
    private Mesh _baseVisualMesh;
#endif

    [SerializeField] private PlayerNetworkHandler ownerHandler;

#if UNITY_SERVER
    private struct PendingCollisionInfo
    {
        public GameObject other;
        public float impactForce;
        public float impactSpeed;
        public Vector3 contactPoint;
        public bool hasContact;
        public Vector3 relativeVelocity;
        public Vector3 contactNormal;
    }

    private readonly List<PendingCollisionInfo> _pendingCollisions = new List<PendingCollisionInfo>();
    private Coroutine surfaceSlowdownCoroutine;
    private float surfaceSlowdownResetTime;
    private const float BallCollisionMinRelativeSpeed = 0.25f;
    private const float BallCollisionRestitutionFallback = 0.88f;
    private const float BallCollisionShotNormalRetainLimit = 0.35f;
    private const float BallCollisionMaxTargetSpeedMultiplier = 1.15f;
    private const float BallCollisionRollingSpinBlend = 0.35f;
    private static readonly Dictionary<ulong, int> BallCollisionResponseFrameByPair = new Dictionary<ulong, int>();
    private static readonly Dictionary<ulong, float> BallPlayerHitAnnouncementTimeByPair = new Dictionary<ulong, float>();
    private const float BallPlayerHitAnnouncementDedupeSeconds = 0.35f;
#endif

    private void OnHeldPositionChanged()
    {
        // Dùng cho logic hiển thị và camera
//#if !UNITY_SERVER
//    if (IsHolding == 1 && hasBeenShoot == 0)
//    {
//        // Kiểm tra xem có cần nội suy tùy chỉnh hay không
//        if (InterpolationTarget != null)
//        {
//            // Nếu vị trí thay đổi, bắt đầu làm mượt
//            if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
//            _moveCoroutine = StartCoroutine(SmoothMove(InterpolationTarget, HeldPosition, _tweenDuration));
//        }
//        else
//        {
//            // Fallback nếu không có InterpolationTarget
//            transform.position = HeldPosition;
//        }
//    }
//#endif
      
        // Code cũ:
        // #if UNITY_SERVER
        // if (IsHolding == 1 && hasBeenShoot == 0)
        // {
        //     transform.position = HeldPosition;
        // }
        // #endif
    }
#if !UNITY_SERVER
private IEnumerator SmoothMove(Transform targetTransform, Vector3 destination, float duration)
{
    Vector3 startPosition = targetTransform.position;
    float elapsed = 0f;

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        // Dùng Ease.OutQuad hoặc bất kỳ hàm easing nào bạn thích
        targetTransform.position = Vector3.Lerp(startPosition, destination, t); 
        yield return null;
    }
    targetTransform.position = destination;
}
#endif
    // Start is called before the first frame update
    private void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        rb = GetComponent<NetworkRigidbody3D>().Rigidbody;
        _sphereCollider = GetComponent<SphereCollider>();
        if (_sphereCollider != null)
            _baseColliderRadius = _sphereCollider.radius;
        meshDeformer = GetComponent<BallMeshDeformer>();
        physicsHelper = GetComponent<PhysicsBallHelper>();
        if (physicsHelper == null)
        {
            physicsHelper = gameObject.AddComponent<PhysicsBallHelper>();
            physicsHelper.SetFramePhysicsFixedUpdateEnabled(false);
        }
        if (rb != null)
        {
            initialAngularDrag = rb.angularDamping;
            initialLinearDamping = rb.linearDamping;
        }
        if (HasStateAuthority)
        {
            HeldPosition = transform.position;
        }
        //var rbNet = GetComponent<NetworkRigidbody3D>();
        //if (rbNet.InterpolationTarget == null)
        //{
        //    rb = rbNet.Rigidbody;
        //    rbNet.InterpolationTarget = this.transform;
        //}
        // ✅ Tạo GameObject cameraFollow

        //var playerGO = GameSessionNetWork_Host.Instance.GetDictObject(playerToUpdate.playerId, playerDict);
        //if (playerGO != null)
        //    CamFllowLocation = playerGO.transform.Find("FPPPosition").gameObject.transform;
        //FingerLocation = playerGO.transform.Find("FingerPosition").gameObject;

    }
#if UNITY_SERVER
    public void ConfigureDamageColliders(GameObject chipped, GameObject cracked, GameObject shattered)
    {
        chippedColliderPrefab = chipped;
        crackedColliderPrefab = cracked;
        shatteredColliderPrefab = shattered;
    }
#endif
#if UNITY_SERVER
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) { return; }
        if (NetworkObjectManager.Instance.IsGameEnded || IsActive == 0)
            return;

        ClearExpiredGroundPinSkillArmServer();

        if (IsHolding == 1 && hasBeenShoot == 0)
        {
            // 1. Kiểm tra và lấy Owner Handler
            if (ownerHandler == null)
            {
                TryResolveOwnerHandler();
                if (ownerHandler == null) return;
            }

            // 2. Đọc vị trí FingerPos đã được đồng bộ từ Player Handler
            Vector3 targetFingerPos = ownerHandler.FingerPos;

            // Bot không có client input nên FingerPos luôn (0,0,0) → fallback player body
            if (targetFingerPos == Vector3.zero && IsOwnerBot())
                targetFingerPos = ResolveBotFingerPosition();

            // 3. Áp dụng vị trí Simulation
            HeldPosition = targetFingerPos; // Cập nhật [Networked] State
            transform.position = HeldPosition; // Cập nhật Simulation Position

            // 4. Đảm bảo vật lý mạng được thiết lập
            EnsureKinematic();
        }
        else if (IsHolding == 0 && hasBeenShoot == 1)
        {
            float deltaTime = Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime;
            physicsHelper?.ApplyDamagedRollingPhysics(rb, deltaTime, initialImpactResistance, CurrentImpactResistance);
            if (!UpdateGroundPinSkillServer())
            {
                ApplyNearStopBrake(deltaTime);
                CheckBallStoped();
            }
        }

        ProcessPendingCollisionsServer();

    }
#endif
    public override void Spawned()
    {
        networkStateReady = true;

        if (Runner != null && Object != null && Object.IsValid)
        {
            Runner.SetIsSimulated(Object, true);
        }

        if (HasStateAuthority)
        {
            HeldPosition = transform.position;
        }
        InitializeSpawnRestPosition();
        StartCoroutine(WaitForPlayerId());
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        networkStateReady = false;
#if !UNITY_SERVER
        StopShotGroundAudioMonitor();
#endif
        base.Despawned(runner, hasState);
    }

    private void InitializeSpawnRestPosition()
    {
        Vector3 basePosition = transform.position;
#if UNITY_SERVER
        var host = GameSessionNetWork_Host.Instance;
        if (host != null && host.SpawnBallPoint != null)
        {
            basePosition = host.SpawnBallPoint.position;
        }
#endif
        SetSpawnRestPosition(basePosition);
    }

    public Vector3 GetSpawnRestPosition()
    {
        if (!spawnRestPositionInitialized)
        {
#if UNITY_SERVER
            var host = GameSessionNetWork_Host.Instance;
            if (host != null && host.SpawnBallPoint != null)
            {
                spawnRestPosition = host.SpawnBallPoint.position;
                spawnRestPositionInitialized = true;
            }
#endif
            if (!spawnRestPositionInitialized)
            {
                spawnRestPosition = transform.position;
                spawnRestPositionInitialized = true;
            }
        }

        return spawnRestPosition;
    }

    public void SetSpawnRestPosition(Vector3 position)
    {
        spawnRestPosition = position;
        spawnRestPositionInitialized = true;
    }

    private IEnumerator WaitForPlayerId()
    {
        while (playerId == 0)
            yield return null; // đợi đến khi playerId được host gán

        cachedPlayerId = playerId;

        while (!TryResolveOwnerHandler())
            yield return null;

#if UNITY_SERVER
        // Nếu là bot, đảm bảo ngay khi resolve owner thì đặt vị trí network về FingerPos (tránh mặc định 0,0,0)
        if (IsOwnerBot())
        {
            Vector3 finger = ResolveBotFingerPosition();

            HeldPosition = finger;
            transform.position = finger;
            EnsureKinematic();
            Debug.Log($"[HOST][BotBallInit] pid={playerId} ball snapped to {finger}");

            // Bắt đầu sync liên tục trong trường hợp vòng FixedUpdateNetwork chưa chạy kịp
            if (_botHoldSyncRoutine != null)
            {
                StopCoroutine(_botHoldSyncRoutine);
            }
            _botHoldSyncRoutine = StartCoroutine(KeepBotHeldAtFinger());
        }
#endif

        if (HasStateAuthority)
        {
            lastSyncedResistance = CurrentImpactResistance;
            lastSyncedDamagePoint = DamagePoint;
            RegisterBall();
        }
        else
        {
            #if !UNITY_SERVER
            EnsureBallModelVisualInstance();
            StartCoroutine(WaitForMaterialId());
            UpdateDamageVisual();
            RefreshNameArrow();
            #endif
        }
#if !UNITY_SERVER
        if (heartbeatCoroutine == null)
            heartbeatCoroutine = StartCoroutine(MonitorHeartbeatProximity());
#endif
    }

#if UNITY_SERVER
    private Coroutine _botHoldSyncRoutine;

    private bool IsOwnerBot()
    {
        var botCtrl = BotPlayerController.Instance;
        return botCtrl != null && botCtrl.IsBotPlayer(playerId);
    }

    /// <summary>
    /// Lấy vị trí finger cho bot. Bot không có client gửi input nên FingerPos luôn (0,0,0).
    /// Fallback: dùng vị trí player body (ownerHandler.transform.position) làm gốc.
    /// </summary>
    private Vector3 ResolveBotFingerPosition()
    {
        // 1. Nếu FingerPos đã được set (ví dụ từ logic khác), dùng nó
        if (ownerHandler != null && ownerHandler.FingerPos != Vector3.zero)
            return ownerHandler.FingerPos;

        // 2. Fallback: lấy vị trí player body
        if (ownerHandler != null && ownerHandler.transform.position != Vector3.zero)
        {
            Vector3 bodyPos = ownerHandler.transform.position;
            // Gán lại FingerPos cho networked state để client cũng nhận được
            ownerHandler.FingerPos = bodyPos;
            return bodyPos;
        }

        // 3. Fallback cuối: giữ nguyên vị trí ball hiện tại
        return transform.position;
    }

    private IEnumerator KeepBotHeldAtFinger()
    {
        var wait = new WaitForFixedUpdate();
        while (IsOwnerBot() && IsHolding == 1 && hasBeenShoot == 0)
        {
            if (ownerHandler == null && !TryResolveOwnerHandler())
            {
                yield return wait;
                continue;
            }

            Vector3 finger = ResolveBotFingerPosition();

            HeldPosition = finger;
            transform.position = finger;
            EnsureKinematic();

            yield return wait;
        }

        _botHoldSyncRoutine = null;
    }
#endif

#if !UNITY_SERVER
    private Transform GetInterpolationRenderTarget()
    {
        if (InterpolationTarget != null)
            return InterpolationTarget;

        var rbNet = GetComponent<NetworkRigidbody3D>();
        if (rbNet != null && rbNet.InterpolationTarget != null)
            return rbNet.InterpolationTarget;

        return transform;
    }

    private void KeepBallModelAtCenteredLocalOffset()
    {
        if (ballModelVisualInstance == null)
            return;

        ballModelVisualInstance.transform.localPosition = _centeredVisualLocalPosition;
    }

    private void SnapVisualAnchorTo(Vector3 position, Quaternion rotation)
    {
        EnsureBallVisualAnchor();
        if (_ballVisualAnchor == null)
            return;

        _ballVisualAnchor.SetPositionAndRotation(position, rotation);
        _smoothedRenderVelocity = Vector3.zero;
        _lastRenderPosition = position;
        _hasRenderSample = true;
        KeepBallModelAtCenteredLocalOffset();
    }

    private void UpdateVisualInterpolation(float deltaTime, bool snap = false)
    {
        if (ballModelVisualInstance == null)
            return;

        EnsureBallVisualAnchor();
        if (_ballVisualAnchor == null)
            return;

        var target = GetInterpolationRenderTarget();
        if (target == null)
            return;

        bool snapThisFrame = snap || !_hasRenderSample || HasInputAuthority || HasStateAuthority;
        if (snapThisFrame)
        {
            SnapVisualAnchorTo(target.position, target.rotation);
            return;
        }

        float safeDelta = Mathf.Max(0.0001f, deltaTime);
        var smoothed = Vector3.SmoothDamp(
            _ballVisualAnchor.position,
            target.position,
            ref _smoothedRenderVelocity,
            _renderSmoothTime,
            Mathf.Infinity,
            safeDelta);

        _ballVisualAnchor.position = smoothed;
        _lastRenderPosition = smoothed;

        float rotationT = Mathf.Clamp01(safeDelta * _renderRotationLerpSpeed);
        _ballVisualAnchor.rotation = Quaternion.Slerp(_ballVisualAnchor.rotation, target.rotation, rotationT);
        KeepBallModelAtCenteredLocalOffset();
    }

    private void EnsureBallModelVisualInstance()
    {
        if (GameInitializer.Instance == null || GameInitializer.Instance.BallModelVisual == null)
            return;

        EnsureBallVisualAnchor();

        if (ballModelVisualInstance == null)
        {
            ballModelVisualInstance = Instantiate(GameInitializer.Instance.BallModelVisual, _ballVisualAnchor);
            ballModelVisualInstance.transform.localPosition = Vector3.zero;
            ballModelVisualInstance.transform.localRotation = Quaternion.identity;
            _centeredVisualLocalPosition = Vector3.zero;
            CaptureBaseVisualScaleIfNeeded();
            ballModelVisualInstance.transform.localScale = _baseVisualScale * _currentScaleMultiplier;
            _hasRenderSample = false;
            _smoothedRenderVelocity = Vector3.zero;
            _hasCenteredVisual = false;
        }

        if (!_hasCenteredVisual)
        {
            CenterBallVisual();
        }

        CaptureBaseVisualScaleIfNeeded();

        if (_baseVisualMesh == null && ballModelVisualInstance != null)
        {
            var meshFilter = ballModelVisualInstance.GetComponentInChildren<MeshFilter>();
            if (meshFilter != null)
                _baseVisualMesh = meshFilter.sharedMesh;
        }
        //Tạm thời không sử dụng để tránh mất hoài niệm tuổi thơ
        //ApplyClientLevel10VfxIfReady();
    }

    public Transform GetCameraFocusTarget()
    {
        if (ballModelVisualInstance == null)
            EnsureBallModelVisualInstance();

        if (ballModelVisualInstance != null)
            return ballModelVisualInstance.transform;

        var rbNet = GetComponent<NetworkRigidbody3D>();
        if (rbNet != null && rbNet.InterpolationTarget != null)
            return rbNet.InterpolationTarget;

        return transform;
    }

    private void ApplyClientLevel10VfxIfReady()
    {
        return;
        if (ballModelVisualInstance == null)
            return;

        var levelVfx = ballModelVisualInstance.GetComponent<BallClientLocalVfx>();
        if (levelVfx == null)
            levelVfx = ballModelVisualInstance.AddComponent<BallClientLocalVfx>();

        levelVfx.SetLevel(BallLevel);
    }

    private void EnsureBallVisualAnchor()
    {
        Transform anchorParent = GetInterpolationRenderTarget();
        if (anchorParent == null)
            anchorParent = transform;

        if (_ballVisualAnchor != null)
        {
            if (_ballVisualAnchor.parent != anchorParent)
            {
                _ballVisualAnchor.SetParent(anchorParent, false);
            }
            return;
        }

        var anchorGO = new GameObject("BallVisualAnchor");
        _ballVisualAnchor = anchorGO.transform;
        _ballVisualAnchor.SetParent(anchorParent, false);
        _ballVisualAnchor.localPosition = Vector3.zero;
        _ballVisualAnchor.localRotation = Quaternion.identity;
        _ballVisualAnchor.localScale = Vector3.one;
        _hasRenderSample = false;
        _smoothedRenderVelocity = Vector3.zero;
    }

    private void CenterBallVisual()
    {
        if (ballModelVisualInstance == null)
            return;

        var renderer = ballModelVisualInstance.GetComponentInChildren<Renderer>();
        if (renderer == null)
            return;

        var worldCenter = renderer.bounds.center;
        var localCenter = ballModelVisualInstance.transform.InverseTransformPoint(worldCenter);
        if (localCenter.sqrMagnitude > Mathf.Epsilon)
        {
            ballModelVisualInstance.transform.localPosition -= localCenter;
        }

        _centeredVisualLocalPosition = ballModelVisualInstance.transform.localPosition;
        _hasCenteredVisual = true;
    }
#endif

    private bool TryResolveOwnerHandler()
    {
        if (playerId == 0)
            return false;

        if (ownerHandler != null)
        {
            var cachedModel = ownerHandler.PlayerModel;
            if (cachedModel.playerId == playerId)
            {
                _hasLoggedMissingOwnerHandler = false;
                return true;
            }

            ownerHandler = null;
        }

        var objectManager = NetworkObjectManager.Instance;
        if (objectManager == null)
            return false;

        var ownerGO = objectManager.GetPlayerObject(playerId);
        if (ownerGO == null)
            return false;

        ownerHandler = ownerGO.GetComponent<PlayerNetworkHandler>();
        if (ownerHandler == null || ownerHandler.PlayerModel.playerId != playerId)
        {
            if (HasStateAuthority && !_hasLoggedMissingOwnerHandler)
            {
                Debug.LogError($"[HOST] lỗi không tìm thấy PlayerNetworkHandler hợp lệ cho playerId {playerId}");
                _hasLoggedMissingOwnerHandler = true;
            }

            ownerHandler = null;
            return false;
        }

        _hasLoggedMissingOwnerHandler = false;
        return true;
    }

#if !UNITY_SERVER
    internal bool TryResolveOwnerHandlerForReconnect(out PlayerNetworkHandler handler)
    {
        if (ownerHandler == null && !TryResolveOwnerHandler())
        {
            handler = null;
            return false;
        }

        handler = ownerHandler;
        return handler != null;
    }


    private bool TryGetOwnerPlayerInfo(out PlayerInfoStruct ownerInfo, out PlayerNetworkHandler resolvedHandler)
    {
        ownerInfo = default;
        resolvedHandler = null;

        if (playerId == 0)
            return false;

        if (TryResolveOwnerHandler())
        {
            resolvedHandler = ownerHandler;
            ownerInfo = ownerHandler.PlayerModel;
            if (ownerInfo.playerId == playerId)
                return true;
        }

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return false;

        for (int i = 0; i < manager.players.Length; i++)
        {
            var info = manager.players.Get(i);
            if (info.playerId != playerId)
                continue;

            ownerInfo = info;
            return true;
        }

        return false;
    }

    private bool IsOwnerBotClient()
    {
        if (ownerHandler == null && !TryResolveOwnerHandler())
            return false;

        var provider = ownerHandler.PlayerModel.providerType.ToString();
        return !string.IsNullOrEmpty(provider) && provider.Equals("BOT", System.StringComparison.OrdinalIgnoreCase);
    }
#endif

    [Networked, OnChangedRender(nameof(OnScaleMultiplierChanged))]
    public float ScaleMultiplier { get; set; } = 1f;

    [Networked, OnChangedRender(nameof(OnGroundPinSkillSpinChanged))]
    public int GroundPinSkillSpinCounter { get; set; }

    private void OnScaleMultiplierChanged()
    {
        ApplyScaleMultiplierLocal(ScaleMultiplier, true);
    }

    private void OnGroundPinSkillSpinChanged()
    {
#if !UNITY_SERVER
        PlayGroundPinSkillVisualEffect();
#endif
    }

    public void ApplyScaleMultiplier(float multiplier, bool animate = true)
    {
        float clamped = Mathf.Max(0.1f, multiplier);
        if (HasStateAuthority)
            ScaleMultiplier = clamped;

        ApplyScaleMultiplierLocal(clamped, animate);
    }

    public void ResetScaleToDefault()
    {
        ApplyScaleMultiplier(1f, false);
    }

    private void ApplyScaleMultiplierLocal(float multiplier, bool animate = true)
    {
        float previousMultiplier = _currentScaleMultiplier;
        bool multiplierChanged = !Mathf.Approximately(previousMultiplier, multiplier);
        _currentScaleMultiplier = multiplier;

        if (_sphereCollider == null)
            _sphereCollider = GetComponent<SphereCollider>();

        if (_baseColliderRadius < 0f && _sphereCollider != null)
            _baseColliderRadius = _sphereCollider.radius;

#if UNITY_SERVER
        if (_sphereCollider != null)
        {
            _sphereCollider.radius = Mathf.Max(0.01f, _baseColliderRadius * _currentScaleMultiplier);
        }
#endif

#if !UNITY_SERVER
        EnsureBallModelVisualInstance();
        if (ballModelVisualInstance != null)
        {
            CaptureBaseVisualScaleIfNeeded();

            Vector3 targetScale = _baseVisualScale * _currentScaleMultiplier;
            if (!multiplierChanged)
                return;

            if (animate && ShouldPlayScaleSkillEffect(previousMultiplier, multiplier))
                PlayScaleSkillEffect(previousMultiplier, multiplier, targetScale);
            else
                SetVisualScaleImmediate(targetScale);
        }
#endif
    }

#if !UNITY_SERVER
    private bool ShouldPlayScaleSkillEffect(float previousMultiplier, float targetMultiplier)
    {
        if (Mathf.Approximately(targetMultiplier, 1f))
            return false;

        return Mathf.Abs(targetMultiplier - previousMultiplier) > 0.02f;
    }

    private void SetVisualScaleImmediate(Vector3 targetScale)
    {
        CleanupScaleSkillEffect(true);

        if (ballModelVisualInstance != null)
            ballModelVisualInstance.transform.localScale = targetScale;
    }

    private void PlayScaleSkillEffect(float previousMultiplier, float targetMultiplier, Vector3 targetScale)
    {
        if (ballModelVisualInstance == null)
            return;

        CleanupScaleSkillEffect(true);

        Transform visual = ballModelVisualInstance.transform;
        Vector3 currentScale = visual.localScale;
        if (currentScale.sqrMagnitude <= 0.0001f)
            currentScale = _baseVisualScale * Mathf.Max(0.1f, previousMultiplier);

        bool isBigSkill = targetMultiplier > previousMultiplier;
        Vector3 center = ResolveScaleSkillEffectCenter();
        float radius = ResolveScaleSkillEffectRadius(previousMultiplier, targetMultiplier);

        if (isBigSkill)
        {
            SoundManager.Instance?.PlayBallBigSkillEffect(center);
            CreateSkillGlow(center, new Color(0.35f, 0.9f, 1f, 1f), 1.8f, 0.55f);
            CreateScaleSkillSparks(center, radius, 14, new Color(0.4f, 0.95f, 1f, 1f), true);
            CreateScaleSkillShadow(center, previousMultiplier, targetMultiplier, 0.55f);
            ShakeScaleSkillCamera(0.1f, 0.025f);

            _scaleSkillEffectSequence = DOTween.Sequence();
            _scaleSkillEffectSequence.Append(visual.DOScale(currentScale * 0.85f, 0.08f).SetEase(Ease.InQuad));
            _scaleSkillEffectSequence.InsertCallback(0.12f, () =>
            {
                CreateScaleSkillSparks(ResolveScaleSkillEffectCenter(), radius * 1.1f, 10, new Color(1f, 0.7f, 0.25f, 1f), true);
            });
            _scaleSkillEffectSequence.Append(visual.DOScale(targetScale * 1.125f, 0.18f).SetEase(Ease.OutBack));
            _scaleSkillEffectSequence.Append(visual.DOScale(targetScale, 0.12f).SetEase(Ease.OutQuad));
        }
        else
        {
            SoundManager.Instance?.PlayBallSmallSkillEffect(center);
            CreateSkillGlow(center, new Color(0.95f, 0.55f, 1f, 1f), 1.35f, 0.34f);
            CreateCompressingSkillRing(center, radius * 2.35f, new Color(0.85f, 0.55f, 1f, 1f), 0.3f);
            CreateScaleSkillShadow(center, previousMultiplier, targetMultiplier, 0.3f);
            ShakeScaleSkillCamera(0.08f, 0.018f);

            _scaleSkillEffectSequence = DOTween.Sequence();
            _scaleSkillEffectSequence.Append(visual.DOScale(currentScale * 1.15f, 0.08f).SetEase(Ease.OutQuad));
            _scaleSkillEffectSequence.Append(visual.DOScale(targetScale, 0.16f).SetEase(Ease.InBack));
            _scaleSkillEffectSequence.InsertCallback(0.18f, () =>
            {
                CreateScaleSkillSparks(ResolveScaleSkillEffectCenter(), radius * 0.7f, 8, new Color(0.95f, 0.8f, 1f, 1f), false);
            });
        }

        _scaleSkillEffectSequence.OnComplete(() =>
        {
            if (ballModelVisualInstance != null)
                ballModelVisualInstance.transform.localScale = targetScale;
            _scaleSkillEffectSequence = null;
            CleanupScaleSkillEffect(false);
        });
    }

    private Vector3 ResolveScaleSkillEffectCenter()
    {
        if (ballModelVisualInstance != null)
            return ballModelVisualInstance.transform.position;

        return transform.position;
    }

    private float ResolveScaleSkillEffectRadius(float previousMultiplier, float targetMultiplier)
    {
        float baseRadius = _baseColliderRadius > 0f
            ? _baseColliderRadius
            : _sphereCollider != null ? _sphereCollider.radius : 0.25f;

        float multiplier = Mathf.Max(Mathf.Max(previousMultiplier, targetMultiplier), 1f);
        return Mathf.Clamp(baseRadius * multiplier * 2.2f, 0.35f, 2.4f);
    }

    private void CreateScaleSkillSparks(Vector3 center, float radius, int count, Color color, bool pullInward)
    {
        Material sparkMaterial = CreateScaleSkillMaterial(color);
        if (sparkMaterial != null)
            Destroy(sparkMaterial, 1f);

        for (int i = 0; i < count; i++)
        {
            Vector3 direction = UnityEngine.Random.onUnitSphere;
            direction.y = Mathf.Abs(direction.y) * 0.55f + 0.1f;
            direction.Normalize();

            Vector3 start = pullInward
                ? center + direction * UnityEngine.Random.Range(radius * 0.65f, radius * 1.35f)
                : center + direction * UnityEngine.Random.Range(radius * 0.08f, radius * 0.3f);
            Vector3 end = pullInward
                ? center + direction * 0.03f
                : center + direction * UnityEngine.Random.Range(radius * 0.45f, radius * 0.9f);

            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = pullInward ? "BallSkillSpark_In" : "BallSkillSpark_Out";
            spark.transform.position = start;
            spark.transform.localScale = Vector3.one * Mathf.Clamp(radius * 0.08f, 0.025f, 0.09f);
            var collider = spark.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = spark.GetComponent<Renderer>();
            if (renderer != null && sparkMaterial != null)
                renderer.sharedMaterial = sparkMaterial;

            _scaleSkillEffectObjects.Add(spark);
            float delay = UnityEngine.Random.Range(0f, 0.06f);
            float duration = pullInward ? UnityEngine.Random.Range(0.12f, 0.2f) : UnityEngine.Random.Range(0.08f, 0.14f);
            spark.transform.DOMove(end, duration).SetDelay(delay).SetEase(pullInward ? Ease.InQuad : Ease.OutQuad);
            spark.transform.DOScale(Vector3.zero, duration).SetDelay(delay).SetEase(Ease.InQuad);
        }
    }

    private void CreateCompressingSkillRing(Vector3 center, float radius, Color color, float duration)
    {
        GameObject ringObject = new GameObject("BallSkillCompressRing");
        ringObject.transform.position = center;
        ringObject.transform.localScale = Vector3.one * Mathf.Max(0.1f, radius);

        var line = ringObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 64;
        line.widthMultiplier = 0.035f;
        line.material = CreateScaleSkillMaterial(color);

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i / (float)line.positionCount * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
        }

        _scaleSkillEffectObjects.Add(ringObject);
        ringObject.transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack);
        FadeLineRenderer(line, duration);
    }

    private void CreateScaleSkillShadow(Vector3 center, float previousMultiplier, float targetMultiplier, float duration)
    {
        Vector3 shadowPosition = center + Vector3.down * 0.08f;
        if (Physics.Raycast(center + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            shadowPosition = hit.point + Vector3.up * 0.015f;

        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadow.name = "BallSkillTweenShadow";
        shadow.transform.position = shadowPosition;
        shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var collider = shadow.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Color shadowColor = new Color(0f, 0f, 0f, 0.22f);
        Material shadowMaterial = CreateScaleSkillMaterial(shadowColor);
        var renderer = shadow.GetComponent<Renderer>();
        if (renderer != null && shadowMaterial != null)
            renderer.sharedMaterial = shadowMaterial;

        float startSize = Mathf.Clamp(previousMultiplier * 0.48f, 0.18f, 1.2f);
        float endSize = Mathf.Clamp(targetMultiplier * 0.48f, 0.12f, 1.35f);
        shadow.transform.localScale = new Vector3(startSize, startSize, 1f);
        _scaleSkillEffectObjects.Add(shadow);

        shadow.transform.DOScale(new Vector3(endSize, endSize, 1f), duration).SetEase(Ease.OutQuad);
        if (shadowMaterial != null)
        {
            Color color = shadowMaterial.color;
            DOTween.To(() => color.a, value =>
            {
                if (shadowMaterial == null)
                    return;
                color.a = value;
                shadowMaterial.color = color;
            }, 0f, duration).SetDelay(Mathf.Max(0f, duration - 0.12f));
            Destroy(shadowMaterial, duration + 0.2f);
        }
    }

    private void CreateSkillGlow(Vector3 center, Color color, float intensity, float duration)
    {
        GameObject lightObject = new GameObject("BallSkillGlowLight");
        lightObject.transform.position = center;
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = 2.2f;
        light.intensity = intensity;
        _scaleSkillEffectObjects.Add(lightObject);

        DOTween.To(() => light != null ? light.intensity : 0f, value =>
        {
            if (light != null)
                light.intensity = value;
        }, 0f, duration).SetEase(Ease.OutQuad);
    }

    private void FadeLineRenderer(LineRenderer line, float duration)
    {
        if (line == null || line.material == null)
            return;

        Material material = line.material;
        Color color = material.color;
        DOTween.To(() => color.a, value =>
        {
            if (material == null)
                return;
            color.a = value;
            material.color = color;
        }, 0f, duration).SetEase(Ease.InQuad);
        Destroy(material, duration + 0.2f);
    }

    private static Material CreateScaleSkillMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            return null;

        var material = new Material(shader)
        {
            color = color
        };

        return material;
    }

    private void ShakeScaleSkillCamera(float duration, float strength)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        camera.transform.DOShakePosition(duration, strength, 8, 45f, false, true);
    }

    private void CleanupScaleSkillEffect(bool killSequence)
    {
        if (killSequence && _scaleSkillEffectSequence != null)
        {
            _scaleSkillEffectSequence.Kill();
            _scaleSkillEffectSequence = null;
        }

        for (int i = 0; i < _scaleSkillEffectObjects.Count; i++)
        {
            var obj = _scaleSkillEffectObjects[i];
            if (obj == null)
                continue;

            obj.transform.DOKill();
            Destroy(obj);
        }

        _scaleSkillEffectObjects.Clear();
    }

    private void PlayGroundPinSkillVisualEffect()
    {
        EnsureBallModelVisualInstance();
        if (ballModelVisualInstance == null)
            return;

        _groundPinSkillVisualSequence?.Kill();

        Transform visual = ballModelVisualInstance.transform;
        _groundPinSkillVisualSequence = DOTween.Sequence();
        _groundPinSkillVisualSequence.Append(
            visual.DOLocalRotate(new Vector3(0f, 1440f, 0f), 0.26f, RotateMode.FastBeyond360)
                .SetRelative()
                .SetEase(Ease.OutQuart));
        _groundPinSkillVisualSequence.Append(
            visual.DOLocalRotate(new Vector3(0f, 360f, 0f), 0.12f, RotateMode.FastBeyond360)
                .SetRelative()
                .SetEase(Ease.InQuad));
        _groundPinSkillVisualSequence.OnComplete(() => _groundPinSkillVisualSequence = null);
    }
#endif

    private IEnumerator WaitForMaterialId()
    {
        while (BallMaterialId == 0)
            yield return null;

#if !UNITY_SERVER
        EnsureBallModelVisualInstance();
#endif
        StartCoroutine(LoadBallAssets());
    }

#if UNITY_SERVER
    private void OnMaterialChanged()
    {
        StartCoroutine(LoadBallAssets());
    }

    private void OnCateyeChanged()
    {
        StartCoroutine(LoadBallAssets());
    }

    private IEnumerator LoadBallAssets()
    {
        yield break;
    }
    private void OnBallLevelChanged()
    {
 
    }
#else
    private void OnMaterialChanged()
    {
        EnsureBallModelVisualInstance();
        StartCoroutine(LoadBallAssets());
    }

    private void OnCateyeChanged()
    {
        EnsureBallModelVisualInstance();
        StartCoroutine(LoadBallAssets());
    }

    private void OnBallLevelChanged()
    {
        EnsureBallModelVisualInstance();
        ApplyClientLevel10VfxIfReady();
    }

    private IEnumerator LoadBallAssets()
    {
        EnsureBallModelVisualInstance();

        var visual = ballModelVisualInstance;
        if (visual == null)
            yield break;

        if (mainMaterialHandle.IsValid())
        {
            Addressables.Release(mainMaterialHandle);
            mainMaterialHandle = default;
        }
        if (cateyeMaterialHandle.IsValid())
        {
            Addressables.Release(cateyeMaterialHandle);
            cateyeMaterialHandle = default;
        }
        if (vfxHandle.IsValid())
        {
            Addressables.Release(vfxHandle);
            vfxHandle = default;
        }
        if (vipVFXInstance != null)
        {
            Destroy(vipVFXInstance);
            vipVFXInstance = null;
        }

        yield return AddressablesHelper.LoadAssetWithHandle<Material>($"{AddressablePaths.Items.Culi}/{BallMaterialId}.mat", (mat, handle) =>
        {
            mainMaterialHandle = handle;
            if (mat != null)
            {
                var rend = visual.GetComponent<Renderer>();
                if (rend != null)
                    rend.material = mat;
            }
        });

        Transform cateye = visual.transform.Find("Cateye");
        if (cateye != null)
        {
            cateye.gameObject.SetActive(HasCateye);
        }

        if (HasCateye && cateye != null)
        {
            var cateyeRenderer = cateye.GetComponent<Renderer>();
            var cateyeMaterialMissing = false;

            yield return AddressablesHelper.LoadAssetWithHandle<Material>($"{AddressablePaths.Items.CuliCateye}/{BallMaterialId}.mat", (mat, handle) =>
            {
                cateyeMaterialHandle = handle;
                if (cateyeRenderer != null && mat != null)
                {
                    cateyeRenderer.material = mat;
                }
                else if (mat == null)
                {
                    cateyeMaterialMissing = true;
                }
            });

            if (cateyeMaterialMissing)
            {
                if (cateyeMaterialHandle.IsValid())
                {
                    Addressables.Release(cateyeMaterialHandle);
                    cateyeMaterialHandle = default;
                }

                Debug.LogWarning($"Cateye material for BallMaterialId {BallMaterialId} not found. Falling back to default material.");

                yield return AddressablesHelper.LoadAssetWithHandle<Material>(AddressablePaths.Items.DefaultCateyeCuliMaterial, (mat, handle) =>
                {
                    cateyeMaterialHandle = handle;
                    if (cateyeRenderer != null && mat != null)
                    {
                        cateyeRenderer.material = mat;
                    }
                });
            }
        }
        //tạm thời không dùng
        //string effectPath = null;
        //if (BallLevel >= 10)
        //    effectPath = $"{AddressablePaths.Items.CuliEffect}/vip3.prefab";
        //else if (BallLevel >= 8)
        //    effectPath = $"{AddressablePaths.Items.CuliEffect}/vip2.prefab";
        //else if (BallLevel >= 5)
        //    effectPath = $"{AddressablePaths.Items.CuliEffect}/vip1.prefab";

        //if (effectPath != null)
        //{
        //    bool loaded = false;
        //    yield return AddressablesHelper.LoadAssetWithHandle<GameObject>(effectPath, (prefab, handle) =>
        //    {
        //        vfxHandle = handle;
        //        if (prefab != null)
        //        {
        //            loaded = true;
        //            vipVFXInstance = Instantiate(prefab, visual.transform);
        //            vipVFXInstance.transform.localPosition = Vector3.zero;
        //        }
        //    });

        //    if (!loaded)
        //    {
        //        yield return AddressablesHelper.LoadAssetWithHandle<GameObject>($"{AddressablePaths.Items.CuliEffect}/Default.prefab", (prefab, handle) =>
        //        {
        //            vfxHandle = handle;
        //            if (prefab != null)
        //            {
        //                vipVFXInstance = Instantiate(prefab, visual.transform);
        //                vipVFXInstance.transform.localPosition = Vector3.zero;
        //            }
        //            else
        //            {
        //                Debug.LogError("Fallback prefab cũng bị null!");
        //            }
        //        });
        //    }
        //}
    }
#endif
    public void RefreshClientLevel10Vfx()
    {
#if !UNITY_SERVER
        EnsureBallModelVisualInstance();
        ApplyClientLevel10VfxIfReady();
#endif
    }
    // ✅ Gọi khi cần xin quyền để điều khiển viên bi (ví dụ lúc bắt đầu kéo bắn)
    public void RequestAuthorityIfNeeded()
    {
        if (!_networkObject.HasStateAuthority)
        {
            _networkObject.RequestStateAuthority();
            _hasRequestedAuthority = true;
            Debug.Log("✅ [Client] Requested State Authority for ball.");
        }
    }
    // ✅ Gọi khi đã bắn xong hoặc thả tay => trả quyền lại cho host
    public void ReturnAuthorityToHost()
    {
        if (_networkObject.HasStateAuthority && _hasRequestedAuthority)
        {
            _networkObject.ReleaseStateAuthority();
            _hasRequestedAuthority = false;
            Debug.Log("🔁 [Client] Returned State Authority to Host.");
        }
    }

    //public IEnumerator ShootBall(Vector3 direction, float force, Vector3 spin)
    //{
    //    float timeout = 15f;
    //    float elapsed = 0f;
    //    //Host xin lại quyền
    //    RequestAuthorityIfNeeded();

    //    while (!HasStateAuthority && elapsed < timeout)
    //    {
    //        elapsed += Time.deltaTime;
    //        yield return null;
    //    }

    //    if (!HasStateAuthority)
    //    {
    //        Debug.LogWarning($"❌ Không có quyền StateAuthority sau {timeout}s – không thể AddForce");
    //        yield break;
    //    }

    //    // ⚙️ Cấu hình Rigidbody để đảm bảo xoáy hoạt động
    //    rb.isKinematic = false;
    //    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    //    hasBeenShoot = 1;
    //    IsHolding = 0;

    //    // 🎯 Lực bắn chính
    //    rb.AddForce(direction * force, ForceMode.Impulse);

    //    // Các hiệu ứng môi trường như trượt khi trời mưa đã được chuyển sang
    //    // PhysicsBallHelper để tránh lặp lại xử lý ở nhiều nơi.

    //    // 🌀 Xử lý Spin (Xì-đê)
    //    Vector3 torque = Vector3.zero;

    //    if (Mathf.Abs(spin.z) > 0.01f)
    //    {
    //        float backSpinPower = spin.z * 8f;
    //        torque += -direction.normalized * backSpinPower;
    //    }

    //    if (Mathf.Abs(spin.x) > 0.01f)
    //    {
    //        Vector3 left = Vector3.Cross(Vector3.up, direction).normalized;
    //        float sideSpinPower = spin.x * 8f;
    //        torque += left * sideSpinPower;
    //    }

    //    if (torque != Vector3.zero)
    //    {
    //        // Chỉ áp dụng xoáy; các hiệu ứng lực phụ đã được quản lý bởi PhysicsBallHelper
    //        rb.AddTorque(torque, ForceMode.VelocityChange);
    //    }

    //    Debug.Log($"🟢 {playerId} shot ball | Force: {force} | Spin: {spin} | AngularVel: {rb.angularVelocity}");

    //    yield break;
    //}

#if UNITY_SERVER
    private void EnsureKinematic()
    {
        if (rb == null)
            rb = GetComponent<NetworkRigidbody3D>().Rigidbody;

        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;  // Ngăn mọi tác động vật lý
        }
    }

    public bool TryConsumeActiveBallSkillUse(int skillId)
    {
        if (!HasStateAuthority || skillId <= 0 || IsActive == 0)
            return false;

        int maxUses = GetActiveBallSkillMaxUses();
        int used = activeBallSkillUseCountBySkill.TryGetValue(skillId, out int current) ? current : 0;
        if (used >= maxUses)
        {
            Debug.Log($"[HOST][BallSkillLimit] Reject skill {skillId} for player {playerId}. used={used}/{maxUses}, level={BallLevel}.");
            return false;
        }

        activeBallSkillUseCountBySkill[skillId] = used + 1;
        return true;
    }

    private int GetActiveBallSkillMaxUses()
    {
        int level = Mathf.Max(1, BallLevel);
        if (level >= 10)
            return 3;
        if (level >= 5)
            return 2;
        return 1;
    }

    public bool CanArmGroundPinSkill()
    {
        var manager = NetworkObjectManager.Instance;
        return HasStateAuthority &&
               IsActive != 0 &&
               IsHolding == 1 &&
               hasBeenShoot == 0 &&
               manager != null &&
               manager.IsYourTurn(playerId);
    }

    public bool ArmGroundPinSkill()
    {
        if (!CanArmGroundPinSkill())
            return false;

        groundPinSkillArmed = true;
        groundPinSkillShotActive = false;
        groundPinSkillHasLeftGround = false;
        groundPinSkillTriggered = false;
        groundPinSkillShotElapsed = 0f;
        groundPinSkillSpinElapsed = 0f;
        RestoreGroundPinConstraints();
        Debug.Log($"[HOST][GroundPinSkill] Armed for player {playerId}.");
        return true;
    }

    private void BeginGroundPinSkillShotIfArmed()
    {
        RestoreGroundPinConstraints();

        if (!groundPinSkillArmed)
            return;

        groundPinSkillArmed = false;
        groundPinSkillShotActive = true;
        groundPinSkillHasLeftGround = false;
        groundPinSkillTriggered = false;
        groundPinSkillShotElapsed = 0f;
        groundPinSkillSpinElapsed = 0f;
        groundPinSkillPinnedPosition = transform.position;
        groundPinSkillSpinAxis = Vector3.up;
    }

    private bool UpdateGroundPinSkillServer()
    {
        if (!groundPinSkillShotActive)
            return false;

        float deltaTime = Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime;
        groundPinSkillShotElapsed += deltaTime;

        if (groundPinSkillTriggered)
        {
            UpdateGroundPinSpinServer(deltaTime);
            return true;
        }

        bool touchingGround = TryGetGroundPinContactServer(transform.position, out RaycastHit groundHit);
        if (!touchingGround)
        {
            groundPinSkillHasLeftGround = true;
            return false;
        }

        bool canUseContact = groundPinSkillShotElapsed >= GroundPinSkillMinFlightSeconds &&
            (groundPinSkillHasLeftGround || groundPinSkillShotElapsed >= GroundPinSkillFallbackGroundSeconds);
        if (!canUseContact)
            return false;

        TriggerGroundPinSkillServer(groundHit.point, groundHit.normal);
        return true;
    }

    private void UpdateGroundPinSpinServer(float deltaTime)
    {
        if (rb == null)
            EnsureRigidbody();

        if (rb != null)
        {
            rb.position = groundPinSkillPinnedPosition;
            transform.position = groundPinSkillPinnedPosition;
            rb.linearVelocity = Vector3.zero;

            groundPinSkillSpinElapsed += deltaTime;
            float t = Mathf.Clamp01(groundPinSkillSpinElapsed / GroundPinSkillSpinDuration);
            float speed = Mathf.Lerp(GroundPinSkillInitialAngularSpeed, 0f, t * t);
            rb.angularVelocity = groundPinSkillSpinAxis * speed;
        }

        if (groundPinSkillSpinElapsed >= GroundPinSkillSpinDuration)
            CompleteGroundPinSkillStopServer();
    }

    private bool TryTriggerGroundPinSkillServer(Collision collision)
    {
        if (!groundPinSkillShotActive ||
            groundPinSkillTriggered ||
            collision == null ||
            collision.gameObject == null ||
            !HasTagInHierarchy(collision.gameObject, "Ground"))
        {
            return false;
        }

        if (groundPinSkillShotElapsed < GroundPinSkillMinFlightSeconds &&
            !groundPinSkillHasLeftGround)
        {
            return false;
        }

        ContactPoint contact = collision.contactCount > 0 ? collision.contacts[0] : default(ContactPoint);
        Vector3 point = collision.contactCount > 0 ? contact.point : transform.position;
        Vector3 normal = collision.contactCount > 0 && contact.normal.sqrMagnitude > 0.0001f
            ? contact.normal.normalized
            : Vector3.up;
        TriggerGroundPinSkillServer(point, normal);
        return true;
    }

    private void TriggerGroundPinSkillServer(Vector3 contactPoint, Vector3 contactNormal)
    {
        if (groundPinSkillTriggered)
            return;

        EnsureRigidbody();

        Vector3 normal = contactNormal.sqrMagnitude > 0.0001f ? contactNormal.normalized : Vector3.up;
        float radius = ResolveGroundPinSkillRadiusServer();
        groundPinSkillPinnedPosition = contactPoint + normal * radius;
        groundPinSkillSpinAxis = normal;
        groundPinSkillTriggered = true;
        groundPinSkillSpinElapsed = 0f;
        groundPinSkillArmed = false;

        if (rb != null)
        {
            SaveGroundPinConstraintsIfNeeded();
            rb.position = groundPinSkillPinnedPosition;
            transform.position = groundPinSkillPinnedPosition;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = groundPinSkillSpinAxis * GroundPinSkillInitialAngularSpeed;
            rb.constraints |= RigidbodyConstraints.FreezePositionX |
                              RigidbodyConstraints.FreezePositionY |
                              RigidbodyConstraints.FreezePositionZ;
        }

        GroundPinSkillSpinCounter++;
        Debug.Log($"[HOST][GroundPinSkill] Triggered for player {playerId} at {groundPinSkillPinnedPosition}.");
    }

    private void ClearExpiredGroundPinSkillArmServer()
    {
        if (!groundPinSkillArmed || groundPinSkillShotActive)
            return;

        var manager = NetworkObjectManager.Instance;
        bool shouldClear = manager == null ||
            manager.IsGameEnded ||
            !manager.IsYourTurn(playerId) ||
            IsActive == 0 ||
            IsHolding != 1 ||
            hasBeenShoot != 0;

        if (shouldClear)
            ClearGroundPinSkillState();
    }

    private void CompleteGroundPinSkillStopServer()
    {
        if (rb != null)
        {
            rb.position = groundPinSkillPinnedPosition;
            transform.position = groundPinSkillPinnedPosition;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        RestoreGroundPinConstraints();
        groundPinSkillShotActive = false;
        groundPinSkillTriggered = false;
        groundPinSkillHasLeftGround = false;
        groundPinSkillShotElapsed = 0f;
        groundPinSkillSpinElapsed = 0f;
        stoppedTime = 0f;
        hasBeenShoot = 0;

        if (ownerHandler == null)
            TryResolveOwnerHandler();

        GameSessionNetWork_Host.Instance?.HandleBallStopped(ownerHandler, playerId, transform.position);
        Debug.Log($"[HOST][GroundPinSkill] Stopped player {playerId} at {transform.position}.");
    }

    private void ClearGroundPinSkillState()
    {
        groundPinSkillArmed = false;
        groundPinSkillShotActive = false;
        groundPinSkillHasLeftGround = false;
        groundPinSkillTriggered = false;
        groundPinSkillShotElapsed = 0f;
        groundPinSkillSpinElapsed = 0f;
        RestoreGroundPinConstraints();
    }

    private void SaveGroundPinConstraintsIfNeeded()
    {
        if (rb == null || groundPinSkillConstraintsSaved)
            return;

        groundPinSkillOriginalConstraints = rb.constraints;
        groundPinSkillConstraintsSaved = true;
    }

    private void RestoreGroundPinConstraints()
    {
        if (rb == null || !groundPinSkillConstraintsSaved)
            return;

        rb.constraints = groundPinSkillOriginalConstraints;
        groundPinSkillConstraintsSaved = false;
    }

    private bool TryGetGroundPinContactServer(Vector3 ballPosition, out RaycastHit groundHit)
    {
        groundHit = default;
        float radius = ResolveGroundPinSkillRadiusServer();
        float probeOffset = Mathf.Max(GroundPinSkillGroundProbeOffset, radius + 0.05f);
        float probeDistance = probeOffset + radius + GroundPinSkillGroundContactTolerance;
        Vector3 origin = ballPosition + Vector3.up * probeOffset;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            probeDistance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || !HasTagInHierarchy(hit.collider.gameObject, "Ground"))
                continue;

            float centerToGround = ballPosition.y - hit.point.y;
            if (centerToGround > radius + GroundPinSkillGroundContactTolerance ||
                centerToGround < -GroundPinSkillGroundContactTolerance)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundHit = hit;
            }
        }

        return closestDistance < float.MaxValue;
    }

    private float ResolveGroundPinSkillRadiusServer()
    {
        if (_sphereCollider == null)
            _sphereCollider = GetComponent<SphereCollider>();

        if (_sphereCollider != null)
            return Mathf.Max(0.01f, _sphereCollider.radius * MaxAbsScale(transform.lossyScale));

        var collider = GetComponent<Collider>();
        if (collider != null)
            return Mathf.Max(0.01f, Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.z));

        return 0.1f;
    }

    /// <summary>
    /// Snap bi về vị trí ngón tay của owner (hoặc fallback vị trí hiện tại) và khóa động học.
    /// Dùng cho bot trước khi bắn để tránh rơi tự do do FingerPos không được gửi từ input.
    /// </summary>
    public void SnapToOwnerFinger()
    {
        if (ownerHandler == null)
            TryResolveOwnerHandler();

        Vector3 target;
        if (IsOwnerBot())
        {
            target = ResolveBotFingerPosition();
        }
        else
        {
            target = ownerHandler != null ? ownerHandler.FingerPos : transform.position;
        }

        // Nếu vẫn chưa có vị trí hợp lệ, giữ nguyên vị trí hiện tại
        if (target == Vector3.zero)
            target = transform.position;

        // Căn xuống mặt đất (Ground). "Ground" hiện là Tag, không phải Layer.
        const float groundProbeHeight = 2f;
        const float groundProbeDistance = 5f;
        Vector3 probeOrigin = target + Vector3.up * groundProbeHeight;

        RaycastHit? taggedGroundHit = null;
        float taggedDistance = float.MaxValue;
        RaycastHit? nearestHit = null;
        float nearestDistance = float.MaxValue;

        var hits = Physics.RaycastAll(probeOrigin, Vector3.down, groundProbeDistance, Physics.AllLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.distance < nearestDistance)
            {
                nearestDistance = h.distance;
                nearestHit = h;
            }

            if (h.collider != null && h.collider.CompareTag("Ground") && h.distance < taggedDistance)
            {
                taggedDistance = h.distance;
                taggedGroundHit = h;
            }
        }

        var chosenHit = taggedGroundHit.HasValue ? taggedGroundHit.Value : (nearestHit ?? default(RaycastHit?));
        if (chosenHit.HasValue)
        {
            float radius = _sphereCollider != null ? _sphereCollider.radius * Mathf.Max(0.01f, _currentScaleMultiplier) : 0.05f;
            target = chosenHit.Value.point + Vector3.up * radius;
        }

        HeldPosition = target;
        transform.position = target;

        if (rb == null)
            rb = GetComponent<NetworkRigidbody3D>().Rigidbody;

        if (rb != null)
        {
            bool wasKinematic = rb.isKinematic;
            if (wasKinematic)
                rb.isKinematic = false; // tránh cảnh báo khi set velocity

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Đảm bảo collider bật để va chạm mặt đất
        if (_sphereCollider != null && !_sphereCollider.enabled)
            _sphereCollider.enabled = true;
    }


    private void ApplyNearStopBrake(float deltaTime)
    {
        if (rb == null || rb.isKinematic || hasBeenShoot != 1 || IsHolding == 1 || deltaTime <= 0f)
        {
            nearStopBrakeTime = 0f;
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float horizontalSpeed = horizontalVelocity.magnitude;
        float verticalSpeed = Mathf.Abs(velocity.y);
        float angularSpeed = rb.angularVelocity.magnitude;

        bool shouldBrake =
            horizontalSpeed <= NearStopBrakeLinearThreshold &&
            verticalSpeed <= NearStopBrakeVerticalThreshold &&
            angularSpeed <= NearStopBrakeAngularThreshold &&
            IsGroundedForNearStopBrake();

        if (!shouldBrake)
        {
            nearStopBrakeTime = 0f;
            return;
        }

        nearStopBrakeTime += deltaTime;
        float ramp = Mathf.Clamp01(nearStopBrakeTime / NearStopBrakeRampSeconds);
        float linearDecel = Mathf.Lerp(NearStopBrakeLinearDecelMin, NearStopBrakeLinearDecelMax, ramp);
        float angularDecel = Mathf.Lerp(NearStopBrakeAngularDecelMin, NearStopBrakeAngularDecelMax, ramp);

        Vector3 brakedHorizontal = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, linearDecel * deltaTime);
        float brakedVertical = Mathf.MoveTowards(velocity.y, 0f, linearDecel * 0.35f * deltaTime);
        rb.linearVelocity = new Vector3(brakedHorizontal.x, brakedVertical, brakedHorizontal.z);
        rb.angularVelocity = Vector3.MoveTowards(rb.angularVelocity, Vector3.zero, angularDecel * deltaTime);

        if (nearStopBrakeTime >= NearStopForceStopDelay &&
            brakedHorizontal.magnitude <= NearStopForceStopVelocity &&
            rb.angularVelocity.magnitude <= NearStopForceStopAngular)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private bool IsGroundedForNearStopBrake()
    {
        float radius = ResolveWorldBallRadiusForNearStopBrake();
        Vector3 origin = transform.position + Vector3.up * Mathf.Max(radius * 0.8f, 0.02f);
        float castRadius = Mathf.Max(radius * 0.75f, 0.006f);
        float castDistance = Mathf.Max(radius * 2.5f, 0.08f);
        var hits = Physics.SphereCastAll(origin, castRadius, Vector3.down, castDistance, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
    }

    private float ResolveWorldBallRadiusForNearStopBrake()
    {
        if (_sphereCollider == null)
            _sphereCollider = GetComponent<SphereCollider>();

        float radius = _sphereCollider != null ? _sphereCollider.radius : 0.5f;
        Vector3 scale = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return Mathf.Max(0.005f, radius * Mathf.Max(maxScale, 0.001f));
    }

    private void CheckBallStoped()
    {
        // Nếu tốc độ thấp và không bị giữ, bắt đầu đếm thời gian dừng
        if (rb.linearVelocity.magnitude < minVelocity && rb.angularVelocity.magnitude < minAngular && hasBeenShoot == 1)
        {
            stoppedTime += Runner.DeltaTime;

            if (stoppedTime >= requiredStopDuration)
            {
                // Bi thực sự đứng yên đủ lâu => xử lý
                //tạm thời ẩn đi
                //float pct = CurrentImpactResistance / initialImpactResistance;
                //physicsHelper?.SpawnCrackPrefab(pct); // spawn crack prefab when ball stops

                hasBeenShoot = 0;
                stoppedTime = 0f;
                nearStopBrakeTime = 0f;
                //SoundManager.Instance.StartBallRollingLoop(gameObject, () => rb.velocity.magnitude);
                //RPC_StartBallRollingLoop();
                Debug.Log($"[HOST][BallStop] pid={playerId} pos={transform.position} vel={rb.linearVelocity.magnitude:F4} ang={rb.angularVelocity.magnitude:F4}");
                ClearGroundPinSkillState();
                if (ownerHandler == null)
                {
                    TryResolveOwnerHandler();
                }

                var host = GameSessionNetWork_Host.Instance;
                if (host == null)
                    return;

                host.HandleBallStopped(ownerHandler, playerId, transform.position);
            }
        }
        else
        {
            // Nếu bi vẫn đang chạy => reset thời gian đứng yên
            stoppedTime = 0f;
            if (rb.linearVelocity.magnitude > NearStopBrakeLinearThreshold ||
                rb.angularVelocity.magnitude > NearStopBrakeAngularThreshold)
            {
                nearStopBrakeTime = 0f;
            }
        }
    }
#endif

    //public override void FixedUpdateNetwork()
    //{
    //    if (!HasStateAuthority) return;
    //    if (IsActive == 0) return;
    //    if (IsHolding == 1 && hasBeenShoot == 0)
    //    {
    //        UpdateBallFollowFinger();
    //    }
    //    else if (IsHolding == 0 && hasBeenShoot == 1)
    //    {
    //        CheckBallStoped();
    //    }    



    //    //physicsHelper?.ApplyFramePhysics(Runner.DeltaTime);

    //    //if (HasStateAuthority && hasBeenShoot == 1 && _hasRequestedAuthority)
    //    //{

    //    //    ReturnAuthorityToHost();
    //    //    Debug.Log("🔁 [Client] Đã trả quyền về cho host trước khi addfroce");
    //    //}


    //}

#if UNITY_SERVER
    void ResetToStartPoint(string reason = null)
    {
        var host = GameSessionNetWork_Host.Instance;
        var botCtrl = BotPlayerController.Instance;
        var manager = NetworkObjectManager.Instance;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        ClearGroundPinSkillState();

        var playerGO = manager?.GetPlayerObject(playerId);
        var handler = playerGO != null ? playerGO.GetComponent<PlayerNetworkHandler>() : null;

        bool isExam = handler != null && handler.PlayerModel.statusPlayer == StatusPlayer.ShootExam;
        bool isBotExam = isExam && botCtrl != null && botCtrl.IsBotPlayer(playerId);

        if (isExam)
        {
            ownerHandler = ownerHandler ?? handler;
            IsHolding = 1;
            hasBeenShoot = 0;

            if (manager != null)
            {
                for (int i = 0; i < manager.players.Length; i++)
                {
                    var info = manager.players.Get(i);
                    if (info.playerId == playerId)
                    {
                        info.isHolding = true;
                        manager.players.Set(i, info);
                        if (handler != null)
                            handler.PlayerModel = info;
                        break;
                    }
                }
            }

            // Snap back to the finger so the player can retry the exam shot.
            var targetFingerPos = handler.FingerPos;
            HeldPosition = targetFingerPos;
            transform.position = HeldPosition;
            EnsureKinematic();
            Debug.Log($"[HOST][ProtectiveShieldResetExam] pid={playerId} returned to finger at {HeldPosition}. reason={reason ?? "unknown"}");

            if (isBotExam)
            {
                StartCoroutine(botCtrl.ExecuteBotExamShot(playerId));
            }
            return;
        }

        var startPoint = host != null && host.StartPointMain != null
            ? host.StartPointMain
            : null;

        if (startPoint != null)
        {
            transform.position = startPoint.position;
        }
        hasBeenShoot = 0;

        if (handler != null && startPoint != null)
        {
            host?.SetPlayerStatus(playerId, StatusPlayer.MoveStartPoint);
            StartCoroutine(handler.TeleportToTarget(startPoint.position, startPoint));
            Debug.Log($"[HOST][ProtectiveShieldReset] pid={playerId} returned to start point at {startPoint.position}. reason={reason ?? "unknown"}");
        }
    }
#endif
    void RegisterBall()
    {
        cachedPlayerId = playerId;
        NetworkObjectManager.Instance.RegisterPlayerBall(playerId, Object, BallIndex, IsActive == 1);
        //GameSessionNetWork_Host.Instance.RegisterDict(playerId, Object, GameSessionNetWork_Host.Instance.ballDict);
        //if (!GameSessionNetWork_Host.Instance.playerBalls.ContainsKey(playerId))
        //    GameSessionNetWork_Host.Instance.playerBalls[playerId] = new List<NetworkObject>();
        //GameSessionNetWork_Host.Instance.playerBalls[playerId].Add(Object);
        _networkObject = GetComponent<NetworkObject>();
        IsSpawned = 1;
        didRegisterBall = true;
        Debug.Log($"[HOST] spwned culi thành công pid={playerId} ballIndex={BallIndex}");
        if (ownerHandler == null)
            TryResolveOwnerHandler();
#if UNITY_SERVER
        PlayerNetworkHandler.NotifyBallSpawnedServer(this);
#endif
    }
#if !UNITY_SERVER
    private int GetCachedOrNetworkPlayerId()
    {
        if (cachedPlayerId != 0)
            return cachedPlayerId;

        if (CanAccessNetworkedState)
            return playerId;

        return 0;
    }

    private void ClearNameArrow()
    {
        if (nameArrow == null)
            return;

        int ownerPlayerId = GetCachedOrNetworkPlayerId();
        if (ownerPlayerId != 0 && NameArrowsByPlayer.TryGetValue(ownerPlayerId, out var existingArrow) && existingArrow == nameArrow)
            NameArrowsByPlayer.Remove(ownerPlayerId);

        Destroy(nameArrow);
        nameArrow = null;
    }

    private bool IsBehindMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return false;

        Vector3 toBall = transform.position - mainCamera.transform.position;
        return Vector3.Dot(mainCamera.transform.forward, toBall) <= 0f;
    }

    public void RefreshNameArrow()
    {
        if (GameManagerNetWork.Instance == null || GameManagerNetWork.Instance.loginUserModel == null)
            return;

        if (IsBehindMainCamera())
        {
            ClearNameArrow();
            return;
        }

        if (GameManagerNetWork.Instance.serverRPC == null && !GameManagerNetWork.Instance.TryResolveServerRPC())
            return;

        if (GameSessionClientLocal.Instance == null || GameSessionClientLocal.Instance.playerArrowPrefab == null)
            return;

        if (!TryGetOwnerPlayerInfo(out var ownerInfo, out _))
        {
            ClearNameArrow();
            return;
        }

        int loginUserId = GameManagerNetWork.Instance.loginUserModel.UserId;
        int currentIndex = GameManagerNetWork.Instance.serverRPC.currentPlayerIndex;

        if (playerId == loginUserId)
        {
            ClearNameArrow();
            return; // Không hiển thị mũi tên cho chính người chơi đang đăng nhập
        }

        bool isYourTurn = NetworkObjectManager.Instance != null && NetworkObjectManager.Instance.IsYourTurn(playerId); // Kiểm tra có phải lượt của người chơi này không
        bool shouldShow = IsActive == 1 &&
                          ownerInfo.turnOrder != currentIndex &&
                          !isYourTurn; // Chỉ hiển thị khi đang hoạt động, không phải lượt hiện tại và không phải của bạn
        bool shouldShowChamCat = ClientGameplayBridge.Skill.ShouldShowChamCatIconForTarget(loginUserId, this);

        bool shouldShowArrow = shouldShow || shouldShowChamCat;
        var manager = NetworkObjectManager.Instance;
        if (manager != null)
        {
            var activeBall = manager.GetBallPhysics(playerId)?.active;
            if (activeBall != null && activeBall != this)
            {
                shouldShowArrow = false;
            }
        }

        string fullname = ownerInfo.fullname.ToString();

        if (shouldShowArrow)
        {
            if (nameArrow == null) // Chỉ tạo mới khi chưa có mũi tên hiện hữu
            {
                if (NameArrowsByPlayer.TryGetValue(playerId, out var existingArrow) && existingArrow != null && existingArrow != nameArrow)
                {
                    Destroy(existingArrow);
                    NameArrowsByPlayer.Remove(playerId);
                }

                nameArrow = ArrowTextHelper.ShowArrow(transform, fullname, GameSessionClientLocal.Instance.playerArrowPrefab); // Tạo mũi tên hiển thị tên
                if (nameArrow != null)
                    NameArrowsByPlayer[playerId] = nameArrow;
            }

            if (nameArrow != null)
            {
                var arrowUI = nameArrow.GetComponent<PlayerArrowUI>();
                if (arrowUI != null)
                {
                    arrowUI.SetLabelText(fullname);
                    arrowUI.SetKillIconVisible(shouldShowChamCat, this);
                }
            }
        }
        else
        {
            ClearNameArrow();
        }
    }
#endif
    private void CacheAllCollidersIfNeeded()
    {
        if (_cachedAllColliders != null && _cachedAllColliders.Length > 0)
            return;

        _cachedAllColliders = GetComponentsInChildren<Collider>(true);
    }

#if !UNITY_SERVER
    private void CacheVisualRenderersIfNeeded()
    {
        if (_cachedVisualRenderers != null && _cachedVisualRenderers.Length > 0)
            return;

        _cachedVisualRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void CaptureBaseVisualScaleIfNeeded()
    {
        if (_hasBaseVisualScale || ballModelVisualInstance == null)
            return;

        _baseVisualScale = Vector3.one;
        _hasBaseVisualScale = true;
    }
#endif

    // Bật/tắt viên bi và đảm bảo không ảnh hưởng tới vật lý khi ẩn
    void SetBallActive(bool active)
    {
#if UNITY_SERVER
        if (!active)
            ClearGroundPinSkillState();
#endif
        if (_networkObject == null)
            _networkObject = GetComponent<NetworkObject>();

        if (HasStateAuthority)
        {
            var rbNet = GetComponent<NetworkRigidbody3D>();
            if (rbNet != null)
            {
                if (rb == null)
                    rb = rbNet.Rigidbody;

                if (!active)
                {
                    if (!rbNet.Rigidbody.isKinematic)
                    {
                        rbNet.Rigidbody.linearVelocity = Vector3.zero;
                        rbNet.Rigidbody.angularVelocity = Vector3.zero;
                    }

                    rbNet.Rigidbody.isKinematic = true;
                }
                else
                {
                    rbNet.Rigidbody.isKinematic = false;
                    rbNet.Rigidbody.linearVelocity = Vector3.zero;
                    rbNet.Rigidbody.angularVelocity = Vector3.zero;
                }
            }
        }

        CacheAllCollidersIfNeeded();
        if (_cachedAllColliders != null)
        {
            foreach (var collider in _cachedAllColliders)
            {
                if (collider == null)
                    continue;

                collider.enabled = active;
            }
        }

#if !UNITY_SERVER
        CacheVisualRenderersIfNeeded();
        if (_cachedVisualRenderers != null)
        {
            foreach (var renderer in _cachedVisualRenderers)
            {
                if (renderer == null)
                    continue;

                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    renderer.enabled = active;
            }
        }
#endif

        if (Runner != null && CanAccessNetworkedState)
        {
            Runner.SetIsSimulated(Object, active || !HasStateAuthority);
        }

        Transform cateye = transform.Find("Cateye");
        if (cateye != null)
        {
            cateye.gameObject.SetActive(active && HasCateye);
        }
#if !UNITY_SERVER
        if (vipVFXInstance != null)
            vipVFXInstance.SetActive(active);
        if (!active && nameArrow != null) // Khi viên bi bị ẩn mà vẫn có mũi tên thì cần dọn dẹp
        {
            ClearNameArrow();
        }
#endif

    }

    void OnActiveChanged()
    {
        SetBallActive(IsActive == 1);
#if !UNITY_SERVER
        RefreshNameArrow();
#endif
    }
#if !UNITY_SERVER


    void LateUpdate()
    {
        // Cập nhật vị trí nhưng không xoay theo viên bi
        //cameraFollowBall.position = InterpolationTarget.position;
        //cameraFollowBall.rotation = Quaternion.Euler(0, 0, 0); // Giữ hướng cố định
        if(cameraFollowBall != null && IsHolding == 0 && hasBeenShoot == 1)
        {
            // 1. Dừng Tween hiện tại (nếu có) để tránh xung đột
            cameraFollowBall.DOKill();

            // 2. Chuyển vị trí camera đến vị trí của mục tiêu trong khoảng thời gian FollowDuration
            cameraFollowBall.DOMove(
                ballModelVisualInstance != null ? ballModelVisualInstance.transform.position : transform.position,
                0.2f // Thời gian di chuyển
            )
            // SetEase là tùy chọn, dùng Ease.OutQuad để làm chậm lại khi đến mục tiêu.
            .SetEase(Ease.OutQuad);

            // 3. Giữ Rotation Cố định:
            // Đảm bảo không có script nào khác thay đổi rotation, 
            // hoặc gán lại nếu cần thiết:
            cameraFollowBall.rotation = Quaternion.Euler(0, 0, 0);

        }

        if (Time.time >= _nextArrowRefreshTime)
        {
            RefreshNameArrow();
            _nextArrowRefreshTime = Time.time + ArrowRefreshInterval;
        }

    }

    public override void Render()
    {
        base.Render();
        float deltaTime = Time.deltaTime;
        EnsureBallModelVisualInstance();

        // Đồng bộ hệ số scale nếu có thay đổi từ server
        if (!Mathf.Approximately(_currentScaleMultiplier, ScaleMultiplier))
        {
            ApplyScaleMultiplierLocal(ScaleMultiplier);
        }

        if (ballModelVisualInstance != null)
        {
            if (IsHolding == 1 && hasBeenShoot == 0)
            {
                Vector3 holdPosition = HeldPosition;

                if (!IsOwnerBotClient())
                {
                    if (ownerHandler == null)
                        TryResolveOwnerHandler();

                    if (ownerHandler != null && ownerHandler.FingerPosition != null)
                        holdPosition = ownerHandler.FingerPosition.position;
                }

                var renderTarget = GetInterpolationRenderTarget();
                if (holdPosition == Vector3.zero && renderTarget != null)
                    holdPosition = renderTarget.position;

                Quaternion holdRotation = renderTarget != null ? renderTarget.rotation : transform.rotation;
                SnapVisualAnchorTo(holdPosition, holdRotation);
            }
            else
            {
                UpdateVisualInterpolation(deltaTime);
            }
        }

        //if (!damageInitialized || CurrentImpactResistance != lastSyncedResistance || (DamagePoint - lastSyncedDamagePoint).sqrMagnitude > 0.0001f)
        //{
        //    UpdateDamageVisual();
        //    lastSyncedResistance = CurrentImpactResistance;
        //    lastSyncedDamagePoint = DamagePoint;
        //    damageInitialized = true;
        //}

        UpdateLocalCollisionAudioState();
    }
#endif




#if UNITY_SERVER
    private bool _waterCollisionHandledThisShot;

    public void NotifyShotStartedServer()
    {
        _waterCollisionHandledThisShot = false;
        GameSessionNetWork_Host.Instance?.NotifyPlayerShotStarted(playerId);
    }

    private static bool HasTagInHierarchy(GameObject obj, string tag)
    {
        if (obj == null || string.IsNullOrEmpty(tag))
            return false;

        if (SafeCompareTag(obj, tag))
            return true;

        Transform current = obj.transform.parent;
        while (current != null)
        {
            if (SafeCompareTag(current.gameObject, tag))
                return true;
            current = current.parent;
        }

        return false;
    }

    private static bool SafeCompareTag(GameObject obj, string tag)
    {
        if (obj == null)
            return false;

        try
        {
            return obj.CompareTag(tag);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private bool HandleProtectiveShieldCollisionServer(GameObject otherObject)
    {
        if (playerId <= 0 ||
            IsHolding == 1 ||
            hasBeenShoot != 1 ||
            NetworkObjectManager.Instance == null ||
            NetworkObjectManager.Instance.IsGameEnded)
        {
            return false;
        }

        string objectName = otherObject != null ? otherObject.name : "null";
        Debug.Log($"[HOST][ProtectiveShieldHit] pid={playerId} obj={objectName} pos={transform.position}");
        ResetToStartPoint("protective_shield");
        return true;
    }

    private void HandleWaterCollisionServer(GameObject otherObject)
    {
        if (_waterCollisionHandledThisShot)
            return;

        var host = GameSessionNetWork_Host.Instance;
        if (host == null)
            return;

        if (host.HandleBallWaterHit(playerId, transform.position, otherObject, "physics"))
        {
            _waterCollisionHandledThisShot = true;
        }
    }

    private void RegisterPendingCollision(Collision collision)
    {
        float impactForce = CalculateImpactForce(collision);
        Vector3 relativeVelocity = collision.relativeVelocity;
        float impactSpeed = relativeVelocity.magnitude;
        Vector3 contactNormal = collision.contactCount > 0 ? collision.contacts[0].normal : Vector3.zero;
        RegisterPendingCollision(
            collision.gameObject,
            collision.contactCount > 0 ? collision.contacts[0].point : (Vector3?)null,
            impactForce,
            impactSpeed,
            relativeVelocity,
            contactNormal);
    }

    private static float CalculateImpactForce(Collision collision)
    {
        float impulse = collision.impulse.magnitude;
        if (impulse > 0f)
            return impulse / Time.fixedDeltaTime;

        return collision.relativeVelocity.magnitude;
    }

    private void RegisterPendingCollision(
        GameObject otherObject,
        Vector3? contactPoint = null,
        float? impactForce = null,
        float? impactSpeed = null,
        Vector3? relativeVelocity = null,
        Vector3? contactNormal = null)
    {
        var info = new PendingCollisionInfo
        {
            other = otherObject,
            impactForce = impactForce ?? 0f,
            impactSpeed = impactSpeed ?? 0f,
            hasContact = contactPoint.HasValue,
            contactPoint = contactPoint ?? Vector3.zero,
            relativeVelocity = relativeVelocity ?? Vector3.zero,
            contactNormal = contactNormal ?? Vector3.zero
        };

        _pendingCollisions.Add(info);
    }

    private void ProcessPendingCollisionsServer()
    {
        if (_pendingCollisions.Count == 0)
            return;

        for (int i = 0; i < _pendingCollisions.Count; i++)
        {
            var info = _pendingCollisions[i];
            var otherObject = info.other;
            if (otherObject == null)
                continue;

            var audibleMagnitude = Mathf.Max(info.impactForce, 0.1f);
            Vector3 contactPoint = info.hasContact ? info.contactPoint : transform.position;

            HitSurface surfaceType = HitSurface.None;
            bool hitGrass = false;
            bool hitSwamp = false;
            bool hitPuddle = false;

            if (otherObject.layer == LayerMask.NameToLayer("Ball"))
            {
                surfaceType = HitSurface.Ball;
            }
            else if (HasTagInHierarchy(otherObject, "Water"))
            {
                HandleWaterCollisionServer(otherObject);
                surfaceType = HitSurface.Water;
            }
            else if (otherObject.CompareTag("Rock"))
            {
                surfaceType = HitSurface.Rock;
            }
            else if (otherObject.CompareTag("Tree"))
            {
                surfaceType = HitSurface.Tree;
            }
            else if (otherObject.CompareTag("Grass"))
            {
                hitGrass = true;
                surfaceType = HitSurface.Grass;
            }
            else if (otherObject.CompareTag("Swamp"))
            {
                hitSwamp = true;
                surfaceType = HitSurface.Swamp;
            }
            else if (otherObject.CompareTag("Puddle"))
            {
                hitPuddle = true;
                surfaceType = HitSurface.Puddle;
            }

            if (hitGrass)
            {
                ApplySurfaceSlowdown(0.6f, 0.7f, 0.6f, 1.25f);
            }
            else if (hitSwamp)
            {
                ApplySurfaceSlowdown(0.25f, 0.4f, 1.5f, 2.5f);
            }
            else if (hitPuddle)
            {
                ApplySurfaceSlowdown(0.05f, 0.1f, 2.5f, 1.2f);
            }

            float selfDamage = 0f;
            if (ShouldApplyImpactDamage(otherObject, info.impactSpeed))
            {
                selfDamage = ProcessImpactDamage(info);
            }

            if (surfaceType != HitSurface.None)
            {
                LastHitDamage = selfDamage;
                LastHitInfo = new HitInfo
                {
                    magnitude = audibleMagnitude,
                    point = contactPoint,
                    surfaceType = surfaceType
                };
            }
            if(ownerHandler == null)
            {
                var playerbody = NetworkObjectManager.Instance?.GetPlayerObject(playerId);
                if (playerbody == null)
                    continue;

                  ownerHandler = playerbody.GetComponent<PlayerNetworkHandler>();
                if (ownerHandler == null)
                    continue;
            }    


            var playerToUpdate = ownerHandler.PlayerModel;
            if (playerToUpdate.statusPlayer == StatusPlayer.ShootExam ||
                playerToUpdate.statusPlayer == StatusPlayer.MoveStartPoint)
                continue;

            if (IsActive == 0 || IsHolding == 1 || hasBeenShoot != 1)
                continue;

            if (!NetworkObjectManager.Instance.IsYourTurn(playerId) || playerToUpdate.score <= 0)
                continue;

            if (otherObject.CompareTag("BallPlayer") || otherObject.tag.StartsWith("BallPlayer"))
            {
                var ballController = otherObject.GetComponent<BallServerController>();
                if (ballController == null)
                    ballController = otherObject.GetComponentInParent<BallServerController>();

                if (ballController != null)
                {
                    int targetPlayerId = ballController.playerId;
                    if (targetPlayerId == playerId)
                    {
                        Debug.Log($"[HOST] Bỏ qua self-hit: bi của {playerId} va chạm bi cùng chủ khi đổi/cầm/bắn.");
                        continue;
                    }

                    if (ballController.IsActive == 0)
                    {
                        Debug.Log($"[HOST] Bỏ qua va chạm với bi inactive của player {targetPlayerId}.");
                        continue;
                    }

                    bool skipRepeatedHitAnnouncement = ShouldSkipRepeatedBallPlayerHitAnnouncement(playerId, targetPlayerId);
                    if (skipRepeatedHitAnnouncement)
                    {
                        Debug.Log($"[HOST][BallHitUI] Bỏ qua thông báo/âm thanh trùng: {playerId} -> {targetPlayerId}");
                    }
                    else
                    {
                        Debug.Log($"👑 [HOST] Viên bi của {playerId} bắn trúng {targetPlayerId}");
                        RPC_PlayBallPlayerHitAnnouncement(audibleMagnitude);
                        RegisterSlowMotionHit(targetPlayerId, contactPoint);
                    }

                    var playerbodyTarGet = NetworkObjectManager.Instance?.GetPlayerObject(targetPlayerId);
                    if (playerbodyTarGet == null)
                        continue;

                    var scriptTarGetPlayer = playerbodyTarGet.GetComponent<PlayerNetworkHandler>();
                    if (scriptTarGetPlayer == null)
                        continue;

                    var host = GameSessionNetWork_Host.Instance;
                    bool catAnTienEffective = scriptTarGetPlayer.PlayerModel.isCatAnTienActive == 1 &&
                                              (host == null || host.IsCatAnTienEffectiveAgainstAttacker(targetPlayerId, playerId));

                    if (catAnTienEffective)
                    {
                        StartCoroutine(CheckCatAnTien(() =>
                        {
                            host?.SetPlayerStatus(targetPlayerId, StatusPlayer.WaitingDestroy);
                        }));
                    }
                    else
                    {
                        host?.SetPlayerStatus(targetPlayerId, StatusPlayer.WaitingDestroy);
                    }
                }
                else
                {
                    Debug.LogWarning("Không tìm thấy BallServerController trên vật thể va chạm");
                }
            }
        }

        _pendingCollisions.Clear();
    }

    private void ApplyBallCollisionResponse(Collision collision)
    {
        if (collision == null || collision.gameObject == null || !HasStateAuthority)
            return;

        var collisionBody = collision.rigidbody != null
            ? collision.rigidbody
            : collision.collider != null ? collision.collider.attachedRigidbody : null;
        if (!IsBallCollisionObject(collision.gameObject) &&
            (collisionBody == null || !IsBallCollisionObject(collisionBody.gameObject)))
        {
            return;
        }

        EnsureRigidbody();
        if (rb == null || rb.isKinematic)
            return;

        if (!TryGetCollisionBallRigidbody(collision, out var otherRb, out var otherCtrl))
            return;

        if (otherRb == null || otherRb == rb || otherRb.isKinematic)
            return;

        if (IsActive == 0 || (otherCtrl != null && otherCtrl.IsActive == 0))
            return;

        if (ShouldSkipRepeatedBallCollisionResponse(rb, otherRb))
            return;

        Rigidbody moverRb = rb;
        Rigidbody targetRb = otherRb;
        BallServerController moverCtrl = this;
        BallServerController targetCtrl = otherCtrl;

        float thisSpeedSqr = ProjectHorizontal(rb.linearVelocity).sqrMagnitude;
        float otherSpeedSqr = ProjectHorizontal(otherRb.linearVelocity).sqrMagnitude;
        bool thisIsShotBall = hasBeenShoot == 1;
        bool otherIsShotBall = otherCtrl != null && otherCtrl.hasBeenShoot == 1;
        bool shouldUseOtherAsMover = otherIsShotBall && !thisIsShotBall;
        if (!thisIsShotBall && !otherIsShotBall)
        {
            shouldUseOtherAsMover = otherSpeedSqr > thisSpeedSqr;
        }

        if (shouldUseOtherAsMover)
        {
            moverRb = otherRb;
            targetRb = rb;
            moverCtrl = otherCtrl;
            targetCtrl = this;
        }

        Vector3 moverVelocity = ProjectHorizontal(moverRb.linearVelocity);
        Vector3 targetVelocity = ProjectHorizontal(targetRb.linearVelocity);
        Vector3 relativeVelocity = moverVelocity - targetVelocity;
        if (relativeVelocity.sqrMagnitude < BallCollisionMinRelativeSpeed * BallCollisionMinRelativeSpeed)
            return;

        Vector3 normal = ResolveBallCollisionNormal(moverRb, targetRb, collision);
        if (Vector3.Dot(relativeVelocity, normal) < 0f)
            normal = -normal;

        float moverNormalSpeed = Vector3.Dot(moverVelocity, normal);
        float targetNormalSpeed = Vector3.Dot(targetVelocity, normal);
        float approachSpeed = moverNormalSpeed - targetNormalSpeed;
        if (approachSpeed < BallCollisionMinRelativeSpeed)
            return;

        float moverMass = Mathf.Max(0.05f, moverRb.mass);
        float targetMass = Mathf.Max(0.05f, targetRb.mass);
        float restitution = ResolveBallCollisionRestitution(moverRb, targetRb, collision);
        float massSum = moverMass + targetMass;

        float newMoverNormalSpeed =
            ((moverMass - restitution * targetMass) * moverNormalSpeed +
             (1f + restitution) * targetMass * targetNormalSpeed) / massSum;
        float newTargetNormalSpeed =
            ((1f + restitution) * moverMass * moverNormalSpeed +
             (targetMass - restitution * moverMass) * targetNormalSpeed) / massSum;

        if (moverCtrl != null && moverCtrl.hasBeenShoot == 1 && moverNormalSpeed > 0f)
        {
            float retainLimit = moverNormalSpeed * BallCollisionShotNormalRetainLimit;
            if (newMoverNormalSpeed > retainLimit)
                newMoverNormalSpeed = retainLimit;
        }

        Vector3 moverTangent = moverVelocity - normal * moverNormalSpeed;
        Vector3 targetTangent = targetVelocity - normal * targetNormalSpeed;
        Vector3 newMoverVelocity = moverTangent + normal * newMoverNormalSpeed;
        Vector3 newTargetVelocity = targetTangent + normal * newTargetNormalSpeed;

        float moverOriginalY = moverRb.linearVelocity.y;
        float targetOriginalY = targetRb.linearVelocity.y;
        newMoverVelocity.y = moverOriginalY;
        newTargetVelocity.y = targetOriginalY;

        float maxTargetSpeed = Mathf.Max(ProjectHorizontal(targetRb.linearVelocity).magnitude, ProjectHorizontal(moverRb.linearVelocity).magnitude * BallCollisionMaxTargetSpeedMultiplier);
        newTargetVelocity = ClampHorizontalSpeed(newTargetVelocity, maxTargetSpeed);

        moverRb.linearVelocity = newMoverVelocity;
        targetRb.linearVelocity = newTargetVelocity;
        ApplyRollingAngularVelocity(moverRb, newMoverVelocity);
        ApplyRollingAngularVelocity(targetRb, newTargetVelocity);
        moverCtrl?.physicsHelper?.ApplyRareRealWorldBallCollision(
            moverRb,
            targetRb,
            normal,
            moverVelocity,
            targetVelocity,
            approachSpeed,
            restitution,
            moverCtrl.hasBeenShoot == 1);
        moverRb.WakeUp();
        targetRb.WakeUp();

        if (targetCtrl != null && targetCtrl.hasBeenShoot == 0 && ProjectHorizontal(newTargetVelocity).sqrMagnitude > minVelocity * minVelocity)
            targetCtrl.stoppedTime = 0f;
    }

    private static Vector3 ProjectHorizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static Vector3 ClampHorizontalSpeed(Vector3 velocity, float maxHorizontalSpeed)
    {
        if (maxHorizontalSpeed <= 0f)
            return velocity;

        Vector3 horizontal = ProjectHorizontal(velocity);
        float maxSpeedSqr = maxHorizontalSpeed * maxHorizontalSpeed;
        if (horizontal.sqrMagnitude <= maxSpeedSqr)
            return velocity;

        horizontal = horizontal.normalized * maxHorizontalSpeed;
        velocity.x = horizontal.x;
        velocity.z = horizontal.z;
        return velocity;
    }

    private static Vector3 ResolveBallCollisionNormal(Rigidbody moverRb, Rigidbody targetRb, Collision collision)
    {
        Vector3 normal = ProjectHorizontal(targetRb.worldCenterOfMass - moverRb.worldCenterOfMass);
        if (normal.sqrMagnitude > 0.0001f)
            return normal.normalized;

        if (collision != null && collision.contactCount > 0)
        {
            normal = ProjectHorizontal(targetRb.worldCenterOfMass - collision.contacts[0].point);
            if (normal.sqrMagnitude > 0.0001f)
                return normal.normalized;
        }

        normal = ProjectHorizontal(moverRb.linearVelocity - targetRb.linearVelocity);
        if (normal.sqrMagnitude > 0.0001f)
            return normal.normalized;

        return Vector3.forward;
    }

    private static float ResolveBallCollisionRestitution(Rigidbody moverRb, Rigidbody targetRb, Collision collision)
    {
        float restitution = BallCollisionRestitutionFallback;
        if (collision != null)
        {
            restitution = Mathf.Max(
                restitution,
                GetColliderBounciness(collision.collider),
                GetColliderBounciness(GetPrimaryEnabledCollider(moverRb)),
                GetColliderBounciness(GetPrimaryEnabledCollider(targetRb)));
        }

        return Mathf.Clamp(restitution, 0.65f, 0.95f);
    }

    private static Collider GetPrimaryEnabledCollider(Rigidbody targetBody)
    {
        if (targetBody == null)
            return null;

        var directCollider = targetBody.GetComponent<Collider>();
        if (directCollider != null && directCollider.enabled)
            return directCollider;

        var colliders = targetBody.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var collider = colliders[i];
            if (collider != null && collider.enabled)
                return collider;
        }

        return directCollider;
    }

    private static float GetColliderBounciness(Collider collider)
    {
        if (collider == null || collider.sharedMaterial == null)
            return 0f;

        return collider.sharedMaterial.bounciness;
    }

    private static void ApplyRollingAngularVelocity(Rigidbody targetBody, Vector3 velocity)
    {
        if (targetBody == null)
            return;

        Vector3 horizontalVelocity = ProjectHorizontal(velocity);
        if (horizontalVelocity.sqrMagnitude < 0.0001f)
            return;

        float radius = ResolveRigidbodyRadius(targetBody);
        if (radius <= 0.001f)
            return;

        Vector3 rollingAngularVelocity = Vector3.Cross(Vector3.up, horizontalVelocity) / radius;
        targetBody.angularVelocity = Vector3.Lerp(targetBody.angularVelocity, rollingAngularVelocity, BallCollisionRollingSpinBlend);
    }

    private static float ResolveRigidbodyRadius(Rigidbody targetBody)
    {
        var sphere = targetBody.GetComponent<SphereCollider>();
        if (sphere != null)
            return Mathf.Max(0.01f, sphere.radius * MaxAbsScale(targetBody.transform.lossyScale));

        var collider = targetBody.GetComponent<Collider>();
        if (collider != null)
            return Mathf.Max(0.01f, Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.z));

        return 0.1f;
    }

    private static float MaxAbsScale(Vector3 scale)
    {
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    private bool TryGetCollisionBallRigidbody(Collision collision, out Rigidbody otherRb, out BallServerController otherCtrl)
    {
        otherRb = null;
        otherCtrl = null;

        if (collision == null)
            return false;

        otherRb = collision.rigidbody != null ? collision.rigidbody : collision.collider != null ? collision.collider.attachedRigidbody : null;
        if (otherRb == null && collision.collider != null)
            otherRb = collision.collider.GetComponentInParent<NetworkRigidbody3D>()?.Rigidbody;

        if (otherRb == null || otherRb == rb)
            return false;

        otherCtrl = otherRb.GetComponent<BallServerController>();
        if (otherCtrl == null && collision.collider != null)
            otherCtrl = collision.collider.GetComponentInParent<BallServerController>();

        return otherCtrl != null || IsBallCollisionObject(otherRb.gameObject);
    }

    private static bool ShouldSkipRepeatedBallCollisionResponse(Rigidbody first, Rigidbody second)
    {
        if (first == null || second == null)
            return true;

        if (BallCollisionResponseFrameByPair.Count > 512)
            BallCollisionResponseFrameByPair.Clear();

        ulong key = BuildRigidbodyPairKey(first, second);
        int frame = Time.frameCount;
        if (BallCollisionResponseFrameByPair.TryGetValue(key, out var lastFrame) && lastFrame == frame)
            return true;

        BallCollisionResponseFrameByPair[key] = frame;
        return false;
    }

    private static ulong BuildRigidbodyPairKey(Rigidbody first, Rigidbody second)
    {
        uint firstId = unchecked((uint)first.GetInstanceID());
        uint secondId = unchecked((uint)second.GetInstanceID());
        if (firstId > secondId)
        {
            uint temp = firstId;
            firstId = secondId;
            secondId = temp;
        }

        return ((ulong)firstId << 32) | secondId;
    }

    private static bool IsBallCollisionObject(GameObject obj)
    {
        if (obj == null)
            return false;

        int ballLayer = LayerMask.NameToLayer("Ball");
        if (ballLayer >= 0 && obj.layer == ballLayer)
            return true;

        string tagValue = obj.tag;
        if (!string.IsNullOrEmpty(tagValue) && IsBallPlayerTag(tagValue))
            return true;

        return obj.GetComponentInParent<BallServerController>() != null;
    }

    private static bool ShouldSkipRepeatedBallPlayerHitAnnouncement(int attackerPlayerId, int targetPlayerId)
    {
        if (attackerPlayerId <= 0 || targetPlayerId <= 0)
            return false;

        if (BallPlayerHitAnnouncementTimeByPair.Count > 512)
            BallPlayerHitAnnouncementTimeByPair.Clear();

        ulong key = BuildPlayerHitAnnouncementKey(attackerPlayerId, targetPlayerId);
        float now = Time.time;
        if (BallPlayerHitAnnouncementTimeByPair.TryGetValue(key, out float lastTime) &&
            now - lastTime < BallPlayerHitAnnouncementDedupeSeconds)
        {
            return true;
        }

        BallPlayerHitAnnouncementTimeByPair[key] = now;
        return false;
    }

    private static ulong BuildPlayerHitAnnouncementKey(int attackerPlayerId, int targetPlayerId)
    {
        uint attackerId = unchecked((uint)attackerPlayerId);
        uint targetId = unchecked((uint)targetPlayerId);
        return ((ulong)attackerId << 32) | targetId;
    }

    private void ApplySurfaceSlowdown(float velocityMultiplier, float angularMultiplier, float dampingBoost, float duration)
    {
        EnsureRigidbody();
        if (rb == null)
            return;

        if (velocityMultiplier < 1f)
            rb.linearVelocity *= Mathf.Clamp01(velocityMultiplier);

        if (angularMultiplier < 1f)
            rb.angularVelocity *= Mathf.Clamp01(angularMultiplier);

        if (dampingBoost <= 0f || duration <= 0f)
            return;

        float targetDamping = initialLinearDamping + dampingBoost;
        if (rb.linearDamping < targetDamping)
            rb.linearDamping = targetDamping;

        float resetTime = Time.time + duration;
        if (surfaceSlowdownCoroutine == null)
        {
            surfaceSlowdownResetTime = resetTime;
            surfaceSlowdownCoroutine = StartCoroutine(ResetSurfaceDampingAfterDelay());
        }
        else if (resetTime > surfaceSlowdownResetTime)
        {
            surfaceSlowdownResetTime = resetTime;
        }
    }

    private IEnumerator ResetSurfaceDampingAfterDelay()
    {
        while (Time.time < surfaceSlowdownResetTime)
            yield return null;

        if (rb != null)
            rb.linearDamping = initialLinearDamping;

        surfaceSlowdownCoroutine = null;
    }

#endif

#if UNITY_SERVER
    private void OnCollisionEnter(Collision collision)
    {
        var collisionObject = collision.gameObject;
        if (collisionObject != null &&
            HasTagInHierarchy(collisionObject, ProtectiveShieldTag) &&
            HandleProtectiveShieldCollisionServer(collisionObject))
        {
            return;
        }

        // Xử lý water ngay lập tức (không đợi ProcessPendingCollisionsServer)
        // vì ProcessPendingCollisionsServer có thể bị skip khi IsActive==0 hoặc IsGameEnded
        if (collisionObject != null && HasTagInHierarchy(collisionObject, "Water"))
        {
            HandleWaterCollisionServer(collisionObject);
        }

        if (groundPinSkillTriggered && collisionObject != null && HasTagInHierarchy(collisionObject, "Ground"))
            return;

        if (TryTriggerGroundPinSkillServer(collision))
            return;

        ApplyBallCollisionResponse(collision);
        RegisterPendingCollision(collision);
        if (collisionObject == null || !HasTagInHierarchy(collisionObject, "Water"))
            physicsHelper?.ApplyDamagedCollisionBounceModifier(rb, collision, initialImpactResistance, CurrentImpactResistance);
        if (playerId == 0)
            return;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        if (HasTagInHierarchy(other.gameObject, ProtectiveShieldTag) &&
            HandleProtectiveShieldCollisionServer(other.gameObject))
        {
            return;
        }

        if (HasTagInHierarchy(other.gameObject, "Water"))
        {   
            HandleWaterCollisionServer(other.gameObject);
            var currentVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
            RegisterPendingCollision(
                other.gameObject,
                other.ClosestPoint(transform.position),
                currentVelocity.magnitude,
                currentVelocity.magnitude,
                currentVelocity,
                Vector3.zero);
        }
        else if (other.CompareTag("Puddle"))
        {
            var currentVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
            RegisterPendingCollision(
                other.gameObject,
                other.ClosestPoint(transform.position),
                currentVelocity.magnitude,
                currentVelocity.magnitude,
                currentVelocity,
                Vector3.zero);
        }
    }
#endif
#if !UNITY_SERVER
    private void OnCollisionEnter(Collision collision)
    {
        TryPlayLocalCollisionAudio(collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryPlayLocalTriggerAudio(other);
    }
#endif
#if UNITY_SERVER
    private IEnumerator CheckCatAnTien(Action onSuccess)
    {
        Vector3 startPos = transform.position;
        yield return new WaitForSeconds(1f);
        //kiểm tra sau 1 giây viên bi có di chuyển hay không.
        float moveDistance = Vector3.Distance(transform.position, startPos);
        if (moveDistance > 0.3f)
        {
            onSuccess?.Invoke();
        }
    }
#endif
    private void OnBallHit()
    {
#if !UNITY_SERVER
        if (ShouldSuppressServerHitFeedback(LastHitInfo))
            return;

        if (ShouldSkipRepeatedServerHitFeedback(LastHitInfo))
            return;

        RememberServerHitFeedback(LastHitInfo);
        PlayHitFeedback(LastHitInfo);
#endif
    }

#if !UNITY_SERVER
    private void UpdateLocalCollisionAudioState()
    {
        Vector3 currentPosition = ResolveLocalCollisionAudioPosition();
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        if (_hasLocalCollisionSample)
            _localCollisionSampleVelocity = (currentPosition - _lastLocalCollisionSamplePosition) / deltaTime;
        else
            _localCollisionSampleVelocity = Vector3.zero;

        _lastLocalCollisionSamplePosition = currentPosition;
        _hasLocalCollisionSample = true;

        ProbeLocalBallCollisionAudio(currentPosition);
    }

    private Vector3 ResolveLocalCollisionAudioPosition()
    {
        if (ballModelVisualInstance != null)
            return ballModelVisualInstance.transform.position;

        return transform.position;
    }

    private void ProbeLocalBallCollisionAudio(Vector3 currentPosition)
    {
        if (!ShouldPlayLocalCollisionAudio())
            return;

        if (_localCollisionSampleVelocity.magnitude < LocalCollisionMinAudibleForce)
            return;

        float radius = ResolveLocalCollisionProbeRadius();
        int hitCount = Physics.OverlapSphereNonAlloc(
            currentPosition,
            radius,
            LocalCollisionProbeHits,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = LocalCollisionProbeHits[i];
            if (hit == null || hit.gameObject == gameObject)
                continue;

            var otherBall = hit.GetComponentInParent<BallServerController>();
            if (otherBall == null || otherBall == this || ShouldSkipLocalBallCollisionAudio(otherBall))
                continue;

            if (ShouldSkipActiveLocalCollisionPair(hit.gameObject))
                continue;

            Vector3 otherVelocity = otherBall._localCollisionSampleVelocity;
            float force = Mathf.Max((_localCollisionSampleVelocity - otherVelocity).magnitude, LocalCollisionMinAudibleForce);
            Vector3 point = hit.ClosestPoint(currentPosition);
            PlayLocalPredictedHit(HitSurface.Ball, point, force, hit.gameObject);
        }
    }

    private float ResolveLocalCollisionProbeRadius()
    {
        float radius = _sphereCollider != null
            ? _sphereCollider.radius * MaxAbsScaleLocal(transform.lossyScale)
            : 0.08f;

        return Mathf.Max(0.04f, radius + LocalBallCollisionProbePadding);
    }

    private bool TryPlayLocalCollisionAudio(Collision collision)
    {
        if (collision == null || !ShouldPlayLocalCollisionAudio())
            return false;

        GameObject otherObject = collision.gameObject;
        if (!TryResolveLocalHitSurface(otherObject, out HitSurface surfaceType, out BallServerController otherBall))
            return false;

        if (surfaceType == HitSurface.Ball && ShouldSkipLocalBallCollisionAudio(otherBall))
            return false;

        float force = CalculateLocalImpactForce(collision);
        if (force < LocalCollisionMinAudibleForce)
            return false;

        Vector3 point = collision.contactCount > 0 ? collision.contacts[0].point : ResolveLocalCollisionAudioPosition();
        return PlayLocalPredictedHit(surfaceType, point, force, otherObject);
    }

    private bool TryPlayLocalTriggerAudio(Collider other)
    {
        if (other == null || !ShouldPlayLocalCollisionAudio())
            return false;

        if (!TryResolveLocalHitSurface(other.gameObject, out HitSurface surfaceType, out _))
            return false;

        if (surfaceType != HitSurface.Water && surfaceType != HitSurface.Puddle)
            return false;

        float force = Mathf.Max(_localCollisionSampleVelocity.magnitude, LocalCollisionMinAudibleForce);
        Vector3 point = other.ClosestPoint(ResolveLocalCollisionAudioPosition());
        return PlayLocalPredictedHit(surfaceType, point, force, other.gameObject);
    }

    private bool ShouldPlayLocalCollisionAudio()
    {
        if (IsActive == 0 || IsHolding == 1)
            return false;

        if (NetworkObjectManager.Instance != null && NetworkObjectManager.Instance.IsGameEnded)
            return false;

        return true;
    }

    private bool TryResolveLocalHitSurface(GameObject otherObject, out HitSurface surfaceType, out BallServerController otherBall)
    {
        surfaceType = HitSurface.None;
        otherBall = null;

        if (otherObject == null)
            return false;

        otherBall = otherObject.GetComponentInParent<BallServerController>();
        int ballLayer = LayerMask.NameToLayer("Ball");
        if (otherBall != null ||
            (ballLayer >= 0 && otherObject.layer == ballLayer) ||
            IsBallPlayerTag(GetSafeTag(otherObject)))
        {
            surfaceType = HitSurface.Ball;
            return true;
        }

        if (HasTagInHierarchyLocal(otherObject, "Water"))
            surfaceType = HitSurface.Water;
        else if (SafeCompareTagLocal(otherObject, "Rock"))
            surfaceType = HitSurface.Rock;
        else if (SafeCompareTagLocal(otherObject, "Tree"))
            surfaceType = HitSurface.Tree;
        else if (SafeCompareTagLocal(otherObject, "Grass"))
            surfaceType = HitSurface.Grass;
        else if (SafeCompareTagLocal(otherObject, "Swamp"))
            surfaceType = HitSurface.Swamp;
        else if (SafeCompareTagLocal(otherObject, "Puddle"))
            surfaceType = HitSurface.Puddle;

        return surfaceType != HitSurface.None;
    }

    private bool ShouldSkipLocalBallCollisionAudio(BallServerController otherBall)
    {
        if (otherBall == null)
            return false;

        if (otherBall == this)
            return true;

        if (playerId != 0 && otherBall.playerId == playerId)
            return true;

        return otherBall.IsActive == 0 || otherBall.IsHolding == 1;
    }

    private float CalculateLocalImpactForce(Collision collision)
    {
        float impulseForce = collision.impulse.magnitude;
        if (impulseForce > 0f)
            return impulseForce / Mathf.Max(Time.fixedDeltaTime, 0.0001f);

        float relativeSpeed = collision.relativeVelocity.magnitude;
        float ownSpeed = _localCollisionSampleVelocity.magnitude;
        float otherSpeed = 0f;
        Rigidbody otherBody = collision.rigidbody != null
            ? collision.rigidbody
            : collision.collider != null ? collision.collider.attachedRigidbody : null;
        if (otherBody != null)
            otherSpeed = otherBody.linearVelocity.magnitude;

        return Mathf.Max(Mathf.Max(relativeSpeed, ownSpeed), otherSpeed);
    }

    private bool PlayLocalPredictedHit(HitSurface surfaceType, Vector3 point, float force, GameObject otherObject)
    {
        if (surfaceType == HitSurface.None || ShouldSkipLocalCollisionPair(otherObject))
            return false;

        var info = new HitInfo
        {
            magnitude = Mathf.Max(force, 0.1f),
            point = point,
            surfaceType = surfaceType
        };

        _lastLocalCollisionAudioTime = Time.time;
        _lastLocalCollisionAudioPoint = point;
        _lastLocalCollisionAudioSurface = surfaceType;
        PlayHitFeedback(info);
        return true;
    }

    private bool ShouldSkipLocalCollisionPair(GameObject otherObject)
    {
        if (otherObject == null)
            return false;

        if (LocalCollisionAudioTimeByPair.Count > 512)
            LocalCollisionAudioTimeByPair.Clear();

        ulong key = BuildLocalCollisionPairKey(gameObject, otherObject);
        float now = Time.time;
        if (LocalCollisionAudioTimeByPair.TryGetValue(key, out float lastTime) &&
            now - lastTime < LocalCollisionPairDedupeSeconds)
        {
            return true;
        }

        LocalCollisionAudioTimeByPair[key] = now;
        return false;
    }

    private bool ShouldSkipActiveLocalCollisionPair(GameObject otherObject)
    {
        if (otherObject == null)
            return false;

        if (LocalCollisionActivePairTimeByPair.Count > 512)
            LocalCollisionActivePairTimeByPair.Clear();

        ulong key = BuildLocalCollisionPairKey(gameObject, otherObject);
        float now = Time.time;
        bool isStillTouching = LocalCollisionActivePairTimeByPair.TryGetValue(key, out float lastSeen) &&
            now - lastSeen <= LocalCollisionActivePairMemorySeconds;
        LocalCollisionActivePairTimeByPair[key] = now;
        return isStillTouching;
    }

    private bool ShouldSuppressServerHitFeedback(HitInfo info)
    {
        if (info.surfaceType == HitSurface.None)
            return false;

        if (Time.time - _lastLocalCollisionAudioTime > LocalServerHitSuppressionSeconds)
            return false;

        if (info.surfaceType != _lastLocalCollisionAudioSurface)
            return false;

        if (info.surfaceType == HitSurface.Ball)
            return true;

        return (info.point - _lastLocalCollisionAudioPoint).sqrMagnitude <= LocalServerHitSuppressionDistanceSqr;
    }

    private bool ShouldSkipRepeatedServerHitFeedback(HitInfo info)
    {
        if (info.surfaceType == HitSurface.None)
            return false;

        if (Time.time - _lastServerHitFeedbackTime > ServerHitFeedbackDedupeSeconds)
            return false;

        if (info.surfaceType != _lastServerHitFeedbackSurface)
            return false;

        return (info.point - _lastServerHitFeedbackPoint).sqrMagnitude <= LocalServerHitSuppressionDistanceSqr;
    }

    private void RememberServerHitFeedback(HitInfo info)
    {
        _lastServerHitFeedbackTime = Time.time;
        _lastServerHitFeedbackPoint = info.point;
        _lastServerHitFeedbackSurface = info.surfaceType;
    }

    private void PlayHitFeedback(HitInfo info)
    {
        if (info.surfaceType == HitSurface.None)
            return;

        float force = Mathf.Max(info.magnitude, 0.1f);
        VibrationManager.Instance?.PlayImpact(info.surfaceType, force);

        if (info.surfaceType == HitSurface.Ball || info.surfaceType == HitSurface.Rock)
        {
            PlayHeavyImpactVfx(info.point);
        }

        switch (info.surfaceType)
        {
            case HitSurface.Water:
                PlayWaterSplashVfx(info.point);
                StopCameraFollowOnWaterHit();
                ClientGameplayBridge.Sound.StopShotBallRollingLoop(gameObject);
                ClientGameplayBridge.Sound.PlayBallHitWater(info.point, force);
                break;
            case HitSurface.Puddle:
                PlayWaterSplashVfx(info.point);
                ClientGameplayBridge.Sound.PlayBallHitPuddle(info.point, force);
                break;
            case HitSurface.Grass:
                ClientGameplayBridge.Sound.PlayBallHitGrass(info.point, force);
                break;
            case HitSurface.Swamp:
                ClientGameplayBridge.Sound.PlayBallHitSwamp(info.point, force);
                break;
            case HitSurface.Rock:
                ClientGameplayBridge.Sound.PlayBallHitRock(info.point, force);
                break;
            case HitSurface.Tree:
                ClientGameplayBridge.Sound.PlayBallHitTree(info.point, force);
                break;
            default:
                ClientGameplayBridge.Sound.PlayBallHit(force, info.point);
                break;
        }
    }

    private static bool HasTagInHierarchyLocal(GameObject obj, string tag)
    {
        if (obj == null || string.IsNullOrEmpty(tag))
            return false;

        if (SafeCompareTagLocal(obj, tag))
            return true;

        Transform current = obj.transform.parent;
        while (current != null)
        {
            if (SafeCompareTagLocal(current.gameObject, tag))
                return true;
            current = current.parent;
        }

        return false;
    }

    private static bool SafeCompareTagLocal(GameObject obj, string tag)
    {
        if (obj == null)
            return false;

        try
        {
            return obj.CompareTag(tag);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private static string GetSafeTag(GameObject obj)
    {
        if (obj == null)
            return string.Empty;

        try
        {
            return obj.tag;
        }
        catch (UnityException)
        {
            return string.Empty;
        }
    }

    private static ulong BuildLocalCollisionPairKey(GameObject first, GameObject second)
    {
        uint firstId = unchecked((uint)GetLocalCollisionDedupeId(first));
        uint secondId = unchecked((uint)GetLocalCollisionDedupeId(second));
        if (firstId > secondId)
        {
            uint temp = firstId;
            firstId = secondId;
            secondId = temp;
        }

        return ((ulong)firstId << 32) | secondId;
    }

    private static int GetLocalCollisionDedupeId(GameObject obj)
    {
        if (obj == null)
            return 0;

        Rigidbody body = obj.GetComponentInParent<Rigidbody>();
        if (body != null)
            return body.GetInstanceID();

        BallServerController ball = obj.GetComponentInParent<BallServerController>();
        if (ball != null)
            return ball.GetInstanceID();

        return obj.transform.root != null ? obj.transform.root.gameObject.GetInstanceID() : obj.GetInstanceID();
    }

    private static float MaxAbsScaleLocal(Vector3 scale)
    {
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }
#endif

    private void OnDamageChanged()
    {
        UpdateDamageVisual();
    }

    private void UpdateDamageVisual()
    {
        if (initialImpactResistance <= 0f)
            return;

        float pct = CurrentImpactResistance / initialImpactResistance;
        var newStage = EvaluateDamageStage(pct);
        RefreshDamagedPhysicsMaterial();

#if UNITY_SERVER
        RefreshDamageCollider(newStage);
#else
        RefreshDamageVisual(newStage);
        if (physicsHelper != null && meshDeformer != null)
            physicsHelper.UpdateDamageVisual(pct, DamagePoint, meshDeformer, rb, initialAngularDrag, initialLinearDamping);
#endif

        _currentDamageStage = newStage;
    }

    private BallDamageStage EvaluateDamageStage(float pct)
    {
        if (pct <= shatteredThreshold)
            return BallDamageStage.Shattered;
        if (pct <= crackedThreshold)
            return BallDamageStage.Cracked;
        if (pct <= chippedThreshold)
            return BallDamageStage.Chipped;

        return BallDamageStage.Pristine;
    }

    public bool IsDamageStageActive()
    {
        if (initialImpactResistance <= 0f)
            return false;

        float pct = CurrentImpactResistance / initialImpactResistance;
        return EvaluateDamageStage(pct) != BallDamageStage.Pristine;
    }

    private void RefreshDamagedPhysicsMaterial()
    {
        if (physicsHelper == null)
            return;

        var colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col == null || col.isTrigger)
                continue;

            physicsHelper.RefreshDamagedPhysicsMaterial(col, initialImpactResistance, CurrentImpactResistance);
        }
    }

#if UNITY_SERVER
    private GameObject GetColliderPrefabForStage(BallDamageStage stage)
    {
        switch (stage)
        {
            case BallDamageStage.Chipped:
                return chippedColliderPrefab;
            case BallDamageStage.Cracked:
                return crackedColliderPrefab;
            case BallDamageStage.Shattered:
                return shatteredColliderPrefab;
            default:
                return null;
        }
    }

    private void RefreshDamageCollider(BallDamageStage stage)
    {
        if (_sphereCollider == null)
            _sphereCollider = GetComponent<SphereCollider>();

        if (stage == BallDamageStage.Pristine)
        {
            if (_spawnedDamageCollider != null)
            {
                Destroy(_spawnedDamageCollider);
                _spawnedDamageCollider = null;
                _currentColliderPrefab = null;
            }

            if (_sphereCollider != null)
                _sphereCollider.enabled = true;
            return;
        }

        var prefab = GetColliderPrefabForStage(stage);

        if (prefab == null)
        {
            if (_sphereCollider != null)
                _sphereCollider.enabled = true;
            return;
        }

        if (_currentColliderPrefab == prefab && _spawnedDamageCollider != null)
        {
            if (_sphereCollider != null)
                _sphereCollider.enabled = false;
            return;
        }

        if (_spawnedDamageCollider != null)
            Destroy(_spawnedDamageCollider);

        _spawnedDamageCollider = Instantiate(prefab, transform);
        _spawnedDamageCollider.transform.localPosition = Vector3.zero;
        _spawnedDamageCollider.transform.localRotation = Quaternion.identity;
        _spawnedDamageCollider.transform.localScale = Vector3.one;
        _currentColliderPrefab = prefab;

        if (_sphereCollider != null)
            _sphereCollider.enabled = false;
    }
#else
    private Mesh GetVisualMeshForStage(BallDamageStage stage)
    {
        if (GameInitializer.Instance == null)
            return null;

        switch (stage)
        {
            case BallDamageStage.Chipped:
                return GameInitializer.Instance.BallModelVisualChipped;
            case BallDamageStage.Cracked:
                return GameInitializer.Instance.BallModelVisualCracked;
            case BallDamageStage.Shattered:
                return GameInitializer.Instance.BallModelVisualShattered;
            default:
                return null;
        }
    }

    private void RefreshDamageVisual(BallDamageStage stage)
    {
        EnsureBallVisualAnchor();
        EnsureBallModelVisualInstance();

        if (ballModelVisualInstance == null)
            return;

        var meshFilter = ballModelVisualInstance.GetComponentInChildren<MeshFilter>();
        if (meshFilter == null)
            return;

        if (_baseVisualMesh == null)
            _baseVisualMesh = meshFilter.sharedMesh;

        var desiredMesh = GetVisualMeshForStage(stage) ?? _baseVisualMesh;
        if (_currentVisualMesh == desiredMesh)
        {
            CaptureBaseVisualScaleIfNeeded();
            ballModelVisualInstance.transform.localScale = _baseVisualScale * _currentScaleMultiplier;
            return;
        }

        meshFilter.sharedMesh = desiredMesh;
        _currentVisualMesh = desiredMesh;

        CaptureBaseVisualScaleIfNeeded();
        ballModelVisualInstance.transform.localScale = _baseVisualScale * _currentScaleMultiplier;
        _hasRenderSample = false;
        _smoothedRenderVelocity = Vector3.zero;
        _hasCenteredVisual = false;
    }
#endif

    private void OnShotParamsChanged()
    {
#if UNITY_SERVER
        if (HasStateAuthority)
            if (ShotData.force > 0f)
            {
                NotifyShotStartedServer();
                hasBeenShoot = 1;
                IsHolding = 0;
                nearStopBrakeTime = 0f;
                BeginGroundPinSkillShotIfArmed();
                physicsHelper?.ResetDamagedRollingState();
                EnsureRigidbody();
                if (rb != null)
                    rb.isKinematic = false;
                Debug.Log("Giải phóng viên bi");
                StartCoroutine(GameSessionNetWork_Host.Instance.ShootBall(
                    playerId,
                    rb,
                    ShotData.direction,
                    ShotData.force,
                    ShotData.spin,
                    ShotData.shootAngle));
                physicsHelper?.ApplyDamagedShotStartImpulse(rb, ShotData.direction, ShotData.force, ShotData.shootAngle, initialImpactResistance, CurrentImpactResistance);

            }
#endif
    }

    // Kiểm tra khoảng cách giữa viên bi của mình và các viên bi khác
#if UNITY_SERVER
    private IEnumerator MonitorHeartbeatProximity()
    {
        yield break;
    }
#else
    private static readonly WaitForSeconds HeartbeatPollDelay = new WaitForSeconds(0.2f);
    private bool heartbeatPlaying;

    private IEnumerator MonitorHeartbeatProximity()
    {
        while (true)
        {
            if (!IsLocalPlayerBall() || !CanPlayHeartbeatInCurrentTurn())
            {
                if (heartbeatPlaying)
                {
                    ClientGameplayBridge.Sound.StopHeartbeatLoop();
                    heartbeatPlaying = false;
                }
                yield return HeartbeatPollDelay;
                continue;
            }

            bool shouldPlay = IsNearOtherBall(heartbeatDistance);
            if (shouldPlay && !heartbeatPlaying)
            {
                ClientGameplayBridge.Sound.StartHeartbeatLoop();
                heartbeatPlaying = true;
            }
            else if (!shouldPlay && heartbeatPlaying)
            {
                ClientGameplayBridge.Sound.StopHeartbeatLoop();
                heartbeatPlaying = false;
            }

            yield return HeartbeatPollDelay;
        }
    }

    private bool IsLocalPlayerBall()
    {
        if (Object == null)
            return false;

        var manager = GameManagerNetWork.Instance;
        if (manager == null || manager.loginUserModel == null)
            return false;

        return manager.loginUserModel.UserId == playerId;
    }

    private bool CanPlayHeartbeatInCurrentTurn()
    {
        var localSession = GameSessionClientLocal.Instance;
        return localSession != null && localSession.IsNormalPlayerTurnActive;
    }

    private bool IsNearOtherBall(float distance)
    {
        var balls = GameObject.FindGameObjectsWithTag("BallPlayer");
        if (balls == null || balls.Length == 0)
            return false;

        Vector3 currentPosition = transform.position;
        for (int i = 0; i < balls.Length; i++)
        {
            var ball = balls[i];
            if (ball == null || ball == gameObject)
                continue;

            if (Vector3.Distance(currentPosition, ball.transform.position) <= distance)
                return true;
        }

        return false;
    }
#endif

    // Hàm giám sát hiệu ứng chuyển động chậm (Slow Motion) khi có va chạm với đối thủ
    private IEnumerator MonitorSlowMotion()
    {
        if (slowMotionLoopYield == null)
            slowMotionLoopYield = new WaitForSecondsRealtime(Mathf.Max(0f, slowMotionLoopDelay));

        while (hasBeenShoot == 1)
        {
            // Chặn hiệu ứng slow motion nếu flag bị tắt
            if (!HasStateAuthority || !ShouldRunSlowMotionMonitor() || !EnableSlowMotion)
            {
                StopSlowMotionPrediction();
                slowMotionLoopYield.waitTime = Mathf.Max(0f, slowMotionLoopDelay);
                yield return slowMotionLoopYield;
                continue;
            }

            EnsureRigidbody();
            Vector3 velocity = rb.linearVelocity;
            float speed = velocity.magnitude;

            if (speed < slowMotionMinSpeed)
            {
                TryStopSlowMotionPrediction();
                slowMotionLoopYield.waitTime = Mathf.Max(0f, slowMotionLoopDelay);
                yield return slowMotionLoopYield;
                continue;
            }

            if (TryPredictVictims(transform.position, velocity, out var victimIds, out var predictedPoint, out var timeToHit))
            {
                slowMoLastPredictionTime = Time.time;
                bool victimsChanged = !AreVictimsMatching(victimIds);

                if (!slowMoPredictionActive || victimsChanged)
                {
                    if (!slowMoPredictionActive)
                        slowMoTriggerId++;
                    slowMoPredictionActive = true;
                    slowMoPredictedVictims.Clear();
                    for (int i = 0; i < victimIds.Count; i++)
                        slowMoPredictedVictims.Add(victimIds[i]);

                    RPC_BeginKillCamSlowMotion(slowMoTriggerId, victimIds.ToArray(), predictedPoint, timeToHit);
                }
            }
            else
            {
                TryStopSlowMotionPrediction();
            }

            slowMotionLoopYield.waitTime = Mathf.Max(0f, slowMotionLoopDelay);
            yield return slowMotionLoopYield;
        }

        StopSlowMotionPrediction();
        slowMotionCoroutine = null;
    }

    public void OnShootBall()
    {
        if (hasBeenShoot == 1)
        {
            Debug.Log($"{playerId} Đã bắn");
            ClientGameplayBridge.Sound.PlayShoot();
#if !UNITY_SERVER
            StartShotGroundAudioMonitor();
#endif
            var script = NetworkObjectManager.Instance?.GetPlayerObject(playerId)?.GetComponent<PlayerNetworkHandler>();
            if (script == null)
                return;
            int IsExam = script.PlayerModel.statusPlayer == StatusPlayer.ShootExam ? 1 : 0;
            ClientGameplayBridge.Camera.OnBallShot(transform, playerId, HasStateAuthority, IsExam);
            ClientGameplayBridge.Skill.ShowSkillList();

            // Chặn hiệu ứng slow motion nếu flag bị tắt
            if (HasStateAuthority && IsExam == 0 && slowMotionCoroutine == null && EnableSlowMotion)
            {
                slowMotionCoroutine = StartCoroutine(MonitorSlowMotion());
            }

        }
        else
        {
            if (slowMotionCoroutine != null)
            {
                StopCoroutine(slowMotionCoroutine);
                slowMotionCoroutine = null;
            }
            if (HasStateAuthority)
                StopSlowMotionPrediction();
#if !UNITY_SERVER
            StopShotGroundAudioMonitor();
#endif
            ClientGameplayBridge.Camera.EndKillCamSlowMotion(playerId);
            ClientGameplayBridge.Camera.OnBallShotReset(playerId);
        }

    }

#if !UNITY_SERVER
    private void StartShotGroundAudioMonitor()
    {
        StopShotGroundAudioMonitor();
        shotGroundAudioSpeed = 0f;
        shotGroundAudioRoutine = StartCoroutine(ShotGroundAudioMonitorRoutine());
    }

    private void StopShotGroundAudioMonitor()
    {
        if (shotGroundAudioRoutine != null)
        {
            StopCoroutine(shotGroundAudioRoutine);
            shotGroundAudioRoutine = null;
        }

        ClientGameplayBridge.Sound.StopShotBallRollingLoop(gameObject);
        shotGroundAudioSpeed = 0f;
    }

    private IEnumerator ShotGroundAudioMonitorRoutine()
    {
        bool impactPlayed = false;
        Vector3 previousPosition = transform.position;
        float previousTime = Time.time;

        while (CanAccessNetworkedState && hasBeenShoot == 1 && IsActive != 0)
        {
            EnsureRigidbody();
            Vector3 currentPosition = transform.position;
            float currentTime = Time.time;
            float deltaTime = Mathf.Max(0.0001f, currentTime - previousTime);
            float renderSpeed = Vector3.Distance(currentPosition, previousPosition) / deltaTime;
            float rigidbodySpeed = rb != null ? rb.linearVelocity.magnitude : 0f;
            shotGroundAudioSpeed = Mathf.Max(renderSpeed, rigidbodySpeed);

            if (!impactPlayed && TryGetShotGroundContact(currentPosition, out RaycastHit groundHit))
            {
                float intensity = Mathf.Clamp01(Mathf.Max(shotGroundAudioSpeed, 0.2f) / 2.5f);
                ClientGameplayBridge.Sound.PlayShotBallGroundImpact(groundHit.point, intensity);
                ClientGameplayBridge.Sound.StartShotBallRollingLoop(gameObject, GetShotGroundAudioSpeed);
                impactPlayed = true;
            }

            previousPosition = currentPosition;
            previousTime = currentTime;
            yield return null;
        }

        ClientGameplayBridge.Sound.StopShotBallRollingLoop(gameObject);
        shotGroundAudioRoutine = null;
        shotGroundAudioSpeed = 0f;
    }

    private float GetShotGroundAudioSpeed()
    {
        return shotGroundAudioSpeed;
    }

    private bool TryGetShotGroundContact(Vector3 ballPosition, out RaycastHit groundHit)
    {
        groundHit = default;
        float radius = ResolveShotGroundAudioRadius();
        float probeOffset = Mathf.Max(ShotGroundProbeOffset, radius + 0.05f);
        float probeDistance = probeOffset + radius + ShotGroundContactTolerance;
        Vector3 origin = ballPosition + Vector3.up * probeOffset;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            probeDistance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || !HasClientTagInHierarchy(hit.collider.gameObject, "Ground"))
            {
                continue;
            }

            float centerToGround = ballPosition.y - hit.point.y;
            if (centerToGround > radius + ShotGroundContactTolerance || centerToGround < -ShotGroundContactTolerance)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundHit = hit;
            }
        }

        return closestDistance < float.MaxValue;
    }

    private float ResolveShotGroundAudioRadius()
    {
        if (_sphereCollider != null)
        {
            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return Mathf.Max(0.01f, _sphereCollider.radius * maxScale);
        }

        Collider ballCollider = GetComponent<Collider>();
        return ballCollider != null
            ? Mathf.Max(0.01f, Mathf.Min(ballCollider.bounds.extents.x, ballCollider.bounds.extents.z))
            : 0.1f;
    }

    private static bool HasClientTagInHierarchy(GameObject target, string tagName)
    {
        Transform current = target != null ? target.transform : null;
        while (current != null)
        {
            if (current.CompareTag(tagName))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
#endif

    #region [=============== RPC ========================]

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StartBallRollingLoop()
    {
        //SoundManager.Instance.StartBallRollingLoop(gameObject, () => rb.velocity.magnitude);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayBallPlayerHitAnnouncement(float force)
    {
#if !UNITY_SERVER
        Debug.Log($"[CLIENT][BallHitUI] RPC_PlayBallPlayerHitAnnouncement received. ball={name}, force={force:0.###}");

        if (UIControllerOnline.Instance == null)
        {
            Debug.LogWarning($"[CLIENT][BallHitUI] Không thể hiện thông báo trúng bi vì UIControllerOnline.Instance=null. ball={name}");
            return;
        }

        UIControllerOnline.Instance.PlayBallPlayerHitAnnouncement(Mathf.Max(force, 0.1f));
#endif
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_BeginKillCamSlowMotion(int triggerId, int[] victimIds, Vector3 predictedPoint, float predictedTimeToHit)
    {
#if !UNITY_SERVER
        if (victimIds == null || victimIds.Length == 0)
            return;

        int localPlayerId = GetLocalPlayerId();
        if (localPlayerId <= 0)
            return;

        bool isShooterLocal = localPlayerId == playerId;
        bool isVictimLocal = victimIds.Contains(localPlayerId);
        if (!isShooterLocal && !isVictimLocal)
            return;

        activeSlowMoTriggerId = triggerId;
        activeSlowMoVictims.Clear();
        for (int i = 0; i < victimIds.Length; i++)
            activeSlowMoVictims.Add(victimIds[i]);

        Transform shooterTarget = ResolveBallTarget(playerId);
        if (isShooterLocal)
        {
            Transform focusTarget = ResolveClosestVictimTarget(victimIds, shooterTarget);
            ClientGameplayBridge.Camera.PlayKillCamSlowMotionShooter(playerId, shooterTarget, focusTarget, predictedPoint, predictedTimeToHit);
            ClientGameplayBridge.Sound.PlayKillCamShooterPredicted();
        }

        if (isVictimLocal)
        {
            Transform victimTarget = ResolveBallTarget(localPlayerId);
            ClientGameplayBridge.Camera.PlayKillCamSlowMotionVictim(playerId, shooterTarget, victimTarget, predictedPoint, predictedTimeToHit);
        }
#endif
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ConfirmKillCamHit(int triggerId, int victimId, Vector3 hitPoint)
    {
#if !UNITY_SERVER
        if (activeSlowMoTriggerId != 0 && triggerId < activeSlowMoTriggerId)
            return;

        int localPlayerId = GetLocalPlayerId();
        if (localPlayerId <= 0)
            return;

        bool isShooterLocal = localPlayerId == playerId;
        bool isVictimLocal = localPlayerId == victimId;
        if (!isShooterLocal && !isVictimLocal)
            return;

        activeSlowMoTriggerId = triggerId;
        activeSlowMoVictims.Add(victimId);

        Transform shooterTarget = ResolveBallTarget(playerId);
        Transform victimTarget = ResolveBallTarget(victimId);
        ClientGameplayBridge.Camera.ConfirmKillCamHit(playerId, victimId, shooterTarget, victimTarget, hitPoint);

        if (isShooterLocal)
            ClientGameplayBridge.Sound.PlayKillCamShooterHit();

        if (isVictimLocal)
            ClientGameplayBridge.Sound.PlayKillCamVictimHit();
#endif
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_EndKillCamSlowMotion(int triggerId)
    {
#if !UNITY_SERVER
        if (activeSlowMoTriggerId != 0 && triggerId < activeSlowMoTriggerId)
            return;

        activeSlowMoTriggerId = 0;
        activeSlowMoVictims.Clear();
        ClientGameplayBridge.Camera.EndKillCamSlowMotion(playerId);
#endif
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcApplyPhysics(float mass, float gravityScale, float drag, float bounciness, float elasticity, float impactResistance)
    {
        ApplyPhysicsValues(mass, gravityScale, drag, bounciness, elasticity, impactResistance);
    }

    public void ApplyPhysicsLocally(float mass, float gravityScale, float drag, float bounciness, float elasticity, float impactResistance)
    {
        ApplyPhysicsValues(mass, gravityScale, drag, bounciness, elasticity, impactResistance);
    }

    public void ApplyPhysicsLocally(BallPhysicsStruct data)
    {
        ApplyPhysicsValues(data.Mass, data.GravityScale, data.Drag, data.Bounciness, data.Elasticity, data.ImpactResistance);
    }

    private void ApplyPhysicsValues(float mass, float gravityScale, float drag, float bounciness, float elasticity, float impactResistance)
    {
        if (rb == null)
            rb = GetComponent<NetworkRigidbody3D>().Rigidbody;

        rb.mass = mass;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxAngularVelocity = Mathf.Max(rb.maxAngularVelocity, 50f);

        // Increase drag on rainy weather to simulate the ground becoming more
        // slippery and slowing the ball quicker.
        WeatherType weather = WeatherType.Sunny;
        if (NetworkObjectManager.Instance != null)
            weather = NetworkObjectManager.Instance.rpgRoomModel.weatherType;
        rb.linearDamping = weather == WeatherType.Rainy ? drag + 0.5f : drag;

        initialAngularDrag = rb.angularDamping;
        initialLinearDamping = rb.linearDamping;

        var col = GetComponent<Collider>();
        if (col != null)
        {
            if (col.material == null)
                col.material = new PhysicsMaterial();

            col.material.bounciness = bounciness;
            col.material.dynamicFriction = elasticity;
            col.material.staticFriction = impactResistance;
            physicsHelper?.CaptureBasePhysicsMaterial(col, bounciness, elasticity, impactResistance);
        }
        initialImpactResistance = impactResistance;
        CurrentImpactResistance = impactResistance;
        RefreshDamagedPhysicsMaterial();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcSetActive(int active)
    {
        IsActive = active;
        OnActiveChanged();
        bool enable = active == 1;
        var rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = enable;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = enable;
        if (rb == null)
            rb = GetComponent<NetworkRigidbody3D>().Rigidbody;
        rb.isKinematic = !enable;
        Transform cateye = transform.Find("Cateye");
        if (cateye != null)
        {
            var cateyeRenderer = cateye.GetComponent<Renderer>();
            if (cateyeRenderer != null) cateyeRenderer.enabled = enable;
        }
        if (vipVFXInstance != null)
            vipVFXInstance.SetActive(enable);
    }

    public void ApplyImpactDamage(float dmg, Vector3 point)
    {
        lastDamagePoint = point;
        DamagePoint = point;
        CurrentImpactResistance = Mathf.Max(CurrentImpactResistance - dmg, 0f);
        UpdateDamageVisual();
#if UNITY_SERVER
        if (HasStateAuthority && dmg > 0f && BallMaterialId > 0)
        {
            GameSessionNetWork_Host.Instance?.RegisterBallDamage(playerId, BallMaterialId, BallItemSeq, dmg);
        }
#endif
    }

#if UNITY_SERVER
    private float ProcessImpactDamage(PendingCollisionInfo info)
    {
        var otherObject = info.other;
        if (otherObject == null)
            return 0f;

        float impactSpeed = info.impactSpeed > 0f ? info.impactSpeed : info.impactForce;
        Vector3 contactPoint = info.hasContact ? info.contactPoint : transform.position;
        float severityMultiplier = 1f;
        float angleFactor = 1f;

        if (otherObject.CompareTag("Rock"))
        {
            severityMultiplier = 1f;
        }
        else if (otherObject.layer == LayerMask.NameToLayer("Ball") ||
                 otherObject.CompareTag("BallPlayer") ||
                 otherObject.tag.StartsWith("BallPlayer"))
        {
            severityMultiplier = 0.2f;
        }

        EnsureRigidbody();

        if (info.contactNormal.sqrMagnitude > 0f && info.relativeVelocity.sqrMagnitude > 0f)
        {
            angleFactor = Mathf.Abs(Vector3.Dot(info.relativeVelocity.normalized, info.contactNormal.normalized));
        }

        float selfDamage = CalculateImpactDamage(impactSpeed, severityMultiplier, angleFactor);
        if (selfDamage <= 0f)
            return 0f;
        ApplyImpactDamage(selfDamage, contactPoint);
        return selfDamage;
    }
#endif

#if !UNITY_SERVER
    private void PlayHeavyImpactVfx(Vector3 position)
    {
        var heavyImpactVfxPrefab = GameInitializer.Instance != null
            ? GameInitializer.Instance.HeavyImpactVfxPrefab
            : null;
        if (heavyImpactVfxPrefab == null)
            return;

        var vfxInstance = Instantiate(heavyImpactVfxPrefab, position, Quaternion.identity);
        var particleSystem = vfxInstance.GetComponent<ParticleSystem>();
        if (particleSystem != null)
        {
            var lifetime = particleSystem.main.duration + particleSystem.main.startLifetime.constantMax;
            Destroy(vfxInstance, Mathf.Max(lifetime, 0.1f));
        }
        else
        {
            Destroy(vfxInstance, 2f);
        }
    }

    private void PlayWaterSplashVfx(Vector3 position)
    {
        ClientGameplayBridge.Vfx.PlayWaterSplash(position, playerId);
    }

    private void StopCameraFollowOnWaterHit()
    {
        if (cameraFollowBall != null)
        {
            cameraFollowBall.DOKill();
        }

        cameraFollowBall = null;
        ClientGameplayBridge.Camera.StopFollowingBall();
    }
#endif

    private static bool IsBallPlayerTag(string tagValue)
    {
        if (string.IsNullOrEmpty(tagValue))
            return false;

        return tagValue == "BallPlayer" || tagValue.StartsWith("BallPlayer", StringComparison.Ordinal);
    }

    private bool ShouldRunSlowMotionMonitor()
    {
        if (!HasStateAuthority || hasBeenShoot != 1)
            return false;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return false;

        var playerObject = manager.GetPlayerObject(playerId);
        if (playerObject == null)
            return false;

        var handler = playerObject.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
            return false;

        var model = handler.PlayerModel;
        if (model.statusPlayer == StatusPlayer.ShootExam || model.statusPlayer == StatusPlayer.MoveStartPoint)
            return false;

        if (!manager.IsYourTurn(playerId) || model.score <= 0)
            return false;

        return true;
    }

    private bool TryPredictVictims(Vector3 position, Vector3 velocity, out List<int> victimIds, out Vector3 predictedPoint, out float timeToHit)
    {
        victimIds = null;
        predictedPoint = position;
        timeToHit = 0f;

        float speed = velocity.magnitude;
        if (speed < slowMotionMinSpeed)
            return false;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return false;

        Vector3 direction = velocity / speed;
        float predictionLeadTime = Mathf.Max(slowMotionPredictionTime, slowMotionMinLeadTime);
        float predictionDistance = Mathf.Clamp(speed * predictionLeadTime, slowMotionDetectionRange, slowMotionMaxPredictionDistance);
        var hits = Physics.SphereCastAll(position, slowMotionRadius, direction, predictionDistance, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        float bestDistance = float.MaxValue;
        Vector3 bestPoint = position;
        var predictedVictims = new List<int>(slowMotionVictimLimit);
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            var collider = hit.collider;
            if (collider == null || collider.gameObject == gameObject)
                continue;

            if (!IsBallPlayerTag(collider.tag))
                continue;

            var controller = collider.GetComponent<BallServerController>();
            if (controller == null || controller.playerId == playerId)
                continue;

            int victimId = controller.playerId;
            if (predictedVictims.Contains(victimId))
                continue;

            var targetPlayerObject = manager.GetPlayerObject(victimId);
            if (targetPlayerObject == null)
                continue;

            var targetHandler = targetPlayerObject.GetComponent<PlayerNetworkHandler>();
            if (targetHandler == null)
                continue;

            var targetStatus = targetHandler.PlayerModel.statusPlayer;
            if (targetStatus == StatusPlayer.WaitingDestroy || targetStatus == StatusPlayer.Destroy)
                continue;

            float hitTime = hit.distance / speed;
            if (hitTime > predictionLeadTime)
                continue;

            predictedVictims.Add(victimId);
            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestPoint = hit.point;
                timeToHit = hitTime;
            }

            if (predictedVictims.Count >= slowMotionVictimLimit)
                break;
        }

        if (predictedVictims.Count == 0)
            return false;

        victimIds = predictedVictims;
        predictedPoint = bestPoint;
        return true;
    }

    private bool AreVictimsMatching(List<int> victimIds)
    {
        if (!slowMoPredictionActive || victimIds == null)
            return false;

        if (victimIds.Count != slowMoPredictedVictims.Count)
            return false;

        for (int i = 0; i < victimIds.Count; i++)
        {
            if (!slowMoPredictedVictims.Contains(victimIds[i]))
                return false;
        }

        return true;
    }

    private void TryStopSlowMotionPrediction()
    {
        if (!slowMoPredictionActive)
            return;

        float timeSincePrediction = Time.time - slowMoLastPredictionTime;
        float timeSinceConfirmedHit = Time.time - slowMoLastConfirmedHitTime;
        float requiredHoldAfterHit = slowMotionPostHitHoldTime;
        if (slowMoLastPredictionTime > 0f)
        {
            float timeSincePredictionStart = Time.time - slowMoLastPredictionTime;
            requiredHoldAfterHit = Mathf.Max(requiredHoldAfterHit, slowMotionMinCinematicDuration - timeSincePredictionStart);
        }

        bool shouldHoldAfterHit = timeSinceConfirmedHit <= requiredHoldAfterHit;

        if (timeSincePrediction <= slowMotionPredictionGraceTime || shouldHoldAfterHit)
            return;

        StopSlowMotionPrediction();
    }

    private void StopSlowMotionPrediction()
    {
        if (slowMoPredictionActive && HasStateAuthority)
        {
            RPC_EndKillCamSlowMotion(slowMoTriggerId);
        }

        slowMoPredictionActive = false;
        slowMoPredictedVictims.Clear();
        slowMoLastPredictionTime = 0f;
        slowMoLastConfirmedHitTime = -999f;
    }

    private void RegisterSlowMotionHit(int victimId, Vector3 hitPoint)
    {
        // Chặn hiệu ứng slow motion nếu flag bị tắt
        if (!HasStateAuthority || victimId <= 0 || !EnableSlowMotion)
            return;

        slowMoLastConfirmedHitTime = Time.time;

        if (!slowMoPredictionActive)
        {
            slowMoTriggerId++;
            slowMoPredictionActive = true;
            slowMoPredictedVictims.Clear();
            slowMoPredictedVictims.Add(victimId);
            slowMoLastPredictionTime = Time.time;
            RPC_BeginKillCamSlowMotion(slowMoTriggerId, new[] { victimId }, hitPoint, 0f);
        }
        else if (!slowMoPredictedVictims.Contains(victimId))
        {
            slowMoPredictedVictims.Add(victimId);
            slowMoLastPredictionTime = Time.time;
            RPC_BeginKillCamSlowMotion(slowMoTriggerId, slowMoPredictedVictims.ToArray(), hitPoint, 0f);
        }

        RPC_ConfirmKillCamHit(slowMoTriggerId, victimId, hitPoint);
    }

#if !UNITY_SERVER
    private int GetLocalPlayerId()
    {
        return GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
    }

    private Transform ResolveBallTarget(int targetPlayerId)
    {
        if (targetPlayerId <= 0)
            return null;

        var ballObject = NetworkObjectManager.Instance?.GetActiveBallObject(targetPlayerId);
        if (ballObject == null)
            return null;

        var ballController = ballObject.GetComponent<BallServerController>();
        if (ballController != null)
            return ballController.GetCameraFocusTarget();

        var rbNet = ballObject.GetComponent<NetworkRigidbody3D>();
        if (rbNet != null && rbNet.InterpolationTarget != null)
            return rbNet.InterpolationTarget;

        return ballObject.transform;
    }

    private Transform ResolveClosestVictimTarget(int[] victimIds, Transform shooterTarget)
    {
        if (victimIds == null || victimIds.Length == 0)
            return null;

        Vector3 origin = shooterTarget != null ? shooterTarget.position : transform.position;
        float bestDistance = float.MaxValue;
        Transform bestTarget = null;

        for (int i = 0; i < victimIds.Length; i++)
        {
            var victimTarget = ResolveBallTarget(victimIds[i]);
            if (victimTarget == null)
                continue;

            float distance = Vector3.SqrMagnitude(victimTarget.position - origin);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = victimTarget;
            }
        }

        return bestTarget;
    }
#endif

    private float CalculateImpactDamage(float speed, float severityMultiplier, float angleFactor)
    {
        float safeSpeed = Mathf.Max(speed, 0f);
        float damage = baseImpactDamage * safeSpeed * safeSpeed * Mathf.Clamp01(angleFactor);
        return damage * severityMultiplier;
    }

    private void EnsureRigidbody()
    {
        if (rb == null)
            rb = GetComponent<NetworkRigidbody3D>()?.Rigidbody;
    }

    public float GetDamageAmount()
    {
        if (initialImpactResistance <= 0f)
            return 0f;

        return Mathf.Max(initialImpactResistance - CurrentImpactResistance, 0f);
    }

    public void ApplyInitialDamage(float damage)
    {
        if (initialImpactResistance <= 0f)
            return;

        float clampedDamage = Mathf.Clamp(damage, 0f, initialImpactResistance);
        CurrentImpactResistance = Mathf.Max(initialImpactResistance - clampedDamage, 0f);
        DamagePoint = transform.position;
        UpdateDamageVisual();
    }

#if UNITY_SERVER
    private bool ShouldApplyImpactDamage(GameObject otherObject, float impactForce)
    {
        if (otherObject == null || impactForce < damageImpactThreshold)
            return false;

        if (IsActive == 0 || IsHolding == 1 || hasBeenShoot != 1)
            return false;

        if (otherObject.CompareTag("Rock"))
            return true;

        return otherObject.layer == LayerMask.NameToLayer("Ball");
    }
#endif
  
    [Rpc(RpcSources.All, RpcTargets.InputAuthority)]
    public void RPC_DestroyBall()
    {
#if !UNITY_SERVER
        GetComponent<Renderer>().enabled = false;
        ClearNameArrow();
#endif
    }
#endregion

    private void OnDestroy()
    {
        var manager = NetworkObjectManager.Instance;
        int ownerPlayerId = cachedPlayerId;
        if (manager != null && manager.HasStateAuthority && didRegisterBall && ownerPlayerId != 0)
        {
            var obj = _networkObject != null ? _networkObject : GetComponent<NetworkObject>();
            if (obj != null)
                manager.RemovePlayerBall(ownerPlayerId, obj);
        }

#if UNITY_SERVER
        ClearGroundPinSkillState();
#endif
#if !UNITY_SERVER
        CleanupScaleSkillEffect(true);
        _groundPinSkillVisualSequence?.Kill();
        _groundPinSkillVisualSequence = null;
        StopShotGroundAudioMonitor();
        if (heartbeatCoroutine != null)
            StopCoroutine(heartbeatCoroutine);
        if (rb != null)
            Debug.Log($"[BallDestroyCleanup] pid={ownerPlayerId} kin={rb.isKinematic} vel={rb.linearVelocity}");
        if (ownerPlayerId != 0 && GameManagerNetWork.Instance != null && GameManagerNetWork.Instance.loginUserModel.UserId == ownerPlayerId)
            ClientGameplayBridge.Sound.StopHeartbeatLoop();
        if (vipVFXInstance != null)
            Destroy(vipVFXInstance);
        if (mainMaterialHandle.IsValid())
            Addressables.Release(mainMaterialHandle);
        if (cateyeMaterialHandle.IsValid())
            Addressables.Release(cateyeMaterialHandle);
        if (vfxHandle.IsValid())
            Addressables.Release(vfxHandle);
        ClearNameArrow();
#endif
#if UNITY_SERVER
        if (_spawnedDamageCollider != null)
            Destroy(_spawnedDamageCollider);
#endif

    }
}
