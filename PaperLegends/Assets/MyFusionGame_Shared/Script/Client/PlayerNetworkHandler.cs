using UnityEngine;
using Fusion;
#if !UNITY_SERVER
using DG.Tweening;
#endif
using System.Collections;
using WebSocketSharp;
using System.Collections.Generic;
using System.Linq;
#if !UNITY_SERVER
using UnityEngine.Animations.Rigging;
using Unity.VisualScripting;
#endif




public class PlayerNetworkHandler : NetworkBehaviour
{
    [Header("DATA CONFIG")]
    private PlayerInfoStruct _playerModelCache; // Bộ nhớ đệm thông tin người chơi để tránh gọi lại dữ liệu quản lý
    private int _playerIndexCache = -1; // Chỉ mục người chơi được lưu trữ để truy cập nhanh trong bộ quản lý
    private bool hasSpawnedNetworkState;

    [Networked] private int PlayerId { get; set; }
    private bool CanAccessNetworkedState
    {
        get
        {
            var networkObject = Object;
            return hasSpawnedNetworkState && networkObject != null && networkObject.IsValid;
        }
    }

    public PlayerInfoStruct PlayerModel
    {
        get
        {
            return SyncPlayerModelFromManager();
        }
        set
        {
            _playerModelCache = value;
            if (CanAccessNetworkedState)
            {
                PlayerId = value.playerId;
            }

            var manager = NetworkObjectManager.Instance;
            if (manager == null || !manager.IsNetworkStateReady)
            {
                _playerIndexCache = -1;
                return;
            }

            if (value.playerId == 0)
            {
                _playerIndexCache = -1;
                return;
            }

            if (manager.HasStateAuthority)
            {
                int index = GetOrCreatePlayerIndex(manager, value.playerId);
                if (index >= 0)
                {
                    manager.players.Set(index, value);
                    _playerIndexCache = index;
                }
                else
                {
                    _playerIndexCache = -1;
                }
            }
            else if (TryGetExistingPlayerIndex(manager, value.playerId, out var existingIndex))
            {
                _playerIndexCache = existingIndex;
            }
        }
    }
    [Networked] public int IsSpawned { get; set; }
    [Networked] public bool isContinueTurn { get; set; }

    [Networked] private Vector3 TargetPosition { get; set; }
    [Networked] public Quaternion TargetRotation { get; set; }
    [Networked] public Quaternion HeadRotation { get; set; } // tách riêng ra
    [Networked] public CharacterAnimState CurrentAnimState { get; set; }
    [Networked] public int IdleAnimIndex { get; set; }
    [Networked] public int isPausedAni { get; set; } = 0;
    [Networked] public int WeightRig { get; set; } = 0;
    [Networked] public Vector3 PointPosToSync { get; set; }
    [Networked] public Vector3 FingerPos { get; set; }
    [Networked] public float FingerRigPower { get; set; }
    [Networked] public NetworkBool IsBananaJumpActive { get; set; }

    [Header("SYSTEM CONFIG")]
    //Tốc độ di chuyển khi nội suy vị trí nhân vật
    private float moveSpeed = 1.2f;
    [SerializeField, Tooltip("Tốc độ xoay thân để bám theo hướng nhìn")] private float bodyTurnSpeed = 5f;
    [SerializeField, Tooltip("Khoảng cách từ đầu đến điểm ngắm"), Min(0.01f)] private float pointAimDistance = 1.5f;
    [SerializeField, Tooltip("Độ lệch gốc để tính điểm ngắm khi không có head transform")] private Vector3 pointAimOriginOffset = new Vector3(0f, 1.55f, 0f);
    private float playAreaKeepDistance = 0.001f; //Khoảng cách giữ khi tiến gần khu vực playArea
    [SerializeField, Tooltip("Độ lệch nhỏ để nhân vật bám sát mặt đất tự nhiên hơn")] private float groundFollowOffset = 0.02f;
    private float groundSurfaceOffset = 0.02f;//Khoảng cách giữ nhân vật cách bề mặt Ground/Way
    [SerializeField, Tooltip("Khoảng cách tối thiểu tới tường khi di chuyển tới mục tiêu")] private float wallAvoidanceDistance = 0.5f;
    [SerializeField, Tooltip("Độ đẩy thêm để tránh đứng sát tường")] private float wallAvoidanceExtraPush = 0.1f;
    private string lastPlayedAnim; // Tên animation cuối cùng đã phát để tránh phát lặp
    private CharacterAnimState lastAnimState; // Trạng thái animation trước đó để so sánh thay đổi
    private Vector3 _lastTargetPosition; // Vị trí mục tiêu cuối cùng dùng nội suy chuyển động
    [SerializeField, Tooltip("Thời gian kỹ năng nhảy né vỏ chuối (giây)")] private float bananaJumpSkillDuration = 3f;
    [SerializeField, Tooltip("Chiều cao tối đa khi bật kỹ năng nhảy né vỏ chuối")]
    private float bananaJumpHeight = 0.09f;
    private float _bananaJumpBaseY;
    private bool _hasBananaJumpBaseY;
#if !UNITY_SERVER
    private bool _hasRefreshedSkillsForRunningState;
    private bool _isFootstepLoopActive;
    private bool _hasInitializedDestroyAudioState;
    private bool _wasMarkedDestroyedForAudio;
    private bool _hasFocusedDefeatCamera;
    private Coroutine _slipVibrationRoutine;
#endif
#if UNITY_SERVER
    private CharacterAnimState? _animStateBeforeBananaJump;
#endif
#if UNITY_SERVER
    private Vector3 _serverLockedPosition; // Vị trí được khóa để chống dịch chuyển ngoài ý muốn
    private bool _hasServerLockedPosition; // Đánh dấu đã khởi tạo vị trí khóa hay chưa
    private Collider[] _serverCachedColliders;
    private static readonly List<PlayerNetworkHandler> ServerHandlers = new List<PlayerNetworkHandler>();
    private bool _hasLoggedDeltaTimeFallback;
    private readonly List<Collider> _pendingBananaCollisions = new List<Collider>();
    private float _bananaJumpElapsed;
#endif
    private Collider[] _cachedAllColliders;
    private bool _isDestroyedPresentationApplied;
    private bool _areCollidersPresentationEnabled = true;
    private Coroutine _temporaryAnimResetRoutine;
    private CharacterAnimState _animStateBeforeTemporaryEmote = CharacterAnimState.Idle;

    public bool IsMarkedDestroyed
    {
        get
        {
            var model = PlayerModel;
            return model.isDestroy || model.statusPlayer == StatusPlayer.Destroy || model.statusPlayer == StatusPlayer.WaitingDestroy;
        }
    }

#if !UNITY_SERVER
    private const float LocalBananaSlipDuration = 4.5f;

    [Header("MODEL VISUAL CONFIG")]
    private PlayerModelVisualComponent _playerModelVisualComponent;
    private GameObject _playerModelVisualInstance;
    private Animator CurrentAnimator => _playerModelVisualComponent?.Animator;
    public Transform HeadTransform => _playerModelVisualComponent?.HeadTransform;
    public Transform FingerPosition => _playerModelVisualComponent?.FingerPosition;
    public Transform FingerJointPrimary => _playerModelVisualComponent?.FingerJointPrimary;
    public Transform FingerJointSecondary => _playerModelVisualComponent?.FingerJointSecondary;
    public Transform FPPPosition => _playerModelVisualComponent?.FPPPosition;
    public Transform FPPPositionCam2 => _playerModelVisualComponent?.FPPPositionCam2;
    public Transform PointPosition => _playerModelVisualComponent?.PointPosition; 
    public Transform PointPositionCam2 => _playerModelVisualComponent?.PointPositionCam2;
    public Transform PowerPosition => _playerModelVisualComponent?.PowerPosition;
    private MultiAimConstraint AimConstraint
    {
        get => _playerModelVisualComponent?.AimConstraint;
        set
        {
            if (_playerModelVisualComponent != null)
                _playerModelVisualComponent.AimConstraint = value;
        }
    }
    private RigBuilder RigBuilder
    {
        get => _playerModelVisualComponent?.RigBuilder;
        set
        {
            if (_playerModelVisualComponent != null)
                _playerModelVisualComponent.RigBuilder = value;
        }
    }
    private Rig RigLayer
    {
        get => _playerModelVisualComponent?.RigLayer;
        set
        {
            if (_playerModelVisualComponent != null)
                _playerModelVisualComponent.RigLayer = value;
        }
    }
    private Transform RigLayerTransform
    {
        get => _playerModelVisualComponent?.RigLayerTransform;
        set
        {
            if (_playerModelVisualComponent != null)
                _playerModelVisualComponent.RigLayerTransform = value;
        }
    }
    private Transform SpineTargetTransform
    {
        get => _playerModelVisualComponent?.SpineTargetTransform;
        set
        {
            if (_playerModelVisualComponent != null)
                _playerModelVisualComponent.SpineTargetTransform = value;
        }
    }
    private Renderer[] CharacterRenderers
    {
        get => _playerModelVisualComponent?.CharacterRenderers;
        set
        {
            if (_playerModelVisualComponent != null)
                _playerModelVisualComponent.CharacterRenderers = value;
        }
    }
    public bool IsNetworkStateReady => CanAccessNetworkedState && IsSpawned == 1;

    private void AssignPlayerIdToVisualComponent()
    {
        if (_playerModelVisualComponent != null)
            _playerModelVisualComponent.PlayerId = PlayerModel.playerId;
    }
    private Tween _moveTween; // Tween nội suy di chuyển nhân vật
    private Tween _rotateTween; // Tween nội suy xoay toàn thân nhân vật
    private Tween _headTween; // Tween nội suy xoay đầu nhân vật
    [SerializeField, Tooltip("Giữ nguyên độ lệch mục tiêu ban đầu của rig")]
    private bool maintainAimOffset = false;
    [SerializeField, Tooltip("Độ lệch mặc định áp dụng cho mục tiêu rig")]
    private Vector3 constraintOffset = new Vector3(35f, 0f, 0f);
    [SerializeField, Tooltip("Giới hạn xoay rig tính theo độ")] private Vector2 constraintLimits = new Vector2(-180f, 180f);
    [SerializeField, Tooltip("Bật ràng buộc theo trục X của rig")] private bool constrainXAxis = true;
    [SerializeField, Tooltip("Bật ràng buộc theo trục Y của rig")] private bool constrainYAxis = true;
    [SerializeField, Tooltip("Bật ràng buộc theo trục Z của rig")] private bool constrainZAxis = false;
    private bool _rigInitialized; // Trạng thái đã khởi tạo hệ thống rig hay chưa
    [Header("FINGER RIG CONFIG")]
    [SerializeField, Tooltip("Tốc độ làm mượt khi kéo ngón tay theo lực.")]
    private float fingerRigBlendSpeed = 6f;
    [SerializeField, Tooltip("Góc kéo lùi của khớp ngón tay chính.")]
    private Vector3 fingerJointPrimaryPullRotation = new Vector3(-25f, 0f, 0f);
    [SerializeField, Tooltip("Góc kéo lùi của khớp ngón tay phụ.")]
    private Vector3 fingerJointSecondaryPullRotation = new Vector3(-40f, 0f, 0f);
    [SerializeField, Tooltip("Curve lực cho khớp 1 (căng trước).")]
    private AnimationCurve fingerJointPrimaryWeightCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.6f, 1f)
    );
    [SerializeField, Tooltip("Curve lực cho khớp 2 (căng sau).")]
    private AnimationCurve fingerJointSecondaryWeightCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.6f, 0f),
        new Keyframe(1f, 1f)
    );
    private bool _fingerRigInitialized;
    private float _fingerRigPowerSmoothed;
    private Transform _fingerRigTargetRoot;
    private Transform _fingerJointPrimaryTarget;
    private Transform _fingerJointSecondaryTarget;
    private MultiRotationConstraint _fingerJointPrimaryConstraint;
    private MultiRotationConstraint _fingerJointSecondaryConstraint;

    private bool _defaultMaterialApplied; // Đánh dấu đã áp dụng vật liệu mặc định chưa

    [Header("INTERPOLATION CONFIG")]
    [SerializeField, Tooltip("Thời gian nội suy mượt hình ảnh ở client proxy")] private float _renderSmoothTime = 0.045f;
    [SerializeField, Tooltip("Tốc độ nội suy xoay của hiển thị")] private float _renderRotationLerpSpeed = 22f;
    private Transform _playerVisualAnchor; // Neo hiển thị của nhân vật trong scene
    private Transform _playerVisualRootInstance; // Bản thể gốc visual được sinh ra khi nội suy
    private Vector3 _lastRenderPosition; // Vị trí hiển thị cuối cùng để tính tốc độ mượt
    private Vector3 _smoothedRenderVelocity; // Vận tốc mượt được tính toán cho nội suy
    private bool _hasRenderSample; // Đánh dấu đã có mẫu dữ liệu hiển thị trước đó
    private bool _hasPointPosToSyncValue; // Đánh dấu đã nhận được dữ liệu PointPosToSync hợp lệ từ server
    private Vector3 _lastPointPosToSyncValue; // Giá trị PointPosToSync lần trước để phát hiện thay đổi
    private bool _hasPointPosSample; // Đánh dấu đã có mẫu PointPosToSync trước đó
