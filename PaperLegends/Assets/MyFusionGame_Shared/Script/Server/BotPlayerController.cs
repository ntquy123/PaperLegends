#if UNITY_SERVER
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Server-side controller quáº£n lÃ½ bot AI trong online match.
/// Bot cháº¡y trÃªn dedicated server, tá»± Ä‘á»™ng báº¯n khi Ä‘áº¿n lÆ°á»£t.
/// </summary>
public class BotPlayerController : MonoBehaviour
{
    private const int MaxSupportedBotLevel = 25;
    private const float BotTurnIndicatorLeadTime = 0.35f;
    private const int InterceptRingBallThreshold = 3;
    private const float InterceptEnemyScoreThreshold = 7f;
    private const int MaxRingBallForceScaleCount = 30;
    private const float MinRingBallForceMultiplier = 1f;
    private const float MaxRingBallForceMultiplier = 1.5f;
    private const float RingSparseNeighborRadius = 1.35f;
    private const float RingSparseAvoidanceRadius = 2.1f;
    private const int RingSparseCrowdLimit = 5;
    private const float RingSparseAimOffsetMin = 0.16f;
    private const float RingSparseAimOffsetMax = 0.48f;
    private float ringDirectShotMaxDistance = 30000f;
    // Khoáº£ng cÃ¡ch tá»‘i Ä‘a (tÃ­nh báº±ng mÃ©t) mÃ  bot sáº½ quyáº¿t Ä‘á»‹nh báº¯n tháº³ng vÃ o bi vÃ²ng (ring ball).
    // Náº¿u bi vÃ²ng á»Ÿ xa hÆ¡n giÃ¡ trá»‹ nÃ y, bot sáº½ khÃ´ng báº¯n tháº³ng mÃ  sáº½ di chuyá»ƒn láº¡i gáº§n trÆ°á»›c khi báº¯n Ä‘á»ƒ tÄƒng Ä‘á»™ chÃ­nh xÃ¡c.
    private float enemyPriorityOverRingCloserDistance = 50000f;
    // Khi bot Ä‘Ã£ cÃ³ Ä‘iá»ƒm vÃ  bi vÃ²ng Ä‘ang náº±m trong táº§m báº¯n tháº³ng, náº¿u Ä‘á»‘i thá»§ gáº§n hÆ¡n bi vÃ²ng Ã­t nháº¥t ngÆ°á»¡ng nÃ y thÃ¬ bot sáº½ Æ°u tiÃªn káº¿t liá»…u Ä‘á»‘i thá»§.
    private float enemyPriorityOverRingDistanceSlack = 10000f;
    // Cho phÃ©p bot váº«n Æ°u tiÃªn báº¯n Ä‘á»‘i thá»§ ngay cáº£ khi Ä‘á»‘i thá»§ xa hÆ¡n bi vÃ²ng má»™t chÃºt, miá»…n lÃ  Ä‘á»™ chÃªnh khÃ´ng Ä‘Ã¡ng ká»ƒ.
    private float ringApproachStandOffDistance = 5000f;
    // Khoáº£ng cÃ¡ch "dá»«ng láº¡i" khi bot tiáº¿p cáº­n bi vÃ²ng. Khi bot di chuyá»ƒn láº¡i gáº§n bi vÃ²ng (thay vÃ¬ báº¯n tháº³ng), 
    // nÃ³ sáº½ dá»«ng cÃ¡ch bi vÃ²ng má»™t khoáº£ng báº±ng giÃ¡ trá»‹ nÃ y, khÃ´ng tiáº¿n sÃ¡t hoÃ n toÃ n, 
    // Ä‘á»ƒ chuáº©n bá»‹ cho cÃº báº¯n tiáº¿p theo hoáº·c giá»¯ vá»‹ trÃ­ chiáº¿n thuáº­t.
    public static BotPlayerController Instance { get; private set; }

    /// <summary>
    /// Táº­p há»£p cÃ¡c playerId thuá»™c vá» bot (sá»­ dá»¥ng ID tháº­t tá»« database).
    /// </summary>
    private readonly HashSet<int> botPlayerIds = new HashSet<int>();
    private readonly Dictionary<int, int> botTurnExecutionVersions = new Dictionary<int, int>();

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

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Query â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public bool IsBotPlayer(int playerId)
    {
        return botPlayerIds.Contains(playerId);
    }

    public bool HasBots => botPlayerIds.Count > 0;

    public IReadOnlyCollection<int> BotIds => botPlayerIds;

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Registration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public void RegisterBot(int botId)
    {
        botPlayerIds.Add(botId);
        Debug.Log($"[BOT] Registered bot playerId={botId}.");
    }

    public void ClearBots()
    {
        botPlayerIds.Clear();
        botTurnExecutionVersions.Clear();
    }

    public void CancelBotTurn(int botPlayerId, string reason = null)
    {
        if (botPlayerId == 0)
            return;

        int nextVersion = 1;
        if (botTurnExecutionVersions.TryGetValue(botPlayerId, out int currentVersion))
            nextVersion = currentVersion + 1;

        botTurnExecutionVersions[botPlayerId] = nextVersion;

        if (!string.IsNullOrWhiteSpace(reason))
            Debug.Log($"ðŸ¤– [BOT] Há»§y lÆ°á»£t Ä‘ang cháº¡y cá»§a bot {botPlayerId}: {reason}");
    }

    private int BeginBotTurnExecution(int botPlayerId)
    {
        int nextVersion = 1;
        if (botTurnExecutionVersions.TryGetValue(botPlayerId, out int currentVersion))
            nextVersion = currentVersion + 1;

        botTurnExecutionVersions[botPlayerId] = nextVersion;
        return nextVersion;
    }

    private bool IsBotTurnExecutionCurrent(int botPlayerId, int executionVersion)
    {
        return botTurnExecutionVersions.TryGetValue(botPlayerId, out int currentVersion) &&
               currentVersion == executionVersion;
    }

    private bool IsBotStillOnItsTurn(int botPlayerId)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return false;

        var currentEntry = manager.GetOrderedPlayerInfos()
            .FirstOrDefault(x => x.turnOrder == manager.currentPlayerIndex);

