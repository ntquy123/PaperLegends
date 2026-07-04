using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Fusion.Sockets;
using Fusion;
using System;
using System.Text.RegularExpressions;
using Fusion.Photon.Realtime;
using UnityEngine.Events;

public class MenuController : MonoBehaviour
{
    public static MenuController Instance;
    private const int MarketUnlockLevel = 5;
    private const float LockedMenuButtonAlpha = 0.35f;
    private const string AutoMenuLockOverlayName = "AutoMenuLockOverlay";
    private const bool EnableCompanionSelectionDuringAccountCreation = false;
    private const int TemporaryDefaultCompanionBallItemId = 0;

    [Header("UI PANEL CONFIG")]
    [FormerlySerializedAs("MainMenu")]
    [SerializeField]
    private GameObject mainMenu;
    [FormerlySerializedAs("MapGame")]
    [SerializeField]
    private GameObject mapGame;
    [SerializeField]
    private GameObject CreateNameAccountPanel;
    [SerializeField]
    private GameObject LoginSocialPanel;

    [Header("Companion Selection")]
    [SerializeField]
    private GameObject companionSelectionPanel;

    [SerializeField]
    private Button confirmCompanionSelectionButton;

    [SerializeField]
    private GameObject companionBallOptionPrefab;

    [SerializeField]
    private Transform companionBallOptionsParent;

    [SerializeField]
    private List<CompanionBallOption> companionBallOptions = new();


    public GameObject ChacterViewPanel;


    [Header("Player Profile")]
    [SerializeField]
    private Button showProfileButton;
    [SerializeField]
    private Image playerAvatarImage;

    [Header("Menu Configuration")]
    [SerializeField]
    private List<MenuEntry> menuEntries = new();

    [Header("Menu Header")]
    [SerializeField]
    private MenuHeader menuHeader = new();
    [SerializeField]
    private GameObject menuHeaderRight;
    [Header("Social Login Buttons")]
 
    [SerializeField]
    private List<SocialLoginButtonEntry> socialLoginButtons = new();
 
    [SerializeField]
    private TMP_InputField inputField; // Ô nhập tên nhân vật
    private string playerName = "default";
    private string pendingAuthCode ="default";
    private LoginUserModel pendingSocialLoginModel;
    private int selectedCompanionBallItemId = -1;
    private Coroutine loadCompanionBallOptionsRoutine;

    private const string AccessTokenKey = "AccessToken";
    private const string RefreshTokenKey = "RefreshToken";
    private const string AccessTokenExpiryKey = "AccessTokenExpiresAt";
    private const string RefreshTokenExpiryKey = "RefreshTokenExpiresAt";

    private const int MinPlayerNameLength = 3;
    private const int MaxPlayerNameLength = 12;
    private const string LoginFailedLocalizationKey = "noti_login_failed";
    private const string LoginFailedWaitLocalizationKey = "noti_login_timeout";
    private const string LoginConflictLocalizationKey = "noti_login_conflict";

    private readonly Dictionary<MenuActionType, MenuEntry> menuLookup = new();
    private MenuEntry currentMenuEntry;
    private Coroutine effectLoadRoutine;
    private Sprite runtimeAvatarSprite;
    private Coroutine newPlayerGiftPopupRoutine;

    private UnityAction showProfileButtonCallback;
    private UnityAction headerBackButtonCallback;
    private UnityAction headerInstructionButtonCallback;
    public void TestDraw()
    {
        GameOverManager.Instance.CheckDrawLucky();
    }    
    public void SetMainMenuActive(bool isActive)
    {
        if (mainMenu == null)
        {
            return;
        }

        if (mainMenu.activeSelf != isActive)
        {
            mainMenu.SetActive(isActive);
        }
    }

    public void SetMapGameActive(bool isActive)
    {
        if (mapGame == null)
        {
            return;
        }

        if (mapGame.activeSelf != isActive)
        {
            mapGame.SetActive(isActive);
        }
    }
 
    public GameObject EffectPanel => GetPanelByAction(MenuActionType.Effect);
    public GameObject MarketPanel => GetPanelByAction(MenuActionType.Market);
    public Sprite PlayerAvatarSprite => playerAvatarImage != null ? playerAvatarImage.sprite : null;
 
    [Serializable]
    public class MenuEntry
    {
        [SerializeField]
        private MenuActionType actionType = MenuActionType.None;

        [SerializeField]
        private bool hideMainMenu = true;

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Button menuButton;

        [Header("Level Lock")]
        [SerializeField, Tooltip("Overlay hien thi icon khoa. Neu de trong se duoc tao tu dong khi menu bi khoa.")]
        private GameObject lockOverlay;
        [SerializeField, Tooltip("Text hien thi cap yeu cau. Neu de trong se tim trong overlay hoac tao tu dong.")]
        private TMP_Text requiredLevelText;

        [NonSerialized]
        private UnityAction buttonCallback;
        [NonSerialized]
        private CanvasGroup buttonCanvasGroup;
        [NonSerialized]
        private float originalButtonAlpha = 1f;
        [NonSerialized]
        private bool hasOriginalButtonAlpha;

        public MenuActionType ActionType => actionType;
        public bool HideMainMenu => hideMainMenu;
        public GameObject Panel => panel;
        public Button MenuButton => menuButton;

        public void SetupMenuButton(MenuController controller)
        {
            if (controller == null || menuButton == null)
            {
                return;
            }

            if (buttonCallback != null)
            {
                menuButton.onClick.RemoveListener(buttonCallback);
            }

            bool locked = controller.IsMenuLocked(actionType, out int requiredLevel);
            ApplyLockVisualState(locked, requiredLevel);
            buttonCallback = () => controller.ShowMenu(actionType);
            menuButton.onClick.AddListener(buttonCallback);
        }

        public void ResetButtonCallback()
        {
            if (menuButton == null || buttonCallback == null)
            {
                return;
            }

            menuButton.onClick.RemoveListener(buttonCallback);
            buttonCallback = null;
        }

        private void ApplyLockVisualState(bool locked, int requiredLevel)
        {
            if (menuButton == null)
            {
                return;
            }

            EnsureButtonCanvasGroup();
            if (buttonCanvasGroup != null)
            {
                buttonCanvasGroup.alpha = locked ? LockedMenuButtonAlpha : originalButtonAlpha;
                buttonCanvasGroup.interactable = true;
                buttonCanvasGroup.blocksRaycasts = true;
            }

            if (!locked && lockOverlay == null && !HasAutoLockOverlay())
            {
                return;
            }

            GameObject overlay = EnsureLockOverlay();
            if (overlay != null)
            {
                overlay.SetActive(locked);
                overlay.transform.SetAsLastSibling();
                SetRequiredLevelText(requiredLevel);
            }
        }

        private void EnsureButtonCanvasGroup()
        {
            if (buttonCanvasGroup != null || menuButton == null)
            {
                return;
            }

            buttonCanvasGroup = menuButton.GetComponent<CanvasGroup>();
            if (buttonCanvasGroup == null)
            {
                buttonCanvasGroup = menuButton.gameObject.AddComponent<CanvasGroup>();
            }

            if (!hasOriginalButtonAlpha)
            {
                originalButtonAlpha = buttonCanvasGroup.alpha;
                hasOriginalButtonAlpha = true;
            }
        }

        private bool HasAutoLockOverlay()
        {
            return menuButton != null
                && menuButton.transform.Find(AutoMenuLockOverlayName) != null;
        }

