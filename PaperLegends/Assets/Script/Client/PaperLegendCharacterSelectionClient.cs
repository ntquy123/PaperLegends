using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class PaperLegendCharacterSelectionClient : MonoBehaviour
{
    public static PaperLegendCharacterSelectionClient Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject matchmakingPanel;
    [SerializeField] private GameObject characterSelectionPanel;
    [SerializeField] private bool hideMatchmakingPanelOnSelectionStart = true;
    [SerializeField] private bool hidePanelOnCompleted = true;

    [Header("Labels")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text selectedCountText;

    private readonly List<int> activePlayerIds = new List<int>();
    private readonly List<int> selectableModelIds = new List<int>();
    private readonly List<PaperLegendHeroData> selectableHeroes = new List<PaperLegendHeroData>();
    private readonly Dictionary<int, int> selectionsByPlayerId = new Dictionary<int, int>();
    private readonly Dictionary<int, PaperLegendHeroData> heroDataByModelId = new Dictionary<int, PaperLegendHeroData>();
    private readonly Dictionary<int, string> playerNamesById = new Dictionary<int, string>();
    private readonly HashSet<int> botPlayerIds = new HashSet<int>();
    private readonly HashSet<int> lockedPlayerIds = new HashSet<int>();
    private readonly HashSet<int> selectedModelIds = new HashSet<int>();
    private Coroutine countdownRoutine;
    private Coroutine heroCatalogRoutine;
    private float countdownRemainingSeconds;
    private string activeMatchId = string.Empty;

    public IReadOnlyList<int> ActivePlayerIds => activePlayerIds;
    public IReadOnlyList<int> SelectableModelIds => selectableModelIds;
    public IReadOnlyList<PaperLegendHeroData> SelectableHeroes => selectableHeroes;
    public IReadOnlyDictionary<int, PaperLegendHeroData> HeroDataByModelId => heroDataByModelId;
    public IReadOnlyDictionary<int, int> SelectionsByPlayerId => selectionsByPlayerId;
    public IReadOnlyDictionary<int, string> PlayerNamesById => playerNamesById;
    public IReadOnlyCollection<int> SelectedModelIds => selectedModelIds;
    public bool IsSelectionActive { get; private set; }

    public event Action<IReadOnlyList<int>, IReadOnlyList<int>, float> SelectionStarted;
    public event Action<IReadOnlyList<PaperLegendHeroData>> HeroCatalogLoaded;
    public event Action<int, int, int, int, int, float, bool> SelectionUpdated;
    public event Action<IReadOnlyDictionary<int, int>> SelectionCompleted;
    public event Action<int, string> SelectionRejected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);
    }

    private void OnEnable()
    {
        WebSocketHelper.OnPaperLegendCharacterSelectionStart += HandleSocketSelectionStart;
        WebSocketHelper.OnPaperLegendCharacterSelectionUpdate += HandleSocketSelectionUpdate;
        WebSocketHelper.OnPaperLegendCharacterSelectionComplete += HandleSocketSelectionComplete;
        WebSocketHelper.OnPaperLegendCharacterSelectionRejected += HandleSocketSelectionRejected;
    }

    private void OnDisable()
    {
        WebSocketHelper.OnPaperLegendCharacterSelectionStart -= HandleSocketSelectionStart;
        WebSocketHelper.OnPaperLegendCharacterSelectionUpdate -= HandleSocketSelectionUpdate;
        WebSocketHelper.OnPaperLegendCharacterSelectionComplete -= HandleSocketSelectionComplete;
        WebSocketHelper.OnPaperLegendCharacterSelectionRejected -= HandleSocketSelectionRejected;
        StopCountdown();
        StopHeroCatalogLoad();
    }

    public void BeginSelection(string matchId, string playerIdsCsv, string selectableModelIdsCsv, float countdownSeconds)
    {
        BeginSelection(matchId, playerIdsCsv, selectableModelIdsCsv, countdownSeconds, string.Empty, string.Empty);
    }

    public void BeginSelection(
        string matchId,
        string playerIdsCsv,
        string selectableModelIdsCsv,
        float countdownSeconds,
        string playerNamesPayload,
        string botPlayerIdsCsv)
    {
        activeMatchId = matchId ?? string.Empty;
        IsSelectionActive = true;
        activePlayerIds.Clear();
        selectableModelIds.Clear();
        selectableHeroes.Clear();
        selectionsByPlayerId.Clear();
        heroDataByModelId.Clear();
        playerNamesById.Clear();
        botPlayerIds.Clear();
        lockedPlayerIds.Clear();
        selectedModelIds.Clear();

        ParseCsvInts(playerIdsCsv, activePlayerIds);
        ParseCsvInts(selectableModelIdsCsv, selectableModelIds);
        ParseCsvInts(botPlayerIdsCsv, botPlayerIds);
        ParsePlayerNames(playerNamesPayload, playerNamesById);

        countdownRemainingSeconds = Mathf.Max(0f, countdownSeconds);
        ShowCharacterSelectionPanel();

        UpdateLabels(0, activePlayerIds.Count, countdownRemainingSeconds);
        StartCountdown();
        LoadHeroCatalogForModelIds(selectableModelIds);
        SelectionStarted?.Invoke(activePlayerIds, selectableModelIds, countdownRemainingSeconds);
    }

    public void ApplySelectionUpdate(int playerId, int modelId, int selectedCount, int lockedCount, int totalCount, float remainingSeconds, bool isLocked, string selectedModelIdsCsv = null)
    {
        if (playerId != 0 && modelId > 0)
            selectionsByPlayerId[playerId] = modelId;

        if (playerId != 0 && isLocked)
            lockedPlayerIds.Add(playerId);

        if (!string.IsNullOrWhiteSpace(selectedModelIdsCsv))
        {
            ParseCsvInts(selectedModelIdsCsv, selectedModelIds);
        }
        else
        {
            RebuildSelectedModelIdsFromSelections();
        }

        countdownRemainingSeconds = Mathf.Max(0f, remainingSeconds);
        UpdateLabels(lockedCount, totalCount, countdownRemainingSeconds);
        SelectionUpdated?.Invoke(playerId, modelId, selectedCount, lockedCount, totalCount, countdownRemainingSeconds, isLocked);
    }

    public void CompleteSelection(string selectionsCsv)
    {
        ParseSelectionCsv(selectionsCsv, selectionsByPlayerId);
        RebuildSelectedModelIdsFromSelections();
        foreach (int playerId in activePlayerIds)
            lockedPlayerIds.Add(playerId);

        LoadHeroCatalogForModelIds(selectionsByPlayerId.Values);
        IsSelectionActive = false;
        StopCountdown();
        UpdateLabels(selectionsByPlayerId.Count, activePlayerIds.Count, 0f);

        if (hidePanelOnCompleted && characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);

        SelectionCompleted?.Invoke(selectionsByPlayerId);
    }

    public void RejectSelection(int modelId, string reason, string selectedModelIdsCsv = null)
    {
        if (!string.IsNullOrWhiteSpace(selectedModelIdsCsv))
            ParseCsvInts(selectedModelIdsCsv, selectedModelIds);

        SelectionRejected?.Invoke(modelId, reason);
    }

    public bool IsBotPlayer(int playerId)
    {
        return botPlayerIds.Contains(playerId);
    }

    public bool IsPlayerLocked(int playerId)
    {
        return lockedPlayerIds.Contains(playerId);
    }

    public string GetPlayerDisplayName(int playerId)
    {
        if (playerNamesById.TryGetValue(playerId, out string playerName) && !string.IsNullOrWhiteSpace(playerName))
            return playerName;

        return IsBotPlayer(playerId) ? $"BOT {playerId}" : $"Player {playerId}";
    }

    private void ShowCharacterSelectionPanel()
    {
        if (hideMatchmakingPanelOnSelectionStart && matchmakingPanel != null)
            matchmakingPanel.SetActive(false);

        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(true);
    }

    public bool TryGetHeroData(int modelId, out PaperLegendHeroData hero)
    {
        return heroDataByModelId.TryGetValue(modelId, out hero);
    }

    public PaperLegendHeroData GetHeroDataOrNull(int modelId)
    {
        heroDataByModelId.TryGetValue(modelId, out var hero);
        return hero;
    }

    public void SelectCharacter(int modelId)
    {
        if (!IsSelectionActive)
            return;

        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (userId <= 0)
        {
            Debug.LogWarning("[PaperLegends][CharacterSelect] Cannot select character because local userId is invalid.");
            return;
        }

        if (WebSocketHelper.Instance == null)
        {
            Debug.LogWarning("[PaperLegends][CharacterSelect] Cannot select character because WebSocketHelper is missing.");
            return;
        }

        WebSocketHelper.Instance.SendPaperLegendCharacterSelect(activeMatchId, userId, modelId);
    }

    public void LockCharacterSelection()
    {
        if (!IsSelectionActive)
            return;

        int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (userId <= 0)
        {
            Debug.LogWarning("[PaperLegends][CharacterSelect] Cannot lock character because local userId is invalid.");
            return;
        }

        if (lockedPlayerIds.Contains(userId))
            return;

        if (WebSocketHelper.Instance == null)
        {
            Debug.LogWarning("[PaperLegends][CharacterSelect] Cannot lock character because WebSocketHelper is missing.");
            return;
        }

        WebSocketHelper.Instance.SendPaperLegendCharacterLock(activeMatchId, userId);
    }


    private void StartCountdown()
    {
        StopCountdown();
        countdownRoutine = StartCoroutine(CountdownRoutine());
    }

    private void StopCountdown()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }
    }

    private void LoadHeroCatalogForModelIds(IEnumerable<int> modelIds)
    {
        StopHeroCatalogLoad();

        var ids = new List<int>();
        if (modelIds != null)
        {
            foreach (int modelId in modelIds)
            {
                if (modelId > 0 && !ids.Contains(modelId))
                    ids.Add(modelId);
            }
        }

        if (ids.Count == 0)
        {
            HeroCatalogLoaded?.Invoke(selectableHeroes);
            return;
        }

        heroCatalogRoutine = StartCoroutine(LoadHeroCatalogRoutine(ids));
    }

    private void StopHeroCatalogLoad()
    {
        if (heroCatalogRoutine != null)
        {
            StopCoroutine(heroCatalogRoutine);
            heroCatalogRoutine = null;
        }
    }

    private IEnumerator LoadHeroCatalogRoutine(List<int> modelIds)
    {
        if (APIManager.Instance == null)
        {
            Debug.LogWarning("[PaperLegends][CharacterSelect] APIManager is missing; cannot load hero catalog.");
            heroCatalogRoutine = null;
            yield break;
        }

        PaperLegendHeroListResponse response = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetPaperLegendHeroesByModelIdsAsync(modelIds),
            result => response = result));

        heroCatalogRoutine = null;

        if (response == null || response.heroes == null)
        {
            Debug.LogWarning("[PaperLegends][CharacterSelect] Hero catalog response is empty.");
            HeroCatalogLoaded?.Invoke(selectableHeroes);
            yield break;
        }

        selectableHeroes.Clear();
        heroDataByModelId.Clear();

        for (int i = 0; i < response.heroes.Count; i++)
        {
            var hero = response.heroes[i];
            if (hero == null)
                continue;

            selectableHeroes.Add(hero);

            int resolvedModelId = hero.ResolveModelIdInt();
            if (resolvedModelId > 0)
                heroDataByModelId[resolvedModelId] = hero;
        }

        Debug.Log($"[PaperLegends][CharacterSelect] Loaded {selectableHeroes.Count} hero(s) from API.");
        HeroCatalogLoaded?.Invoke(selectableHeroes);
    }

    private IEnumerator CountdownRoutine()
    {
        while (IsSelectionActive && countdownRemainingSeconds > 0f)
        {
            countdownRemainingSeconds = Mathf.Max(0f, countdownRemainingSeconds - Time.deltaTime);
            UpdateCountdownLabel(countdownRemainingSeconds);
            yield return null;
        }

        countdownRoutine = null;
    }

    private void UpdateLabels(int selectedCount, int totalCount, float remainingSeconds)
    {
        UpdateCountdownLabel(remainingSeconds);

        if (selectedCountText != null && totalCount > 0)
            selectedCountText.text = $"{selectedCount}/{totalCount}";
    }

    private void UpdateCountdownLabel(float remainingSeconds)
    {
        if (countdownText == null)
            return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds));
        countdownText.text = $"{seconds}s";
    }

    private void HandleSocketSelectionStart(WebSocketHelper.PaperLegendCharacterSelectionStartMessage message)
    {
        if (message == null)
            return;

        float seconds = message.countdownSeconds;
        if (seconds <= 0f && message.deadlineMs > 0)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            seconds = Mathf.Max(0f, (message.deadlineMs - nowMs) / 1000f);
        }

        BeginSelection(
            message.matchId,
            message.playerIds,
            message.selectableModelIds,
            seconds,
            message.playerNames,
            message.botPlayerIds);
    }

    private void HandleSocketSelectionUpdate(WebSocketHelper.PaperLegendCharacterSelectionUpdateMessage message)
    {
        if (message == null)
            return;

        ApplySelectionUpdate(
            message.playerId,
            message.characterModelId,
            message.selectedCount,
            message.lockedCount,
            message.totalCount,
            message.remainingSeconds,
            message.isLocked,
            message.selectedModelIds);
    }

    private void HandleSocketSelectionComplete(WebSocketHelper.PaperLegendCharacterSelectionCompleteMessage message)
    {
        if (message == null)
            return;

        CompleteSelection(message.selections);
    }

    private void HandleSocketSelectionRejected(WebSocketHelper.PaperLegendCharacterSelectionRejectedMessage message)
    {
        if (message == null)
            return;

        RejectSelection(message.characterModelId, message.reason, message.selectedModelIds);
    }

    private void RebuildSelectedModelIdsFromSelections()
    {
        selectedModelIds.Clear();
        foreach (int modelId in selectionsByPlayerId.Values)
        {
            if (modelId > 0)
                selectedModelIds.Add(modelId);
        }
    }

    private static void ParseCsvInts(string csv, List<int> results)
    {
        results.Clear();
        if (string.IsNullOrWhiteSpace(csv))
            return;

        string[] parts = csv.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i].Trim(), out int value))
                results.Add(value);
        }
    }

    private static void ParseCsvInts(string csv, HashSet<int> results)
    {
        results.Clear();
        if (string.IsNullOrWhiteSpace(csv))
            return;

        string[] parts = csv.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i].Trim(), out int value))
                results.Add(value);
        }
    }

    private static void ParsePlayerNames(string payload, Dictionary<int, string> results)
    {
        results.Clear();
        if (string.IsNullOrWhiteSpace(payload))
            return;

        string[] pairs = payload.Split('|');
        for (int i = 0; i < pairs.Length; i++)
        {
            string[] parts = pairs[i].Split(':');
            if (parts.Length < 2)
                continue;

            if (int.TryParse(parts[0].Trim(), out int playerId))
                results[playerId] = Uri.UnescapeDataString(parts[1].Trim());
        }
    }

    private static void ParseSelectionCsv(string csv, Dictionary<int, int> results)
    {
        results.Clear();
        if (string.IsNullOrWhiteSpace(csv))
            return;

        string[] pairs = csv.Split(',');
        for (int i = 0; i < pairs.Length; i++)
        {
            string[] parts = pairs[i].Split(':');
            if (parts.Length != 2)
                continue;

            if (int.TryParse(parts[0].Trim(), out int playerId) &&
                int.TryParse(parts[1].Trim(), out int modelId))
            {
                results[playerId] = modelId;
            }
        }
    }
}