#endif
    [SerializeField, Tooltip("Gốc transform chứa toàn bộ hiển thị của nhân vật")] private Transform playerVisualRoot;
    [SerializeField, Tooltip("Transform làm mục tiêu nội suy cho hiển thị")] private Transform InterpolationTarget;
    public List<EffectPlayerSchema> ActiveEffects = new List<EffectPlayerSchema>(); // Danh sách hiệu ứng đang kích hoạt trên nhân vật

    private void ApplyAuthoritativePosition(Vector3 newPosition)
    {
        transform.position = newPosition;
#if UNITY_SERVER
        if (HasStateAuthority)
        {
            _serverLockedPosition = newPosition;
            _hasServerLockedPosition = true;
        }
#endif
    }




    private void EnforceServerLockedPosition()
    {
#if UNITY_SERVER
        if (!HasStateAuthority || !_hasServerLockedPosition)
            return;

        Vector3 current = transform.position;
        if ((current - _serverLockedPosition).sqrMagnitude > 0.0001f)
        {
            transform.position = _serverLockedPosition;
        }
#endif
    }

#if UNITY_SERVER
    private void RegisterPlayer()
    {
        if (HasStateAuthority)
        {
            // lưu vào thư viện map model
            var manager = NetworkObjectManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("⚠️ NetworkObjectManager chưa sẵn sàng để đăng ký player object.");
                return;
            }
            manager.RegisterPlayerObject(PlayerModel.playerId, Object);
            IsSpawned = 1;
            if (AnimatorController.Instance != null)
                IdleAnimIndex = Random.Range(0, AnimatorController.Instance.IdleAnimationCount);
            RegisterServerCollisionExclusions();
        }
    }

    public bool TryActivateBananaJumpSkill()
    {
        if (!CanAccessNetworkedState)
            return false;

#if UNITY_SERVER
        Debug.Log($"[BananaJump][Server] TryActivateBananaJumpSkill request for player {PlayerModel.playerId}.");
        if (!HasStateAuthority)
        {
            Debug.LogWarning($"[BananaJump][Server] Player {PlayerModel.playerId} does not have state authority.");
            return false;
        }

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.IsYourTurn(PlayerModel.playerId))
        {
            Debug.LogWarning($"[BananaJump][Server] Player {PlayerModel.playerId} cannot activate Banana Jump because it is not their turn or manager missing.");
            return false;
        }

        if (CurrentAnimState != CharacterAnimState.Running || IsBananaJumpActive)
        {
            Debug.LogWarning($"[BananaJump][Server] Player {PlayerModel.playerId} cannot activate Banana Jump. CurrentAnimState={CurrentAnimState}, IsBananaJumpActive={IsBananaJumpActive}.");
            return false;
        }

        StartBananaJump();
        Debug.Log($"[BananaJump][Server] Banana Jump routine started for player {PlayerModel.playerId}.");
        return true;
#else
        return false;
#endif
    }

#if UNITY_SERVER
    private void StartBananaJump()
    {
        _animStateBeforeBananaJump = CurrentAnimState;
        _bananaJumpElapsed = 0f;
        _bananaJumpBaseY = transform.position.y;
        _hasBananaJumpBaseY = true;
        CurrentAnimState = CharacterAnimState.Jumping;
        IsBananaJumpActive = true;
        Debug.Log($"[BananaJump][Server] Player {PlayerModel.playerId} animation state set to Jumping.");
    }

    private float UpdateBananaJumpHeight(float baseY, float deltaTime)
    {
        if (!IsBananaJumpActive)
            return baseY;

        float groundedY = _hasBananaJumpBaseY ? _bananaJumpBaseY : baseY;

        float duration = Mathf.Max(0.1f, bananaJumpSkillDuration);
        _bananaJumpElapsed += deltaTime;
        float t = Mathf.Clamp01(_bananaJumpElapsed / duration);
        float heightOffset = Mathf.Sin(Mathf.PI * t) * bananaJumpHeight;

        if (_bananaJumpElapsed >= duration)
        {
            CompleteBananaJump();
            return baseY;
        }

        return groundedY + heightOffset;
    }

    private void CompleteBananaJump()
    {
        IsBananaJumpActive = false;
        _bananaJumpElapsed = 0f;
        _hasBananaJumpBaseY = false;
        if (_animStateBeforeBananaJump.HasValue)
        {
            CurrentAnimState = _animStateBeforeBananaJump.Value;
            _animStateBeforeBananaJump = null;
        }
        Debug.Log($"[BananaJump][Server] Banana Jump coroutine finished for player {PlayerModel.playerId}. State reverted to {CurrentAnimState}.");
    }
#endif
    private void CaptureServerPosition()
    {
        if (HasStateAuthority)
        {
            _serverLockedPosition = transform.position;
            _hasServerLockedPosition = true;
        }
    }
    private void RegisterServerCollisionExclusions()
    {
        if (!HasStateAuthority)
            return;

        // ✅ Đảm bảo đã lưu lại toàn bộ collider của nhân vật để dùng cho bước loại trừ va chạm
        CacheServerCollidersIfNeeded();

        if (!ServerHandlers.Contains(this))
            // ✅ Lưu handler hiện tại vào danh sách tĩnh để xử lý các bóng sinh ra sau này
            ServerHandlers.Add(this);

        // ✅ Áp dụng bỏ qua va chạm cho toàn bộ bóng đang tồn tại trên server
        ApplyCollisionExclusionsToAllBalls();
    }

    private void CacheServerCollidersIfNeeded()
    {
        if (_serverCachedColliders != null && _serverCachedColliders.Length > 0)
            return;

        // ✅ Lấy toàn bộ collider của player (bao gồm cả con cháu) và lưu lại để tái sử dụng
        _serverCachedColliders = GetComponentsInChildren<Collider>(true);
    }

    private void ApplyCollisionExclusionsToAllBalls()
    {
        if (_serverCachedColliders == null || _serverCachedColliders.Length == 0)
            return;

        // ✅ Tìm tất cả các bóng đang tồn tại trên server và thiết lập bỏ qua va chạm với player này
        var balls = UnityEngine.Object.FindObjectsOfType<BallServerController>();
        foreach (var ball in balls)
        {
            // ✅ Mỗi bóng sẽ được xử lý để bỏ qua va chạm với toàn bộ collider của người chơi
            HandleBallCollisionIgnore(ball);
        }
    }

    private void HandleBallCollisionIgnore(BallServerController ball)
    {
        if (ball == null)
            return;

        CacheServerCollidersIfNeeded();

        if (_serverCachedColliders == null || _serverCachedColliders.Length == 0)
            return;

        // ✅ Lấy danh sách collider của bóng, nếu không có thì không cần xử lý
        var ballColliders = ball.GetComponentsInChildren<Collider>(true);
        if (ballColliders == null || ballColliders.Length == 0)
            return;

        foreach (var playerCollider in _serverCachedColliders)
        {
            if (playerCollider == null)
                continue;

            foreach (var ballCollider in ballColliders)
            {
                if (ballCollider == null)
                    continue;

                if (ReferenceEquals(playerCollider, ballCollider))
                    continue;

                // ✅ Thiết lập Unity bỏ qua va chạm giữa collider của player và collider của bóng
                Physics.IgnoreCollision(playerCollider, ballCollider, true);
            }
        }
    }

    internal static void NotifyBallSpawnedServer(BallServerController ball)
    {
        if (ball == null)
            return;

        for (int i = ServerHandlers.Count - 1; i >= 0; i--)
        {
            var handler = ServerHandlers[i];
            if (handler == null)
            {
                // ✅ Dọn dẹp những handler đã bị hủy để tránh lỗi tham chiếu null
                ServerHandlers.RemoveAt(i);
                continue;
            }

            // ✅ Mỗi handler sẽ áp dụng lại logic bỏ qua va chạm cho bóng vừa được sinh ra
            handler.HandleBallCollisionIgnore(ball);
        }
    }
#endif

    private Transform GetHeadTransformSafe()
    {
#if !UNITY_SERVER
        return HeadTransform;
#else
        return null;
#endif
    }

    private bool TryGetPointAimOrigin(out Vector3 origin)
    {
#if !UNITY_SERVER
        var head = HeadTransform;
        if (head != null)
        {
            origin = head.position;
            return true;
        }
#endif

        origin = transform.position + pointAimOriginOffset;
        return true;
    }

    private bool ShouldKeepVisualForDefeatPresentation()
    {
        return CurrentAnimState == CharacterAnimState.LoseEmotion;
    }

    private bool ShouldHideDestroyedVisuals()
    {
        return IsMarkedDestroyed && !ShouldKeepVisualForDefeatPresentation();
    }

    private static bool IsTemporaryEmoteState(CharacterAnimState state)
    {
        switch (state)
        {
            case CharacterAnimState.EmoteLaugh:
            case CharacterAnimState.EmoteTaunt:
            case CharacterAnimState.EmoteAngry:
            case CharacterAnimState.EmoteClap:
            case CharacterAnimState.EmoteSad:
                return true;
            default:
                return false;
        }
    }

    private CharacterAnimState ResolveAnimStateAfterTemporaryEmote()
    {
        switch (CurrentAnimState)
        {
            case CharacterAnimState.Running:
            case CharacterAnimState.SitToShoot:
            case CharacterAnimState.Idle:
            case CharacterAnimState.Sleeping:
            case CharacterAnimState.None:
                return CurrentAnimState;
            default:
                return CharacterAnimState.Idle;
        }
    }

    private void ApplyRequestedAnimState(CharacterAnimState requestedState)
    {
        if (!IsTemporaryEmoteState(requestedState))
        {
            StopTemporaryAnimResetRoutine();
            CurrentAnimState = requestedState;
            return;
        }

        _animStateBeforeTemporaryEmote = ResolveAnimStateAfterTemporaryEmote();
        CurrentAnimState = requestedState;

        if (_temporaryAnimResetRoutine != null)
            StopCoroutine(_temporaryAnimResetRoutine);

        _temporaryAnimResetRoutine = StartCoroutine(ResetTemporaryAnimStateRoutine(requestedState, _animStateBeforeTemporaryEmote, 2.2f));
    }

    private IEnumerator ResetTemporaryAnimStateRoutine(CharacterAnimState emoteState, CharacterAnimState fallbackState, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (CurrentAnimState == emoteState)
            CurrentAnimState = fallbackState;

        _temporaryAnimResetRoutine = null;
    }

    private void StopTemporaryAnimResetRoutine()
    {
        if (_temporaryAnimResetRoutine == null)
            return;

        StopCoroutine(_temporaryAnimResetRoutine);
        _temporaryAnimResetRoutine = null;
    }

    public bool TryGetAimOrigin(out Vector3 origin)
    {
        return TryGetPointAimOrigin(out origin);
    }

    private PlayerInfoStruct SyncPlayerModelFromManager()
    {
        int playerId = _playerModelCache.playerId;
        if (playerId == 0 && CanAccessNetworkedState)
            playerId = PlayerId;

        if (playerId != 0 && _playerModelCache.playerId != playerId)
            _playerModelCache.playerId = playerId;

        var manager = NetworkObjectManager.Instance;
        if (manager == null || !manager.IsNetworkStateReady)
        {
            _playerIndexCache = -1;
            return _playerModelCache;
        }

        if (playerId == 0)
        {
            _playerIndexCache = -1;
            return _playerModelCache;
        }

        var players = manager.players;

        if (_playerIndexCache >= 0)
        {
            var cached = players.Get(_playerIndexCache);
            if (cached.playerId == playerId)
            {
                _playerModelCache = cached;
                return _playerModelCache;
            }
        }

        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (info.playerId == playerId)
            {
                _playerIndexCache = i;
                _playerModelCache = info;
                return _playerModelCache;
            }
        }

        _playerIndexCache = -1;
        return _playerModelCache;
    }

    private int GetOrCreatePlayerIndex(NetworkObjectManager manager, int playerId)
    {
        if (manager == null || !manager.IsNetworkStateReady || playerId == 0)
            return -1;

        var players = manager.players;

        if (_playerIndexCache >= 0)
        {
            var cached = players.Get(_playerIndexCache);
            if (cached.playerId == playerId || cached.playerId == 0)
                return _playerIndexCache;
        }

        int emptySlot = -1;

        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (info.playerId == playerId)
                return i;

            if (info.playerId == 0 && emptySlot == -1)
                emptySlot = i;
        }

        return emptySlot;
    }

    private bool TryGetExistingPlayerIndex(NetworkObjectManager manager, int playerId, out int index)
    {
        index = -1;
        if (manager == null || !manager.IsNetworkStateReady || playerId == 0)
            return false;

        var players = manager.players;

        if (_playerIndexCache >= 0)
        {
            var cached = players.Get(_playerIndexCache);
            if (cached.playerId == playerId)
            {
                index = _playerIndexCache;
                return true;
            }
        }

        for (int i = 0; i < players.Length; i++)
        {
            var info = players.Get(i);
            if (info.playerId == playerId)
            {
                _playerIndexCache = i;
                index = i;
                return true;
            }
        }

        _playerIndexCache = -1;
        return false;
    }

    //public void SetActiveEffects(IEnumerable<EffectPlayerSchema> effects)
    //{
    //    ActiveEffects.Clear();
    //    if (effects == null)
    //    {
    //        return;
    //    }

    //    ActiveEffects.AddRange(effects);
    //}

    private void CacheAllCollidersIfNeeded()
    {
        if (_cachedAllColliders != null && _cachedAllColliders.Length > 0)
            return;

        _cachedAllColliders = GetComponentsInChildren<Collider>(true);
    }

    private void SetAllCollidersEnabled(bool enabled)
    {
        CacheAllCollidersIfNeeded();
        if (_cachedAllColliders == null)
            return;

        foreach (var collider in _cachedAllColliders)
        {
            if (collider == null)
                continue;

            collider.enabled = enabled;
        }
    }

