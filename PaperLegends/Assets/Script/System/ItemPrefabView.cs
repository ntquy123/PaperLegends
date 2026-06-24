using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemPrefabView : MonoBehaviour
{
    [Header("Core Images")]
    [SerializeField]
    private Image backgroundImage;
    [SerializeField]
    private Image itemIcon;

    [Header("Level")]
    [SerializeField]
    private TextMeshProUGUI levelText;
    [SerializeField]
    private Image levelBanner;

    [Header("Quantity")]
    [SerializeField]
    private TextMeshProUGUI quantityText;

    [Header("Status")]
    [SerializeField]
    private GameObject statusLabel;
    [SerializeField]
    private GameObject lineBanner;

    [Header("Actions")]
    [SerializeField]
    private Button removeButton;

    public Image BackgroundImage => backgroundImage;
    public Image ItemIcon => itemIcon;
    public TextMeshProUGUI LevelText => levelText;
    public Image LevelBanner => levelBanner;
    public TextMeshProUGUI QuantityText => quantityText;
    public GameObject StatusLabel => statusLabel;
    public GameObject LineBanner => lineBanner;
    public Button RemoveButton => removeButton;
}
