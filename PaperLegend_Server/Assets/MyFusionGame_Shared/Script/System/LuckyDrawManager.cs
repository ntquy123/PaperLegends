using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if !UNITY_SERVER
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Unity.VisualScripting;
#endif

public class RewardButtonModel
{
    public int locationId;
    public bool isUsed;
}

public class LuckyDrawManager : MonoBehaviour
{
    public static LuckyDrawManager Instance;

#if UNITY_SERVER
    private int drawCount = 0;
    private int maxDraw = 3;
    private bool skipInProgress;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetMaxDraw(int value)
    {
        maxDraw = value;
    }

    public void HideExitButton()
    {
        Debug.Log("[LuckyDrawManager][Server] HideExitButton called - no UI available.");
    }

    public void ConfirmDraw()
    {
#if !UNITY_SERVER
        Time.timeScale = 1;
        maxDraw = 0;
        drawCount = 0;

        GameManagerNetWork.Instance?.CloseConnectToRunner();
        DayNightWeatherManager.Instance?.StopEnvironmentSound();
        LoadingManager.LoadScene("Menu");
#endif
    }

    public void OnClickShowDraw()
    {
        Debug.Log("[LuckyDrawManager][Server] OnClickShowDraw called - feature disabled on server build.");
    }

    public void RefreshLuckyDrawRewards()
    {
        Debug.Log("[LuckyDrawManager][Server] RefreshLuckyDrawRewards called - feature disabled on server build.");
    }

    public void OnRewardButtonClicked(RewardButtonModel model)
    {
        Debug.Log("[LuckyDrawManager][Server] OnRewardButtonClicked called - feature disabled on server build.");
    }

    public void OnLuckyDrawClick()
    {
        if (drawCount >= maxDraw)
            return;

        drawCount++;
        Debug.Log("[LuckyDrawManager][Server] Lucky draw requested - UI animation skipped in server build.");
    }

    public void SkipDrawEffect()
    {
        if (skipInProgress)
            return;

        skipInProgress = true;
        drawCount = Mathf.Min(drawCount + 1, maxDraw);
        skipInProgress = false;
    }

#else
        [Header("LUCKY DRAW")]
    public Transform canvasTransform;
    public Image itemImage;
    public TextMeshProUGUI quantityText;
    public GameObject ExitGameButton;
    public Transform RewardsTodayContent;
    public Transform PlayerDrawHistoryContent;
    public GameObject rewardTodayItemPrefab;
    public GameObject drawHistoryItemPrefab;
    public TextMeshProUGUI remainingDrawsText;
    public List<Button> rewardButtons;
    public GameObject tearPrefab;
    private List<RewardButtonModel> rewardButtonModels = new List<RewardButtonModel>();
    private int drawCount = 0;
    private int maxDraw = 3;
    private const int CuliSpriteId = 88000001;

    private FoldedPaperController currentPaper;
    private Button currentSkipButton;
    private bool skipInProgress;