#if !UNITY_SERVER
    private void SetCharacterRenderersEnabled(bool enabled)
    {
        Renderer[] targets = null;

        var configuredRenderers = CharacterRenderers;
        if (configuredRenderers != null && configuredRenderers.Length > 0)
        {
            targets = configuredRenderers;
        }
        else if (_playerModelVisualComponent != null)
        {
            targets = _playerModelVisualComponent.GetComponentsInChildren<Renderer>(true);
            if (targets != null && targets.Length > 0)
                CharacterRenderers = targets;
        }
        else
        {
            targets = GetComponentsInChildren<Renderer>(true);
        }

        if (targets == null)
            return;

        foreach (var renderer in targets)
        {
            if (renderer == null)
                continue;

            if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
                renderer.enabled = enabled;
        }
    }
#endif

    public void RefreshDestroyedPresentationState()
    {
        bool isMarkedDestroyed = IsMarkedDestroyed;
#if !UNITY_SERVER
        TryPlayLocalEliminationAudio(isMarkedDestroyed);
#endif
        bool shouldHideDestroyedPlayer = isMarkedDestroyed && !ShouldKeepVisualForDefeatPresentation();
        bool shouldEnableColliders = !isMarkedDestroyed;

        if (_areCollidersPresentationEnabled != shouldEnableColliders)
        {
            _areCollidersPresentationEnabled = shouldEnableColliders;
            SetAllCollidersEnabled(shouldEnableColliders);
        }

        if (_isDestroyedPresentationApplied != shouldHideDestroyedPlayer)
        {
            _isDestroyedPresentationApplied = shouldHideDestroyedPlayer;
#if !UNITY_SERVER
            SetCharacterRenderersEnabled(!shouldHideDestroyedPlayer);
#endif
        }

        if (isMarkedDestroyed && HasStateAuthority)
        {
            FingerRigPower = 0f;
            IsBananaJumpActive = false;
        }
    }

#if !UNITY_SERVER
    private void TryPlayLocalEliminationAudio(bool isMarkedDestroyed)
    {
        if (!_hasInitializedDestroyAudioState)
        {
            _hasInitializedDestroyAudioState = true;
            _wasMarkedDestroyedForAudio = isMarkedDestroyed;
            return;
        }

        if (!isMarkedDestroyed)
        {
            _wasMarkedDestroyedForAudio = false;
            return;
        }

        if (_wasMarkedDestroyedForAudio)
            return;

        _wasMarkedDestroyedForAudio = true;

        if (!IsLocalHumanPlayer())
            return;

        ClientGameplayBridge.Sound.PlayPlayerEliminated();
    }

    private bool IsLocalHumanPlayer()
    {
        int localPlayerId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        return localPlayerId > 0 && PlayerModel.playerId == localPlayerId;
    }

    private void TryFollowLocalDefeatedPlayer()
    {
        if (_hasFocusedDefeatCamera || !IsLocalHumanPlayer())
            return;

        _hasFocusedDefeatCamera = true;
        ClientGameplayBridge.Camera.StartFollowingPlayerOnline(transform);
    }
#endif

    public override void Spawned()
    {
        base.Spawned();
        hasSpawnedNetworkState = true;
        if (_playerModelCache.playerId != 0 && CanAccessNetworkedState && PlayerId == 0)
        {
            PlayerId = _playerModelCache.playerId;
        }
#if UNITY_SERVER
// Chỉ server mới được quyền đăng ký tải xong
        StartCoroutine(LoadRegisterPlayer());
#endif
#if !UNITY_SERVER
        _hasFocusedDefeatCamera = false;
        //Client tiến hành tải toàn bộ visual từ client
        StartCoroutine(LoadVisualModel());
        // Chỉ tải kỹ năng cho nhân vật của bạn
        if(HasInputAuthority)
            StartCoroutine(LoadActiveEffectsWhenReady());
#endif
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        hasSpawnedNetworkState = false;
        _playerIndexCache = -1;
        base.Despawned(runner, hasState);
    }





