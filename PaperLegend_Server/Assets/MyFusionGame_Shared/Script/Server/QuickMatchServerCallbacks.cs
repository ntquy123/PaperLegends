using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuickMatchServerCallbacks : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField]
    private NetworkObject? _quickMatchServerInstance;

    [SerializeField]
    private int _roomIndex = -1;

    [SerializeField]
    private string _roomName = string.Empty;

    private Transform? _roomRoot;
    private Scene _networkScene;

    public NetworkObject? QuickMatchServerInstance => _quickMatchServerInstance;
    public int RoomIndex => _roomIndex;
    public string RoomName => _roomName;
    private Transform RoomRoot => _roomRoot != null ? _roomRoot : transform;
    private bool HasNetworkScene => _networkScene.IsValid() && _networkScene.isLoaded;

    public void Initialise(int roomIndex, string roomName, NetworkObject? instance, Scene networkScene, Transform? roomRoot)
    {
        _roomIndex = roomIndex;
        _roomName = roomName ?? string.Empty;
        _quickMatchServerInstance = instance;
        _networkScene = networkScene;
        _roomRoot = roomRoot;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!HasNetworkScene)
        {
            Debug.LogError($"❌ Cannot assign quick match services for player {player} because the network scene for room '{_roomName}' is not loaded.");
            return;
        }

        Debug.Log($"{player} joined quick match room '{_roomName}'.");

      /*  if (!runner.TryGetPlayerObject(player, out var playerObject) || playerObject == null)
        {
            Debug.LogWarning($"Unable to resolve player object for {player} to assign quick match services.");
            return;
        }

        if (playerObject.gameObject.scene != _networkScene)
        {
            try
            {
                SceneManager.MoveGameObjectToScene(playerObject.gameObject, _networkScene);
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Unable to move player object for {player} into network scene '{_networkScene.name}': {ex.Message}");
            }
        }

        if (playerObject.TryGetComponent(out QuickMatchClient quickMatchClient))
        {
            quickMatchClient.AssignQuickMatchServer(_quickMatchServerInstance);
        }
        else
        {
            Debug.LogWarning("Player object is missing the QuickMatchClient component.");
        } */
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.TryGetPlayerObject(player, out _))
        {
            runner.SetPlayerObject(player, null);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        _quickMatchServerInstance = null;
        _networkScene = default;
        _roomRoot = null;
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected from server. Reason: {reason}.");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
    }

    //public void OnReflexStage2(NetworkRunner runner, Fusion.Sockets. callback)
    //{
    //}
}
