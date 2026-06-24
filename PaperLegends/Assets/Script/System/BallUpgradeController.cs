using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using DG.Tweening;

public class BallUpgradeController : MonoBehaviour
{
    public static BallUpgradeController Instance;

    [Header("Inventory Grid")]
    public InventoryGridView inventoryGridView;

    [Header("Ingredient Slots")]
    public Transform ingredientSlotRoot;
    public int ingredientSlotCount = 4;
    private readonly List<GameObject> _slotItems = new List<GameObject>();
    private readonly List<ItemSchema> _ingredients = new List<ItemSchema>();
    private readonly List<SlotDefaults> _slotDefaults = new List<SlotDefaults>();

    public IReadOnlyList<GameObject> SlotItems => _slotItems;
    public bool IsSlotItem(GameObject obj) => _slotItems.Contains(obj);

    [Header("Upgrade Slot")]
    public Transform upgradeSlot;
    private GameObject _upgradeSlotItem;
    private ItemSchema _upgradeItem;
    private Sprite _upgradeDefaultSprite;
    private GameObject _level10VfxInstance;

    [Header("Success Rate")]
    public Slider successRateSlider;
    public TextMeshProUGUI successRateText;
    private Image successRateFill;

    [Header("Buttons")]
    public Button UplevelButton;
    public Button addMaterialButton;
    public Button removeUplevelButton;
    public Button moveUplevelButton;
    public Button resetUpgradeButton;

    [Header("Selected Item Info")]
    public TextMeshProUGUI ItemLevel;

    [Header("Effects")]
    public GameObject successVFXPrefab;
    public GameObject failureVFXPrefab;
    public GameObject explosionVFXPrefab;
    public GameObject Level10VFXPrefab;

    private int _currentSlotIndex = -1;
 
    private CanvasGroup _uplevelButtonCanvasGroup;
    private Coroutine _scaleCoroutine;
    private Vector3 _uplevelButtonOriginalScale;
    private TextMeshProUGUI _uplevelButtonLabel;

    private const int MaxUpgradeLevel = 10;
    private const string UplevelButtonTextKey = "uplevel_button";
    private const string UplevelButtonMaxTextKey = "uplevel_button_max_level";

    public ItemSchema ModelSelected;
    private ItemSchema ModelSelectedTemp;
    private PlayerInventorySchema _inventoryData;

