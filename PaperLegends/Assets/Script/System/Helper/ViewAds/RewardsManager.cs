using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;


public class RewardsManager : MonoBehaviour
{

    public static RewardsManager Instance;
    [SerializeField]
    private GameObject rewardItemPrefab;
    [SerializeField]
    private Transform rewardListParent;
    [SerializeField]
    private Sprite[] rewardIcons; // sprite load từ IDITEM

    private List<RewardItem> rewards = new();
    private const RewardType DefaultRewardType = RewardType.WatchAds;
    private const int DefaultPlayerId = 21;

    private void Awake()
    {
        Instance = this;
    }


    public void GetListAds()
    {
        int playerId = ResolvePlayerId();

        StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetPlayerAchievementRewardsAsync((int)DefaultRewardType, playerId),
            fetchedRewards =>
            {
                rewards = fetchedRewards ?? new List<RewardItem>();
                RenderUI();
            }));
    }



    void RenderUI()
    {
        foreach (Transform child in rewardListParent)
            Destroy(child.gameObject);

        if (rewards == null)
        {
            return;
        }

        bool allPreviousRewardsReceived = true;

        for (int i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (reward == null)
            {
                continue;
            }

            var go = Instantiate(rewardItemPrefab, rewardListParent);

            int displayIndex = reward.seq > 0 ? reward.seq : i + 1;
 

            var iconTransform = go.transform.Find("Icon");
            var iconringball = go.transform.Find("iconringball");
            var iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            var amountText = go.transform.Find("Amount").GetComponent<TMP_Text>();
            var indexText = go.transform.Find("Index").GetComponent<TMP_Text>();
            if (amountText != null)
            {
                amountText.text = reward.rewardAmount.ToString();
            }
            if (indexText != null)
            {
                int index = i + 1;
                indexText.text = index.ToString();
            }
            var countGifText = go.transform.Find("CountGif").GetComponent<TMP_Text>();
            if (countGifText != null)
            {
                countGifText.text = reward.countGif.ToString();
            }

            if (iconImage != null)
            {
                if (reward.itemId.HasValue && reward.itemId.Value > 0)
                {
                    int itemId = reward.itemId.Value;
                    if (itemId != ItemIdConfig.CuliSpriteId)
                    {
                        iconringball.gameObject.SetActive(false);
                        iconTransform.gameObject.SetActive(true);
                        iconImage.enabled = true;
                        StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{itemId}.png", sprite =>
                        {
                            if (iconImage != null)
                            {
                                iconImage.sprite = sprite != null ? sprite : ItemVisualHelper.LoadSpriteByID(itemId);
                            }
                        }));
                    }
                    else if (rewardIcons != null && rewardIcons.Length > 0)
                    {
                        int iconIndex = Mathf.Clamp(itemId - 1, 0, rewardIcons.Length - 1);
                        iconImage.sprite = rewardIcons[iconIndex];
                        iconImage.enabled = true;
                    }
                    else
                    {
                        iconringball.gameObject.SetActive(true);
                        iconTransform.gameObject.SetActive(false);
                        iconImage.sprite = null;
                        iconImage.enabled = false;
                    }
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.enabled = false;
                }
            }

            var ClaimedImageTransform = go.transform.Find("ClaimedImage");
            var watchBtnTransform = go.transform.Find("WatchAdButton");
            var claimBtnTransform = go.transform.Find("ClaimButton");
            var watchedAdBtnTransform = go.transform.Find("WatchedAdButton");

            var watchBtn = watchBtnTransform != null ? watchBtnTransform.GetComponent<Button>() : null;
            var claimBtn = claimBtnTransform != null ? claimBtnTransform.GetComponent<Button>() : null;
            var watchedAdBtn = watchedAdBtnTransform != null ? watchedAdBtnTransform.GetComponent<Button>() : null;


            var rewardData = reward;

            if (watchBtn != null)
            {
                watchBtn.onClick.AddListener(() => TryWatchAd(rewardData));
            }

            if (claimBtn != null)
            {
                claimBtn.onClick.AddListener(() => TryClaimReward(rewardData));
            }

            bool hasWatchedAd = rewardData.isGiftReceived;
            bool isComplete = rewardData.isComplete;
            bool isUnlocked = allPreviousRewardsReceived;
            bool shouldShowWatch = !hasWatchedAd && !isComplete;
            bool shouldShowClaim = hasWatchedAd && !isComplete;
            bool shouldShowWatchedIndicator = hasWatchedAd;

            if (watchBtnTransform != null)
            {
                watchBtnTransform.gameObject.SetActive(shouldShowWatch);
            }
            if (ClaimedImageTransform != null)
            {
                ClaimedImageTransform.gameObject.SetActive(!shouldShowWatch);
            }

            bool canWatch = isUnlocked && shouldShowWatch && rewardData.countGif > 0;

            if (watchBtn != null)
            {
                watchBtn.interactable = canWatch;

                var watchLabel = watchBtn.GetComponentInChildren<TMP_Text>(true);
                if (watchLabel != null)
                {
                    watchLabel.alpha = canWatch ? 1f : 0.5f;
                }
            }

            if (claimBtnTransform != null)
            {
                claimBtnTransform.gameObject.SetActive(shouldShowClaim);
            }

            if (claimBtn != null)
            {
                claimBtn.interactable = shouldShowClaim;
            }

            if (watchedAdBtnTransform != null)
            {
                watchedAdBtnTransform.gameObject.SetActive(shouldShowWatchedIndicator);

                if (shouldShowWatchedIndicator)
                {
                    if (watchedAdBtn != null)
                    {
                        watchedAdBtn.interactable = false;
                    }

                    ApplyDisabledVisuals(watchedAdBtnTransform);
                }
            }

            allPreviousRewardsReceived = isUnlocked && hasWatchedAd;
        }
    }

    private int ResolvePlayerId()
    {
        if (GameManagerNetWork.Instance != null && GameManagerNetWork.Instance.loginUserModel != null)
        {
            return GameManagerNetWork.Instance.loginUserModel.UserId;
        }

        return DefaultPlayerId;
    }

    public void TryWatchAd(RewardItem reward)
    {
        if (reward == null)
        {
            Debug.LogWarning("Reward data is not available for watching.");
            return;
        }

        if (reward.countGif <= 0)
        {
            Debug.LogWarning("Không còn lượt xem quảng cáo khả dụng cho phần thưởng này.");
            return;
        }

        int watchIdentifier = reward.seq > 0 ? reward.seq : reward.locationId;
        if (watchIdentifier <= 0)
        {
            Debug.LogWarning("Không tìm thấy mã phần thưởng hợp lệ để xem quảng cáo.");
            return;
        }

        int playerId = ResolvePlayerId();
        if (playerId <= 0)
        {
            Debug.LogWarning("Không xác định được người chơi hiện tại để xác nhận xem quảng cáo.");
            return;
        }

        int achievementId = watchIdentifier;
        int locationId = reward.locationId > 0 ? reward.locationId : reward.seq;
        string rewardType = !string.IsNullOrEmpty(reward.rewardType)
            ? reward.rewardType
            : ((int)DefaultRewardType).ToString();

        var adsManager = GoogleAdsRewardManager.Instance;
        if (adsManager == null)
        {
            Debug.LogWarning("GoogleAdsRewardManager chưa được khởi tạo.");
            UIControllerOnline.Instance?.ShowMesByUser("Quảng cáo chưa sẵn sàng. Vui lòng thử lại sau.");
            return;
        }

        // Gọi phương thức mới để xử lý toàn bộ luồng
        adsManager.RequestAndShowAd(watchIdentifier, () =>
        {
            var apiManager = APIManager.Instance;
            if (apiManager == null)
            {
                Debug.LogWarning("APIManager chưa được khởi tạo.");
                return;
            }

            ToggleLoadingScreen(true);

            StartCoroutine(APIManager.Instance.RunTask(
                apiManager.ConfirmRewardClaimAsync(playerId, rewardType, achievementId, locationId),
                updatedReward =>
                {
                    bool success = updatedReward != null;

                    try
                    {
                        if (!success)
                        {
                            Debug.LogWarning("Xác nhận xem quảng cáo thất bại hoặc không trả về dữ liệu phần thưởng.");
                            UIControllerOnline.Instance?.ShowMesByUser("Xác nhận quảng cáo thất bại. Vui lòng thử lại.");
                        }
                        else
                        {
                            reward.UpdateFrom(updatedReward);
                            PlayRewardSuccessSound();
                        }

                        ShowRewardNotification(success);
                        RenderUI();
                    }
                    finally
                    {
                        ToggleLoadingScreen(false);
                    }
                }));
        }, () => {
            // Callback khi quảng cáo không hiển thị được
            UIControllerOnline.Instance?.ShowMesByUser("Quảng cáo không hoàn tất. Vui lòng thử lại.");
            RenderUI();
        });
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

    public void TryClaimReward(RewardItem reward)
    {
        if (reward == null)
        {
            Debug.LogWarning("Reward data is not available for claiming.");
            return;
        }

        int claimIdentifier = reward.locationId > 0 ? reward.locationId : reward.seq;
        if (claimIdentifier <= 0)
        {
            Debug.LogWarning("Không tìm thấy mã phần thưởng hợp lệ để nhận quà.");
            return;
        }

        StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.SendRewardClaimAsync(reward.seq, reward.locationId),
            success =>
            {
                if (!success)
                {
                    return;
                }

                reward.isComplete = true;
                RenderUI();
            }));
    }
 
 
}
