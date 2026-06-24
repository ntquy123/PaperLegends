using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShopItemEntryView : MonoBehaviour
{
    [Header("Main")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceByMoneyText;
    [SerializeField] private TMP_Text priceByRingBallText;
    [SerializeField] private Image itemImage;
    [SerializeField] private Button buyByMoneyButton;
    [SerializeField] private Button buyByRingBallButton;

    [Header("Daily Purchase")]
    [SerializeField] private TMP_Text maxPurchasePerDayText;
    [SerializeField] private TMP_Text purchasedTodayText;

    public TMP_Text NameText => nameText;
    public TMP_Text PriceByMoneyText => priceByMoneyText;
    public TMP_Text PriceByRingBallText => priceByRingBallText;
    public Image ItemImage => itemImage;
    public Button BuyByMoneyButton => buyByMoneyButton;
    public Button BuyByRingBallButton => buyByRingBallButton;
    public TMP_Text MaxPurchasePerDayText => maxPurchasePerDayText;
    public TMP_Text PurchasedTodayText => purchasedTodayText;

    private void Reset()
    {
        if (buyByMoneyButton == null || buyByRingBallButton == null)
        {
            var allButtons = GetComponentsInChildren<Button>(true);
            if (allButtons.Length > 0) buyByMoneyButton = allButtons[0];
            if (allButtons.Length > 1) buyByRingBallButton = allButtons[1];
        }
    }
}