#if !UNITY_SERVER
    private IEnumerator LoadVisualModel()
    {
        while (PlayerModel.playerId == 0)
            yield return null;
        SpawnLocalPlayerModelVisual();
        EnsurePlayerVisualInstance();
        ResolveAnimatorFromVisual();
        //SetupAimConstraintIfNeeded();
        InitializeRigSetup();
        yield return ApplyDefaultMaterialRoutine();
        RefreshDestroyedPresentationState();
        //syn vị trí của bạn
        if (HasInputAuthority && MovePlayerOnlineHandler.Instance != null)
        {
            MovePlayerOnlineHandler.Instance.SetLocalPlayerHandler(this);
        }
    }
    private IEnumerator LoadActiveEffectsWhenReady()
    {
        while (PlayerModel.playerId == 0)
            yield return null;

        var apiManager = APIManager.Instance;
        if (apiManager == null)
        {
            Debug.LogWarning("⚠️ APIManager chưa sẵn sàng để tải kỹ năng nhân vật.");
            yield break;
        }

        List<EffectPlayerSchema> effects = null;
        yield return StartCoroutine(apiManager.RunTask(
            apiManager.GetEffectPlayersAsync(PlayerModel.playerId),
            result => effects = result));

        ActiveEffects.Clear();
        if (effects != null && effects.Count > 0)
        {
            ActiveEffects.AddRange(effects);
            Debug.Log("Load kỹ năng thành công");
        }
    }

    //private void Start()
    //{

    //}
    public void SetupAimConstraintIfNeeded()
    {
        if (_playerModelVisualComponent == null)
        {
            Debug.LogWarning($"⚠️ {nameof(PlayerNetworkHandler)} on {name} thiếu {nameof(PlayerModelVisualComponent)}, bỏ qua thiết lập AimConstraint.");
            return;
        }

        var point = PointPosition;
        if (point == null)
        {
            Debug.LogWarning($"⚠️ {nameof(PlayerNetworkHandler)} on {name} thiếu PointPosition, bỏ qua thiết lập AimConstraint.");
            return;
        }

        var head = HeadTransform;
        if (head == null)
        {
            Debug.LogWarning($"⚠️ {nameof(PlayerNetworkHandler)} on {name} thiếu headTransform, bỏ qua thiết lập AimConstraint.");
            return;
        }

        var rigObj = new GameObject("AimConstraintRig");
        rigObj.transform.SetParent(_playerModelVisualComponent.transform, false);

        var rig = rigObj.AddComponent<Rig>();
        var constraint = rigObj.AddComponent<MultiAimConstraint>();

        var data = constraint.data;
        data.constrainedObject = head;
        data.aimAxis = MultiAimConstraintData.Axis.Z;
        data.upAxis = MultiAimConstraintData.Axis.Y;
        data.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;

        var src = new WeightedTransformArray();
        src.Add(new WeightedTransform(point, 1f));
        data.sourceObjects = src;

        data.maintainOffset = false;
        data.offset = new Vector3(35f, 0f, 0f);
        data.constrainedXAxis = true;
        data.constrainedYAxis = true;
        data.constrainedZAxis = false;
        data.limits = new Vector2(20f, 60f);

        constraint.data = data;

        var builder = RigBuilder;
        if (builder == null)
        {
            builder = _playerModelVisualComponent.GetComponent<RigBuilder>();
            if (builder == null)
                builder = _playerModelVisualComponent.gameObject.AddComponent<RigBuilder>();
            RigBuilder = builder;
        }

        var layers = builder.layers;
        if (!layers.Any(layer => layer.rig == rig))
        {
            layers.Add(new RigLayer(rig));
            builder.layers = layers;
        }
        builder.Build();

        AimConstraint = constraint;
    }

    private void InitializeRigSetup()
    {
        if (_rigInitialized)
            return;

        if (_playerModelVisualComponent == null)
            return;

        _rigInitialized = true;

        var rootObject = _playerModelVisualComponent.gameObject;

        var rigBuilder = RigBuilder;
        if (rigBuilder == null)
        {
            rigBuilder = rootObject.GetComponent<RigBuilder>();
            if (rigBuilder == null)
                rigBuilder = rootObject.AddComponent<RigBuilder>();
            RigBuilder = rigBuilder;
        }

        var rigLayerTransform = RigLayerTransform;
        if (rigLayerTransform == null)
        {
            var rigLayerObject = new GameObject("RigLayer");
            rigLayerObject.transform.SetParent(rootObject.transform, false);
            rigLayerTransform = rigLayerObject.transform;
            RigLayerTransform = rigLayerTransform;
        }

        var rigLayer = RigLayer;
        if (rigLayer == null)
        {
            rigLayer = rigLayerTransform.GetComponent<Rig>() ?? rigLayerTransform.gameObject.AddComponent<Rig>();
            RigLayer = rigLayer;
        }

        var spineTargetTransform = SpineTargetTransform;
        if (spineTargetTransform == null)
        {
            var spineTargetObject = new GameObject("SpineTarget");
            spineTargetObject.transform.SetParent(rigLayerTransform, false);
            spineTargetTransform = spineTargetObject.transform;
            SpineTargetTransform = spineTargetTransform;
        }

        var aimConstraint = AimConstraint;
        if (aimConstraint == null)
        {
            aimConstraint = spineTargetTransform.GetComponent<MultiAimConstraint>();
            if (aimConstraint == null)
                aimConstraint = spineTargetTransform.gameObject.AddComponent<MultiAimConstraint>();
            AimConstraint = aimConstraint;
        }

        aimConstraint.weight = 1f;

        ConfigureAimConstraint();
        InitializeFingerRig();

        var layers = rigBuilder.layers ?? new List<RigLayer>();
        if (!layers.Any(layer => layer.rig == rigLayer))
        {
            layers.Add(new RigLayer(rigLayer));
            rigBuilder.layers = layers;
        }

        rigBuilder.Build();
    }

    private void ConfigureAimConstraint()
    {
        var constraint = AimConstraint;
        if (constraint == null)
            return;

        var head = HeadTransform;
        if (head == null)
            return;

        var point = PointPosition;
        if (point == null)
            return;

        var data = constraint.data;
        data.constrainedObject = head; // ⚠️ Gắn xương Spine (constrained object) tại đây trong inspector
        data.aimAxis = MultiAimConstraintData.Axis.Z;
        data.upAxis = MultiAimConstraintData.Axis.Y;
        data.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
        data.maintainOffset = maintainAimOffset;
        data.offset = constraintOffset;
        data.constrainedXAxis = constrainXAxis;
        data.constrainedYAxis = constrainYAxis;
        data.constrainedZAxis = constrainZAxis;
        data.limits = constraintLimits;

        var sources = new WeightedTransformArray();
        sources.Add(new WeightedTransform(point, 1f)); // ⚠️ Gắn transform Source (điểm Aim) tại đây trong inspector
        data.sourceObjects = sources;

        constraint.data = data;
    }

    private void InitializeFingerRig()
    {
        if (_fingerRigInitialized)
            return;

        var jointPrimary = FingerJointPrimary;
        var jointSecondary = FingerJointSecondary;
        if (jointPrimary == null || jointSecondary == null)
            return;

        var rigLayerTransform = RigLayerTransform;
        if (rigLayerTransform == null)
            return;

        _fingerRigInitialized = true;

        if (_fingerRigTargetRoot == null)
        {
            var targetRootObject = new GameObject("FingerRigTargets");
            targetRootObject.transform.SetParent(rigLayerTransform, false);
            _fingerRigTargetRoot = targetRootObject.transform;
        }

        if (_fingerJointPrimaryTarget == null)
        {
            var targetObject = new GameObject("FingerJointPrimaryTarget");
            targetObject.transform.SetParent(_fingerRigTargetRoot, false);
            _fingerJointPrimaryTarget = targetObject.transform;
        }

        if (_fingerJointSecondaryTarget == null)
        {
            var targetObject = new GameObject("FingerJointSecondaryTarget");
            targetObject.transform.SetParent(_fingerRigTargetRoot, false);
            _fingerJointSecondaryTarget = targetObject.transform;
        }

        if (_fingerJointPrimaryConstraint == null)
        {
            _fingerJointPrimaryConstraint = CreateFingerRotationConstraint(
                rigLayerTransform,
                "FingerJointPrimaryConstraint",
                jointPrimary,
                _fingerJointPrimaryTarget
            );
        }

        if (_fingerJointSecondaryConstraint == null)
        {
            _fingerJointSecondaryConstraint = CreateFingerRotationConstraint(
                rigLayerTransform,
                "FingerJointSecondaryConstraint",
                jointSecondary,
                _fingerJointSecondaryTarget
            );
        }

        UpdateFingerRigTargets();
        ApplyFingerRigWeights(0f);

        var rigBuilder = RigBuilder;
        if (rigBuilder != null)
            rigBuilder.Build();
    }

    private static MultiRotationConstraint CreateFingerRotationConstraint(
        Transform parent,
        string name,
        Transform constrained,
        Transform target)
    {
        var constraintObject = new GameObject(name);
        constraintObject.transform.SetParent(parent, false);
        var constraint = constraintObject.AddComponent<MultiRotationConstraint>();

        var data = constraint.data;
        data.constrainedObject = constrained;
        var sources = new WeightedTransformArray();
        sources.Add(new WeightedTransform(target, 1f));
        data.sourceObjects = sources;
        data.maintainOffset = false;
        constraint.data = data;

        constraint.weight = 0f;
        return constraint;
    }

    private void UpdateFingerRigTargets()
    {
        var jointPrimary = FingerJointPrimary;
        var jointSecondary = FingerJointSecondary;
        if (jointPrimary == null || jointSecondary == null)
            return;

        if (_fingerJointPrimaryTarget != null)
        {
            _fingerJointPrimaryTarget.SetPositionAndRotation(
                jointPrimary.position,
                jointPrimary.rotation * Quaternion.Euler(fingerJointPrimaryPullRotation)
            );
        }

        if (_fingerJointSecondaryTarget != null)
        {
            _fingerJointSecondaryTarget.SetPositionAndRotation(
                jointSecondary.position,
                jointSecondary.rotation * Quaternion.Euler(fingerJointSecondaryPullRotation)
            );
        }
    }

    private void ApplyFingerRigWeights(float power01)
    {
        if (_fingerJointPrimaryConstraint != null)
            _fingerJointPrimaryConstraint.weight = Mathf.Clamp01(fingerJointPrimaryWeightCurve.Evaluate(power01));

        if (_fingerJointSecondaryConstraint != null)
            _fingerJointSecondaryConstraint.weight = Mathf.Clamp01(fingerJointSecondaryWeightCurve.Evaluate(power01));
    }

    private float ResolveFingerRigPower01()
    {
        if (HasInputAuthority)
        {
            var powerBar = PowerBarController.Instance;
            if (powerBar != null && powerBar.isShootting && powerBar.powerSlider != null)
                return Mathf.Clamp01(powerBar.powerSlider.value);

            return 0f;
        }

        return Mathf.Clamp01(FingerRigPower);
    }

    private void UpdateFingerRig(float deltaTime)
    {
        if (!_fingerRigInitialized)
            InitializeFingerRig();

        if (!_fingerRigInitialized)
            return;

        float targetPower = ResolveFingerRigPower01();
        _fingerRigPowerSmoothed = Mathf.MoveTowards(
            _fingerRigPowerSmoothed,
            targetPower,
            fingerRigBlendSpeed * Mathf.Max(0.001f, deltaTime)
        );

        UpdateFingerRigTargets();
        ApplyFingerRigWeights(_fingerRigPowerSmoothed);
    }

    private IEnumerator ApplyDefaultMaterialRoutine()
    {
        if (_defaultMaterialApplied)
            yield break;

        bool hasResult = false;
        yield return AddressablesHelper.LoadAsset<Material>(AddressablePaths.Character.DefaultMaterial, material =>
        {
            hasResult = true;
            if (material == null)
            {
                Debug.LogWarning("⚠️ Không thể tải material mặc định cho nhân vật.");
                return;
            }

            ApplyMaterialToRenderers(material);
            _defaultMaterialApplied = true;
        });

        if (!hasResult)
        {
            Debug.LogWarning("⚠️ Không có phản hồi khi tải material mặc định cho nhân vật.");
        }
    }

    private void ApplyMaterialToRenderers(Material material)
    {
        if (material == null)
            return;

        Renderer[] targets = null;

        var configuredRenderers = CharacterRenderers;
        if (configuredRenderers != null && configuredRenderers.Length > 0)
        {
            targets = configuredRenderers;
        }
        else if (_playerModelVisualComponent != null)
        {
            targets = _playerModelVisualComponent.GetComponentsInChildren<Renderer>();
            if (targets != null && targets.Length > 0)
                CharacterRenderers = targets;
        }
        else
        {
            targets = GetComponentsInChildren<Renderer>();
        }

        if (targets == null)
            return;

        foreach (var renderer in targets)
        {
            if (renderer == null)
                continue;

            if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
            {
                var sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length == 0)
                {
                    renderer.sharedMaterial = material;
                    continue;
                }

                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    sharedMaterials[i] = material;
                }

                renderer.sharedMaterials = sharedMaterials;
            }
        }
    }

    private void SpawnLocalPlayerModelVisual()
    {
        if (_playerModelVisualInstance == null && GameInitializer.Instance.PlayerModelVisual != null && InterpolationTarget != null)
        {
            _playerModelVisualInstance = Instantiate(GameInitializer.Instance.PlayerModelVisual, InterpolationTarget);
            var instanceTransform = _playerModelVisualInstance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;

            _playerModelVisualComponent = _playerModelVisualInstance.GetComponent<PlayerModelVisualComponent>();
            if (_playerModelVisualComponent == null)
                _playerModelVisualComponent = _playerModelVisualInstance.AddComponent<PlayerModelVisualComponent>();

            AssignPlayerIdToVisualComponent();

            playerVisualRoot = instanceTransform;
            _playerVisualRootInstance = instanceTransform;
        }

        if (_playerModelVisualComponent == null)
        {
            Transform searchRoot = null;
            if (_playerModelVisualInstance != null)
                searchRoot = _playerModelVisualInstance.transform;
            else if (playerVisualRoot != null)
                searchRoot = playerVisualRoot;
            else if (InterpolationTarget != null)
                searchRoot = InterpolationTarget;
            else
                searchRoot = transform;

            if (searchRoot != null)
                _playerModelVisualComponent = searchRoot.GetComponentInChildren<PlayerModelVisualComponent>();

            AssignPlayerIdToVisualComponent();
        }
    }

    private void EnsurePlayerVisualAnchor()
    {
        if (_playerModelVisualComponent != null && InterpolationTarget != null)
        {
            _playerVisualAnchor = InterpolationTarget;
            return;
        }

        if (playerVisualRoot == null)
        {
            _playerVisualAnchor = transform;
            return;
        }

        if (_playerVisualAnchor != null)
        {
            if (_playerVisualAnchor.parent != transform)
                _playerVisualAnchor.SetParent(transform, false);
            return;
        }

        var anchorGO = new GameObject("PlayerVisualAnchor");
        _playerVisualAnchor = anchorGO.transform;
        _playerVisualAnchor.SetParent(transform, false);
        _playerVisualAnchor.localPosition = Vector3.zero;
        _playerVisualAnchor.localRotation = Quaternion.identity;
        _playerVisualAnchor.localScale = Vector3.one;
        _hasRenderSample = false;
        _smoothedRenderVelocity = Vector3.zero;
    }

    private void EnsurePlayerVisualInstance()
    {
        if (_playerModelVisualComponent != null)
        {
            // Trường hợp prefab nhân vật đã cấp sẵn component hiển thị -> chỉ cần đồng bộ lại thông tin
            AssignPlayerIdToVisualComponent();
            var visualTransform = _playerModelVisualComponent.transform;
            playerVisualRoot = visualTransform;
            _playerVisualRootInstance = visualTransform;
            // Đảm bảo anchor (điểm gắn visual) đã được tạo và gán đúng cha con
            EnsurePlayerVisualAnchor();
            return;
        }

        if (_playerVisualRootInstance != null)
        {
            // Nếu đã có instance đang dùng thì đồng bộ lại root do inspector có thể thay đổi
            if (playerVisualRoot != null && _playerVisualRootInstance != playerVisualRoot)
                _playerVisualRootInstance = playerVisualRoot;

            // Bảo đảm root instance luôn là con của anchor để áp dụng nội suy chuyển động
            if (_playerVisualAnchor != null && _playerVisualRootInstance.parent != _playerVisualAnchor && _playerVisualAnchor != transform)
                _playerVisualRootInstance.SetParent(_playerVisualAnchor, true);

            return;
        }

        if (playerVisualRoot != null)
        {
            // Nếu inspector chỉ định sẵn visual root, gán nó làm instance chính và neo vào anchor
            EnsurePlayerVisualAnchor();
            _playerVisualRootInstance = playerVisualRoot;
            if (_playerVisualRootInstance.parent != _playerVisualAnchor)
                _playerVisualRootInstance.SetParent(_playerVisualAnchor, true);
        }
        else
        {
            // Fallback: dùng chính transform của handler để khỏi null reference
            _playerVisualRootInstance = transform;
            _playerVisualAnchor = transform;
        }

        _hasRenderSample = false;
        _smoothedRenderVelocity = Vector3.zero;
        // Khi có visual mới cần tìm lại animator tương ứng
        ResolveAnimatorFromVisual();
    }

    private Transform GetPlayerVisualAnchor()
    {
        if (_playerVisualAnchor != null)
            return _playerVisualAnchor;

        if (playerVisualRoot != null)
        {
            EnsurePlayerVisualAnchor();
            return _playerVisualAnchor;
        }

        return transform;
    }

    private Transform GetPlayerVisualRoot()
    {
        if (_playerVisualRootInstance != null)
            return _playerVisualRootInstance;

        if (playerVisualRoot != null)
            return playerVisualRoot;

        return transform;
    }

    private Transform GetVisualRenderTransform()
    {
        if (_playerModelVisualComponent != null)
            return _playerModelVisualComponent.transform;

        if (_playerModelVisualInstance != null)
            return _playerModelVisualInstance.transform;

        if (_playerVisualRootInstance != null)
            return _playerVisualRootInstance;

        if (playerVisualRoot != null && playerVisualRoot != transform)
            return playerVisualRoot;

        return null;
    }

    private void ResolveAnimatorFromVisual()
    {
        if (_playerModelVisualComponent == null)
        {
            // Cố gắng tìm component hiển thị trong cây con nếu chưa được gán thủ công
            var root = GetPlayerVisualRoot();
            if (root != null)
                _playerModelVisualComponent = root.GetComponentInChildren<PlayerModelVisualComponent>();
        }

        if (_playerModelVisualComponent == null)
            return;

        // Ghi lại playerId cho visual (phục vụ phân biệt khi đồng bộ nhiều người chơi)
        AssignPlayerIdToVisualComponent();
        // Truy cập CurrentAnimator để khởi tạo cache animator nếu chưa có
        _ = CurrentAnimator;
    }

    private void UpdateVisualInterpolation(Transform renderTransform, float deltaTime)
    {
        if (renderTransform == null)
            return;

        var desiredPosition = TargetPosition;
        var desiredRotation = TargetRotation;

        bool targetChanged = (_lastTargetPosition - desiredPosition).sqrMagnitude > 0.0001f;

        if (!_hasRenderSample || targetChanged && Vector3.Distance(renderTransform.position, desiredPosition) > 5f)
        {
            renderTransform.SetPositionAndRotation(desiredPosition, desiredRotation);
            _smoothedRenderVelocity = Vector3.zero;
            _lastRenderPosition = desiredPosition;
            _hasRenderSample = true;
            _lastTargetPosition = desiredPosition;
            return;
        }

        if (targetChanged)
            _smoothedRenderVelocity = Vector3.zero;

        var smoothedPosition = Vector3.SmoothDamp(renderTransform.position, desiredPosition, ref _smoothedRenderVelocity, _renderSmoothTime, Mathf.Infinity, deltaTime);
        renderTransform.position = smoothedPosition;
        _lastRenderPosition = smoothedPosition;

        float rotationT = Mathf.Clamp01(deltaTime * _renderRotationLerpSpeed);
        renderTransform.rotation = Quaternion.Slerp(renderTransform.rotation, desiredRotation, rotationT);

        _lastTargetPosition = desiredPosition;
    }
