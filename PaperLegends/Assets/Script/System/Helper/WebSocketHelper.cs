using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using WebSocketSharp;

public class WebSocketHelper : MonoBehaviour
{
    private static WebSocketHelper instance;
    private MainThreadDispatcher mainThreadDispatcher;
    public static WebSocketHelper Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject(nameof(WebSocketHelper));
                instance = go.AddComponent<WebSocketHelper>();
            }
            return instance;
        }
    }

    private WebSocket socket;
    private readonly Dictionary<int, Action<bool>> pendingOnlineChecks = new Dictionary<int, Action<bool>>();
    private int playerId;
    private Coroutine reconnectCoroutine;
    private int reconnectAttempts;
    private const int MaxReconnectAttempts = 5;
    private const float ReconnectTimeoutSeconds = 8f;
    private Coroutine heartbeatCoroutine;
    private string currentAccessToken;
    private bool unauthorizedNotified;
    private bool reconnectLoadingShown;
    private bool reconnectInProgress;
    private bool reconnectResultNotified;
    private bool unauthorizedHandlingInProgress;
    private bool isConnecting;
    private bool isPaused;
    private bool manualDisconnectRequested;
    private bool socketGameOverFallbackInProgress;

    private const string AccessTokenKey = "AccessToken";
    private const string RefreshTokenKey = "RefreshToken";
    private const string AccessTokenExpiryKey = "AccessTokenExpiresAt";
    private const string RefreshTokenExpiryKey = "RefreshTokenExpiresAt";

    public static event Action<QueueUpdateMessage> OnQueueUpdate;
    public static event Action<MatchFoundMessage> OnMatchFound;
    public static event Action<MatchConfirmedMessage> OnMatchConfirmed;
    public static event Action<MatchTicketMessage> OnMatchTicket;
    public static event Action<MatchFailedMessage> OnMatchFailed;
    public static event Action<MatchCancelledMessage> OnMatchCancelled;
    public static event Action<QueueCancelledMessage> OnQueueCancelled;
    public static event Action<QueueBlockedMessage> OnQueueBlocked;
    public static event Action<MatchLoadingMessage> OnMatchLoading;
    public static event Action<PaperLegendCharacterSelectionStartMessage> OnPaperLegendCharacterSelectionStart;
    public static event Action<PaperLegendCharacterSelectionUpdateMessage> OnPaperLegendCharacterSelectionUpdate;
    public static event Action<PaperLegendCharacterSelectionCompleteMessage> OnPaperLegendCharacterSelectionComplete;
    public static event Action<PaperLegendCharacterSelectionRejectedMessage> OnPaperLegendCharacterSelectionRejected;
    public static event Action<MatchFinishedMessage> OnMatchFinished;
    public static event Action<MatchEarlyExitRegisteredMessage> OnMatchEarlyExitRegistered;
    public static event Action<MatchEarlyExitResultMessage> OnMatchEarlyExitResultMessage;
    public static event Action<RoomReadyUpdateMessage> OnRoomReadyUpdate;
    public static event Action<RoomStartMessage> OnRoomStart;
    public static event Action<RoomStartCancelMessage> OnRoomStartCanceled;
    public static event Action<RoomUsersMessage> OnRoomUsersUpdate;
    public static event Action<RoomChatMessage> OnRoomChatMessage;
    public static event Action<FriendListMessage> OnFriendListMessage;
    public static event Action<RoomKickedMessage> OnRoomKicked;
    public static event Action<RoomListUpdateMessage> OnRoomListUpdate;
    public static event Action<RoomLookupResultMessage> OnRoomLookupResult;

    public bool IsConnected => socket != null && socket.IsAlive;
    private bool HasActiveSocket => socket != null && (socket.IsAlive || socket.ReadyState == WebSocketState.Connecting);

    private bool ShouldShowMatchFinishedResults(MatchFinishedMessage finished)
    {
        if (finished == null || string.IsNullOrWhiteSpace(finished.matchId))
        {
            return false;
        }

        var results = finished.result?.overGameResults;
        if (results == null || results.Count == 0)
        {
            return false;
        }

        int localPlayerId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? playerId;
        if (localPlayerId <= 0 || !results.Exists(result => result != null && result.playerId == localPlayerId))
        {
            return false;
        }

        var quickMatchClient = QuickMatchClient.Instance;
        if (quickMatchClient != null && quickMatchClient.ShouldAcceptMatchFinished(finished.matchId))
        {
            return true;
        }

        var networkManager = GameManagerNetWork.Instance;
        string activeResultMatchId = networkManager != null ? networkManager.currentQuickMatchResultId : null;
        bool matchIdMatches = !string.IsNullOrWhiteSpace(activeResultMatchId) &&
                              string.Equals(activeResultMatchId.Trim(), finished.matchId.Trim(), StringComparison.OrdinalIgnoreCase);
        bool hasActiveQuickMatchSession = networkManager != null &&
                                          !networkManager.ManualShutdownRequested &&
                                          (!string.IsNullOrEmpty(networkManager.currentQuickMatchId) ||
                                           networkManager.IsRunnerActive ||
                                           networkManager.IsReconnecting ||
                                           networkManager.WillAttemptReconnect);

        if (matchIdMatches && hasActiveQuickMatchSession)
        {
            Debug.Log($"[WebSocketHelper] Accept match:finished via persistent matchId={finished.matchId}, localPlayerId={localPlayerId}.");
            return true;
        }

        Debug.Log($"[WebSocketHelper] Ignore match:finished for inactive matchId={finished.matchId}, localPlayerId={localPlayerId}, activeResultMatchId={activeResultMatchId ?? "null"}.");
        return false;
    }

    private void StartSocketGameOverFallback(List<OverGameRequest> results)
    {
        if (socketGameOverFallbackInProgress)
            return;

        socketGameOverFallbackInProgress = true;
        StartCoroutine(ShowSocketGameOverFallbackRoutine(results));
    }

    private IEnumerator ShowSocketGameOverFallbackRoutine(List<OverGameRequest> results)
    {
        TryAcknowledgeGameOverBeforeSocketFallback();
        yield return new WaitForSecondsRealtime(0.15f);

        if (GameOverManager.Instance == null || GameOverManager.Instance.HasGameOverResults)
        {
            socketGameOverFallbackInProgress = false;
            yield break;
        }

        if (ClientGameplayBridge.UI.HasInstance())
        {
            var announcement = ClientGameplayBridge.UI.ShowImpactAnnouncementRunTime(
                LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText("game_over_title") : "Game Over",
                "game_over_subtitle",
                1.2f);

            if (announcement != null)
                yield return announcement;
        }

        if (GameOverManager.Instance != null && !GameOverManager.Instance.HasGameOverResults)
        {
            ClientGameplayBridge.Match.ShowGameOverResults(results);
        }

        socketGameOverFallbackInProgress = false;
    }

    private void TryAcknowledgeGameOverBeforeSocketFallback()
    {
        var networkManager = GameManagerNetWork.Instance;
        int localPlayerId = networkManager?.loginUserModel?.UserId ?? playerId;
        if (localPlayerId <= 0)
            return;

        var serverRpc = networkManager?.serverRPC != null ? networkManager.serverRPC : NetworkObjectManager.Instance;
        if (serverRpc == null || serverRpc.Object == null || !serverRpc.Object.IsValid)
            return;

        try
        {
            Debug.Log($"[WebSocketHelper] Sending GameOver ACK via socket fallback for playerId={localPlayerId}.");
            serverRpc.RpcClientAcknowledgeGameOver(localPlayerId);
            serverRpc.RpcClientReadyToDisconnect(localPlayerId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocketHelper] Failed to ACK GameOver via socket fallback: {e.Message}");
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            mainThreadDispatcher = MainThreadDispatcher.Instance();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
 

    public void Send(object payload)
    {
        if (socket == null || !socket.IsAlive)
        {
            Debug.LogWarning($"[WebSocketHelper] WebSocket not connected. payloadType={payload?.GetType().Name ?? "null"}");
            TryReconnect();
            return;
        }

        try
        {
            string json = JsonUtility.ToJson(payload);
            Debug.Log($"[WebSocketHelper] Send => {json}");
            socket.SendAsync(json, null);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[WebSocketHelper] WebSocket send failed: " + e.Message);
            TryReconnect();
        }
    }

    public void Connect(int playerId)
    {
        if (playerId <= 0)
            return;

        this.playerId = playerId;

        if (isPaused)
        {
            Debug.Log("Skip WebSocket connect while application is paused.");
            return;
        }

        if (HasActiveSocket || isConnecting)
            return;

        manualDisconnectRequested = false;
        isConnecting = true;

        try
        {
            string url = ApiConfig.WebSocketUrl;
            socket = new WebSocket(url);
            var activeSocket = socket;

            // Sự kiện khi WebSocket kết nối thành công
            socket.OnOpen += (sender, e) => EnqueueOnMainThread(() =>
            {
                try
                {
                    isConnecting = false;
                    Debug.Log("WebSocket connection successful!");
                    reconnectAttempts = 0;
                    HideReconnectLoading();
                    NotifyReconnectResult(true);

                    if (activeSocket != socket || activeSocket == null || !activeSocket.IsAlive)
                        return;

                    var registerMessage = new RegisterMessage
                    {
                        type = "register",
                        playerId = playerId
                    };

                    activeSocket.Send(JsonUtility.ToJson(registerMessage));
                    RestartHeartbeat();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("WebSocket OnOpen handler failed: " + ex.Message);

                    if (activeSocket == socket && !manualDisconnectRequested && !isPaused)
                        TryReconnect();
                }
            });

            socket.OnClose += (sender, e) => EnqueueOnMainThread(() =>
            {
                Debug.LogWarning($"[WebSocketHelper] Socket closed. code={e.Code}, reason={e.Reason}, wasClean={e.WasClean}, manualDisconnect={manualDisconnectRequested}, isPaused={isPaused}");
                if (activeSocket == socket)
                {
                    isConnecting = false;
                    socket = null;
                }

                if (e.Code == 4001)
                {
                    HandleUnauthorized(null);
                }

                StopHeartbeat();

                if (!manualDisconnectRequested && !isPaused && e.Code != 4001)
                    TryReconnect();
            });

            socket.OnError += (sender, e) => EnqueueOnMainThread(() =>
            {
                Debug.LogWarning("[WebSocketHelper] WebSocket error: " + e.Message);

                if (activeSocket == socket && !manualDisconnectRequested && !isPaused)
                    TryReconnect();
            });

            socket.OnMessage += HandleMessage;
            socket.ConnectAsync();
        }
        catch (Exception e)
        {
            isConnecting = false;
            Debug.LogWarning("WebSocket connect failed: " + e.Message);
            TryReconnect();
        }
    }

    public void CheckPlayerOnline(int playerId, Action<bool> callback)
    {
        if (callback != null)
        {
            pendingOnlineChecks[playerId] = callback;
        }
        Send(new RegisterMessage  { type = "check_player_online",playerId= playerId });
    }

    public void SendLeaveRoom(int roomId, int playerId)
    {
        if (roomId <= 0 || playerId <= 0)
        {
            Debug.LogWarning("Room leave payload is invalid.");
            return;
        }

        Send(new RoomLeaveMessage
        {
            type = "room_leave",
            roomId = roomId,
            playerId = playerId
        });
    }

    public void SendRoomLookup(string roomCode, int playerId)
    {
        if (string.IsNullOrWhiteSpace(roomCode) || playerId <= 0)
        {
            Debug.LogWarning("Room lookup payload is invalid.");
            return;
        }

        Send(new RoomLookupRequestMessage
        {
            type = "room_lookup",
            roomCode = roomCode,
            playerId = playerId
        });
    }

    public void SendMatchAck(string matchId, int playerId)
    {
        if (string.IsNullOrWhiteSpace(matchId) || playerId <= 0)
        {
            Debug.LogWarning("Match ack payload is invalid.");
            return;
        }

        Send(new MatchAckMessage
        {
            type = "match:ack",
            matchId = matchId,
            playerId = playerId
        });
    }

    public void SendPaperLegendCharacterSelect(string matchId, int playerId, int characterModelId)
    {
        if (playerId <= 0 || characterModelId <= 0)
        {
            Debug.LogWarning("Paper Legends character selection payload is invalid.");
            return;
        }

        Send(new PaperLegendCharacterSelectMessage
        {
            type = "paper_legend:character_select",
            matchId = matchId ?? string.Empty,
            playerId = playerId,
            characterModelId = characterModelId
        });
    }

    public void SendPaperLegendCharacterLock(string matchId, int playerId)
    {
        if (playerId <= 0)
        {
            Debug.LogWarning("Paper Legends character lock payload is invalid.");
            return;
        }

        Send(new PaperLegendCharacterLockMessage
        {
            type = "paper_legend:character_lock",
            matchId = matchId ?? string.Empty,
            playerId = playerId
        });
    }



    public void Disconnect()
    {
        manualDisconnectRequested = true;
        isConnecting = false;

        if (socket != null)
        {
            socket.OnMessage -= HandleMessage;
            socket.CloseAsync();
            socket = null;
        }

        StopHeartbeat();

        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }
    }

    public void SetAccessToken(string accessToken)
    {
        currentAccessToken = accessToken;
        unauthorizedNotified = false;
        RestartHeartbeat();
    }

    private void TryReconnect()
    {
        if (manualDisconnectRequested || isPaused || reconnectCoroutine != null || playerId == 0)
            return;

        ShowReconnectLoading();
        reconnectCoroutine = StartCoroutine(ReconnectRoutine());
    }

    private IEnumerator ReconnectRoutine()
    {
        reconnectInProgress = true;
        reconnectResultNotified = false;

        while (reconnectAttempts < MaxReconnectAttempts)
        {
            reconnectAttempts++;
            float delay = Mathf.Min(5f, Mathf.Pow(2, reconnectAttempts));
            yield return new WaitForSeconds(delay);

            Connect(playerId);

            float elapsed = 0f;
            while (elapsed < ReconnectTimeoutSeconds)
            {
                if (IsConnected)
                {
                    reconnectCoroutine = null;
                    reconnectInProgress = false;
                    NotifyReconnectResult(true);
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        Debug.LogWarning("Max reconnection attempts reached.");
        reconnectCoroutine = null;
        NotifyReconnectResult(false);
        reconnectInProgress = false;
        HandleReconnectFailure();
    }

    private void RestartHeartbeat()
    {
        StopHeartbeat();

        if (socket == null || !socket.IsAlive || string.IsNullOrEmpty(currentAccessToken))
            return;

        heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
    }

    private void StopHeartbeat()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }
    }

    private IEnumerator HeartbeatRoutine()
    {
        var wait = new WaitForSeconds(60f);
        while (socket != null && socket.IsAlive && !string.IsNullOrEmpty(currentAccessToken))
        {
            Send(new HeartbeatMessage { type = "heartbeat", accessToken = currentAccessToken });
            yield return wait;
        }

        heartbeatCoroutine = null;
    }

    private void HandleMessage(object sender, MessageEventArgs e)
    {
        string messageData = e.Data;

        if (string.IsNullOrEmpty(messageData))
            return;

        EnqueueOnMainThread(() => ProcessMessage(messageData));
    }

    private void ProcessMessage(string messageData)
    {
        Debug.Log("WebSocket message: " + messageData);

        try
        {
            var baseMsg = JsonUtility.FromJson<SocketMessage>(messageData);
            if (baseMsg != null && !string.IsNullOrEmpty(baseMsg.type))
            {
                switch (baseMsg.type.ToLower())
                {
                    case "invite":
                    case "register":
                        Debug.Log("Đăng nhập thành công");
                        break;
                    case "heartbeat":
                        break;
                    case "error":
                        var error = JsonUtility.FromJson<ErrorMessage>(messageData);
                        if (error != null && IsRecoverableHeartbeatError(error))
                        {
                            HandleHeartbeatRecoverableError();
                        }
                        else if (error != null && string.Equals(error.code, "unauthorized", StringComparison.OrdinalIgnoreCase))
                        {
                            HandleUnauthorized(error.message);
                        }
                        break;
                    case "friend_invite":
                        MainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            APIManager.Instance?.LoadNotifications();
                            UserInfoHandler.Instance?.IncrementPendingFriendRequestCount();
                        });
                        break;
                    case "message":
                        var msg = JsonUtility.FromJson<MessagePayload>(messageData);
                        if (msg != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                FriendController.Instance?.HandleIncomingMessage(msg));
                        }
                        break;
                    case "friend_challenge": //bước gửi khiêu chiến cho bạn bè
                        var challenge = JsonUtility.FromJson<ChallengeMessage>(messageData);
                        if (challenge != null)
                        {
                            Debug.Log($"[WebSocketHelper] Received friend_challenge. senderId={challenge.senderId}, receiverId={challenge.receiverId}, bet={challenge.bet}, roomId={challenge.roomId}");
                            //gửi trên 1 luồn khác hiện tại
                            MainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                if (FriendController.Instance != null)
                                {
                                    FriendController.Instance.ShowChallengePopup(challenge.senderId, challenge.bet, challenge.roomId);
                                    return;
                                }

                                if (PopupHelper.Instance != null)
                                {
                                    Debug.LogWarning("[WebSocketHelper] FriendController.Instance == null, fallback hiển thị popup bằng PopupHelper.");
                                    PopupHelper.Instance.ShowIncomingChallengePopup(challenge.senderId, challenge.bet, challenge.roomId);
                                    return;
                                }

                                Debug.LogWarning("[WebSocketHelper] Không thể hiển thị popup friend_challenge vì FriendController và PopupHelper đều null.");
                            });
                        }
                        break;
                  //  case "friend_challenge_response_fromsocket": // sau khi đối thủ xác nhận khiêu chiến tại ShowIncomingChallengePopup gửi lên websocket, lúc này websocke trả về lại tín hiệu này
                        var response = JsonUtility.FromJson<ChallengeResponseMessage>(messageData);
                        if (response != null)
                          //  MainThreadDispatcher.Instance().Enqueue(() =>
                              //  QuickMatchHandler.Instance.OnChallengeResponse(response.senderId, response.bet, response.accepted));
                        break;
                    case "check_player_online":
                        var online = JsonUtility.FromJson<CheckOnlineMessage>(messageData);
                        if (online != null && pendingOnlineChecks.TryGetValue(online.playerId, out var cb))
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                            cb?.Invoke(online.isOnline));
                            MainThreadDispatcher.Instance().Enqueue(() =>
                            pendingOnlineChecks.Remove(online.playerId));
                        }
                        break;
                    case "player_status_change":
                        var status = JsonUtility.FromJson<PlayerStatusChangeMessage>(messageData);
                        if (status != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                FriendController.Instance?.UpdateFriendStatus(status.playerId, status.isOnline));
                        }
                        break;
                    case "queue:update":
                        var update = JsonUtility.FromJson<QueueUpdateMessage>(messageData);
                        if (update != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnQueueUpdate?.Invoke(update));
                        }
                        break;
                    case "match:found":
                        var found = JsonUtility.FromJson<MatchFoundMessage>(messageData);
                        if (found != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnMatchFound?.Invoke(found));
                        }
                        break;
                    case "match:confirmed":
                        var confirmed = JsonUtility.FromJson<MatchConfirmedMessage>(messageData);
                        if (confirmed != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnMatchConfirmed?.Invoke(confirmed));
                        }
                        break;
                    case "match:ticket":
                        var ticket = JsonUtility.FromJson<MatchTicketMessage>(messageData);
                        if (ticket != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnMatchTicket?.Invoke(ticket));
                        }
                        break;
                    case "match:failed":
                        var failed = JsonUtility.FromJson<MatchFailedMessage>(messageData);
                        if (failed != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnMatchFailed?.Invoke(failed));
                        }
                        break;
                    case "match:cancelled":
                        var cancelledMatch = JsonUtility.FromJson<MatchCancelledMessage>(messageData);
                        if (cancelledMatch != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnMatchCancelled?.Invoke(cancelledMatch));
                        }
                        break;
                    case "queue:cancelled":
                        var cancelled = JsonUtility.FromJson<QueueCancelledMessage>(messageData);
                        if (cancelled != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnQueueCancelled?.Invoke(cancelled));
                        }
                        break;
                    case "queue:blocked":
                        var blocked = JsonUtility.FromJson<QueueBlockedMessage>(messageData);
                        if (blocked != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnQueueBlocked?.Invoke(blocked));
                        }
                        break;
                    case "match:loading":
                        var loading = JsonUtility.FromJson<MatchLoadingMessage>(messageData);
                        if (loading != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnMatchLoading?.Invoke(loading));
                        }
                        break;
                    case "paper_legend:character_selection_start":
                        var characterSelectionStart = JsonUtility.FromJson<PaperLegendCharacterSelectionStartMessage>(messageData);
                        if (characterSelectionStart != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnPaperLegendCharacterSelectionStart?.Invoke(characterSelectionStart));
                        }
                        break;
                    case "paper_legend:character_selection_update":
                        var characterSelectionUpdate = JsonUtility.FromJson<PaperLegendCharacterSelectionUpdateMessage>(messageData);
                        if (characterSelectionUpdate != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnPaperLegendCharacterSelectionUpdate?.Invoke(characterSelectionUpdate));
                        }
                        break;
                    case "paper_legend:character_selection_complete":
                        var characterSelectionComplete = JsonUtility.FromJson<PaperLegendCharacterSelectionCompleteMessage>(messageData);
                        if (characterSelectionComplete != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnPaperLegendCharacterSelectionComplete?.Invoke(characterSelectionComplete));
                        }
                        break;
                    case "paper_legend:character_selection_rejected":
                        var characterSelectionRejected = JsonUtility.FromJson<PaperLegendCharacterSelectionRejectedMessage>(messageData);
                        if (characterSelectionRejected != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnPaperLegendCharacterSelectionRejected?.Invoke(characterSelectionRejected));
                        }
                        break;
                    case "match:finished":
                        var finished = JsonUtility.FromJson<MatchFinishedMessage>(messageData);
                        if (finished != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                OnMatchFinished?.Invoke(finished);
                                var results = finished.result?.overGameResults;
                                if (results != null && results.Count > 0)
                                {
                                    if (ShouldShowMatchFinishedResults(finished) &&
                                        GameOverManager.Instance != null &&
                                        !GameOverManager.Instance.HasGameOverResults)
                                    {
                                        StartSocketGameOverFallback(results);
                                    }
                                }
                            });
                        }
                        break;
                    case "match:early_exit_registered":
                        var earlyExit = JsonUtility.FromJson<MatchEarlyExitRegisteredMessage>(messageData);
                        if (earlyExit != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnMatchEarlyExitRegistered?.Invoke(earlyExit));
                        }
                        break;
                    case "match:early_exit_result_message":
                        var earlyExitResult = JsonUtility.FromJson<MatchEarlyExitResultMessage>(messageData);
                        if (earlyExitResult != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnMatchEarlyExitResultMessage?.Invoke(earlyExitResult));
                        }
                        break;
                    case "room_ready_update":
                        var readyUpdate = JsonUtility.FromJson<RoomReadyUpdateMessage>(messageData);
                        if (readyUpdate != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnRoomReadyUpdate?.Invoke(readyUpdate));
                        }
                        break;
                    case "room_start":
                        var roomStart = JsonUtility.FromJson<RoomStartMessage>(messageData);
                        if (roomStart != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnRoomStart?.Invoke(roomStart));
                        }
                        break;
                    case "room_start_cancel":
                        var roomStartCancel = JsonUtility.FromJson<RoomStartCancelMessage>(messageData);
                        if (roomStartCancel != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnRoomStartCanceled?.Invoke(roomStartCancel));
                        }
                        break;
                    case "room_users":
                        var roomUsers = JsonUtility.FromJson<RoomUsersMessage>(messageData);
                        if (roomUsers != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnRoomUsersUpdate?.Invoke(roomUsers));
                        }
                        break;
                    case "room_kicked":
                        var roomKicked = JsonUtility.FromJson<RoomKickedMessage>(messageData);
                        if (roomKicked != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnRoomKicked?.Invoke(roomKicked));
                        }
                        break;
                    case "room_chat":
                        var roomChat = JsonUtility.FromJson<RoomChatMessage>(messageData);
                        if (roomChat != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnRoomChatMessage?.Invoke(roomChat));
                        }
                        break;
                    case "room_list_update":
                    case "room_update":
                        var roomListUpdate = JsonUtility.FromJson<RoomListUpdateMessage>(messageData);
                        if (roomListUpdate != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnRoomListUpdate?.Invoke(roomListUpdate));
                        }
                        break;
                    case "room_lookup_result":
                        var roomLookupResult = JsonUtility.FromJson<RoomLookupResultMessage>(messageData);
                        if (roomLookupResult != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnRoomLookupResult?.Invoke(roomLookupResult));
                        }
                        break;
                    case "friend_list":
                        var friendList = JsonUtility.FromJson<FriendListMessage>(messageData);
                        if (friendList != null)
                        {
                            MainThreadDispatcher.Instance().Enqueue(() =>
                                OnFriendListMessage?.Invoke(friendList));
                        }
                        break;
                }
            }
            else
                Debug.Log("Not support this request");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Parse WebSocket message failed: " + ex.Message);
        }
    }

    private void HandleUnauthorized(string message)
    {
        if (unauthorizedHandlingInProgress)
            return;

        unauthorizedHandlingInProgress = true;
        unauthorizedNotified = true;
        StopHeartbeat();

        StartCoroutine(HandleUnauthorizedRoutine(message));
    }

    private IEnumerator HandleUnauthorizedRoutine(string message)
    {
        bool refreshCompleted = false;
        bool refreshSuccess = false;
        string refreshFailureReason = null;

        yield return StartCoroutine(TryRefreshSessionCoroutine(
            success =>
            {
                refreshSuccess = success;
                refreshCompleted = true;
            },
            reason => refreshFailureReason = reason));

        if (refreshCompleted && refreshSuccess)
        {
            unauthorizedNotified = false;
            unauthorizedHandlingInProgress = false;

            if (socket != null)
            {
                socket.OnMessage -= HandleMessage;
                socket.CloseAsync();
                socket = null;
            }

            TryReconnect();
            yield break;
        }

        if (socket != null)
        {
            socket.OnMessage -= HandleMessage;
            socket.CloseAsync();
        }

        playerId = 0;
        unauthorizedHandlingInProgress = false;

        string popupMessage = BuildUnauthorizedErrorMessage(message, refreshFailureReason);
        EnqueueOnMainThread(() =>
        {
            PopupHelper.Instance?.ShowPopupOut(popupMessage, null);
        });
    }

    private IEnumerator TryRefreshSessionCoroutine(Action<bool> onComplete, Action<string> onFailureReason)
    {
        var loginModel = GameManagerNetWork.Instance?.loginUserModel;
        string refreshToken = loginModel?.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            onFailureReason?.Invoke("Không có refresh token để tự động đăng nhập lại.");
            onComplete?.Invoke(false);
            yield break;
        }

        string url = ApiConfig.BaseUrl + "/auth/refresh";
        var payload = new RefreshTokenRequestMessage
        {
            refreshToken = refreshToken,
            deviceId = SystemInfo.deviceUniqueIdentifier
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string backendMessage = ExtractBackendMessage(request.downloadHandler != null ? request.downloadHandler.text : null);
                string reason = $"Không thể tự động đăng nhập lại (HTTP {request.responseCode}): {request.error}";
                if (!string.IsNullOrWhiteSpace(backendMessage))
                {
                    reason += $" | Chi tiết: {backendMessage}";
                }

                onFailureReason?.Invoke(reason);
                onComplete?.Invoke(false);
                yield break;
            }

            RefreshTokenResponseMessage tokenResponse = null;
            try
            {
                tokenResponse = JsonUtility.FromJson<RefreshTokenResponseMessage>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onFailureReason?.Invoke("Parse token refresh response thất bại: " + ex.Message);
                onComplete?.Invoke(false);
                yield break;
            }

            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.accessToken))
            {
                onFailureReason?.Invoke("API refresh không trả về accessToken hợp lệ.");
                onComplete?.Invoke(false);
                yield break;
            }

            if (loginModel != null)
            {
                loginModel.AccessToken = tokenResponse.accessToken;
                loginModel.AccessTokenExpiresAt = tokenResponse.accessTokenExpiresAt;

                if (!string.IsNullOrWhiteSpace(tokenResponse.refreshToken))
                    loginModel.RefreshToken = tokenResponse.refreshToken;

                if (!string.IsNullOrWhiteSpace(tokenResponse.refreshTokenExpiresAt))
                    loginModel.RefreshTokenExpiresAt = tokenResponse.refreshTokenExpiresAt;
            }

            PersistSessionTokens(loginModel);
            SetAccessToken(tokenResponse.accessToken);
            onComplete?.Invoke(true);
        }
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
    }

    private string BuildUnauthorizedErrorMessage(string wsMessage, string refreshFailureReason)
    {
        string normalizedWs = string.IsNullOrWhiteSpace(wsMessage)
            ? "unauthorized"
            : wsMessage.Trim();

        if (string.Equals(normalizedWs, "unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            normalizedWs = "Phiên đăng nhập không hợp lệ.";
        }

        if (string.IsNullOrWhiteSpace(refreshFailureReason))
        {
            return normalizedWs;
        }

        return normalizedWs + "\n" + refreshFailureReason;
    }

    private string ExtractBackendMessage(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        try
        {
            var backendError = JsonUtility.FromJson<BackendMessageResponse>(responseText);
            if (backendError != null && !string.IsNullOrWhiteSpace(backendError.message))
                return backendError.message.Trim();
        }
        catch
        {
            // ignore parse errors, fallback to raw text
        }

        return responseText.Trim();
    }

    private bool IsRecoverableHeartbeatError(ErrorMessage error)
    {
        if (error == null)
            return false;

        if (!string.Equals(error.code, "unauthorized", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(error.message))
            return false;

        return error.message.Contains("exp", StringComparison.OrdinalIgnoreCase)
            || error.message.Contains("timestamp", StringComparison.OrdinalIgnoreCase)
            || error.message.Contains("claim", StringComparison.OrdinalIgnoreCase);
    }

    private void HandleHeartbeatRecoverableError()
    {
        StopHeartbeat();
        ShowReconnectLoading();

        if (socket != null)
        {
            socket.OnMessage -= HandleMessage;
            socket.CloseAsync();
            socket = null;
        }

        TryReconnect();
    }

    private void ShowReconnectLoading()
    {
        if (reconnectLoadingShown)
            return;

        reconnectLoadingShown = true;
        EnqueueOnMainThread(() =>
        {
            if (LoadingManager.Instance?.UILoadingScreenPrefab != null)
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        });
    }

    private void HideReconnectLoading()
    {
        if (!reconnectLoadingShown)
            return;

        reconnectLoadingShown = false;
        EnqueueOnMainThread(() =>
        {
            if (LoadingManager.Instance?.UILoadingScreenPrefab != null)
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
        });
    }

    private void NotifyReconnectResult(bool success)
    {
        if (!reconnectInProgress || reconnectResultNotified)
            return;

        reconnectResultNotified = true;
        EnqueueOnMainThread(() =>
        {
            if (success)
            {
                NotificationHelper.Instance?.ShowNotification("Kết nối lại thành công.", true);
            }
            else
            {
                NotificationHelper.Instance?.ShowNotification("Kết nối lại thất bại. Vui lòng đăng nhập lại.", false);
            }
        });
    }

    private void HandleReconnectFailure()
    {
        HideReconnectLoading();

        if (socket != null)
        {
            socket.OnMessage -= HandleMessage;
            socket.CloseAsync();
            socket = null;
        }

        playerId = 0;
        unauthorizedNotified = false;

        EnqueueOnMainThread(() =>
        {
            if (GameManagerNetWork.Instance != null)
            {
                GameManagerNetWork.Instance.loginUserModel = new LoginUserModel();
            }

            if (MenuController.Instance != null)
            {
                MenuController.Instance.ShowLoginPanelAfterReconnectFailure();
            }
            else
            {
                LoadingManager.LoadScene("Menu");
            }
        });
    }

    private void EnqueueOnMainThread(Action action)
    {
        if (action == null)
            return;

        if (MainThreadDispatcher.IsMainThread)
        {
            action.Invoke();
            return;
        }

        if (mainThreadDispatcher == null)
        {
            mainThreadDispatcher = MainThreadDispatcher.Instance();
        }

        if (mainThreadDispatcher != null)
        {
            mainThreadDispatcher.Enqueue(action);
            return;
        }

        Debug.LogWarning("MainThreadDispatcher is unavailable; skip queued UI update.");
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        isPaused = pauseStatus;

        if (pauseStatus)
        {
            Disconnect();
            return;
        }

        manualDisconnectRequested = false;

        if (playerId > 0 && !string.IsNullOrEmpty(currentAccessToken))
            Connect(playerId);
    }

    private void OnApplicationQuit()
    {
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }

        Disconnect();
    }

    [Serializable]
    private class SocketMessage
    {
        public string type;
    }

    [Serializable]
    private class RefreshTokenRequestMessage
    {
        public string refreshToken;
        public string deviceId;
    }

    [Serializable]
    private class RefreshTokenResponseMessage
    {
        public string accessToken;
        public string accessTokenExpiresAt;
        public string refreshToken;
        public string refreshTokenExpiresAt;
    }

    [Serializable]
    private class BackendMessageResponse
    {
        public string message;
    }

    [Serializable]
    public class MatchFinishedMessage
    {
        public string matchId;
        public MatchFinishedResult result;
    }

    [Serializable]
    public class MatchFinishedResult
    {
        public long endedAtUnixMs;
        public List<OverGameRequest> overGameResults;
    }

    [Serializable]
    public class MatchEarlyExitRegisteredMessage
    {
        public string type;
        public string matchId;
        public int playerId;
        public int roomId;
    }

    [Serializable]
    public class MatchEarlyExitResultMessage
    {
        public string type;
        public string matchId;
        public int playerId;
        public string message;
    }

    [Serializable]
    private class CheckOnlineMessage
    {
        public string type;
        public int playerId;
        public bool isOnline;
    }

    [Serializable]
    private class PlayerStatusChangeMessage
    {
        public string type;
        public int playerId;
        public bool isOnline;
    }

    [Serializable]
    public class QueueUpdateMessage
    {
        public string type;
        public string bucket;
        public int current;
        public int required;
    }

    [Serializable]
    public class MatchFoundMessage
    {
        public string type;
        public string matchId;
        public int required;
        public int players;
    }

    [Serializable]
    public class MatchConfirmedMessage
    {
        public string type;
        public string matchId;
        public int required;
        public int players;
    }

    [Serializable]
    public class MatchTicketMessage
    {
        public string type;
        public string matchId;
        public string sessionName;
        public string region;
        public string joinToken;
        public long deadlineMs;
        public int hostPort;
    }

    [Serializable]
    public class MatchFailedMessage
    {
        public string type;
        public string matchId;
        public string reason;
        public string detail;
    }

    [Serializable]
    public class MatchCancelledMessage
    {
        public string type;
        public string matchId;
        public string reason;
    }

    [Serializable]
    public class RoomReadyMessage
    {
        public string type;
        public int roomId;
        public int playerId;
        public bool ready;
    }

    [Serializable]
    public class RoomReadyUpdateMessage
    {
        public string type;
        public int roomId;
        public int playerId;
        public bool ready;
    }

    [Serializable]
    public class RoomStartMessage
    {
        public string type;
        public int roomId;
        public string roomName;
        public int port;
        public int mapId;
    }

    [Serializable]
    public class RoomStartCancelMessage
    {
        public string type;
        public int roomId;
        public int playerId;
    }

    [Serializable]
    public class RoomStartAckMessage
    {
        public string type;
        public int roomId;
        public int playerId;
    }

    [Serializable]
    public class RoomUsersRequestMessage
    {
        public string type;
        public int roomId;
        public int playerId;
    }

    [Serializable]
    public class RoomKickMessage
    {
        public string type;
        public int roomId;
        public int playerId;
        public int requesterId;
    }

    [Serializable]
    public class RoomKickedMessage
    {
        public string type;
        public int roomId;
        public int playerId;
        public int requesterId;
    }

    [Serializable]
    public class RoomUsersMessage
    {
        public string type;
        public int roomId;
        public List<UserRoom> users;
    }

    [Serializable]
    public class RoomChatMessage
    {
        public string type;
        public int roomId;
        public int senderId;
        public string senderName;
        public string message;
    }

    [Serializable]
    public class RoomListUpdateMessage
    {
        public string type;
        public string action;
        public RoomData room;
        public int roomId;
        public int currentPlayers;
    }

    [Serializable]
    public class RoomLookupRequestMessage
    {
        public string type;
        public string roomCode;
        public int playerId;
    }

    [Serializable]
    public class RoomLookupResultMessage
    {
        public string type;
        public string roomCode;
        public bool success;
        public string message;
        public int roomId;
        public string roomName;
        public int bet;
        public int maxPlayers;
        public int mapId;
        public int createId;
        public int currentPlayers;
    }

    [Serializable]
    public class FriendListRequestMessage
    {
        public string type;
        public int playerId;
    }

    [Serializable]
    public class FriendListMessage
    {
        public string type;
        public int playerId;
        public List<FriendInfo> friends;
    }

    [Serializable]
    public class FriendInfo
    {
        public int playerId;
        public int level;
        public string fullname;
        public bool isOnline;
        public string avatarUrl;
    }

    [Serializable]
    public class QueueCancelledMessage
    {
        public string type;
        public int userId;
    }

    [Serializable]
    public class QueueBlockedMessage
    {
        public string type;
        public string reason;
        public int maxCCU;
    }

    [Serializable]
    public class MatchLoadingMessage
    {
        public string type;
        public string matchId;
        public string stage;
    }

    [Serializable]
    private class PaperLegendCharacterSelectMessage
    {
        public string type;
        public string matchId;
        public int playerId;
        public int characterModelId;
    }

    [Serializable]
    private class PaperLegendCharacterLockMessage
    {
        public string type;
        public string matchId;
        public int playerId;
    }

    [Serializable]
    public class PaperLegendCharacterSelectionStartMessage
    {
        public string type;
        public string matchId;
        public string playerIds;
        public string selectableModelIds;
        public string playerNames;
        public string botPlayerIds;
        public int totalPlayers;
        public int realPlayerCount;
        public int botCount;
        public float countdownSeconds;
        public long deadlineMs;
    }

    [Serializable]
    public class PaperLegendCharacterSelectionUpdateMessage
    {
        public string type;
        public string matchId;
        public int playerId;
        public int characterModelId;
        public string selectedModelIds;
        public int selectedCount;
        public int lockedCount;
        public int totalCount;
        public bool isLocked;
        public float remainingSeconds;
    }

    [Serializable]
    public class PaperLegendCharacterSelectionCompleteMessage
    {
        public string type;
        public string matchId;
        public string selections;
    }

    [Serializable]
    public class PaperLegendCharacterSelectionRejectedMessage
    {
        public string type;
        public string matchId;
        public int playerId;
        public int characterModelId;
        public string selectedModelIds;
        public string reason;
    }

    [Serializable]
    public class MatchAckMessage
    {
        public string type;
        public string matchId;
        public int playerId;
    }

    [Serializable]
    public class MessagePayload
    {
        public string type;
        public int senderId;
        public int receiverId;
        public int seqMess;
        public string content;
    }

    [Serializable]
    public class ChallengeMessage
    {
        public string type;
        public int senderId;
        public int receiverId;
        public int bet;
        public int roomId;
    }

    [Serializable]
    public class ChallengeResponseMessage
    {
        public string type;
        public int senderId;
        public int receiverId;
        public int bet;
        public int roomId;
        public bool accepted;
    }

    [Serializable]
    private class HeartbeatMessage
    {
        public string type;
        public string accessToken;
    }

    [Serializable]
    private class ErrorMessage
    {
        public string type;
        public string message;
        public string code;
    }
    [System.Serializable]
    public class RegisterMessage
    {
        public string type;
        public int playerId;
    }

    [Serializable]
    public class RoomLeaveMessage
    {
        public string type;
        public int roomId;
        public int playerId;
    }
}


