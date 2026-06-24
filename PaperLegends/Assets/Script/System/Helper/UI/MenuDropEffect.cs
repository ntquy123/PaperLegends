using DG.Tweening;
using UnityEngine;

/// <summary>
/// Hiệu ứng rơi dành cho các bảng hiệu menu sử dụng DOTween.
/// Gắn script này lên GameObject cần hiệu ứng để bảng hiệu rơi từ trên xuống
/// và cố định ở vị trí mong muốn trên màn hình.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class MenuDropEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private RectTransform menuRect;

    [Header("Drop Settings")]
    [Tooltip("Sử dụng vị trí hiện tại của RectTransform làm điểm cố định cuối cùng.")]
    [SerializeField]
    private bool useCurrentAnchoredPosition = true;

    [Tooltip("Vị trí anchor cuối cùng sau khi hiệu ứng kết thúc.")]
    [SerializeField]
    private Vector2 targetAnchoredPosition = new Vector2(0f, -100f);

    [Tooltip("Độ lệch ban đầu (tính từ vị trí cuối cùng) để bảng hiệu bắt đầu ngoài màn hình.")]
    [SerializeField]
    private Vector2 startOffset = new Vector2(0f, 600f);

    [Tooltip("Độ lệch nhỏ giúp bảng hiệu nảy nhẹ trước khi dừng.")]
    [SerializeField]
    private Vector2 settleOffset = new Vector2(0f, -25f);

    [SerializeField, Min(0f)]
    private float dropDuration = 0.6f;

    [SerializeField, Min(0f)]
    private float settleDuration = 0.25f;

    [SerializeField]
    private Ease dropEase = Ease.OutBounce;

    [SerializeField]
    private Ease settleEase = Ease.OutQuad;

    [Header("Behaviour")]
    [SerializeField]
    private bool playOnEnable = true;

    private Sequence activeSequence;

    private void Reset()
    {
        menuRect = GetComponent<RectTransform>();
        targetAnchoredPosition = menuRect.anchoredPosition;
    }

    private void Awake()
    {
        if (menuRect == null)
        {
            menuRect = GetComponent<RectTransform>();
        }

        if (menuRect == null)
        {
            Debug.LogWarning($"{nameof(MenuDropEffect)}: Không tìm thấy RectTransform để thực hiện hiệu ứng.");
            return;
        }

        if (useCurrentAnchoredPosition)
        {
            targetAnchoredPosition = menuRect.anchoredPosition;
        }
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            PlayDropEffect();
        }
    }

    private void OnDisable()
    {
        activeSequence?.Kill();
        if (menuRect != null)
        {
            menuRect.anchoredPosition = targetAnchoredPosition;
        }
    }

    /// <summary>
    /// Kích hoạt hiệu ứng rơi cho bảng hiệu menu.
    /// </summary>
    public void PlayDropEffect()
    {
        if (menuRect == null)
        {
            Debug.LogWarning($"{nameof(MenuDropEffect)}: Không thể phát hiệu ứng vì thiếu RectTransform.");
            return;
        }

        activeSequence?.Kill();

        // Đặt vị trí bắt đầu ở ngoài màn hình (bên trên) dựa vào startOffset.
        menuRect.anchoredPosition = targetAnchoredPosition + startOffset;

        activeSequence = DOTween.Sequence();
        activeSequence.Append(menuRect.DOAnchorPos(targetAnchoredPosition, dropDuration).SetEase(dropEase));

        if (settleDuration > 0f && settleOffset != Vector2.zero)
        {
            float halfSettle = settleDuration * 0.5f;
            activeSequence.Append(menuRect.DOAnchorPos(targetAnchoredPosition + settleOffset, halfSettle).SetEase(settleEase));
            activeSequence.Append(menuRect.DOAnchorPos(targetAnchoredPosition, halfSettle).SetEase(settleEase));
        }
    }
}
