using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InstructionSliderPopupUI : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Slides")]
    [SerializeField] private ScrollRect slideScrollRect;
    [SerializeField] private RectTransform slideViewport;
    [SerializeField] private RectTransform slideContent;
    [SerializeField] private List<GameObject> slideObjects = new();
    [SerializeField] private bool syncSlideSizeWithViewport = true;
    [SerializeField] private float manualSlideStep = 0f;
    [SerializeField] private float slideTweenDuration = 0.32f;
    [SerializeField] private Ease slideEase = Ease.OutCubic;
    [SerializeField] private bool enableSwipe = true;
    [SerializeField] private float swipeThreshold = 80f;

    [Header("Dots")]
    [SerializeField] private Transform dotContainer;
    [SerializeField] private GameObject dotTemplate;
    [SerializeField] private bool enableDotClick = true;
    [SerializeField] private float activeDotAlpha = 1f;
    [SerializeField] private float inactiveDotAlpha = 0.35f;
    [SerializeField] private float activeDotScale = 1.1f;
    [SerializeField] private float inactiveDotScale = 0.9f;

    [Header("Disabled Button Visual")]
    [SerializeField] private float enabledButtonAlpha = 1f;
    [SerializeField] private float disabledButtonAlpha = 0.45f;

    private readonly List<Button> dotButtons = new();
    private readonly List<CanvasGroup> dotCanvasGroups = new();
    private readonly List<Transform> dotTransforms = new();
    private readonly List<Vector3> dotBaseScales = new();

    private Action onClose;
    private int currentSlideIndex;
    private Sequence slideSequence;
    private Vector2 dragStartPosition;
    private bool initialized;

    public int CurrentSlideIndex => currentSlideIndex;
    public int SlideCount => slideObjects?.Count ?? 0;

    public void Initialize(Action closeAction)
    {
        onClose = closeAction;
        initialized = true;

        NormalizeReferences();
        PrepareSlides();
        RegisterButtons();
        BuildDots();
        GoToSlide(0, true);
    }

    public void ShowPrevious()
    {
        if (currentSlideIndex <= 0)
            return;

        GoToSlide(currentSlideIndex - 1);
    }

    public void ShowNext()
    {
        if (currentSlideIndex >= SlideCount - 1)
            return;

        GoToSlide(currentSlideIndex + 1);
    }

    public void GoToSlide(int slideIndex)
    {
        GoToSlide(slideIndex, false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!enableSwipe || SlideCount <= 1)
            return;

        dragStartPosition = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!enableSwipe || SlideCount <= 1)
            return;

        float deltaX = eventData.position.x - dragStartPosition.x;
        if (Mathf.Abs(deltaX) < swipeThreshold)
        {
            GoToSlide(currentSlideIndex);
            return;
        }

        if (deltaX < 0f)
            ShowNext();
        else
            ShowPrevious();
    }

    private void NormalizeReferences()
    {
        if (slideObjects == null)
            slideObjects = new List<GameObject>();

        slideObjects.RemoveAll(slide => slide == null);

        if (slideScrollRect != null)
        {
            if (slideContent == null)
                slideContent = slideScrollRect.content;
            if (slideViewport == null)
                slideViewport = slideScrollRect.viewport;
            slideScrollRect.horizontal = true;
        }

        if (slideContent == null && slideObjects.Count > 0)
            slideContent = slideObjects[0].transform.parent as RectTransform;

        if (slideViewport == null && slideContent != null)
            slideViewport = slideContent.parent as RectTransform;

        if (slideObjects.Count == 0 && slideContent != null)
        {
            for (int i = 0; i < slideContent.childCount; i++)
            {
                Transform child = slideContent.GetChild(i);
                if (child != null)
                    slideObjects.Add(child.gameObject);
            }
        }
    }

    private void PrepareSlides()
    {
        for (int i = 0; i < slideObjects.Count; i++)
        {
            if (slideObjects[i] != null)
                slideObjects[i].SetActive(true);
        }

        if (slideContent == null)
            return;

        Canvas.ForceUpdateCanvases();

        if (syncSlideSizeWithViewport)
        {
            SyncSlideSizesToViewport();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(slideContent);
    }

    private void SyncSlideSizesToViewport()
    {
        if (slideViewport == null)
            return;

        float viewportWidth = slideViewport.rect.width;
        float viewportHeight = slideViewport.rect.height;
        if (viewportWidth <= 0f)
            return;

        GridLayoutGroup gridLayout = slideContent.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            float cellHeight = viewportHeight > 0f ? viewportHeight : gridLayout.cellSize.y;
            gridLayout.cellSize = new Vector2(viewportWidth, cellHeight);
        }

        foreach (GameObject slideObject in slideObjects)
        {
            RectTransform slideRect = slideObject != null ? slideObject.GetComponent<RectTransform>() : null;
            if (slideRect == null)
                continue;

            slideRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewportWidth);
            if (viewportHeight > 0f)
                slideRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);
        }
    }

    private void RegisterButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(HandleClose);
        }

        if (previousButton != null)
        {
            previousButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(ShowPrevious);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(ShowNext);
        }
    }

    private void BuildDots()
    {
        dotButtons.Clear();
        dotCanvasGroups.Clear();
        dotTransforms.Clear();
        dotBaseScales.Clear();

        if (dotContainer == null || dotTemplate == null)
            return;

        for (int i = dotContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = dotContainer.GetChild(i);
            if (child != null && child.gameObject != dotTemplate)
                Destroy(child.gameObject);
        }

        dotTemplate.SetActive(false);

        for (int i = 0; i < SlideCount; i++)
        {
            int capturedIndex = i;
            GameObject dotObject = Instantiate(dotTemplate, dotContainer);
            dotObject.name = $"{dotTemplate.name}_{i + 1}";
            dotObject.SetActive(true);

            Button dotButton = dotObject.GetComponent<Button>();
            if (dotButton == null)
                dotButton = dotObject.GetComponentInChildren<Button>(true);
            if (dotButton == null)
                dotButton = dotObject.AddComponent<Button>();

            dotButton.onClick.RemoveAllListeners();
            dotButton.onClick.AddListener(() => GoToSlide(capturedIndex));

            CanvasGroup dotCanvasGroup = dotObject.GetComponent<CanvasGroup>();
            if (dotCanvasGroup == null)
                dotCanvasGroup = dotObject.AddComponent<CanvasGroup>();

            Vector3 baseScale = dotObject.transform.localScale == Vector3.zero
                ? Vector3.one
                : dotObject.transform.localScale;

            dotButtons.Add(dotButton);
            dotCanvasGroups.Add(dotCanvasGroup);
            dotTransforms.Add(dotObject.transform);
            dotBaseScales.Add(baseScale);
        }
    }

    private void GoToSlide(int slideIndex, bool instant)
    {
        if (SlideCount == 0)
        {
            UpdateNavigationButtons();
            return;
        }

        int targetIndex = Mathf.Clamp(slideIndex, 0, SlideCount - 1);
        currentSlideIndex = targetIndex;

        if (slideContent != null)
        {
            MoveSlideContent(targetIndex, instant);
        }
        else
        {
            SetOnlyActiveSlide(targetIndex);
        }

        UpdateNavigationButtons();
        UpdateDots(instant);
    }

    private void MoveSlideContent(int targetIndex, bool instant)
    {
        float targetX = -targetIndex * GetSlideStep();
        Vector2 targetPosition = new(targetX, slideContent.anchoredPosition.y);

        if (slideScrollRect != null)
            slideScrollRect.StopMovement();

        slideSequence?.Kill();
        slideContent.DOKill();

        if (instant)
        {
            slideContent.anchoredPosition = targetPosition;
            return;
        }

        slideSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(slideContent.DOAnchorPos(targetPosition, slideTweenDuration).SetEase(slideEase))
            .OnKill(() => slideSequence = null)
            .OnComplete(() => slideSequence = null);
    }

    private float GetSlideStep()
    {
        if (manualSlideStep > 0f)
            return manualSlideStep;

        float spacing = GetLayoutSpacing();

        if (slideViewport != null && slideViewport.rect.width > 0f)
            return slideViewport.rect.width + spacing;

        if (slideObjects.Count > 0 && slideObjects[0] != null)
        {
            RectTransform firstSlideRect = slideObjects[0].GetComponent<RectTransform>();
            if (firstSlideRect != null && firstSlideRect.rect.width > 0f)
                return firstSlideRect.rect.width + spacing;
        }

        return 0f;
    }

    private float GetLayoutSpacing()
    {
        if (slideContent == null)
            return 0f;

        HorizontalLayoutGroup horizontalLayout = slideContent.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout != null)
            return horizontalLayout.spacing;

        GridLayoutGroup gridLayout = slideContent.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
            return gridLayout.spacing.x;

        return 0f;
    }

    private void SetOnlyActiveSlide(int targetIndex)
    {
        for (int i = 0; i < slideObjects.Count; i++)
        {
            if (slideObjects[i] != null)
                slideObjects[i].SetActive(i == targetIndex);
        }
    }

    private void UpdateNavigationButtons()
    {
        SetButtonState(previousButton, currentSlideIndex > 0);
        SetButtonState(nextButton, currentSlideIndex < SlideCount - 1);
    }

    private void SetButtonState(Button button, bool isEnabled)
    {
        if (button == null)
            return;

        button.interactable = isEnabled;

        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = button.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.DOKill();
        canvasGroup.DOFade(isEnabled ? enabledButtonAlpha : disabledButtonAlpha, 0.15f)
            .SetUpdate(true);
    }

    private void UpdateDots(bool instant)
    {
        for (int i = 0; i < dotButtons.Count; i++)
        {
            bool isActive = i == currentSlideIndex;

            if (dotButtons[i] != null)
                dotButtons[i].interactable = enableDotClick && !isActive;

            CanvasGroup dotCanvasGroup = i < dotCanvasGroups.Count ? dotCanvasGroups[i] : null;
            if (dotCanvasGroup != null)
            {
                dotCanvasGroup.DOKill();
                float targetAlpha = isActive ? activeDotAlpha : inactiveDotAlpha;
                if (instant)
                    dotCanvasGroup.alpha = targetAlpha;
                else
                    dotCanvasGroup.DOFade(targetAlpha, 0.18f).SetUpdate(true);
            }

            Transform dotTransform = i < dotTransforms.Count ? dotTransforms[i] : null;
            if (dotTransform == null)
                continue;

            Vector3 baseScale = i < dotBaseScales.Count ? dotBaseScales[i] : Vector3.one;
            Vector3 targetScale = baseScale * (isActive ? activeDotScale : inactiveDotScale);

            dotTransform.DOKill();
            if (instant)
                dotTransform.localScale = targetScale;
            else
                dotTransform.DOScale(targetScale, 0.18f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
    }

    private void HandleClose()
    {
        onClose?.Invoke();
    }

    private void OnDisable()
    {
        slideSequence?.Kill();
        slideSequence = null;

        if (slideContent != null)
            slideContent.DOKill();

        KillButtonTween(previousButton);
        KillButtonTween(nextButton);

        for (int i = 0; i < dotButtons.Count; i++)
        {
            Transform dotTransform = i < dotTransforms.Count ? dotTransforms[i] : null;
            if (dotTransform != null)
                dotTransform.DOKill();

            CanvasGroup canvasGroup = i < dotCanvasGroups.Count ? dotCanvasGroups[i] : null;
            if (canvasGroup != null)
                canvasGroup.DOKill();
        }
    }

    private void OnDestroy()
    {
        if (!initialized)
            return;

        if (closeButton != null)
            closeButton.onClick.RemoveListener(HandleClose);
        if (previousButton != null)
            previousButton.onClick.RemoveListener(ShowPrevious);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(ShowNext);

        foreach (Button dotButton in dotButtons)
        {
            if (dotButton != null)
                dotButton.onClick.RemoveAllListeners();
        }
    }

    private static void KillButtonTween(Button button)
    {
        if (button == null)
            return;

        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.DOKill();
    }
}
