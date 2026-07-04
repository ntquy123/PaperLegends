#if UNITY_SERVER
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class PaperLegendServerDrumObjectiveZone : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float minRespawnSeconds = 13f;
    [SerializeField, Min(0.1f)] private float maxRespawnSeconds = 18f;
    [SerializeField, Min(0.1f)] private float drumWarMinRespawnSeconds = 10f;
    [SerializeField, Min(0.1f)] private float drumWarMaxRespawnSeconds = 10f;
    [SerializeField, Min(0.1f)] private float captureSeconds = 5f;
    [SerializeField, Min(0.1f)] private float warningSeconds = 2f;

    [Header("Alert")]
    [SerializeField, Min(1)] private int captureAlertPulseCount = 3;
    [SerializeField, Min(0.1f)] private float captureAlertIntervalSeconds = 1f;

    public bool IsActive => _isActive;
    public float CaptureProgress01 => Mathf.Clamp01(_captureElapsedSeconds / Mathf.Max(0.1f, captureSeconds));

    private Collider _captureTrigger;
    private PaperLegendCharacterNetworkHandler _capturingCharacter;
    private float _captureElapsedSeconds;
    private float _respawnAtSeconds;
    private float _warningAtSeconds;
    private float _nextCaptureAlertAtSeconds;
    private int _captureAlertPulsesRemaining;
    private int _captureAlertTick;
    private bool _isActive;
    private bool _isWarning;
    private bool _usingDrumWarRespawnTiming;

    public void ConfigureFromMapAnchor(Transform anchor)
    {
        if (anchor == null)
            return;

        transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        CacheComponents();
        _captureTrigger.isTrigger = true;
        _captureTrigger.enabled = false;

        Debug.Log($"[PaperLegends][Drum] Server objective zone uses configured DrumObjective trigger '{anchor.name}' at {anchor.position}.");
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        ScheduleNextSpawn();
    }

    private void Update()
    {
        var scoreManager = GameScoreManager.Instance;
        if (scoreManager == null || !scoreManager.HasStateAuthority)
            return;

        if (!_isActive)
        {
            ApplyDrumWarRespawnAccelerationIfNeeded(scoreManager);
            TickInactiveState(scoreManager);
            SyncState(scoreManager);
            return;
        }

        TickCaptureState(scoreManager);
        SyncState(scoreManager);
    }

    private void TickInactiveState(GameScoreManager scoreManager)
    {
        float now = Time.time;
        if (!_isWarning && now >= _warningAtSeconds)
            _isWarning = true;

        if (now < _respawnAtSeconds)
            return;

        _isActive = true;
        _isWarning = false;
        _captureTrigger.enabled = true;
        CancelCapture();
        SyncState(scoreManager);
        Debug.Log("[PaperLegends][Drum] DrumObjective trigger is active.");
    }

    private void TickCaptureState(GameScoreManager scoreManager)
    {
        if (_capturingCharacter == null || !_capturingCharacter.IsAlive || !IsCharacterInside(_capturingCharacter))
        {
            var nextCharacter = FindFirstCharacterInside();
            if (nextCharacter == null)
            {
                CancelCapture();
                return;
            }

            StartCapture(nextCharacter);
        }

        TickCaptureAlert();
        _captureElapsedSeconds += Time.deltaTime;

        if (_captureElapsedSeconds < captureSeconds)
            return;

        scoreManager.CaptureDrum(_capturingCharacter);
        DeactivateAfterCapture();
    }

    private PaperLegendCharacterNetworkHandler FindFirstCharacterInside()
    {
        var matchHost = PaperLegendMatchNetworkHost.Instance;
        if (matchHost == null)
            return null;

        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = matchHost.GetRegisteredPlayers();
        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler candidate = players[i];
            if (candidate != null && candidate.IsAlive && IsCharacterInside(candidate))
                return candidate;
        }

        return null;
    }

    private bool IsCharacterInside(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null || _captureTrigger == null)
            return false;

        Bounds characterBounds = character.GetWorldBounds();
        return _captureTrigger.bounds.Intersects(characterBounds)
            || Vector3.SqrMagnitude(_captureTrigger.ClosestPoint(characterBounds.center) - characterBounds.center) <= 0.04f;
    }

    private void StartCapture(PaperLegendCharacterNetworkHandler character)
    {
        _capturingCharacter = character;
        _captureElapsedSeconds = 0f;
        _captureAlertPulsesRemaining = Mathf.Max(1, captureAlertPulseCount);
        PulseCaptureAlert();
        Debug.Log($"[PaperLegends][Drum] player={character.PlayerId} started capturing DrumObjective.");
    }

    private void CancelCapture()
    {
        _capturingCharacter = null;
        _captureElapsedSeconds = 0f;
        _captureAlertPulsesRemaining = 0;
        _nextCaptureAlertAtSeconds = 0f;
    }

    private void DeactivateAfterCapture()
    {
        _isActive = false;
        _isWarning = false;
        _captureTrigger.enabled = false;
        CancelCapture();
        ScheduleNextSpawn();
    }

    private void ScheduleNextSpawn()
    {
        CacheComponents();
        float delay = ResolveNextRespawnDelay(out bool useDrumWarTiming);
        _respawnAtSeconds = Time.time + delay;
        _warningAtSeconds = _respawnAtSeconds - Mathf.Max(0f, warningSeconds);
        _usingDrumWarRespawnTiming = useDrumWarTiming;
        _isActive = false;
        _isWarning = false;
        _captureTrigger.enabled = false;
        CancelCapture();
    }

    private void ApplyDrumWarRespawnAccelerationIfNeeded(GameScoreManager scoreManager)
    {
        if (_usingDrumWarRespawnTiming || scoreManager == null || !scoreManager.IsDrumWarActive)
            return;

        float warMax = Mathf.Max(drumWarMinRespawnSeconds, drumWarMaxRespawnSeconds);
        float remaining = _respawnAtSeconds - Time.time;
        if (remaining <= warMax)
        {
            _usingDrumWarRespawnTiming = true;
            return;
        }

        float delay = ResolveDrumWarRespawnDelay();
        _respawnAtSeconds = Time.time + delay;
        _warningAtSeconds = _respawnAtSeconds - Mathf.Max(0f, warningSeconds);
        _usingDrumWarRespawnTiming = true;
        Debug.Log($"[PaperLegends][Drum] Drum War active. Server objective respawn accelerated to {delay:0.0}s.");
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

    private void TickCaptureAlert()
    {
        if (_captureAlertPulsesRemaining <= 0 || Time.time < _nextCaptureAlertAtSeconds)
            return;

        PulseCaptureAlert();
    }

    private void PulseCaptureAlert()
    {
        _captureAlertTick++;
        _captureAlertPulsesRemaining--;
        _nextCaptureAlertAtSeconds = _captureAlertPulsesRemaining > 0
            ? Time.time + captureAlertIntervalSeconds
            : 0f;
    }

    private void SyncState(GameScoreManager scoreManager)
    {
        scoreManager.SetDrumObjectiveState(
            _isActive,
            _isWarning,
            _capturingCharacter != null ? _capturingCharacter.PlayerId : 0,
            CaptureProgress01,
            _captureAlertTick);
    }

    private void CacheComponents()
    {
        if (_captureTrigger == null)
            _captureTrigger = GetComponent<Collider>();
    }
}
#endif
