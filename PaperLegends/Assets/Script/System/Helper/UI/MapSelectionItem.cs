using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapSelectionItem : MonoBehaviour
{
    [SerializeField]
    private Image iconImage;
    [SerializeField]
    private TMP_Text nameText;
    [SerializeField]
    private Button selectButton;
    [SerializeField]
    private GameObject selectedHighlight;

    private MapOptionData optionData;
    private Action<MapSelectionItem> onSelected;
    private Color defaultButtonColor = Color.white;

    public GameMapId MapId => optionData != null ? optionData.mapId : GameMapId.HometownHouse;

    public void Bind(MapOptionData option, Action<MapSelectionItem> onSelectedCallback)
    {
        optionData = option;
        onSelected = onSelectedCallback;

        if (iconImage != null && optionData != null && optionData.mapIcon != null)
            iconImage.sprite = optionData.mapIcon;

        if (nameText != null)
        {
            if (optionData != null && LocalizationManager.Instance != null)
            {
                string mapKey = optionData.mapId.ToString();
                nameText.text = LocalizationManager.Instance.GetText(mapKey);
            }
            else
            {
                nameText.text = optionData != null ? optionData.mapId.ToString() : string.Empty;
            }
        }

        if (selectButton != null)
        {
            defaultButtonColor = selectButton.colors.normalColor;
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected?.Invoke(this));
        }

        SetSelected(false);
    }

    public void SetSelected(bool isSelected)
    {
        if (selectedHighlight != null)
            selectedHighlight.SetActive(isSelected);

        if (selectButton != null)
        {
            var colors = selectButton.colors;
            colors.normalColor = isSelected ? new Color(0.75f, 0.9f, 1f) : defaultButtonColor;
            colors.selectedColor = colors.normalColor;
            selectButton.colors = colors;
        }
    }
}
