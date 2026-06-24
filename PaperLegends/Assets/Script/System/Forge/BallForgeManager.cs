using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
 
public class BallForgeManager
{
    private const int MaxSkillSlots = 2;
    private readonly List<BallForgeSkillData> _pendingSkills = new List<BallForgeSkillData>(MaxSkillSlots);

    public IReadOnlyList<BallForgeSkillData> PendingSkills => _pendingSkills;

    public BallForgeSkillData RollRandomSkill(ItemSchema item, IReadOnlyList<EffectPlayer> availableSkills = null)
    {
        if (availableSkills == null || !availableSkills.Any())
        {
            availableSkills = EffectPlayerController.Instance != null ? EffectPlayerController.Instance.Effects : null;
        }

        bool canRollSupport = availableSkills != null && availableSkills.Any();
        bool rollSupportSkill = canRollSupport && UnityEngine.Random.value > 0.5f;

        return rollSupportSkill
            ? RollSupportSkill(availableSkills)
            : RollBallShootingTechnique(item);
    }

    private BallForgeSkillData RollBallShootingTechnique(ItemSchema item)
    {
        var result = new BallForgeSkillData
        {
            SkillType = BallForgeSkillType.BallShootingTechnique,
            BallShootingTechnique = new BallTechniqueBonuses(),
            SupportBonus = null
        };

        var bonuses = result.BallShootingTechnique;
        var availableAttributes = new List<Action>
        {
            () => bonuses.MassBonus = RollBonus(item?.Mass ?? 0f, 0.05f, 0.22f, 0.05f, 0.4f),
            () => bonuses.GravityScaleBonus = RollBonus(item?.GravityScale ?? 0f, 0.05f, 0.2f, 0.05f, 0.3f),
            () => bonuses.DragBonus = RollBonus(item?.Drag ?? 0f, 0.07f, 0.25f, 0.05f, 0.35f),
            () => bonuses.BouncinessBonus = RollBonus(item?.Bounciness ?? 0f, 0.05f, 0.18f, 0.03f, 0.25f),
            () => bonuses.ElasticityBonus = RollBonus(item?.Elasticity ?? 0f, 0.08f, 0.28f, 0.05f, 0.35f),
            () => bonuses.ImpactResistanceBonus = RollBonus(item?.ImpactResistance ?? 0f, 0.1f, 0.35f, 0.08f, 0.45f)
        };

        int attributeCount = UnityEngine.Random.Range(1, availableAttributes.Count + 1);
        foreach (var roll in availableAttributes.OrderBy(_ => UnityEngine.Random.value).Take(attributeCount))
            roll();

        return result;
    }

    private BallForgeSkillData RollSupportSkill(IReadOnlyList<EffectPlayer> availableSkills)
    {
        var targetSkill = availableSkills
            .Where(s => s != null)
            .OrderBy(_ => UnityEngine.Random.value)
            .FirstOrDefault();

        if (targetSkill == null)
            return RollBallShootingTechnique(null);

        return new BallForgeSkillData
        {
            SkillType = BallForgeSkillType.Support,
            BallShootingTechnique = null,
            SupportBonus = new SupportSkillBonus
            {
                SupportedSkillId = targetSkill.ID,
                AdditionalCharges = Mathf.Max(1, UnityEngine.Random.Range(1, 4))
            }
        };
    }

    public void QueuePendingSkill(BallForgeSkillData skill)
    {
        if (skill == null || !skill.HasAnyBonus())
            return;

        if (_pendingSkills.Count >= MaxSkillSlots)
            _pendingSkills.RemoveAt(0);

        _pendingSkills.Add(skill);
    }

    public IEnumerator SaveSkillSlotAsync(int slotIndex, BallForgeSkillRequest request, Action<bool> onComplete)
    {
        if (APIManager.Instance == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        yield return APIManager.Instance.SaveBallForgeSkillCoroutine(slotIndex, request, onComplete);
    }

    public IEnumerator ActivateSkillAsync(BallForgeActivationRequest request, Action<bool> onComplete)
    {
        if (APIManager.Instance == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        yield return APIManager.Instance.ActivateBallForgeSkillCoroutine(request, onComplete);
    }

    private float RollBonus(float baseValue, float minMultiplier, float maxMultiplier, float minFlat, float maxFlat)
    {
        if (baseValue <= 0f)
            return UnityEngine.Random.Range(minFlat, maxFlat);

        float multiplier = UnityEngine.Random.Range(minMultiplier, maxMultiplier);
        return (float)Math.Round(baseValue * multiplier, 3);
    }
}
