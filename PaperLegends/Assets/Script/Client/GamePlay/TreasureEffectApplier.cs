using System;
using System.Collections;
using UnityEngine;

public enum TreasureType
{
    OneHitKill,
    ExtraTurn,
    BounceBoost,
    FireRateBoost,
    Immunity
}

public class TreasureEffectApplier : MonoBehaviour
{
    public void Apply(Player target, TreasureType type)
    {
        switch (type)
        {
            case TreasureType.OneHitKill:
                target.canInstantKill = true;
                break;
            case TreasureType.ExtraTurn:
                NPCController.Instance.isContinueTurn = true;
                break;
            case TreasureType.BounceBoost:
                target.bounceMultiplier = 3f;
                target.bounceBoostTurns = 2;
                StartCoroutine(EffectCountdown(() => target.bounceBoostTurns, () => target.bounceMultiplier = 1f));
                break;
            case TreasureType.FireRateBoost:
                target.fireRateMultiplier = 2f;
                target.fireRateBoostTurns = 2;
                StartCoroutine(EffectCountdown(() => target.fireRateBoostTurns, () => target.fireRateMultiplier = 1f));
                break;
            case TreasureType.Immunity:
                target.isImmune = true;
                target.immunityTurns = 2;
                StartCoroutine(EffectCountdown(() => target.immunityTurns, () => target.isImmune = false));
                break;
        }
    }

    private IEnumerator EffectCountdown(Func<int> counterGetter, Action resetAction)
    {
        while (counterGetter() > 0)
        {
            yield return null;
        }
        resetAction?.Invoke();
    }
}
