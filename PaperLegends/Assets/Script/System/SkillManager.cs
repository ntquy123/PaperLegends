using System.Collections;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using Fusion.Addons.Physics;


public class SkillManager : MonoBehaviour
{
    private const int BallPowerSkillId = 11400001;
    private const int BallSpinSkillId = 11400002;
    private const int BallMentalitySkillId = 11400003;
    private const int BallGrazeHitSkillId = 11400004;
    private const int BallSkillIdMin = 11400000;
    private const int BallSkillIdMaxExclusive = 11500000;
    private const int PaperLegendHero10000001ModelId = 10000001;
    private const int PaperLegendHero10000004ModelId = 10000004;
    private const int PaperLegendMaxSkillSlots = 4;
    public static SkillManager Instance;
    // Kỹ năng đã sử dụng trong lượt hiện tại của từng người chơi
    public readonly Dictionary<int, List<int>> skillsUsedThisTurn = new Dictionary<int, List<int>>();
    // Danh sách icon kỹ năng hiển thị vĩnh viễn cho từng người chơi
    public readonly Dictionary<int, List<int>> permanentSkillIcons = new Dictionary<int, List<int>>();
    // Lưu trữ các kỹ năng đã dùng theo từng người chơi
    public readonly Dictionary<int, List<int>> skillsUsedByPlayer = new Dictionary<int, List<int>>();
    private readonly Dictionary<int, HashSet<int>> counteredSkillUseIndexesByPlayer = new Dictionary<int, HashSet<int>>();
    private readonly Dictionary<int, HashSet<int>> counteredSkillUseIndexesThisTurn = new Dictionary<int, HashSet<int>>();
    private readonly HashSet<int> hasKillPermissionIcon = new HashSet<int>();
    private readonly Dictionary<int, SkillBaseStats> _skillBaseStats = new Dictionary<int, SkillBaseStats>();
    private readonly Dictionary<int, Dictionary<int, int>> _skillChargesRemaining = new Dictionary<int, Dictionary<int, int>>();
    private readonly Dictionary<int, List<PaperLegendHeroSkillData>> _paperLegendSkillsByModelId = new Dictionary<int, List<PaperLegendHeroSkillData>>();
    private readonly HashSet<int> _paperLegendSkillLoadsInProgress = new HashSet<int>();
    [Header("CONFIG EFFECT")]
    public float DistanceForChamCat = 0.5f;
    [Header("ChamCat Context Action")]
    [SerializeField] private float chamCatHoldDuration = 20f;
    [SerializeField] private Vector2 chamCatHoldUISize = new Vector2(80f, 80f);
    private CanvasGroup _chamCatHoldGroup;
    private Image _chamCatHoldFill;
    private Coroutine _chamCatHoldCoroutine;
    private BallServerController _chamCatHoldTarget;
    [Header("SKILL UI CONFIG")]
    public Transform SkillListPanel;
    public Transform SkillListNormalPanel;
    public Transform SkillUsedListPanel;
    public GameObject skillItemPrefab;
    public GameObject skillUsedItemPrefab;
    [SerializeField, Tooltip("Icon cấm phủ lên skill đã bị khắc chế. Có thể kéo prefab icon vào đây, hoặc tạo child tên CounteredSkillBanIcon trong skillUsedItemPrefab.")]
    private GameObject counteredSkillBanIconPrefab;
    [Header("PAPER LEGENDS SKILL UI")]
    [SerializeField, Tooltip("Horizontal layout group contains the 4 Paper Legends skills.")]
    private Transform paperLegendSkillLayoutRoot;
    [SerializeField, Tooltip("Prefab for one Paper Legends skill slot. It must include PaperLegendSkillSlotView.")]
    private PaperLegendSkillSlotView paperLegendSkillSlotPrefab;
    private bool useRadialSkills = false;
    [SerializeField] private RectTransform shootButtonTarget;
    [SerializeField] private RadialSkills radialSkills = new RadialSkills();
    [SerializeField] private ViewSkillEffectController viewSkillEffectController;
    [Header("VIEW SKILL VFX")]
    [SerializeField] private GameObject viewSkillVfxPrefab;
    private readonly Dictionary<int, ISkillHandler> _skillHandlers = new Dictionary<int, ISkillHandler>();
    private readonly ISkillHandler _defaultSkillHandler = new DefaultSkillHandler();
    private readonly List<PaperLegendSkillSlotView> _paperLegendSkillSlotViews = new List<PaperLegendSkillSlotView>();
    private int _lastPaperLegendSkillUiModelId = -1;
    private int _lastPaperLegendSkillUiLevel = -1;
    private int _lastPaperLegendSkillUiPointCount = -1;
    private int _lastPaperLegendSkillUiSlot1 = -1;
    private int _lastPaperLegendSkillUiSlot2 = -1;
    private int _lastPaperLegendSkillUiSlot3 = -1;
    private int _lastPaperLegendSkillUiSlot4 = -1;
    private int _lastPaperLegendSkillUiCooldown1 = -1;
    private int _lastPaperLegendSkillUiCooldown2 = -1;
    private int _lastPaperLegendSkillUiCooldown3 = -1;
    private int _lastPaperLegendSkillUiCooldown4 = -1;

    private class SkillBaseStats
    {
        public float Power;
        public float Spin;
        public float Bounciness;
        public float Mass;
    }
 
    private void Awake()
    {
        Instance = this;
        if (viewSkillEffectController == null)
            viewSkillEffectController = new ViewSkillEffectController();
        viewSkillEffectController.Initialize(this);
        viewSkillEffectController.SetViewSkillVfxPrefab(viewSkillVfxPrefab);
        InitializeSkillHandlers();
        radialSkills.Initialize(SkillListPanel as RectTransform, ResolveShootButtonTarget());
    }

    private void Update()
    {
        RefreshPaperLegendSkillListIfChanged();
    }

    private void RefreshPaperLegendSkillListIfChanged()
    {
        if (!PaperLegendRuntimeState.IsPaperLegendMatch)
        {
            ResetPaperLegendSkillUiSignature();
            return;
        }

        if (!TryGetLocalPaperLegendHandler(out PaperLegendCharacterNetworkHandler handler))
            return;

        int modelId = handler.CharacterModelId;
        int level = handler.Level;
        int points = handler.SkillUpgradePoints;
        int slot1 = handler.Skill1Level;
        int slot2 = handler.Skill2Level;
        int slot3 = handler.Skill3Level;
        int slot4 = handler.Skill4Level;
        int cooldown1 = ResolvePaperLegendCooldownUiSeconds(handler, 1);
        int cooldown2 = ResolvePaperLegendCooldownUiSeconds(handler, 2);
        int cooldown3 = ResolvePaperLegendCooldownUiSeconds(handler, 3);
        int cooldown4 = ResolvePaperLegendCooldownUiSeconds(handler, 4);

        if (_lastPaperLegendSkillUiModelId == modelId &&
            _lastPaperLegendSkillUiLevel == level &&
            _lastPaperLegendSkillUiPointCount == points &&
            _lastPaperLegendSkillUiSlot1 == slot1 &&
            _lastPaperLegendSkillUiSlot2 == slot2 &&
            _lastPaperLegendSkillUiSlot3 == slot3 &&
            _lastPaperLegendSkillUiSlot4 == slot4 &&
            _lastPaperLegendSkillUiCooldown1 == cooldown1 &&
            _lastPaperLegendSkillUiCooldown2 == cooldown2 &&
            _lastPaperLegendSkillUiCooldown3 == cooldown3 &&
            _lastPaperLegendSkillUiCooldown4 == cooldown4)
        {
            return;
        }

        _lastPaperLegendSkillUiModelId = modelId;
        _lastPaperLegendSkillUiLevel = level;
        _lastPaperLegendSkillUiPointCount = points;
        _lastPaperLegendSkillUiSlot1 = slot1;
        _lastPaperLegendSkillUiSlot2 = slot2;
        _lastPaperLegendSkillUiSlot3 = slot3;
        _lastPaperLegendSkillUiSlot4 = slot4;
        _lastPaperLegendSkillUiCooldown1 = cooldown1;
        _lastPaperLegendSkillUiCooldown2 = cooldown2;
        _lastPaperLegendSkillUiCooldown3 = cooldown3;
        _lastPaperLegendSkillUiCooldown4 = cooldown4;

        ShowPaperLegendSkillList();
    }

    private static int ResolvePaperLegendCooldownUiSeconds(PaperLegendCharacterNetworkHandler handler, int slot)
    {
        if (handler == null)
            return 0;

        float cooldownRemaining = handler.GetSkillCooldownRemaining(slot);
        return cooldownRemaining > 0.01f ? Mathf.CeilToInt(cooldownRemaining) : 0;
    }

    private void ResetPaperLegendSkillUiSignature()
    {
        _lastPaperLegendSkillUiModelId = -1;
        _lastPaperLegendSkillUiLevel = -1;
        _lastPaperLegendSkillUiPointCount = -1;
        _lastPaperLegendSkillUiSlot1 = -1;
        _lastPaperLegendSkillUiSlot2 = -1;
        _lastPaperLegendSkillUiSlot3 = -1;
        _lastPaperLegendSkillUiSlot4 = -1;
        _lastPaperLegendSkillUiCooldown1 = -1;
        _lastPaperLegendSkillUiCooldown2 = -1;
        _lastPaperLegendSkillUiCooldown3 = -1;
        _lastPaperLegendSkillUiCooldown4 = -1;
    }

    public void ClearSkillsUsedThisTurn(int playerId)
    {
        if (skillsUsedThisTurn.ContainsKey(playerId))
        {
            skillsUsedThisTurn[playerId].Clear();
        }

        counteredSkillUseIndexesThisTurn.Remove(playerId);
    }

    public void AddPermanentSkillIcon(int playerId, int skillId)
    {
        if (!permanentSkillIcons.ContainsKey(playerId))
        {
            permanentSkillIcons[playerId] = new System.Collections.Generic.List<int>();
        }
        var list = permanentSkillIcons[playerId];
        if (!list.Contains(skillId))
            list.Insert(0, skillId);
    }

