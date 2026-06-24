using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles visual state of a shop item.
/// Keeps base color (gray when item is not buyable) and controls highlight.
/// </summary>
public class ShopItemUI : MonoBehaviour
{
    private Image itemImage;

    /// <summary>Base color when item is not highlighted.</summary>
    private Color baseColor = Color.white;

    /// <summary>Whether the item can be purchased.</summary>
    public bool IsBuyable { get; private set; }

    private void Awake()
    {
        itemImage = GetComponent<Image>();
    }

    /// <summary>
    /// Initialize the item state.
    /// </summary>
    /// <param name="buyable">True if the player can purchase this item.</param>
    public void Init(bool buyable)
    {
        Init(buyable, Color.white);
    }

    public void Init(bool buyable, Color rarityColor)
    {
        IsBuyable = buyable;
        baseColor = buyable
            ? rarityColor
            : new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.5f);
        if (itemImage != null)
            itemImage.color = baseColor;
    }

    /// <summary>
    /// Toggle highlight on the item.
    /// </summary>
    /// <param name="highlight">Enable or disable highlight.</param>
    public void SetHighlight(bool highlight)
    {
        if (itemImage == null)
            return;
        itemImage.color = highlight ? new Color(1f, 1f, 0f, 1f) : baseColor;
    }
}
