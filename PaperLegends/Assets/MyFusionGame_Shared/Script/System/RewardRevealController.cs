using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class RewardRevealController : MonoBehaviour, IDragHandler
{
    public TextMeshProUGUI rewardText;
    public GameObject rewardAmount;
    public TextMeshProUGUI AmountText;
    public float dragThreshold = 200f;

    private RectTransform rectTransform;
    private float dragAmount;
    private bool revealed;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rewardText != null)
            rewardText.gameObject.SetActive(false);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (revealed || rectTransform == null)
            return;

        dragAmount += eventData.delta.y;
        float t = Mathf.Clamp01(dragAmount / dragThreshold);
        rectTransform.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -180f, t), 0f, 0f);
        rectTransform.localScale = Vector3.one * (1f + 0.1f * t);

        if (t >= 1f)
        {
            revealed = true;
            if (rewardText != null)
                rewardText.gameObject.SetActive(true);
        }
    }
}
