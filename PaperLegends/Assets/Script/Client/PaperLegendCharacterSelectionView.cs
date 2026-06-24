using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PaperLegendCharacterSelectionView : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PaperLegendCharacterSelectionClient selectionClient;

    [Header("Player Slots")]
    [SerializeField] private VerticalLayoutGroup playerSlotsLayoutGroup;
    [SerializeField] private PaperLegendCharacterSelectionSlotView playerSlotPrefab;

    [Header("Hero Grid")]
    [SerializeField] private Transform heroGridRoot;
    [SerializeField] private PaperLegendHeroSelectCardView heroCardPrefab;
    [SerializeField] private Image fallbackHeroIconImage;
    [SerializeField] private bool disableAlreadySelectedHeroes = true;

    [Header("Selected Preview")]
    [SerializeField] private Image selectedSlotIconImage;
    [SerializeField] private Image previewPortraitImage;
    [SerializeField] private TMP_Text previewNameText;
    [SerializeField] private TMP_Text previewRoleText;
    [SerializeField] private TMP_Text previewDescriptionText;
    [SerializeField] private TMP_Text selectionStatusText;
    [SerializeField] private Button lockSelectionButton;

    [Header("Behavior")]
    [SerializeField] private bool submitSelectionOnClick = true;
    [SerializeField] private bool clearGridBeforeRebuild = true;

    private readonly List<PaperLegendHeroSelectCardView> cards = new List<PaperLegendHeroSelectCardView>();
    private readonly Dictionary<int, PaperLegendHeroData> heroesByModelId = new Dictionary<int, PaperLegendHeroData>();
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<int, int> selectedModelByPlayerId = new Dictionary<int, int>();
    private readonly Dictionary<int, PaperLegendCharacterSelectionSlotView> playerSlotByPlayerId = new Dictionary<int, PaperLegendCharacterSelectionSlotView>();
    private readonly List<PaperLegendCharacterSelectionSlotView> spawnedPlayerSlots = new List<PaperLegendCharacterSelectionSlotView>();
    private readonly Dictionary<int, Sprite> iconByModelId = new Dictionary<int, Sprite>();
    private readonly HashSet<int> unavailableModelIds = new HashSet<int>();
    private readonly HashSet<int> lockedPlayerIds = new HashSet<int>();
    private int selectedModelId;
    private bool localSelectionLocked;
    private Sprite FallbackHeroIcon => fallbackHeroIconImage != null ? fallbackHeroIconImage.sprite : null;

    private void Awake()
    {
        ResolveSelectionClient();
        SetImageSprite(selectedSlotIconImage, null);
        SetImageSprite(previewPortraitImage, null);
        ClearPlayerSlots();
    }

    private void OnEnable()
    {
        ResolveSelectionClient();
        Subscribe();
        SubscribeLockButton();

        if (selectionClient != null && selectionClient.SelectableHeroes.Count > 0)
            RebuildHeroGrid(selectionClient.SelectableHeroes);
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnsubscribeLockButton();
    }

    private void ResolveSelectionClient()
    {
        if (selectionClient == null)
            selectionClient = PaperLegendCharacterSelectionClient.Instance;

        if (selectionClient == null)
            selectionClient = FindObjectOfType<PaperLegendCharacterSelectionClient>();
    }

    private void Subscribe()
    {
        if (selectionClient == null)
            return;

        selectionClient.SelectionStarted += HandleSelectionStarted;
        selectionClient.HeroCatalogLoaded += HandleHeroCatalogLoaded;
        selectionClient.SelectionUpdated += HandleSelectionUpdated;
        selectionClient.SelectionCompleted += HandleSelectionCompleted;
        selectionClient.SelectionRejected += HandleSelectionRejected;
    }

    private void Unsubscribe()
    {
        if (selectionClient == null)
            return;

        selectionClient.SelectionStarted -= HandleSelectionStarted;
        selectionClient.HeroCatalogLoaded -= HandleHeroCatalogLoaded;
        selectionClient.SelectionUpdated -= HandleSelectionUpdated;
        selectionClient.SelectionCompleted -= HandleSelectionCompleted;
        selectionClient.SelectionRejected -= HandleSelectionRejected;
    }

    private void HandleSelectionStarted(IReadOnlyList<int> playerIds, IReadOnlyList<int> modelIds, float remainingSeconds)
    {
        selectedModelId = 0;
        localSelectionLocked = false;
        unavailableModelIds.Clear();
        lockedPlayerIds.Clear();
        selectedModelByPlayerId.Clear();
        heroesByModelId.Clear();
        SetImageSprite(selectedSlotIconImage, null);
        SetImageSprite(previewPortraitImage, null);
        SetText(selectionStatusText, string.Empty);
        ConfigurePlayerSlots(playerIds);
        RefreshLockButton();
        RefreshCardStates();
    }

    private void HandleHeroCatalogLoaded(IReadOnlyList<PaperLegendHeroData> heroes)
    {
        RebuildHeroGrid(heroes);
        RefreshPlayerSlotIcons();
    }

    private void HandleSelectionUpdated(int playerId, int modelId, int selectedCount, int lockedCount, int totalCount, float remainingSeconds, bool isLocked)
    {
        if (playerId != 0 && modelId > 0)
        {
            selectedModelByPlayerId[playerId] = modelId;
            if (isLocked)
                lockedPlayerIds.Add(playerId);

            RebuildUnavailableModelIds();
            SetPlayerSlotHero(playerId, modelId, isLocked);
        }

        if (IsLocalPlayerId(playerId) && isLocked)
            localSelectionLocked = true;

        SetText(selectionStatusText, $"Locked {lockedCount}/{totalCount}");
        RefreshLockButton();
        RefreshCardStates();
    }

    private void HandleSelectionCompleted(IReadOnlyDictionary<int, int> selections)
    {
        if (selections != null)
        {
            selectedModelByPlayerId.Clear();
            unavailableModelIds.Clear();
            lockedPlayerIds.Clear();

            foreach (var selection in selections)
            {
                if (selection.Key == 0 || selection.Value <= 0)
                    continue;

                selectedModelByPlayerId[selection.Key] = selection.Value;
                lockedPlayerIds.Add(selection.Key);
                SetPlayerSlotHero(selection.Key, selection.Value, true);
            }

            RebuildUnavailableModelIds();
        }

        localSelectionLocked = true;
        SetText(selectionStatusText, "Selection completed");
        RefreshLockButton();
        RefreshCardStates();
    }

    private void HandleSelectionRejected(int modelId, string reason)
    {
        if (modelId == selectedModelId)
            selectedModelId = 0;

        ClearLocalPendingSelection(modelId);
        SetText(selectionStatusText, string.IsNullOrWhiteSpace(reason) ? "Selection rejected" : reason);
        RefreshLockButton();
        RefreshCardStates();
    }

    private void ConfigurePlayerSlots(IReadOnlyList<int> playerIds)
    {
        ClearPlayerSlots();

        if (playerIds == null || playerSlotsLayoutGroup == null || playerSlotPrefab == null)
            return;

        for (int i = 0; i < playerIds.Count; i++)
        {
            int playerId = playerIds[i];
            if (playerId == 0 || playerSlotByPlayerId.ContainsKey(playerId))
                continue;

            var slot = Instantiate(playerSlotPrefab, playerSlotsLayoutGroup.transform);
            if (slot == null)
                continue;

            string playerName = selectionClient != null ? selectionClient.GetPlayerDisplayName(playerId) : $"Player {playerId}";
            bool isBot = selectionClient != null && selectionClient.IsBotPlayer(playerId);
            slot.ConfigurePlayer(playerId, playerName, isBot);
            spawnedPlayerSlots.Add(slot);
            playerSlotByPlayerId[playerId] = slot;
        }
    }

    private void ClearPlayerSlots()
    {
        playerSlotByPlayerId.Clear();

        for (int i = 0; i < spawnedPlayerSlots.Count; i++)
        {
            if (spawnedPlayerSlots[i] == null)
                continue;

            Destroy(spawnedPlayerSlots[i].gameObject);
        }

        spawnedPlayerSlots.Clear();
    }

    private void RebuildHeroGrid(IReadOnlyList<PaperLegendHeroData> heroes)
    {
        if (heroGridRoot == null || heroCardPrefab == null)
            return;

        if (clearGridBeforeRebuild)
            ClearCards();

        heroesByModelId.Clear();

        if (heroes == null)
            return;

        for (int i = 0; i < heroes.Count; i++)
        {
            PaperLegendHeroData hero = heroes[i];
            if (hero == null)
                continue;

            int modelId = hero.ResolveModelIdInt();
            if (modelId <= 0)
                continue;

            heroesByModelId[modelId] = hero;
            PaperLegendHeroSelectCardView card = Instantiate(heroCardPrefab, heroGridRoot);
            cards.Add(card);

            Sprite icon = ResolveCachedIcon(modelId, FallbackHeroIcon);
            ConfigureCard(card, hero, icon);

            StartCoroutine(LoadHeroIconSpriteRoutine(modelId, sprite =>
            {
                Sprite resolved = sprite != null ? sprite : FallbackHeroIcon;
                iconByModelId[modelId] = resolved;
                ConfigureCard(card, hero, resolved);
                RefreshPlayerSlotIcons();
            }));
        }

        RefreshCardStates();
    }

    private void ConfigureCard(PaperLegendHeroSelectCardView card, PaperLegendHeroData hero, Sprite icon)
    {
        if (card == null || hero == null)
            return;

        int modelId = hero.ResolveModelIdInt();
        bool unavailable = disableAlreadySelectedHeroes && unavailableModelIds.Contains(modelId) && selectedModelId != modelId;
        card.Configure(hero, icon, selectedModelId == modelId, unavailable, HandleHeroClicked);
    }

    private void HandleHeroClicked(int modelId)
    {
        if (localSelectionLocked)
            return;

        if (modelId <= 0 || !heroesByModelId.TryGetValue(modelId, out PaperLegendHeroData hero))
            return;

        if (disableAlreadySelectedHeroes && unavailableModelIds.Contains(modelId) && selectedModelId != modelId)
            return;

        selectedModelId = modelId;
        ShowHeroPreview(hero);
        ApplyLocalPendingSelection(modelId);
        RefreshLockButton();
        RefreshCardStates();

        if (submitSelectionOnClick && selectionClient != null)
            selectionClient.SelectCharacter(modelId);
    }

    private void ShowHeroPreview(PaperLegendHeroData hero)
    {
        if (hero == null)
            return;

        int modelId = hero.ResolveModelIdInt();
        SetText(previewNameText, hero.name);
        SetText(previewRoleText, hero.role);
        SetText(previewDescriptionText, hero.description);

        Sprite cachedIcon = ResolveCachedIcon(modelId, FallbackHeroIcon);
        SetImageSprite(selectedSlotIconImage, cachedIcon);
        SetImageSprite(previewPortraitImage, cachedIcon);

        StartCoroutine(LoadHeroIconSpriteRoutine(modelId, sprite =>
        {
            Sprite resolved = sprite != null ? sprite : FallbackHeroIcon;
            SetImageSprite(selectedSlotIconImage, resolved);
        }));

        StartCoroutine(LoadHeroPortraitSpriteRoutine(modelId, sprite =>
        {
            Sprite resolved = sprite != null ? sprite : ResolveCachedIcon(modelId, FallbackHeroIcon);
            SetImageSprite(previewPortraitImage, resolved);
        }));
    }

    private void RefreshCardStates()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            PaperLegendHeroSelectCardView card = cards[i];
            if (card == null)
                continue;

            bool unavailable = disableAlreadySelectedHeroes && unavailableModelIds.Contains(card.ModelId) && selectedModelId != card.ModelId;
            card.SetSelected(card.ModelId == selectedModelId);
            card.SetUnavailable(localSelectionLocked || unavailable);
        }
    }

    private void RefreshPlayerSlotIcons()
    {
        foreach (var selection in selectedModelByPlayerId)
            SetPlayerSlotHero(selection.Key, selection.Value, lockedPlayerIds.Contains(selection.Key));
    }

    private void ApplyLocalPendingSelection(int modelId)
    {
        int userId = ResolveLocalUserId();
        if (userId == 0 || modelId <= 0)
            return;

        selectedModelByPlayerId[userId] = modelId;
        RebuildUnavailableModelIds();
        SetPlayerSlotHero(userId, modelId, false);
    }

    private void ClearLocalPendingSelection(int modelId)
    {
        int userId = ResolveLocalUserId();
        if (userId == 0)
            return;

        if (selectedModelByPlayerId.TryGetValue(userId, out int currentModelId) && currentModelId == modelId)
        {
            selectedModelByPlayerId.Remove(userId);
            RebuildUnavailableModelIds();

            PaperLegendCharacterSelectionSlotView slot = FindSlot(userId);
            if (slot != null)
                slot.ClearHero();
        }
    }

    private void RebuildUnavailableModelIds()
    {
        unavailableModelIds.Clear();

        foreach (int modelId in selectedModelByPlayerId.Values)
        {
            if (modelId > 0)
                unavailableModelIds.Add(modelId);
        }
    }

    private void SetPlayerSlotHero(int playerId, int modelId, bool isLocked)
    {
        PaperLegendCharacterSelectionSlotView slot = FindSlot(playerId);
        if (slot == null)
            return;

        slot.SetHero(ResolveCachedIcon(modelId, FallbackHeroIcon), isLocked);
    }

    private PaperLegendCharacterSelectionSlotView FindSlot(int playerId)
    {
        return playerSlotByPlayerId.TryGetValue(playerId, out PaperLegendCharacterSelectionSlotView slot) ? slot : null;
    }

    private Sprite ResolveCachedIcon(int modelId, Sprite fallback)
    {
        return iconByModelId.TryGetValue(modelId, out Sprite icon) && icon != null ? icon : fallback;
    }

    private IEnumerator LoadHeroIconSpriteRoutine(int modelId, Action<Sprite> onLoaded)
    {
        if (modelId <= 0)
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        string cacheKey = PaperLegendHeroAddressables.BuildHeroIconAddress(modelId);
        if (spriteCache.TryGetValue(cacheKey, out Sprite cachedSprite))
        {
            onLoaded?.Invoke(cachedSprite);
            yield break;
        }

        Sprite loadedSprite = null;
        yield return PaperLegendHeroAddressables.LoadHeroIconSpriteRoutine(modelId, sprite => loadedSprite = sprite);

        if (loadedSprite != null)
            spriteCache[cacheKey] = loadedSprite;

        onLoaded?.Invoke(loadedSprite);
    }

    private IEnumerator LoadHeroPortraitSpriteRoutine(int modelId, Action<Sprite> onLoaded)
    {
        if (modelId <= 0)
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        string cacheKey = PaperLegendHeroAddressables.BuildHeroPortraitAddress(modelId);
        if (spriteCache.TryGetValue(cacheKey, out Sprite cachedSprite))
        {
            onLoaded?.Invoke(cachedSprite);
            yield break;
        }

        Sprite loadedSprite = null;
        yield return PaperLegendHeroAddressables.LoadHeroPortraitSpriteRoutine(modelId, sprite => loadedSprite = sprite);

        if (loadedSprite != null)
            spriteCache[cacheKey] = loadedSprite;

        onLoaded?.Invoke(loadedSprite);
    }

    private void SubscribeLockButton()
    {
        if (lockSelectionButton != null)
            lockSelectionButton.onClick.AddListener(HandleLockSelectionClicked);
    }

    private void UnsubscribeLockButton()
    {
        if (lockSelectionButton != null)
            lockSelectionButton.onClick.RemoveListener(HandleLockSelectionClicked);
    }

    private void HandleLockSelectionClicked()
    {
        if (localSelectionLocked || selectedModelId <= 0 || selectionClient == null)
            return;

        selectionClient.LockCharacterSelection();
    }

    private void RefreshLockButton()
    {
        if (lockSelectionButton == null)
            return;

        lockSelectionButton.interactable = !localSelectionLocked && selectedModelId > 0 && selectionClient != null && selectionClient.IsSelectionActive;
    }

    private static bool IsLocalPlayerId(int playerId)
    {
        int userId = ResolveLocalUserId();
        return userId != 0 && playerId == userId;
    }

    private static int ResolveLocalUserId()
    {
        return GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
    }

    private void ClearCards()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
                Destroy(cards[i].gameObject);
        }

        cards.Clear();
    }

    private static void SetImageSprite(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }
}
