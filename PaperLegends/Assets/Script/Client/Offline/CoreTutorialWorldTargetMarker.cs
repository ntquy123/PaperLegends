using TMPro;
using UnityEngine;
#if !UNITY_SERVER
using DG.Tweening;
#endif

[DisallowMultipleComponent]
public sealed class CoreTutorialWorldTargetMarker : MonoBehaviour
{
    [Header("Marker Visual")]
    [SerializeField, Tooltip("Text nam trong World Space Canvas cua marker.")]
    private TMP_Text targetText;
    [SerializeField] private string targetMessage = "Hay ban vao vien bi nay!";
    [SerializeField, Tooltip("Object mui ten con se duoc animate nhap nho.")]
    private Transform floatingArrow;
    [SerializeField, Tooltip("Dieu chinh them neu mat truoc cua World Space Canvas bi nguoc huong camera.")]
    private Vector3 billboardEulerOffset;

    [Header("Animation")]
    [SerializeField] private float arrowFloatDistance = 0.035f;
    [SerializeField] private float arrowFloatDuration = 0.4f;

    private Transform followedTarget;
    private Vector3 worldOffset;
    private Vector3 arrowStartLocalPosition;
    private Camera viewingCamera;
#if !UNITY_SERVER
    private Tween arrowTween;
#endif

    public void Initialize(Transform target, Vector3 positionOffset)
    {
        followedTarget = target;
        worldOffset = positionOffset;
        if (targetText == null)
        {
            targetText = GetComponentInChildren<TMP_Text>(true);
        }

        if (targetText != null)
        {
            targetText.text = targetMessage;
        }

        if (floatingArrow != null)
        {
            arrowStartLocalPosition = floatingArrow.localPosition;
        }

        Canvas worldCanvas = GetComponentInChildren<Canvas>(true);
        if (worldCanvas != null && worldCanvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning("CoreTutorialWorldTargetMarker: Canvas should use World Space render mode.");
        }

        UpdateMarkerPose();
    }

    public void SetVisible(bool visible)
    {
#if !UNITY_SERVER
        arrowTween?.Kill();
        arrowTween = null;
#endif
        if (floatingArrow != null)
        {
            floatingArrow.localPosition = arrowStartLocalPosition;
        }

        gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        UpdateMarkerPose();
#if !UNITY_SERVER
        if (floatingArrow != null)
        {
            arrowTween = floatingArrow
                .DOLocalMoveY(arrowStartLocalPosition.y + arrowFloatDistance, arrowFloatDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
#endif
    }

    private void LateUpdate()
    {
        UpdateMarkerPose();
    }

    private void UpdateMarkerPose()
    {
        if (followedTarget == null)
        {
            return;
        }

        transform.position = followedTarget.position + worldOffset;
        if (viewingCamera == null)
        {
            viewingCamera = Camera.main;
        }

        if (viewingCamera == null)
        {
            return;
        }

        Vector3 toMarker = transform.position - viewingCamera.transform.position;
        if (toMarker.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(toMarker.normalized, viewingCamera.transform.up)
                * Quaternion.Euler(billboardEulerOffset);
        }
    }

    private void OnDestroy()
    {
#if !UNITY_SERVER
        arrowTween?.Kill();
#endif
    }
}
