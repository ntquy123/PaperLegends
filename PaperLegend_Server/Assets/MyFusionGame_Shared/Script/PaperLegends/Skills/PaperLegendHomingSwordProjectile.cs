using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public sealed class PaperLegendHomingSwordProjectile : NetworkBehaviour
{
    private enum HomingSwordPhase : byte
    {
        Ballistic = 0,
        HomingStrike = 1
    }

    [Header("Launch")]
    [SerializeField, Min(0f)] private float minHorizontalImpulse = 3f;
    [SerializeField, Min(0.01f)] private float maxHorizontalImpulse = 9.5f;
    [SerializeField, Min(0f)] private float minUpwardImpulse = 0.1f;
    [SerializeField, Min(0f)] private float maxUpwardImpulse = 0.45f;
    [SerializeField] private AnimationCurve forceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] private float gravityMultiplier = 0.35f;

    [Header("Stop Detection")]
    [SerializeField, Min(0.01f)] private float stopVelocityThreshold = 0.18f;
    [SerializeField, Min(1)] private int stopConfirmTicks = 6;

    [Header("Landed Homing")]
    [SerializeField, Min(0.1f)] private float landedScanRadius = 2.5f;
    [SerializeField, Min(0.1f)] private float homingSteerStrength = 18f;
    [SerializeField, Min(0.1f)] private float strikeMinSpeed = 4f;
    [SerializeField, Min(0.1f)] private float strikeMaxSpeed = 10f;
    [SerializeField, Min(0.05f)] private float hitRadius = 0.55f;
    [SerializeField, Min(0.1f)] private float maxBallisticLifetimeSeconds = 4f;
    [SerializeField, Min(0.1f)] private float maxStrikeLifetimeSeconds = 2.5f;

    [Header("Hit Vfx")]
    [SerializeField, Min(0.05f)] private float hitVfxRadius = 0.75f;
    [SerializeField, Min(0f)] private float despawnDelayAfterHitSeconds = 1.5f;

#if !UNITY_SERVER
    [Header("Client Visuals")]
    [SerializeField] private GameObject visualPrefab;
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField, Min(0f)] private float hitVfxLifetimeSeconds = 2.5f;
#endif

    [Networked, OnChangedRender(nameof(OnHitStateChanged))]
    private NetworkBool HasHit { get; set; }

    [Networked] private Vector3 NetworkPosition { get; set; }
    [Networked] private Quaternion NetworkRotation { get; set; }
    [Networked] private Vector3 HitPosition { get; set; }

    private PaperLegendCharacterNetworkHandler _owner;
    private PaperLegendCharacterNetworkHandler _lockedTarget;
    private HomingSwordPhase _phase;
    private float _hitDamage;
    private float _slowPercent;
    private float _slowDurationSeconds;
    private int _skillId;
    private float _phaseElapsedSeconds;
    private int _lowVelocityTicks;
    private TickTimer _despawnTimer;
    private Rigidbody _rigidbody;
#if !UNITY_SERVER
    private GameObject _visualInstance;
    private bool _lastRenderedHitState;
