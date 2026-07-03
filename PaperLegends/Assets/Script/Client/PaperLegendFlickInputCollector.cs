using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PaperLegendFlickInputCollector : MonoBehaviour
{
    public static PaperLegendFlickInputCollector Instance { get; private set; }
    private const string AutoObjectName = "PaperLegendFlickInputCollector";

    [Header("Raycast")]
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private LayerMask characterMask = ~0;
    [SerializeField, Min(0.01f)] private float raycastDistance = 200f;
    [SerializeField] private QueryTriggerInteraction raycastTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Charge")]
    [SerializeField, Min(0.01f)] private float maxChargeSeconds = 0.8f;
    [SerializeField, Min(0f)] private float autoReleaseAfterFullPowerSeconds = 1f;
    [SerializeField, Min(0f)] private float aimDragDeadZonePixels = 12f;
    [SerializeField, Range(0f, 1f)] private float minimumForce01 = 0.18f;
    [SerializeField] private bool useSharedPowerBar = true;

    [Header("Debug")]
    [SerializeField] private bool debugInputLogs = true;

    [Header("Targeted Skill")]
    [SerializeField, Min(0.1f)] private float targetedSkillDefaultRadius = 3.2f;
    [SerializeField, Min(8)] private int targetedSkillIndicatorSegments = 64;
    [SerializeField] private Color targetedSkillIndicatorColor = new Color(0.45f, 0.78f, 1f, 0.95f);
    [SerializeField, Min(0.001f)] private float targetedSkillIndicatorHeightOffset = 0.04f;

    [Header("Hero 10000003 Targeted Charge Skills")]
    [SerializeField, Min(0.1f)] private float gravityWellTargetingRadius = 4f;
    [SerializeField, Min(0.1f)] private float gravityWellChargeDurationSeconds = 1f;
    [SerializeField, Min(0.1f)] private float tidalCataclysmTargetingRadius = 6.5f;
    [SerializeField, Min(0.1f)] private float tidalCataclysmChargeDurationSeconds = 3f;
    [SerializeField, Min(0.05f)] private float tidalCataclysmChargeMovementBreakDistance = 0.25f;
    [SerializeField, Min(0.1f)] private float targetedChargeAimingTimeoutSeconds = 5f;
    [SerializeField] private Vector2 targetedChargeUISize = new Vector2(120f, 120f);

    [Header("Hero 10000002 Skill 3")]
    [SerializeField, Min(0.1f)] private float shoveStunNearbyRadius = 2.8f;
    [SerializeField] private bool showShoveStunHintIcon = true;
    [SerializeField] private Vector3 shoveStunHintWorldOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField, Min(0.05f)] private float shoveStunHintCharacterSize = 0.38f;
    [SerializeField] private Color shoveStunHintColor = new Color(1f, 0.9f, 0.15f, 1f);

    [Header("Hero 10000002 Skill 1")]
    [SerializeField, Min(1f)] private float forwardSlideFullForcePixelsPerSecond = 1600f;

    private bool _isTracking;
    private int _flickSequence;
    private Vector2 _startScreenPosition;
    private Vector2 _currentScreenPosition;
    private Vector3 _contactWorldPosition;
    private Vector3 _contactSurfaceNormal;
    private float _startTime;
    private float _fallbackFullPowerTime = -1f;
    private bool _usingPowerBar;
    private PaperLegendCharacterNetworkHandler _target;
    private PaperLegendCharacterNetworkHandler _shoveStunVictim;
    private bool _isShoveStunSwipe;
    private bool _autoUseShoveStunSkill;
    private bool _isForwardSlideSwipe;
    private bool _isHomingSwordDirectionalSwipe;
    private readonly Dictionary<PaperLegendCharacterNetworkHandler, GameObject> _shoveStunHintIcons = new Dictionary<PaperLegendCharacterNetworkHandler, GameObject>();
    private readonly List<PaperLegendCharacterNetworkHandler> _shoveStunHintRemovalBuffer = new List<PaperLegendCharacterNetworkHandler>();
    private PaperLegendPlayerInputData _pendingInput;
    private bool _hasPendingInput;
    private float _nextRejectedLogTime;
    private float _nextMissLogTime;
    private bool _isTargetingSkill;
    private int _targetedSkillSlot;
    private int _targetedSkillId;
    private float _targetedSkillRadius;
    private Vector3 _targetedSkillWorldPosition;
    private bool _hasTargetedSkillWorldPosition;
    private PaperLegendCharacterNetworkHandler _targetedSkillOwner;
    private GameObject _targetedSkillIndicatorObject;
    private LineRenderer _targetedSkillIndicatorRenderer;
    private TargetedChargeSkillKind _targetedChargeKind;
    private bool _isTargetedChargeAiming;
    private bool _isTargetedChargeCharging;
    private bool _targetedChargePositionLocked;
    private Vector3 _targetedChargeAnchorPosition;
    private bool _tidalCataclysmChargeNetworkActive;
    private Coroutine _targetedChargeAimingTimeoutCoroutine;
    private Coroutine _targetedChargeCoroutine;
    private GameObject _targetedChargeUiRoot;
    private CanvasGroup _targetedChargeGroup;
    private Image _targetedChargeFill;

    public static PaperLegendFlickInputCollector EnsureExists()
    {
#if UNITY_SERVER
        return null;
#else
        if (Instance != null)
            return Instance;

        var existing = FindObjectOfType<PaperLegendFlickInputCollector>();
        if (existing != null)
        {
            Instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return existing;
        }

        var go = new GameObject(AutoObjectName);
        DontDestroyOnLoad(go);
        return go.AddComponent<PaperLegendFlickInputCollector>();
#endif
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
#if !UNITY_SERVER
        Debug.Log("[PaperLegends][Input] Flick input collector is ready.");
#endif
    }

    private void OnEnable()
    {
#if !UNITY_SERVER
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
#endif
    }

    private void OnDisable()
    {
#if !UNITY_SERVER
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

#if !UNITY_SERVER
        DestroyTargetedChargeUI();
        DestroyTargetedSkillIndicator();
        DestroyShoveStunHintIcons();
#endif
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        CleanupTransientGameplayState(notifyNetwork: false);
    }

    private void Update()
    {
#if !UNITY_SERVER
        PollPointer();
        UpdateShoveStunHintIcons();
#endif
    }

    public static bool TryGetInput(out PaperLegendPlayerInputData input)
    {
        input = default;

        if (Instance == null)
            return false;

        return Instance.TryConsumePendingInput(out input);
    }

    public static void QueueSkillUse(int skillSlot)
    {
#if !UNITY_SERVER
        EnsureExists()?.QueueSkillInput(skillSlot, upgrade: false);
#endif
    }

    public static void QueueSkillUpgrade(int skillSlot)
    {
#if !UNITY_SERVER
        EnsureExists()?.QueueSkillInput(skillSlot, upgrade: true);
#endif
    }

    public static void BeginTargetedSkillUse(int skillSlot, int skillId, Vector2 screenPosition, float radius = -1f)
    {
#if !UNITY_SERVER
        EnsureExists()?.BeginTargetedSkillUseInternal(skillSlot, skillId, screenPosition, radius);
#endif
    }

    public static void UpdateTargetedSkillUse(Vector2 screenPosition)
    {
#if !UNITY_SERVER
        Instance?.UpdateTargetedSkillUseInternal(screenPosition);
#endif
    }

    public static void EndTargetedSkillUse(Vector2 screenPosition, bool canceled)
    {
#if !UNITY_SERVER
        Instance?.EndTargetedSkillUseInternal(screenPosition, canceled);
#endif
    }

    public static void BeginGravityWellAiming(int skillSlot, int skillId, float radius = -1f)
    {
#if !UNITY_SERVER
        EnsureExists()?.BeginTargetedChargeAimingInternal(TargetedChargeSkillKind.GravityWell, skillSlot, skillId, radius);
#endif
    }

    public static void BeginTidalCataclysmAiming(int skillSlot, int skillId, float radius = -1f)
    {
#if !UNITY_SERVER
        EnsureExists()?.BeginTargetedChargeAimingInternal(TargetedChargeSkillKind.TidalCataclysm, skillSlot, skillId, radius);
#endif
    }

    public static void CancelTargetedChargeAiming()
    {
#if !UNITY_SERVER
        Instance?.CancelTargetedChargeAimingInternal();
#endif
    }

    public static void ResetGameplayState()
    {
#if !UNITY_SERVER
        Instance?.CleanupTransientGameplayState(notifyNetwork: false);
#endif
    }

    public static void CancelGravityWellAiming()
    {
        CancelTargetedChargeAiming();
    }

    public static bool IsTargetedChargeAimingOrCharging =>
