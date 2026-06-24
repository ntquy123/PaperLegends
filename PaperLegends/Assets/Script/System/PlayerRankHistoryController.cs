using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerRankHistoryController : MonoBehaviour
{
    [Serializable]
    public class RankDefinition
    {
        [SerializeField]
        private string displayName = "";

        [SerializeField]
        private string visualNote = "";

        [SerializeField]
        private Sprite icon;

        [SerializeField]
        private int minPoints;

        [SerializeField]
        private int maxPoints = -1;

        [SerializeField]
        private int starsToAdvance = 3;

        public RankDefinition()
        {
        }

        public RankDefinition(string displayName, string visualNote, int minPoints, int maxPoints, int starsToAdvance)
        {
            this.displayName = displayName;
            this.visualNote = visualNote;
            this.minPoints = minPoints;
            this.maxPoints = maxPoints;
            this.starsToAdvance = starsToAdvance;
        }

        public string DisplayName
        {
            get
            {
                if (LocalizationManager.Instance != null)
                {
                    return LocalizationManager.Instance.GetText(displayName);
                }

                return displayName;
            }
        }

        public string VisualNote
        {
            get
            {
                if (LocalizationManager.Instance != null)
                {
                    return LocalizationManager.Instance.GetText(visualNote);
                }

                return visualNote;
            }
        }
        public Sprite Icon => icon;
        public int MinPoints => minPoints;
        public int MaxPoints => maxPoints;
        public int StarsToAdvance => starsToAdvance;
        public bool HasUpperBound => maxPoints >= 0;
    }

    public static PlayerRankHistoryController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField]
    private TMP_Text totalMatchesText;

    [SerializeField]
    private TMP_Text winRateText;

    [SerializeField]
    private TMP_Text rankPointsText;

    [SerializeField]
    private TMP_Text remainingBallsAfterBetText;

    [SerializeField]
    private int rankMatchBetCost = 7;

    [SerializeField]
    private TMP_Text rankNameText;

    [SerializeField]
    private TMP_Text nextRankText;

    [SerializeField]
    private Image rankIcon;

    [SerializeField]
    private Slider rankProgressSlider;

    [SerializeField]
    private GameObject starPrefab;

    [SerializeField]
    private Transform starContainer;

    private readonly List<StarVisual> starVisuals = new();

    [SerializeField]
    private Color earnedStarColor = Color.yellow;

    [SerializeField]
    private Color pendingStarColor = Color.gray;

    [Header("Instruction")]
    private string rankInstructionDescription = "rankInstruction_1";

    [Header("History")]
    [SerializeField]
    private GameObject historyItemPrefab;

    [SerializeField]
    private int historyPageSize = 10;

    [SerializeField, Range(0.01f, 0.5f)]
    private float historyLoadMoreThreshold = 0.05f;

    [Header("History Colors")]
    [SerializeField]
    private Color winGradientTopLeft = new(1f, 0.85f, 0.35f);

    [SerializeField]
    private Color winGradientTopRight = new(1f, 0.69f, 0.16f);

    [SerializeField]
    private Color winGradientBottomLeft = new(1f, 0.56f, 0.13f);

    [SerializeField]
    private Color winGradientBottomRight = new(1f, 0.79f, 0.36f);

    [SerializeField]
    private Color nonWinGradientTopLeft = new(0.74f, 0.76f, 0.8f);

    [SerializeField]
    private Color nonWinGradientTopRight = new(0.94f, 0.96f, 0.98f);

    [SerializeField]
    private Color nonWinGradientBottomLeft = new(0.63f, 0.77f, 0.93f);

    [SerializeField]
    private Color nonWinGradientBottomRight = new(0.82f, 0.86f, 0.9f);

    [SerializeField]
    private Color rankPointPositiveColor = new(0.18f, 0.74f, 0.39f);

    [SerializeField]
    private Color rankPointNegativeColor = new(0.86f, 0.23f, 0.23f);

    [SerializeField]
    private Color rankPointNeutralColor = Color.white;

    [Header("Rank Definition")]
    [SerializeField]
    private List<RankDefinition> rankDefinitions = new();

    [Header("Rank Definition Panel")]
    [SerializeField]
    private GameObject rankDefinitionItemPrefab;

    private class StarVisual
    {
        public GameObject Instance;
        public Image Image;
        public Renderer Renderer;
    }

    private readonly List<PlayerMatchHistory> loadedMatchHistories = new();
    private bool isLoadingHistory;
    private bool hasMoreHistory = true;
    private int currentHistoryPage = 0;
    private int historyPlayerId = -1;
    private PlayerRankHistoryPopupUI historyPopupUI;
    private RankDefinitionPopupUI rankDefinitionPopupUI;
    private PlayerRankLeaderboardPopupUI leaderboardPopupUI;
    private const string RemainingBallsAfterBetTextName = "RemainingBallsAfterBetText";

    private struct RankVisualData
    {
        public RankDefinition CurrentRank;
        public RankDefinition NextRank;
        public int EarnedStars;
        public float PartialStar;
        public float ProgressToNext;
        public int PointsIntoRank;
        public bool IsTopRank;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureDefaultRankDefinitions();
    }

    private void OnEnable()
    {
        AttachHistoryScrollListener();
        UpdateRemainingBallsAfterBetText();
    }

    private void OnDisable()
    {
        DetachHistoryScrollListener();
    }

    public void ShowRankLeaderboardPanel()
    {
        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("PopupHelper instance is not available. Cannot show rank leaderboard popup.");
            return;
        }

        leaderboardPopupUI = PopupHelper.Instance.ShowRankLeaderboardPopup(OnRankLeaderboardPopupClosed);
        if (leaderboardPopupUI == null)
        {
            return;
        }

        RefreshRankLeaderboard();
    }

    public void HideRankLeaderboardPanel()
    {
        CloseRankLeaderboardPopup();
    }

    public void ShowRankDefinitionPanel()
    {
        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("PopupHelper instance is not available. Cannot show rank definition popup.");
            return;
        }

        rankDefinitionPopupUI = PopupHelper.Instance.ShowRankDefinitionPopup(OnRankDefinitionPopupClosed);
        if (rankDefinitionPopupUI == null)
        {
            return;
        }

        RefreshRankDefinitions();
    }

    public void HideRankDefinitionPanel()
    {
        CloseRankDefinitionPopup();
    }

    private void RefreshRankDefinitions()
    {
        var rankDefinitionContent = rankDefinitionPopupUI?.RankDefinitionContent;
        if (rankDefinitionContent == null || rankDefinitionItemPrefab == null)
        {
            Debug.LogWarning("Rank definition content or item prefab is missing.");
            return;
        }

        foreach (Transform child in rankDefinitionContent)
        {
            Destroy(child.gameObject);
        }

        EnsureDefaultRankDefinitions();

        foreach (var rank in rankDefinitions.OrderBy(r => r.MinPoints))
        {
            var item = Instantiate(rankDefinitionItemPrefab, rankDefinitionContent);
            ApplyRankDefinition(item.transform, rank);
        }
    }

    public void ShowRankInstructionPopup()
    {
        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("PopupHelper instance is not available. Cannot show instruction popup.");
            return;
        }

        string description = string.IsNullOrWhiteSpace(rankInstructionDescription)
            ? "Hướng dẫn chưa được cập nhật."
            : rankInstructionDescription.Trim();

        PopupHelper.Instance.ShowInstructionPopup(description);
    }

    public void ButtonClickShowHistory()
    {
        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("PopupHelper instance is not available. Cannot show rank history popup.");
            return;
        }

        historyPopupUI = PopupHelper.Instance.ShowRankHistoryPopup(OnHistoryPopupClosed);
        if (historyPopupUI == null)
        {
            return;
        }

        AttachHistoryScrollListener();
        RefreshPlayerMatchHistory();
    }

    public void ButtonClickCloseHistory()
    {
        CloseHistoryPopup();
    }

    private void OnHistoryPopupClosed()
    {
        DetachHistoryScrollListener();
        historyPopupUI = null;
    }

    private void CloseHistoryPopup()
    {
        if (historyPopupUI == null)
        {
            return;
        }

        DetachHistoryScrollListener();
        PopupHelper.Instance?.CloseActivePopup();
        historyPopupUI = null;
    }

    private void OnRankDefinitionPopupClosed()
    {
        rankDefinitionPopupUI = null;
    }

    private void OnRankLeaderboardPopupClosed()
    {
        leaderboardPopupUI = null;
    }

    private void CloseRankDefinitionPopup()
    {
        if (rankDefinitionPopupUI == null)
        {
            return;
        }

        PopupHelper.Instance?.CloseActivePopup();
        rankDefinitionPopupUI = null;
    }

    private void CloseRankLeaderboardPopup()
    {
        if (leaderboardPopupUI == null)
        {
            return;
        }

        PopupHelper.Instance?.CloseActivePopup();
        leaderboardPopupUI = null;
    }

    private void AttachHistoryScrollListener()
    {
        var historyScrollRect = historyPopupUI?.HistoryScrollRect;
        if (historyScrollRect != null)
        {
            historyScrollRect.onValueChanged.RemoveListener(OnHistoryScroll);
            historyScrollRect.onValueChanged.AddListener(OnHistoryScroll);
        }
    }

    private void DetachHistoryScrollListener()
    {
        var historyScrollRect = historyPopupUI?.HistoryScrollRect;
        if (historyScrollRect != null)
        {
            historyScrollRect.onValueChanged.RemoveListener(OnHistoryScroll);
        }
    }

    public void RefreshPlayerRankStats()
    {
        if (APIManager.Instance == null)
        {
            Debug.LogWarning("APIManager instance is not available. Cannot refresh player rank stats.");
            return;
        }

        var network = GameManagerNetWork.Instance;
        if (network?.loginUserModel == null)
        {
            Debug.LogWarning("Login data is not available. Cannot refresh player rank stats.");
            return;
        }

        int playerId = network.loginUserModel.UserId;
        UpdateRemainingBallsAfterBetText();
        StartCoroutine(RefreshPlayerRankStatsCoroutine(playerId));

    }

    public void RefreshRankLeaderboard()
    {
        if (APIManager.Instance == null)
        {
            Debug.LogWarning("APIManager instance is not available. Cannot refresh rank leaderboard.");
            return;
        }

        if (leaderboardPopupUI == null)
        {
            Debug.LogWarning("Leaderboard popup is not available. Cannot refresh rank leaderboard.");
            return;
        }

        if (leaderboardPopupUI.LeaderboardContent == null || leaderboardPopupUI.LeaderboardItemPrefab == null)
        {
            Debug.LogWarning("Leaderboard content or item prefab is missing.");
            return;
        }

        var network = GameManagerNetWork.Instance;
        if (network?.loginUserModel == null)
        {
            Debug.LogWarning("Login data is not available. Cannot refresh rank leaderboard.");
            return;
        }

        int playerId = network.loginUserModel.UserId;
        StartCoroutine(RefreshRankLeaderboardCoroutine(playerId));
    }

    public void RefreshPlayerMatchHistory()
    {
        if (APIManager.Instance == null)
        {
            Debug.LogWarning("APIManager instance is not available. Cannot refresh player match history.");
            return;
        }

        var historyContent = historyPopupUI?.HistoryContent;
        if (historyContent == null || historyItemPrefab == null)
        {
            Debug.LogWarning("History content or item prefab is missing.");
            return;
        }

        var network = GameManagerNetWork.Instance;
        if (network?.loginUserModel == null)
        {
            Debug.LogWarning("Login data is not available. Cannot refresh player match history.");
            return;
        }

        historyPlayerId = network.loginUserModel.UserId;
        ResetHistoryPagination();
        TryLoadNextHistoryPage();
    }

    private void OnHistoryScroll(Vector2 normalizedPosition)
    {
        if (normalizedPosition.y <= historyLoadMoreThreshold)
        {
            TryLoadNextHistoryPage();
        }
    }

    private IEnumerator RefreshPlayerRankStatsCoroutine(int playerId)
    {
        PlayerHistoryStats stats = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetPlayerHistoryStatsAsync(playerId),
            result => stats = result));

        ApplyStatsToUI(stats);
    }

    private IEnumerator RefreshRankLeaderboardCoroutine(int playerId)
    {
        PlayerRankLeaderboardResponse response = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetRankLeaderboardAsync(playerId),
            result => response = result));

        PopulateLeaderboard(response);
    }

    private void ResetHistoryPagination()
    {
        currentHistoryPage = 0;
        hasMoreHistory = true;
        isLoadingHistory = false;
        loadedMatchHistories.Clear();
        ClearHistoryContent();
    }

    private void TryLoadNextHistoryPage()
    {
        if (isLoadingHistory || !hasMoreHistory || historyPlayerId <= 0)
        {
            return;
        }

        currentHistoryPage++;
        StartCoroutine(LoadHistoryPageCoroutine(historyPlayerId, currentHistoryPage, historyPageSize));
    }

    private IEnumerator LoadHistoryPageCoroutine(int playerId, int page, int pageSize)
    {
        isLoadingHistory = true;

        List<PlayerMatchHistory> pageResult = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetPlayerMatchHistoriesAsync(playerId, page, pageSize),
            result => pageResult = result));

        isLoadingHistory = false;

        if (pageResult == null || pageResult.Count == 0)
        {
            hasMoreHistory = false;
            yield break;
        }

        loadedMatchHistories.AddRange(pageResult);
        if (pageResult.Count < pageSize)
        {
            hasMoreHistory = false;
        }

        AppendHistoryEntries(pageResult);
    }

    private void ApplyStatsToUI(PlayerHistoryStats stats)
    {
        if (stats == null)
        {
            return;
        }

        UpdateBasicTexts(stats);
        UpdateRankVisuals(stats.totalRankPoints);
        UpdateRemainingBallsAfterBetText();
    }

    private void UpdateBasicTexts(PlayerHistoryStats stats)
    {
        if (totalMatchesText != null)
        {
            totalMatchesText.text = stats.totalMatches.ToString();
        }

        if (winRateText != null)
        {
            winRateText.text = FormatWinRate(stats.winRate);
        }

        if (rankPointsText != null)
        {
            int displayPoints = Mathf.Max(0, stats.totalRankPoints);
            rankPointsText.text = displayPoints.ToString("N0");
        }
    }

    private void UpdateRemainingBallsAfterBetText()
    {
        if (remainingBallsAfterBetText == null)
        {
            remainingBallsAfterBetText = ResolveRemainingBallsAfterBetText();
        }

        if (remainingBallsAfterBetText == null)
        {
            return;
        }

        if (!TryGetCurrentPlayerRingBall(out int currentRingBall))
        {
            remainingBallsAfterBetText.gameObject.SetActive(false);
            return;
        }

        int remainingAfterBet = currentRingBall - rankMatchBetCost;
        remainingBallsAfterBetText.gameObject.SetActive(true);
        remainingBallsAfterBetText.text = FormatRemainingBallsAfterBetText(remainingAfterBet);
        remainingBallsAfterBetText.color = remainingAfterBet >= 0 ? Color.black : rankPointNegativeColor;
    }

    private static string FormatRemainingBallsAfterBetText(int remainingAfterBet)
    {
        return $"{remainingAfterBet:N0}";
    }

    private bool TryGetCurrentPlayerRingBall(out int ringBall)
    {
        var inventory = UserInfoHandler.Instance?.PlayerInventory;
        if (inventory != null)
        {
            ringBall = inventory.RingBall;
            return true;
        }

        var loginUser = GameManagerNetWork.Instance?.loginUserModel;
        if (loginUser != null)
        {
            ringBall = loginUser.RingBall;
            return true;
        }

        ringBall = 0;
        return false;
    }

    private TMP_Text ResolveRemainingBallsAfterBetText()
    {
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null && string.Equals(text.name, RemainingBallsAfterBetTextName, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }
        }

        return CreateRemainingBallsAfterBetText();
    }

    private TMP_Text CreateRemainingBallsAfterBetText()
    {
        TMP_Text sourceText = rankNameText != null ? rankNameText : rankPointsText != null ? rankPointsText : totalMatchesText;
        Transform parent = sourceText != null && sourceText.transform.parent != null ? sourceText.transform.parent : transform;

        var textObject = new GameObject(RemainingBallsAfterBetTextName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        if (sourceText != null)
        {
            text.font = sourceText.font;
            text.fontSharedMaterial = sourceText.fontSharedMaterial;
            text.fontSize = Mathf.Max(20f, sourceText.fontSize * 0.72f);
            text.alignment = sourceText.alignment;
        }
        else
        {
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.Center;
        }

        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.color = Color.black;

        var rect = text.rectTransform;
        if (sourceText != null)
        {
            var sourceRect = sourceText.rectTransform;
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = sourceRect.pivot;
            rect.sizeDelta = sourceRect.sizeDelta;
            rect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -46f);
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 44f);
            rect.anchoredPosition = Vector2.zero;
        }

        return text;
    }

    private void UpdateRankVisuals(int totalRankPoints)
    {
        var visualData = CalculateRankVisualData(totalRankPoints);
        if (visualData.CurrentRank == null)
        {
            return;
        }

        if (rankNameText != null)
        {
            rankNameText.text = visualData.CurrentRank.DisplayName;
        }

        if (rankIcon != null)
        {
            rankIcon.sprite = visualData.CurrentRank.Icon;
            rankIcon.enabled = visualData.CurrentRank.Icon != null;
        }

        UpdateStarDisplay(visualData);
        UpdateRemainingPointsUI(visualData);

        if (rankProgressSlider != null)
        {
            rankProgressSlider.normalizedValue = visualData.ProgressToNext;
        }

        if (nextRankText != null)
        {
            if (visualData.CurrentRank.HasUpperBound && visualData.NextRank != null)
            {
                int pointsNeeded = Mathf.Max(0, visualData.CurrentRank.MaxPoints - totalRankPoints);
                nextRankText.text = $"Cần {pointsNeeded} điểm để lên {visualData.NextRank.DisplayName}";
            }
            else
            {
                int extraPoints = Mathf.Max(0, visualData.PointsIntoRank);
                nextRankText.text = $"Đã đạt hạng cao nhất • Điểm còn dư: {extraPoints:N0}";
            }
        }
    }

    private void UpdateStarDisplay(RankVisualData visualData)
    {
        if (visualData.IsTopRank)
        {
            SetStarContainerActive(false);
            return;
        }

        if (!EnsureStarInstances(visualData.CurrentRank.StarsToAdvance))
        {
            return;
        }

        SetStarContainerActive(true);

        for (int i = 0; i < starVisuals.Count; i++)
        {
            var star = starVisuals[i];
            bool shouldShow = i < visualData.CurrentRank.StarsToAdvance;
            if (star.Instance != null)
            {
                star.Instance.SetActive(shouldShow);
            }

            if (!shouldShow)
            {
                continue;
            }

            bool isEarnedStar = i < visualData.EarnedStars;
            bool isPartialStar = i == visualData.EarnedStars && visualData.PartialStar > 0f;
            ApplyStarVisual(star, isEarnedStar, isPartialStar ? visualData.PartialStar : 0f);
        }
    }

    private void UpdateRemainingPointsUI(RankVisualData visualData)
    {
        var remainingText = leaderboardPopupUI?.RemainingPointsText;
        if (remainingText == null)
        {
            return;
        }

        bool showRemaining = visualData.IsTopRank;
        remainingText.gameObject.SetActive(showRemaining);
        if (showRemaining)
        {
            remainingText.text = $"Điểm còn dư: {Mathf.Max(0, visualData.PointsIntoRank):N0}";
        }
    }

    private void SetStarContainerActive(bool active)
    {
        if (starContainer != null)
        {
            starContainer.gameObject.SetActive(active);
        }
    }

    private bool EnsureStarInstances(int totalStars)
    {
        if (starContainer == null || starPrefab == null)
        {
            Debug.LogWarning("Star container or prefab is missing. Cannot display rank stars.");
            return false;
        }

        while (starVisuals.Count < totalStars)
        {
            var starObject = Instantiate(starPrefab, starContainer);
            var visual = new StarVisual
            {
                Instance = starObject,
                Image = starObject.GetComponentInChildren<Image>(true),
                Renderer = starObject.GetComponentInChildren<Renderer>(true)
            };
            starVisuals.Add(visual);
        }

        for (int i = starVisuals.Count - 1; i >= totalStars; i--)
        {
            if (starVisuals[i].Instance != null)
            {
                Destroy(starVisuals[i].Instance);
            }

            starVisuals.RemoveAt(i);
        }

        return true;
    }

    private void ApplyStarVisual(StarVisual star, bool isEarned, float partialFill)
    {
        Color targetColor = Color.Lerp(pendingStarColor, earnedStarColor, isEarned ? 1f : Mathf.Clamp01(partialFill));

        if (star.Image != null)
        {
            star.Image.color = targetColor;
            if (star.Image.type == Image.Type.Filled)
            {
                star.Image.fillAmount = isEarned ? 1f : Mathf.Clamp01(partialFill);
            }
        }

        if (star.Renderer != null)
        {
            var material = star.Renderer.material;
            if (material != null)
            {
                material.color = targetColor;
            }
        }
    }

    private void PopulateLeaderboard(PlayerRankLeaderboardResponse response)
    {
        var leaderboardContent = leaderboardPopupUI?.LeaderboardContent;
        if (leaderboardContent == null)
        {
            Debug.LogWarning("Leaderboard content is missing. Cannot populate leaderboard.");
            return;
        }

        foreach (Transform child in leaderboardContent)
        {
            Destroy(child.gameObject);
        }

        if (response?.leaderboard != null)
        {
            foreach (var entry in response.leaderboard)
            {
                var item = Instantiate(leaderboardPopupUI.LeaderboardItemPrefab, leaderboardContent);
                ApplyLeaderboardEntry(item.transform, entry);
            }
        }

        UpdatePlayerRankDisplay(response?.playerRank);
    }

    private void ClearHistoryContent()
    {
        var historyContent = historyPopupUI?.HistoryContent;
        if (historyContent == null)
        {
            return;
        }

        foreach (Transform child in historyContent)
        {
            Destroy(child.gameObject);
        }
    }

    private void AppendHistoryEntries(IEnumerable<PlayerMatchHistory> entries)
    {
        var historyContent = historyPopupUI?.HistoryContent;
        if (historyContent == null || historyItemPrefab == null)
        {
            Debug.LogWarning("History content or item prefab is missing. Cannot populate history.");
            return;
        }

        if (entries == null)
        {
            return;
        }

        foreach (var entry in entries)
        {
            var item = Instantiate(historyItemPrefab, historyContent);
            ApplyHistoryEntry(item.transform, entry);
        }
    }

    private void UpdatePlayerRankDisplay(PlayerRankLeaderboardEntry playerRank)
    {
        var playerRankDisplay = leaderboardPopupUI?.PlayerRankText;
        if (playerRankDisplay == null)
        {
            return;
        }

        if (playerRank == null || playerRank.position <= 0)
        {
            playerRankDisplay.text = "không có hạng";
            return;
        }

        playerRankDisplay.text = playerRank.position.ToString();
    }

    private void ApplyLeaderboardEntry(Transform item, PlayerRankLeaderboardEntry entry)
    {
        if (item == null)
        {
            return;
        }

        SetTextOnChild(item, "PlayerNameText", entry.playerName);
        SetTextOnChild(item, "LevelText", entry.level.ToString());
        SetTextOnChild(item, "RingBallText", entry.ringBall.ToString("N0"));
        SetTextOnChild(item, "RankPointsText", entry.totalRankPoints.ToString("N0"));
        UpdateLeaderboardPosition(item, entry.position);

        var rankVisual = CalculateRankVisualData(entry.totalRankPoints);
        SetTextOnChild(item, "RankNameText", rankVisual.CurrentRank != null ? rankVisual.CurrentRank.DisplayName : string.Empty);

        var rankIconImage = item.Find("RankIcon")?.GetComponent<Image>();
        if (rankIconImage != null)
        {
            rankIconImage.sprite = rankVisual.CurrentRank?.Icon;
            rankIconImage.enabled = rankIconImage.sprite != null;
        }

        var starContainerTransform = item.Find("StarContainer");
        UpdateLeaderboardStars(starContainerTransform, rankVisual);

        var remainingPoints = item.Find("RemainingPointsText")?.GetComponent<TMP_Text>();
        UpdateLeaderboardRemainingPoints(remainingPoints, rankVisual);
    }

    private void UpdateLeaderboardPosition(Transform item, int position)
    {
        if (item == null)
        {
            return;
        }
        Debug.Log("Hạng" + position);
        var positionText = item.Find("RankPositionText")?.GetComponent<TMP_Text>();
        var medalImage = item.Find("RankPositionMedal")?.GetComponent<Image>();
        bool hasMedal = position >= 1 && position <= 3;

        if (positionText != null)
        {
            positionText.gameObject.SetActive(!hasMedal);
            positionText.text = position > 0 ? position.ToString() : string.Empty;
        }

        if (medalImage != null)
        {
            medalImage.gameObject.SetActive(hasMedal);
            medalImage.sprite = hasMedal ? GetMedalSprite(position) : null;
            medalImage.enabled = medalImage.gameObject.activeSelf && medalImage.sprite != null;
        }
    }

    private Sprite GetMedalSprite(int position)
    {
        if (leaderboardPopupUI == null)
        {
            return null;
        }

        return position switch
        {
            1 => leaderboardPopupUI.GoldMedalSprite,
            2 => leaderboardPopupUI.SilverMedalSprite,
            3 => leaderboardPopupUI.BronzeMedalSprite,
            _ => null
        };
    }

    private void UpdateLeaderboardStars(Transform container, RankVisualData visualData)
    {
        if (container == null)
        {
            return;
        }

        bool showStars = !visualData.IsTopRank && visualData.CurrentRank != null && visualData.CurrentRank.StarsToAdvance > 0;
        container.gameObject.SetActive(showStars);

        if (!showStars)
        {
            return;
        }

        if (starPrefab == null)
        {
            Debug.LogWarning("Star prefab is missing. Cannot display leaderboard stars.");
            return;
        }

        int totalStars = visualData.CurrentRank.StarsToAdvance;
        for (int i = container.childCount; i < totalStars; i++)
        {
            Instantiate(starPrefab, container);
        }

        for (int i = container.childCount - 1; i >= totalStars; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }

        for (int i = 0; i < container.childCount; i++)
        {
            var starTransform = container.GetChild(i);
            bool isEarned = i < visualData.EarnedStars;
            bool isPartial = i == visualData.EarnedStars && visualData.PartialStar > 0f;
            float partialFill = isPartial ? visualData.PartialStar : 0f;

            var image = starTransform.GetComponentInChildren<Image>(true);
            var renderer = starTransform.GetComponentInChildren<Renderer>(true);
            ApplyStarVisual(new StarVisual { Image = image, Renderer = renderer }, isEarned, partialFill);
        }
    }

    private void UpdateLeaderboardRemainingPoints(TMP_Text remainingText, RankVisualData visualData)
    {
        if (remainingText == null)
        {
            return;
        }

        bool showRemaining = visualData.IsTopRank;
        remainingText.gameObject.SetActive(showRemaining);
        if (showRemaining)
        {
            remainingText.text = $"Điểm còn dư: {Mathf.Max(0, visualData.PointsIntoRank):N0}";
        }
    }

    private void ApplyRankDefinition(Transform item, RankDefinition definition)
    {
        if (item == null || definition == null)
        {
            return;
        }

        SetTextOnChild(item, "RankNameText", definition.DisplayName);

        var rankIconImage = item.Find("RankIcon")?.GetComponent<Image>();
        if (rankIconImage != null)
        {
            rankIconImage.sprite = definition.Icon;
            rankIconImage.enabled = rankIconImage.sprite != null;
        }

        var starContainerTransform = item.Find("StarContainer");
        UpdateRankDefinitionStars(starContainerTransform, definition.StarsToAdvance);
    }

    private void UpdateRankDefinitionStars(Transform container, int starCount)
    {
        if (container == null)
        {
            return;
        }

        bool showStars = starCount > 0;
        container.gameObject.SetActive(showStars);

        if (!showStars)
        {
            return;
        }

        if (starPrefab == null)
        {
            Debug.LogWarning("Star prefab is missing. Cannot display rank definition stars.");
            return;
        }

        for (int i = container.childCount; i < starCount; i++)
        {
            Instantiate(starPrefab, container);
        }

        for (int i = container.childCount - 1; i >= starCount; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }

        for (int i = 0; i < container.childCount; i++)
        {
            var starTransform = container.GetChild(i);
            var image = starTransform.GetComponentInChildren<Image>(true);
            var renderer = starTransform.GetComponentInChildren<Renderer>(true);
            ApplyStarVisual(new StarVisual { Image = image, Renderer = renderer }, true, 1f);
        }
    }

    private static TMP_Text FindText(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        return parent.Find(childName)?.GetComponent<TMP_Text>();
    }

    private static void SetTextOnChild(Transform parent, string childName, string value)
    {
        var text = FindText(parent, childName);
        if (text != null)
        {
            text.text = value;
        }
    }

    private VertexGradient WinResultGradient => new VertexGradient(winGradientTopLeft, winGradientTopRight, winGradientBottomLeft, winGradientBottomRight);

    private VertexGradient NonWinResultGradient => new VertexGradient(nonWinGradientTopLeft, nonWinGradientTopRight, nonWinGradientBottomLeft, nonWinGradientBottomRight);

    private void ApplyResultText(TMP_Text resultText, StatusWin status)
    {
        if (resultText == null)
        {
            return;
        }

        resultText.text = GetLocalizedStatus(status);
        resultText.enableVertexGradient = true;
        resultText.colorGradient = status == StatusWin.Win ? WinResultGradient : NonWinResultGradient;
    }

    private void ApplyRankPointVisual(TMP_Text rankPointText, int rankPoints)
    {
        if (rankPointText == null)
        {
            return;
        }

        rankPointText.text = rankPoints > 0 ? "+" + rankPoints.ToString("N0") : rankPoints.ToString("N0");
        rankPointText.color = rankPoints > 0
            ? rankPointPositiveColor
            : rankPoints < 0 ? rankPointNegativeColor : rankPointNeutralColor;
    }

    private void ApplyHistoryEntry(Transform item, PlayerMatchHistory entry)
    {
        if (item == null || entry == null)
        {
            return;
        }

        ApplyResultText(FindText(item, "ResultText"), (StatusWin)entry.statusWin);
        SetTextOnChild(item, "BetText", entry.marbBet.ToString("N0"));
        SetTextOnChild(item, "WonText", entry.marblesWon.ToString("N0"));
        SetTextOnChild(item, "LostText", entry.marblesLost.ToString("N0"));
        SetTextOnChild(item, "TypeText", GetMatchTypeText(entry.typeMatchGid));
        SetTextOnChild(item, "CreatedAtText", ItemVisualHelper.FormatRelativeTime(entry.createdAt));
        if (TryGetCurrentPlayerRingBall(out int currentRingBall))
        {
            SetTextOnChild(item, RemainingBallsAfterBetTextName, FormatRemainingBallsAfterBetText(currentRingBall - rankMatchBetCost));
        }
        ApplyRankPointVisual(FindText(item, "RankPointText"), entry.rankPoints);
    }

    private string GetLocalizedStatus(StatusWin status)
    {
        string key = status.ToString().Trim();
        string localized = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText(key) : null;
        return string.IsNullOrEmpty(localized) ? key : localized;
    }

    private string GetMatchTypeText(int typeMatchGid)
    {
        string key;
        if (Enum.IsDefined(typeof(TypeMatchGid), typeMatchGid))
        {
            key = ((TypeMatchGid)typeMatchGid).ToString();
        }
        else
        {
            key = $"TypeMatch_{typeMatchGid}";
        }

        key = key.Trim();
        string localized = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText(key) : null;
        return string.IsNullOrEmpty(localized) ? key : localized;
    }

    private RankVisualData CalculateRankVisualData(int totalRankPoints)
    {
        var data = new RankVisualData
        {
            CurrentRank = ResolveRankDefinition(totalRankPoints, out RankDefinition nextRank),
            NextRank = null,
            EarnedStars = 0,
            PartialStar = 0f,
            ProgressToNext = 1f,
            PointsIntoRank = 0,
            IsTopRank = false
        };

        data.NextRank = nextRank;

        if (data.CurrentRank == null)
        {
            return data;
        }

        data.PointsIntoRank = Mathf.Max(0, totalRankPoints - data.CurrentRank.MinPoints);
        data.IsTopRank = !data.CurrentRank.HasUpperBound || data.NextRank == null;

        if (data.CurrentRank.HasUpperBound && data.CurrentRank.StarsToAdvance > 0)
        {
            float rankSpan = Mathf.Max(1, data.CurrentRank.MaxPoints - data.CurrentRank.MinPoints);
            float pointsPerStar = rankSpan / data.CurrentRank.StarsToAdvance;
            data.ProgressToNext = Mathf.Clamp01(data.PointsIntoRank / rankSpan);

            if (pointsPerStar > 0)
            {
                data.EarnedStars = Mathf.Clamp(Mathf.FloorToInt(data.PointsIntoRank / pointsPerStar), 0, data.CurrentRank.StarsToAdvance);
                data.PartialStar = Mathf.Clamp01((data.PointsIntoRank - data.EarnedStars * pointsPerStar) / pointsPerStar);
                if (data.EarnedStars >= data.CurrentRank.StarsToAdvance)
                {
                    data.EarnedStars = data.CurrentRank.StarsToAdvance;
                    data.PartialStar = 1f;
                }
            }
        }
        else
        {
            data.ProgressToNext = 1f;
        }

        return data;
    }

    private RankDefinition ResolveRankDefinition(int totalRankPoints, out RankDefinition nextRank)
    {
        EnsureDefaultRankDefinitions();
        nextRank = null;
        if (rankDefinitions == null || rankDefinitions.Count == 0)
        {
            return null;
        }

        var ordered = rankDefinitions.OrderBy(r => r.MinPoints).ToList();
        RankDefinition currentRank = ordered[0];

        for (int i = 0; i < ordered.Count; i++)
        {
            var rank = ordered[i];
            bool withinUpperBound = !rank.HasUpperBound || totalRankPoints < rank.MaxPoints;
            if (totalRankPoints >= rank.MinPoints && withinUpperBound)
            {
                currentRank = rank;
                if (i + 1 < ordered.Count)
                {
                    nextRank = ordered[i + 1];
                }
                break;
            }
        }

        return currentRank;
    }

    private void EnsureDefaultRankDefinitions()
    {
        if (rankDefinitions != null && rankDefinitions.Count > 0)
        {
            rankDefinitions = rankDefinitions.OrderBy(r => r.MinPoints).ToList();
            return;
        }

        rankDefinitions = new List<RankDefinition>
        {
            new RankDefinition("Home Yard", "Small marble yard", 0, 30, 3),
            new RankDefinition("Small Alley", "Concrete alley play", 30, 60, 3),
            new RankDefinition("Village Lane", "Village banyan and marbles", 60, 90, 3),
            new RankDefinition("Ward League", "Ward marble board", 90, 130, 4),
            new RankDefinition("District Road", "District cup route", 130, 170, 4),
            new RankDefinition("Province Arena", "Province map glow", 170, 220, 5),
            new RankDefinition("Vietnam Marble King", "VN flag and crown", 220, -1, 0)
        };
    }

    private static string FormatWinRate(float winRate)
    {
        float value = winRate;
        if (value > 0f && value <= 1f)
        {
            value *= 100f;
        }

        value = Mathf.Clamp(value, 0f, 100f);
        return $"{value:0.#}%";
    }
}
