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

    private readonly List<PaperLegendCharacterNetworkHandler> _botCharacters =
        new List<PaperLegendCharacterNetworkHandler>();

    private readonly Dictionary<PaperLegendCharacterNetworkHandler, BotFlickPlan> _activePlans =
        new Dictionary<PaperLegendCharacterNetworkHandler, BotFlickPlan>();

    private Coroutine _botRoutine;
    private int _flickSequence;

    private sealed class BotFlickPlan
    {
        public PaperLegendCharacterNetworkHandler Target;
        public float Force01;
        public float ReleaseTime;
        public float ChargeSeconds;
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

            if (!bot.CanAcceptLocalFlick)
                continue;

            var target = FindBestTarget(bot, host.GetRegisteredPlayers());
            if (target == null)
                continue;

            StartFlickPlan(bot, target);
        }
    }

    private bool TickActivePlan(PaperLegendCharacterNetworkHandler bot)
    {
        if (bot == null || !_activePlans.TryGetValue(bot, out BotFlickPlan plan))
            return false;

        if (plan == null || plan.Target == null || !plan.Target.IsAlive || bot.IsSameFaction(plan.Target))
        {
            _activePlans.Remove(bot);
            return false;
        }

        if (Time.time < plan.ReleaseTime)
            return true;

        _activePlans.Remove(bot);

        if (!bot.IsAlive || !bot.CanAcceptLocalFlick)
            return false;

        bool flicked = TryFlickAtTarget(bot, plan.Target, plan.Force01);
       // Debug.Log($"[PaperLegends][BOT] Flick release player={bot.PlayerId}, target={plan.Target.PlayerId}, force={plan.Force01:0.00}, charge={plan.ChargeSeconds:0.00}s, applied={flicked}.");
        return flicked;
    }

    private void StartFlickPlan(PaperLegendCharacterNetworkHandler bot, PaperLegendCharacterNetworkHandler target)
    {
        float force = CalculateFlickForce01(bot, ResolveTargetLandingPoint(bot, target));
        float chargeSeconds = CalculateChargeSeconds(force);

        _activePlans[bot] = new BotFlickPlan
        {
            Target = target,
            Force01 = force,
            ChargeSeconds = chargeSeconds,
            ReleaseTime = Time.time + chargeSeconds
        };

        //Debug.Log($"[PaperLegends][BOT] Flick charge started player={bot.PlayerId}, target={target.PlayerId}, force={force:0.00}, charge={chargeSeconds:0.00}s.");
    }

    private PaperLegendCharacterNetworkHandler FindBestTarget(
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

        return best;
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

    private bool TryFlickAtTarget(PaperLegendCharacterNetworkHandler bot, PaperLegendCharacterNetworkHandler target, float forceOverride = -1f)
    {
        if (bot == null || target == null)
            return false;

        Vector3 targetPosition = ResolveTargetLandingPoint(bot, target);
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