    private void Awake()
    {
        Instance = this;

        if (successRateSlider != null)
        {
            successRateSlider.maxValue = 100f;
            successRateFill = successRateSlider.fillRect.GetComponent<Image>();
        }

        if (addMaterialButton != null)
        {
            addMaterialButton.gameObject.SetActive(false);
            addMaterialButton.onClick.AddListener(AttachSelectedItem);
        }

         
        if (moveUplevelButton != null)
        {
            moveUplevelButton.gameObject.SetActive(true);
            moveUplevelButton.onClick.AddListener(onClickMoveUplevelButton);
        }
        if (removeUplevelButton != null)
        {
            removeUplevelButton.gameObject.SetActive(false);
            removeUplevelButton.onClick.AddListener(OnClickRemoveUplevelButton);
        }

        if (resetUpgradeButton != null)
            resetUpgradeButton.onClick.AddListener(ResetUpgradeSlots);

        if (UplevelButton != null)
        {
            UplevelButton.onClick.AddListener(OnClickLevelUpItem);
            _uplevelButtonCanvasGroup = UplevelButton.GetComponent<CanvasGroup>();
            if (_uplevelButtonCanvasGroup == null)
                _uplevelButtonCanvasGroup = UplevelButton.gameObject.AddComponent<CanvasGroup>();
            _uplevelButtonOriginalScale = UplevelButton.transform.localScale;
            _uplevelButtonLabel = UplevelButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // capture upgrade slot default sprite
        var upgradeImg = upgradeSlot?.GetComponent<Image>();
        if (upgradeImg != null)
            _upgradeDefaultSprite = upgradeImg.sprite;

        InitializeIngredientSlots();

        if (inventoryGridView != null)
            inventoryGridView.OnItemSelected += OnGridItemSelected;

        UpdateUplevelButtonState();
    }

    public void OnLoadTab()
    {
        if (inventoryGridView != null)
            inventoryGridView.SetNotifySelectionEvents(true);
        ShowInventoryGrid();
        if (moveUplevelButton != null)
            moveUplevelButton.gameObject.SetActive(ModelSelected != null && ModelSelected.level < MaxUpgradeLevel);
    }

    private void MoveItemToUpgradeSlot(ItemSchema item)
    {
        if (item == null)
            return;

        AddUpgradeItem(item);
    }

    public void onClickMoveUplevelButton()
    {
        if (ModelSelected == null)
            return;

        MoveItemToUpgradeSlot(ModelSelected);
        UpdateSuccessRate();
        ResetSelectionUI();

        if (moveUplevelButton != null)
            moveUplevelButton.gameObject.SetActive(false);
        UpdateUplevelButtonState();
    }

    public void OnClickRemoveUplevelButton()
    {
        RemoveUpgradeItem();
        for (int i = 0; i < _ingredients.Count; i++)
            RemoveIngredient(i);
        ShowInventoryGrid();
        if (moveUplevelButton != null)
            moveUplevelButton.gameObject.SetActive(true);
    }

    [ContextMenu("Reset Upgrade Slots")]
    public void ResetUpgradeSlots()
    {
        for (int i = 0; i < _ingredients.Count; i++)
            RemoveIngredient(i);

        RemoveUpgradeItem();

        ResetSelectionUI();
        ModelSelectedTemp = null;

        ShowInventoryGrid(false);
        UpdateSuccessRate();
        UpdateUplevelButtonState();
    }

    public void SetTargetItem(ItemSchema item)
    {
        ModelSelected = item;
        bool isSaleItem = item.IsSolded == StatusSold.Sale;
        if (moveUplevelButton != null)
        {
            moveUplevelButton.gameObject.SetActive(item.level < MaxUpgradeLevel);
            moveUplevelButton.interactable = !isSaleItem;
        }
        // UplevelButton display/text is handled in UpdateUplevelButtonState()
        if (ItemLevel != null)
            ItemLevel.text = item.level >= MaxUpgradeLevel ? "Cấp tối đa" : $"Lv. {item.level}";
        if (addMaterialButton != null && addMaterialButton.gameObject.activeSelf)
            addMaterialButton.interactable = !isSaleItem;
        UpdateSuccessRate();
        UpdateUplevelButtonState();
    }

    public void ShowInventoryGrid(bool selectDefault = true)
    {
        StartCoroutine(ShowInventoryGridCoroutine(selectDefault));
    }

    private IEnumerator ShowInventoryGridCoroutine(bool selectDefault)
    {
        yield return StartCoroutine(ResetAll());

        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetPlayerInventoryAsync(GameManagerNetWork.Instance.loginUserModel.UserId),
            result => _inventoryData = result));

        if (_inventoryData == null)
            yield break;

        var excludedSeqs = new HashSet<(int id, int seq)>();

        var equippedSeqs = new HashSet<(int id, int seq)>();
        if (_inventoryData.equippedItems != null)
        {
            foreach (var equip in _inventoryData.equippedItems)
                equippedSeqs.Add((equip.id, equip.seq));
        }

        inventoryGridView.ShowGridForUplevel(_inventoryData, excludedSeqs, equippedSeqs);
        if (_upgradeItem != null)
            inventoryGridView.SetItemActive(_upgradeItem.id, _upgradeItem.seq, false);
        for (int i = 0; i < _ingredients.Count; i++)
        {
            if (_ingredients[i] != null)
                inventoryGridView.SetItemSelectedForUpgrade(_ingredients[i].id, _ingredients[i].seq, true);
        }

