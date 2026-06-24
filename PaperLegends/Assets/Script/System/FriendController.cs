using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller quản lý các thao tác bạn bè và tin nhắn.
/// </summary>
public class FriendController : MonoBehaviour
{
    public static FriendController Instance;

    [SerializeField]
    private FriendMessageUIController messageUIController;

    [Header("UI Buttons")]
    [FormerlySerializedAs("AddFriendButton")]
    [SerializeField]
    private Button addFriendButton;

    [Header("Input UI")]
    [FormerlySerializedAs("TargetPlayerInput")]
    [SerializeField]
    private TMP_InputField targetPlayerInput;

    [Header("My Friend Code UI")]
    [FormerlySerializedAs("MyFriendCodeText")]
    [SerializeField]
    private TMP_Text myFriendCodeText;
    [FormerlySerializedAs("CopyFriendCodeButton")]
    [SerializeField]
    private Button copyFriendCodeButton;

    [Header("Friend List UI")]
    [FormerlySerializedAs("FriendListPanel")]
    [SerializeField]
    private Transform friendListPanel;
    [FormerlySerializedAs("FriendItemPrefab")]
    [SerializeField]
    private GameObject friendItemPrefab;

    [Header("Friend Request UI")]
    [FormerlySerializedAs("FriendRequestPanel")]
    [SerializeField]
    private Transform friendRequestPanel;
    [FormerlySerializedAs("FriendRequestBadge")]
    [SerializeField]
    private GameObject friendRequestBadge;
    [FormerlySerializedAs("FriendRequestBadgeText")]
    [SerializeField]
    private TMP_Text friendRequestBadgeText;

    [Header("Challenge Popup")]
    [FormerlySerializedAs("ChallengePopupPrefab")]
    [SerializeField]
    private GameObject challengePopupPrefab;

    [Header("Status Icons")]
    [FormerlySerializedAs("OnlineSprite")]
    [SerializeField]
    private Sprite onlineSprite;
    [FormerlySerializedAs("OfflineSprite")]
    [SerializeField]
    private Sprite offlineSprite;

    private readonly Dictionary<int, Image> statusIcons = new Dictionary<int, Image>();
    private readonly Dictionary<int, Sprite> friendAvatarSprites = new Dictionary<int, Sprite>();
    private Coroutine checkOnlineCoroutine;
    private const float OnlineCheckInterval = 30f;
    private bool isInitialized;

 
    

    /// <summary>
    /// ID của người chơi mục tiêu. Các nút UI sẽ sử dụng giá trị này
    /// để truyền vào các hàm xử lý tương ứng.
    /// </summary>
    [FormerlySerializedAs("TargetFriendCode")]
    [SerializeField]
    private string targetFriendCode;

    private int PlayerId
    {
        get
        {
            var networkManager = GameManagerNetWork.Instance;
            var loginModel = networkManager != null ? networkManager.loginUserModel : null;
            return loginModel != null ? loginModel.UserId : 0;
        }
    }

    private bool HasValidPlayerId() => PlayerId > 0;

    private void Awake()
    {
        Instance = this;
        if (messageUIController == null)
            messageUIController = GetComponent<FriendMessageUIController>();
    }

    private void OnEnable()
    {
        TryInitialize();
    }

    private void Start()
    {
        TryInitialize();
    }

    public void EnsureInitialized()
    {
        TryInitialize();
    }

    private void TryInitialize()
    {
        if (isInitialized || !HasValidPlayerId())
            return;

        InitializeFriendUi();
        isInitialized = true;
    }

    private void InitializeFriendUi()
    {
        if (targetPlayerInput != null)
        {
            targetPlayerInput.contentType = TMP_InputField.ContentType.Standard;
            targetPlayerInput.onValueChanged.RemoveAllListeners();
            targetPlayerInput.onValueChanged.AddListener(OnFriendCodeChanged);
        }

        if (addFriendButton != null)
        {
            addFriendButton.onClick.RemoveAllListeners();
            addFriendButton.onClick.AddListener(() => SendFriendRequest(PlayerId, targetFriendCode));
        }

        if (myFriendCodeText != null)
        {
            var loginModel = GameManagerNetWork.Instance?.loginUserModel;
            myFriendCodeText.text = loginModel != null ? loginModel.FriendCode : string.Empty;
        }

        if (copyFriendCodeButton != null)
        {
            copyFriendCodeButton.onClick.RemoveAllListeners();
            copyFriendCodeButton.onClick.AddListener(CopyFriendCode);
        }

        if (friendListPanel != null && friendItemPrefab != null)
        {
            LoadFriendList();
        }

        if (friendRequestPanel != null && friendItemPrefab != null)
        {
            LoadFriendRequests();
        }
    }