#if !UNITY_SERVER
        Instance != null && Instance._targetedChargeKind != TargetedChargeSkillKind.None;
#else
        false;
#endif

    public static bool IsGravityWellAimingOrCharging => IsTargetedChargeAimingOrCharging;

    private bool TryConsumePendingInput(out PaperLegendPlayerInputData input)
    {
        input = default;

        if (!_hasPendingInput)
            return false;

        input = _pendingInput;
        _pendingInput = default;
        _hasPendingInput = false;
        return true;
    }

#if !UNITY_SERVER
    private enum TargetedChargeSkillKind
    {
        None,
        GravityWell,
        TidalCataclysm
    }

    private enum PointerPhase
    {
        Began,
        Moved,
        Stationary,
        Ended,
        Canceled
    }

    private void PollPointer()
    {
        if (_isTargetedChargeCharging)
        {
            PollTargetedChargePointer();
            return;
        }

        if (_isTargetedChargeAiming)
        {
            PollTargetedChargeAimingPointer();
            return;
        }

        if (_isTargetingSkill)
        {
            PollTargetedSkillPointer();
            return;
        }

#if ENABLE_INPUT_SYSTEM
        if (PollInputSystemPointer())
            return;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        PollLegacyInputPointer();
#endif
    }

    private void PollTargetedSkillPointer()
    {
#if ENABLE_INPUT_SYSTEM
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            Vector2 touchPosition = touchscreen.primaryTouch.position.ReadValue();
            if (touchscreen.primaryTouch.press.isPressed)
            {
                UpdateTargetedSkillUseInternal(touchPosition);
                return;
            }

            if (touchscreen.primaryTouch.press.wasReleasedThisFrame)
            {
                EndTargetedSkillUseInternal(touchPosition, canceled: false);
                return;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mousePosition = mouse.position.ReadValue();
            if (mouse.leftButton.isPressed)
            {
                UpdateTargetedSkillUseInternal(mousePosition);
                return;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                EndTargetedSkillUseInternal(mousePosition, canceled: false);
                return;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchSupported && Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            PointerPhase phase = ConvertLegacyTouchPhase(touch.phase);
            if (phase == PointerPhase.Ended || phase == PointerPhase.Canceled)
                EndTargetedSkillUseInternal(touch.position, phase == PointerPhase.Canceled);
            else
                UpdateTargetedSkillUseInternal(touch.position);

            return;
        }

        if (Input.GetMouseButton(0))
            UpdateTargetedSkillUseInternal(Input.mousePosition);
        else if (Input.GetMouseButtonUp(0))
            EndTargetedSkillUseInternal(Input.mousePosition, canceled: false);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private bool PollInputSystemPointer()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            PollPointerPhase(
                touchscreen.primaryTouch.press.wasPressedThisFrame ? PointerPhase.Began : PointerPhase.Moved,
                touchscreen.primaryTouch.position.ReadValue());
            return true;
        }

        if (touchscreen != null && touchscreen.primaryTouch.press.wasReleasedThisFrame)
        {
            PollPointerPhase(PointerPhase.Ended, touchscreen.primaryTouch.position.ReadValue());
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return false;

        Vector2 mousePosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
        {
            PollPointerPhase(PointerPhase.Began, mousePosition);
            return true;
        }

        if (mouse.leftButton.isPressed)
        {
            PollPointerPhase(PointerPhase.Moved, mousePosition);
            return true;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            PollPointerPhase(PointerPhase.Ended, mousePosition);
            return true;
        }

        return false;
    }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
    private void PollLegacyInputPointer()
    {
        if (Input.touchSupported && Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            PollPointerPhase(ConvertLegacyTouchPhase(touch.phase), touch.position);
            return;
        }

        if (Input.GetMouseButtonDown(0))
            PollPointerPhase(PointerPhase.Began, Input.mousePosition);
        else if (Input.GetMouseButton(0))
            PollPointerPhase(PointerPhase.Moved, Input.mousePosition);
        else if (Input.GetMouseButtonUp(0))
            PollPointerPhase(PointerPhase.Ended, Input.mousePosition);
    }

    private static PointerPhase ConvertLegacyTouchPhase(UnityEngine.TouchPhase phase)
    {
        switch (phase)
        {
            case UnityEngine.TouchPhase.Began:
                return PointerPhase.Began;
            case UnityEngine.TouchPhase.Moved:
                return PointerPhase.Moved;
            case UnityEngine.TouchPhase.Stationary:
                return PointerPhase.Stationary;
            case UnityEngine.TouchPhase.Canceled:
                return PointerPhase.Canceled;
            case UnityEngine.TouchPhase.Ended:
            default:
                return PointerPhase.Ended;
        }
    }
#endif

    private void PollPointerPhase(PointerPhase phase, Vector2 screenPosition)
    {
        switch (phase)
        {
            case PointerPhase.Began:
                BeginTracking(screenPosition);
                break;
            case PointerPhase.Moved:
            case PointerPhase.Stationary:
                TickTracking(screenPosition);
                break;
            case PointerPhase.Ended:
            case PointerPhase.Canceled:
                EndTracking(screenPosition, phase == PointerPhase.Canceled);
                break;
        }
    }

    private void BeginTracking(Vector2 screenPosition)
    {
        LogDebug($"Pointer down at screen={screenPosition}.");

        if (!TryRaycastPaperLegendCharacter(screenPosition, out var handler, out var hit, out string missReason))
        {
            if (TryFindLocalDirectionalSkillTarget(out var directionalSkillTarget))
            {
                BeginTrackingForTarget(
                    directionalSkillTarget,
                    screenPosition,
                    directionalSkillTarget.transform.position,
                    Vector3.up,
                    "global directional skill swipe",
                    useForwardSlideSwipe: true,
                    useHomingSwordDirectionalSwipe: true);
                return;
            }

            LogRaycastMiss(missReason);
            return;
        }

        LogDebug(
            $"Raycast hit '{hit.collider.name}' -> player={handler.PlayerId}, model={handler.CharacterModelId}, state={handler.State}, grounded={handler.IsGrounded}, alive={handler.IsAlive}, hasInputAuthority={handler.HasInputAuthority}, objectInputAuthority={ResolveInputAuthorityLabel(handler)}.");

        if (!handler.HasInputAuthority)
        {
            if (TryFindLocalInputAuthorityCharacter(out PaperLegendCharacterNetworkHandler localHero)
                && TryHandleHero10000004EnemyTargetTap(localHero, handler, screenPosition))
            {
                return;
            }

            if (TryFindLocalShoveStunCaster(out PaperLegendCharacterNetworkHandler shoveCaster)
                && handler.IsAlive
                && !shoveCaster.IsSameFaction(handler)
                && IsWithinShoveStunRange(shoveCaster, handler))
            {
                BeginShoveStunTracking(
                    shoveCaster,
                    handler,
                    screenPosition,
                    hit.point,
                    hit.normal,
                    autoUseSkill: !shoveCaster.Hero10000002ShoveStunArmed);
                return;
            }

            if (TryFindLocalDirectionalSkillTarget(out var directionalSkillTarget))
            {
                BeginTrackingForTarget(
                    directionalSkillTarget,
                    screenPosition,
                    directionalSkillTarget.transform.position,
                    Vector3.up,
                    $"global directional skill swipe over non-owned player={handler.PlayerId}",
                    useForwardSlideSwipe: true,
                    useHomingSwordDirectionalSwipe: true);
                return;
            }

            LogRejectedInput($"hit player={handler.PlayerId}, model={handler.CharacterModelId}, but this client does not have input authority.");
            return;
        }

        if (!handler.CanAcceptLocalFlickInput(out string rejectReason))
        {
            LogRejectedInput($"player={handler.PlayerId} cannot flick now. {rejectReason}");
            return;
        }

        BeginTrackingForTarget(handler, screenPosition, hit.point, hit.normal, "paper character hit", useForwardSlideSwipe: false);
    }

    private void BeginShoveStunTracking(
        PaperLegendCharacterNetworkHandler caster,
        PaperLegendCharacterNetworkHandler victim,
        Vector2 screenPosition,
        Vector3 contactWorldPosition,
        Vector3 contactSurfaceNormal,
        bool autoUseSkill = false)
    {
        if (caster == null || victim == null)
            return;

        _isTracking = true;
        _isShoveStunSwipe = true;
        _autoUseShoveStunSkill = autoUseSkill;
        _isForwardSlideSwipe = false;
        _isHomingSwordDirectionalSwipe = false;
        _target = caster;
        _shoveStunVictim = victim;
        _startScreenPosition = screenPosition;
        _currentScreenPosition = screenPosition;
        _contactWorldPosition = contactWorldPosition;
        _contactSurfaceNormal = contactSurfaceNormal;
        _startTime = Time.unscaledTime;
        _fallbackFullPowerTime = -1f;
        StartChargeUi();
        LogDebug($"Started shove stun swipe for caster={caster.PlayerId} -> victim={victim.PlayerId}, autoUse={_autoUseShoveStunSkill}, contact={_contactWorldPosition}.");
    }

    private void BeginTrackingForTarget(
        PaperLegendCharacterNetworkHandler handler,
        Vector2 screenPosition,
        Vector3 contactWorldPosition,
        Vector3 contactSurfaceNormal,
        string context,
        bool useForwardSlideSwipe = false,
        bool useHomingSwordDirectionalSwipe = false)
    {
        if (handler == null)
            return;

        _isTracking = true;
        _isShoveStunSwipe = false;
        _autoUseShoveStunSkill = false;
        _isForwardSlideSwipe = useForwardSlideSwipe
            && handler.CharacterModelId == 10000002
            && handler.Hero10000002ForwardSlideArmed
            && handler.Hero10000002ForwardSlideRemaining > 0;
        _isHomingSwordDirectionalSwipe = useHomingSwordDirectionalSwipe
            && handler.CharacterModelId == PaperLegendHero10000004SonTinhSkillSet.HeroId
            && (handler.Hero10000004HomingSwordArmed || HasPendingHero10000004HomingSwordUse());
        _shoveStunVictim = null;
        _target = handler;
        _startScreenPosition = screenPosition;
        _currentScreenPosition = screenPosition;
        _contactWorldPosition = contactWorldPosition;
        _contactSurfaceNormal = contactSurfaceNormal;
        _startTime = Time.unscaledTime;
        _fallbackFullPowerTime = -1f;
        if (!_isForwardSlideSwipe)
            StartChargeUi();

        LogDebug($"Started flick charge for player={handler.PlayerId}, contact={_contactWorldPosition}, normal={_contactSurfaceNormal}, context={context}.");
    }

    private void TickTracking(Vector2 screenPosition)
    {
        if (!_isTracking)
            return;

        _currentScreenPosition = screenPosition;

        if (_target == null || !_target.HasInputAuthority)
        {
            CancelTracking();
            return;
        }

        if (_isShoveStunSwipe)
        {
            bool canAutoUseShoveStun = _autoUseShoveStunSkill && CanAutoUseShoveStun(_target);
            if ((!_target.Hero10000002ShoveStunArmed && !canAutoUseShoveStun)
                || _shoveStunVictim == null
                || !_shoveStunVictim.IsAlive
                || _target.IsSameFaction(_shoveStunVictim))
            {
                CancelTracking();
                return;
            }
        }
        else if (!_target.CanAcceptLocalFlick)
        {
            CancelTracking();
            return;
        }

        if (!_isForwardSlideSwipe && ShouldAutoRelease())
            EndTracking(screenPosition, canceled: false);
    }

    private void EndTracking(Vector2 screenPosition, bool canceled)
    {
        if (!_isTracking)
            return;

        _currentScreenPosition = screenPosition;

        if (!canceled && _target != null && _target.HasInputAuthority && (_isShoveStunSwipe || _target.CanAcceptLocalFlick))
            QueueFlickInput();
        else
            ResetChargeUi();

        _isTracking = false;
        _target = null;
    }

    private void QueueFlickInput()
    {
        float force01 = Mathf.Max(
            minimumForce01,
            _isForwardSlideSwipe ? ResolveSwipeSpeedForce01() : ResolveChargeForce01());
        Vector3 aimWorldDirection = ResolveScreenDragWorldDirection();

        bool autoShoveStunRequest = _isShoveStunSwipe && _autoUseShoveStunSkill && _target != null && !_target.Hero10000002ShoveStunArmed;
        bool carrySkillRequest = autoShoveStunRequest || (_hasPendingInput && _pendingInput.SkillRequested);
        int carriedSkillSlot = carrySkillRequest ? _pendingInput.SkillSlot : 0;
        if (autoShoveStunRequest)
            carriedSkillSlot = 2;
        bool carrySkillTarget = _hasPendingInput && _pendingInput.SkillTargetWorldPositionSet;
        Vector3 carriedSkillTarget = carrySkillTarget ? _pendingInput.SkillTargetWorldPosition : default;

        _flickSequence++;
        _pendingInput = new PaperLegendPlayerInputData
        {
            FlickRequested = true,
            FlickSequence = _flickSequence,
            ContactWorldPosition = _contactWorldPosition,
            ContactSurfaceNormal = _contactSurfaceNormal,
            AimWorldDirection = aimWorldDirection,
            Force01 = force01,
            FlickTargetPlayerId = _isShoveStunSwipe && _shoveStunVictim != null ? _shoveStunVictim.PlayerId : 0,
            Hero10000002ForwardSlideRequested = _isForwardSlideSwipe,
            Hero10000004HomingSwordRequested = _isHomingSwordDirectionalSwipe,
            SkillRequested = carrySkillRequest,
            SkillSlot = carriedSkillSlot,
            SkillTargetWorldPositionSet = carrySkillTarget,
            SkillTargetWorldPosition = carriedSkillTarget
        };

        _hasPendingInput = true;
        LogDebug($"Queued flick input seq={_flickSequence}, force={force01:0.00}, contact={_pendingInput.ContactWorldPosition}, direction={_pendingInput.AimWorldDirection}.");
        ResetChargeUi();
        _autoUseShoveStunSkill = false;
        _isForwardSlideSwipe = false;
        _isHomingSwordDirectionalSwipe = false;
    }

    private void QueueSkillInput(int skillSlot, bool upgrade)
    {
        skillSlot = Mathf.Clamp(skillSlot, 1, 4);
        _pendingInput = new PaperLegendPlayerInputData
        {
            SkillRequested = !upgrade,
            SkillSlot = upgrade ? 0 : skillSlot,
            SkillUpgradeRequested = upgrade,
            SkillUpgradeSlot = upgrade ? skillSlot : 0
        };

        _hasPendingInput = true;
        LogDebug($"Queued {(upgrade ? "skill upgrade" : "skill use")} input slot={skillSlot}.");
    }

    private void QueueSkillInputWithPlayerTarget(int skillSlot, int targetPlayerId)
    {
        skillSlot = Mathf.Clamp(skillSlot, 1, 4);
        _pendingInput = new PaperLegendPlayerInputData
        {
            SkillRequested = true,
            SkillSlot = skillSlot,
            SkillTargetPlayerId = targetPlayerId
        };

        _hasPendingInput = true;
        LogDebug($"Queued skill input slot={skillSlot} with targetPlayerId={targetPlayerId}.");
    }

    private void QueueTargetedSkillInput()
    {
        _pendingInput = new PaperLegendPlayerInputData
        {
            SkillRequested = true,
            SkillSlot = _targetedSkillSlot,
            SkillTargetWorldPositionSet = true,
            SkillTargetWorldPosition = _targetedSkillWorldPosition
        };

        _hasPendingInput = true;
        LogDebug($"Queued targeted skill input slot={_targetedSkillSlot}, skillId={_targetedSkillId}, target={_targetedSkillWorldPosition}.");
    }

    private void BeginTargetedSkillUseInternal(int skillSlot, int skillId, Vector2 screenPosition, float radius)
    {
        if (_isTracking)
            CancelTracking();

        if (!TryFindLocalInputAuthorityCharacter(out PaperLegendCharacterNetworkHandler owner))
        {
            LogRejectedInput("targeted skill requested but no local Paper Legends character has input authority.");
            return;
        }

        skillSlot = Mathf.Clamp(skillSlot, 1, 4);
        if (!owner.CanUseSkill(skillSlot))
        {
            LogRejectedInput($"targeted skill rejected locally. player={owner.PlayerId}, model={owner.CharacterModelId}, slot={skillSlot}, level={owner.GetSkillLevel(skillSlot)}.");
            return;
        }

        if (skillId == (int)PaperLegendHeroSkillId.Hero10000003TidalCataclysm
            && (owner.Hero10000003TidalCataclysmCharging || _isTargetedChargeCharging))
        {
            LogRejectedInput($"targeted skill rejected locally. player={owner.PlayerId}, tidal cataclysm is already charging.");
            return;
        }

        _isTargetingSkill = true;
        _targetedSkillSlot = skillSlot;
        _targetedSkillId = skillId;
        _targetedSkillRadius = radius > 0f ? radius : targetedSkillDefaultRadius;
        _targetedSkillOwner = owner;

        if (!ResolveTargetedSkillWorldPosition(screenPosition, out _targetedSkillWorldPosition))
            _targetedSkillWorldPosition = owner.transform.position;

        EnsureTargetedSkillIndicator();
        UpdateTargetedSkillIndicator();
        LogDebug($"Started targeted skill selection slot={skillSlot}, skillId={skillId}, target={_targetedSkillWorldPosition}.");
    }

    private void UpdateTargetedSkillUseInternal(Vector2 screenPosition)
    {
        if (!_isTargetingSkill)
            return;

        if (_targetedSkillOwner == null || !_targetedSkillOwner.HasInputAuthority || !_targetedSkillOwner.CanUseSkill(_targetedSkillSlot))
        {
            EndTargetedSkillUseInternal(screenPosition, canceled: true);
            return;
        }

        if (ResolveTargetedSkillWorldPosition(screenPosition, out Vector3 worldPosition))
            _targetedSkillWorldPosition = worldPosition;

        UpdateTargetedSkillIndicator();
    }

    private void EndTargetedSkillUseInternal(Vector2 screenPosition, bool canceled)
    {
        if (!_isTargetingSkill)
            return;

        if (ResolveTargetedSkillWorldPosition(screenPosition, out Vector3 worldPosition))
            _targetedSkillWorldPosition = worldPosition;

        UpdateTargetedSkillIndicator();

        if (!canceled && _targetedSkillOwner != null && _targetedSkillOwner.HasInputAuthority && _targetedSkillOwner.CanUseSkill(_targetedSkillSlot))
        {
            if (_targetedSkillId == (int)PaperLegendHeroSkillId.Hero10000003TidalCataclysm)
            {
                BeginTargetedChargeFromPlacedPosition();
                _isTargetingSkill = false;
                return;
            }

            QueueTargetedSkillInput();
        }

        HideTargetedSkillIndicator();
        _isTargetingSkill = false;
        _targetedSkillOwner = null;
        _targetedSkillSlot = 0;
        _targetedSkillId = 0;
        _targetedSkillRadius = 0f;
    }

    private void BeginTargetedChargeFromPlacedPosition()
    {
        if (_targetedSkillOwner == null || !_targetedSkillOwner.HasInputAuthority || !_targetedSkillOwner.CanUseSkill(_targetedSkillSlot))
        {
            HideTargetedSkillIndicator();
            _targetedSkillOwner = null;
            _targetedSkillSlot = 0;
            _targetedSkillId = 0;
            _targetedSkillRadius = 0f;
            return;
        }

        _targetedChargeKind = TargetedChargeSkillKind.TidalCataclysm;
        _targetedChargePositionLocked = true;
        _tidalCataclysmChargeNetworkActive = false;
        _targetedChargeAnchorPosition = _targetedSkillOwner.transform.position;
        _hasTargetedSkillWorldPosition = true;
        _isTargetedChargeAiming = false;
        _isTargetedChargeCharging = true;

        NotifyTargetedChargeBeginRpc(_targetedSkillWorldPosition);
        UpdateTargetedSkillIndicator();
        SetTargetedChargeVisible(true);
        UpdateTargetedChargeProgress(0f);

        if (_targetedChargeCoroutine != null)
            StopCoroutine(_targetedChargeCoroutine);

        _targetedChargeCoroutine = StartCoroutine(TargetedChargeRoutine());
        LogDebug($"Tidal Cataclysm charge started at placed position {_targetedSkillWorldPosition}. Hold still for {tidalCataclysmChargeDurationSeconds:0.0}s.");
    }

    private void BeginTargetedChargeAimingInternal(TargetedChargeSkillKind kind, int skillSlot, int skillId, float radius)
    {
        if (_isTracking)
            CancelTracking();

        if (_isTargetingSkill)
            EndTargetedSkillUseInternal(Vector2.zero, canceled: true);

        CancelTargetedChargeAimingInternal();

        if (!TryFindLocalInputAuthorityCharacter(out PaperLegendCharacterNetworkHandler owner))
        {
            LogRejectedInput("targeted charge skill requested but no local Paper Legends character has input authority.");
            return;
        }

        skillSlot = Mathf.Clamp(skillSlot, 1, 4);
        if (!owner.CanUseSkill(skillSlot))
        {
            LogRejectedInput($"targeted charge skill rejected locally. player={owner.PlayerId}, slot={skillSlot}, level={owner.GetSkillLevel(skillSlot)}.");
            return;
        }

        _targetedChargeKind = kind;
        _isTargetedChargeAiming = true;
        _targetedSkillSlot = skillSlot;
        _targetedSkillId = skillId;
        _targetedSkillRadius = radius > 0f ? radius : ResolveTargetedChargeRadius(kind);
        _targetedSkillOwner = owner;
        _hasTargetedSkillWorldPosition = false;

        HideTargetedSkillIndicator();
        _targetedChargeAimingTimeoutCoroutine = StartCoroutine(TargetedChargeAimingTimeoutRoutine());
        LogDebug($"Started targeted charge aiming kind={kind}, slot={skillSlot}, skillId={skillId}. Hold a map point for {ResolveTargetedChargeDuration(kind):0.0}s.");
    }

    private void CancelTargetedChargeAimingInternal(bool notifyNetwork = true)
    {
        if (_targetedChargeKind == TargetedChargeSkillKind.None
            && !_isTargetedChargeAiming
            && !_isTargetedChargeCharging
            && _targetedChargeCoroutine == null
            && _targetedChargeAimingTimeoutCoroutine == null)
        {
            return;
        }

        if (_targetedChargeAimingTimeoutCoroutine != null)
        {
            StopCoroutine(_targetedChargeAimingTimeoutCoroutine);
            _targetedChargeAimingTimeoutCoroutine = null;
        }

        if (_targetedChargeCoroutine != null)
        {
            StopCoroutine(_targetedChargeCoroutine);
            _targetedChargeCoroutine = null;
        }

        if (notifyNetwork && _isTargetedChargeCharging && _targetedSkillOwner != null && _targetedSkillOwner.HasInputAuthority)
            NotifyTargetedChargeCanceledRpc();

        SetTargetedChargeVisible(false);
        HideTargetedSkillIndicator();

        _targetedChargeKind = TargetedChargeSkillKind.None;
        _isTargetedChargeAiming = false;
        _isTargetedChargeCharging = false;
        _targetedChargePositionLocked = false;
        _tidalCataclysmChargeNetworkActive = false;
        _targetedSkillOwner = null;
        _targetedSkillSlot = 0;
        _targetedSkillId = 0;
        _targetedSkillRadius = 0f;
        _hasTargetedSkillWorldPosition = false;
    }

    private void CleanupTransientGameplayState(bool notifyNetwork)
    {
        if (_isTargetingSkill)
            EndTargetedSkillUseInternal(Vector2.zero, canceled: true);

        if (_isTracking)
            CancelTracking();

        CancelTargetedChargeAimingInternal(notifyNetwork);
        DestroyTargetedChargeUI();
        HideTargetedSkillIndicator();
        DestroyShoveStunHintIcons();
        _hasPendingInput = false;
        _pendingInput = default;
    }

    private IEnumerator TargetedChargeAimingTimeoutRoutine()
    {
        float timeout = Mathf.Max(0.1f, targetedChargeAimingTimeoutSeconds);
        yield return new WaitForSecondsRealtime(timeout);

        if (_isTargetedChargeAiming && !_isTargetedChargeCharging)
        {
            LogDebug($"Targeted charge aiming timed out for kind={_targetedChargeKind}.");
            CancelTargetedChargeAimingInternal();
        }

        _targetedChargeAimingTimeoutCoroutine = null;
    }

    private void PollTargetedChargeAimingPointer()
    {
        if (!TryPollPointerPressState(out bool pressed, out bool began, out bool ended, out Vector2 screenPosition))
            return;

        if (IsPointerOverBlockingUi())
            return;

        if (began)
            BeginTargetedCharge(screenPosition);
        else if (pressed)
            UpdateTargetedChargeAimingPreview(screenPosition);
    }

    private void UpdateTargetedChargeAimingPreview(Vector2 screenPosition)
    {
        if (!ResolveTargetedSkillWorldPosition(screenPosition, out Vector3 worldPosition))
            return;

        _targetedSkillWorldPosition = worldPosition;
        _hasTargetedSkillWorldPosition = true;
        UpdateTargetedSkillIndicator();
    }

    private void PollTargetedChargePointer()
    {
        if (!TryPollPointerPressState(out bool pressed, out bool began, out bool ended, out Vector2 screenPosition))
            return;

        if (pressed && !_targetedChargePositionLocked)
            UpdateTargetedChargePosition(screenPosition);

        if (ended && !_targetedChargePositionLocked)
            CancelTargetedCharge(canceled: true);
    }

    private void BeginTargetedCharge(Vector2 screenPosition)
    {
        if (_targetedSkillOwner == null || !_targetedSkillOwner.HasInputAuthority || !_targetedSkillOwner.CanUseSkill(_targetedSkillSlot))
        {
            CancelTargetedChargeAimingInternal();
            return;
        }

        if (!ResolveTargetedSkillWorldPosition(screenPosition, out _targetedSkillWorldPosition))
        {
            LogDebug("Targeted charge rejected: could not resolve map position.");
            CancelTargetedChargeAimingInternal();
            return;
        }

        _hasTargetedSkillWorldPosition = true;
        _isTargetedChargeAiming = false;
        _isTargetedChargeCharging = true;
        _targetedChargePositionLocked = false;

        if (_targetedChargeAimingTimeoutCoroutine != null)
        {
            StopCoroutine(_targetedChargeAimingTimeoutCoroutine);
            _targetedChargeAimingTimeoutCoroutine = null;
        }

        NotifyTargetedChargeBeginRpc(_targetedSkillWorldPosition);
        UpdateTargetedSkillIndicator();
        SetTargetedChargeVisible(true);
        UpdateTargetedChargeProgress(0f);

        if (_targetedChargeCoroutine != null)
            StopCoroutine(_targetedChargeCoroutine);

        _targetedChargeCoroutine = StartCoroutine(TargetedChargeRoutine());
        LogDebug($"Targeted charge started kind={_targetedChargeKind} at {_targetedSkillWorldPosition}.");
    }

    private void UpdateTargetedChargePosition(Vector2 screenPosition)
    {
        if (!ResolveTargetedSkillWorldPosition(screenPosition, out Vector3 worldPosition))
            return;

        if ((worldPosition - _targetedSkillWorldPosition).sqrMagnitude > 0.01f)
        {
            _targetedSkillWorldPosition = worldPosition;
            _hasTargetedSkillWorldPosition = true;
            NotifyTargetedChargePositionRpc(worldPosition);
        }
        else
        {
            _targetedSkillWorldPosition = worldPosition;
            _hasTargetedSkillWorldPosition = true;
        }

        UpdateTargetedSkillIndicator();
    }

    private void CancelTargetedCharge(bool canceled)
    {
        if (_targetedChargeCoroutine != null)
        {
            StopCoroutine(_targetedChargeCoroutine);
            _targetedChargeCoroutine = null;
        }

        if (_targetedSkillOwner != null && _targetedSkillOwner.HasInputAuthority)
            NotifyTargetedChargeCanceledRpc();

        SetTargetedChargeVisible(false);
        HideTargetedSkillIndicator();
        _targetedChargeKind = TargetedChargeSkillKind.None;
        _isTargetedChargeCharging = false;
        _isTargetedChargeAiming = false;
        _targetedChargePositionLocked = false;
        _tidalCataclysmChargeNetworkActive = false;
        _targetedSkillOwner = null;
        _targetedSkillSlot = 0;
        _targetedSkillId = 0;
        _targetedSkillRadius = 0f;

        if (canceled)
            LogDebug("Targeted charge canceled.");
    }

    private void FailTidalCataclysmChargeDueToMovement()
    {
        if (_targetedChargeCoroutine != null)
        {
            StopCoroutine(_targetedChargeCoroutine);
            _targetedChargeCoroutine = null;
        }

        if (_targetedSkillOwner != null && _targetedSkillOwner.HasInputAuthority)
            NotifyTargetedChargeFailedDueToMovementRpc();

        SetTargetedChargeVisible(false);
        HideTargetedSkillIndicator();
        _targetedChargeKind = TargetedChargeSkillKind.None;
        _isTargetedChargeCharging = false;
        _isTargetedChargeAiming = false;
        _targetedChargePositionLocked = false;
        _tidalCataclysmChargeNetworkActive = false;
        _targetedSkillOwner = null;
        _targetedSkillSlot = 0;
        _targetedSkillId = 0;
        _targetedSkillRadius = 0f;
        LogDebug("Tidal Cataclysm charge failed because the hero moved.");
    }

    private void CleanupTidalCataclysmChargeLocalStateAfterServerFailure()
    {
        if (_targetedChargeCoroutine != null)
        {
            StopCoroutine(_targetedChargeCoroutine);
            _targetedChargeCoroutine = null;
        }

        SetTargetedChargeVisible(false);
        HideTargetedSkillIndicator();
        _targetedChargeKind = TargetedChargeSkillKind.None;
        _isTargetedChargeCharging = false;
        _isTargetedChargeAiming = false;
        _targetedChargePositionLocked = false;
        _tidalCataclysmChargeNetworkActive = false;
        _targetedSkillOwner = null;
        _targetedSkillSlot = 0;
        _targetedSkillId = 0;
        _targetedSkillRadius = 0f;
        LogDebug("Tidal Cataclysm charge ended because the server canceled it.");
    }

    private IEnumerator TargetedChargeRoutine()
    {
        float duration = Mathf.Max(0.1f, ResolveTargetedChargeDuration(_targetedChargeKind));
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (_targetedSkillOwner == null || !_targetedSkillOwner.HasInputAuthority)
            {
                CancelTargetedCharge(canceled: true);
                yield break;
            }

            if (_targetedSkillOwner.Hero10000003TidalCataclysmCharging)
                _tidalCataclysmChargeNetworkActive = true;

            if (_targetedChargeKind == TargetedChargeSkillKind.TidalCataclysm
                && _targetedChargePositionLocked
                && _tidalCataclysmChargeNetworkActive
                && !_targetedSkillOwner.Hero10000003TidalCataclysmCharging)
            {
                CleanupTidalCataclysmChargeLocalStateAfterServerFailure();
                yield break;
            }

            if (_targetedChargeKind == TargetedChargeSkillKind.TidalCataclysm
                && _targetedChargePositionLocked
                && HasMovedBeyondTidalCataclysmChargeAnchor(_targetedSkillOwner.transform.position))
            {
                FailTidalCataclysmChargeDueToMovement();
                yield break;
            }

            if (!_targetedSkillOwner.CanUseSkill(_targetedSkillSlot)
                && !(_targetedChargeKind == TargetedChargeSkillKind.TidalCataclysm && _targetedChargePositionLocked))
            {
                CancelTargetedCharge(canceled: true);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            UpdateTargetedChargeProgress(elapsed / duration);
            yield return null;
        }

        CompleteTargetedCharge();
    }

    private void CompleteTargetedCharge()
    {
        _targetedChargeCoroutine = null;
        SetTargetedChargeVisible(false);

        if (_targetedSkillOwner != null && _targetedSkillOwner.HasInputAuthority && _targetedSkillOwner.CanUseSkill(_targetedSkillSlot) && _hasTargetedSkillWorldPosition)
            QueueTargetedSkillInput();

        HideTargetedSkillIndicator();
        _targetedChargeKind = TargetedChargeSkillKind.None;
        _isTargetedChargeCharging = false;
        _isTargetedChargeAiming = false;
        _targetedChargePositionLocked = false;
        _tidalCataclysmChargeNetworkActive = false;
        _targetedSkillOwner = null;
        _targetedSkillSlot = 0;
        _targetedSkillId = 0;
        _targetedSkillRadius = 0f;
        LogDebug("Targeted charge completed. Skill cast queued.");
    }

    private float ResolveTargetedChargeDuration(TargetedChargeSkillKind kind)
    {
        return kind == TargetedChargeSkillKind.TidalCataclysm
            ? tidalCataclysmChargeDurationSeconds
            : gravityWellChargeDurationSeconds;
    }

    private float ResolveTargetedChargeRadius(TargetedChargeSkillKind kind)
    {
        return kind == TargetedChargeSkillKind.TidalCataclysm
            ? tidalCataclysmTargetingRadius
            : gravityWellTargetingRadius;
    }

    private void NotifyTargetedChargeBeginRpc(Vector3 worldPosition)
    {
        if (_targetedSkillOwner == null)
            return;

        switch (_targetedChargeKind)
        {
            case TargetedChargeSkillKind.TidalCataclysm:
                _targetedSkillOwner.RpcBeginHero10000003TidalCataclysmCharge(worldPosition);
                break;
            default:
                _targetedSkillOwner.RpcBeginHero10000003GravityWellCharge(worldPosition);
                break;
        }
    }

    private bool HasMovedBeyondTidalCataclysmChargeAnchor(Vector3 currentPosition)
    {
        Vector3 delta = currentPosition - _targetedChargeAnchorPosition;
        delta.y = 0f;
        float breakDistance = Mathf.Max(0.05f, tidalCataclysmChargeMovementBreakDistance);
        return delta.sqrMagnitude > breakDistance * breakDistance;
    }

    private void NotifyTargetedChargeFailedDueToMovementRpc()
    {
        if (_targetedSkillOwner == null)
            return;

        if (_targetedChargeKind == TargetedChargeSkillKind.TidalCataclysm)
            _targetedSkillOwner.RpcFailHero10000003TidalCataclysmChargeDueToMovement();
    }

    private void NotifyTargetedChargeCanceledRpc()
    {
        if (_targetedSkillOwner == null)
            return;

        switch (_targetedChargeKind)
        {
            case TargetedChargeSkillKind.TidalCataclysm:
                _targetedSkillOwner.RpcCancelHero10000003TidalCataclysmCharge();
                break;
            default:
                _targetedSkillOwner.RpcCancelHero10000003GravityWellCharge();
                break;
        }
    }

    private void NotifyTargetedChargePositionRpc(Vector3 worldPosition)
    {
        if (_targetedSkillOwner == null)
            return;

        switch (_targetedChargeKind)
        {
            case TargetedChargeSkillKind.TidalCataclysm:
                _targetedSkillOwner.RpcUpdateHero10000003TidalCataclysmChargePosition(worldPosition);
                break;
            default:
                _targetedSkillOwner.RpcUpdateHero10000003GravityWellChargePosition(worldPosition);
                break;
        }
    }

    private bool TryPollPointerPressState(out bool pressed, out bool began, out bool ended, out Vector2 screenPosition)
    {
        pressed = false;
        began = false;
        ended = false;
        screenPosition = default;

#if ENABLE_INPUT_SYSTEM
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            screenPosition = touchscreen.primaryTouch.position.ReadValue();
            pressed = touchscreen.primaryTouch.press.isPressed;
            began = touchscreen.primaryTouch.press.wasPressedThisFrame;
            ended = touchscreen.primaryTouch.press.wasReleasedThisFrame;
            if (pressed || began || ended)
                return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            pressed = mouse.leftButton.isPressed;
            began = mouse.leftButton.wasPressedThisFrame;
            ended = mouse.leftButton.wasReleasedThisFrame;
            if (pressed || began || ended)
                return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchSupported && Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            screenPosition = touch.position;
            pressed = touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
            began = touch.phase == TouchPhase.Began;
            ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            return true;
        }

        screenPosition = Input.mousePosition;
        pressed = Input.GetMouseButton(0);
        began = Input.GetMouseButtonDown(0);
        ended = Input.GetMouseButtonUp(0);
        return pressed || began || ended;
#else
        return false;
#endif
    }

    private static bool IsPointerOverBlockingUi()
    {
        if (EventSystem.current == null)
            return false;

#if ENABLE_INPUT_SYSTEM
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            return EventSystem.current.IsPointerOverGameObject(touchscreen.primaryTouch.touchId.ReadValue());
#endif

        return EventSystem.current.IsPointerOverGameObject();
    }

    private void EnsureTargetedChargeUI()
    {
        if (_targetedChargeGroup != null)
            return;

        if (!isActiveAndEnabled)
            return;

        Sprite uiSprite = PaperLegendUiSpriteFactory.GetSolidSprite();

        _targetedChargeUiRoot = new GameObject("PaperLegendTargetedChargeUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        var holderRect = _targetedChargeUiRoot.GetComponent<RectTransform>();
        holderRect.SetParent(transform, false);
        holderRect.anchorMin = new Vector2(0.5f, 0.5f);
        holderRect.anchorMax = new Vector2(0.5f, 0.5f);
        holderRect.anchoredPosition = Vector2.zero;
        holderRect.sizeDelta = targetedChargeUISize;

        var canvas = _targetedChargeUiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        _targetedChargeGroup = _targetedChargeUiRoot.GetComponent<CanvasGroup>();
        _targetedChargeGroup.alpha = 0f;
        _targetedChargeGroup.blocksRaycasts = false;
        _targetedChargeGroup.interactable = false;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.SetParent(holderRect, false);
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = targetedChargeUISize;

        var backgroundImage = background.GetComponent<Image>();
        backgroundImage.sprite = uiSprite;
        backgroundImage.color = new Color(0.35f, 0.72f, 1f, 0.22f);
        backgroundImage.type = Image.Type.Simple;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.SetParent(holderRect, false);
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = targetedChargeUISize;

        _targetedChargeFill = fill.GetComponent<Image>();
        _targetedChargeFill.sprite = uiSprite;
        _targetedChargeFill.color = new Color(0.35f, 0.82f, 1f, 0.95f);
        _targetedChargeFill.type = Image.Type.Filled;
        _targetedChargeFill.fillMethod = Image.FillMethod.Radial360;
        _targetedChargeFill.fillOrigin = 2;
        _targetedChargeFill.fillClockwise = false;
        _targetedChargeFill.fillAmount = 0f;
    }

    private void SetTargetedChargeVisible(bool visible)
    {
        if (!visible)
        {
            if (_targetedChargeGroup != null)
                _targetedChargeGroup.alpha = 0f;

            if (_targetedChargeFill != null)
                _targetedChargeFill.fillAmount = 0f;

            return;
        }

        EnsureTargetedChargeUI();
        if (_targetedChargeGroup == null)
            return;

        _targetedChargeGroup.alpha = 1f;
        if (_targetedChargeFill != null)
        {
            _targetedChargeFill.color = _targetedChargeKind == TargetedChargeSkillKind.TidalCataclysm
                ? new Color(1f, 0.58f, 0.2f, 0.95f)
                : new Color(0.35f, 0.82f, 1f, 0.95f);
        }

        UpdateTargetedChargeProgress(0f);
    }

    private void DestroyTargetedChargeUI()
    {
        if (_targetedChargeUiRoot != null)
        {
            Destroy(_targetedChargeUiRoot);
            _targetedChargeUiRoot = null;
        }

        _targetedChargeGroup = null;
        _targetedChargeFill = null;
    }

    private void DestroyTargetedSkillIndicator()
    {
        if (_targetedSkillIndicatorObject == null)
            return;

        Destroy(_targetedSkillIndicatorObject);
        _targetedSkillIndicatorObject = null;
        _targetedSkillIndicatorRenderer = null;
    }

    private void UpdateTargetedChargeProgress(float progress)
    {
        if (_targetedChargeFill != null)
            _targetedChargeFill.fillAmount = Mathf.Clamp01(progress);
    }

    private Vector3 ResolveScreenDragWorldDirection()
    {
        Camera cam = ResolveCamera();
        Vector2 drag = _currentScreenPosition - _startScreenPosition;

        float deadZone = Mathf.Max(0f, aimDragDeadZonePixels);
        if (cam == null || drag.sqrMagnitude <= deadZone * deadZone)
            return Vector3.zero;

        Vector3 cameraRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        Vector3 cameraForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 direction = cameraRight * drag.x + cameraForward * drag.y;
        direction.y = 0f;

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private bool TryRaycastPaperLegendCharacter(
        Vector2 screenPosition,
        out PaperLegendCharacterNetworkHandler handler,
        out RaycastHit hit,
        out string missReason)
    {
        handler = null;
        hit = default;
        missReason = string.Empty;

        Camera cam = ResolveCamera();
        if (cam == null)
        {
            missReason = "Camera.main/raycastCamera is null.";
            return false;
        }

        Ray ray = cam.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, characterMask, raycastTriggerInteraction);
        if (hits == null || hits.Length == 0)
        {
            missReason = $"raycast missed. camera='{cam.name}', mask={characterMask.value}, distance={raycastDistance}, trigger={raycastTriggerInteraction}.";
            return false;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit firstNonCharacterHit = default;
        bool hasNonCharacterHit = false;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit candidateHit = hits[i];
            if (candidateHit.collider == null)
                continue;

            var candidateHandler = candidateHit.collider.GetComponentInParent<PaperLegendCharacterNetworkHandler>();
            if (candidateHandler == null)
            {
                if (!hasNonCharacterHit)
                {
                    firstNonCharacterHit = candidateHit;
                    hasNonCharacterHit = true;
                }

                continue;
            }

            handler = candidateHandler;
            hit = candidateHit;

            if (hasNonCharacterHit)
            {
                LogDebug($"Raycast skipped non-character hit '{firstNonCharacterHit.collider.name}' and selected paper character '{hit.collider.name}'.");
            }

            return true;
        }

        if (hasNonCharacterHit)
        {
            hit = firstNonCharacterHit;
            missReason = $"raycast hit {hits.Length} object(s); nearest was '{hit.collider.name}' on layer '{LayerMask.LayerToName(hit.collider.gameObject.layer)}', but no PaperLegendCharacterNetworkHandler was found in any hit.";
        }
        else
        {
            missReason = $"raycast hit {hits.Length} object(s), but all colliders were null.";
        }

        return false;
    }

    private bool TryHandleHero10000004EnemyTargetTap(
        PaperLegendCharacterNetworkHandler localHero,
        PaperLegendCharacterNetworkHandler enemy,
        Vector2 screenPosition)
    {
        if (localHero == null || enemy == null || !localHero.HasInputAuthority)
            return false;

        if (localHero.CharacterModelId != PaperLegendHero10000004SonTinhSkillSet.HeroId)
            return false;

        if (!enemy.IsAlive || localHero.IsSameFaction(enemy))
            return false;

        if (!localHero.CanUseSkill(2) && !localHero.Hero10000004Skill2Armed)
            return false;

        if (IsPointerOverBlockingUi())
            return false;

        QueueSkillInputWithPlayerTarget(2, enemy.PlayerId);
        return true;
    }

    private static bool TryFindLocalShoveStunCaster(out PaperLegendCharacterNetworkHandler handler)
    {
        handler = null;

        if (!TryFindLocalInputAuthorityCharacter(out PaperLegendCharacterNetworkHandler candidate))
            return false;

        if (!candidate.Hero10000002ShoveStunArmed)
            return false;

        handler = candidate;
        return true;
    }

    private static bool CanAutoUseShoveStun(PaperLegendCharacterNetworkHandler candidate)
    {
        return candidate != null
            && candidate.HasInputAuthority
            && candidate.CharacterModelId == 10000002
            && candidate.Skill2Level > 0
            && candidate.IsAlive
            && (candidate.Hero10000002ShoveStunArmed || candidate.CanUseSkill(2));
    }

    private bool IsWithinShoveStunRange(PaperLegendCharacterNetworkHandler caster, PaperLegendCharacterNetworkHandler target)
    {
        if (caster == null || target == null)
            return false;

        Vector3 offset = target.transform.position - caster.transform.position;
        offset.y = 0f;
        float radius = Mathf.Max(0.1f, shoveStunNearbyRadius);
        return offset.sqrMagnitude <= radius * radius;
    }

    private void UpdateShoveStunHintIcons()
    {
        if (!showShoveStunHintIcon || !TryFindLocalShoveStunCaster(out PaperLegendCharacterNetworkHandler caster))
        {
            HideShoveStunHintIcons();
            return;
        }

        Camera cam = ResolveCamera();
        if (cam == null)
        {
            HideShoveStunHintIcons();
            return;
        }

        HashSet<PaperLegendCharacterNetworkHandler> activeTargets = new HashSet<PaperLegendCharacterNetworkHandler>();
#if UNITY_2023_1_OR_NEWER
        PaperLegendCharacterNetworkHandler[] candidates = FindObjectsByType<PaperLegendCharacterNetworkHandler>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
        PaperLegendCharacterNetworkHandler[] candidates = FindObjectsOfType<PaperLegendCharacterNetworkHandler>();
#endif

        if (candidates != null)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                PaperLegendCharacterNetworkHandler target = candidates[i];
                if (target == null
                    || target == caster
                    || !target.IsAlive
                    || caster.IsSameFaction(target)
                    || !IsWithinShoveStunRange(caster, target))
                {
                    continue;
                }

                activeTargets.Add(target);
                UpdateShoveStunHintIcon(target, cam);
            }
        }

        _shoveStunHintRemovalBuffer.Clear();
        foreach (var pair in _shoveStunHintIcons)
        {
            if (!activeTargets.Contains(pair.Key))
                _shoveStunHintRemovalBuffer.Add(pair.Key);
        }

        for (int i = 0; i < _shoveStunHintRemovalBuffer.Count; i++)
            RemoveShoveStunHintIcon(_shoveStunHintRemovalBuffer[i]);
    }

    private void UpdateShoveStunHintIcon(PaperLegendCharacterNetworkHandler target, Camera cam)
    {
        if (!_shoveStunHintIcons.TryGetValue(target, out GameObject iconObject) || iconObject == null)
        {
            iconObject = CreateShoveStunHintIcon();
            _shoveStunHintIcons[target] = iconObject;
        }

        Bounds bounds = target.GetWorldBounds();
        Vector3 offset = shoveStunHintWorldOffset;
        if (offset.y <= 0f)
            offset.y = Mathf.Max(0.35f, bounds.extents.y + 0.25f);

        iconObject.transform.position = bounds.center + offset;
        iconObject.transform.rotation = cam.transform.rotation;
        iconObject.SetActive(true);
    }

    private GameObject CreateShoveStunHintIcon()
    {
        var iconObject = new GameObject("PaperLegendShoveStunHintIcon");
        var textMesh = iconObject.AddComponent<TextMesh>();
        textMesh.text = "!";
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = Mathf.Max(0.05f, shoveStunHintCharacterSize);
        textMesh.fontSize = 96;
        textMesh.color = shoveStunHintColor;
        var renderer = iconObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return iconObject;
    }

    private void HideShoveStunHintIcons()
    {
        foreach (var pair in _shoveStunHintIcons)
        {
            if (pair.Value != null)
                pair.Value.SetActive(false);
        }
    }

    private void RemoveShoveStunHintIcon(PaperLegendCharacterNetworkHandler target)
    {
        if (!_shoveStunHintIcons.TryGetValue(target, out GameObject iconObject))
            return;

        if (iconObject != null)
            Destroy(iconObject);

        _shoveStunHintIcons.Remove(target);
    }

    private void DestroyShoveStunHintIcons()
    {
        foreach (var pair in _shoveStunHintIcons)
        {
            if (pair.Value != null)
                Destroy(pair.Value);
        }

        _shoveStunHintIcons.Clear();
        _shoveStunHintRemovalBuffer.Clear();
    }

    private static bool TryFindLocalDirectionalSkillTarget(out PaperLegendCharacterNetworkHandler handler)
    {
        handler = null;

        if (!TryFindLocalInputAuthorityCharacter(out PaperLegendCharacterNetworkHandler candidate))
            return false;

        if ((candidate.Hero10000003WaterBurstArmed
                || candidate.Hero10000003WavePushArmed
                || candidate.Hero10000001PaperArrowArmed
                || candidate.Hero10000004HomingSwordArmed
                || (candidate.CharacterModelId == PaperLegendHero10000004SonTinhSkillSet.HeroId && HasPendingHero10000004HomingSwordUse())
                || (candidate.Hero10000002ForwardSlideArmed && candidate.Hero10000002ForwardSlideRemaining > 0))
            && candidate.CanAcceptLocalFlick)
        {
            handler = candidate;
            return true;
        }

        return false;
    }

    private static bool HasPendingHero10000004HomingSwordUse()
    {
        return Instance != null
            && Instance._hasPendingInput
            && Instance._pendingInput.SkillRequested
            && Mathf.Clamp(Instance._pendingInput.SkillSlot, 1, 4) == 1;
    }

    private static bool TryFindLocalInputAuthorityCharacter(out PaperLegendCharacterNetworkHandler handler)
    {
        handler = null;

#if UNITY_2023_1_OR_NEWER
        PaperLegendCharacterNetworkHandler[] candidates = FindObjectsByType<PaperLegendCharacterNetworkHandler>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
#else
        PaperLegendCharacterNetworkHandler[] candidates = FindObjectsOfType<PaperLegendCharacterNetworkHandler>(true);
#endif

        if (candidates == null)
            return false;

        for (int i = 0; i < candidates.Length; i++)
        {
            PaperLegendCharacterNetworkHandler candidate = candidates[i];
            if (candidate == null || !candidate.HasInputAuthority)
                continue;

            handler = candidate;
            return true;
        }

        return false;
    }

    private bool ResolveTargetedSkillWorldPosition(Vector2 screenPosition, out Vector3 worldPosition)
    {
        worldPosition = default;

        Camera cam = ResolveCamera();
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(screenPosition);
        float planeY = _targetedSkillOwner != null ? _targetedSkillOwner.transform.position.y : 0f;

        RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, ~0, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                    continue;

                if (hit.collider.GetComponentInParent<PaperLegendCharacterNetworkHandler>() != null)
                    continue;

                worldPosition = hit.point;
                return true;
            }
        }

        Plane fallbackPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (!fallbackPlane.Raycast(ray, out float enter))
            return false;

        worldPosition = ray.GetPoint(enter);
        worldPosition.y = planeY;

        Vector3 probeOrigin = worldPosition + Vector3.up * 30f;
        if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit downHit, 60f, ~0, QueryTriggerInteraction.Ignore)
            && downHit.collider != null
            && downHit.collider.GetComponentInParent<PaperLegendCharacterNetworkHandler>() == null)
        {
            worldPosition = downHit.point;
        }

        return true;
    }

    private void EnsureTargetedSkillIndicator()
    {
        if (_targetedSkillIndicatorObject != null && _targetedSkillIndicatorRenderer != null)
            return;

        _targetedSkillIndicatorObject = new GameObject("PaperLegendTargetedSkillIndicator");
        DontDestroyOnLoad(_targetedSkillIndicatorObject);

        _targetedSkillIndicatorRenderer = _targetedSkillIndicatorObject.AddComponent<LineRenderer>();
        _targetedSkillIndicatorRenderer.loop = true;
        _targetedSkillIndicatorRenderer.useWorldSpace = false;
        _targetedSkillIndicatorRenderer.widthMultiplier = 0.055f;
        _targetedSkillIndicatorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _targetedSkillIndicatorRenderer.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        _targetedSkillIndicatorRenderer.material = new Material(shader);
        _targetedSkillIndicatorRenderer.startColor = targetedSkillIndicatorColor;
        _targetedSkillIndicatorRenderer.endColor = targetedSkillIndicatorColor;
    }

    private void UpdateTargetedSkillIndicator()
    {
        EnsureTargetedSkillIndicator();
        if (_targetedSkillIndicatorObject == null || _targetedSkillIndicatorRenderer == null)
            return;

        int segments = Mathf.Max(8, targetedSkillIndicatorSegments);
        _targetedSkillIndicatorRenderer.positionCount = segments;
        float radius = Mathf.Max(0.1f, _targetedSkillRadius);
        for (int i = 0; i < segments; i++)
        {
            float radians = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 point = new Vector3(Mathf.Cos(radians) * radius, 0f, Mathf.Sin(radians) * radius);
            _targetedSkillIndicatorRenderer.SetPosition(i, point);
        }

        _targetedSkillIndicatorObject.transform.position = _targetedSkillWorldPosition + Vector3.up * targetedSkillIndicatorHeightOffset;
        _targetedSkillIndicatorObject.SetActive(true);
    }

    private void HideTargetedSkillIndicator()
    {
        if (_targetedSkillIndicatorObject != null)
            _targetedSkillIndicatorObject.SetActive(false);
    }

    private void StartChargeUi()
    {
        _usingPowerBar = false;

        if (!useSharedPowerBar)
            return;

        PowerBarController powerBar = PowerBarController.Instance;
        if (powerBar == null)
            return;

        powerBar.StartPingPong();
        _usingPowerBar = true;
    }

    private float ResolveChargeForce01()
    {
        if (_usingPowerBar && PowerBarController.Instance != null)
            return Mathf.Clamp01(PowerBarController.Instance.StopPingPongAndGet01());

        return ResolveFallbackCharge01();
    }

    private float ResolveSwipeSpeedForce01()
    {
        float elapsed = Mathf.Max(0.05f, Time.unscaledTime - _startTime);
        float pixelsPerSecond = (_currentScreenPosition - _startScreenPosition).magnitude / elapsed;
        return Mathf.Clamp01(pixelsPerSecond / Mathf.Max(1f, forwardSlideFullForcePixelsPerSecond));
    }

    private float ResolveFallbackCharge01()
    {
        float heldSeconds = Mathf.Max(0f, Time.unscaledTime - _startTime);
        float charge01 = Mathf.Clamp01(heldSeconds / Mathf.Max(0.01f, maxChargeSeconds));

        if (charge01 >= 1f && _fallbackFullPowerTime < 0f)
            _fallbackFullPowerTime = Time.unscaledTime;

        return charge01;
    }

    private bool ShouldAutoRelease()
    {
        float delay = Mathf.Max(0f, autoReleaseAfterFullPowerSeconds);

        if (_usingPowerBar && PowerBarController.Instance != null)
            return PowerBarController.Instance.IsFullPower
                && PowerBarController.Instance.FullPowerElapsed >= delay;

        ResolveFallbackCharge01();
        return _fallbackFullPowerTime >= 0f
            && Time.unscaledTime - _fallbackFullPowerTime >= delay;
    }

    private void CancelTracking()
    {
        ResetChargeUi();
        _isTracking = false;
        _isShoveStunSwipe = false;
        _isForwardSlideSwipe = false;
        _isHomingSwordDirectionalSwipe = false;
        _shoveStunVictim = null;
        _target = null;
    }

    private void ResetChargeUi()
    {
        if (_usingPowerBar && PowerBarController.Instance != null)
            PowerBarController.Instance.ResetBar();

        _usingPowerBar = false;
        _fallbackFullPowerTime = -1f;
    }

    private Camera ResolveCamera()
    {
        if (raycastCamera != null)
            return raycastCamera;

        return Camera.main;
    }

    private void LogRejectedInput(string reason)
    {
        if (Time.unscaledTime < _nextRejectedLogTime)
            return;

        _nextRejectedLogTime = Time.unscaledTime + 1f;
        Debug.LogWarning($"[PaperLegends][Input] Flick ignored: {reason}");
    }

    private void LogRaycastMiss(string reason)
    {
        if (!debugInputLogs || Time.unscaledTime < _nextMissLogTime)
            return;

        _nextMissLogTime = Time.unscaledTime + 0.5f;
        Debug.LogWarning($"[PaperLegends][Input] Pointer down did not select a paper character: {reason}");
    }

    private void LogDebug(string message)
    {
        if (!debugInputLogs)
            return;

        Debug.Log($"[PaperLegends][Input] {message}");
    }

    private static string ResolveInputAuthorityLabel(PaperLegendCharacterNetworkHandler handler)
    {
        if (handler == null || handler.Object == null)
            return "null";

        return handler.Object.InputAuthority.ToString();
    }
#endif
}