    public static bool IsGrazeHitSkillId(int skillId)
    {
        return skillId == (int)EffectPlayerType.GrazeHit ||
               skillId == (int)EffectPlayerType.HeavyBallSkill ||
               skillId == BallGrazeHitSkillId;
    }

    public void RecordSkillUsed(int playerId, int skillId, bool isCountered = false)
    {
        if (!skillsUsedByPlayer.TryGetValue(playerId, out var list))
        {
            list = new System.Collections.Generic.List<int>();
            skillsUsedByPlayer[playerId] = list;
        }

        int index = list.Count;
        list.Add(skillId);

        if (isCountered)
        {
            AddCounteredSkillIndex(counteredSkillUseIndexesByPlayer, playerId, index);
        }
    }

    public void OnSkillUsedByPlayer(int playerId, int skillId)
    {
        OnSkillUsedByPlayer(playerId, skillId, false);
    }

    public void OnSkillUsedByPlayer(int playerId, int skillId, bool isCountered)
    {
        if (!skillsUsedThisTurn.TryGetValue(playerId, out var skillsThisTurn))
        {
            skillsThisTurn = new List<int>();
            skillsUsedThisTurn[playerId] = skillsThisTurn;
        }

        int turnIndex = skillsThisTurn.Count;
        skillsThisTurn.Add(skillId);
        if (isCountered)
        {
            AddCounteredSkillIndex(counteredSkillUseIndexesThisTurn, playerId, turnIndex);
        }

        RecordSkillUsed(playerId, skillId, isCountered);

        var networkManager = GameManagerNetWork.Instance;
        if (networkManager != null && networkManager.loginUserModel != null && networkManager.loginUserModel.UserId == playerId)
        {
            ShowSkillUsedList();
        }
    }

    public void ClearSkillUsageHistory()
    {
        skillsUsedByPlayer.Clear();
        skillsUsedThisTurn.Clear();
        counteredSkillUseIndexesByPlayer.Clear();
        counteredSkillUseIndexesThisTurn.Clear();
    }

    private static void AddCounteredSkillIndex(Dictionary<int, HashSet<int>> map, int playerId, int index)
    {
        if (!map.TryGetValue(playerId, out var indexes))
        {
            indexes = new HashSet<int>();
            map[playerId] = indexes;
        }

        indexes.Add(index);
    }

    private Dictionary<int, int> GetOrCreateChargeMap(int playerId)
    {
        if (!_skillChargesRemaining.TryGetValue(playerId, out var map))
        {
            map = new Dictionary<int, int>();
            _skillChargesRemaining[playerId] = map;
        }

        return map;
    }

    private void InitializeChargesForPlayer(int playerId, IEnumerable<EffectPlayerSchema> effects)
    {
        var map = GetOrCreateChargeMap(playerId);
        if (effects == null)
            return;

        foreach (var effect in effects)
        {
            if (!map.ContainsKey(effect.effectId) || map[effect.effectId] > effect.charges)
            {
                map[effect.effectId] = Mathf.Max(0, effect.charges);
            }
        }
    }

    private bool HasAvailableCharge(int playerId, int skillId)
    {
        var map = GetOrCreateChargeMap(playerId);
        return map.TryGetValue(skillId, out var remaining) ? remaining > 0 : true;
    }

    private void ConsumeCharge(int playerId, int skillId)
    {
        var map = GetOrCreateChargeMap(playerId);
        if (!map.ContainsKey(skillId))
            map[skillId] = 0;

        map[skillId] = Mathf.Max(0, map[skillId] - 1);
    }

    private List<EffectPlayerSchema> GetActiveEffectsForPlayer(int playerId)
    {
        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null)
            return null;

