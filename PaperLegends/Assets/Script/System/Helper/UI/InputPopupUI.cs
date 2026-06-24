using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Holds references to input popup UI elements to avoid runtime hierarchy lookups.
/// </summary>
public class InputPopupUI : MonoBehaviour
{
    [Header("Common Elements")]
    public TMP_Text MessageText;
    public TMP_InputField InputField;
    public Button YesButton;
    public Button NoButton;

    [Header("Price Overview")]
    public TMP_Text MinPriceText;
    public TMP_Text MaxPriceText;
    public TMP_Text SuggestedPriceText;
    public GameObject PriceOverviewContainer;
}

