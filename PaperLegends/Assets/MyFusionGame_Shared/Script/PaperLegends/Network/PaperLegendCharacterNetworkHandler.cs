using System.Collections;
using System.Collections.Generic;
using Fusion.Addons.Physics;
using Fusion;
using UnityEngine;
#if !UNITY_SERVER
using DG.Tweening;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class PaperLegendCharacterNetworkHandler : NetworkBehaviour
{
    private const int SkillDamageSlotCount = 4;
    private const int SkillDamageLevelCount = 4;

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

    [Header("Fall Safety")]
    [SerializeField, Min(0.1f)] private float freeFallRespawnSeconds = 7f;
    [SerializeField] private float freeFallRespawnMinY = -12f;

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
    [SerializeField, Min(0f)] private float edgeBounceHorizontalImpulse = 2.2f;
    [SerializeField, Min(0f)] private float edgeBounceUpwardImpulse = 1.1f;
    [SerializeField, Min(0.05f)] private float edgeBounceIntervalSeconds = 0.55f;
    [SerializeField, Min(0.1f)] private float edgeBounceVfxRadius = 0.65f;
    [SerializeField] private NetworkObject paperArrowProjectilePrefab;
    [SerializeField, Min(0f)] private float paperArrowSpawnForwardOffset = 0.45f;
    [SerializeField, Min(0f)] private float paperArrowSpawnHeightOffset = 0.12f;
    [SerializeField, Min(0f)] private float paperArrowBaseImpactDamage = 10f;
    [SerializeField, Min(0f)] private float paperArrowImpactDamagePerLevel = 3f;
    [SerializeField, Range(0f, 1f)] private float paperArrowSlowPercent = 0.3f;
    [SerializeField, Min(0f)] private float paperArrowSlowDurationSeconds = 2.5f;

    [Header("Hero 10000002 Skills")]
    [SerializeField, Min(0.1f)] private float forwardSlideRechargeSeconds = 10f;
    [SerializeField, Min(1)] private int forwardSlideMaxCharges = 3;
    [SerializeField, Min(0f)] private float forwardSlideMinHorizontalImpulse = 2.2f;
    [SerializeField, Min(0.01f)] private float forwardSlideMaxHorizontalImpulse = 7.8f;
    [SerializeField, Min(1f)] private float forwardSlideFastSwipeMovementMultiplier = 10f;
    [SerializeField, Min(0f)] private float forwardSlideMinUpwardImpulse = 0.04f;
    [SerializeField, Min(0f)] private float forwardSlideMaxUpwardImpulse = 0.28f;
    [SerializeField, Min(1f)] private float forwardSlideHorizontalMultiplierPerLevel = 0.14f;
    [SerializeField, Min(0.1f)] private float forwardSlideVfxRadius = 0.55f;
    [SerializeField, Min(0.1f)] private float shoveStunWindowSeconds = 3f;
    [SerializeField, Min(0.1f)] private float shoveStunNearbyRadius = 2.8f;
    [SerializeField, Min(0f)] private float shoveStunMinHorizontalImpulse = 5.5f;
    [SerializeField, Min(0.01f)] private float shoveStunMaxHorizontalImpulse = 10.5f;
    [SerializeField, Min(1f)] private float shoveStunMovementMultiplier = 5f;
    [SerializeField, Min(0f)] private float shoveStunMinUpwardImpulse = 0.45f;
    [SerializeField, Min(0f)] private float shoveStunMaxUpwardImpulse = 1.15f;
    [SerializeField, Min(0f)] private float shoveStunMinDurationSeconds = 1f;
    [SerializeField, Min(0f)] private float shoveStunMaxDurationSeconds = 1.8f;
    [SerializeField, Min(0.1f)] private float shoveStunVfxRadius = 0.65f;
    [SerializeField, Min(0.05f)] private float skillSpeedCapBoostSeconds = 1.25f;
    [SerializeField, Min(0.1f)] private float lastStandDurationSeconds = 5f;
    [SerializeField, Min(0.1f)] private float lastStandAuraVfxRadius = 1.25f;
    [SerializeField, Min(0f)] private float lastStandMinimumHealth = 1f;

    [Header("Status Effects")]
    [SerializeField, Min(0f)] private float stunVelocityDampingPerSecond = 6f;
    [SerializeField, Min(0f)] private float stunFreezeVelocityThreshold = 0.35f;

    [Header("Hero 10000003 Skills")]
    [SerializeField, Min(0.1f)] private float waterBurstRadius = 1.05f;
    [SerializeField, Min(0.1f)] private float waterBurstStepDistance = 2.5f;
    [SerializeField, Min(0.05f)] private float waterBurstIntervalSeconds = 0.3f;
    [SerializeField, Min(0f)] private float waterBurstFallbackDamage = 10f;
    [SerializeField, Min(0.1f)] private float wavePushLength = 5.5f;
    [SerializeField, Min(0.1f)] private float wavePushHalfWidth = 1.15f;
    [SerializeField, Min(0.1f)] private float wavePushTargetRadius = 2.8f;
    [SerializeField, Min(0.1f)] private float wavePushTargetMaxRange = 5.5f;
    [SerializeField, Min(0f)] private float wavePushOriginForwardOffset = 0.35f;
    [SerializeField, Min(0f)] private float wavePushBaseHorizontalImpulse = 4.25f;
    [SerializeField, Min(0f)] private float wavePushHorizontalImpulsePerLevel = 0.7f;
    [SerializeField, Min(0f)] private float wavePushBaseUpwardImpulse = 3.2f;
    [SerializeField, Min(0f)] private float wavePushUpwardImpulsePerLevel = 0.45f;
    [SerializeField, Min(0f)] private float wavePushStunMinDurationSeconds = 0.9f;
    [SerializeField, Min(0f)] private float wavePushStunMaxDurationSeconds = 1.5f;
    [SerializeField, Min(0.1f)] private float gravityWellRadius = 4f;
    [SerializeField, Min(0.1f)] private float gravityWellTargetMaxRange = 5f;
    [SerializeField, Min(0f)] private float gravityWellEffectDelaySeconds = 1f;
    [SerializeField, Min(0f)] private float gravityWellPullHorizontalImpulse = 5.5f;
    [SerializeField, Min(0f)] private float gravityWellPullUpwardImpulse = 0.25f;
    [SerializeField, Min(0f)] private float gravityWellFallbackDamage = 8f;
    [SerializeField, Min(0f)] private float gravityWellSlowDurationSeconds = 3f;
    [SerializeField, Min(0.1f)] private float tidalCataclysmRadius = 6.5f;
    [SerializeField, Min(0.1f)] private float tidalCataclysmTargetMaxRange = 6.5f;
    [SerializeField, Min(0.05f)] private float tidalCataclysmChargeMovementBreakDistance = 0.25f;
    [SerializeField, Min(0f)] private float tidalCataclysmFallbackDamage = 22f;

    [Header("Hero 10000004 Skills")]
    [SerializeField] private NetworkObject hero10000004HomingSwordProjectilePrefab;
    [SerializeField, Min(0f)] private float hero10000004HomingSwordSpawnForwardOffset = 0.45f;
    [SerializeField, Min(0f)] private float hero10000004HomingSwordSpawnHeightOffset = 0.12f;
    [SerializeField, Min(0f)] private float hero10000004HomingSwordBaseDamage = 8f;
    [SerializeField, Min(0f)] private float hero10000004HomingSwordDamagePerLevel = 2f;
    [SerializeField, Range(0f, 1f)] private float hero10000004HomingSwordSlowPercent = 0.9f;
    [SerializeField, Min(0.1f)] private float hero10000004RushPaperSpeedStopDistance = 1.1f;
    [SerializeField, Min(0.1f)] private float hero10000004RushPaperSpeedBuffDurationSeconds = 5f;
    [SerializeField, Min(0f)] private float hero10000004RushPaperSpeedFallGravityBoost = 12f;
    [SerializeField, Min(0.1f)] private float hero10000004Skill2ArmTimeoutSeconds = 5f;
    [SerializeField, Min(0.1f)] private float hero10000004RushPaperSpeedVfxRadius = 0.85f;
    [SerializeField, Min(0.05f)] private float hero10000004PinCritBurstIntervalSeconds = 0.2f;
    [SerializeField, Min(0f)] private float hero10000004PinCritPopupHeightOffset = 0.72f;
    [SerializeField, Min(0.1f)] private float hero10000004PinDodgeMinSlideDistance = 0.42f;
    [SerializeField, Min(0.1f)] private float hero10000004PinDodgeSlideSpeed = 4.5f;
    [SerializeField, Min(0.05f)] private float hero10000004PinDodgeIFrameSeconds = 0.35f;

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
    [Networked] public int LifeStateRevision { get; private set; }
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
    [Networked] public float Skill1CooldownRemaining { get; private set; }
    [Networked] public float Skill2CooldownRemaining { get; private set; }
    [Networked] public float Skill3CooldownRemaining { get; private set; }
    [Networked] public float Skill4CooldownRemaining { get; private set; }
    [Networked] public NetworkBool Hero10000001DistanceDamageArmed { get; private set; }
    [Networked] public NetworkBool Hero10000001FlickBoostArmed { get; private set; }
    [Networked] public NetworkBool Hero10000001EdgeBounceArmed { get; private set; }
    [Networked, OnChangedRender(nameof(OnHero10000001EdgeBounceLandingVfxChanged))]
    private int Hero10000001EdgeBounceLandingVfxTick { get; set; }
    [Networked] private Vector3 Hero10000001EdgeBounceLandingVfxPosition { get; set; }
    [Networked] private Vector3 Hero10000001EdgeBounceLandingVfxDirection { get; set; }
    [Networked] public NetworkBool Hero10000001PaperArrowArmed { get; private set; }
    [Networked] public NetworkBool Hero10000004HomingSwordArmed { get; private set; }
    [Networked] public NetworkBool Hero10000004Skill2Armed { get; private set; }
    [Networked] public NetworkBool Hero10000002ForwardSlideArmed { get; private set; }
    [Networked] public int Hero10000002ForwardSlideRemaining { get; private set; }
    [Networked] public NetworkBool Hero10000002ShoveStunArmed { get; private set; }
    [Networked] public NetworkBool Hero10000003WaterBurstArmed { get; private set; }
    [Networked, OnChangedRender(nameof(OnHero10000003WaterBurstVfxChanged))]
    private int Hero10000003WaterBurstVfxTick { get; set; }
    [Networked] private Vector3 Hero10000003WaterBurstVfxStartPosition { get; set; }
    [Networked] private Vector3 Hero10000003WaterBurstVfxDirection { get; set; }
    [Networked] public NetworkBool Hero10000003WavePushArmed { get; private set; }
    [Networked, OnChangedRender(nameof(OnHero10000003GravityWellChargeVfxChanged))]
    private int Hero10000003GravityWellChargeVfxTick { get; set; }
    [Networked] private Vector3 Hero10000003GravityWellChargeVfxPosition { get; set; }
    [Networked, OnChangedRender(nameof(OnHero10000003GravityWellVfxChanged))]
    private int Hero10000003GravityWellVfxTick { get; set; }
    [Networked] private Vector3 Hero10000003GravityWellVfxPosition { get; set; }
    [Networked, OnChangedRender(nameof(OnHero10000003TidalCataclysmChargingChanged))]
    public NetworkBool Hero10000003TidalCataclysmCharging { get; private set; }
    [Networked, OnChangedRender(nameof(OnHero10000003TidalCataclysmChargeVfxChanged))]
    private int Hero10000003TidalCataclysmChargeVfxTick { get; set; }
    [Networked] private Vector3 Hero10000003TidalCataclysmChargeVfxPosition { get; set; }
    [Networked, OnChangedRender(nameof(OnHero10000003TidalCataclysmVfxChanged))]
    private int Hero10000003TidalCataclysmVfxTick { get; set; }
    [Networked] private Vector3 Hero10000003TidalCataclysmVfxPosition { get; set; }
    [Networked] public float MoveSlowMultiplier { get; private set; }
    [Networked] private TickTimer MoveSlowTimer { get; set; }
    [Networked] public NetworkBool StatusIsStunned { get; private set; }
    [Networked] private TickTimer StatusStunTimer { get; set; }
    [Networked] public NetworkBool StatusIsInvincibleAtOneHealth { get; private set; }
    [Networked] private TickTimer StatusInvincibleAtOneHealthTimer { get; set; }

    public bool IsAlive => State != PaperLegendCharacterState.Eliminated
        && State != PaperLegendCharacterState.Respawning;

    public bool CanAcceptLocalFlick => CanAcceptLocalFlickInput(out _);

    public bool IsStunned => StatusIsStunned;

    public bool IsInvincibleAtOneHealth => StatusIsInvincibleAtOneHealth;

    public PaperLegendCharacterStatusController StatusController
    {
        get
        {
            if (_statusController == null)
                _statusController = new PaperLegendCharacterStatusController(this);
            return _statusController;
        }
    }

    internal float StatusStunVelocityDampingPerSecond => stunVelocityDampingPerSecond;

    internal float StatusStunFreezeVelocityThreshold => stunFreezeVelocityThreshold;

    internal Rigidbody StatusRigidbody
    {
        get
        {
            CacheComponents();
            return _rigidbody;
        }
    }

    internal void InternalSetStunState(bool active, TickTimer timer)
    {
        if (!HasStateAuthority)
            return;

        StatusIsStunned = active;
        StatusStunTimer = timer;
    }

    internal bool InternalIsStunTimerExpired()
    {
        return StatusStunTimer.Expired(Runner);
    }

    internal void InternalSetInvincibleAtOneHealthState(bool active, TickTimer timer)
    {
        if (!HasStateAuthority)
            return;

        StatusIsInvincibleAtOneHealth = active;
        StatusInvincibleAtOneHealthTimer = timer;
    }

    internal bool InternalIsInvincibilityAtOneHealthTimerExpired()
    {
        return StatusInvincibleAtOneHealthTimer.Expired(Runner);
    }

    internal float StatusInvincibilityMinimumHealth => Mathf.Max(0.1f, lastStandMinimumHealth);

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
    private float _freeFallSeconds;
    private float _pendingLandingStunSeconds;
    private float _temporaryHorizontalSpeedCapMultiplier = 1f;
    private float _temporaryHorizontalSpeedCapRemainingSeconds;
#if !UNITY_SERVER
    private bool _lastProxyColliderAlive = true;
#endif
    private bool _hasFlickStartPosition;
    private Vector3 _flickStartPosition;
    private PaperLegendCharacterNetworkHandler _lastEliminator;
    private bool _hasLastEliminationPosition;
    private Vector3 _lastEliminationPosition;
    private int _activeDistanceDamageLevel;
    private bool _hero10000001EdgeBounceActive;
    private int _remainingHero10000001EdgeBounces;
    private Vector3 _hero10000001EdgeBounceDirection;
    private float _hero10000001EdgeBounceTimerSeconds;
#if !UNITY_SERVER
    private int _lastRenderedHero10000001EdgeBounceLandingVfxTick;
#endif
    private int _hero10000002ForwardSlideLevel;
    private float _hero10000002ForwardSlideRemainingSeconds;
    private int _hero10000003WavePushLevel;
    private float _hero10000003WavePushRemainingSeconds;
    private Vector3 _hero10000003TidalCataclysmChargeAnchorPosition;
    private bool _hero10000003TidalCataclysmChargeAnchorCaptured;
    private int _hero10000003WaterBurstLevel;
    private float _hero10000003WaterBurstInputRemainingSeconds;
    private bool _hero10000003WaterBurstActive;
    private int _hero10000003WaterBurstNextBurstIndex;
    private float _hero10000003WaterBurstTimerSeconds;
    private float _hero10000003WaterBurstBaseDamage;
    private Vector3 _hero10000003WaterBurstStartPosition;
    private Vector3 _hero10000003WaterBurstDirection;
#if !UNITY_SERVER
    private int _lastRenderedHero10000003WaterBurstVfxTick;
    private int _lastRenderedHero10000003GravityWellChargeVfxTick;
    private int _lastRenderedHero10000003GravityWellVfxTick;
    private int _lastRenderedHero10000003TidalCataclysmChargeVfxTick;
    private int _lastRenderedHero10000003TidalCataclysmVfxTick;
    private Tween _tidalCataclysmChargeShakeTween;
    private Sequence _tidalCataclysmChargePoseSequence;
    private Vector3 _tidalCataclysmChargeShakeBaseLocalPosition;
    private Vector3 _tidalCataclysmChargeShakeBaseLocalScale;
    private Transform _tidalCataclysmChargeShakeTransform;
#endif
    private int _hero10000002ShoveStunLevel;
    private float _hero10000002ShoveStunRemainingSeconds;
    private int _hero10000001PaperArrowLevel;
    private int _hero10000004HomingSwordLevel;
    private int _hero10000004Skill2Level;
    private float _hero10000004Skill2ArmRemainingSeconds;
    private float _hero10000004PaperSpeedBuffMultiplier = 1f;
    private float _hero10000004PaperSpeedBuffRemainingSeconds;
    private float _hero10000004PinDamageAccumulator;
    private float _hero10000004PinDamageBurstTimer;
    private PaperLegendCharacterNetworkHandler _hero10000004PinDamageVictim;
    private float _hero10000004PinDodgeIFrameRemainingSeconds;
    private PaperLegendCharacterStatusController _statusController;
    private bool _hasPendingSkillTargetPosition;
    private Vector3 _pendingSkillTargetWorldPosition;
    private bool _hasPendingSkillTargetPlayerId;
    private int _pendingSkillTargetPlayerId;
    private float _baseFlickForceMultiplier = 1f;
    private readonly float[,] _configuredSkillDamageBySlotAndLevel = new float[SkillDamageSlotCount, SkillDamageLevelCount];
    private readonly float[] _configuredSkillCooldownSeconds = new float[PaperLegendHeroSkillRegistry.MaxSkillSlots];
    private PhysicsMaterial _runtimePaperPhysicsMaterial;
    private readonly HashSet<PaperLegendCharacterNetworkHandler> _pinnedDamageVictims = new HashSet<PaperLegendCharacterNetworkHandler>();

    public override void Spawned()
    {
        CacheComponents();
        _statusController = new PaperLegendCharacterStatusController(this);
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
        PaperLegendHeroSkillVfxPlayer.EnsureFor(this);
        HeroAudioPlayer.EnsureFor(this);
        PaperLegendCharacterStatusIndicator.EnsureFor(this);
        PaperLegendCharacterInvincibilityAura.EnsureFor(this);

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

        TickSkillCooldowns();
        TickHero10000001EdgeBounceTimer();
        TickHero10000003WaterBurstTimers();
        TickHero10000003WavePushTimer();
        TickHero10000003TidalCataclysmChargeMovement();
        TickHero10000004Skill2ArmTimer();
        TickHero10000004PaperSpeedBuff();
        TickHero10000004PinDodgeIFrames();
        TickHero10000002ForwardSlideTimer();
        TickHero10000002ShoveStunTimer();
        TickTemporaryHorizontalSpeedCapBoost();
        TickMoveSlowDebuff();
        _statusController?.ServerTick(ResolveNetworkDeltaTime());
        _statusController?.ServerTickInvincibilityAtOneHealth();
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

    public void ConfigureHeroSkillStats(PaperLegendHeroData heroData)
    {
        if (!HasStateAuthority)
            return;

        ClearConfiguredSkillDamageLevels();
        ClearConfiguredSkillCooldowns();

        if (heroData?.skills == null)
            return;

        for (int i = 0; i < heroData.skills.Count; i++)
        {
            PaperLegendHeroSkillData skill = heroData.skills[i];
            if (skill == null || skill.slot < 1 || skill.slot > SkillDamageSlotCount)
                continue;

            int slotIndex = skill.slot - 1;
            for (int level = 1; level <= SkillDamageLevelCount; level++)
                _configuredSkillDamageBySlotAndLevel[slotIndex, level - 1] = Mathf.Max(0f, skill.ResolveDamageForLevel(level));

            _configuredSkillCooldownSeconds[slotIndex] = Mathf.Max(0f, skill.cooldown);
        }
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

    public void ServerArmHero10000001EdgeBounce()
    {
        if (HasStateAuthority)
            Hero10000001EdgeBounceArmed = true;
    }

    public void ServerArmHero10000001PaperArrow()
    {
        if (!HasStateAuthority)
            return;

        Hero10000001PaperArrowArmed = true;
        _hero10000001PaperArrowLevel = Mathf.Clamp(Skill2Level, 1, maxSkillLevel);
    }

    public void ServerArmHero10000004HomingSword(int skillLevel)
    {
        if (!HasStateAuthority || CharacterModelId != 10000004 || Skill1Level <= 0)
            return;

        Hero10000004HomingSwordArmed = true;
        _hero10000004HomingSwordLevel = Mathf.Clamp(skillLevel, 1, maxSkillLevel);
    }

    public void ServerArmHero10000004RushPaperSpeed(int skillLevel)
    {
        if (!HasStateAuthority || CharacterModelId != 10000004 || Skill2Level <= 0)
            return;

        Hero10000004Skill2Armed = true;
        _hero10000004Skill2Level = Mathf.Clamp(skillLevel, 1, maxSkillLevel);
        _hero10000004Skill2ArmRemainingSeconds = Mathf.Max(0.1f, hero10000004Skill2ArmTimeoutSeconds);
    }

    public bool ServerTryCastHero10000004RushPaperSpeed(int skillLevel)
    {
        if (!HasStateAuthority || !IsAlive || IsMatchEnded() || CharacterModelId != 10000004)
            return false;

        if (!TryGetPendingSkillTargetPlayerId(out int targetPlayerId))
        {
            Debug.LogWarning($"[PaperLegends][Skill] Rush Paper Speed rejected for player={PlayerId}: target player is missing.");
            return false;
        }

        PaperLegendCharacterNetworkHandler target = FindRegisteredPlayerById(targetPlayerId);
        if (target == null || target == this || !target.IsAlive || IsSameFaction(target))
        {
            Debug.LogWarning($"[PaperLegends][Skill] Rush Paper Speed rejected for player={PlayerId}: invalid target playerId={targetPlayerId}.");
            return false;
        }

        int level = Mathf.Clamp(skillLevel, 1, maxSkillLevel);
        float speedMultiplier = PaperLegendHero10000004SonTinhSkillSet.ResolvePaperSpeedMultiplier(level);
        ServerRelocateNearTargetForHero10000004Rush(target);
        ServerApplyHero10000004PaperSpeedBuff(speedMultiplier, hero10000004RushPaperSpeedBuffDurationSeconds);
        ClearHero10000004Skill2State();

        ServerDispatchSkillEvent(
            PaperLegendHeroSkillId.Hero10000004ReservedSkill2,
            2,
            transform.position,
            hero10000004RushPaperSpeedVfxRadius);

        Debug.Log($"[PaperLegends][Skill] Rush Paper Speed cast by player={PlayerId}, level={level}, target={target.PlayerId}, speedMultiplier={speedMultiplier:0.00}x, duration={hero10000004RushPaperSpeedBuffDurationSeconds:0.0}s.");
        return true;
    }

    public void ServerArmHero10000002ForwardSlide(int skillLevel)
    {
        if (!HasStateAuthority)
            return;

        int maxCharges = ResolveHero10000002ForwardSlideMaxCharges();
        Hero10000002ForwardSlideRemaining = maxCharges;
        Hero10000002ForwardSlideArmed = Hero10000002ForwardSlideRemaining > 0;
        _hero10000002ForwardSlideLevel = Mathf.Clamp(skillLevel, 1, maxSkillLevel);
        _hero10000002ForwardSlideRemainingSeconds = Mathf.Max(0.1f, forwardSlideRechargeSeconds);
    }

    public void ServerArmHero10000002ShoveStun(int skillLevel)
    {
        if (!HasStateAuthority)
            return;

        Hero10000002ShoveStunArmed = true;
        _hero10000002ShoveStunLevel = Mathf.Clamp(skillLevel, 1, maxSkillLevel);
        _hero10000002ShoveStunRemainingSeconds = Mathf.Max(0.1f, shoveStunWindowSeconds);
    }

    public bool ServerTryActivateHero10000002LastStand(bool manualTrigger)
    {
        if (!HasStateAuthority || !IsAlive || IsMatchEnded() || CharacterModelId != 10000002)
            return false;

        if (Skill4Level <= 0 || StatusIsInvincibleAtOneHealth)
            return false;

        if (CurrentHealth <= StatusInvincibilityMinimumHealth)
            CurrentHealth = StatusInvincibilityMinimumHealth;

        PaperLegendCharacterStatusEffects.ServerApplyInvincibilityAtOneHealth(this, lastStandDurationSeconds);

        Debug.Log($"[PaperLegends][Skill] player={PlayerId} activated hero 10000002 last stand manual={manualTrigger}, duration={lastStandDurationSeconds:0.00}s, hp={CurrentHealth:0.00}.");
        return true;
    }

    public void ServerHandleInvincibilityAtOneHealthEnded()
    {
        if (!HasStateAuthority || CharacterModelId != 10000002 || Skill4Level <= 0)
            return;

        float cooldownSeconds = PaperLegendHero10000002SkillSet.ResolveLastStandCooldownSeconds(Skill4Level);
        if (cooldownSeconds <= 0f)
            return;

        SetSkillCooldownRemaining(4, cooldownSeconds);
        Debug.Log($"[PaperLegends][Skill] player={PlayerId} last stand ended. cooldown={cooldownSeconds:0.00}s at level={Skill4Level}.");
    }

    public bool CanActivateHero10000002LastStand()
    {
        return CharacterModelId == 10000002
            && Skill4Level > 0
            && IsAlive
            && !IsMatchEnded()
            && !StatusIsInvincibleAtOneHealth
            && GetSkillCooldownRemaining(4) <= 0.01f;
    }

    public void ServerApplyMoveSlowDebuff(float slowPercent, float durationSeconds)
    {
        if (!HasStateAuthority || !IsAlive)
            return;

        slowPercent = Mathf.Clamp01(slowPercent);
        durationSeconds = Mathf.Max(0f, durationSeconds);
        if (slowPercent <= 0f || durationSeconds <= 0f)
            return;

        MoveSlowMultiplier = Mathf.Clamp01(1f - slowPercent);
        MoveSlowTimer = TickTimer.CreateFromSeconds(Runner, durationSeconds);
    }

    public void ServerArmHero10000003WaterBurst(int skillLevel, float inputTimeoutSeconds)
    {
        if (!HasStateAuthority)
            return;

        Hero10000003WaterBurstArmed = true;
        _hero10000003WaterBurstLevel = Mathf.Clamp(skillLevel, 1, maxSkillLevel);
        _hero10000003WaterBurstInputRemainingSeconds = Mathf.Max(0.1f, inputTimeoutSeconds);
    }

    public void ServerArmHero10000003WavePush(int skillLevel, float inputTimeoutSeconds)
    {
        if (!HasStateAuthority)
            return;

        Hero10000003WavePushArmed = true;
        _hero10000003WavePushLevel = Mathf.Clamp(skillLevel, 1, maxSkillLevel);
        _hero10000003WavePushRemainingSeconds = Mathf.Max(0.1f, inputTimeoutSeconds);
    }

    public bool ServerTryCastHero10000003WavePushArea(int skillLevel)
    {
        if (!HasStateAuthority || !IsAlive || IsMatchEnded() || CharacterModelId != 10000003)
            return false;

        if (!TryGetPendingSkillTargetPosition(out Vector3 targetPosition))
        {
            Debug.LogWarning($"[PaperLegends][Skill] Wave Push rejected for player={PlayerId}: target position is missing.");
            return false;
        }

        int level = Mathf.Clamp(skillLevel, 1, maxSkillLevel);
        Vector3 center = ClampWavePushTargetPosition(targetPosition);
        Vector3 direction = ResolveWavePushAreaDirection(center);
        float radius = Mathf.Max(0.1f, wavePushTargetRadius);
        float horizontalImpulse = wavePushBaseHorizontalImpulse + wavePushHorizontalImpulsePerLevel * (level - 1);
        float upwardImpulse = wavePushBaseUpwardImpulse + wavePushUpwardImpulsePerLevel * (level - 1);
        float stunDuration = ResolveHero10000003WavePushStunDuration(level);
        int affected = ServerApplyHero10000003WavePushArea(center, radius, direction, horizontalImpulse, upwardImpulse, stunDuration);

        ServerDispatchDirectionalSkillEvent(
            PaperLegendHeroSkillId.Hero10000003WavePush,
            2,
            center,
            radius,
            direction);

        Debug.Log($"[PaperLegends][Skill] Son Tinh wave push area cast by player={PlayerId}, level={level}, center={center}, radius={radius:0.00}, affected={affected}.");
        return true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcBeginHero10000003GravityWellCharge(Vector3 worldPosition)
    {
        if (!HasStateAuthority || CharacterModelId != 10000003 || Skill3Level <= 0)
            return;

        if (GetSkillCooldownRemaining(3) > 0.01f || !IsAlive || IsMatchEnded())
            return;

        Hero10000003GravityWellChargeVfxPosition = worldPosition;
        Hero10000003GravityWellChargeVfxTick++;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcCancelHero10000003GravityWellCharge()
    {
        if (!HasStateAuthority)
            return;

        Hero10000003GravityWellChargeVfxPosition = default;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcUpdateHero10000003GravityWellChargePosition(Vector3 worldPosition)
    {
        if (!HasStateAuthority)
            return;

        Hero10000003GravityWellChargeVfxPosition = worldPosition;
    }

    public bool ServerTryCastHero10000003GravityWell(int skillLevel)
    {
        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
            return false;

        if (!TryGetPendingSkillTargetPosition(out Vector3 targetPosition))
        {
            Debug.LogWarning($"[PaperLegends][Skill] Tidal Vortex rejected for player={PlayerId}: target position is missing.");
            return false;
        }

        int level = Mathf.Clamp(skillLevel, 1, maxSkillLevel);
        Vector3 center = ClampGravityWellTargetPosition(targetPosition);
        float damage = ResolveConfiguredSkillDamage(3, level, gravityWellFallbackDamage);
        float slowPercent = ResolveHero10000003GravityWellSlowPercent(level);
        float radius = Mathf.Max(0.1f, gravityWellRadius);

        Hero10000003GravityWellChargeVfxPosition = default;
        ServerDispatchSkillEvent(PaperLegendHeroSkillId.Hero10000003GravityWell, 3, center, radius);
        StartCoroutine(ServerHero10000003GravityWellDelayedEffectRoutine(center, radius, damage, slowPercent));
        Debug.Log($"[PaperLegends][Skill] Tidal Vortex cast by player={PlayerId}, level={level}, target={center}, radius={radius:0.0}, effectDelay={gravityWellEffectDelaySeconds:0.0}s.");
        return true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcBeginHero10000003TidalCataclysmCharge(Vector3 worldPosition)
    {
        if (!HasStateAuthority || CharacterModelId != 10000003 || Skill4Level <= 0)
            return;

        if (Hero10000003TidalCataclysmCharging || GetSkillCooldownRemaining(4) > 0.01f || !IsAlive || IsMatchEnded())
            return;

        Vector3 clampedPosition = ClampTidalCataclysmTargetPosition(worldPosition);
        _hero10000003TidalCataclysmChargeAnchorPosition = transform.position;
        _hero10000003TidalCataclysmChargeAnchorCaptured = true;
        Hero10000003TidalCataclysmCharging = true;
        Hero10000003TidalCataclysmChargeVfxPosition = clampedPosition;
        Hero10000003TidalCataclysmChargeVfxTick++;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcCancelHero10000003TidalCataclysmCharge()
    {
        if (!HasStateAuthority)
            return;

        ClearHero10000003TidalCataclysmChargeState();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcFailHero10000003TidalCataclysmChargeDueToMovement()
    {
        if (!HasStateAuthority)
            return;

        ServerFailHero10000003TidalCataclysmChargeDueToMovement();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcUpdateHero10000003TidalCataclysmChargePosition(Vector3 worldPosition)
    {
        if (!HasStateAuthority)
            return;

        Hero10000003TidalCataclysmChargeVfxPosition = worldPosition;
    }

    public bool ServerTryCastHero10000003TidalCataclysm(int skillLevel)
    {
        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
            return false;

        if (!Hero10000003TidalCataclysmCharging)
        {
            Debug.LogWarning($"[PaperLegends][Skill] Tidal Cataclysm rejected for player={PlayerId}: charge is not active.");
            return false;
        }

        int level = Mathf.Clamp(skillLevel, 1, maxSkillLevel);
        Vector3 center = Hero10000003TidalCataclysmChargeVfxPosition;
        float damage = ResolveConfiguredSkillDamage(4, level, tidalCataclysmFallbackDamage);
        float radius = Mathf.Max(0.1f, tidalCataclysmRadius);
        int affected = ServerApplyHero10000003TidalCataclysmDamage(center, radius, damage);

        ClearHero10000003TidalCataclysmChargeState();
        ServerDispatchHero10000003TidalCataclysmVfx(center);
        ServerDispatchSkillEvent(PaperLegendHeroSkillId.Hero10000003TidalCataclysm, 4, center, radius);
        Debug.Log($"[PaperLegends][Skill] Tidal Cataclysm cast by player={PlayerId}, level={level}, target={center}, affected={affected}, damage={damage:0.0}, radius={radius:0.0}.");
        return true;
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

    public bool TryGetPendingSkillTargetPlayerId(out int targetPlayerId)
    {
        targetPlayerId = _pendingSkillTargetPlayerId;
        return _hasPendingSkillTargetPlayerId;
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcShowPaperLegendPinnedCritDamage(Vector3 worldPosition, float damage)
    {
#if !UNITY_SERVER
        PaperLegendCombatDamagePopupPlayer.ShowCriticalDamage(worldPosition, damage);
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

        if (attacker.IsPinningCharacter(this)
            && ServerTryHero10000004PinDodge(attacker))
            return true;

        float newHealth = CurrentHealth - damageAmount;

        if (CharacterModelId == 10000002 && Skill4Level > 0 && !StatusIsInvincibleAtOneHealth
            && GetSkillCooldownRemaining(4) <= 0.01f
            && newHealth <= StatusInvincibilityMinimumHealth)
        {
            CurrentHealth = StatusInvincibilityMinimumHealth;
            ServerTryActivateHero10000002LastStand(manualTrigger: false);
            return true;
        }

        if (StatusIsInvincibleAtOneHealth)
        {
            CurrentHealth = Mathf.Max(StatusInvincibilityMinimumHealth, newHealth);
            return true;
        }

        CurrentHealth = Mathf.Max(0f, newHealth);
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

    public bool ServerApplyPullTowardPoint(
        PaperLegendCharacterNetworkHandler source,
        Vector3 center,
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
        Vector3 targetCenter = ownBounds.center;

        Vector3 toCenter = center - targetCenter;
        toCenter.y = 0f;
        if (toCenter.sqrMagnitude > radius * radius)
            return false;

        if (toCenter.sqrMagnitude <= 0.0001f)
        {
            toCenter = targetCenter - source.transform.position;
            toCenter.y = 0f;
        }

        Vector3 direction = toCenter.sqrMagnitude > 0.0001f ? toCenter.normalized : Vector3.forward;
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

    public bool ServerApplyDirectionalShove(
        PaperLegendCharacterNetworkHandler source,
        Vector3 direction,
        float horizontalImpulse,
        float upwardImpulse)
    {
        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
            return false;

        if (source == null || source == this || !source.IsAlive || source.IsSameFaction(this))
            return false;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        direction.Normalize();
        CacheComponents();
        if (_rigidbody == null)
            return false;

        Vector3 impulse = direction * Mathf.Max(0f, horizontalImpulse)
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

    public void ServerApplyStunStatus(float durationSeconds)
    {
        _statusController?.ServerApplyStun(durationSeconds);
    }

    public void ServerScheduleLandingStun(float durationSeconds)
    {
        if (!HasStateAuthority || !IsAlive || durationSeconds <= 0f)
            return;

        _pendingLandingStunSeconds = Mathf.Max(_pendingLandingStunSeconds, durationSeconds);
    }

    public void ServerClearStunStatus()
    {
        _pendingLandingStunSeconds = 0f;
        _statusController?.ServerClearStun();
    }

    public bool ServerHasNearbyEnemyForShoveStun(float radius)
    {
        if (!HasStateAuthority || !IsAlive)
            return false;

        radius = Mathf.Max(0.1f, radius);
        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        if (host == null)
            return false;

        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host.GetRegisteredPlayers();
        Vector3 origin = transform.position;
        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler candidate = players[i];
            if (candidate == null || candidate == this || !candidate.IsAlive || IsSameFaction(candidate))
                continue;

            Vector3 offset = candidate.transform.position - origin;
            offset.y = 0f;
            if (offset.sqrMagnitude <= radius * radius)
                return true;
        }

        return false;
    }

    private PaperLegendCharacterNetworkHandler FindRegisteredPlayerById(int playerId)
    {
        if (playerId == 0)
            return null;

        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        if (host == null)
            return null;

        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host.GetRegisteredPlayers();
        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler candidate = players[i];
            if (candidate != null && candidate.PlayerId == playerId)
                return candidate;
        }

        return null;
    }

    public bool ServerIsWithinShoveStunRadius(PaperLegendCharacterNetworkHandler target, float radius)
    {
        if (target == null)
            return false;

        radius = Mathf.Max(0.1f, radius);
        Vector3 offset = target.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= radius * radius;
    }

    private float ResolveHero10000002ShoveStunDuration(int level)
    {
        level = Mathf.Clamp(level, 1, maxSkillLevel);
        if (maxSkillLevel <= 1)
            return shoveStunMaxDurationSeconds;

        float t = (level - 1f) / Mathf.Max(1f, maxSkillLevel - 1f);
        return Mathf.Lerp(shoveStunMinDurationSeconds, shoveStunMaxDurationSeconds, t);
    }

    private float ResolveHero10000003WavePushStunDuration(int level)
    {
        level = Mathf.Clamp(level, 1, maxSkillLevel);
        if (maxSkillLevel <= 1)
            return wavePushStunMaxDurationSeconds;

        float t = (level - 1f) / Mathf.Max(1f, maxSkillLevel - 1f);
        return Mathf.Lerp(wavePushStunMinDurationSeconds, wavePushStunMaxDurationSeconds, t);
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
        LifeStateRevision++;
        State = autoRespawn ? PaperLegendCharacterState.Respawning : PaperLegendCharacterState.Eliminated;
        _respawnCountdown = autoRespawn ? respawnDelaySeconds : 0f;
        RespawnRemainingSeconds = _respawnCountdown;
        _freeFallSeconds = 0f;

        ClearArmedSkillState();
        ServerClearStunStatus();
        PaperLegendCharacterStatusEffects.ServerClearInvincibilityAtOneHealth(this);
        StopPhysicsForElimination();
        SetCollidersEnabled(false);
        PublishAuthoritativeTransform();
    }

    public void ServerRespawnAt(Vector3 position, Quaternion rotation)
    {
        if (!HasStateAuthority)
            return;

        CacheComponents();

        Quaternion resolvedRotation = SanitizeRotation(rotation);

        SetCollidersEnabled(false);

        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.position = position;
        _rigidbody.rotation = resolvedRotation;
        transform.SetPositionAndRotation(position, resolvedRotation);
        Physics.SyncTransforms();

        if (TryGetComponent<NetworkRigidbody3D>(out var networkRigidbody))
            networkRigidbody.Teleport(position, resolvedRotation);

        SetCollidersEnabled(true);
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.WakeUp();

        CurrentHealth = MaxHealth;
        State = PaperLegendCharacterState.Idle;
        LifeStateRevision++;
        IsGrounded = true;
        RespawnRemainingSeconds = 0f;
        _wasGrounded = true;
        _hadAirbornePhase = false;
        _freeFallSeconds = 0f;
        _lastEliminator = null;
        _hasLastEliminationPosition = false;
        ClearArmedSkillState();
        ServerClearStunStatus();
        PaperLegendCharacterStatusEffects.ServerClearInvincibilityAtOneHealth(this);
        PublishAuthoritativeTransform();

        Debug.Log($"[PaperLegends][Respawn] Respawned player={PlayerId} at position={position}, rotation={resolvedRotation.eulerAngles}.");
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

        if (Hero10000003WaterBurstArmed || Hero10000003WavePushArmed)
            return true;

        if (Hero10000001PaperArrowArmed)
            return true;

        if (Hero10000004HomingSwordArmed)
            return true;

        if (Hero10000002ForwardSlideArmed && Hero10000002ForwardSlideRemaining > 0)
            return true;

        if (Hero10000002ShoveStunArmed)
            return true;

        if (PaperLegendCharacterStatusEffects.BlocksFlickInput(this))
        {
            rejectReason = "character is stunned.";
            return false;
        }

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
        if (!IsAlive || IsMatchEnded() || IsStunned)
            return false;

        if (GetSkillCooldownRemaining(slot) > 0.01f)
            return false;

        return PaperLegendHeroSkillRegistry.CanUseSkill(this, slot);
    }

    public float GetSkillCooldownRemaining(int slot)
    {
        switch (Mathf.Clamp(slot, 1, 4))
        {
            case 1:
                return Skill1CooldownRemaining;
            case 2:
                return Skill2CooldownRemaining;
            case 3:
                return Skill3CooldownRemaining;
            case 4:
                return Skill4CooldownRemaining;
            default:
                return 0f;
        }
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

        if (Hero10000003WaterBurstArmed)
        {
            TryConsumeHero10000003WaterBurst(input);
            return;
        }

        if (Hero10000003WavePushArmed)
        {
            TryConsumeHero10000003WavePush(input);
            return;
        }

        if (Hero10000001PaperArrowArmed)
        {
            TryConsumeHero10000001PaperArrow(input);
            return;
        }

        if (input.Hero10000004HomingSwordRequested && Hero10000004HomingSwordArmed)
        {
            TryConsumeHero10000004HomingSword(input);
            return;
        }

        if (input.Hero10000002ForwardSlideRequested && Hero10000002ForwardSlideArmed && Hero10000002ForwardSlideRemaining > 0)
        {
            TryConsumeHero10000002ForwardSlide(input);
            return;
        }

        if (Hero10000002ShoveStunArmed)
        {
            TryConsumeHero10000002ShoveStun(input);
            return;
        }

        if (input.SkillRequested && Mathf.Clamp(input.SkillSlot, 1, 4) == 2 && input.FlickTargetPlayerId > 0)
            return;

        if (!CanAcceptAuthoritativeFlick(out string rejectReason))
        {
            Debug.LogWarning($"[PaperLegends][Input][Server] Rejected flick for player={PlayerId}: {rejectReason}");
            return;
        }

        ApplyFlick(input);
    }

    private bool TryConsumeHero10000003WaterBurst(PaperLegendPlayerInputData input)
    {
        if (!Hero10000003WaterBurstArmed)
            return false;

        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
        {
            ClearHero10000003WaterBurstState();
            return true;
        }

        Vector3 direction = ResolveSkillAimDirection(input);
        if (direction.sqrMagnitude <= 0.0001f)
            direction = ResolveFlickDirection(input);

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        direction.Normalize();

        int level = Mathf.Clamp(_hero10000003WaterBurstLevel, 1, maxSkillLevel);
        float fallbackDamage = Mathf.Max(1f, waterBurstFallbackDamage > 0f ? waterBurstFallbackDamage : AttackPower);
        float baseDamage = ResolveConfiguredSkillDamage(1, level, fallbackDamage);

        Hero10000003WaterBurstArmed = false;
        _hero10000003WaterBurstActive = true;
        _hero10000003WaterBurstLevel = level;
        _hero10000003WaterBurstNextBurstIndex = 0;
        _hero10000003WaterBurstTimerSeconds = 0f;
        _hero10000003WaterBurstBaseDamage = baseDamage;
        _hero10000003WaterBurstStartPosition = transform.position;
        _hero10000003WaterBurstDirection = direction;

        ServerDispatchHero10000003WaterBurstVfx(_hero10000003WaterBurstStartPosition, direction);
        TickHero10000003WaterBurstSequence(forceImmediate: true);
        Debug.Log($"[PaperLegends][Skill] Water Burst started by player={PlayerId}, level={level}, direction={direction}, baseDamage={baseDamage:0.0}.");
        return true;
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
        float stunDuration = ResolveHero10000003WavePushStunDuration(level);
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
                    target.ServerScheduleLandingStun(stunDuration);
                    affectedCount++;
                }
            }
        }

        ServerDispatchDirectionalSkillEvent(
            PaperLegendHeroSkillId.Hero10000003WavePush,
            2,
            origin,
            wavePushLength,
            direction);

        ClearHero10000003WavePushState();
        Debug.Log($"[PaperLegends][Skill] Son Tinh wave push fired by player={PlayerId}, level={level}, direction={direction}, affected={affectedCount}.");
        return true;
    }

    private Vector3 ClampWavePushTargetPosition(Vector3 requestedPosition)
    {
        Vector3 origin = transform.position;
        Vector3 offset = requestedPosition - origin;
        offset.y = 0f;

        float maxRange = Mathf.Max(0.1f, wavePushTargetMaxRange);
        if (offset.sqrMagnitude > maxRange * maxRange)
            offset = offset.normalized * maxRange;

        Vector3 clamped = origin + offset;
        clamped.y = requestedPosition.y;
        return clamped;
    }

    private Vector3 ClampGravityWellTargetPosition(Vector3 requestedPosition)
    {
        Vector3 origin = transform.position;
        Vector3 offset = requestedPosition - origin;
        offset.y = 0f;

        float maxRange = Mathf.Max(0.1f, gravityWellTargetMaxRange);
        if (offset.sqrMagnitude > maxRange * maxRange)
            offset = offset.normalized * maxRange;

        Vector3 clamped = origin + offset;
        clamped.y = requestedPosition.y;
        return clamped;
    }

    private Vector3 ClampTidalCataclysmTargetPosition(Vector3 requestedPosition)
    {
        Vector3 origin = transform.position;
        Vector3 offset = requestedPosition - origin;
        offset.y = 0f;

        float maxRange = Mathf.Max(0.1f, tidalCataclysmTargetMaxRange);
        if (offset.sqrMagnitude > maxRange * maxRange)
            offset = offset.normalized * maxRange;

        Vector3 clamped = origin + offset;
        clamped.y = requestedPosition.y;
        return clamped;
    }

    private Vector3 ResolveWavePushAreaDirection(Vector3 center)
    {
        Vector3 direction = center - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private int ServerApplyHero10000003WavePushArea(
        Vector3 center,
        float radius,
        Vector3 direction,
        float horizontalImpulse,
        float upwardImpulse,
        float stunDuration)
    {
        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host != null ? host.GetRegisteredPlayers() : null;
        if (players == null)
            return 0;

        int affected = 0;
        float radiusSqr = radius * radius;
        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler target = players[i];
            if (target == null || target == this || !target.IsAlive || IsSameFaction(target))
                continue;

            Vector3 offset = target.GetWorldBounds().center - center;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSqr)
                continue;

            Vector3 resolvedDirection = direction;
            if (resolvedDirection.sqrMagnitude <= 0.0001f)
            {
                resolvedDirection = target.transform.position - transform.position;
                resolvedDirection.y = 0f;
            }

            if (resolvedDirection.sqrMagnitude <= 0.0001f)
                resolvedDirection = Vector3.forward;

            resolvedDirection.Normalize();
            if (!target.ServerApplyDirectionalShove(this, resolvedDirection, horizontalImpulse, upwardImpulse))
                continue;

            target.ServerScheduleLandingStun(stunDuration);
            affected++;
        }

        return affected;
    }

    private bool TryConsumeHero10000001PaperArrow(PaperLegendPlayerInputData input)
    {
        if (!Hero10000001PaperArrowArmed)
            return false;

        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
        {
            ClearHero10000001PaperArrowState();
            return true;
        }

        Vector3 direction = ResolveSkillAimDirection(input);
        if (direction.sqrMagnitude <= 0.0001f)
            direction = ResolveFlickDirection(input);

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.Normalize();

        int level = Mathf.Clamp(_hero10000001PaperArrowLevel, 1, maxSkillLevel);
        float force01 = Mathf.Clamp01(input.Force01);
        bool spawned = ServerSpawnHero10000001PaperArrow(direction, force01, level);
        ClearHero10000001PaperArrowState();

        if (spawned)
        {
            Debug.Log($"[PaperLegends][Skill] player={PlayerId} fired paper arrow: level={level}, direction={direction}, force={force01:0.00}.");
        }
        else
        {
            Debug.LogWarning($"[PaperLegends][Skill] player={PlayerId} failed to fire paper arrow. prefabAssigned={paperArrowProjectilePrefab != null}.");
        }

        return true;
    }

    private bool TryConsumeHero10000004HomingSword(PaperLegendPlayerInputData input)
    {
        if (!Hero10000004HomingSwordArmed)
            return false;

        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
        {
            ClearHero10000004HomingSwordState();
            return true;
        }

        Vector3 direction = ResolveSkillAimDirection(input);
        if (direction.sqrMagnitude <= 0.0001f)
            direction = ResolveFlickDirection(input);

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.Normalize();

        int level = Mathf.Clamp(_hero10000004HomingSwordLevel, 1, maxSkillLevel);
        float force01 = Mathf.Clamp01(input.Force01);
        bool spawned = ServerSpawnHero10000004HomingSword(direction, force01, level);
        ClearHero10000004HomingSwordState();

        if (spawned)
        {
            StartSkillCooldown(1);
            Debug.Log($"[PaperLegends][Skill] player={PlayerId} fired homing sword: level={level}, direction={direction}, force={force01:0.00}.");
        }
        else
        {
            Debug.LogWarning($"[PaperLegends][Skill] player={PlayerId} failed to fire homing sword. prefabAssigned={hero10000004HomingSwordProjectilePrefab != null}.");
        }

        return true;
    }

    private bool ServerSpawnHero10000004HomingSword(Vector3 direction, float force01, int skillLevel)
    {
        if (!HasStateAuthority || hero10000004HomingSwordProjectilePrefab == null || Runner == null)
            return false;

        Vector3 spawnPosition = transform.position
            + direction * Mathf.Max(0f, hero10000004HomingSwordSpawnForwardOffset)
            + Vector3.up * Mathf.Max(0f, hero10000004HomingSwordSpawnHeightOffset);
        Quaternion spawnRotation = Quaternion.LookRotation(direction, Vector3.up);

        NetworkObject spawned = Runner.Spawn(
            hero10000004HomingSwordProjectilePrefab,
            spawnPosition,
            spawnRotation,
            Object.InputAuthority);

        if (spawned == null)
            return false;

        if (!spawned.TryGetComponent(out PaperLegendHomingSwordProjectile projectile))
            return false;

        float fallbackDamage = Mathf.Max(0f, hero10000004HomingSwordBaseDamage + hero10000004HomingSwordDamagePerLevel * Mathf.Max(0, skillLevel - 1));
        float damage = ResolveConfiguredSkillDamage(1, skillLevel, fallbackDamage);
        float slowDuration = ResolveHero10000004HomingSwordSlowDuration(skillLevel);
        projectile.ServerConfigureAndLaunch(
            this,
            direction,
            force01,
            (int)PaperLegendHeroSkillId.Hero10000004ReservedSkill1,
            damage,
            hero10000004HomingSwordSlowPercent,
            slowDuration);

        return true;
    }

    private static float ResolveHero10000004HomingSwordSlowDuration(int level)
    {
        return Mathf.Clamp(level, 1, 4) + 1f;
    }

    private bool ServerSpawnHero10000001PaperArrow(Vector3 direction, float force01, int skillLevel)
    {
        if (!HasStateAuthority || paperArrowProjectilePrefab == null || Runner == null)
            return false;

        Vector3 spawnPosition = transform.position
            + direction * Mathf.Max(0f, paperArrowSpawnForwardOffset)
            + Vector3.up * Mathf.Max(0f, paperArrowSpawnHeightOffset);
        Quaternion spawnRotation = Quaternion.LookRotation(direction, Vector3.up);

        NetworkObject spawned = Runner.Spawn(
            paperArrowProjectilePrefab,
            spawnPosition,
            spawnRotation,
            Object.InputAuthority);

        if (spawned == null)
            return false;

        if (!spawned.TryGetComponent(out PaperLegendPaperArrowProjectile projectile))
            return false;

        float fallbackDamage = Mathf.Max(0f, paperArrowBaseImpactDamage + paperArrowImpactDamagePerLevel * Mathf.Max(0, skillLevel - 1));
        float damage = ResolveConfiguredSkillDamage(2, skillLevel, fallbackDamage);
        projectile.ServerConfigureAndLaunch(
            this,
            direction,
            force01,
            skillLevel,
            damage,
            paperArrowSlowPercent,
            paperArrowSlowDurationSeconds);

        return true;
    }

    private void ClearConfiguredSkillDamageLevels()
    {
        for (int slot = 0; slot < SkillDamageSlotCount; slot++)
        {
            for (int level = 0; level < SkillDamageLevelCount; level++)
                _configuredSkillDamageBySlotAndLevel[slot, level] = 0f;
        }
    }

    private void ClearConfiguredSkillCooldowns()
    {
        for (int slot = 0; slot < _configuredSkillCooldownSeconds.Length; slot++)
            _configuredSkillCooldownSeconds[slot] = 0f;
    }

    private float ResolveConfiguredSkillDamage(int slot, int skillLevel, float fallbackDamage)
    {
        if (slot < 1 || slot > SkillDamageSlotCount)
            return Mathf.Max(0f, fallbackDamage);

        int level = Mathf.Clamp(skillLevel, 1, SkillDamageLevelCount);
        float configuredDamage = _configuredSkillDamageBySlotAndLevel[slot - 1, level - 1];
        return configuredDamage > 0f ? configuredDamage : Mathf.Max(0f, fallbackDamage);
    }

    private void TickSkillCooldowns()
    {
        float deltaTime = ResolveNetworkDeltaTime();
        if (deltaTime <= 0f)
            return;

        Skill1CooldownRemaining = TickCooldownValue(Skill1CooldownRemaining, deltaTime);
        Skill2CooldownRemaining = TickCooldownValue(Skill2CooldownRemaining, deltaTime);
        Skill3CooldownRemaining = TickCooldownValue(Skill3CooldownRemaining, deltaTime);
        Skill4CooldownRemaining = TickCooldownValue(Skill4CooldownRemaining, deltaTime);
    }

    private static float TickCooldownValue(float remainingSeconds, float deltaTime)
    {
        return remainingSeconds > 0f ? Mathf.Max(0f, remainingSeconds - deltaTime) : 0f;
    }

    private void StartSkillCooldown(int slot)
    {
        float cooldownSeconds = ResolveConfiguredSkillCooldown(slot);
        if (cooldownSeconds <= 0f)
            return;

        SetSkillCooldownRemaining(slot, cooldownSeconds);
    }

    private float ResolveConfiguredSkillCooldown(int slot)
    {
        if (slot < 1 || slot > _configuredSkillCooldownSeconds.Length)
            return 0f;

        return Mathf.Max(0f, _configuredSkillCooldownSeconds[slot - 1]);
    }

    private void SetSkillCooldownRemaining(int slot, float remainingSeconds)
    {
        remainingSeconds = Mathf.Max(0f, remainingSeconds);
        switch (Mathf.Clamp(slot, 1, 4))
        {
            case 1:
                Skill1CooldownRemaining = remainingSeconds;
                break;
            case 2:
                Skill2CooldownRemaining = remainingSeconds;
                break;
            case 3:
                Skill3CooldownRemaining = remainingSeconds;
                break;
            case 4:
                Skill4CooldownRemaining = remainingSeconds;
                break;
        }
    }

    private void ClearSkillCooldowns()
    {
        Skill1CooldownRemaining = 0f;
        Skill2CooldownRemaining = 0f;
        Skill3CooldownRemaining = 0f;
        Skill4CooldownRemaining = 0f;
    }

    private bool TryConsumeHero10000002ForwardSlide(PaperLegendPlayerInputData input)
    {
        if (!Hero10000002ForwardSlideArmed || Hero10000002ForwardSlideRemaining <= 0)
            return false;

        if (!HasStateAuthority || !IsAlive || IsMatchEnded() || CharacterModelId != 10000002)
        {
            ClearHero10000002ForwardSlideState();
            return true;
        }

        if (PaperLegendCharacterStatusEffects.BlocksFlickInput(this))
        {
            Debug.LogWarning($"[PaperLegends][Skill] player={PlayerId} rejected flying horse slide: character is stunned.");
            return true;
        }

        Vector3 direction = ResolveSkillAimDirection(input);
        if (direction.sqrMagnitude <= 0.0001f)
            direction = ResolveFlickDirection(input);

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.Normalize();

        float force01 = Mathf.Clamp01(input.Force01);
        float curvedForce = forceCurve != null ? Mathf.Clamp01(forceCurve.Evaluate(force01)) : force01;
        int level = Mathf.Clamp(_hero10000002ForwardSlideLevel > 0 ? _hero10000002ForwardSlideLevel : Skill1Level, 1, maxSkillLevel);
        float horizontalMultiplier = 1f + forwardSlideHorizontalMultiplierPerLevel * Mathf.Max(0, level - 1);
        if (Skill3Level > 0)
            horizontalMultiplier += PaperLegendHero10000002SkillSet.ResolveFlickSpeedBonus(Skill3Level);

        float movementMultiplier = Mathf.Max(1f, forwardSlideFastSwipeMovementMultiplier);
        float horizontalImpulse = Mathf.Lerp(forwardSlideMinHorizontalImpulse, forwardSlideMaxHorizontalImpulse, curvedForce)
            * horizontalMultiplier
            * movementMultiplier;
        float upwardImpulse = Mathf.Lerp(forwardSlideMinUpwardImpulse, forwardSlideMaxUpwardImpulse, curvedForce);
        if (Skill3Level > 0)
            upwardImpulse *= 1f + PaperLegendHero10000002SkillSet.ResolveFlickSpeedBonus(Skill3Level);
        Vector3 impulse = direction * horizontalImpulse + Vector3.up * upwardImpulse;

        ServerBoostHorizontalSpeedCap(movementMultiplier, skillSpeedCapBoostSeconds);
        _rigidbody.WakeUp();
        if (applyImpulseAtContactPoint)
            _rigidbody.AddForceAtPosition(impulse, input.ContactWorldPosition, ForceMode.Impulse);
        else
            _rigidbody.AddForce(impulse, ForceMode.Impulse);

        _flickCooldownRemaining = 0f;
        State = PaperLegendCharacterState.Flicked;
        IsGrounded = false;
        _hadAirbornePhase = true;
        _wasGrounded = false;

        Hero10000002ForwardSlideRemaining = Mathf.Max(0, Hero10000002ForwardSlideRemaining - 1);
        Hero10000002ForwardSlideArmed = Hero10000002ForwardSlideRemaining > 0;
        if (Hero10000002ForwardSlideRemaining < ResolveHero10000002ForwardSlideMaxCharges()
            && _hero10000002ForwardSlideRemainingSeconds <= 0f)
        {
            _hero10000002ForwardSlideRemainingSeconds = Mathf.Max(0.1f, forwardSlideRechargeSeconds);
        }

        ServerDispatchDirectionalSkillEvent(
            PaperLegendHeroSkillId.Hero10000002ForwardSlide,
            1,
            transform.position,
            forwardSlideVfxRadius,
            direction);

        Debug.Log($"[PaperLegends][Skill] player={PlayerId} flying horse slide: direction={direction}, force={force01:0.00}, impulse={impulse}, remainingUses={Hero10000002ForwardSlideRemaining}, remainingTime={_hero10000002ForwardSlideRemainingSeconds:0.00}s, level={level}.");
        return true;
    }

    private bool TryConsumeHero10000002ShoveStun(PaperLegendPlayerInputData input)
    {
        if (!Hero10000002ShoveStunArmed)
            return false;

        if (!HasStateAuthority || !IsAlive || IsMatchEnded() || CharacterModelId != 10000002)
        {
            ClearHero10000002ShoveStunState();
            return true;
        }

        if (input.FlickTargetPlayerId <= 0)
            return true;

        PaperLegendCharacterNetworkHandler target = FindRegisteredPlayerById(input.FlickTargetPlayerId);
        if (target == null || target == this || !target.IsAlive || IsSameFaction(target))
        {
            Debug.LogWarning($"[PaperLegends][Skill] player={PlayerId} rejected shove stun: invalid target playerId={input.FlickTargetPlayerId}.");
            return true;
        }

        if (!ServerIsWithinShoveStunRadius(target, shoveStunNearbyRadius))
        {
            Debug.LogWarning($"[PaperLegends][Skill] player={PlayerId} rejected shove stun: target player={target.PlayerId} is outside nearby radius.");
            return true;
        }

        if (!ServerHasNearbyEnemyForShoveStun(shoveStunNearbyRadius))
        {
            ClearHero10000002ShoveStunState();
            return true;
        }

        Vector3 direction = ResolveSkillAimDirection(input);
        if (direction.sqrMagnitude <= 0.0001f)
            direction = ResolveFlickDirection(input);

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = (target.transform.position - transform.position).normalized;

        direction.Normalize();

        float force01 = Mathf.Clamp01(input.Force01);
        float curvedForce = forceCurve != null ? Mathf.Clamp01(forceCurve.Evaluate(force01)) : force01;
        int level = Mathf.Clamp(_hero10000002ShoveStunLevel, 1, maxSkillLevel);
        float movementMultiplier = Mathf.Max(1f, shoveStunMovementMultiplier);
        float horizontalImpulse = Mathf.Lerp(shoveStunMinHorizontalImpulse, shoveStunMaxHorizontalImpulse, curvedForce) * movementMultiplier;
        float upwardImpulse = Mathf.Lerp(shoveStunMinUpwardImpulse, shoveStunMaxUpwardImpulse, curvedForce);
        float stunDuration = ResolveHero10000002ShoveStunDuration(level);

        target.ServerBoostHorizontalSpeedCap(movementMultiplier, skillSpeedCapBoostSeconds);
        if (!target.ServerApplyDirectionalShove(this, direction, horizontalImpulse, upwardImpulse))
        {
            Debug.LogWarning($"[PaperLegends][Skill] player={PlayerId} failed to shove target player={target.PlayerId}.");
            return true;
        }

        target.ServerScheduleLandingStun(stunDuration);
        ClearHero10000002ShoveStunState();

        ServerDispatchDirectionalSkillEvent(
            PaperLegendHeroSkillId.Hero10000002ShoveStun,
            2,
            target.transform.position,
            shoveStunVfxRadius,
            direction);

        Debug.Log($"[PaperLegends][Skill] player={PlayerId} shove stun: target={target.PlayerId}, direction={direction}, force={force01:0.00}, stun={stunDuration:0.00}s, level={level}.");
        return true;
    }

    private IEnumerator ServerHero10000003GravityWellDelayedEffectRoutine(
        Vector3 center,
        float radius,
        float damage,
        float slowPercent)
    {
        float delay = Mathf.Max(0f, gravityWellEffectDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!HasStateAuthority || IsMatchEnded())
            yield break;

        int affected = ServerApplyHero10000003GravityWell(center, radius, damage, slowPercent);
        Debug.Log($"[PaperLegends][Skill] Tidal Vortex effect resolved for player={PlayerId}, target={center}, affected={affected}, slow={slowPercent:P0}.");
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

        if (PaperLegendCharacterStatusEffects.BlocksFlickInput(this))
        {
            rejectReason = "character is stunned.";
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
        IsGrounded = false;
        _hadAirbornePhase = true;
        _wasGrounded = false;
        _hasFlickStartPosition = true;
        _flickStartPosition = transform.position;
        _activeDistanceDamageLevel = ResolveHero10000001PassiveDistanceDamageLevel();
        bool edgeBounceArmed = Hero10000001EdgeBounceArmed;
        Hero10000001DistanceDamageArmed = false;
        Hero10000001FlickBoostArmed = false;
        Hero10000001EdgeBounceArmed = false;
        ClearHero10000001EdgeBounceState();
        if (CharacterModelId == 10000001 && edgeBounceArmed && Skill4Level > 0)
        {
            _hero10000001EdgeBounceActive = true;
            _remainingHero10000001EdgeBounces = Mathf.Clamp(Skill4Level, 1, PaperLegendHero10000001SkillSet.Skill4MaxLevel);
            _hero10000001EdgeBounceDirection = direction;
            _hero10000001EdgeBounceTimerSeconds = Mathf.Max(0.05f, edgeBounceIntervalSeconds);
            Debug.Log($"[PaperLegends][Skill] player={PlayerId} armed timed edge bounce: remaining={_remainingHero10000001EdgeBounces}, interval={_hero10000001EdgeBounceTimerSeconds:0.00}s, level={Skill4Level}.");
        }

        //Debug.Log($"[PaperLegends][Input][Server] Applied flick to player={PlayerId}: impulse={impulse}, force={force01:0.00}, direction={direction}, baseFlickMul={_baseFlickForceMultiplier:0.00}, hSkillMul={horizontalSkillMultiplier:0.00}, upSkillMul={upwardSkillMultiplier:0.00}, distanceSkillLevel={_activeDistanceDamageLevel}, edgeBounceRebounds={(_hero10000001EdgeBounceActive ? _remainingHero10000001EdgeBounces : 0)}.");
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
        Hero10000001EdgeBounceArmed = false;
        ClearHero10000003WaterBurstState();
        ClearHero10000003WavePushState();
        ClearHero10000001PaperArrowState();
        ClearHero10000004HomingSwordState();
        ClearHero10000004Skill2State();
        ClearHero10000004PaperSpeedBuffState();
        ClearHero10000004PinDamageBurstState();
        _hero10000004PinDodgeIFrameRemainingSeconds = 0f;
        ClearHero10000002ForwardSlideState();
        ClearHero10000002ShoveStunState();
        ClearHero10000001EdgeBounceState();
        ClearSkillCooldowns();
        _activeDistanceDamageLevel = 0;
        _hasFlickStartPosition = false;
        MoveSlowMultiplier = 1f;
        MoveSlowTimer = TickTimer.None;
        ServerClearStunStatus();
        PaperLegendCharacterStatusEffects.ServerClearInvincibilityAtOneHealth(this);
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
        if (CharacterModelId == 10000004)
        {
            if (slot == 2 && input.SkillTargetPlayerId == 0)
            {
                Debug.LogWarning($"[PaperLegends][Skill] Rush Paper Speed rejected for player={PlayerId}: enemy target input is required.");
                return false;
            }
        }

        float cooldownRemaining = GetSkillCooldownRemaining(slot);
        if (cooldownRemaining > 0.01f)
        {
            Debug.LogWarning($"[PaperLegends][Skill] Cast rejected for player={PlayerId}, model={CharacterModelId}, slot={slot}, cooldownRemaining={cooldownRemaining:0.00}s.");
            return false;
        }

        if (!CanUseSkill(slot))
        {
            Debug.LogWarning($"[PaperLegends][Skill] Cast rejected for player={PlayerId}, model={CharacterModelId}, slot={slot}, level={GetSkillLevel(slot)}, alive={IsAlive}.");
            return false;
        }

        _hasPendingSkillTargetPosition = input.SkillTargetWorldPositionSet;
        _pendingSkillTargetWorldPosition = input.SkillTargetWorldPosition;
        _hasPendingSkillTargetPlayerId = input.SkillTargetPlayerId != 0;
        _pendingSkillTargetPlayerId = input.SkillTargetPlayerId;
        bool result = PaperLegendHeroSkillRegistry.TryUseSkill(this, slot);
        _hasPendingSkillTargetPosition = false;
        _pendingSkillTargetWorldPosition = default;
        _hasPendingSkillTargetPlayerId = false;
        _pendingSkillTargetPlayerId = 0;
        if (result && !ShouldDeferSkillCooldownUntilAfterCast(slot, input))
            StartSkillCooldown(slot);
        return result;
    }

    private bool ShouldDeferSkillCooldownUntilAfterCast(int slot, PaperLegendPlayerInputData input)
    {
        if (CharacterModelId == 10000002 && slot == 4)
            return true;

        if (CharacterModelId == 10000004 && slot == 1)
            return true;

        return false;
    }

    private bool ShouldDeferSkillCooldownUntilAfterCast(int slot)
    {
        return ShouldDeferSkillCooldownUntilAfterCast(slot, default);
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

    private int ResolveHero10000001PassiveDistanceDamageLevel()
    {
        if (CharacterModelId != 10000001 || Skill1Level <= 0)
            return 0;

        return Mathf.Clamp(Skill1Level, 1, maxSkillLevel);
    }

    private void ResolveFlickSkillMultipliers(out float horizontalMultiplier, out float upwardMultiplier)
    {
        horizontalMultiplier = 1f;
        upwardMultiplier = 1f;

        if (CharacterModelId == 10000002 && Skill3Level > 0)
        {
            float bonus = PaperLegendHero10000002SkillSet.ResolveFlickSpeedBonus(Skill3Level);
            horizontalMultiplier += bonus;
            upwardMultiplier += bonus;
        }

        if (CharacterModelId == 10000004 && _hero10000004PaperSpeedBuffRemainingSeconds > 0.01f)
        {
            float paperSpeedMultiplier = Mathf.Max(1f, _hero10000004PaperSpeedBuffMultiplier);
            horizontalMultiplier *= paperSpeedMultiplier;
            upwardMultiplier *= paperSpeedMultiplier;
        }

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

    private void TickHero10000003WaterBurstTimers()
    {
        if (Hero10000003WaterBurstArmed)
        {
            _hero10000003WaterBurstInputRemainingSeconds -= ResolveNetworkDeltaTime();
            if (_hero10000003WaterBurstInputRemainingSeconds <= 0f)
            {
                Debug.Log($"[PaperLegends][Skill] Water Burst input timed out for player={PlayerId}.");
                ClearHero10000003WaterBurstState();
            }
        }

        TickHero10000003WaterBurstSequence(forceImmediate: false);
    }

    private void TickHero10000003WaterBurstSequence(bool forceImmediate)
    {
        if (!_hero10000003WaterBurstActive)
            return;

        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
        {
            ClearHero10000003WaterBurstState();
            return;
        }

        if (!forceImmediate)
        {
            _hero10000003WaterBurstTimerSeconds -= ResolveNetworkDeltaTime();
            if (_hero10000003WaterBurstTimerSeconds > 0f)
                return;
        }

        int burstIndex = _hero10000003WaterBurstNextBurstIndex;
        if (burstIndex < 0 || burstIndex >= 3)
        {
            ClearHero10000003WaterBurstState();
            return;
        }

        Vector3 center = _hero10000003WaterBurstStartPosition
            + _hero10000003WaterBurstDirection * Mathf.Max(0.1f, waterBurstStepDistance) * burstIndex;
        float multiplier = ResolveHero10000003WaterBurstDamageMultiplier(burstIndex);
        float damage = Mathf.Max(0f, _hero10000003WaterBurstBaseDamage * multiplier);
        int affected = ServerApplyHero10000003WaterBurstDamage(center, Mathf.Max(0.1f, waterBurstRadius), damage);

        _hero10000003WaterBurstNextBurstIndex++;
        _hero10000003WaterBurstTimerSeconds = Mathf.Max(0.05f, waterBurstIntervalSeconds);

        Debug.Log($"[PaperLegends][Skill] Water Burst hit player={PlayerId}, burst={burstIndex + 1}/3, center={center}, damage={damage:0.0}, affected={affected}.");

        if (_hero10000003WaterBurstNextBurstIndex >= 3)
            ClearHero10000003WaterBurstState();
    }

    private static float ResolveHero10000003WaterBurstDamageMultiplier(int burstIndex)
    {
        switch (Mathf.Clamp(burstIndex, 0, 2))
        {
            case 1:
                return 1.2f;
            case 2:
                return 1.5f;
            default:
                return 1f;
        }
    }

    private int ServerApplyHero10000003WaterBurstDamage(Vector3 center, float radius, float damage)
    {
        if (!HasStateAuthority || damage <= 0f || IsMatchEnded())
            return 0;

        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host != null ? host.GetRegisteredPlayers() : null;
        if (players == null)
            return 0;

        int affected = 0;
        float radiusSqr = radius * radius;
        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler target = players[i];
            if (target == null || target == this || !target.IsAlive || IsSameFaction(target))
                continue;

            Vector3 offset = target.GetWorldBounds().center - center;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSqr)
                continue;

            if (target.ServerApplyPinnedDamage(this, damage))
                affected++;
        }

        return affected;
    }

    private static float ResolveHero10000003GravityWellSlowPercent(int level)
    {
        switch (Mathf.Clamp(level, 1, 4))
        {
            case 1:
                return 0.4f;
            case 2:
                return 0.5f;
            case 3:
                return 0.6f;
            default:
                return 0.7f;
        }
    }

    private int ServerApplyHero10000003GravityWell(Vector3 center, float radius, float damage, float slowPercent)
    {
        if (!HasStateAuthority || IsMatchEnded())
            return 0;

        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host != null ? host.GetRegisteredPlayers() : null;
        if (players == null)
            return 0;

        int affected = 0;
        float radiusSqr = radius * radius;
        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler target = players[i];
            if (target == null || target == this || !target.IsAlive || IsSameFaction(target))
                continue;

            Vector3 offset = target.GetWorldBounds().center - center;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSqr)
                continue;

            bool hit = false;
            if (target.ServerApplyPullTowardPoint(
                    this,
                    center,
                    radius,
                    gravityWellPullHorizontalImpulse,
                    gravityWellPullUpwardImpulse))
            {
                hit = true;
            }

            if (damage > 0f && target.ServerApplyPinnedDamage(this, damage))
                hit = true;

            if (slowPercent > 0f && gravityWellSlowDurationSeconds > 0f)
            {
                target.ServerApplyMoveSlowDebuff(slowPercent, gravityWellSlowDurationSeconds);
                hit = true;
            }

            if (hit)
                affected++;
        }

        return affected;
    }

    private int ServerApplyHero10000003TidalCataclysmDamage(Vector3 center, float radius, float damage)
    {
        if (!HasStateAuthority || damage <= 0f || IsMatchEnded())
            return 0;

        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host != null ? host.GetRegisteredPlayers() : null;
        if (players == null)
            return 0;

        int affected = 0;
        float radiusSqr = radius * radius;
        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler target = players[i];
            if (target == null || target == this || !target.IsAlive || IsSameFaction(target))
                continue;

            Vector3 offset = target.GetWorldBounds().center - center;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSqr)
                continue;

            if (target.ServerApplyPinnedDamage(this, damage))
                affected++;
        }

        return affected;
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

    private void TickHero10000002ForwardSlideTimer()
    {
        if (!HasStateAuthority || CharacterModelId != 10000002 || Skill1Level <= 0 || !IsAlive || IsMatchEnded())
            return;

        int maxCharges = ResolveHero10000002ForwardSlideMaxCharges();
        if (_hero10000002ForwardSlideLevel <= 0 && Hero10000002ForwardSlideRemaining <= 0)
        {
            Hero10000002ForwardSlideRemaining = maxCharges;
            Hero10000002ForwardSlideArmed = true;
            _hero10000002ForwardSlideRemainingSeconds = Mathf.Max(0.1f, forwardSlideRechargeSeconds);
        }

        _hero10000002ForwardSlideLevel = Mathf.Clamp(Skill1Level, 1, maxSkillLevel);
        Hero10000002ForwardSlideRemaining = Mathf.Clamp(Hero10000002ForwardSlideRemaining, 0, maxCharges);

        if (Hero10000002ForwardSlideRemaining >= maxCharges)
        {
            Hero10000002ForwardSlideArmed = true;
            _hero10000002ForwardSlideRemainingSeconds = Mathf.Max(0.1f, forwardSlideRechargeSeconds);
            return;
        }

        _hero10000002ForwardSlideRemainingSeconds -= ResolveNetworkDeltaTime();
        if (_hero10000002ForwardSlideRemainingSeconds > 0f)
        {
            Hero10000002ForwardSlideArmed = Hero10000002ForwardSlideRemaining > 0;
            return;
        }

        Hero10000002ForwardSlideRemaining = Mathf.Clamp(Hero10000002ForwardSlideRemaining + 1, 0, maxCharges);
        Hero10000002ForwardSlideArmed = Hero10000002ForwardSlideRemaining > 0;
        _hero10000002ForwardSlideRemainingSeconds = Mathf.Max(0.1f, forwardSlideRechargeSeconds);
        Debug.Log($"[PaperLegends][Skill] hero 10000002 flying horse recharged for player={PlayerId}: charges={Hero10000002ForwardSlideRemaining}/{maxCharges}.");
    }

    private void TickHero10000002ShoveStunTimer()
    {
        if (!Hero10000002ShoveStunArmed)
            return;

        _hero10000002ShoveStunRemainingSeconds -= ResolveNetworkDeltaTime();
        if (_hero10000002ShoveStunRemainingSeconds > 0f)
            return;

        Debug.Log($"[PaperLegends][Skill] hero 10000002 shove stun timed out for player={PlayerId}.");
        ClearHero10000002ShoveStunState();
    }

    private void ServerBoostHorizontalSpeedCap(float multiplier, float durationSeconds)
    {
        if (!HasStateAuthority)
            return;

        _temporaryHorizontalSpeedCapMultiplier = Mathf.Max(_temporaryHorizontalSpeedCapMultiplier, Mathf.Max(1f, multiplier));
        _temporaryHorizontalSpeedCapRemainingSeconds = Mathf.Max(
            _temporaryHorizontalSpeedCapRemainingSeconds,
            Mathf.Max(0.05f, durationSeconds));
    }

    private void TickTemporaryHorizontalSpeedCapBoost()
    {
        if (_temporaryHorizontalSpeedCapRemainingSeconds <= 0f)
        {
            _temporaryHorizontalSpeedCapMultiplier = 1f;
            return;
        }

        _temporaryHorizontalSpeedCapRemainingSeconds -= ResolveNetworkDeltaTime();
        if (_temporaryHorizontalSpeedCapRemainingSeconds <= 0f)
        {
            _temporaryHorizontalSpeedCapRemainingSeconds = 0f;
            _temporaryHorizontalSpeedCapMultiplier = 1f;
        }
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
            TickFreeFallSafety();
            return;
        }

        _freeFallSeconds = 0f;

        if (!_wasGrounded && _hadAirbornePhase && _pendingLandingStunSeconds > 0f)
        {
            float pendingStun = _pendingLandingStunSeconds;
            _pendingLandingStunSeconds = 0f;
            ServerApplyStunStatus(pendingStun);
        }

        if (!_wasGrounded && _hadAirbornePhase && HandleLanding())
        {
            _wasGrounded = false;
            return;
        }

        if (_rigidbody.linearVelocity.sqrMagnitude <= groundedVelocityThreshold * groundedVelocityThreshold)
            State = PaperLegendCharacterState.Idle;
        else if (State == PaperLegendCharacterState.Airborne || State == PaperLegendCharacterState.Flicked)
            State = PaperLegendCharacterState.Grounded;

        _wasGrounded = true;
    }

    private void TickFreeFallSafety()
    {
        if (!HasStateAuthority || !IsAlive || IsMatchEnded())
            return;

        _freeFallSeconds += ResolveNetworkDeltaTime();
        if (_freeFallSeconds < Mathf.Max(0.1f, freeFallRespawnSeconds) && transform.position.y > freeFallRespawnMinY)
            return;

        ServerRecoverFromFreeFall();
    }

    private void ServerRecoverFromFreeFall()
    {
        if (!HasStateAuthority || !IsAlive)
            return;

        float preservedHealth = Mathf.Clamp(CurrentHealth, 1f, MaxHealth);
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

        Debug.LogWarning($"[PaperLegends][FallSafety] player={PlayerId} recovered after free fall {_freeFallSeconds:0.00}s at y={transform.position.y:0.00}. Respawning to {respawnPosition}.");
        ServerRespawnAt(respawnPosition, respawnRotation);
        CurrentHealth = preservedHealth;
        _freeFallSeconds = 0f;
        PublishAuthoritativeTransform();
    }

    private bool HandleLanding()
    {
        ApplyDistanceLandingDamageSkill();

        if (ShouldKeepDistanceDamageForEdgeBounce())
        {
            _hadAirbornePhase = true;
            State = PaperLegendCharacterState.Flicked;
            return true;
        }

        ClearHero10000001EdgeBounceState();
        State = PaperLegendCharacterState.Grounded;
        _hadAirbornePhase = false;
        return false;
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
            if (!ShouldKeepDistanceDamageForEdgeBounce())
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
        Hero10000001EdgeBounceArmed = false;
        ClearHero10000003WaterBurstState();
        ClearHero10000003WavePushState();
        Hero10000003GravityWellChargeVfxPosition = default;
        ClearHero10000003TidalCataclysmChargeState();
        ClearHero10000001PaperArrowState();
        ClearHero10000004HomingSwordState();
        ClearHero10000004Skill2State();
        ClearHero10000002ForwardSlideState();
        ClearHero10000002ShoveStunState();
        ClearHero10000001EdgeBounceState();
        ClearDistanceLandingDamageState();
    }

    private bool ShouldKeepDistanceDamageForEdgeBounce()
    {
        return CharacterModelId == 10000001
            && _hero10000001EdgeBounceActive
            && _remainingHero10000001EdgeBounces > 0;
    }

    private void ClearHero10000001EdgeBounceState()
    {
        _hero10000001EdgeBounceActive = false;
        _remainingHero10000001EdgeBounces = 0;
        _hero10000001EdgeBounceDirection = Vector3.zero;
        _hero10000001EdgeBounceTimerSeconds = 0f;
    }

    private void TickHero10000001EdgeBounceTimer()
    {
        if (!HasStateAuthority || CharacterModelId != 10000001 || !_hero10000001EdgeBounceActive)
            return;

        if (_remainingHero10000001EdgeBounces <= 0 || !IsAlive || IsMatchEnded())
        {
            ClearHero10000001EdgeBounceState();
            return;
        }

        _hero10000001EdgeBounceTimerSeconds -= ResolveNetworkDeltaTime();
        if (_hero10000001EdgeBounceTimerSeconds > 0f)
            return;

        ApplyHero10000001TimedEdgeBounce();
        _hero10000001EdgeBounceTimerSeconds = Mathf.Max(0.05f, edgeBounceIntervalSeconds);
    }

    private void ApplyHero10000001TimedEdgeBounce()
    {
        Vector3 direction = ResolveEdgeBounceDirection();
        Vector3 impulse = direction * edgeBounceHorizontalImpulse + Vector3.up * edgeBounceUpwardImpulse;
        CacheComponents();
        if (_rigidbody == null)
            return;

        _rigidbody.isKinematic = false;
        _rigidbody.WakeUp();
        _rigidbody.linearVelocity = impulse;
        IsGrounded = false;
        _wasGrounded = false;
        _hadAirbornePhase = true;
        State = PaperLegendCharacterState.Flicked;

        _remainingHero10000001EdgeBounces--;
        _hero10000001EdgeBounceDirection = direction;

        ServerDispatchHero10000001EdgeBounceFeedback(transform.position, direction);

        Debug.Log($"[PaperLegends][Skill] player={PlayerId} timed edge bounce rebound: direction={direction}, remaining={_remainingHero10000001EdgeBounces}, impulse={impulse}.");
        if (_remainingHero10000001EdgeBounces <= 0)
            ClearHero10000001EdgeBounceState();
    }

    private void ServerDispatchHero10000001EdgeBounceFeedback(Vector3 worldPosition, Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();

        Hero10000001EdgeBounceLandingVfxPosition = worldPosition;
        Hero10000001EdgeBounceLandingVfxDirection = direction;
        Hero10000001EdgeBounceLandingVfxTick++;
    }

    private void ServerDispatchHero10000003WaterBurstVfx(Vector3 startPosition, Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();

        Hero10000003WaterBurstVfxStartPosition = startPosition;
        Hero10000003WaterBurstVfxDirection = direction;
        Hero10000003WaterBurstVfxTick++;
    }

    private void ServerDispatchHero10000003GravityWellVfx(Vector3 worldPosition)
    {
        Hero10000003GravityWellVfxPosition = worldPosition;
        Hero10000003GravityWellVfxTick++;
    }

    private void ServerDispatchHero10000003TidalCataclysmVfx(Vector3 worldPosition)
    {
        Hero10000003TidalCataclysmVfxPosition = worldPosition;
        Hero10000003TidalCataclysmVfxTick++;
    }

    private void ClearHero10000003TidalCataclysmChargeState()
    {
        Hero10000003TidalCataclysmCharging = false;
        Hero10000003TidalCataclysmChargeVfxPosition = default;
        _hero10000003TidalCataclysmChargeAnchorCaptured = false;
        _hero10000003TidalCataclysmChargeAnchorPosition = default;
    }

    private void ServerFailHero10000003TidalCataclysmChargeDueToMovement()
    {
        if (!Hero10000003TidalCataclysmCharging)
            return;

        ClearHero10000003TidalCataclysmChargeState();
        StartSkillCooldown(4);
        Debug.Log($"[PaperLegends][Skill] Tidal Cataclysm failed for player={PlayerId} because the hero moved during charge. Cooldown applied.");
    }

    private void TickHero10000003TidalCataclysmChargeMovement()
    {
        if (!HasStateAuthority || CharacterModelId != 10000003 || !Hero10000003TidalCataclysmCharging || !_hero10000003TidalCataclysmChargeAnchorCaptured)
            return;

        Vector3 delta = transform.position - _hero10000003TidalCataclysmChargeAnchorPosition;
        delta.y = 0f;
        float breakDistance = Mathf.Max(0.05f, tidalCataclysmChargeMovementBreakDistance);
        if (delta.sqrMagnitude <= breakDistance * breakDistance)
            return;

        ServerFailHero10000003TidalCataclysmChargeDueToMovement();
    }

    private void OnHero10000001EdgeBounceLandingVfxChanged()
    {
#if !UNITY_SERVER
        int tick = Hero10000001EdgeBounceLandingVfxTick;
        if (tick <= 0 || tick == _lastRenderedHero10000001EdgeBounceLandingVfxTick)
            return;

        _lastRenderedHero10000001EdgeBounceLandingVfxTick = tick;
        HeroAudioPlayer.PlaySkillForCharacter(this, (int)PaperLegendHeroSkillId.Hero10000001EdgeBounceRebound, 4);
        PaperLegendHeroSkillVfxPlayer.PlaySkillVfx(
            this,
            (int)PaperLegendHeroSkillId.Hero10000001EdgeBounceRebound,
            Hero10000001EdgeBounceLandingVfxPosition,
            edgeBounceVfxRadius,
            Hero10000001EdgeBounceLandingVfxDirection);
#endif
    }

    private void OnHero10000003WaterBurstVfxChanged()
    {
#if !UNITY_SERVER
        int tick = Hero10000003WaterBurstVfxTick;
        if (tick <= 0 || tick == _lastRenderedHero10000003WaterBurstVfxTick)
            return;

        _lastRenderedHero10000003WaterBurstVfxTick = tick;
        HeroAudioPlayer.PlaySkillForCharacter(this, (int)PaperLegendHeroSkillId.Hero10000003WaterBurst, 1);
        PaperLegendHeroSkillVfxPlayer.PlaySkillVfx(
            this,
            (int)PaperLegendHeroSkillId.Hero10000003WaterBurst,
            Hero10000003WaterBurstVfxStartPosition,
            waterBurstRadius,
            Hero10000003WaterBurstVfxDirection);
#endif
    }

    private void OnHero10000003GravityWellChargeVfxChanged()
    {
#if !UNITY_SERVER
        int tick = Hero10000003GravityWellChargeVfxTick;
        if (tick <= 0 || tick == _lastRenderedHero10000003GravityWellChargeVfxTick)
            return;

        _lastRenderedHero10000003GravityWellChargeVfxTick = tick;
        HeroAudioPlayer.PlaySkillForCharacter(this, (int)PaperLegendHeroSkillId.Hero10000003GravityWell, 3);
        PaperLegendHeroSkillVfxPlayer.PlaySkillVfx(
            this,
            (int)PaperLegendHeroSkillId.Hero10000003GravityWell,
            Hero10000003GravityWellChargeVfxPosition,
            gravityWellRadius);
#endif
    }

    private void OnHero10000003GravityWellVfxChanged()
    {
#if !UNITY_SERVER
        int tick = Hero10000003GravityWellVfxTick;
        if (tick <= 0 || tick == _lastRenderedHero10000003GravityWellVfxTick)
            return;

        _lastRenderedHero10000003GravityWellVfxTick = tick;
        HeroAudioPlayer.PlaySkillForCharacter(this, (int)PaperLegendHeroSkillId.Hero10000003GravityWell, 3);
        PaperLegendHeroSkillVfxPlayer.PlaySkillVfx(
            this,
            (int)PaperLegendHeroSkillId.Hero10000003GravityWell,
            Hero10000003GravityWellVfxPosition,
            gravityWellRadius);
#endif
    }

    private void OnHero10000003TidalCataclysmChargingChanged()
    {
#if !UNITY_SERVER
        if (Hero10000003TidalCataclysmCharging)
            BeginHero10000003TidalCataclysmChargePose();
        else
            EndHero10000003TidalCataclysmChargePose();
#endif
    }

    private void OnHero10000003TidalCataclysmChargeVfxChanged()
    {
#if !UNITY_SERVER
        int tick = Hero10000003TidalCataclysmChargeVfxTick;
        if (tick <= 0 || tick == _lastRenderedHero10000003TidalCataclysmChargeVfxTick)
            return;

        _lastRenderedHero10000003TidalCataclysmChargeVfxTick = tick;
        HeroAudioPlayer.PlaySkillForCharacter(this, (int)PaperLegendHeroSkillId.Hero10000003TidalCataclysm, 4);
        PaperLegendHeroSkillVfxPlayer.PlaySkillVfx(
            this,
            (int)PaperLegendHeroSkillId.Hero10000003TidalCataclysm,
            Hero10000003TidalCataclysmChargeVfxPosition,
            tidalCataclysmRadius * 0.72f);
#endif
    }

    private void OnHero10000003TidalCataclysmVfxChanged()
    {
#if !UNITY_SERVER
        int tick = Hero10000003TidalCataclysmVfxTick;
        if (tick <= 0 || tick == _lastRenderedHero10000003TidalCataclysmVfxTick)
            return;

        _lastRenderedHero10000003TidalCataclysmVfxTick = tick;
        HeroAudioPlayer.PlaySkillForCharacter(this, (int)PaperLegendHeroSkillId.Hero10000003TidalCataclysm, 4);
        PaperLegendHeroSkillVfxPlayer.PlaySkillVfx(
            this,
            (int)PaperLegendHeroSkillId.Hero10000003TidalCataclysm,
            Hero10000003TidalCataclysmVfxPosition,
            tidalCataclysmRadius);
#endif
    }

#if !UNITY_SERVER
    private Transform ResolveTidalCataclysmChargeShakeTransform()
    {
        PaperLegendCharacterClientVisualSpawner spawner = GetComponent<PaperLegendCharacterClientVisualSpawner>();
        if (spawner != null && spawner.SpawnedVisual != null)
            return spawner.SpawnedVisual.transform;

        return transform;
    }

    private void BeginHero10000003TidalCataclysmChargePose()
    {
        EndHero10000003TidalCataclysmChargePose();

        Transform target = ResolveTidalCataclysmChargeShakeTransform();
        _tidalCataclysmChargeShakeTransform = target;
        _tidalCataclysmChargeShakeBaseLocalPosition = target.localPosition;
        _tidalCataclysmChargeShakeBaseLocalScale = target.localScale;

        _tidalCataclysmChargeShakeTween = target
            .DOShakePosition(0.42f, new Vector3(0.05f, 0.08f, 0.05f), 16, 90f, false, false)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);

        _tidalCataclysmChargePoseSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(target.DOScale(_tidalCataclysmChargeShakeBaseLocalScale * 1.06f, 0.28f).SetEase(Ease.OutQuad))
            .Append(target.DOScale(_tidalCataclysmChargeShakeBaseLocalScale * 0.97f, 0.22f).SetEase(Ease.InOutSine))
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void EndHero10000003TidalCataclysmChargePose()
    {
        if (_tidalCataclysmChargeShakeTween != null)
        {
            _tidalCataclysmChargeShakeTween.Kill();
            _tidalCataclysmChargeShakeTween = null;
        }

        if (_tidalCataclysmChargePoseSequence != null)
        {
            _tidalCataclysmChargePoseSequence.Kill();
            _tidalCataclysmChargePoseSequence = null;
        }

        if (_tidalCataclysmChargeShakeTransform != null)
        {
            _tidalCataclysmChargeShakeTransform.localPosition = _tidalCataclysmChargeShakeBaseLocalPosition;
            _tidalCataclysmChargeShakeTransform.localScale = _tidalCataclysmChargeShakeBaseLocalScale;
            _tidalCataclysmChargeShakeTransform = null;
        }
    }
#endif

    private Vector3 ResolveEdgeBounceDirection()
    {
        CacheComponents();

        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.01f)
            return velocity.normalized;

        Vector3 fallback = _hero10000001EdgeBounceDirection;
        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.01f)
            return fallback.normalized;

        fallback = transform.forward;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.01f ? fallback.normalized : Vector3.forward;
    }

    private void ClearHero10000003WavePushState()
    {
        Hero10000003WavePushArmed = false;
        _hero10000003WavePushLevel = 0;
        _hero10000003WavePushRemainingSeconds = 0f;
    }

    private void ClearHero10000003WaterBurstState()
    {
        Hero10000003WaterBurstArmed = false;
        _hero10000003WaterBurstLevel = 0;
        _hero10000003WaterBurstInputRemainingSeconds = 0f;
        _hero10000003WaterBurstActive = false;
        _hero10000003WaterBurstNextBurstIndex = 0;
        _hero10000003WaterBurstTimerSeconds = 0f;
        _hero10000003WaterBurstBaseDamage = 0f;
        _hero10000003WaterBurstStartPosition = Vector3.zero;
        _hero10000003WaterBurstDirection = Vector3.zero;
    }

    private void ClearHero10000001PaperArrowState()
    {
        Hero10000001PaperArrowArmed = false;
        _hero10000001PaperArrowLevel = 0;
    }

    private void ClearHero10000004HomingSwordState()
    {
        Hero10000004HomingSwordArmed = false;
        _hero10000004HomingSwordLevel = 0;
    }

    private void ClearHero10000004Skill2State()
    {
        Hero10000004Skill2Armed = false;
        _hero10000004Skill2Level = 0;
        _hero10000004Skill2ArmRemainingSeconds = 0f;
    }

    private void ClearHero10000004PaperSpeedBuffState()
    {
        _hero10000004PaperSpeedBuffMultiplier = 1f;
        _hero10000004PaperSpeedBuffRemainingSeconds = 0f;
    }

    private void TickHero10000004Skill2ArmTimer()
    {
        if (!HasStateAuthority || CharacterModelId != 10000004 || !Hero10000004Skill2Armed)
            return;

        _hero10000004Skill2ArmRemainingSeconds -= ResolveNetworkDeltaTime();
        if (_hero10000004Skill2ArmRemainingSeconds > 0f)
            return;

        Debug.Log($"[PaperLegends][Skill] Hero 10000004 skill 2 arm timed out for player={PlayerId}.");
        ClearHero10000004Skill2State();
    }

    private void TickHero10000004PaperSpeedBuff()
    {
        if (!HasStateAuthority || CharacterModelId != 10000004)
            return;

        if (_hero10000004PaperSpeedBuffRemainingSeconds <= 0f)
        {
            _hero10000004PaperSpeedBuffMultiplier = 1f;
            return;
        }

        if (!IsGrounded && _rigidbody != null && _hero10000004PaperSpeedBuffMultiplier > 1.01f)
        {
            float gravityBoost = Mathf.Max(0f, hero10000004RushPaperSpeedFallGravityBoost)
                * (_hero10000004PaperSpeedBuffMultiplier - 1f);
            if (gravityBoost > 0f)
                _rigidbody.AddForce(Vector3.down * gravityBoost, ForceMode.Acceleration);
        }

        _hero10000004PaperSpeedBuffRemainingSeconds -= ResolveNetworkDeltaTime();
        if (_hero10000004PaperSpeedBuffRemainingSeconds <= 0f)
        {
            _hero10000004PaperSpeedBuffRemainingSeconds = 0f;
            _hero10000004PaperSpeedBuffMultiplier = 1f;
        }
    }

    private void ServerApplyHero10000004PaperSpeedBuff(float multiplier, float durationSeconds)
    {
        multiplier = Mathf.Max(1f, multiplier);
        durationSeconds = Mathf.Max(0.05f, durationSeconds);
        _hero10000004PaperSpeedBuffMultiplier = Mathf.Max(_hero10000004PaperSpeedBuffMultiplier, multiplier);
        _hero10000004PaperSpeedBuffRemainingSeconds = Mathf.Max(_hero10000004PaperSpeedBuffRemainingSeconds, durationSeconds);
        ServerBoostHorizontalSpeedCap(multiplier, durationSeconds);
    }

    private void ServerRelocateNearTargetForHero10000004Rush(PaperLegendCharacterNetworkHandler target)
    {
        if (!HasStateAuthority || target == null)
            return;

        CacheComponents();

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        float stopDistance = Mathf.Max(0.35f, hero10000004RushPaperSpeedStopDistance);
        Vector3 landingPosition = target.transform.position - direction * stopDistance;
        landingPosition.y = transform.position.y;

        _rigidbody.isKinematic = true;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.position = landingPosition;
        _rigidbody.rotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.SetPositionAndRotation(landingPosition, _rigidbody.rotation);
        Physics.SyncTransforms();

        if (TryGetComponent<NetworkRigidbody3D>(out var networkRigidbody))
            networkRigidbody.Teleport(landingPosition, _rigidbody.rotation);

        _rigidbody.isKinematic = false;
        _rigidbody.WakeUp();
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        IsGrounded = true;
        State = PaperLegendCharacterState.Grounded;
        PublishAuthoritativeTransform();
    }

    private void ClearHero10000002ForwardSlideState()
    {
        Hero10000002ForwardSlideArmed = false;
        Hero10000002ForwardSlideRemaining = 0;
        _hero10000002ForwardSlideLevel = 0;
        _hero10000002ForwardSlideRemainingSeconds = 0f;
    }

    private int ResolveHero10000002ForwardSlideMaxCharges()
    {
        return Mathf.Max(1, forwardSlideMaxCharges);
    }

    private void ClearHero10000002ShoveStunState()
    {
        Hero10000002ShoveStunArmed = false;
        _hero10000002ShoveStunLevel = 0;
        _hero10000002ShoveStunRemainingSeconds = 0f;
    }

    private void TickMoveSlowDebuff()
    {
        if (!HasStateAuthority)
            return;

        if (MoveSlowMultiplier <= 0.999f && MoveSlowTimer.Expired(Runner))
            MoveSlowMultiplier = 1f;
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

        bool pinnedVictimThisFrame = false;
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
            pinnedVictimThisFrame = true;

            break;
        }

        if (!pinnedVictimThisFrame)
            FlushHero10000004PinDamageBurst(forcePartial: true);
    }

    private void ApplyPinnedDamageToVictim(PaperLegendCharacterNetworkHandler victim, float deltaTime)
    {
        if (victim == null || deltaTime <= 0f)
            return;

        float damagePerSecond = CalculatePinnedDamagePerSecond(victim);
        float damage = damagePerSecond * deltaTime;

        if (CharacterModelId != 10000004 || Skill4Level <= 0)
        {
            victim.ServerApplyPinnedDamage(this, damage);
            return;
        }

        if (_hero10000004PinDamageVictim != victim)
        {
            FlushHero10000004PinDamageBurst(forcePartial: true);
            _hero10000004PinDamageVictim = victim;
        }

        _hero10000004PinDamageAccumulator += damage;
        _hero10000004PinDamageBurstTimer += deltaTime;

        float burstInterval = Mathf.Max(0.05f, hero10000004PinCritBurstIntervalSeconds);
        if (_hero10000004PinDamageBurstTimer >= burstInterval)
            FlushHero10000004PinDamageBurst(forcePartial: false);
    }

    private void FlushHero10000004PinDamageBurst(bool forcePartial)
    {
        if (!HasStateAuthority || CharacterModelId != 10000004 || Skill4Level <= 0)
        {
            ClearHero10000004PinDamageBurstState();
            return;
        }

        PaperLegendCharacterNetworkHandler victim = _hero10000004PinDamageVictim;
        float accumulatedDamage = _hero10000004PinDamageAccumulator;
        if (victim == null || accumulatedDamage <= 0.0001f)
        {
            if (forcePartial)
                ClearHero10000004PinDamageBurstState();
            return;
        }

        int level = Mathf.Clamp(Skill4Level, 1, maxSkillLevel);
        bool isCrit = Random.value <= PaperLegendHero10000004SonTinhSkillSet.LandingPinCritChance;
        float damageMultiplier = isCrit
            ? PaperLegendHero10000004SonTinhSkillSet.ResolveLandingPinCritMultiplier(level)
            : 1f;
        float finalDamage = accumulatedDamage * damageMultiplier;

        _hero10000004PinDamageAccumulator = 0f;
        _hero10000004PinDamageBurstTimer = 0f;

        if (finalDamage > 0.0001f)
            victim.ServerApplyPinnedDamage(this, finalDamage);

        if (isCrit && finalDamage > 0.5f)
        {
            Vector3 popupPosition = victim.transform.position + Vector3.up * hero10000004PinCritPopupHeightOffset;
            RpcShowPaperLegendPinnedCritDamage(popupPosition, finalDamage);
            Debug.Log($"[PaperLegends][Skill] Hero 10000004 player={PlayerId} landed pin crit on victim={victim.PlayerId}, level={level}, multiplier={damageMultiplier:0.00}x, damage={finalDamage:0.0}.");
        }

        if (forcePartial)
            ClearHero10000004PinDamageBurstState();
    }

    private void ClearHero10000004PinDamageBurstState()
    {
        _hero10000004PinDamageAccumulator = 0f;
        _hero10000004PinDamageBurstTimer = 0f;
        _hero10000004PinDamageVictim = null;
    }

    private void TickHero10000004PinDodgeIFrames()
    {
        if (!HasStateAuthority || CharacterModelId != 10000004)
            return;

        if (_hero10000004PinDodgeIFrameRemainingSeconds <= 0f)
            return;

        _hero10000004PinDodgeIFrameRemainingSeconds -= ResolveNetworkDeltaTime();
        if (_hero10000004PinDodgeIFrameRemainingSeconds < 0f)
            _hero10000004PinDodgeIFrameRemainingSeconds = 0f;
    }

    public bool IsPinningCharacter(PaperLegendCharacterNetworkHandler victim)
    {
        return victim != null && IsPressingVictimFromAbove(victim);
    }

    private bool ServerTryHero10000004PinDodge(PaperLegendCharacterNetworkHandler attacker)
    {
        if (!HasStateAuthority || !IsAlive || IsMatchEnded() || CharacterModelId != 10000004 || Skill3Level <= 0)
            return false;

        if (_hero10000004PinDodgeIFrameRemainingSeconds > 0.01f)
            return false;

        if (attacker == null || !attacker.IsAlive || attacker.IsSameFaction(this))
            return false;

        int level = Mathf.Clamp(Skill3Level, 1, maxSkillLevel);
        float dodgeChance = PaperLegendHero10000004SonTinhSkillSet.ResolvePinDodgeChance(level);
        if (Random.value > dodgeChance)
            return false;

        ServerPerformHero10000004PinDodgeEscape(attacker);
        Debug.Log($"[PaperLegends][Skill] Hero 10000004 player={PlayerId} dodged pin damage from attacker={attacker.PlayerId}, level={level}, chance={dodgeChance:P0}.");
        return true;
    }

    private void ServerPerformHero10000004PinDodgeEscape(PaperLegendCharacterNetworkHandler attacker)
    {
        if (!HasStateAuthority || attacker == null)
            return;

        CacheComponents();
        if (_rigidbody == null)
            return;

        Vector3 away = transform.position - attacker.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude <= 0.0001f)
        {
            away = transform.position - attacker.transform.forward;
            away.y = 0f;
        }

        if (away.sqrMagnitude <= 0.0001f)
            away = Vector3.right;
        else
            away.Normalize();

        float slideDistance = ResolveHero10000004PinDodgeSlideDistance(attacker);
        Vector3 landingPosition = transform.position + away * slideDistance;
        landingPosition.y = transform.position.y;

        _rigidbody.isKinematic = true;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.position = landingPosition;
        transform.SetPositionAndRotation(landingPosition, transform.rotation);
        Physics.SyncTransforms();

        if (TryGetComponent<NetworkRigidbody3D>(out NetworkRigidbody3D networkRigidbody))
            networkRigidbody.Teleport(landingPosition, transform.rotation);

        _rigidbody.isKinematic = false;
        _rigidbody.WakeUp();
        _rigidbody.linearVelocity = away * Mathf.Max(0.1f, hero10000004PinDodgeSlideSpeed);
        _rigidbody.angularVelocity = Vector3.zero;

        _hero10000004PinDodgeIFrameRemainingSeconds = Mathf.Max(0.05f, hero10000004PinDodgeIFrameSeconds);
        IsGrounded = true;
        State = PaperLegendCharacterState.Grounded;
        _hadAirbornePhase = false;
        PublishAuthoritativeTransform();

        ServerDispatchSkillEvent(
            PaperLegendHeroSkillId.Hero10000004ReservedSkill3,
            3,
            landingPosition,
            0.55f);
    }

    private float ResolveHero10000004PinDodgeSlideDistance(PaperLegendCharacterNetworkHandler attacker)
    {
        Bounds victimBounds = GetWorldBounds();
        Bounds attackerBounds = attacker.GetWorldBounds();
        float overlapX = Mathf.Min(attackerBounds.max.x, victimBounds.max.x) - Mathf.Max(attackerBounds.min.x, victimBounds.min.x);
        float overlapZ = Mathf.Min(attackerBounds.max.z, victimBounds.max.z) - Mathf.Max(attackerBounds.min.z, victimBounds.min.z);
        float overlap = Mathf.Max(overlapX, overlapZ);
        float neededDistance = overlap + pinnedMinHorizontalOverlap + 0.08f;
        return Mathf.Max(hero10000004PinDodgeMinSlideDistance, neededDistance);
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
        float speedCap = maxHorizontalSpeed
            * Mathf.Clamp(MoveSlowMultiplier, 0.1f, 1f)
            * Mathf.Max(1f, _temporaryHorizontalSpeedCapMultiplier);
        if (horizontal.sqrMagnitude <= speedCap * speedCap)
            return;

        horizontal = horizontal.normalized * speedCap;
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

