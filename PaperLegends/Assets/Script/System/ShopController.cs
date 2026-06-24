using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class ShopController : MonoBehaviour
{
    private const string CurrencyMoneyLabel = "Price";
    private const string CurrencyRingBallLabel = "RingBall";

    public GameObject TabShopListPanel;
    public GameObject ItemPrefab;
    [SerializeField] private TabManager tabManager;
    private int idItem;
    private int typeItemGid;
    private int selectedPrice;
    private ShopPurchaseCurrency selectedCurrency = ShopPurchaseCurrency.Money;
    public TextMeshProUGUI itemName;
    public TextMeshProUGUI description;
    public TextMeshProUGUI itemPrice;
   // public TextMeshProUGUI LevelPlayer;
    public TextMeshProUGUI InforItem;
    public TextMeshProUGUI InforItemLevel;

    public Renderer characterRenderer;
    public Renderer BallRenderer;
    public SkinnedMeshRenderer hairRenderer; // Component chứa Mesh tóc

    public static ShopController Instance;
    public TextMeshProUGUI TextNamePlayer;
    public TextMeshProUGUI TextLevel;
    public TextMeshProUGUI TextEXP;
    public TextMeshProUGUI Money;
    public TextMeshProUGUI RingBall;
    public Slider ExpSlider;
    public Slider MassSlider;
    public Slider SpeedSlider;
    public Slider BounceSlider;
    public Slider ImpactSlider;
    private PlayerInventorySchema modelplayer = null;
    private void Awake()
    {
        Instance = this;
    }

    public void onClickBuy()
    {
        StartCoroutine(BuyItemCoroutine());
    }

    private IEnumerator BuyItemCoroutine()
    {
        if (modelplayer == null)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_buy_false"), false);
            yield break;
        }

        bool isEnoughCurrency = selectedCurrency == ShopPurchaseCurrency.Money
            ? selectedPrice <= modelplayer.Money
            : selectedPrice <= modelplayer.RingBall;
        if (!isEnoughCurrency)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_buy_false"), false);
            yield break;
        }

        PlayerInventorySchema data = null;
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.BuyItemAsync(playerId, idItem, selectedCurrency),
            result => data = result));

        if (data == null)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_buy_false"), false);
            yield break;
        }

        NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_buy_true"), true);
        yield return StartCoroutine(LoadInforPlayerCoroutine());
        yield return StartCoroutine(ShowShopListCoroutine());
    }

    private IEnumerator LoadInforPlayerCoroutine()
    {
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetPlayerInventoryAsync(GameManagerNetWork.Instance.loginUserModel.UserId),
            result => modelplayer = result));

        if (modelplayer == null)
            yield break;

        int maxExp = ItemVisualHelper.GetExpForNextLevel(modelplayer.Level);
        ExpSlider.maxValue = maxExp;
        ExpSlider.value = modelplayer.Exp;

        TextNamePlayer.text = modelplayer.PlayerName;
        TextLevel.text = modelplayer.Level.ToString();
        TextEXP.text = modelplayer.Exp + "/" + maxExp.ToString();
        RingBall.text = modelplayer.RingBall.ToString();
        Money.text = modelplayer.Money.ToString();
        yield break;
    }

    public void ShowShopList()
    {
        StartCoroutine(ShowShopListCoroutine());
    }

    private IEnumerator ShowShopListCoroutine()
    {
        yield return StartCoroutine(LoadInforPlayerCoroutine());

        if (tabManager == null && TabShopListPanel != null)
            tabManager = TabShopListPanel.GetComponent<TabManager>();
        if (tabManager == null || tabManager.TabContents == null)
        {
            Debug.LogError("ShopController chưa được gán TabManager hoặc thiếu TabContents.");
            yield break;
        }

        Dictionary<TypeItemGid, Transform> tabPanels = new Dictionary<TypeItemGid, Transform>()
        {
            { TypeItemGid.PackageMoney, FindContent(tabManager.TabContents[0]) },
            { TypeItemGid.PackageBall, FindContent(tabManager.TabContents[1]) },
            { TypeItemGid.Gem, FindContent(tabManager.TabContents[2]) }
        };

        List<ItemSchema> items = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetItemsAsync((int)LocationItemGid.Shop, GameManagerNetWork.Instance.loginUserModel.UserId),
            result => items = result));

        foreach (var panel in tabPanels.Values)
        {
            ClearItems(panel);
        }

        if (items != null && items.Count > 0)
        {
            foreach (var group in items.GroupBy(i => (TypeItemGid)i.typeGid))
            {
                if (tabPanels.TryGetValue(group.Key, out Transform panel))
                    LoadItemsToPanel(group.ToList(), panel);
            }
        }

        // Tự động chọn item đầu tiên để xem trước
        AutoSelectFirstItem(tabPanels[TypeItemGid.PackageMoney]);

        yield break;
    }

    private bool CanBuy(ItemSchema item)
    {
        return CanBuy(item, ShopPurchaseCurrency.Money) || CanBuy(item, ShopPurchaseCurrency.RingBall);
    }

    private bool CanBuy(ItemSchema item, ShopPurchaseCurrency currencyType)
    {
        if (item == null || modelplayer == null)
            return false;

        int itemPrice = GetItemPrice(item, currencyType);
        if (itemPrice <= 0)
            return false;

        return modelplayer != null &&
               modelplayer.Level >= item.Levelrequired &&
               (currencyType == ShopPurchaseCurrency.Money
                    ? modelplayer.Money >= itemPrice
                    : modelplayer.RingBall >= itemPrice) &&
               !item.isDailyPurchaseLocked;
    }

    private int GetItemPrice(ItemSchema item, ShopPurchaseCurrency currencyType)
    {
        return currencyType == ShopPurchaseCurrency.Money ? item.price : item.priceByBall;
    }

    private static string GetCurrencyLabel(ShopPurchaseCurrency currencyType)
    {
        return currencyType == ShopPurchaseCurrency.Money ? CurrencyMoneyLabel : CurrencyRingBallLabel;
    }

    private void LoadItemsToPanel(List<ItemSchema> itemList, Transform tabPanel)
    {
        var sortedList = new List<ItemSchema>(itemList);
        sortedList.Sort((a, b) =>
        {
            int canBuyA = CanBuy(a) ? 0 : 1;
            int canBuyB = CanBuy(b) ? 0 : 1;
            int cmp = canBuyA.CompareTo(canBuyB);
            if (cmp != 0) return cmp;
            return a.Levelrequired.CompareTo(b.Levelrequired);
        });

        // Duyệt danh sách item và tạo UI
        foreach (var item in sortedList)
        {
            GameObject newItem = Instantiate(ItemPrefab, tabPanel);

            var itemView = newItem.GetComponent<ShopItemEntryView>();
            if (itemView == null)
            {
                Debug.LogError("Prefab shop item chưa gắn ShopItemEntryView.");
                continue;
            }

            if (itemView.NameText != null)
                itemView.NameText.text = LocalizationManager.Instance.GetText(item.name);

            if (itemView.PriceByMoneyText != null)
                itemView.PriceByMoneyText.text = item.price > 0 ? item.price.ToString("#,0") : "--";

            if (itemView.PriceByRingBallText != null)
                itemView.PriceByRingBallText.text = item.priceByBall > 0 ? item.priceByBall.ToString("#,0") : "--";

            if (itemView.MaxPurchasePerDayText != null)
                itemView.MaxPurchasePerDayText.text = item.dailyPurchaseLimit.ToString();

            if (itemView.PurchasedTodayText != null)
                itemView.PurchasedTodayText.text = item.dailyPurchasedCount.ToString();

            if (itemView.ItemImage != null)
            {
                StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{item.id}.png", sprite =>
                {
                    if (sprite != null)
                        itemView.ItemImage.sprite = sprite;
                    else
                        itemView.ItemImage.sprite = ItemVisualHelper.LoadSpriteByID(item.id);
                }));
            }
            else
            {
                Debug.LogError("Không tìm thấy ItemImage trong ShopItemEntryView!");
            }

            bool itemBuyable = CanBuy(item);

            SetupBuyButton(itemView.BuyByMoneyButton, item, ShopPurchaseCurrency.Money, itemBuyable);
            SetupBuyButton(itemView.BuyByRingBallButton, item, ShopPurchaseCurrency.RingBall, itemBuyable);
        }

        // Đảm bảo UI cập nhật ngay lập tức
        LayoutRebuilder.ForceRebuildLayoutImmediate(tabPanel.GetComponent<RectTransform>());
    }

    private void SetupBuyButton(Button buyButton, ItemSchema item, ShopPurchaseCurrency currencyType, bool itemBuyable)
    {
        if (buyButton == null)
            return;

        buyButton.onClick.RemoveAllListeners();
        int itemPrice = GetItemPrice(item, currencyType);
        bool canBuyByCurrency = itemBuyable && itemPrice > 0 && CanBuy(item, currencyType);
        buyButton.interactable = canBuyByCurrency;

        Image btnImage = buyButton.GetComponent<Image>();
        if (btnImage != null)
            btnImage.color = canBuyByCurrency ? Color.white : new Color(1f, 1f, 1f, 0.5f);

        buyButton.onClick.AddListener(() =>
        {
            if (item.isDailyPurchaseLocked)
            {
                NotificationHelper.Instance.ShowNotification($"Đã đạt giới hạn mua hôm nay ({item.dailyPurchasedCount}/{item.dailyPurchaseLimit}).", false);
                return;
            }

            idItem = item.id;
            typeItemGid = item.typeGid;
            selectedPrice = itemPrice;
            selectedCurrency = currencyType;
            itemName.text = LocalizationManager.Instance.GetText(item.name);
            //itemPrice.text = $"{itemPrice:#,0} {GetCurrencyLabel(currencyType)}";
            description.text = LocalizationManager.Instance.GetText(item.description);
            onClickBuy();
        });
    }


    private void ClearItems(Transform tabPanel)
    {
        foreach (Transform child in tabPanel)
        {
            Destroy(child.gameObject);
        }
    }

    private Transform FindContent(GameObject parentTab)
    {
        return parentTab.transform.Find("Viewport/Content");
    }

    private void AutoSelectFirstItem(Transform tabPanel)
    {
        if (tabPanel.childCount > 0)
        {
            Button btn = tabPanel.GetChild(0).GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.Invoke();
            }
        }
    }

}