        var playerGO = serverRPC.GetPlayerObject(playerId);
        var handler = playerGO != null ? playerGO.GetComponent<PlayerNetworkHandler>() : null;
        return handler != null ? handler.ActiveEffects.Where(x => x.IsEquiped).ToList() : null;
    }

    private void PlaySkillClickSound()
    {
        SoundManager.Instance?.PlayUseSkill();
    }

    private static bool UsesCustomScaleSkillSound(int typeSkill)
    {
        return typeSkill == (int)EffectPlayerType.BigBallSkill ||
               typeSkill == (int)EffectPlayerType.SmallBallSkill;
    }

    private void UpdateChargeLabel(Transform skillItem, string label)
    {
        var chargesTransform = skillItem != null ? skillItem.Find("Charges") : null;
        if (chargesTransform == null)
            return;

        if (chargesTransform.TryGetComponent<Text>(out var text))
        {
            text.text = label;
        }
        else if (chargesTransform.TryGetComponent<TMP_Text>(out var tmpText))
        {
            tmpText.text = label;
        }
    }

    private void ApplyDepletedVisual(Image skillImage, Button button)
    {
        if (button != null)
            button.interactable = false;

        if (skillImage != null)
        {
            skillImage.transform.DOKill();
            skillImage.DOKill();
            skillImage.color = Color.black;
        }
    }

    public void EndTurnCleanup(int playerId)
    {
        var ballObj = NetworkObjectManager.Instance?.GetActiveBallObject(playerId);
        ballObj?.SendMessage("OnEndTurn", SendMessageOptions.DontRequireReceiver);

        if (skillsUsedByPlayer.TryGetValue(playerId, out var list))
        {
            list.Remove((int)EffectPlayerType.CatAnTienSkill);
        }

        skillsUsedThisTurn.Remove((int)EffectPlayerType.CatAnTienSkill);

        var playerGO = NetworkObjectManager.Instance?.GetPlayerObject(playerId);
        if (playerGO != null)
        {
            var model = playerGO.GetComponent<PlayerNetworkHandler>().PlayerModel;
            if (model.isCatAnTienActive == 1)
            {
                model.isCatAnTienActive = 0;
                playerGO.GetComponent<PlayerNetworkHandler>().PlayerModel = model;
            }
        }

        ShowSkillUsedList();
    }

    public void OnClickSkill(int typeSkill)
    {
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        var activeEffects = GetActiveEffectsForPlayer(playerId);
        InitializeChargesForPlayer(playerId, activeEffects);

        if (!HasAvailableCharge(playerId, typeSkill))
        {
            ShowSkilldList();
            return;
        }

        bool skillActivated = TryActivateSkill(typeSkill, activeEffects);
        if (!skillActivated)
            return;

        ConsumeCharge(playerId, typeSkill);
        if (!UsesCustomScaleSkillSound(typeSkill))
            PlaySkillClickSound();

        GameManagerNetWork.Instance.serverRPC.RpcSyncSkillUsage(playerId, typeSkill);
        ShowSkilldList();
        //ShowSkillUsedList();
    }
    public void onClickSkillCatAnTien()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        manager.RpcRequestActivateCatAnTienSkill(playerId);
    }

    public void OnClickSkillChamCat(BallServerController target = null)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        if (!HasChamCatPermission(playerId))
            return;

        if (target != null && !ShouldShowChamCatIconForTarget(playerId, target))
            return;

        var playerGO = manager.GetPlayerObject(playerId);
        if (playerGO != null)
            CameraRotation.Instance?.StartFollowingPlayerOnline(playerGO.transform);

        PlaySkillClickSound();
        StartCoroutine(PlayChamCatSkillSequence(playerId, manager, target != null ? target.playerId : 0));

        ShowSkilldList();
        ClientGameplayBridge.UI.ShowPlayerList();
    }

    public bool BeginChamCatHold(BallServerController target)
    {
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        if (!ShouldShowChamCatIconForTarget(playerId, target))
            return false;

        CancelChamCatHold();
        OnClickSkillChamCat(target);
        return true;
    }

    public void CancelChamCatHold()
    {
        if (_chamCatHoldCoroutine != null)
        {
            StopCoroutine(_chamCatHoldCoroutine);
            _chamCatHoldCoroutine = null;
        }

        _chamCatHoldTarget = null;
        SetChamCatHoldVisible(false);
    }

    private IEnumerator ChamCatHoldRoutine()
    {
        EnsureChamCatHoldUI();
        SetChamCatHoldVisible(true);
        float elapsed = 0f;

        while (elapsed < chamCatHoldDuration)
        {
            if (_chamCatHoldTarget == null)
            {
                CancelChamCatHold();
                yield break;
            }

            int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
            if (!ShouldShowChamCatIconForTarget(playerId, _chamCatHoldTarget))
            {
                CancelChamCatHold();
                yield break;
            }

            elapsed += Time.deltaTime;
            UpdateChamCatHoldProgress(elapsed / chamCatHoldDuration);
            yield return null;
        }

        SetChamCatHoldVisible(false);
        _chamCatHoldCoroutine = null;
        var selectedTarget = _chamCatHoldTarget;
        _chamCatHoldTarget = null;
        NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_skill"), true);
        OnClickSkillChamCat(selectedTarget);
    }

    private void EnsureChamCatHoldUI()
    {
        if (_chamCatHoldGroup != null)
            return;

        Transform root = null;
        if (SkillListPanel != null)
            root = SkillListPanel.root;

        if (root == null && ClientGameplayBridge.UI.HasInstance())
            root = ClientGameplayBridge.UI.GetCanvasTransform();

        if (root == null)
            return;

        var holder = new GameObject("ChamCatHoldUI", typeof(RectTransform), typeof(CanvasGroup));
        var holderRect = holder.GetComponent<RectTransform>();
        holderRect.SetParent(root, false);
        holderRect.anchorMin = new Vector2(0.5f, 0.5f);
        holderRect.anchorMax = new Vector2(0.5f, 0.5f);
        holderRect.anchoredPosition = Vector2.zero;
        holderRect.sizeDelta = chamCatHoldUISize;

        _chamCatHoldGroup = holder.GetComponent<CanvasGroup>();
        _chamCatHoldGroup.alpha = 0f;
        _chamCatHoldGroup.blocksRaycasts = false;
        _chamCatHoldGroup.interactable = false;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.SetParent(holderRect, false);
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = chamCatHoldUISize;

        var backgroundImage = background.GetComponent<Image>();
        backgroundImage.sprite = PaperLegendUiSpriteFactory.GetSolidSprite();
        backgroundImage.color = new Color(1f, 1f, 1f, 0.2f);
        backgroundImage.type = Image.Type.Simple;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.SetParent(holderRect, false);
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = chamCatHoldUISize;

        _chamCatHoldFill = fill.GetComponent<Image>();
        _chamCatHoldFill.sprite = PaperLegendUiSpriteFactory.GetSolidSprite();
        _chamCatHoldFill.color = new Color(1f, 1f, 1f, 0.9f);
        _chamCatHoldFill.type = Image.Type.Filled;
        _chamCatHoldFill.fillMethod = Image.FillMethod.Radial360;
        _chamCatHoldFill.fillOrigin = 2;
        _chamCatHoldFill.fillClockwise = false;
        _chamCatHoldFill.fillAmount = 0f;
    }

    private void SetChamCatHoldVisible(bool isVisible)
    {
        if (_chamCatHoldGroup == null)
            return;

        _chamCatHoldGroup.alpha = isVisible ? 1f : 0f;
        UpdateChamCatHoldProgress(0f);
    }

    private void UpdateChamCatHoldProgress(float progress)
    {
        if (_chamCatHoldFill != null)
            _chamCatHoldFill.fillAmount = Mathf.Clamp01(progress);
    }

    private IEnumerator PlayChamCatSkillSequence(int playerId, NetworkObjectManager manager, int targetPlayerId = 0)
    {
        if (manager == null)
            yield break;

        var playerGO = manager.GetPlayerObject(playerId);
        var handler = playerGO != null ? playerGO.GetComponent<PlayerNetworkHandler>() : null;
        var previousAnimState = handler != null ? handler.CurrentAnimState : CharacterAnimState.None;
        if (handler != null)
            handler.CurrentAnimState = CharacterAnimState.PickingUp;

        yield return null;

        var visual = playerGO != null ? playerGO.GetComponentInChildren<PlayerModelVisualComponent>() : null;
        var animator = visual != null ? visual.Animator : playerGO != null ? playerGO.GetComponentInChildren<Animator>() : null;
        if (animator != null)
        {
            var clipInfos = animator.GetCurrentAnimatorClipInfo(0);
            float clipLength = clipInfos != null && clipInfos.Length > 0 && clipInfos[0].clip != null
                ? clipInfos[0].clip.length
                : 0f;

            if (clipLength > 0f)
                yield return new WaitForSeconds(clipLength);
        }

        if (handler != null && handler.CurrentAnimState == CharacterAnimState.PickingUp)
            handler.CurrentAnimState = previousAnimState;

        manager.RpcRequestUseChamCatSkill(playerId, targetPlayerId);
        StartCoroutine(RestoreFppAfterChamCat(playerId));
    }

    private IEnumerator RestoreFppAfterChamCat(int playerId)
    {
        yield return new WaitForSeconds(0.15f);

        var serverRPC = GameManagerNetWork.Instance?.serverRPC;
        if (serverRPC == null || !serverRPC.IsYourTurn(playerId))
            yield break;

        var playerObj = serverRPC.GetPlayerObject(playerId);
        var handler = playerObj != null ? playerObj.GetComponent<PlayerNetworkHandler>() : null;
        if (handler == null || handler.FPPPosition == null || handler.PointPosition == null)
            yield break;

        if (!(UIControllerOnline.Instance?.MoveCameraToCurrentFirstPersonView(handler) ?? false))
            CameraRotation.Instance?.MoveCameraToFPPOnline(handler.FPPPosition, handler.PointPosition);
        UIControllerOnline.Instance?.UIforPlayNormalOnline();
        UIControllerOnline.Instance?.StartTurnCountdown();
    }

    public bool TryUseBananaJumpSkill()
    {
        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null)
        {
            Debug.LogWarning("[BananaJump][Client] Cannot use Banana Jump skill because serverRPC is missing.");
            return false;
        }

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        Debug.Log($"[BananaJump][Client] Player {playerId} is attempting to use Banana Jump skill.");
        if (!serverRPC.IsYourTurn(playerId))
        {
            Debug.LogWarning($"[BananaJump][Client] Player {playerId} cannot use Banana Jump skill because it is not their turn.");
            return false;
        }

        var playerGO = serverRPC.GetPlayerObject(playerId);
        if (playerGO == null)
        {
            Debug.LogWarning($"[BananaJump][Client] Player {playerId} cannot use Banana Jump skill because the player object is missing.");
            return false;
        }

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
        {
            Debug.LogWarning($"[BananaJump][Client] Player {playerId} cannot use Banana Jump skill because PlayerNetworkHandler is missing.");
            return false;
        }

        if (handler.CurrentAnimState != CharacterAnimState.Running || handler.IsBananaJumpActive)
        {
            Debug.LogWarning($"[BananaJump][Client] Player {playerId} cannot use Banana Jump skill. CurrentAnimState={handler.CurrentAnimState}, IsBananaJumpActive={handler.IsBananaJumpActive}.");
            return false;
        }

        Debug.Log($"[BananaJump][Client] Sending Banana Jump RPC for player {playerId}.");
        serverRPC.RpcRequestUseBananaJumpSkill(playerId);
        ShowSkilldList();
        return true;
    }

    public bool TryUseBigBallSkill(List<EffectPlayerSchema> activeEffects)
    {
        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null)
        {
            Debug.LogWarning("[BigBall][Client] Cannot use Big Ball skill because serverRPC is missing.");
            return false;
        }

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        if (!serverRPC.IsYourTurn(playerId))
        {
            Debug.LogWarning("[BigBall][Client] Cannot use Big Ball skill outside of your turn.");
            return false;
        }

        var effectData = activeEffects?.FirstOrDefault(e => e.effectId == (int)EffectPlayerType.BigBallSkill);
        if (effectData == null)
        {
            Debug.LogWarning("[BigBall][Client] Skill data not found in active effects.");
            return false;
        }

        float level = Mathf.Max(1, effectData.level);
        const float scalePerLevel = 0.15f;
        float multiplier = 1f + level * scalePerLevel;

        var ballObj = NetworkObjectManager.Instance?.GetActiveBallObject(playerId);
        if (ballObj == null)
        {
            Debug.LogWarning("[BigBall][Client] No active ball found for the player.");
            return false;
        }

        var ballCtrl = ballObj.GetComponent<BallServerController>();
        if (ballCtrl == null)
        {
            Debug.LogWarning("[BigBall][Client] Ball controller missing on active ball.");
            return false;
        }

        serverRPC.RpcRequestUseBigBallSkill(playerId, multiplier);
#if !UNITY_SERVER
        ballCtrl.ApplyScaleMultiplier(multiplier);
#endif
        return true;
    }

    public bool TryUseSmallBallSkill(List<EffectPlayerSchema> activeEffects)
    {
        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null)
        {
            Debug.LogWarning("[SmallBall][Client] Cannot use Small Ball skill because serverRPC is missing.");
            return false;
        }

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        if (!serverRPC.IsYourTurn(playerId))
        {
            Debug.LogWarning("[SmallBall][Client] Cannot use Small Ball skill outside of your turn.");
            return false;
        }

        var effectData = activeEffects?.FirstOrDefault(e => e.effectId == (int)EffectPlayerType.SmallBallSkill);
        if (effectData == null)
        {
            Debug.LogWarning("[SmallBall][Client] Skill data not found in active effects.");
            return false;
        }

        float level = Mathf.Max(1, effectData.level);
        const float scalePerLevel = 0.1f;
        float multiplier = Mathf.Max(0.3f, 1f - level * scalePerLevel);

        var ballObj = NetworkObjectManager.Instance?.GetActiveBallObject(playerId);
        if (ballObj == null)
        {
            Debug.LogWarning("[SmallBall][Client] No active ball found for the player.");
            return false;
        }

        var ballCtrl = ballObj.GetComponent<BallServerController>();
        if (ballCtrl == null)
        {
            Debug.LogWarning("[SmallBall][Client] Ball controller missing on active ball.");
            return false;
        }

        serverRPC.RpcRequestUseSmallBallSkill(playerId, multiplier);
#if !UNITY_SERVER
        ballCtrl.ApplyScaleMultiplier(multiplier);