        return currentEntry.playerId == botPlayerId;
    }

    private bool ShouldAbortBotTurn(int botPlayerId, int executionVersion, PlayerNetworkHandler handler = null)
    {
        if (!IsBotTurnExecutionCurrent(botPlayerId, executionVersion))
            return true;

        if (!IsBotStillOnItsTurn(botPlayerId))
            return true;

        if (handler == null)
            return false;

        var model = handler.PlayerModel;
        if (model.isDestroy || model.statusPlayer == StatusPlayer.Destroy || model.statusPlayer == StatusPlayer.WaitingDestroy)
            return true;

        return handler.CurrentAnimState == CharacterAnimState.Slipping;
    }

    private bool TryHoldActiveBallOnFinger(int botPlayerId, PlayerNetworkHandler handler, NetworkObjectManager manager = null)
    {
        if (handler == null)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng thá»ƒ láº¥y bi lÃªn tay vÃ¬ thiáº¿u handler cho bot {botPlayerId}");
            return false;
        }

        manager ??= NetworkObjectManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng thá»ƒ láº¥y bi lÃªn tay vÃ¬ thiáº¿u NetworkObjectManager cho bot {botPlayerId}");
            return false;
        }

        var ballObj = manager.GetActiveBallObject(botPlayerId);
        if (ballObj == null)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng tÃ¬m tháº¥y bi active Ä‘á»ƒ láº¥y lÃªn tay cho bot {botPlayerId}");
            return false;
        }

        var ballCtrl = ballObj.GetComponent<BallServerController>();
        if (ballCtrl == null)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng tÃ¬m tháº¥y BallServerController Ä‘á»ƒ láº¥y bi lÃªn tay cho bot {botPlayerId}");
            return false;
        }

        // Bot khÃ´ng cÃ³ client gá»­i FingerPos nÃªn server pháº£i tá»± gÃ¡n vá»‹ trÃ­ tay trÆ°á»›c khi snap bi.
        handler.FingerPos = handler.transform.position;
        ballCtrl.hasBeenShoot = 0;
        ballCtrl.IsHolding = 1;
        ballCtrl.SnapToOwnerFinger();

        Debug.Log($"ðŸ¤– [BOT] pid={botPlayerId} Ä‘Ã£ láº¥y bi lÃªn tay táº¡i {ballCtrl.HeldPosition}");
        return true;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Create Bot Data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Táº¡o PlayerInfoStruct cho bot tá»« BotPlayerData (láº¥y tá»« API).
    /// </summary>
    public static PlayerInfoStruct CreateBotPlayerInfo(BotPlayerData botData)
    {
        return new PlayerInfoStruct
        {
            playerId = botData.id,
            level = botData.Level,
            fullname = botData.PlayerName,
            avatarUrl = botData.AvatarUrl ?? "",
            powerForce = Random.Range(0.8f, 1.2f),
            spinForce = Random.Range(0.3f, 0.8f),
            exactRatio = Random.Range(0.5f, 0.9f),
            avatar = Random.Range(0, 5),
            ball = ItemCode.CuliMatTroi,
            playerbody = PlayerBodyType.ChuBe,
            RingBall = botData.RingBall,
            providerType = "BOT",
            idAccount = botData.IdAccount ?? "",
            score = 0,
            scoreExam = 0,
            combo = 0,
            statusPlayer = StatusPlayer.Normal,
            distance = 0,
            isDestroy = false,
            isHolding = false,
            turnOrder = -1,
            isCatAnTienActive = 0
        };
    }

    /// <summary>
    /// Táº¡o default ball physics cho bot khi API khÃ´ng tráº£ vá» dá»¯ liá»‡u equip.
    /// </summary>
    public static PlayerBallPhysics CreateDefaultBotBallPhysics(int botId)
    {
        return new PlayerBallPhysics
        {
            playerId = botId,
            physics = new System.Collections.Generic.List<BallPhysicsItem>
            {
                new BallPhysicsItem
                {
                    name = "BotBall",
                    itemId = 1,
                    seqItem = 0,
                    Mass = 1f,
                    GravityScale = 1f,
                    Drag = 0.5f,
                    Bounciness = 0.6f,
                    Elasticity = 0.5f,
                    ImpactResistance = 0.5f,
                    level = 1,
                    isCateye = false,
                    damage = 0f
                }
            }
        };
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Shot Calculation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private float NormalizeBotLevel(int level)
    {
        return Mathf.InverseLerp(1f, MaxSupportedBotLevel, Mathf.Clamp(level, 1, MaxSupportedBotLevel));
    }

    private BotSkillProfile BuildSkillProfile(int level)
    {
        float level01 = NormalizeBotLevel(level);
        return new BotSkillProfile
        {
            level = Mathf.Clamp(level, 1, MaxSupportedBotLevel),
            level01 = level01,
            accuracyRadius = Mathf.Lerp(1.2f, 0.08f, level01),
            directionErrorDegrees = Mathf.Lerp(18f, 1.25f, level01),
            forceVariance = Mathf.Lerp(0.28f, 0.04f, level01),
            spinNoise = Mathf.Lerp(0.18f, 0.02f, level01),
            enemyAggression = Mathf.Lerp(0.2f, 1.25f, level01),
            tacticalBias = Mathf.Lerp(0.15f, 1.4f, level01),
            finishingBias = Mathf.Lerp(0.25f, 1.5f, level01),
            ringPriority = Mathf.Lerp(1.15f, 1.35f, level01),
            positionBias = Mathf.Lerp(0.25f, 1.2f, level01),
            anticipationStrength = Mathf.Lerp(0.15f, 1f, level01),
            focusTargetWeight = Mathf.Lerp(0.2f, 1f, level01),
            aimStepsMin = level >= 18 ? 3 : 2,
            aimStepsMax = level >= 18 ? 6 : (level >= 9 ? 5 : 4),
            aimSweepAngle = Mathf.Lerp(55f, 14f, level01),
            aimDurationMin = Mathf.Lerp(0.14f, 0.22f, level01),
            aimDurationMax = Mathf.Lerp(0.75f, 0.38f, level01),
            holdDelayMin = Mathf.Lerp(0.06f, 0.16f, level01),
            holdDelayMax = Mathf.Lerp(0.45f, 0.28f, level01),
            settleShotDelayMin = Mathf.Lerp(0.18f, 0.42f, level01),
            settleShotDelayMax = Mathf.Lerp(0.85f, 1.05f, level01),
            pitchNoiseMin = Mathf.Lerp(0.3f, 0.04f, level01),
            pitchNoiseMax = Mathf.Lerp(1.6f, 0.18f, level01),
            verticalFocusBias = Mathf.Lerp(0.22f, 0.95f, level01),
            reacquireChance = Mathf.Lerp(0.45f, 0.12f, level01),
            overshootChance = Mathf.Lerp(0.35f, 0.08f, level01),
            idleJitterDistance = Mathf.Lerp(0.42f, 0.08f, level01)
        };
    }

    private BotTurnContext BuildTurnContext(int botPlayerId, Vector3 ballPos)
    {
        var manager = NetworkObjectManager.Instance;
        var host = GameSessionNetWork_Host.Instance;
        var context = new BotTurnContext
        {
            botPlayerId = botPlayerId,
            botBallPos = ballPos,
            centerPoint = GetFallbackPlayAreaPoint(ballPos, host),
            nearestRingPos = ballPos + Vector3.forward,
            nearestRingDistance = float.MaxValue,
            nearestEnemyDistance = float.MaxValue,
            ownScore = 0f,
            hasOwnScore = false,
            ringBallCount = 0,
            enemies = new List<BotEnemySnapshot>()
        };

        if (manager == null)
            return context;

        Vector3 ringSum = Vector3.zero;
        int activeRingCount = 0;

        foreach (var obj in manager.ringBalls)
        {
            if (obj == null)
                continue;

            var ctrl = obj.GetComponent<BallServerController>();
            if (ctrl == null || ctrl.IsActive == 0)
                continue;

            Vector3 ringPos = obj.transform.position;
            float dist = Vector3.Distance(ballPos, ringPos);
            if (dist < context.nearestRingDistance)
            {
                context.nearestRingDistance = dist;
                context.nearestRingPos = ringPos;
            }

            ringSum += ringPos;
            activeRingCount++;
        }

        if (activeRingCount > 0)
            context.centerPoint = ringSum / activeRingCount;

        // Äá»“ng bá»™ vá»›i cÃ¡ch UI hiá»ƒn thá»‹ (ShowInforList_Online/countCurrentRingBall):
        // Æ°u tiÃªn Ä‘áº¿m theo NetworkId cÃ²n tá»“n táº¡i, khÃ´ng phá»¥ thuá»™c activeInHierarchy.
        context.ringBallCount = GetCurrentRingBallCount(manager);

        var playerScores = new Dictionary<int, float>();
        for (int i = 0; i < manager.players.Length; i++)
        {
            var info = manager.players.Get(i);
            playerScores[info.playerId] = info.score;
            if (info.playerId != botPlayerId)
                continue;

            context.ownScore = info.score;
            context.hasOwnScore = true;
            break;
        }

        foreach (var kvp in manager.PlayerBalls)
        {
            if (kvp.Key == botPlayerId)
                continue;

            foreach (var ball in kvp.Value)
            {
                if (ball == null || ball.gameObject == null || !ball.gameObject.activeInHierarchy)
                    continue;

                var ctrl = ball.GetComponent<BallServerController>();
                if (ctrl == null || ctrl.IsActive == 0)
                    continue;

                Vector3 enemyPos = ball.transform.position;
                float enemyDist = Vector3.Distance(ballPos, enemyPos);
                if (enemyDist < context.nearestEnemyDistance)
                {
                    context.nearestEnemyDistance = enemyDist;
                    context.nearestEnemyPos = enemyPos;
                    context.hasNearestEnemy = true;
                }

                BotEnemySnapshot enemy = new BotEnemySnapshot
                {
                    playerId = kvp.Key,
                    score = playerScores.TryGetValue(kvp.Key, out float enemyScore) ? enemyScore : 0f,
                    position = enemyPos,
                    distanceToBot = enemyDist,
                    likelyTarget = GuessEnemyPreferredTarget(enemyPos, context.centerPoint, context.nearestRingPos, ballPos),
                    threatToBot = EstimateEnemyThreat(enemyPos, ballPos, context.centerPoint),
                    pressureOnRing = EstimateEnemyThreat(enemyPos, context.nearestRingPos, context.centerPoint)
                };
                context.enemies.Add(enemy);
            }
        }

        return context;
    }

    private bool ShouldConsiderIntercept(BotTurnContext context)
    {
        if (context.ringBallCount >= InterceptRingBallThreshold)
            return false;

        for (int i = 0; i < context.enemies.Count; i++)
        {
            if (context.enemies[i].score > InterceptEnemyScoreThreshold)
                return true;
        }

        return false;
    }

    private Vector3 GetFallbackPlayAreaPoint(Vector3 ballPos, GameSessionNetWork_Host host)
    {
        if (host != null && host.playArea != null)
        {
            Vector3 center = host.playArea.bounds.center;
            center.y = ballPos.y;
            return center;
        }

        if (host != null && host.StartPointMain != null)
        {
            Vector3 forwardTarget = host.StartPointMain.position + (Vector3.forward * 3.5f);
            forwardTarget.y = ballPos.y;
            return forwardTarget;
        }

        return ballPos + new Vector3(0.75f, 0f, 1.5f);
    }

    private Vector3 ResolveDegenerateRegularTarget(Vector3 ballPos, Vector3 targetPos, BotAimPlan plan, BotTurnContext context)
    {
        Vector3 flatDelta = targetPos - ballPos;
        flatDelta.y = 0f;
        if (flatDelta.sqrMagnitude >= 0.0001f)
            return targetPos;

        var host = GameSessionNetWork_Host.Instance;
        Vector3 fallbackTarget = context.centerPoint;
        Vector3 fallbackDelta = fallbackTarget - ballPos;
        fallbackDelta.y = 0f;

        if (fallbackDelta.sqrMagnitude < 0.0001f)
        {
            fallbackTarget = GetFallbackPlayAreaPoint(ballPos, host);
            fallbackDelta = fallbackTarget - ballPos;
            fallbackDelta.y = 0f;
        }

        if (fallbackDelta.sqrMagnitude < 0.0001f)
        {
            Vector3 lateralDir = host != null && host.StartPointMain != null
                ? Vector3.Cross(Vector3.up, host.StartPointMain.forward)
                : Vector3.right;
            if (lateralDir.sqrMagnitude < 0.0001f)
                lateralDir = Vector3.right;

            lateralDir.Normalize();
            fallbackTarget = ballPos + lateralDir * 1.2f + Vector3.forward * 1.8f;
            fallbackTarget.y = ballPos.y;
        }

        Debug.LogWarning($"ðŸ¤– [BOT] Äiá»u chá»‰nh target quÃ¡ gáº§n cho plan={plan.planType}: ball={ballPos} target={targetPos} fallback={fallbackTarget}");
        return fallbackTarget;
    }

    private Vector3 GuessEnemyPreferredTarget(Vector3 enemyPos, Vector3 centerPoint, Vector3 nearestRingPos, Vector3 botBallPos)
    {
        float ringDist = Vector3.Distance(enemyPos, nearestRingPos);
        float botDist = Vector3.Distance(enemyPos, botBallPos);

        if (botDist < ringDist * 0.85f)
            return botBallPos;

        return Vector3.Lerp(nearestRingPos, centerPoint, 0.35f);
    }

    private float EstimateEnemyThreat(Vector3 enemyPos, Vector3 targetPos, Vector3 centerPoint)
    {
        float distToTarget = Vector3.Distance(enemyPos, targetPos);
        float distToCenter = Vector3.Distance(enemyPos, centerPoint);
        return Mathf.Clamp01(1f - distToTarget / 8f) * 0.65f + Mathf.Clamp01(1f - distToCenter / 12f) * 0.35f;
    }

    private BotAimPlan ChooseSmartTarget(int botPlayerId, Vector3 ballPos, BotSkillProfile profile)
    {
        BotTurnContext context = BuildTurnContext(botPlayerId, ballPos);

        if (context.ownScore <= 0f)
        {
            BotAimPlan openingPlan = BuildOpeningScorePlan(context, profile);
            Debug.Log($"ðŸ¤– [BOT] pid={botPlayerId} chÆ°a cÃ³ Ä‘iá»ƒm â†’ Ã©p Æ°u tiÃªn ghi Ä‘iá»ƒm trÆ°á»›c báº±ng chiáº¿n thuáº­t={openingPlan.planType} target={openingPlan.targetPosition}");
            return openingPlan;
        }

        BotAimPlan ringPlan = BuildRingPlan(context, profile);
        BotAimPlan enemyPlan = context.hasNearestEnemy
            ? BuildEnemyPlan(context, profile)
            : new BotAimPlan();

        if (ShouldPrioritizeEnemyOverRing(context, ringPlan, enemyPlan))
        {
            Debug.Log($"ðŸ¤– [BOT] pid={botPlayerId} Æ°u tiÃªn plan={enemyPlan.planType} thay vÃ¬ ring vÃ¬ enemyDist={context.nearestEnemyDistance:F2}, ringDist={context.nearestRingDistance:F2}, slack={enemyPriorityOverRingDistanceSlack:F2}");
            return enemyPlan;
        }

        var candidates = new List<BotAimPlan>(4)
        {
            ringPlan
        };

        if (context.hasNearestEnemy)
            candidates.Add(enemyPlan);

        if (context.enemies.Count > 0 && ShouldConsiderIntercept(context))
            candidates.Add(BuildInterceptPlan(context, profile));

        candidates.Add(BuildPositioningPlan(context, profile));

        BotAimPlan bestPlan = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            if (candidates[i].score > bestPlan.score)
                bestPlan = candidates[i];
        }

        Debug.Log($"ðŸ¤– [BOT] pid={botPlayerId} chá»n chiáº¿n thuáº­t={bestPlan.planType} score={bestPlan.score:F2} target={bestPlan.targetPosition}");
        return bestPlan;
    }

    private RingTargetCandidate SelectSparseRingTarget(BotTurnContext context, BotSkillProfile profile)
    {
        var manager = NetworkObjectManager.Instance;
        RingTargetCandidate best = new RingTargetCandidate { isValid = false, score = float.MinValue };
        if (manager == null || manager.ringBalls == null)
            return best;

        int activeRingCount = 0;
        float ringDensity01 = GetRingBallDensity01(context.ringBallCount);

        foreach (var obj in manager.ringBalls)
        {
            if (obj == null)
                continue;

            var ctrl = obj.GetComponent<BallServerController>();
            if (ctrl == null || ctrl.IsActive == 0)
                continue;

            activeRingCount++;
            Vector3 ringPos = obj.transform.position;
            EvaluateRingShotLane(context.botBallPos, ringPos, manager, out int interferenceCount, out float laneClarity, out float nearestSafetyMargin);

            int localCrowd = CountNearbyRingBalls(ringPos, manager, RingSparseNeighborRadius, obj);
            float sparseScore = Mathf.Clamp01(1f - localCrowd / (float)RingSparseCrowdLimit);
            float centerPenalty = EstimateCenterCrowdPenalty(ringPos, ringDensity01);

            float enemyPressure = 0f;
            for (int i = 0; i < context.enemies.Count; i++)
            {
                float enemyDist = Vector3.Distance(context.enemies[i].position, ringPos);
                enemyPressure = Mathf.Max(enemyPressure, Mathf.Clamp01(1f - enemyDist / 2.8f));
            }

            float distanceToRing = Vector3.Distance(context.botBallPos, ringPos);
            float proximity = Mathf.Clamp01(1f - distanceToRing / 9f);
            float candidateScore = 0.95f * profile.ringPriority
                + laneClarity * Mathf.Lerp(1.2f, 2.65f, profile.level01)
                + sparseScore * Mathf.Lerp(1.35f, 3.25f, Mathf.Max(profile.level01, ringDensity01))
                + proximity * Mathf.Lerp(0.65f, 1.18f, profile.level01)
                - localCrowd * Mathf.Lerp(0.25f, 0.78f, Mathf.Max(profile.level01, ringDensity01))
                - interferenceCount * Mathf.Lerp(0.28f, 1.05f, profile.level01)
                - centerPenalty * Mathf.Lerp(0.25f, 1.15f, ringDensity01)
                - enemyPressure * 0.5f;

            if (candidateScore <= best.score)
                continue;

            best = new RingTargetCandidate
            {
                isValid = true,
                ringPosition = ringPos,
                aimPosition = BuildSparseRingAimPosition(context, ringPos, manager, profile, sparseScore),
                laneClarity = laneClarity,
                interferenceCount = interferenceCount,
                safetyMargin = nearestSafetyMargin,
                distanceToBot = distanceToRing,
                localCrowd = localCrowd,
                sparseScore = sparseScore,
                activeRingCount = activeRingCount,
                score = candidateScore
            };
        }

        if (best.isValid)
            best.activeRingCount = activeRingCount;

        return best;
    }

    private int CountNearbyRingBalls(Vector3 origin, NetworkObjectManager manager, float radius, UnityEngine.Object ignoredObject)
    {
        if (manager == null || manager.ringBalls == null || radius <= 0f)
            return 0;

        int count = 0;
        float sqrRadius = radius * radius;
        foreach (var obj in manager.ringBalls)
        {
            if (obj == null || obj == ignoredObject)
                continue;

            var ctrl = obj.GetComponent<BallServerController>();
            if (ctrl == null || ctrl.IsActive == 0)
                continue;

            Vector3 delta = obj.transform.position - origin;
            delta.y = 0f;
            if (delta.sqrMagnitude <= sqrRadius)
                count++;
        }

        return count;
    }

    private Vector3 BuildSparseRingAimPosition(BotTurnContext context, Vector3 ringPos, NetworkObjectManager manager, BotSkillProfile profile, float sparseScore)
    {
        Vector3 escapeDirection = ringPos - GetPlayAreaCenterAtHeight(ringPos.y);
        escapeDirection.y = 0f;

        if (manager != null && manager.ringBalls != null)
        {
            foreach (var obj in manager.ringBalls)
            {
                if (obj == null)
                    continue;

                var ctrl = obj.GetComponent<BallServerController>();
                if (ctrl == null || ctrl.IsActive == 0)
                    continue;

                Vector3 otherPos = obj.transform.position;
                Vector3 away = ringPos - otherPos;
                away.y = 0f;
                float dist = away.magnitude;
                if (dist <= 0.001f || dist > RingSparseAvoidanceRadius)
                    continue;

                escapeDirection += away.normalized * (1f - dist / RingSparseAvoidanceRadius);
            }
        }

        if (escapeDirection.sqrMagnitude < 0.0001f)
        {
            escapeDirection = ringPos - context.botBallPos;
            escapeDirection.y = 0f;
        }

        if (escapeDirection.sqrMagnitude < 0.0001f)
            escapeDirection = Vector3.forward;

        escapeDirection.Normalize();
        float crowdedOffset = 1f - sparseScore;
        float aimOffset = Mathf.Lerp(RingSparseAimOffsetMin, RingSparseAimOffsetMax, Mathf.Max(profile.level01, crowdedOffset));
        Vector3 aimPosition = ringPos + escapeDirection * aimOffset;
        return ClampPointInsidePlayArea(aimPosition, ringPos.y, 0.04f);
    }

    private float EstimateCenterCrowdPenalty(Vector3 ringPos, float ringDensity01)
    {
        var host = GameSessionNetWork_Host.Instance;
        if (host == null || host.playArea == null)
            return 0f;

        Bounds bounds = host.playArea.bounds;
        float safeRadius = Mathf.Max(0.01f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.5f);
        Vector3 center = bounds.center;
        float distanceToCenter = Vector2.Distance(new Vector2(ringPos.x, ringPos.z), new Vector2(center.x, center.z));
        float center01 = Mathf.Clamp01(1f - distanceToCenter / Mathf.Max(0.01f, safeRadius * 0.55f));
        return center01 * Mathf.Clamp01(ringDensity01 * 1.4f);
    }

    private Vector3 GetPlayAreaCenterAtHeight(float y)
    {
        var host = GameSessionNetWork_Host.Instance;
        if (host != null && host.playArea != null)
        {
            Vector3 center = host.playArea.bounds.center;
            center.y = y;
            return center;
        }

        return new Vector3(0f, y, 0f);
    }

    private Vector3 ClampPointInsidePlayArea(Vector3 position, float fallbackY, float margin)
    {
        var host = GameSessionNetWork_Host.Instance;
        if (host == null || host.playArea == null)
        {
            position.y = fallbackY;
            return position;
        }

        var playArea = host.playArea;
        Vector3 local = playArea.transform.InverseTransformPoint(position) - playArea.center;
        Vector3 halfSize = playArea.size * 0.5f;
        float safeX = Mathf.Min(Mathf.Max(0f, margin), Mathf.Max(0f, halfSize.x - 0.001f));
        float safeZ = Mathf.Min(Mathf.Max(0f, margin), Mathf.Max(0f, halfSize.z - 0.001f));
        local.x = Mathf.Clamp(local.x, -halfSize.x + safeX, halfSize.x - safeX);
        local.z = Mathf.Clamp(local.z, -halfSize.z + safeZ, halfSize.z - safeZ);
        Vector3 clamped = playArea.transform.TransformPoint(local + playArea.center);
        clamped.y = fallbackY;
        return clamped;
    }

    private BotAimPlan BuildOpeningScorePlan(BotTurnContext context, BotSkillProfile profile)
    {
        RingTargetCandidate sparseTarget = SelectSparseRingTarget(context, profile);
        bool hasRingTarget = sparseTarget.isValid || context.nearestRingDistance < float.MaxValue;
        Vector3 focusTarget = sparseTarget.isValid ? sparseTarget.ringPosition : context.nearestRingPos;
        float focusDistance = sparseTarget.isValid ? sparseTarget.distanceToBot : context.nearestRingDistance;
        bool shouldApproachRing = hasRingTarget && focusDistance > ringDirectShotMaxDistance;
        Vector3 target = context.centerPoint;
        if (hasRingTarget)
        {
            target = shouldApproachRing
                ? GetRingApproachTarget(context.botBallPos, focusTarget)
                : (sparseTarget.isValid ? sparseTarget.aimPosition : focusTarget);
        }

        float ringAccess = hasRingTarget
            ? Mathf.Clamp01(1f - focusDistance / 10f)
            : 0.25f;
        float sparseBonus = sparseTarget.isValid ? sparseTarget.sparseScore * Mathf.Lerp(0.8f, 1.7f, profile.level01) : 0f;
        float score = 5f + profile.ringPriority * 2f + ringAccess * 1.5f + sparseBonus;

        return new BotAimPlan
        {
            planType = BotPlanType.OpeningScore,
            targetPosition = target,
            focusPoint = hasRingTarget ? focusTarget : target,
            score = score,
            desiredForceScale = sparseTarget.isValid && !shouldApproachRing
                ? EstimateRingForceScale(context, profile, sparseTarget.ringPosition, sparseTarget.laneClarity, sparseTarget.interferenceCount, sparseTarget.safetyMargin)
                : (shouldApproachRing
                    ? Mathf.Lerp(0.78f, 0.92f, profile.level01)
                    : Mathf.Lerp(1.02f, 1.16f, profile.level01)),
            description = hasRingTarget
                ? (shouldApproachRing
                    ? $"Bi trong vÃ²ng quÃ¡ xa nÃªn Ä‘áº­u láº¡i gáº§n vÃ²ng trÆ°á»›c (ngÆ°á»¡ng báº¯n tháº³ng {ringDirectShotMaxDistance:F1}m)"
                    : (sparseTarget.isValid
                        ? $"ChÆ°a cÃ³ Ä‘iá»ƒm, chá»n vÃ¹ng thÆ°a Ä‘á»ƒ má»Ÿ Ä‘iá»ƒm: crowd={sparseTarget.localCrowd}, sparse={sparseTarget.sparseScore:F2}, clear={sparseTarget.laneClarity:F2}"
                        : "ChÆ°a cÃ³ Ä‘iá»ƒm nÃªn khÃ³a má»¥c tiÃªu bi trong vÃ²ng Ä‘á»ƒ má»Ÿ Ä‘iá»ƒm"))
                : "ChÆ°a cÃ³ Ä‘iá»ƒm vÃ  chÆ°a tháº¥y bi Ä‘áº­u gáº§n, báº¯n Ã©p vÃ o trung tÃ¢m PlayArea Ä‘á»ƒ táº¡o cÆ¡ há»™i láº¥y Ä‘iá»ƒm"
        };
    }

    private bool ShouldPrioritizeEnemyOverRing(BotTurnContext context, BotAimPlan ringPlan, BotAimPlan enemyPlan)
    {
        if (context.ownScore <= 0f || !context.hasNearestEnemy)
            return false;

        if (context.nearestRingDistance == float.MaxValue)
            return false;

        if (context.nearestRingDistance > ringDirectShotMaxDistance)
            return false;

        if (enemyPlan.planType != BotPlanType.Enemy || enemyPlan.score <= 0f)
            return false;

        float enemyDist = context.nearestEnemyDistance;
        float ringDist = context.nearestRingDistance;
        float distanceDelta = enemyDist - ringDist;
        bool enemyClearlyCloser = enemyDist <= ringDist - enemyPriorityOverRingCloserDistance;
        bool enemyCompetitiveRange = distanceDelta <= enemyPriorityOverRingDistanceSlack;

        if (!enemyClearlyCloser && !enemyCompetitiveRange)
            return false;

        float scoreBuffer = enemyClearlyCloser ? -0.25f : 0.2f;
        return enemyPlan.score >= ringPlan.score - scoreBuffer;
    }

    private BotAimPlan BuildRingPlan(BotTurnContext context, BotSkillProfile profile)
    {
        if (context.nearestRingDistance == float.MaxValue)
        {
            return new BotAimPlan
            {
                planType = BotPlanType.Position,
                targetPosition = context.centerPoint,
                focusPoint = context.centerPoint,
                score = 0.25f + profile.positionBias * 0.25f,
                desiredForceScale = 0.86f,
                description = "Khong con bi trong vong, chuyen sang giu vi tri."
            };
        }

        float dist = context.nearestRingDistance;

        bool shouldApproachRing = context.nearestRingDistance < float.MaxValue &&
                                  context.nearestRingDistance > ringDirectShotMaxDistance;
        if (shouldApproachRing)
        {
            Vector3 approachTarget = GetRingApproachTarget(context.botBallPos, context.nearestRingPos);
            return new BotAimPlan
            {
                planType = BotPlanType.Ring,
                targetPosition = approachTarget,
                focusPoint = context.nearestRingPos,
                score = 1.25f * profile.ringPriority + Mathf.Clamp01(1f - dist / 8f) * 1.75f,
                desiredForceScale = Mathf.Lerp(0.8f, 0.94f, profile.level01),
                description = $"Bi trong vÃ²ng Ä‘ang xa, Æ°u tiÃªn Ä‘áº­u gáº§n vÃ²ng trÆ°á»›c (ngÆ°á»¡ng báº¯n tháº³ng {ringDirectShotMaxDistance:F1}m)"
            };
        }

        RingTargetCandidate sparseTarget = SelectSparseRingTarget(context, profile);
        Vector3 selectedRingPos = sparseTarget.isValid ? sparseTarget.ringPosition : context.nearestRingPos;
        Vector3 selectedAimPos = sparseTarget.isValid ? sparseTarget.aimPosition : context.nearestRingPos;
        float selectedLaneClarity = sparseTarget.isValid ? sparseTarget.laneClarity : 1f;
        int selectedInterference = sparseTarget.isValid ? sparseTarget.interferenceCount : 0;
        float selectedSafetyMargin = sparseTarget.isValid ? sparseTarget.safetyMargin : 1f;
        float selectedDistance = sparseTarget.isValid ? sparseTarget.distanceToBot : context.nearestRingDistance;
        int activeRingCount = sparseTarget.activeRingCount;
        float bestScore = sparseTarget.isValid ? sparseTarget.score : float.MinValue;

        float fallbackProximity = Mathf.Clamp01(1f - dist / 8f);
        if (bestScore == float.MinValue)
        {
            bestScore = 1.25f * profile.ringPriority + fallbackProximity * 1.75f;
            selectedRingPos = context.nearestRingPos;
            selectedAimPos = context.nearestRingPos;
        }

        if (context.ownScore <= 0f)
            bestScore += 0.45f;

        if (selectedLaneClarity < 0.28f && activeRingCount > 1)
            bestScore -= Mathf.Lerp(0.45f, 1.25f, profile.level01);

        float desiredForceScale = EstimateRingForceScale(context, profile, selectedRingPos, selectedLaneClarity, selectedInterference, selectedSafetyMargin);
        string smartDesc = $"Chon bi trong vong de trung: dist={selectedDistance:F2}, clear={selectedLaneClarity:F2}, overlap={selectedInterference}";
        return new BotAimPlan
        {
            planType = BotPlanType.Ring,
            targetPosition = selectedAimPos,
            focusPoint = selectedRingPos,
            score = bestScore,
            desiredForceScale = desiredForceScale,
            description = smartDesc
        };
    }

    private BotAimPlan BuildEnemyPlan(BotTurnContext context, BotSkillProfile profile)
    {
        float finishReadiness = context.ownScore > 0f ? 1f : 0.2f;
        BotEnemySnapshot bestEnemy = context.enemies.Count > 0
            ? context.enemies[0]
            : new BotEnemySnapshot
            {
                playerId = 0,
                position = context.nearestEnemyPos,
                distanceToBot = context.nearestEnemyDistance
            };
        float bestLaneClarity = 1f;
        float bestScore = float.MinValue;

        for (int i = 0; i < context.enemies.Count; i++)
        {
            var enemy = context.enemies[i];
            float dist = enemy.distanceToBot;
            float proximity = Mathf.Clamp01(1f - dist / 7f);
            float laneClarity = EstimateEnemyShotLaneClarity(context.botBallPos, enemy.position, enemy.playerId, context.botPlayerId);
            float targetLane = Mathf.Clamp01(1f - Vector3.Distance(enemy.position, context.centerPoint) / 12f);
            float scoreLeadPressure = Mathf.Clamp01(enemy.score / 10f) * Mathf.Lerp(0.25f, 0.9f, profile.level01);
            float score = proximity * (1.25f + profile.enemyAggression)
                + laneClarity * Mathf.Lerp(0.85f, 1.85f, profile.level01)
                + finishReadiness * profile.finishingBias
                + targetLane * 0.4f
                + scoreLeadPressure;

            if (context.ownScore <= 0f)
                score *= 0.58f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestEnemy = enemy;
            bestLaneClarity = laneClarity;
        }

        return new BotAimPlan
        {
            planType = BotPlanType.Enemy,
            targetPosition = bestEnemy.position,
            focusPoint = bestEnemy.position,
            score = bestScore,
            desiredForceScale = Mathf.Lerp(0.9f, 1.14f, Mathf.Clamp01(1f - bestEnemy.distanceToBot / 7f)) * Mathf.Lerp(0.92f, 1.05f, bestLaneClarity),
            description = context.ownScore > 0f
                ? $"Co diem, chon doi thu co lane tot: enemy={bestEnemy.playerId}, clear={bestLaneClarity:F2}"
                : $"Theo doi doi thu nhung uu tien lane an toan: enemy={bestEnemy.playerId}, clear={bestLaneClarity:F2}"
        };
    }

    private BotAimPlan BuildInterceptPlan(BotTurnContext context, BotSkillProfile profile)
    {
        BotAimPlan bestPlan = new BotAimPlan
        {
            planType = BotPlanType.Intercept,
            targetPosition = Vector3.Lerp(context.botBallPos, context.centerPoint, 0.5f),
            focusPoint = context.centerPoint,
            score = 0.35f,
            desiredForceScale = 0.9f,
            description = "Di chuyá»ƒn vÃ o khu vá»±c trung tÃ¢m Ä‘á»ƒ chá» cÆ¡ há»™i"
        };

        for (int i = 0; i < context.enemies.Count; i++)
        {
            BotEnemySnapshot enemy = context.enemies[i];
            // Chá»‰ xÃ©t Intercept náº¿u score cá»§a bot > 0
            if (context.ownScore <= 0f)
                continue;
            // Chá»‰ cháº·n lane khi sá»‘ lÆ°á»£ng bi trong vÃ²ng tháº¥p vÃ  enemy Ä‘ang dáº«n Ä‘iá»ƒm cao
            if (context.ringBallCount >= InterceptRingBallThreshold || enemy.score <= InterceptEnemyScoreThreshold)
                continue;

            Vector3 laneDir = (enemy.likelyTarget - enemy.position);
            laneDir.y = 0f;
            if (laneDir.sqrMagnitude < 0.0001f)
                continue;

            laneDir.Normalize();
            float interceptDistance = Mathf.Lerp(1.1f, 2.7f, profile.anticipationStrength);
            Vector3 interceptPoint = enemy.position + laneDir * interceptDistance;
            interceptPoint = Vector3.Lerp(interceptPoint, context.centerPoint, 0.2f);

            float laneToBot = DistancePointToLineXZ(context.botBallPos, enemy.position, enemy.likelyTarget);
            float pathToIntercept = Vector3.Distance(context.botBallPos, interceptPoint);
            float score = 0.55f
                + enemy.threatToBot * (0.85f + profile.tacticalBias)
                + Mathf.Clamp01(1f - laneToBot / 4.5f) * 1.1f
                + Mathf.Clamp01(1f - pathToIntercept / 8f) * 0.85f
                + profile.positionBias * 0.4f;

            if (score > bestPlan.score)
            {
                bestPlan = new BotAimPlan
                {
                    planType = BotPlanType.Intercept,
                    targetPosition = interceptPoint,
                    focusPoint = enemy.position,
                    score = score,
                    desiredForceScale = Mathf.Lerp(0.82f, 1.02f, profile.anticipationStrength),
                    description = $"Cháº·n lane cá»§a enemy {enemy.playerId} (score={enemy.score:F1}) khi ringBallCount={context.ringBallCount}"
                };
            }
        }

        return bestPlan;
    }

    private BotAimPlan BuildPositioningPlan(BotTurnContext context, BotSkillProfile profile)
    {
        Vector3 toCenter = context.centerPoint - context.botBallPos;
        toCenter.y = 0f;
        float centerDistance = toCenter.magnitude;

        Vector3 desiredPos = context.centerPoint;
        if (context.hasNearestEnemy)
        {
            Vector3 enemyToCenter = context.centerPoint - context.nearestEnemyPos;
            enemyToCenter.y = 0f;
            if (enemyToCenter.sqrMagnitude > 0.001f)
            {
                enemyToCenter.Normalize();
                desiredPos = context.centerPoint + enemyToCenter * Mathf.Lerp(0.6f, 1.5f, profile.positionBias);
            }
        }

        float score = 0.45f
            + Mathf.Clamp01(1f - centerDistance / 9f) * 0.9f
            + profile.positionBias * 0.8f
            + (context.hasNearestEnemy ? Mathf.Clamp01(1f - context.nearestEnemyDistance / 8f) * 0.4f : 0f);

        return new BotAimPlan
        {
            planType = BotPlanType.Position,
            targetPosition = desiredPos,
            focusPoint = context.centerPoint,
            score = score,
            desiredForceScale = Mathf.Lerp(0.78f, 0.96f, profile.positionBias),
            description = "Báº¯n chiáº¿n thuáº­t Ä‘á»ƒ chiáº¿m vá»‹ trÃ­ thuáº­n lá»£i cho lÆ°á»£t káº¿"
        };
    }

    private Vector3 ApplyAimNoise(Vector3 rawTarget, Vector3 ballPos, BotSkillProfile profile, bool isExamShot)
    {
        Vector3 noisyTarget = rawTarget;
        float radius = profile.accuracyRadius * (isExamShot ? 0.8f : 1f);
        noisyTarget.x += Random.Range(-radius, radius);
        noisyTarget.z += Random.Range(-radius, radius);

        Vector3 direction = noisyTarget - ballPos;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return noisyTarget;

        float angleError = profile.directionErrorDegrees * (isExamShot ? 0.7f : 1f);
        float signedError = Random.Range(-angleError, angleError);
        Vector3 rotatedDir = Quaternion.Euler(0f, signedError, 0f) * direction.normalized;
        float distance = direction.magnitude;
        return ballPos + rotatedDir * distance;
    }

    private float CalculateShotForce(float distance, float desiredForceScale, BotSkillProfile profile, bool isExamShot)
    {
        float baseForce = Mathf.Clamp(distance * 0.8f, 0.5f, 1.35f);
        float variance = profile.forceVariance * (isExamShot ? 0.7f : 1f);
        float randomScale = 1f + Random.Range(-variance, variance);
        float force = Mathf.Clamp(baseForce * desiredForceScale * randomScale, 0.42f, 1.22f);
        return force;
    }


    private void EvaluateRingShotLane(Vector3 botPos, Vector3 ringPos, NetworkObjectManager manager, out int interferenceCount, out float laneClarity, out float nearestSafetyMargin)
    {
        interferenceCount = 0;
        laneClarity = 1f;
        nearestSafetyMargin = 1f;

        if (manager == null || manager.ringBalls == null)
            return;

        Vector2 start = new Vector2(botPos.x, botPos.z);
        Vector2 end = new Vector2(ringPos.x, ringPos.z);
        Vector2 shotLine = end - start;
        float shotLength = shotLine.magnitude;
        if (shotLength < 0.001f)
            return;

        Vector2 shotDir = shotLine / shotLength;
        float safeLaneRadius = 0.48f;
        float targetExclusionRadius = 0.32f;

        foreach (var obj in manager.ringBalls)
        {
            if (obj == null)
                continue;

            var ctrl = obj.GetComponent<BallServerController>();
            if (ctrl == null || ctrl.IsActive == 0)
                continue;

            Vector3 otherPos3 = obj.transform.position;
            if (Vector3.Distance(otherPos3, ringPos) <= targetExclusionRadius)
                continue;

            Vector2 other = new Vector2(otherPos3.x, otherPos3.z);
            Vector2 toOther = other - start;
            float travel = Vector2.Dot(toOther, shotDir);
            if (travel <= 0.12f || travel >= shotLength - 0.08f)
                continue;

            Vector2 closestPoint = start + shotDir * travel;
            float lateralDistance = Vector2.Distance(other, closestPoint);
            float margin = Mathf.Clamp01(lateralDistance / safeLaneRadius);
            nearestSafetyMargin = Mathf.Min(nearestSafetyMargin, margin);
            if (lateralDistance < safeLaneRadius)
                interferenceCount++;
        }

        laneClarity = Mathf.Clamp01(1f - (interferenceCount * 0.24f + (1f - nearestSafetyMargin) * 0.6f));
    }

    private float EstimateEnemyShotLaneClarity(Vector3 botPos, Vector3 enemyPos, int targetEnemyId, int botPlayerId)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return 1f;

        Vector2 start = new Vector2(botPos.x, botPos.z);
        Vector2 end = new Vector2(enemyPos.x, enemyPos.z);
        Vector2 shotLine = end - start;
        float shotLength = shotLine.magnitude;
        if (shotLength < 0.001f)
            return 0f;

        Vector2 shotDir = shotLine / shotLength;
        float safeLaneRadius = 0.55f;
        float targetExclusionRadius = 0.36f;
        float nearestSafetyMargin = 1f;
        int interferenceCount = 0;

        void SampleObstacle(Vector3 obstaclePos)
        {
            if (Vector3.Distance(obstaclePos, enemyPos) <= targetExclusionRadius)
                return;

            Vector2 obstacle = new Vector2(obstaclePos.x, obstaclePos.z);
            Vector2 toObstacle = obstacle - start;
            float travel = Vector2.Dot(toObstacle, shotDir);
            if (travel <= 0.12f || travel >= shotLength - 0.08f)
                return;

            Vector2 closestPoint = start + shotDir * travel;
            float lateralDistance = Vector2.Distance(obstacle, closestPoint);
            float margin = Mathf.Clamp01(lateralDistance / safeLaneRadius);
            nearestSafetyMargin = Mathf.Min(nearestSafetyMargin, margin);
            if (lateralDistance < safeLaneRadius)
                interferenceCount++;
        }

        if (manager.ringBalls != null)
        {
            foreach (var ringBall in manager.ringBalls)
            {
                if (ringBall == null)
                    continue;

                var ctrl = ringBall.GetComponent<BallServerController>();
                if (ctrl == null || ctrl.IsActive == 0)
                    continue;

                SampleObstacle(ringBall.transform.position);
            }
        }

        foreach (var kvp in manager.PlayerBalls)
        {
            if (kvp.Key == botPlayerId || kvp.Key == targetEnemyId || kvp.Value == null)
                continue;

            foreach (var ball in kvp.Value)
            {
                if (ball == null || ball.gameObject == null || !ball.gameObject.activeInHierarchy)
                    continue;

                var ctrl = ball.GetComponent<BallServerController>();
                if (ctrl == null || ctrl.IsActive == 0)
                    continue;

                SampleObstacle(ball.transform.position);
            }
        }

        return Mathf.Clamp01(1f - (interferenceCount * 0.26f + (1f - nearestSafetyMargin) * 0.58f));
    }

    private float EstimateRingForceScale(BotTurnContext context, BotSkillProfile profile, Vector3 ringPos, float laneClarity, int interferenceCount, float safetyMargin)
    {
        float distance = Vector3.Distance(context.botBallPos, ringPos);
        float distanceFactor = Mathf.Clamp01(distance / 8.5f);
        float clutterFactor = Mathf.Clamp01(interferenceCount / 4f);
        float precisionGain = Mathf.Lerp(0.06f, 0.32f, profile.level01);

        float forceScale = 0.96f
            + distanceFactor * Mathf.Lerp(0.2f, 0.48f, profile.level01)
            + (1f - laneClarity) * Mathf.Lerp(0.08f, 0.24f, profile.level01)
            + (1f - safetyMargin) * 0.12f
            + clutterFactor * Mathf.Lerp(0.05f, 0.16f, profile.level01)
            + precisionGain;

        if (profile.level >= 20)
            forceScale -= Mathf.Lerp(0.02f, 0.08f, laneClarity);

        return Mathf.Clamp(forceScale, 0.96f, 1.42f);
    }

    private float CalculateMomentumReserve(BotAimPlan plan, BotTurnContext context, BotSkillProfile profile, float distance, bool isExamShot)
    {
        if (isExamShot)
            return Mathf.Lerp(0.015f, 0.045f, profile.level01);

        float distanceFactor = Mathf.Clamp01(distance / 8f);
        float reserve = Mathf.Lerp(0.025f, 0.07f, profile.level01) + distanceFactor * 0.02f;

        switch (plan.planType)
        {
            case BotPlanType.OpeningScore:
            case BotPlanType.Ring:
                reserve += Mathf.Lerp(0.04f, 0.12f, profile.level01);
                break;
            case BotPlanType.Enemy:
                reserve += Mathf.Lerp(0.05f, 0.14f, profile.level01);
                break;
            case BotPlanType.Intercept:
                reserve += Mathf.Lerp(0.015f, 0.05f, profile.level01);
                break;
            default:
                reserve += Mathf.Lerp(0.01f, 0.03f, profile.level01);
                break;
        }

        if (context.ownScore <= 0f)
            reserve += Mathf.Lerp(0.03f, 0.08f, profile.level01);

        return reserve;
    }

    private Vector3 BuildSpin(Vector3 shotDirection, BotSkillProfile profile, BotPlanType planType)
    {
        Vector3 flatDirection = Vector3.ProjectOnPlane(shotDirection, Vector3.up);
        if (flatDirection.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        flatDirection.Normalize();

        float forwardSpin = Mathf.Lerp(0.015f, 0.06f, profile.level01);
        if (planType == BotPlanType.Enemy || planType == BotPlanType.OpeningScore || planType == BotPlanType.Ring)
            forwardSpin += Mathf.Lerp(0.005f, 0.03f, profile.level01);

        // Giá»¯ spin cÃ¹ng chiá»u báº¯n vÃ  loáº¡i bá» side-spin Ä‘á»ƒ bot khÃ´ng kÃ­ch hoáº¡t xÃ¬-Ä‘Ãª ngoÃ i Ã½ muá»‘n.
        return flatDirection * forwardSpin;
    }

    private float DistancePointToLineXZ(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 a = new Vector2(lineStart.x, lineStart.z);
        Vector2 b = new Vector2(lineEnd.x, lineEnd.z);
        Vector2 ab = b - a;

        if (ab.sqrMagnitude < 0.0001f)
            return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        Vector2 projection = a + ab * t;
        return Vector2.Distance(p, projection);
    }

    /// <summary>
    /// TÃ­nh toÃ¡n hÆ°á»›ng báº¯n cho lÆ°á»£t thÆ°á»ng dá»±a trÃªn má»¥c tiÃªu thÃ´ng minh.
    /// HÆ°á»›ng báº¯n = tá»« vá»‹ trÃ­ bi â†’ má»¥c tiÃªu (khÃ´ng dÃ¹ng transform.forward).
    /// </summary>
    public BotShotResult CalculateRegularShot(int botPlayerId)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return BotShotResult.Empty;

        var ballObj = manager.GetActiveBallObject(botPlayerId);
        if (ballObj == null)
            return BotShotResult.Empty;

        Vector3 ballPos = ballObj.transform.position;
        int level = GetBotLevel(botPlayerId);
        BotSkillProfile profile = BuildSkillProfile(level);
        BotAimPlan plan = ChooseSmartTarget(botPlayerId, ballPos, profile);
        BotTurnContext context = BuildTurnContext(botPlayerId, ballPos);
        Vector3 targetPos = ApplyAimNoise(plan.targetPosition, ballPos, profile, false);
        if (plan.planType == BotPlanType.OpeningScore)
        {
            float openingNoiseBlend = Mathf.Lerp(0.35f, 0.08f, profile.level01);
            targetPos = Vector3.Lerp(plan.targetPosition, targetPos, openingNoiseBlend);
        }

        targetPos = ResolveDegenerateRegularTarget(ballPos, targetPos, plan, context);

        Vector3 direction = targetPos - ballPos;
        direction.y = 0f;
        float distance = direction.magnitude;

        if (distance < 0.01f)
            return BotShotResult.Empty;

        direction = direction.normalized;
        float force = CalculateShotForce(distance, plan.desiredForceScale, profile, false);
        force = Mathf.Clamp(force + CalculateMomentumReserve(plan, context, profile, distance, false), 0.42f, 1.28f);
        Vector3 spinJitter = BuildSpin(direction, profile, plan.planType);
        float shootAngle = 0f;

        Debug.Log($"ðŸ¤– [BOT] pid={botPlayerId} lv={level} plan={plan.planType} desc={plan.description} target={plan.targetPosition} noisyTarget={targetPos} force={force:F2}");

        return new BotShotResult
        {
            direction = direction,
            force = force,
            spin = spinJitter,
            shootAngle = shootAngle,
            forceMultiplier = plan.desiredForceScale,
            isValid = true,
            planType = plan.planType,
            intendedTarget = plan.targetPosition,
            aimFocusPoint = plan.focusPoint,
            debugReason = plan.description
        };
    }

    /// <summary>
    /// TÃ­nh toÃ¡n hÆ°á»›ng báº¯n cho giai Ä‘oáº¡n thi (nháº¯m Ä‘áº¿n Ä‘Æ°á»ng káº» StartPoint).
    /// HÆ°á»›ng báº¯n = tá»« vá»‹ trÃ­ bi â†’ StartPoint.
    /// </summary>
    public BotShotResult CalculateExamShot(int botPlayerId)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return BotShotResult.Empty;

        var ballObj = manager.GetActiveBallObject(botPlayerId);
        if (ballObj == null)
            return BotShotResult.Empty;

        var host = GameSessionNetWork_Host.Instance;
        if (host == null || host.StartPointMain == null)
            return BotShotResult.Empty;

        Vector3 ballPos = ballObj.transform.position;
        Vector3 targetPos = host.StartPointMain.position;

        int level = GetBotLevel(botPlayerId);
        BotSkillProfile profile = BuildSkillProfile(level);
        targetPos = ApplyAimNoise(targetPos, ballPos, profile, true);

        Vector3 direction = targetPos - ballPos;
        direction.y = 0f;
        float distance = direction.magnitude;

        if (distance < 0.01f)
            return BotShotResult.Empty;

        direction = direction.normalized;
        float force = CalculateShotForce(distance, 1f, profile, true);
        BotAimPlan examPlan = new BotAimPlan { planType = BotPlanType.Exam, desiredForceScale = 1f };
        BotTurnContext examContext = BuildTurnContext(botPlayerId, ballPos);
        force = Mathf.Clamp(force + CalculateMomentumReserve(examPlan, examContext, profile, distance, true), 0.42f, 1.25f);
        Vector3 spinJitter = BuildSpin(direction, profile, BotPlanType.Exam);
        float shootAngle = 0f;

        return new BotShotResult
        {
            direction = direction,
            force = force,
            spin = spinJitter,
            shootAngle = shootAngle,
            forceMultiplier = 1f,
            isValid = true,
            planType = BotPlanType.Exam,
            intendedTarget = host.StartPointMain.position,
            aimFocusPoint = host.StartPointMain.position,
            debugReason = "Báº¯n thi vá» StartPoint vá»›i sai sá»‘ theo level"
        };
    }

    private bool CanBotUseChamCatSkill(int botPlayerId, PlayerNetworkHandler handler)
    {
        var host = GameSessionNetWork_Host.Instance;
        if (host == null || handler == null)
            return false;

        var model = handler.PlayerModel;
        if (model.score <= 0 || model.isDestroy)
            return false;

        if (model.statusPlayer == StatusPlayer.Destroy || model.statusPlayer == StatusPlayer.WaitingDestroy)
            return false;

        if (handler.CurrentAnimState != CharacterAnimState.SitToShoot)
            return false;

        return host.TryGetChamCatTarget(botPlayerId, out _);
    }

    private IEnumerator TryExecuteChamCatSkill(int botPlayerId, PlayerNetworkHandler handler)
    {
        var host = GameSessionNetWork_Host.Instance;
        if (!CanBotUseChamCatSkill(botPlayerId, handler) || host == null)
            yield break;

        Debug.Log($"ðŸ¤– [BOT][ChamCat] pid={botPlayerId} phÃ¡t hiá»‡n má»¥c tiÃªu trong táº§m, Æ°u tiÃªn dÃ¹ng ká»¹ nÄƒng.");

        CharacterAnimState previousAnimState = handler.CurrentAnimState;
        yield return new WaitForSeconds(Random.Range(0.15f, 0.35f));

        handler.CurrentAnimState = CharacterAnimState.PickingUp;
        yield return new WaitForSeconds(Random.Range(0.35f, 0.6f));

        bool usedSkill = host.HandleChamCatSkill(botPlayerId);

        if (handler != null && handler.CurrentAnimState == CharacterAnimState.PickingUp)
            handler.CurrentAnimState = previousAnimState;

        if (usedSkill)
            yield return new WaitForSeconds(Random.Range(0.15f, 0.3f));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Shot Execution â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Coroutine thá»±c hiá»‡n toÃ n bá»™ lÆ°á»£t báº¯n thÆ°á»ng cho bot:
    /// 1) Di chuyá»ƒn nhÃ¢n váº­t Ä‘áº¿n vá»‹ trÃ­ bi (hoáº·c StartPoint)
    /// 2) Giáº£ láº­p ngáº¯m báº¯n theo phong cÃ¡ch phá»¥ thuá»™c level
    /// 3) Xoay vá» hÆ°á»›ng má»¥c tiÃªu
    /// 4) Báº¯n
    /// </summary>
    public IEnumerator ExecuteBotTurnShot(int botPlayerId)
    {
        int executionVersion = BeginBotTurnExecution(botPlayerId);
        var manager = NetworkObjectManager.Instance;
        var host = GameSessionNetWork_Host.Instance;
        if (manager == null || host == null)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] ExecuteBotTurnShot â€“ thiáº¿u manager/host cho pid={botPlayerId}");
            host?.HandelNextTurn();
            yield break;
        }

        var playerObj = manager.GetPlayerObject(botPlayerId);
        if (playerObj == null)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng tÃ¬m tháº¥y playerObj cho bot {botPlayerId}, skip lÆ°á»£t");
            host.HandelNextTurn();
            yield break;
        }

        var handler = playerObj.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng tÃ¬m tháº¥y PlayerNetworkHandler cho bot {botPlayerId}, skip lÆ°á»£t");
            host.HandelNextTurn();
            yield break;
        }

        if (ShouldAbortBotTurn(botPlayerId, executionVersion, handler))
            yield break;

        var ballObj = manager.GetActiveBallObject(botPlayerId);
        if (ballObj == null)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng tÃ¬m tháº¥y bi active cho bot {botPlayerId}, skip lÆ°á»£t");
            host.HandelNextTurn();
            yield break;
        }

        if (BotTurnIndicatorLeadTime > 0f)
        {
            Debug.Log($"ðŸ¤– [BOT] pid={botPlayerId} chá» {BotTurnIndicatorLeadTime:F2}s Ä‘á»ƒ client ká»‹p hiá»‡n thÃ´ng bÃ¡o lÆ°á»£t má»›i trÆ°á»›c khi bot di chuyá»ƒn.");
            yield return new WaitForSeconds(BotTurnIndicatorLeadTime);
        }

        if (ShouldAbortBotTurn(botPlayerId, executionVersion, handler))
            yield break;

        // â”€â”€â”€ BÆ°á»›c 1: Di chuyá»ƒn bot Ä‘áº¿n vá»‹ trÃ­ thÃ­ch há»£p â”€â”€â”€
        Vector3 moveTarget;
        PlayerMovementRequestType moveType;

        if (handler.PlayerModel.statusPlayer == StatusPlayer.StartPoint)
        {
            moveTarget = host.StartPointMain != null ? host.StartPointMain.position : playerObj.transform.position;
            moveType = PlayerMovementRequestType.TeleportStartPoint;
            Debug.Log($"ðŸ¤– [BOT] pid={botPlayerId} status=StartPoint â†’ teleport Ä‘áº¿n má»©c");
        }
        else
        {
            moveTarget = ballObj.transform.position;
            moveType = PlayerMovementRequestType.MoveToPlayArea;
            Debug.Log($"ðŸ¤– [BOT] pid={botPlayerId} â†’ di chuyá»ƒn Ä‘áº¿n bi táº¡i {moveTarget}");
        }

        host.StartServerControlledMovement(botPlayerId, moveTarget, moveType);

        float moveTimeout = 25f;
        float moveElapsed = 0f;
        while (handler.CurrentAnimState != CharacterAnimState.SitToShoot && moveElapsed < moveTimeout)
        {
            if (ShouldAbortBotTurn(botPlayerId, executionVersion, handler))
                yield break;

            moveElapsed += Time.deltaTime;
            yield return null;
        }

        if (ShouldAbortBotTurn(botPlayerId, executionVersion, handler))
            yield break;

        if (handler.CurrentAnimState != CharacterAnimState.SitToShoot)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] pid={botPlayerId} di chuyá»ƒn timeout, Ã©p SitToShoot");
            handler.CurrentAnimState = CharacterAnimState.SitToShoot;
            NetworkObjectManager.Instance?.StartTurnTimerWhenPlayerReadyToShoot(botPlayerId, "bot_force_sit_to_shoot");
            yield return new WaitForSeconds(0.5f);
        }

        TryHoldActiveBallOnFinger(botPlayerId, handler, manager);

        yield return TryExecuteChamCatSkill(botPlayerId, handler);
        if (ShouldAbortBotTurn(botPlayerId, executionVersion, handler))
            yield break;

        var previewShot = CalculateRegularShot(botPlayerId);
        if (!previewShot.isValid)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng tÃ­nh Ä‘Æ°á»£c hÆ°á»›ng preview cho bot {botPlayerId}, skip lÆ°á»£t");
            if (IsBotStillOnItsTurn(botPlayerId) && IsBotTurnExecutionCurrent(botPlayerId, executionVersion))
                host.HandelNextTurn();
            yield break;
        }

        int level = GetBotLevel(botPlayerId);
        BotSkillProfile profile = BuildSkillProfile(level);

        yield return SimulateHumanAiming(handler, botPlayerId, previewShot, profile);
        if (ShouldAbortBotTurn(botPlayerId, executionVersion, handler))
            yield break;

        TryHoldActiveBallOnFinger(botPlayerId, handler, manager);

        var shot = CalculateRegularShot(botPlayerId);
        if (!shot.isValid)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng tÃ­nh Ä‘Æ°á»£c hÆ°á»›ng báº¯n cho bot {botPlayerId}, skip lÆ°á»£t");
            if (IsBotStillOnItsTurn(botPlayerId) && IsBotTurnExecutionCurrent(botPlayerId, executionVersion))
                host.HandelNextTurn();
            yield break;
        }

        if (ShouldAbortBotTurn(botPlayerId, executionVersion, handler))
            yield break;

        if (shot.direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(shot.direction, Vector3.up);
            handler.TargetRotation = targetRot;
            handler.transform.rotation = targetRot;
        }

        yield return new WaitForSeconds(Random.Range(profile.settleShotDelayMin, profile.settleShotDelayMax));

        if (ShouldAbortBotTurn(botPlayerId, executionVersion, handler))
            yield break;

        if (ballObj != null)
        {
            var ballCtrl = ballObj.GetComponent<BallServerController>();
            if (ballCtrl != null)
            {
                ballCtrl.NotifyShotStartedServer();
                ballCtrl.IsHolding = 0;
                ballCtrl.hasBeenShoot = 1;
            }
        }
        Debug.Log($"ðŸ¤– [BOT] pid={botPlayerId} báº¯n! plan={shot.planType} reason={shot.debugReason} dir={shot.direction} force={shot.force:F2}");
        ApplyBotShot(botPlayerId, shot);
    }

    /// <summary>
    /// Giáº£ láº­p hÃ nh vi ngáº¯m báº¯n giá»‘ng ngÆ°á»i tháº­t nhÆ°ng cÃ³ ká»¹ nÄƒng theo level:
    /// bot level tháº¥p xoay vá»¥ng, biÃªn Ä‘á»™ lá»›n vÃ  khÃ³ khÃ³a má»¥c tiÃªu;
    /// bot level cao sweep nhá» dáº§n, bÃ¡m focus point vÃ  chá»‘t gÃ³c gá»n hÆ¡n.
    /// </summary>
    private IEnumerator SimulateHumanAiming(PlayerNetworkHandler handler, int botPlayerId, BotShotResult previewShot, BotSkillProfile profile)
    {
        if (handler == null)
            yield break;

        int aimSteps = Random.Range(profile.aimStepsMin, profile.aimStepsMax + 1);
        Vector3 aimOrigin = GetBotAimOrigin(handler);
        Vector3 finalLookTarget = ResolveBotLookTarget(handler, previewShot, aimOrigin, 5.5f);
        Vector3 baseDirection = finalLookTarget - aimOrigin;
        if (baseDirection.sqrMagnitude <= 0.0001f)
            baseDirection = handler.transform.forward;

        Vector3 flatDirection = Vector3.ProjectOnPlane(baseDirection, Vector3.up);
        if (flatDirection.sqrMagnitude <= 0.0001f)
            flatDirection = Vector3.ProjectOnPlane(handler.transform.forward, Vector3.up);
        if (flatDirection.sqrMagnitude <= 0.0001f)
            flatDirection = Vector3.forward;
        flatDirection.Normalize();

        Vector3 rightAxis = Vector3.Cross(Vector3.up, flatDirection);
        if (rightAxis.sqrMagnitude <= 0.0001f)
            rightAxis = Vector3.right;
        rightAxis.Normalize();

        for (int i = 0; i < aimSteps; i++)
        {
            float step01 = aimSteps <= 1 ? 1f : (float)i / (aimSteps - 1);
            float discipline = Mathf.Lerp(0.35f, 1f, profile.level01);
            float sweep = Mathf.Lerp(profile.aimSweepAngle, profile.aimSweepAngle * 0.18f, step01 * discipline);
            float driftYaw = Random.Range(-sweep, sweep);
            float focusBias = Mathf.Lerp(0.15f, 0.92f, profile.focusTargetWeight * step01);
            float yawOffsetDistance = Mathf.Tan(Mathf.Deg2Rad * driftYaw) * Mathf.Max(2.5f, baseDirection.magnitude);
            float pitchNoise = Random.Range(profile.pitchNoiseMin, profile.pitchNoiseMax);
            float pitchSign = Random.value < 0.5f ? -1f : 1f;
            float verticalOffset = pitchNoise * pitchSign * Mathf.Lerp(1f, 0.2f, step01 * profile.verticalFocusBias);
            float distanceJitter = profile.idleJitterDistance * (1f - step01) * Random.Range(0.4f, 1f);

            Vector3 candidateLookTarget = finalLookTarget
                + rightAxis * yawOffsetDistance * (1f - focusBias)
                + Vector3.up * verticalOffset
                - flatDirection * distanceJitter;

            if (i == 0 && Random.value < profile.overshootChance)
                candidateLookTarget += rightAxis * yawOffsetDistance * 0.35f;

            if (i == aimSteps - 1 && Random.value < profile.reacquireChance)
            {
                Vector3 reacquireNudge = rightAxis * Random.Range(-0.18f, 0.18f)
                    + Vector3.up * Random.Range(-0.06f, 0.1f);
                candidateLookTarget = finalLookTarget + reacquireNudge;
            }

            yield return SmoothBotLookAt(handler, candidateLookTarget, Random.Range(profile.aimDurationMin, profile.aimDurationMax));
            yield return new WaitForSeconds(Random.Range(profile.holdDelayMin, profile.holdDelayMax));
        }

        yield return SmoothBotLookAt(handler, finalLookTarget, Random.Range(profile.aimDurationMin * 0.8f, profile.aimDurationMax * 0.85f));

        Debug.Log($"ðŸ¤– [BOT] pid={botPlayerId} hoÃ n táº¥t ngáº¯m ({aimSteps} láº§n) plan={previewShot.planType} focus={previewShot.aimFocusPoint}");
    }

    private IEnumerator SmoothBotLookAt(PlayerNetworkHandler handler, Vector3 lookTarget, float duration)
    {
        if (handler == null)
            yield break;

        duration = Mathf.Max(0.01f, duration);
        Vector3 startOrigin = GetBotAimOrigin(handler);
        Vector3 startDirection = lookTarget - startOrigin;
        if (startDirection.sqrMagnitude <= 0.0001f)
            yield break;

        Vector3 startFlatDirection = Vector3.ProjectOnPlane(handler.transform.forward, Vector3.up);
        if (startFlatDirection.sqrMagnitude <= 0.0001f)
            startFlatDirection = Vector3.ProjectOnPlane(startDirection, Vector3.up);
        if (startFlatDirection.sqrMagnitude <= 0.0001f)
            startFlatDirection = Vector3.forward;
        startFlatDirection.Normalize();

        Quaternion startBodyRotation = Quaternion.LookRotation(startFlatDirection, Vector3.up);
        Quaternion targetBodyRotation = GetBodyRotationFromLookDirection(startDirection, startBodyRotation);
        float startPitch = NormalizePitch(handler.HeadRotation.eulerAngles.x);
        float targetPitch = GetPitchFromLookDirection(startDirection);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            Quaternion bodyRotation = Quaternion.Slerp(startBodyRotation, targetBodyRotation, t);
            float pitch = Mathf.Lerp(startPitch, targetPitch, t);
            ApplyBotLookRotation(handler, bodyRotation, pitch);
            yield return null;
        }

        ApplyBotLookRotation(handler, targetBodyRotation, targetPitch);
    }

    private Vector3 GetBotAimOrigin(PlayerNetworkHandler handler)
    {
        if (handler == null)
            return Vector3.zero;

        if (handler.TryGetAimOrigin(out var origin))
            return origin;

        return handler.transform.position;
    }

    private Vector3 ResolveBotLookTarget(PlayerNetworkHandler handler, BotShotResult previewShot, Vector3 aimOrigin, float fallbackDistance)
    {
        Vector3 lookTarget = previewShot.aimFocusPoint;
        if ((lookTarget - aimOrigin).sqrMagnitude > 0.0001f)
            return lookTarget;

        lookTarget = previewShot.intendedTarget;
        if ((lookTarget - aimOrigin).sqrMagnitude > 0.0001f)
            return lookTarget;

        Vector3 direction = previewShot.direction.sqrMagnitude > 0.0001f
            ? previewShot.direction.normalized
            : handler != null ? handler.transform.forward : Vector3.forward;

        return aimOrigin + direction * Mathf.Max(1.5f, fallbackDistance);
    }

    private Quaternion GetBodyRotationFromLookDirection(Vector3 lookDirection, Quaternion fallbackRotation)
    {
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(lookDirection, Vector3.up);
        if (horizontalDirection.sqrMagnitude <= 0.0001f)
            return fallbackRotation;

        return Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
    }

    private float GetPitchFromLookDirection(Vector3 lookDirection)
    {
        if (lookDirection.sqrMagnitude <= 0.0001f)
            return 0f;

        Quaternion lookRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        return NormalizePitch(lookRotation.eulerAngles.x);
    }

    private float NormalizePitch(float pitch)
    {
        if (pitch > 180f)
            pitch -= 360f;
        return pitch;
    }

    private void ApplyBotLookRotation(PlayerNetworkHandler handler, Quaternion bodyRotation, float pitch)
    {
        if (handler == null)
            return;

        handler.TargetRotation = bodyRotation;
        handler.transform.rotation = bodyRotation;
        handler.HeadRotation = Quaternion.Euler(NormalizePitch(pitch), 0f, 0f);
        handler.UpdatePointPosition(pitch);
    }

    /// <summary>
    /// Coroutine thá»±c hiá»‡n báº¯n thi cho bot. Chá» 1-3 giÃ¢y rá»“i báº¯n.
    /// </summary>
    public IEnumerator ExecuteBotExamShot(int botPlayerId)
    {
        int level = GetBotLevel(botPlayerId);
        BotSkillProfile profile = BuildSkillProfile(level);
        yield return new WaitForSeconds(Random.Range(2.6f, 5.4f) + (1f - profile.level01) * 0.45f);

        var shot = CalculateExamShot(botPlayerId);
        if (!shot.isValid)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng tÃ­nh Ä‘Æ°á»£c hÆ°á»›ng báº¯n thi cho bot {botPlayerId}");
            yield break;
        }

        ApplyBotShot(botPlayerId, shot);
    }

    /// <summary>
    /// Ãp dá»¥ng shot data lÃªn viÃªn bi cá»§a bot (phÃ­a server).
    /// Viáº¿t trá»±c tiáº¿p vÃ o ShotData cá»§a BallServerController â†’ trigger OnShotParamsChanged â†’ ShootBall.
    /// </summary>
    private void ApplyBotShot(int botPlayerId, BotShotResult shot)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null) return;

        for (int i = 0; i < manager.players.Length; i++)
        {
            var p = manager.players.Get(i);
            if (p.playerId == botPlayerId)
            {
                p.isHolding = false;
                if (manager.HasStateAuthority)
                    manager.players.Set(i, p);
                break;
            }
        }

        var hostInstance = GameSessionNetWork_Host.Instance;
        if (hostInstance != null)
            hostInstance.ResetConsecutiveTimeout(botPlayerId);

        var ballObj = manager.GetActiveBallObject(botPlayerId);
        if (ballObj == null)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng tÃ¬m tháº¥y bi active cho bot {botPlayerId}");
            return;
        }

        var ctrl = ballObj.GetComponent<BallServerController>();
        if (ctrl == null)
        {
            Debug.LogWarning($"ðŸ¤– [BOT] KhÃ´ng tÃ¬m tháº¥y BallServerController cho bot {botPlayerId}");
            return;
        }

        // LuÃ´n reset water-collision flag trÆ°á»›c khi báº¯n, báº¥t ká»ƒ Ä‘Æ°á»ng dáº«n nÃ o gá»i ApplyBotShot
        // (ExecuteBotExamShot khÃ´ng gá»i NotifyShotStartedServer nÃªn cáº§n reset á»Ÿ Ä‘Ã¢y)
        ctrl.NotifyShotStartedServer();

        ctrl.SnapToOwnerFinger();

        Vector3 safeDirection = Vector3.ProjectOnPlane(shot.direction, Vector3.up);
        if (safeDirection.sqrMagnitude < 0.0001f)
            safeDirection = shot.direction.sqrMagnitude > 0.0001f ? shot.direction.normalized : ctrl.transform.forward;
        else
            safeDirection.Normalize();

        Vector3 safeSpin = Vector3.zero;
        if (shot.spin.sqrMagnitude > 0.0001f)
        {
            float alignedSpin = Mathf.Max(0f, Vector3.Dot(shot.spin, safeDirection));
            safeSpin = safeDirection * alignedSpin;
        }

        float safeAngle = Mathf.Clamp(shot.shootAngle, -2f, 2f);
        float safeForce = shot.force;
        int ringBallCount = GetCurrentRingBallCount(manager);
        float ringBallForceMultiplier = GetRingBallForceMultiplier(ringBallCount);
        int botLevel = GetBotLevel(botPlayerId);
        float botLevel01 = NormalizeBotLevel(botLevel);
        float ringBallDensity01 = GetRingBallDensity01(ringBallCount);
        bool isRingClusterShot = IsRingClusterShot(shot.planType);

        if (isRingClusterShot)
            safeSpin *= 0.25f;

        if (isRingClusterShot)
        {
            Vector3 ringFocus = shot.aimFocusPoint == Vector3.zero ? shot.intendedTarget : shot.aimFocusPoint;
            float dist = Vector3.Distance(ctrl.transform.position, ringFocus);
            float baseShotForce = Mathf.Clamp(shot.force, 0.42f, 1.28f);
            float distanceScale = Mathf.Lerp(1.25f, 1.85f, Mathf.Clamp01(dist / 8f));
            float levelScale = Mathf.Lerp(1f, 1.08f, botLevel01);

            safeForce = baseShotForce * distanceScale * levelScale * ringBallForceMultiplier;
            safeForce = Mathf.Clamp(safeForce, Mathf.Lerp(1.65f, 2.15f, ringBallDensity01), Mathf.Lerp(3.4f, 4.6f, ringBallDensity01));
        }
        else if (shot.planType == BotPlanType.Enemy)
        {
            float dist = Vector3.Distance(ctrl.transform.position, shot.intendedTarget);
            // Cháº¿ Ä‘á»™ truy sÃ¡t ngÆ°á»i chÆ¡i khÃ¡c: luÃ´n giá»¯ lá»±c cao Ä‘á»ƒ cÃº va cháº¡m cÃ³ quÃ¡n tÃ­nh máº¡nh.
            safeForce = Mathf.Clamp(Mathf.Lerp(2.5f, 7f, Mathf.Clamp01(dist / 7f)) * Mathf.Lerp(1f, 1.12f, ringBallDensity01), 2.5f, 7.8f);
        }
        else if (shot.planType == BotPlanType.Exam)
        {
            safeForce = Mathf.Clamp(safeForce, 2f, 3f);
        }
        else
        {
           // safeForce = Mathf.Clamp(safeForce, 0.42f, 1.15f);
            safeForce = Mathf.Clamp(safeForce * ringBallForceMultiplier, 2f, Mathf.Lerp(3f, 3.8f, ringBallDensity01));
        }

        Debug.Log($"ðŸ¤– [BOT] Bot {botPlayerId} báº¯n: plan={shot.planType}, reason={shot.debugReason}, target={shot.intendedTarget}, dir={safeDirection}, force={safeForce:F2}, spin={safeSpin}, angle={safeAngle:F2}, mult={shot.forceMultiplier:F2}, ringBallCount={ringBallCount}, ringDensity={ringBallDensity01:F2}, ringForceMul={ringBallForceMultiplier:F2}");

        ctrl.ShotData = new ShotParams
        {
            direction = safeDirection,
            force = safeForce,
            spin = safeSpin,
            shootAngle = safeAngle
        };
    }

    private static int GetCurrentRingBallCount(NetworkObjectManager manager)
    {
        if (manager == null || manager.ringBalls == null)
            return 0;

        int count = 0;
        foreach (var id in manager.ringBalls.EnumerateIds())
        {
            if (id != default)
                count++;
        }

        if (count > 0)
            return count;

        // Fallback: náº¿u collection chÆ°a Ä‘á»“ng bá»™ id, Ä‘áº¿m theo object thá»±c táº¿.
        foreach (var ringBall in manager.ringBalls)
        {
            if (ringBall != null && ringBall.Id != default)
                count++;
        }

        return count;
    }

    private static float GetRingBallForceMultiplier(int ringBallCount)
    {
        float normalizedCount = GetRingBallDensity01(ringBallCount);
        return Mathf.Lerp(MinRingBallForceMultiplier, MaxRingBallForceMultiplier, normalizedCount);
    }

    private static float GetRingBallDensity01(int ringBallCount)
    {
        int safeCount = Mathf.Clamp(ringBallCount, 0, MaxRingBallForceScaleCount);
        return safeCount / (float)MaxRingBallForceScaleCount;
    }

    private static bool IsRingClusterShot(BotPlanType planType)
    {
        return planType == BotPlanType.Ring || planType == BotPlanType.OpeningScore;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private Vector3 FindNearestRingBallPosition(Vector3 fromPos)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return fromPos + Vector3.forward;

        float minDist = float.MaxValue;
        Vector3 nearest = fromPos + Vector3.forward;

        foreach (var obj in manager.ringBalls)
        {
            if (obj == null) continue;
            var ctrl = obj.GetComponent<BallServerController>();
            if (ctrl == null || ctrl.IsActive == 0) continue;

            float dist = Vector3.Distance(fromPos, obj.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = obj.transform.position;
            }
        }

        return nearest;
    }

    private Vector3 GetRingApproachTarget(Vector3 botBallPos, Vector3 ringPos)
    {
        Vector3 approachDirection = ringPos - botBallPos;
        approachDirection.y = 0f;

        if (approachDirection.sqrMagnitude < 0.0001f)
            return ringPos;

        float desiredStandOff = Mathf.Max(0.1f, ringApproachStandOffDistance);
        float distanceToRing = approachDirection.magnitude;
        float travelDistance = Mathf.Max(0.1f, distanceToRing - desiredStandOff);

        return botBallPos + approachDirection.normalized * travelDistance;
    }

    private int GetBotLevel(int botPlayerId)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null) return 5;

        for (int i = 0; i < manager.players.Length; i++)
        {
            var info = manager.players.Get(i);
            if (info.playerId == botPlayerId)
                return Mathf.Clamp(Mathf.Max(info.level, 1), 1, MaxSupportedBotLevel);
        }

        return 5;
    }
}