        private GameObject EnsureLockOverlay()
        {
            if (lockOverlay != null)
            {
                EnsureOverlayCanvasGroup(lockOverlay);
                if (requiredLevelText == null)
                {
                    requiredLevelText = lockOverlay.GetComponentInChildren<TMP_Text>(true);
                }

                return lockOverlay;
            }

            if (menuButton == null)
            {
                return null;
            }

            Transform existingOverlay = menuButton.transform.Find(AutoMenuLockOverlayName);
            if (existingOverlay != null)
            {
                lockOverlay = existingOverlay.gameObject;
                EnsureOverlayCanvasGroup(lockOverlay);
                if (requiredLevelText == null)
                {
                    requiredLevelText = lockOverlay.GetComponentInChildren<TMP_Text>(true);
                }

                return lockOverlay;
            }

            lockOverlay = new GameObject(AutoMenuLockOverlayName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            lockOverlay.transform.SetParent(menuButton.transform, false);

            RectTransform overlayRect = lockOverlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImage = lockOverlay.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.28f);
            overlayImage.raycastTarget = false;

            EnsureOverlayCanvasGroup(lockOverlay);
            CreateLockIcon(lockOverlay.transform);
            requiredLevelText = CreateRequiredLevelLabel(lockOverlay.transform);
            return lockOverlay;
        }

        private static void EnsureOverlayCanvasGroup(GameObject overlay)
        {
            if (overlay == null)
            {
                return;
            }

            CanvasGroup overlayCanvasGroup = overlay.GetComponent<CanvasGroup>();
            if (overlayCanvasGroup == null)
            {
                overlayCanvasGroup = overlay.AddComponent<CanvasGroup>();
            }

            overlayCanvasGroup.alpha = 1f;
            overlayCanvasGroup.interactable = false;
            overlayCanvasGroup.blocksRaycasts = false;
            overlayCanvasGroup.ignoreParentGroups = true;
        }

        private static void CreateLockIcon(Transform parent)
        {
            GameObject iconObject = new GameObject("LockIcon", typeof(RectTransform), typeof(TextMeshProUGUI));
            iconObject.transform.SetParent(parent, false);

            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 11f);
            iconRect.sizeDelta = new Vector2(64f, 32f);

            TMP_Text iconText = iconObject.GetComponent<TextMeshProUGUI>();
            iconText.text = "\U0001F512";
            iconText.fontSize = 24f;
            iconText.alignment = TextAlignmentOptions.Center;
            iconText.color = Color.white;
            iconText.raycastTarget = false;
        }

        private static TMP_Text CreateRequiredLevelLabel(Transform parent)
        {
            GameObject labelObject = new GameObject("RequiredLevelText", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.08f, 0.5f);
            labelRect.anchorMax = new Vector2(0.92f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, -16f);
            labelRect.sizeDelta = new Vector2(0f, 34f);

            TMP_Text labelText = labelObject.GetComponent<TextMeshProUGUI>();
            labelText.fontSize = 13f;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            labelText.enableWordWrapping = true;
            labelText.raycastTarget = false;
            return labelText;
        }