    [Header("LUCKY DRAW PAPER")]
    public GameObject foldedPaperPrefab;
    public GameObject celebrationParticlePrefab;
    public AudioClip celebrationClip;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }


    public void SetMaxDraw(int value)
    {
        maxDraw = value;
    }

    public void HideExitButton()
    {
        if (ExitGameButton != null)
            ExitGameButton.SetActive(false);
    }

    public void ConfirmDraw()
    {
        Time.timeScale = 1;
        maxDraw = 0;
        drawCount = 0;
        GameManagerNetWork.Instance.CloseConnectToRunner();

        GameObject[] objs = GameObject.FindObjectsOfType<GameObject>();
        foreach (var o in objs)
        {
            if (o.name == "LuckyDrawEffect" || o.name == "LuckyDrawItemEffect")
                Destroy(o);
        }
        DayNightWeatherManager.Instance?.StopEnvironmentSound();
        LoadingManager.LoadScene("Menu");
    }

    public void OnClickShowDraw()
    {
        HideExitButton();
        GameOverManager.Instance.EndGamePopup.SetActive(false);
        StartCoroutine(InitRewardButtons());
        //StartCoroutine(PopulateLuckyDrawPopup());
    }

    public void RefreshLuckyDrawRewards()
    {
        StartCoroutine(RefreshRewardsRoutine());
    }

    private IEnumerator RefreshRewardsRoutine()
    {
        int playerId = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.loginUserModel.UserId : 21;
        bool success = false;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.RefreshRewardsAsync(playerId), r => success = r));
        if (success)
        {
            yield return StartCoroutine(InitRewardButtons());
        }
    }

    private IEnumerator PopulateLuckyDrawPopup()
    {
        int playerId = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.loginUserModel.UserId : -1;
        List<LuckyDrawItem> rewards = null;
        List<LuckyDrawResult> history = null;

        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetDailyLuckyDrawItemsAsync(), r => rewards = r));
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetPlayerDailyDrawHistoryAsync(playerId), r => history = r));

        if (RewardsTodayContent != null && rewardTodayItemPrefab != null && rewards != null)
        {
            foreach (Transform child in RewardsTodayContent)
                Destroy(child.gameObject);
            foreach (var item in rewards)
            {
                var go = Instantiate(rewardTodayItemPrefab, RewardsTodayContent);
                var img = go.transform.Find("Icon")?.GetComponent<Image>();
                if (img != null)
                    img.sprite = item.IsCuli ? ItemVisualHelper.LoadSpriteByID(CuliSpriteId) : ItemVisualHelper.LoadSpriteByID(item.ItemId);
                var qty = go.transform.Find("Quantity")?.GetComponent<TextMeshProUGUI>();
                if (qty != null)
                    qty.text = $"x{item.Quantity}";
            }
        }

        if (PlayerDrawHistoryContent != null && drawHistoryItemPrefab != null && history != null)
        {
            foreach (Transform child in PlayerDrawHistoryContent)
                Destroy(child.gameObject);
            foreach (var item in history)
            {
                var go = Instantiate(drawHistoryItemPrefab, PlayerDrawHistoryContent);
                var img = go.transform.Find("Icon")?.GetComponent<Image>();
                if (img != null)
                    img.sprite = item.IsCuli ? ItemVisualHelper.LoadSpriteByID(CuliSpriteId) : ItemVisualHelper.LoadSpriteByID(item.ItemId);
                var qty = go.transform.Find("Quantity")?.GetComponent<TextMeshProUGUI>();
                if (qty != null)
                    qty.text = $"x{item.Quantity}";
            }
            if (remainingDrawsText != null)
                remainingDrawsText.text = $"{Mathf.Max(0, maxDraw - history.Count)}";
        }
    }

    private IEnumerator InitRewardButtons()
    {
        int playerId = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.loginUserModel.UserId : -1;
        List<RewardLocation> locations = null;

        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetRewardLocationsAsync((int)RewardType.LuckyDraw, playerId), r => locations = r));

        if (rewardButtons == null)
            yield break;

        rewardButtonModels.Clear();

        for (int i = 0; i < rewardButtons.Count; i++)
        {
            var btn = rewardButtons[i];
            if (btn == null)
                continue;

            var loc = locations != null ? locations.Find(l => l.locationId == i + 1) : null;
            bool isUsed = loc != null && loc.isUsed;

            var model = new RewardButtonModel {
                locationId = i + 1,
                isUsed = isUsed
            };
            rewardButtonModels.Add(model);

            if (model.isUsed)
            {
                btn.gameObject.SetActive(false);
                if (tearPrefab != null)
                    Instantiate(tearPrefab, btn.transform.position, Quaternion.identity, btn.transform.parent);
            }
            else
            {
                btn.gameObject.SetActive(true);
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnRewardButtonClicked(model));

            }
        }
    }

    public void OnRewardButtonClicked(RewardButtonModel model)
    {
        StartCoroutine(ClaimRewardRoutine(model));
    }

    private IEnumerator ClaimRewardRoutine(RewardButtonModel model)
    {
        int playerId = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.loginUserModel.UserId : -1;
        RewardClaimResponse response = null;

        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.ClaimRewardAsync(playerId, model.locationId, RewardType.LuckyDraw), r => response = r));

        if (response != null)
        {
            model.isUsed = true;
            var btn = rewardButtons[model.locationId - 1];
            if (btn != null)
            {
                btn.gameObject.SetActive(false);
                if (tearPrefab != null)
                    Instantiate(tearPrefab, btn.transform.position, Quaternion.identity, btn.transform.parent);
            }
            ClientGameplayBridge.Popup.ShowRewardReveal(response);
        }
    }

    public void OnLuckyDrawClick()
    {
        if (drawCount >= maxDraw)
            return;

        SoundManager.Instance?.PlayLuckyDrawClick();

        GameObject paper = Instantiate(foldedPaperPrefab, canvasTransform);
        if (paper == null)
            return;

        var rt = paper.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        var text = paper.GetComponentInChildren<TextMeshProUGUI>(true);
        currentPaper = paper.AddComponent<FoldedPaperController>();
        currentPaper.Init(text, celebrationParticlePrefab, celebrationClip);

        currentSkipButton = paper.GetComponentInChildren<Button>(true);
        if (currentSkipButton != null)
        {
            currentSkipButton.onClick.RemoveListener(SkipDrawEffect);
            currentSkipButton.onClick.AddListener(SkipDrawEffect);
            currentSkipButton.interactable = false;
        }

        StartCoroutine(LuckyDrawRoutine(currentPaper));
    }

    private IEnumerator LuckyDrawRoutine(FoldedPaperController controller)
    {
        LuckyDrawResponse result = null;
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.LuckyDrawAsync(playerId), r => result = r));

        if (result == null)
            yield break;

        controller?.SetResult(result);

        if (currentSkipButton != null)
            currentSkipButton.interactable = true;
    }

    public void SkipDrawEffect()
    {
        if (skipInProgress)
            return;
        skipInProgress = true;

        DOTween.KillAll(true);

        if (currentSkipButton != null)
        {
            currentSkipButton.interactable = false;
            currentSkipButton.onClick.RemoveListener(SkipDrawEffect);
            currentSkipButton = null;
        }

        if (currentPaper != null)
        {
            Destroy(currentPaper.gameObject);
            currentPaper = null;
        }

        drawCount++;
        skipInProgress = false;

        if (drawCount < maxDraw)
        {
            OnLuckyDrawClick();
        }
        else if (ExitGameButton != null)
        {
            ExitGameButton.SetActive(true);
        }
    }

    private void ShowLuckyDrawEffect(Sprite sprite, Transform parent)
    {
        if (sprite == null || parent == null)
            return;

        GameObject obj = new GameObject("LuckyDrawEffect", typeof(Image));
        Canvas c = parent.GetComponentInParent<Canvas>();
        obj.transform.SetParent(c != null ? c.transform : parent, false);
        var img = obj.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        var rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(200f, 200f);
        obj.transform.position = parent.position;
        obj.transform.localScale = Vector3.zero;
        CanvasGroup cg = obj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        obj.transform.rotation = Quaternion.identity;
        var seq = DOTween.Sequence();
        seq.Append(cg.DOFade(1f, 0.3f));
        seq.Join(obj.transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack));
        seq.Join(obj.transform.DORotate(new Vector3(0f, 0f, 360f), 0.6f, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
        seq.Append(obj.transform.DOScale(1f, 0.2f));
        seq.AppendInterval(1f);
        seq.Append(cg.DOFade(0f, 0.3f));
        seq.Join(obj.transform.DOScale(0f, 0.3f));
        seq.OnComplete(() =>
        {
            Destroy(obj);
            SkipDrawEffect();
        });
    }

    private void ShowLuckyDrawItemEffect(Sprite sprite)
    {
        if (sprite == null)
            return;

        Canvas canvas = canvasTransform != null ? canvasTransform.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        GameObject obj = new GameObject("LuckyDrawItemEffect", typeof(Image));
        obj.transform.SetParent(canvas.transform, false);
        var img = obj.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(300f, 300f);
        obj.transform.localScale = Vector3.zero;
        CanvasGroup cg = obj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        var seq = DOTween.Sequence();
        seq.Append(cg.DOFade(1f, 0.4f));
        seq.Join(obj.transform.DOScale(1.3f, 0.4f).SetEase(Ease.OutBack));
        seq.AppendInterval(1f);
        seq.Append(cg.DOFade(0f, 0.3f));
        seq.Join(obj.transform.DOScale(0f, 0.3f));
        seq.OnComplete(() =>
        {
            Destroy(obj);
            SkipDrawEffect();
        });
    }

    private void OnDisable()
    {
        DOTween.Kill(transform);
    }
#endif
}

