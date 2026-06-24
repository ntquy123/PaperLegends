using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
 

public class InventoryGridView : MonoBehaviour
{
    [Header("Inventory Grid")]
    [FormerlySerializedAs("TabInventoryListPanel")]
    [SerializeField]
    private GameObject tabInventoryListPanel;
    [FormerlySerializedAs("TabTypeListPanel")]
    [SerializeField]
    private GameObject tabTypeListPanel;

    [FormerlySerializedAs("ItemPrefab")]
    [SerializeField]
    private GameObject itemPrefab;
    [Header("Infor Header")]
    [SerializeField]
    private Image selectedItemType;
    //public Button moveToFusionButton;
    //public Button removeFusionButton;
    //public Button addFusionButton;

    public event Action<ItemSchema> OnItemSelected;
    [SerializeField]
    private bool notifySelectionEvents = true;
    public ItemSchema ModelSelected { get; private set; }

    public GameObject TabInventoryListPanel => tabInventoryListPanel;
    public GameObject TabTypeListPanel => tabTypeListPanel;
    public GameObject ItemPrefab => itemPrefab;
    public void SetNotifySelectionEvents(bool notify) => notifySelectionEvents = notify;

    private readonly Dictionary<(int itemId, int seq), List<GameObject>> _gridItemObjects = new();

    private GameObject _selectedLineBanner;
    private PlayerInventorySchema _currentInventory;
    private IEnumerable<(int id, int seq)> _excludedItems = Enumerable.Empty<(int, int)>();
    private IEnumerable<(int id, int seq)> _equippedItems = Enumerable.Empty<(int, int)>();

    private void Start()
    {
        RegisterTabEvents();
    }

    private void RegisterTabEvents()
    {
        var scriptTab = TabTypeListPanel.GetComponent<TabManager>();
        if (scriptTab == null || scriptTab.TabButtons == null)
            return;

        if (scriptTab.TabButtons.Length > 0 && scriptTab.TabButtons[0] != null)
            scriptTab.TabButtons[0].onClick.AddListener(ShowInventoryTab);
        if (scriptTab.TabButtons.Length > 1 && scriptTab.TabButtons[1] != null)
            scriptTab.TabButtons[1].onClick.AddListener(ShowUplevelTab);
        if (scriptTab.TabButtons.Length > 2 && scriptTab.TabButtons[2] != null)
            scriptTab.TabButtons[2].onClick.AddListener(ShowFusionTab);
    }

