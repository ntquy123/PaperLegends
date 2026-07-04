using Fusion;
using UnityEngine;
#if !UNITY_SERVER
using DG.Tweening;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class DrumObjective : NetworkBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float minRespawnSeconds = 13f;
    [SerializeField, Min(0.1f)] private float maxRespawnSeconds = 18f;
    [SerializeField, Min(0.1f)] private float drumWarMinRespawnSeconds = 10f;
    [SerializeField, Min(0.1f)] private float drumWarMaxRespawnSeconds = 10f;
    [SerializeField, Min(0.1f)] private float captureSeconds = 5f;
    [SerializeField, Min(0.1f)] private float warningSeconds = 2f;

    [Header("Presentation")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private AudioSource warningAudioSource;
    [SerializeField] private AudioSource captureAlertAudioSource;
    [SerializeField, Min(1)] private int captureAlertPulseCount = 3;
    [SerializeField, Min(0.1f)] private float captureAlertIntervalSeconds = 1f;
    [SerializeField] private Transform terrainShakeTarget;
    [SerializeField, Min(0f)] private float terrainShakeStrength = 0.08f;
    [SerializeField, Min(0f)] private float terrainShakeDuration = 0.45f;

    [Networked, OnChangedRender(nameof(OnDrumStateChanged))] public NetworkBool IsActive { get; private set; }
    [Networked, OnChangedRender(nameof(OnDrumStateChanged))] public NetworkBool IsWarning { get; private set; }
    [Networked] public PlayerRef CapturingPlayer { get; private set; }
    [Networked] public int CapturingPlayerId { get; private set; }
    [Networked] public float CaptureProgress01 { get; private set; }
    [Networked, OnChangedRender(nameof(OnCaptureAlertChanged))]
    private int CaptureAlertTick { get; set; }
    [Networked] private TickTimer RespawnTimer { get; set; }
    [Networked] private TickTimer WarningTimer { get; set; }
    [Networked] private TickTimer CaptureAlertTimer { get; set; }

    private Collider _trigger;
    private PaperLegendCharacterNetworkHandler _capturingCharacter;
    private float _captureElapsedSeconds;
    private int _captureAlertPulsesRemaining;
    private bool _usingDrumWarRespawnTiming;

    public override void Spawned()
    {
        CacheComponents();

        if (HasStateAuthority)
            ScheduleNextSpawn();

        ApplyVisualState();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!IsActive)
        {
            ApplyDrumWarRespawnAccelerationIfNeeded();
            TickInactiveState();
            return;
        }

        TickCaptureState();
    }

    public override void Render()
    {
        ApplyVisualState();
    }

    public void ConfigureCaptureAreaFromObjectiveAnchor(Transform anchor)
    {
        if (anchor == null)
            return;

        CacheComponents();

        transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        CopyCaptureColliderFromAnchor(anchor);

        Debug.Log($"[PaperLegends][Drum] Capture area bound to DrumObjective anchor '{anchor.name}' at {anchor.position}.");
    }

    private void TickInactiveState()
    {
        if (!IsWarning && WarningTimer.Expired(Runner))
            StartWarning();

        if (RespawnTimer.Expired(Runner))
            ActivateDrum();
    }

    private void TickCaptureState()
    {
        if (_capturingCharacter == null || !_capturingCharacter.IsAlive || !IsCharacterInside(_capturingCharacter))
        {
            CancelCapture();
            return;
        }

        TickCaptureAlert();

        _captureElapsedSeconds += Runner.DeltaTime;
        CaptureProgress01 = Mathf.Clamp01(_captureElapsedSeconds / Mathf.Max(0.1f, captureSeconds));

        if (_captureElapsedSeconds < captureSeconds)
            return;

        GameScoreManager.Instance?.CaptureDrum(_capturingCharacter);
        DeactivateAfterCapture();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority || !IsActive)
            return;

        var character = other != null ? other.GetComponentInParent<PaperLegendCharacterNetworkHandler>() : null;
        if (character == null || !character.IsAlive)
            return;

        if (_capturingCharacter == character)
            return;

        if (_capturingCharacter != null)
            return;

        StartCapture(character);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!HasStateAuthority || !IsActive || _capturingCharacter == null)
            return;

        var character = other != null ? other.GetComponentInParent<PaperLegendCharacterNetworkHandler>() : null;
        if (character == _capturingCharacter)
            CancelCapture();
    }

    private void StartCapture(PaperLegendCharacterNetworkHandler character)
    {
        _capturingCharacter = character;
        _captureElapsedSeconds = 0f;
        CapturingPlayer = character.Object != null ? character.Object.InputAuthority : PlayerRef.None;
        CapturingPlayerId = character.PlayerId;
        CaptureProgress01 = 0f;
        StartCaptureAlertSequence();
    }

    private void CancelCapture()
    {
        _capturingCharacter = null;
        _captureElapsedSeconds = 0f;
        CapturingPlayer = PlayerRef.None;
        CapturingPlayerId = 0;
        CaptureProgress01 = 0f;
        CaptureAlertTimer = TickTimer.None;
        _captureAlertPulsesRemaining = 0;
    }

    private void DeactivateAfterCapture()
    {
        CancelCapture();
        IsActive = false;
        IsWarning = false;
        ScheduleNextSpawn();
    }

    private void ScheduleNextSpawn()
    {
        float delay = ResolveNextRespawnDelay(out bool useDrumWarTiming);
        SetRespawnTimers(delay);
        _usingDrumWarRespawnTiming = useDrumWarTiming;
        IsActive = false;
        IsWarning = false;
        CancelCapture();
    }

    private void StartWarning()
    {
        IsWarning = true;
    }

    private void ActivateDrum()
    {
        IsActive = true;
        IsWarning = false;
        RespawnTimer = TickTimer.None;
        WarningTimer = TickTimer.None;
        CancelCapture();
    }

    private bool IsCharacterInside(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null || _trigger == null)
            return false;

        Bounds bounds = character.GetWorldBounds();
        Vector3 closest = _trigger.ClosestPoint(bounds.center);
        Vector3 flatDelta = closest - bounds.center;
        flatDelta.y = 0f;
        return _trigger.bounds.Intersects(bounds) || flatDelta.sqrMagnitude <= 0.04f;
    }

    private void ApplyDrumWarRespawnAccelerationIfNeeded()
    {
        if (_usingDrumWarRespawnTiming)
            return;

        var scoreManager = GameScoreManager.Instance;
        if (scoreManager == null || !scoreManager.IsDrumWarActive)
            return;

        float warMax = Mathf.Max(drumWarMinRespawnSeconds, drumWarMaxRespawnSeconds);
        float? remaining = RespawnTimer.RemainingTime(Runner);
        if (remaining.HasValue && remaining.Value <= warMax)
        {
            _usingDrumWarRespawnTiming = true;
            return;
        }

        float delay = ResolveDrumWarRespawnDelay();
        SetRespawnTimers(delay);
        _usingDrumWarRespawnTiming = true;
        Debug.Log($"[PaperLegends][Drum] Drum War active. Respawn accelerated to {delay:0.0}s.");
    }

    private float ResolveNextRespawnDelay(out bool useDrumWarTiming)
    {
        var scoreManager = GameScoreManager.Instance;
        useDrumWarTiming = scoreManager != null && scoreManager.IsDrumWarActive;
        return useDrumWarTiming ? ResolveDrumWarRespawnDelay() : ResolveNormalRespawnDelay();
    }

    private float ResolveNormalRespawnDelay()
    {
        float min = Mathf.Min(minRespawnSeconds, maxRespawnSeconds);
        float max = Mathf.Max(minRespawnSeconds, maxRespawnSeconds);
        return Random.Range(min, max);
    }

    private float ResolveDrumWarRespawnDelay()
    {
        float min = Mathf.Min(drumWarMinRespawnSeconds, drumWarMaxRespawnSeconds);
        float max = Mathf.Max(drumWarMinRespawnSeconds, drumWarMaxRespawnSeconds);
        return Random.Range(min, max);
    }

    private void SetRespawnTimers(float delay)
    {
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.1f, delay));
        WarningTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0f, delay - warningSeconds));
    }

    private void StartCaptureAlertSequence()
    {
        _captureAlertPulsesRemaining = Mathf.Max(1, captureAlertPulseCount);
        PulseCaptureAlert();
    }

    private void TickCaptureAlert()
    {
        if (_captureAlertPulsesRemaining <= 0)
            return;

        if (!CaptureAlertTimer.Expired(Runner))
            return;

        PulseCaptureAlert();
    }

    private void PulseCaptureAlert()
    {
        CaptureAlertTick++;
        _captureAlertPulsesRemaining--;
        CaptureAlertTimer = _captureAlertPulsesRemaining > 0
            ? TickTimer.CreateFromSeconds(Runner, captureAlertIntervalSeconds)
            : TickTimer.None;
    }

    private void OnDrumStateChanged()
    {
        ApplyVisualState();

        if (IsWarning)
            PlaySpawnWarningFeedback();
    }

    private void PlaySpawnWarningFeedback()
    {
#if !UNITY_SERVER
        if (warningAudioSource != null)
            warningAudioSource.Play();

        if (terrainShakeTarget != null && terrainShakeStrength > 0f && terrainShakeDuration > 0f)
        {
            DOTween.Kill(terrainShakeTarget);
            terrainShakeTarget.DOShakePosition(terrainShakeDuration, terrainShakeStrength, 12, 90f, false, true);
        }
#endif
    }

    private void OnCaptureAlertChanged()
    {
        PlayCaptureAlertFeedback();
    }

    private void PlayCaptureAlertFeedback()
    {
#if !UNITY_SERVER
        AudioSource source = captureAlertAudioSource != null ? captureAlertAudioSource : warningAudioSource;
        if (source != null)
            source.Play();
#endif
    }

    private void ApplyVisualState()
    {
        CacheComponents();

        if (visualRoot != null && visualRoot.activeSelf != IsActive)
            visualRoot.SetActive(IsActive);

        if (_trigger != null)
            _trigger.enabled = IsActive;
    }

    private void CacheComponents()
    {
        if (_trigger == null)
        {
            _trigger = GetComponent<Collider>();
            _trigger.isTrigger = true;
        }

        // Keep this NetworkObject active. Assign visualRoot to a child object if visuals should hide while inactive.
    }

    private void CopyCaptureColliderFromAnchor(Transform anchor)
    {
        if (anchor == null || _trigger == null)
            return;

        var source = anchor.GetComponent<Collider>();
        if (source == null)
            return;

        if (_trigger is BoxCollider targetBox && source is BoxCollider sourceBox)
        {
            targetBox.center = sourceBox.center;
            targetBox.size = sourceBox.size;
        }
        else if (_trigger is CapsuleCollider targetCapsule && source is CapsuleCollider sourceCapsule)
        {
            targetCapsule.center = sourceCapsule.center;
            targetCapsule.radius = sourceCapsule.radius;
            targetCapsule.height = sourceCapsule.height;
            targetCapsule.direction = sourceCapsule.direction;
        }

        _trigger.isTrigger = true;
    }
}
