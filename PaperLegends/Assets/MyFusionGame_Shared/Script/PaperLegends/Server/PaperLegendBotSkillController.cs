#if UNITY_SERVER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PaperLegendBotSkillController : MonoBehaviour
{
    public static PaperLegendBotSkillController Instance { get; private set; }

    private const float thinkIntervalSeconds = 0.35f;
    private const float minSkillScore = 999f;
    private const float armedSkillForce01 = 0.62f;
    private const float contactOffset = 0.35f;

    private const float closeCombatRange = 3.5f;
    private const float mediumRange = 8f;
    private const float longRange = 14f;
    private const float areaSkillRadius = 4f;
    private const float shoveStunRadius = 2.8f;

    private readonly List<PaperLegendCharacterNetworkHandler> _botCharacters =
        new List<PaperLegendCharacterNetworkHandler>();

    private Coroutine _botRoutine;
    private int _flickSequence;

    private struct BotSkillChoice
    {
        public int Slot;
        public float Score;
        public PaperLegendCharacterNetworkHandler TargetEnemy;
        public Vector3 TargetPosition;
        public bool HasTargetPosition;
    }

    public static PaperLegendBotSkillController Ensure()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject(nameof(PaperLegendBotSkillController));
        DontDestroyOnLoad(go);
        return go.AddComponent<PaperLegendBotSkillController>();
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

        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host.GetRegisteredPlayers();

        for (int i = 0; i < _botCharacters.Count; i++)
        {
            var bot = _botCharacters[i];
            if (bot == null)
                continue;

            if (!bot.IsAlive || !botController.IsBotPlayer(bot.PlayerId))
                continue;

            if (bot.HasPendingBotSkillFollowUp)
            {
                TryCompleteArmedSkill(bot, players);
                continue;
            }

            if (!CanConsiderNewSkill(bot))
                continue;

            TryActivateBestSkill(bot, players);
        }
    }

    private static bool CanConsiderNewSkill(PaperLegendCharacterNetworkHandler bot)
    {
        if (bot == null || !bot.IsAlive || bot.IsStunned)
            return false;

        return bot.CanAcceptLocalFlick;
    }

    private bool TryCompleteArmedSkill(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players)
    {
        if (bot == null || !bot.HasPendingBotSkillFollowUp)
            return false;

        PaperLegendCharacterNetworkHandler target = FindBestEnemy(bot, players);
        Vector3 direction = ResolveDirectionToward(bot, target);
        var input = BuildDirectionalFlickInput(bot, direction, armedSkillForce01);

        if (bot.Hero10000004HomingSwordArmed)
            input.Hero10000004HomingSwordRequested = true;

        if (bot.Hero10000002ForwardSlideArmed && bot.Hero10000002ForwardSlideRemaining > 0)
            input.Hero10000002ForwardSlideRequested = true;

        bot.ServerTryApplyBotInput(input);
        return true;
    }

    private bool TryActivateBestSkill(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players)
    {
        if (!TryEvaluateBestSkill(bot, players, out BotSkillChoice choice))
            return false;

        if (choice.Score < minSkillScore)
            return false;

        var input = new PaperLegendPlayerInputData
        {
            SkillRequested = true,
            SkillSlot = choice.Slot,
            SkillTargetWorldPositionSet = choice.HasTargetPosition,
            SkillTargetWorldPosition = choice.TargetPosition,
            SkillTargetPlayerId = choice.TargetEnemy != null ? choice.TargetEnemy.PlayerId : 0
        };

        bot.ServerTryApplyBotInput(input);
        Debug.Log($"[PaperLegends][BOT][Skill] player={bot.PlayerId} model={bot.CharacterModelId} cast slot={choice.Slot} score={choice.Score:0.00} targetPlayer={(choice.TargetEnemy != null ? choice.TargetEnemy.PlayerId : 0)}.");
        return true;
    }

    private bool TryEvaluateBestSkill(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players,
        out BotSkillChoice choice)
    {
        choice = default;

        switch (bot.CharacterModelId)
        {
            case PaperLegendHero10000001SkillSet.HeroId:
                return TryEvaluateHero10000001(bot, players, out choice);
            case PaperLegendHero10000002SkillSet.HeroId:
                return TryEvaluateHero10000002(bot, players, out choice);
            case PaperLegendHero10000003SonTinhSkillSet.HeroId:
                return TryEvaluateHero10000003(bot, players, out choice);
            case PaperLegendHero10000004SonTinhSkillSet.HeroId:
                return TryEvaluateHero10000004(bot, players, out choice);
            case PaperLegendHero10000005ThanSamSkillSet.HeroId:
                return TryEvaluateHero10000005(bot, players, out choice);
            default:
                return false;
        }
    }

    private bool TryEvaluateHero10000001(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players,
        out BotSkillChoice choice)
    {
        choice = default;
        PaperLegendCharacterNetworkHandler target = FindBestEnemy(bot, players);
        if (target == null)
            return false;

        float distance = HorizontalDistance(bot.transform.position, target.transform.position);
        BotSkillChoice best = default;
        float bestScore = minSkillScore;

        TryScoreSlot(bot, 2, ScoreRangeSkill(distance, mediumRange, longRange), ref best, ref bestScore, target);
        TryScoreSlot(bot, 3, ScoreRangeSkill(distance, closeCombatRange, mediumRange) + 0.5f, ref best, ref bestScore, target);
        TryScoreSlot(bot, 4, ScoreRangeSkill(distance, closeCombatRange, mediumRange), ref best, ref bestScore, target);

        if (best.Score < minSkillScore)
            return false;

        choice = best;
        return true;
    }

    private bool TryEvaluateHero10000002(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players,
        out BotSkillChoice choice)
    {
        choice = default;
        BotSkillChoice best = default;
        float bestScore = minSkillScore;

        if (bot.CanActivateHero10000002LastStand()
            && bot.MaxHealth > 0f
            && bot.CurrentHealth / bot.MaxHealth <= 0.35f)
        {
            TryScoreSlot(bot, 4, 8f, ref best, ref bestScore, null);
        }

        if (bot.ServerHasNearbyEnemyForShoveStun(shoveStunRadius))
        {
            PaperLegendCharacterNetworkHandler target = FindClosestEnemy(bot, players, shoveStunRadius);
            TryScoreSlot(bot, 2, 5f + (target != null ? 1f : 0f), ref best, ref bestScore, target);
        }

        if (best.Score < minSkillScore)
            return false;

        choice = best;
        return true;
    }

    private bool TryEvaluateHero10000003(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players,
        out BotSkillChoice choice)
    {
        choice = default;
        if (!TryFindEnemyCluster(bot, players, areaSkillRadius, out Vector3 clusterCenter, out int enemyCount))
            return false;

        BotSkillChoice best = default;
        float bestScore = minSkillScore;
        PaperLegendCharacterNetworkHandler clusterTarget = FindClosestEnemyToPoint(clusterCenter, bot, players);

        float clusterScore = enemyCount * 2.2f;
        TryScoreSlot(bot, 3, clusterScore + 1f, ref best, ref bestScore, clusterTarget, clusterCenter, true);
        TryScoreSlot(bot, 2, clusterScore, ref best, ref bestScore, clusterTarget, clusterCenter, true);

        PaperLegendCharacterNetworkHandler lineTarget = FindBestEnemy(bot, players);
        if (lineTarget != null)
        {
            float lineDistance = HorizontalDistance(bot.transform.position, lineTarget.transform.position);
            TryScoreSlot(bot, 1, ScoreRangeSkill(lineDistance, closeCombatRange, longRange), ref best, ref bestScore, lineTarget);
        }

        if (best.Score < minSkillScore)
            return false;

        choice = best;
        return true;
    }

    private bool TryEvaluateHero10000004(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players,
        out BotSkillChoice choice)
    {
        choice = default;
        PaperLegendCharacterNetworkHandler target = FindBestEnemy(bot, players);
        if (target == null)
            return false;

        float distance = HorizontalDistance(bot.transform.position, target.transform.position);
        BotSkillChoice best = default;
        float bestScore = minSkillScore;

        TryScoreSlot(bot, 2, ScoreRangeSkill(distance, mediumRange * 0.6f, longRange) + 1.5f, ref best, ref bestScore, target);
        TryScoreSlot(bot, 1, ScoreRangeSkill(distance, closeCombatRange, mediumRange + 2f), ref best, ref bestScore, target);

        if (best.Score < minSkillScore)
            return false;

        choice = best;
        return true;
    }

    private bool TryEvaluateHero10000005(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players,
        out BotSkillChoice choice)
    {
        choice = default;
        if (!TryFindEnemyCluster(bot, players, areaSkillRadius, out Vector3 clusterCenter, out int enemyCount))
            return false;

        BotSkillChoice best = default;
        float bestScore = minSkillScore;
        PaperLegendCharacterNetworkHandler clusterTarget = FindClosestEnemyToPoint(clusterCenter, bot, players);
        TryScoreSlot(bot, 4, enemyCount * 2.5f + 1f, ref best, ref bestScore, clusterTarget, clusterCenter, true);

        if (best.Score < minSkillScore)
            return false;

        choice = best;
        return true;
    }

    private void TryScoreSlot(
        PaperLegendCharacterNetworkHandler bot,
        int slot,
        float score,
        ref BotSkillChoice best,
        ref float bestScore,
        PaperLegendCharacterNetworkHandler target,
        Vector3 targetPosition = default,
        bool hasTargetPosition = false)
    {
        if (score <= 0f || !bot.CanUseSkill(slot) || bot.GetSkillLevel(slot) <= 0)
            return;

        if (score <= bestScore)
            return;

        bestScore = score;
        best = new BotSkillChoice
        {
            Slot = slot,
            Score = score,
            TargetEnemy = target,
            TargetPosition = targetPosition,
            HasTargetPosition = hasTargetPosition
        };
    }

    private static float ScoreRangeSkill(float distance, float idealMin, float idealMax)
    {
        if (distance < idealMin * 0.5f || distance > idealMax * 1.35f)
            return 0f;

        float center = (idealMin + idealMax) * 0.5f;
        float halfSpan = Mathf.Max(0.5f, (idealMax - idealMin) * 0.5f);
        float normalized = 1f - Mathf.Abs(distance - center) / halfSpan;
        return Mathf.Max(0f, normalized) * 4f;
    }

    private static PaperLegendCharacterNetworkHandler FindBestEnemy(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players)
    {
        PaperLegendCharacterNetworkHandler best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < players.Count; i++)
        {
            var candidate = players[i];
            if (candidate == null || candidate == bot || !candidate.IsAlive || bot.IsSameFaction(candidate))
                continue;

            float distance = HorizontalDistance(bot.transform.position, candidate.transform.position);
            float score = distance;
            if (!candidate.IsGrounded)
                score += 1.25f;

            int levelGap = candidate.Level - bot.Level;
            if (levelGap > 2)
                score += (levelGap - 2) * 1.5f;
            else if (levelGap < 0)
                score -= Mathf.Abs(levelGap) * 0.35f;

            if (score >= bestScore)
                continue;

            best = candidate;
            bestScore = score;
        }

        return best;
    }

    private static PaperLegendCharacterNetworkHandler FindClosestEnemy(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players,
        float maxRange)
    {
        PaperLegendCharacterNetworkHandler best = null;
        float bestDistance = maxRange;

        for (int i = 0; i < players.Count; i++)
        {
            var candidate = players[i];
            if (candidate == null || candidate == bot || !candidate.IsAlive || bot.IsSameFaction(candidate))
                continue;

            float distance = HorizontalDistance(bot.transform.position, candidate.transform.position);
            if (distance > bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    private static PaperLegendCharacterNetworkHandler FindClosestEnemyToPoint(
        Vector3 point,
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players)
    {
        PaperLegendCharacterNetworkHandler best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < players.Count; i++)
        {
            var candidate = players[i];
            if (candidate == null || candidate == bot || !candidate.IsAlive || bot.IsSameFaction(candidate))
                continue;

            float distance = HorizontalDistance(point, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    private static bool TryFindEnemyCluster(
        PaperLegendCharacterNetworkHandler bot,
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players,
        float radius,
        out Vector3 clusterCenter,
        out int enemyCount)
    {
        clusterCenter = default;
        enemyCount = 0;

        PaperLegendCharacterNetworkHandler seed = FindBestEnemy(bot, players);
        if (seed == null)
            return false;

        radius = Mathf.Max(0.5f, radius);
        Vector3 center = seed.transform.position;
        int count = 0;

        for (int i = 0; i < players.Count; i++)
        {
            var candidate = players[i];
            if (candidate == null || candidate == bot || !candidate.IsAlive || bot.IsSameFaction(candidate))
                continue;

            if (HorizontalDistance(center, candidate.transform.position) > radius)
                continue;

            count++;
        }

        if (count <= 0)
            return false;

        clusterCenter = center;
        enemyCount = count;
        return true;
    }

    private static Vector3 ResolveDirectionToward(
        PaperLegendCharacterNetworkHandler bot,
        PaperLegendCharacterNetworkHandler target)
    {
        Vector3 direction = target != null
            ? target.transform.position - bot.transform.position
            : bot.transform.forward;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        return direction.normalized;
    }

    private PaperLegendPlayerInputData BuildDirectionalFlickInput(
        PaperLegendCharacterNetworkHandler bot,
        Vector3 direction,
        float force01)
    {
        Vector3 contactPoint = bot.transform.position - direction * contactOffset;

        return new PaperLegendPlayerInputData
        {
            FlickRequested = true,
            FlickSequence = ++_flickSequence,
            ContactWorldPosition = contactPoint,
            ContactSurfaceNormal = -direction,
            AimWorldDirection = direction,
            Force01 = Mathf.Clamp01(force01)
        };
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
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
                _botCharacters.RemoveAt(i);
        }
    }
}
#endif