#endif
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestShot(ShotParams shot)
    {
#if UNITY_SERVER
        ApplyShotRequest(shot);
#endif
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_NotifyShotCommitted()
    {
#if UNITY_SERVER
        int playerId = ResolvePlayerIdForServerAction();
        GameSessionNetWork_Host.Instance?.NotifyPlayerShotCommitted(playerId);
#endif
    }

#if UNITY_SERVER
    private int ResolvePlayerIdForServerAction()
    {
        int playerId = _playerModelCache.playerId;
        if (playerId == 0 && CanAccessNetworkedState)
            playerId = PlayerId;

        if (playerId == 0)
            playerId = PlayerModel.playerId;

        return playerId;
    }
#endif
#if UNITY_SERVER
    public void DetermineTurnOrder()
    {
        if (!HasStateAuthority)
            return;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        var playerDict = manager.PlayerDict;
        var sortedPlayers = new List<(int index, PlayerInfoStruct player)>();

        foreach (var kvp in playerDict)
        {
            var playerGO = manager.GetPlayerObject(kvp.Key);
            var handler = playerGO.GetComponent<PlayerNetworkHandler>();
            sortedPlayers.Add((kvp.Key, handler.PlayerModel));
        }

        // Sắp xếp theo:
        // 1. distance >= 0 → ưu tiên (true = 0), distance < 0 → đi sau (false = 1)
        // 2. distance >= 0 → sắp tăng dần
        // 3. distance < 0 → sắp giảm dần (càng gần mức thì distance càng lớn)
        sortedPlayers = sortedPlayers
            .OrderBy(p => p.player.distance >= 0 ? 0 : 1)
            .ThenBy(p => p.player.distance >= 0 ? p.player.distance : -p.player.distance)
            .ToList();

        // Cập nhật thứ tự lượt
        for (int order = 0; order < sortedPlayers.Count; order++)
        {
            var index = sortedPlayers[order].index;
            for (int i = 0; i < manager.players.Length; i++)
            {
                var info = manager.players.Get(i);
                if (info.playerId != index)
                    continue;

                info.turnOrder = order;
                if (manager.HasStateAuthority)
                    manager.players.Set(i, info);
                GameSessionNetWork_Host.Instance?.UpdateTurnOrderEntry(index, order);
                break;
            }

            if (index == PlayerModel.playerId)
            {
                var model = PlayerModel;
                model.turnOrder = order;
                PlayerModel = model;
            }
        }
    }

    private BallServerController GetActiveBall()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return null;

        var ballObj = manager.GetActiveBallObject(PlayerModel.playerId);
        return ballObj != null ? ballObj.GetComponent<BallServerController>() : null;
    }
   private float GetGroundHeightAt(Vector3 position, float fallbackY)
    {
        var host = GameSessionNetWork_Host.Instance;
        const float raycastHeight = 10f;
        const float raycastDistance = 50f;
        Vector3 origin = position + Vector3.up * raycastHeight;

        if (TryGetSurfaceHeightByTag(origin, raycastDistance, "Way", out float wayY))
            return wayY + groundSurfaceOffset;

        if (TryGetSurfaceHeightByTag(origin, raycastDistance, "Ground", out float groundY))
            return groundY + groundSurfaceOffset;

        if (host == null)
            return fallbackY;

        Terrain terrain = host.TerrainGround;
        if (terrain == null)
            return fallbackY;

        float terrainY = terrain.SampleHeight(position) + terrain.transform.position.y;
        return terrainY + groundFollowOffset;
    }

    private bool TryGetSurfaceHeightByTag(Vector3 origin, float maxDistance, string tag, out float surfaceY)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, maxDistance, ~0, QueryTriggerInteraction.Collide);
        float closestDistance = float.MaxValue;
        surfaceY = 0f;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || !hit.collider.CompareTag(tag))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                surfaceY = hit.point.y;
            }
        }

        return closestDistance < float.MaxValue;
    }

    private bool TryAdjustTargetAwayFromWall(Vector3 targetPosition, out Vector3 adjustedTarget)
    {
        adjustedTarget = targetPosition;

        if (wallAvoidanceDistance <= 0f)
            return false;

        Collider[] hits = Physics.OverlapSphere(targetPosition, wallAvoidanceDistance, ~0, QueryTriggerInteraction.Collide);
        float closestDistance = float.MaxValue;
        Collider closestWall = null;
        Vector3 closestPoint = Vector3.zero;

        foreach (Collider hit in hits)
        {
            if (hit == null || !hit.CompareTag("Wall"))
                continue;

            Vector3 point = hit.ClosestPoint(targetPosition);
            float distance = Vector3.Distance(targetPosition, point);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestWall = hit;
                closestPoint = point;
            }
        }

        if (closestWall == null)
            return false;

        Vector3 awayDirection = targetPosition - closestPoint;
        if (awayDirection.sqrMagnitude < 0.0001f)
            awayDirection = targetPosition - closestWall.bounds.center;
        if (awayDirection.sqrMagnitude < 0.0001f)
            awayDirection = Vector3.forward;

        Vector3 pushedTarget = closestPoint + awayDirection.normalized * (wallAvoidanceDistance + wallAvoidanceExtraPush);
        pushedTarget.y = targetPosition.y;
        adjustedTarget = pushedTarget;
        return true;
    }
    public IEnumerator ProgressMoveToTarget(Vector3 targetPosition, Transform lookAtTarget = null)
    {
        float fixedY = GetGroundHeightAt(transform.position, transform.position.y);

        if (HasStateAuthority)
            CurrentAnimState = CharacterAnimState.Running;
 
        Debug.Log($"🚶‍♂️ [SERVER] Player {PlayerModel.playerId} bắt đầu di chuyển từ {transform.position} tới {targetPosition} (lookTarget={(lookAtTarget != null ? lookAtTarget.name : "None")})");
 
        Vector3 startPosition = new Vector3(transform.position.x, fixedY, transform.position.z);
        targetPosition.y = fixedY;
        if (TryAdjustTargetAwayFromWall(targetPosition, out Vector3 adjustedTarget))
        {
            Debug.Log($"🧱 [SERVER] Điều chỉnh vị trí mục tiêu để tránh sát tường cho player {PlayerModel.playerId}: {targetPosition} -> {adjustedTarget}");
            targetPosition = adjustedTarget;
        }

        BoxCollider area = GameSessionNetWork_Host.Instance != null ? GameSessionNetWork_Host.Instance.playArea : null;
        List<Vector3> path = BuildMovementPath(startPosition, targetPosition, area, playAreaKeepDistance, fixedY);

        if (path.Count == 0)
        {
            Debug.LogWarning($"⚠️ [SERVER] Không tìm được đường đi hợp lệ cho player {PlayerModel.playerId} tới {targetPosition}");
            yield break;
        }

        Vector3 finalTarget = path[path.Count - 1];

        Debug.Log($"🗺️ [SERVER] Lộ trình di chuyển player {PlayerModel.playerId} có {path.Count} điểm waypoint");

        foreach (Vector3 waypoint in path)
        {
            Vector3 target = new Vector3(waypoint.x, fixedY, waypoint.z);
            float targetGroundY = GetGroundHeightAt(target, fixedY);
            Vector3 groundedTarget = new Vector3(target.x, targetGroundY, target.z);

            if (HasStateAuthority)
                TargetPosition = groundedTarget;

            while (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(target.x, target.z)) > 0.05f)
            {
                Vector3 direction = target - transform.position;
                direction.y = 0f;

                if (HasStateAuthority && direction.sqrMagnitude > 0.0001f)
                    TargetRotation = Quaternion.LookRotation(direction.normalized);

                // Cập nhật độ cao nếu đang bật kỹ năng nhảy vỏ chuối để nhân vật vừa chạy vừa bay qua chướng ngại vật.
                if (IsBananaJumpActive)
                {
                    float baseY = GetGroundHeightAt(transform.position, fixedY);
                    float newY = UpdateBananaJumpHeight(baseY, Time.deltaTime);
                    ApplyAuthoritativePosition(new Vector3(transform.position.x, newY, transform.position.z));
                }

                yield return null;
            }

            ApplyAuthoritativePosition(groundedTarget);
        }

        if (HasStateAuthority)
            TargetPosition = new Vector3(finalTarget.x, GetGroundHeightAt(finalTarget, fixedY), finalTarget.z);
        ClientGameplayBridge.Sound.StopFootstepLoop();
        // 👉 Sau khi đến nơi Ngồi xuống
        if (HasStateAuthority)
        {
            CurrentAnimState = CharacterAnimState.SitToShoot;
#if UNITY_SERVER
            StartTurnTimerIfReadyToShoot(CurrentAnimState, "move_to_play_area");
#endif
        }
        yield return new WaitForSeconds(2f);

        Debug.Log($"✅ [SERVER] Player {PlayerModel.playerId} đã đến vị trí {finalTarget}");

        var scriptBall = GetActiveBall();
        if (scriptBall == null)
        {
            Debug.LogWarning($"⚠️ Không tìm thấy bi cho player {PlayerModel.playerId}");
            yield break;
        }

        // Nếu lượt bắn đã bắt đầu trong lúc coroutine di chuyển vẫn còn chờ hoàn tất,
        // không được set lại trạng thái cầm bi vì sẽ làm mất callback BallStop của lượt hiện tại.
        if (scriptBall.hasBeenShoot == 1)
        {
            Debug.LogWarning($"⚠️ [SERVER] Bỏ qua hold bi sau khi di chuyển cho player {PlayerModel.playerId} vì bi đã được bắn.");
            yield break;
        }

        //xin quyền
        //scriptBall.RequestAuthorityIfNeeded();
        //yield return new WaitUntil(() => scriptBall.HasStateAuthority);
        // lấy viên bi lên ngón tay
        scriptBall.IsHolding = 1;
        // lấy viên bi lên ngón tay
        // GameManagerNetWork.Instance.serverRPC.RpcHolindgBall(PlayerModel.playerId);
        yield return new WaitForSeconds(2f);
        // Việc xoay nhân vật được xử lý tại client khi bắt đầu lượt
    }

    public IEnumerator ProgressMoveToTargetForSkill(Vector3 targetPosition, System.Func<bool> shouldAbort, Transform lookAtTarget = null)
    {
        if (HasStateAuthority)
        {
            CurrentAnimState = CharacterAnimState.Running;
        }

        float fixedY = GetGroundHeightAt(transform.position, transform.position.y);

        Debug.Log($"🚶‍♂️ [SERVER] Player {PlayerModel.playerId} bắt đầu di chuyển kỹ năng tới {targetPosition}");

        Vector3 startPosition = new Vector3(transform.position.x, fixedY, transform.position.z);
        targetPosition.y = fixedY;

        BoxCollider area = GameSessionNetWork_Host.Instance != null ? GameSessionNetWork_Host.Instance.playArea : null;
        List<Vector3> path = BuildMovementPath(startPosition, targetPosition, area, playAreaKeepDistance, fixedY);

        if (path.Count == 0)
        {
            Debug.LogWarning($"⚠️ [SERVER] Không tìm được đường đi hợp lệ cho player {PlayerModel.playerId} tới {targetPosition}");
            yield break;
        }

        foreach (Vector3 waypoint in path)
        {
            Vector3 target = new Vector3(waypoint.x, fixedY, waypoint.z);
            float targetGroundY = GetGroundHeightAt(target, fixedY);
            Vector3 groundedTarget = new Vector3(target.x, targetGroundY, target.z);

            if (HasStateAuthority)
                TargetPosition = groundedTarget;

            while (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(target.x, target.z)) > 0.05f)
            {
                if (shouldAbort != null && shouldAbort())
                {
                    if (HasStateAuthority)
                    {
                        TargetPosition = transform.position;
                        CurrentAnimState = CharacterAnimState.Idle;
                    }
                    ClientGameplayBridge.Sound.StopFootstepLoop();
                    yield break;
                }

                Vector3 direction = target - transform.position;
                direction.y = 0f;

                if (HasStateAuthority && direction.sqrMagnitude > 0.0001f)
                    TargetRotation = Quaternion.LookRotation(direction.normalized);

                if (IsBananaJumpActive)
                {
                    float baseY = GetGroundHeightAt(transform.position, fixedY);
                    float newY = UpdateBananaJumpHeight(baseY, Time.deltaTime);
                    ApplyAuthoritativePosition(new Vector3(transform.position.x, newY, transform.position.z));
                }

                yield return null;
            }

            ApplyAuthoritativePosition(groundedTarget);
        }

        if (HasStateAuthority)
            TargetPosition = new Vector3(path[path.Count - 1].x, GetGroundHeightAt(path[path.Count - 1], fixedY), path[path.Count - 1].z);

        ClientGameplayBridge.Sound.StopFootstepLoop();
    }

#if UNITY_SERVER

    private IEnumerator LoadRegisterPlayer()
    {
        while (PlayerModel.playerId == 0)
            yield return null;
        RegisterPlayer();
        Debug.Log($"🎮User: {PlayerModel.playerId}, IsSpawned:True, Phân quyền: HasStateAuthority: {HasStateAuthority}, HasInputAuthority: {HasInputAuthority}");
        CaptureServerPosition();
    }
    private float GetAuthoritativeDeltaTime()
    {
        float deltaTime = 0f;

        if (Runner != null && Runner.IsRunning)
            deltaTime = Runner.DeltaTime;

        if (deltaTime <= 0f)
        {
            float unityDelta = Time.deltaTime;
            if (unityDelta > 0f)
                deltaTime = unityDelta;
        }

        if (deltaTime <= 0f)
        {
            float fixedDelta = Time.fixedDeltaTime;
            if (fixedDelta > 0f)
                deltaTime = fixedDelta;
        }

        if (deltaTime <= 0f)
            deltaTime = 0.02f;

        if (!_hasLoggedDeltaTimeFallback && (Runner == null || Runner.DeltaTime <= 0f))
        {
            Debug.LogWarning($"⚠️ [SERVER] DeltaTime không hợp lệ (Runner={(Runner != null ? Runner.DeltaTime : -1f)}). Sử dụng fallback {deltaTime} cho player {PlayerModel.playerId}");
            _hasLoggedDeltaTimeFallback = true;
        }

        return deltaTime;
    }
#else
    private float GetAuthoritativeDeltaTime()
    {
        return Runner != null && Runner.IsRunning ? Runner.DeltaTime : Time.deltaTime;
    }
