using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BallActiveSkillManager : MonoBehaviour
{
    public static BallActiveSkillManager Instance { get; private set; }

    private const int BallBigBallSkillId = 11400005;
    private const int BallSmallBallSkillId = 11400006;
    private const int BallGroundPinSkillId = 11400007;

    private readonly List<BallPhysicsItem> localBallSkills = new List<BallPhysicsItem>();
    private readonly Dictionary<long, int> limitedSkillUsesBySlot = new Dictionary<long, int>();
    private Image skillImage;
    private Button skillButton;
    private int currentBallIndex = -1;
    private int groundPinSkillPendingSlot = -1;
    private float suppressSkillClickUntil;

    public static BallActiveSkillManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        var existing = FindObjectOfType<BallActiveSkillManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        var go = new GameObject("BallActiveSkillManager");
        Instance = go.AddComponent<BallActiveSkillManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        RefreshGroundPinPendingState();
    }

    public void BindUi(Image image, Button button)
    {
        skillImage = image;
        skillButton = button;
        RefreshUi();
    }

    public void SetLocalBallSkills(List<BallPhysicsItem> physics)
    {
        localBallSkills.Clear();
        limitedSkillUsesBySlot.Clear();
        groundPinSkillPendingSlot = -1;
        if (physics != null)
            localBallSkills.AddRange(physics);
        RefreshUi();
    }

    public void SetCurrentBallIndex(int index)
    {
        if (currentBallIndex == index)
            return;

        if (groundPinSkillPendingSlot != index)
            groundPinSkillPendingSlot = -1;

        currentBallIndex = index;
        RefreshUi();
    }

    public ActiveSkillSchema GetLocalActiveSkill(int slotIndex, int skillId)
    {
        if (slotIndex < 0 || slotIndex >= localBallSkills.Count)
            return null;

        var skillData = localBallSkills[slotIndex];
        var activeSkill = skillData?.activeSkill;
        if (activeSkill == null || activeSkill.GenCode <= 0)
            return null;

        if (skillId > 0 && activeSkill.GenCode != skillId)
            return null;

        return activeSkill;
    }

    public ActiveSkillSchema GetCurrentActiveSkill()
    {
        if (currentBallIndex < 0 || currentBallIndex >= localBallSkills.Count)
            return null;

        var activeSkill = localBallSkills[currentBallIndex]?.activeSkill;
        return activeSkill != null && activeSkill.GenCode > 0 ? activeSkill : null;
    }

    public void SuppressNextSkillClick(float duration = 0.25f)
    {
        suppressSkillClickUntil = Mathf.Max(suppressSkillClickUntil, Time.unscaledTime + Mathf.Max(0.05f, duration));
    }

    private void RefreshUi()
    {
        if (this == null || !isActiveAndEnabled)
            return;

        if (skillImage == null || skillButton == null)
            return;

        skillButton.onClick.RemoveAllListeners();

        BallPhysicsItem skillData = currentBallIndex >= 0 && currentBallIndex < localBallSkills.Count
            ? localBallSkills[currentBallIndex]
            : null;
        bool hasSkill = skillData != null && skillData.activeSkill != null && skillData.activeSkill.GenCode > 0;

        skillImage.gameObject.SetActive(hasSkill);
        skillButton.gameObject.SetActive(hasSkill);

        if (!hasSkill)
            return;

        int slotIndex = currentBallIndex;
        int skillId = skillData.activeSkill.GenCode;
        skillButton.interactable = !IsLimitedBallSkill(skillId) ||
            (GetRemainingSkillUses(slotIndex, skillId) > 0 &&
             (!IsGroundPinSkill(skillId) || groundPinSkillPendingSlot != slotIndex));
        skillButton.onClick.AddListener(() => OnClickBallSkill(slotIndex, skillId));

        StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Root}/Skills/Ball/{skillId}.png", sprite =>
        {
            if (sprite != null && skillImage != null)
                skillImage.sprite = sprite;
        }));
    }

    private void OnClickBallSkill(int slotIndex, int skillId)
    {
        if (Time.unscaledTime <= suppressSkillClickUntil)
        {
            suppressSkillClickUntil = 0f;
            return;
        }

        int playerId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (playerId <= 0 || skillId <= 0)
            return;

        if (slotIndex != currentBallIndex)
        {
            NotificationHelper.Instance?.ShowNotification("Hay chuyen sang vien bi tuong ung de dung ky nang.", false);
            return;
        }

        if (skillId == BallBigBallSkillId)
        {
            TryUseBigBallSkill(slotIndex);
            return;
        }

        if (skillId == BallSmallBallSkillId)
        {
            TryUseSmallBallSkill(slotIndex);
            return;
        }

        if (skillId == BallGroundPinSkillId)
        {
            TryUseGroundPinSkill(slotIndex);
            return;
        }

        PlayUseSkillSound();
        GameManagerNetWork.Instance?.serverRPC?.RpcSyncSkillUsage(playerId, skillId);
    }

    private void PlayUseSkillSound()
    {
        SoundManager.Instance?.PlayUseSkill();
    }

    private bool TryUseBigBallSkill(int slotIndex)
    {
        var serverRPC = GameManagerNetWork.Instance?.serverRPC;
        if (serverRPC == null)
            return false;

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        if (!serverRPC.IsYourTurn(playerId))
            return false;

        if (!EnsureSkillUseAvailable(slotIndex, BallBigBallSkillId))
            return false;

        var ballData = slotIndex >= 0 && slotIndex < localBallSkills.Count ? localBallSkills[slotIndex] : null;
        float level = Mathf.Max(1, ballData != null ? ballData.level : 1);
        const float scalePerLevel = 0.15f;
        float multiplier = 1f + level * scalePerLevel;

        var ballObj = NetworkObjectManager.Instance?.GetActiveBallObject(playerId);
        if (ballObj == null)
            return false;

        var ballCtrl = ballObj.GetComponent<BallServerController>();
        if (ballCtrl == null)
            return false;

        serverRPC.RpcRequestUseBigBallSkill(playerId, multiplier);
#if !UNITY_SERVER
        ballCtrl.ApplyScaleMultiplier(multiplier);
#endif
        ConsumeLimitedSkillUse(slotIndex, BallBigBallSkillId);
        serverRPC.RpcSyncSkillUsage(playerId, BallBigBallSkillId);
        RefreshUi();
        return true;
    }

    private bool TryUseSmallBallSkill(int slotIndex)
    {
        var serverRPC = GameManagerNetWork.Instance?.serverRPC;
        if (serverRPC == null)
            return false;

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        if (!serverRPC.IsYourTurn(playerId))
            return false;

        if (!EnsureSkillUseAvailable(slotIndex, BallSmallBallSkillId))
            return false;

        var ballData = slotIndex >= 0 && slotIndex < localBallSkills.Count ? localBallSkills[slotIndex] : null;
        float level = Mathf.Max(1, ballData != null ? ballData.level : 1);
        const float scalePerLevel = 0.1f;
        float multiplier = Mathf.Max(0.3f, 1f - level * scalePerLevel);

        var ballObj = NetworkObjectManager.Instance?.GetActiveBallObject(playerId);
        if (ballObj == null)
            return false;

        var ballCtrl = ballObj.GetComponent<BallServerController>();
        if (ballCtrl == null)
            return false;

        serverRPC.RpcRequestUseSmallBallSkill(playerId, multiplier);
#if !UNITY_SERVER
        ballCtrl.ApplyScaleMultiplier(multiplier);
#endif
        ConsumeLimitedSkillUse(slotIndex, BallSmallBallSkillId);
        serverRPC.RpcSyncSkillUsage(playerId, BallSmallBallSkillId);
        RefreshUi();
        return true;
    }

    private bool TryUseGroundPinSkill(int slotIndex)
    {
        var serverRPC = GameManagerNetWork.Instance?.serverRPC;
        if (serverRPC == null)
            return false;

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        if (!serverRPC.IsYourTurn(playerId))
            return false;

        if (!EnsureSkillUseAvailable(slotIndex, BallGroundPinSkillId))
            return false;

        var ballObj = NetworkObjectManager.Instance?.GetActiveBallObject(playerId);
        if (ballObj == null)
            return false;

        var ballCtrl = ballObj.GetComponent<BallServerController>();
        if (ballCtrl == null)
            return false;

        if (groundPinSkillPendingSlot == slotIndex && ballCtrl.hasBeenShoot == 0 && ballCtrl.IsHolding == 1)
        {
            NotificationHelper.Instance?.ShowNotification("Ky nang xoay cam da san sang cho lan ban nay.", false);
            return false;
        }

        if (ballCtrl.hasBeenShoot != 0 || ballCtrl.IsHolding != 1)
        {
            groundPinSkillPendingSlot = -1;
            NotificationHelper.Instance?.ShowNotification("Chi dung ky nang xoay cam truoc khi ban.", false);
            RefreshUi();
            return false;
        }

        serverRPC.RpcRequestUseGroundPinSkill(playerId);
        ConsumeLimitedSkillUse(slotIndex, BallGroundPinSkillId);
        groundPinSkillPendingSlot = slotIndex;
        PlayUseSkillSound();
        serverRPC.RpcSyncSkillUsage(playerId, BallGroundPinSkillId);
        RefreshUi();
        return true;
    }

    private void RefreshGroundPinPendingState()
    {
        if (groundPinSkillPendingSlot < 0)
            return;

        int playerId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        var serverRPC = GameManagerNetWork.Instance?.serverRPC;
        if (playerId <= 0 || serverRPC == null || !serverRPC.IsYourTurn(playerId))
        {
            ClearGroundPinPendingState(true);
            return;
        }

        var ballObj = NetworkObjectManager.Instance?.GetActiveBallObject(playerId);
        var ballCtrl = ballObj != null ? ballObj.GetComponent<BallServerController>() : null;
        if (ballCtrl == null || ballCtrl.hasBeenShoot == 1 || ballCtrl.IsHolding == 0)
        {
            ClearGroundPinPendingState(true);
        }
    }

    private void ClearGroundPinPendingState(bool refresh)
    {
        if (groundPinSkillPendingSlot < 0)
            return;

        groundPinSkillPendingSlot = -1;
        if (refresh)
            RefreshUi();
    }

    private bool EnsureSkillUseAvailable(int slotIndex, int skillId)
    {
        if (!IsLimitedBallSkill(skillId))
            return true;

        if (GetRemainingSkillUses(slotIndex, skillId) > 0)
            return true;

        NotificationHelper.Instance?.ShowNotification("Ky nang vien bi da het so lan dung.", false);
        RefreshUi();
        return false;
    }

    private void ConsumeLimitedSkillUse(int slotIndex, int skillId)
    {
        if (!IsLimitedBallSkill(skillId))
            return;

        long key = BuildSkillUseKey(slotIndex, skillId);
        int current = limitedSkillUsesBySlot.TryGetValue(key, out int used) ? used : 0;
        limitedSkillUsesBySlot[key] = Mathf.Min(current + 1, GetMaxSkillUses(slotIndex));
    }

    private int GetRemainingSkillUses(int slotIndex, int skillId)
    {
        int maxUses = GetMaxSkillUses(slotIndex);
        int used = limitedSkillUsesBySlot.TryGetValue(BuildSkillUseKey(slotIndex, skillId), out int value) ? value : 0;
        return Mathf.Max(0, maxUses - used);
    }

    private int GetMaxSkillUses(int slotIndex)
    {
        BallPhysicsItem ballData = slotIndex >= 0 && slotIndex < localBallSkills.Count ? localBallSkills[slotIndex] : null;
        float level = Mathf.Max(1, ballData != null ? ballData.level : 1);

        if (level >= 10f)
            return 3;
        if (level >= 5f)
            return 2;
        return 1;
    }

    private static long BuildSkillUseKey(int slotIndex, int skillId)
    {
        return ((long)slotIndex << 32) ^ (uint)skillId;
    }

    private static bool IsLimitedBallSkill(int skillId)
    {
        return skillId == BallBigBallSkillId ||
               skillId == BallSmallBallSkillId ||
               skillId == BallGroundPinSkillId;
    }

    private static bool IsGroundPinSkill(int skillId)
    {
        return skillId == BallGroundPinSkillId;
    }
}
