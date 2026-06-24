using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class PaperLegendCharacterNetworkHandler : NetworkBehaviour
{
    [Header("Identity")]
    [SerializeField] private int fallbackPlayerId;
    [SerializeField] private PaperLegendTeam fallbackTeam = PaperLegendTeam.None;
    [SerializeField, Min(0)] private int fallbackFactionId;

    [Header("Flick Physics")]
    [SerializeField, Min(0f)] private float minHorizontalImpulse = 1.2f;
    [SerializeField, Min(0.01f)] private float maxHorizontalImpulse = 4.5f;
    [SerializeField, Min(0f)] private float minUpwardImpulse = 0.8f;
    [SerializeField, Min(0.01f)] private float maxUpwardImpulse = 2.6f;
    [SerializeField, Min(0.01f)] private float flickCooldownSeconds = 0.45f;
    [SerializeField, Min(0.01f)] private float maxHorizontalSpeed = 6f;
    [SerializeField, Min(0.01f)] private float maxAngularVelocity = 12f;
    [SerializeField] private bool applyImpulseAtContactPoint = true;
    [SerializeField] private AnimationCurve forceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Paper Physics")]
    [SerializeField, Min(0.01f)] private float fallbackPaperWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float fallbackPaperBounce = 0.35f;
    [SerializeField, Range(0f, 1f)] private float fallbackPaperFriction = 0.5f;
    [SerializeField, Min(0.01f)] private float fallbackFlickForceMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float minPaperWeight = 0.1f;
    [SerializeField, Min(0.01f)] private float maxPaperWeight = 10f;
    [SerializeField, Min(0.01f)] private float minFlickForceMultiplier = 0.2f;
    [SerializeField, Min(0.01f)] private float maxFlickForceMultiplier = 4f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0.01f)] private float groundProbeStartHeight = 0.25f;
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.45f;
    [SerializeField, Min(0f)] private float groundedVelocityThreshold = 0.18f;

    [Header("Pinned Damage")]
    [SerializeField] private bool enableLandingKill = true;
    [SerializeField] private LayerMask landingTargetMask = ~0;
    [SerializeField] private Vector3 landingKillBoxOffset = new Vector3(0f, 0.08f, 0f);
    [SerializeField] private Vector3 landingKillBoxHalfExtents = new Vector3(0.35f, 0.12f, 0.35f);
    [SerializeField, Min(1)] private int landingHitBufferSize = 16;

    [Header("Combat")]
    [SerializeField, Min(1)] private int defaultMaxHealth = 100;
    [SerializeField, Min(0f)] private float defaultAttack = 15f;
    [SerializeField, Min(0.1f)] private float defaultAttackSpeed = 1f;
    [SerializeField, Min(0.1f)] private float slowestPinnedKillSeconds = 2f;
    [SerializeField, Min(0.1f)] private float fastestPinnedKillSeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float lowAttackSpeedReference = 10f;
    [SerializeField, Min(0.01f)] private float highAttackSpeedReference = 25f;
    [SerializeField, Min(0f)] private float pinnedMinVerticalSeparation = 0.02f;
    [SerializeField, Min(0f)] private float pinnedMinHorizontalOverlap = 0.03f;

    [Header("Respawn")]
    [SerializeField] private bool autoRespawn = true;
    [SerializeField, Min(0f)] private float respawnDelaySeconds = 5f;

    [Header("Progression")]
    [SerializeField, Min(1)] private int maxLevel = 25;
    [SerializeField, Min(1)] private int baseExperienceToNextLevel = 100;
    [SerializeField, Min(0)] private int experienceGrowthPerLevel = 25;
    [SerializeField, Min(1)] private int maxSkillLevel = 4;

    [Header("Hero 10000001 Skills")]
    [SerializeField, Min(0f)] private float distanceDamagePerMeterPerLevel = 0.18f;
    [SerializeField, Min(1f)] private float distanceDamageMaxMultiplier = 4f;
    [SerializeField, Min(0f)] private float distanceLandingBaseDamageScale = 1f;
    [SerializeField, Min(1f)] private float flickBoostHorizontalMultiplierPerLevel = 0.28f;
    [SerializeField, Min(0f)] private float flickBoostUpwardMultiplierPerLevel = 0.12f;

    [Header("Hero 10000003 Skills")]
    [SerializeField, Min(0.1f)] private float wavePushLength = 5.5f;
    [SerializeField, Min(0.1f)] private float wavePushHalfWidth = 1.15f;
    [SerializeField, Min(0f)] private float wavePushOriginForwardOffset = 0.35f;
    [SerializeField, Min(0f)] private float wavePushBaseHorizontalImpulse = 4.25f;
    [SerializeField, Min(0f)] private float wavePushHorizontalImpulsePerLevel = 0.7f;
    [SerializeField, Min(0f)] private float wavePushBaseUpwardImpulse = 0.55f;
    [SerializeField, Min(0f)] private float wavePushUpwardImpulsePerLevel = 0.12f;

    [Header("Hero 10000005 Skills")]
    [SerializeField, Min(0.1f)] private float thunderStormRadius = 3.2f;
    [SerializeField, Min(0.1f)] private float thunderStormStrikeRadius = 0.85f;
    [SerializeField, Min(0.05f)] private float thunderStormCastDelaySeconds = 1f;
    [SerializeField, Min(0.1f)] private float thunderStormStrikeIntervalSeconds = 0.65f;
    [SerializeField, Min(0f)] private float thunderStormBaseDamage = 16f;
    [SerializeField, Min(0f)] private float thunderStormDamagePerLevel = 4f;

    [Header("Render")]
    [SerializeField, Min(0f)] private float proxySmoothTime = 0.035f;
    [SerializeField, Min(0.01f)] private float proxyRotationLerpSpeed = 24f;

    [Networked] public int PlayerId { get; private set; }
    [Networked] public PaperLegendTeam Team { get; private set; }
    [Networked] public int FactionId { get; private set; }
    [Networked] public int CharacterModelId { get; private set; }
    [Networked] public int HeroFormId { get; private set; }
    [Networked] public int VisualModelVariantId { get; private set; }
    [Networked] public PaperLegendCharacterState State { get; private set; }
    [Networked] public Vector3 NetworkPosition { get; private set; }
    [Networked] public Quaternion NetworkRotation { get; private set; }
    [Networked] public Vector3 NetworkScale { get; private set; }
    [Networked] public NetworkBool IsGrounded { get; private set; }
    [Networked] public int LastProcessedFlickSequence { get; private set; }
    [Networked] public int KillCount { get; private set; }
    [Networked] public int DeathCount { get; private set; }
    [Networked] public int Level { get; private set; }
    [Networked] public int CurrentExperience { get; private set; }
    [Networked] public int ExperienceToNextLevel { get; private set; }
    [Networked] public int TotalExperience { get; private set; }
    [Networked] public float RespawnRemainingSeconds { get; private set; }
    [Networked] public float MaxHealth { get; private set; }
    [Networked] public float CurrentHealth { get; private set; }
    [Networked] public float AttackPower { get; private set; }
    [Networked] public float AttackSpeed { get; private set; }
    [Networked] public int SkillUpgradePoints { get; private set; }
    [Networked] public int Skill1Level { get; private set; }
    [Networked] public int Skill2Level { get; private set; }
    [Networked] public int Skill3Level { get; private set; }
    [Networked] public int Skill4Level { get; private set; }
    [Networked] public NetworkBool Hero10000001DistanceDamageArmed { get; private set; }
    [Networked] public NetworkBool Hero10000001FlickBoostArmed { get; private set; }
    [Networked] public NetworkBool Hero10000003WavePushArmed { get; private set; }

    public bool IsAlive => State != PaperLegendCharacterState.Eliminated
        && State != PaperLegendCharacterState.Respawning;

    public bool CanAcceptLocalFlick => CanAcceptLocalFlickInput(out _);

    public int ResolvedVisualModelId => VisualModelVariantId > 0 ? VisualModelVariantId : CharacterModelId;

    private Rigidbody _rigidbody;
    private Collider[] _ownColliders;
    private Collider[] _landingHits;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private Vector3 _baseLocalScale = Vector3.one;
    private Vector3 _proxyVelocity;
    private float _flickCooldownRemaining;
    private float _respawnCountdown;
    private bool _wasGrounded = true;
    private bool _hadAirbornePhase;
