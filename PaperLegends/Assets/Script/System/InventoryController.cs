using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class InventoryController : MonoBehaviour
{
    [Header("Data Config")]
    public Transform EquipListPanel;
    public InventoryGridView inventoryGridView;
    public GameObject Level10VFXPrefab;
    private List<EquipPlayer> equippedItems = new List<EquipPlayer>();

    private int _selectedEquipSlot = 1;
 
    public Renderer characterRenderer;
    public Renderer BallRenderer;
    public SkinnedMeshRenderer hairRenderer; // Component chứa Mesh tóc
    private PlayerInventorySchema modelplayer = null;
    public static InventoryController Instance;
    private readonly Dictionary<Image, Color> _equipSlotBaseColors = new Dictionary<Image, Color>();
    public PlayerInventorySchema CurrentInventory => modelplayer;

    // public TextMeshProUGUI TextNamePlayer;
    // public TextMeshProUGUI TextLevel;
    //  public TextMeshProUGUI TextEXP;
    // public TextMeshProUGUI Money;
    // public TextMeshProUGUI RingBall;
    public TextMeshProUGUI InforItem;
    public TextMeshProUGUI InforItemLevel;
    private const int DefaultItemId = 3;
    private void Awake()
    {
        Instance = this;
    }
    public void OnLoadTab()
    {
        if (inventoryGridView != null)
            inventoryGridView.SetNotifySelectionEvents(false);
        ShowInventoryList(true);
        StartCoroutine(ItemVisualHelper.ApplyMaterial(characterRenderer, BallRenderer, hairRenderer, 0, (int)TypeItemGid.Other, modelplayer.Hair));
    }
    private void Start()
    {
    }

    public void SelectEquipSlot(int slotId)
    {
        _selectedEquipSlot = Mathf.Clamp(slotId, 1, 3);
    }

 
    private IEnumerator LoadInforPlayerCoroutine()
    {
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetPlayerInventoryAsync(GameManagerNetWork.Instance.loginUserModel.UserId),
            result => modelplayer = result));

        if (modelplayer == null)
        {
            yield break;
        }
        equippedItems = modelplayer.equippedItems;
        //TextNamePlayer.text = modelplayer.PlayerName;
        //TextLevel.text = modelplayer.Level.ToString();
       // TextEXP.text = modelplayer.Exp.ToString() + "/" + maxExp.ToString();
       // RingBall.text = "Culi: " + modelplayer.RingBall.ToString();
       // Money.text = "Tiền: " + modelplayer.Money.ToString();
        yield break;
    }

    public void OnClickDismantleItem(ItemSchema item)
    {
        StartCoroutine(DismantleItemCoroutine(item));
    }

    public void OnClickRepairItem(ItemSchema item)
    {
        StartCoroutine(RepairItemCoroutine(item));
    }

    private IEnumerator DismantleItemCoroutine(ItemSchema item)
    {
        if (item == null)
            yield break;

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        PlayerInventorySchema data = null;
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.DismantleBallAsync(playerId, item.id, item.seq),
            result => data = result));
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);

        if (data != null)
        {
            modelplayer = data;
            equippedItems = modelplayer.equippedItems;
            NotificationHelper.Instance.ShowNotification("Phân rã thành công.", true);
            if (UserInfoHandler.Instance != null)
            {
                yield return StartCoroutine(UserInfoHandler.Instance.LoadPlayerInfo());
            }
            yield return StartCoroutine(ShowInventoryListCoroutine(true));
        }
        else
        {
            string message = APIManager.Instance != null && !string.IsNullOrWhiteSpace(APIManager.Instance.LastErrorMessage)
                ? APIManager.Instance.LastErrorMessage
                : LocalizationManager.Instance.GetText("noti_network_false");
            NotificationHelper.Instance.ShowNotification(message, false);
        }
    }

    private IEnumerator RepairItemCoroutine(ItemSchema item)
    {
        if (item == null)
            yield break;

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        PlayerInventorySchema data = null;
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.RepairBallAsync(playerId, item.id, item.seq),
            result => data = result));
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);

        if (data != null)
        {
            modelplayer = data;
            equippedItems = modelplayer.equippedItems;
            NotificationHelper.Instance.ShowNotification("Sửa chữa thành công.", true);
            if (UserInfoHandler.Instance != null)
            {
                yield return StartCoroutine(UserInfoHandler.Instance.LoadPlayerInfo());
            }
            yield return StartCoroutine(ShowInventoryListCoroutine(true));
        }
        else
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_network_false"), false);
        }
    }
    public void onClickEquip()
    {
        StartCoroutine(EquipItemCoroutine());
    }

    private IEnumerator EquipItemCoroutine()
    {
        if (inventoryGridView == null || inventoryGridView.ModelSelected == null)
            yield break;

        var selectedItem = inventoryGridView.ModelSelected;
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        PlayerInventorySchema data = null;
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.EquipItemAsync(playerId, _selectedEquipSlot, selectedItem.id, selectedItem.seq),
            result => data = result));
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);

        if (data != null)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("equipped"), true);
            yield return StartCoroutine(LoadInforPlayerCoroutine());
            yield return StartCoroutine(ShowInventoryListCoroutine(true));
        }
        else
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_network_false"), false);
        }
        yield break;
    }

    public void onClickUnEquip()
    {
        StartCoroutine(UnEquipItemCoroutine());
    }

    public void onClickUnEquip(int itemId)
    {
        StartCoroutine(UnEquipItemCoroutine(itemId));
    }

    private IEnumerator UnEquipItemCoroutine(int? itemId = null)
    {
        EquipPlayer selectedEquip = itemId.HasValue
            ? equippedItems.FirstOrDefault(item => item.id == itemId.Value && item.locationId == _selectedEquipSlot)
              ?? equippedItems.FirstOrDefault(item => item.id == itemId.Value)
            : equippedItems.FirstOrDefault(item => item.locationId == _selectedEquipSlot);
        if (selectedEquip == null)
        {
            yield break;
        }

        int slotId = selectedEquip.locationId;
        int remainingEquippedCount = equippedItems.Count(item => item.id != DefaultItemId && item.locationId != slotId);
        if (remainingEquippedCount < 1)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_unequip_last"), false);
            yield break;
        }

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        PlayerInventorySchema data = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.UnequipItemAsync(playerId, slotId),
            result => data = result));

        if (data == null)
        {
            yield break;
        }

        yield return StartCoroutine(LoadInforPlayerCoroutine());
        yield return StartCoroutine(ShowInventoryListCoroutine(true));
        yield break;
    }
    public void onClickSellMarket(ItemSchema item = null)
    {
        StartCoroutine(ShowSellMarketPopupCoroutine(item));
    }

    public void onClickCancelSell(ItemSchema item = null)
    {
        StartCoroutine(CancelSellCoroutine(item));
    }

    private IEnumerator ShowSellMarketPopupCoroutine(ItemSchema itemFromPopup = null)
    {
        var selectedItem = itemFromPopup ?? inventoryGridView?.ModelSelected;
        if (selectedItem == null)
        {
            yield break;
        }

        int itemId = selectedItem.id;

        ItemPriceOverviewData priceOverview = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetItemPriceOverviewAsync(itemId),
            result => priceOverview = result));

        PopupHelper.Instance.ShowInputPopup(LocalizationManager.Instance.GetText("input_price"), inputText =>
        {
            if (int.TryParse(inputText, out int price) && price > 0)
            {
                StartCoroutine(SellMarketCoroutine(selectedItem, price));
            }
            else
            {
                NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_sold_false"), false);
            }
        }, priceOverview);
    }

    private IEnumerator SellMarketCoroutine(ItemSchema selectedItem, int customPrice)
    {
        if (selectedItem == null)
        {
            yield break;
        }

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        bool success = false;
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.SellItemOnMarketAsync(playerId, selectedItem.id, selectedItem.seq, customPrice),
            result => success = result));

        if (success)
        {
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("sold"), true);
            yield return StartCoroutine(ShowInventoryListCoroutine(true));
        }
        else
        {
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_network_false"), false);
        }
    }

    private IEnumerator CancelSellCoroutine(ItemSchema itemFromPopup = null)
    {
        var selectedItem = itemFromPopup ?? inventoryGridView?.ModelSelected;
        if (selectedItem == null)
        {
            yield break;
        }

        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        bool success = false;
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.CancelSellMarketAsync(playerId, selectedItem.id, selectedItem.seq),
            result => success = result));
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);

        if (success)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("cancel_sold"), true);
            yield return StartCoroutine(ShowInventoryListCoroutine(true));
        }
        else
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_network_false"), false);
        }
    }

    IEnumerator ShowEquipList()
    {
        // Clear old items
        foreach (Transform child in EquipListPanel)
        {
            Destroy(child.gameObject);
        }
        _equipSlotBaseColors.Clear();

        // Sắp xếp theo locationId 1 → 3
        var sortedEquips = equippedItems.OrderBy(e => e.locationId).ToList();

        for (int i = 0; i < 3; i++)
        {
            int slotIndex = i + 1;
            GameObject slot = Instantiate(inventoryGridView.ItemPrefab, EquipListPanel);
            slot.tag = "Untagged";
            var view = slot.GetComponent<ItemPrefabView>();
            Image icon = view != null ? view.ItemIcon : null;
            var outline = slot.GetComponent<Image>();
            if (outline != null && !_equipSlotBaseColors.ContainsKey(outline))
                _equipSlotBaseColors.Add(outline, outline.color);

            EquipPlayer equipItem = sortedEquips.FirstOrDefault(x => x.locationId == slotIndex);

            if (equipItem != null)
            {
                ItemVisualHelper.SetLevelVisual(
                    slot.transform,
                    equipItem.level,
                    (TypeItemGid)equipItem.typeGid);
                ItemVisualHelper.ApplyRarityBackground(slot.transform, equipItem.rarityGid);

                if (icon != null)
                {
                    icon.gameObject.SetActive(true);
                    int id = equipItem.id;
                    StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{id}.png", sprite =>
                    {
                        if (sprite != null)
                            icon.sprite = sprite;
                        else
                            icon.sprite = ItemVisualHelper.LoadSpriteByID(id);
                    }));
                }
            }
            else
            {
                ConfigureEmptyEquipSlot(view);
            }

            if (view != null && view.LineBanner != null)
                view.LineBanner.SetActive(false);

            Button btn = slot.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    SelectEquipSlot(slotIndex);
                    UpdateEquipSlotHighlight();
                    if (equipItem != null)
                    {
                        var itemData = new ItemSchema
                        {
                            id = equipItem.id,
                            seq = equipItem.seq,
                            name = equipItem.name,
                            description = equipItem.description,
                            Levelrequired = equipItem.Levelrequired,
                            level = equipItem.level,
                            typeGid = equipItem.typeGid,
                            price = equipItem.price,
                            isLevelUp = equipItem.isLevelUp,
                            isOpen = equipItem.isOpen,
                            isCateye = equipItem.isCateye,
                            locationGid = equipItem.locationGid,
                            rarityGid = equipItem.rarityGid,
                            Mass = equipItem.Mass,
                            GravityScale = equipItem.GravityScale,
                            Drag = equipItem.Drag,
                            Bounciness = equipItem.Bounciness,
                            Elasticity = equipItem.Elasticity,
                            ImpactResistance = equipItem.ImpactResistance,
                            damage = equipItem.damage,
                            ElementType = equipItem.element,
                            activeSkill = equipItem.activeSkill,
                            SkillGid = equipItem.SkillGid
                        };
                        ApplyInventorySkillData(itemData);
                        PopupHelper.Instance.ShowItemInfoPopup(itemData, ItemInfoPopupTab.Equipped);
                    }
                    else
                    {
                    }
                });
            }
        }

        UpdateEquipSlotHighlight();
        LayoutRebuilder.ForceRebuildLayoutImmediate(EquipListPanel.GetComponent<RectTransform>());
        yield break;
    }

    private void ApplyInventorySkillData(ItemSchema itemData)
    {
        if (itemData == null)
            return;

        bool hasSkillDescription = itemData.activeSkill != null &&
                                   itemData.activeSkill.GenCode > 0 &&
                                   !string.IsNullOrWhiteSpace(itemData.activeSkill.description);
        if (hasSkillDescription)
            return;

        var inventoryItem = modelplayer?.playerItems?
            .FirstOrDefault(playerItem => playerItem.id == itemData.id && playerItem.seq == itemData.seq);

        if (inventoryItem == null)
            return;

        if (inventoryItem.activeSkill != null && inventoryItem.activeSkill.GenCode > 0)
            itemData.activeSkill = inventoryItem.activeSkill;

        if (!itemData.SkillGid.HasValue && inventoryItem.SkillGid.HasValue)
            itemData.SkillGid = inventoryItem.SkillGid;
    }

    private static void ConfigureEmptyEquipSlot(ItemPrefabView view)
    {
        if (view == null)
            return;

        if (view.ItemIcon != null)
            view.ItemIcon.gameObject.SetActive(false);
        if (view.LevelText != null)
            view.LevelText.gameObject.SetActive(false);
        if (view.LevelBanner != null)
            view.LevelBanner.gameObject.SetActive(false);
        if (view.QuantityText != null)
            view.QuantityText.gameObject.SetActive(false);
        if (view.StatusLabel != null)
            view.StatusLabel.SetActive(false);
        if (view.LineBanner != null)
            view.LineBanner.SetActive(false);
        if (view.RemoveButton != null)
            view.RemoveButton.gameObject.SetActive(false);
    }


    private void UpdateEquipSlotHighlight()
    {
        int index = Mathf.Clamp(_selectedEquipSlot, 1, 3);
        int current = 1;
        foreach (Transform child in EquipListPanel)
        {
            Image outline = child.GetComponent<Image>();
            ItemPrefabView view = child.GetComponent<ItemPrefabView>();
            bool hasItem = view != null && view.ItemIcon != null && view.ItemIcon.gameObject.activeSelf;
            if (view != null && view.LineBanner != null)
                view.LineBanner.SetActive(hasItem && current == index);

            if (outline != null)
            {
                if (current == index)
                {
                    outline.color = new Color(1f, 1f, 0f, 1f);
                }
                else if (ItemVisualHelper.TryGetRarityBaseColor(outline, out var baseColor))
                {
                    outline.color = baseColor;
                }
                else if (_equipSlotBaseColors.TryGetValue(outline, out var defaultColor))
                {
                    outline.color = defaultColor;
                }
                else
                {
                    outline.color = new Color(1f, 1f, 1f, 1f);
                }
            }
            current++;
        }
    }
    public void ShowInventoryList(bool clearSelection = false)
    {
        if (inventoryGridView != null)
            inventoryGridView.SetNotifySelectionEvents(false);

        StartCoroutine(ShowInventoryListCoroutine(clearSelection));
    }

    private IEnumerator ShowInventoryListCoroutine(bool clearSelection = false)
    {
        if (inventoryGridView != null)
            inventoryGridView.SetNotifySelectionEvents(false);

        if (clearSelection)
            inventoryGridView?.ClearSelection();

        yield return StartCoroutine(LoadInforPlayerCoroutine());
        yield return StartCoroutine(ShowEquipList());
        inventoryGridView.ShowGrid(modelplayer, Enumerable.Empty<(int, int)>());
        yield break;
    }
}
