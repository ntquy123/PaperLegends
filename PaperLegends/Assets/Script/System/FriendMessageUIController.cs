using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendMessageUIController : MonoBehaviour
{
    public static FriendMessageUIController Instance;
    private const string MessageButtonObjectName = "MessageButton";
    private const string DeleteButtonObjectName = "DeleteButton";
    private const string EarlyExitLuckyDrawMessageMarker = "__MATCH_EARLY_EXIT_LUCKY_DRAW__";
    private const float BubblePaddingX = 60f;
    private const float BubblePaddingY = 50f;
    private sealed class MessageListEntry
    {
        public GameObject Item;
        public TMP_Text SenderText;
        public TMP_Text ContentText;
        public TMP_Text TimeText;
        public Button Button;
        public Button DeleteButton;
        public GameObject NewMessageIcon;
        public int FriendId;
        public int SequenceId;
        public bool IsRead;
        public bool IsDeleting;
    }

    [Header("Message List UI")]
    [SerializeField]
    private Transform MessageListPanel;
    [SerializeField]
    private GameObject MessageItemPrefab;

    [SerializeField]
    private GameObject PendingMessageBadge;
    [SerializeField]
    private TMP_Text PendingMessageBadgeText;

    [Header("Friend List UI")]
    [SerializeField]
    private Transform FriendListPanel;
    [SerializeField]
    private GameObject FriendItemPrefab;

    [Header("System Message UI")]
    [SerializeField]
    private Transform SystemMessageListPanel;
    [SerializeField]
    private TabManager FriendTabManager;

    [SerializeField]
    private GameObject PendingSystemMessageBadge;
    [SerializeField]
    private TMP_Text PendingSystemMessageBadgeText;

    [Tooltip("Index of the system messages tab inside the assigned TabManager.")]
    [SerializeField]
    private int SystemTabIndex = -1;

    private readonly Dictionary<int, MessageListEntry> messageSummaryEntries = new Dictionary<int, MessageListEntry>();
    private readonly Dictionary<int, string> friendNames = new Dictionary<int, string>();
    private readonly Dictionary<int, Image> friendStatusIcons = new Dictionary<int, Image>();
    private readonly Dictionary<int, Sprite> friendAvatarSprites = new Dictionary<int, Sprite>();

    private Coroutine loadConversationCoroutine;
    private Coroutine scrollConversationCoroutine;
    private Coroutine loadSystemMessagesCoroutine;
    private Coroutine systemTabInitializationCoroutine;
    private Button cachedSystemTabButton;

    private int? activeConversationFriendId;
    private MessageDetailPopupUI activeConversationPopup;

    [Header("Status Icons")]
    [SerializeField]
    private Sprite onlineSprite;
    [SerializeField]
    private Sprite offlineSprite;
    private Func<int> playerIdProvider;
    private APIManager apiManager;
    private NotificationHelper notificationHelper;
    private LocalizationManager localizationManager;

    private int PlayerId
    {
        get
        {
            if (playerIdProvider != null)
                return playerIdProvider();

            var networkManager = GameManagerNetWork.Instance;
            if (networkManager != null)
            {
                var loginUser = networkManager.loginUserModel;
                if (loginUser != null)
                    return loginUser.UserId;
            }

            return 0;
        }
    }
    private void Awake()
    {
        Instance = this; 
    }

    public void Initialize(Func<int> playerIdProvider)
    {
        this.playerIdProvider = playerIdProvider;
        apiManager = APIManager.Instance;
        notificationHelper = NotificationHelper.Instance;
        localizationManager = LocalizationManager.Instance;

        if (MessageListPanel != null && MessageItemPrefab != null)
            LoadMessages();
    }

    private void OnEnable()
    {
        if (FriendTabManager == null)
            FriendTabManager = GetComponentInChildren<TabManager>(true);

        RegisterSystemTabCallbacks();
        WebSocketHelper.OnMatchEarlyExitResultMessage += HandleMatchEarlyExitResultMessage;
        ScheduleSystemTabLoadIfActive();
    }

    private void OnDisable()
    {
        WebSocketHelper.OnMatchEarlyExitResultMessage -= HandleMatchEarlyExitResultMessage;
        UnregisterSystemTabCallbacks();

        if (loadSystemMessagesCoroutine != null)
        {
            StopCoroutine(loadSystemMessagesCoroutine);
            loadSystemMessagesCoroutine = null;
        }

        if (systemTabInitializationCoroutine != null)
        {
            StopCoroutine(systemTabInitializationCoroutine);
            systemTabInitializationCoroutine = null;
        }

        if (loadConversationCoroutine != null)
        {
            StopCoroutine(loadConversationCoroutine);
            loadConversationCoroutine = null;
        }

        if (scrollConversationCoroutine != null)
        {
            StopCoroutine(scrollConversationCoroutine);
            scrollConversationCoroutine = null;
        }

        activeConversationFriendId = null;
        CloseConversationPopup(false);
    }

    private void HandleMatchEarlyExitResultMessage(WebSocketHelper.MatchEarlyExitResultMessage message)
    {
        if (message == null)
            return;

        int playerId = PlayerId;
        if (playerId <= 0 || message.playerId != playerId)
            return;

        LoadSystemMessages();
    }

    public void LoadMessages()
    {
        int playerId = PlayerId;
        if (playerId <= 0)
            return;

        StartCoroutine(LoadMessagesRoutine(playerId));
    }

    public void LoadSystemMessages()
    {
        if (loadSystemMessagesCoroutine != null)
        {
            StopCoroutine(loadSystemMessagesCoroutine);
            loadSystemMessagesCoroutine = null;
        }

        loadSystemMessagesCoroutine = StartCoroutine(LoadSystemMessagesRoutine());
    }

    private void ScheduleSystemTabLoadIfActive()
    {
        if (systemTabInitializationCoroutine != null)
        {
            StopCoroutine(systemTabInitializationCoroutine);
            systemTabInitializationCoroutine = null;
        }

        if (!isActiveAndEnabled || FriendTabManager == null || SystemTabIndex < 0)
            return;

        var contents = FriendTabManager.TabContents;
        if (contents == null || SystemTabIndex >= contents.Length)
            return;

        if (contents[SystemTabIndex] == null)
            return;

        systemTabInitializationCoroutine = StartCoroutine(EnsureSystemTabLoadedIfActive());
    }

    public void ShowConversation(int friendId)
    {
        int myId = PlayerId;
        if (myId == 0 || friendId == 0)
            return;

        if (activeConversationPopup != null)
            CloseConversationPopup(false);

        var popupHelper = PopupHelper.Instance;
        if (popupHelper == null)
            return;

        activeConversationPopup = popupHelper.ShowMessageDetailPopup(() => CloseConversationPopup(true, false));
        if (activeConversationPopup == null)
            return;

        activeConversationFriendId = friendId;

        StartConversationLoad(myId, friendId);
        ShowInputFrom(friendId);
    }

    public void OnFriendMessageSent(MessageModel message)
    {
        if (message == null)
            return;

        UpdateMessageSummaryAfterSend(message);
        AppendMessageToActiveConversation(message, ResolveFriendId(message.senderId, message.receiverId, PlayerId), PlayerId);
    }

    public void OnIncomingMessage(WebSocketHelper.MessagePayload msg)
    {
        if (msg == null)
            return;

        int myId = PlayerId;
        int friendId = ResolveFriendId(msg.senderId, msg.receiverId, myId);
        if (friendId == 0)
            return;

        bool isIncoming = msg.senderId != myId;

        var message = new MessageModel
        {
            senderId = msg.senderId,
            receiverId = msg.receiverId,
            seqMess = msg.seqMess,
            seqId = msg.seqMess,
            message = msg.content,
            status = isIncoming ? "UNREAD" : "READ",
            createdAt = DateTime.UtcNow.ToString("o"),
            PlayerName = GetFriendDisplayName(friendId)
        };

        MessageListEntry entry = null;
        bool sequenceChanged = true;

        if (MessageListPanel != null && MessageItemPrefab != null)
        {
            entry = UpdateOrCreateMessageSummaryEntry(friendId, message, myId, out sequenceChanged);
            if (entry != null && entry.Item != null)
                entry.Item.transform.SetSiblingIndex(0);
        }

        bool conversationActive = activeConversationPopup != null &&
                                  activeConversationPopup.gameObject.activeInHierarchy &&
                                  activeConversationFriendId.HasValue &&
                                  activeConversationFriendId.Value == friendId;

        if (entry != null)
        {
            if (conversationActive && isIncoming)
            {
                entry.IsRead = true;
                UpdateMessageSummaryReadState(entry);
            }

            RecalculatePendingMessageBadge();

            if (sequenceChanged || (conversationActive && isIncoming))
                RefreshMessageSummaryLayout(entry);
        }
        else
        {
            RecalculatePendingMessageBadge();
        }

        if (sequenceChanged)
            AppendMessageToActiveConversation(message, friendId, myId);
    }

    public void ClearMessageInput()
    {
        if (activeConversationPopup != null && activeConversationPopup.MessageInput != null)
            activeConversationPopup.MessageInput.text = string.Empty;
    }

    public void RegisterFriendName(int friendId, string displayName)
    {
        if (friendId <= 0)
            return;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            friendNames.Remove(friendId);
            return;
        }

        friendNames[friendId] = displayName;
    }

    public void ClearFriendNames()
    {
        friendNames.Clear();
        friendStatusIcons.Clear();
    }
    public void LoadFriendList()
    {
        int playerId = PlayerId;
        if (playerId <= 0)
            return;

        StartCoroutine(LoadFriendListRoutine(playerId));
    }

    private IEnumerator LoadFriendListRoutine(int playerId)
    {
        var loadingScreen = LoadingManager.Instance != null ? LoadingManager.Instance.UILoadingScreenPrefab : null;
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        PlayerInfoStruct[] friends = null;
        var api = GetApiManager();
        if (api != null)
        {
            yield return StartCoroutine(api.RunTask(
                api.GetFriendList(playerId),
                r => friends = r));
        }

        if (loadingScreen != null)
            loadingScreen.SetActive(false);

        bool success = friends != null;
        if (success)
            BuildFriendListForMessaging(friends);

    }
    public void BuildFriendListForMessaging(PlayerInfoStruct[] friends)
    {
        if (FriendListPanel == null || FriendItemPrefab == null)
            return;

        foreach (Transform child in FriendListPanel)
            Destroy(child.gameObject);

        friendStatusIcons.Clear();
        ClearFriendAvatarSprites();

        if (friends == null || friends.Length == 0)
        {
            RefreshFriendListLayout();
            return;
        }

        foreach (var friend in friends)
        {
            if (friend.playerId <= 0)
                continue;

            GameObject item = Instantiate(FriendItemPrefab, FriendListPanel);
            if (item == null)
                continue;

            var nameText = item.transform.Find("PlayerName")?.GetComponent<TMP_Text>();
            if (nameText != null)
                nameText.text = friend.fullname.ToString();

            var levelText = item.transform.Find("Level")?.GetComponent<TMP_Text>();
            if (levelText != null)
                levelText.text = $"Lv {friend.level}";

            int fid = friend.playerId;
            RegisterFriendName(fid, friend.fullname.ToString());

            var avatarRaw = item.transform.Find("Avatar")?.GetComponent<RawImage>();
            var avatarImage = item.transform.Find("Avatar")?.GetComponent<Image>();
            TryLoadFriendAvatar(friend, avatarRaw, avatarImage);

            var statusIcon = item.transform.Find("OnlineIcon")?.GetComponent<Image>();
            if (statusIcon != null)
            {
                friendStatusIcons[fid] = statusIcon;
                RequestFriendStatus(fid, statusIcon);
            }

            var messageButton = FindButtonByName(item.transform, MessageButtonObjectName);
            if (messageButton != null)
            {
                messageButton.gameObject.SetActive(true);
                messageButton.onClick.RemoveAllListeners();
                messageButton.onClick.AddListener(() => ShowConversation(fid));
            }

            DisableButtonIfExists(item.transform, "ChallengeButton");
            DisableButtonIfExists(item.transform, "RemoveButton");
            DisableButtonIfExists(item.transform, "AcceptButton");
            DisableButtonIfExists(item.transform, "DeclineButton");
        }

        RefreshFriendListLayout();
    }

    private void TryLoadFriendAvatar(PlayerInfoStruct friend, RawImage rawImage, Image image)
    {
        if (friend.playerId <= 0)
            return;

        if (rawImage == null && image == null)
            return;

        var avatarUrl = friend.avatarUrl.ToString();
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return;

        var providerType = ResolveProviderType(friend.providerType.ToString());
        var avatarService = AvatarService.EnsureInstance();
        avatarService.LoadAvatar(new AvatarService.AvatarRequest(providerType, avatarUrl, string.Empty),
            texture => ApplyFriendAvatarTexture(friend.playerId, texture, rawImage, image),
            _ => { });
    }

    private void ApplyFriendAvatarTexture(int friendId, Texture2D texture, RawImage rawImage, Image image)
    {
        if (texture == null)
            return;

        if (rawImage != null)
        {
            rawImage.texture = texture;
            rawImage.color = Color.white;
            return;
        }

        if (image == null)
            return;

        if (friendAvatarSprites.TryGetValue(friendId, out var existingSprite))
        {
            if (existingSprite == null || existingSprite.texture != texture)
            {
                if (existingSprite != null)
                {
                    Destroy(existingSprite);
                }

                existingSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                friendAvatarSprites[friendId] = existingSprite;
            }
        }
        else
        {
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            friendAvatarSprites[friendId] = sprite;
        }

        image.sprite = friendAvatarSprites[friendId];
        image.color = Color.white;
    }

    private void ClearFriendAvatarSprites()
    {
        foreach (var sprite in friendAvatarSprites.Values)
        {
            if (sprite != null)
            {
                Destroy(sprite);
            }
        }

        friendAvatarSprites.Clear();
    }

    private static AuthenticationProviderType ResolveProviderType(string providerType)
    {
        if (!string.IsNullOrEmpty(providerType) && Enum.TryParse(providerType, true, out AuthenticationProviderType parsed))
        {
            return parsed;
        }

        return AuthenticationProviderType.Anonymous;
    }

    public void UpdateFriendStatusIcon(int friendId, bool isOnline)
    {
        if (friendId <= 0)
            return;

        if (friendStatusIcons.TryGetValue(friendId, out var icon))
            UpdateStatusIcon(icon, isOnline);
    }

    private void RequestFriendStatus(int friendId, Image statusIcon)
    {
        if (statusIcon == null)
            return;

        WebSocketHelper.Instance?.CheckPlayerOnline(friendId, isOnline =>
        {
            if (statusIcon == null)
                return;

            UpdateStatusIcon(statusIcon, isOnline);
        });
    }

    private void UpdateStatusIcon(Image icon, bool isOnline)
    {
        if (icon == null)
            return;

        if (isOnline)
        {
            if (onlineSprite != null)
                icon.sprite = onlineSprite;
            icon.gameObject.SetActive(true);
        }
        else
        {
            if (offlineSprite != null)
            {
                icon.sprite = offlineSprite;
                icon.gameObject.SetActive(true);
            }
            else
            {
                icon.gameObject.SetActive(false);
            }
        }
    }

    private void RefreshFriendListLayout()
    {
        if (FriendListPanel == null)
            return;

        var rect = FriendListPanel.GetComponent<RectTransform>();
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private static void DisableButtonIfExists(Transform root, string buttonName)
    {
        if (root == null || string.IsNullOrEmpty(buttonName))
            return;

        var target = root.Find(buttonName);
        if (target != null)
        {
            target.gameObject.SetActive(false);
            var button = target.GetComponent<Button>();
            if (button != null)
                button.onClick.RemoveAllListeners();
        }
    }

    public string GetFriendDisplayName(int friendId)
    {
        if (friendId <= 0)
            return string.Empty;

        if (messageSummaryEntries.TryGetValue(friendId, out var existingEntry))
        {
            if (existingEntry != null && existingEntry.SenderText != null)
            {
                string text = existingEntry.SenderText.text;
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        if (friendNames.TryGetValue(friendId, out var name) && !string.IsNullOrWhiteSpace(name))
            return name;

        return string.Empty;
    }

    public static Button FindButtonByName(Transform root, string buttonName)
    {
        if (root == null || string.IsNullOrEmpty(buttonName))
            return null;

        Transform directChild = root.Find(buttonName);
        if (directChild != null)
        {
            var directButton = directChild.GetComponent<Button>();
            if (directButton != null)
                return directButton;
        }

        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            if (string.Equals(button.name, buttonName, StringComparison.OrdinalIgnoreCase))
                return button;
        }

        return null;
    }

    private Button ResolveOrCreateMessageButton(GameObject item)
    {
        if (item == null)
            return null;

        var button = item.GetComponent<Button>();
        if (button == null)
            button = FindButtonByName(item.transform, MessageButtonObjectName);
        if (button == null)
            button = item.AddComponent<Button>();

        if (button == null)
            return null;

        if (button.targetGraphic == null)
        {
            var graphic = button.GetComponent<Graphic>();
            if (graphic == null)
                graphic = item.GetComponent<Graphic>();
            if (graphic == null)
                graphic = item.GetComponentInChildren<Graphic>(true);

            if (graphic != null)
                button.targetGraphic = graphic;
            else
                button.transition = Selectable.Transition.None;
        }

        return button;
    }

    private IEnumerator LoadMessagesRoutine(int playerId)
    {
        var loadingScreen = LoadingManager.Instance != null ? LoadingManager.Instance.UILoadingScreenPrefab : null;
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        MessageModel[] messages = null;
        var api = GetApiManager();
        if (api != null)
        {
            yield return StartCoroutine(api.RunTask(
                api.GetFriendMessages(playerId),
                r => messages = r));
        }

        if (loadingScreen != null)
            loadingScreen.SetActive(false);

        bool success = messages != null;
        if (success)
            BuildMessageList(messages);
    }

    private IEnumerator LoadSystemMessagesRoutine()
    {
        GameObject loadingScreen = LoadingManager.Instance != null ? LoadingManager.Instance.UILoadingScreenPrefab : null;
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        var api = GetApiManager();
        if (api == null)
        {
            if (loadingScreen != null)
                loadingScreen.SetActive(false);
            loadSystemMessagesCoroutine = null;
            yield break;
        }

        MessageModel[] systemMessages = null;
        yield return StartCoroutine(api.RunTask(
            api.GetSystemMessages(playerId: PlayerId),
            r => systemMessages = r));

        if (loadingScreen != null)
            loadingScreen.SetActive(false);

        if (systemMessages == null)
            systemMessages = Array.Empty<MessageModel>();

        BuildSystemMessageList(systemMessages);

        loadSystemMessagesCoroutine = null;
    }

    private bool TryGetSystemTabButton(out Button systemTabButton)
    {
        systemTabButton = null;

        if (FriendTabManager == null)
            return false;

        var buttons = FriendTabManager.TabButtons;
        if (buttons == null || SystemTabIndex < 0 || SystemTabIndex >= buttons.Length)
            return false;

        systemTabButton = buttons[SystemTabIndex];
        return systemTabButton != null;
    }

    private bool IsSystemTabActive()
    {
        if (FriendTabManager == null || SystemTabIndex < 0)
            return false;

        var contents = FriendTabManager.TabContents;
        if (contents == null || SystemTabIndex >= contents.Length)
            return false;

        var systemTab = contents[SystemTabIndex];
        return systemTab != null && systemTab.activeInHierarchy;
    }

    private void RegisterSystemTabCallbacks()
    {
        if (!TryGetSystemTabButton(out var systemTabButton))
            return;

        if (cachedSystemTabButton != null && cachedSystemTabButton != systemTabButton)
            cachedSystemTabButton.onClick.RemoveListener(OnSystemTabClicked);

        cachedSystemTabButton = systemTabButton;
        cachedSystemTabButton.onClick.RemoveListener(OnSystemTabClicked);
        cachedSystemTabButton.onClick.AddListener(OnSystemTabClicked);
    }

    private void UnregisterSystemTabCallbacks()
    {
        if (cachedSystemTabButton == null)
            return;

        cachedSystemTabButton.onClick.RemoveListener(OnSystemTabClicked);
        cachedSystemTabButton = null;
    }

    private IEnumerator EnsureSystemTabLoadedIfActive()
    {
        try
        {
            yield return null;

            if (IsSystemTabActive())
                LoadSystemMessages();
        }
        finally
        {
            systemTabInitializationCoroutine = null;
        }
    }

    private void OnSystemTabClicked()
    {
        ScheduleSystemTabLoadIfActive();
    }

    private void StartConversationLoad(int myId, int friendId)
    {
        if (loadConversationCoroutine != null)
        {
            StopCoroutine(loadConversationCoroutine);
            loadConversationCoroutine = null;
        }

        loadConversationCoroutine = StartCoroutine(LoadConversationRoutine(myId, friendId));
    }

    private IEnumerator LoadConversationRoutine(int myId, int friendId)
    {
        ClearConversationContent();

        if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);

        var api = GetApiManager();
        if (api == null)
        {
            if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);

            loadConversationCoroutine = null;
            yield break;
        }

        MessageModel[] history = null;
        yield return StartCoroutine(api.RunTask(
            api.GetConversationHistory(myId, friendId),
            r => history = r));

        if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);

        if (history == null)
            history = Array.Empty<MessageModel>();

        BuildConversationHistory(history, myId);

        loadConversationCoroutine = null;
    }

    private void BuildConversationHistory(MessageModel[] messages, int myId)
    {
        if (activeConversationPopup == null || activeConversationPopup.MessageConversationContent == null || messages == null)
            return;

        foreach (var msg in messages)
        {
            if (msg == null)
                continue;

            bool isMine = msg.senderId == myId;
            CreateMessageBubble(msg, isMine);
        }

        RefreshConversationLayout();
        ScrollConversationToBottom();
    }

    private GameObject CreateMessageBubble(MessageModel msg, bool isMine)
    {
        if (activeConversationPopup == null || activeConversationPopup.MessageConversationContent == null || msg == null)
            return null;

        GameObject prefab = isMine ? activeConversationPopup.MyMessageBubblePrefab : activeConversationPopup.FriendMessageBubblePrefab;
        if (prefab == null)
            return null;

        GameObject bubble = Instantiate(prefab, activeConversationPopup.MessageConversationContent);
        if (bubble == null)
            return null;

        ConfigureMessageBubble(bubble, msg, isMine);
        return bubble;
    }

    private void ConfigureMessageBubble(GameObject bubble, MessageModel msg, bool isMine)
    {
        if (bubble == null)
            return;

        RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
        if (bubbleRect != null)
        {
            float anchorX = isMine ? 1f : 0f;
            bubbleRect.anchorMin = new Vector2(anchorX, bubbleRect.anchorMin.y);
            bubbleRect.anchorMax = new Vector2(anchorX, bubbleRect.anchorMax.y);
            var anchoredPosition = bubbleRect.anchoredPosition;
            bubbleRect.anchoredPosition = new Vector2(0f, anchoredPosition.y);
        }

        Transform messageTransform = bubble.transform.Find("MessageText");
        TMP_Text messageText = messageTransform != null ? messageTransform.GetComponent<TMP_Text>() : null;
        if (messageText == null)
            messageText = bubble.GetComponentInChildren<TMP_Text>();
        if (messageText != null)
            messageText.text = GetMessageBodyWithRewards(msg);

        Transform timeTransform = bubble.transform.Find("TimeText");
        TMP_Text timeText = timeTransform != null ? timeTransform.GetComponent<TMP_Text>() : null;
        if (timeText != null)
            timeText.text = ItemVisualHelper.FormatRelativeTime(msg != null ? msg.createdAt : null);

        UpdateBubbleBackgroundSize(bubble, messageText);
    }

    private void UpdateBubbleBackgroundSize(GameObject bubble, TMP_Text messageText)
    {
        if (bubble == null || messageText == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(messageText.rectTransform);

        Transform backgroundTransform = bubble.transform.Find("Image");
        Image background = backgroundTransform != null ? backgroundTransform.GetComponent<Image>() : null;
        if (background == null)
            background = bubble.GetComponentInChildren<Image>(true);

        if (background == null)
            return;

        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.sizeDelta = new Vector2(
            messageText.preferredWidth + BubblePaddingX,
            messageText.preferredHeight + BubblePaddingY);
    }

    private void RefreshConversationLayout()
    {
        if (activeConversationPopup == null || activeConversationPopup.MessageConversationContent == null)
            return;

        var rect = activeConversationPopup.MessageConversationContent.GetComponent<RectTransform>();
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private void ScrollConversationToBottom()
    {
        if (activeConversationPopup == null || activeConversationPopup.MessageConversationScrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        if (!isActiveAndEnabled || !activeConversationPopup.MessageConversationScrollRect.gameObject.activeInHierarchy)
        {
            activeConversationPopup.MessageConversationScrollRect.verticalNormalizedPosition = 0f;
            return;
        }

        if (scrollConversationCoroutine != null)
        {
            StopCoroutine(scrollConversationCoroutine);
            scrollConversationCoroutine = null;
        }

        scrollConversationCoroutine = StartCoroutine(ScrollConversationToBottomRoutine());
    }

    private IEnumerator ScrollConversationToBottomRoutine()
    {
        Canvas.ForceUpdateCanvases();
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (activeConversationPopup != null && activeConversationPopup.MessageConversationScrollRect != null)
            activeConversationPopup.MessageConversationScrollRect.verticalNormalizedPosition = 0f;
        scrollConversationCoroutine = null;
    }

    private void AppendMessageToActiveConversation(MessageModel message, int friendId, int myId)
    {
        if (activeConversationPopup == null || activeConversationPopup.MessageConversationContent == null || message == null)
            return;

        if (!activeConversationPopup.gameObject.activeInHierarchy)
            return;

        if (!activeConversationFriendId.HasValue || activeConversationFriendId.Value != friendId)
            return;

        bool isMine = message.senderId == myId;
        CreateMessageBubble(message, isMine);
        RefreshConversationLayout();
        ScrollConversationToBottom();
    }

    private void ClearConversationContent()
    {
        if (activeConversationPopup == null || activeConversationPopup.MessageConversationContent == null)
            return;

        foreach (Transform child in activeConversationPopup.MessageConversationContent)
            Destroy(child.gameObject);

        RefreshConversationLayout();
    }

    public void UpdatePendingMessageBadge(int count)
    {
        count = Mathf.Max(0, count);

        if (PendingMessageBadge != null)
            PendingMessageBadge.SetActive(count > 0);
        if (PendingMessageBadgeText != null)
            PendingMessageBadgeText.text = count.ToString();

        UserInfoHandler.Instance?.SetPendingMessageCount(count);
    }

    private void UpdatePendingSystemMessageBadge(int count)
    {
        if (PendingSystemMessageBadge != null)
            PendingSystemMessageBadge.SetActive(count > 0);
        if (PendingSystemMessageBadgeText != null)
            PendingSystemMessageBadgeText.text = count.ToString();
    }

    private void BuildMessageList(MessageModel[] messages)
    {
        if (MessageListPanel == null || MessageItemPrefab == null)
            return;

        foreach (Transform child in MessageListPanel)
            Destroy(child.gameObject);

        messageSummaryEntries.Clear();

        if (messages == null || messages.Length == 0)
        {
            UpdatePendingMessageBadge(0);
            return;
        }

        int myId = PlayerId;

        foreach (var msg in messages)
        {
            if (msg == null)
                continue;

            int friendId = msg.senderId == myId ? msg.receiverId : msg.senderId;
            var entry = CreateMessageSummaryEntry(friendId, msg, myId);
            if (entry == null)
                continue;

            messageSummaryEntries[friendId] = entry;
        }

        RecalculatePendingMessageBadge();
        LayoutRebuilder.ForceRebuildLayoutImmediate(MessageListPanel.GetComponent<RectTransform>());
    }

    private MessageListEntry CreateMessageSummaryEntry(int friendId, MessageModel msg, int myId)
    {
        if (MessageListPanel == null || MessageItemPrefab == null)
            return null;

        GameObject item = Instantiate(MessageItemPrefab, MessageListPanel);
        if (item == null)
            return null;

        var newMessageIcon = item.transform.Find("NewIconMess")?.gameObject;

        var entry = new MessageListEntry
        {
            Item = item,
            FriendId = friendId,
            SenderText = item.transform.Find("SenderName")?.GetComponent<TMP_Text>(),
            ContentText = item.transform.Find("MessageBody")?.GetComponent<TMP_Text>(),
            TimeText = item.transform.Find("Time")?.GetComponent<TMP_Text>(),
            Button = ResolveOrCreateMessageButton(item),
            DeleteButton = FindButtonByName(item.transform, DeleteButtonObjectName),
            NewMessageIcon = newMessageIcon
        };

        ApplyMessageSummaryData(entry, msg, myId);
        ConfigureMessageSummaryButton(entry, myId);
        ConfigureMessageSummaryDeleteButton(entry);

        return entry;
    }

    private void ApplyMessageSummaryData(MessageListEntry entry, MessageModel msg, int myId)
    {
        if (entry == null)
            return;

        entry.SequenceId = msg != null ? msg.seqMess : 0;

        bool isRead = false;
        if (msg != null)
        {
            isRead = string.Equals(msg.status, "READ", StringComparison.OrdinalIgnoreCase);
            if (msg.senderId == myId)
                isRead = true;
        }
        entry.IsRead = isRead;

        if (entry.ContentText != null)
            entry.ContentText.text = GetMessageBodyWithRewards(msg);

        string senderName = GetSafeSenderName(msg);
        if (string.IsNullOrWhiteSpace(senderName))
            senderName = GetFriendDisplayName(entry.FriendId);
        if (!string.IsNullOrWhiteSpace(senderName) && entry.SenderText != null)
            entry.SenderText.text = senderName;

        if (entry.TimeText != null)
            entry.TimeText.text = ItemVisualHelper.FormatRelativeTime(msg != null ? msg.createdAt : null);

        UpdateMessageSummaryReadState(entry);
    }

    private void ConfigureMessageSummaryButton(MessageListEntry entry, int myId)
    {
        if (entry == null)
            return;

        var button = entry.Button != null ? entry.Button : ResolveOrCreateMessageButton(entry.Item);
        if (button == null)
            return;

        entry.Button = button;
        entry.Button.interactable = true;
        entry.Button.onClick.RemoveAllListeners();
        entry.Button.onClick.AddListener(() =>
        {
            ShowConversation(entry.FriendId);
            if (!entry.IsRead && entry.SequenceId != 0)
            {
                ReadMessage(entry.SequenceId);
                entry.IsRead = true;
                UpdateMessageSummaryReadState(entry);
                RecalculatePendingMessageBadge();
            }
        });
    }

    private void ConfigureMessageSummaryDeleteButton(MessageListEntry entry)
    {
        if (entry == null)
            return;

        if (entry.DeleteButton == null && entry.Item != null)
            entry.DeleteButton = FindButtonByName(entry.Item.transform, DeleteButtonObjectName);

        var deleteButton = entry.DeleteButton;
        if (deleteButton == null)
            return;

        deleteButton.gameObject.SetActive(true);
        deleteButton.onClick.RemoveAllListeners();
        deleteButton.interactable = !entry.IsDeleting;
        deleteButton.onClick.AddListener(() =>
        {
            if (entry.IsDeleting)
                return;

            StartCoroutine(DeleteMessageSummaryRoutine(entry));
        });
    }

    private IEnumerator DeleteMessageSummaryRoutine(MessageListEntry entry)
    {
        if (entry == null)
            yield break;

        if (entry.Item == null)
            yield break;

        if (entry.DeleteButton == null)
            entry.DeleteButton = FindButtonByName(entry.Item.transform, DeleteButtonObjectName);

        var deleteButton = entry.DeleteButton;

        entry.IsDeleting = true;
        if (deleteButton != null)
            deleteButton.interactable = false;

        bool success = false;
        int playerId = PlayerId;
        int friendId = entry.FriendId;

        var api = GetApiManager();
        if (playerId > 0 && friendId > 0 && api != null)
        {
            yield return StartCoroutine(api.RunTask(
                api.DeleteFriendMessage(playerId, friendId),
                r => success = r));
        }
        else
        {
            yield return null;
        }

        entry.IsDeleting = false;

        if (success)
        {
            RemoveMessageSummaryEntry(entry);
        }
        else if (deleteButton != null)
        {
            deleteButton.interactable = true;
        }

        var notification = GetNotificationHelper();
        if (notification != null)
        {
            string key = success ? "friend_message_delete_success" : "friend_message_delete_failure";
            string fallback = success ? "Message deleted" : "Failed to delete message";
            notification.ShowNotification(GetLocalizedOrFallback(key, fallback), success);
        }
    }

    private void RemoveMessageSummaryEntry(MessageListEntry entry)
    {
        if (entry == null)
            return;

        if (entry.FriendId != 0 && messageSummaryEntries.TryGetValue(entry.FriendId, out var existing) && ReferenceEquals(existing, entry))
            messageSummaryEntries.Remove(entry.FriendId);
        else if (entry.FriendId != 0)
            messageSummaryEntries.Remove(entry.FriendId);

        if (entry.Item != null)
            Destroy(entry.Item);

        entry.Item = null;
        entry.Button = null;
        entry.DeleteButton = null;
        entry.NewMessageIcon = null;
        entry.IsDeleting = false;

        RecalculatePendingMessageBadge();

        if (MessageListPanel != null)
        {
            var listRect = MessageListPanel.GetComponent<RectTransform>();
            if (listRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);
                LayoutRebuilder.MarkLayoutForRebuild(listRect);
            }
        }

        if (activeConversationFriendId.HasValue && activeConversationFriendId.Value == entry.FriendId)
        {
            CloseConversationPopup(false);
        }
    }

    private static void UpdateMessageSummaryReadState(MessageListEntry entry)
    {
        if (entry == null)
            return;

        FontStyles style = entry.IsRead ? FontStyles.Normal : FontStyles.Bold;

        if (entry.SenderText != null)
            entry.SenderText.fontStyle = style;
        if (entry.ContentText != null)
            entry.ContentText.fontStyle = style;
        if (entry.TimeText != null)
            entry.TimeText.fontStyle = style;
        if (entry.NewMessageIcon != null)
            entry.NewMessageIcon.SetActive(!entry.IsRead);
    }

    private void RecalculatePendingMessageBadge()
    {
        int unreadCount = 0;

        foreach (var entry in messageSummaryEntries.Values)
        {
            if (entry == null || entry.Item == null)
                continue;

            if (!entry.IsRead)
                unreadCount++;
        }

        UpdatePendingMessageBadge(unreadCount);
    }

    private static int ResolveFriendId(int senderId, int receiverId, int myId)
    {
        if (senderId > 0 && senderId != myId)
            return senderId;

        if (receiverId > 0 && receiverId != myId)
            return receiverId;

        if (senderId == myId && receiverId > 0 && receiverId != myId)
            return receiverId;

        if (receiverId == myId && senderId > 0 && senderId != myId)
            return senderId;

        return 0;
    }

    private MessageListEntry UpdateOrCreateMessageSummaryEntry(int friendId, MessageModel message, int myId, out bool sequenceChanged)
    {
        sequenceChanged = true;

        if (MessageListPanel == null || MessageItemPrefab == null || message == null || friendId == 0)
            return null;

        if (!messageSummaryEntries.TryGetValue(friendId, out var entry) || entry == null || entry.Item == null)
        {
            entry = CreateMessageSummaryEntry(friendId, message, myId);
            if (entry != null)
                messageSummaryEntries[friendId] = entry;
            return entry;
        }

        if (message.seqMess != 0 && entry.SequenceId == message.seqMess)
        {
            sequenceChanged = false;
            return entry;
        }

        ApplyMessageSummaryData(entry, message, myId);
        ConfigureMessageSummaryDeleteButton(entry);
        sequenceChanged = true;
        return entry;
    }

    private void RefreshMessageSummaryLayout(MessageListEntry entry)
    {
        if (entry != null && entry.Item != null)
        {
            var entryRect = entry.Item.GetComponent<RectTransform>();
            if (entryRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(entryRect);
        }

        if (MessageListPanel != null)
        {
            var listRect = MessageListPanel.GetComponent<RectTransform>();
            if (listRect != null)
                LayoutRebuilder.MarkLayoutForRebuild(listRect);
        }
    }

    private void UpdateMessageSummaryAfterSend(MessageModel message)
    {
        if (message == null)
            return;

        int myId = PlayerId;
        int friendId = ResolveFriendId(message.senderId, message.receiverId, myId);
        if (friendId == 0)
            return;

        bool sequenceChanged;
        var entry = UpdateOrCreateMessageSummaryEntry(friendId, message, myId, out sequenceChanged);
        if (entry == null)
            return;

        if (entry.Item != null)
            entry.Item.transform.SetSiblingIndex(0);

        RecalculatePendingMessageBadge();

        if (sequenceChanged)
            RefreshMessageSummaryLayout(entry);
    }

    private void BuildSystemMessageList(MessageModel[] messages)
    {
        if (SystemMessageListPanel == null || MessageItemPrefab == null)
            return;

        foreach (Transform child in SystemMessageListPanel)
            Destroy(child.gameObject);

        if (messages == null)
        {
            UpdatePendingSystemMessageBadge(0);
            return;
        }
        if (messages == null || messages.Length == 0)
        {
            var systemRect = SystemMessageListPanel.GetComponent<RectTransform>();
            if (systemRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(systemRect);
            return;
        }
        int unreadCount = 0;
        foreach (var msg in messages)
        {
            if (msg == null)
                continue;

            GameObject item = Instantiate(MessageItemPrefab, SystemMessageListPanel);
            if (item == null)
                continue;

            var deleteButton = FindButtonByName(item.transform, DeleteButtonObjectName);
            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(false);
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.interactable = false;
            }

            var senderTransform = item.transform.Find("SenderName");
            var contentTransform = item.transform.Find("MessageBody");
            var timeTransform = item.transform.Find("Time");

            var senderText = senderTransform != null ? senderTransform.GetComponent<TMP_Text>() : null;
            var contentText = contentTransform != null ? contentTransform.GetComponent<TMP_Text>() : null;
            var timeText = timeTransform != null ? timeTransform.GetComponent<TMP_Text>() : null;

            if (senderText != null)
                senderText.text = "SYSTEM";
            if (contentText != null)
                contentText.text = GetMessageBodyWithRewards(msg);
            if (timeText != null)
                timeText.text = ItemVisualHelper.FormatRelativeTime(msg != null ? msg.createdAt : null);

            bool isRead = string.Equals(msg != null ? msg.status : null, "READ", StringComparison.OrdinalIgnoreCase);
            if (!isRead)
                unreadCount++;
            var style = isRead ? FontStyles.Normal : FontStyles.Bold;
            if (senderText != null)
                senderText.fontStyle = style;
            if (contentText != null)
                contentText.fontStyle = style;
            if (timeText != null)
                timeText.fontStyle = style;

            var button = item.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }

            var receiveButton = FindButtonByName(item.transform, "Receive");
            if (receiveButton != null)
            {
                bool hasRewards = HasClaimableRewards(msg);
                bool hasEarlyExitLuckyDraw = IsEarlyExitLuckyDrawMessage(msg);
                bool canHandleMessage = hasRewards || hasEarlyExitLuckyDraw;
                receiveButton.gameObject.SetActive(canHandleMessage);
                receiveButton.onClick.RemoveAllListeners();
                receiveButton.interactable = canHandleMessage;

                var receiveButtonText = receiveButton.GetComponentInChildren<TMP_Text>(true);
                if (receiveButtonText != null && hasEarlyExitLuckyDraw)
                    receiveButtonText.text = GetLocalizedOrFallback("system_message_lucky_draw", "Lucky Draw");

                if (canHandleMessage)
                {
                    var targetMessage = msg;
                    var targetButton = receiveButton;
                    receiveButton.onClick.AddListener(() =>
                    {
                        if (!targetButton.interactable)
                            return;

                        targetButton.interactable = false;
                        if (IsEarlyExitLuckyDrawMessage(targetMessage))
                            StartCoroutine(OpenEarlyExitLuckyDrawRoutine(targetMessage, targetButton));
                        else
                            StartCoroutine(ClaimSystemMessageRewardRoutine(targetMessage, targetButton));
                    });
                }
            }
        }
        UpdatePendingSystemMessageBadge(unreadCount);
        var rect = SystemMessageListPanel.GetComponent<RectTransform>();
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private IEnumerator OpenEarlyExitLuckyDrawRoutine(MessageModel message, Button receiveButton)
    {
        if (message == null)
            yield break;

        int playerId = PlayerId;
        var api = GetApiManager();
        if (playerId <= 0 || PopupHelper.Instance == null || api == null)
        {
            if (receiveButton != null)
                receiveButton.interactable = IsEarlyExitLuckyDrawMessage(message);
            yield break;
        }

        var loadingManager = LoadingManager.Instance;
        GameObject loadingScreen = loadingManager != null ? loadingManager.UILoadingScreenPrefab : null;
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        SystemMessageClaimResponse claimResponse = null;
        yield return StartCoroutine(api.RunTask(
            api.ClaimSystemMessageRewardWithResult(message),
            r => claimResponse = r));

        if (loadingScreen != null)
            loadingScreen.SetActive(false);

        if (claimResponse != null && claimResponse.success)
        {
            LoadSystemMessages();
            MenuController.Instance?.ReloadPlayerInfoData();
            var displayReward = claimResponse.luckyDrawReward ?? new LuckyDrawAfterMatchReward();
            PopupHelper.Instance.ShowLuckyDrawAfterMatchPopup(playerId, false, null, displayReward);
            yield break;
        }

        string failureMessage = GetLocalizedOrFallback("system_message_claim_failure", "Failed to claim reward");
        GetNotificationHelper()?.ShowNotification(failureMessage, false);
        if (receiveButton != null)
            receiveButton.interactable = IsEarlyExitLuckyDrawMessage(message);
    }

    private IEnumerator ClaimSystemMessageRewardRoutine(MessageModel message, Button receiveButton)
    {
        if (message == null)
            yield break;

        var api = GetApiManager();
        if (api == null)
        {
            string failureMessage = GetLocalizedOrFallback("system_message_claim_failure", "Failed to claim reward");
            GetNotificationHelper()?.ShowNotification(failureMessage, false);
            if (receiveButton != null)
                receiveButton.interactable = HasClaimableRewards(message);
            yield break;
        }

        var loadingManager = LoadingManager.Instance;
        GameObject loadingScreen = loadingManager != null ? loadingManager.UILoadingScreenPrefab : null;
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        if (receiveButton != null)
            receiveButton.interactable = false;

        bool success = false;
        yield return StartCoroutine(api.RunTask(
            api.ClaimSystemMessageReward(message),
            r => success = r));

        if (loadingScreen != null)
            loadingScreen.SetActive(false);

        string notificationKey = success ? "system_message_claim_success" : "system_message_claim_failure";
        string notificationFallback = success ? "Reward claimed successfully" : "Failed to claim reward";
        string notificationMessage = GetLocalizedOrFallback(notificationKey, notificationFallback);
        GetNotificationHelper()?.ShowNotification(notificationMessage, success);

        if (success)
        {
            LoadSystemMessages();
            MenuController.Instance?.ReloadPlayerInfoData();
        }
        else if (receiveButton != null)
        {
            receiveButton.interactable = HasClaimableRewards(message);
        }
    }

    private string GetMessageBodyWithRewards(MessageModel msg)
    {
        if (msg == null)
            return string.Empty;

        string baseContent = msg.message ?? string.Empty;
        baseContent = StripEarlyExitLuckyDrawMarker(baseContent);
        baseContent = TranslateSystemMessageContent(baseContent);
        bool hasMessageBody = !string.IsNullOrWhiteSpace(baseContent);

        string rewardsText = BuildRewardDescription(msg);
        if (string.IsNullOrEmpty(rewardsText))
            return hasMessageBody ? baseContent : string.Empty;

        if (!hasMessageBody)
            return rewardsText;

        string messageWithSpacing = baseContent;
        if (!messageWithSpacing.EndsWith("\n"))
            messageWithSpacing += "\n";
        return messageWithSpacing + rewardsText;
    }

    private string TranslateSystemMessageContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content ?? string.Empty;

        string trimmedContent = content.Trim();
        if (IsLocalizationKeyCandidate(trimmedContent))
        {
            string directLocalizedText = ResolveLocalizedText(trimmedContent);
            if (!string.IsNullOrWhiteSpace(directLocalizedText))
                return directLocalizedText;
        }

        int languageStart = content.IndexOf('[');
        int languageEnd = languageStart >= 0 ? content.IndexOf(']', languageStart + 1) : -1;
        if (languageStart < 0 || languageEnd <= languageStart)
            return content;

        int translationStart = content.IndexOf('(', languageEnd + 1);
        int translationEnd = translationStart >= 0 ? content.IndexOf(')', translationStart + 1) : -1;
        if (translationStart < 0 || translationEnd <= translationStart)
            return content;

        string rawKey = content.Substring(translationStart + 1, translationEnd - translationStart - 1);
        if (string.IsNullOrWhiteSpace(rawKey))
            return content;

        string trimmedKey = rawKey.Trim();
        if (string.IsNullOrEmpty(trimmedKey))
            return content;

        string localizedText = ResolveLocalizedText(trimmedKey);
        if (string.IsNullOrWhiteSpace(localizedText))
            return content;

        int leadingWhitespace = 0;
        while (leadingWhitespace < rawKey.Length && char.IsWhiteSpace(rawKey[leadingWhitespace]))
            leadingWhitespace++;

        int trailingWhitespace = 0;
        while (trailingWhitespace < rawKey.Length - leadingWhitespace && char.IsWhiteSpace(rawKey[rawKey.Length - 1 - trailingWhitespace]))
            trailingWhitespace++;

        string leadingSegment = leadingWhitespace > 0 ? rawKey.Substring(0, leadingWhitespace) : string.Empty;
        string trailingSegment = trailingWhitespace > 0 ? rawKey.Substring(rawKey.Length - trailingWhitespace) : string.Empty;

        string prefix = content.Substring(0, translationStart + 1);
        string suffix = content.Substring(translationEnd);

        return string.Concat(prefix, leadingSegment, localizedText, trailingSegment, suffix);
    }

    private string ResolveLocalizedText(string localizationKey)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
            return null;

        var localization = GetLocalizationManager();
        if (localization == null)
            return null;

        string localizedText = localization.GetText(localizationKey.Trim());
        if (string.IsNullOrWhiteSpace(localizedText) ||
            string.Equals(localizedText, localizationKey.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return localizedText;
    }

    private static bool IsLocalizationKeyCandidate(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
                continue;

            return false;
        }

        return true;
    }

    private static bool IsEarlyExitLuckyDrawMessage(MessageModel msg)
    {
        return msg != null &&
               !string.IsNullOrEmpty(msg.message) &&
               msg.message.StartsWith(EarlyExitLuckyDrawMessageMarker, StringComparison.Ordinal);
    }

    private static string StripEarlyExitLuckyDrawMarker(string content)
    {
        if (string.IsNullOrEmpty(content) ||
            !content.StartsWith(EarlyExitLuckyDrawMessageMarker, StringComparison.Ordinal))
        {
            return content ?? string.Empty;
        }

        string withoutMarker = content.Substring(EarlyExitLuckyDrawMessageMarker.Length).TrimStart();
        if (withoutMarker.StartsWith("matchId=", StringComparison.OrdinalIgnoreCase))
        {
            int lineBreakIndex = withoutMarker.IndexOf('\n');
            if (lineBreakIndex >= 0 && lineBreakIndex + 1 < withoutMarker.Length)
                withoutMarker = withoutMarker.Substring(lineBreakIndex + 1);
            else
                withoutMarker = string.Empty;
        }

        return withoutMarker.TrimStart();
    }

    private string BuildRewardDescription(MessageModel msg)
    {
        if (msg == null)
            return null;

        var rewardParts = new List<string>();
        AppendRewardPart(rewardParts, msg.ringBallReward, "system_message_ringball_reward", "Ring Ball");
        AppendRewardPart(rewardParts, msg.moneyReward, "system_message_money_reward", "Money");

        if (msg.itemRewardId > 0)
        {
            string itemLabel = GetLocalizedOrFallback("system_message_item_reward", "Item");
            rewardParts.Add($"{itemLabel} #{msg.itemRewardId}");
        }

        if (rewardParts.Count == 0)
            return null;

        string rewardsLabel = GetLocalizedOrFallback("system_message_rewards_label", "Rewards");
        return $"{rewardsLabel}: {string.Join(", ", rewardParts)}";
    }

    private static bool HasClaimableRewards(MessageModel msg)
    {
        if (msg == null)
            return false;

        return msg.ringBallReward > 0 || msg.moneyReward > 0 || msg.itemRewardId > 0 || msg.itemId > 0 || msg.seqId > 0;
    }

    private void AppendRewardPart(List<string> parts, int value, string localizationKey, string fallback)
    {
        if (value <= 0)
            return;

        string label = GetLocalizedOrFallback(localizationKey, fallback);
        parts.Add($"+{value} {label}");
    }

    private string GetLocalizedOrFallback(string key, string fallback)
    {
        var localization = GetLocalizationManager();
        if (localization == null)
            return fallback;

        string localized = localization.GetText(key);
        if (string.IsNullOrWhiteSpace(localized) || string.Equals(localized, key, StringComparison.OrdinalIgnoreCase))
            return fallback;

        return localized;
    }

    private static string GetSafeSenderName(MessageModel msg)
    {
        if (msg == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(msg.PlayerName))
            return msg.PlayerName;

        if (msg.sender != null && !string.IsNullOrWhiteSpace(msg.sender.PlayerName))
            return msg.sender.PlayerName;

        return "Unknown";
    }

    public void SendMessageToFriend(int friendId, string message, int itemId = 0, int seqId = 0)
    {
        int senderId = PlayerId;
        if (senderId == 0 || friendId == 0)
            return;

        string trimmedContent = message != null ? message.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedContent))
        {
            ShowEmptyMessageNotification();
            return;
        }

        StartCoroutine(SendMessageRoutine(senderId, friendId, trimmedContent, itemId, seqId));
    }

    private IEnumerator SendMessageRoutine(int senderId, int receiverId, string content, int itemId, int seqId)
    {
        GameObject loadingScreen = LoadingManager.Instance != null ? LoadingManager.Instance.UILoadingScreenPrefab : null;
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        bool success = false;
        var api = GetApiManager();
        if (api != null)
        {
            yield return StartCoroutine(api.RunTask(
                api.SendMessage(senderId, receiverId, content, itemId, seqId),
                r => success = r));
        }
        else
        {
            yield return null;
        }

        if (loadingScreen != null)
            loadingScreen.SetActive(false);

        if (success)
        {
            WebSocketHelper.Instance?.Send(new WebSocketHelper.MessagePayload
            {
                type = "message",
                senderId = senderId,
                receiverId = receiverId,
                seqMess = seqId,
                content = content
            });

            ClearMessageInput();

            var newMessage = new MessageModel
            {
                senderId = senderId,
                receiverId = receiverId,
                message = content,
                itemId = itemId,
                seqId = seqId,
                seqMess = seqId,
                status = "READ",
                createdAt = DateTime.UtcNow.ToString("o"),
                PlayerName = GetFriendDisplayName(receiverId)
            };

            OnFriendMessageSent(newMessage);
        }

        var notification = GetNotificationHelper();
        if (notification != null)
        {
            var localization = GetLocalizationManager();
            string messageText = localization != null
                ? localization.GetText(success ? "noti_friend_true" : "noti_friend_false")
                : string.Empty;
            if(success == false)
                notification.ShowNotification(messageText, success);
        }
    }

    private void ShowInputFrom(int friendId)
    {
        if (activeConversationPopup == null)
            return;

        if (activeConversationPopup.SendMessageButton != null)
        {
            activeConversationPopup.SendMessageButton.gameObject.SetActive(true);
            activeConversationPopup.SendMessageButton.onClick.RemoveAllListeners();
            activeConversationPopup.SendMessageButton.onClick.AddListener(() =>
            {
                string message = activeConversationPopup.MessageInput != null ? activeConversationPopup.MessageInput.text : string.Empty;
                if (string.IsNullOrWhiteSpace(message))
                {
                    ShowEmptyMessageNotification();
                    return;
                }

                SendMessageToFriend(friendId, message);
            });
        }

        if (activeConversationPopup.MessageInput != null)
        {
            activeConversationPopup.MessageInput.characterLimit = 100;
            activeConversationPopup.MessageInput.gameObject.SetActive(true);
        }
    }

    public void HideInputFrom()
    {
        if (activeConversationPopup == null)
            return;

        if (activeConversationPopup.SendMessageButton != null)
            activeConversationPopup.SendMessageButton.gameObject.SetActive(false);
        if (activeConversationPopup.MessageInput != null)
            activeConversationPopup.MessageInput.gameObject.SetActive(false);
    }

    private void CloseConversationPopup(bool reloadMessages, bool closePopup = true)
    {
        if (activeConversationPopup == null)
            return;

        if (loadConversationCoroutine != null)
        {
            StopCoroutine(loadConversationCoroutine);
            loadConversationCoroutine = null;
        }

        ClearConversationContent();
        HideInputFrom();

        var popupInstance = activeConversationPopup.gameObject;
        activeConversationPopup = null;
        activeConversationFriendId = null;

        if (reloadMessages)
            LoadMessages();

        if (popupInstance != null && closePopup)
        {
            var popupHelper = PopupHelper.Instance;
            if (popupHelper != null)
                popupHelper.ClosePopup(popupInstance);
            else
                Destroy(popupInstance);
        }
    }

    private void ReadMessage(int seqMess)
    {
        StartCoroutine(ReadMessageRoutine(seqMess));
    }

    private IEnumerator ReadMessageRoutine(int seqMess)
    {
        if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);

        bool success = false;
        var api = GetApiManager();
        if (api != null)
        {
            yield return StartCoroutine(api.RunTask(
                api.ReadMessage(PlayerId, seqMess),
                r => success = r));
        }

        if (LoadingManager.Instance != null && LoadingManager.Instance.UILoadingScreenPrefab != null)
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);

        if (success)
            LoadMessages();
    }

    private void ShowEmptyMessageNotification()
    {
        string message = "Tin nhắn không được để trống";
        var localization = GetLocalizationManager();
        if (localization != null)
        {
            string localized = localization.GetText("noti_message_empty");
            if (!string.IsNullOrWhiteSpace(localized) &&
                !string.Equals(localized, "noti_message_empty", StringComparison.OrdinalIgnoreCase))
            {
                message = localized;
            }
        }

        GetNotificationHelper()?.ShowNotification(message, false);
    }

    private APIManager GetApiManager()
    {
        if (apiManager == null)
            apiManager = APIManager.Instance;
        return apiManager;
    }

    private NotificationHelper GetNotificationHelper()
    {
        if (notificationHelper == null)
            notificationHelper = NotificationHelper.Instance;
        return notificationHelper;
    }

    private LocalizationManager GetLocalizationManager()
    {
        if (localizationManager == null)
            localizationManager = LocalizationManager.Instance;
        return localizationManager;
    }
}
