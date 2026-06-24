using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TabManager : MonoBehaviour
{
    [SerializeField]
    private GameObject[] tabContents; // Các panel nội dung của từng tab
    [SerializeField]
    private Button[] tabButtons; // Các nút tab
    [SerializeField]
    private TabTransitionEffect transitionEffect = TabTransitionEffect.FadeScale;

    private int currentTab = 0; // Tab hiện tại

    public GameObject[] TabContents => tabContents;
    public Button[] TabButtons => tabButtons;
    public TabTransitionEffect TransitionEffect => transitionEffect;

    void Start()
    {
        int buttonCount = tabButtons?.Length ?? 0;
        for (int i = 0; i < buttonCount; i++)
        {
            if (tabButtons[i] == null) continue;

            int index = i;
            tabButtons[i].onClick.AddListener(() => ChangeTab(index));

            // Thêm CanvasGroup vào button nếu chưa có
            if (!tabButtons[i].gameObject.GetComponent<CanvasGroup>())
            {
                tabButtons[i].gameObject.AddComponent<CanvasGroup>();
            }

            // Làm mờ tất cả nút trừ nút đầu tiên
            tabButtons[i].GetComponent<CanvasGroup>().alpha = (i == 0) ? 1f : 0.5f;
        }

        if (HasValidTabContents())
        {
            for (int i = 0; i < tabContents.Length; i++)
            {
                if (tabContents[i] == null) continue;

                var canvasGroup = tabContents[i].GetComponent<CanvasGroup>();
                if (!canvasGroup)
                {
                    canvasGroup = tabContents[i].AddComponent<CanvasGroup>();
                }
                bool isActive = (i == 0);
                canvasGroup.alpha = isActive ? 1f : 0f;
                canvasGroup.interactable = isActive;
                canvasGroup.blocksRaycasts = isActive;
                tabContents[i].SetActive(isActive);
            }

            ShowTab(0); // Mặc định hiển thị tab đầu tiên
        }
    }
    public void ChangeTab(int newTab)
    {
        int buttonCount = tabButtons?.Length ?? 0;
        if (newTab < 0 || newTab >= buttonCount)
        {
            Debug.LogWarning($"Tab index {newTab} is out of range.");
            return;
        }

        if (newTab == currentTab) return; // Nếu bấm vào tab đang mở thì bỏ qua

        int oldTab = currentTab;
        UpdateButtonVisual(oldTab, newTab);

        if (!HasValidTabContents() || oldTab >= tabContents.Length || newTab >= tabContents.Length || tabContents[oldTab] == null || tabContents[newTab] == null)
        {
            currentTab = newTab;
            return;
        }

        CanvasGroup oldTabCanvas = tabContents[oldTab].GetComponent<CanvasGroup>();
        if (!oldTabCanvas) oldTabCanvas = tabContents[oldTab].AddComponent<CanvasGroup>();

        CanvasGroup newTabCanvas = tabContents[newTab].GetComponent<CanvasGroup>();
        if (!newTabCanvas) newTabCanvas = tabContents[newTab].AddComponent<CanvasGroup>();

        // Kích hoạt tab mới và ẩn các tab khác trừ tab cũ
        for (int i = 0; i < tabContents.Length; i++)
        {
            if (tabContents[i] == null || i == oldTab) continue;
            tabContents[i].SetActive(i == newTab);
        }

        switch (transitionEffect)
        {
            case TabTransitionEffect.Instant:
                oldTabCanvas.alpha = 0f;
                oldTabCanvas.interactable = false;
                oldTabCanvas.blocksRaycasts = false;
                tabContents[oldTab].SetActive(false);

                newTabCanvas.alpha = 1f;
                newTabCanvas.interactable = true;
                newTabCanvas.blocksRaycasts = true;

                break;

            case TabTransitionEffect.FadeScale:
                oldTabCanvas.interactable = false;
                oldTabCanvas.blocksRaycasts = false;
                oldTabCanvas.DOFade(0, 0.3f)
                    .OnComplete(() => tabContents[oldTab].SetActive(false));

                newTabCanvas.alpha = 0;
                newTabCanvas.interactable = true;
                newTabCanvas.blocksRaycasts = true;
                tabContents[newTab].transform.localScale = Vector3.zero;
                newTabCanvas.DOFade(1, 0.3f);
                tabContents[newTab].transform.DOScale(1, 0.3f).SetEase(Ease.OutBack);

                break;

            case TabTransitionEffect.SlideHorizontal:
                oldTabCanvas.interactable = false;
                oldTabCanvas.blocksRaycasts = false;

                RectTransform oldRect = tabContents[oldTab].GetComponent<RectTransform>();
                RectTransform newRect = tabContents[newTab].GetComponent<RectTransform>();
                float width = oldRect.rect.width;

                newRect.anchoredPosition = new Vector2(width, 0);
                newTabCanvas.alpha = 1f;
                newTabCanvas.interactable = true;
                newTabCanvas.blocksRaycasts = true;

                oldRect.DOAnchorPosX(-width, 0.3f).SetEase(Ease.OutQuad).OnComplete(() =>
                {
                    tabContents[oldTab].SetActive(false);
                    oldRect.anchoredPosition = Vector2.zero;
                });
                newRect.DOAnchorPosX(0, 0.3f).SetEase(Ease.OutQuad);

                break;

            case TabTransitionEffect.Flip:
                oldTabCanvas.interactable = false;
                oldTabCanvas.blocksRaycasts = false;

                RectTransform oldFlipRect = tabContents[oldTab].GetComponent<RectTransform>();
                RectTransform newFlipRect = tabContents[newTab].GetComponent<RectTransform>();

                newFlipRect.localRotation = Quaternion.Euler(0, 90, 0);
                newTabCanvas.alpha = 0f;
                newTabCanvas.interactable = true;
                newTabCanvas.blocksRaycasts = true;

                oldFlipRect.DOLocalRotate(new Vector3(0, -90, 0), 0.3f).OnComplete(() =>
                {
                    tabContents[oldTab].SetActive(false);
                    oldFlipRect.localRotation = Quaternion.identity;
                });
                newFlipRect.DOLocalRotate(Vector3.zero, 0.3f);

                oldTabCanvas.DOFade(0, 0.3f);
                newTabCanvas.DOFade(1, 0.3f);

                break;
        }

        currentTab = newTab; // Cập nhật tab hiện tại
    }


    void ShowTab(int index)
    {
        if (!HasValidTabContents()) return;

        for (int i = 0; i < tabContents.Length; i++)
        {
            if (tabContents[i] == null) continue;
            tabContents[i].SetActive(i == index);
        }
    }

    private bool HasValidTabContents()
    {
        return tabContents != null && tabContents.Length > 0;
    }

    private void UpdateButtonVisual(int oldTab, int newTab)
    {
        if (tabButtons == null) return;

        if (oldTab >= 0 && oldTab < tabButtons.Length && tabButtons[oldTab] != null)
        {
            var oldCanvas = tabButtons[oldTab].GetComponent<CanvasGroup>() ?? tabButtons[oldTab].gameObject.AddComponent<CanvasGroup>();
            oldCanvas.DOKill(true);
            oldCanvas.DOFade(0.5f, 0.3f);
        }

        if (newTab >= 0 && newTab < tabButtons.Length && tabButtons[newTab] != null)
        {
            var newCanvas = tabButtons[newTab].GetComponent<CanvasGroup>() ?? tabButtons[newTab].gameObject.AddComponent<CanvasGroup>();
            newCanvas.DOKill(true);
            newCanvas.DOFade(1f, 0.3f);
        }
    }

    private void OnDisable()
    {
        // Stop any tab tweens when disabled
        if (tabContents != null)
        foreach (var tab in tabContents)
        {
            if (tab != null)
            {
                tab.transform.DOKill(true);
                var cg = tab.GetComponent<CanvasGroup>();
                if (cg != null)
                    cg.DOKill(true);
            }
        }
        if (tabButtons != null)
        foreach (var btn in tabButtons)
        {
            if (btn != null)
            {
                btn.transform.DOKill(true);
                var cg = btn.GetComponent<CanvasGroup>();
                if (cg != null)
                    cg.DOKill(true);
            }
        }
    }
    public Transform FindContent(GameObject parentTab)
    {
        return parentTab.transform.Find("Viewport/Content");
    }
}