#endif
        return true;
    }

    public bool TryUseWindBlowSkill()
    {
        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null)
        {
            Debug.LogWarning("[WindBlow][Client] Cannot use Wind Blow skill because serverRPC is missing.");
            return false;
        }

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        if (!serverRPC.IsYourTurn(playerId))
        {
            Debug.LogWarning("[WindBlow][Client] Cannot use Wind Blow skill outside of your turn.");
            return false;
        }

        if (!IsWindBlowSkillReady(playerId))
        {
            Debug.LogWarning("[WindBlow][Client] Ball is not moving enough to trigger Wind Blow.");
            return false;
        }

        var playerGO = serverRPC.GetPlayerObject(playerId);
        if (playerGO != null)
        {
            CameraRotation.Instance?.StartFollowingPlayerOnline(playerGO.transform);
        }

        serverRPC.RpcRequestUseWindBlowSkill(playerId);
        ShowSkilldList();
        return true;
    }

    public bool TryUseHuSkill(List<EffectPlayerSchema> activeEffects)
    {
        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null)
        {
            Debug.LogWarning("[HuSkill][Client] Cannot use Hu skill because serverRPC is missing.");
            return false;
        }

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
 

        if (!TryResolveCurrentTurnTargetId(out int targetId))
        {
            Debug.LogWarning("[HuSkill][Client] Cannot resolve target for Hu skill.");
            return false;
        }

        if (targetId == playerId)
        {
            Debug.LogWarning("[HuSkill][Client] Cannot use Hu skill on yourself.");
            return false;
        }

        var effectData = activeEffects?.FirstOrDefault(e => e.effectId == (int)EffectPlayerType.HuSkill);
        if (effectData == null)
        {
            Debug.LogWarning("[HuSkill][Client] Skill data not found in active effects.");
            return false;
        }

        int level = Mathf.Clamp(effectData.level, 1, 3);
        serverRPC.RpcRequestUseHuSkill(playerId, level);
        ShowSkilldList();
        return true;
    }

    public bool TryUseGrazeHitSkill()
    {
        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null)
        {
            Debug.LogWarning("[GrazeHit][Client] Cannot use GrazeHit because serverRPC is missing.");
            return false;
        }

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        if (!serverRPC.IsYourTurn(playerId))
        {
            Debug.LogWarning("[GrazeHit][Client] Cannot use GrazeHit outside of your turn.");
            return false;
        }

        var handler = GetPlayerNetworkHandler(playerId);
        if (handler == null || handler.IsMarkedDestroyed)
        {
            Debug.LogWarning("[GrazeHit][Client] Player handler missing or destroyed.");
            return false;
        }

        if (handler.CurrentAnimState != CharacterAnimState.SitToShoot)
        {
            Debug.LogWarning($"[GrazeHit][Client] Cannot use GrazeHit in anim state {handler.CurrentAnimState}.");
            return false;
        }

        return true;
    }

    public void CheckAddKillPermissionIcon(int playerId, int currentScore)
    {
        bool hadPermission = hasKillPermissionIcon.Contains(playerId);
        bool hasPermission = currentScore > 0;

        SyncChamCatPermanentIcon(playerId, hasPermission);

        if (!hadPermission && hasPermission && playerId == GameManagerNetWork.Instance.loginUserModel.UserId)
        {
            ShowSkillUsedList();
            NotificationHelper.Instance.ShowNotification("Bạn đã có quyền bắn hạ đối thủ", true);
        }
    }
    #region Skill UI
    private void ShowUnavailableSkill(GameObject notUseOverlay, Image skillImage, Button button)
    {
        if (notUseOverlay != null)
            notUseOverlay.SetActive(true);

        if (button != null)
            button.interactable = false;

        if (skillImage != null)
        {
            skillImage.transform.DOKill();
            skillImage.DOKill();
        }
    }

    private void HideUnavailableOverlay(GameObject notUseOverlay)
    {
        if (notUseOverlay != null)
            notUseOverlay.SetActive(false);
    }

    private void HideActiveSkillList()
    {
        HidePaperLegendSkillSlots();
        radialSkills.HideAll();

        Transform normalParent = useRadialSkills ? SkillListNormalPanel : GetNormalSkillListParent(true);
        if (normalParent != null)
            ClearSkillListChildren(normalParent);
    }

    private bool IsPaperLegendSkillLayoutInside(Transform candidateParent)
    {
        return candidateParent != null &&
            paperLegendSkillLayoutRoot != null &&
            (candidateParent == paperLegendSkillLayoutRoot || paperLegendSkillLayoutRoot.IsChildOf(candidateParent));
    }

    private IReadOnlyList<RectTransform> PrepareActiveSkillItems(int count)
    {
        if (count <= 0 || skillItemPrefab == null)
            return new List<RectTransform>();

        if (useRadialSkills)
        {
            Transform normalParent = SkillListNormalPanel;
            if (normalParent != null)
                ClearSkillListChildren(normalParent);

            radialSkills.Initialize(SkillListPanel as RectTransform, ResolveShootButtonTarget());
            var radialItems = radialSkills.PrepareItems(count, skillItemPrefab);
            radialSkills.Layout(count);
            return radialItems;
        }

        radialSkills.HideAll();

        Transform parent = GetNormalSkillListParent(true);
        if (parent == null)
        {
            Debug.LogWarning("SkillManager: SkillListNormalPanel/SkillListPanel is not assigned.");
            return new List<RectTransform>();
        }

        ClearSkillListChildren(parent);

        var listItems = new List<RectTransform>(count);
        for (int i = 0; i < count; i++)
        {
            GameObject newItem = Instantiate(skillItemPrefab, parent);
            if (newItem == null)
                continue;

            var rect = newItem.transform as RectTransform;
            if (rect != null)
                listItems.Add(rect);
        }

        return listItems;
    }

    private Transform GetNormalSkillListParent(bool allowFallbackToSkillListPanel)
    {
        if (SkillListNormalPanel != null)
            return SkillListNormalPanel;

        return allowFallbackToSkillListPanel ? SkillListPanel : null;
    }

    private void ClearSkillListChildren(Transform parent)
    {
        if (parent == null)
            return;

        foreach (Transform child in parent)
        {
            if (child == null)
                continue;

            if (IsPaperLegendSkillLayoutInside(child))
                continue;

            child.DOKill(true);
            Destroy(child.gameObject);
        }
    }

    private void ShowPaperLegendSkillList()
    {
        if (!TryGetLocalPaperLegendHandler(out PaperLegendCharacterNetworkHandler handler))
        {
            HideActiveSkillList();
            return;
        }

        int modelId = handler.CharacterModelId;
        if (modelId <= 0)
        {
            HideActiveSkillList();
            return;
        }

        CachePaperLegendHeroSkillsFromSelection(modelId);
        if (!_paperLegendSkillsByModelId.ContainsKey(modelId) && !_paperLegendSkillLoadsInProgress.Contains(modelId))
            StartCoroutine(LoadPaperLegendHeroSkillsRoutine(modelId));

        List<PaperLegendHeroSkillData> skillList = ResolvePaperLegendSkillList(modelId, handler.Level);
        if (skillList.Count == 0)
        {
            HideActiveSkillList();
            return;
        }

        if (TryRenderPaperLegendSkillSlotViews(skillList, handler))
            return;

        var skillItems = PrepareActiveSkillItems(skillList.Count);
        if (skillItems.Count == 0)
            return;

        for (int index = 0; index < skillList.Count; index++)
        {
            if (index >= skillItems.Count)
                break;

            PaperLegendHeroSkillData skill = skillList[index];
            RectTransform itemRect = skillItems[index];
            if (skill == null || itemRect == null)
                continue;

            int slot = NormalizePaperLegendSkillSlot(skill, index);
            int skillId = ResolvePaperLegendSkillId(skill, slot);
            int level = handler.GetSkillLevel(slot);
            int maxSkillLevel = ResolvePaperLegendSkillMaxLevel(handler, slot);
            bool canUse = handler.CanUseSkill(slot);
            bool canUpgrade = handler.CanUpgradeSkill(slot);
            bool isPassive = skill != null && skill.isPassive;
            float cooldownRemaining = handler.GetSkillCooldownRemaining(slot);
            bool blockManualSkillClick = ShouldBlockManualPaperLegendSkillClick(modelId, slot, skillId);

            GameObject item = itemRect.gameObject;
            item.SetActive(true);
            itemRect.DOKill();
            itemRect.localScale = Vector3.one;

            Image skillImage = item.GetComponent<Image>();
            Button skillButton = item.GetComponent<Button>();
            GameObject notUseOverlay = item.transform.Find("NotUseImage")?.gameObject;
            GameObject passiveIcon = FindPaperLegendPassiveSkillIcon(item.transform);

            HideUnavailableOverlay(notUseOverlay);
            SetPaperLegendPassiveSkillIcon(passiveIcon, isPassive);
            if (skillImage != null)
            {
                skillImage.DOKill();
                skillImage.color = Color.white;
            }

            SetSkillImage(skillImage, skillId);
            UpdateChargeLabel(item.transform, cooldownRemaining > 0.01f && !isPassive
                ? $"{Mathf.CeilToInt(cooldownRemaining)}s"
                : $"{level}/{maxSkillLevel}");
            ConfigurePaperLegendUpgradeButton(item.transform, slot, canUpgrade);

            if (skillButton != null)
            {
                skillButton.onClick.RemoveAllListeners();
                skillButton.interactable = canUse && !isPassive;
                int capturedSlot = slot;
                if (!blockManualSkillClick)
                    skillButton.onClick.AddListener(() => OnClickPaperLegendSkill(capturedSlot));
            }

            if (cooldownRemaining > 0.01f && !isPassive)
                ShowUnavailableSkill(notUseOverlay, skillImage, skillButton);
        }
    }

    private static GameObject FindPaperLegendPassiveSkillIcon(Transform skillItem)
    {
        if (skillItem == null)
            return null;

        Transform passive = skillItem.Find("PassiveIcon")
            ?? skillItem.Find("PassiveSkillIcon")
            ?? skillItem.Find("PassiveBadge")
            ?? skillItem.Find("PassiveOverlay");
        return passive != null ? passive.gameObject : null;
    }

    private static void SetPaperLegendPassiveSkillIcon(GameObject passiveIcon, bool visible)
    {
        if (passiveIcon != null)
            passiveIcon.SetActive(visible);
    }

    private bool TryRenderPaperLegendSkillSlotViews(List<PaperLegendHeroSkillData> skillList, PaperLegendCharacterNetworkHandler handler)
    {
        if (paperLegendSkillLayoutRoot == null || paperLegendSkillSlotPrefab == null || skillList == null || handler == null)
            return false;

        radialSkills.HideAll();
        Transform normalParent = GetNormalSkillListParent(false);
        if (normalParent != null &&
            normalParent != paperLegendSkillLayoutRoot &&
            !paperLegendSkillLayoutRoot.IsChildOf(normalParent))
        {
            ClearSkillListChildren(normalParent);
        }

        paperLegendSkillLayoutRoot.gameObject.SetActive(true);
        EnsurePaperLegendSkillSlotViewCount(skillList.Count);

        for (int index = 0; index < _paperLegendSkillSlotViews.Count; index++)
        {
            PaperLegendSkillSlotView view = _paperLegendSkillSlotViews[index];
            if (view == null)
                continue;

            if (index >= skillList.Count)
            {
                view.Hide();
                continue;
            }

            PaperLegendHeroSkillData skill = skillList[index];
            int slot = NormalizePaperLegendSkillSlot(skill, index);
            int skillId = ResolvePaperLegendSkillId(skill, slot);
            int level = handler.GetSkillLevel(slot);
            int maxSkillLevel = ResolvePaperLegendSkillMaxLevel(handler, slot);
            bool canUse = handler.CanUseSkill(slot);
            bool canUpgrade = handler.CanUpgradeSkill(slot);
            float cooldownRemaining = handler.GetSkillCooldownRemaining(slot);
            string skillName = skill != null ? ResolvePaperLegendLocalizedText(skill.name) : null;
            bool blockManualSkillClick = ShouldBlockManualPaperLegendSkillClick(handler.CharacterModelId, slot, skillId);

            view.gameObject.SetActive(true);
            view.transform.SetSiblingIndex(index);
            view.Configure(
                slot,
                skillId,
                skillName,
                level,
                maxSkillLevel,
                canUse,
                canUpgrade,
                OnClickPaperLegendSkill,
                OnClickPaperLegendUpgradeSkill,
                skill != null && skill.isPassive,
                cooldownRemaining,
                blockManualSkillClick);
        }

        return true;
    }

    private static bool ShouldBlockManualPaperLegendSkillClick(int modelId, int slot, int skillId)
    {
        if (modelId != PaperLegendHero10000004ModelId)
            return false;

        return slot == 2 && skillId == (int)PaperLegendHeroSkillId.Hero10000004ReservedSkill2;
    }

    private static string ResolvePaperLegendLocalizedText(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        return LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(key)
            : key;
    }

    private void EnsurePaperLegendSkillSlotViewCount(int count)
    {
        for (int i = _paperLegendSkillSlotViews.Count - 1; i >= 0; i--)
        {
            if (_paperLegendSkillSlotViews[i] == null)
                _paperLegendSkillSlotViews.RemoveAt(i);
        }

        if (_paperLegendSkillSlotViews.Count == 0 && paperLegendSkillLayoutRoot != null)
        {
            var existingViews = paperLegendSkillLayoutRoot.GetComponentsInChildren<PaperLegendSkillSlotView>(true);
            for (int i = 0; i < existingViews.Length; i++)
            {
                if (existingViews[i] != null && existingViews[i].transform.parent == paperLegendSkillLayoutRoot)
                    _paperLegendSkillSlotViews.Add(existingViews[i]);
            }
        }

        while (_paperLegendSkillSlotViews.Count < count)
        {
            PaperLegendSkillSlotView view = Instantiate(paperLegendSkillSlotPrefab, paperLegendSkillLayoutRoot);
            view.gameObject.name = $"PaperLegendSkillSlot_{_paperLegendSkillSlotViews.Count + 1}";
            _paperLegendSkillSlotViews.Add(view);
        }
    }

    private void HidePaperLegendSkillSlots()
    {
        if (paperLegendSkillLayoutRoot != null)
            paperLegendSkillLayoutRoot.gameObject.SetActive(false);

        for (int i = 0; i < _paperLegendSkillSlotViews.Count; i++)
        {
            if (_paperLegendSkillSlotViews[i] != null)
                _paperLegendSkillSlotViews[i].Hide();
        }
    }

    private bool TryGetLocalPaperLegendHandler(out PaperLegendCharacterNetworkHandler handler)
    {
        handler = null;

        var gameManager = GameManagerNetWork.Instance;
        int playerId = gameManager != null && gameManager.loginUserModel != null
            ? gameManager.loginUserModel.UserId
            : 0;
        if (playerId <= 0)
            return false;

        var playerObject = NetworkObjectManager.Instance != null
            ? NetworkObjectManager.Instance.GetPlayerObject(playerId)
            : null;
        if (playerObject == null)
            return false;

        handler = playerObject.GetComponent<PaperLegendCharacterNetworkHandler>();
        return handler != null;
    }

    private void CachePaperLegendHeroSkillsFromSelection(int modelId)
    {
        if (_paperLegendSkillsByModelId.ContainsKey(modelId))
            return;

        var selectionClient = PaperLegendCharacterSelectionClient.Instance;
        if (selectionClient == null)
            return;

        if (!selectionClient.HeroDataByModelId.TryGetValue(modelId, out PaperLegendHeroData hero))
            return;

        CachePaperLegendHeroSkills(hero);
    }

    private IEnumerator LoadPaperLegendHeroSkillsRoutine(int modelId)
    {
        _paperLegendSkillLoadsInProgress.Add(modelId);

        if (APIManager.Instance == null)
        {
            Debug.LogWarning("[PaperLegends][Skill] APIManager is missing; using local fallback skills if available.");
            _paperLegendSkillLoadsInProgress.Remove(modelId);
            yield break;
        }

        PaperLegendHeroListResponse response = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetPaperLegendHeroesByModelIdsAsync(new List<int> { modelId }),
            result => response = result));

        if (response != null && response.heroes != null)
        {
            for (int i = 0; i < response.heroes.Count; i++)
                CachePaperLegendHeroSkills(response.heroes[i]);
        }

        _paperLegendSkillLoadsInProgress.Remove(modelId);
        ShowSkilldList();
    }

    private void CachePaperLegendHeroSkills(PaperLegendHeroData hero)
    {
        if (hero == null)
            return;

        int modelId = hero.ResolveModelIdInt();
        if (modelId <= 0)
            return;

        List<PaperLegendHeroSkillData> skills = hero.skills != null
            ? new List<PaperLegendHeroSkillData>(hero.skills)
            : new List<PaperLegendHeroSkillData>();
        skills.Sort((left, right) => NormalizePaperLegendSkillSlot(left, 0).CompareTo(NormalizePaperLegendSkillSlot(right, 0)));
        _paperLegendSkillsByModelId[modelId] = skills;
    }

    private List<PaperLegendHeroSkillData> ResolvePaperLegendSkillList(int modelId, int heroLevel)
    {
        List<PaperLegendHeroSkillData> apiSkills = null;
        if (_paperLegendSkillsByModelId.TryGetValue(modelId, out List<PaperLegendHeroSkillData> cachedSkills) &&
            cachedSkills != null &&
            cachedSkills.Count > 0)
        {
            apiSkills = cachedSkills;
        }

        List<PaperLegendHeroSkillData> resolvedSkills = PaperLegendHeroSkillRegistry.BuildSkillList(modelId, heroLevel, apiSkills);
        if (resolvedSkills != null)
            return resolvedSkills;

        return new List<PaperLegendHeroSkillData>();
    }

    private List<PaperLegendHeroSkillData> BuildPaperLegendFourSlotSkillList(List<PaperLegendHeroSkillData> source)
    {
        PaperLegendHeroSkillData[] bySlot = new PaperLegendHeroSkillData[PaperLegendMaxSkillSlots];
        for (int i = 0; i < source.Count; i++)
        {
            PaperLegendHeroSkillData skill = source[i];
            int slot = NormalizePaperLegendSkillSlot(skill, i);
            if (slot >= 1 && slot <= PaperLegendMaxSkillSlots && bySlot[slot - 1] == null)
                bySlot[slot - 1] = skill;
        }

        List<PaperLegendHeroSkillData> result = new List<PaperLegendHeroSkillData>(PaperLegendMaxSkillSlots);
        List<PaperLegendHeroSkillData> fallback = CreateHero10000001FallbackSkills();
        for (int slot = 1; slot <= PaperLegendMaxSkillSlots; slot++)
            result.Add(bySlot[slot - 1] ?? fallback[slot - 1]);

        return result;
    }

    private List<PaperLegendHeroSkillData> CreateHero10000001FallbackSkills()
    {
        return new List<PaperLegendHeroSkillData>
        {
            new PaperLegendHeroSkillData
            {
                slot = 1,
                code = ((int)PaperLegendHeroSkillId.Hero10000001DistanceLandingDamage).ToString(),
                name = "Distance Landing Damage",
                description = "Passive. The farther the paper hero travels before landing on a target, the higher the damage. Max x4.",
                isPassive = true
            },
            new PaperLegendHeroSkillData
            {
                slot = 2,
                code = ((int)PaperLegendHeroSkillId.Hero10000001PaperArrow).ToString(),
                name = "Paper Arrow",
                description = "After casting, the next swipe shoots a paper arrow forward. When it stops, it slows enemies in the area by 30% and deals light damage.",
                damage = 10f,
                damageLevel1 = 10f,
                damageLevel2 = 13f,
                damageLevel3 = 16f,
                damageLevel4 = 19f
            },
            new PaperLegendHeroSkillData
            {
                slot = 3,
                code = ((int)PaperLegendHeroSkillId.Hero10000001FlickForceBoost).ToString(),
                name = "Flick Force Boost",
                description = "Boosts the next flick force and lets the camera look farther."
            },
            new PaperLegendHeroSkillData
            {
                slot = 4,
                code = ((int)PaperLegendHeroSkillId.Hero10000001EdgeBounceRebound).ToString(),
                name = "Lat Mep Nay Lai",
                description = "Next flick: each landing that does not consume all rebounds bounces again in the travel direction, even when pinning an enemy. Level 1-3 grants 1-3 extra bounces."
            }
        };
    }

    private int NormalizePaperLegendSkillSlot(PaperLegendHeroSkillData skill, int index)
    {
        if (skill != null && skill.slot >= 1 && skill.slot <= PaperLegendMaxSkillSlots)
            return skill.slot;

        int skillId = skill != null ? skill.ResolveSkillIdInt() : 0;
        int slotFromId = skillId % 100;
        if (slotFromId >= 1 && slotFromId <= PaperLegendMaxSkillSlots)
            return slotFromId;

        return Mathf.Clamp(index + 1, 1, PaperLegendMaxSkillSlots);
    }

    private int ResolvePaperLegendSkillId(PaperLegendHeroSkillData skill, int slot)
    {
        int skillId = skill != null ? skill.ResolveSkillIdInt() : 0;
        if (skillId > 0)
            return skillId;

        switch (Mathf.Clamp(slot, 1, PaperLegendMaxSkillSlots))
        {
            case 1:
                return (int)PaperLegendHeroSkillId.Hero10000001DistanceLandingDamage;
            case 2:
                return (int)PaperLegendHeroSkillId.Hero10000001PaperArrow;
            case 3:
                return (int)PaperLegendHeroSkillId.Hero10000001FlickForceBoost;
            case 4:
                return (int)PaperLegendHeroSkillId.Hero10000001EdgeBounceRebound;
            default:
                return 0;
        }
    }

    private int ResolvePaperLegendSkillMaxLevel(PaperLegendCharacterNetworkHandler handler, int slot)
    {
        if (handler != null && handler.CharacterModelId == 10000005 && slot == 4)
            return 3;

        return PaperLegendMaxSkillSlots;
    }

    private void ConfigurePaperLegendUpgradeButton(Transform skillItem, int slot, bool canUpgrade)
    {
        Button upgradeButton = ResolvePaperLegendUpgradeButton(skillItem);
        if (upgradeButton == null)
            return;

        upgradeButton.transform.SetAsLastSibling();
        upgradeButton.gameObject.SetActive(canUpgrade);
        upgradeButton.onClick.RemoveAllListeners();
        upgradeButton.interactable = canUpgrade;

        if (!canUpgrade)
            return;

        int capturedSlot = slot;
        upgradeButton.onClick.AddListener(() => OnClickPaperLegendUpgradeSkill(capturedSlot));
    }

    private Button ResolvePaperLegendUpgradeButton(Transform skillItem)
    {
        if (skillItem == null)
            return null;

        string[] candidateNames =
        {
            "UpgradeButton",
            "LevelUpButton",
            "PaperLegendUpgradeButton"
        };

        for (int i = 0; i < candidateNames.Length; i++)
        {
            Transform child = skillItem.Find(candidateNames[i]);
            if (child != null && child.TryGetComponent(out Button existingButton))
                return existingButton;
        }

        GameObject buttonObject = new GameObject("PaperLegendUpgradeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(skillItem, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(8f, 8f);
        rect.sizeDelta = new Vector2(34f, 34f);

        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color32(27, 126, 78, 230);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObject.GetComponent<Text>();
        label.text = "+";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 24;
        label.fontStyle = FontStyle.Bold;

        return button;
    }

    private void OnClickPaperLegendSkill(int slot)
    {
        if (TryGetLocalPaperLegendHandler(out PaperLegendCharacterNetworkHandler handler))
        {
            if (handler.CharacterModelId == PaperLegendHero10000004ModelId && slot == 2)
                return;

            if (handler.CharacterModelId == 10000005 && slot == 4)
            {
                Vector2 fallbackTargetPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                PaperLegendFlickInputCollector.BeginTargetedSkillUse(
                    slot,
                    (int)PaperLegendHeroSkillId.Hero10000005ThunderStorm,
                    fallbackTargetPosition);
                PaperLegendFlickInputCollector.EndTargetedSkillUse(fallbackTargetPosition, canceled: false);
                PlaySkillClickSound();
                return;
            }
        }

        PaperLegendFlickInputCollector.QueueSkillUse(slot);

        if (slot == 3)
            CameraRotation.Instance?.PulsePaperLegendFollowFov(8f, 1.6f);

        PlaySkillClickSound();
        StartCoroutine(RefreshPaperLegendSkillListSoon());
    }

    private void OnClickPaperLegendUpgradeSkill(int slot)
    {
        PaperLegendFlickInputCollector.QueueSkillUpgrade(slot);
        PlaySkillClickSound();
        StartCoroutine(RefreshPaperLegendSkillListSoon());
    }

    private IEnumerator RefreshPaperLegendSkillListSoon()
    {
        yield return new WaitForSeconds(0.15f);
        ShowSkilldList();
    }

    public void ShowSkilldList()
    {
        if (PaperLegendRuntimeState.IsPaperLegendMatch)
        {
            ShowPaperLegendSkillList();
            return;
        }

        var current = GameManagerNetWork.Instance.GetCurrentPlayerGame();
        if (current.statusPlayer == StatusPlayer.ShootExam)
            return;

        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null)
            return;

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        var playerGO = serverRPC.GetPlayerObject(playerId);
        var handler = playerGO != null ? playerGO.GetComponent<PlayerNetworkHandler>() : null;
        if (handler == null || !handler.IsNetworkStateReady || handler.IsMarkedDestroyed)
        {
            HideActiveSkillList();
            return;
        }

        var lst = handler.ActiveEffects.Where(x => x.IsEquiped).ToList();
        ApplyPassiveSkillEffects(handler, lst);
        if (lst == null || lst.Count == 0)
        {
            Debug.LogError("Không tìm thấy kỹ năng nào");
            HideActiveSkillList();
            return;
        }

        InitializeChargesForPlayer(playerId, lst);
        var chargesMap = GetOrCreateChargeMap(playerId);

        bool isPlayerTurn = serverRPC.IsYourTurn(playerId);
        bool isInSitToShootState = handler.CurrentAnimState == CharacterAnimState.SitToShoot;

        var skillList = new List<EffectPlayerSchema>();
        foreach (var effect in lst)
        {
            if (effect.level <= 0)
                continue;

            if (effect.effectId == (int)EffectPlayerType.ChamCat)
                continue;

            int remainingCharges = effect.charges;
            if (chargesMap.TryGetValue(effect.effectId, out var savedCharge))
                remainingCharges = savedCharge;

            if (remainingCharges > 0)
                skillList.Add(effect);
        }
        if (skillList.Count == 0)
        {
            HideActiveSkillList();
            return;
        }

        var skillItems = PrepareActiveSkillItems(skillList.Count);
        if (skillItems.Count == 0)
            return;

        for (int index = 0; index < skillList.Count; index++)
        {
            var item = skillList[index];
            if (index >= skillItems.Count)
                break;

            var newItemRect = skillItems[index];
            if (newItemRect == null)
                continue;

            GameObject newItem = newItemRect.gameObject;
            newItem.SetActive(true);
            newItemRect.DOKill();
            newItemRect.localScale = Vector3.one;
            Image skillImage = newItem.transform.GetComponent<Image>();
            var button = newItem.GetComponent<Button>();
            var notUseOverlay = newItem.transform.Find("NotUseImage")?.gameObject;
            HideUnavailableOverlay(notUseOverlay);
            if (skillImage != null)
            {
                skillImage.DOKill();
                skillImage.color = Color.white;
            }
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }

            int remainingCharges = item.charges;
            if (chargesMap.TryGetValue(item.effectId, out var savedCharge))
                remainingCharges = savedCharge;

            remainingCharges = Mathf.Max(0, remainingCharges);
            chargesMap[item.effectId] = remainingCharges;

            UpdateChargeLabel(newItem.transform, remainingCharges.ToString());
            bool hasCharges = remainingCharges > 0;
            if (!hasCharges)
            {
                ApplyDepletedVisual(skillImage, button);
                ShowUnavailableSkill(notUseOverlay, skillImage, button);
                continue;
            }
            SetSkillImage(skillImage, item.effectId);

            bool isOwnTurnInShootState = isPlayerTurn && isInSitToShootState;

            if (item.effectId == (int)EffectPlayerType.CatAnTienSkill)
            {
                bool canUse = false;
                int loginUserId = GameManagerNetWork.Instance.loginUserModel.UserId;
                if (NetworkObjectManager.Instance != null && !NetworkObjectManager.Instance.IsYourTurn(loginUserId))
                {
                    canUse = true;
                    //var ballObj = NetworkObjectManager.Instance?.GetActiveBallObject(loginUserId);
                    //if (ballObj != null)
                    //{
                    //    var ballCtr = ballObj.GetComponent<BallServerController>();
                    //    if (ballCtr != null && ballCtr.hasBeenShoot == 1)
                    //        canUse = true;
                    //}
                }

                if (canUse)
                {
                    HideUnavailableOverlay(notUseOverlay);
                    skillImage.color = new Color(1f, 1f, 1f, 0.5f);
                    skillImage.DOFade(1f, 0.5f);
                    skillImage.transform.DOScale(new Vector3(1.1f, 1.1f, 1f), 0.5f).SetLoops(-1, LoopType.Yoyo);
                    skillImage.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo);
                    if (button != null)
                    {
                        button.onClick.AddListener(() =>
                            OnClickSkill(item.effectId)
                        );
                        button.interactable = true;
                    }
                }
                else
                {
                    ShowUnavailableSkill(notUseOverlay, skillImage, button);
                }
            }
            else if (item.effectId == (int)EffectPlayerType.BananaJumpSkill)
            {
                bool canUse = isOwnTurnInShootState && handler != null && !handler.IsBananaJumpActive;

                if (canUse)
                {
                    HideUnavailableOverlay(notUseOverlay);
                    skillImage.color = new Color(1f, 1f, 1f, 0.5f);
                    skillImage.DOFade(1f, 0.5f);
                    skillImage.transform.DOScale(new Vector3(1.1f, 1.1f, 1f), 0.5f).SetLoops(-1, LoopType.Yoyo);
                    skillImage.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo);
                    if (button != null)
                    {
                        button.onClick.AddListener(() =>
                            OnClickSkill(item.effectId)
                        );
                        button.interactable = true;
                    }
                }
                else
                {
                    ShowUnavailableSkill(notUseOverlay, skillImage, button);
                }
            }
            else if (item.effectId == (int)EffectPlayerType.BigBallSkill)
            {
                if (isOwnTurnInShootState)
                {
                    HideUnavailableOverlay(notUseOverlay);
                    skillImage.color = new Color(1f, 1f, 1f, 0.5f);
                    skillImage.DOFade(1f, 0.5f);
                    skillImage.transform.DOScale(new Vector3(1.1f, 1.1f, 1f), 0.5f).SetLoops(-1, LoopType.Yoyo);
                    skillImage.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo);
                    if (button != null)
                    {
                        button.onClick.AddListener(() =>
                            OnClickSkill(item.effectId)
                        );
                        button.interactable = true;  
                    }
                }
                else
                {
                    ShowUnavailableSkill(notUseOverlay, skillImage, button);
                }
            }
            else if (item.effectId == (int)EffectPlayerType.SmallBallSkill)
            {
                if (isOwnTurnInShootState)
                {
                    HideUnavailableOverlay(notUseOverlay);
                    skillImage.color = new Color(1f, 1f, 1f, 0.5f);
                    skillImage.DOFade(1f, 0.5f);
                    skillImage.transform.DOScale(new Vector3(1.1f, 1.1f, 1f), 0.5f).SetLoops(-1, LoopType.Yoyo);
                    skillImage.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo);
                    if (button != null)
                    {
                        button.onClick.AddListener(() =>
                            OnClickSkill(item.effectId)
                        );
                        button.interactable = true;
                    }
                }
                else
                {
                    ShowUnavailableSkill(notUseOverlay, skillImage, button);
                }
            }
            else if (item.effectId == (int)EffectPlayerType.ViewSkill)
            {
                if (isOwnTurnInShootState)
                {
                    HideUnavailableOverlay(notUseOverlay);
                    skillImage.color = new Color(1f, 1f, 1f, 0.5f);
                    skillImage.DOFade(1f, 0.5f);
                    skillImage.transform.DOScale(new Vector3(1.05f, 1.05f, 1f), 0.5f).SetLoops(-1, LoopType.Yoyo);
                    skillImage.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo);
                    if (button != null)
                    {
                        button.onClick.AddListener(() =>
                            OnClickSkill(item.effectId)
                        );
                        button.interactable = true;
                    }
                }
                else
                {
                    ShowUnavailableSkill(notUseOverlay, skillImage, button);
                }
            }
            else if (item.effectId == (int)EffectPlayerType.WindBlowSkill)
            {
                bool canUse = isPlayerTurn && IsWindBlowSkillReady(playerId);

                if (canUse)
                {
                    HideUnavailableOverlay(notUseOverlay);
                    skillImage.color = new Color(1f, 1f, 1f, 0.5f);
                    skillImage.DOFade(1f, 0.5f);
                    skillImage.transform.DOScale(new Vector3(1.05f, 1.05f, 1f), 0.5f).SetLoops(-1, LoopType.Yoyo);
                    skillImage.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo);
                    if (button != null)
                    {
                        button.onClick.AddListener(() =>
                            OnClickSkill(item.effectId)
                        );
                        button.interactable = true;
                    }
                }
                else
                {
                    ShowUnavailableSkill(notUseOverlay, skillImage, button);
                }
            }
            else if (item.effectId == (int)EffectPlayerType.HuSkill)
            {
                bool canUse = !isPlayerTurn && TryResolveCurrentTurnTargetId(out int targetId) && targetId != playerId;

                if (canUse)
                {
                    HideUnavailableOverlay(notUseOverlay);
                    skillImage.color = new Color(1f, 1f, 1f, 0.5f);
                    skillImage.DOFade(1f, 0.5f);
                    skillImage.transform.DOScale(new Vector3(1.05f, 1.05f, 1f), 0.5f).SetLoops(-1, LoopType.Yoyo);
                    skillImage.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo);
                    if (button != null)
                    {
                        button.onClick.AddListener(() =>
                            OnClickSkill(item.effectId)
                        );
                        button.interactable = true;
                    }
                }
                else
                {
                    ShowUnavailableSkill(notUseOverlay, skillImage, button);
                }
            }
            else
            {
                SetSkillImage(newItem.transform.GetComponent<Image>(), item.effectId);
                if (isOwnTurnInShootState)
                {
                    if (button != null)
                    {
                        button.onClick.AddListener(() =>
                            OnClickSkill(item.effectId)
                        );
                        button.interactable = true;
                    }
                }
                else
                {
                    ShowUnavailableSkill(notUseOverlay, skillImage, button);
                }
            }
        }
    }

    private RectTransform ResolveShootButtonTarget()
    {
        if (shootButtonTarget != null)
            return shootButtonTarget;

        if (SkillListPanel == null)
            return null;

        var root = SkillListPanel.root;
        if (root == null)
            return null;

        foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect != null && rect.name == "ShootButton")
                return rect;
        }

        return null;
    }

    private void ApplyPassiveSkillEffects(PlayerNetworkHandler handler, List<EffectPlayerSchema> effects)
    {
        if (handler == null || effects == null)
            return;

        int playerId = handler.PlayerModel.playerId;
        if (playerId == 0)
            return;

        if (!_skillBaseStats.TryGetValue(playerId, out var baseStats))
        {
            baseStats = new SkillBaseStats
            {
                Power = handler.PlayerModel.powerForce,
                Spin = handler.PlayerModel.spinForce,
                Bounciness = GetBaseBounciness(playerId),
                Mass = GetBaseMass(playerId)
            };
            _skillBaseStats[playerId] = baseStats;
        }

        float updatedPower = baseStats.Power;
        float updatedSpin = baseStats.Spin;
        float updatedBounciness = baseStats.Bounciness;
        float updatedMass = baseStats.Mass;

        foreach (var effect in effects)
        {
            if (effect.level <= 0)
                continue;

            if (effect.effectId == BallPowerSkillId)
            {
                updatedPower += effect.level;
                continue;
            }

            if (effect.effectId == BallSpinSkillId)
            {
                updatedSpin += effect.level;
                continue;
            }

            if (effect.effectId == BallMentalitySkillId)
            {
                updatedBounciness += 0.05f * effect.level;
                continue;
            }

            switch ((EffectPlayerType)effect.effectId)
            {
                case EffectPlayerType.PowerSkill:
                    updatedPower += effect.level;
                    break;
                case EffectPlayerType.SpinSkill:
                    updatedSpin += effect.level;
                    break;
                case EffectPlayerType.ChiemSkill:
                    updatedBounciness += 0.05f * effect.level;
                    break;
            }
        }

        ApplyPassiveSkillStatsNetworked(handler, updatedPower, updatedSpin, updatedBounciness, updatedMass);
    }

    private void ApplyPassiveSkillStatsNetworked(
        PlayerNetworkHandler handler,
        float power,
        float spin,
        float bounciness,
        float mass)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        int playerId = handler.PlayerModel.playerId;
        bounciness = Mathf.Clamp01(bounciness);
        mass = Mathf.Max(0.01f, mass);

        if (manager.HasStateAuthority)
        {
            manager.ApplySkillStatOverrides(playerId, power, spin, bounciness, mass);
        }
        else
        {
            manager.RpcRequestApplySkillStats(playerId, power, spin, bounciness, mass);
        }

        var model = handler.PlayerModel;
        model.powerForce = power;
        model.spinForce = spin;
        handler.PlayerModel = model;

        ApplyBouncinessToActiveBall(playerId, bounciness);
        ApplyMassToActiveBall(playerId, mass);
    }

    private float GetBaseBounciness(int playerId)
    {
        var physics = NetworkObjectManager.Instance?.GetBallPhysics(playerId);
        if (physics.HasValue)
            return physics.Value.data.Bounciness;

        return 0f;
    }

    private float GetBaseMass(int playerId)
    {
        var physics = NetworkObjectManager.Instance?.GetBallPhysics(playerId);
        if (physics.HasValue)
            return physics.Value.data.Mass;

        return 1f;
    }

    private void ApplyBouncinessToActiveBall(int playerId, float bounciness)
    {
        var physics = NetworkObjectManager.Instance?.GetBallPhysics(playerId);
        if (!physics.HasValue)
            return;

        var data = physics.Value.data;
        var activeBall = physics.Value.active;
        data.Bounciness = Mathf.Clamp01(bounciness);
        activeBall?.ApplyPhysicsLocally(data);
    }

    private void ApplyMassToActiveBall(int playerId, float mass)
    {
        var physics = NetworkObjectManager.Instance?.GetBallPhysics(playerId);
        if (!physics.HasValue)
            return;

        var data = physics.Value.data;
        var activeBall = physics.Value.active;
        data.Mass = Mathf.Max(0.01f, mass);
        activeBall?.ApplyPhysicsLocally(data);
    }

    public void ShowSkillUsedList()
    {
        foreach (Transform child in SkillUsedListPanel)
        {
            Destroy(child.gameObject);
        }

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        SyncChamCatPermanentIcon(playerId, HasChamCatPermission(playerId));
        System.Collections.Generic.List<int> tempList = null;
        System.Collections.Generic.List<int> permList = null;

        skillsUsedThisTurn.TryGetValue(playerId, out tempList);
        permanentSkillIcons.TryGetValue(playerId, out permList);

        var skillIds = new System.Collections.Generic.List<int>();
        if (permList != null) skillIds.AddRange(permList);
        if (tempList != null) skillIds.AddRange(tempList);

        if (skillIds.Count > 0)
        {
            int permanentCount = permList != null ? permList.Count : 0;
            counteredSkillUseIndexesThisTurn.TryGetValue(playerId, out var counteredThisTurn);
            for (int i = 0; i < skillIds.Count; i++)
            {
                var id = skillIds[i];
                GameObject newItem = Instantiate(skillUsedItemPrefab, SkillUsedListPanel);
                SetSkillImage(newItem.transform.GetComponent<Image>(), id);
                int turnIndex = i - permanentCount;
                if (turnIndex >= 0 && counteredThisTurn != null && counteredThisTurn.Contains(turnIndex))
                {
                    ShowCounteredSkillOverlay(newItem);
                }
            }
            return;
        }

        var selectedSkills = ClientGameplayBridge.UI.GetSelectedSkills();
        if (selectedSkills == null)
            return;

        foreach (var item in selectedSkills)
        {
            GameObject newItem = Instantiate(skillUsedItemPrefab, SkillUsedListPanel);
            newItem.transform.GetComponent<Image>().sprite = SkillHelper.GetSkillSprite(item);
        }
    }

    public void ShowSkillUsedList(int playerId, Transform parent)
    {
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }

        if (skillsUsedByPlayer.TryGetValue(playerId, out var usedSkills))
        {
            counteredSkillUseIndexesByPlayer.TryGetValue(playerId, out var counteredIndexes);
            for (int i = 0; i < usedSkills.Count; i++)
            {
                var id = usedSkills[i];
                GameObject newItem = Instantiate(skillUsedItemPrefab, parent);
                SetSkillImage(newItem.transform.GetComponent<Image>(), id);
                if (counteredIndexes != null && counteredIndexes.Contains(i))
                {
                    ShowCounteredSkillOverlay(newItem);
                }
            }
        }
    }

    private void ShowCounteredSkillOverlay(GameObject skillItem)
    {
        if (skillItem == null)
        {
            return;
        }

        Transform existingOverlay = FindCounteredSkillOverlay(skillItem.transform);
        if (existingOverlay != null)
        {
            existingOverlay.gameObject.SetActive(true);
            return;
        }

        if (counteredSkillBanIconPrefab == null)
        {
            return;
        }

        var overlay = Instantiate(counteredSkillBanIconPrefab, skillItem.transform);
        overlay.name = "CounteredSkillBanIcon";
        overlay.SetActive(true);

        if (overlay.transform is RectTransform overlayRect)
        {
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.localScale = Vector3.one;
        }
    }

    private static Transform FindCounteredSkillOverlay(Transform skillItem)
    {
        if (skillItem == null)
        {
            return null;
        }

        string[] candidateNames =
        {
            "CounteredSkillBanIcon",
            "CounteredSkillIcon",
            "SkillBanIcon",
            "BanIcon",
            "BlockIcon",
            "CounterIcon",
            "DisabledIcon",
            "ForbiddenIcon"
        };

        foreach (var name in candidateNames)
        {
            var child = skillItem.Find(name);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    public void StopViewSkillEffect()
    {
        viewSkillEffectController?.StopViewSkillEffect();
    }

    private void SetSkillImage(Image targetImage, int effectId)
    {
        if (targetImage == null)
            return;

        targetImage.enabled = false;

        string primaryPath = GetSkillIconPath(effectId);
        string fallbackPath = GetLegacySkillIconPath(effectId);

        StartCoroutine(LoadSkillImageWithFallback(targetImage, primaryPath, fallbackPath));
    }

    private IEnumerator LoadSkillImageWithFallback(Image targetImage, string primaryPath, string fallbackPath)
    {
        Sprite loadedSprite = null;
        yield return AddressablesHelper.LoadSprite(primaryPath, sprite => loadedSprite = sprite);

        if (loadedSprite == null && primaryPath != fallbackPath)
        {
            yield return AddressablesHelper.LoadSprite(fallbackPath, sprite => loadedSprite = sprite);
        }

        if (loadedSprite != null && targetImage != null)
        {
            targetImage.sprite = loadedSprite;
            targetImage.enabled = true;
        }
    }

    private static bool IsBallSkillId(int skillId)
    {
        return skillId >= BallSkillIdMin && skillId < BallSkillIdMaxExclusive;
    }

    private static string GetSkillIconPath(int skillId)
    {
        if (!IsBallSkillId(skillId))
            return PaperLegendHeroAddressables.BuildSkillIconAddress(skillId);

        return GetLegacySkillIconPath(skillId);
    }

    private static string GetLegacySkillIconPath(int skillId)
    {
        string skillFolder = IsBallSkillId(skillId) ? "Skills/Ball" : "Skills";
        return $"{AddressablePaths.Root}/{skillFolder}/{skillId}.png";
    }
    private void OnDisable()
    {
        // Kill any tweens on this manager and its children
        transform.DOKill(true);
        viewSkillEffectController?.StopViewSkillEffect();
        CancelChamCatHold();
    }
    // Kiểm tra xem có kẻ địch nào ở gần người chơi hiện tại hay không
    // Mặc định bán kính kiểm tra là 0.75f (~1.5 gang tay)
    public bool IsEnemyNearPlayer(int playerId, float radius)
    {
        if (playerId == 0)
            return false;

        var currentBall = NetworkObjectManager.Instance.GetActiveBallObject(playerId);
        if (currentBall == null)
            return false;

        Vector3 myPos = currentBall.transform.position;
        var players = NetworkObjectManager.Instance.players;
        foreach (var entry in players)
        {
            if (entry.playerId == playerId)
                continue;

            var playerGO = NetworkObjectManager.Instance.GetPlayerObject(entry.playerId);
            var ball = NetworkObjectManager.Instance.GetActiveBallObject(entry.playerId);
            if (playerGO == null || ball == null)
                continue;

            var handler = playerGO.GetComponent<PlayerNetworkHandler>();
            var model = handler.PlayerModel;
            if (model.isDestroy || model.statusPlayer == StatusPlayer.Destroy || model.statusPlayer == StatusPlayer.WaitingDestroy)
                continue;

            float distance = Vector3.Distance(myPos, ball.transform.position);
            if (distance <= radius)
                return true;
        }
        return false;
    }

    public bool IsEnemyNearCurrentPlayer(float radius)
    {
        int currentId = GameManagerNetWork.Instance.loginUserModel.UserId;
        return IsEnemyNearPlayer(currentId, radius);
    }

    private bool IsWindBlowSkillReady(int playerId)
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return false;

        var ballObj = manager.GetActiveBallObject(playerId);
        if (ballObj == null)
            return false;

        var ballCtrl = ballObj.GetComponent<BallServerController>();
        if (ballCtrl == null || ballCtrl.hasBeenShoot != 1)
            return false;
        return true;
        //var rb = ballObj.GetComponent<NetworkRigidbody3D>()?.Rigidbody;
        //if (rb == null)
        //    return false;

        //return rb.linearVelocity.magnitude > 0.02f || rb.angularVelocity.magnitude > 0.2f;
    }

    private bool TryResolveCurrentTurnTargetId(out int targetPlayerId)
    {
        targetPlayerId = 0;

        var manager = NetworkObjectManager.Instance;
        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (manager == null || serverRPC == null)
            return false;

        var listUserOrder = manager.GetOrderedPlayerInfos();
        if (listUserOrder == null || listUserOrder.Count == 0)
            return false;

        int currentOrder = serverRPC.currentPlayerIndex;
        var data = listUserOrder.FirstOrDefault(t => t.turnOrder == currentOrder);
        if (data.playerId == 0)
            return false;

        targetPlayerId = data.playerId;
        return true;
    }

    private PlayerNetworkHandler GetPlayerNetworkHandler(int playerId)
    {
        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null)
            return null;

        var playerGO = serverRPC.GetPlayerObject(playerId);
        return playerGO != null ? playerGO.GetComponent<PlayerNetworkHandler>() : null;
    }

    private bool HasChamCatPermission(int playerId)
    {
        var handler = GetPlayerNetworkHandler(playerId);
        return handler != null && handler.PlayerModel.score > 0;
    }

    private void SyncChamCatPermanentIcon(int playerId, bool hasPermission)
    {
        if (hasPermission)
        {
            if (!hasKillPermissionIcon.Contains(playerId))
                hasKillPermissionIcon.Add(playerId);

            AddPermanentSkillIcon(playerId, (int)EffectPlayerType.ChamCat);
            return;
        }

        hasKillPermissionIcon.Remove(playerId);
        if (permanentSkillIcons.TryGetValue(playerId, out var skillIds))
        {
            skillIds.RemoveAll(id => id == (int)EffectPlayerType.ChamCat);
            if (skillIds.Count == 0)
                permanentSkillIcons.Remove(playerId);
        }
    }

    public bool IsChamCatContextAvailable(int playerId)
    {
        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null || !serverRPC.IsYourTurn(playerId))
            return false;

        var handler = GetPlayerNetworkHandler(playerId);
        if (handler == null)
            return false;

        if (handler.PlayerModel.score <= 0)
            return false;

        if (handler.CurrentAnimState != CharacterAnimState.SitToShoot)
            return false;

        var status = handler.PlayerModel.statusPlayer;
        return status != StatusPlayer.ShootExam && status != StatusPlayer.StartPoint && status != StatusPlayer.Destroy;
    }

    public bool ShouldShowChamCatIconForTarget(int playerId, BallServerController target)
    {
        if (target == null || playerId == 0)
            return false;

        if (target.playerId == playerId || target.IsActive != 1)
            return false;

        if (!IsChamCatContextAvailable(playerId))
            return false;

        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return false;

        var playerGO = manager.GetPlayerObject(target.playerId);
        if (playerGO == null)
            return false;

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
            return false;

        var model = handler.PlayerModel;
        if (model.isDestroy || model.statusPlayer == StatusPlayer.Destroy || model.statusPlayer == StatusPlayer.WaitingDestroy)
            return false;

        var myBall = manager.GetActiveBallObject(playerId);
        if (myBall == null)
            return false;

        float distance = Vector3.Distance(myBall.transform.position, target.transform.position);
        return distance <= DistanceForChamCat;
    }
    #endregion

    private bool TryActivateSkill(int typeSkill, List<EffectPlayerSchema> activeEffects)
    {
        if (_skillHandlers.TryGetValue(typeSkill, out var handler))
            return handler.TryActivate(this, activeEffects);

        return _defaultSkillHandler.TryActivate(this, activeEffects);
    }

    private void InitializeSkillHandlers()
    {
        _skillHandlers[(int)EffectPlayerType.ViewSkill] = new ViewSkillHandler(viewSkillEffectController);
        _skillHandlers[(int)EffectPlayerType.CatAnTienSkill] = new CatAnTienSkillHandler();
        _skillHandlers[(int)EffectPlayerType.BananaJumpSkill] = new BananaJumpSkillHandler();
        _skillHandlers[(int)EffectPlayerType.GrazeHit] = new GrazeHitSkillHandler();
        _skillHandlers[BallGrazeHitSkillId] = new GrazeHitSkillHandler();
        _skillHandlers[(int)EffectPlayerType.BigBallSkill] = new BigBallSkillHandler();
        _skillHandlers[(int)EffectPlayerType.SmallBallSkill] = new SmallBallSkillHandler();
        _skillHandlers[(int)EffectPlayerType.WindBlowSkill] = new WindBlowSkillHandler();
        _skillHandlers[(int)EffectPlayerType.HuSkill] = new HuSkillHandler();
    }
}