        private void SetRequiredLevelText(int requiredLevel)
        {
            if (requiredLevelText == null)
            {
                return;
            }

            requiredLevelText.text = $"Lv.{requiredLevel}";
        }
    }

    [Serializable]
    public class SocialLoginButtonEntry
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private AuthenticationProviderType providerType = AuthenticationProviderType.GooglePlayGames;

        [NonSerialized]
        private UnityAction buttonCallback;

        public void Configure(MenuController controller)
        {
            if (controller == null || button == null)
            {
                return;
            }

            if (buttonCallback != null)
            {
                button.onClick.RemoveListener(buttonCallback);
            }

            buttonCallback = () => controller.HandleSocialLogin(providerType);
            button.onClick.AddListener(buttonCallback);
        }

        public void ResetButtonCallback()
        {
            if (button == null || buttonCallback == null)
            {
                return;
            }

            button.onClick.RemoveListener(buttonCallback);
            buttonCallback = null;
        }
    }

    [Serializable]
    public class MenuHeader
    {
        [SerializeField]
        private GameObject container;

        [SerializeField]
        private Button backButton;

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private Button instructionButton;

        public GameObject Container => container;
        public Button BackButton => backButton;
        public TMP_Text TitleText => titleText;
        public Button InstructionButton => instructionButton;
    }

    [Serializable]
    public class CompanionBallOption
    {
        [SerializeField]
        private Button selectButton;

        [SerializeField]
        private Image highlightImage;

        [SerializeField]
        private Image itemImage;

        [SerializeField]
        private TMP_Text itemNameLabel;

        [SerializeField]
        private int itemId;

        [SerializeField]
        private ItemSchema itemData;

        [NonSerialized]
        private UnityAction buttonCallback;

        public int ItemId => itemId;
        public ItemSchema ItemData => itemData;
        public Image ItemImage => itemImage;
        public TMP_Text ItemNameLabel => itemNameLabel;

        public void Initialize(MenuController controller, Button button, Image highlight, Image icon, TMP_Text nameText, ItemSchema data)
        {
            selectButton = button;
            highlightImage = highlight;
            itemImage = icon;
            itemNameLabel = nameText;
            itemData = data;
            itemId = data != null ? data.id : 0;

            Configure(controller);
            SetSelected(false);

            if (itemNameLabel != null && data != null)
            {
                itemNameLabel.text = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText(data.name)
                    : data.name;
            }
        }

        public void Configure(MenuController controller)
        {
            if (controller == null || selectButton == null)
            {
                return;
            }

            if (buttonCallback != null)
            {
                selectButton.onClick.RemoveListener(buttonCallback);
            }

            buttonCallback = () =>
            {
                controller.OnCompanionBallSelected(this);
                controller.ShowCompanionBallInfo(this);
            };
            selectButton.onClick.AddListener(buttonCallback);
        }

        public void Reset()
        {
            if (selectButton != null && buttonCallback != null)
            {
                selectButton.onClick.RemoveListener(buttonCallback);
                buttonCallback = null;
            }
        }

        public void SetSelected(bool isSelected)
        {
            if (highlightImage != null)
            {
                highlightImage.enabled = isSelected;
            }
        }
    }

    [Header("Network Scene CONFIG")]
    [SerializeField]
    private string additiveNetworkSceneName;

    public void PrepareAccountCreation(string authCode)
    {
        pendingAuthCode = authCode;
        CreateNameAccountPanel.SetActive(true);
    }

    private void Awake()
    {
        Debug.Log("Đã vào Menu");
        Instance = this;
        //step hiển thị danh sách menu kèm button đính kèm
        InitializeMenuConfiguration();
        //step cài đài button phụ khác liên quan
        SetupButtons();
        InitializeSocialLoginButtons();
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }


        InitializeMenuConfiguration();
        SetupButtons();
        InitializeSocialLoginButtons();
        InitializeCompanionSelection();
        UgsToFirebaseAuth.Instance?.RefreshCurrentUserAvatar();
    }

    private void OnDestroy()
    {
        if (runtimeAvatarSprite != null)
        {
            Destroy(runtimeAvatarSprite);
            runtimeAvatarSprite = null;
        }

        if (playerAvatarImage != null)
        {
            playerAvatarImage.sprite = null;
        }
    }

    private void OnDisable()
    {
        menuLookup.Clear();
        currentMenuEntry = null;
        StopEffectRoutine();
        StopNewPlayerGiftPopupRoutine();

        if (menuEntries != null)
        {
            foreach (var entry in menuEntries)
            {
                entry?.ResetButtonCallback();
            }
        }

        ResetSocialLoginButtons();
        ResetCompanionSelectionButtons();
        ResetShowProfileButton();
        ResetHeaderButtons();
    }

 

    private void InitializeMenuConfiguration()
    {
        menuLookup.Clear();

        if (menuEntries == null)
        {
            return;
        }

        foreach (var entry in menuEntries)
        {
            if (entry == null)
            {
                continue;
            }

            if (entry.ActionType == MenuActionType.None)
            {
                Debug.LogWarning("Menu entry configured without a valid action type.");
                continue;
            }

            if (entry.Panel == null)
            {
                Debug.LogWarning($"Menu entry '{entry.ActionType}' is missing an assigned panel.");
                continue;
            }

            if (menuLookup.ContainsKey(entry.ActionType))
            {
                Debug.LogWarning($"Duplicate menu configuration detected for '{entry.ActionType}'. Only the first entry will be used.");
                continue;
            }

            menuLookup.Add(entry.ActionType, entry);
        }
    }

    private void InitializeSocialLoginButtons()
    {
        if (socialLoginButtons == null)
        {
            return;
        }

        foreach (var entry in socialLoginButtons)
        {
            entry?.Configure(this);
        }
    }

    private void InitializeCompanionSelection()
    {
        if (confirmCompanionSelectionButton != null)
        {
            confirmCompanionSelectionButton.onClick.RemoveListener(OnConfirmCompanionSelection);
            confirmCompanionSelectionButton.onClick.AddListener(OnConfirmCompanionSelection);
        }

        StopCompanionBallOptionsRoutine();
        loadCompanionBallOptionsRoutine = StartCoroutine(LoadCompanionBallOptionsCoroutine());

        if (companionSelectionPanel != null)
        {
            companionSelectionPanel.SetActive(false);
        }
    }

    private void ResetSocialLoginButtons()
    {
        if (socialLoginButtons == null)
        {
            return;
        }

        foreach (var entry in socialLoginButtons)
        {
            entry?.ResetButtonCallback();
        }
    }

    private void ResetCompanionSelectionButtons()
    {
        StopCompanionBallOptionsRoutine();

        if (companionBallOptions != null)
        {
            foreach (var option in companionBallOptions)
            {
                option?.Reset();
            }
            companionBallOptions.Clear();
        }

        if (companionBallOptionsParent != null)
        {
            foreach (Transform child in companionBallOptionsParent)
            {
                Destroy(child.gameObject);
            }
        }

        if (confirmCompanionSelectionButton != null)
        {
            confirmCompanionSelectionButton.onClick.RemoveListener(OnConfirmCompanionSelection);
        }
    }

    private void SetupButtons()
    {
        SetupShowProfileButton();
        SetupHeaderButtons();
        if (menuEntries == null)
        {
            return;
        }

        foreach (var entry in menuEntries)
        {
            entry?.SetupMenuButton(this);
        }
    }

    private void RefreshMenuLockStates()
    {
        if (menuEntries == null)
        {
            return;
        }

        foreach (var entry in menuEntries)
        {
            entry?.SetupMenuButton(this);
        }
    }

    private bool IsMenuLocked(MenuActionType actionType, out int requiredLevel)
    {
        requiredLevel = GetRequiredMenuLevel(actionType);
        return requiredLevel > 0 && GetCurrentPlayerLevel() < requiredLevel;
    }

    private static int GetRequiredMenuLevel(MenuActionType actionType)
    {
        switch (actionType)
        {
            case MenuActionType.Market:
                return MarketUnlockLevel;
            default:
                return 0;
        }
    }

    private static int GetCurrentPlayerLevel()
    {
        LoginUserModel loginModel = GameManagerNetWork.Instance != null
            ? GameManagerNetWork.Instance.loginUserModel
            : null;
        return loginModel != null ? Mathf.Max(0, loginModel.Level) : 0;
    }

    private void HandleLockedMenuClick(MenuActionType actionType, int requiredLevel)
    {
        string message = $"Lv.{requiredLevel}";
        if (NotificationHelper.Instance != null)
        {
            NotificationHelper.Instance.ShowNotification(message, false);
        }
        else
        {
            Debug.LogWarning($"Menu '{actionType}' is locked. Required level: {requiredLevel}.");
        }
    }

    private void SetupShowProfileButton()
    {
        if (showProfileButton == null)
        {
            return;
        }

        if (showProfileButtonCallback != null)
        {
            showProfileButton.onClick.RemoveListener(showProfileButtonCallback);
        }

        showProfileButtonCallback = OnShowProfileButtonClicked;
        showProfileButton.onClick.AddListener(showProfileButtonCallback);
    }

    private void SetupHeaderButtons()
    {
        SetHeaderBackButtonCallback(OnHeaderBackButtonClicked);
    }

    private void ResetShowProfileButton()
    {
        if (showProfileButton == null || showProfileButtonCallback == null)
        {
            return;
        }

        showProfileButton.onClick.RemoveListener(showProfileButtonCallback);
        showProfileButtonCallback = null;
    }

    private void ResetHeaderButtons()
    {
        if (menuHeader != null)
        {
            if (menuHeader.BackButton != null && headerBackButtonCallback != null)
            {
                menuHeader.BackButton.onClick.RemoveListener(headerBackButtonCallback);
            }

            if (menuHeader.InstructionButton != null && headerInstructionButtonCallback != null)
            {
                menuHeader.InstructionButton.onClick.RemoveListener(headerInstructionButtonCallback);
            }
        }

        headerBackButtonCallback = null;
        headerInstructionButtonCallback = null;
    }

    private void SetHeaderBackButtonCallback(UnityAction callback)
    {
        if (menuHeader == null || menuHeader.BackButton == null)
        {
            return;
        }

        if (headerBackButtonCallback != null)
        {
            menuHeader.BackButton.onClick.RemoveListener(headerBackButtonCallback);
        }

        headerBackButtonCallback = callback;

        if (headerBackButtonCallback != null)
        {
            menuHeader.BackButton.onClick.AddListener(headerBackButtonCallback);
        }
    }

    public void ShowRoomHeader(string roomName, UnityAction backAction)
    {
        if (menuHeader == null)
        {
            return;
        }

        SetHeaderActive(true);
        SetMenuHeaderRightActive(false);

        if (menuHeader.TitleText != null)
        {
            menuHeader.TitleText.text = roomName ?? string.Empty;
        }

        SetHeaderBackButtonCallback(backAction ?? OnHeaderBackButtonClicked);

        if (menuHeader.InstructionButton != null)
        {
            menuHeader.InstructionButton.gameObject.SetActive(false);
        }
    }

    public void ResetRoomHeader()
    {
        if (menuHeader == null)
        {
            return;
        }

        if (menuHeader.InstructionButton != null)
        {
            menuHeader.InstructionButton.gameObject.SetActive(true);
        }

        if (currentMenuEntry != null)
        {
            UpdateHeaderForMenu(currentMenuEntry.ActionType);
        }
        else
        {
            SetHeaderBackButtonCallback(OnHeaderBackButtonClicked);
            SetHeaderActive(false);
        }
    }

    private void OnShowProfileButtonClicked()
    {
        UgsToFirebaseAuth.Instance?.ShowLoggedInSocialUserInfo();
    }

    public void HandleSocialLogin(AuthenticationProviderType providerType)
    {
        UgsToFirebaseAuth.Instance.HandleSocialLogin(providerType);
    }

    public void OnCompanionBallSelected(CompanionBallOption option)
    {
        if (option == null)
        {
            return;
        }

        selectedCompanionBallItemId = option.ItemId;

        if (companionBallOptions != null)
        {
            foreach (var entry in companionBallOptions)
            {
                entry?.SetSelected(entry == option);
            }
        }
    }

    public void ShowCompanionBallInfo(CompanionBallOption option)
    {
        if (option == null)
        {
            return;
        }

        if (option.ItemData == null)
        {
            Debug.LogWarning("Companion ball option is missing item data for the info popup.");
            return;
        }

        PopupHelper.Instance?.ShowItemInfoPopup(option.ItemData, ItemInfoPopupTab.CompanionSelection);
    }

    private void StopCompanionBallOptionsRoutine()
    {
        if (loadCompanionBallOptionsRoutine != null)
        {
            StopCoroutine(loadCompanionBallOptionsRoutine);
            loadCompanionBallOptionsRoutine = null;
        }
    }

    private IEnumerator LoadCompanionBallOptionsCoroutine()
    {
        if (APIManager.Instance == null)
        {
            Debug.LogWarning("APIManager is not ready to load companion ball options.");
            yield break;
        }

        List<ItemSchema> companionItems = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetItemsAsync((int)LocationItemGid.CompanionBall),
            result => companionItems = result));

        PopulateCompanionBallOptions(companionItems);
        loadCompanionBallOptionsRoutine = null;
    }

    private void PopulateCompanionBallOptions(List<ItemSchema> items)
    {
        if (items == null || items.Count == 0)
        {
            Debug.LogWarning("No companion ball items received from API.");
            return;
        }

        if (companionBallOptionsParent == null && companionSelectionPanel != null)
        {
            companionBallOptionsParent = companionSelectionPanel.transform;
        }

        if (companionBallOptionPrefab == null || companionBallOptionsParent == null)
        {
            Debug.LogWarning("Companion ball option prefab or parent is not configured.");
            return;
        }

        foreach (Transform child in companionBallOptionsParent)
        {
            Destroy(child.gameObject);
        }

        companionBallOptions.Clear();

        foreach (var item in items)
        {
            if (item == null)
            {
                continue;
            }

            var optionObject = Instantiate(companionBallOptionPrefab, companionBallOptionsParent);
            var optionView = optionObject.GetComponent<CompanionBallOptionView>();
            var option = new CompanionBallOption();
            option.Initialize(
                this,
                optionView != null ? optionView.SelectButton : optionObject.GetComponent<Button>(),
                optionView != null ? optionView.HighlightImage : null,
                optionView != null ? optionView.ItemImage : null,
                optionView != null ? optionView.ItemNameText : null,
                item);

            companionBallOptions.Add(option);

            if (option.ItemImage != null)
            {
                StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{item.id}.png", sprite =>
                {
                    if (sprite != null)
                    {
                        option.ItemImage.sprite = sprite;
                    }
                }));
            }
        }

        if (selectedCompanionBallItemId > 0)
        {
            foreach (var option in companionBallOptions)
            {
                option?.SetSelected(option.ItemId == selectedCompanionBallItemId);
            }
        }
    }

    private void OnConfirmCompanionSelection()
    {
        if (selectedCompanionBallItemId <= 0)
        {
            Debug.LogWarning("Vui lòng chọn viên bi đồng hành trước khi tiếp tục.");
            return;
        }

        PlayerPrefs.SetInt("SelectedCompanionBallItemId", selectedCompanionBallItemId);
        PlayerPrefs.Save();

        ShowPlayerNamePanel();
    }

    public void SetPlayerAvatarTexture(Texture2D texture)
    {
        if (playerAvatarImage == null)
        {
            Debug.LogWarning("Player avatar image reference is missing in MenuController.");
            return;
        }

        if (runtimeAvatarSprite != null)
        {
            Destroy(runtimeAvatarSprite);
            runtimeAvatarSprite = null;
        }

        if (texture == null)
        {
            playerAvatarImage.sprite = null;
            return;
        }

        var rect = new Rect(0, 0, texture.width, texture.height);
        runtimeAvatarSprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f));
        playerAvatarImage.sprite = runtimeAvatarSprite;
        playerAvatarImage.preserveAspect = true;
    }

    public void ClearPlayerAvatar()
    {
        SetPlayerAvatarTexture(null);
    }

    private void EnsureMenuConfigurationCached()
    {
        if (menuLookup.Count == 0 && menuEntries != null && menuEntries.Count > 0)
        {
            InitializeMenuConfiguration();
            SetupButtons();
        }
    }

    private void StopEffectRoutine()
    {
        if (effectLoadRoutine != null)
        {
            StopCoroutine(effectLoadRoutine);
            effectLoadRoutine = null;
        }
    }

    private GameObject GetPanelByAction(MenuActionType actionType)
    {
        EnsureMenuConfigurationCached();
        return menuLookup.TryGetValue(actionType, out var entry) ? entry.Panel : null;
    }

    public GameObject GetMenuPanel(MenuActionType actionType)
    {
        EnsureMenuConfigurationCached();
        return menuLookup.TryGetValue(actionType, out var entry) ? entry.Panel : null;
    }

    public void ShowMenu(MenuActionType actionType)
    {
        EnsureMenuConfigurationCached();

        if (!menuLookup.TryGetValue(actionType, out var entry) || entry == null)
        {
            Debug.LogWarning($"Menu '{actionType}' is not configured in MenuController.");
            return;
        }

        ShowMenu(entry);
    }

    private void ShowMenu(MenuEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        if (IsMenuLocked(entry.ActionType, out int requiredLevel))
        {
            RefreshMenuLockStates();
            HandleLockedMenuClick(entry.ActionType, requiredLevel);
            return;
        }

        if (currentMenuEntry == entry)
        {
            if (entry.Panel != null && !entry.Panel.activeSelf)
            {
                entry.Panel.SetActive(true);
            }

            HandleMenuSpecificLogic(entry);
            UpdateHeaderForMenu(entry.ActionType);
            return;
        }

        HideAllMenuPanels();

        if (entry.HideMainMenu)
        {
            SetMainMenuActive(false);
        }
        else
        {
            SetMainMenuActive(true);
        }

        if (entry.Panel != null)
        {
            entry.Panel.SetActive(true);
        }

        currentMenuEntry = entry;
        HandleMenuSpecificLogic(entry);
        UpdateHeaderForMenu(entry.ActionType);
    }

    private void HideAllMenuPanels()
    {
        if (menuEntries == null)
        {
            return;
        }

        foreach (var entry in menuEntries)
        {
            if (entry?.Panel != null && entry.Panel.activeSelf)
            {
                entry.Panel.SetActive(false);
            }
        }

        StopEffectRoutine();
        currentMenuEntry = null;
        SetHeaderActive(false);
    }

    private void HandleMenuSpecificLogic(MenuEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        switch (entry.ActionType)
        {
            case MenuActionType.Effect:
                StopEffectRoutine();
                if (EffectPlayerController.Instance != null)
                {
                    effectLoadRoutine = StartCoroutine(EffectPlayerController.Instance.GetlistEffect());
                }
                break;
            case MenuActionType.Shop:
                ShopController.Instance?.ShowShopList();
                break;
            case MenuActionType.Market:
                MarketController.Instance?.ShowMarketList();
                break;
            case MenuActionType.Rule:
                break;
            case MenuActionType.Settings:
                break;
            case MenuActionType.Friends:
                FriendController.Instance?.LoadFriendList();
                FriendController.Instance?.LoadFriendRequests();
                break;
            case MenuActionType.Messages:
                FriendMessageUIController.Instance?.LoadMessages();
                FriendMessageUIController.Instance?.LoadFriendList();
                FriendMessageUIController.Instance?.LoadSystemMessages();
                break;
            case MenuActionType.RewardDailyLogin:
                DailyLoginRewardsManager.Instance?.RefreshRewards();
                break;
            case MenuActionType.Inventory:
                ChacterViewPanel.SetActive(true);
                InventoryController.Instance?.ShowInventoryList();
                break;
            case MenuActionType.MenuRoom:
                RoomManager.Instance?.LoadRooms();
                break;
            case MenuActionType.Uplevel:
                BallUpgradeController.Instance?.OnLoadTab();
                break;
            case MenuActionType.QuickMatch:
                PlayerRankHistoryController.Instance?.RefreshPlayerRankStats();
                QuickMatchClient.Instance?.UpdateStartSearchAvailability(showNotification: false);
                break;
        }
    }

    private void UpdateHeaderForMenu(MenuActionType actionType)
    {
        SetMenuHeaderRightForMenu(actionType);

        if (menuHeader == null)
        {
            return;
        }

        SetHeaderActive(true);
        UpdateHeaderTitle(actionType);
        UpdateHeaderInstructionButton(actionType);
    }

    private void UpdateHeaderTitle(MenuActionType actionType)
    {
        if (menuHeader.TitleText == null)
        {
            return;
        }

        string titleKey = actionType.ToString();
        menuHeader.TitleText.text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(titleKey)
            : titleKey;
    }

    private void UpdateHeaderInstructionButton(MenuActionType actionType)
    {
        if (menuHeader.InstructionButton == null)
        {
            return;
        }

        if (headerInstructionButtonCallback != null)
        {
            menuHeader.InstructionButton.onClick.RemoveListener(headerInstructionButtonCallback);
        }

        headerInstructionButtonCallback = () =>
        {
            string instructionKey = $"{actionType}_instruc";
            PopupHelper.Instance?.ShowInstructionPopup(instructionKey);
        };

        menuHeader.InstructionButton.onClick.AddListener(headerInstructionButtonCallback);
        menuHeader.InstructionButton.interactable = PopupHelper.Instance != null;
    }

    private void SetHeaderActive(bool isActive)
    {
        if (menuHeader == null || menuHeader.Container == null)
        {
            if (!isActive)
            {
                SetMenuHeaderRightActive(false);
            }

            return;
        }

        if (menuHeader.Container.activeSelf != isActive)
        {
            menuHeader.Container.SetActive(isActive);
        }

        if (!isActive)
        {
            SetMenuHeaderRightActive(false);
        }
    }

    private void SetMenuHeaderRightForMenu(MenuActionType actionType)
    {
        SetMenuHeaderRightActive(ShouldShowMenuHeaderRight(actionType));
    }

    private void SetMenuHeaderRightActive(bool isActive)
    {
        if (menuHeaderRight == null)
        {
            return;
        }

        if (menuHeaderRight.activeSelf != isActive)
        {
            menuHeaderRight.SetActive(isActive);
        }
    }

    private bool ShouldShowMenuHeaderRight(MenuActionType actionType)
    {
        switch (actionType)
        {
            case MenuActionType.None:
                return mainMenu != null && mainMenu.activeSelf;
            case MenuActionType.Shop:
            case MenuActionType.Market:
            case MenuActionType.Friends:
            case MenuActionType.Messages:
            case MenuActionType.RewardDailyLogin:
            case MenuActionType.Inventory:
            case MenuActionType.QuickMatch:
                return true;
            default:
                return false;
        }
    }

    private void OnHeaderBackButtonClicked()
    {
        HideAllMenuPanels();
        SetMainMenuActive(true);
        SetMenuHeaderRightForMenu(MenuActionType.None);
    }
    //public async void TestConnectToServer()
    //{
    //    Debug.Log("🟡 Chuẩn bị kết nối với Server Dedicated...");
    //    GameManagerNetWork.Instance.OpenConnectToPhotonServer();
    //    var runner = GameManagerNetWork.Instance.runner;
    //    var startGameArgs = new StartGameArgs()
    //    {
    //        GameMode = GameMode.Client,
    //        SessionName = "DefaultRoom",
    //        PlayerCount = 4, // Số lượng người chơi tối đa
    //        // Fusion Client sẽ tìm phòng với SessionName này thông qua Photon Cloud Master Server
    //        SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
    //    };

    //    var result = await runner.StartGame(startGameArgs);

    //    if (result.Ok)
    //    {
    //        Debug.Log($"✅ Client đã tham gia phòng");
    //    }
    //    else
    //    {
    //        Debug.LogWarning($"❌ Client kết nối thất bại: {result.ShutdownReason}");
    //    }
    //}
    public void StartQuickMatch()
    {
        Debug.Log($"[QuickMatch] Player '{playerName}' requested StartQuickMatch. Starting coroutine...");
        if (QuickMatchClient.Instance == null)
        {
            Debug.LogWarning("QuickMatchClient instance is not available. Cannot start quick match.");
            return;
        }

        QuickMatchClient.Instance.StartQuickMatch(playerName);
    }

    private IEnumerator EnsureAdditiveNetworkSceneLoaded(NetworkRunner runner, NetworkSceneManagerDefault sceneManager, Action<string> reportError)
    {
        if (runner == null)
        {
            reportError?.Invoke("Network runner is not available for additive scene loading.");
            yield break;
        }

        if (sceneManager == null)
        {
            reportError?.Invoke("Network scene manager is not available for additive scene loading.");
            yield break;
        }

        if (runner.SceneManager == null)
        {
            reportError?.Invoke("Runner does not have a scene manager assigned.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(additiveNetworkSceneName))
        {
            yield break;
        }

        SceneRef sceneRef;
        try
        {
            sceneRef = sceneManager.GetSceneRef(additiveNetworkSceneName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[QuickMatch] Failed to resolve additive scene '{additiveNetworkSceneName}': {ex}");
            reportError?.Invoke($"Unable to resolve additive scene '{additiveNetworkSceneName}'.");
            yield break;
        }

        if (!sceneRef.IsValid)
        {
            Debug.LogWarning($"[QuickMatch] Scene reference for '{additiveNetworkSceneName}' is invalid. Skipping additive load.");
            yield break;
        }

        if (IsSceneAlreadyLoadedLocally(additiveNetworkSceneName))
        {
            Debug.Log($"[QuickMatch] Additive network scene '{additiveNetworkSceneName}' already loaded. Skipping reload.");
            yield break;
        }

        var loadParams = new NetworkLoadSceneParameters(
    //LoadSceneMode= LoadSceneMode.Additive,
    // LocalPhysicsMode.None // Tham số thứ 3 (LocalPhysicsMode) là tùy chọn
 );

        var usingDefaultSceneManager = runner.SceneManager == sceneManager && sceneManager is NetworkSceneManagerDefault;
        var shouldRequestLoad = true;

        if (usingDefaultSceneManager && runner.IsClient && !runner.IsServer && !runner.IsSharedModeMasterClient)
        {
            shouldRequestLoad = false;
            Debug.Log($"[QuickMatch] Additive scene '{additiveNetworkSceneName}' expected to sync from host. Waiting for load.");
        }

        NetworkSceneAsyncOp loadOperation = default;

        if (shouldRequestLoad)
        {
            try
            {
                loadOperation = runner.SceneManager.LoadScene(sceneRef, loadParams);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuickMatch] Failed to request load for additive scene '{additiveNetworkSceneName}': {ex}");
                reportError?.Invoke($"Failed to request load for scene '{additiveNetworkSceneName}'.");
                yield break;
            }

            if (!loadOperation.IsValid)
            {
                Debug.LogWarning($"[QuickMatch] Load operation for additive scene '{additiveNetworkSceneName}' is invalid.");
                reportError?.Invoke($"Unable to start loading scene '{additiveNetworkSceneName}'.");
                yield break;
            }

            while (!loadOperation.IsDone)
            {
                yield return null;
            }

            if (loadOperation.Error != null)
            {
                Debug.LogError($"[QuickMatch] Loading additive scene '{additiveNetworkSceneName}' failed: {loadOperation.Error.Message}");
                reportError?.Invoke($"Loading scene '{additiveNetworkSceneName}' failed.");
                yield break;
            }
        }
        else
        {
            while (!IsSceneAlreadyLoadedLocally(additiveNetworkSceneName))
            {
                yield return null;
            }
        }

        if (!IsSceneAlreadyLoadedLocally(additiveNetworkSceneName))
        {
            Debug.LogWarning($"[QuickMatch] Additive scene '{additiveNetworkSceneName}' did not finish loading locally.");
            reportError?.Invoke($"Scene '{additiveNetworkSceneName}' did not load correctly.");
            yield break;
        }

        Debug.Log($"[QuickMatch] Additive network scene '{additiveNetworkSceneName}' loaded successfully.");
    }

    private static bool IsSceneAlreadyLoadedLocally(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        var scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }

    public void onClickCancelQuickMatch()
    {
        StartCoroutine(CancelQuickMatchRoutine());
    }

    private IEnumerator CancelQuickMatchRoutine()
    {
        bool shouldWaitForShutdown = false; // Cờ theo dõi xem có cần chờ runner tắt không
        var networkManager = GameManagerNetWork.Instance;

        // 1. KHỐI TRY-CATCH XỬ LÝ LỖI ĐỒNG BỘ
        try
        {
            var quickMatchClient = QuickMatchClient.Instance;
            if (quickMatchClient != null && quickMatchClient.State == QuickMatchState.MatchReady)
            {
                // Cancel any pending ready check if the user backs out while a prompt is visible.
                quickMatchClient.ConfirmReady(false);
            }

            CancelQuickMatch(); // Lệnh này được bảo vệ khỏi lỗi đồng bộ

            if (networkManager != null)
            {
                var runner = networkManager.runner;
                if (runner != null && runner.IsRunning)
                {
                    Debug.Log("[QuickMatch] Cancel requested. Shutting down active runner and leaving session.");

                    // Khởi động tác vụ Shutdown. KHÔNG CHỜ trong khối TRY
                    runner.Shutdown();
                    shouldWaitForShutdown = true; // Đặt cờ để chờ ở bước 3
                }
                // Các lệnh đồng bộ khác
                networkManager.currentQuickMatchId = null;
                networkManager.currentQuickMatchResultId = null;
                networkManager.CloseConnectToRunner();
            }
        }
        catch (Exception ex)
        {
            // Bắt lỗi đồng bộ xảy ra trong khối try
            Debug.LogWarning(ex.Message);
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_runner_shutdown_error"), false);
        }

        if (shouldWaitForShutdown)
        {
            StartCoroutine(FinishQuickMatchShutdown(networkManager));
        }

        yield break;
    }

    private IEnumerator FinishQuickMatchShutdown(GameManagerNetWork networkManager)
    {
        if (networkManager != null && networkManager.runner != null)
        {
            yield return new WaitUntil(() => networkManager.runner == null || !networkManager.runner.IsRunning);
        }

        if (string.IsNullOrWhiteSpace(additiveNetworkSceneName))
        {
            yield break;
        }

        var additiveScene = SceneManager.GetSceneByName(additiveNetworkSceneName);
        if (!additiveScene.IsValid() || !additiveScene.isLoaded)
        {
            yield break;
        }

        // Bảo vệ PhotonFusionManager (GameManagerNetWork) không bị hủy khi dỡ scene additive
        if (GameManagerNetWork.Instance != null)
        {
            var managerGO = GameManagerNetWork.Instance.gameObject;
            if (managerGO != null && managerGO.scene == additiveScene)
            {
                Debug.Log("[QuickMatch] Preserving PhotonFusionManager before unloading additive scene.");
                SceneManager.MoveGameObjectToScene(managerGO, SceneManager.GetActiveScene());
                DontDestroyOnLoad(managerGO);
            }
        }

        Debug.Log($"[QuickMatch] Unloading additive scene '{additiveNetworkSceneName}' after cancelling quick match.");
        var unloadOperation = SceneManager.UnloadSceneAsync(additiveScene);
        unloadOperation ??= SceneManager.UnloadSceneAsync(additiveNetworkSceneName);

        if (unloadOperation == null)
        {
            Debug.LogWarning($"[QuickMatch] Unable to unload additive scene '{additiveNetworkSceneName}'.");
            yield break;
        }

        while (!unloadOperation.isDone)
        {
            yield return null;
        }
    }
    public void CancelQuickMatch()
    {
        QuickMatchClient.Instance?.CancelQuickMatch();
    }







    public void Start()
    {
 
        //đặt ngôn ngữ mặc định
        LoadSettingLanguage();
        //modelplayer = DatabaseManager.Instance.GetPlayerData();
        if (GameManagerNetWork.Instance.loginUserModel.UserId > 0)
        {
            if (SoundManager.Instance != null)
            {
                // Khi quay về Menu, luôn tắt nhạc môi trường (override từ map game)
                // và khôi phục nhạc nền của menu.
                SoundManager.Instance.ClearBgmOverride();
                SoundManager.Instance.PlayBackGroundSound();
            }
            SetMainMenuActive(true);
            SetMapGameActive(false);
            SetMenuHeaderRightForMenu(MenuActionType.None);
            LoginSocialPanel.SetActive(false);
            RefreshMenuLockStates();
            UserInfoHandler.Instance.StartLoadingPlayerInfo();
            ScheduleNewPlayerGiftPopupAfterTutorial();
        }
        else
        {
            inputField.onValueChanged.AddListener(OnInputChanged);
            SetMainMenuActive(false);
            SetMapGameActive(false);
            LoginSocialPanel.SetActive(true);
            // GoogleLoginPanel.SetActive(true);
        }
    }

    private void ScheduleNewPlayerGiftPopupAfterTutorial()
    {
        if (!CoreShootingTutorialController.HasNewPlayerGiftPopupRequest)
        {
            return;
        }

        StopNewPlayerGiftPopupRoutine();
        newPlayerGiftPopupRoutine = StartCoroutine(ShowNewPlayerGiftPopupAfterTutorialRoutine());
    }

    private void StopNewPlayerGiftPopupRoutine()
    {
        if (newPlayerGiftPopupRoutine == null)
        {
            return;
        }

        StopCoroutine(newPlayerGiftPopupRoutine);
        newPlayerGiftPopupRoutine = null;
    }

    private IEnumerator ShowNewPlayerGiftPopupAfterTutorialRoutine()
    {
        yield return null;

        const float waitTimeout = 3f;
        float elapsed = 0f;
        while (PopupHelper.Instance == null && elapsed < waitTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("MenuController: PopupHelper is not available for the new player gift popup.");
            newPlayerGiftPopupRoutine = null;
            yield break;
        }

        if (CoreShootingTutorialController.ConsumeNewPlayerGiftPopupRequest())
        {
            PopupHelper.Instance.ShowNewPlayerGiftPopup();
        }

        newPlayerGiftPopupRoutine = null;
    }

    //private void UpdateEquippedBallDisplay(EquipPlayer ball)
    //{
    //    if (BallRenderer != null)
    //        BallRenderer.gameObject.SetActive(true);

    //    int level = ball != null ? ball.level : 1;
    //    if (EquippedBallLevel != null)
    //        EquippedBallLevel.text = level.ToString();

    //    if (vipVFXInstance != null)
    //    {
    //        Destroy(vipVFXInstance);
    //        vipVFXInstance = null;
    //    }

    //    if (level >= 10 && Level10VFXPrefab != null)
    //    {
    //        vipVFXInstance = Instantiate(Level10VFXPrefab, BallRenderer.transform);
    //        vipVFXInstance.transform.localPosition = Vector3.zero;
    //    }
    //}
    void OnInputChanged(string input)
    {
       // playerName = input;
       //tesst
        pendingAuthCode = input;
        playerName = input;
    }

    private bool TryValidatePlayerName(out string sanitizedName)
    {
        sanitizedName = inputField != null ? inputField.text : playerName;
        sanitizedName = sanitizedName != null ? sanitizedName.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("invalid_player_name"), false);
            return false;
        }

        if (sanitizedName.Length < MinPlayerNameLength)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("player_name_too_short"), false);
            return false;
        }

        if (sanitizedName.Length > MaxPlayerNameLength)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("player_name_too_long"), false);
            return false;
        }

        if (!Regex.IsMatch(sanitizedName, "^[\\p{L}0-9 ]+$"))
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("player_name_invalid_chars"), false);
            return false;
        }

        return true;
    }
 
    //public void SetLanguageToVietnamese()
    //{
       
    //    LocalizationManager.Instance.LoadLanguage("vi");
    //}

    //public void SetLanguageToEnglish()
    //{
    //    LocalizationManager.Instance.LoadLanguage("en");
    //}
     void LoadSettingLanguage()
    {
        string newLang = PlayerPrefs.GetString("language") == "vi" ? "vi" : "en";
        LocalizationManager.Instance.LoadLanguage(newLang);

    }

    
    public void ExitGame()
    {
        Application.Quit();

    #if UNITY_EDITOR
            // Chỉ dùng khi đang chạy trong Unity Editor
            UnityEditor.EditorApplication.isPlaying = false;
    #endif
    }

    //public void onClickBackMainMenu()
    //{
    //    MainMenu.SetActive(true);
    //    InventoryPanel.SetActive(false);
    //    ShopPanel.SetActive(false);
    //    MarketPanel.SetActive(false);
    //    LobbyPanel.SetActive(false);
    //    FriendPanel.SetActive(false);
    //    StartCoroutine(LoadPlayerInfoCoroutine());
    //}



 



  /*  public void ShowShopList()
    {
        // Lấy danh sách item từ database
        //var lstItem = DatabaseManager.Instance.GetListItemForShop((int)TypeItemGid.Clother,(int)LocationItemGid.Shop);
        var lstItem = DatabaseManager.Instance.GetListItem();
        if (lstItem == null || lstItem.Count == 0)
        {
            Debug.LogWarning("Danh sách item rỗng!");
            return;
        }

        // Xóa danh sách cũ trước khi hiển thị
        foreach (Transform child in TabListCuliPanel)
        {
            Destroy(child.gameObject);
        }

        // Duyệt danh sách item và tạo UI
        foreach (var item in lstItem)
        {
            GameObject newItem = Instantiate(inventoryPrefab, TabListCuliPanel);

            // Tìm các component một cách an toàn 
            Image itemImage = newItem.GetComponentInChildren<Image>();
            //TMP_Text itemLevelText = newItem.GetComponentInChildren<TMP_Text>();

            if (itemImage != null)
            {
                itemImage.sprite = LoadSpriteByID(item.ID);
                newItem.GetComponent<Button>().onClick.AddListener(() =>
                {
                    idItem = item.ID;
                    typeItemGid = item.TypeGid;
                    money = item.Price;
                    itemName.text = item.Name;
                    levelItem.text = "Cấp: " + item.Level.ToString();
                    description.text = item.Description;
                    ChangeMaterial(idItem);
                });
            }
            else
            {
                Debug.LogError("Không tìm thấy Image trong prefab!");
            }



        }

        // Đảm bảo UI cập nhật ngay lập tức
        LayoutRebuilder.ForceRebuildLayoutImmediate(TabListCuliPanel.GetComponent<RectTransform>());
    } */


    private Coroutine createAccountRoutine;

    public void onClickCreate()
    {
        if (createAccountRoutine != null)
            return;

        if (!TryValidatePlayerName(out var sanitizedName))
        {
            return;
        }

        playerName = sanitizedName;

        if (pendingSocialLoginModel != null)
        {
            if (selectedCompanionBallItemId <= 0 && EnableCompanionSelectionDuringAccountCreation)
            {
                Debug.LogWarning("Bạn cần chọn viên bi đồng hành trước khi đặt tên nhân vật.");
                ShowCompanionSelectionPanel();
                return;
            }

            createAccountRoutine = StartCoroutine(HandleSocialPlayerNameConfirmation());
            return;
        }

        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(pendingAuthCode))
        {
            FinalizeAccountCreation();
            return;
        }

        createAccountRoutine = StartCoroutine(HandleAccountCreation());
    }

    private IEnumerator HandleAccountCreation()
    {
        SetLoadingScreenVisible(true);

        LoginUserModel loginResult = null;
        var avatarUrl = UgsToFirebaseAuth.Instance?.CurrentAvatarUrl;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.CreateAccount(pendingAuthCode, playerName, avatarUrl),
            result => loginResult = result));

        if (loginResult != null)
        {
            OnLoginComplete(loginResult);
        }
        else
        {
            FinalizeAccountCreation();
        }

        createAccountRoutine = null;
    }

    private IEnumerator HandleSocialPlayerNameConfirmation()
    {
        if (!TryValidatePlayerName(out var desiredName))
        {
            createAccountRoutine = null;
            yield break;
        }

        if (pendingSocialLoginModel == null)
        {
            createAccountRoutine = null;
            yield break;
        }

        if (APIManager.Instance == null)
        {
            Debug.LogWarning("APIManager chưa sẵn sàng để xác nhận tên nhân vật.");
            createAccountRoutine = null;
            yield break;
        }

        playerName = desiredName;
        if (!EnableCompanionSelectionDuringAccountCreation && selectedCompanionBallItemId <= 0)
        {
            selectedCompanionBallItemId = ResolveTemporaryDefaultCompanionBallItemId();
        }

        SetLoadingScreenVisible(true);

        LoginUserModel loginResult = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.ConfirmPlayerName(pendingSocialLoginModel, desiredName, selectedCompanionBallItemId),
            result => loginResult = result));

        if (loginResult != null)
        {
            OnLoginComplete(loginResult);
        }
        else
        {
            SetLoadingScreenVisible(false);
        }

        createAccountRoutine = null;
    }

    private void SetLoadingScreenVisible(bool isVisible)
    {
        if (LoadingManager.Instance?.UILoadingScreenPrefab != null)
        {
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(isVisible);
        }
    }

    private void FinalizeAccountCreation()
    {
        SetLoadingScreenVisible(false);
    }

    private void PersistSessionTokens(LoginUserModel model)
    {
        if (model == null)
            return;

        if (!string.IsNullOrEmpty(model.AccessToken))
            PlayerPrefs.SetString(AccessTokenKey, model.AccessToken);
        else
            PlayerPrefs.DeleteKey(AccessTokenKey);

        if (!string.IsNullOrEmpty(model.RefreshToken))
            PlayerPrefs.SetString(RefreshTokenKey, model.RefreshToken);
        else
            PlayerPrefs.DeleteKey(RefreshTokenKey);

        if (!string.IsNullOrEmpty(model.AccessTokenExpiresAt))
            PlayerPrefs.SetString(AccessTokenExpiryKey, model.AccessTokenExpiresAt);
        else
            PlayerPrefs.DeleteKey(AccessTokenExpiryKey);

        if (!string.IsNullOrEmpty(model.RefreshTokenExpiresAt))
            PlayerPrefs.SetString(RefreshTokenExpiryKey, model.RefreshTokenExpiresAt);
        else
            PlayerPrefs.DeleteKey(RefreshTokenExpiryKey);

        PlayerPrefs.Save();

        WebSocketHelper.Instance.SetAccessToken(model.AccessToken);
    }

    public void HandleLoginFailure(string errorMessage = null, long errorCode = 0)
    {
        pendingSocialLoginModel = null;
        FinalizeAccountCreation();

        var notificationMessage = GetLocalizedTextOrFallback(LoginFailedWaitLocalizationKey, null);
        if (string.IsNullOrWhiteSpace(notificationMessage))
        {
            notificationMessage = ResolveLoginErrorMessage(errorMessage, errorCode);
        }

        if (!string.IsNullOrWhiteSpace(notificationMessage))
        {
            NotificationHelper.Instance.ShowNotification(notificationMessage, false);
        }
    }

    private string ResolveLoginErrorMessage(string errorMessage, long errorCode)
    {
        if (IsLoginConflict(errorMessage, errorCode))
        {
            return GetLocalizedTextOrFallback(LoginConflictLocalizationKey, "Tài khoản đang đăng nhập trên thiết bị khác.");
        }

        return GetLocalizedTextOrFallback(LoginFailedLocalizationKey, "Đăng nhập thất bại. Vui lòng thử lại.");
    }

    private bool IsLoginConflict(string errorMessage, long errorCode)
    {
        if (errorCode == 409)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        return errorMessage.IndexOf("already logged in", StringComparison.OrdinalIgnoreCase) >= 0
            || errorMessage.IndexOf("đang đăng nhập", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string GetLocalizedTextOrFallback(string localizationKey, string fallback)
    {
        var localization = LocalizationManager.Instance;
        if (localization == null)
        {
            return fallback;
        }

        var localizedText = localization.GetText(localizationKey);
        return string.IsNullOrWhiteSpace(localizedText) || localizedText == localizationKey
            ? fallback
            : localizedText;
    }

    public void OnLoginComplete(LoginUserModel model)
    {
        if (model == null)
        {
            Debug.Log("Chưa login");
            pendingSocialLoginModel = null;
            FinalizeAccountCreation();
            return;
        }

        if (string.IsNullOrWhiteSpace(model.Username))
        {
            Debug.Log("Nhập tên người chơi");
            pendingSocialLoginModel = model;
            ShowCompanionSelectionPanel();
            FinalizeAccountCreation();
            return;
        }

        CompleteLoginFlow(model);
    }

    private void CompleteLoginFlow(LoginUserModel model)
    {
        Debug.Log("Login thành công");
        pendingSocialLoginModel = null;
        GameManagerNetWork.Instance.loginUserModel = model;
        PersistSessionTokens(model);
        WebSocketHelper.Instance.Connect(GameManagerNetWork.Instance.loginUserModel.UserId);
        FriendController.Instance?.EnsureInitialized();
        SoundManager.Instance?.ApplyLoginAudioSettings();
        if (CreateNameAccountPanel != null)
        {
            CreateNameAccountPanel.SetActive(false);
        }
        if (LoginSocialPanel != null)
        {
            LoginSocialPanel.SetActive(false);
        }
        if (companionSelectionPanel != null)
        {
            companionSelectionPanel.SetActive(false);
        }

        bool tutorialStarted = CoreShootingTutorialController.TryStartAfterLogin(model);
        if (tutorialStarted)
        {
            SetMainMenuActive(false);
            SetMapGameActive(false);
            HideAllMenuPanels();
            SetMenuHeaderRightActive(false);

            FinalizeAccountCreation();
            return;
        }

        SetMainMenuActive(true);
        PrepareMenuForLoggedInUser();
        RefreshMenuLockStates();
        StartCoroutine(LoadInforAndReconnect());
        FinalizeAccountCreation();
    }

    private void ShowPlayerNamePanel()
    {
        if (LoginSocialPanel != null)
        {
            LoginSocialPanel.SetActive(false);
        }
        if (companionSelectionPanel != null)
        {
            companionSelectionPanel.SetActive(false);
        }
        if (CreateNameAccountPanel != null)
        {
            CreateNameAccountPanel.SetActive(true);
        }

        SetMainMenuActive(false);
        SetMapGameActive(false);
        SetMenuHeaderRightActive(false);

        if (inputField != null)
        {
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }

        playerName = string.Empty;
    }

    private void ShowCompanionSelectionPanel()
    {
        if (!EnableCompanionSelectionDuringAccountCreation)
        {
            selectedCompanionBallItemId = ResolveTemporaryDefaultCompanionBallItemId();
            if (companionSelectionPanel != null)
            {
                companionSelectionPanel.SetActive(false);
            }

            ShowPlayerNamePanel();
            return;
        }

        if (LoginSocialPanel != null)
        {
            LoginSocialPanel.SetActive(false);
        }

        if (CreateNameAccountPanel != null)
        {
            CreateNameAccountPanel.SetActive(false);
        }

        if (companionSelectionPanel != null)
        {
            companionSelectionPanel.SetActive(true);
        }

        SetMainMenuActive(false);
        SetMapGameActive(false);
        SetMenuHeaderRightActive(false);

        selectedCompanionBallItemId = PlayerPrefs.GetInt("SelectedCompanionBallItemId", -1);

        if (companionBallOptions != null)
        {
            foreach (var option in companionBallOptions)
            {
                option?.SetSelected(option != null && option.ItemId == selectedCompanionBallItemId);
            }
        }
    }

    private int ResolveTemporaryDefaultCompanionBallItemId()
    {
        int savedItemId = PlayerPrefs.GetInt("SelectedCompanionBallItemId", TemporaryDefaultCompanionBallItemId);
        if (savedItemId > 0)
        {
            return savedItemId;
        }

        if (companionBallOptions != null)
        {
            foreach (var option in companionBallOptions)
            {
                if (option != null && option.ItemId > 0)
                {
                    PlayerPrefs.SetInt("SelectedCompanionBallItemId", option.ItemId);
                    PlayerPrefs.Save();
                    return option.ItemId;
                }
            }
        }

        return TemporaryDefaultCompanionBallItemId;
    }

    public void ReloadPlayerInfoData()
    {
        StartCoroutine(loadInfor());
    }

    private void PrepareMenuForLoggedInUser()
    {
        RefreshMenuLockStates();
        SetMenuHeaderRightForMenu(currentMenuEntry != null ? currentMenuEntry.ActionType : MenuActionType.None);

        if (LoginSocialPanel != null)
        {
            LoginSocialPanel.SetActive(false);
        }

        if (companionSelectionPanel != null)
        {
            companionSelectionPanel.SetActive(false);
        }
    }

    private IEnumerator LoadInforAndReconnect()
    {
        yield return StartCoroutine(loadInfor());
        RefreshMenuLockStates();
        yield return StartCoroutine(TryReconnectToActiveRoom());
    }

    IEnumerator loadInfor()
    {
        if (UserInfoHandler.Instance == null)
        {
            yield break;
        }

        yield return StartCoroutine(UserInfoHandler.Instance.LoadPlayerInfo());
    }

    private IEnumerator TryReconnectToActiveRoom()
    {
        var networkManager = GameManagerNetWork.Instance;
        var apiManager = APIManager.Instance;

        if (networkManager == null || apiManager == null)
        {
            yield break;
        }

        if (networkManager.runner != null && networkManager.runner.IsRunning)
        {
            yield break;
        }

        var loginModel = networkManager.loginUserModel;
        if (loginModel == null || loginModel.UserId <= 0)
        {
            yield break;
        }

        PlayerRoomApiResponse roomResponse = null;
        yield return StartCoroutine(apiManager.RunTask(
            apiManager.GetCurrentPlayerRoomAsync(loginModel.UserId),
            result => roomResponse = result));

        if (roomResponse == null || roomResponse.room == null || string.IsNullOrWhiteSpace(roomResponse.room.roomName))
        {
            yield break;
        }

        string sessionName = roomResponse.room.roomName;
        networkManager.currentQuickMatchId = sessionName;

        SetLoadingScreenVisible(true);
        yield return StartCoroutine(networkManager.ReconnectQuickMatch(sessionName));
        SetLoadingScreenVisible(false);
    }

    public void ShowLoginPanelAfterLogout()
    {
        HideAllMenuPanels();
        SetMainMenuActive(false);
        SetMapGameActive(false);
        SetMenuHeaderRightActive(false);

        if (CreateNameAccountPanel != null)
        {
            CreateNameAccountPanel.SetActive(false);
        }

        if (companionSelectionPanel != null)
        {
            companionSelectionPanel.SetActive(false);
        }

        if (LoginSocialPanel != null)
        {
            LoginSocialPanel.SetActive(true);
        }

        if (inputField != null)
        {
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }

        pendingSocialLoginModel = null;
        playerName = string.Empty;
        pendingAuthCode = string.Empty;
        selectedCompanionBallItemId = -1;
    }

    public void ShowLoginPanelAfterReconnectFailure()
    {
        ShowLoginPanelAfterLogout();

        if (inputField != null)
        {
            inputField.onValueChanged.RemoveListener(OnInputChanged);
            inputField.onValueChanged.AddListener(OnInputChanged);
        }
    }

    public void LoadMainMenu()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.ClearBgmOverride();
            SoundManager.Instance.PlayBackGroundSound();
        }

        ChacterViewPanel.SetActive(false);
        SetMainMenuActive(true);
        SetMapGameActive(false); // Ẩn nút khi load
        HideAllMenuPanels();
        StartCoroutine(UserInfoHandler.Instance.LoadPlayerInfo());
        SetMenuHeaderRightForMenu(MenuActionType.None);
        RefreshMenuLockStates();
    }
      public void LoadMainMenuForBackButton()
    {
        ChacterViewPanel.SetActive(false);
        SetMainMenuActive(true);
        SetMapGameActive(false); // Ẩn nút khi load
        HideAllMenuPanels();
        StartCoroutine(UserInfoHandler.Instance.LoadPlayerInfo());
        SetMenuHeaderRightForMenu(MenuActionType.None);
        RefreshMenuLockStates();
    }
    public void LoadPlayerInfoMenu()
    {
        StartCoroutine(UserInfoHandler.Instance.LoadPlayerInfo());
        SetMenuHeaderRightForMenu(currentMenuEntry != null ? currentMenuEntry.ActionType : MenuActionType.None);
        RefreshMenuLockStates();
    }    
    public void LoadMapGame()
    {
        SetMapGameActive(true);
        SetMainMenuActive(false); // Ẩn nút khi load
        HideAllMenuPanels();
    }
    public void onClickMapGame(string mapName)
    {
        LoadingManager.LoadScene(mapName);
    }
    public void onClickShowRewardAdsPopup()
    {
        PopupHelper.Instance.ShowRewardAdsPopup();
    }

}
