// NotificationHelper.cs

using UnityEngine;
using Unity.VisualScripting;
// ===== CLIENT: có DOTween/UI/TMP =====
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
public class NotificationHelper : MonoBehaviour
{
    public static NotificationHelper Instance;

    private void Awake() => Instance = this;
    [Header("Client Only")]
    public GameObject notificationPrefab; // Prefab chứa Background + Text (CanvasGroup + Image + TMP_Text)
    public Transform parentPanel;         // Panel cha để spawn prefab vào

    public void ShowNotification(string message, bool isSuccess)
    {
        if (notificationPrefab == null || parentPanel == null)
        {
            Debug.LogWarning("NotificationHelper: Chưa gán prefab hoặc parentPanel.");
            return;
        }

        parentPanel.SetAsLastSibling();
        var notificationObj = Instantiate(notificationPrefab, parentPanel);
        if (!notificationObj.TryGetComponent(out CanvasGroup canvasGroup))
            canvasGroup = notificationObj.AddComponent<CanvasGroup>();

        var messageText = notificationObj.transform.Find("text").GetComponent<TMP_Text>();
        var background  = notificationObj.transform.Find("image").GetComponent<Image>();

        if (messageText == null || background == null)
        {
            Debug.LogError("Notification prefab thiếu TMP_Text hoặc Image.");
            Destroy(notificationObj);
            return;
        }

        // Set nội dung + màu
        messageText.text = message;
        background.color = isSuccess ? Color.green : Color.red;

        // Cập nhật kích thước nền theo text
        LayoutRebuilder.ForceRebuildLayoutImmediate(messageText.rectTransform);
        var bgRect = background.rectTransform;
        bgRect.sizeDelta = new Vector2(messageText.preferredWidth + 60f, messageText.preferredHeight + 50f);

        // Hiệu ứng fade in/out
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.5f).OnComplete(() =>
        {
            DOVirtual.DelayedCall(2f, () =>
            {
                canvasGroup.DOFade(0f, 0.5f).OnComplete(() => Destroy(notificationObj));
            });
        });
    }
}
