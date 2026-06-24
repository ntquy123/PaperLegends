using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarketOrderBoardPopupUI : MonoBehaviour
{
    public TMP_Text TitleText;
    public TMP_Text MinPriceText;
    public TMP_Text MaxPriceText;
    public TMP_Text RemainingBiText;
    public Transform GridContent;
    public GameObject RowPrefab;
    public TMP_InputField PriceInput;
    public TMP_InputField QuantityInput;
    public Image ItemImage;
    public Button IncreaseQuantityButton;
    public Button DecreaseQuantityButton;
    public Button BuyButton;
    public Button CloseButton;
}