#if !UNITY_SERVER
    private bool _lastProxyColliderAlive = true;
#endif
    private bool _hasFlickStartPosition;
    private Vector3 _flickStartPosition;
    private PaperLegendCharacterNetworkHandler _lastEliminator;
    private bool _hasLastEliminationPosition;
    private Vector3 _lastEliminationPosition;
    private int _activeDistanceDamageLevel;
    private int _hero10000003WavePushLevel;
    private float _hero10000003WavePushRemainingSeconds;
    private bool _hasPendingSkillTargetPosition;
    private Vector3 _pendingSkillTargetWorldPosition;
    private float _baseFlickForceMultiplier = 1f;
    private PhysicsMaterial _runtimePaperPhysicsMaterial;
    private readonly HashSet<PaperLegendCharacterNetworkHandler> _pinnedDamageVictims = new HashSet<PaperLegendCharacterNetworkHandler>();

    public override void Spawned()
    {
        CacheComponents();
        _spawnPosition = transform.position;
        _spawnRotation = SanitizeRotation(transform.rotation);
        _baseLocalScale = SanitizeScale(transform.localScale);
        transform.localScale = _baseLocalScale;

        if (HasStateAuthority)
        {
            if (PlayerId == 0)
                PlayerId = fallbackPlayerId;

            if (Team == PaperLegendTeam.None)
                Team = fallbackTeam;

            if (FactionId == 0)
                FactionId = Mathf.Max(0, fallbackFactionId);

            InitializeProgressionState();
            InitializeSkillProgressionState();
            InitializeCombatState();
            State = PaperLegendCharacterState.Idle;
            PublishAuthoritativeTransform();
            PaperLegendMatchNetworkHost.Instance?.RegisterPlayer(this);
        }

        ConfigureRigidbodyForAuthority();
        Debug.Log($"[PaperLegends][Character] Spawned player={PlayerId}, model={CharacterModelId}, stateAuthority={HasStateAuthority}, inputAuthority={HasInputAuthority}, objectInputAuthority={ResolveObjectInputAuthorityLabel()}.");

#if !UNITY_SERVER
        PaperLegendCharacterClientVisualSpawner.EnsureFor(this);

        if (HasInputAuthority)
            StartCoroutine(SetupLocalPaperLegendClientViewRoutine());
#endif
    }

    public override void FixedUpdateNetwork()
    {
        CacheComponents();

        if (!HasStateAuthority)
            return;

        if (State == PaperLegendCharacterState.Eliminated || State == PaperLegendCharacterState.Respawning)
        {
            TickRespawn();
            PublishAuthoritativeTransform();
            return;
        }

        if (_flickCooldownRemaining > 0f)
            _flickCooldownRemaining -= Runner.DeltaTime;

        TickHero10000003WavePushTimer();
        UpdateGroundedState();
        ApplyStackDamageToPinnedVictims();

        if (GetInput(out PaperLegendPlayerInputData input))
            ConsumeInput(input);

        ClampHorizontalSpeed();
        PublishAuthoritativeTransform();
    }

    public override void Render()
    {
        if (HasStateAuthority)
            return;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            NetworkPosition,
            ref _proxyVelocity,
            proxySmoothTime);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            SanitizeRotation(NetworkRotation),
            Time.deltaTime * proxyRotationLerpSpeed);

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            SanitizeScale(NetworkScale),
            Time.deltaTime * proxyRotationLerpSpeed);

        ApplyProxyColliderAliveState();
    }

    public void ConfigureIdentity(int playerId, PaperLegendTeam team)
    {
        ConfigureIdentity(playerId, team, 0);
    }

    public void ConfigureIdentity(int playerId, PaperLegendTeam team, int factionId)
    {
        ConfigureIdentity(playerId, team, factionId, 0);
    }

    public void ConfigureIdentity(int playerId, PaperLegendTeam team, int factionId, int characterModelId)
    {
        if (!HasStateAuthority)
            return;

        PlayerId = playerId;
        Team = team;
        FactionId = Mathf.Max(0, factionId);
        CharacterModelId = Mathf.Max(0, characterModelId);
        HeroFormId = 0;
        VisualModelVariantId = 0;
        PaperLegendHeroSkillRegistry.NotifyHeroConfigured(this);
    }

    public void ConfigureCombatStats(int hp, int attack, float speed)
    {
        if (!HasStateAuthority)
            return;

        float resolvedMaxHealth = hp > 0 ? hp : defaultMaxHealth;
        MaxHealth = Mathf.Max(1f, resolvedMaxHealth);
        CurrentHealth = MaxHealth;
        AttackPower = Mathf.Max(0f, attack > 0 ? attack : defaultAttack);
        AttackSpeed = Mathf.Max(0.1f, speed > 0f ? speed : defaultAttackSpeed);
    }

    public void ConfigurePaperPhysicsStats(float weight, float bounce, float friction, float flickForce)
    {
        if (!HasStateAuthority)
            return;

        CacheComponents();

        float resolvedWeight = weight > 0f ? weight : fallbackPaperWeight;
        float resolvedBounce = bounce >= 0f ? bounce : fallbackPaperBounce;
        float resolvedFriction = friction >= 0f ? friction : fallbackPaperFriction;
        float resolvedFlickForce = flickForce > 0f ? flickForce : fallbackFlickForceMultiplier;

        resolvedWeight = Mathf.Clamp(resolvedWeight, minPaperWeight, Mathf.Max(minPaperWeight, maxPaperWeight));
        resolvedBounce = Mathf.Clamp01(resolvedBounce);
        resolvedFriction = Mathf.Clamp01(resolvedFriction);
        _baseFlickForceMultiplier = Mathf.Clamp(
            resolvedFlickForce,
            minFlickForceMultiplier,
            Mathf.Max(minFlickForceMultiplier, maxFlickForceMultiplier));

        if (_rigidbody != null)
            _rigidbody.mass = resolvedWeight;

        ApplyRuntimePaperPhysicsMaterial(resolvedBounce, resolvedFriction);

        Debug.Log($"[PaperLegends][Physics] Applied paper physics to player={PlayerId}, model={CharacterModelId}: weight={resolvedWeight:0.###}, bounce={resolvedBounce:0.###}, friction={resolvedFriction:0.###}, flickForceMul={_baseFlickForceMultiplier:0.###}.");
    }

    public void ServerApplyHeroForm(int formId, int visualModelId, Vector3 localScaleMultiplier)
    {
        if (!HasStateAuthority)
            return;

        HeroFormId = Mathf.Max(0, formId);
        VisualModelVariantId = Mathf.Max(0, visualModelId);

        Vector3 multiplier = SanitizeScale(localScaleMultiplier);
        Vector3 resolvedScale = MultiplyScale(_baseLocalScale, multiplier);
        transform.localScale = SanitizeScale(resolvedScale);
        PublishAuthoritativeTransform();

        Debug.Log($"[PaperLegends][Form] player={PlayerId} model={CharacterModelId} form={HeroFormId} visual={VisualModelVariantId} scale={transform.localScale}.");
    }

    public void ServerArmHero10000001DistanceDamage()
    {
        if (HasStateAuthority)
            Hero10000001DistanceDamageArmed = true;
    }

    public void ServerArmHero10000001FlickBoost()
    {
        if (HasStateAuthority)
            Hero10000001FlickBoostArmed = true;
    }

    public void ServerArmHero10000003WavePush(int skillLevel, float inputTimeoutSeconds)
    {
        if (!HasStateAuthority)
            return;

        Hero10000003WavePushArmed = true;
        _hero10000003WavePushLevel = Mathf.Clamp(skillLevel, 1, maxSkillLevel);
        _hero10000003WavePushRemainingSeconds = Mathf.Max(0.1f, inputTimeoutSeconds);
    }

    public void ServerDispatchSkillEvent(PaperLegendHeroSkillId skillId, int slot, Vector3 worldPosition, float radius)
    {
        if (!HasStateAuthority)
            return;

        RpcPlayPaperLegendSkillEvent((int)skillId, Mathf.Clamp(slot, 1, 4), worldPosition, Mathf.Max(0f, radius));
    }

    public void ServerDispatchDirectionalSkillEvent(PaperLegendHeroSkillId skillId, int slot, Vector3 worldPosition, float radius, Vector3 direction)
    {
        if (!HasStateAuthority)
            return;

        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();

        RpcPlayPaperLegendDirectionalSkillEvent(
            (int)skillId,
            Mathf.Clamp(slot, 1, 4),
            worldPosition,
            Mathf.Max(0f, radius),
            direction);
    }

    public bool TryGetPendingSkillTargetPosition(out Vector3 targetPosition)
    {
        targetPosition = _pendingSkillTargetWorldPosition;
        return _hasPendingSkillTargetPosition;
    }

    public bool ServerTryCastHero10000005ThunderStorm(int skillLevel)
    {
        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
            return false;

        if (!TryGetPendingSkillTargetPosition(out Vector3 targetPosition))
        {
            Debug.LogWarning($"[PaperLegends][Skill] Thunder Storm rejected for player={PlayerId}: target position is missing.");
            return false;
        }

        int level = Mathf.Clamp(skillLevel, 1, 3);
        float duration = ResolveHero10000005ThunderStormDuration(level);
        StartCoroutine(ServerHero10000005ThunderStormRoutine(targetPosition, level, duration));

        ServerDispatchSkillEvent(PaperLegendHeroSkillId.Hero10000005ThunderStorm, 4, targetPosition, thunderStormRadius);
        Debug.Log($"[PaperLegends][Skill] Thunder Storm armed by player={PlayerId}, level={level}, target={targetPosition}, duration={duration:0.0}s.");
        return true;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPlayPaperLegendSkillEvent(int skillId, int slot, Vector3 worldPosition, float radius)
    {
#if !UNITY_SERVER
        HeroAudioPlayer.PlaySkillForCharacter(this, skillId, slot);
        PaperLegendHeroSkillVfxPlayer.PlaySkillVfx(this, skillId, worldPosition, radius);
#endif
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPlayPaperLegendDirectionalSkillEvent(int skillId, int slot, Vector3 worldPosition, float radius, Vector3 direction)
    {
#if !UNITY_SERVER
        HeroAudioPlayer.PlaySkillForCharacter(this, skillId, slot);
        PaperLegendHeroSkillVfxPlayer.PlaySkillVfx(this, skillId, worldPosition, radius, direction);
#endif
    }

    public void ConfigureRespawnPoint(Vector3 position, Quaternion rotation)
    {
        ConfigureRespawnPoint(position, rotation, respawnDelaySeconds);
    }

    public void ConfigureRespawnPoint(Vector3 position, Quaternion rotation, float delaySeconds)
    {
        if (!HasStateAuthority)
            return;

        _spawnPosition = position;
        _spawnRotation = SanitizeRotation(rotation);
        respawnDelaySeconds = Mathf.Max(0f, delaySeconds);
    }

    public bool ServerTryApplyFlick(PaperLegendPlayerInputData input)
    {
        if (!HasStateAuthority)
            return false;

        if (!CanAcceptAuthoritativeFlick())
            return false;

        if (input.FlickSequence <= LastProcessedFlickSequence)
            input.FlickSequence = LastProcessedFlickSequence + 1;

        LastProcessedFlickSequence = input.FlickSequence;
        ApplyFlick(input);
        return true;
    }

    public void ServerAddKill()
    {
        if (!HasStateAuthority)
            return;

        KillCount++;
    }

    public bool ServerGrantExperience(int amount, PaperLegendExperienceSource source)
    {
        if (!HasStateAuthority)
            return false;

        if (Level <= 0)
            InitializeProgressionState();

        amount = PaperLegendHeroSkillRegistry.ModifyExperienceReward(this, amount, source);
        if (amount == 0)
            return false;

        TotalExperience += amount;

        if (Level >= maxLevel)
        {
            CurrentExperience = 0;
            ExperienceToNextLevel = 0;
            return false;
        }

        CurrentExperience += amount;
        bool leveledUp = false;
        int levelsGained = 0;
        int oldLevel = Level;

        while (Level < maxLevel && ExperienceToNextLevel > 0 && CurrentExperience >= ExperienceToNextLevel)
        {
            CurrentExperience -= ExperienceToNextLevel;
            Level++;
            levelsGained++;
            leveledUp = true;

            if (Level >= maxLevel)
            {
                CurrentExperience = 0;
                ExperienceToNextLevel = 0;
                break;
            }

            ExperienceToNextLevel = CalculateExperienceToNextLevel(Level);
        }

        if (levelsGained > 0)
        {
            SkillUpgradePoints += levelsGained;
            PaperLegendHeroSkillRegistry.NotifyHeroLevelChanged(this, oldLevel, Level);
        }

        Debug.Log($"[PaperLegends][XP] player={PlayerId} +{amount} xp source={source} level={Level} xp={CurrentExperience}/{ExperienceToNextLevel}");
        return leveledUp;
    }

    public bool ServerApplyPinnedDamage(PaperLegendCharacterNetworkHandler attacker, float damageAmount)
    {
        if (!HasStateAuthority || !IsAlive)
            return false;

        if (attacker == null || attacker == this || !attacker.IsAlive || attacker.IsSameFaction(this))
            return false;

        if (IsMatchEnded())
            return false;

        damageAmount = Mathf.Max(0f, damageAmount);
        if (damageAmount <= 0f)
            return false;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damageAmount);
        if (CurrentHealth > 0.0001f)
            return true;

        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        if (host != null)
            return host.ReportCharacterElimination(attacker, this);

        attacker.ServerAddKill();
        ServerEliminate(attacker);
        return true;
    }

    public bool ServerApplyRadialKnockback(
        PaperLegendCharacterNetworkHandler source,
        Vector3 origin,
        float radius,
        float horizontalImpulse,
        float upwardImpulse)
    {
        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
            return false;

        if (source == null || source == this || !source.IsAlive || source.IsSameFaction(this))
            return false;

        radius = Mathf.Max(0.05f, radius);
        Bounds ownBounds = GetWorldBounds();
        Vector3 center = ownBounds.center;
        Vector3 offset = center - origin;
        offset.y = 0f;

        if (offset.sqrMagnitude > radius * radius)
            return false;

        if (offset.sqrMagnitude <= 0.0001f)
        {
            offset = transform.position - source.transform.position;
            offset.y = 0f;
        }

        Vector3 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : source.transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        Vector3 impulse = direction.normalized * Mathf.Max(0f, horizontalImpulse)
            + Vector3.up * Mathf.Max(0f, upwardImpulse);

        CacheComponents();
        if (_rigidbody == null)
            return false;

        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        _rigidbody.WakeUp();
        _rigidbody.AddForce(impulse, ForceMode.Impulse);

        State = PaperLegendCharacterState.Flicked;
        IsGrounded = false;
        _hadAirbornePhase = true;
        _wasGrounded = false;
        PublishAuthoritativeTransform();
        return true;
    }

    public bool ServerApplyDirectionalWaveKnockback(
        PaperLegendCharacterNetworkHandler source,
        Vector3 waveOrigin,
        Vector3 waveDirection,
        float length,
        float halfWidth,
        float horizontalImpulse,
        float upwardImpulse)
    {
        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
            return false;

        if (source == null || source == this || !source.IsAlive || source.IsSameFaction(this))
            return false;

        waveDirection.y = 0f;
        if (waveDirection.sqrMagnitude <= 0.0001f)
            return false;

        waveDirection.Normalize();
        length = Mathf.Max(0.1f, length);
        halfWidth = Mathf.Max(0.05f, halfWidth);

        Bounds ownBounds = GetWorldBounds();
        Vector3 center = ownBounds.center;
        Vector3 offset = center - waveOrigin;
        offset.y = 0f;

        float forwardDistance = Vector3.Dot(offset, waveDirection);
        if (forwardDistance < 0f || forwardDistance > length)
            return false;

        Vector3 lateral = offset - waveDirection * forwardDistance;
        float boundsAllowance = Mathf.Max(ownBounds.extents.x, ownBounds.extents.z);
        if (lateral.magnitude > halfWidth + boundsAllowance)
            return false;

        CacheComponents();
        if (_rigidbody == null)
            return false;

        Vector3 impulse = waveDirection * Mathf.Max(0f, horizontalImpulse)
            + Vector3.up * Mathf.Max(0f, upwardImpulse);

        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        _rigidbody.WakeUp();
        _rigidbody.AddForce(impulse, ForceMode.Impulse);

        State = PaperLegendCharacterState.Flicked;
        IsGrounded = false;
        _hadAirbornePhase = true;
        _wasGrounded = false;
        PublishAuthoritativeTransform();
        return true;
    }

    public void ServerEliminate(PaperLegendCharacterNetworkHandler attacker)
    {
        if (!HasStateAuthority || !IsAlive)
            return;

        _lastEliminator = attacker;
        _lastEliminationPosition = transform.position;
        _hasLastEliminationPosition = true;

        CurrentHealth = 0f;
        DeathCount++;
        State = autoRespawn ? PaperLegendCharacterState.Respawning : PaperLegendCharacterState.Eliminated;
        _respawnCountdown = autoRespawn ? respawnDelaySeconds : 0f;
        RespawnRemainingSeconds = _respawnCountdown;

        ClearArmedSkillState();
        StopPhysicsForElimination();
        SetCollidersEnabled(false);
        PublishAuthoritativeTransform();
    }

    public void ServerRespawnAt(Vector3 position, Quaternion rotation)
    {
        if (!HasStateAuthority)
            return;

        transform.SetPositionAndRotation(position, SanitizeRotation(rotation));

        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;

        SetCollidersEnabled(true);
        CurrentHealth = MaxHealth;
        State = PaperLegendCharacterState.Idle;
        IsGrounded = true;
        RespawnRemainingSeconds = 0f;
        _wasGrounded = true;
        _hadAirbornePhase = false;
        _lastEliminator = null;
        _hasLastEliminationPosition = false;
        ClearArmedSkillState();
        PublishAuthoritativeTransform();
    }

    public Bounds GetWorldBounds()
    {
        CacheComponents();

        if (_ownColliders == null || _ownColliders.Length == 0)
            return new Bounds(transform.position, Vector3.one * 0.1f);

        Bounds bounds = _ownColliders[0].bounds;
        for (int i = 1; i < _ownColliders.Length; i++)
        {
            if (_ownColliders[i] != null)
                bounds.Encapsulate(_ownColliders[i].bounds);
        }

        return bounds;
    }

    public bool IsSameFaction(PaperLegendCharacterNetworkHandler other)
    {
        if (other == null)
            return false;

        if (FactionId > 0 && other.FactionId > 0)
            return FactionId == other.FactionId;

        return Team != PaperLegendTeam.None && Team == other.Team;
    }

    public bool CanAcceptLocalFlickInput(out string rejectReason)
    {
        rejectReason = string.Empty;

        if (!IsAlive)
        {
            rejectReason = $"not alive. state={State}.";
            return false;
        }

        if (IsMatchEnded())
        {
            rejectReason = "match already ended.";
            return false;
        }

        if (Hero10000003WavePushArmed)
            return true;

        bool locallyGrounded = IsGrounded || CheckGrounded();
        if (!locallyGrounded)
        {
            rejectReason = $"character is not grounded. state={State}, networkGrounded={IsGrounded}.";
            return false;
        }

        return true;
    }

    public int GetSkillLevel(int slot)
    {
        switch (Mathf.Clamp(slot, 1, 4))
        {
            case 1:
                return Skill1Level;
            case 2:
                return Skill2Level;
            case 3:
                return Skill3Level;
            case 4:
                return Skill4Level;
            default:
                return 0;
        }
    }

    public bool CanUpgradeSkill(int slot)
    {
        if (!IsAlive || SkillUpgradePoints <= 0)
            return false;

        if (!PaperLegendHeroSkillRegistry.CanUpgradeSkill(this, slot))
            return false;

        return GetSkillLevel(slot) < maxSkillLevel;
    }

    public bool CanUseSkill(int slot)
    {
        if (!IsAlive || IsMatchEnded())
            return false;

        return PaperLegendHeroSkillRegistry.CanUseSkill(this, slot);
    }

    private void ConsumeInput(PaperLegendPlayerInputData input)
    {
        if (input.SkillUpgradeRequested)
            ServerTryUpgradeSkill(input.SkillUpgradeSlot);

        if (input.SkillRequested)
            ServerTryUseSkill(input);

        if (!input.FlickRequested)
            return;

        Debug.Log($"[PaperLegends][Input][Server] Received flick input for player={PlayerId}, seq={input.FlickSequence}, force={input.Force01:0.00}, contact={input.ContactWorldPosition}, direction={input.AimWorldDirection}, objectInputAuthority={ResolveObjectInputAuthorityLabel()}.");

        if (input.FlickSequence <= LastProcessedFlickSequence)
        {
            Debug.LogWarning($"[PaperLegends][Input][Server] Ignored duplicate flick input for player={PlayerId}. seq={input.FlickSequence}, last={LastProcessedFlickSequence}.");
            return;
        }

        LastProcessedFlickSequence = input.FlickSequence;

        if (Hero10000003WavePushArmed)
        {
            TryConsumeHero10000003WavePush(input);
            return;
        }

        if (!CanAcceptAuthoritativeFlick(out string rejectReason))
        {
            Debug.LogWarning($"[PaperLegends][Input][Server] Rejected flick for player={PlayerId}: {rejectReason}");
            return;
        }

        ApplyFlick(input);
    }

    private bool TryConsumeHero10000003WavePush(PaperLegendPlayerInputData input)
    {
        if (!Hero10000003WavePushArmed)
            return false;

        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
        {
            ClearHero10000003WavePushState();
            return true;
        }

        Vector3 direction = ResolveSkillAimDirection(input);
        if (direction.sqrMagnitude <= 0.0001f)
            direction = ResolveFlickDirection(input);

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        direction.Normalize();

        int level = Mathf.Clamp(_hero10000003WavePushLevel, 1, maxSkillLevel);
        float horizontalImpulse = wavePushBaseHorizontalImpulse + wavePushHorizontalImpulsePerLevel * (level - 1);
        float upwardImpulse = wavePushBaseUpwardImpulse + wavePushUpwardImpulsePerLevel * (level - 1);
        Vector3 origin = transform.position + direction * Mathf.Max(0f, wavePushOriginForwardOffset);

        int affectedCount = 0;
        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        if (host != null)
        {
            IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host.GetRegisteredPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                PaperLegendCharacterNetworkHandler target = players[i];
                if (target != null && target.ServerApplyDirectionalWaveKnockback(
                    this,
                    origin,
                    direction,
                    wavePushLength,
                    wavePushHalfWidth,
                    horizontalImpulse,
                    upwardImpulse))
                {
                    affectedCount++;
                }
            }
        }

        ServerDispatchDirectionalSkillEvent(
            PaperLegendHeroSkillId.Hero10000003WavePush,
            1,
            origin,
            wavePushLength,
            direction);

        ClearHero10000003WavePushState();
        Debug.Log($"[PaperLegends][Skill] Son Tinh wave push fired by player={PlayerId}, level={level}, direction={direction}, affected={affectedCount}.");
        return true;
    }

    private IEnumerator ServerHero10000005ThunderStormRoutine(Vector3 center, int level, float duration)
    {
        float delay = Mathf.Max(0f, thunderStormCastDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float elapsed = 0f;
        float interval = Mathf.Max(0.1f, thunderStormStrikeIntervalSeconds);
        float stormRadius = Mathf.Max(0.1f, thunderStormRadius);
        float strikeRadius = Mathf.Max(0.1f, thunderStormStrikeRadius);
        float damage = Mathf.Max(0f, thunderStormBaseDamage + thunderStormDamagePerLevel * Mathf.Max(0, level - 1));
        int strikeCount = 0;

        while (elapsed <= duration && !IsMatchEnded())
        {
            Vector2 randomOffset = Random.insideUnitCircle * stormRadius;
            Vector3 strikePosition = center + new Vector3(randomOffset.x, 0f, randomOffset.y);
            int affected = ServerApplyHero10000005ThunderStrikeDamage(strikePosition, strikeRadius, damage);

            ServerDispatchSkillEvent(PaperLegendHeroSkillId.Hero10000005ThunderStorm, 4, strikePosition, strikeRadius);
            strikeCount++;
            Debug.Log($"[PaperLegends][Skill] Thunder Storm strike player={PlayerId}, level={level}, strike={strikeCount}, position={strikePosition}, damage={damage:0.0}, affected={affected}.");

            elapsed += interval;
            yield return new WaitForSeconds(interval);
        }
    }

    private int ServerApplyHero10000005ThunderStrikeDamage(Vector3 strikePosition, float strikeRadius, float damage)
    {
        if (!HasStateAuthority || damage <= 0f || IsMatchEnded())
            return 0;

        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host != null ? host.GetRegisteredPlayers() : null;
        if (players == null)
            return 0;

        int affected = 0;
        float radiusSqr = strikeRadius * strikeRadius;
        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler target = players[i];
            if (target == null || target == this || !target.IsAlive || IsSameFaction(target))
                continue;

            Vector3 offset = target.GetWorldBounds().center - strikePosition;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSqr)
                continue;

            if (target.ServerApplyPinnedDamage(this, damage))
                affected++;
        }

        return affected;
    }

    private bool CanAcceptAuthoritativeFlick()
    {
        return CanAcceptAuthoritativeFlick(out _);
    }

    private bool CanAcceptAuthoritativeFlick(out string rejectReason)
    {
        rejectReason = string.Empty;

        if (!HasStateAuthority || !IsAlive)
        {
            rejectReason = $"stateAuthority={HasStateAuthority}, alive={IsAlive}, state={State}.";
            return false;
        }

        if (IsMatchEnded())
        {
            rejectReason = "match already ended.";
            return false;
        }

        if (_flickCooldownRemaining > 0f)
        {
            rejectReason = $"cooldown remaining {_flickCooldownRemaining:0.00}s.";
            return false;
        }

        bool grounded = CheckGrounded();
        IsGrounded = grounded;

        if (!grounded)
        {
            rejectReason = "character is not grounded.";
            return false;
        }

        if (State == PaperLegendCharacterState.Airborne || State == PaperLegendCharacterState.Flicked)
            State = _rigidbody.linearVelocity.sqrMagnitude <= groundedVelocityThreshold * groundedVelocityThreshold
                ? PaperLegendCharacterState.Idle
                : PaperLegendCharacterState.Grounded;

        return true;
    }

    private void ApplyFlick(PaperLegendPlayerInputData input)
    {
        Vector3 direction = ResolveFlickDirection(input);
        float force01 = Mathf.Clamp01(input.Force01);
        float curvedForce = forceCurve != null ? Mathf.Clamp01(forceCurve.Evaluate(force01)) : force01;

        ResolveFlickSkillMultipliers(out float horizontalSkillMultiplier, out float upwardSkillMultiplier);
        float horizontalImpulse = Mathf.Lerp(minHorizontalImpulse, maxHorizontalImpulse, curvedForce) * _baseFlickForceMultiplier * horizontalSkillMultiplier;
        float upwardImpulse = Mathf.Lerp(minUpwardImpulse, maxUpwardImpulse, curvedForce) * _baseFlickForceMultiplier * upwardSkillMultiplier;
        Vector3 impulse = direction * horizontalImpulse + Vector3.up * upwardImpulse;

        _rigidbody.WakeUp();

        if (applyImpulseAtContactPoint)
            _rigidbody.AddForceAtPosition(impulse, input.ContactWorldPosition, ForceMode.Impulse);
        else
            _rigidbody.AddForce(impulse, ForceMode.Impulse);

        _flickCooldownRemaining = flickCooldownSeconds;
        State = PaperLegendCharacterState.Flicked;
        _hadAirbornePhase = true;
        _hasFlickStartPosition = true;
        _flickStartPosition = transform.position;
        _activeDistanceDamageLevel = Hero10000001DistanceDamageArmed ? Mathf.Clamp(Skill1Level, 0, maxSkillLevel) : 0;
        Hero10000001DistanceDamageArmed = false;
        Hero10000001FlickBoostArmed = false;
        Debug.Log($"[PaperLegends][Input][Server] Applied flick to player={PlayerId}: impulse={impulse}, force={force01:0.00}, direction={direction}, baseFlickMul={_baseFlickForceMultiplier:0.00}, hSkillMul={horizontalSkillMultiplier:0.00}, upSkillMul={upwardSkillMultiplier:0.00}, distanceSkillLevel={_activeDistanceDamageLevel}.");
    }

    private void InitializeProgressionState()
    {
        Level = 1;
        CurrentExperience = 0;
        TotalExperience = 0;
        ExperienceToNextLevel = maxLevel > 1 ? CalculateExperienceToNextLevel(Level) : 0;
    }

    private void InitializeSkillProgressionState()
    {
        SkillUpgradePoints = 1;
        Skill1Level = 0;
        Skill2Level = 0;
        Skill3Level = 0;
        Skill4Level = 0;
        HeroFormId = 0;
        VisualModelVariantId = 0;
        NetworkScale = SanitizeScale(transform.localScale);
        Hero10000001DistanceDamageArmed = false;
        Hero10000001FlickBoostArmed = false;
        ClearHero10000003WavePushState();
        _activeDistanceDamageLevel = 0;
        _hasFlickStartPosition = false;
    }

    private bool ServerTryUpgradeSkill(int slot)
    {
        if (!HasStateAuthority)
            return false;

        slot = Mathf.Clamp(slot, 1, 4);
        if (!CanUpgradeSkill(slot))
        {
            Debug.LogWarning($"[PaperLegends][Skill] Upgrade rejected for player={PlayerId}, slot={slot}, points={SkillUpgradePoints}, currentLevel={GetSkillLevel(slot)}.");
            return false;
        }

        int nextLevel = Mathf.Clamp(GetSkillLevel(slot) + 1, 1, maxSkillLevel);
        SetSkillLevel(slot, nextLevel);
        SkillUpgradePoints = Mathf.Max(0, SkillUpgradePoints - 1);

        Debug.Log($"[PaperLegends][Skill] player={PlayerId} upgraded slot={slot} to level={nextLevel}. remainingPoints={SkillUpgradePoints}.");
        return true;
    }

    private bool ServerTryUseSkill(PaperLegendPlayerInputData input)
    {
        if (!HasStateAuthority)
            return false;

        int slot = Mathf.Clamp(input.SkillSlot, 1, 4);
        if (!CanUseSkill(slot))
        {
            Debug.LogWarning($"[PaperLegends][Skill] Cast rejected for player={PlayerId}, model={CharacterModelId}, slot={slot}, level={GetSkillLevel(slot)}, alive={IsAlive}.");
            return false;
        }

        _hasPendingSkillTargetPosition = input.SkillTargetWorldPositionSet;
        _pendingSkillTargetWorldPosition = input.SkillTargetWorldPosition;
        bool result = PaperLegendHeroSkillRegistry.TryUseSkill(this, slot);
        _hasPendingSkillTargetPosition = false;
        _pendingSkillTargetWorldPosition = default;
        return result;
    }

    private void SetSkillLevel(int slot, int level)
    {
        level = Mathf.Clamp(level, 0, maxSkillLevel);
        switch (Mathf.Clamp(slot, 1, 4))
        {
            case 1:
                Skill1Level = level;
                break;
            case 2:
                Skill2Level = level;
                break;
            case 3:
                Skill3Level = level;
                break;
            case 4:
                Skill4Level = level;
                break;
        }
    }

    private void ResolveFlickSkillMultipliers(out float horizontalMultiplier, out float upwardMultiplier)
    {
        horizontalMultiplier = 1f;
        upwardMultiplier = 1f;

        if (CharacterModelId != 10000001 || !Hero10000001FlickBoostArmed)
            return;

        int level = Mathf.Clamp(Skill3Level, 0, maxSkillLevel);
        if (level <= 0)
            return;

        horizontalMultiplier += flickBoostHorizontalMultiplierPerLevel * level;
        upwardMultiplier += flickBoostUpwardMultiplierPerLevel * level;
    }

    private void InitializeCombatState()
    {
        MaxHealth = Mathf.Max(1f, defaultMaxHealth);
        CurrentHealth = MaxHealth;
        AttackPower = Mathf.Max(0f, defaultAttack);
        AttackSpeed = Mathf.Max(0.1f, defaultAttackSpeed);
    }

    private int CalculateExperienceToNextLevel(int currentLevel)
    {
        int levelIndex = Mathf.Max(0, currentLevel - 1);
        return Mathf.Max(1, baseExperienceToNextLevel + experienceGrowthPerLevel * levelIndex);
    }

    private Vector3 ResolveFlickDirection(PaperLegendPlayerInputData input)
    {
        Vector3 fromContactToCenter = transform.position - input.ContactWorldPosition;
        fromContactToCenter.y = 0f;

        if (fromContactToCenter.sqrMagnitude > 0.0001f)
            return fromContactToCenter.normalized;

        Vector3 aim = input.AimWorldDirection;
        aim.y = 0f;

        if (aim.sqrMagnitude > 0.0001f)
            return aim.normalized;

        Vector3 fallback = transform.forward;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private static Vector3 ResolveSkillAimDirection(PaperLegendPlayerInputData input)
    {
        Vector3 aim = input.AimWorldDirection;
        aim.y = 0f;
        return aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.zero;
    }

    private static float ResolveHero10000005ThunderStormDuration(int level)
    {
        switch (Mathf.Clamp(level, 1, 3))
        {
            case 1:
                return 4f;
            case 2:
                return 5f;
            default:
                return 6f;
        }
    }

    private void TickHero10000003WavePushTimer()
    {
        if (!Hero10000003WavePushArmed)
            return;

        _hero10000003WavePushRemainingSeconds -= ResolveNetworkDeltaTime();
        if (_hero10000003WavePushRemainingSeconds > 0f)
            return;

        Debug.Log($"[PaperLegends][Skill] Son Tinh wave push timed out for player={PlayerId}.");
        ClearHero10000003WavePushState();
    }

    private void UpdateGroundedState()
    {
        bool grounded = CheckGrounded();
        IsGrounded = grounded;

        if (!grounded)
        {
            _hadAirbornePhase = true;
            State = PaperLegendCharacterState.Airborne;
            _wasGrounded = false;
            return;
        }

        if (!_wasGrounded && _hadAirbornePhase)
            HandleLanding();

        if (_rigidbody.linearVelocity.sqrMagnitude <= groundedVelocityThreshold * groundedVelocityThreshold)
            State = PaperLegendCharacterState.Idle;
        else if (State == PaperLegendCharacterState.Airborne || State == PaperLegendCharacterState.Flicked)
            State = PaperLegendCharacterState.Grounded;

        _wasGrounded = true;
    }

    private void HandleLanding()
    {
        State = PaperLegendCharacterState.Grounded;
        _hadAirbornePhase = false;
        ApplyDistanceLandingDamageSkill();

    }

    private void ApplyDistanceLandingDamageSkill()
    {
        if (!HasStateAuthority || CharacterModelId != 10000001)
            return;

        if (_activeDistanceDamageLevel <= 0 || !_hasFlickStartPosition || !IsAlive || IsMatchEnded())
        {
            ClearDistanceLandingDamageState();
            return;
        }

        if (!TryFindLandingDamageVictim(out PaperLegendCharacterNetworkHandler victim))
        {
            ClearDistanceLandingDamageState();
            return;
        }

        float travelDistance = Vector3.Distance(_flickStartPosition, transform.position);
        float multiplier = Mathf.Clamp(
            1f + travelDistance * distanceDamagePerMeterPerLevel * _activeDistanceDamageLevel,
            1f,
            distanceDamageMaxMultiplier);
        float baseDamage = Mathf.Max(1f, AttackPower * Mathf.Max(0f, distanceLandingBaseDamageScale));
        float finalDamage = baseDamage * multiplier;

        victim.ServerApplyPinnedDamage(this, finalDamage);
        Debug.Log($"[PaperLegends][Skill] player={PlayerId} applied distance landing damage to victim={victim.PlayerId}: distance={travelDistance:0.00}, level={_activeDistanceDamageLevel}, multiplier={multiplier:0.00}, damage={finalDamage:0.0}.");

        ClearDistanceLandingDamageState();
    }

    private void ClearDistanceLandingDamageState()
    {
        _activeDistanceDamageLevel = 0;
        _hasFlickStartPosition = false;
    }

    private void ClearArmedSkillState()
    {
        Hero10000001DistanceDamageArmed = false;
        Hero10000001FlickBoostArmed = false;
        ClearHero10000003WavePushState();
        ClearDistanceLandingDamageState();
    }

    private void ClearHero10000003WavePushState()
    {
        Hero10000003WavePushArmed = false;
        _hero10000003WavePushLevel = 0;
        _hero10000003WavePushRemainingSeconds = 0f;
    }

    private bool TryFindLandingDamageVictim(out PaperLegendCharacterNetworkHandler victim)
    {
        victim = null;
        EnsureLandingHitBuffer();

        Bounds ownBounds = GetWorldBounds();
        Vector3 center = ownBounds.center + landingKillBoxOffset;
        Vector3 halfExtents = new Vector3(
            Mathf.Max(landingKillBoxHalfExtents.x, ownBounds.extents.x + 0.03f),
            Mathf.Max(landingKillBoxHalfExtents.y, ownBounds.extents.y + 0.08f),
            Mathf.Max(landingKillBoxHalfExtents.z, ownBounds.extents.z + 0.03f));

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _landingHits,
            transform.rotation,
            landingTargetMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _landingHits[i];
            if (hit == null || IsOwnCollider(hit))
                continue;

            PaperLegendCharacterNetworkHandler candidate = hit.GetComponentInParent<PaperLegendCharacterNetworkHandler>();
            if (candidate == null || candidate == this || !candidate.IsAlive || IsSameFaction(candidate))
                continue;

            if (!IsPressingVictimFromAbove(candidate))
                continue;

            victim = candidate;
            return true;
        }

        return false;
    }

    private void ApplyStackDamageToPinnedVictims()
    {
        if (!enableLandingKill || !IsAlive || IsMatchEnded())
            return;

        if (!IsGrounded && State == PaperLegendCharacterState.Airborne)
            return;

        ResolveLandingHits();
    }

    private void ResolveLandingHits()
    {
        EnsureLandingHitBuffer();
        _pinnedDamageVictims.Clear();

        Bounds ownBounds = GetWorldBounds();
        Vector3 center = ownBounds.center + landingKillBoxOffset;
        Vector3 halfExtents = new Vector3(
            Mathf.Max(landingKillBoxHalfExtents.x, ownBounds.extents.x + 0.03f),
            Mathf.Max(landingKillBoxHalfExtents.y, ownBounds.extents.y + 0.08f),
            Mathf.Max(landingKillBoxHalfExtents.z, ownBounds.extents.z + 0.03f));
        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _landingHits,
            transform.rotation,
            landingTargetMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _landingHits[i];
            if (hit == null || IsOwnCollider(hit))
                continue;

            PaperLegendCharacterNetworkHandler victim = hit.GetComponentInParent<PaperLegendCharacterNetworkHandler>();
            if (victim == null || victim == this || !victim.IsAlive)
                continue;

            if (IsSameFaction(victim))
                continue;


            if (!CanDamagePinnedVictim(victim))
                continue;
            ApplyPinnedDamageToVictim(victim, ResolveNetworkDeltaTime());

            break;
        }
    }

    private void ApplyPinnedDamageToVictim(PaperLegendCharacterNetworkHandler victim, float deltaTime)
    {
        if (victim == null || deltaTime <= 0f)
            return;

        float damagePerSecond = CalculatePinnedDamagePerSecond(victim);
        victim.ServerApplyPinnedDamage(this, damagePerSecond * deltaTime);
    }

    private float CalculatePinnedDamagePerSecond(PaperLegendCharacterNetworkHandler victim)
    {
        float attackerPower = Mathf.Max(0f, AttackPower) * Mathf.Max(0.1f, AttackSpeed);
        float normalizedPower = Mathf.InverseLerp(lowAttackSpeedReference, Mathf.Max(lowAttackSpeedReference + 0.01f, highAttackSpeedReference), attackerPower);
        float slowKillSeconds = Mathf.Max(slowestPinnedKillSeconds, fastestPinnedKillSeconds);
        float fastKillSeconds = Mathf.Max(0.1f, Mathf.Min(slowestPinnedKillSeconds, fastestPinnedKillSeconds));
        float killSeconds = Mathf.Lerp(slowKillSeconds, fastKillSeconds, normalizedPower);
        float victimHealth = victim != null ? Mathf.Max(1f, victim.MaxHealth) : Mathf.Max(1f, defaultMaxHealth);
        return victimHealth / Mathf.Max(0.1f, killSeconds);
    }

    private bool CanDamagePinnedVictim(PaperLegendCharacterNetworkHandler victim)
    {
        if (victim == null || victim == this || !victim.IsAlive || IsSameFaction(victim))
            return false;

        if (_pinnedDamageVictims.Contains(victim))
            return false;

        if (!IsPressingVictimFromAbove(victim))
            return false;

        _pinnedDamageVictims.Add(victim);
        return true;
    }

    private bool IsPressingVictimFromAbove(PaperLegendCharacterNetworkHandler victim)
    {
        Bounds attackerBounds = GetWorldBounds();
        Bounds victimBounds = victim.GetWorldBounds();

        if (attackerBounds.center.y <= victimBounds.center.y + pinnedMinVerticalSeparation)
            return false;

        float overlapX = Mathf.Min(attackerBounds.max.x, victimBounds.max.x) - Mathf.Max(attackerBounds.min.x, victimBounds.min.x);
        float overlapZ = Mathf.Min(attackerBounds.max.z, victimBounds.max.z) - Mathf.Max(attackerBounds.min.z, victimBounds.min.z);
        return overlapX >= pinnedMinHorizontalOverlap && overlapZ >= pinnedMinHorizontalOverlap;
    }

    private float ResolveNetworkDeltaTime()
    {
        if (Runner != null && Runner.DeltaTime > 0f)
            return Runner.DeltaTime;

        return Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
    }

    private void ResolveKillWithoutHost(PaperLegendCharacterNetworkHandler victim)
    {
        ServerAddKill();
        victim.ServerEliminate(this);
    }

    private bool CheckGrounded()
    {
        CacheComponents();

        Bounds bounds = GetWorldBounds();
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.03f, bounds.extents.x * 0.75f),
            0.02f,
            Mathf.Max(0.03f, bounds.extents.z * 0.75f));
        Vector3 origin = bounds.center + Vector3.up * groundProbeStartHeight;
        float distance = Mathf.Max(0.05f, bounds.extents.y + groundProbeStartHeight + groundProbeDistance);

        RaycastHit[] hits = Physics.BoxCastAll(
            origin,
            halfExtents,
            Vector3.down,
            Quaternion.identity,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || IsOwnCollider(hitCollider))
                continue;

            if (hits[i].normal.sqrMagnitude > 0.0001f && hits[i].normal.y < 0.15f)
                continue;

            return true;
        }

        return false;
    }

    private void TickRespawn()
    {
        if (!autoRespawn || State != PaperLegendCharacterState.Respawning)
            return;

        _respawnCountdown -= Runner.DeltaTime;
        RespawnRemainingSeconds = Mathf.Max(0f, _respawnCountdown);
        if (_respawnCountdown > 0f)
            return;

        Vector3 respawnPosition = _spawnPosition;
        Quaternion respawnRotation = _spawnRotation;
        var matchHost = PaperLegendMatchNetworkHost.Instance;
        if (matchHost != null &&
            matchHost.TryResolveRespawnPose(
                this,
                _lastEliminator,
                _hasLastEliminationPosition,
                _lastEliminationPosition,
                out Vector3 resolvedPosition,
                out Quaternion resolvedRotation))
        {
            respawnPosition = resolvedPosition;
            respawnRotation = resolvedRotation;
        }
        else if (matchHost == null)
        {
            Debug.LogWarning($"[PaperLegends][Respawn] Match host missing for player={PlayerId}. Falling back to initial spawn position.");
        }
        else
        {
            Debug.LogWarning($"[PaperLegends][Respawn] Match host could not resolve a safe point for player={PlayerId}. Falling back to initial spawn position.");
        }

        ServerRespawnAt(respawnPosition, respawnRotation);
    }

    private static bool IsMatchEnded()
    {
        var host = PaperLegendMatchNetworkHost.Instance;
        return host != null && host.IsMatchEnded;
    }

    private void StopPhysicsForElimination()
    {
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
    }

    private void ClampHorizontalSpeed()
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        if (horizontal.sqrMagnitude <= maxHorizontalSpeed * maxHorizontalSpeed)
            return;

        horizontal = horizontal.normalized * maxHorizontalSpeed;
        _rigidbody.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
    }

    private void PublishAuthoritativeTransform()
    {
        NetworkPosition = transform.position;
        NetworkRotation = SanitizeRotation(transform.rotation);
        NetworkScale = SanitizeScale(transform.localScale);
    }

    private static Quaternion SanitizeRotation(Quaternion rotation)
    {
        float sqrMagnitude = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
        if (float.IsNaN(sqrMagnitude) || float.IsInfinity(sqrMagnitude) || sqrMagnitude <= 0.0001f)
            return Quaternion.identity;

        float magnitude = Mathf.Sqrt(sqrMagnitude);
        return new Quaternion(rotation.x / magnitude, rotation.y / magnitude, rotation.z / magnitude, rotation.w / magnitude);
    }

    private static Vector3 SanitizeScale(Vector3 scale)
    {
        if (float.IsNaN(scale.x) || float.IsNaN(scale.y) || float.IsNaN(scale.z) ||
            float.IsInfinity(scale.x) || float.IsInfinity(scale.y) || float.IsInfinity(scale.z))
        {
            return Vector3.one;
        }

        return new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(scale.x)),
            Mathf.Max(0.01f, Mathf.Abs(scale.y)),
            Mathf.Max(0.01f, Mathf.Abs(scale.z)));
    }

    private static Vector3 MultiplyScale(Vector3 left, Vector3 right)
    {
        return new Vector3(left.x * right.x, left.y * right.y, left.z * right.z);
    }

    private string ResolveObjectInputAuthorityLabel()
    {
        return Object != null ? Object.InputAuthority.ToString() : "null";
    }

    private void ApplyProxyColliderAliveState()
    {
#if !UNITY_SERVER
        bool alive = IsAlive;
        if (_lastProxyColliderAlive == alive)
            return;

        _lastProxyColliderAlive = alive;
        SetCollidersEnabled(alive);
#endif
    }

