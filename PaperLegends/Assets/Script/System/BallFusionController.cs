using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class BallFusionController : MonoBehaviour
{
    public static BallFusionController Instance;

    [Header("Inventory Grid")]
    public InventoryGridView inventoryGridView;

    [Header("Fusion Slots")]
    public Transform fusionSlotA;
    public Transform fusionSlotB;
    public Transform fusionCatalystSlot;
    public Slider fusionSuccessRateSlider;
    [Header("Fusion Button")]
    public Button FusionButton;
    public Button AddMaterialFusionButton;
    public Button removeMaterialFusionButton;

    private ItemSchema _fusionItemA;
    private ItemSchema _fusionItemB;
    private CatalystItem _fusionCatalyst;
    private CanvasGroup _fusionButtonCanvasGroup;
    private Coroutine _fusionScaleCoroutine;
    private Vector3 _fusionButtonOriginalScale;
    private Transform[] _fusionSlots;
    private readonly ItemSchema[] _fusionItems = new ItemSchema[2];
    private Sprite[] _fusionDefaultSprites;
    private int _currentFusionSlot = -1;

    [Header("Fusion Info")]
    public TextMeshProUGUI fusionInteractionDescription;
    public TextMeshProUGUI fusionSuccessRateLabel;
    public TextMeshProUGUI fusionMutationRateLabel;

    [Header("Effects")]
    public GameObject successVFXPrefab;
    public GameObject failureVFXPrefab;
    public GameObject explosionVFXPrefab;

    public ItemSchema ModelSelected;

    public class ElementInteractionInfo
    {
        public string name;
        public float successRate;
        public ElementalType resultElement;
        public float mutationRate;

        public ElementInteractionInfo(string name, float successRate, ElementalType resultElement, float mutationRate)
        {
            this.name = name;
            this.successRate = successRate;
            this.resultElement = resultElement;
            this.mutationRate = mutationRate;
        }
    }

    private static readonly Dictionary<(ElementalType, ElementalType), ElementInteractionInfo> ElementInteractionTable = new()
    {
        { (ElementalType.Fire, ElementalType.Water), new ElementInteractionInfo("Steam Burst", 40f, ElementalType.Steam, 10f) },
        { (ElementalType.Water, ElementalType.Fire), new ElementInteractionInfo("Steam Burst", 40f, ElementalType.Steam, 10f) },
        { (ElementalType.Fire, ElementalType.Earth), new ElementInteractionInfo("Smoke Cloud", 35f, ElementalType.Smoke, 15f) },
        { (ElementalType.Earth, ElementalType.Fire), new ElementInteractionInfo("Smoke Cloud", 35f, ElementalType.Smoke, 15f) },
        { (ElementalType.Water, ElementalType.Earth), new ElementInteractionInfo("Bloom", 50f, ElementalType.Life, 5f) },
        { (ElementalType.Earth, ElementalType.Water), new ElementInteractionInfo("Bloom", 50f, ElementalType.Life, 5f) }
    };

    static BallFusionController()
    {
        var elements = new HashSet<ElementalType>();
        foreach (var kvp in ElementInteractionTable)
        {
            elements.Add(kvp.Key.Item1);
            elements.Add(kvp.Key.Item2);
            elements.Add(kvp.Value.resultElement);
        }
        ItemElementDisplay.VerifySpriteMappings(elements);
    }

    private void Awake()
    {
        Instance = this;

        if (fusionSuccessRateSlider != null)
        {
            fusionSuccessRateSlider.maxValue = 100f;
            fusionSuccessRateSlider.interactable = false;
        }

        if (FusionButton != null)
        {
            _fusionButtonCanvasGroup = FusionButton.GetComponent<CanvasGroup>();
            if (_fusionButtonCanvasGroup == null)
                _fusionButtonCanvasGroup = FusionButton.gameObject.AddComponent<CanvasGroup>();
            _fusionButtonOriginalScale = FusionButton.transform.localScale;
        }
        if (AddMaterialFusionButton != null)
        {
            AddMaterialFusionButton.gameObject.SetActive(false);
            AddMaterialFusionButton.onClick.AddListener(MoveSelectedItemToFusion);
        }

        if (removeMaterialFusionButton != null)
        {
            removeMaterialFusionButton.gameObject.SetActive(false);
            removeMaterialFusionButton.onClick.AddListener(() => RemoveFusionItem(_currentFusionSlot));
        }

        _fusionSlots = new[] { fusionSlotA, fusionSlotB };
        _fusionDefaultSprites = new Sprite[_fusionSlots.Length];
        for (int i = 0; i < _fusionSlots.Length; i++)
        {
            int index = i;
            var img = _fusionSlots[i]?.GetComponent<Image>();
            if (img != null)
                _fusionDefaultSprites[i] = img.sprite;

            var btn = _fusionSlots[i]?.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() =>
                {
                    _currentFusionSlot = index;
                    if (removeMaterialFusionButton != null)
                        removeMaterialFusionButton.gameObject.SetActive(_fusionItems[index] != null);
                    if (AddMaterialFusionButton != null)
                        AddMaterialFusionButton.gameObject.SetActive(false);
                    inventoryGridView?.ClearSelection();

                    var item = _fusionItems[index];
                    ModelSelected = item;
                    inventoryGridView?.ShowSelectedItem(item);
                });
        }

        if (inventoryGridView != null)
            inventoryGridView.OnItemSelected += OnGridItemSelected;

        UpdateFusionUI();
    }

    private void OnGridItemSelected(ItemSchema item)
    {
        ModelSelected = item;
        if (AddMaterialFusionButton != null)
            AddMaterialFusionButton.gameObject.SetActive(true);
        if (removeMaterialFusionButton != null)
            removeMaterialFusionButton.gameObject.SetActive(false);
        inventoryGridView?.ShowSelectedItem(item);
    }

    private void ResetSelectionUI()
    {
        ModelSelected = null;
        _currentFusionSlot = -1;
        if (AddMaterialFusionButton != null)
            AddMaterialFusionButton.gameObject.SetActive(false);
        if (removeMaterialFusionButton != null)
            removeMaterialFusionButton.gameObject.SetActive(false);
        inventoryGridView?.ClearSelection();
        inventoryGridView?.ClearHeader();
    }

    public void MoveSelectedItemToFusion()
    {
        if (ModelSelected == null)
            return;

        int slot;
        if (_fusionItems[0] == null)
            slot = 0;
        else if (_fusionItems[1] == null)
            slot = 1;
        else
        {
            slot = 1;
            var old = _fusionItems[1];
            if (old != null)
                inventoryGridView.SetItemActive(old.id, old.seq, true);
        }

        _fusionItems[slot] = ModelSelected;
        _fusionItemA = _fusionItems[0];
        _fusionItemB = _fusionItems[1];

        var img = _fusionSlots?[slot]?.GetComponent<Image>();
        if (img != null)
        {
            int id = ModelSelected.id;
            StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{id}.png", sprite =>
            {
                if (img != null)
                    img.sprite = sprite != null ? sprite : ItemVisualHelper.LoadSpriteByID(id);
            }));
        }

        inventoryGridView.SetItemActive(ModelSelected.id, ModelSelected.seq, false);

        ResetSelectionUI();
        UpdateFusionUI();
    }

    public void RemoveFusionItem(int slot)
    {
        if (slot < 0 || slot >= _fusionItems.Length)
            return;

        var item = _fusionItems[slot];
        if (item == null)
            return;

        inventoryGridView.SetItemActive(item.id, item.seq, true);

        _fusionItems[slot] = null;
        _fusionItemA = _fusionItems[0];
        _fusionItemB = _fusionItems[1];

        var img = _fusionSlots?[slot]?.GetComponent<Image>();
        if (img != null && _fusionDefaultSprites != null && slot < _fusionDefaultSprites.Length)
            img.sprite = _fusionDefaultSprites[slot];

        ResetSelectionUI();
        UpdateFusionUI();
    }

    public void SetFusionItemA(ItemSchema item)
    {
        _fusionItemA = item;
        _fusionItems[0] = item;
        if (_fusionSlots != null && _fusionSlots.Length > 0)
        {
            var img = _fusionSlots[0]?.GetComponent<Image>();
            if (img != null)
            {
                if (item != null)
                {
                    int id = item.id;
                    StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{id}.png", sprite =>
                    {
                        if (img != null)
                            img.sprite = sprite != null ? sprite : ItemVisualHelper.LoadSpriteByID(id);
                    }));
                }
                else if (_fusionDefaultSprites != null && _fusionDefaultSprites.Length > 0)
                {
                    img.sprite = _fusionDefaultSprites[0];
                }
            }
        }
        UpdateFusionUI();
    }

    public void SetFusionItemB(ItemSchema item)
    {
        _fusionItemB = item;
        _fusionItems[1] = item;
        if (_fusionSlots != null && _fusionSlots.Length > 1)
        {
            var img = _fusionSlots[1]?.GetComponent<Image>();
            if (img != null)
            {
                if (item != null)
                {
                    int id = item.id;
                    StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{id}.png", sprite =>
                    {
                        if (img != null)
                            img.sprite = sprite != null ? sprite : ItemVisualHelper.LoadSpriteByID(id);
                    }));
                }
                else if (_fusionDefaultSprites != null && _fusionDefaultSprites.Length > 1)
                {
                    img.sprite = _fusionDefaultSprites[1];
                }
            }
        }
        UpdateFusionUI();
    }

    public void SetFusionCatalyst(CatalystItem item)
    {
        _fusionCatalyst = item;
        UpdateFusionUI();
    }

    public void OnFusionButtonClick()
    {
        if (_fusionItemA == null || _fusionItemB == null || _fusionCatalyst == null)
            return;

        StartCoroutine(FusionSequenceCoroutine());
    }

    private IEnumerator FusionSequenceCoroutine()
    {
        float rate = CalculateSuccessRate(_fusionItemA, _fusionItemB, _fusionCatalyst);
        int playerId = GameManagerNetWork.Instance.loginUserModel.UserId;

        if (FusionButton != null)
        {
            StopFusionBlink();
            Transform target = FusionButton.transform;
            Vector3 originalScale = _fusionButtonOriginalScale;
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
        SoundManager.Instance?.PlayFusionExplosion();

        yield return StartCoroutine(FusionCoroutine(playerId, rate));

        if (FusionButton != null)
            FusionButton.transform.localScale = _fusionButtonOriginalScale;

        StopFusionBlink();
    }

    private IEnumerator FusionCoroutine(int playerId, float rate)
    {
        PlayerInventorySchema data = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.FusionItems(playerId, _fusionItemA.id, _fusionItemB.id, _fusionCatalyst.id, rate),
            result => data = result));

        Transform effectParent = MenuController.Instance != null && MenuController.Instance.EffectPanel != null
            ? MenuController.Instance.EffectPanel.transform
            : transform;

        if (data == null)
        {
            if (failureVFXPrefab != null)
                Instantiate(failureVFXPrefab, effectParent);
            SoundManager.Instance?.PlayFusionFailure();
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_fusion_false"), false);
            yield break;
        }

        if (successVFXPrefab != null)
            Instantiate(successVFXPrefab, effectParent);
        SoundManager.Instance?.PlayFusionSuccess();
        NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_fusion_true"), true);

        var interaction = GetFusionInteraction(_fusionItemA, _fusionItemB);
        if (interaction != null)
        {
            var resultItem = data.playerItems?.FirstOrDefault(i => i.seq == _fusionItemA.seq);
            if (resultItem != null)
            {
                resultItem.ElementType = interaction.resultElement;
                
            }
        }

        InventoryController.Instance?.ShowInventoryList();
        BallUpgradeController.Instance?.ShowInventoryGrid();
        SetFusionItemA(null);
        SetFusionItemB(null);
        SetFusionCatalyst(null);
    }

    private IEnumerator FusionScaleCoroutine()
    {
        if (FusionButton == null)
            yield break;

        Transform target = FusionButton.transform;
        Vector3 baseScale = _fusionButtonOriginalScale;
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

    private void StartFusionBlink()
    {
        if (_fusionScaleCoroutine == null)
            _fusionScaleCoroutine = StartCoroutine(FusionScaleCoroutine());
    }

    private void StopFusionBlink()
    {
        if (_fusionScaleCoroutine != null)
        {
            StopCoroutine(_fusionScaleCoroutine);
            _fusionScaleCoroutine = null;
            if (FusionButton != null)
                FusionButton.transform.localScale = _fusionButtonOriginalScale;
        }
    }

    private float CalculateSuccessRate(ItemSchema itemA, ItemSchema itemB, CatalystItem catalyst)
    {
        var info = GetFusionInteraction(itemA, itemB);
        if (info == null)
            return 0f;

        float rate = info.successRate;
        if (catalyst != null)
            rate += catalyst.bonusRate;
        return Mathf.Clamp(rate, 0f, 100f);
    }

    public ElementInteractionInfo GetFusionInteraction(ItemSchema itemA, ItemSchema itemB)
    {
        if (itemA == null || itemB == null)
            return null;
        return GetFusionInteraction(itemA.ElementType, itemB.ElementType);
    }

    public ElementInteractionInfo GetFusionInteraction(ElementalType elementA, ElementalType elementB)
    {
        ElementInteractionTable.TryGetValue((elementA, elementB), out var info);
        return info;
    }

    private void UpdateFusionUI()
    {
        var info = GetFusionInteraction(_fusionItemA, _fusionItemB);
        bool complete = _fusionItemA != null && _fusionItemB != null && _fusionCatalyst != null && info != null;

        float rate = CalculateSuccessRate(_fusionItemA, _fusionItemB, _fusionCatalyst);
        if (fusionSuccessRateSlider != null)
        {
            fusionSuccessRateSlider.value = complete ? rate : 0f;
        }

        if (fusionInteractionDescription != null)
        {
            fusionInteractionDescription.text = complete ? info.name : string.Empty;
            fusionInteractionDescription.gameObject.SetActive(complete);
        }

        if (fusionSuccessRateLabel != null)
        {
            fusionSuccessRateLabel.text = complete ? $"{rate:F0}%" : string.Empty;
            fusionSuccessRateLabel.gameObject.SetActive(complete);
        }

        if (fusionMutationRateLabel != null)
        {
            fusionMutationRateLabel.text = complete ? $"{info.mutationRate:F0}%" : string.Empty;
            fusionMutationRateLabel.gameObject.SetActive(complete);
        }

        if (FusionButton != null)
        {
            FusionButton.interactable = complete;
            if (_fusionButtonCanvasGroup != null)
                _fusionButtonCanvasGroup.alpha = complete ? 1f : 0.5f;
            if (complete)
                StartFusionBlink();
            else
                StopFusionBlink();
        }
    }
}

