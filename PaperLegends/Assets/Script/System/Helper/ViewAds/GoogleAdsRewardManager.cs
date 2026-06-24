using System;
using System.Collections;
using GoogleMobileAds.Api;
using UnityEngine;

public class GoogleAdsRewardManager : MonoBehaviour
{
    // GoogleMobileAds v10.4.2
    public static GoogleAdsRewardManager Instance { get; private set; }

    [SerializeField] private string androidAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    [SerializeField] private string iosAdUnitId = "ca-app-pub-3940256099942544/1712485313";
    [SerializeField] private float retryDelaySeconds = 10f;

    private RewardedAd rewardedAd;
    private bool isInitialized;
    private bool isLoading;
    private bool isPaused;
    private Coroutine retryCoroutine;

    // Hàng đợi callback khi user bấm xem lúc chưa sẵn sàng
    private Action _onPendingRewardCompleted;
    private Action _onPendingRewardAborted;
    private int _pendingRewardIndex = -1;

    public bool IsAdReady => rewardedAd != null && rewardedAd.CanShowAd();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Đảm bảo callback của GMA chạy trên Unity main thread
        MobileAds.RaiseAdEventsOnUnityMainThread = true;

        MobileAds.Initialize(initStatus =>
        {
            isInitialized = true;
            Debug.Log("Google Mobile Ads đã khởi tạo thành công.");
            LoadAd();
        });
    }

    private void OnApplicationPause(bool pause) => isPaused = pause;

    public void LoadAd()
    {
        if (!isInitialized || IsAdReady || isLoading) return;

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("Không có kết nối mạng. Sẽ thử tải lại quảng cáo rewarded sau.");
            ScheduleRetry();
            return;
        }

        string adUnitId = GetAdUnitId();
        if (string.IsNullOrEmpty(adUnitId))
        {
            Debug.LogWarning("Ad Unit Id của quảng cáo rewarded chưa được cấu hình.");
            return;
        }

        isLoading = true;

        var request = new AdRequest();
        RewardedAd.Load(adUnitId, request, (RewardedAd newAd, LoadAdError loadError) =>
        {
            isLoading = false;

            if (loadError != null || newAd == null)
            {
                Debug.LogWarning($"Tải quảng cáo rewarded thất bại: {loadError}");
                ScheduleRetry();
                return;
            }

            // ✅ KHÔNG destroy ad mới. Hủy ad cũ trước rồi thay thế.
            ReplaceAd(newAd);
            Debug.Log("Tải quảng cáo rewarded thành công.");

            // Nếu có yêu cầu hiển thị đang chờ → show ở frame kế cho an toàn
            if (_onPendingRewardCompleted != null)
            {
                StartCoroutine(ShowOnNextFrame(_pendingRewardIndex, _onPendingRewardCompleted, _onPendingRewardAborted));
                ClearPendingCallbacks();
            }
        });
    }

    // API cho game gọi
    public void RequestAndShowAd(int rewardIndex, Action onComplete, Action onFailed = null)
    {
        // Không show khi đang pause (ví dụ app minimize hoặc đang đổi scene)
        if (isPaused)
        {
            Debug.Log("Ứng dụng đang pause. Hoãn hiển thị quảng cáo.");
            onFailed?.Invoke();
            return;
        }

        if (IsAdReady)
        {
            // ✅ Luôn show ở frame kế để tránh show ngay trong callback load
            StartCoroutine(ShowOnNextFrame(rewardIndex, onComplete, onFailed));
        }
        else
        {
            // Hàng đợi 1 yêu cầu pending
            _onPendingRewardCompleted = onComplete;
            _onPendingRewardAborted = onFailed;
            _pendingRewardIndex = rewardIndex;

            Debug.LogWarning("Quảng cáo rewarded chưa sẵn sàng. Đang tải lại và sẽ hiển thị sau.");
            LoadAd();
        }
    }

    // Luôn đợi qua 1 frame trước khi gọi Show() để chắc main thread & render sẵn sàng
    private IEnumerator ShowOnNextFrame(int rewardIndex, Action onComplete, Action onFailed)
    {
        yield return null; // đợi qua frame kế
        yield return new WaitForEndOfFrame(); // và chờ render sync thêm 1 nhịp

        if (!IsAdReady || isPaused)
        {
            Debug.LogWarning("Quảng cáo rewarded không thể hiển thị ngay.");
            onFailed?.Invoke();
            yield break;
        }

        Debug.Log($"Hiển thị quảng cáo rewarded cho reward index: {rewardIndex}");

        // Show - callback reward chạy trên main thread (đã set RaiseAdEventsOnUnityMainThread)
        rewardedAd.Show((Reward reward) =>
        {
            Debug.Log($"Người chơi nhận thưởng từ quảng cáo: {reward.Type} x{reward.Amount} (index: {rewardIndex}).");
            onComplete?.Invoke();
            ClearPendingCallbacks();
        });
    }

    // Thay thế ad hiện tại bằng ad mới, đăng ký callback vòng đời
    private void ReplaceAd(RewardedAd newAd)
    {
        if (rewardedAd != null)
        {
            try { rewardedAd.Destroy(); } catch { /* ignore */ }
            rewardedAd = null;
        }

        rewardedAd = newAd;

        // Đăng ký callback cho ad mới
        rewardedAd.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Quảng cáo rewarded đã đóng.");
            ClearAd();
            LoadAd();
        };

        rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogWarning($"Quảng cáo rewarded gặp lỗi khi hiển thị: {error}");
            ClearAd();
            LoadAd();
            _onPendingRewardAborted?.Invoke();
            ClearPendingCallbacks();
        };

        // Nếu đang có retry schedule thì huỷ
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }
    }

    private void ClearAd()
    {
        if (rewardedAd == null) return;
        try { rewardedAd.Destroy(); } catch { /* ignore */ }
        rewardedAd = null;
    }

    private void ClearPendingCallbacks()
    {
        _onPendingRewardCompleted = null;
        _onPendingRewardAborted = null;
        _pendingRewardIndex = -1;
    }

    private void ScheduleRetry()
    {
        if (retryDelaySeconds <= 0f || retryCoroutine != null) return;
        retryCoroutine = StartCoroutine(RetryLoadCoroutine());
    }

    private IEnumerator RetryLoadCoroutine()
    {
        yield return new WaitForSeconds(retryDelaySeconds);
        retryCoroutine = null;
        LoadAd();
    }

    private string GetAdUnitId()
    {
#if UNITY_IOS
        return iosAdUnitId;
#elif UNITY_ANDROID
        return androidAdUnitId;
#else
        return androidAdUnitId;
#endif
    }
}
