using TMPro;
using UnityEngine.Serialization;
using UnityEngine;

public class MarketOrderBoardRowUI : MonoBehaviour
{
    public TMP_Text PriceText;
    [FormerlySerializedAs("QuantityText")]
    public TMP_Text QuantityBuyText;
    public TMP_Text QuantitySellText;
}
