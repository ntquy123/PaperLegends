/*
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using UnityEngine.SceneManagement;
using Fusion.Sockets;
using System.Linq;
using System;

public class LoadingManagerServer : MonoBehaviour, INetworkRunnerCallbacks
{
    public static LoadingManagerServer Instance;
    public Slider loadingSlider;
    public TextMeshProUGUI loadingText;
    public GameObject UILoading;

    private NetworkRunner runner;
    private HashSet<PlayerRef> playersLoaded = new HashSet<PlayerRef>();

    private void Awake()
    {
 
    }

    public void LoadScene(NetworkRunner runner, string sceneName)
    {
        if (runner == null)
        {
            Debug.LogError("❌ NetworkRunner chưa được gán!");
            return;
        }

        this.runner = runner;
        int sceneIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Map1.unity");
        SceneRef sceneRef = SceneRef.FromIndex(sceneIndex);
        var loadParams = new NetworkLoadSceneParameters
        {
           // LoadSceneMode = LoadSceneMode.Single
        };
        runner.SceneManager.LoadScene(sceneRef, loadParams);

    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log("⏳ Bắt đầu tải Scene...");
        UILoading.SetActive(true);
        loadingSlider.value = 0;
        loadingText.text = "Loading... 0%";
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"✅ Player {runner.LocalPlayer.PlayerId} đã tải xong scene!");

        playersLoaded.Add(runner.LocalPlayer);

        loadingSlider.value = 1f;
        loadingText.text = "Loading... 100%";

        if ( runner.IsSharedModeMasterClient)
        {
            if (playersLoaded.Count >= runner.ActivePlayers.Count())
            {
                Debug.Log("🎯 Tất cả player đã load xong! Bắt đầu game!");
                UILoading.SetActive(false);
                // TODO: Kích hoạt Gameplay tại đây
            }
            else
            {
                Debug.Log($"⌛ Đang chờ những player còn lại... ({playersLoaded.Count}/{runner.ActivePlayers.Count()})");
            }
        }
    }

    // =========================
    // Các hàm bắt buộc phải implement
    // =========================
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
 
    public void OnSceneLoadProgress(NetworkRunner runner, float progress) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("LoadingManager received host migration");
        // Reinitialize the loading runner using the token
       // StartCoroutine(HandleHostMigration(hostMigrationToken));
    }

    //private IEnumerator HandleHostMigration(HostMigrationToken token)
    //{
    //    var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
    //    var args = new StartGameArgs
    //    {
    //        HostMigrationToken = token,
    //        GameMode = token.GameMode,
    //        SceneManager = sceneManager
    //    };

    //    var start = runner.StartGame(args);
    //    while (!start.IsCompleted)
    //        yield return null;

    //    if (!start.Result.Ok)
    //    {
    //        Debug.LogError($"Failed to resume after migration: {start.Result.ShutdownReason}");
    //    }
    //    yield break;
    //}
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

}
*/
