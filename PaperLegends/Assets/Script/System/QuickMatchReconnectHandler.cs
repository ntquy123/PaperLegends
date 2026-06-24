using System;
using System.Collections;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;

public class QuickMatchReconnectHandler
{
    private const float StartGameTimeoutSeconds = 10f;
    private const float QuickMatchServerSearchTimeoutSeconds = 25f;
    private const float NetworkObjectManagerTimeoutSeconds = 60f;
    private const int MaxReconnectAttempts = 5;
    private const float RetryDelaySeconds = 2f;
    private const float InternetPollIntervalSeconds = 0.5f;
    private const float InternetStabilizationDelaySeconds = 1f;

    private readonly GameManagerNetWork networkManager;

    public QuickMatchReconnectHandler(GameManagerNetWork networkManager)
    {
        this.networkManager = networkManager;
    }

    public IEnumerator ReconnectQuickMatch(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            Debug.LogWarning("⚠️ ReconnectQuickMatch called with empty session name.");
            yield break;
        }

        networkManager.currentQuickMatchId = sessionName;

        bool shouldShowReconnectLoading = networkManager.IsReconnecting || !networkManager.IsRunnerActive;
        if (shouldShowReconnectLoading)
        {
            QuickMatchClient.Instance?.BeginReconnectLoadingSequence();
        }

        // Wait for internet to be available before attempting reconnect
        yield return WaitForInternetConnectivity();
        yield return networkManager.UnloadMenuSceneIfLoaded();

        bool connected = false;

        for (int attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
        {
            Debug.Log($"🔄 Reconnect attempt {attempt}/{MaxReconnectAttempts} for session {sessionName}");

            // Fusion runner đã từng StartGame thì không được tái sử dụng.
            // Khi reconnect sau rớt mạng, luôn tạo runner mới sạch trước khi StartGame.
            yield return networkManager.RecreateRunnerForReconnect();

            var runner = networkManager.runner;
            if (runner == null)
            {
                Debug.LogError($"⛔ Không thể khởi tạo runner mới (attempt {attempt}/{MaxReconnectAttempts}).");
                if (attempt < MaxReconnectAttempts)
                {
                    yield return new WaitForSecondsRealtime(RetryDelaySeconds);
                    yield return WaitForInternetConnectivity();
                }
                continue;
            }

            var sceneManager = networkManager.GetOrAddSceneManager();
            if (sceneManager == null)
            {
                if (attempt < MaxReconnectAttempts)
                {
                    yield return new WaitForSecondsRealtime(RetryDelaySeconds);
                }
                continue;
            }

            var customSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
            customSettings.FixedRegion = "asia";
            customSettings.AppVersion = PhotonAppSettings.Global.AppSettings.AppVersion;

            Debug.Log($"Reconnect: {sessionName} (attempt {attempt})");

            var startArgs = new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = sessionName,
                MatchmakingMode = MatchmakingMode.FillRoom,
                SceneManager = sceneManager,
                CustomPhotonAppSettings = customSettings,
                EnableClientSessionCreation = false
            };

            var startTask = runner.StartGame(startArgs);
            float elapsed = 0f;
            while (!startTask.IsCompleted && elapsed < StartGameTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!startTask.IsCompleted)
            {
                Debug.LogWarning($"⏱ Timeout khi kết nối StartGame (attempt {attempt}/{MaxReconnectAttempts}).");
                if (attempt < MaxReconnectAttempts)
                {
                    yield return new WaitForSecondsRealtime(RetryDelaySeconds);
                    yield return WaitForInternetConnectivity();
                }
                continue;
            }

            var result = startTask.Result;

            if (!result.Ok || !runner.IsRunning)
            {
                Debug.LogError($"⛔ StartGame thất bại (attempt {attempt}/{MaxReconnectAttempts})! Lý do: {result.ShutdownReason}");
                if (attempt < MaxReconnectAttempts)
                {
                    yield return new WaitForSecondsRealtime(RetryDelaySeconds);
                    yield return WaitForInternetConnectivity();
                }
                continue;
            }

            connected = true;
            Debug.Log($"✅ Reconnected quick match {sessionName} (attempt {attempt})");
            break;
        }

        if (!connected)
        {
            Debug.LogWarning($"❌ All {MaxReconnectAttempts} reconnect attempts failed for session {sessionName}");
            yield break;
        }

        var activeRunner = networkManager.runner;
        QuickMatchClient.Instance?.EnsureKeepNetworkForReconnect();

        QuickMatchServer quickMatchServer = null;
        yield return WaitForQuickMatchServer(activeRunner, server => quickMatchServer = server);

        if (quickMatchServer == null)
        {
            Debug.LogWarning("[QuickMatch] Không tìm thấy QuickMatchServer khi reconnect.");
            yield break;
        }

        int loginUserId = networkManager.loginUserModel?.UserId ?? 0;
        if (loginUserId <= 0)
        {
            Debug.LogWarning("[QuickMatch] Không có UserId hợp lệ để thông báo reconnect.");
        }
        else
        {
            quickMatchServer.NotifyClientEnteredGame(loginUserId);
            Debug.Log("[QuickMatch] Đã thông báo reconnect tới QuickMatchServer.");
        }

        bool rpcReady = false;
        yield return networkManager.LoadServerRPC(activeRunner, rpc =>
        {
            rpcReady = rpc != null;
            if (rpc != null)
            {
                QuickMatchClient.Instance?.SyncAvatarGuidForReconnect(rpc);
            }
        }, NetworkObjectManagerTimeoutSeconds);

        if (rpcReady)
        {
            networkManager.EnsureReconnectSceneActive();
        }
        else
        {
            Debug.LogWarning("⏰ Timeout khi chờ server RPC sau khi reconnect quick match.");
        }
    }

    private IEnumerator WaitForInternetConnectivity()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
            yield break;

        Debug.Log("⏳ Waiting for internet connectivity...");
        while (Application.internetReachability == NetworkReachability.NotReachable)
        {
            yield return new WaitForSecondsRealtime(InternetPollIntervalSeconds);
        }

        // Brief stabilization delay for DNS/routing to recover
        yield return new WaitForSecondsRealtime(InternetStabilizationDelaySeconds);
        Debug.Log("✅ Internet connectivity restored.");
    }

    private IEnumerator WaitForQuickMatchServer(NetworkRunner runner, Action<QuickMatchServer> onResolved)
    {
        float elapsed = 0f;
        QuickMatchServer resolved = null;

        while (elapsed < QuickMatchServerSearchTimeoutSeconds && resolved == null)
        {
            var candidates = UnityEngine.Object.FindObjectsOfType<QuickMatchServer>();
            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate.Runner == null || candidate.Object == null || !candidate.Object.IsValid)
                {
                    continue;
                }

                if (candidate.Runner == runner)
                {
                    resolved = candidate;
                    break;
                }
            }

            if (resolved == null)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        onResolved?.Invoke(resolved);
    }
}
