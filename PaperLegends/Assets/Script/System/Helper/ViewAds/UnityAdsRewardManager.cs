/*using UnityEngine;
using UnityEngine.Advertisements;
using System;

public class UnityAdsRewardManager : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener, IUnityAdsInitializationListener
{
    public static UnityAdsRewardManager Instance;

    [SerializeField] private string androidAdUnitId = "Rewarded_Android"; 
    [SerializeField] private string iosAdUnitId = "Rewarded_iOS";

    private string adUnitId;
    private Action onCompleteCallback;
    private bool isAdLoaded = false;
    public bool IsAdReady => isAdLoaded;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

#if UNITY_IOS
        adUnitId = iosAdUnitId;
#else
        adUnitId = androidAdUnitId;
#endif

#if UNITY_EDITOR

        Advertisement.Initialize(GetGameId(), true, this);
#else
     
        Advertisement.Initialize(GetGameId(), true, this); // ← đổi thành false khi release
#endif
    }

    private string GetGameId()
    {
#if UNITY_IOS
        return "5874204"; // iOS Game ID từ dashboard
#else
        return "5874205"; // Android Game ID từ dashboard
#endif
    }

    public void ShowAd(int index, Action onComplete)
    {
        Debug.LogWarning("Đang bật quảng cáo..");
        if (!isAdLoaded)
        {
            Debug.LogWarning("Quảng cáo chưa sẵn sàng, đang load lại...");
            Advertisement.Load(adUnitId, this);
            return;
        }

        onCompleteCallback = onComplete;
        Advertisement.Show(adUnitId, this);
        isAdLoaded = false; // reset flag, cần load lại sau mỗi lượt xem
    }

    // ✅ Callback khi quảng cáo đã load
    public void OnUnityAdsAdLoaded(string placementId)
    {
        if (placementId == adUnitId)
        {
            Debug.Log("✅ Quảng cáo đã sẵn sàng");
            isAdLoaded = true;
        }
        else
        {
            Debug.Log("✅ Quảng cáo không thể load");
        }    
    }

    public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
    {
        Debug.LogError($"❌ Lỗi load quảng cáo: {message}");
        isAdLoaded = false;
    }

    // ✅ Callback khi xem quảng cáo xong
    public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
    {
        if (placementId == adUnitId && showCompletionState == UnityAdsShowCompletionState.COMPLETED)
        {
            Debug.Log("🎉 Người chơi đã xem hết quảng cáo!");
            onCompleteCallback?.Invoke();
        }
        else
        {
            Debug.Log("⚠️ Quảng cáo không được xem hết");
        }

        onCompleteCallback = null;
        Advertisement.Load(adUnitId, this); // Load lại sau mỗi lần show
    }

    public void OnUnityAdsShowStart(string placementId) { }
    public void OnUnityAdsShowClick(string placementId) { }
    public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
    {
        Debug.LogError($"❌ Lỗi hiển thị quảng cáo: {message}");
        onCompleteCallback = null;
        Advertisement.Load(adUnitId, this);
    }

    public void OnInitializationComplete()
    {
        Debug.Log($"Unity Ads đã cài xong id: {GetGameId()}");
        Advertisement.Load(adUnitId, this);
    }

    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        Debug.LogError($"Unity Ads Initialization failed: {error} - {message}");
    }
}
*/