using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarketItemView : MonoBehaviour
{
    [Header("Main Info")]
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text sellerText;

    [Header("Actions")]
    [SerializeField] private Button rootButton;
    [SerializeField] private Button saleButton;
    [SerializeField] private Button buyButton;

    public Image ItemImage => itemImage;
    public TMP_Text NameText => nameText;
    public TMP_Text PriceText => priceText;
    public TMP_Text LevelText => levelText;
    public TMP_Text SellerText => sellerText;
    public Button RootButton => rootButton;
    public Button SaleButton => saleButton;
    public Button BuyButton => buyButton;

    private void Reset()
    {
        rootButton = GetComponent<Button>();
    }
}
