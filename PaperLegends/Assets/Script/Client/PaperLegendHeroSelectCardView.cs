using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PaperLegendHeroSelectCardView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image selectedFrame;
    [SerializeField] private Image unavailableOverlay;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text roleText;

    private int modelId;

    public int ModelId => modelId;

    public void Configure(PaperLegendHeroData hero, Sprite iconSprite, bool isSelected, bool isUnavailable, Action<int> onClicked)
    {
        modelId = hero != null ? hero.ResolveModelIdInt() : 0;

        if (button == null)
            button = GetComponent<Button>();

        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.enabled = iconSprite != null;
        }

        if (nameText != null)
            nameText.text = hero != null ? ResolveLocalizedText(hero.name) : string.Empty;

        if (roleText != null)
            roleText.text = hero != null ? ResolveLocalizedRole(hero.role) : string.Empty;

        SetSelected(isSelected);
        SetUnavailable(isUnavailable);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = !isUnavailable && modelId > 0;
            button.onClick.AddListener(() => onClicked?.Invoke(modelId));
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null)
            selectedFrame.gameObject.SetActive(selected);
    }

    public void SetUnavailable(bool unavailable)
    {
        if (unavailableOverlay != null)
            unavailableOverlay.gameObject.SetActive(unavailable);

        if (button != null)
            button.interactable = !unavailable && modelId > 0;
    }

    private static string ResolveLocalizedRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return string.Empty;

        return ResolveLocalizedText($"hero_role_{role.ToLowerInvariant()}");
    }

    private static string ResolveLocalizedText(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        return LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(key)
            : key;
    }
}