        if (selectDefault && _inventoryData.equippedItems != null && _inventoryData.equippedItems.Count > 0)
        {
            int idfrist = _inventoryData.equippedItems.FirstOrDefault().id;
            ModelSelected = _inventoryData.playerItems.FirstOrDefault(x => x.id == idfrist);
            _currentSlotIndex = -1;
        }
        else
        {
            ModelSelected = null;
            _currentSlotIndex = -1;
            inventoryGridView?.ClearHeader();
        }
        if (addMaterialButton != null)
            addMaterialButton.gameObject.SetActive(false);
        if (selectDefault)
            inventoryGridView?.ClearHeader();
        if (moveUplevelButton != null)
            moveUplevelButton.gameObject.SetActive(ModelSelected != null && ModelSelected.level < MaxUpgradeLevel);
        UpdateUplevelButtonState();
    }

    private Image GetSlotImage(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slotItems.Count) return null;
        return _slotItems[slotIndex]?.GetComponent<Image>();
    }

    private void SetSlotImage(int slotIndex, Sprite sprite)
    {
        var slotImg = GetSlotImage(slotIndex);
        if (slotImg != null)
            slotImg.sprite = sprite;
    }

    private Button GetRemoveButton(GameObject slotObj)
    {
        if (slotObj == null)
            return null;

        if (slotObj.TryGetComponent<ItemPrefabView>(out var view) && view.RemoveButton != null)
            return view.RemoveButton;

        var removeTransform = slotObj.transform.Find("Remove");
        if (removeTransform == null)
            return null;

        return removeTransform.GetComponent<Button>();
    }

    private void ConfigureRemoveButton(GameObject slotObj, UnityAction onRemove, bool allowNonSlot = false)
    {
        var btn = GetRemoveButton(slotObj);
        if (btn == null)
            return;

        bool belongs = IsSlotItem(slotObj);
        bool show = onRemove != null && (allowNonSlot || belongs);

        btn.gameObject.SetActive(show);
        btn.onClick.RemoveAllListeners();
        if (show)
            btn.onClick.AddListener(onRemove);
    }

    private void ConfigureIngredientRemoveButton(GameObject slotObj, UnityAction onRemove)
    {
        ConfigureRemoveButton(slotObj, onRemove, false);
    }

    private void RestoreDefaultSlotImage(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slotItems.Count)
            return;

        var slotObj = _slotItems[slotIndex];
        if (slotObj == null)
            return;

        var defaults = _slotDefaults[slotIndex];
        if (slotObj.TryGetComponent<ItemPrefabView>(out var view))
        {
            if (view.ItemIcon != null && defaults.HasIcon)
                view.ItemIcon.sprite = defaults.IconSprite;
            if (view.BackgroundImage != null && defaults.HasBackground)
                view.BackgroundImage.color = defaults.BackgroundColor;
            if (view.QuantityText != null)
                view.QuantityText.gameObject.SetActive(false);
            if (view.LineBanner != null)
                view.LineBanner.SetActive(false);
            if (view.RemoveButton != null)
                view.RemoveButton.gameObject.SetActive(false);
        }
        else
        {
            var slotImg = slotObj.GetComponent<Image>();
            if (slotImg != null && defaults.HasIcon)
                slotImg.sprite = defaults.IconSprite;
        }

        ItemVisualHelper.SetLevelVisual(slotObj.transform, 0, TypeItemGid.Other);
    }

    public void AttachSelectedItem()
    {
        if (ModelSelected == null || ModelSelected.typeGid != (int)TypeItemGid.Gem)
            return;
        if (_ingredients.Count == 0)
            return;

        // If the gem is stacked, take the first available sequence
        if (ModelSelected.seqList == null || ModelSelected.seqList.Count == 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BallUpgradeController] AttachSelectedItem missing seqList for gem id={ModelSelected.id}. Refreshing group data.");
#endif
            var group = ItemVisualHelper.GetGroup(ModelSelected.id);
            ModelSelected.seqList = group != null ? group.seqList : null;
        }
        if (ModelSelected.seqList == null || ModelSelected.seqList.Count == 0)
            return;

        ModelSelected.seq = ModelSelected.seqList[0];

        bool hasMoreGem = ModelSelected.seqList.Count > 1;
        ItemSchema selectedItem = ModelSelected;

        int slotIndex = -1;
        for (int i = 0; i < _ingredients.Count; i++)
        {
            if (_ingredients[i] == null)
            {
                slotIndex = i;
                break;
            }
        }

        if (slotIndex == -1)
        {
            // No empty slot found, store the last slot item and free it
            slotIndex = _ingredients.Count - 1;
            ItemSchema oldItem = _ingredients[slotIndex];
            if (oldItem != null)
            {
                RemoveIngredient(slotIndex); // Reactivates the old material in inventory
            }
        }

        // Add the newly selected material to the determined slot
        AddIngredient(selectedItem, slotIndex);

        if (hasMoreGem)
            ItemVisualHelper.UpdateGroupedItemQuantity(selectedItem.id);

        UpdateSuccessRate();

        if (inventoryGridView != null && inventoryGridView.TabInventoryListPanel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryGridView.TabInventoryListPanel.GetComponent<RectTransform>());

        ModelSelected = null;
        _currentSlotIndex = -1;
        if (addMaterialButton != null)
            addMaterialButton.gameObject.SetActive(false);
        inventoryGridView?.ClearSelection();
    }
 

    public void DetachSlotItem(int slotIndex)
    {
        RemoveIngredient(slotIndex);
        if (inventoryGridView != null && inventoryGridView.TabInventoryListPanel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryGridView.TabInventoryListPanel.GetComponent<RectTransform>());
    }

    public void AddUpgradeItem(ItemSchema item)
    {
        if (upgradeSlot == null || item == null)
            return;

        if (_upgradeItem != null)
            RemoveUpgradeItem();

        ModelSelectedTemp = item;
        _upgradeItem = item;

        // UplevelButton display/text is handled in UpdateUplevelButtonState()
        if (ItemLevel != null)
            ItemLevel.text = item.level >= MaxUpgradeLevel ? "Cấp tối đa" : $"Lv. {item.level}";

        GameObject slotObj = Instantiate(inventoryGridView.ItemPrefab, upgradeSlot);
        RectTransform rect = slotObj.GetComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        _upgradeSlotItem = slotObj;
        ItemVisualHelper.SetLevelVisual(slotObj.transform, item.level, (TypeItemGid)item.typeGid);
        ItemVisualHelper.ApplyRarityBackground(slotObj.transform, item.rarityGid);
        var itemView = slotObj.GetComponent<ItemPrefabView>();
        if (itemView != null)
        {
            if (itemView.LineBanner != null)
                itemView.LineBanner.SetActive(false);
        }
        ConfigureRemoveButton(slotObj, OnClickRemoveUplevelButton, true);
        Image childImg = itemView != null ? itemView.ItemIcon : null;
        if (childImg != null)
        {
            StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{item.id}.png", sprite =>
            {
                childImg.sprite = sprite != null ? sprite : ItemVisualHelper.LoadSpriteByID(item.id);
            }));
        }

        if (item.level >= MaxUpgradeLevel)
        {
            if (_level10VfxInstance == null && Level10VFXPrefab != null)
                _level10VfxInstance = Instantiate(Level10VFXPrefab, _upgradeSlotItem.transform);
        }
        else if (_level10VfxInstance != null)
        {
            Destroy(_level10VfxInstance);
            _level10VfxInstance = null;
        }

        inventoryGridView.SetItemActive(item.id, item.seq, false);
        if (removeUplevelButton != null)
            removeUplevelButton.gameObject.SetActive(true);
        UpdateSuccessRate();
        UpdateUplevelButtonState();
        ResetSelectionUI();
    }

    public void RemoveUpgradeItem()
    {
        if (_upgradeItem == null)
            return;

        var item = _upgradeItem;
        _upgradeItem = null;

        if (_upgradeSlotItem != null && _upgradeSlotItem != upgradeSlot?.gameObject)
            Destroy(_upgradeSlotItem);

        _upgradeSlotItem = upgradeSlot != null ? upgradeSlot.gameObject : null;
        _level10VfxInstance = null;

        var img = upgradeSlot?.GetComponent<Image>();
        if (img != null)
            img.sprite = _upgradeDefaultSprite;
        ItemVisualHelper.SetLevelVisual(upgradeSlot, 0, TypeItemGid.Other);
        inventoryGridView.SetItemActive(item.id, item.seq, true);
        if (removeUplevelButton != null)
            removeUplevelButton.gameObject.SetActive(false);
        UpdateSuccessRate();
        UpdateUplevelButtonState();
    }

    public void AddIngredient(ItemSchema item, int slotIndex)
    {
        if (item == null || item.typeGid != (int)TypeItemGid.Gem) return;
        if (slotIndex < 0 || slotIndex >= _slotItems.Count) return;
        if (_ingredients[slotIndex] != null) return;

        // Clone the item so each slot holds an independent copy
        ItemSchema copy = new ItemSchema
        {
            id = item.id,
            level = item.level,
            typeGid = item.typeGid,
            seq = item.seq,
            rarityGid = item.rarityGid,
            // Keep reference to the original seqList so returning the gem restores the stack
            seqList = item.seqList
        };

        _ingredients[slotIndex] = copy;

        var slotObj = _slotItems[slotIndex];
        if (slotObj == null)
            return;

        UpdateIngredientView(copy, slotObj);
        ConfigureIngredientRemoveButton(slotObj, () => RemoveIngredient(slotIndex));

        // Mark this particular sequence as selected in the inventory grid
        inventoryGridView.SetItemSelectedForUpgrade(copy.id, copy.seq, true);
        UpdateUplevelButtonState();
        ResetSelectionUI();
    }

    public void RemoveIngredient(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _ingredients.Count) return;

        ItemSchema item = _ingredients[slotIndex];
        if (item == null) return;

        _ingredients[slotIndex] = null;

        RestoreDefaultSlotImage(slotIndex);

        if (_slotItems[slotIndex] != null)
            ConfigureIngredientRemoveButton(_slotItems[slotIndex], null);

        // Reactivate this sequence in the inventory grid and restore the stack count
        inventoryGridView.SetItemSelectedForUpgrade(item.id, item.seq, false);
        if (item.seqList != null && !item.seqList.Contains(item.seq))
            item.seqList.Insert(0, item.seq);

        ResetSelectionUI();
        UpdateSuccessRate();
        UpdateUplevelButtonState();
    }

    private void ResetSelectionUI()
    {
        ModelSelected = null;
        _currentSlotIndex = -1;

        if (addMaterialButton != null)
        {
            addMaterialButton.gameObject.SetActive(false);
            addMaterialButton.interactable = true;
        }
        if (moveUplevelButton != null)
            moveUplevelButton.interactable = true;
        inventoryGridView?.ClearSelection();
    }

    private void OnGridItemSelected(ItemSchema item)
    {
        ModelSelected = item;
        _currentSlotIndex = -1;
        bool isSaleItem = item.IsSolded == StatusSold.Sale;
        bool isGem = item.typeGid == (int)TypeItemGid.Gem;
        if (isGem)
        {
            var group = ItemVisualHelper.GetGroup(item.id);
            if (group != null)
            {
                item.seqList = group.seqList;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.LogWarning($"[BallUpgradeController] OnGridItemSelected gem id={item.id} missing group data.");
            }
#endif
        }
        if (addMaterialButton != null)
            addMaterialButton.gameObject.SetActive(false);
        if (moveUplevelButton != null)
            moveUplevelButton.gameObject.SetActive(false);
        inventoryGridView.ShowSelectedItem(item);
        if (isSaleItem)
            return;

        if (isGem)
        {
            AttachSelectedItem();
            return;
        }

        MoveItemToUpgradeSlot(item);
        UpdateSuccessRate();
        UpdateUplevelButtonState();
    }

    private void OnSlotClicked(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _ingredients.Count)
            return;

        _currentSlotIndex = slotIndex;
        ItemSchema item = _ingredients[slotIndex];
        ModelSelected = item;

        if (item != null && item.typeGid == (int)TypeItemGid.Gem)
        {
            if (addMaterialButton != null)
                addMaterialButton.gameObject.SetActive(false);
            inventoryGridView.ShowSelectedItem(item);
        }
        else
        {
            if (addMaterialButton != null)
                addMaterialButton.gameObject.SetActive(false);
            if (item != null)
                inventoryGridView.ShowSelectedItem(item);
            else
                inventoryGridView.ClearHeader();
        }
    }

    private int CalculateSuccessRate()
    {
        if (ModelSelectedTemp == null) return 0;

        float points = _ingredients.Where(g => g != null).Sum(g => g.level switch
        {
            1 => 100f,
            2 => 200f,
            3 => 400f,
            _ => 0f
        });

        // 20% decay per current level (lvl1 -> 100%, lvl2 -> 80%, lvl3 -> 64%, …)
        float decay = Mathf.Pow(0.8f, Mathf.Max(0, ModelSelectedTemp.level - 1));
        float rate = points * decay;
        int resultRate = Mathf.RoundToInt(Mathf.Clamp(rate, 0f, 100f));
        return resultRate;
    }

    private void UpdateSuccessRate()
    {
        int rate = CalculateSuccessRate();
        if (successRateSlider != null)
        {
            successRateSlider.value = rate;
            if (successRateFill != null)
            {
                float t = rate / successRateSlider.maxValue;
                successRateFill.color = Color.Lerp(Color.red, Color.green, t);
            }
        }
        if (successRateText != null)
            successRateText.text = $"{rate}%";
    }

    private IEnumerator ScaleCoroutine()
    {
        if (UplevelButton == null)
            yield break;

        Transform target = UplevelButton.transform;
        Vector3 baseScale = _uplevelButtonOriginalScale;
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float scale = 1f + 0.1f * Mathf.Sin(t * Mathf.PI * 2f);
            scale = Mathf.Clamp(scale, 0.9f, 1.1f);
            target.localScale = baseScale * scale;
            yield return null;
        }
    }

    private void StartBlink()
    {
        if (_uplevelButtonCanvasGroup != null)
            _uplevelButtonCanvasGroup.alpha = 1f;
        if (_scaleCoroutine == null)
            _scaleCoroutine = StartCoroutine(ScaleCoroutine());
    }

    private void StopBlink()
    {
        if (_scaleCoroutine != null)
        {
            StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = null;
            if (UplevelButton != null)
                UplevelButton.transform.localScale = _uplevelButtonOriginalScale;
        }
    }

    private void UpdateUplevelButtonState()
    {
        if (UplevelButton == null)
            return;

        bool atMax = _upgradeItem != null && _upgradeItem.level >= MaxUpgradeLevel;
        UplevelButton.gameObject.SetActive(true);
        if (atMax)
        {
            StopBlink();
            UplevelButton.interactable = false;
            if (_uplevelButtonCanvasGroup != null)
                _uplevelButtonCanvasGroup.alpha = 0.5f;
            if (_uplevelButtonLabel != null && LocalizationManager.Instance != null)
                _uplevelButtonLabel.text = LocalizationManager.Instance.GetText(UplevelButtonMaxTextKey);
            return;
        }

        bool enable = ModelSelectedTemp != null && _ingredients.Any(i => i != null);

        UplevelButton.interactable = enable;

        if (_uplevelButtonCanvasGroup != null)
            _uplevelButtonCanvasGroup.alpha = enable ? 1f : 0.5f;

        if (_uplevelButtonLabel != null && LocalizationManager.Instance != null)
            _uplevelButtonLabel.text = LocalizationManager.Instance.GetText(UplevelButtonTextKey);

        if (enable)
            StartBlink();
        else
            StopBlink();
    }

    public void OnClickLevelUpItem()
    {
        StartCoroutine(LevelUpAndRefreshCoroutine());
    }

    private IEnumerator LevelUpAndRefreshCoroutine()
    {
        yield return StartCoroutine(LevelUpSequenceCoroutine());
        yield return StartCoroutine(ReloadAndReattachCoroutine());
    }

    private IEnumerator LevelUpSequenceCoroutine()
    {
        if (UplevelButton != null)
        {
            Transform target = UplevelButton.transform;
            Vector3 originalScale = _uplevelButtonOriginalScale;
            target.localScale = originalScale;
            Sequence seq = DOTween.Sequence();
            seq.Append(target.DOScale(originalScale * 1.2f, 0.2f));
            seq.Join(target.DOShakePosition(0.2f, 0.1f, 10, 90, false, true));
            yield return seq.WaitForCompletion();
            yield return new WaitForSeconds(0.5f);
        }

        Transform effectParent = MenuController.Instance != null && MenuController.Instance.EffectPanel != null
            ? MenuController.Instance.EffectPanel.transform
            : transform;

        if (explosionVFXPrefab != null)
            Instantiate(explosionVFXPrefab, effectParent);
        SoundManager.Instance?.PlayUpgradeExplosion();

        yield return StartCoroutine(LevelUpItemCoroutine());

        if (explosionVFXPrefab != null)
            Instantiate(explosionVFXPrefab, effectParent);
        SoundManager.Instance?.PlayUpgradeExplosion();

        if (UplevelButton != null)
            UplevelButton.transform.localScale = _uplevelButtonOriginalScale;
    }

    private IEnumerator LevelUpItemCoroutine()
    {
        if (ModelSelectedTemp == null)
            yield break;
        int idItemTemp = ModelSelectedTemp.id;
        int seqItemTemp = ModelSelectedTemp.seq;
        float rate = CalculateSuccessRate();
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;
        var seenSeqs = new HashSet<int>();
        var materials = new List<UpgradeMaterial>();
        foreach (var ing in _ingredients)
        {
            if (ing == null || !seenSeqs.Add(ing.seq))
                continue;
            materials.Add(new UpgradeMaterial { id = ing.id, seq = ing.seq });
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"LevelUpItem - materials: {string.Join(", ", materials.Select(m => $"{m.id}:{m.seq}"))}");
#endif
        PlayerItemSchema data = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.LevelUpItem(playerId, ModelSelectedTemp.id, ModelSelectedTemp.seq, rate, materials),
            result => data = result));

        // remove all used ingredients regardless of result
        for (int i = 0; i < _ingredients.Count; i++)
            RemoveIngredient(i);

        Transform effectParent = MenuController.Instance != null && MenuController.Instance.EffectPanel != null
            ? MenuController.Instance.EffectPanel.transform
            : transform;

        if (data == null)
        {
            if (failureVFXPrefab != null)
                Instantiate(failureVFXPrefab, effectParent);
            SoundManager.Instance?.PlayUpgradeFailure();
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_uplevel_false"), false);
            ItemVisualHelper.SetLevelVisual(upgradeSlot, _upgradeItem.level, (TypeItemGid)_upgradeItem.typeGid);
            UpdateSuccessRate();
            yield break;
        }

        if (successVFXPrefab != null)
            Instantiate(successVFXPrefab, effectParent);

        SoundManager.Instance?.PlayUpgradeSuccess();

        NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_uplevel_true"), true);

        // update upgrade item to new level and refresh UI
        ModelSelectedTemp.level = data.level;
        _upgradeItem = ModelSelectedTemp;
        ItemVisualHelper.SetLevelVisual(upgradeSlot, _upgradeItem.level, (TypeItemGid)_upgradeItem.typeGid);
        if (ItemLevel != null)
            ItemLevel.text = _upgradeItem.level >= MaxUpgradeLevel ? "Cấp tối đa" : $"Lv. {_upgradeItem.level}";

        UpdateSuccessRate();

        if (_upgradeItem.level >= MaxUpgradeLevel)
        {
            if (_level10VfxInstance == null && Level10VFXPrefab != null)
                _level10VfxInstance = Instantiate(Level10VFXPrefab, upgradeSlot);
        }
        else if (_level10VfxInstance != null)
        {
            Destroy(_level10VfxInstance);
            _level10VfxInstance = null;
        }
    }

    private IEnumerator ReloadAndReattachCoroutine()
    {
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetPlayerInventoryAsync(GameManagerNetWork.Instance.loginUserModel.UserId),
            result => _inventoryData = result));

        if (_inventoryData == null)
            yield break;

        inventoryGridView.ShowGridForUplevel(
            _inventoryData,
            Enumerable.Empty<(int, int)>(),
            Enumerable.Empty<(int, int)>());
        if (addMaterialButton != null)
            addMaterialButton.gameObject.SetActive(false);

        if (_upgradeItem != null)
        {
            ModelSelected = _upgradeItem;
            SetTargetItem(_upgradeItem);
            onClickMoveUplevelButton();
        }
    }

    private void UpdateIngredientView(ItemSchema item, GameObject slotObj)
    {
        if (slotObj == null)
            return;

        ItemVisualHelper.SetLevelVisual(slotObj.transform, item.level, (TypeItemGid)item.typeGid);
        ItemVisualHelper.ApplyRarityBackground(slotObj.transform, item.rarityGid);

        var itemView = slotObj.GetComponent<ItemPrefabView>();
        if (itemView != null)
        {
            if (itemView.LineBanner != null)
                itemView.LineBanner.SetActive(false);
            if (itemView.QuantityText != null)
                itemView.QuantityText.gameObject.SetActive(false);
            if (itemView.RemoveButton != null)
                itemView.RemoveButton.gameObject.SetActive(true);
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BallUpgradeController] UpdateIngredientView missing ItemPrefabView for slot '{slotObj.name}'.");
#endif
        }
        Image icon = itemView != null ? itemView.ItemIcon : null;
        if (icon != null)
        {
            StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{item.id}.png", sprite =>
            {
                icon.sprite = sprite != null ? sprite : ItemVisualHelper.LoadSpriteByID(item.id);
            }));
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BallUpgradeController] UpdateIngredientView missing icon image for slot '{slotObj.name}'.");
#endif
        }
    }

    private void InitializeIngredientSlots()
    {
        _slotItems.Clear();
        _ingredients.Clear();
        _slotDefaults.Clear();

        if (ingredientSlotRoot == null || inventoryGridView == null || inventoryGridView.ItemPrefab == null)
            return;

        if (ingredientSlotRoot.childCount > 0)
        {
            int childCount = ingredientSlotRoot.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var child = ingredientSlotRoot.GetChild(i).gameObject;
                if (child.TryGetComponent<ItemPrefabView>(out _))
                {
                    RegisterSlot(child, i);
                    continue;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[BallUpgradeController] Ingredient slot child at index {i} has no ItemPrefabView. Instantiating ItemPrefab instead.");
#endif
                var slotObj = Instantiate(inventoryGridView.ItemPrefab, ingredientSlotRoot);
                var rect = slotObj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.localRotation = Quaternion.identity;
                    rect.localScale = Vector3.one;
                }
                RegisterSlot(slotObj, i);
                Destroy(child);
            }
        }
        else
        {
            int count = Mathf.Max(1, ingredientSlotCount);
            for (int i = 0; i < count; i++)
            {
                var slotObj = Instantiate(inventoryGridView.ItemPrefab, ingredientSlotRoot);
                var rect = slotObj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.localRotation = Quaternion.identity;
                    rect.localScale = Vector3.one;
                }
                RegisterSlot(slotObj, i);
            }
        }
    }

    private void RegisterSlot(GameObject slotObj, int index)
    {
        if (slotObj == null)
            return;

        _slotItems.Add(slotObj);
        _ingredients.Add(null);

        var defaults = new SlotDefaults();
        if (slotObj.TryGetComponent<ItemPrefabView>(out var view))
        {
            if (view.ItemIcon != null)
            {
                defaults.IconSprite = view.ItemIcon.sprite;
                defaults.HasIcon = true;
            }
            if (view.BackgroundImage != null)
            {
                defaults.BackgroundColor = view.BackgroundImage.color;
                defaults.HasBackground = true;
            }
            if (view.LineBanner != null)
                view.LineBanner.SetActive(false);
            if (view.QuantityText != null)
                view.QuantityText.gameObject.SetActive(false);
            if (view.RemoveButton != null)
                view.RemoveButton.gameObject.SetActive(false);
        }
        else
        {
            var img = slotObj.GetComponent<Image>();
            if (img != null)
            {
                defaults.IconSprite = img.sprite;
                defaults.HasIcon = true;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.LogWarning("[BallUpgradeController] Ingredient slot has no ItemPrefabView or Image. UI data may not display.");
            }
#endif
        }
        _slotDefaults.Add(defaults);

        int slotIndex = index;
        var btn = slotObj.GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(() => OnSlotClicked(slotIndex));

        RestoreDefaultSlotImage(slotIndex);
        ConfigureIngredientRemoveButton(slotObj, null);
    }

    private struct SlotDefaults
    {
        public Sprite IconSprite;
        public Color BackgroundColor;
        public bool HasIcon;
        public bool HasBackground;
    }

    private IEnumerator ResetAll()
    {
        for (int i = 0; i < _ingredients.Count; i++)
            RemoveIngredient(i);

        RemoveUpgradeItem();

        ResetSelectionUI();
        ModelSelectedTemp = null;

        if (moveUplevelButton != null)
            moveUplevelButton.gameObject.SetActive(ModelSelected != null && ModelSelected.level < MaxUpgradeLevel);

        if (inventoryGridView != null && inventoryGridView.TabInventoryListPanel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryGridView.TabInventoryListPanel.GetComponent<RectTransform>());
        UpdateUplevelButtonState();
        yield break;
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _ingredients.Count; i++)
            RemoveIngredient(i);
        RemoveUpgradeItem();
        ModelSelectedTemp = null;
        if (inventoryGridView != null && inventoryGridView.TabInventoryListPanel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryGridView.TabInventoryListPanel.GetComponent<RectTransform>());
    }
}