      void CopyFriendCode()
    {
        if (myFriendCodeText == null)
            return;

        GUIUtility.systemCopyBuffer = myFriendCodeText.text;

        if (NotificationHelper.Instance != null)
            NotificationHelper.Instance.ShowNotification("Friend code copied", true);
    }
    void OnFriendCodeChanged(string text)
    {
        targetFriendCode = text.Trim().ToUpperInvariant();
    }
    public void SendFriendRequest(int senderId, string receiverId)
    {
        if (!HasValidPlayerId() || string.IsNullOrWhiteSpace(receiverId))
            return;

        StartCoroutine(SendFriendRequestRoutine(senderId, receiverId));
    }

    private IEnumerator SendFriendRequestRoutine(int senderId, string FriendCode)
    {
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        bool success = false;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.SendFriendRequest(senderId, FriendCode),
            r => success = r));
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
        NotificationHelper.Instance.ShowNotification(
            LocalizationManager.Instance.GetText(success ? "noti_friend_true" : "noti_friend_false"),
            success);
    }

    public void RemoveFriend(int playerId, int friendId)
    {
        if (!HasValidPlayerId())
            return;

        StartCoroutine(RemoveFriendRoutine(playerId, friendId));
    }

    private IEnumerator RemoveFriendRoutine(int playerId, int friendId)
    {
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        bool success = false;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.RemoveFriend(playerId, friendId),
            r => success = r));
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
        NotificationHelper.Instance.ShowNotification(
            LocalizationManager.Instance.GetText(success ? "noti_friend_true" : "noti_friend_false"),
            success);
    }

    public void RespondFriendRequest(int senderId, int receiverId, int status)
    {
        if (!HasValidPlayerId())
            return;

        StartCoroutine(RespondFriendRequestRoutine(senderId, receiverId, status));
    }

    private IEnumerator RespondFriendRequestRoutine(int senderId, int receiverId, int status)
    {
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        bool success = false;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.RespondFriendRequest(senderId, receiverId, status),
            r => success = r));
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
        NotificationHelper.Instance.ShowNotification(
            LocalizationManager.Instance.GetText(success ? "noti_friend_true" : "noti_friend_false"),
            success);
    }

    public void LoadFriendList()
    {
        if (!HasValidPlayerId())
            return;

        StartCoroutine(LoadFriendListRoutine());
    }

    private IEnumerator LoadFriendListRoutine()
    {
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        PlayerInfoStruct[] friends = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetFriendList(PlayerId),
            r => friends = r));
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
        bool success = friends != null;
        if (success)
            BuildFriendList(friends);
 
    }

    public void LoadFriendRequests()
    {
        if (!HasValidPlayerId())
            return;

        StartCoroutine(LoadFriendRequestsRoutine());
    }

    private IEnumerator LoadFriendRequestsRoutine()
    {
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        PlayerInfoStruct[] requests = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetPendingFriendRequests(PlayerId),
            r => requests = r));
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
        bool success = requests != null;
        if (success)
            BuildFriendRequestList(requests);
        UpdateFriendRequestBadge(requests != null ? requests.Length : 0);

    }

    private void UpdateFriendRequestBadge(int count)
    {
        count = Mathf.Max(0, count);

        if (friendRequestBadge != null)
            friendRequestBadge.SetActive(count > 0);
        if (friendRequestBadgeText != null)
            friendRequestBadgeText.text = count.ToString();

        UserInfoHandler.Instance?.SetPendingFriendRequestCount(count);
    }
    public void LoadMessages()
    {
        messageUIController?.LoadMessages();
    }

    public void LoadSystemMessages()
    {
        messageUIController?.LoadSystemMessages();
    }

    public void HandleIncomingMessage(WebSocketHelper.MessagePayload msg)
    {
        messageUIController?.OnIncomingMessage(msg);
    }

    public void UpdateFriendStatus(int playerId, bool isOnline)
    {
        if (statusIcons.TryGetValue(playerId, out var icon))
        {
            UpdateIcon(icon, isOnline);
        }

        messageUIController?.UpdateFriendStatusIcon(playerId, isOnline);
    }

    private void BuildFriendList(PlayerInfoStruct[] friends)
    {
        if (friendListPanel == null || friendItemPrefab == null)
        {
            messageUIController?.ClearFriendNames();
            messageUIController?.BuildFriendListForMessaging(friends);
            return;
        }

        foreach (Transform child in friendListPanel)
            Destroy(child.gameObject);

        statusIcons.Clear();
        ClearFriendAvatarSprites();
        messageUIController?.ClearFriendNames();
        messageUIController?.BuildFriendListForMessaging(friends);

        if (friends == null)
            return;

        foreach (var friend in friends)
        {
            GameObject item = Instantiate(friendItemPrefab, friendListPanel);
            var statusIcon = item.transform.Find("OnlineIcon")?.GetComponent<Image>();
            var nameText = item.transform.Find("PlayerName").GetComponent<TMP_Text>();
            var levelText = item.transform.Find("Level").GetComponent<TMP_Text>();
            nameText.text = friend.fullname.ToString();
            levelText.text = $"Lv {friend.level}";
            int fid = friend.playerId;
            messageUIController?.RegisterFriendName(fid, friend.fullname.ToString());
            var avatarRaw = item.transform.Find("Avatar")?.GetComponent<RawImage>();
            var avatarImage = item.transform.Find("Avatar")?.GetComponent<Image>();
            TryLoadFriendAvatar(friend, avatarRaw, avatarImage);
            statusIcons[fid] = statusIcon;
            WebSocketHelper.Instance.CheckPlayerOnline(fid, isOnline =>
            {
                UpdateIcon(statusIcon, isOnline);
                messageUIController?.UpdateFriendStatusIcon(fid, isOnline);
            });
            var challengeBtnTransform = item.transform.Find("ChallengeButton");
            if (challengeBtnTransform != null)
            {
                challengeBtnTransform.gameObject.SetActive(true);
                var challengeBtn = challengeBtnTransform.GetComponent<Button>();
            if (challengeBtn != null)
            {
                challengeBtn.onClick.AddListener(() => ShowChallengePopup(fid));
            }
            }

            var messageButton = FriendMessageUIController.FindButtonByName(item.transform, "MessageButton");
            if (messageButton != null)
            {
                messageButton.gameObject.SetActive(true);
                messageButton.onClick.RemoveAllListeners();
                messageButton.onClick.AddListener(() =>
                {
                    messageUIController?.ShowConversation(fid);
                });
            }

            var removeBtnTransform = item.transform.Find("RemoveButton");
            if (removeBtnTransform != null)
            {
                removeBtnTransform.gameObject.SetActive(true);
                var removeBtn = removeBtnTransform.GetComponent<Button>();
                if (removeBtn != null)
                {
                    removeBtn.onClick.AddListener(() =>
                        PopupHelper.Instance.ShowPopup("Bạn có chắc muốn hủy kết bạn?", () =>
                        {
                            RemoveFriend(PlayerId, fid);
                            LoadFriendList();
                        }));
                }
            }

            var acceptBtnTransform = item.transform.Find("AcceptButton");
            if (acceptBtnTransform != null)
            {
                acceptBtnTransform.gameObject.SetActive(false);
            }

            var declineBtnTransform = item.transform.Find("DeclineButton");
            if (declineBtnTransform != null)
            {
                declineBtnTransform.gameObject.SetActive(false);
            }
        }

        if (checkOnlineCoroutine != null)
            StopCoroutine(checkOnlineCoroutine);
        checkOnlineCoroutine = StartCoroutine(CheckFriendsOnlineRoutine());

        LayoutRebuilder.ForceRebuildLayoutImmediate(friendListPanel.GetComponent<RectTransform>());
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

    private void UpdateIcon(Image icon, bool isOnline)
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

    private IEnumerator CheckFriendsOnlineRoutine()
    {
        var wait = new WaitForSeconds(OnlineCheckInterval);
        while (true)
        {
            foreach (var kv in statusIcons)
            {
                int fid = kv.Key;
                var icon = kv.Value;
                WebSocketHelper.Instance.CheckPlayerOnline(fid, isOnline =>
                {
                    UpdateIcon(icon, isOnline);
                    messageUIController?.UpdateFriendStatusIcon(fid, isOnline);
                });
            }
            yield return wait;
        }
    }

    private void BuildFriendRequestList(PlayerInfoStruct[] requests)
    {
        if (friendRequestPanel == null || friendItemPrefab == null)
            return;

        foreach (Transform child in friendRequestPanel)
            Destroy(child.gameObject);

        if (requests == null)
            return;

        foreach (var req in requests)
        {
            GameObject item = Instantiate(friendItemPrefab, friendRequestPanel);
            var nameText = item.transform.Find("PlayerName").GetComponent<TMP_Text>();
            var levelText = item.transform.Find("Level").GetComponent<TMP_Text>();
            nameText.text = req.fullname.ToString();
            levelText.text = $"Lv {req.level}";

            int rid = req.playerId;
            var challengeBtnTransform = item.transform.Find("ChallengeButton");
            if (challengeBtnTransform != null)
            {
                challengeBtnTransform.gameObject.SetActive(false);
            }

            var removeBtnTransform = item.transform.Find("RemoveButton");
            if (removeBtnTransform != null)
            {
                removeBtnTransform.gameObject.SetActive(false);
            }

            var requestMessageButton = FriendMessageUIController.FindButtonByName(item.transform, "MessageButton");
            if (requestMessageButton != null)
            {
                requestMessageButton.gameObject.SetActive(false);
                requestMessageButton.onClick.RemoveAllListeners();
            }

            Button acceptBtn = null;
            var acceptBtnTransform = item.transform.Find("AcceptButton");
            if (acceptBtnTransform != null)
            {
                acceptBtnTransform.gameObject.SetActive(true);
                acceptBtn = acceptBtnTransform.GetComponent<Button>();
            }

            Button declineBtn = null;
            var declineBtnTransform = item.transform.Find("DeclineButton");
            if (declineBtnTransform != null)
            {
                declineBtnTransform.gameObject.SetActive(true);
                declineBtn = declineBtnTransform.GetComponent<Button>();
            }

            if (acceptBtn != null)
            {
                acceptBtn.onClick.AddListener(() =>
                {
                    RespondFriendRequest(rid, PlayerId, 1);
                    LoadFriendRequests();
                    LoadFriendList();
                });
            }

            if (declineBtn != null)
            {
                declineBtn.onClick.AddListener(() =>
                {
                    RespondFriendRequest(rid, PlayerId, 0);
                    LoadFriendRequests();
                });
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(friendRequestPanel.GetComponent<RectTransform>());
    }

    public void ShowChallengePopup(int friendId, int? bet = null, int roomId = 0)
    {
        if (bet.HasValue)
        {
            PopupHelper.Instance?.ShowIncomingChallengePopup(friendId, bet.Value, roomId);
            return;
        }

        if (challengePopupPrefab == null)
            return;

        GameObject canvas = GameObject.FindGameObjectWithTag("UICanvas");
        if (canvas == null)
            return;

        GameObject popup = Instantiate(challengePopupPrefab, canvas.transform);
        Button bet3 = popup.transform.Find("Bet3Button").GetComponent<Button>();
        Button bet6 = popup.transform.Find("Bet6Button").GetComponent<Button>();
        Button bet12 = popup.transform.Find("Bet12Button").GetComponent<Button>();
        bet3.onClick.AddListener(() => { Destroy(popup); SendChallenge(friendId, 6); });
        bet6.onClick.AddListener(() => { Destroy(popup); SendChallenge(friendId, 12); });
        bet12.onClick.AddListener(() => { Destroy(popup); SendChallenge(friendId, 24); });
    }

    public void SendChallenge(int friendId, int bet)
    {
        WebSocketHelper.Instance.Send(new WebSocketHelper.ChallengeMessage
        {
            type = "friend_challenge",
            senderId = PlayerId,
            receiverId = friendId,
            bet = bet
        });
    }

    






}
