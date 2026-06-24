using UnityEngine;
using UnityEngine.UI;
public class ButtonEffect : MonoBehaviour
{
    public Button skillButton;
    public int Level;
    public EffectPlayerType efftype;
    [SerializeField]
    private GameObject lineBanner;

    public GameObject LineBanner => lineBanner;

    private void Awake()
    {
        if (lineBanner == null)
            lineBanner = FindLineBanner();
    }

    private GameObject FindLineBanner()
    {
        string[] names =
        {
            "LineBanner",
            "BannerLine",
            "SelectedLine",
            "SelectedBanner"
        };

        foreach (var name in names)
        {
            var child = transform.Find(name);
            if (child != null)
                return child.gameObject;
        }

        return null;
    }
}
