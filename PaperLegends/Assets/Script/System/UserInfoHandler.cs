using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserInfoHandler : MonoBehaviour
{
    public static UserInfoHandler Instance { get; private set; }

    [Header("DATA USER CONFIG")]
    [SerializeField]
    private TextMeshProUGUI textNamePlayer;
    [SerializeField]
    private TextMeshProUGUI textLevel;
    [SerializeField]
    private TextMeshProUGUI textRingBall;
    [SerializeField]
    private TextMeshProUGUI textGlassShard;
    [SerializeField]
    private Slider expSlider;
    [SerializeField]
    private GameObject pendingMessageBadge;
    [SerializeField]
    private TMP_Text pendingMessageBadgeText;
    [SerializeField]
    private GameObject pendingFriendRequestBadge;
    [SerializeField]
    private TMP_Text pendingFriendRequestBadgeText;
    [SerializeField]
    private TMP_Text expText;

    public PlayerInventorySchema PlayerInventory { get; private set; }
    public List<EquipPlayer> EquippedItems { get; private set; } = new List<EquipPlayer>();
    public PlayerModel ModelPlayer { get; private set; }

    private Sequence pendingMessageBadgeSequence;
    private Vector3 pendingMessageBadgeBaseScale = Vector3.one;
    private Vector3 pendingFriendRequestBadgeBaseScale = Vector3.one;
    private int pendingMessageCount = -1;
    private int pendingFriendRequestCount = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CacheBadgeBaseScales();
        HideBadgeImmediately(pendingMessageBadge, pendingMessageBadgeBaseScale);
        HideBadgeImmediately(pendingFriendRequestBadge, pendingFriendRequestBadgeBaseScale);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        KillMessageBadgeTween();
    }

    public void SetInventory(PlayerInventorySchema inventory)
    {
        PlayerInventory = inventory;
        if (inventory?.equippedItems != null)
        {
            EquippedItems = inventory.equippedItems.ToList();
        }
        else
        {
            EquippedItems = new List<EquipPlayer>();
        }
    }

    public void SetModel(PlayerModel model)
    {
        ModelPlayer = model;
    }

    public EquipPlayer GetEquippedBall()
    {
        return EquippedItems.FirstOrDefault(e => e.locationId == 1);
    }

    public void ApplyToUI()
    {
        if (PlayerInventory == null)
        {
            return;
        }

        UpdateExpSlider();
        UpdateBasicInfo();
        UpdateBadges();
    }

    public void StartLoadingPlayerInfo()
    {
        StartCoroutine(LoadPlayerInfo());
    }

    public IEnumerator LoadPlayerInfo()
    {
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetPlayerInventoryAsync(GameManagerNetWork.Instance.loginUserModel.UserId),
            SetInventory));

        if (PlayerInventory == null)
        {
            Debug.LogWarning("Không thể load được data");
            yield break;
        }

        yield return new WaitUntil(() => PlayerInventory != null);
        yield return StartCoroutine(ApplyPlayerInfoRoutine());
    }

    public void RefreshPlayerInfo(PlayerInventorySchema newData)
    {
        if (newData == null)
        {
            return;
        }

        SetInventory(newData);
        StartCoroutine(ApplyPlayerInfoRoutine());
    }

    private IEnumerator ApplyPlayerInfoRoutine()
    {
        ApplyToUI();

        var inventory = PlayerInventory;
        if (inventory != null)
        {
            var ball = GetEquippedBall();
            int ballId = ball != null ? ball.id : inventory.Ball;
            int ballType = ball != null ? ball.typeGid : (int)TypeItemGid.Culi;
            yield return null;
        }

        yield break;
    }

    private void UpdateExpSlider()
    {
        if (expSlider == null)
        {
            return;
        }

        expSlider.maxValue = ItemVisualHelper.GetExpForNextLevel(PlayerInventory.Level);
        expSlider.value = PlayerInventory.Exp;
    }

    private void UpdateBasicInfo()
    {
        if (textNamePlayer != null)
        {
            textNamePlayer.text = PlayerInventory.PlayerName;
        }

        if (textLevel != null)
        {
            textLevel.text = PlayerInventory.Level.ToString();
        }
        if (expText != null)
        {
            expText.text = PlayerInventory.Exp.ToString();
        }
        if (textRingBall != null)
        {
            textRingBall.text = PlayerInventory.RingBall.ToString();
        }
        QuickMatchClient.Instance?.UpdateStartSearchAvailability(showNotification: false);
        if (textGlassShard != null)
        {
            textGlassShard.text = PlayerInventory.GlassShard.ToString();
        }
    }

    private void UpdateBadges()
    {
        SetPendingMessageCount(PlayerInventory.newmessage);
        SetPendingFriendRequestCount(PlayerInventory.newreqfriends);
    }

    public void SetPendingMessageCount(int count)
    {
        count = Mathf.Max(0, count);
        bool isFirstUnreadLoad = pendingMessageCount < 0 && count > 0;
        bool hasNewUnreadMessage = pendingMessageCount >= 0 && count > pendingMessageCount;
        bool shouldAnimate = isFirstUnreadLoad || hasNewUnreadMessage;
        pendingMessageCount = count;

        if (PlayerInventory != null)
        {
            PlayerInventory.newmessage = count;
        }

        UpdateBadge(
            pendingMessageBadge,
            pendingMessageBadgeText,
            count,
            shouldAnimate,
            pendingMessageBadgeBaseScale);

        if (shouldAnimate)
        {
            PopupHelper.Instance?.ShowNewMessageNoticePopup();
        }
    }

    public void SetPendingFriendRequestCount(int count)
    {
        count = Mathf.Max(0, count);
        pendingFriendRequestCount = count;

        if (PlayerInventory != null)
        {
            PlayerInventory.newreqfriends = count;
        }

        UpdateBadge(
            pendingFriendRequestBadge,
            pendingFriendRequestBadgeText,
            count,
            false,
            pendingFriendRequestBadgeBaseScale);
    }

    public void IncrementPendingMessageCount()
    {
        SetPendingMessageCount(Mathf.Max(0, pendingMessageCount) + 1);
    }

    public void IncrementPendingFriendRequestCount()
    {
        SetPendingFriendRequestCount(Mathf.Max(0, pendingFriendRequestCount) + 1);
    }

    private void UpdateBadge(GameObject badge, TMP_Text badgeText, int count, bool playPopAnimation, Vector3 baseScale)
    {
        bool hasItems = count > 0;

        if (badgeText != null)
        {
            badgeText.text = count.ToString();
        }

        if (badge == null)
        {
            return;
        }

        if (!hasItems)
        {
            if (badge == pendingMessageBadge)
            {
                KillMessageBadgeTween();
            }

            badge.transform.localScale = baseScale;
            badge.SetActive(false);
            return;
        }

        badge.SetActive(true);

        if (badge == pendingMessageBadge)
        {
            if (playPopAnimation)
            {
                PlayMessageBadgePopAnimation(baseScale);
                return;
            }

            KillMessageBadgeTween();
        }

        badge.transform.localScale = baseScale;
    }

    private void CacheBadgeBaseScales()
    {
        pendingMessageBadgeBaseScale = GetBadgeBaseScale(pendingMessageBadge);
        pendingFriendRequestBadgeBaseScale = GetBadgeBaseScale(pendingFriendRequestBadge);
    }

    private static Vector3 GetBadgeBaseScale(GameObject badge)
    {
        if (badge == null)
        {
            return Vector3.one;
        }

        Vector3 scale = badge.transform.localScale;
        return scale == Vector3.zero ? Vector3.one : scale;
    }

    private static void HideBadgeImmediately(GameObject badge, Vector3 baseScale)
    {
        if (badge == null)
        {
            return;
        }

        badge.transform.localScale = baseScale;
        badge.SetActive(false);
    }

    private void PlayMessageBadgePopAnimation(Vector3 baseScale)
    {
        if (pendingMessageBadge == null)
        {
            return;
        }

        KillMessageBadgeTween();

        Transform badgeTransform = pendingMessageBadge.transform;
        badgeTransform.localScale = baseScale * 0.25f;

        pendingMessageBadgeSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(badgeTransform.DOScale(baseScale * 1.25f, 0.18f).SetEase(Ease.OutBack))
            .Append(badgeTransform.DOScale(baseScale * 0.92f, 0.08f).SetEase(Ease.OutQuad))
            .Append(badgeTransform.DOScale(baseScale, 0.12f).SetEase(Ease.OutBack))
            .OnKill(() => pendingMessageBadgeSequence = null);
    }

    private void KillMessageBadgeTween()
    {
        if (pendingMessageBadgeSequence == null)
        {
            return;
        }

        pendingMessageBadgeSequence.Kill();
        pendingMessageBadgeSequence = null;
    }
}