#endif

    private List<Vector3> BuildMovementPath(Vector3 startPosition, Vector3 desiredTarget, BoxCollider area, float keepDistance, float fixedY)
    {
        var path = new List<Vector3>();

        if (area == null)
        {
            desiredTarget.y = fixedY;
            path.Add(desiredTarget);
            return path;
        }

        Vector3 areaCenter = area.transform.position + area.center;
        Vector3 halfSize = area.size * 0.5f;
        Vector2 rectMin = new Vector2(areaCenter.x - halfSize.x - keepDistance, areaCenter.z - halfSize.z - keepDistance);
        Vector2 rectMax = new Vector2(areaCenter.x + halfSize.x + keepDistance, areaCenter.z + halfSize.z + keepDistance);

        const float epsilon = 0.01f;
        float leftEdge = rectMin.x - epsilon;
        float rightEdge = rectMax.x + epsilon;
        float bottomEdge = rectMin.y - epsilon;
        float topEdge = rectMax.y + epsilon;

        Vector3 adjustedStart = new Vector3(startPosition.x, fixedY, startPosition.z);
        Vector3 adjustedTarget = new Vector3(desiredTarget.x, fixedY, desiredTarget.z);

        adjustedTarget = AdjustPointOutside(adjustedTarget, leftEdge, rightEdge, bottomEdge, topEdge, fixedY);

        if (IsPointInsideRect(ToVector2XZ(adjustedStart), rectMin, rectMax))
            adjustedStart = AdjustPointOutside(adjustedStart, leftEdge, rightEdge, bottomEdge, topEdge, fixedY);

        Vector2 start2 = ToVector2XZ(adjustedStart);
        Vector2 target2 = ToVector2XZ(adjustedTarget);

        if (!SegmentIntersectsRect(start2, target2, rectMin, rectMax))
        {
            path.Add(adjustedTarget);
            return path;
        }

        var candidates = new List<List<Vector3>>();

        AddHorizontalPathCandidate(candidates, adjustedStart, adjustedTarget, rectMin, rectMax, leftEdge, fixedY);
        AddHorizontalPathCandidate(candidates, adjustedStart, adjustedTarget, rectMin, rectMax, rightEdge, fixedY);
        AddVerticalPathCandidate(candidates, adjustedStart, adjustedTarget, rectMin, rectMax, bottomEdge, fixedY);
        AddVerticalPathCandidate(candidates, adjustedStart, adjustedTarget, rectMin, rectMax, topEdge, fixedY);

        if (candidates.Count == 0)
        {
            path.Add(adjustedTarget);
            return path;
        }

        List<Vector3> bestPath = candidates[0];
        float bestDistance = ComputePathDistance(adjustedStart, bestPath);

        for (int i = 1; i < candidates.Count; i++)
        {
            float distance = ComputePathDistance(adjustedStart, candidates[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPath = candidates[i];
            }
        }

        path.AddRange(bestPath);
        return path;
    }

    private static void AddHorizontalPathCandidate(List<List<Vector3>> candidates, Vector3 start, Vector3 target, Vector2 rectMin, Vector2 rectMax, float edgeX, float fixedY)
    {
        Vector3 waypoint1 = new Vector3(edgeX, fixedY, start.z);
        Vector3 waypoint2 = new Vector3(edgeX, fixedY, target.z);

        if (IsSegmentClear(start, waypoint1, rectMin, rectMax) &&
            IsSegmentClear(waypoint1, waypoint2, rectMin, rectMax) &&
            IsSegmentClear(waypoint2, target, rectMin, rectMax))
        {
            candidates.Add(new List<Vector3> { waypoint1, waypoint2, target });
        }
    }

    private static void AddVerticalPathCandidate(List<List<Vector3>> candidates, Vector3 start, Vector3 target, Vector2 rectMin, Vector2 rectMax, float edgeZ, float fixedY)
    {
        Vector3 waypoint1 = new Vector3(start.x, fixedY, edgeZ);
        Vector3 waypoint2 = new Vector3(target.x, fixedY, edgeZ);

        if (IsSegmentClear(start, waypoint1, rectMin, rectMax) &&
            IsSegmentClear(waypoint1, waypoint2, rectMin, rectMax) &&
            IsSegmentClear(waypoint2, target, rectMin, rectMax))
        {
            candidates.Add(new List<Vector3> { waypoint1, waypoint2, target });
        }
    }

    private static float ComputePathDistance(Vector3 start, List<Vector3> path)
    {
        float distance = 0f;
        Vector3 current = start;
        for (int i = 0; i < path.Count; i++)
        {
            distance += Vector3.Distance(current, path[i]);
            current = path[i];
        }
        return distance;
    }

    private static Vector3 AdjustPointOutside(Vector3 point, float leftEdge, float rightEdge, float bottomEdge, float topEdge, float fixedY)
    {
        Vector3 adjusted = point;
        adjusted.y = fixedY;

        if (adjusted.x > leftEdge && adjusted.x < rightEdge)
        {
            float distLeft = Mathf.Abs(adjusted.x - leftEdge);
            float distRight = Mathf.Abs(rightEdge - adjusted.x);
            adjusted.x = distLeft < distRight ? leftEdge : rightEdge;
        }

        if (adjusted.z > bottomEdge && adjusted.z < topEdge)
        {
            float distBottom = Mathf.Abs(adjusted.z - bottomEdge);
            float distTop = Mathf.Abs(topEdge - adjusted.z);
            adjusted.z = distBottom < distTop ? bottomEdge : topEdge;
        }

        return adjusted;
    }

    private static bool IsSegmentClear(Vector3 a, Vector3 b, Vector2 rectMin, Vector2 rectMax)
    {
        Vector2 a2 = ToVector2XZ(a);
        Vector2 b2 = ToVector2XZ(b);

        if ((a2 - b2).sqrMagnitude <= 0.0001f)
            return !IsPointInsideRect(a2, rectMin, rectMax);

        return !SegmentIntersectsRect(a2, b2, rectMin, rectMax);
    }

    private static Vector2 ToVector2XZ(Vector3 value)
    {
        return new Vector2(value.x, value.z);
    }

    private static bool IsPointInsideRect(Vector2 point, Vector2 rectMin, Vector2 rectMax)
    {
        return point.x > rectMin.x && point.x < rectMax.x && point.y > rectMin.y && point.y < rectMax.y;
    }

    private static bool SegmentIntersectsRect(Vector2 start, Vector2 end, Vector2 rectMin, Vector2 rectMax)
    {
        if (Mathf.Max(start.x, end.x) < rectMin.x || Mathf.Min(start.x, end.x) > rectMax.x ||
            Mathf.Max(start.y, end.y) < rectMin.y || Mathf.Min(start.y, end.y) > rectMax.y)
        {
            return false;
        }

        if (IsPointInsideRect(start, rectMin, rectMax) || IsPointInsideRect(end, rectMin, rectMax))
            return true;

        if (CheckVerticalIntersection(start, end, rectMin.x, rectMin.y, rectMax.y))
            return true;
        if (CheckVerticalIntersection(start, end, rectMax.x, rectMin.y, rectMax.y))
            return true;
        if (CheckHorizontalIntersection(start, end, rectMin.y, rectMin.x, rectMax.x))
            return true;
        if (CheckHorizontalIntersection(start, end, rectMax.y, rectMin.x, rectMax.x))
            return true;

        return false;
    }

    private static bool CheckVerticalIntersection(Vector2 p1, Vector2 p2, float x, float minY, float maxY)
    {
        if ((p1.x < x && p2.x < x) || (p1.x > x && p2.x > x))
            return false;

        if (Mathf.Abs(p1.x - p2.x) <= 0.0001f)
        {
            if (Mathf.Abs(p1.x - x) > 0.0001f)
                return false;

            float minSegmentY = Mathf.Min(p1.y, p2.y);
            float maxSegmentY = Mathf.Max(p1.y, p2.y);
            return maxSegmentY >= minY && minSegmentY <= maxY;
        }

        float t = (x - p1.x) / (p2.x - p1.x);
        if (t < 0f || t > 1f)
            return false;

        float y = Mathf.Lerp(p1.y, p2.y, t);
        return y >= minY && y <= maxY;
    }

    private static bool CheckHorizontalIntersection(Vector2 p1, Vector2 p2, float z, float minX, float maxX)
    {
        if ((p1.y < z && p2.y < z) || (p1.y > z && p2.y > z))
            return false;

        if (Mathf.Abs(p1.y - p2.y) <= 0.0001f)
        {
            if (Mathf.Abs(p1.y - z) > 0.0001f)
                return false;

            float minSegmentX = Mathf.Min(p1.x, p2.x);
            float maxSegmentX = Mathf.Max(p1.x, p2.x);
            return maxSegmentX >= minX && minSegmentX <= maxX;
        }

        float t = (z - p1.y) / (p2.y - p1.y);
        if (t < 0f || t > 1f)
            return false;

        float x = Mathf.Lerp(p1.x, p2.x, t);
        return x >= minX && x <= maxX;
    }

    //Hàm này để xử lý di chuyển nhanh đến 1 vị trí không hiệu ứng

    private bool TryResolveHorizontalLookRotation(Vector3 originPosition, Transform lookAtTarget, out Quaternion rotation)
    {
        rotation = transform.rotation;

        if (lookAtTarget == null)
            return false;

        Vector3 direction = lookAtTarget.position - originPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        return true;
    }

#if UNITY_SERVER
    private void StartTurnTimerIfReadyToShoot(CharacterAnimState state, string reason)
    {
        if (state != CharacterAnimState.SitToShoot)
            return;

        NetworkObjectManager.Instance?.StartTurnTimerWhenPlayerReadyToShoot(PlayerModel.playerId, reason);
    }
#endif

    public IEnumerator TeleportToTarget(
        Vector3 targetPosition,
        Transform lookAtTarget = null,
        CharacterAnimState finalAnimState = CharacterAnimState.SitToShoot,
        bool holdBallAfterArrival = true,
        float settleDelaySeconds = 1f)
    {
        if (!HasStateAuthority)
            yield break;

        float groundedY = GetGroundHeightAt(targetPosition, targetPosition.y);
        Vector3 groundedTargetPosition = new Vector3(targetPosition.x, groundedY, targetPosition.z);
        var controller = gameObject.GetComponent<NetworkCharacterController>();

        Quaternion targetRotation = transform.rotation;
        if (TryResolveHorizontalLookRotation(groundedTargetPosition, lookAtTarget, out targetRotation))
        {
            TargetRotation = targetRotation;
            transform.rotation = targetRotation;
        }

        controller.Teleport(groundedTargetPosition, targetRotation);
        TargetPosition = groundedTargetPosition;
        CaptureServerPosition();
        CurrentAnimState = finalAnimState;
#if UNITY_SERVER
        StartTurnTimerIfReadyToShoot(finalAnimState, "teleport_to_target");
#endif

        if (settleDelaySeconds > 0f)
            yield return new WaitForSeconds(settleDelaySeconds);

        if (!holdBallAfterArrival)
            yield break;

        var scriptBall = GetActiveBall();
        if (scriptBall == null)
        {
            Debug.LogWarning($"⚠️ Không tìm thấy bi cho player {PlayerModel.playerId}");
            yield break;
        }

        // Tránh race-condition: nếu trong lúc coroutine teleport còn đang chờ mà lượt bắn đã bắt đầu,
        // không được kéo bi về trạng thái cầm tay nữa vì sẽ làm kẹt flow chờ BallStop.
        if (scriptBall.hasBeenShoot == 1)
        {
            Debug.LogWarning($"⚠️ [SERVER] Bỏ qua snap bi sau teleport cho player {PlayerModel.playerId} vì bi đã được bắn.");
            yield break;
        }

        // Đảm bảo hasBeenShoot = 0 trước khi bật IsHolding (trường hợp ball vừa bắn xong)
        scriptBall.hasBeenShoot = 0;
        scriptBall.IsHolding = 1;

#if UNITY_SERVER
        // Bot không có client input → server phải tự gán FingerPos từ vị trí player body
        // và snap ball về vị trí đó ngay lập tức
        var botCtrl = BotPlayerController.Instance;
        if (botCtrl != null && botCtrl.IsBotPlayer(PlayerModel.playerId))
        {
            FingerPos = transform.position;
            scriptBall.SnapToOwnerFinger();
            Debug.Log($"[HOST][TeleportBotBallSnap] pid={PlayerModel.playerId} ball snapped to {scriptBall.HeldPosition} playerPos={transform.position}");
        }
#endif
        yield break;
    }

//    public void RequestRotateSightingPoint(Vector3 lookAtTarget)
//    {
//        if (HasStateAuthority)
//            RPC_RequestRotateSightingPoint(lookAtTarget);

//#if !UNITY_SERVER
//        if (Object != null && Object.HasInputAuthority)
//            MovePlayerOnlineHandler.Instance?.RotateSightingPoint(lookAtTarget);
//#endif
//    }

    public void RPC_RequestRotateSightingPoint(Vector3 lookAtTarget)
    {
        if (!HasStateAuthority)
            return;

        if (Object == null || !Object.IsValid)
            return;

        var head = GetHeadTransformSafe();

        Vector3 origin = head != null
            ? head.position
            : transform.position;

        Vector3 lookDirection = lookAtTarget - origin;
        if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
            return;

        Vector3 horizontalDirection = new Vector3(lookDirection.x, 0f, lookDirection.z);
        Quaternion bodyRotation = transform.rotation;

        if (horizontalDirection.sqrMagnitude > Mathf.Epsilon)
        {
            bodyRotation = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
            TargetRotation = bodyRotation;
            transform.rotation = bodyRotation;
        }

        Quaternion lookRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        float pitch = lookRotation.eulerAngles.x;
        if (pitch > 180f)
            pitch -= 360f;
        //pitch = Mathf.Clamp(pitch, -maxHeadPitch, maxHeadPitch);

        HeadRotation = Quaternion.Euler(pitch, 0f, 0f);
        UpdatePointPosition(pitch);

        float currentYaw = Mathf.Repeat(transform.rotation.eulerAngles.y, 360f);
        if (Object.InputAuthority == PlayerRef.None)
        {
            Debug.LogWarning($"⚠️ Không thể đồng bộ xoay vì player {PlayerModel.playerId} không có InputAuthority.");
            return;
        }
        RPC_SyncRotateSightingPoint(currentYaw, pitch);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_SyncRotateSightingPoint(float yaw, float pitch)
    {
        ClientGameplayBridge.PlayerMovement.ApplyServerRotation(yaw, pitch);
    }

    private void ApplyShotRequest(ShotParams shot)
    {
#if UNITY_SERVER
        int playerId = ResolvePlayerIdForServerAction();
        var host = GameSessionNetWork_Host.Instance;
        if (host != null && !host.CanAcceptPlayerShotAction(playerId))
        {
            Debug.LogWarning($"Bỏ qua yêu cầu bắn của player {playerId} vì lượt hiện tại không còn hợp lệ.");
            return;
        }
#endif

        var activeBall = GetActiveBall();
        if (activeBall == null)
        {
            Debug.LogWarning("Không tìm thấy bi đang hoạt động để thực thi cú bắn");
            return;
        }

        activeBall.ShotData = shot;
    }

    public void RequestShotExecution(ShotParams shot)
    {
        if (!CanAccessNetworkedState)
        {
            Debug.LogWarning("Không thể gửi yêu cầu bắn vì trạng thái mạng không hợp lệ");
            return;
        }

        if (HasStateAuthority)
        {
            ApplyShotRequest(shot);
            return;
        }

        if (HasInputAuthority)
        {
            RPC_RequestShot(shot);
            return;
        }

        Debug.LogWarning("Đối tượng hiện tại không có quyền gửi yêu cầu bắn");
    }



    public override void FixedUpdateNetwork()
    {
        EnforceServerLockedPosition();
        RefreshDestroyedPresentationState();

        if (NetworkObjectManager.Instance.IsGameEnded)
            return;
        if (IsSpawned == 0)
            return;
        if (IsMarkedDestroyed)
        {
            CaptureServerPosition();
            return;
        }

        // === only on server ===
        if (HasStateAuthority)
        {
            if (GetInput(out PlayerInputData input))
            {
                if (input.hasFingerPosition)
                {
                    FingerPos = input.fingerPosition;
                   // if (FingerPosition != null)
                   //   FingerPosition.position = input.fingerPosition;
                }
                if (input.hasFingerRigPower)
                {
                    FingerRigPower = Mathf.Clamp01(input.fingerRigPower);
                }
                else
                {
                    FingerRigPower = 0f;
                }
                //else if (FingerPosition != null)
                //{
                //    FingerPos = FingerPosition.position;
                //}
                // Cập nhật góc quay thân và đầu
                float desiredYaw = input.yaw;

                if (input.hasYawInput)
                    TargetRotation = Quaternion.Euler(0f, desiredYaw, 0f);
                HeadRotation = Quaternion.Euler(input.pitch, 0f, 0f);
                UpdatePointPosition(input.pitch);

                if (input.shotRequested)
                {
                    ApplyShotRequest(input.shotParams);
                }

                if (input.animStateRequested)
                {
                    ApplyRequestedAnimState(input.animState);
                }

                if (input.moveHorizontal != 0 && CanApplyHorizontalMoveInput())
                {
                    MoveHorizontal(Mathf.Clamp(input.moveHorizontal, -1, 1));
                }
            }
            // Xoay thân theo yaw vì object này là network object nên khi thay đổi rotation nó sẽ tự sync cho toàn bộ client
            transform.rotation = Quaternion.RotateTowards(transform.rotation, TargetRotation, 720 * Runner.DeltaTime);

            // Di chuyển nếu đang chạy hoặc đang kích hoạt Banana Jump
            if (CurrentAnimState == CharacterAnimState.Running || IsBananaJumpActive)
            {
                Vector3 currentPosition = transform.position;
                Vector3 nextPosition = Vector3.MoveTowards(currentPosition, TargetPosition, moveSpeed * Runner.DeltaTime);
                float baseGroundY = GetGroundHeightAt(currentPosition, currentPosition.y);
#if UNITY_SERVER
                float targetY = IsBananaJumpActive
                    ? UpdateBananaJumpHeight(baseGroundY, Runner.DeltaTime)
                    : baseGroundY;
#else
                float targetY = baseGroundY;
#endif
                nextPosition.y = targetY;
                ApplyAuthoritativePosition(nextPosition);
            }
            else
            {
                CaptureServerPosition();
            }

            // Giữ trạng thái nhảy vỏ chuối cho tới khi hoàn tất để tránh bị tắt sớm khiến nhân vật đứng im giữa chừng.
            if (IsBananaJumpActive && CurrentAnimState != CharacterAnimState.Running && CurrentAnimState != CharacterAnimState.Jumping)
                IsBananaJumpActive = false;

            UpdateLookTarget();
            // xử lý trượt vỏ chuối
            ProcessPendingBananaCollisions();
        }
        else
        {
            //if (FingerPosition != null)
            //    FingerPosition.position = FingerPos;
            //// Client khác tái hiện lại vị trí ngắm đã sync
            //if (PointPosition != null)
            //    PointPosition.position = PointPosToSync;
        }
    }
#endif


    private bool CanApplyHorizontalMoveInput()
    {
#if UNITY_SERVER
        if (PlayerId == 0 || IsMarkedDestroyed)
            return false;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return false;

        if (manager.StatusLoading == StatusLoadingGame.isExam)
            return PlayerModel.statusPlayer == StatusPlayer.ShootExam;

        bool isTurnPhase = manager.StatusLoading == StatusLoadingGame.StartTurn ||
                           manager.StatusLoading == StatusLoadingGame.NextTurn ||
                           manager.StatusLoading == StatusLoadingGame.ContinueTurn;
        return isTurnPhase && manager.IsYourTurn(PlayerId);
#else
        return false;
#endif
    }

    public void MoveHorizontal(int direction)
    {
#if UNITY_SERVER
        if (direction == 0) return;
 
        if (PlayerId == 0)
            return;

        var host = GameSessionNetWork_Host.Instance;
        if (host == null)
            return;

        float leftLimit;
        float rightLimit;

        if (PlayerModel.statusPlayer == StatusPlayer.ShootExam)
        {
            if (host.ExamMain == null)
                return;

            float moveLimit = host.ExamHorizontalMoveLimit;
            leftLimit = host.ExamMain.position.x - moveLimit;
            rightLimit = host.ExamMain.position.x + moveLimit;
        }
        else
        {
            if (host.StartPointMain == null)
                return;

            float moveLimit = host.StartPointHorizontalMoveLimit;
            leftLimit = host.StartPointMain.position.x - moveLimit;
            rightLimit = host.StartPointMain.position.x + moveLimit;
        }

        Vector3 newPos = transform.position + Vector3.right * direction * moveSpeed * Runner.DeltaTime;
        newPos.x = Mathf.Clamp(newPos.x, leftLimit, rightLimit);

        newPos.y = GetGroundHeightAt(newPos, newPos.y);

        if (HasStateAuthority)
            TargetPosition = newPos;
        ApplyAuthoritativePosition(newPos);
#endif
    }

    public void SetConstraintActive(bool active)
    {
        //var constraint = GameInitializer.Instance != null
        //    ? GameInitializer.Instance.GetAimConstraint(PlayerModel.playerId)
        //    : aimConstraint;
        //có thể điều khiển được vì object này do mỗi client tự tạo ở local và gắn vào
#if !UNITY_SERVER
        var constraint = AimConstraint;
        if (constraint != null)
            constraint.weight = active ? 1f : 0f;
#endif
    }



    void PlayAnim(string clipName)
    {
#if !UNITY_SERVER
        var animator = CurrentAnimator;
        if (animator != null && lastPlayedAnim != clipName)
        {
            animator.CrossFade(clipName, 0.1f);
            lastPlayedAnim = clipName;
        }
#endif
    }

#if !UNITY_SERVER
    private void PlayAnimWithFallbacks(params string[] clipNames)
    {
        var animator = CurrentAnimator;
        if (animator == null || clipNames == null)
            return;

        foreach (var clipName in clipNames)
        {
            if (string.IsNullOrWhiteSpace(clipName))
                continue;

            if (AnimatorHasState(animator, clipName))
            {
                PlayAnim(clipName);
                return;
            }
        }
    }

    private static bool AnimatorHasState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        int stateHash = Animator.StringToHash(stateName);
        for (int layerIndex = 0; layerIndex < animator.layerCount; layerIndex++)
        {
            if (animator.HasState(layerIndex, stateHash))
                return true;
        }

        return false;
    }
#endif

    private void PlaySlipAnimationImmediate()
    {
#if !UNITY_SERVER
        var animator = CurrentAnimator;
        if (animator == null)
            return;

        lastPlayedAnim = null;
        int fallingAnimIndex = Random.Range(1, 3);
        animator.CrossFadeInFixedTime($"Falling_{fallingAnimIndex}", 0.05f);
#endif
    }

#if !UNITY_SERVER
    private void TriggerSlipVibration()
    {
        if (!HasInputAuthority)
            return;

        if (_slipVibrationRoutine != null)
            StopCoroutine(_slipVibrationRoutine);

        _slipVibrationRoutine = StartCoroutine(PlaySlipVibrationSequence());
    }

    private IEnumerator PlaySlipVibrationSequence()
    {
        int burstCount = Random.Range(2, 4);

        for (int i = 0; i < burstCount; i++)
        {
            float force = Random.Range(0.9f, 2.4f);
            VibrationManager.Instance?.PlayImpact(HitSurface.Rock, force);

            float pause = Random.Range(0.06f, 0.18f);
            yield return new WaitForSeconds(pause);
        }

        if (Random.value > 0.6f)
        {
            yield return new WaitForSeconds(Random.Range(0.08f, 0.16f));
            VibrationManager.Instance?.PlayImpact(HitSurface.Tree, Random.Range(0.5f, 1.3f));
        }

        _slipVibrationRoutine = null;
    }
#endif

 
    public void UpdatePointPosition(float pitch)
    {
        if (!TryGetPointAimOrigin(out Vector3 origin))
            return;

        Quaternion yawRotation = transform.rotation;
        Quaternion pitchRotation = Quaternion.AngleAxis(pitch, Vector3.right);
        Vector3 offset = yawRotation * (pitchRotation * Vector3.forward * pointAimDistance);
        Vector3 targetPos = origin + offset;

        if (HasStateAuthority && (targetPos - PointPosToSync).sqrMagnitude > 0.0001f)
            PointPosToSync = targetPos;

#if !UNITY_SERVER
        ApplyLocalPointPosition(targetPos);
#endif

    }
 

    private void UpdateLookTarget()
    {
        if (!TryGetPointAimOrigin(out _))
            return;

        if (HasStateAuthority)
        {
            float pitch = HeadRotation.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            UpdatePointPosition(pitch);
        }
        //else
        //{
        //    // Client không có quyền chỉ cần dùng sync từ server
        //    PointPosition.position = PointPosToSync;
        //}
    }



    // Gọi từ Animation Event ở giây thứ 2
    //public void PauseAtSit()
    //{
    //    if (!HasInputAuthority) return;
    //    Debug.Log("Animation Ngưng lại ở bước ngồi");
    //    currentAnimator.speed = 0;
    //    isPausedAni = 1;
    //}
    //public void ContinueAni(Animator ani)
    //{
    //    if (isPausedAni == 1)
    //    {
    //        Debug.Log("Animation Bắn");
    //        ani.speed = 1;
    //        isPausedAni = 0;
    //    }    
    //}
    //public void TriggerReleaseBall()
    //{
    //    GameSessionNetWork_Host.Instance.ReleaseBall();
    //}
    //Đây là hàm mỗi client đều gọi ra để điều khiển animation tại local
#if !UNITY_SERVER
    public override void Render()
    {
        float deltaTime = Runner != null ? Runner.DeltaTime : Time.deltaTime;

        EnsurePlayerVisualInstance();
        ResolveAnimatorFromVisual();
        RefreshDestroyedPresentationState();

        if (ShouldHideDestroyedVisuals())
        {
            StopFootstepAudio();
            return;
        }

        UpdateFootstepAudioForAnimState();

        if (!HasInputAuthority)
            HandlePointPosToSyncUpdate();

        // Đảm bảo client không quyền vẫn hiển thị đúng vị trí đầu nhìn
       // if (PointPosition != null)
           // PointPosition.position = PointPosToSync;

        //Client có thể gán local mà kh cần quyền
        //if (PointPosition != null)
        // PointPosition.localPosition = PointPosToSync;
        // Đồng bộ tham chiếu constraint từ GameInitializer nếu có
        //if (GameInitializer.Instance != null)
        //{
        //    var c = GameInitializer.Instance.GetAimConstraint(PlayerModel.playerId);
        //    if (aimConstraint != null && c != null) 
        //        aimConstraint = c;
        //}
        // Đồng bộ animation khi trạng thái thay đổi
        if (CurrentAnimState != lastAnimState)
        {
            lastPlayedAnim = null;
            if (CurrentAnimState != CharacterAnimState.Running)
                _hasRefreshedSkillsForRunningState = false;
            switch (CurrentAnimState)
            {
                case CharacterAnimState.None:
                    SetConstraintActive(false);
                    PlayAnim("Sitting Idle");
                    break;
                case CharacterAnimState.Idle:
                    SetConstraintActive(false);
                    //PlayAnim("Angry");
                    var idleAnimator = CurrentAnimator;
                    if (idleAnimator != null)
                        AnimatorController.Instance?.SetWaitingAnimation(idleAnimator, IdleAnimIndex);
                    break;
                case CharacterAnimState.Running:
                    SetConstraintActive(false);
                    // Trạng thái chạy chỉ nên phát hoạt ảnh chạy bình thường.
                    // Tránh dùng clip nhảy ở đây để không bị trùng với trạng thái Jumping.
                    PlayAnim("Running");
                    if (!_hasRefreshedSkillsForRunningState)
                    {
                        ClientGameplayBridge.Skill.ShowSkillList();
                        _hasRefreshedSkillsForRunningState = true;
                    }
                    break;
                case CharacterAnimState.Jumping:
                    SetConstraintActive(false);
                    // Khi người chơi bấm nhảy, ưu tiên dùng clip nhảy chuối nếu có,
                    PlayAnim("StandingJump");
                    break;
                case CharacterAnimState.SitToShoot:
                    SetConstraintActive(true);
                    PlayAnim("SitToShoot");
                    ClientGameplayBridge.Skill.ShowSkillList();
                    // PlayAnim("CrouchedToShoot");
                    break;
                case CharacterAnimState.Shoot:
                    //SetConstraintActive(false);
                    PlayAnim("Shoot");
                    break;

                //case CharacterAnimState.Changeball:
                //    PlayAnim("Changeball");
                //    break;
                case CharacterAnimState.StandingUp:
                    SetConstraintActive(false);
                    PlayAnim("StandingJump");
                    break;
                //case CharacterAnimState.Shooting:
                //    PlayAnim("Shoot");
                //    break;
                case CharacterAnimState.Sleeping:
                    SetConstraintActive(false);
                    PlayAnim("OldManIdle");
                    break;

                case CharacterAnimState.Slipping:
                    SetConstraintActive(false);
                    PlaySlipAnimationImmediate();
                    TriggerSlipVibration();
                    break;
                case CharacterAnimState.BlowWind:
                    SetConstraintActive(false);
                    PlayAnim("BlowWind");
                    break;
                case CharacterAnimState.Hu:
                    SetConstraintActive(false);
                    PlayAnim("Hu");
                    break;
                case CharacterAnimState.PickingUp:
                    SetConstraintActive(false);
                    PlayAnim("PickingUp");
                    break;
                // case CharacterAnimState.EmoteLaugh:
                //     SetConstraintActive(false);
                //     PlayAnimWithFallbacks("Laugh");
                //     break;
                // case CharacterAnimState.EmoteTaunt:
                //     SetConstraintActive(false);
                //     PlayAnimWithFallbacks("Taunt");
                //     break;
                case CharacterAnimState.EmoteAngry:
                    SetConstraintActive(false);
                    PlayAnimWithFallbacks("Angry");
                    break;
                case CharacterAnimState.EmoteClap:
                    SetConstraintActive(false);
                    PlayAnimWithFallbacks("Clapping");
                    break;
                // case CharacterAnimState.EmoteSad:
                //     SetConstraintActive(false);
                //     PlayAnimWithFallbacks("Sad");
                //     break;
                case CharacterAnimState.HurtAfterSlip:
                    SetConstraintActive(false);
                    PlayAnimWithFallbacks("Hurting");
                    break;
                case CharacterAnimState.LoseEmotion:
                    SetConstraintActive(false);
                    PlayAnimWithFallbacks("Defeated");
                    TryFollowLocalDefeatedPlayer();
                    break;


            }
            lastAnimState = CurrentAnimState;
        }
        else if (CurrentAnimState == CharacterAnimState.Running && lastPlayedAnim != "Running")
        {
            // Đảm bảo animation chạy được bật lại nếu bị giữ ở clip kỹ năng trước đó.
            SetConstraintActive(false);
            PlayAnim("Running");
        }

        UpdateFingerRig(deltaTime);
        // currentAnimator.SetBool("IsMoving", IsMoving);
        // currentAnimator.SetBool("IsShitdown", IsShitdown);
        // currentAnimator.SetBool("IsStanding", IsStanding);
        // if(IsWaiting)
        //   AnimatorController.Instance.SetWaitingAnimation(currentAnimator);
        if (_playerModelVisualComponent != null)
        {
            var modelTransform = _playerModelVisualComponent.transform;
            modelTransform.SetPositionAndRotation(transform.position, transform.rotation);
            _lastTargetPosition = transform.position;
            _smoothedRenderVelocity = Vector3.zero;
            _hasRenderSample = true;
            return;
        }

        var renderTransform = GetVisualRenderTransform();
        if (renderTransform == null)
        {
            _lastTargetPosition = TargetPosition;
        }

        // Tween đầu xoay mượt (hướng mặt)
        //if (headTransform.localRotation != HeadRotation)
        //{
        //    // Dừng tween cũ nếu có
        //    _headTween?.Kill();

        //    // Tạo tween mới và CÀI ĐẶT UPDATE CHO NÓ
        //    _headTween = headTransform.DOLocalRotateQuaternion(HeadRotation, 0.1f)
        //                              .SetEase(Ease.OutQuad)
        //                              .SetUpdate(UpdateType.Late); // <--- THÊM DÒNG NÀY
        //}
        //if (headTransform.localRotation != HeadRotation)
        //{
        //    _headTween?.Kill();
        //    _headTween = headTransform.DOLocalRotateQuaternion(HeadRotation, 0.1f)
        //                              .SetEase(Ease.OutQuad)
        //                              .SetUpdate(UpdateType.Late);
        //}



        UpdateVisualInterpolation(renderTransform, deltaTime);
    }

    private void UpdateFootstepAudioForAnimState()
    {
        bool shouldPlay = CurrentAnimState == CharacterAnimState.Running
            && !IsBananaJumpActive
            && !IsMarkedDestroyed
            && gameObject.activeInHierarchy
            && HasFootstepMovementTarget();

        if (shouldPlay)
            StartFootstepAudio();
        else
            StopFootstepAudio();
    }

    private void StartFootstepAudio()
    {
        if (_isFootstepLoopActive)
            return;

        if (SoundManager.Instance == null)
            return;

        ClientGameplayBridge.Sound.StartFootstepLoop(gameObject);
        _isFootstepLoopActive = true;
    }

    private void StopFootstepAudio()
    {
        if (!_isFootstepLoopActive)
            return;

        ClientGameplayBridge.Sound.StopFootstepLoop(gameObject);
        _isFootstepLoopActive = false;
    }

    private bool HasFootstepMovementTarget()
    {
        Vector3 delta = TargetPosition - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude > 0.0025f;
    }
#endif
#if !UNITY_SERVER
    private void ApplyLocalPointPosition(Vector3 targetPos)
    {
        var point = PointPosition;
        if (point != null)
            point.position = targetPos;
    }

    private void HandlePointPosToSyncUpdate()
    {
        if (HasStateAuthority || HasInputAuthority)
            return;

        // Client không có quyền: lưu mẫu mới khi server gửi khác biệt đáng kể
        if (!_hasPointPosSample || (_lastPointPosToSyncValue - PointPosToSync).sqrMagnitude > 0.0001f)
        {
            _lastPointPosToSyncValue = PointPosToSync;
            _hasPointPosSample = true;
            _hasPointPosToSyncValue = true;
        }

        // Áp dụng ngay vị trí head look từ dữ liệu mạng để hiển thị đồng bộ
        ApplyPointPositionFromNetwork();
    }

    private void ApplyPointPositionFromNetwork()
    {
        if (HasInputAuthority)
            return;

        var point = PointPosition;
        if (point == null)
            return;

        if (!_hasPointPosToSyncValue)
        {
            if (!TryGetPointAimOrigin(out Vector3 compareOrigin))
                return;

            if ((PointPosToSync - compareOrigin).sqrMagnitude <= 0.0001f)
                return;

            _hasPointPosToSyncValue = true;
        }

        Vector3 target = PointPosToSync;
        if ((point.position - target).sqrMagnitude > 0.0001f)
            point.position = target;
    }
#endif
    //void LateUpdate()
    //{
    //    if (!HasStateAuthority && PointPosition != null)
    //        PointPosition.localPosition = PointPosToSync;
    //}
    void LateUpdate()
    {
        //if (!HasStateAuthority && PointPosition != null && GameManagerNetWork.Instance?.serverRPC != null)
        //    PointPosition.position = PointPosToSync;

    }

    private void OnDestroy()
    {
#if !UNITY_SERVER
        StopFootstepAudio();
#endif
        hasSpawnedNetworkState = false;
        _playerIndexCache = -1;
#if UNITY_SERVER
        ServerHandlers.Remove(this);
#endif
    }
#if UNITY_SERVER
    private void OnTriggerEnter(Collider other)
    {
        RegisterPendingBananaCollision(other);
    }
    private void RegisterPendingBananaCollision(Collider other)
    {
        // Bỏ qua va chạm không phải bẫy chuối để tránh thêm thừa.
        if (!IsBananaCollider(other))
            return;

        // Lưu collider lại để xử lý ở cuối frame (sau khi server chắc chắn có quyền state).
        if (!_pendingBananaCollisions.Contains(other))
            _pendingBananaCollisions.Add(other);
    }

    private void HandleServerBananaCollision(Collider other)
    {
        // Đề phòng collider bị huỷ giữa lúc chờ xử lý.
        if (other == null)
            return;

        // Chỉ quan tâm collider có tag "BananaSpawn".
        if (!IsBananaCollider(other))
            return;

        // Bảo vệ: chỉ server (state authority) mới được phép xử lý logic bẫy.
        if (!CanAccessNetworkedState || Object == null || !Object.HasStateAuthority)
            return;

        // Lấy host session rồi chuyển sự kiện té chuối sang đó xử lý.
        var host = GameSessionNetWork_Host.Instance;
        if (host == null)
            return;

        host.HandleBananaSlip(this, other.gameObject);
    }

    private void ProcessPendingBananaCollisions()
    {
        // Nếu không có va chạm nào được ghi nhận thì thoát sớm.
        if (_pendingBananaCollisions.Count == 0)
            return;

        if (IsBananaJumpActive)
        {
            _pendingBananaCollisions.Clear();
            return;
        }

        // Duyệt từng collider đã lưu rồi chuyển cho server xử lý.
        for (int i = 0; i < _pendingBananaCollisions.Count; i++)
        {
            var collider = _pendingBananaCollisions[i];
            // Collider có thể đã bị disable/null do despawn trong frame hiện tại.
            if (collider == null)
                continue;

            HandleServerBananaCollision(collider);
        }

        // Dọn danh sách để frame sau không xử lý lặp lại.
        _pendingBananaCollisions.Clear();
    }
#endif

    private static bool IsBananaCollider(Collider other)
    {
        if (other == null)
            return false;

        return other.CompareTag("BananaSpawn");
    }

 
    #region [======================== RPC ======================]
#if UNITY_SERVER
    //[Rpc(RpcSources.All, RpcTargets.InputAuthority)]
    //public void RPC_UpdateAfterShootExam(float distance)
    //{
    //    if (!GameManagerNetWork.Instance.ValidateNetworkObjects())
    //        return;
    //    var model = PlayerModel;
    //    model.statusPlayer = StatusPlayer.StartPoint;
    //    model.distance = distance;
    //    PlayerModel = model;
    //    CurrentAnimState = CharacterAnimState.None;
    //    Debug.Log($"🧍 [CLIENT] {PlayerModel.playerId} đã thi xong");
    //    GameManagerNetWork.Instance.serverRPC.RPC_ExamBallStopped();
    //}
   /* [Rpc(RpcSources.All, RpcTargets.InputAuthority)]
    public void RPC_HandleAfterShoot()
    {
        //if (!HasStateAuthority) return;
        //SoundManager.Instance.StopBallRollingLoop(gameObject);
            HandleAfterShoot();
    }
    void HandleAfterShoot()
    {
        if (!GameManagerNetWork.Instance.ValidateNetworkObjects())
            return;
        Debug.Log("🧍[CLIENT] Tiến hành xử lý game sau khi bắn xong...");
        int ScoreTotal = 0;
        GameSessionNetWork_Host.Instance.isContinueTurn = false;
        bool wasPower = PlayerModel.statusPlayer == StatusPlayer.Power;
        var modelupdate = PlayerModel;
        modelupdate.statusPlayer = StatusPlayer.Normal;
        //step kiểm tra có quậy không ?
       bool isQuay = GameSessionNetWork_Host.Instance.CheckIfBallInRing2P(PlayerModel.playerId);
        if(isQuay)
        {
            if(modelupdate.score > 0)
                GameSessionNetWork_Host.Instance.AddRingBalls(modelupdate.score);

            modelupdate.score = 0;
            modelupdate.isDestroy = true;
            modelupdate.statusPlayer = StatusPlayer.Destroy;
            PlayerModel = modelupdate;

            GameManagerNetWork.Instance.serverRPC.RpcShowMesByUser($"{modelupdate.fullname} đã quậy");
            NetworkObjectManager.Instance.CheckEndGame();
        }
        else
        {        //step check tiêu diệt người chơi khác
            if (wasPower || modelupdate.score > 0)
            {
                GameManagerNetWork.Instance.serverRPC.RpcTogglePowerVFX(PlayerModel.playerId, false);
                ScoreTotal += GameSessionNetWork_Host.Instance.CheckRemovePlayer();
            }
            //step check ăn điểm bắn culi ra khỏi vòng
            ScoreTotal += GameSessionNetWork_Host.Instance.CheckOutBall();
            if (ScoreTotal > 0)
            {
                modelupdate.score += ScoreTotal;
                modelupdate.combo += 1;
                PlayerModel = modelupdate;
                string mess = "+" + ScoreTotal;
                GameManagerNetWork.Instance.serverRPC.RpcAddPowerOtherPlayers(modelupdate.playerId, ScoreTotal * 0.3f);
                GameManagerNetWork.Instance.serverRPC.RpcShowMesByUser(mess);
                GameManagerNetWork.Instance.serverRPC.RpcShowCombo(modelupdate.playerId, modelupdate.combo);
            }
            else
            {
                modelupdate.combo = 0;
                PlayerModel = modelupdate;
                if(!GameSessionNetWork_Host.Instance.isContinueTurn)
                    CircularButton.Instance.SetPower(0.4f);
            }

        }
        NetworkObjectManager.Instance.CheckEndGame();
        if (GameSessionNetWork_Host.Instance.IsGameEnded)
            return;

        if (wasPower)
            GameManagerNetWork.Instance.serverRPC.RpcStopPowerEffect(PlayerModel.playerId);

        if (GameSessionNetWork_Host.Instance.isContinueTurn)
            GameManagerNetWork.Instance.serverRPC.RpcContinueTurn(); // Gọi tiếp tục lượt
        else
        {   //Đổi hoạt ảnh chờ đợi
            if (HasStateAuthority)
                IdleAnimIndex = Random.Range(0, AnimatorController.Instance.IdleAnimationCount);
            CurrentAnimState = CharacterAnimState.Idle;
            GameManagerNetWork.Instance.serverRPC.RPC_NextTurn();
        }
    } */

#endif

    private void OnDisable()
    {
#if !UNITY_SERVER
        StopFootstepAudio();
        // Stop any running tweens when this object is destroyed or disabled
        transform.DOKill();
        //headTransform.DOKill();
        _headTween?.Kill();
        _moveTween = null;
        _rotateTween = null;
        _headTween = null;
#endif
    }

    #endregion
}
public struct PlayerInputData : INetworkInput
{
    public float yaw;   // hướng quay ngang (thân)
    public float pitch; // hướng quay lên/xuống (đầu)
    public int moveHorizontal; // -1/1: di chuyển ngang do client giữ nút trái/phải, server xử lý thật
    public NetworkBool hasYawInput;
    public NetworkBool hasFingerPosition;
    public Vector3 fingerPosition;
    public NetworkBool hasFingerRigPower;
    public float fingerRigPower;
    public NetworkBool shotRequested;
    public ShotParams shotParams;
    public NetworkBool animStateRequested;
    public CharacterAnimState animState;
}
