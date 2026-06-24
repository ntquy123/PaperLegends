using System.Collections.Generic;
using UnityEngine;

public class ViewSkillHandler : ISkillHandler
{
    private readonly ViewSkillEffectController _effectController;

    public ViewSkillHandler(ViewSkillEffectController effectController)
    {
        _effectController = effectController;
    }

    public EffectPlayerType EffectType => EffectPlayerType.ViewSkill;

    public bool TryActivate(SkillManager manager, List<EffectPlayerSchema> activeEffects)
    {
        if (_effectController == null)
        {
            Debug.LogWarning("[ViewSkill] Effect controller missing.");
            return false;
        }

        bool activated = _effectController.TryActivate();
        if (activated)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_skill"), true);
        }

        return activated;
    }
}

public class CatAnTienSkillHandler : ISkillHandler
{
    public EffectPlayerType EffectType => EffectPlayerType.CatAnTienSkill;

    public bool TryActivate(SkillManager manager, List<EffectPlayerSchema> activeEffects)
    {
        int loginUserId = GameManagerNetWork.Instance.loginUserModel.UserId;
        if (NetworkObjectManager.Instance != null && !NetworkObjectManager.Instance.IsYourTurn(loginUserId))
        {
            var ballObj = NetworkObjectManager.Instance.GetActiveBallObject(loginUserId);
            if (ballObj != null)
            {
                var ballCtr = ballObj.GetComponent<BallServerController>();
                if (ballCtr != null && ballCtr.hasBeenShoot == 1)
                {
                    manager.onClickSkillCatAnTien();
                    NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_skill"), true);
                    return true;
                }
            }
        }

        return false;
    }
}

public class BananaJumpSkillHandler : ISkillHandler
{
    public EffectPlayerType EffectType => EffectPlayerType.BananaJumpSkill;

    public bool TryActivate(SkillManager manager, List<EffectPlayerSchema> activeEffects)
    {
        return manager.TryUseBananaJumpSkill();
    }
}

public class GrazeHitSkillHandler : ISkillHandler
{
    public EffectPlayerType EffectType => EffectPlayerType.GrazeHit;

    public bool TryActivate(SkillManager manager, List<EffectPlayerSchema> activeEffects)
    {
        if (!manager.TryUseGrazeHitSkill())
            return false;

        NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_skill"), true);
        return true;
    }
}

public class BigBallSkillHandler : ISkillHandler
{
    public EffectPlayerType EffectType => EffectPlayerType.BigBallSkill;

    public bool TryActivate(SkillManager manager, List<EffectPlayerSchema> activeEffects)
    {
        if (!manager.TryUseBigBallSkill(activeEffects))
            return false;

        NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_skill"), true);
        return true;
    }
}

public class SmallBallSkillHandler : ISkillHandler
{
    public EffectPlayerType EffectType => EffectPlayerType.SmallBallSkill;

    public bool TryActivate(SkillManager manager, List<EffectPlayerSchema> activeEffects)
    {
        if (!manager.TryUseSmallBallSkill(activeEffects))
            return false;

        NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_skill"), true);
        return true;
    }
}

public class WindBlowSkillHandler : ISkillHandler
{
    public EffectPlayerType EffectType => EffectPlayerType.WindBlowSkill;

    public bool TryActivate(SkillManager manager, List<EffectPlayerSchema> activeEffects)
    {
        if (!manager.TryUseWindBlowSkill())
            return false;

        NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_skill"), true);
        return true;
    }
}

public class HuSkillHandler : ISkillHandler
{
    public EffectPlayerType EffectType => EffectPlayerType.HuSkill;

    public bool TryActivate(SkillManager manager, List<EffectPlayerSchema> activeEffects)
    {
        if (!manager.TryUseHuSkill(activeEffects))
            return false;

        NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_skill"), true);
        return true;
    }
}
