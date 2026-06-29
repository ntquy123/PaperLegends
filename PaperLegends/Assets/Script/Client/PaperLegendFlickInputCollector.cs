using System;
using Fusion;
using UnityEngine;
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

    [Header("Hero 10000002 Skill 3")]
    [SerializeField, Min(0.1f)] private float shoveStunNearbyRadius = 2.8f;

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
    private PaperLegendPlayerInputData _pendingInput;
    private bool _hasPendingInput;
    private float _nextRejectedLogTime;
    private float _nextMissLogTime;
    private bool _isTargetingSkill;
    private int _targetedSkillSlot;
    private int _targetedSkillId;
    private float _targetedSkillRadius;
    private Vector3 _targetedSkillWorldPosition;
    private PaperLegendCharacterNetworkHandler _targetedSkillOwner;
    private GameObject _targetedSkillIndicatorObject;
    private LineRenderer _targetedSkillIndicatorRenderer;

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

    private void Update()
    {
#if !UNITY_SERVER
        PollPointer();
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
                    "global directional skill swipe");
                return;
            }

            LogRaycastMiss(missReason);
            return;
        }

        LogDebug(
            $"Raycast hit '{hit.collider.name}' -> player={handler.PlayerId}, model={handler.CharacterModelId}, state={handler.State}, grounded={handler.IsGrounded}, alive={handler.IsAlive}, hasInputAuthority={handler.HasInputAuthority}, objectInputAuthority={ResolveInputAuthorityLabel(handler)}.");

        if (!handler.HasInputAuthority)
        {
            if (TryFindLocalShoveStunCaster(out PaperLegendCharacterNetworkHandler shoveCaster)
                && handler.IsAlive
                && !shoveCaster.IsSameFaction(handler)
                && IsWithinShoveStunRange(shoveCaster, handler))
            {
                BeginShoveStunTracking(shoveCaster, handler, screenPosition, hit.point, hit.normal);
                return;
            }

            if (TryFindLocalDirectionalSkillTarget(out var directionalSkillTarget))
            {
                BeginTrackingForTarget(
                    directionalSkillTarget,
                    screenPosition,
                    directionalSkillTarget.transform.position,
                    Vector3.up,
                    $"global directional skill swipe over non-owned player={handler.PlayerId}");
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

        BeginTrackingForTarget(handler, screenPosition, hit.point, hit.normal, "paper character hit");
    }

    private void BeginShoveStunTracking(
        PaperLegendCharacterNetworkHandler caster,
        PaperLegendCharacterNetworkHandler victim,
        Vector2 screenPosition,
        Vector3 contactWorldPosition,
        Vector3 contactSurfaceNormal)
    {
        if (caster == null || victim == null)
            return;

        _isTracking = true;
        _isShoveStunSwipe = true;
        _target = caster;
        _shoveStunVictim = victim;
        _startScreenPosition = screenPosition;
        _currentScreenPosition = screenPosition;
        _contactWorldPosition = contactWorldPosition;
        _contactSurfaceNormal = contactSurfaceNormal;
        _startTime = Time.unscaledTime;
        _fallbackFullPowerTime = -1f;
        StartChargeUi();
        LogDebug($"Started shove stun swipe for caster={caster.PlayerId} -> victim={victim.PlayerId}, contact={_contactWorldPosition}.");
    }

    private void BeginTrackingForTarget(
        PaperLegendCharacterNetworkHandler handler,
        Vector2 screenPosition,
        Vector3 contactWorldPosition,
        Vector3 contactSurfaceNormal,
        string context)
    {
        if (handler == null)
            return;

        _isTracking = true;
        _isShoveStunSwipe = false;
        _shoveStunVictim = null;
        _target = handler;
        _startScreenPosition = screenPosition;
        _currentScreenPosition = screenPosition;
        _contactWorldPosition = contactWorldPosition;
        _contactSurfaceNormal = contactSurfaceNormal;
        _startTime = Time.unscaledTime;
        _fallbackFullPowerTime = -1f;
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
            if (!_target.Hero10000002ShoveStunArmed || _shoveStunVictim == null || !_shoveStunVictim.IsAlive || _target.IsSameFaction(_shoveStunVictim))
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

        if (ShouldAutoRelease())
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
        float force01 = Mathf.Max(minimumForce01, ResolveChargeForce01());

        bool carrySkillRequest = _hasPendingInput && _pendingInput.SkillRequested;
        int carriedSkillSlot = carrySkillRequest ? _pendingInput.SkillSlot : 0;
        bool carrySkillTarget = _hasPendingInput && _pendingInput.SkillTargetWorldPositionSet;
        Vector3 carriedSkillTarget = carrySkillTarget ? _pendingInput.SkillTargetWorldPosition : default;

        _flickSequence++;
        _pendingInput = new PaperLegendPlayerInputData
        {
            FlickRequested = true,
            FlickSequence = _flickSequence,
            ContactWorldPosition = _contactWorldPosition,
            ContactSurfaceNormal = _contactSurfaceNormal,
            AimWorldDirection = ResolveScreenDragWorldDirection(),
            Force01 = force01,
            FlickTargetPlayerId = _isShoveStunSwipe && _shoveStunVictim != null ? _shoveStunVictim.PlayerId : 0,
            SkillRequested = carrySkillRequest,
            SkillSlot = carriedSkillSlot,
            SkillTargetWorldPositionSet = carrySkillTarget,
            SkillTargetWorldPosition = carriedSkillTarget
        };

        _hasPendingInput = true;
        LogDebug($"Queued flick input seq={_flickSequence}, force={force01:0.00}, contact={_pendingInput.ContactWorldPosition}, direction={_pendingInput.AimWorldDirection}.");
        ResetChargeUi();
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
            QueueTargetedSkillInput();

        HideTargetedSkillIndicator();
        _isTargetingSkill = false;
        _targetedSkillOwner = null;
        _targetedSkillSlot = 0;
        _targetedSkillId = 0;
        _targetedSkillRadius = 0f;
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

    private bool IsWithinShoveStunRange(PaperLegendCharacterNetworkHandler caster, PaperLegendCharacterNetworkHandler target)
    {
        if (caster == null || target == null)
            return false;

        Vector3 offset = target.transform.position - caster.transform.position;
        offset.y = 0f;
        float radius = Mathf.Max(0.1f, shoveStunNearbyRadius);
        return offset.sqrMagnitude <= radius * radius;
    }

    private static bool TryFindLocalDirectionalSkillTarget(out PaperLegendCharacterNetworkHandler handler)
    {
        handler = null;

        if (!TryFindLocalInputAuthorityCharacter(out PaperLegendCharacterNetworkHandler candidate))
            return false;

        if ((candidate.Hero10000003WaterBurstArmed || candidate.Hero10000003WavePushArmed || candidate.Hero10000001PaperArrowArmed || (candidate.Hero10000002ForwardSlideArmed && candidate.Hero10000002ForwardSlideRemaining > 0)) && candidate.CanAcceptLocalFlick)
        {
            handler = candidate;
            return true;
        }

        return false;
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
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            worldPosition = hit.point;
            return true;
        }

        float planeY = _targetedSkillOwner != null ? _targetedSkillOwner.transform.position.y : 0f;
        Plane fallbackPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (fallbackPlane.Raycast(ray, out float enter))
        {
            worldPosition = ray.GetPoint(enter);
            return true;
        }

        return false;
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
