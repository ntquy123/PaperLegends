using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CompanionBallOptionView : MonoBehaviour
{
    [SerializeField]
    private Button selectButton;

    [SerializeField]
    private Image highlightImage;

    [SerializeField]
    private Image itemImage;

    [SerializeField]
    private TMP_Text itemNameText;

    public Button SelectButton => selectButton;
    public Image HighlightImage => highlightImage;
    public Image ItemImage => itemImage;
    public TMP_Text ItemNameText => itemNameText;
}