#if !UNITY_SERVER
    private IEnumerator SetupLocalPaperLegendClientViewRoutine()
    {
        const float timeoutSeconds = 5f;
        float elapsed = 0f;

        ClientGameplayBridge.Loading.FinishGameLoading();
        yield return null;

        while (elapsed < timeoutSeconds)
        {
            ClientGameplayBridge.Loading.FinishGameLoading();

            if (ClientGameplayBridge.Camera.HasInstance())
            {
                ClientGameplayBridge.Camera.StartFollowingPaperLegendCharacter(transform);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ClientGameplayBridge.Loading.FinishGameLoading();
        Debug.LogWarning($"[PaperLegends][Client] CameraRotation was not ready for local player {PlayerId}; loading UI was hidden but camera follow was not started.");
    }
#endif

    private void CacheComponents()
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.maxAngularVelocity = maxAngularVelocity;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        if (_ownColliders == null || _ownColliders.Length == 0)
            _ownColliders = GetComponentsInChildren<Collider>(true);
    }

    private void ConfigureRigidbodyForAuthority()
    {
        CacheComponents();

        bool stateAuthority = HasStateAuthority;
        _rigidbody.isKinematic = !stateAuthority;
        _rigidbody.useGravity = stateAuthority;
    }

    private void ApplyRuntimePaperPhysicsMaterial(float bounce, float friction)
    {
        if (_ownColliders == null || _ownColliders.Length == 0)
            return;

        PhysicsMaterial template = null;
        for (int i = 0; i < _ownColliders.Length; i++)
        {
            if (_ownColliders[i] != null && _ownColliders[i].sharedMaterial != null)
            {
                template = _ownColliders[i].sharedMaterial;
                break;
            }
        }

        _runtimePaperPhysicsMaterial = new PhysicsMaterial
        {
            name = template != null
                ? $"{template.name}_Runtime_{CharacterModelId}_{PlayerId}"
                : $"PM_Paper_Runtime_{CharacterModelId}_{PlayerId}"
        };

        if (template != null)
        {
            _runtimePaperPhysicsMaterial.frictionCombine = template.frictionCombine;
            _runtimePaperPhysicsMaterial.bounceCombine = template.bounceCombine;
        }

        _runtimePaperPhysicsMaterial.dynamicFriction = friction;
        _runtimePaperPhysicsMaterial.staticFriction = friction;
        _runtimePaperPhysicsMaterial.bounciness = bounce;

        for (int i = 0; i < _ownColliders.Length; i++)
        {
            if (_ownColliders[i] != null)
                _ownColliders[i].sharedMaterial = _runtimePaperPhysicsMaterial;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        CacheComponents();

        for (int i = 0; i < _ownColliders.Length; i++)
        {
            if (_ownColliders[i] != null)
                _ownColliders[i].enabled = enabled;
        }
    }

    private bool IsOwnCollider(Collider candidate)
    {
        if (candidate == null || _ownColliders == null)
            return false;

        for (int i = 0; i < _ownColliders.Length; i++)
        {
            if (_ownColliders[i] == candidate)
                return true;
        }

        return candidate.transform.IsChildOf(transform);
    }

    private void EnsureLandingHitBuffer()
    {
        if (_landingHits != null && _landingHits.Length == landingHitBufferSize)
            return;

        _landingHits = new Collider[Mathf.Max(1, landingHitBufferSize)];
    }

    private void OnDestroy()
    {
        if (PaperLegendMatchNetworkHost.Instance != null)
            PaperLegendMatchNetworkHost.Instance.UnregisterPlayer(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(transform.position + landingKillBoxOffset, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, landingKillBoxHalfExtents * 2f);
    }
}

