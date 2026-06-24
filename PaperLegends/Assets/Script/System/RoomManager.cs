using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Fusion.Sockets;
using Fusion;
using Fusion.Photon.Realtime;
using TMPro;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Linq;




public class RoomManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static RoomManager Instance;
    [Header("ROOM UI")]
    public Transform roomContentParent;
    public GameObject roomItemPrefab;
    public GameObject RoomSinger;
    public GameObject LobbyObject;
    public GameObject mainMenuObject;
    [SerializeField]
    private TMP_Text roomMapNameText;
    [SerializeField]
    private TMP_Text roomBetCountText;
    [SerializeField]
    private TMP_Text roomCountdownText;
    [Header("ROOM PLAYER LIST UI")]
    [SerializeField]
    private Transform roomPlayerListRoot;
    [SerializeField]
    private GameObject roomPlayerListItemPrefab;
    [SerializeField]
    private GameObject roomPlayerEmptySlotPrefab;
    [SerializeField]
    private Button readyButton;
    [SerializeField]
    private Button cancelReadyButton;
    [SerializeField]
    private Button startGameButton;
    [SerializeField]
    private Button cancelStartGameButton;
    [SerializeField]
    private CanvasGroup startGameButtonGroup;
    [Header("ROOM SEARCH")]
    [SerializeField]
    private TMP_InputField roomCodeInput;
    [SerializeField]
    private Button roomSearchButton;

    public TMP_InputField inputField;    // Ô nhập tin nhắn
    public Transform chatContentParent;  // Parent chứa các item chat trong GridLayout
    public GameObject chatItemPrefab;    // Prefab cho mỗi item chat

    [Header("FRIEND INVITE UI")]
    [SerializeField]
    private Transform friendInviteListPanel;
    [SerializeField]
    private GameObject friendInviteItemPrefab;
    [SerializeField]
    private Button refreshFriendInviteButton;
    [SerializeField]
    private Sprite onlineSprite;
    [SerializeField]
    private Sprite offlineSprite;
    [Header("NET WORK CONFIG")]

    public string proomName;
    private Dictionary<string, Dictionary<PlayerRef, bool>> roomReadyMap = new();
    private int roomId;
    private int InputbetCount = 8;
    private int selectedMaxRound = MinRoomRoundCount;
    private GameMapId selectedMapId = GameMapId.HometownHouse;
    private RoomResponse latestRoom;
    private readonly Dictionary<int, RoomData> roomLookup = new();
    private readonly Dictionary<string, int> sessionNameToRoomId = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> roomIdToSessionName = new();
    private int roomOwnerId;
    private bool isRoomOwner;
    private string currentRoomName;
    private Coroutine roomStartCoroutine;
    private Coroutine roomStartRequestCoroutine;
    private Coroutine roomCountdownCoroutine;
    private bool _isLeavingRoom;
    private bool _awaitingMainMenuLeaveConfirmation;
    private Coroutine keepMainMenuHiddenCoroutine;
    private bool _mainMenuWasActiveBeforeLeavePrompt;
    private bool isRoomCountdownActive;
    private readonly Dictionary<int, FriendInviteEntry> friendInviteEntries = new();
    private readonly Dictionary<int, RoomPlayerListItemUI> roomPlayerItems = new();
    private readonly Dictionary<int, bool> roomPlayerReadyStates = new();
    private readonly Dictionary<int, RoomItemUI> roomItemLookup = new();
    private readonly List<int> roomOrder = new();
    private readonly Dictionary<int, Coroutine> roomAvatarLoaders = new();
    private readonly Dictionary<int, Coroutine> inviteCooldownCoroutines = new();
    private readonly Dictionary<int, Texture2D> roomAvatarTextures = new();
    private readonly Dictionary<int, Sprite> roomAvatarSprites = new();
    private NetworkRunner lobbyRunner;
    private int nextGeneratedRoomId = 100000;
    private const int RoomStartCountdownSeconds = 5;
    private const int MaxRoomPlayerSlots = 3;
    private const int MinRoomRoundCount = 5;
    private const int MaxRoomRoundCount = 10;
    private const float FriendInviteCooldownSeconds = 5f;
    [Header("ROOM CREATE CONFIG")]
    [SerializeField]
    private List<MapOptionData> mapOptions = new();
    [SerializeField]
    private GameMapId defaultMapId = GameMapId.HometownHouse;
    [SerializeField]
    private int defaultBetAmount = 8;
    [SerializeField]
    [Tooltip("SessionProperties config string (e.g., key=value;key2=value2) to match dedicated server settings.")]
    private string sessionPropertiesConfig = string.Empty;
    [Header("ROOM SINGER VISUAL")]
    [SerializeField]
    private Transform roomSingerVisualRoot;
    [SerializeField]
    private Transform[] roomSingerSpawnPoints = new Transform[3];
    private readonly Dictionary<int, GameObject> roomSingerVisuals = new();

    private class FriendInviteEntry
    {
        public Image StatusIcon;
        public Button ChallengeButton;
        public Button InvitedButton;
        public bool IsOnline;
        public bool IsInviteCoolingDown;
    }
    private void Awake()
    {
        Instance = this;
        selectedMapId = defaultMapId;
    }

    private void Start()
    {
        HookFriendInviteRefreshButton();
        HookRoomActionButtons();
        HookRoomSearchButton();
        WebSocketHelper.OnRoomReadyUpdate += HandleRoomReadyUpdate;
        WebSocketHelper.OnRoomStart += HandleRoomStart;
        WebSocketHelper.OnRoomStartCanceled += HandleRoomStartCanceled;
        WebSocketHelper.OnRoomKicked += HandleRoomKicked;
        WebSocketHelper.OnRoomUsersUpdate += HandleRoomUsersUpdate;
        WebSocketHelper.OnRoomChatMessage += HandleRoomChatMessage;
        WebSocketHelper.OnFriendListMessage += HandleFriendListMessage;
    }

    private void OnDisable()
    {
        UnhookFriendInviteRefreshButton();
        UnhookRoomActionButtons();
        UnhookRoomSearchButton();
        WebSocketHelper.OnRoomReadyUpdate -= HandleRoomReadyUpdate;
        WebSocketHelper.OnRoomStart -= HandleRoomStart;
        WebSocketHelper.OnRoomStartCanceled -= HandleRoomStartCanceled;
        WebSocketHelper.OnRoomKicked -= HandleRoomKicked;
        WebSocketHelper.OnRoomUsersUpdate -= HandleRoomUsersUpdate;
        WebSocketHelper.OnRoomChatMessage -= HandleRoomChatMessage;
        WebSocketHelper.OnFriendListMessage -= HandleFriendListMessage;
        ClearInviteCooldowns();
        ClearRoomAvatarCache();

        if (lobbyRunner != null)
            lobbyRunner.RemoveCallbacks(this);
    }

    private int PlayerId
    {
        get
        {
            var loginModel = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.loginUserModel : null;
            return loginModel != null ? loginModel.UserId : 0;
        }
    }

    private bool HasValidPlayerId() => PlayerId > 0;


    #region [================================== THÔNG TIN PHÒNG ==================================]

    public void onClickStartGame()
    {
        if (!isRoomOwner)
        {
            NotificationHelper.Instance?.ShowNotification("Chỉ chủ phòng mới có thể bắt đầu.", false);
            return;
        }

        if (roomPlayerItems.Count <= 1)
        {
            NotificationHelper.Instance?.ShowNotification("Cần ít nhất 2 người chơi trong phòng để bắt đầu.", false);
            UpdateStartGameAvailability();
            return;
        }

        if (!AreAllRoomPlayersReady())
        {
            NotificationHelper.Instance?.ShowNotification("Cần tất cả người chơi sẵn sàng trước khi bắt đầu.", false);
            UpdateStartGameAvailability();
            return;
        }

        if (roomStartRequestCoroutine != null)
            StopCoroutine(roomStartRequestCoroutine);

        roomStartRequestCoroutine = StartCoroutine(RequestStartRoomMatch());
    }
    //CHAT
    public void ShowChat(string senderName,string message)
    {
        // Tạo một đối tượng chat item mới trong grid
        GameObject chatItem = Instantiate(chatItemPrefab, chatContentParent);
        TMP_Text chatText = chatItem.GetComponent<TMP_Text>();

        // Cập nhật nội dung tin nhắn
        chatText.text = $"{senderName}: {message}";
        Debug.Log(chatText.text);
    }    
    public void OnSendButtonPressed()
    {
        if (roomId <= 0 || !HasValidPlayerId())
        {
            Debug.LogWarning("[RoomManager] Không thể gửi chat vì chưa có roomId hoặc playerId hợp lệ.");
            return;
        }

        string message = inputField.text?.Trim();
        if (string.IsNullOrEmpty(message))
            return; // Nếu không có tin nhắn thì không gửi

        string senderName = GameManagerNetWork.Instance?.loginUserModel?.Username ?? "Player";

        if (WebSocketHelper.Instance == null || !WebSocketHelper.Instance.IsConnected)
        {
            Debug.LogWarning("[RoomManager] WebSocket chưa kết nối, không thể gửi chat.");
            return;
        }
        WebSocketHelper.Instance.Send(new WebSocketHelper.RoomChatMessage
        {
            type = "room_chat",
            roomId = roomId,
            senderId = PlayerId,
            senderName = senderName,
            message = message
        });

        // Xóa ô nhập sau khi gửi
        inputField.text = "";
    }

    private void HookFriendInviteRefreshButton()
    {
        if (refreshFriendInviteButton == null)
            return;

        refreshFriendInviteButton.onClick.RemoveAllListeners();
        refreshFriendInviteButton.onClick.AddListener(LoadFriendInviteList);
    }

    private void HookRoomSearchButton()
    {
        if (roomSearchButton == null)
            return;

        roomSearchButton.onClick.RemoveAllListeners();
        roomSearchButton.onClick.AddListener(OnRoomSearchButtonPressed);
    }

    private void UnhookRoomSearchButton()
    {
        if (roomSearchButton == null)
            return;

        roomSearchButton.onClick.RemoveListener(OnRoomSearchButtonPressed);
    }

    private void UnhookFriendInviteRefreshButton()
    {
        if (refreshFriendInviteButton == null)
            return;

        refreshFriendInviteButton.onClick.RemoveAllListeners();
    }

    public void LoadFriendInviteList()
    {
        if (!HasValidPlayerId() || friendInviteListPanel == null || friendInviteItemPrefab == null)
            return;

        RequestFriendInviteList();
    }

    private void HookRoomActionButtons()
    {
        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(ReadyButton);
        }

        if (cancelReadyButton != null)
        {
            cancelReadyButton.onClick.RemoveAllListeners();
            cancelReadyButton.onClick.AddListener(CancelReadyButton);
        }

        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveAllListeners();
            startGameButton.onClick.AddListener(onClickStartGame);
        }

        if (cancelStartGameButton != null)
        {
            cancelStartGameButton.onClick.RemoveAllListeners();
            cancelStartGameButton.gameObject.SetActive(false);
        }
    }

    private void UnhookRoomActionButtons()
    {
        if (readyButton != null)
            readyButton.onClick.RemoveAllListeners();
        if (cancelReadyButton != null)
            cancelReadyButton.onClick.RemoveAllListeners();
        if (startGameButton != null)
            startGameButton.onClick.RemoveAllListeners();
        if (cancelStartGameButton != null)
            cancelStartGameButton.onClick.RemoveAllListeners();
    }

    private void RequestFriendInviteList()
    {
        if (!HasValidPlayerId())
        {
            Debug.LogWarning("[RoomManager] Không thể gọi GetFriendList vì playerId không hợp lệ.");
            return;
        }

        Debug.Log($"[RoomManager] Bắt đầu tải danh sách bạn bè từ API GetFriendList. playerId={PlayerId}");

        // if (WebSocketHelper.Instance != null && WebSocketHelper.Instance.IsConnected)
        // {
        //     WebSocketHelper.Instance.Send(new WebSocketHelper.FriendListRequestMessage
        //     {
        //         type = "friend_list",
        //         playerId = PlayerId
        //     });
        //     return;
        // }

        StartCoroutine(LoadFriendInviteListRoutineFallback());
    }

    private IEnumerator LoadFriendInviteListRoutineFallback()
    {
        SetFriendInviteLoading(true);
        PlayerInfoStruct[] friends = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetFriendList(PlayerId),
            r => friends = r));

        SetFriendInviteLoading(false);

        if (friends == null)
        {
            Debug.LogWarning($"[RoomManager] API GetFriendList trả về null. playerId={PlayerId}");
        }
        else if (friends.Length == 0)
        {
            Debug.LogWarning($"[RoomManager] API GetFriendList trả về 0 bạn bè. playerId={PlayerId}");
        }
        else
        {
            Debug.Log($"[RoomManager] API GetFriendList trả về {friends.Length} bạn bè. playerId={PlayerId}");
        }

        BuildFriendInviteList(friends);
    }

    private void SetFriendInviteLoading(bool isActive)
    {
        if (LoadingManager.Instance?.UILoadingScreenPrefab != null)
        {
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(isActive);
        }
        else
        {
            Debug.LogWarning("[RoomManager] Không tìm thấy LoadingManager/UIPrefab để hiển thị loading khi tải friend list.");
        }
    }

    private void BuildFriendInviteList(PlayerInfoStruct[] friends)
    {
        if (friendInviteListPanel == null || friendInviteItemPrefab == null)
        {
            Debug.LogWarning("[RoomManager] Thiếu friendInviteListPanel hoặc friendInviteItemPrefab, không thể dựng danh sách mời bạn.");
            return;
        }

        foreach (Transform child in friendInviteListPanel)
            Destroy(child.gameObject);

        ClearInviteCooldowns();
        friendInviteEntries.Clear();

        if (friends == null)
        {
            Debug.LogWarning("[RoomManager] Danh sách bạn bè null, không có dữ liệu để hiển thị.");
            return;
        }

        if (friends.Length == 0)
        {
            Debug.LogWarning("[RoomManager] Danh sách bạn bè rỗng (0 data) nên UI không có item nào để hiển thị.");
            return;
        }

        foreach (var friend in friends)
        {
            GameObject item = Instantiate(friendInviteItemPrefab, friendInviteListPanel);
            var itemUI = item.GetComponent<RoomFriendInviteItemUI>();
            if (itemUI == null)
            {
                Debug.LogWarning("[RoomManager] friendInviteItemPrefab thiếu component RoomFriendInviteItemUI.");
                continue;
            }

            if (itemUI.PlayerNameText != null)
                itemUI.PlayerNameText.text = friend.fullname.ToString();
            if (itemUI.LevelText != null)
                itemUI.LevelText.text = $"Lv {friend.level}";

            Button challengeButton = itemUI.ChallengeButton;
            Button invitedButton = itemUI.InvitedButton;
            if (challengeButton != null)
                challengeButton.gameObject.SetActive(false);
            if (invitedButton != null)
            {
                invitedButton.gameObject.SetActive(false);
                invitedButton.onClick.RemoveAllListeners();
            }

            int friendId = friend.playerId;
            if (challengeButton != null)
            {
                challengeButton.onClick.RemoveAllListeners();
                challengeButton.onClick.AddListener(() => InviteFriendToRoom(friendId));
            }

            if (itemUI.MessageButton != null)
            {
                itemUI.MessageButton.gameObject.SetActive(false);
                itemUI.MessageButton.onClick.RemoveAllListeners();
            }

            if (itemUI.RemoveButtonObject != null)
                itemUI.RemoveButtonObject.SetActive(false);

            if (itemUI.AcceptButtonObject != null)
                itemUI.AcceptButtonObject.SetActive(false);

            if (itemUI.DeclineButtonObject != null)
                itemUI.DeclineButtonObject.SetActive(false);

            friendInviteEntries[friendId] = new FriendInviteEntry
            {
                StatusIcon = itemUI.OnlineIcon,
                ChallengeButton = challengeButton,
                InvitedButton = invitedButton
            };
            UpdateFriendInviteEntry(friendId, false);

            if (WebSocketHelper.Instance != null)
            {
                WebSocketHelper.Instance.CheckPlayerOnline(friendId, isOnline =>
                {
                    UpdateFriendInviteEntry(friendId, isOnline);
                });
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(friendInviteListPanel.GetComponent<RectTransform>());
    }

    private void BuildFriendInviteListFromSocket(List<WebSocketHelper.FriendInfo> friends)
    {
        if (friendInviteListPanel == null || friendInviteItemPrefab == null)
            return;

        foreach (Transform child in friendInviteListPanel)
            Destroy(child.gameObject);

        ClearInviteCooldowns();
        friendInviteEntries.Clear();

        if (friends == null)
            return;

        foreach (var friend in friends)
        {
            if (friend == null)
                continue;

            GameObject item = Instantiate(friendInviteItemPrefab, friendInviteListPanel);
            var itemUI = item.GetComponent<RoomFriendInviteItemUI>();
            if (itemUI == null)
            {
                Debug.LogWarning("[RoomManager] friendInviteItemPrefab thiếu component RoomFriendInviteItemUI.");
                continue;
            }

            if (itemUI.PlayerNameText != null)
                itemUI.PlayerNameText.text = friend.fullname;
            if (itemUI.LevelText != null)
                itemUI.LevelText.text = $"Lv {friend.level}";

            Button challengeButton = itemUI.ChallengeButton;
            Button invitedButton = itemUI.InvitedButton;
            if (challengeButton != null)
                challengeButton.gameObject.SetActive(false);
            if (invitedButton != null)
            {
                invitedButton.gameObject.SetActive(false);
                invitedButton.onClick.RemoveAllListeners();
            }

            int friendId = friend.playerId;
            if (challengeButton != null)
            {
                challengeButton.onClick.RemoveAllListeners();
                challengeButton.onClick.AddListener(() => InviteFriendToRoom(friendId));
            }

            if (itemUI.MessageButton != null)
            {
                itemUI.MessageButton.gameObject.SetActive(false);
                itemUI.MessageButton.onClick.RemoveAllListeners();
            }

            if (itemUI.RemoveButtonObject != null)
                itemUI.RemoveButtonObject.SetActive(false);

            if (itemUI.AcceptButtonObject != null)
                itemUI.AcceptButtonObject.SetActive(false);

            if (itemUI.DeclineButtonObject != null)
                itemUI.DeclineButtonObject.SetActive(false);

            friendInviteEntries[friendId] = new FriendInviteEntry
            {
                StatusIcon = itemUI.OnlineIcon,
                ChallengeButton = challengeButton,
                InvitedButton = invitedButton
            };

            UpdateFriendInviteEntry(friendId, friend.isOnline);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(friendInviteListPanel.GetComponent<RectTransform>());
    }

    private void UpdateFriendInviteEntry(int friendId, bool isOnline)
    {
        if (!friendInviteEntries.TryGetValue(friendId, out var entry))
            return;

        entry.IsOnline = isOnline;
        UpdateFriendInviteIcon(entry.StatusIcon, isOnline);
        UpdateFriendInviteActionState(entry);
    }

    private void UpdateFriendInviteActionState(FriendInviteEntry entry)
    {
        if (entry == null)
            return;

        bool showInvitedState = entry.IsOnline && entry.IsInviteCoolingDown;
        bool showChallengeState = entry.IsOnline && !entry.IsInviteCoolingDown;

        if (entry.InvitedButton != null)
            entry.InvitedButton.gameObject.SetActive(showInvitedState);

        if (entry.ChallengeButton != null)
            entry.ChallengeButton.gameObject.SetActive(showChallengeState);
    }

    private void UpdateFriendInviteIcon(Image icon, bool isOnline)
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

    private void InviteFriendToRoom(int friendId)
    {
        Debug.Log($"[RoomManager] InviteFriendToRoom click. friendId={friendId}, roomId={roomId}, playerId={PlayerId}");

        if (friendInviteEntries.TryGetValue(friendId, out var entry))
        {
            if (entry.IsInviteCoolingDown)
            {
                Debug.LogWarning($"[RoomManager] Bỏ qua lời mời vì đang cooldown. friendId={friendId}");
                return;
            }

            if (!entry.IsOnline)
            {
                Debug.LogWarning($"[RoomManager] Bỏ qua lời mời vì bạn đang offline. friendId={friendId}");
                return;
            }
        }

        if (!HasValidPlayerId())
        {
            Debug.LogWarning("[RoomManager] Không thể mời bạn vì playerId không hợp lệ.");
            return;
        }

        if (WebSocketHelper.Instance == null || !WebSocketHelper.Instance.IsConnected)
        {
            Debug.LogWarning($"[RoomManager] Không thể mời bạn vì WebSocket chưa kết nối. friendId={friendId}, roomId={roomId}");
            NotificationHelper.Instance?.ShowNotification("Mất kết nối socket, không thể gửi lời mời.", false);
            return;
        }

        int bet = GetStartingBet();
        var challengePayload = new WebSocketHelper.ChallengeMessage
        {
            type = "friend_challenge",
            senderId = PlayerId,
            receiverId = friendId,
            bet = bet,
            roomId = roomId
        };
        Debug.Log($"[RoomManager] Gửi friend_challenge. senderId={challengePayload.senderId}, receiverId={challengePayload.receiverId}, bet={challengePayload.bet}, roomId={challengePayload.roomId}");
        WebSocketHelper.Instance.Send(challengePayload);
        StartFriendInviteCooldown(friendId);
    }

    private void StartFriendInviteCooldown(int friendId)
    {
        if (!friendInviteEntries.TryGetValue(friendId, out var entry))
            return;

        if (inviteCooldownCoroutines.TryGetValue(friendId, out var runningCoroutine) && runningCoroutine != null)
            StopCoroutine(runningCoroutine);

        entry.IsInviteCoolingDown = true;
        UpdateFriendInviteActionState(entry);
        inviteCooldownCoroutines[friendId] = StartCoroutine(ResetFriendInviteCooldown(friendId));
    }

    private IEnumerator ResetFriendInviteCooldown(int friendId)
    {
        yield return new WaitForSeconds(FriendInviteCooldownSeconds);

        if (friendInviteEntries.TryGetValue(friendId, out var entry))
        {
            entry.IsInviteCoolingDown = false;
            UpdateFriendInviteActionState(entry);
        }

        inviteCooldownCoroutines.Remove(friendId);
    }

    private void ClearInviteCooldowns()
    {
        foreach (var cooldown in inviteCooldownCoroutines.Values)
        {
            if (cooldown != null)
                StopCoroutine(cooldown);
        }

        inviteCooldownCoroutines.Clear();
    }
    // hiển thị danh sách phòng
    public void LoadRooms()
    {
        StartCoroutine(LoadRoomsCoroutine());
    }

    public void RefreshRooms()
    {
        LoadRooms();
    }

    //hiển thị thông tin có trong phòng
    public void LoadInforRoom(int roomId)
    {
        this.roomId = roomId;
        UpdateRoomContextFromLookup(roomId);
        UpdateRoomInfoUI();
        RequestRoomUsers(roomId);
    }

    private void UpdateRoomContextFromLookup(int roomId)
    {
        if (roomLookup.TryGetValue(roomId, out var roomData) && roomData != null)
        {
            currentRoomName = roomData.roomName;
            InputbetCount = roomData.bet;
            selectedMapId = (GameMapId)roomData.mapId;
            selectedMaxRound = Mathf.Clamp(roomData.GetMaxRound(selectedMaxRound), MinRoomRoundCount, MaxRoomRoundCount);
            roomOwnerId = roomData.createId;
            isRoomOwner = roomOwnerId == PlayerId && roomOwnerId > 0;
            UpdateRoomRoleUI();
        }
    }

    private void UpdateRoomInfoUI()
    {
        UpdateRoomMapNameText();
        UpdateRoomBetCountText();
        UpdateRoomHeader();
    }

    private void UpdateRoomMapNameText()
    {
        if (roomMapNameText == null)
            return;

        var mapKey = selectedMapId.ToString();
        var localizedName = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(mapKey)
            : mapKey;
        roomMapNameText.text = localizedName;
        roomMapNameText.gameObject.SetActive(!string.IsNullOrWhiteSpace(localizedName));
    }

    private void UpdateRoomBetCountText()
    {
        if (roomBetCountText == null)
            return;

        if (InputbetCount > 0)
        {
            roomBetCountText.text = InputbetCount.ToString(CultureInfo.InvariantCulture);
            roomBetCountText.gameObject.SetActive(true);
        }
        else
        {
            roomBetCountText.text = string.Empty;
            roomBetCountText.gameObject.SetActive(false);
        }
    }

    private void UpdateRoomHeader()
    {
        if (MenuController.Instance == null)
            return;

        if (roomId > 0)
        {
            string roomCode = roomId.ToString(CultureInfo.InvariantCulture);
            MenuController.Instance.ShowRoomHeader(roomCode, BackToMainMenu);
        }
        else
        {
            MenuController.Instance.ResetRoomHeader();
        }
    }

    private void OnRoomSearchButtonPressed()
    {
        if (roomCodeInput == null)
        {
            Debug.LogWarning("[RoomManager] Room code input is not assigned.");
            return;
        }

        string roomCode = roomCodeInput.text?.Trim();
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            NotificationHelper.Instance?.ShowNotification("Vui lòng nhập mã phòng.", false);
            return;
        }

        if (!int.TryParse(roomCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedRoomId) || parsedRoomId <= 0)
        {
            NotificationHelper.Instance?.ShowNotification("Mã phòng không hợp lệ.", false);
            return;
        }

        if (!roomLookup.ContainsKey(parsedRoomId))
        {
            NotificationHelper.Instance?.ShowNotification("Không tìm thấy phòng phù hợp.", false);
            return;
        }

        JoinRoom(parsedRoomId);
    }

    private void ResetRoomInfoUI()
    {
        if (roomMapNameText != null)
        {
            roomMapNameText.text = string.Empty;
            roomMapNameText.gameObject.SetActive(false);
        }

        if (roomBetCountText != null)
        {
            roomBetCountText.text = string.Empty;
            roomBetCountText.gameObject.SetActive(false);
        }

        UpdateRoomCountdownText(0);
    }
  
    IEnumerator LoadRoomsCoroutine()
    {
        if (GameManagerNetWork.Instance == null)
        {
            Debug.LogWarning("❌ Load room failed: GameManagerNetWork is missing.");
            yield break;
        }

        lobbyRunner = GameManagerNetWork.Instance.OpenConnectToPhotonServer();
        if (lobbyRunner == null)
        {
            Debug.LogWarning("❌ Load room failed: Lobby runner is null.");
            yield break;
        }

        lobbyRunner.RemoveCallbacks(this);
        lobbyRunner.AddCallbacks(this);

        var joinLobbyTask = lobbyRunner.JoinSessionLobby(SessionLobby.ClientServer);
        while (!joinLobbyTask.IsCompleted)
        {
            yield return null;
        }

        if (joinLobbyTask.IsFaulted)
        {
            Debug.LogWarning($"❌ JoinSessionLobby failed: {joinLobbyTask.Exception?.GetBaseException().Message}");
        }
    }

    private void RebuildRoomsFromSessions(List<SessionInfo> sessionList)
    {
        roomLookup.Clear();
        roomOrder.Clear();
        roomItemLookup.Clear();
        sessionNameToRoomId.Clear();
        roomIdToSessionName.Clear();

        if (roomContentParent == null)
            return;

        foreach (Transform child in roomContentParent)
            Destroy(child.gameObject);

        if (sessionList == null || sessionList.Count == 0)
            return;

        int index = 1;
        foreach (var session in sessionList)
        {
            if (!session.IsValid || !session.IsVisible || !session.IsOpen)
                continue;

            int resolvedRoomId = GetOrCreateRoomId(session.Name);
            var roomData = BuildRoomDataFromSession(resolvedRoomId, session);
            roomLookup[resolvedRoomId] = roomData;
            roomOrder.Add(resolvedRoomId);
            CreateOrUpdateRoomItem(resolvedRoomId, roomData, index);
            index++;
        }
    }

    private int GetOrCreateRoomId(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            return nextGeneratedRoomId++;
        }

        if (sessionNameToRoomId.TryGetValue(sessionName, out int existingRoomId))
            return existingRoomId;

        int resolvedRoomId;
        if (!int.TryParse(sessionName, NumberStyles.Integer, CultureInfo.InvariantCulture, out resolvedRoomId) || resolvedRoomId <= 0)
        {
            resolvedRoomId = nextGeneratedRoomId++;
        }

        sessionNameToRoomId[sessionName] = resolvedRoomId;
        roomIdToSessionName[resolvedRoomId] = sessionName;
        return resolvedRoomId;
    }

    private RoomData BuildRoomDataFromSession(int resolvedRoomId, SessionInfo session)
    {
        int mapId = ReadSessionInt(session, "mapId", "map");
        int bet = ReadSessionInt(session, "bet", "marbBet");
        int ownerId = ReadSessionInt(session, "createId", "ownerId", "hostId");
        int maxRound = ReadSessionInt(session, "maxRound", "MaxRound", "rounds");
        int maxPlayers = session.MaxPlayers > 0 ? (int)session.MaxPlayers : 0;

        return new RoomData
        {
            id = resolvedRoomId,
            roomName = session.Name,
            createPlayerName = string.Empty,
            createId = ownerId,
            bet = bet,
            mapId = mapId,
            maxRound = maxRound,
            rounds = maxRound,
            maxPlayer = maxPlayers,
            maxPlayers = maxPlayers,
            currentPlayers = session.PlayerCount
        };
    }

    private static int ReadSessionInt(SessionInfo session, params string[] keys)
    {
        if (session.Properties == null || keys == null)
            return 0;

        foreach (string key in keys)
        {
            if (string.IsNullOrWhiteSpace(key) || !session.Properties.TryGetValue(key, out var value))
                continue;

            if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                return parsed;
        }

        return 0;
    }

    private int GetRoomIndex(int roomId)
    {
        int index = roomOrder.IndexOf(roomId);
        return index >= 0 ? index + 1 : roomOrder.Count + 1;
    }

    private void RefreshRoomIndices()
    {
        for (int i = 0; i < roomOrder.Count; i++)
        {
            int id = roomOrder[i];
            if (roomLookup.TryGetValue(id, out var roomData) && roomData != null)
            {
                CreateOrUpdateRoomItem(id, roomData, i + 1);
            }
        }
    }

    private void CreateOrUpdateRoomItem(int roomId, RoomData roomData, int index)
    {
        if (roomItemPrefab == null || roomContentParent == null)
            return;

        if (!roomItemLookup.TryGetValue(roomId, out var itemUi) || itemUi == null)
        {
            GameObject newItem = Instantiate(roomItemPrefab, roomContentParent);
            itemUi = newItem.GetComponent<RoomItemUI>();
            if (itemUi == null)
            {
                Debug.LogWarning("⚠️ RoomItemUI component is missing on room item prefab.");
                Destroy(newItem);
                return;
            }
            roomItemLookup[roomId] = itemUi;
        }

        itemUi.Bind(index, roomData, () => JoinRoom(roomId));
    }
    #endregion
 
    #region [================================== TẠO PHÒNG ==================================]
    public void CreateRoom()
    {
        selectedMapId = defaultMapId;
        ShowCreateRoomPopup();
    }

    private void ShowCreateRoomPopup()
    {
        int startingBet = GetStartingBet();
        var options = mapOptions;

        if (PopupHelper.Instance != null)
        {
            PopupHelper.Instance.ShowCreateRoomPopup(options, startingBet, defaultMapId, selectedMaxRound,
                (betCount, maxPlayer, mapId, maxRound) => OnConfirmCreateRoom(betCount, maxPlayer, mapId, maxRound));
        }
        else
        {
            OnConfirmCreateRoom(startingBet, 3, defaultMapId, selectedMaxRound);
        }
    }

    private static bool TryGetMatchRoomValue(Dictionary<string, SessionProperty> sessionProperties, out int value)
    {
        value = (int)TypeMatchGid.MatchRoom;

        if (sessionProperties != null &&
            sessionProperties.TryGetValue("MatchRoom", out var matchRoomValue) &&
            int.TryParse(matchRoomValue.ToString(), out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private Dictionary<string, SessionProperty> BuildSessionPropertiesForMatchRoom()
    {
        var sessionProperties = new Dictionary<string, SessionProperty>
        {
            { "MatchRoom", (SessionProperty)(int)TypeMatchGid.MatchRoom },
            { "maxRound", (SessionProperty)Mathf.Clamp(selectedMaxRound, MinRoomRoundCount, MaxRoomRoundCount) }
        };

        string config = sessionPropertiesConfig;

        if (string.IsNullOrWhiteSpace(config))
        {
            config = Environment.GetEnvironmentVariable("SessionProperties");
        }

        var parsedFromConfig = ParseSessionProperties(config);
        if (parsedFromConfig != null)
        {
            foreach (var kvp in parsedFromConfig)
            {
                sessionProperties[kvp.Key] = kvp.Value;
            }
        }

        return sessionProperties;
    }

    private static Dictionary<string, SessionProperty>? ParseSessionProperties(string sessionPropertiesConfig)
    {
        if (string.IsNullOrWhiteSpace(sessionPropertiesConfig))
        {
            return null;
        }

        var parsedProperties = new Dictionary<string, SessionProperty>(StringComparer.Ordinal);
        var entries = sessionPropertiesConfig.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var entry in entries)
        {
            var kvp = entry.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (kvp.Length != 2)
            {
                continue;
            }

            var key = kvp[0].Trim();
            var value = kvp[1].Trim();

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            SessionProperty sessionProperty;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                sessionProperty = (SessionProperty)intValue;
            }
            else if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
            {
                sessionProperty = (SessionProperty)floatValue;
            }
            else if (bool.TryParse(value, out var boolValue))
            {
                sessionProperty = (SessionProperty)boolValue;
            }
            else
            {
                sessionProperty = (SessionProperty)value;
            }

            parsedProperties[key] = sessionProperty;
        }

        return parsedProperties.Count > 0 ? parsedProperties : null;
    }

    private void OnConfirmCreateRoom(int betCount, int selectedMaxPlayer, GameMapId mapId, int maxRound)
    {
        if (!HasEnoughRingBall(betCount))
        {
            return;
        }

        selectedMapId = mapId;
        selectedMaxRound = Mathf.Clamp(maxRound, MinRoomRoundCount, MaxRoomRoundCount);
        if (mainMenuObject != null)
        {
            mainMenuObject.SetActive(false);
        }
        StartCoroutine(CreateRoomCoroutine(betCount, selectedMaxPlayer, mapId, selectedMaxRound));
    }

    IEnumerator CreateRoomCoroutine(int betCount, int selectedMaxPlayer, GameMapId mapId, int maxRound)
    {
        var loadingScreen = LoadingManager.Instance != null ? LoadingManager.Instance.UILoadingScreenPrefab : null;
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
        }

        string errorMessage = null;
        string sessionName = $"room_{PlayerId}_{Guid.NewGuid():N}";

        RoomResponse response = null;
        yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
            APIManager.Instance.CreateRoomAsync(GameManagerNetWork.Instance.loginUserModel.UserId, betCount, selectedMaxPlayer, (int)mapId, maxRound, sessionName),
            result => response = result));

        if (response == null)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("Tạo phòng thất bại"), false);
            if (loadingScreen != null)
                loadingScreen.SetActive(false);
            yield break;
        }

        roomId = response.roomId;
        proomName = string.IsNullOrEmpty(response.roomName) ? sessionName : response.roomName;
        currentRoomName = proomName;
        latestRoom = response;
        InputbetCount = betCount;
        selectedMapId = mapId;
        selectedMaxRound = Mathf.Clamp(response.GetMaxRound(maxRound), MinRoomRoundCount, MaxRoomRoundCount);
        roomOwnerId = PlayerId;
        isRoomOwner = true;
        GameManagerNetWork.Instance.SetCurrentRoomState(response.roomId, true);

        Debug.Log("✅ Tạo phòng thành công qua warm buffer");
        LobbyObject.SetActive(false);
        RoomSinger.SetActive(true);
        LoadInforRoom(response.roomId);
        UpdateRoomRoleUI();
        RequestFriendInviteList();

        if (loadingScreen != null)
        {
            loadingScreen.SetActive(false);
        }
    }

    private int GetStartingBet()
    {
        int normalizedBet = defaultBetAmount > 0 ? defaultBetAmount : 1;
        InputbetCount = normalizedBet;
        return normalizedBet;
    }

    private bool HasEnoughRingBall(int requiredBet)
    {
        int ringBall = UserInfoHandler.Instance?.PlayerInventory?.RingBall ?? 0;
        if (ringBall >= requiredBet)
        {
            return true;
        }

        string message = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText("noti_ringball_not_enough")
            : "Not enough RingBall.";
        NotificationHelper.Instance?.ShowNotification(message, false);
        return false;
    }

    private NetworkObjectManager ResolveNetworkObjectManager(NetworkRunner runner)
    {
        if (NetworkObjectManager.Instance != null &&
            NetworkObjectManager.Instance.Object != null &&
            NetworkObjectManager.Instance.Object.IsValid &&
            (runner == null || NetworkObjectManager.Instance.Runner == runner))
        {
            return NetworkObjectManager.Instance;
        }

        var candidates = FindObjectsOfType<NetworkObjectManager>();
        foreach (var candidate in candidates)
        {
            if (candidate == null || candidate.Object == null || !candidate.Object.IsValid)
            {
                continue;
            }

            if (runner != null && candidate.Runner != runner)
            {
                continue;
            }

            NetworkObjectManager.Instance = candidate;
            return candidate;
        }

        return null;
    }

   
    #endregion



    #region [================================== SẴN SÀNG ==================================]

    private void HandleRoomReadyUpdate(WebSocketHelper.RoomReadyUpdateMessage message)
    {
        if (message == null)
            return;

        HandleRoomPlayerReady(message.roomId, message.playerId, message.ready);
    }

    private void HandleRoomStart(WebSocketHelper.RoomStartMessage message)
    {
        if (message == null)
            return;

        if (roomId <= 0 || message.roomId != roomId)
            return;

        if (HasValidPlayerId() && WebSocketHelper.Instance != null && WebSocketHelper.Instance.IsConnected)
        {
            WebSocketHelper.Instance.Send(new WebSocketHelper.RoomStartAckMessage
            {
                type = "room_start_ack",
                roomId = roomId,
                playerId = PlayerId
            });
        }

        if (roomStartCoroutine != null || roomCountdownCoroutine != null)
            return;

        if (message.mapId > 0)
        {
            selectedMapId = (GameMapId)message.mapId;
            UpdateRoomMapNameText();
        }

        BeginRoomCountdown(message.roomName);
    }

    private void HandleRoomStartCanceled(WebSocketHelper.RoomStartCancelMessage message)
    {
        if (message == null || message.roomId != roomId)
            return;

        CancelRoomCountdown();
    }

    private void HandleRoomKicked(WebSocketHelper.RoomKickedMessage message)
    {
        if (message == null || message.roomId != roomId || message.playerId != PlayerId)
            return;

        ForceLeaveRoom("Bạn đã bị đuổi khỏi phòng.");
    }

    private void HandleRoomUsersUpdate(WebSocketHelper.RoomUsersMessage message)
    {
        if (message == null || message.roomId != roomId)
            return;

        HandleRoomUsers(message.users);
    }

    private void HandleRoomChatMessage(WebSocketHelper.RoomChatMessage message)
    {
        if (message == null || message.roomId != roomId)
            return;

        string sender = string.IsNullOrWhiteSpace(message.senderName) ? $"Player {message.senderId}" : message.senderName;
        ShowChat(sender, message.message);
    }

    private void HandleFriendListMessage(WebSocketHelper.FriendListMessage message)
    {
        if (message == null || message.playerId != PlayerId)
            return;

        BuildFriendInviteListFromSocket(message.friends);
    }

    public async void ReadyButton()
    {
        if (!HasValidPlayerId())
        {
            NotificationHelper.Instance?.ShowNotification("Không tìm thấy thông tin người chơi.", false);
            return;
        }

        if (roomId <= 0)
        {
            NotificationHelper.Instance?.ShowNotification("Bạn chưa ở trong phòng.", false);
            return;
        }

        try
        {
            SetRoomPlayerReadyState(PlayerId, true);
            if (WebSocketHelper.Instance != null)
            {
                WebSocketHelper.Instance.Send(new WebSocketHelper.RoomReadyMessage
                {
                    type = "room_ready",
                    roomId = roomId,
                    playerId = PlayerId,
                    ready = true
                });
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Lỗi khi tham gia phòng: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public void CancelReadyButton()
    {
        if (!HasValidPlayerId())
        {
            NotificationHelper.Instance?.ShowNotification("Không tìm thấy thông tin người chơi.", false);
            return;
        }

        if (roomId <= 0)
        {
            NotificationHelper.Instance?.ShowNotification("Bạn chưa ở trong phòng.", false);
            return;
        }

        if (isRoomCountdownActive)
        {
            NotificationHelper.Instance?.ShowNotification("Không thể hủy sẵn sàng khi đang đếm giờ.", false);
            return;
        }

        try
        {
            SetRoomPlayerReadyState(PlayerId, false);
            if (WebSocketHelper.Instance != null)
            {
                WebSocketHelper.Instance.Send(new WebSocketHelper.RoomReadyMessage
                {
                    type = "room_ready",
                    roomId = roomId,
                    playerId = PlayerId,
                    ready = false
                });
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Lỗi khi hủy sẵn sàng: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void OnCancelStartGameClicked()
    {
        if (!isRoomOwner || roomId <= 0 || !isRoomCountdownActive)
            return;

        if (WebSocketHelper.Instance != null)
        {
            WebSocketHelper.Instance.Send(new WebSocketHelper.RoomStartCancelMessage
            {
                type = "room_start_cancel",
                roomId = roomId,
                playerId = PlayerId
            });
        }

        CancelRoomCountdown();
    }

    private IEnumerator RequestStartRoomMatch()
    {
        try
        {
            if (!HasValidPlayerId())
            {
                NotificationHelper.Instance?.ShowNotification("Không tìm thấy thông tin người chơi.", false);
                yield break;
            }

            if (APIManager.Instance == null)
            {
                NotificationHelper.Instance?.ShowNotification("Không thể bắt đầu trận.", false);
                yield break;
            }

            var roomPlayerIds = GetRoomPlayerIds();
            if (roomPlayerIds.Count <= 1)
            {
                NotificationHelper.Instance?.ShowNotification("Cần ít nhất 2 người chơi trong phòng để bắt đầu.", false);
                yield break;
            }

            if (!AreAllRoomPlayersReady())
            {
                NotificationHelper.Instance?.ShowNotification("Cần tất cả người chơi sẵn sàng trước khi bắt đầu.", false);
                yield break;
            }

            StartRoomLoading("Đang chuẩn bị vào trận...");

            var roomName = string.IsNullOrWhiteSpace(currentRoomName) ? proomName : currentRoomName;
            if (string.IsNullOrWhiteSpace(roomName))
            {
                NotificationHelper.Instance?.ShowNotification("Không tìm thấy thông tin phòng.", false);
                FinishRoomLoading();
                yield break;
            }

            APIManager.JoinRoomsBatchResult joinResult = null;
            yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
                APIManager.Instance.JoinRoomsBatchAsync(
                    roomPlayerIds,
                    roomName,
                    selectedMapId,
                    0,
                    null,
                    (int)TypeMatchGid.MatchRoom,
                    InputbetCount,
                    selectedMaxRound),
                result => joinResult = result));

            if (joinResult == null || !joinResult.Success)
            {
                NotificationHelper.Instance?.ShowNotification("Không thể bắt đầu trận.", false);
                FinishRoomLoading();
                yield break;
            }

            currentRoomName = joinResult.RoomName;

            if (WebSocketHelper.Instance == null || !WebSocketHelper.Instance.IsConnected)
            {
                BeginRoomCountdown(currentRoomName);
            }
        }
        finally
        {
            roomStartRequestCoroutine = null;
        }
    }

    private void BeginRoomCountdown(string roomName)
    {
        if (roomCountdownCoroutine != null || roomStartCoroutine != null)
            return;

        SetRoomCountdownActive(true);
        roomCountdownCoroutine = StartCoroutine(RoomStartCountdownRoutine(roomName));
    }

    private IEnumerator RoomStartCountdownRoutine(string roomName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                NotificationHelper.Instance?.ShowNotification("Không tìm thấy thông tin phòng.", false);
                UpdateRoomCountdownText(0);
                yield break;
            }

            for (int seconds = RoomStartCountdownSeconds; seconds > 0; seconds--)
            {
                UpdateRoomCountdownText(seconds);
                yield return new WaitForSeconds(1f);
            }

            UpdateRoomCountdownText(0);
            SetRoomCountdownActive(false);
            roomStartCoroutine = StartCoroutine(StartRoomMatchRoutine(roomName));
        }
        finally
        {
            roomCountdownCoroutine = null;
        }
    }

    private void UpdateRoomCountdownText(int seconds)
    {
        if (roomCountdownText == null)
            return;

        if (seconds <= 0)
        {
            roomCountdownText.text = string.Empty;
            roomCountdownText.gameObject.SetActive(false);
            return;
        }

        roomCountdownText.text = $"Bắt đầu sau {seconds}s";
        roomCountdownText.gameObject.SetActive(true);
    }

    private void SetRoomCountdownActive(bool isActive)
    {
        isRoomCountdownActive = isActive;

        if (cancelStartGameButton != null)
            cancelStartGameButton.gameObject.SetActive(false);

        if (startGameButton != null)
            startGameButton.gameObject.SetActive(isRoomOwner && !isActive);
    }

    private void CancelRoomCountdown()
    {
        if (roomCountdownCoroutine != null)
        {
            StopCoroutine(roomCountdownCoroutine);
            roomCountdownCoroutine = null;
        }

        UpdateRoomCountdownText(0);
        SetRoomCountdownActive(false);
        UpdateStartGameAvailability();
    }

    private IEnumerator StartRoomMatchRoutine(string sessionName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionName))
            {
                NotificationHelper.Instance?.ShowNotification("Không tìm thấy session để vào trận.", false);
                FinishRoomLoading();
                yield break;
            }

            if (GameManagerNetWork.Instance == null)
            {
                NotificationHelper.Instance?.ShowNotification("Không thể kết nối vào trận.", false);
                FinishRoomLoading();
                yield break;
            }

            StartRoomLoading("Đang chuẩn bị vào trận...");
            UpdateRoomLoadingProgress(0.3f, "Đang kết nối Photon...");

            bool joinSuccess = false;
            var joinTask = GameManagerNetWork.Instance.JoinRoomByNameAsync(roomId, sessionName, BuildSessionPropertiesForMatchRoom());
            yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
                joinTask,
                result => joinSuccess = result,
                20f));

            if (!joinSuccess)
            {
                NotificationHelper.Instance?.ShowNotification("Không thể vào trận.", false);
                FinishRoomLoading();
                yield break;
            }

            UpdateRoomLoadingProgress(0.85f, "Đang tải bản đồ...");
        }
        finally
        {
            roomStartCoroutine = null;
        }
    }

    private List<int> GetRoomPlayerIds()
    {
        var players = new List<int>();
        foreach (var playerId in roomPlayerReadyStates.Keys)
        {
            if (playerId > 0 && !players.Contains(playerId))
                players.Add(playerId);
        }

        if (roomOwnerId > 0 && !players.Contains(roomOwnerId))
            players.Add(roomOwnerId);
        if (HasValidPlayerId() && !players.Contains(PlayerId))
            players.Add(PlayerId);

        return players;
    }

    private void StartRoomLoading(string message)
    {
        if (LoadingManager.Instance == null)
            return;

        LoadingManager.Instance.StartLoadingLocalPersistent();
        LoadingManager.Instance.UpdateProgress(0.05f, message);
    }

    private void UpdateRoomLoadingProgress(float progress, string text)
    {
        if (LoadingManager.Instance == null)
            return;

        LoadingManager.Instance.UpdateProgress(progress, text);
    }

    private void FinishRoomLoading()
    {
        if (LoadingManager.Instance == null)
            return;

        LoadingManager.Instance.FinishLoading();
    }




    #endregion




    //public bool AllPlayersReadyInRoom(string roomId, NetworkRunner runner)
    //{
    //    if (!roomReadyMap.ContainsKey(roomId))
    //        return false;

    //    foreach (var p in runner.ActivePlayers)
    //    {
    //        if (!roomReadyMap[roomId].ContainsKey(p) || !roomReadyMap[roomId][p])
    //            return false;
    //    }

    //    return true;
    //}

    #region [================================== THAM GIA PHÒNG ==================================]
    public async void JoinRoom(int roomId)
    {
        try
        {
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
            bool apiSuccess = await APIManager.Instance.JoinRoomAPI(roomId, GameManagerNetWork.Instance.loginUserModel.UserId);

            if (apiSuccess)
            {
                this.roomId = roomId;
                if (roomLookup.TryGetValue(roomId, out var roomData) && roomData != null)
                {
                    currentRoomName = roomData.roomName;
                    InputbetCount = roomData.bet;
                    selectedMapId = (GameMapId)roomData.mapId;
                    selectedMaxRound = Mathf.Clamp(roomData.GetMaxRound(selectedMaxRound), MinRoomRoundCount, MaxRoomRoundCount);
                    roomOwnerId = roomData.createId;
                    isRoomOwner = roomOwnerId == PlayerId && roomOwnerId > 0;
                }

                GameManagerNetWork.Instance.SetCurrentRoomState(roomId, false);
                if (mainMenuObject != null)
                {
                    mainMenuObject.SetActive(false);
                }
                LobbyObject.SetActive(false);
                RoomSinger.SetActive(true);
                LoadInforRoom(roomId);
                UpdateRoomRoleUI();
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            }
            else
            {
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
                NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("Tham gia phòng thất bại"), false);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"❌ Exception: {ex.Message}");
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("Tham gia phòng thất bại"), false);
        }
    }

    private void RequestRoomUsers(int targetRoomId)
    {
        if (targetRoomId <= 0 || !HasValidPlayerId())
            return;

        if (WebSocketHelper.Instance != null && WebSocketHelper.Instance.IsConnected)
        {
            WebSocketHelper.Instance.Send(new WebSocketHelper.RoomUsersRequestMessage
            {
                type = "room_users",
                roomId = targetRoomId,
                playerId = PlayerId
            });
            return;
        }

        StartCoroutine(LoadUsersCoroutineFallback(targetRoomId));
    }

    private IEnumerator LoadUsersCoroutineFallback(int targetRoomId)
    {
        string url = $"{ApiConfig.BaseUrl}/getUserRooms?roomId={targetRoomId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("❌ Load user failed: " + req.error);
            yield break;
        }

        string json = "{\"users\":" + req.downloadHandler.text + "}";
        UserRoomListWrapper wrapper = JsonUtility.FromJson<UserRoomListWrapper>(json);
        if (wrapper == null || wrapper.users == null)
        {
            Debug.LogError("❌ Users data is null!");
            yield break;
        }

        HandleRoomUsers(wrapper.users);
    }

    private void HandleRoomUsers(List<UserRoom> users)
    {
        RefreshRoomPlayerList(users);
    }
    #endregion


    #region [================================== THOÁT KHỎI PHÒNG ==================================]
    // public void BackToLobby()
    // {
    //     LeaveRoomRoutine(false);
    // }

    public void BackToMainMenu()
    {
        LeaveRoomRoutine(true);
    }

    public void LeaveRoomRoutine(bool backToMainMenu = false)
    {
        if (_isLeavingRoom)
        {
            Debug.Log("⚠️ Đang xử lý rời phòng, bỏ qua yêu cầu rời phòng tiếp theo.");
            return;
        }
        try
        {
            if (backToMainMenu)
            {
                _awaitingMainMenuLeaveConfirmation = true;
                _mainMenuWasActiveBeforeLeavePrompt = mainMenuObject != null && mainMenuObject.activeSelf;

                if (mainMenuObject != null)
                {
                    mainMenuObject.SetActive(false);
                }

                if (keepMainMenuHiddenCoroutine != null)
                {
                    StopCoroutine(keepMainMenuHiddenCoroutine);
                }

                keepMainMenuHiddenCoroutine = StartCoroutine(KeepMainMenuHiddenUntilLeaveDecision());
            }

            string confirmText = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText("confirm_leave_room")
                : "Bạn có chắc muốn rời phòng?";

            PopupHelper.Instance.ShowPopup(confirmText, () =>
            {
                _awaitingMainMenuLeaveConfirmation = false;
                if (keepMainMenuHiddenCoroutine != null)
                {
                    StopCoroutine(keepMainMenuHiddenCoroutine);
                    keepMainMenuHiddenCoroutine = null;
                }
                StartCoroutine(LeaveRoomRoutineCoroutine(backToMainMenu));
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"❌ Lỗi khi thoát khỏi phòng: {ex.Message}\n{ex.StackTrace}");
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
        }
    }

    private IEnumerator KeepMainMenuHiddenUntilLeaveDecision()
    {
        while (_awaitingMainMenuLeaveConfirmation && !_isLeavingRoom)
        {
            if (mainMenuObject != null && mainMenuObject.activeSelf)
            {
                mainMenuObject.SetActive(false);
            }

            yield return null;

            // Nếu popup xác nhận đã đóng mà chưa thực hiện rời phòng
            // thì coi như người chơi đã huỷ thao tác.
            if (GameObject.FindGameObjectWithTag("PopupUI") == null)
            {
                _awaitingMainMenuLeaveConfirmation = false;
            }
        }

        if (!_isLeavingRoom && mainMenuObject != null)
        {
            // Khi chưa thực sự rời phòng (ví dụ bấm Huỷ ở popup xác nhận),
            // Main Menu phải luôn tắt để tránh hiển thị đè lên UI phòng.
            mainMenuObject.SetActive(false);
            UpdateRoomHeader();
        }

        keepMainMenuHiddenCoroutine = null;
    }

    private IEnumerator LeaveRoomRoutineCoroutine(bool backToMainMenu)
    {
        if (_isLeavingRoom)
        {
            yield break;
        }

        _isLeavingRoom = true;

        try
        {
            var currentRoomId = roomId;
            var currentPlayerId = PlayerId;
            var sentViaSocket = false;

            if (currentRoomId > 0 && currentPlayerId > 0)
            {
                if (WebSocketHelper.Instance != null && WebSocketHelper.Instance.IsConnected)
                {
                    WebSocketHelper.Instance.SendLeaveRoom(currentRoomId, currentPlayerId);
                    sentViaSocket = true;
                }
                else if (APIManager.Instance != null)
                {
                    bool apiSuccess = false;
                    yield return StartCoroutine(LoadingManager.Instance.RunTaskWithTimeout(
                        APIManager.Instance.LeaveRoomAsync(currentRoomId, currentPlayerId),
                        result => apiSuccess = result));

                    if (!apiSuccess)
                    {
                        Debug.LogWarning("❌ Không thể gọi API rời phòng.");
                    }
                }
            }

            if (sentViaSocket)
            {
                Debug.Log("➡️ Đã gửi yêu cầu rời phòng qua WebSocket.");
            }

            // Đóng kết nối; server sẽ bắt sự kiện OnPlayerLeft hoặc WebSocket mất kết nối để cập nhật phòng.
            GameManagerNetWork.Instance.CloseConnectToRunner();
            if (backToMainMenu)
            {
                if (mainMenuObject != null)
                {
                    mainMenuObject.SetActive(true);
                }

                if (LobbyObject != null)
                {
                    LobbyObject.SetActive(false);
                }
            }
            else if (LobbyObject != null)
            {
                LobbyObject.SetActive(true);
            }
            RoomSinger.SetActive(false);
            ClearRoomSingerVisuals();
            ClearRoomPlayerList();
            latestRoom = null;
            roomId = 0;
            roomOwnerId = 0;
            isRoomOwner = false;
            currentRoomName = null;
            if (roomStartCoroutine != null)
            {
                StopCoroutine(roomStartCoroutine);
                roomStartCoroutine = null;
            }
            if (roomCountdownCoroutine != null)
            {
                StopCoroutine(roomCountdownCoroutine);
                roomCountdownCoroutine = null;
            }
            SetRoomCountdownActive(false);
            if (roomStartRequestCoroutine != null)
            {
                StopCoroutine(roomStartRequestCoroutine);
                roomStartRequestCoroutine = null;
            }
            ResetRoomInfoUI();
            UpdateRoomHeader();
            Debug.Log(backToMainMenu
                ? "➡️ Đã quay về Main Menu sau khi yêu cầu thoát phòng."
                : "➡️ Đã quay về Lobby sau khi yêu cầu thoát phòng (API được server xử lý một lần).");
        }
        finally
        {
            _isLeavingRoom = false;
            _awaitingMainMenuLeaveConfirmation = false;
            if (keepMainMenuHiddenCoroutine != null)
            {
                StopCoroutine(keepMainMenuHiddenCoroutine);
                keepMainMenuHiddenCoroutine = null;
            }
        }
    }




    #endregion

    private void RefreshRoomSingerVisuals(List<UserRoom> users)
    {
        if (users == null || users.Count == 0)
        {
            ClearRoomSingerVisuals();
            return;
        }

        if (RoomSinger == null)
        {
            Debug.LogWarning("[RoomManager] RoomSinger chưa được gán để spawn model.");
            return;
        }

        var prefab = GameInitializer.Instance != null ? GameInitializer.Instance.PlayerModelVisual : null;
        if (prefab == null)
        {
            Debug.LogWarning("[RoomManager] PlayerModelVisual chưa được gán trong GameInitializer.");
            return;
        }

        Transform root = roomSingerVisualRoot != null ? roomSingerVisualRoot : RoomSinger.transform;
        int maxSlots = roomSingerSpawnPoints != null ? roomSingerSpawnPoints.Length : 0;
        if (maxSlots == 0)
        {
            Debug.LogWarning("[RoomManager] Chưa cấu hình roomSingerSpawnPoints.");
            return;
        }

        var activeIds = new HashSet<int>();
        int spawnCount = Mathf.Min(users.Count, maxSlots);
        for (int i = 0; i < spawnCount; i++)
        {
            var user = users[i];
            if (user == null || user.player == null)
                continue;

            int playerId = user.player.id;
            activeIds.Add(playerId);

            if (!roomSingerVisuals.TryGetValue(playerId, out var visual) || visual == null)
            {
                visual = Instantiate(prefab, root);
                visual.name = $"RoomSinger_Player_{playerId}";
                roomSingerVisuals[playerId] = visual;
            }

            var spawnPoint = roomSingerSpawnPoints[i];
            if (spawnPoint != null)
            {
                visual.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            }

            ApplyPlayerVisualData(visual, user.player);
        }

        var toRemove = roomSingerVisuals.Keys.Where(id => !activeIds.Contains(id)).ToList();
        foreach (var playerId in toRemove)
        {
            if (roomSingerVisuals.TryGetValue(playerId, out var visual) && visual != null)
                Destroy(visual);
            roomSingerVisuals.Remove(playerId);
        }
    }

    private void ApplyPlayerVisualData(GameObject visual, PlayerSchema player)
    {
        if (visual == null || player == null)
            return;

        var visualComponent = visual.GetComponentInChildren<PlayerModelVisualComponent>();
        if (visualComponent != null)
            visualComponent.PlayerId = player.id;

        var characterRenderer = ResolveCharacterRenderer(visualComponent, visual);
        var hairRenderer = ResolveHairRenderer(visual);

        int materialId = player.Shirt > 0 ? player.Shirt : player.Body > 0 ? player.Body : 0;
        StartCoroutine(ItemVisualHelper.ApplyMaterial(
            characterRenderer,
            null,
            hairRenderer,
            materialId,
            (int)TypeItemGid.Clother,
            player.Hair));
    }

    private Renderer ResolveCharacterRenderer(PlayerModelVisualComponent component, GameObject visual)
    {
        if (component != null && component.CharacterRenderers != null)
        {
            foreach (var renderer in component.CharacterRenderers)
            {
                if (renderer != null)
                    return renderer;
            }
        }

        return visual != null ? visual.GetComponentInChildren<Renderer>() : null;
    }

    private SkinnedMeshRenderer ResolveHairRenderer(GameObject visual)
    {
        if (visual == null)
            return null;

        var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.name.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0)
                return renderer;
        }

        return renderers.Length > 0 ? renderers[0] : null;
    }

    private void ClearRoomSingerVisuals()
    {
        foreach (var visual in roomSingerVisuals.Values)
        {
            if (visual != null)
                Destroy(visual);
        }

        roomSingerVisuals.Clear();
    }

    public void HandleRoomPlayerReady(int roomId, int playerId, bool ready)
    {
        if (this.roomId != roomId)
            return;

        SetRoomPlayerReadyState(playerId, ready);
    }

    private void RefreshRoomPlayerList(List<UserRoom> users)
    {
        if (roomPlayerListRoot == null || roomPlayerListItemPrefab == null)
        {
            Debug.LogWarning("[RoomManager] Chưa gán roomPlayerListRoot hoặc roomPlayerListItemPrefab.");
            return;
        }

        EnsureRoomOwner(users);

        foreach (Transform child in roomPlayerListRoot)
            Destroy(child.gameObject);

        roomPlayerItems.Clear();

        var activePlayerIds = new HashSet<int>();
        if (users != null)
        {
            foreach (var user in users)
            {
                if (user == null || user.player == null)
                    continue;

                int playerId = user.player.id;
                activePlayerIds.Add(playerId);

                if (!roomPlayerReadyStates.ContainsKey(playerId))
                    roomPlayerReadyStates[playerId] = false;

                GameObject item = Instantiate(roomPlayerListItemPrefab, roomPlayerListRoot);
                var itemUi = item.GetComponent<RoomPlayerListItemUI>();
                if (itemUi == null)
                {
                    Debug.LogWarning("[RoomManager] RoomPlayerListItemUI chưa được gắn trên prefab danh sách người chơi.");
                    continue;
                }

                bool canKick = isRoomOwner && playerId != roomOwnerId;
                string playerName = user.player != null ? user.player.PlayerName : $"Player {playerId}";
                itemUi.Bind(user.player, roomPlayerReadyStates[playerId], playerId == roomOwnerId, canKick,
                    () => PromptKickPlayer(playerId, playerName));
                SetupRoomPlayerAvatar(itemUi, user.player);
                roomPlayerItems[playerId] = itemUi;
            }
        }

        var removeKeys = roomPlayerReadyStates.Keys.Where(id => !activePlayerIds.Contains(id)).ToList();
        foreach (var id in removeKeys)
            roomPlayerReadyStates.Remove(id);

        int emptySlots = Mathf.Max(0, MaxRoomPlayerSlots - activePlayerIds.Count);
        if (emptySlots > 0)
        {
            if (roomPlayerEmptySlotPrefab == null)
            {
                Debug.LogWarning("[RoomManager] Chưa gán roomPlayerEmptySlotPrefab.");
            }
            else
            {
                for (int i = 0; i < emptySlots; i++)
                    Instantiate(roomPlayerEmptySlotPrefab, roomPlayerListRoot);
            }
        }

        UpdateStartGameAvailability();
        UpdateRoomRoleUI();
        UpdateReadyButtons();
    }

    private void EnsureRoomOwner(List<UserRoom> users)
    {
        if (users == null || users.Count == 0)
        {
            isRoomOwner = false;
            return;
        }

        if (roomOwnerId <= 0 && roomLookup.TryGetValue(roomId, out var roomData) && roomData != null)
        {
            roomOwnerId = roomData.createId;
        }

        bool ownerStillHere = users.Any(user => user?.player != null && user.player.id == roomOwnerId);
        if (ownerStillHere)
        {
            isRoomOwner = roomOwnerId == PlayerId && roomOwnerId > 0;
            return;
        }
        
        // Chỉ chủ phòng đã tạo phòng mới có quyền host/kick.
        // Nếu chủ phòng không còn trong danh sách users thì không gán quyền host cho người khác.
        isRoomOwner = false;
    }

    private void SetRoomPlayerReadyState(int playerId, bool ready)
    {
        roomPlayerReadyStates[playerId] = ready;

        if (roomPlayerItems.TryGetValue(playerId, out var itemUi) && itemUi != null)
        {
            itemUi.SetReady(ready);
        }

        if (!ready)
        {
            CancelRoomCountdown();
        }

        UpdateStartGameAvailability();
        if (playerId == PlayerId)
        {
            UpdateReadyButtons();
        }
    }

    private bool AreAllRoomPlayersReady()
    {
        if (roomPlayerItems.Count <= 1)
            return false;

        foreach (var playerId in roomPlayerItems.Keys)
        {
            if (roomOwnerId > 0 && playerId == roomOwnerId)
                continue;

            if (!roomPlayerReadyStates.TryGetValue(playerId, out var ready) || !ready)
                return false;
        }

        return true;
    }

    private void UpdateStartGameAvailability()
    {
        if (startGameButton != null)
        {
            bool canStart = isRoomOwner && AreAllRoomPlayersReady();
            startGameButton.interactable = canStart;

            if (startGameButtonGroup != null)
                startGameButtonGroup.alpha = canStart ? 1f : 0.5f;
        }
    }

    private void UpdateRoomRoleUI()
    {
        if (roomId <= 0)
        {
            if (startGameButton != null)
                startGameButton.gameObject.SetActive(false);
            if (cancelStartGameButton != null)
                cancelStartGameButton.gameObject.SetActive(false);
            if (readyButton != null)
                readyButton.gameObject.SetActive(false);
            if (cancelReadyButton != null)
                cancelReadyButton.gameObject.SetActive(false);
            return;
        }

        isRoomOwner = roomOwnerId == PlayerId && roomOwnerId > 0;

        if (startGameButton != null)
            startGameButton.gameObject.SetActive(isRoomOwner && !isRoomCountdownActive);
        if (cancelStartGameButton != null)
            cancelStartGameButton.gameObject.SetActive(false);

        UpdateReadyButtons();
    }

    private void ClearRoomPlayerList()
    {
        if (roomPlayerListRoot != null)
        {
            foreach (Transform child in roomPlayerListRoot)
                Destroy(child.gameObject);
        }

        roomPlayerItems.Clear();
        roomPlayerReadyStates.Clear();
        ClearRoomAvatarCache();
        UpdateStartGameAvailability();
        UpdateRoomRoleUI();
    }

    private void SetupRoomPlayerAvatar(RoomPlayerListItemUI itemUi, PlayerSchema player)
    {
        if (itemUi == null || player == null || player.id <= 0)
            return;

        var providerType = ResolveProviderType(player.ProviderType);
        var rawImage = itemUi.AvatarRawImage;
        var image = itemUi.AvatarImage;
        if (rawImage == null && image == null)
            return;

        if (roomAvatarLoaders.TryGetValue(player.id, out var running) && running != null)
        {
            StopCoroutine(running);
            roomAvatarLoaders.Remove(player.id);
        }

        if (roomAvatarTextures.TryGetValue(player.id, out var cachedTexture) && cachedTexture != null)
        {
            ApplyRoomAvatarTexture(cachedTexture, rawImage, image, player.id);
            return;
        }

#if UNITY_EDITOR
        // Trong Unity Editor không cần tải avatar nhân vật từ service/network để tránh log lỗi không cần thiết.
        return;
#endif

        var avatarService = AvatarService.EnsureInstance();
        if (avatarService == null)
        {
            Debug.LogError($"[RoomManager] Không thể tải avatar vì thiếu AvatarService cho player {player.id}.");
            return;
        }

        var avatarUrl = player.AvatarUrl;
        var firebaseUid = player.IdAccount;

        if (string.IsNullOrWhiteSpace(firebaseUid))
        {
            var loginModel = GameManagerNetWork.Instance?.loginUserModel;
            if (loginModel != null && loginModel.UserId == player.id)
                firebaseUid = loginModel.Token;
        }

        if (string.IsNullOrWhiteSpace(avatarUrl) && string.IsNullOrWhiteSpace(firebaseUid))
        {
            Debug.LogWarning($"[RoomManager] Không tìm thấy avatar URL hoặc Firebase UID cho player {player.id} (ProviderType={player.ProviderType}).");
            return;
        }

        var routine = StartCoroutine(LoadAndApplyRoomAvatarRoutine(player.id, providerType, avatarUrl, firebaseUid, rawImage, image));
        roomAvatarLoaders[player.id] = routine;
    }

    private AuthenticationProviderType ResolveProviderType(string providerType)
    {
        if (!string.IsNullOrEmpty(providerType) && Enum.TryParse(providerType, true, out AuthenticationProviderType parsed))
        {
            return parsed;
        }

        return AuthenticationProviderType.Anonymous;
    }

    private IEnumerator LoadAndApplyRoomAvatarRoutine(int playerId, AuthenticationProviderType providerType, string avatarUrl, string firebaseUid, RawImage rawImage, Image image)
    {
        bool allowStorageFallback = providerType != AuthenticationProviderType.GooglePlayGames &&
                                    providerType != AuthenticationProviderType.Google;

        if (!string.IsNullOrWhiteSpace(avatarUrl) && string.IsNullOrWhiteSpace(firebaseUid))
        {
            firebaseUid = string.Empty;
        }

        if (allowStorageFallback && string.IsNullOrWhiteSpace(firebaseUid) && APIManager.Instance != null)
        {
            var profileTask = APIManager.Instance.GetPlayerInventoryAsync(playerId);
            while (profileTask != null && !profileTask.IsCompleted)
            {
                yield return null;
            }

            if (profileTask != null && profileTask.Status == TaskStatus.RanToCompletion && profileTask.Result != null)
            {
                firebaseUid = profileTask.Result.IdAccount;
            }
            else if (profileTask != null && profileTask.IsFaulted)
            {
                Debug.LogWarning($"[RoomManager] Không thể lấy GUID avatar cho player {playerId}: {profileTask.Exception?.GetBaseException().Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(avatarUrl) && string.IsNullOrWhiteSpace(firebaseUid))
        {
            Debug.LogWarning($"[RoomManager] Không tìm thấy nguồn avatar cho player {playerId}.");
            roomAvatarLoaders.Remove(playerId);
            yield break;
        }

        Texture2D texture = null;
        string errorMessage = null;
        bool isDone = false;

        var avatarService = AvatarService.EnsureInstance();
        avatarService.LoadAvatar(new AvatarService.AvatarRequest(providerType, avatarUrl, firebaseUid, "avatars", allowStorageFallback),
            downloaded =>
            {
                texture = downloaded;
                isDone = true;
            },
            error =>
            {
                errorMessage = error;
                isDone = true;
            });

        while (!isDone)
        {
            yield return null;
        }

        roomAvatarLoaders.Remove(playerId);

        if (texture == null)
        {
            Debug.LogWarning($"[RoomManager] Không thể tải avatar cho player {playerId}: {errorMessage}");
            yield break;
        }

        roomAvatarTextures[playerId] = texture;
        ApplyRoomAvatarTexture(texture, rawImage, image, playerId);
    }

    private void ApplyRoomAvatarTexture(Texture2D texture, RawImage rawImage, Image image, int playerId)
    {
        if (texture == null)
            return;

        if (rawImage != null)
        {
            rawImage.texture = texture;
            rawImage.color = Color.white;
        }
        else if (image != null)
        {
            if (roomAvatarSprites.TryGetValue(playerId, out var existingSprite))
            {
                if (existingSprite == null || existingSprite.texture != texture)
                {
                    if (existingSprite != null)
                        Destroy(existingSprite);

                    existingSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    roomAvatarSprites[playerId] = existingSprite;
                }
            }
            else
            {
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                roomAvatarSprites[playerId] = sprite;
            }

            image.sprite = roomAvatarSprites[playerId];
            image.color = Color.white;
        }
    }

    private void ClearRoomAvatarCache()
    {
        foreach (var loader in roomAvatarLoaders.Values)
        {
            if (loader != null)
            {
                StopCoroutine(loader);
            }
        }

        roomAvatarLoaders.Clear();

        foreach (var sprite in roomAvatarSprites.Values)
        {
            if (sprite != null)
            {
                Destroy(sprite);
            }
        }

        roomAvatarSprites.Clear();

        // Textures belong to AvatarService.avatarCache — do NOT Destroy them here.
        roomAvatarTextures.Clear();
    }


    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        RebuildRoomsFromSessions(sessionList);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    private void OnDestroy()
    {
        ClearRoomAvatarCache();
    }

    private void UpdateReadyButtons()
    {
        if (isRoomOwner)
        {
            if (readyButton != null)
                readyButton.gameObject.SetActive(false);
            if (cancelReadyButton != null)
                cancelReadyButton.gameObject.SetActive(false);
            return;
        }

        bool isReady = roomPlayerReadyStates.TryGetValue(PlayerId, out var ready) && ready;

        if (readyButton != null)
            readyButton.gameObject.SetActive(!isReady);
        if (cancelReadyButton != null)
        {
            cancelReadyButton.gameObject.SetActive(isReady);
            cancelReadyButton.interactable = !isRoomCountdownActive;
        }
    }

    private void PromptKickPlayer(int targetPlayerId, string playerName)
    {
        if (!isRoomOwner || targetPlayerId == roomOwnerId)
            return;

        string confirmText = $"Bạn có chắc muốn đuổi {playerName} ra khỏi phòng?";
        PopupHelper.Instance?.ShowPopup(confirmText, () => KickPlayer(targetPlayerId));
    }

    private void KickPlayer(int targetPlayerId)
    {
        if (!isRoomOwner || targetPlayerId <= 0 || roomId <= 0)
            return;

        if (WebSocketHelper.Instance == null || !WebSocketHelper.Instance.IsConnected)
        {
            NotificationHelper.Instance?.ShowNotification("Không thể kết nối để đuổi người chơi.", false);
            return;
        }

        WebSocketHelper.Instance.Send(new WebSocketHelper.RoomKickMessage
        {
            type = "room_kick",
            roomId = roomId,
            playerId = targetPlayerId,
            requesterId = PlayerId
        });
    }

    private void ForceLeaveRoom(string message)
    {
        NotificationHelper.Instance?.ShowNotification(message, false);

        GameManagerNetWork.Instance?.CloseConnectToRunner();
        LobbyObject.SetActive(true);
        RoomSinger.SetActive(false);
        ClearRoomSingerVisuals();
        ClearRoomPlayerList();
        latestRoom = null;
        roomId = 0;
        roomOwnerId = 0;
        isRoomOwner = false;
        currentRoomName = null;

        if (roomStartCoroutine != null)
        {
            StopCoroutine(roomStartCoroutine);
            roomStartCoroutine = null;
        }
        if (roomCountdownCoroutine != null)
        {
            StopCoroutine(roomCountdownCoroutine);
            roomCountdownCoroutine = null;
        }
        SetRoomCountdownActive(false);
        if (roomStartRequestCoroutine != null)
        {
            StopCoroutine(roomStartRequestCoroutine);
            roomStartRequestCoroutine = null;
        }
        ResetRoomInfoUI();
        UpdateRoomHeader();
    }
}
