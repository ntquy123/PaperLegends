using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarketController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject MarketPanel;
    public GameObject ItemPrefab;
    public Button buyButton;
    public Button searchButton;
    public TMP_InputField itemNameInput;
    public ScrollRect scrollRect;
    public Transform content;
    [SerializeField] private TabManager marketTabManager;

    [Header("Detail Panel")]
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemLevelText;
    public TextMeshProUGUI itemPriceText;
    public TextMeshProUGUI minPriceText;
    public TextMeshProUGUI maxPriceText;
    public TextMeshProUGUI itemDescriptionText;
    public Image itemImage;
    public Button detailInfoButton;
    public GameObject detailPanelRoot;
    private PlayerInventorySchema modelplayer = null;
    public TextMeshProUGUI RingBall;
    [Header("Item Infor")]
    public Renderer characterRenderer;
    public Renderer BallRenderer;
    public TextMeshProUGUI InforItem;
    public Slider MassSlider;
    public Slider SpeedSlider;
    public Slider BounceSlider;
    public Slider ImpactSlider;

    public static MarketController Instance;

    private readonly List<MarketItemSchema> loadedItems = new();
    private MarketItemSchema selectedItem;
    private PlayerInventorySchema playerInfo;
    private MarketSearchPopupValues currentSearchValues = new();
    private int currentPage;
    private bool isLoading;
    private Coroutine priceOverviewCoroutine;
    private MarketTabType currentTabType = MarketTabType.Market;

    private enum MarketTabType
    {
        Market = 0,
        MyBall = 1,
        Trading = 2
    }

    private void Awake()
    {
        Instance = this;
        EnsureDetailPanel();
        RegisterTabEvents();
        if (scrollRect != null)
            scrollRect.onValueChanged.AddListener(OnScroll);
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => OnClickBuy(selectedItem));
        }
        if (searchButton != null)
        {
            searchButton.onClick.RemoveAllListeners();
            searchButton.onClick.AddListener(OnSearch);
        }
        // if (itemNameInput != null)
        //   itemNameInput.onEndEdit.AddListener(_ => OnSearch());
    }

    public void ShowMarketList()
    {
        MarketPanel?.SetActive(true);
        currentTabType = MarketTabType.Market;
        currentSearchValues = CaptureSearchValuesFromUI();
        currentPage = 0;
        loadedItems.Clear();
        selectedItem = null;
        ClearDetailPanel();
        foreach (Transform child in content)
            Destroy(child.gameObject);
        StartCoroutine(LoadInforPlayerCoroutine());
        StartCoroutine(ShowCurrentTabCoroutine(true));
    }
    private IEnumerator LoadInforPlayerCoroutine()
    {
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetPlayerInventoryAsync(GameManagerNetWork.Instance.loginUserModel.UserId),
            result => modelplayer = result));

        if (modelplayer == null)
            yield break;
        RingBall.text = modelplayer.RingBall.ToString();
        yield break;
    }
    private void RegisterTabEvents()
    {
        if (marketTabManager == null && MarketPanel != null)
            marketTabManager = MarketPanel.GetComponentInChildren<TabManager>(true);

        if (marketTabManager?.TabButtons == null)
            return;

        if (marketTabManager.TabButtons.Length > 0 && marketTabManager.TabButtons[0] != null)
            marketTabManager.TabButtons[0].onClick.AddListener(() => OnClickTab(MarketTabType.Market));
        if (marketTabManager.TabButtons.Length > 1 && marketTabManager.TabButtons[1] != null)
            marketTabManager.TabButtons[1].onClick.AddListener(() => OnClickTab(MarketTabType.MyBall));
        if (marketTabManager.TabButtons.Length > 2 && marketTabManager.TabButtons[2] != null)
            marketTabManager.TabButtons[2].onClick.AddListener(() => OnClickTab(MarketTabType.Trading));
    }

    private void OnClickTab(MarketTabType tabType)
    {
        currentTabType = tabType;
        currentPage = 0;
        loadedItems.Clear();
        selectedItem = null;
        ClearDetailPanel();
        ClearItemGrid();
        StartCoroutine(ShowCurrentTabCoroutine(true));
    }

    private IEnumerator ShowCurrentTabCoroutine(bool reset)
    {
        yield return StartCoroutine(LoadPlayerInfoCoroutine());
        switch (currentTabType)
        {
            case MarketTabType.MyBall:
                PopulateMyInventoryItems(false, reset);
                break;
            case MarketTabType.Trading:
                PopulateMyInventoryItems(true, reset);
                break;
            default:
                yield return StartCoroutine(LoadMarketCatalogCoroutine(reset));
                break;
        }
    }


    private IEnumerator LoadMarketCatalogCoroutine(bool reset)
    {
        isLoading = true;
        List<ItemSchema> items = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetMarketCatalogItemsAsync((int)TypeItemGid.Culi),
            result => items = result));

        if (reset)
            ClearItemGrid();

        if (items != null && items.Count > 0)
        {
            var marketItems = items
                .Where(MatchesCurrentSearchFilters)
                .Select(p => new MarketItemSchema
                {
                    playerId = 0,
                    playerName = string.Empty,
                    itemId = p.id,
                    seq = 0,
                    level = p.level,
                    Price = p.priceByBall > 0 ? p.priceByBall : p.price,
                    IsSolded = 0,
                    item = p
                })
                .ToList();

            loadedItems.AddRange(marketItems);
            PopulateItems(marketItems, currentTabType);
        }

        isLoading = false;
    }

    private IEnumerator LoadPlayerInfoCoroutine()
    {
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetPlayerInventoryAsync(GameManagerNetWork.Instance.loginUserModel.UserId),
            result => playerInfo = result));

        if (playerInfo != null && RingBall != null)
            RingBall.text = playerInfo.RingBall.ToString();
    }

    private IEnumerator LoadMarketPageCoroutine(bool reset)
    {
        isLoading = true;
        string name = currentSearchValues != null ? currentSearchValues.ItemName : null;
        int? levelFrom = currentSearchValues != null ? currentSearchValues.LevelFrom : null;
        int? levelTo = currentSearchValues != null ? currentSearchValues.LevelTo : null;
        List<int> rarityGids = currentSearchValues != null ? currentSearchValues.RarityGids : null;

        List<MarketItemSchema> items = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetMarketItemsAsync(name, levelFrom, levelTo, rarityGids, currentPage),
            result => items = result));

        if (reset)
            ClearItemGrid();

        if (items != null && items.Count > 0)
        {
            if (currentTabType == MarketTabType.Trading && playerInfo != null)
                items = items.Where(x => x.playerId == playerInfo.id).ToList();

            loadedItems.AddRange(items);
            PopulateItems(items, currentTabType);
        }
        isLoading = false;
    }

    private void PopulateMyInventoryItems(bool onlySelling, bool reset)
    {
        if (reset)
            ClearItemGrid();
        if (playerInfo?.playerItems == null)
            return;

        var items = playerInfo.playerItems
            .Where(p => p.typeGid == (int)TypeItemGid.Culi)
            .Where(p => onlySelling ? p.IsSolded == StatusSold.Sale : p.IsSolded != StatusSold.Sale)
            .Where(MatchesCurrentSearchFilters)
            .Select(p => new MarketItemSchema
            {
                playerId = playerInfo.id,
                playerName = playerInfo.PlayerName,
                itemId = p.id,
                seq = p.seq,
                level = p.level,
                Price = p.priceByBall > 0 ? p.priceByBall : p.price,
                IsSolded = (int)p.IsSolded,
                item = p
            })
            .ToList();

        loadedItems.AddRange(items);
        PopulateItems(items, onlySelling ? MarketTabType.Trading : MarketTabType.MyBall);
    }

    private bool MatchesCurrentSearchFilters(ItemSchema item)
    {
        if (item == null)
            return false;

        if (currentSearchValues == null)
            return true;

        string keyword = (currentSearchValues.ItemName ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            string itemName = item.name ?? string.Empty;
            string localizedName = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText(itemName)
                : itemName;

            bool matchName = itemName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                             || localizedName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!matchName)
                return false;
        }

        if (currentSearchValues.LevelFrom.HasValue && item.level < currentSearchValues.LevelFrom.Value)
            return false;
        if (currentSearchValues.LevelTo.HasValue && item.level > currentSearchValues.LevelTo.Value)
            return false;

        if (currentSearchValues.RarityGids != null && currentSearchValues.RarityGids.Count > 0)
        {
            if (!currentSearchValues.RarityGids.Contains(item.rarityGid))
                return false;
        }

        return true;
    }

    private void PopulateItems(List<MarketItemSchema> items, MarketTabType tabType)
    {
        foreach (var item in items)
        {
            GameObject obj = Instantiate(ItemPrefab, content);
            MarketItemView itemView = obj.GetComponent<MarketItemView>();
            if (itemView == null)
            {
                Debug.LogError("MarketController: ItemPrefab is missing MarketItemView component.");
                Destroy(obj);
                continue;
            }

            ItemVisualHelper.ApplyRarityBackground(obj.transform, item.item?.rarityGid ?? 0);

            if (itemView.ItemImage != null)
            {
                StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{item.itemId}.png", s =>
                {
                    if (s != null)
                        itemView.ItemImage.sprite = s;
                }));
            }

            if (itemView.NameText != null)
                itemView.NameText.text = LocalizationManager.Instance.GetText(item.item?.name ?? string.Empty);
            if (itemView.LevelText != null)
                itemView.LevelText.text = item.level.ToString();
            if (itemView.PriceText != null)
                itemView.PriceText.text = item.Price.ToString("#,0");

            if (itemView.SellerText != null)
            {
                if (tabType == MarketTabType.Trading)
                {
                    itemView.SellerText.gameObject.SetActive(true);
                    itemView.SellerText.text = $"Treo bán: {item.Price:#,0}";
                }
                else
                {
                    itemView.SellerText.gameObject.SetActive(false);
                }
            }

            ShopItemUI itemUI = obj.AddComponent<ShopItemUI>();
            bool canBuy = tabType == MarketTabType.Market
                          && playerInfo != null
                          && playerInfo.RingBall >= item.Price
                          && playerInfo.id != item.playerId;
            itemUI.Init(canBuy, ItemVisualHelper.GetRarityColor(item.item?.rarityGid ?? 0));

            Button btn = itemView.RootButton;
            if (btn != null)
            {
                btn.onClick.AddListener(() =>
                {
                    SelectItem(item);
                    foreach (Transform child in content)
                        child.GetComponent<ShopItemUI>()?.SetHighlight(false);
                    itemUI.SetHighlight(true);
                });
            }

            if (itemView.SaleButton != null)
            {
                itemView.SaleButton.gameObject.SetActive(tabType == MarketTabType.Trading);
                itemView.SaleButton.onClick.RemoveAllListeners();
                if (tabType == MarketTabType.Trading)
                    itemView.SaleButton.onClick.AddListener(() => OnClickCancelSell(item));
            }

            if (itemView.BuyButton != null)
                itemView.BuyButton.gameObject.SetActive(false);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content.GetComponent<RectTransform>());
    }

    private void OnClickCancelSell(MarketItemSchema item)
    {
        if (item == null)
            return;
        if (PopupHelper.Instance == null)
            return;

        PopupHelper.Instance.ShowPopup("Xác nhận hủy treo bán?", () => StartCoroutine(CancelSellItemCoroutine(item)));
    }

    private IEnumerator CancelSellItemCoroutine(MarketItemSchema item)
    {
        bool success = false;
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;

        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.CancelSellMarketAsync(playerId, item.itemId, item.seq),
            result => success = result));

        if (success)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("cancel_sold"), true);
            currentPage = 0;
            loadedItems.Clear();
            ClearItemGrid();
            StartCoroutine(ShowCurrentTabCoroutine(true));
        }
        else
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_network_false"), false);
        }
    }

    private void SelectItem(MarketItemSchema item)
    {
        selectedItem = item;
        EnsureDetailPanel();
        UpdateDetailPanel(item);
        StartPriceOverviewLoad(item?.itemId ?? 0);

        if (item?.item != null && item.item.typeGid == (int)TypeItemGid.Culi)
        {
            StartCoroutine(ItemVisualHelper.ApplyMaterial(characterRenderer, BallRenderer, null, item.itemId, item.item.typeGid, null, item.item.isCateye));

            float mass = ItemVisualHelper.CalculateStatByLevel(item.item.Mass, item.level);
            float speed = ItemVisualHelper.CalculateDragByLevel(item.item.Mass, item.item.GravityScale, item.item.Drag, item.item.Bounciness, item.item.Elasticity, item.item.ImpactResistance, item.level);
            float bounce = ItemVisualHelper.CalculateStatByLevel(item.item.Bounciness, item.level);
            float impact = ItemVisualHelper.CalculateStatByLevel(item.item.ImpactResistance, item.level);

            if (InforItem != null)
            {
                InforItem.gameObject.SetActive(true);
                InforItem.text = ItemVisualHelper.BuildStatInfo(mass, speed, bounce, impact);
            }

            ItemVisualHelper.UpdateStatSliders(MassSlider, SpeedSlider, BounceSlider, ImpactSlider,
                mass, speed, bounce, impact);
        }
        else
        {
            if (InforItem != null)
            {
                InforItem.text = string.Empty;
                InforItem.gameObject.SetActive(false);
            }
            ItemVisualHelper.UpdateStatSliders(MassSlider, SpeedSlider, BounceSlider, ImpactSlider, 0, 0, 0, 0);
        }
    }

    private void OnScroll(Vector2 pos)
    {
        if (isLoading || scrollRect == null)
            return;
        if (currentTabType == MarketTabType.MyBall)
            return;
        if (scrollRect.verticalNormalizedPosition <= 0.01f)
        {
            currentPage++;
            StartCoroutine(LoadMarketPageCoroutine(false));
        }
    }

    private void OnFiltersChanged()
    {
        if (isLoading)
            return;
        currentPage = 0;
        loadedItems.Clear();
        ClearItemGrid();
        StartCoroutine(ShowCurrentTabCoroutine(true));
    }

    private void OnSearch()
    {
        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("MarketController: PopupHelper instance is not available for market search popup.");
            return;
        }

        currentSearchValues ??= CaptureSearchValuesFromUI();
        PopupHelper.Instance.ShowMarketSearchPopup(currentSearchValues.Clone(), ApplySearchValues);
    }

    private MarketSearchPopupValues CaptureSearchValuesFromUI()
    {
        return new MarketSearchPopupValues
        {
            ItemName = itemNameInput != null ? itemNameInput.text : string.Empty
        };
    }

    private void ApplySearchValues(MarketSearchPopupValues values)
    {
        if (values == null)
            return;

        currentSearchValues = values.Clone();

        if (itemNameInput != null)
            itemNameInput.SetTextWithoutNotify(currentSearchValues.ItemName ?? string.Empty);


        OnFiltersChanged();
    }

    public void OnClickBuy(MarketItemSchema item)
    {
        if (item == null || playerInfo == null || PopupHelper.Instance == null)
            return;

        int minPrice = ExtractPriceFromText(minPriceText != null ? minPriceText.text : string.Empty);
        int maxPrice = ExtractPriceFromText(maxPriceText != null ? maxPriceText.text : string.Empty);
        if (maxPrice <= 0) maxPrice = int.MaxValue;

        PopupHelper.Instance.ShowMarketOrderBoardPopup(item, minPrice, maxPrice, playerInfo.RingBall, (price, quantity) =>
            StartCoroutine(PlaceBuyRequestOrderCoroutine(item, price, quantity)));
    }


    private static int ExtractPriceFromText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }

    private IEnumerator PlaceBuyRequestOrderCoroutine(MarketItemSchema item, int price, int quantity)
    {
        bool success = false;
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;

        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.PlaceBuyRequestOrderAsync(playerId, item.itemId, price, quantity),
            r => success = r));

        NotificationHelper.Instance.ShowNotification(success ? "Đặt lệnh mua thành công" : LocalizationManager.Instance.GetText("noti_buy_false"), success);
        if (success)
            StartPriceOverviewLoad(item.itemId);
    }
    private IEnumerator BuyItemCoroutine(MarketItemSchema item)
    {
        PlayerInventorySchema result = null;
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.BuyItemOnMarketAsync(playerId, item.playerId, item.itemId, item.seq),
            r => result = r));

        if (result == null)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_buy_false"), false);
            yield break;
        }

        NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_buy_true"), true);
        InventoryController.Instance?.ShowInventoryList();
        ShowMarketList();
    }

    private void StartPriceOverviewLoad(int itemId)
    {
        if (itemId <= 0)
        {
            UpdatePriceOverview(null);
            return;
        }

        if (priceOverviewCoroutine != null)
            StopCoroutine(priceOverviewCoroutine);

        UpdatePriceOverview(null);
        priceOverviewCoroutine = StartCoroutine(LoadPriceOverviewCoroutine(itemId));
    }

    private IEnumerator LoadPriceOverviewCoroutine(int itemId)
    {
        ItemPriceOverviewData priceOverview = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetItemPriceOverviewAsync(itemId),
            result => priceOverview = result));

        priceOverviewCoroutine = null;

        if (selectedItem == null || selectedItem.itemId != itemId)
            yield break;

        UpdatePriceOverview(priceOverview);
    }

    private void UpdatePriceOverview(ItemPriceOverviewData priceOverview)
    {
        if (minPriceText != null)
        {
            string minValue = priceOverview != null ? priceOverview.minPrice.ToString("#,0") : "--";
            minPriceText.text = $"Giá thấp nhất: {minValue}";
        }

        if (maxPriceText != null)
        {
            string maxValue = priceOverview != null ? priceOverview.maxPrice.ToString("#,0") : "--";
            maxPriceText.text = $"Giá cao nhất: {maxValue}";
        }
    }

    private void UpdateDetailPanel(MarketItemSchema item)
    {
        if (item == null || item.item == null)
        {
            ClearDetailPanel();
            return;
        }

        detailPanelRoot?.SetActive(true);

        string localizedName = LocalizationManager.Instance.GetText(item.item.name);
        if (itemNameText != null) itemNameText.text = localizedName;
        if (itemLevelText != null) itemLevelText.text = $"Lv. {item.level}";
        if (itemPriceText != null) itemPriceText.text = $"Giá hiện tại: {item.Price:#,0}";
        if (itemDescriptionText != null) itemDescriptionText.text = LocalizationManager.Instance.GetText(item.item.description);

        if (itemImage != null)
        {
            itemImage.sprite = null;
            StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{item.itemId}.png", s =>
            {
                if (s != null && selectedItem == item)
                    itemImage.sprite = s;
            }));
        }

        bool canBuy = playerInfo != null && playerInfo.RingBall >= item.Price && playerInfo.id != item.playerId;
        ConfigureBuyButton(item, canBuy);
        ConfigureDetailInfoButton(item);
    }

    private void ConfigureBuyButton(MarketItemSchema item, bool canBuy)
    {
        if (buyButton == null)
            return;

        buyButton.onClick.RemoveAllListeners();
        buyButton.interactable = canBuy;
        SetButtonAlpha(buyButton, canBuy ? 1f : 0.5f);

        if (canBuy)
        {
            buyButton.onClick.AddListener(() => OnClickBuy(item));
        }
    }

    private void ConfigureDetailInfoButton(MarketItemSchema item)
    {
        if (detailInfoButton == null)
            return;

        detailInfoButton.onClick.RemoveAllListeners();
        detailInfoButton.interactable = item?.item != null;
        SetButtonAlpha(detailInfoButton, detailInfoButton.interactable ? 1f : 0.5f);

        if (detailInfoButton.interactable)
            detailInfoButton.onClick.AddListener(() => PopupHelper.Instance.ShowItemInfoPopup(item.item, ItemInfoPopupTab.Market));
    }

    private void ClearDetailPanel()
    {
        if (itemNameText != null) itemNameText.text = "Chọn vật phẩm";
        if (itemLevelText != null) itemLevelText.text = string.Empty;
        if (itemPriceText != null) itemPriceText.text = "Giá hiện tại: --";
        if (itemDescriptionText != null) itemDescriptionText.text = string.Empty;
        if (itemImage != null) itemImage.sprite = null;
        UpdatePriceOverview(null);
        SetButtonInteractable(buyButton, false);
        SetButtonInteractable(detailInfoButton, false);
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.interactable = interactable;
        SetButtonAlpha(button, interactable ? 1f : 0.5f);
    }

    private static void SetButtonAlpha(Button button, float alpha)
    {
        if (button == null)
            return;

        var buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            var color = buttonImage.color;
            color.a = alpha;
            buttonImage.color = color;
        }

        var buttonText = button.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            var color = buttonText.color;
            color.a = alpha;
            buttonText.color = color;
        }
    }

    private void EnsureDetailPanel()
    {
        Transform parent = MarketPanel != null ? MarketPanel.transform : transform;
        if (parent == null)
            return;

        if (detailPanelRoot == null)
        {
            var existing = parent.Find("MarketDetailPanel");
            detailPanelRoot = existing != null ? existing.gameObject : CreateDetailPanelRoot(parent);
        }

        if (detailPanelRoot == null)
            return;

        var rootTransform = detailPanelRoot.transform;
        itemImage ??= rootTransform.Find("ItemImage")?.GetComponent<Image>();
        itemNameText ??= rootTransform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        itemLevelText ??= rootTransform.Find("LevelText")?.GetComponent<TextMeshProUGUI>();
        itemPriceText ??= rootTransform.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
        minPriceText ??= rootTransform.Find("MinPriceText")?.GetComponent<TextMeshProUGUI>();
        maxPriceText ??= rootTransform.Find("MaxPriceText")?.GetComponent<TextMeshProUGUI>();
        detailInfoButton ??= rootTransform.Find("DetailButton")?.GetComponent<Button>();
        buyButton = buyButton == null || buyButton == searchButton ? rootTransform.Find("BuyButton")?.GetComponent<Button>() : buyButton;

        if (itemImage == null) itemImage = CreateDetailImage(rootTransform);
        if (itemNameText == null) itemNameText = CreateDetailText(rootTransform, "NameText", 44, FontStyles.Bold);
        if (itemLevelText == null) itemLevelText = CreateDetailText(rootTransform, "LevelText", 32, FontStyles.Normal);
        if (itemPriceText == null) itemPriceText = CreateDetailText(rootTransform, "PriceText", 36, FontStyles.Bold);
        if (minPriceText == null) minPriceText = CreateDetailText(rootTransform, "MinPriceText", 30, FontStyles.Normal);
        if (maxPriceText == null) maxPriceText = CreateDetailText(rootTransform, "MaxPriceText", 30, FontStyles.Normal);

        if (buyButton == null || buyButton == searchButton || detailInfoButton == null)
            CreateDetailButtons(rootTransform);

        ApplyDetailDefaults();
    }

    private GameObject CreateDetailPanelRoot(Transform parent)
    {
        var root = new GameObject("MarketDetailPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        root.transform.SetParent(parent, false);

        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(520f, 980f);
        rect.anchoredPosition = new Vector2(-60f, 0f);

        var background = root.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.45f);
        background.raycastTarget = false;

        var layout = root.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = root.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        return root;
    }

    private Image CreateDetailImage(Transform parent)
    {
        var imageObj = new GameObject("ItemImage", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(AspectRatioFitter));
        imageObj.transform.SetParent(parent, false);

        var layoutElement = imageObj.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 360f;
        layoutElement.minHeight = 320f;

        var image = imageObj.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        var aspect = imageObj.GetComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 1f;

        return image;
    }

    private TextMeshProUGUI CreateDetailText(Transform parent, string name, float fontSize, FontStyles fontStyle)
    {
        var textObj = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(parent, false);

        var layoutElement = textObj.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = fontSize + 24f;
        layoutElement.minHeight = fontSize + 12f;

        var text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = string.Empty;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.color = Color.white;

        return text;
    }

    private void CreateDetailButtons(Transform parent)
    {
        var row = parent.Find("ButtonRow")?.gameObject;
        if (row == null)
        {
            row = new GameObject("ButtonRow", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);

            var layoutElement = row.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 120f;
            layoutElement.minHeight = 110f;

            var horizontalLayout = row.GetComponent<HorizontalLayoutGroup>();
            horizontalLayout.spacing = 20f;
            horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
            horizontalLayout.childControlWidth = false;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;
        }

        buyButton = buyButton == null || buyButton == searchButton ? CreateButton(row.transform, "BuyButton", "Mua", new Vector2(300f, 110f)) : buyButton;
        detailInfoButton ??= CreateButton(row.transform, "DetailButton", "i", new Vector2(110f, 110f));
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 size)
    {
        var buttonObj = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        var rect = buttonObj.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        var layoutElement = buttonObj.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = size.x;
        layoutElement.preferredHeight = size.y;
        layoutElement.minHeight = size.y;

        var image = buttonObj.GetComponent<Image>();
        image.color = new Color(0.16f, 0.55f, 0.26f, 1f);

        var button = buttonObj.GetComponent<Button>();
        button.targetGraphic = image;

        var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(buttonObj.transform, false);

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = name == "DetailButton" ? 60f : 44f;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;

        return button;
    }

    private void ApplyDetailDefaults()
    {
        if (detailPanelRoot != null && detailPanelRoot.activeSelf == false)
            detailPanelRoot.SetActive(true);

        if (itemPriceText != null && string.IsNullOrEmpty(itemPriceText.text))
            itemPriceText.text = "Giá hiện tại: --";

        if (minPriceText != null && string.IsNullOrEmpty(minPriceText.text))
            minPriceText.text = "Giá thấp nhất: --";

        if (maxPriceText != null && string.IsNullOrEmpty(maxPriceText.text))
            maxPriceText.text = "Giá cao nhất: --";

        SetButtonInteractable(buyButton, false);
        SetButtonInteractable(detailInfoButton, false);
    }

    private void ClearItemGrid()
    {
        if (content == null)
            return;

        foreach (Transform child in content)
            Destroy(child.gameObject);
    }
}
