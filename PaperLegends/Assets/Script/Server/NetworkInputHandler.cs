using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkInputHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkInputHandler Instance { get; private set; }
    private readonly HashSet<PlayerRef> disconnectedPlayers = new HashSet<PlayerRef>();
    private readonly Dictionary<PlayerRef, string> playerDisplayNameCache = new Dictionary<PlayerRef, string>();
    private readonly Dictionary<PlayerRef, int> playerUserIdCache = new Dictionary<PlayerRef, int>();
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);  
        PaperLegendFlickInputCollector.EnsureExists();
    }
    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log($"Connected to server: {runner}");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning($"Failed to connect to {remoteAddress}: {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        Debug.Log($"Connect request from {request.RemoteAddress}");
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        Debug.Log("Received custom authentication response");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"Disconnected from server: {reason}");
        // GameSessionClientLocal.Instance?.StopPlayerTurn();
        if (ShouldSuppressExpectedDisconnectPopup())
        {
            Debug.Log("Disconnected by expected game flow; skipping disconnect popup.");
            if (ClientGameplayBridge.Camera.HasInstance())
            {
                ClientGameplayBridge.Camera.StopCameraLoop();
            }
            ClientGameplayBridge.PlayerMovement.StopMovementLoop();
            disconnectedPlayers.Clear();
            playerDisplayNameCache.Clear();
            playerUserIdCache.Clear();
            return;
        }

        if (reason == NetDisconnectReason.Requested || reason == NetDisconnectReason.ByRemote)
        {
            if (ClientGameplayBridge.Camera.HasInstance())
            {
                ClientGameplayBridge.Camera.StopCameraLoop();
            }
            ClientGameplayBridge.PlayerMovement.StopMovementLoop();
            disconnectedPlayers.Clear();
            return;
        }

        ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_wait_you_false"));
        if (ClientGameplayBridge.Camera.HasInstance())
        {
            ClientGameplayBridge.Camera.StopCameraLoop();
        }
        ClientGameplayBridge.PlayerMovement.StopMovementLoop();
        disconnectedPlayers.Clear();
        playerDisplayNameCache.Clear();
        playerUserIdCache.Clear();
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("Host migration initiated");
        //if (ClientGameplayBridge.Camera.HasInstance())
        //{
        //    ClientGameplayBridge.Camera.StopCameraLoop();
        //}
        //if (MovePlayerOnlineHandler.Instance != null)
        //{
        //    MovePlayerOnlineHandler.Instance.StopMovementLoop();
        //}
        //// Forward the migration event to the game manager so it can recreate the runner
        //if (GameManagerNetWork.Instance != null)
        //{
        //    GameManagerNetWork.Instance.OnHostMigration(runner, hostMigrationToken);
        //}
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (PaperLegendFlickInputCollector.TryGetInput(out var paperLegendInput))
        {
            if (paperLegendInput.FlickRequested)
            {
                Debug.Log($"[PaperLegends][Input] Sending flick input to Fusion: seq={paperLegendInput.FlickSequence}, force={paperLegendInput.Force01:0.00}, contact={paperLegendInput.ContactWorldPosition}, direction={paperLegendInput.AimWorldDirection}.");
            }
            else if (paperLegendInput.SkillRequested)
            {
                Debug.Log($"[PaperLegends][Input] Sending skill use input to Fusion: slot={paperLegendInput.SkillSlot}.");
            }
            else if (paperLegendInput.SkillUpgradeRequested)
            {
                Debug.Log($"[PaperLegends][Input] Sending skill upgrade input to Fusion: slot={paperLegendInput.SkillUpgradeSlot}.");
            }

            input.Set(paperLegendInput);
            return;
        }

        if (ClientGameplayBridge.PlayerMovement.TryGetInput(out var movementInput))
        {
            input.Set(movementInput);
        }
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        Debug.LogWarning($"Input missing for player {player}");
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        Debug.Log($"Object {obj?.name} entered AOI of player {player}");
        TryCachePlayerDisplayName(runner, player);
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        Debug.Log($"Object {obj?.name} exited AOI of player {player}");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player joined: {player}");
        TryCachePlayerDisplayName(runner, player);
        if (disconnectedPlayers.Remove(player))
        {
            NotifyPlayerReconnectedInChat(runner, player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        //if (GameSessionNetWork_Host.Instance.isGameOver)
        //    return;
        Debug.Log($"Người chơi {player} đã thoát khỏi phòng");

        TryCachePlayerDisplayName(runner, player);
        NotifyPlayerLeftInChat(runner, player);
        disconnectedPlayers.Add(player);
        var srv = NetworkObjectManager.Instance;
        if (srv == null)
        {
            //cảnh bảo cho bạn khi bạn mị mất mạng
            ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_wait_you_false"));
            return;
        }

        if (runner != null && player == runner.LocalPlayer)
        {
        }


        //trường hợp đang loading mà có người chơi mất kết nối
        //if (srv.StatusLoading == StatusLoadingGame.LoadMenu
        //    || srv.StatusLoading == StatusLoadingGame.LoadMapGame
        //    )
        //{
        //    //GameSessionClientLocal.Instance?.StopPlayerTurn();
        //    ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_player_left_loading"));
        //    return;
        //}
        //Nếu chỉ còn mình bạn chơi còn lại đã thoát hết rồi
        //if (runner.ActivePlayers.Count() <= 1)
        //{
        //    //GameSessionClientLocal.Instance?.StopPlayerTurn();
        //    ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_all_players_left"));
        //    return;
        //}
        // Trường hợp host rời phòng, bắt đầu cơ chế host migration
        //if (srv.Object != null && srv.Object.InputAuthority == player)
        //{
        //    Debug.Log("[HOST MIGRATION] Host left, starting migration coroutine");
        //    //UIControllerOnline.Instance.StartCoroutine(UIControllerOnline.Instance.CheckServerConnection());
        //}
    }

    private void NotifyPlayerLeftInChat(NetworkRunner runner, PlayerRef player)
    {
        var chatController = ChatController.Instance;
        if (chatController == null)
        {
            return;
        }

        string playerName = GetPlayerDisplayName(runner, player);
        string message = $"{playerName} đã thoát";
        chatController.ShowSystemMessage(message);
    }

    private void NotifyPlayerReconnectedInChat(NetworkRunner runner, PlayerRef player)
    {
        var chatController = ChatController.Instance;
        if (chatController == null)
        {
            return;
        }

        string playerName = GetPlayerDisplayName(runner, player);
        string message = $"{playerName} đã kết nối lại";
        chatController.ShowSystemMessage(message, "#2ECC71");
    }

    private string GetPlayerDisplayName(NetworkRunner runner, PlayerRef player)
    {
        if (TryResolvePlayerIdentity(runner, player, out int playerId, out string displayName))
        {
            if (playerId > 0)
            {
                playerUserIdCache[player] = playerId;
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                playerDisplayNameCache[player] = displayName;
                return displayName;
            }
        }

        if (playerDisplayNameCache.TryGetValue(player, out var cachedName) && !string.IsNullOrWhiteSpace(cachedName))
        {
            return cachedName;
        }

        if (playerUserIdCache.TryGetValue(player, out var cachedPlayerId) && cachedPlayerId > 0)
        {
            return $"Người chơi {cachedPlayerId}";
        }

        return $"Người chơi {player.PlayerId}";
    }

    private void TryCachePlayerDisplayName(NetworkRunner runner, PlayerRef player)
    {
        if (!TryResolvePlayerIdentity(runner, player, out int playerId, out string displayName))
        {
            return;
        }

        if (playerId > 0)
        {
            playerUserIdCache[player] = playerId;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            playerDisplayNameCache[player] = displayName;
        }
    }

    private bool TryResolvePlayerIdentity(NetworkRunner runner, PlayerRef player, out int playerId, out string displayName)
    {
        playerId = 0;
        displayName = string.Empty;

        if (runner != null && runner.TryGetPlayerObject(player, out var playerObject) && playerObject != null)
        {
            if (TryResolveIdentityFromPlayerObject(playerObject, out playerId, out displayName))
            {
                return true;
            }
        }

        var manager = NetworkObjectManager.Instance;
        if (manager != null)
        {
            foreach (var info in manager.GetOrderedPlayerInfos())
            {
                var candidateObject = manager.GetPlayerObject(info.playerId);
                if (candidateObject != null && (candidateObject.InputAuthority == player || candidateObject.StateAuthority == player))
                {
                    playerId = info.playerId;
                    displayName = GetDisplayName(info.fullname.ToString());
                    return true;
                }
            }

            if (playerUserIdCache.TryGetValue(player, out int cachedPlayerId) && cachedPlayerId > 0)
            {
                foreach (var info in manager.GetOrderedPlayerInfos())
                {
                    if (info.playerId != cachedPlayerId)
                    {
                        continue;
                    }

                    playerId = info.playerId;
                    displayName = GetDisplayName(info.fullname.ToString());
                    return true;
                }

                playerId = cachedPlayerId;
            }
        }

        var loginUser = GameManagerNetWork.Instance?.loginUserModel;
        if (runner != null && player == runner.LocalPlayer && loginUser != null)
        {
            playerId = loginUser.UserId;
            displayName = GetDisplayName(loginUser.Username);
            return playerId > 0 || !string.IsNullOrWhiteSpace(displayName);
        }

        return playerId > 0 || !string.IsNullOrWhiteSpace(displayName);
    }

    private static bool TryResolveIdentityFromPlayerObject(NetworkObject playerObject, out int playerId, out string displayName)
    {
        playerId = 0;
        displayName = string.Empty;

        if (playerObject == null)
        {
            return false;
        }

        var handler = playerObject.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
        {
            return false;
        }

        playerId = handler.PlayerModel.playerId;
        displayName = GetDisplayName(handler.PlayerModel.fullname.ToString());
        return playerId > 0 || !string.IsNullOrWhiteSpace(displayName);
    }

    private static string GetDisplayName(string rawName)
    {
        return string.IsNullOrWhiteSpace(rawName) ? string.Empty : rawName.Trim();
    }


    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        Debug.Log($"Reliable data progress from {player}: {progress * 100f}%");
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        Debug.Log($"Reliable data received from {player}: {data.Count} bytes");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("Scene load done");
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log("Scene load started");
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"Session list updated: {sessionList.Count} sessions");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.LogWarning($"⚠️ Client bị shutdown. Lý do: {shutdownReason}");
       // GameSessionClientLocal.Instance?.StopPlayerTurn();
        if (ClientGameplayBridge.Camera.HasInstance())
        {
            ClientGameplayBridge.Camera.StopCameraLoop();
        }
        ClientGameplayBridge.PlayerMovement.StopMovementLoop();
        disconnectedPlayers.Clear();

        if (ShouldSuppressExpectedDisconnectPopup())
        {
            Debug.Log("Shutdown by expected game flow; skipping shutdown popup.");
            playerDisplayNameCache.Clear();
            playerUserIdCache.Clear();
            return;
        }

        // Skip popup if GameManagerNetWork will handle reconnect
        if (GameManagerNetWork.Instance != null && (GameManagerNetWork.Instance.IsReconnecting || GameManagerNetWork.Instance.WillAttemptReconnect))
        {
            return;
        }

        // Xử lý tùy lý do mất kết nối
        switch (shutdownReason)
        {
            case ShutdownReason.DisconnectedByPluginLogic:
            case ShutdownReason.GameClosed:
                // Server chủ động đóng phòng — GameManagerNetWork xử lý fallback
                break;

            case ShutdownReason.ConnectionTimeout:
                ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_shutdown_timeout"));
                break;

            case ShutdownReason.GameNotFound:
                ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_shutdown_game_not_found"));
                break;

            case ShutdownReason.Ok:
                // Do host shutdown bình thường
                ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_shutdown_host_closed"));
                break;

            default:
                ClientGameplayBridge.Popup.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_shutdown_unknown"));
                break;
        }

    }

    private static bool ShouldSuppressExpectedDisconnectPopup()
    {
        var networkManager = GameManagerNetWork.Instance;
        if (networkManager != null && networkManager.ShouldSuppressExpectedDisconnectPopup())
        {
            return true;
        }

        if (GameOverManager.Instance != null && GameOverManager.Instance.HasGameOverResults)
        {
            return true;
        }

        try
        {
            return NetworkObjectManager.Instance != null && NetworkObjectManager.Instance.IsGameEnded;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Disconnect] Không đọc được trạng thái EndGame: {ex.Message}");
            return false;
        }
    }


    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
        Debug.Log("Received user simulation message");
    }
}