#endif

    public override void Spawned()
    {
        _rigidbody = GetComponent<Rigidbody>();

        if (HasStateAuthority)
            PublishNetworkTransform();

#if !UNITY_SERVER
        CreateClientVisual();
        ApplyClientVisualTransform();
        _lastRenderedHitState = HasHit;
#endif
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
#if !UNITY_SERVER
        if (_visualInstance != null)
            Destroy(_visualInstance);
#endif
    }

    public void ServerConfigureAndLaunch(
        PaperLegendCharacterNetworkHandler owner,
        Vector3 direction,
        float force01,
        int skillId,
        float hitDamage,
        float slowPercent,
        float slowDurationSeconds)
    {
        if (!HasStateAuthority || owner == null)
            return;

        _owner = owner;
        _skillId = skillId;
        _hitDamage = Mathf.Max(0f, hitDamage);
        _slowPercent = Mathf.Clamp01(slowPercent);
        _slowDurationSeconds = Mathf.Max(0f, slowDurationSeconds);
        _lockedTarget = null;
        _phase = HomingSwordPhase.Ballistic;
        _phaseElapsedSeconds = 0f;
        _lowVelocityTicks = 0;
        HasHit = false;
        HitPosition = default;
        _despawnTimer = TickTimer.None;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.Normalize();
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        float curvedForce = forceCurve != null ? Mathf.Clamp01(forceCurve.Evaluate(Mathf.Clamp01(force01))) : Mathf.Clamp01(force01);
        float horizontalImpulse = Mathf.Lerp(minHorizontalImpulse, maxHorizontalImpulse, curvedForce);
        float upwardImpulse = Mathf.Lerp(minUpwardImpulse, maxUpwardImpulse, curvedForce);
        Vector3 impulse = direction * horizontalImpulse + Vector3.up * upwardImpulse;

        _rigidbody.useGravity = gravityMultiplier > 0.01f;
        _rigidbody.isKinematic = false;
        _rigidbody.WakeUp();
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.AddForce(impulse, ForceMode.Impulse);
        PublishNetworkTransform();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (HasHit)
        {
            PublishNetworkTransform();

            if (_despawnTimer.Expired(Runner))
                Runner.Despawn(Object);

            return;
        }

        _phaseElapsedSeconds += Runner.DeltaTime;

        switch (_phase)
        {
            case HomingSwordPhase.Ballistic:
                TickBallisticPhase();
                break;
            case HomingSwordPhase.HomingStrike:
                TickHomingStrikePhase();
                break;
        }

        PublishNetworkTransform();
    }

    public override void Render()
    {
#if !UNITY_SERVER
        if (!HasStateAuthority)
            transform.SetPositionAndRotation(NetworkPosition, SanitizeRotation(NetworkRotation));

        ApplyClientVisualTransform();

        bool hit = HasHit;
        if (hit && !_lastRenderedHitState)
            PlayClientHitVfx(HitPosition);

        _lastRenderedHitState = hit;
#endif
    }

    private void OnHitStateChanged()
    {
#if !UNITY_SERVER
        bool hit = HasHit;
        if (hit && !_lastRenderedHitState)
            PlayClientHitVfx(HitPosition);

        _lastRenderedHitState = hit;
#endif
    }

    private void TickBallisticPhase()
    {
        if (_phaseElapsedSeconds >= maxBallisticLifetimeSeconds)
        {
            ServerDespawnWithoutHit();
            return;
        }

        if (gravityMultiplier > 0.01f && _rigidbody.useGravity)
            _rigidbody.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);

        Vector3 velocity = _rigidbody.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        if (horizontal.sqrMagnitude <= stopVelocityThreshold * stopVelocityThreshold
            && Mathf.Abs(velocity.y) <= stopVelocityThreshold)
        {
            _lowVelocityTicks++;
        }
        else
        {
            _lowVelocityTicks = 0;
        }

        if (_lowVelocityTicks < stopConfirmTicks)
            return;

        ServerBeginLandedHomingScan();
    }

    private void ServerBeginLandedHomingScan()
    {
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.isKinematic = true;
        _lowVelocityTicks = 0;

        _lockedTarget = FindNearestEnemyInRadius(landedScanRadius);
        if (_lockedTarget == null)
        {
            ServerDespawnWithoutHit();
            return;
        }

        _phase = HomingSwordPhase.HomingStrike;
        _phaseElapsedSeconds = 0f;
        _rigidbody.isKinematic = false;
        _rigidbody.WakeUp();

        Vector3 offset = _lockedTarget.GetWorldBounds().center - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
    }

    private void TickHomingStrikePhase()
    {
        if (_phaseElapsedSeconds >= maxStrikeLifetimeSeconds)
        {
            ServerDespawnWithoutHit();
            return;
        }

        if (_lockedTarget == null || !_lockedTarget.IsAlive || _owner == null || _owner.IsSameFaction(_lockedTarget))
        {
            ServerDespawnWithoutHit();
            return;
        }

        SteerTowardTarget(_lockedTarget);

        if (TryResolveDirectHit(out PaperLegendCharacterNetworkHandler hitTarget))
            ServerTriggerHit(hitTarget);
    }

    private void ServerDespawnWithoutHit()
    {
        if (!HasStateAuthority)
            return;

        Runner.Despawn(Object);
    }

    private PaperLegendCharacterNetworkHandler FindNearestEnemyInRadius(float radius)
    {
        if (_owner == null)
            return null;

        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host != null ? host.GetRegisteredPlayers() : null;
        if (players == null)
            return null;

        float radiusSqr = radius * radius;
        float bestDistanceSqr = radiusSqr;
        PaperLegendCharacterNetworkHandler bestTarget = null;

        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler candidate = players[i];
            if (candidate == null || candidate == _owner || !candidate.IsAlive || _owner.IsSameFaction(candidate))
                continue;

            Vector3 offset = candidate.GetWorldBounds().center - transform.position;
            offset.y = 0f;
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr > radiusSqr || distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private void SteerTowardTarget(PaperLegendCharacterNetworkHandler target)
    {
        Vector3 offset = target.GetWorldBounds().center - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= 0.0001f)
            return;

        Vector3 desiredDirection = offset.normalized;
        Vector3 velocity = _rigidbody.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float speed = Mathf.Clamp(
            Mathf.Max(horizontalVelocity.magnitude, strikeMinSpeed),
            strikeMinSpeed,
            strikeMaxSpeed);
        Vector3 desiredVelocity = desiredDirection * speed;
        Vector3 steered = Vector3.Lerp(horizontalVelocity, desiredVelocity, Mathf.Clamp01(homingSteerStrength * Runner.DeltaTime));
        _rigidbody.linearVelocity = new Vector3(steered.x, 0f, steered.z);
        transform.rotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
    }

    private bool TryResolveDirectHit(out PaperLegendCharacterNetworkHandler hitTarget)
    {
        hitTarget = null;
        if (_owner == null)
            return false;

        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host != null ? host.GetRegisteredPlayers() : null;
        if (players == null)
            return false;

        float hitRadiusSqr = hitRadius * hitRadius;
        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler candidate = players[i];
            if (candidate == null || candidate == _owner || !candidate.IsAlive || _owner.IsSameFaction(candidate))
                continue;

            Vector3 offset = candidate.GetWorldBounds().center - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > hitRadiusSqr)
                continue;

            hitTarget = candidate;
            return true;
        }

        return false;
    }

    private void ServerTriggerHit(PaperLegendCharacterNetworkHandler target)
    {
        if (!HasStateAuthority || HasHit || _owner == null || target == null)
            return;

        HasHit = true;
        HitPosition = target.GetWorldBounds().center;

        if (_hitDamage > 0f)
            target.ServerApplyPinnedDamage(_owner, _hitDamage);

        if (_slowPercent > 0f && _slowDurationSeconds > 0f)
            target.ServerApplyMoveSlowDebuff(_slowPercent, _slowDurationSeconds);

        _owner.ServerDispatchSkillEvent((PaperLegendHeroSkillId)_skillId, 1, HitPosition, hitVfxRadius);

        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.isKinematic = true;

        _despawnTimer = despawnDelayAfterHitSeconds > 0f
            ? TickTimer.CreateFromSeconds(Runner, despawnDelayAfterHitSeconds)
            : TickTimer.None;
    }

    private void PublishNetworkTransform()
    {
        if (!HasStateAuthority)
            return;

        NetworkPosition = transform.position;
        NetworkRotation = SanitizeRotation(transform.rotation);
    }

    private static Quaternion SanitizeRotation(Quaternion rotation)
    {
        float sqrMagnitude = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
        if (float.IsNaN(sqrMagnitude) || float.IsInfinity(sqrMagnitude) || sqrMagnitude <= 0.0001f)
            return Quaternion.identity;

        float magnitude = Mathf.Sqrt(sqrMagnitude);
        return new Quaternion(rotation.x / magnitude, rotation.y / magnitude, rotation.z / magnitude, rotation.w / magnitude);
    }

#if !UNITY_SERVER
    private void CreateClientVisual()
    {
        if (_visualInstance != null || visualPrefab == null)
            return;

        _visualInstance = Instantiate(visualPrefab, transform);
        _visualInstance.transform.localPosition = Vector3.zero;
        _visualInstance.transform.localRotation = Quaternion.identity;
        ApplyClientVisualTransform();
    }

    private void ApplyClientVisualTransform()
    {
        if (_visualInstance == null)
            return;

        _visualInstance.transform.localPosition = Vector3.zero;
        _visualInstance.transform.localRotation = Quaternion.identity;
    }

    private void PlayClientHitVfx(Vector3 worldPosition)
    {
        if (hitVfxPrefab == null)
            return;

        GameObject instance = Instantiate(hitVfxPrefab, worldPosition, Quaternion.identity);
        if (hitVfxLifetimeSeconds > 0f)
            Destroy(instance, hitVfxLifetimeSeconds);
    }
#endif
}
