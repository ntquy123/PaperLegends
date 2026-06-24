using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Holds references to all UI elements of the player popup so we
/// don't rely on runtime <c>transform.Find</c> lookups.
/// </summary>
public class PopupPlayerUI : MonoBehaviour
{
    public TMP_Text NamePlayer;
    public TMP_Text LevelPlayer;
    public TMP_Text NameBall;
    public TMP_Text InforItem;

    public Slider MassSlider;
    public Slider DragSlider;
    public Slider BounceSlider;
    public Slider ImpactSlider;

    public RawImage RawImage;
    public Image ItemImage;

    public Button CloseButton;
}

