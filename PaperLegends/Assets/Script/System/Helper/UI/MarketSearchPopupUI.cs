using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarketSearchPopupUI : MonoBehaviour
{
    [Serializable]
    private class RarityButtonBinding
    {
        public Button button;
        public int rarityGid;
        public GameObject selectedState;
    }

    [Header("Filters")]
    [SerializeField] private TMP_InputField itemNameInput;
    [SerializeField] private TMP_InputField levelFromInput;
    [SerializeField] private TMP_InputField levelToInput;

    [Header("Tabs")]
    [SerializeField] private GameObject filterTab;
    [SerializeField] private GameObject rarityTab;
    [SerializeField] private Button openRarityTabButton;
    [SerializeField] private Button backToFilterTabButton;

    [Header("Rarity")]
    [SerializeField] private List<RarityButtonBinding> rarityButtons = new();

    [Header("Actions")]
    [SerializeField] private Button searchButton;
    [SerializeField] private Button clearSearchButton;
    [SerializeField] private Button closeButton;

    private readonly HashSet<int> selectedRarityGids = new();
    private Action<MarketSearchPopupValues> onSearch;
    private Action onClose;

    public void Initialize(MarketSearchPopupValues defaultValues, Action<MarketSearchPopupValues> searchCallback, Action closeCallback)
    {
        TryBindOptionalReferences();
        onSearch = searchCallback;
        onClose = closeCallback;
        ApplyValues(defaultValues != null ? defaultValues.Clone() : new MarketSearchPopupValues());
        RegisterListeners();
    }

    private void ApplyValues(MarketSearchPopupValues values)
    {
        if (itemNameInput != null)
            itemNameInput.SetTextWithoutNotify(values.ItemName ?? string.Empty);

        if (levelFromInput != null)
            levelFromInput.SetTextWithoutNotify(values.LevelFrom.HasValue ? values.LevelFrom.Value.ToString() : string.Empty);

        if (levelToInput != null)
            levelToInput.SetTextWithoutNotify(values.LevelTo.HasValue ? values.LevelTo.Value.ToString() : string.Empty);

        selectedRarityGids.Clear();
        if (values.RarityGids != null)
        {
            foreach (int rarityGid in values.RarityGids)
                selectedRarityGids.Add(rarityGid);
        }

        ApplyRarityVisualState();
        ApplyRarityButtonBackgrounds();
        ShowFilterTab();
    }

    private void RegisterListeners()
    {
        if (searchButton != null)
        {
            searchButton.onClick.RemoveListener(HandleSearchClicked);
            searchButton.onClick.AddListener(HandleSearchClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            closeButton.onClick.AddListener(HandleCloseClicked);
        }
        if (clearSearchButton != null)
        {
            clearSearchButton.onClick.RemoveListener(HandleClearSearchClicked);
            clearSearchButton.onClick.AddListener(HandleClearSearchClicked);
        }

        if (openRarityTabButton != null)
        {
            openRarityTabButton.onClick.RemoveListener(ShowRarityTab);
            openRarityTabButton.onClick.AddListener(ShowRarityTab);
        }

        if (backToFilterTabButton != null)
        {
            backToFilterTabButton.onClick.RemoveListener(ShowFilterTab);
            backToFilterTabButton.onClick.AddListener(ShowFilterTab);
        }

        foreach (var binding in rarityButtons)
        {
            if (binding == null || binding.button == null)
                continue;

            binding.button.onClick.RemoveAllListeners();
            int rarityGid = binding.rarityGid;
            binding.button.onClick.AddListener(() => ToggleRarity(rarityGid));
        }
    }

    private void TryBindOptionalReferences()
    {
        if (clearSearchButton == null)
            clearSearchButton = transform.Find("ClearSearchButton")?.GetComponent<Button>();
    }

    private void HandleSearchClicked()
    {
        onSearch?.Invoke(CollectValues());
    }

    private void HandleCloseClicked()
    {
        onClose?.Invoke();
    }

    private void HandleClearSearchClicked()
    {
        ApplyValues(new MarketSearchPopupValues());
    }

    private MarketSearchPopupValues CollectValues()
    {
        int? levelFrom = ParseNullableInt(levelFromInput != null ? levelFromInput.text : null);
        int? levelTo = ParseNullableInt(levelToInput != null ? levelToInput.text : null);

        if (levelFrom.HasValue && levelFrom.Value < 1)
            levelFrom = 1;
        if (levelTo.HasValue && levelTo.Value < 1)
            levelTo = 1;

        if (levelFrom.HasValue && levelTo.HasValue && levelFrom.Value > levelTo.Value)
        {
            int temp = levelFrom.Value;
            levelFrom = levelTo.Value;
            levelTo = temp;
        }

        return new MarketSearchPopupValues
        {
            ItemName = itemNameInput != null ? itemNameInput.text : string.Empty,
            LevelFrom = levelFrom,
            LevelTo = levelTo,
            RarityGids = new List<int>(selectedRarityGids)
        };
    }

    private static int? ParseNullableInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value.Trim(), out int parsed) ? parsed : null;
    }

    private void ToggleRarity(int rarityGid)
    {
        if (!selectedRarityGids.Add(rarityGid))
            selectedRarityGids.Remove(rarityGid);

        ApplyRarityVisualState();
    }

    private void ApplyRarityVisualState()
    {
        foreach (var binding in rarityButtons)
        {
            if (binding == null)
                continue;

            bool isSelected = selectedRarityGids.Contains(binding.rarityGid);
            if (binding.selectedState != null)
                binding.selectedState.SetActive(isSelected);
        }
    }

    private void ApplyRarityButtonBackgrounds()
    {
        foreach (var binding in rarityButtons)
        {
            if (binding == null || binding.button == null)
                continue;

            if (binding.button.TryGetComponent<Image>(out var buttonImage) && buttonImage != null)
                ItemVisualHelper.ApplyRarityBackground(buttonImage, binding.rarityGid);
        }
    }

    private void ShowRarityTab()
    {
        if (filterTab != null)
            filterTab.SetActive(false);
        if (rarityTab != null)
            rarityTab.SetActive(true);
    }

    private void ShowFilterTab()
    {
        if (filterTab != null)
            filterTab.SetActive(true);
        if (rarityTab != null)
            rarityTab.SetActive(false);
    }

    private void OnDestroy()
    {
        if (searchButton != null)
            searchButton.onClick.RemoveListener(HandleSearchClicked);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HandleCloseClicked);
        if (clearSearchButton != null)
            clearSearchButton.onClick.RemoveListener(HandleClearSearchClicked);
        if (openRarityTabButton != null)
            openRarityTabButton.onClick.RemoveListener(ShowRarityTab);
        if (backToFilterTabButton != null)
            backToFilterTabButton.onClick.RemoveListener(ShowFilterTab);
    }
}