public enum BotPlanType
{
    None = 0,
    OpeningScore = 1,
    Ring = 2,
    Enemy = 3,
    Intercept = 4,
    Position = 5,
    Exam = 6
}

public struct BotSkillProfile
{
    public int level;
    public float level01;
    public float accuracyRadius;
    public float directionErrorDegrees;
    public float forceVariance;
    public float spinNoise;
    public float enemyAggression;
    public float tacticalBias;
    public float finishingBias;
    public float ringPriority;
    public float positionBias;
    public float anticipationStrength;
    public float focusTargetWeight;
    public int aimStepsMin;
    public int aimStepsMax;
    public float aimSweepAngle;
    public float aimDurationMin;
    public float aimDurationMax;
    public float holdDelayMin;
    public float holdDelayMax;
    public float settleShotDelayMin;
    public float settleShotDelayMax;
    public float pitchNoiseMin;
    public float pitchNoiseMax;
    public float verticalFocusBias;
    public float reacquireChance;
    public float overshootChance;
    public float idleJitterDistance;
}

public struct BotEnemySnapshot
{
    public int playerId;
    public float score;
    public Vector3 position;
    public Vector3 likelyTarget;
    public float distanceToBot;
    public float threatToBot;
    public float pressureOnRing;
}

public struct BotTurnContext
{
    public int botPlayerId;
    public Vector3 botBallPos;
    public Vector3 centerPoint;
    public Vector3 nearestRingPos;
    public float nearestRingDistance;
    public Vector3 nearestEnemyPos;
    public float nearestEnemyDistance;
    public bool hasNearestEnemy;
    public float ownScore;
    public bool hasOwnScore;
    public int ringBallCount;
    public List<BotEnemySnapshot> enemies;
}

public struct RingTargetCandidate
{
    public bool isValid;
    public Vector3 ringPosition;
    public Vector3 aimPosition;
    public float laneClarity;
    public int interferenceCount;
    public float safetyMargin;
    public float distanceToBot;
    public int localCrowd;
    public float sparseScore;
    public int activeRingCount;
    public float score;
}

public struct BotAimPlan
{
    public BotPlanType planType;
    public Vector3 targetPosition;
    public Vector3 focusPoint;
    public float desiredForceScale;
    public float score;
    public string description;
}

/// <summary>
/// Káº¿t quáº£ tÃ­nh toÃ¡n hÆ°á»›ng báº¯n cá»§a bot.
/// </summary>
public struct BotShotResult
{
    public Vector3 direction;
    public float force;
    public Vector3 spin;
    public float shootAngle;
    public float forceMultiplier;
    public bool isValid;
    public BotPlanType planType;
    public Vector3 intendedTarget;
    public Vector3 aimFocusPoint;
    public string debugReason;

    public static BotShotResult Empty => new BotShotResult { isValid = false, planType = BotPlanType.None };
}
#endif
