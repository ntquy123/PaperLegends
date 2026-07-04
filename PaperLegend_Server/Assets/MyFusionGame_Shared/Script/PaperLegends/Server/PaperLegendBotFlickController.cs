#if UNITY_SERVER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PaperLegendBotFlickController : MonoBehaviour
{
    public static PaperLegendBotFlickController Instance { get; private set; }

    [Header("Thinking")]
    [SerializeField, Min(0.05f)] private float thinkIntervalSeconds = 0.25f;

    [Header("Charge Timing")]
    [SerializeField, Min(0.1f)] private float minChargeSeconds = 2f;
    [SerializeField, Min(0.1f)] private float maxChargeSeconds = 4f;
    [SerializeField, Min(0f)] private float chargeRandomJitterSeconds = 0.25f;

    [Header("Flick Planning")]
    [SerializeField, Min(0.01f)] private float contactOffset = 0.35f;
    [SerializeField, Range(0f, 1f)] private float minForce01 = 0.45f;
    [SerializeField, Range(0f, 1f)] private float maxForce01 = 0.85f;
    [SerializeField, Min(0.01f)] private float minPlannedLandingDistance = 0.75f;
    [SerializeField, Min(0.01f)] private float maxPlannedLandingDistance = 5.5f;
    [SerializeField, Min(0f)] private float targetOvershootDistance = 0.18f;
    [SerializeField, Min(0f)] private float aimNoiseRadius = 0.08f;

    [Header("Target Selection")]
    [SerializeField, Min(0)] private int dangerousLevelGap = 3;
    [SerializeField, Min(0f)] private float highLevelPenaltyPerLevel = 2.2f;
    [SerializeField, Min(0f)] private float lowerLevelBonusPerLevel = 0.35f;
    [SerializeField, Min(0f)] private float airborneTargetPenalty = 1.25f;

    [Header("Drum Objective")]
    [SerializeField] private bool enableDrumObjectiveSeeking = true;
    [SerializeField, Min(0f)] private float drumSeekMaxDistance = 14f;
    [SerializeField, Min(0f)] private float drumCaptureHoldDistance = 0.75f;
    [SerializeField, Min(0f)] private float drumObjectiveScoreBonus = 4f;
    [SerializeField, Min(0f)] private float drumWarScoreBonus = 2f;
    [SerializeField, Min(0f)] private float behindLeaderScoreBonusPerPoint = 0.2f;
    [SerializeField, Min(0f)] private float drumAimNoiseRadius = 0.12f;

    private readonly List<PaperLegendCharacterNetworkHandler> _botCharacters =
        new List<PaperLegendCharacterNetworkHandler>();

    private readonly Dictionary<PaperLegendCharacterNetworkHandler, BotFlickPlan> _activePlans =
        new Dictionary<PaperLegendCharacterNetworkHandler, BotFlickPlan>();

    private Coroutine _botRoutine;
    private int _flickSequence;

    private enum BotFlickPlanType
    {
        Enemy,
        DrumObjective
    }

    private sealed class BotFlickPlan
    {
        public BotFlickPlanType PlanType;
        public PaperLegendCharacterNetworkHandler Target;
        public Vector3 TargetPosition;
        public float Force01;
        public float ReleaseTime;
        public float ChargeSeconds;
    }

    private struct EnemyTargetPlan
    {
        public PaperLegendCharacterNetworkHandler Target;
        public float Score;
    }

    private struct DrumTargetPlan
    {
        public Vector3 TargetPosition;
        public float Score;
    }

    public static PaperLegendBotFlickController Ensure()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject(nameof(PaperLegendBotFlickController));
        DontDestroyOnLoad(go);
        return go.AddComponent<PaperLegendBotFlickController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterBotCharacter(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null || _botCharacters.Contains(character))
            return;

        _botCharacters.Add(character);
        EnsureBotRoutine();
    }

    public void UnregisterBotCharacter(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null)
            return;

        _botCharacters.Remove(character);
        _activePlans.Remove(character);
    }

    private void EnsureBotRoutine()
    {
        if (_botRoutine != null)
            return;

        _botRoutine = StartCoroutine(BotRoutine());
    }

    private IEnumerator BotRoutine()
    {
        var wait = new WaitForSeconds(thinkIntervalSeconds);

        while (true)
        {
            TickBots();
            yield return wait;
        }
    }

    private void TickBots()
    {
        PruneNullBots();

        var host = PaperLegendMatchNetworkHost.Instance;
        if (host == null || host.IsMatchEnded)
            return;

        var botController = BotPlayerController.Instance;
        if (botController == null)
            return;

        for (int i = 0; i < _botCharacters.Count; i++)
        {
            var bot = _botCharacters[i];
            if (bot == null)
                continue;

            if (!bot.IsAlive || !botController.IsBotPlayer(bot.PlayerId))
            {
                _activePlans.Remove(bot);
                continue;
            }

            if (TickActivePlan(bot))
                continue;

            if (bot.HasPendingBotSkillFollowUp)
                continue;

            if (!bot.CanAcceptLocalFlick)
                continue;

            if (!TryStartBestPlan(bot, host))
                continue;
        }
    }

    private bool TickActivePlan(PaperLegendCharacterNetworkHandler bot)
    {
        if (bot == null || !_activePlans.TryGetValue(bot, out BotFlickPlan plan))
            return false;

        if (plan == null)
        {
            _activePlans.Remove(bot);
            return false;
        }

        if (plan.PlanType == BotFlickPlanType.Enemy)
        {
            if (plan.Target == null || !plan.Target.IsAlive || bot.IsSameFaction(plan.Target))
            {
                _activePlans.Remove(bot);
                return false;
            }
        }
        else if (!TryRefreshDrumPlan(bot, plan))
        {
            _activePlans.Remove(bot);
            return false;
        }

        if (Time.time < plan.ReleaseTime)
            return true;

        _activePlans.Remove(bot);

        if (!bot.IsAlive || !bot.CanAcceptLocalFlick)
            return false;

        bool flicked = plan.PlanType == BotFlickPlanType.DrumObjective
            ? TryFlickAtPosition(bot, plan.TargetPosition, plan.Force01, drumAimNoiseRadius)
            : TryFlickAtTarget(bot, plan.Target, plan.Force01);
       // Debug.Log($"[PaperLegends][BOT] Flick release player={bot.PlayerId}, target={plan.Target.PlayerId}, force={plan.Force01:0.00}, charge={plan.ChargeSeconds:0.00}s, applied={flicked}.");
        return flicked;
    }

    private bool TryStartBestPlan(PaperLegendCharacterNetworkHandler bot, PaperLegendMatchNetworkHost host)
    {
        EnemyTargetPlan enemyPlan = FindBestEnemyTarget(bot, host.GetRegisteredPlayers());
        bool hasEnemyPlan = enemyPlan.Target != null;
        bool hasDrumPlan = TryBuildDrumTargetPlan(bot, out DrumTargetPlan drumPlan);

        if (hasDrumPlan && (!hasEnemyPlan || drumPlan.Score < enemyPlan.Score))
        {
            StartDrumFlickPlan(bot, drumPlan);
            return true;
        }

        if (hasEnemyPlan)
        {
            StartEnemyFlickPlan(bot, enemyPlan.Target);
            return true;
        }

        return false;
    }

    private void StartFlickPlan(PaperLegendCharacterNetworkHandler bot, PaperLegendCharacterNetworkHandler target)
    {
        StartEnemyFlickPlan(bot, target);
    }

    private void StartEnemyFlickPlan(PaperLegendCharacterNetworkHandler bot, PaperLegendCharacterNetworkHandler target)
    {
        float force = CalculateFlickForce01(bot, ResolveTargetLandingPoint(bot, target));
        float chargeSeconds = CalculateChargeSeconds(force);

        _activePlans[bot] = new BotFlickPlan
        {
            PlanType = BotFlickPlanType.Enemy,
            Target = target,
            Force01 = force,
            ChargeSeconds = chargeSeconds,
            ReleaseTime = Time.time + chargeSeconds
        };

        //Debug.Log($"[PaperLegends][BOT] Flick charge started player={bot.PlayerId}, target={target.PlayerId}, force={force:0.00}, charge={chargeSeconds:0.00}s.");
    }

    private void StartDrumFlickPlan(PaperLegendCharacterNetworkHandler bot, DrumTargetPlan drumPlan)
    {
        float force = CalculateFlickForce01(bot, drumPlan.TargetPosition);
        float chargeSeconds = CalculateChargeSeconds(force);

        _activePlans[bot] = new BotFlickPlan
        {
            PlanType = BotFlickPlanType.DrumObjective,
            TargetPosition = drumPlan.TargetPosition,
            Force01 = force,
            ChargeSeconds = chargeSeconds,
            ReleaseTime = Time.time + chargeSeconds
        };

        Debug.Log($"[PaperLegends][BOT] player={bot.PlayerId} prioritizes TrongDong objective target={drumPlan.TargetPosition} score={drumPlan.Score:0.00} force={force:0.00}.");
    }

    private PaperLegendCharacterNetworkHandler FindBestTarget(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> candidates)
    {
        return FindBestEnemyTarget(bot, candidates).Target;
    }

    private EnemyTargetPlan FindBestEnemyTarget(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> candidates)
    {
        PaperLegendCharacterNetworkHandler best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate == null || candidate == bot || !candidate.IsAlive || bot.IsSameFaction(candidate))
                continue;

            float score = CalculateTargetScore(bot, candidate);
            if (score >= bestScore)
                continue;

            best = candidate;
            bestScore = score;
        }

        return new EnemyTargetPlan
        {
            Target = best,
            Score = bestScore
        };
    }

    private float CalculateTargetScore(PaperLegendCharacterNetworkHandler bot, PaperLegendCharacterNetworkHandler target)
    {
        float distance = Vector3.Distance(bot.transform.position, target.transform.position);
        int levelGap = target.Level - bot.Level;
        float score = distance;

        if (levelGap > dangerousLevelGap)
            score += (levelGap - dangerousLevelGap) * highLevelPenaltyPerLevel;
        else if (levelGap < 0)
            score -= Mathf.Abs(levelGap) * lowerLevelBonusPerLevel;

        if (!target.IsGrounded)
            score += airborneTargetPenalty;

        return Mathf.Max(0f, score);
    }

    private bool TryBuildDrumTargetPlan(PaperLegendCharacterNetworkHandler bot, out DrumTargetPlan plan)
    {
        plan = default;
        if (!enableDrumObjectiveSeeking || bot == null || !bot.IsGrounded)
            return false;

        if (!TryResolveActiveDrumObjective(out Vector3 drumPosition))
            return false;

        float distance = ResolveHorizontalDistance(bot.transform.position, drumPosition);
        if (distance <= drumCaptureHoldDistance)
            return false;

        if (drumSeekMaxDistance > 0f && distance > drumSeekMaxDistance)
            return false;

        float score = distance - drumObjectiveScoreBonus;
        var scoreManager = GameScoreManager.Instance;
        if (scoreManager != null)
        {
            if (scoreManager.IsDrumWarActive)
                score -= drumWarScoreBonus;

            if (scoreManager.TryGetScoreByPlayerId(bot.PlayerId, out PlayerScoreData botScore))
            {
                int leaderScore = ResolveLeaderScore(scoreManager);
                if (leaderScore > botScore.Score)
                    score -= (leaderScore - botScore.Score) * behindLeaderScoreBonusPerPoint;
            }
        }

        plan = new DrumTargetPlan
        {
            TargetPosition = drumPosition,
            Score = Mathf.Max(0f, score)
        };
        return true;
    }

    private bool TryRefreshDrumPlan(PaperLegendCharacterNetworkHandler bot, BotFlickPlan plan)
    {
        if (bot == null || plan == null)
            return false;

        if (!TryBuildDrumTargetPlan(bot, out DrumTargetPlan drumPlan))
            return false;

        plan.TargetPosition = drumPlan.TargetPosition;
        return true;
    }

    private bool TryResolveActiveDrumObjective(out Vector3 targetPosition)
    {
        targetPosition = default;

        var scoreManager = GameScoreManager.Instance;
        if (scoreManager == null || !scoreManager.DrumObjectiveIsActive)
            return false;

        var host = GameSessionNetWork_Host.Instance;
        if (host == null || host.DrumObjectiveObject == null)
            return false;

        targetPosition = host.DrumObjectiveObject.position;
        return true;
    }

    private static int ResolveLeaderScore(GameScoreManager scoreManager)
    {
        if (scoreManager == null || scoreManager.CurrentLeaderPlayerId <= 0)
            return 0;

        return scoreManager.TryGetScoreByPlayerId(scoreManager.CurrentLeaderPlayerId, out PlayerScoreData leaderData)
            ? leaderData.Score
            : 0;
    }

    private bool TryFlickAtTarget(PaperLegendCharacterNetworkHandler bot, PaperLegendCharacterNetworkHandler target, float forceOverride = -1f)
    {
        if (bot == null || target == null)
            return false;

        Vector3 targetPosition = ResolveTargetLandingPoint(bot, target);
        return TryFlickAtPosition(bot, targetPosition, forceOverride, 0f);
    }

    private bool TryFlickAtPosition(
        PaperLegendCharacterNetworkHandler bot,
        Vector3 targetPosition,
        float forceOverride = -1f,
        float noiseRadius = 0f)
    {
        if (bot == null)
            return false;

        if (noiseRadius > 0f)
        {
            Vector2 noise = Random.insideUnitCircle * noiseRadius;
            targetPosition += new Vector3(noise.x, 0f, noise.y);
        }

        Vector3 direction = targetPosition - bot.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = bot.transform.forward;

        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        Vector3 contactPoint = bot.transform.position - direction * contactOffset;
        float force = forceOverride >= 0f ? Mathf.Clamp01(forceOverride) : CalculateFlickForce01(bot, targetPosition);

        var input = new PaperLegendPlayerInputData
        {
            FlickRequested = true,
            FlickSequence = ++_flickSequence,
            ContactWorldPosition = contactPoint,
            ContactSurfaceNormal = -direction,
            AimWorldDirection = direction,
            Force01 = force
        };

        return bot.ServerTryApplyFlick(input);
    }

    private float CalculateChargeSeconds(float force01)
    {
        float minCharge = Mathf.Min(minChargeSeconds, maxChargeSeconds);
        float maxCharge = Mathf.Max(minChargeSeconds, maxChargeSeconds);
        float minForce = Mathf.Min(minForce01, maxForce01);
        float maxForce = Mathf.Max(minForce01, maxForce01);

        float forceT = Mathf.InverseLerp(minForce, Mathf.Max(minForce + 0.001f, maxForce), Mathf.Clamp01(force01));
        float jitter = chargeRandomJitterSeconds > 0f
            ? Random.Range(-chargeRandomJitterSeconds, chargeRandomJitterSeconds)
            : 0f;

        return Mathf.Clamp(Mathf.Lerp(minCharge, maxCharge, forceT) + jitter, minCharge, maxCharge);
    }

    private Vector3 ResolveTargetLandingPoint(PaperLegendCharacterNetworkHandler bot, PaperLegendCharacterNetworkHandler target)
    {
        Bounds bounds = target.GetWorldBounds();
        Vector3 targetPoint = bounds.center;
        targetPoint.y = target.transform.position.y;

        Vector3 direction = targetPoint - bot.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
            targetPoint += direction.normalized * targetOvershootDistance;

        if (aimNoiseRadius > 0f)
        {
            Vector2 noise = Random.insideUnitCircle * aimNoiseRadius;
            targetPoint += new Vector3(noise.x, 0f, noise.y);
        }

        return targetPoint;
    }

    private float CalculateFlickForce01(PaperLegendCharacterNetworkHandler bot, Vector3 targetPosition)
    {
        float distance = Vector3.Distance(bot.transform.position, targetPosition);
        float near = Mathf.Min(minPlannedLandingDistance, maxPlannedLandingDistance);
        float far = Mathf.Max(minPlannedLandingDistance, maxPlannedLandingDistance);
        float distance01 = Mathf.InverseLerp(near, far, distance);
        float force = Mathf.Lerp(Mathf.Min(minForce01, maxForce01), Mathf.Max(minForce01, maxForce01), distance01);
        return Mathf.Clamp01(force);
    }

    private static float ResolveHorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 delta = a - b;
        delta.y = 0f;
        return delta.magnitude;
    }

    private void PruneNullBots()
    {
        for (int i = _botCharacters.Count - 1; i >= 0; i--)
        {
            if (_botCharacters[i] == null)
            {
                _botCharacters.RemoveAt(i);
            }
        }
    }
}
#endif
