using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerArrowUI : MonoBehaviour
{
    public Transform target;
    private RectTransform rectTransform;
    private Canvas canvas;
    public TextMeshProUGUI label;
    public Vector3 offset = Vector3.zero;
    [Header("ChamCat")]
    [SerializeField] private GameObject killIconRoot;
    [SerializeField] private Image killIconImage;
    [SerializeField] private ChamCatHoldButton killHoldButton;
    private bool _hasLoadedKillIcon;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        if (killIconRoot != null)
            killIconRoot.SetActive(false);
    }

    void LateUpdate()
    {
        if (target == null || canvas == null)
            return;

        // If the canvas has no worldCamera assigned use Camera.main as fallback
        Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        Vector3 worldPos = target.position + offset;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        rectTransform.position = screenPos;
    }

    public void SetLabelText(string text)
    {
        if (label != null)
            label.text = text;
    }

    public void SetKillIconVisible(bool isVisible, BallServerController targetBall)
    {
        if (killIconRoot != null)
            killIconRoot.SetActive(isVisible);

        if (killHoldButton != null)
            killHoldButton.SetTarget(isVisible ? targetBall : null);

        if (isVisible)
            EnsureKillIconSprite();
    }

    private void EnsureKillIconSprite()
    {
        if (_hasLoadedKillIcon || killIconImage == null)
            return;

        _hasLoadedKillIcon = true;
        StartCoroutine(AddressablesHelper.LoadSprite(
            $"{AddressablePaths.Root}/Skills/{(int)EffectPlayerType.ChamCat}.png",
            sprite =>
            {
                if (sprite != null && killIconImage != null)
                    killIconImage.sprite = sprite;
            }));
    }
}
