
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Unity.VisualScripting;

public class PopupHelper : MonoBehaviour
{
    public static PopupHelper Instance;
    [SerializeField]
    private GameObject popupPrefab;  // hiển thị 1 thông báo và có xác nhận hoặc không xác nhận
    [SerializeField]
    private GameObject popupOutPrefab;  //hiển thị 1 thônng báo và chỉ có xác nhận
    [SerializeField]
    private GameObject popupPlayerPrefab;  // hiển thị thông tin người chơi
    [SerializeField]
    private GameObject IncomingChallengePopupPrefab; // pupup hiển thị khiêu chiến
    [SerializeField]
    private GameObject rewardRevealPopupPrefab;
    [Header("Reward Ads Popup")]
    [SerializeField]
    private GameObject rewardAdsPopupPrefab;
    [Header("New Player Gift Popup")]
    [SerializeField]
    private GameObject newPlayerGiftPopupPrefab;
    [SerializeField]
    private GameObject popupInputPrefab; // Popup với ô nhập liệu
    [SerializeField]
    private GameObject popupMessagePrefab; // Popup gửi tin nhắn
    [SerializeField]
    private GameObject messageDetailPopupPrefab; // Popup hiển thị hội thoại tin nhắn
    [SerializeField]
    private GameObject rankHistoryPopupPrefab; // Popup hiển thị lịch sử rank
    [SerializeField]
    private GameObject rankLeaderboardPopupPrefab; // Popup hiển thị leaderboard rank
    [SerializeField]
    private GameObject rankDefinitionPopupPrefab; // Popup hiển thị định nghĩa rank
    [SerializeField]
    private GameObject chatPopupPrefab; // Popup nhập chat nhanh
    [SerializeField]
    private GameObject itemInfoPopupPrefab; // Prefab hiển thị thông tin item
    [SerializeField]
    private GameObject socialLoginInfoPopupPrefab; // Popup hiển thị thông tin đăng nhập mạng xã hội
    [SerializeField]
    private GameObject createRoomPopupPrefab; // Popup tạo phòng
    [SerializeField]
    private GameObject marketSearchPopupPrefab; // Popup bộ lọc market
    [SerializeField]
    private GameObject marketOrderBoardPopupPrefab; // Popup lịch sử giao dịch & đặt lệnh mua
    [Header("Game Settings Popup")]
    [SerializeField]
    private GameObject gameSettingsPopupPrefab; // Popup cài đặt trong game
    [Header("Action Emote Popup")]
    [SerializeField]
    private GameObject actionEmotePopupPrefab; // Popup chọn action emote trong trận
    [Header("Instruction Popup")]
    [SerializeField]
    private GameObject instructionPopupPrefab; // Popup hiển thị hướng dẫn
    [SerializeField]
    private GameObject instructionSliderPopupPrefab; // Popup hướng dẫn dạng slider
    [SerializeField]
    private Vector2 instructionPopupWidthRange = new(480f, 780f);
    [SerializeField]
    private float instructionPopupHorizontalPadding = 120f;
    [SerializeField]
    private float instructionPopupVerticalPadding = 140f;
    [Header("Lucky Draw After Match")]
    [SerializeField]
    private GameObject luckyDrawAfterMatchPopupPrefab;
    [Header("Social Provider Icons")]
    [SerializeField]
    private Sprite defaultProviderIcon;
    [SerializeField]
    private Sprite googleProviderIcon;
    [SerializeField]
    private Canvas uiCanvas;
    [Header("New Message Notice Popup")]
    [SerializeField]
    private GameObject newMessageNoticePopupPrefab;
    private GameObject currentPlayerPopup;
    private int currentPopupPlayerId = -1;
    private GameObject currentSocialInfoPopup;
    private GameObject currentNewMessageNoticePopup;
    private DG.Tweening.Sequence currentNewMessageNoticeSequence;
    private Coroutine ballMaterialLoadRoutine;
    private AsyncOperationHandle<Material> ballMaterialHandle;
    private Coroutine cateyeMaterialLoadRoutine;
    private AsyncOperationHandle<Material> cateyeMaterialHandle;
    private readonly Dictionary<int, Sprite> activeSkillIconCache = new();
    private static readonly string[] NewPlayerGiftConfirmButtonNames =
    {
        "ReceiveButton",
        "ReceiveGiftButton",
        "ClaimButton",
        "ClaimGiftButton",
        "ConfirmGiftButton",
        "AcceptButton",
        "ConfirmButton",
        "YesButton",
        "OkButton",
        "ContinueButton",
        "ButtonNhanQua",
        "NhanQuaButton",
        "ButtonReceiveGift"
    };
    private static readonly string[] NewPlayerGiftCloseButtonNames =
    {
        "CloseButton",
        "CancelButton",
        "NoButton"
    };


    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    public void ShowPopupConfirm(string mess)
    {
        ShowPopupOut(mess, ExitButton);
    }

    public void ShowNewMessageNoticePopup()
    {
        if (newMessageNoticePopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: newMessageNoticePopupPrefab is not assigned.");
            return;
        }

        if (!TryGetCanvas(out Canvas canvas))
        {
            return;
        }

        HideCurrentNewMessageNoticePopup();

        currentNewMessageNoticePopup = Instantiate(newMessageNoticePopupPrefab, canvas.transform);
        currentNewMessageNoticePopup.transform.SetAsLastSibling();

        Transform popupTransform = currentNewMessageNoticePopup.transform;
        Vector3 baseScale = popupTransform.localScale == Vector3.zero ? Vector3.one : popupTransform.localScale;
        popupTransform.localScale = baseScale * 0.82f;

        RectTransform rectTransform = currentNewMessageNoticePopup.GetComponent<RectTransform>();
        Vector2 baseAnchoredPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(0f, -44f);
        }

        CanvasGroup canvasGroup = currentNewMessageNoticePopup.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = currentNewMessageNoticePopup.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Tween moveIn = rectTransform != null
            ? rectTransform.DOAnchorPos(baseAnchoredPosition + new Vector2(0f, 14f), 0.28f).SetEase(Ease.OutCubic)
            : popupTransform.DOMoveY(popupTransform.position.y + 0.18f, 0.28f).SetEase(Ease.OutCubic);

        Tween moveOut = rectTransform != null
            ? rectTransform.DOAnchorPos(baseAnchoredPosition + new Vector2(0f, 54f), 0.28f).SetEase(Ease.InCubic)
            : popupTransform.DOMoveY(popupTransform.position.y + 0.34f, 0.28f).SetEase(Ease.InCubic);

        currentNewMessageNoticeSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(canvasGroup.DOFade(1f, 0.16f))
            .Join(popupTransform.DOScale(baseScale * 1.08f, 0.22f).SetEase(Ease.OutBack))
            .Join(moveIn)
            .Append(popupTransform.DOScale(baseScale, 0.12f).SetEase(Ease.OutQuad))
            .AppendInterval(1.35f)
            .Append(canvasGroup.DOFade(0f, 0.28f))
            .Join(moveOut)
            .OnComplete(() =>
            {
                currentNewMessageNoticeSequence = null;
                DestroyCurrentNewMessageNoticePopup();
            })
            .OnKill(() => currentNewMessageNoticeSequence = null);
    }

    private void HideCurrentNewMessageNoticePopup()
    {
        if (currentNewMessageNoticeSequence != null)
        {
            currentNewMessageNoticeSequence.Kill();
            currentNewMessageNoticeSequence = null;
        }

        DestroyCurrentNewMessageNoticePopup();
    }

    private void DestroyCurrentNewMessageNoticePopup()
    {
        if (currentNewMessageNoticePopup == null)
        {
            return;
        }

        Destroy(currentNewMessageNoticePopup);
        currentNewMessageNoticePopup = null;
    }

    public void ExitButton()
    {
        Time.timeScale = 1;
       // bool isHost = GameManagerNetWork.Instance.serverRPC != null && GameManagerNetWork.Instance.serverRPC.HasStateAuthority;
       // if (isHost)
           // UIController.Instance.StartCoroutine(UIController.Instance.CheckServerConnection());

        GameManagerNetWork.Instance.CloseConnectToRunner();
        GameManagerNetWork.Instance.currentQuickMatchResultId = null;
        GameOverManager.Instance.EndGamePopup.SetActive(false);
        DayNightWeatherManager.Instance?.StopEnvironmentSound();
        LoadingManager.LoadScene("Menu");
    }
    private bool TryGetCanvas(out Canvas canvas)
    {
        canvas = uiCanvas;
        if (canvas == null)
        {
            Debug.LogError("PopupHelper: UI Canvas reference is not assigned.");
            return false;
        }

        return true;
    }
    public void ShowPopup(string message, Action confirmAction, bool allowStacked = false)
    {
        GameObject popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null && !allowStacked)
            return;
        if (!TryGetCanvas(out Canvas canvas))
            return;

        // Tạo Popup từ Prefab và gắn vào Canvas
        GameObject popupInstance = Instantiate(popupPrefab, canvas.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero; // Bắt đầu từ scale nhỏ

        // Lấy các thành phần UI trong popup
        TMP_Text messageText = popupInstance.transform.Find("MessageText").GetComponent<TMP_Text>();
        Button yesButton = popupInstance.transform.Find("YesButton").GetComponent<Button>();
        Button noButton = popupInstance.transform.Find("NoButton").GetComponent<Button>();

        // Gán nội dung cho popup
        messageText.text = message;

        // Hiệu ứng xuất hiện
        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        // Gán sự kiện cho nút "Có"
        yesButton.onClick.AddListener(() =>
        {
            HidePopup(popupInstance);
            confirmAction?.Invoke();
           
        });

        // Nút "Không" sẽ tự động đóng popup
        noButton.onClick.AddListener(() =>
        {
            HidePopup(popupInstance);
            currentPlayerPopup = null;
            currentPopupPlayerId = -1;
        });
    }
    public void ShowPopupOut(string message, Action confirmAction)
    {
        GameObject popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            return;
        if (!TryGetCanvas(out Canvas canvas))
            return;

        // Tạo Popup từ Prefab và gắn vào Canvas
        GameObject popupInstance = Instantiate(popupOutPrefab, canvas.transform);
        popupInstance.transform.localScale = Vector3.zero; // Bắt đầu từ scale nhỏ

        // Lấy các thành phần UI trong popup
        TMP_Text messageText = popupInstance.transform.Find("MessageText").GetComponent<TMP_Text>();
        Button yesButton = popupInstance.transform.Find("YesButton").GetComponent<Button>();
        Button noButton = popupInstance.transform.Find("NoButton").GetComponent<Button>();

        // Gán nội dung cho popup
        messageText.text = message;

        // Hiệu ứng xuất hiện
        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        // Gán sự kiện cho nút "Có"
        yesButton.onClick.AddListener(() =>
        {
            HidePopup(popupInstance);
            confirmAction?.Invoke();

        });

        // Nút "Không" sẽ tự động đóng popup
        //noButton.onClick.AddListener(() =>
        //{
        //    HidePopup(popupInstance);
        //    currentPlayerPopup = null;
        //    currentPopupPlayerId = -1;
        //});
    }

    public void ShowInputPopup(string message, Action<string> confirmAction, ItemPriceOverviewData priceOverview = null)
    {
        var popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            HidePopup(popupObject);

        if (!TryGetCanvas(out Canvas canvas))
            return;

        GameObject popupInstance = Instantiate(popupInputPrefab, canvas.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;

        var popupUI = popupInstance.GetComponent<InputPopupUI>();
        if (popupUI == null)
        {
            Debug.LogError("InputPopupUI component missing from popupInputPrefab.");
            Destroy(popupInstance);
            return;
        }

        TMP_Text messageText = popupUI.MessageText;
        TMP_InputField inputField = popupUI.InputField;
        Button yesButton = popupUI.YesButton;
        Button noButton = popupUI.NoButton;
        TMP_Text minPriceText = popupUI.MinPriceText;
        TMP_Text maxPriceText = popupUI.MaxPriceText;
        TMP_Text suggestedPriceText = popupUI.SuggestedPriceText;
        GameObject priceContainer = popupUI.PriceOverviewContainer;

        // Configure numeric input
        if (inputField != null)
        {
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.characterValidation = TMP_InputField.CharacterValidation.Integer;
            inputField.text = priceOverview.suggestedPrice.ToString();
        }

        messageText.text = message;

        if (priceOverview != null)
        {
            if (minPriceText != null)
                minPriceText.text = priceOverview.minPrice.ToString();

            if (maxPriceText != null)
                maxPriceText.text = priceOverview.maxPrice.ToString();

            if (suggestedPriceText != null)
                suggestedPriceText.text = priceOverview.suggestedPrice.ToString();

            if (priceContainer != null)
                priceContainer.SetActive(true);
        }
        else if (priceContainer != null)
        {
            priceContainer.SetActive(false);
        }

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        yesButton.onClick.AddListener(() =>
        {
            HidePopup(popupInstance);
            confirmAction?.Invoke(inputField.text);
        });

        noButton.onClick.AddListener(() =>
        {
            HidePopup(popupInstance);
            currentPlayerPopup = null;
            currentPopupPlayerId = -1;
        });
    }

    public void ShowChatInputPopup(Action<string> onSend)
    {
        if (onSend == null)
        {
            Debug.LogWarning("PopupHelper: onSend callback is null for chat popup.");
            return;
        }

        GameObject prefabToUse = chatPopupPrefab != null ? chatPopupPrefab : popupMessagePrefab != null ? popupMessagePrefab : popupInputPrefab;
        if (prefabToUse == null)
        {
            Debug.LogWarning("PopupHelper: Không tìm thấy prefab để hiển thị chat popup.");
            return;
        }

        var popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            HidePopup(popupObject);

        if (!TryGetCanvas(out Canvas canvas))
            return;

        GameObject overlay = new GameObject("ChatInputPopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0f);
        overlayImage.raycastTarget = true;

        GameObject popupInstance = Instantiate(prefabToUse, overlay.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        TMP_InputField inputField = popupInstance.GetComponentInChildren<TMP_InputField>(true);
        if (inputField == null)
        {
            Debug.LogWarning("PopupHelper: Chat popup thiếu TMP_InputField.");
            Destroy(popupInstance);
            return;
        }

        inputField.contentType = TMP_InputField.ContentType.Standard;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterValidation = TMP_InputField.CharacterValidation.None;
        inputField.text = string.Empty;
        inputField.ActivateInputField();

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f);

        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        Button sendButton = popupInstance.transform.Find("SendButton")?.GetComponent<Button>();
        if (sendButton == null)
            sendButton = popupInstance.GetComponentInChildren<Button>(true);

        if (sendButton == null)
        {
            Debug.LogWarning("PopupHelper: Không tìm thấy nút gửi trên chat popup.");
            Destroy(popupInstance);
            return;
        }

        void AttemptSend()
        {
            string text = inputField.text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                inputField.Select();
                inputField.ActivateInputField();
                return;
            }

            onSend(text);
            HidePopup(popupInstance);
        }

        sendButton.onClick.AddListener(AttemptSend);
        inputField.onSubmit.AddListener(_ => AttemptSend());

        Button overlayButton = overlay.GetComponent<Button>();
        overlayButton.onClick.AddListener(() => HidePopup(popupInstance));
    }

    public void ShowInstructionPopup()
    {
        ShowInstructionSliderPopup();
    }

    public void ShowInstructionSliderPopup()
    {
        GameObject prefabToUse = instructionSliderPopupPrefab != null
            ? instructionSliderPopupPrefab
            : instructionPopupPrefab;

        if (prefabToUse == null)
        {
            Debug.LogWarning("PopupHelper: instructionSliderPopupPrefab is not assigned.");
            return;
        }

        CloseActivePopup();

        if (!TryGetCanvas(out Canvas canvas))
            return;

        GameObject overlay = new GameObject("InstructionSliderPopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0.35f);
        overlayImage.raycastTarget = true;

        GameObject popupInstance = Instantiate(prefabToUse, overlay.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        RectTransform popupRect = popupInstance.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.anchoredPosition = Vector2.zero;
        }

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        Button overlayButton = overlay.GetComponent<Button>();
        overlayButton.onClick.AddListener(() => HidePopup(popupInstance));

        InstructionSliderPopupUI popupUI = popupInstance.GetComponentInChildren<InstructionSliderPopupUI>(true);
        if (popupUI == null)
        {
            Debug.LogWarning("PopupHelper: InstructionSliderPopupUI component missing from instruction slider popup prefab.");
            HidePopup(popupInstance);
            return;
        }

        popupUI.Initialize(() => HidePopup(popupInstance));
    }

    public void ShowInstructionPopup(string description)
    {
        if (instructionPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: instructionPopupPrefab is not assigned.");
            return;
        }

        CloseActivePopup();

        if (!TryGetCanvas(out Canvas canvas))
            return;

        GameObject overlay = new GameObject("InstructionPopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0.35f);
        overlayImage.raycastTarget = true;

        GameObject popupInstance = Instantiate(instructionPopupPrefab, overlay.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        RectTransform popupRect = popupInstance.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.anchoredPosition = Vector2.zero;
        }

        Button overlayButton = overlay.GetComponent<Button>();
        overlayButton.onClick.AddListener(() => HidePopup(popupInstance));

        TMP_Text descriptionText = popupInstance.GetComponentsInChildren<TMP_Text>(true)
                                              .FirstOrDefault(t => string.Equals(t.name, "DescriptionText", StringComparison.OrdinalIgnoreCase));

        if (descriptionText != null)
        {
            descriptionText.enableWordWrapping = true;
            descriptionText.richText = true;
            string localizedText = LocalizationManager.Instance.GetText(description);
            descriptionText.text = ItemVisualHelper.ConvertSimpleHtmlToTmp(localizedText);
            Canvas.ForceUpdateCanvases();

            GridLayoutGroup gridLayout = descriptionText.GetComponentInParent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                float cellWidth = Mathf.Max(0f, gridLayout.cellSize.x);
                Vector2 preferredSize = descriptionText.GetPreferredValues(descriptionText.text, cellWidth, 0f);
                gridLayout.cellSize = new Vector2(gridLayout.cellSize.x, Mathf.Ceil(preferredSize.y));
                LayoutRebuilder.ForceRebuildLayoutImmediate(gridLayout.GetComponent<RectTransform>());
            }
        }

        Button closeButton = popupInstance.transform.Find("CloseButton")?.GetComponent<Button>();
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => HidePopup(popupInstance));
        }

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f);

        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
    }

    public void ShowRewardAdsPopup()
    {
        if (rewardAdsPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: rewardAdsPopupPrefab is not assigned.");
            return;
        }

        CloseActivePopup();

        if (!TryGetCanvas(out Canvas canvas))
            return;

        GameObject overlay = new GameObject("RewardAdsPopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0.35f);
        overlayImage.raycastTarget = true;

        GameObject popupInstance = Instantiate(rewardAdsPopupPrefab, overlay.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        var rewardsManager = popupInstance.GetComponentInChildren<RewardsManager>(true);
        if (rewardsManager == null)
        {
            Debug.LogWarning("PopupHelper: RewardsManager component is missing on reward ads popup prefab.");
            HidePopup(popupInstance);
            return;
        }

        rewardsManager.GetListAds();

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        Button overlayButton = overlay.GetComponent<Button>();
        overlayButton.onClick.AddListener(() => HidePopup(popupInstance));
    }

    public void ShowNewPlayerGiftPopup()
    {
        if (newPlayerGiftPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: newPlayerGiftPopupPrefab is not assigned.");
            return;
        }

        CloseActivePopup();

        if (!TryGetCanvas(out Canvas canvas))
            return;

        GameObject overlay = new GameObject("NewPlayerGiftPopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0.35f);
        overlayImage.raycastTarget = true;

        GameObject popupInstance = Instantiate(newPlayerGiftPopupPrefab, overlay.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        Button confirmButton = ResolveNewPlayerGiftConfirmButton(popupInstance);
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(() => HidePopup(popupInstance));
        }
        else
        {
            Debug.LogWarning("PopupHelper: New player gift popup confirm button was not found.");
        }

        Button closeButton = FindPopupButtonByNames(popupInstance, NewPlayerGiftCloseButtonNames);
        if (closeButton != null && closeButton != confirmButton)
        {
            closeButton.onClick.AddListener(() => HidePopup(popupInstance));
        }
    }

    private static Button ResolveNewPlayerGiftConfirmButton(GameObject popupInstance)
    {
        Button confirmButton = FindPopupButtonByNames(popupInstance, NewPlayerGiftConfirmButtonNames);
        if (confirmButton != null)
        {
            return confirmButton;
        }

        Button[] buttons = popupInstance.GetComponentsInChildren<Button>(true);
        return buttons.Length == 1 ? buttons[0] : null;
    }

    private static Button FindPopupButtonByNames(GameObject popupInstance, IReadOnlyCollection<string> buttonNames)
    {
        if (popupInstance == null || buttonNames == null || buttonNames.Count == 0)
        {
            return null;
        }

        Button[] buttons = popupInstance.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            foreach (string buttonName in buttonNames)
            {
                if (string.Equals(button.name, buttonName, StringComparison.OrdinalIgnoreCase))
                {
                    return button;
                }
            }
        }

        return null;
    }



    public void ShowLuckyDrawAfterMatchPopup(int playerId, bool isWinner, Action onClosed = null, LuckyDrawAfterMatchReward preclaimedReward = null)
    {
        if (luckyDrawAfterMatchPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: luckyDrawAfterMatchPopupPrefab is not assigned.");
            onClosed?.Invoke();
            return;
        }

        CloseActivePopup();

        // Create a standalone DontDestroyOnLoad canvas so the popup survives scene transitions
        GameObject canvasObj = new GameObject("LuckyDrawOverlayCanvas");
        Canvas overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 999;

        if (TryGetCanvas(out Canvas mainCanvas))
        {
            var mainScaler = mainCanvas.GetComponent<CanvasScaler>();
            if (mainScaler != null)
            {
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = mainScaler.uiScaleMode;
                scaler.referenceResolution = mainScaler.referenceResolution;
                scaler.screenMatchMode = mainScaler.screenMatchMode;
                scaler.matchWidthOrHeight = mainScaler.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = mainScaler.referencePixelsPerUnit;
            }
        }

        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);

        GameObject popupInstance = Instantiate(luckyDrawAfterMatchPopupPrefab, canvasObj.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;

        var controller = popupInstance.GetComponent<LuckyDrawAfterMatchPopup>();
        if (controller == null)
        {
            Debug.LogWarning("PopupHelper: LuckyDrawAfterMatchPopup component is missing on prefab.");
            Destroy(canvasObj);
            onClosed?.Invoke();
            return;
        }

        controller.SetOverlayCanvas(canvasObj);
        controller.Init(playerId, isWinner, onClosed, preclaimedReward);

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.25f).SetUpdate(true);
        popupInstance.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void ResizeInstructionPopup(RectTransform popupRect, TMP_Text descriptionText)
    {
        if (popupRect == null || descriptionText == null)
            return;

        float minWidth = Mathf.Max(100f, instructionPopupWidthRange.x);
        float maxWidth = Mathf.Max(minWidth, instructionPopupWidthRange.y);

        LayoutRebuilder.ForceRebuildLayoutImmediate(descriptionText.rectTransform);

        Vector2 preferredSize = descriptionText.GetPreferredValues(descriptionText.text, maxWidth - instructionPopupHorizontalPadding, 0f);
        float targetWidth = Mathf.Clamp(preferredSize.x + instructionPopupHorizontalPadding, minWidth, maxWidth);

        descriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth - instructionPopupHorizontalPadding);
        LayoutRebuilder.ForceRebuildLayoutImmediate(descriptionText.rectTransform);

        float targetHeight = descriptionText.preferredHeight + instructionPopupVerticalPadding;
        popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        LayoutRebuilder.ForceRebuildLayoutImmediate(popupRect);
    }

    public void ShowMessagePopup(int receiverId, Action<string> onSend)
    {
        _ = receiverId;

        if (popupMessagePrefab == null)
        {
            Debug.LogWarning("popupMessagePrefab is not assigned");
            return;
        }

        var popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            HidePopup(popupObject);

        if (!TryGetCanvas(out Canvas canvas))
            return;

        GameObject popupInstance = Instantiate(popupMessagePrefab, canvas.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        TMP_InputField inputField = popupInstance.transform.Find("InputField").GetComponentInChildren<TMP_InputField>(true);
        if (inputField != null)
        {
            inputField.contentType = TMP_InputField.ContentType.Standard;
            inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
            inputField.characterValidation = TMP_InputField.CharacterValidation.None;
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.3f);
        }

        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        Button sendButton = popupInstance.transform.Find("SendButton")?.GetComponent<Button>();
        if (sendButton == null)
        {
            foreach (var button in popupInstance.GetComponentsInChildren<Button>(true))
            {
                if (string.Equals(button.name, "SendButton", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(button.name, "ConfirmButton", StringComparison.OrdinalIgnoreCase))
                {
                    sendButton = button;
                    break;
                }
            }
        }

        if (sendButton != null)
        {
            sendButton.onClick.AddListener(() =>
            {
                string message = inputField != null ? inputField.text : string.Empty;
                HidePopup(popupInstance);
                onSend?.Invoke(message);
            });
        }
        else
        {
            Debug.LogWarning("Send button not found on popupMessagePrefab");
        }

        Button closeButton = popupInstance.transform.Find("CloseButton")?.GetComponent<Button>();
        if (closeButton == null)
            closeButton = popupInstance.transform.Find("CancelButton")?.GetComponent<Button>();

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => HidePopup(popupInstance));
        }
    }

    public MessageDetailPopupUI ShowMessageDetailPopup(Action onClose)
    {
        if (messageDetailPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: messageDetailPopupPrefab is not assigned.");
            return null;
        }

        var popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            HidePopup(popupObject);

        if (!TryGetCanvas(out Canvas canvas))
            return null;

        GameObject overlay = new GameObject("MessageDetailPopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0);
        overlayImage.raycastTarget = true;

        GameObject popupInstance = Instantiate(messageDetailPopupPrefab, overlay.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        var popupUI = popupInstance.GetComponent<MessageDetailPopupUI>();
        if (popupUI == null)
        {
            Debug.LogWarning("PopupHelper: MessageDetailPopupUI component missing from messageDetailPopupPrefab.");
            Destroy(popupInstance);
            return null;
        }

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        Button overlayButton = overlay.GetComponent<Button>();
        overlayButton.onClick.AddListener(() =>
        {
            onClose?.Invoke();
            HidePopup(popupInstance);
        });

        return popupUI;
    }

    public PlayerRankHistoryPopupUI ShowRankHistoryPopup(Action onClose)
    {
        if (rankHistoryPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: rankHistoryPopupPrefab is not assigned.");
            return null;
        }

        var popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            HidePopup(popupObject);

        if (!TryGetCanvas(out Canvas canvas))
            return null;

        GameObject overlay = new GameObject("RankHistoryPopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0);
        overlayImage.raycastTarget = true;

        GameObject popupInstance = Instantiate(rankHistoryPopupPrefab, overlay.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        var popupUI = popupInstance.GetComponent<PlayerRankHistoryPopupUI>();
        if (popupUI == null)
        {
            Debug.LogWarning("PopupHelper: PlayerRankHistoryPopupUI component missing from rankHistoryPopupPrefab.");
            Destroy(popupInstance);
            return null;
        }

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        Button overlayButton = overlay.GetComponent<Button>();
        overlayButton.onClick.AddListener(() =>
        {
            onClose?.Invoke();
            HidePopup(popupInstance);
        });

        return popupUI;
    }

    public PlayerRankLeaderboardPopupUI ShowRankLeaderboardPopup(Action onClose)
    {
        if (rankLeaderboardPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: rankLeaderboardPopupPrefab is not assigned.");
            return null;
        }

        var popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            HidePopup(popupObject);

        if (!TryGetCanvas(out Canvas canvas))
            return null;

        GameObject overlay = new GameObject("RankLeaderboardPopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0);
        overlayImage.raycastTarget = true;

        GameObject popupInstance = Instantiate(rankLeaderboardPopupPrefab, overlay.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        var popupUI = popupInstance.GetComponent<PlayerRankLeaderboardPopupUI>();
        if (popupUI == null)
        {
            Debug.LogWarning("PopupHelper: PlayerRankLeaderboardPopupUI component missing from rankLeaderboardPopupPrefab.");
            Destroy(popupInstance);
            return null;
        }

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        Button overlayButton = overlay.GetComponent<Button>();
        overlayButton.onClick.AddListener(() =>
        {
            onClose?.Invoke();
            HidePopup(popupInstance);
        });

        return popupUI;
    }

    public RankDefinitionPopupUI ShowRankDefinitionPopup(Action onClose)
    {
        if (rankDefinitionPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: rankDefinitionPopupPrefab is not assigned.");
            return null;
        }

        var popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            HidePopup(popupObject);

        if (!TryGetCanvas(out Canvas canvas))
            return null;

        GameObject overlay = new GameObject("RankDefinitionPopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0);
        overlayImage.raycastTarget = true;

        GameObject popupInstance = Instantiate(rankDefinitionPopupPrefab, overlay.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        var popupUI = popupInstance.GetComponent<RankDefinitionPopupUI>();
        if (popupUI == null)
        {
            Debug.LogWarning("PopupHelper: RankDefinitionPopupUI component missing from rankDefinitionPopupPrefab.");
            Destroy(popupInstance);
            return null;
        }

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        Button overlayButton = overlay.GetComponent<Button>();
        overlayButton.onClick.AddListener(() =>
        {
            onClose?.Invoke();
            HidePopup(popupInstance);
        });

        return popupUI;
    }

    public void ShowCreateRoomPopup(IEnumerable<MapOptionData> mapOptions, int defaultBet, GameMapId defaultMapId, int defaultRoundCount, Action<int, int, GameMapId, int> onCreateRoom)
    {
        if (createRoomPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: createRoomPopupPrefab is not assigned.");
            return;
        }

        CloseActivePopup();

        if (!TryGetCanvas(out Canvas canvas))
            return;

        GameObject popupInstance = Instantiate(createRoomPopupPrefab, canvas.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.one;
        popupInstance.tag = "PopupUI";

        var popupUI = popupInstance.GetComponent<CreateRoomPopupUI>();
        if (popupUI == null)
        {
            Debug.LogWarning("PopupHelper: createRoomPopupPrefab missing CreateRoomPopupUI component.");
            Destroy(popupInstance);
            return;
        }

        popupUI.Initialize(mapOptions, defaultBet, defaultMapId, defaultRoundCount,
            (bet, maxPlayer, mapId, maxRound) =>
            {
                HidePopup(popupInstance);
                onCreateRoom?.Invoke(bet, maxPlayer, mapId, maxRound);
            },
            () => HidePopup(popupInstance));
    }

    public void ShowMarketSearchPopup(MarketSearchPopupValues defaultValues, Action<MarketSearchPopupValues> onSearch)
    {
        if (marketSearchPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: marketSearchPopupPrefab is not assigned.");
            return;
        }

        CloseActivePopup();

        if (!TryGetCanvas(out Canvas canvas))
            return;

        GameObject popupInstance = Instantiate(marketSearchPopupPrefab, canvas.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.one;
        popupInstance.tag = "PopupUI";

        var popupUI = popupInstance.GetComponent<MarketSearchPopupUI>();
        if (popupUI == null)
        {
            Debug.LogWarning("PopupHelper: marketSearchPopupPrefab missing MarketSearchPopupUI component.");
            Destroy(popupInstance);
            return;
        }

        popupUI.Initialize(defaultValues,
            values =>
            {
                onSearch?.Invoke(values);
                HidePopup(popupInstance);
            },
            () => HidePopup(popupInstance));
    }

    public GameSettingsPopupUI ShowGameSettingsPopup(Action onClose = null)
    {
        if (gameSettingsPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: gameSettingsPopupPrefab is not assigned.");
            return null;
        }

        var popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            HidePopup(popupObject);

        if (!TryGetCanvas(out Canvas canvas))
            return null;

        GameObject overlay = new GameObject("GameSettingsPopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0);
        overlayImage.raycastTarget = true;

        GameObject popupInstance = Instantiate(gameSettingsPopupPrefab, overlay.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        var popupUI = popupInstance.GetComponent<GameSettingsPopupUI>();
        if (popupUI == null)
        {
            Debug.LogWarning("PopupHelper: GameSettingsPopupUI component missing from gameSettingsPopupPrefab.");
            Destroy(popupInstance);
            return null;
        }

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        void ClosePopup()
        {
            onClose?.Invoke();
            HidePopup(popupInstance);
        }

        Button overlayButton = overlay.GetComponent<Button>();
        overlayButton.onClick.AddListener(ClosePopup);

        popupUI.Initialize(ClosePopup);

        return popupUI;
    }

    public ActionEmotePopupUI ShowActionEmotePopup(Action<CharacterAnimState> onSelected, Action onClose = null)
    {
        if (actionEmotePopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: actionEmotePopupPrefab is not assigned.");
            return null;
        }

        var popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            HidePopup(popupObject);

        if (!TryGetCanvas(out Canvas canvas))
            return null;

        GameObject overlay = new GameObject("ActionEmotePopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0f);
        overlayImage.raycastTarget = true;

        GameObject popupInstance = Instantiate(actionEmotePopupPrefab, overlay.transform);
        popupInstance.transform.SetAsLastSibling();
        popupInstance.transform.localScale = Vector3.zero;
        popupInstance.tag = "PopupUI";

        var popupUI = popupInstance.GetComponent<ActionEmotePopupUI>();
        if (popupUI == null)
        {
            Debug.LogWarning("PopupHelper: ActionEmotePopupUI component missing from actionEmotePopupPrefab.");
            Destroy(overlay);
            return null;
        }

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.25f);
        popupInstance.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);

        bool isClosing = false;
        void ClosePopup()
        {
            if (isClosing)
                return;

            isClosing = true;
            onClose?.Invoke();
            HidePopup(popupInstance);
        }

        Button overlayButton = overlay.GetComponent<Button>();
        overlayButton.onClick.AddListener(ClosePopup);

        popupUI.Initialize(onSelected, ClosePopup);

        return popupUI;
    }

    // Closes any currently active popup tagged as "PopupUI"

    public void ShowMarketOrderBoardPopup(MarketItemSchema item, int minPrice, int maxPrice, int currentBi, Action<int, int> onBuyRequest)
    {
        var popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            HidePopup(popupObject);
        if (!TryGetCanvas(out Canvas canvas) || marketOrderBoardPopupPrefab == null)
            return;

        var popupInstance = Instantiate(marketOrderBoardPopupPrefab, canvas.transform);
        popupInstance.transform.SetAsLastSibling();
        var popupUI = popupInstance.GetComponent<MarketOrderBoardPopupUI>();
        if (popupUI == null)
            return;

        if (popupUI.TitleText != null)
            popupUI.TitleText.text = $"Lịch sử giao dịch: {LocalizationManager.Instance.GetText(item?.item?.name ?? string.Empty)}";

        if (popupUI.ItemImage != null)
        {
            popupUI.ItemImage.sprite = null;
            if (item != null)
            {
                StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{item.itemId}.png", s =>
                {
                    if (s != null && popupUI != null && popupUI.ItemImage != null)
                        popupUI.ItemImage.sprite = s;
                }));
            }
        }

        if (popupUI.PriceInput != null)
        {
            popupUI.PriceInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            popupUI.PriceInput.text = minPrice.ToString();
        }

        if (popupUI.MinPriceText != null)
            popupUI.MinPriceText.text = minPrice.ToString("#,0");
        if (popupUI.MaxPriceText != null)
            popupUI.MaxPriceText.text = maxPrice.ToString("#,0");

        int quantity = 1;
        int unitPrice = Mathf.Max(1, minPrice);

        if (popupUI.QuantityInput != null)
        {
            popupUI.QuantityInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            popupUI.QuantityInput.text = quantity.ToString();
        }

        int GetMaxAffordableQuantity()
        {
            if (unitPrice <= 0)
                return 1;

            return Mathf.Max(1, currentBi / unitPrice);
        }

        void SetButtonEnabled(Button button, bool enabled)
        {
            if (button == null)
                return;

            button.interactable = enabled;

            if (button.image != null)
                button.image.color = enabled ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        void UpdatePriceAndRemainingBiText()
        {
            int totalPrice = unitPrice * quantity;
            if (popupUI.PriceInput != null)
                popupUI.PriceInput.SetTextWithoutNotify(totalPrice.ToString());

            int remainBi = currentBi - totalPrice;
            if (popupUI.RemainingBiText != null)
                popupUI.RemainingBiText.text = remainBi.ToString("#,0");

            SetButtonEnabled(popupUI.IncreaseQuantityButton, quantity < GetMaxAffordableQuantity());
        }

        void ClampPriceInputToUnitPrice()
        {
            if (popupUI.PriceInput != null && int.TryParse(popupUI.PriceInput.text, out var totalInputPrice))
            {
                totalInputPrice = Mathf.Clamp(totalInputPrice, minPrice, maxPrice);
                unitPrice = Mathf.Max(1, Mathf.RoundToInt((float)totalInputPrice / Mathf.Max(1, quantity)));
            }
            else
            {
                unitPrice = Mathf.Max(1, minPrice);
            }

            quantity = Mathf.Clamp(quantity, 1, GetMaxAffordableQuantity());
            popupUI.QuantityInput?.SetTextWithoutNotify(quantity.ToString());
            UpdatePriceAndRemainingBiText();
        }

        void ClampQuantityInput()
        {
            if (popupUI.QuantityInput == null)
                return;

            if (!int.TryParse(popupUI.QuantityInput.text, out quantity))
                quantity = 1;

            quantity = Mathf.Clamp(quantity, 1, GetMaxAffordableQuantity());
            popupUI.QuantityInput.SetTextWithoutNotify(quantity.ToString());
            UpdatePriceAndRemainingBiText();
        }

        popupUI.PriceInput?.onEndEdit.AddListener(_ => ClampPriceInputToUnitPrice());
        popupUI.QuantityInput?.onEndEdit.AddListener(_ => ClampQuantityInput());

        popupUI.IncreaseQuantityButton?.onClick.AddListener(() =>
        {
            ClampQuantityInput();
            int maxAffordableQuantity = GetMaxAffordableQuantity();
            if (quantity >= maxAffordableQuantity)
            {
                SetButtonEnabled(popupUI.IncreaseQuantityButton, false);
                return;
            }

            quantity += 1;
            popupUI.QuantityInput?.SetTextWithoutNotify(quantity.ToString());
            UpdatePriceAndRemainingBiText();
        });

        popupUI.DecreaseQuantityButton?.onClick.AddListener(() =>
        {
            ClampQuantityInput();
            quantity = Mathf.Max(1, quantity - 1);
            popupUI.QuantityInput?.SetTextWithoutNotify(quantity.ToString());
            UpdatePriceAndRemainingBiText();
        });

        ClampPriceInputToUnitPrice();
        ClampQuantityInput();

        if (popupUI.CloseButton != null)
            popupUI.CloseButton.onClick.AddListener(() => HidePopup(popupInstance));

        if (popupUI.BuyButton != null)
        {
            popupUI.BuyButton.onClick.AddListener(() =>
            {
                int price = 0;
                int.TryParse(popupUI.PriceInput != null ? popupUI.PriceInput.text : "0", out price);
                int buyQuantity = 1;
                int.TryParse(popupUI.QuantityInput != null ? popupUI.QuantityInput.text : "1", out buyQuantity);
                buyQuantity = Mathf.Max(1, buyQuantity);
                if (price < minPrice || price > maxPrice)
                {
                    NotificationHelper.Instance?.ShowNotification($"Giá phải từ {minPrice:#,0} đến {maxPrice:#,0}", false);
                    return;
                }

                if (price > currentBi)
                {
                    NotificationHelper.Instance?.ShowNotification("Bạn không đủ tiền để mua.", false);
                    return;
                }

                onBuyRequest?.Invoke(price, buyQuantity);
                HidePopup(popupInstance);
            });
        }

        void HandleSelectPriceFromRow(int selectedUnitPrice)
        {
            unitPrice = Mathf.Max(1, selectedUnitPrice);
            quantity = Mathf.Clamp(quantity, 1, GetMaxAffordableQuantity());
            popupUI.QuantityInput?.SetTextWithoutNotify(quantity.ToString());
            UpdatePriceAndRemainingBiText();
        }

        StartCoroutine(LoadMarketOrderBoardRowsCoroutine(item != null ? item.itemId : 0, popupUI, HandleSelectPriceFromRow));
    }

    private IEnumerator LoadMarketOrderBoardRowsCoroutine(int itemId, MarketOrderBoardPopupUI popupUI, Action<int> onRowPriceSelected)
    {
        List<MarketOrderBoardEntry> entries = new();
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.GetMarketOrderBoardAsync(itemId),
            result => entries = result));

        if (popupUI == null || popupUI.GridContent == null || popupUI.RowPrefab == null)
            yield break;

        foreach (Transform child in popupUI.GridContent)
            Destroy(child.gameObject);

        var buyRows = (entries ?? new List<MarketOrderBoardEntry>())
            .Where(x => x != null && x.quantityBuy > 0)
            .OrderByDescending(x => x.price)
            .Take(3)
            .ToList();

        var sellRows = (entries ?? new List<MarketOrderBoardEntry>())
            .Where(x => x != null && x.quantitySell > 0)
            .OrderBy(x => x.price)
            .Take(3)
            .ToList();

        var buyColor = Color.red;
        var sellColor = Color.blue;

        for (int i = 0; i < 3; i++)
        {
            var row = Instantiate(popupUI.RowPrefab, popupUI.GridContent);
            var rowUI = row.GetComponent<MarketOrderBoardRowUI>();
            if (rowUI == null)
                continue;

            var entry = i < buyRows.Count ? buyRows[i] : null;
            if (rowUI.PriceText != null)
            {
                rowUI.PriceText.color = buyColor;
                rowUI.PriceText.text = entry != null ? entry.price.ToString("#,0") : "-";
            }

            if (rowUI.QuantityBuyText != null)
            {
                rowUI.QuantityBuyText.color = buyColor;
                rowUI.QuantityBuyText.text = entry != null && entry.quantityBuy > 0 ? entry.quantityBuy.ToString() : "-";
            }

            if (rowUI.QuantitySellText != null)
            {
                rowUI.QuantitySellText.color = buyColor;
                rowUI.QuantitySellText.text = "";
            }

            if (entry != null)
            {
                var capturedRow = row;
                var button = row.GetComponent<Button>() ?? row.AddComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    capturedRow.transform.DOKill();
                    capturedRow.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 5, 0.5f);
                    onRowPriceSelected?.Invoke(entry.price);
                });
            }
        }

        for (int i = 0; i < 3; i++)
        {
            var row = Instantiate(popupUI.RowPrefab, popupUI.GridContent);
            var rowUI = row.GetComponent<MarketOrderBoardRowUI>();
            if (rowUI == null)
                continue;

            var entry = i < sellRows.Count ? sellRows[i] : null;
            if (rowUI.PriceText != null)
            {
                rowUI.PriceText.color = sellColor;
                rowUI.PriceText.text = entry != null ? entry.price.ToString("#,0") : "-";
            }

            if (rowUI.QuantityBuyText != null)
            {
                rowUI.QuantityBuyText.color = sellColor;
                rowUI.QuantityBuyText.text = "";
            }

            if (rowUI.QuantitySellText != null)
            {
                rowUI.QuantitySellText.color = sellColor;
                rowUI.QuantitySellText.text = entry != null && entry.quantitySell > 0 ? entry.quantitySell.ToString() : "";
            }

            if (entry != null)
            {
                var capturedRow = row;
                var button = row.GetComponent<Button>() ?? row.AddComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    capturedRow.transform.DOKill();
                    capturedRow.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 5, 0.5f);
                    onRowPriceSelected?.Invoke(entry.price);
                });
            }
        }
    }
    public void CloseActivePopup()
    {
        GameObject popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            HidePopup(popupObject);
    }

    public void ShowItemInfoPopup(ItemSchema item, ItemInfoPopupTab tabType)
    {
        if (itemInfoPopupPrefab == null || item == null)
            return;

        // Ensure previous popup is closed before opening a new one
        CloseActivePopup();

        if (!TryGetCanvas(out Canvas canvas))
            return;
        GameObject popupInstance = Instantiate(itemInfoPopupPrefab, canvas.transform);

        // Create a full-screen transparent overlay that will close the popup when tapped
        GameObject overlay = new GameObject("PopupOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0);
        overlayImage.raycastTarget = true;

        Button overlayButton = overlay.GetComponent<Button>();
        overlayButton.onClick.AddListener(() => HidePopup(popupInstance));

        // Move the popup under the overlay so it renders above the overlay image
        popupInstance.transform.SetParent(overlay.transform, false);

        ItemInfoPopup popup = popupInstance.GetComponent<ItemInfoPopup>();
        if (popup == null)
            return;
        popup.BallViewPanel.SetActive(true);
        popup.CateyeModel.SetActive(item.isCateye);
        ApplySkillLevelVfx(popup, item.level);
        GameObject visual = popup.BallVissualModel;
        ItemVisualHelper.BallDamageStage damageStage = ItemVisualHelper.BallDamageStage.Pristine;
        float damagePercent = 0f;
        bool isCuliItem = item.typeGid == (int)TypeItemGid.Culi;
        if (isCuliItem)
        {
            float maxImpact = ItemVisualHelper.CalculateStatByLevel(item.ImpactResistance, item.level);
            float damageValue = Mathf.Max(item.damage, 0f);
            float remainingPercent = ItemVisualHelper.GetRemainingImpactPercent(maxImpact, damageValue);
            damageStage = ItemVisualHelper.GetDamageStage(remainingPercent);
            damagePercent = ItemVisualHelper.GetDamagePercent(maxImpact, damageValue);

            if (visual != null)
            {
                var damagedMesh = ResolveDamageBallVisualMesh(damageStage);
                if (damageStage != ItemVisualHelper.BallDamageStage.Pristine && damagedMesh != null)
                {
                    ApplyPopupBallMesh(popup, damagedMesh);
                }

                if (ballMaterialLoadRoutine != null)
                {
                    StopCoroutine(ballMaterialLoadRoutine);
                    ballMaterialLoadRoutine = null;
                }

                ballMaterialLoadRoutine = StartCoroutine(LoadBallMaterialForVisual(visual, item.id));
            }

            if (item.isCateye && popup.CateyeModel != null)
            {
                if (cateyeMaterialLoadRoutine != null)
                {
                    StopCoroutine(cateyeMaterialLoadRoutine);
                    cateyeMaterialLoadRoutine = null;
                }
                cateyeMaterialLoadRoutine = StartCoroutine(LoadCateyeMaterialForVisual(popup.CateyeModel, item.id));
            }
        }

        void SetButtonActive(Button button, bool isActive)
        {
            if (button != null)
            {
                button.gameObject.SetActive(isActive);
            }
        }

        void SetSliderActive(Slider slider, bool isActive)
        {
            if (slider != null)
            {
                slider.gameObject.SetActive(isActive);
            }
        }

        void ConfigureActiveSkillUi(ItemInfoPopup targetPopup, ItemSchema targetItem, bool isCuli)
        {
            if (targetPopup == null || targetPopup.activeSkillIconImage == null)
                return;

            bool hasSkill = isCuli && targetItem != null && targetItem.activeSkill != null && targetItem.activeSkill.GenCode > 0;
            SetActiveSkillControls(targetPopup, false);
            if (targetPopup.activeSkillMiniPopup != null)
                targetPopup.activeSkillMiniPopup.SetActive(false);

            if (!hasSkill)
                return;

            int skillGenCode = targetItem.activeSkill.GenCode;
            string skillIconPath = $"{AddressablePaths.Root}/Skills/Ball/{skillGenCode}.png";
            Sprite skillIcon = ResolveActiveSkillIconSprite(skillGenCode, skillIconPath);
            if (skillIcon != null)
            {
                ApplyActiveSkillIcon(targetPopup, skillIcon);
                SetActiveSkillControls(targetPopup, true);
            }
            else
            {
                targetPopup.activeSkillIconImage.sprite = null;
                targetPopup.activeSkillIconImage.color = Color.clear;
                StartCoroutine(AddressablesHelper.LoadSprite(skillIconPath, sprite =>
                {
                    if (targetPopup == null || targetPopup.activeSkillIconImage == null || sprite == null)
                        return;

                    activeSkillIconCache[skillGenCode] = sprite;
                    ApplyActiveSkillIcon(targetPopup, sprite);
                    SetActiveSkillControls(targetPopup, true);
                }));
            }

            TMP_Text skillDescriptionText = ResolveActiveSkillDescriptionText(targetPopup);
            if (skillDescriptionText != null)
            {
                string descriptionKey = targetItem.activeSkill.description;
                string localizedDescription = !string.IsNullOrWhiteSpace(descriptionKey)
                    ? LocalizationManager.Instance.GetText(descriptionKey)
                    : string.Empty;

                skillDescriptionText.text = ItemVisualHelper.ConvertSimpleHtmlToTmp(localizedDescription);
                skillDescriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(localizedDescription));
            }

            if (targetPopup.activeSkillIconButton != null)
            {
                targetPopup.activeSkillIconButton.onClick.RemoveAllListeners();
                targetPopup.activeSkillIconButton.onClick.AddListener(() =>
                {
                    if (targetPopup.activeSkillMiniPopup != null)
                    {
                        targetPopup.activeSkillMiniPopup.SetActive(!targetPopup.activeSkillMiniPopup.activeSelf);
                    }
                });
            }
        }

        void SetActiveSkillControls(ItemInfoPopup targetPopup, bool isActive)
        {
            if (targetPopup == null)
                return;

            if (targetPopup.activeSkillIconButton != null)
                targetPopup.activeSkillIconButton.gameObject.SetActive(isActive);
            if (targetPopup.activeSkillIconImage != null)
                targetPopup.activeSkillIconImage.gameObject.SetActive(isActive);
        }

        Sprite ResolveActiveSkillIconSprite(int skillGenCode, string skillIconPath)
        {
            if (activeSkillIconCache.TryGetValue(skillGenCode, out Sprite cachedSprite) && cachedSprite != null)
                return cachedSprite;

            try
            {
                Sprite sprite = AddressablesHelper.LoadAssetSync<Sprite>(skillIconPath);
                if (sprite != null)
                    activeSkillIconCache[skillGenCode] = sprite;

                return sprite;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to synchronously load active skill icon '{skillIconPath}': {ex.Message}");
                return null;
            }
        }

        void ApplyActiveSkillIcon(ItemInfoPopup targetPopup, Sprite sprite)
        {
            if (targetPopup == null || targetPopup.activeSkillIconImage == null || sprite == null)
                return;

            targetPopup.activeSkillIconImage.sprite = sprite;
            targetPopup.activeSkillIconImage.color = Color.white;
        }

        TMP_Text ResolveActiveSkillDescriptionText(ItemInfoPopup targetPopup)
        {
            if (targetPopup == null)
                return null;

            if (targetPopup.activeSkillDescriptionText != null)
                return targetPopup.activeSkillDescriptionText;

            if (targetPopup.activeSkillMiniPopup == null)
                return null;

            return targetPopup.activeSkillMiniPopup
                .GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(t => string.Equals(t.name, "textSkillDescription", StringComparison.OrdinalIgnoreCase));
        }

        void ConfigurePopupButton(Button button, bool isActive, Action onClick = null, string label = null)
        {
            if (button == null)
                return;

            button.gameObject.SetActive(isActive);
            button.onClick.RemoveAllListeners();
            if (isActive && onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                var labelText = button.GetComponentInChildren<TMP_Text>();
                if (labelText != null)
                {
                    labelText.text = label;
                }
            }
        }

        void ApplySkillLevelVfx(ItemInfoPopup targetPopup, int level)
        {
            if (targetPopup == null || targetPopup.skillLevel10Vfx == null)
                return;

            if (!GameInitializer.TryGetSkillLevelVfxColor(level, out var vfxColor))
            {
                targetPopup.skillLevel10Vfx.SetActive(false);
                return;
            }

            targetPopup.skillLevel10Vfx.SetActive(true);
            var particleSystems = targetPopup.skillLevel10Vfx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particleSystem in particleSystems)
            {
                var main = particleSystem.main;
                main.startColor = vfxColor;
            }
        }

        void SetButtonInteractable(Button button, bool isInteractable, float disabledAlpha = 0.5f)
        {
            if (button == null)
                return;

            button.interactable = isInteractable;
            var canvasGroup = button.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = button.gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = isInteractable ? 1f : disabledAlpha;
        }

        void ConfigureShardRewardText(bool isActive, int requiredShard, int currentShard)
        {
            if (popup.shardRewardText == null)
                return;

            popup.shardRewardText.gameObject.SetActive(isActive);
            if (!isActive)
                return;

            popup.shardRewardText.text = requiredShard.ToString();
            popup.shardRewardText.color = requiredShard <= currentShard ? Color.green : Color.red;
        }

        int shardReward = isCuliItem ? ItemVisualHelper.CalculateBallDismantleShardReward(item.level, item.rarityGid, damagePercent) : 0;
        int repairCost = isCuliItem ? ItemVisualHelper.CalculateBallRepairShardCost(item.level, item.rarityGid) : 0;
        int availableShard = InventoryController.Instance != null && InventoryController.Instance.CurrentInventory != null
            ? InventoryController.Instance.CurrentInventory.GlassShard
            : UserInfoHandler.Instance?.PlayerInventory?.GlassShard ?? 0;
        bool canUseCuliActions = isCuliItem && item.IsSolded != StatusSold.Sale;
        bool isDamaged = isCuliItem && item.damage > 0f;
        bool canRepair = canUseCuliActions && isDamaged && availableShard >= repairCost;

        if (popup.shardRepairContainer != null)
        {
            popup.shardRepairContainer.SetActive(isDamaged);
        }

        switch (tabType)
        {
            case ItemInfoPopupTab.Inventory:
                SetButtonActive(popup.unequipButton, false);
                SetButtonActive(popup.unsaleButton, false);

                if (isCuliItem)
                {
                    if (item.IsSolded == StatusSold.Sale)
                    {
                        SetButtonActive(popup.soldButton, false);
                        SetButtonActive(popup.equipButton, false);
                        SetButtonActive(popup.unsaleButton, true);
                        popup.unsaleButton.onClick.AddListener(() =>
                        {
                            HidePopup(popupInstance);
                            InventoryController.Instance.onClickCancelSell(item);
                        });
                    }
                    else
                    {
                        SetButtonActive(popup.unsaleButton, false);
                        if (popup.equipButton != null)
                        {
                            SetButtonActive(popup.equipButton, true);
                            popup.equipButton.onClick.AddListener(() =>
                            {
                                HidePopup(popupInstance);
                                InventoryController.Instance.onClickEquip();
                            });
                        }

                        if (popup.soldButton != null)
                        {
                            SetButtonActive(popup.soldButton, true);
                            popup.soldButton.onClick.AddListener(() =>
                            {
                                HidePopup(popupInstance);
                                InventoryController.Instance.onClickSellMarket(item);
                            });
                        }
                    }

                }
                else
                {
                    SetButtonActive(popup.equipButton, false);
                    SetButtonActive(popup.soldButton, false);
                }

                ConfigurePopupButton(popup.dismantleButton, canUseCuliActions, () =>
                {
                    HidePopup(popupInstance);
                    ShowPopup($"Bạn có muốn phân rã item này để nhận : {shardReward} mảnh vỡ thủy tinh?", () =>
                    {
                        InventoryController.Instance?.OnClickDismantleItem(item);
                    }, true);
                }, "Phân rã");

                ConfigurePopupButton(popup.repairButton, canUseCuliActions, canRepair ? () =>
                {
                    HidePopup(popupInstance);
                    ShowPopup($"Bạn cần tốn {repairCost} mảnh bi để sửa chữa toàn bộ.", () =>
                    {
                        InventoryController.Instance?.OnClickRepairItem(item);
                    }, true);
                } : null, $"Sửa chữa ({repairCost})");
                SetButtonInteractable(popup.repairButton, canRepair);
                ConfigureShardRewardText(canUseCuliActions, repairCost, availableShard);

                break;
            case ItemInfoPopupTab.Market:
            case ItemInfoPopupTab.CompanionSelection:
                SetButtonActive(popup.unsaleButton, false);
                SetButtonActive(popup.soldButton, false);
                SetButtonActive(popup.equipButton, false);
                SetButtonActive(popup.unequipButton, false);
                SetButtonActive(popup.dismantleButton, false);
                SetButtonActive(popup.repairButton, false);
                ConfigureShardRewardText(false, 0, 0);

                break;
            case ItemInfoPopupTab.Equipped:
                SetButtonActive(popup.unsaleButton, false);
                SetButtonActive(popup.soldButton, false);
                SetButtonActive(popup.equipButton, false);
                SetButtonActive(popup.unequipButton, true);
                SetButtonActive(popup.dismantleButton, false);
                ConfigurePopupButton(popup.repairButton, canUseCuliActions, canRepair ? () =>
                {
                    HidePopup(popupInstance);
                    ShowPopup($"Bạn cần tốn {repairCost} mảnh bi để sửa chữa toàn bộ.", () =>
                    {
                        InventoryController.Instance?.OnClickRepairItem(item);
                    }, true);
                } : null, $"Sửa chữa ({repairCost})");
                SetButtonInteractable(popup.repairButton, canRepair);
                ConfigureShardRewardText(canUseCuliActions, repairCost, availableShard);
                popup.unequipButton.onClick.RemoveAllListeners();
                popup.unequipButton.onClick.AddListener(() =>
                {
                    HidePopup(popupInstance);
                    InventoryController.Instance.onClickUnEquip(item.id);
                });

                break;
            case ItemInfoPopupTab.OnlyView:
                SetButtonActive(popup.unsaleButton, false);
                SetButtonActive(popup.soldButton, false);
                SetButtonActive(popup.equipButton, false);
                SetButtonActive(popup.unequipButton, false);
                SetButtonActive(popup.dismantleButton, false);
                SetButtonActive(popup.repairButton, false);
                ConfigureShardRewardText(false, 0, 0);

                break;
            default:
                SetButtonActive(popup.unsaleButton, false);
                SetButtonActive(popup.soldButton, false);
                SetButtonActive(popup.equipButton, false);
                SetButtonActive(popup.unequipButton, false);
                SetButtonActive(popup.dismantleButton, false);
                SetButtonActive(popup.repairButton, false);
                ConfigureShardRewardText(false, 0, 0);
                break;
        }

        bool isNormalItem = item.typeGid != (int)TypeItemGid.Culi;
        ConfigureActiveSkillUi(popup, item, isCuliItem);
        popup.closeButton?.onClick.AddListener(() => HidePopup(popupInstance));
        if (popup.nameText != null)
            popup.nameText.text = LocalizationManager.Instance.GetText(item.name);
        if (popup.rarityText != null)
        {
            string rarityKey = Enum.GetName(typeof(ItemRarity), item.rarityGid);
            popup.rarityText.gameObject.SetActive(true);
            popup.rarityText.color = ItemVisualHelper.GetRarityColor(item.rarityGid);
            popup.rarityText.text = string.IsNullOrEmpty(rarityKey)
                ? string.Empty
                : LocalizationManager.Instance.GetText(rarityKey);
        }

        if (isNormalItem)
        {
            popup.BallViewPanel.SetActive(false);
            if (popup.itemInfoGroup != null)
                popup.itemInfoGroup.SetActive(true);
            if (popup.ballInfoGroup != null)
                popup.ballInfoGroup.SetActive(false);
            if (popup.descriptionTextForItem != null)
            {
                popup.descriptionTextForItem.gameObject.SetActive(true);
                string localizedText = LocalizationManager.Instance.GetText(item.description);
                popup.descriptionTextForItem.text = ItemVisualHelper.ConvertSimpleHtmlToTmp(localizedText);
            }
            if (popup.RawImage != null)
                popup.RawImage.gameObject.SetActive(false);
            if (popup.descriptionTextForBall != null)
                popup.descriptionTextForBall.gameObject.SetActive(false);
            if (popup.levelText != null)
                popup.levelText.gameObject.SetActive(false);
            if (popup.statText != null)
                popup.statText.gameObject.SetActive(false);
            SetSliderActive(popup.massSlider, false);
            SetSliderActive(popup.speedSlider, false);
            SetSliderActive(popup.bounceSlider, false);
            SetSliderActive(popup.impactSlider, false);
            SetSliderActive(popup.damageSlider, false);
            if (popup.equipButton != null)
                popup.equipButton.gameObject.SetActive(false);
            if (popup.unequipButton != null)
                popup.unequipButton.gameObject.SetActive(false);
            if (popup.soldButton != null)
                popup.soldButton.gameObject.SetActive(false);
            if (popup.unsaleButton != null)
                popup.unsaleButton.gameObject.SetActive(false);
            if (popup.closeButton != null)
                popup.closeButton.gameObject.SetActive(false);
        }
        else
        {
            if (popup.ballInfoGroup != null)
                popup.ballInfoGroup.SetActive(true);
            if (popup.itemInfoGroup != null)
                popup.itemInfoGroup.SetActive(false);
            if (popup.descriptionTextForBall != null)
            {
                popup.descriptionTextForBall.gameObject.SetActive(true);
                string localizedText = LocalizationManager.Instance.GetText(item.description);
                popup.descriptionTextForBall.text = ItemVisualHelper.ConvertSimpleHtmlToTmp(localizedText);
            }
            if (popup.descriptionTextForItem != null)
                popup.descriptionTextForItem.gameObject.SetActive(false);
            if (popup.levelText != null)
                popup.levelText.text = $"{LocalizationManager.Instance.GetText("level")}: {item.level}";

            float mass = ItemVisualHelper.CalculateStatByLevel(item.Mass, item.level);
            float speed = ItemVisualHelper.CalculateDragByLevel(item.Mass, item.GravityScale, item.Drag, item.Bounciness, item.Elasticity, item.ImpactResistance, item.level);
            float bounce = ItemVisualHelper.CalculateStatByLevel(item.Bounciness, item.level);
            float impact = ItemVisualHelper.CalculateStatByLevel(item.ImpactResistance, item.level);

            if (popup.statText != null)
            {
                popup.statText.gameObject.SetActive(true);
                popup.statText.richText = true;
                float damageInfoPercent = item.typeGid == (int)TypeItemGid.Culi ? damagePercent : -1f;
                popup.statText.text = ItemVisualHelper.BuildScaledStatInfo(mass, speed, bounce, impact, damageInfoPercent);
            }
            SetSliderActive(popup.massSlider, false);
            SetSliderActive(popup.speedSlider, false);
            SetSliderActive(popup.bounceSlider, false);
            SetSliderActive(popup.impactSlider, false);
            SetSliderActive(popup.damageSlider, false);
        }    

        Mesh ResolveDamageBallVisualMesh(ItemVisualHelper.BallDamageStage stage)
        {
            if (GameInitializer.Instance == null)
                return null;

            switch (stage)
            {
                case ItemVisualHelper.BallDamageStage.Chipped:
                    return GameInitializer.Instance.BallModelVisualChipped;
                case ItemVisualHelper.BallDamageStage.Cracked:
                    return GameInitializer.Instance.BallModelVisualCracked;
                case ItemVisualHelper.BallDamageStage.Shattered:
                    return GameInitializer.Instance.BallModelVisualShattered;
                default:
                    return null;
            }
        }

        void ApplyPopupBallMesh(ItemInfoPopup targetPopup, Mesh mesh)
        {
            if (targetPopup == null || mesh == null || targetPopup.BallVissualModel == null)
                return;

            var meshFilter = targetPopup.BallVissualModel.GetComponentInChildren<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = mesh;
                return;
            }

            var skinnedMesh = targetPopup.BallVissualModel.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinnedMesh != null)
            {
                skinnedMesh.sharedMesh = mesh;
            }
        }
    }

    public void ShowRewardReveal(RewardClaimResponse modelData)
    {
        GameObject popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            return;

        if (!TryGetCanvas(out Canvas canvas))
            return;

        GameObject popupInstance = Instantiate(rewardRevealPopupPrefab, canvas.transform);

        Image rewardImage = popupInstance.transform.Find("Image").GetComponent<Image>();
        RewardRevealController controller = popupInstance.GetComponentInChildren<RewardRevealController>(true);
        if (modelData.itemId != null)
        {
           controller.rewardText.gameObject.SetActive(false);
           controller.rewardAmount.gameObject.SetActive(false);

            StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{modelData.itemId}.png", sprite =>
            {
                if (sprite != null)
                {
                    rewardImage.sprite = sprite;
                    rewardImage.gameObject.SetActive(true);
                }
                else
                {
                    rewardImage.gameObject.SetActive(false);
                    if (controller.rewardText != null)
                    {
                        controller.rewardText.gameObject.SetActive(true);
                        controller.rewardText.text = LocalizationManager.Instance.GetText("lucky_later_infor");
                    }
                }
            }));
        }
        if (modelData.rewardAmount > 0)
        {
            rewardImage.gameObject.SetActive(false);
            controller.rewardText.gameObject.SetActive(false);
            controller.rewardAmount.gameObject.SetActive(true);
            controller.AmountText.text = "+" + modelData.rewardAmount.ToString();
        }    
        else
        {
            rewardImage.gameObject.SetActive(false);
            controller.rewardAmount.gameObject.SetActive(false);
            controller.rewardAmount.gameObject.SetActive(false);
            if (controller.rewardText != null)
            {
                controller.rewardText.gameObject.SetActive(true);
                controller.rewardText.text = LocalizationManager.Instance.GetText("lucky_later_infor");
            }
        }

        Button continueButton = popupInstance.transform.Find("ContinueButton")?.GetComponent<Button>();
        if (continueButton != null)
            continueButton.onClick.AddListener(() => HidePopup(popupInstance));
    }

    public void ShowSocialLoginInfo(SocialLoginInfoData info, Action onLogout)
    {
        if (socialLoginInfoPopupPrefab == null)
        {
            Debug.LogWarning("PopupHelper: socialLoginInfoPopupPrefab is not assigned.");
            return;
        }

        CloseActivePopup();

        if (!TryGetCanvas(out Canvas canvas))
            return;

        GameObject popupInstance = Instantiate(socialLoginInfoPopupPrefab, canvas.transform);
        popupInstance.transform.localScale = Vector3.zero;
        currentSocialInfoPopup = popupInstance;

        var popupUI = popupInstance.GetComponent<SocialLoginInfoPopupUI>();
        if (popupUI == null)
        {
            Debug.LogError("PopupHelper: SocialLoginInfoPopupUI component is missing from prefab.");
            Destroy(popupInstance);
            currentSocialInfoPopup = null;
            return;
        }

        if (popupUI.DisplayNameText != null)
        {
            popupUI.DisplayNameText.text = string.IsNullOrWhiteSpace(info.DisplayName)
                ? "N/A"
                : info.DisplayName;
        }

        TMP_Text myFriendCodeText = popupUI.FriendCodeText;

        if (popupUI.FriendCodeText != null)
        {
            popupUI.FriendCodeText.text = string.IsNullOrWhiteSpace(info.FriendCode)
                ? "--"
                : info.FriendCode;
        }

        if (popupUI.CopyFriendCodeButton != null)
        {
            popupUI.CopyFriendCodeButton.onClick.RemoveAllListeners();
            popupUI.CopyFriendCodeButton.onClick.AddListener(CopyFriendCode);
        }

        if (popupUI.EmailText != null)
        {
            popupUI.EmailText.text = string.IsNullOrWhiteSpace(info.Email)
                ? "--"
                : info.Email;
        }

        if (popupUI.ProviderIcon != null)
        {
            var icon = ResolveProviderIcon(info.ProviderType);
            if (icon != null)
            {
                popupUI.ProviderIcon.sprite = icon;
                popupUI.ProviderIcon.gameObject.SetActive(true);
            }
            else
            {
                popupUI.ProviderIcon.gameObject.SetActive(false);
            }
        }

        if (popupUI.AvatarImage != null)
        {
            var avatarSprite = MenuController.Instance != null ? MenuController.Instance.PlayerAvatarSprite : null;
            if (avatarSprite != null)
            {
                popupUI.AvatarImage.sprite = avatarSprite;
                popupUI.AvatarImage.preserveAspect = true;
                popupUI.AvatarImage.gameObject.SetActive(true);
            }
            else
            {
                popupUI.AvatarImage.sprite = null;
                popupUI.AvatarImage.gameObject.SetActive(false);
            }
        }

        if (popupUI.CloseButton != null)
        {
            popupUI.CloseButton.onClick.RemoveAllListeners();
            popupUI.CloseButton.onClick.AddListener(() => HidePopup(popupInstance));
        }

        if (popupUI.LogoutButton != null)
        {
            popupUI.LogoutButton.onClick.RemoveAllListeners();
            popupUI.LogoutButton.onClick.AddListener(() =>
            {
                ShowPopup("Bạn có chắc muốn đăng xuất?", () =>
                {
                    HidePopup(popupInstance);
                    onLogout?.Invoke();
                }, allowStacked: true);
            });
        }

        void CopyFriendCode()
        {
            if (myFriendCodeText == null)
                return;

            GUIUtility.systemCopyBuffer = myFriendCodeText.text;

            if (NotificationHelper.Instance != null)
                NotificationHelper.Instance.ShowNotification("Friend code copied", true);
        }

        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.3f);
        }

        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
    }

    private Sprite ResolveProviderIcon(AuthenticationProviderType providerType)
    {
        switch (providerType)
        {
            case AuthenticationProviderType.GooglePlayGames:
            case AuthenticationProviderType.Google:
                return googleProviderIcon != null ? googleProviderIcon : defaultProviderIcon;
            default:
                return defaultProviderIcon;
        }
    }
    public void ShowPopupPlayer(PlayerInfoStruct playermodel)
    {
        if (currentPlayerPopup != null)
        {
            if (currentPopupPlayerId == playermodel.playerId)
            {
                HidePopup(currentPlayerPopup);
                currentPlayerPopup = null;
                currentPopupPlayerId = -1;
                return;
            }
            else
            {
                HidePopup(currentPlayerPopup);
                currentPlayerPopup = null;
                currentPopupPlayerId = -1;
            }
        }

        GameObject popupObject = GameObject.FindGameObjectWithTag("PopupUI");
        if (popupObject != null)
            return;
        if (!TryGetCanvas(out Canvas canvas))
            return;

        // Tạo Popup từ Prefab và gắn vào Canvas
        GameObject popupInstance = Instantiate(popupPlayerPrefab, canvas.transform);
        popupInstance.transform.localScale = Vector3.zero; // Bắt đầu từ scale nhỏ
        currentPlayerPopup = popupInstance;
        currentPopupPlayerId = playermodel.playerId;

        // Retrieve UI references from component instead of using transform.Find
        PopupPlayerUI popupUI = popupInstance.GetComponent<PopupPlayerUI>();
        if (popupUI == null)
        {
            Debug.LogError("PopupPlayerUI component missing from prefab");
            return;
        }

        // Gán nội dung cho popup
        popupUI.NamePlayer.text = playermodel.fullname.ToString();
        popupUI.LevelPlayer.text = playermodel.level.ToString();

        var result = GameManagerNetWork.Instance.serverRPC.GetBallPhysics(playermodel.playerId);
        if (result.HasValue)
        {
            var info = result.Value.data;
            var ballCtrl = result.Value.active;
            if (info.playerId != 0)
            {
                float mass = info.Mass;
                float speed = ItemVisualHelper.CalculateSpeedFromStats(info.Mass, info.GravityScale, info.Drag, info.Bounciness, info.Elasticity, info.ImpactResistance);
                float bounce = info.Bounciness;
                float impact = info.ImpactResistance;

                if (popupUI.InforItem != null)
                {
                    popupUI.InforItem.text = ItemVisualHelper.BuildStatInfo(mass, speed, bounce, impact);
                    ItemVisualHelper.UpdateStatSliders(popupUI.MassSlider, popupUI.DragSlider, popupUI.BounceSlider, popupUI.ImpactSlider,
                                                      mass, speed, bounce, impact);
                }

                var rawImg = popupUI.RawImage;
                if (rawImg != null && ballCtrl != null)
                {
                    StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{ballCtrl.BallMaterialId}.png", sprite =>
                    {
                        if (sprite != null)
                            rawImg.texture = sprite.texture;
                    }));
                }
                var itemImg = popupUI.ItemImage;
                if (itemImg != null && ballCtrl != null)
                {
                    StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{ballCtrl.BallMaterialId}.png", sprite =>
                    {
                        if (sprite != null)
                            itemImg.sprite = sprite;
                    }));
                }
            }
            else if (popupUI.InforItem != null)
            {
                popupUI.InforItem.text = string.Empty;
                ItemVisualHelper.UpdateStatSliders(popupUI.MassSlider, popupUI.DragSlider, popupUI.BounceSlider, popupUI.ImpactSlider, 0, 0, 0, 0);
            }
            popupUI.NameBall.text = LocalizationManager.Instance.GetText(info.name.ToString());
        }
        else if (popupUI.InforItem != null)
        {
            popupUI.InforItem.text = string.Empty;
            ItemVisualHelper.UpdateStatSliders(popupUI.MassSlider, popupUI.DragSlider, popupUI.BounceSlider, popupUI.ImpactSlider, 0, 0, 0, 0);
        }

        // Hiệu ứng xuất hiện
        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.DOFade(1f, 0.3f);
        popupInstance.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        // Gán sự kiện cho nút "Có"
        //yesButton.onClick.AddListener(() =>
        //{
        //    HidePopup(popupInstance);
        //    confirmAction?.Invoke();

        //});

        // Nút "Không" sẽ tự động đóng popup
        popupUI.CloseButton.onClick.AddListener(() =>
        {
            HidePopup(popupInstance);
            currentPlayerPopup = null;
            currentPopupPlayerId = -1;
        });
    }
    //Pupup hiển thị khiêu chiến theo bạn bè
    public void ShowIncomingChallengePopup(int challengerId, int bet, int roomId = 0)
    {
        Debug.Log($"ShowIncomingChallengePopup on main thread: {MainThreadDispatcher.IsMainThread}");
        if (!MainThreadDispatcher.IsMainThread)
        {
            Debug.LogWarning("ShowIncomingChallengePopup called from a non-main thread");
            return;
        }
        if (IncomingChallengePopupPrefab == null)
            return;

        if (!TryGetCanvas(out Canvas canvas))
            return;

        // Tạo Popup từ Prefab và gắn vào Canvas

        GameObject popup = Instantiate(IncomingChallengePopupPrefab, canvas.transform);
        var msgText = popup.transform.Find("Message").GetComponent<TMP_Text>();
        msgText.text = $"Người chơi {challengerId} khiêu chiến cược {bet/2} culi";
        Button accept = popup.transform.Find("AcceptButton").GetComponent<Button>();
        Button decline = popup.transform.Find("DeclineButton").GetComponent<Button>();
        accept.onClick.AddListener(() => { Destroy(popup); AcceptChallenge(challengerId, bet, roomId); });
        decline.onClick.AddListener(() => { Destroy(popup); RejectChallenge(challengerId); });
    }


    private void AcceptChallenge(int challengerId, int bet, int roomId)
    {
        WebSocketHelper.Instance.Send(new WebSocketHelper.ChallengeResponseMessage
        {
            type = "friend_challenge_response",
            senderId = GameManagerNetWork.Instance.loginUserModel.UserId,
            receiverId = challengerId,
            bet = bet,
            roomId = roomId,
            accepted = true
        });

        StartCoroutine(JoinInviterRoomAfterAccept(challengerId, roomId));
    }

    private void RejectChallenge(int challengerId)
    {
        WebSocketHelper.Instance.Send(new WebSocketHelper.ChallengeResponseMessage
        {
            type = "friend_challenge_response",
            senderId = GameManagerNetWork.Instance.loginUserModel.UserId,
            receiverId = challengerId,
            bet = 0,
            roomId = 0,
            accepted = false
        });
    }

    private IEnumerator JoinInviterRoomAfterAccept(int challengerId, int challengeRoomId)
    {
        int targetRoomId = challengeRoomId;

        if (targetRoomId <= 0 && APIManager.Instance != null)
        {
            var task = APIManager.Instance.GetCurrentPlayerRoomAsync(challengerId);
            PlayerRoomApiResponse inviterRoom = null;
            if (LoadingManager.Instance != null)
            {
                yield return StartCoroutine(APIManager.Instance.RunTask(
                    task,
                    result => inviterRoom = result));
            }
            else
            {
                while (!task.IsCompleted)
                    yield return null;

                inviterRoom = task.IsCompletedSuccessfully ? task.Result : null;
            }

            targetRoomId = inviterRoom?.room?.id ?? inviterRoom?.roomUser?.roomId ?? 0;
        }

        if (targetRoomId <= 0)
        {
            NotificationHelper.Instance?.ShowNotification("Không lấy được thông tin phòng để tham gia.", false);
            yield break;
        }

        if (RoomManager.Instance == null)
        {
            NotificationHelper.Instance?.ShowNotification("Không tìm thấy RoomManager để vào phòng.", false);
            yield break;
        }

        RoomManager.Instance.JoinRoom(targetRoomId);
    }

    private IEnumerator LoadCateyeMaterialForVisual(GameObject cateyeModel, int itemId)
    {
        if (cateyeModel == null)
            yield break;

        if (cateyeMaterialHandle.IsValid())
        {
            Addressables.Release(cateyeMaterialHandle);
            cateyeMaterialHandle = default;
        }

        yield return AddressablesHelper.LoadAssetWithHandle<Material>($"{AddressablePaths.Items.CuliCateye}/{itemId}.mat", (mat, handle) =>
        {
            cateyeMaterialHandle = handle;
            if (mat != null)
            {
                var renderer = cateyeModel.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = mat;
                }
            }
            else
            {
                Debug.LogWarning($"Cateye material for item {itemId} not found at {AddressablePaths.Items.CuliCateye}/{itemId}.mat");
            }
        });
    }

    private IEnumerator LoadBallMaterialForVisual(GameObject visual, int ballMaterialId)
    {
        if (visual == null)
            yield break;

        if (ballMaterialHandle.IsValid())
        {
            Addressables.Release(ballMaterialHandle);
            ballMaterialHandle = default;
        }

        yield return AddressablesHelper.LoadAssetWithHandle<Material>($"{AddressablePaths.Items.Culi}/{ballMaterialId}.mat", (mat, handle) =>
        {
            ballMaterialHandle = handle;
            if (mat != null)
            {
                var renderer = visual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = mat;
                }
            }
        });
    }
    private void HidePopup(GameObject popupInstance)
    {
        Transform parent = popupInstance.transform.parent;
        popupInstance.GetComponent<CanvasGroup>().DOFade(0f, 0.2f);
        popupInstance.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
        {
            Destroy(popupInstance);
            if (parent != null && parent.GetComponent<Button>() != null && parent.GetComponent<Image>() != null)
                Destroy(parent.gameObject);
        });
        if (popupInstance == currentPlayerPopup)
        {
            currentPlayerPopup = null;
            currentPopupPlayerId = -1;
        }
        if (popupInstance == currentSocialInfoPopup)
        {
            currentSocialInfoPopup = null;
        }
    }

    public void ClosePopup(GameObject popupInstance)
    {
        if (popupInstance == null)
            return;

        HidePopup(popupInstance);
    }

    private void OnDisable()
    {
        CloseActivePopup();
        HideCurrentNewMessageNoticePopup();
        DOTween.Kill(transform);
        if (ballMaterialLoadRoutine != null)
        {
            StopCoroutine(ballMaterialLoadRoutine);
            ballMaterialLoadRoutine = null;
        }

        if (ballMaterialHandle.IsValid())
        {
            Addressables.Release(ballMaterialHandle);
            ballMaterialHandle = default;
        }

        if (cateyeMaterialLoadRoutine != null)
        {
            StopCoroutine(cateyeMaterialLoadRoutine);
            cateyeMaterialLoadRoutine = null;
        }

        if (cateyeMaterialHandle.IsValid())
        {
            Addressables.Release(cateyeMaterialHandle);
            cateyeMaterialHandle = default;
        }
    }
}
 