    public void ShowGrid(PlayerInventorySchema inventory, IEnumerable<(int id, int seq)> excluded)
    {
        _currentInventory = inventory;
        _excludedItems = excluded ?? Enumerable.Empty<(int, int)>();

        Clear();
        if (inventory == null)
            return;

        var scriptTab = TabInventoryListPanel.GetComponent<TabManager>();
        var tabPanels = new Dictionary<TypeItemGid, Transform>();
        var mappings = new (TypeItemGid type, int index)[]
        {
            (TypeItemGid.All, 0),
            (TypeItemGid.Culi, 1),
            (TypeItemGid.Gem, 2),
            (TypeItemGid.Other, 3),
            (TypeItemGid.Sale, 4)
        };

        foreach (var (type, idx) in mappings)
        {
            if (idx < scriptTab.TabContents.Length)
            {
                var content = scriptTab.FindContent(scriptTab.TabContents[idx]);
                if (content != null)
                    tabPanels[type] = content;
            }
        }

        var excludedSet = new HashSet<(int, int)>(_excludedItems);
        var allItems = inventory.playerItems.Where(p => !excludedSet.Contains((p.id, p.seq))).ToList();

        foreach (var grp in allItems.Where(p => p.typeGid == (int)TypeItemGid.Gem).GroupBy(p => p.id))
        {
            ItemVisualHelper.InitGroup(grp.Key, grp.Select(x => x.seq));
        }

        foreach (var kvp in tabPanels)
        {
            var type = kvp.Key;
            var panel = kvp.Value;
            foreach (Transform child in panel)
                Destroy(child.gameObject);

            var items = allItems;
            if (type == TypeItemGid.All)
            {
                items = items.Where(x => x.IsSolded != StatusSold.Sale).ToList();
            }
            else if (type == TypeItemGid.Sale)
            {
                items = items.Where(p => p.IsSolded == StatusSold.Sale).ToList();
            }
            else
            {
                items = items.Where(p => p.typeGid == (int)type && p.IsSolded != StatusSold.Sale).ToList();
            }

            foreach (var item in items.Where(p => p.typeGid != (int)TypeItemGid.Gem))
            {
                GameObject obj = Instantiate(ItemPrefab, panel);
                ItemVisualHelper.SetLevelVisual(obj.transform, item.level, (TypeItemGid)item.typeGid);
                ItemVisualHelper.ApplyRarityBackground(obj.transform, item.rarityGid);
                //ItemVisualHelper.HideItemActions(obj.transform);
                ToggleEquippedLabel(obj.transform, false);
                var key = (item.id, item.seq);
                if (!_gridItemObjects.TryGetValue(key, out var list))
                {
                    list = new List<GameObject>();
                    _gridItemObjects[key] = list;
                }
                list.Add(obj);

                ItemPrefabView itemView = obj.GetComponent<ItemPrefabView>();
                Image itemImage = itemView != null ? itemView.ItemIcon : null;
                if (itemImage != null)
                {
                    StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{item.id}.png", sprite =>
                    {
                        itemImage.sprite = sprite != null ? sprite : ItemVisualHelper.LoadSpriteByID(item.id);
                    }));
                }

                var elementDisplay = obj.GetComponent<ItemElementDisplay>();
                if (elementDisplay != null)
                {
                    elementDisplay.SetElement(item.ElementType);
                }

                Button btn = obj.GetComponent<Button>();
                Image outline = obj.GetComponent<Image>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        ModelSelected = item;
                        StartCoroutine(ItemVisualHelper.ApplyMaterial(null, InventoryController.Instance.BallRenderer, null, item.id, item.typeGid, null,item.isCateye));
                        HighlightItem(outline, itemView != null ? itemView.LineBanner : null);
                        UpdateSelectedItemHeader(item);
                        if (notifySelectionEvents)
                            OnItemSelected?.Invoke(item);
 
                        PopupHelper.Instance.CloseActivePopup();
                        PopupHelper.Instance.ShowItemInfoPopup(item, ItemInfoPopupTab.Inventory);

                        //ItemVisualHelper.ShowItemActions(obj.transform, item);
                    });
                }
                if (itemView != null)
                {
                    if (itemView.RemoveButton != null)
                        itemView.RemoveButton.gameObject.SetActive(false);
                    if (itemView.LineBanner != null)
                        itemView.LineBanner.SetActive(false);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
            }

            foreach (var group in items.Where(p => p.typeGid == (int)TypeItemGid.Gem).GroupBy(p => p.id))
            {
                var item = group.First();
                item.seqList = ItemVisualHelper.GetGroup(item.id).seqList;
                var obj = ItemVisualHelper.InstantiateGroupedItem(ItemPrefab, panel, item, _gridItemObjects);
                ItemVisualHelper.ApplyRarityBackground(obj.transform, item.rarityGid);
                ToggleEquippedLabel(obj.transform, false);
                 

                ItemPrefabView gemView = obj.GetComponent<ItemPrefabView>();
                Image gemImage = gemView != null ? gemView.ItemIcon : null;
                if (gemImage != null)
                {
                    StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{item.id}.png", sprite =>
                    {
                        gemImage.sprite = sprite != null ? sprite : ItemVisualHelper.LoadSpriteByID(item.id);
                    }));
                }

                var gemElementDisplay = obj.GetComponent<ItemElementDisplay>();
                if (gemElementDisplay != null)
                {
                    gemElementDisplay.SetElement(item.ElementType);
                }

                Button gemBtn = obj.GetComponent<Button>();
                Image gemOutline = obj.GetComponent<Image>();
                if (gemBtn != null)
                {
                    gemBtn.onClick.AddListener(() =>
                    {
                        ModelSelected = item;
                        HighlightItem(gemOutline, gemView != null ? gemView.LineBanner : null);
                        UpdateSelectedItemHeader(item);
                        if (notifySelectionEvents)
                            OnItemSelected?.Invoke(item);
                        PopupHelper.Instance.ShowItemInfoPopup(item, ItemInfoPopupTab.Inventory);
                        //ItemVisualHelper.ShowItemActions(obj.transform, item);
                    });
                }
                if (gemView != null && gemView.LineBanner != null)
                    gemView.LineBanner.SetActive(false);

                LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
            }
        }
    }

    public void ShowGridForUplevel(
        PlayerInventorySchema inventory,
        IEnumerable<(int id, int seq)> excluded,
        IEnumerable<(int id, int seq)> equippedItems)
    {
        _currentInventory = inventory;
        _excludedItems = excluded ?? Enumerable.Empty<(int, int)>();
        _equippedItems = equippedItems ?? Enumerable.Empty<(int, int)>();

        Clear();
        if (inventory == null)
            return;

        var scriptTab = TabInventoryListPanel.GetComponent<TabManager>();
        var tabPanels = new Dictionary<TypeItemGid, Transform>();
        var mappings = new (TypeItemGid type, int index)[]
        {
            (TypeItemGid.All, 0),
            (TypeItemGid.Culi, 1),
            (TypeItemGid.Gem, 2),
            (TypeItemGid.Other, 3),
            (TypeItemGid.Sale, 4)
        };

        foreach (var (type, idx) in mappings)
        {
            if (idx < scriptTab.TabContents.Length)
            {
                var content = scriptTab.FindContent(scriptTab.TabContents[idx]);
                if (content != null)
                    tabPanels[type] = content;
            }
        }

        var excludedSet = new HashSet<(int, int)>(_excludedItems);
        var equippedSet = new HashSet<(int, int)>(_equippedItems);
        var inventoryData = new List<(ItemSchema item, bool isEquipped)>();

        if (inventory.playerItems != null)
        {
            foreach (var playerItem in inventory.playerItems)
            {
                if (excludedSet.Contains((playerItem.id, playerItem.seq)))
                    continue;

                var key = (playerItem.id, playerItem.seq);
                inventoryData.Add((playerItem, equippedSet.Contains(key)));
            }
        }

        if (inventory.equippedItems != null)
        {
            foreach (var equippedItem in inventory.equippedItems)
            {
                var key = (equippedItem.id, equippedItem.seq);
                if (excludedSet.Contains(key))
                    continue;

                if (inventoryData.Any(entry => entry.item.id == equippedItem.id && entry.item.seq == equippedItem.seq))
                    continue;

                var itemSchema = new ItemSchema
                {
                    id = equippedItem.id,
                    seq = equippedItem.seq,
                    name = equippedItem.name,
                    description = equippedItem.description,
                    Levelrequired = equippedItem.Levelrequired,
                    level = equippedItem.level,
                    typeGid = equippedItem.typeGid,
                    price = equippedItem.price,
                    isLevelUp = equippedItem.isLevelUp,
                    isOpen = equippedItem.isOpen,
                    locationGid = equippedItem.locationGid,
                    Mass = equippedItem.Mass,
                    GravityScale = equippedItem.GravityScale,
                    Drag = equippedItem.Drag,
                    Bounciness = equippedItem.Bounciness,
                    Elasticity = equippedItem.Elasticity,
                    ImpactResistance = equippedItem.ImpactResistance,
                    ElementType = equippedItem.element,
                    activeSkill = equippedItem.activeSkill,
                    SkillGid = equippedItem.SkillGid
                };

                inventoryData.Add((itemSchema, true));
            }
        }

        foreach (var grp in inventoryData
                     .Select(entry => entry.item)
                     .Where(p => p.typeGid == (int)TypeItemGid.Gem)
                     .GroupBy(p => p.id))
        {
            ItemVisualHelper.InitGroup(grp.Key, grp.Select(x => x.seq));
        }

        foreach (var kvp in tabPanels)
        {
            var type = kvp.Key;
            var panel = kvp.Value;
            foreach (Transform child in panel)
                Destroy(child.gameObject);

            var items = inventoryData;
            if (type == TypeItemGid.All)
            {
                items = items.Where(x => x.item.IsSolded != StatusSold.Sale).ToList();
            }
            else if (type == TypeItemGid.Sale)
            {
                items = items.Where(p => p.item.IsSolded == StatusSold.Sale).ToList();
            }
            else
            {
                items = items.Where(p => p.item.typeGid == (int)type && p.item.IsSolded != StatusSold.Sale).ToList();
            }

            foreach (var entry in items.Where(p => p.item.typeGid != (int)TypeItemGid.Gem))
            {
                var item = entry.item;
                GameObject obj = Instantiate(ItemPrefab, panel);

                var key = (item.id, item.seq);
                ToggleEquippedLabel(obj.transform, entry.isEquipped);
                ItemVisualHelper.SetLevelVisual(obj.transform, item.level, (TypeItemGid)item.typeGid);
                ItemVisualHelper.ApplyRarityBackground(obj.transform, item.rarityGid);
                if (!_gridItemObjects.TryGetValue(key, out var list))
                {
                    list = new List<GameObject>();
                    _gridItemObjects[key] = list;
                }
                list.Add(obj);

                ItemPrefabView itemView = obj.GetComponent<ItemPrefabView>();
                Image itemImage = itemView != null ? itemView.ItemIcon : null;
                if (itemImage != null)
                {
                    StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{item.id}.png", sprite =>
                    {
                        itemImage.sprite = sprite != null ? sprite : ItemVisualHelper.LoadSpriteByID(item.id);
                    }));
                }

                var elementDisplay = obj.GetComponent<ItemElementDisplay>();
                if (elementDisplay != null)
                {
                    elementDisplay.SetElement(item.ElementType);
                }

                Button btn = obj.GetComponent<Button>();
                Image outline = obj.GetComponent<Image>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        ModelSelected = item;
                        StartCoroutine(ItemVisualHelper.ApplyMaterial(null, InventoryController.Instance.BallRenderer, null, item.id, item.typeGid, null, item.isCateye));
                        HighlightItem(outline, itemView != null ? itemView.LineBanner : null);
                        UpdateSelectedItemHeader(item);
                        if (notifySelectionEvents)
                            OnItemSelected?.Invoke(item);
                        PopupHelper.Instance.ShowItemInfoPopup(item, ItemInfoPopupTab.Market);
                    });
                }

                if (itemView != null)
                {
                    if (itemView.LineBanner != null)
                        itemView.LineBanner.SetActive(false);
                    if (itemView.RemoveButton != null)
                        itemView.RemoveButton.gameObject.SetActive(false);
                }
             
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
            }

            foreach (var group in items.Where(p => p.item.typeGid == (int)TypeItemGid.Gem).GroupBy(p => p.item.id))
            {
                var item = group.First().item;
                item.seqList = ItemVisualHelper.GetGroup(item.id).seqList;
                var obj = ItemVisualHelper.InstantiateGroupedItem(ItemPrefab, panel, item, _gridItemObjects);
                ItemVisualHelper.ApplyRarityBackground(obj.transform, item.rarityGid);
                ToggleEquippedLabel(obj.transform, false);

                ItemPrefabView gemView = obj.GetComponent<ItemPrefabView>();
                Image gemImage = gemView != null ? gemView.ItemIcon : null;
                if (gemImage != null)
                {
                    StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{item.id}.png", sprite =>
                    {
                        gemImage.sprite = sprite != null ? sprite : ItemVisualHelper.LoadSpriteByID(item.id);
                    }));
                }

                var gemElementDisplay = obj.GetComponent<ItemElementDisplay>();
                if (gemElementDisplay != null)
                {
                    gemElementDisplay.SetElement(item.ElementType);
                }

                Button gemBtn = obj.GetComponent<Button>();
                Image gemOutline = obj.GetComponent<Image>();
                if (gemBtn != null)
                {
                    gemBtn.onClick.AddListener(() =>
                    {
                        ModelSelected = item;
                        StartCoroutine(ItemVisualHelper.ApplyMaterial(null, InventoryController.Instance.BallRenderer, null, item.id, item.typeGid, null, item.isCateye));
                        HighlightItem(gemOutline, gemView != null ? gemView.LineBanner : null);
                        UpdateSelectedItemHeader(item);
                        if (notifySelectionEvents)
                            OnItemSelected?.Invoke(item);
 

                    });
                }

                if (gemView != null && gemView.RemoveButton != null)
                    gemView.RemoveButton.gameObject.SetActive(false);
                if (gemView != null && gemView.LineBanner != null)
                    gemView.LineBanner.SetActive(false);

                LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
            }
        }
    }

    public void ShowFusionTab()
    {
       // moveToFusionButton.gameObject.SetActive(true);
        //removeFusionButton.gameObject.SetActive(true);
    }

    public void ShowUplevelTab()
    {
        //moveToFusionButton.gameObject.SetActive(false);
    }

    public void ShowInventoryTab()
    {
        //moveToFusionButton.gameObject.SetActive(false);
    }

    public void SetItemActive(int itemId, int seq, bool active)
    {
        ItemVisualHelper.SetItemActive(_gridItemObjects, itemId, seq, active);
    }

    public void SetItemSelectedForUpgrade(int itemId, int seq, bool selected)
    {
        ItemVisualHelper.SetItemSelectedForUpgrade(_gridItemObjects, itemId, seq, selected);
    }

    public void ClearSelection()
    {
        HighlightItem(null, null);
        ClearHeader();
        ModelSelected = null;
    }

    public void Clear()
    {
        var scriptTab = TabInventoryListPanel.GetComponent<TabManager>();
        foreach (var tab in scriptTab.TabContents)
        {
            var panel = scriptTab.FindContent(tab);
            if (panel != null)
            {
                foreach (Transform child in panel)
                    Destroy(child.gameObject);
            }
        }
        _gridItemObjects.Clear();
        ItemVisualHelper.ClearGroups();
        HighlightItem(null, null);
        ClearHeader();
    }

    public void ShowSelectedItem(ItemSchema item)
    {
        UpdateSelectedItemHeader(item);
    }

    public void UpdateSelectedItemHeader(ItemSchema item)
    {
        if (item == null)
        {
            ClearHeader();
            return;
        }
        
        if (selectedItemType != null)
        {
            var elemDisplay = selectedItemType.GetComponent<ItemElementDisplay>();
            if (elemDisplay != null && ItemElementDisplay.HasSpriteFor(item.ElementType))
            {
                elemDisplay.SetElement(item.ElementType);
                selectedItemType.enabled = true;
            }
            else
            {
                selectedItemType.sprite = null;
                selectedItemType.enabled = false;
            }
        }
    }

    public void ClearHeader()
    {
        //MenuController.Instance.BallViewPanel.SetActive(false);
        if (selectedItemType != null)
        {
            selectedItemType.sprite = null;
            selectedItemType.enabled = false;
        }
    }

    private void HighlightItem(Image outline, GameObject lineBanner)
    {
        if (_selectedLineBanner != null)
            _selectedLineBanner.SetActive(false);

        if (lineBanner != null)
            lineBanner.SetActive(true);

        _selectedLineBanner = lineBanner;
    }

    private static void ToggleEquippedLabel(Transform itemTransform, bool isEquipped)
    {
        if (itemTransform == null)
            return;

        if (itemTransform.TryGetComponent<ItemPrefabView>(out var view) && view.StatusLabel != null)
            view.StatusLabel.gameObject.SetActive(isEquipped);
    }
}
