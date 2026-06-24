using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DailyLoginRewardsManager : MonoBehaviour
{
    public static DailyLoginRewardsManager Instance;

    [SerializeField]
    private GameObject rewardItemPrefab;
    [SerializeField]
    private Transform rewardListParent;
    [SerializeField]
    private Sprite[] rewardIcons;

    private readonly List<RewardItem> rewards = new();
    private const RewardType DefaultRewardType = RewardType.RollCallDaily;
    private const int DefaultPlayerId = 21;

    private void Awake()
    {
        Instance = this;
    }

    public void RefreshRewards()
    {
        int playerId = ResolvePlayerId();

        StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetPlayerAchievementRewardsAsync((int)DefaultRewardType, playerId),
            fetchedRewards =>
            {
                rewards.Clear();
                if (fetchedRewards != null)
                {
                    rewards.AddRange(fetchedRewards);
                }
                RenderUI();
            }));
    }

    private int ResolvePlayerId()
    {
        if (GameManagerNetWork.Instance != null && GameManagerNetWork.Instance.loginUserModel != null)
        {
            return GameManagerNetWork.Instance.loginUserModel.UserId;
        }

        return DefaultPlayerId;
    }

    private void RenderUI()
    {
        foreach (Transform child in rewardListParent)
        {
            Destroy(child.gameObject);
        }

        if (rewards == null || rewards.Count == 0)
        {
            return;
        }

        int nextClaimIndex = -1;
        int nextClaimDisplayIndex = int.MaxValue;

        for (int i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (reward == null || reward.isGiftReceived)
            {
                continue;
            }

            int displayIndex = reward.seq > 0 ? reward.seq : i + 1;
            if (displayIndex < nextClaimDisplayIndex)
            {
                nextClaimDisplayIndex = displayIndex;
                nextClaimIndex = i;
            }
        }

        var todayUtcDate = DateTime.UtcNow.Date;

        for (int i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (reward == null)
            {
                continue;
            }

            var go = Instantiate(rewardItemPrefab, rewardListParent);
            int displayIndex = reward.seq > 0 ? reward.seq : i + 1;

            var dayText = go.transform.Find("Day")?.GetComponent<TMP_Text>();
            if (dayText != null)
            {
                dayText.text = LocalizationManager.Instance.GetText("day") + " " + displayIndex.ToString();
            }

            var amountText = go.transform.Find("Amount")?.GetComponent<TMP_Text>();
            if (amountText != null)
            {
                amountText.text = reward.rewardAmount.ToString();
            }

            SetupRewardImage(go.transform, reward);

            var claimBtnTransform = go.transform.Find("ClaimButton");
            var claimBtn = claimBtnTransform != null ? claimBtnTransform.GetComponent<Button>() : null;

            bool hasReceived = reward.isGiftReceived;
            bool isNextClaim = i == nextClaimIndex;
            bool shouldShowButton = isNextClaim;

            bool claimedToday = false;
            if (!string.IsNullOrWhiteSpace(reward.updatedAt) &&
                DateTime.TryParse(reward.updatedAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var updatedAt))
            {
                claimedToday = updatedAt.Date == todayUtcDate;
            }

            bool canClaim = shouldShowButton && !hasReceived && !claimedToday;


            if (claimBtnTransform != null)
            {
                claimBtnTransform.gameObject.SetActive(shouldShowButton);
            }

            if (claimBtn != null)
            {
                claimBtn.onClick.RemoveAllListeners();
                claimBtn.interactable = canClaim;

                if (canClaim)
                {
                    ResetButtonVisuals(claimBtnTransform);
                    var rewardData = reward;
                    claimBtn.onClick.AddListener(() => TryClaimDailyReward(rewardData));
                }
                else if (shouldShowButton)
                {
                    ApplyDisabledVisuals(claimBtnTransform);
                }
            }

            var claimedLabel = go.transform.Find("ClaimedLabel");
            if (claimedLabel != null)
            {
                claimedLabel.gameObject.SetActive(hasReceived);
            }
        }
    }

    private void SetupRewardImage(Transform rewardTransform, RewardItem reward)
    {
        if (rewardTransform == null || reward == null)
        {
            return;
        }

        var imageTransform = rewardTransform.Find("Image");
        if (imageTransform == null)
        {
            return;
        }

        var defaultImage = imageTransform.Find("ImageDefault");
        bool hasRewardAmount = reward.rewardAmount > 0;
        if (defaultImage != null)
        {
            defaultImage.gameObject.SetActive(hasRewardAmount && (!reward.itemId.HasValue || reward.itemId.Value <= 0));
        }

        bool hasItemId = reward.itemId.HasValue && reward.itemId.Value > 0;
        imageTransform.gameObject.SetActive(hasItemId || hasRewardAmount);

        if (!hasItemId)
        {
            return;
        }

        int itemId = reward.itemId.Value;
        var itemImage = imageTransform.GetComponentInChildren<Image>();
        if (itemImage != null)
        {
            StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{itemId}.png", sprite =>
            {
                itemImage.sprite = sprite != null ? sprite : ItemVisualHelper.LoadSpriteByID(itemId);
            }));
        }
    }

    private void TryClaimDailyReward(RewardItem reward)
    {
        if (reward == null)
        {
            Debug.LogWarning("Reward data is not available for claiming.");
            return;
        }

        int playerId = ResolvePlayerId();
        if (playerId <= 0)
        {
            Debug.LogWarning("Không xác định được người chơi hiện tại để nhận quà.");
            return;
        }

        int achievementId = reward.seq > 0 ? reward.seq : reward.locationId;
        if (achievementId <= 0)
        {
            Debug.LogWarning("Không tìm thấy mã phần thưởng hợp lệ để nhận quà.");
            return;
        }

        int locationId = reward.locationId > 0 ? reward.locationId : reward.seq;
        string rewardType = !string.IsNullOrEmpty(reward.rewardType)
            ? reward.rewardType
            : ((int)DefaultRewardType).ToString();

        ToggleLoadingScreen(true);

        StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.ConfirmRewardClaimAsync(playerId, rewardType, achievementId, locationId),
            updatedReward =>
            {
                try
                {
                    bool success = updatedReward != null;
                    if (success)
                    {
                        reward.UpdateFrom(updatedReward);
                        PlayRewardSuccessSound();
                    }
                    else
                    {
                        Debug.LogWarning("Nhận quà đăng nhập thất bại hoặc không trả về dữ liệu phần thưởng.");
                        UIControllerOffline.Instance?.ShowMesByUser("Nhận quà thất bại. Vui lòng thử lại.");
                    }

                    ShowRewardNotification(success);
                    RenderUI();
                }
                finally
                {
                    ToggleLoadingScreen(false);
                }
            }));
    }

    private void ToggleLoadingScreen(bool isActive)
    {
        var loadingManager = LoadingManager.Instance;
        if (loadingManager != null && loadingManager.UILoadingScreenPrefab != null)
        {
            loadingManager.UILoadingScreenPrefab.SetActive(isActive);
        }
    }

    private void ShowRewardNotification(bool success)
    {
        var notification = NotificationHelper.Instance;
        if (notification == null)
        {
            return;
        }

        string key = success ? "noti_friend_true" : "noti_friend_false";
        var localization = LocalizationManager.Instance;
        string message = localization != null ? localization.GetText(key) : key;

        notification.ShowNotification(message, success);
    }

    private void PlayRewardSuccessSound()
    {
        var soundManager = SoundManager.Instance;
        if (soundManager == null)
        {
            return;
        }

        if (soundManager.LuckyDrawNormal != null)
        {
            soundManager.PlayLuckyDrawNormal();
            return;
        }

        soundManager.PlayUpgradeSuccess();
    }

    private static void ApplyDisabledVisuals(Transform buttonTransform)
    {
        if (buttonTransform == null)
        {
            return;
        }

        var canvasGroup = buttonTransform.GetComponent<CanvasGroup>();
        bool hasCanvasGroup = canvasGroup != null;

        if (hasCanvasGroup)
        {
            canvasGroup.alpha = 0.5f;
        }
        else
        {
            foreach (var image in buttonTransform.GetComponentsInChildren<Image>(true))
            {
                var color = image.color;
                color.a = 0.5f;
                image.color = color;
            }
        }

        if (hasCanvasGroup)
        {
            return;
        }

        foreach (var text in buttonTransform.GetComponentsInChildren<TMP_Text>(true))
        {
            text.alpha = 0.5f;
        }
    }

    private static void ResetButtonVisuals(Transform buttonTransform)
    {
        if (buttonTransform == null)
        {
            return;
        }

        var canvasGroup = buttonTransform.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        foreach (var image in buttonTransform.GetComponentsInChildren<Image>(true))
        {
            var color = image.color;
            color.a = 1f;
            image.color = color;
        }

        foreach (var text in buttonTransform.GetComponentsInChildren<TMP_Text>(true))
        {
            text.alpha = 1f;
        }
    }
}
