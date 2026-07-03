using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class PaperLegendCombatDamagePopupPlayer
{
    private static readonly Color CritTextColor = new Color(1f, 0.38f, 0.05f, 1f);
    private static readonly Color CritOutlineColor = new Color(0.12f, 0.02f, 0f, 0.95f);

    private static Transform _popupRoot;

    public static void ShowCriticalDamage(Vector3 worldPosition, float damage)
    {
#if UNITY_SERVER
        return;
#else
        if (damage <= 0.01f)
            return;

        EnsurePopupRoot();
        var popupObject = new GameObject("PaperLegendCritDamagePopup");
        popupObject.transform.SetParent(_popupRoot, false);

        Vector2 horizontalJitter = Random.insideUnitCircle * 0.18f;
        popupObject.transform.position = worldPosition + new Vector3(horizontalJitter.x, 0f, horizontalJitter.y);

        var instance = popupObject.AddComponent<CritDamagePopupInstance>();
        instance.Play(Mathf.Max(1f, damage), popupObject.transform.position);
#endif
    }

    private static void EnsurePopupRoot()
    {
        if (_popupRoot != null)
            return;

        var rootObject = new GameObject("PaperLegendCombatDamagePopups");
        Object.DontDestroyOnLoad(rootObject);
        _popupRoot = rootObject.transform;
    }

    private sealed class CritDamagePopupInstance : MonoBehaviour
    {
        private const float WorldScale = 0.011f;
        private const float CanvasWidth = 180f;
        private const float CanvasHeight = 72f;
        private const int SortingOrder = 420;

        public void Play(float damage, Vector3 worldPosition)
        {
            transform.position = worldPosition;

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = SortingOrder;

            var canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            canvasRect.localScale = Vector3.one * WorldScale;

            var text = CreateDamageText(canvasRect, damage);
            FaceCamera(canvasObject.transform);

            Vector3 startPosition = transform.position;
            Vector3 endPosition = startPosition + Vector3.up * 0.95f + new Vector3(Random.Range(-0.12f, 0.12f), 0f, Random.Range(-0.12f, 0.12f));

            canvasGroup.DOFade(1f, 0.08f);
            text.rectTransform.localScale = Vector3.one * 1.45f;
            text.rectTransform.DOScale(1f, 0.16f).SetEase(Ease.OutBack);

            transform.DOMove(endPosition, 0.72f).SetEase(Ease.OutCubic);
            canvasGroup.DOFade(0f, 0.28f).SetDelay(0.48f).OnComplete(() =>
            {
                if (this != null)
                    Destroy(gameObject);
            });
        }

        private static TextMeshProUGUI CreateDamageText(RectTransform parent, float damage)
        {
            var textObject = new GameObject("CritDamageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 42f;
            text.fontStyle = FontStyles.Bold;
            text.color = CritTextColor;
            text.text = Mathf.RoundToInt(damage).ToString();
            text.enableWordWrapping = false;
            text.outlineWidth = 0.28f;
            text.outlineColor = CritOutlineColor;

            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return text;
        }

        private void LateUpdate()
        {
            if (transform.childCount > 0)
                FaceCamera(transform.GetChild(0));
        }

        private static void FaceCamera(Transform target)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            target.rotation = Quaternion.LookRotation(target.position - cam.transform.position, Vector3.up);
        }
    }
}
