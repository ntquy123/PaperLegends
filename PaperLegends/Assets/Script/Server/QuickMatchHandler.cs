// Script: QuickMatchHandler.cs
/*
using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using Fusion.Photon.Realtime;
 
using System.Linq;
using System.Threading.Tasks;
using System;
using Unity.VisualScripting;
using UnityEngine.SceneManagement;
using UnityEditor;
//using GooglePlayGames.BasicApi;
using UnityEngine.UI;
using TMPro;
using System.Security.Cryptography;

public class QuickMatchHandler : MonoBehaviour
{
    [Header("SYSTEM CONFIG")]
    public static QuickMatchHandler Instance;
   
    public Coroutine currentQuickMatch;
    public bool SuppressNetworkPopup = false;
    private string matchedGameId;
    List<int> listUserId = new List<int>();
    public int MaxPlayer = 2;
    int betCountValue = 20;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    public void StartQuickMatch()
    {
        currentQuickMatch = StartCoroutine(QuickMatchCoroutineWrapper());
    }

    private IEnumerator QuickMatchCoroutineWrapper()
    {
        try
        {
            yield return QuickMatchCoroutine();
        }
        finally
        {
            currentQuickMatch = null;
        }
    }



    public void CancelQuickMatch()
    {
        if (currentQuickMatch != null)
        {
            StopCoroutine(currentQuickMatch);
            currentQuickMatch = null;
        }
        if (GameManagerNetWork.Instance != null)
        {
            GameManagerNetWork.Instance.CloseConnectToRunner();
        }
        TryNotifyQueueStatus(QuickMatchServer.QuickMatchPlayerStatusCodes.Cancelled);
        SuppressNetworkPopup = true;
    }

    private IEnumerator QuickMatchCoroutine()
    {
        /*
         * Quy trình tổng quát của QuickMatchCoroutine
         * 1. Chuẩn bị thông số photon (region, version) và mở kết nối runner.
         * 2. Khởi tạo phòng ShareMode (cho phép client tạo phòng nếu chưa có) và chờ StartGame hoàn tất.
         * 3. Khi vào được phòng: lưu lại sessionId, spawn NetworkObjectManager cho host, gán serverRPC cho client.
         * 4. Nếu là host: cấu hình dữ liệu phòng (map, loại trận, cược, số round) rồi đợi client ready.
         * 5. Nếu là client: đợi runner sẵn sàng và báo serverRPC rằng client đã sẵn sàng bằng RpcReady_CLIENT.
         * 6. Hai nhánh cùng kiểm tra timeout/ lỗi mạng: nếu quá thời gian sẽ quay về màn hình chính và hủy tìm trận.
         *
         * Các hàm con được sử dụng:
         * - WaitForServerRPCAndStart: đợi NetworkObjectManager (serverRPC) spawn thành công trước khi thao tác tiếp.
         * - WaitForClientReady: host theo dõi trạng thái StatusLoadingGame của serverRPC để biết client đã vào màn chơi hay chưa.
         */

      /*  const float waitTime = 30f;
        float startTime = Time.time;
        Debug.Log("🔍 Bắt đầu tìm trận...");
        SuppressNetworkPopup= false;

        // 1. Clone settings photon theo cấu hình toàn cục và cố định region để tối ưu ping
        var customSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
        customSettings.FixedRegion = "asia";
        customSettings.AppVersion = PhotonAppSettings.Global.AppSettings.AppVersion;
        Debug.Log($"🌍 Sử dụng region: {customSettings.FixedRegion}");
        int myCharId = GameManagerNetWork.Instance.loginUserModel.UserId;

        // 2. Mở kết nối tới Photon rồi lấy runner hiện tại để khởi động phòng
        GameManagerNetWork.Instance.OpenConnectToPhotonServer();
        var runner = GameManagerNetWork.Instance.runner;

        string sessionName = "Room_" + myCharId;
        var args = new StartGameArgs
        {
            // SessionName = sessionName, // ✅ cần nhớ tên này
            GameMode = GameMode.Shared,
            MatchmakingMode = MatchmakingMode.FillRoom,
            EnableClientSessionCreation = true,
            // SessionName = string.empty,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            PlayerCount = MaxPlayer,
            CustomPhotonAppSettings = customSettings
            //SessionProperties = new Dictionary<string, SessionProperty>
            //{
            //    { "level", (SessionProperty)myLevel }
            //}
        };

        // 3. Bắt runner.StartGame và chờ task hoàn tất
        var startTask = runner.StartGame(args);
        while (!startTask.IsCompleted)
            yield return null;

        if (!startTask.Result.Ok)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_network_false"), false);
            Debug.LogWarning($"❌ Không thể khởi tạo Quick Match: {startTask.Result.ShutdownReason} - {startTask.Result.ErrorMessage}");
            //LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            // Ngắt kết nối khỏi phòng online
            if(GameManagerNetWork.Instance != null)
            {
                GameManagerNetWork.Instance.CloseConnectToRunner();
                SuppressNetworkPopup = true;
            }
            MenuController.Instance.CancelQuickMatch();
            CancelQuickMatch();
            yield break;
        }
        Debug.Log($"🚀 StartGame thành công cho session: {runner.SessionInfo?.Name ?? sessionName} tại region: {runner.SessionInfo?.Region ?? customSettings.FixedRegion}");
        // Nếu vẫn chưa đủ người, tiếp tục chờ trong khoảng thời gian giới hạn
        if (runner.SessionInfo.PlayerCount < 2)
        {
            float waitStart = Time.time;
            while (runner.SessionInfo.PlayerCount < 2 && Time.time - waitStart < waitTime)
            {
                yield return null;
            }
            if (runner.SessionInfo.PlayerCount < 2)
            {
                Debug.Log("⌛ Không tìm thấy đối thủ, chơi với AI");
                MenuController.Instance.CancelQuickMatch();
                CancelQuickMatch();
                LoadingGameOfflineController.Instance.StartMatch();
                yield break;
            }
        }
        // Sau khi đủ người, kiểm tra host logic
        bool isHostLogic = runner.IsSharedModeMasterClient;
        if (isHostLogic)
        {
            Debug.Log("✅ Mình là host logic");
        }
        else
        {
            Debug.Log("🔁 Mình là client thường");
        }
        matchedGameId = runner.SessionInfo.Name;
        GameManagerNetWork.Instance.currentQuickMatchId = matchedGameId;
        Debug.Log($"✅ Vào phòng: {matchedGameId}");
        Debug.Log($"✅ Kết nối region: {runner.SessionInfo.Region}");
        //   Debug.Log($"👥 MaxPlayers: {runner.SessionInfo.MaxPlayers}, Current: {runner.SessionInfo.PlayerCount}");

        yield return NotifyQuickMatchServerQueueStatus();

        // 4. Nhánh host: spawn NetworkObjectManager và cấu hình thông tin phòng
        if (isHostLogic)
        {
            runner.Spawn(GameManagerNetWork.Instance.networkManagerPrefab, Vector3.zero, Quaternion.identity, runner.LocalPlayer);
            bool RpcIsReady = false;
            yield return WaitForServerRPCAndStart(result => RpcIsReady = result);
            if (RpcIsReady)
            {
                var mapId = GameMapHelper.GetRandomMapId();
                TimeOfDay time = (TimeOfDay)UnityEngine.Random.Range(0, 3);
                //WeatherType weather = (WeatherType)UnityEngine.Random.Range(0, 2);
                WeatherType weather = WeatherType.Sunny;
                int MaxRound = betCountValue <= 6 ? 5 : 10;
                GameManagerNetWork.Instance.serverRPC.rpgRoomModel = new RpgRoomModel
                {
                    gameScene = GameMapHelper.ToSceneName(mapId),
                    TypeMatch = TypeMatchGid.MatchRandomNormal,
                    roomId = 0,
                    betCount = 14,
                    MaxPlayer = MaxPlayer,
                    MaxRound = MaxRound,
                    timeOfDay = time,
                    weatherType = weather
                };
                //step đăng ký playerRef
                //GameManagerNetWork.Instance.serverRPC.RpcSetPlayerRef(myCharId);
                //yield return new WaitUntil(() =>
                //    GameManagerNetWork.Instance.serverRPC.GetPlayerRefById(myCharId) != default);
            }
            else
            {
                Debug.LogWarning("⛔ Kết nối mạng bị lỗi trở về màn hình chính");
                //LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
                PopupHelper.Instance.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_match_network_error"));
                MenuController.Instance.CancelQuickMatch();
                CancelQuickMatch();
                yield break;
            }
            Debug.Log("⌛ Đang Đợi tín hiệu từ client .....");
        }

        // 5. Nhánh client: chờ serverRPC spawn rồi báo về server rằng mình đã sẵn sàng
        if (!isHostLogic)
        {
            bool RpcIsReady = false;
            yield return WaitForServerRPCAndStart(result => RpcIsReady = result);
            if (RpcIsReady)
            {
                if (runner.SessionInfo.IsValid && runner.SessionInfo.PlayerCount == GameManagerNetWork.Instance.serverRPC.rpgRoomModel.MaxPlayer)
                {
                    Debug.Log("Đã đủ người vào phòng chuẩn bị vào game ...");
                    yield return new WaitUntil(() => runner != null && runner.IsRunning);
                    yield return null;
                    GameManagerNetWork.Instance.serverRPC.RpcReady_CLIENT();
                    yield break;
                }
            }
            else
            {
                Debug.LogWarning("⛔ Client không sẵn sàng trở về màn hình chính");
                PopupHelper.Instance.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_client_not_ready"));
                MenuController.Instance.CancelQuickMatch();
                CancelQuickMatch();
                yield break;
            }
        }
        else
        {
            bool clientIsReady = false;
            yield return WaitForClientReady(result => clientIsReady = result);
            if (!clientIsReady)
            {
                Debug.LogWarning("⛔ Client không sẵn sàng trở về màn hình chính");
                PopupHelper.Instance.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_client_not_ready"));
                MenuController.Instance.CancelQuickMatch();
                CancelQuickMatch();
                yield break;
            }
        }
    }
    private IEnumerator WaitForClientReady(System.Action<bool> onComplete)
    {
        // Host đợi client load xong: kiểm tra runner còn chạy và status của serverRPC.
        var networkManager = GameManagerNetWork.Instance;
        if (networkManager == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        var runner = networkManager.runner;
        if (runner == null || !runner.IsRunning)
        {
            onComplete?.Invoke(false);
            yield break;
        }
        float elapsed = 0f;
        const float timeout = 180f;

        while (true)
        {
            // 🔐 Bảo vệ null để tránh crash khi mất kết nối
            if (GameManagerNetWork.Instance == null || GameManagerNetWork.Instance.serverRPC == null)
            {
                string shutdownReason = "Mất kết nối đến Host hoặc lỗi mạng.";
                Debug.LogWarning("❌ Không thể truy cập serverRPC khi chờ client sẵn sàng.");
                PopupHelper.Instance.ShowPopupConfirm($"⚠️ Ngắt kết nối: {shutdownReason}");
                onComplete?.Invoke(false);
                yield break;
            }

            // ✅ Điều kiện hoàn tất: client đã sẵn sàng (StatusLoadingGame.isExam là cờ đã vào được màn thi đấu)
            if (GameManagerNetWork.Instance.serverRPC.Object != null && GameManagerNetWork.Instance.serverRPC.StatusLoading == StatusLoadingGame.isExam)
            {
                Debug.Log("🎉 Client đã sẵn sàng, bắt đầu trận đấu!");
                onComplete?.Invoke(true);
                yield break;
            }

            // ⏰ Hết thời gian chờ
            if (elapsed > timeout)
            {
                Debug.LogWarning("⏰ Hết thời gian chờ client sẵn sàng.");
                onComplete?.Invoke(false);
                PopupHelper.Instance.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_client_ready_timeout"));
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }


    private IEnumerator WaitForServerRPCAndStart(int myCharId)
    {
        float timeout = 5f;
        float elapsed = 0f;
        NetworkObjectManager rpc = null;

        while (elapsed < timeout)
        {
            var obj = GameObject.FindGameObjectWithTag("NetworkManager");
            if (obj != null)
            {
                rpc = obj.GetComponent<NetworkObjectManager>();
                if (rpc != null)
                    break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (rpc != null)
        {
            GameManagerNetWork.Instance.serverRPC = rpc;
           
        }
        else
        {
            Debug.LogError("❌ Không tìm thấy serverRPC từ Host sau timeout.");
        }
    } 
    private IEnumerator WaitForServerRPCAndStart(System.Action<bool> onComplete)
    {
        // Đợi object NetworkObjectManager (serverRPC) được spawn trước khi gửi RPC tới server/clients
        var networkManager = GameManagerNetWork.Instance;
        if (networkManager == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        var runner = networkManager.runner;
        if (runner == null || !runner.IsRunning)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        float timeout = 60f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            var rpc = networkManager.serverRPC;
            if (rpc != null && rpc.Object != null)
            {
                Debug.Log("✅ serverRPC đã được gán.");
                onComplete?.Invoke(true);
                yield break;
                //bool hasIds = false;
                //for (int i = 0; i < rpc.ringBallIds.Length; i++)
                //{
                //    if (rpc.ringBallIds.Get(i) != default)
                //    {
                //        hasIds = true;
                //        break;
                //    }
                //}

                //if (hasIds)
                //{
                //    Debug.Log("✅ serverRPC đã được gán và ringBallIds đã đồng bộ.");
                //    onComplete?.Invoke(true);
                //    yield break;
                //}
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("❌ Không tìm thấy serverRPC sau timeout.");
        PopupHelper.Instance.ShowPopupConfirm(LocalizationManager.Instance.GetText("noti_server_rpc_timeout"));
        onComplete?.Invoke(false);
    }



    public void OnChallengeReceived(int senderId, int bet)
    {
       PopupHelper.Instance.ShowIncomingChallengePopup(senderId, bet);
    }
    public void OnChallengeResponse(int senderId, int bet, bool accepted)
    {
        if (!accepted)
        {
            PopupHelper.Instance.ShowPopup("Đối thủ đã từ chối khiêu chiến", () =>
            {
            });
        }
        else
        {
            StartCoroutine(FriendMatchCoroutine(senderId, bet));
        }
    }
    public IEnumerator FriendMatchCoroutine(int opponentId, int bet)
    {
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        SuppressNetworkPopup = false;
        var customSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
        customSettings.FixedRegion = "asia";
        customSettings.AppVersion = PhotonAppSettings.Global.AppSettings.AppVersion;

        int myId = GameManagerNetWork.Instance.loginUserModel.UserId;
        int minId = Mathf.Min(myId, opponentId);
        int maxId = Mathf.Max(myId, opponentId);
        string sessionName = $"Duel_{minId}_{maxId}";

        GameManagerNetWork.Instance.OpenConnectToPhotonServer();
        var runner = GameManagerNetWork.Instance.runner;

        var args = new StartGameArgs
        {
            SessionName = sessionName,
            GameMode = GameMode.Shared,
            MatchmakingMode = MatchmakingMode.FillRoom,
            EnableClientSessionCreation = true,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            PlayerCount = 2,
            CustomPhotonAppSettings = customSettings
        };

        var startTask = runner.StartGame(args);
        while (!startTask.IsCompleted)
            yield return null;

        if (!startTask.Result.Ok)
        {
            NotificationHelper.Instance.ShowNotification(LocalizationManager.Instance.GetText("noti_network_false"), false);
            Debug.LogWarning($"❌ Không thể khởi tạo Friend Match: {startTask.Result.ErrorMessage}");
            LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
            // Ngắt kết nối khỏi phòng online
            if (GameManagerNetWork.Instance != null)
            {
                GameManagerNetWork.Instance.CloseConnectToRunner();
                SuppressNetworkPopup = true;
            }
            yield break;
        }

        yield return new WaitUntil(() => runner.SessionInfo.IsValid && runner.SessionInfo.PlayerCount >= 2);

        bool isHost = runner.IsSharedModeMasterClient;
        matchedGameId = runner.SessionInfo.Name;
        GameManagerNetWork.Instance.currentQuickMatchId = matchedGameId;

        if (isHost)
        {
            runner.Spawn(GameManagerNetWork.Instance.networkManagerPrefab, Vector3.zero, Quaternion.identity, runner.LocalPlayer);
            bool rpcReady = false;
            yield return WaitForServerRPCAndStart(result => rpcReady = result);
            if (rpcReady)
            {
                var mapId = GameMapHelper.GetRandomMapId();
                int maxRound = 5;
                if (bet == 3)
                    maxRound = 5;
                if (bet == 6)
                    maxRound = 7;
                if (bet == 12)
                    maxRound = 10;
                GameManagerNetWork.Instance.serverRPC.rpgRoomModel = new RpgRoomModel
                {
                    gameScene = GameMapHelper.ToSceneName(mapId),
                    TypeMatch = TypeMatchGid.MatchRandomNormal,
                    roomId = 0,
                    betCount = bet,
                    MaxPlayer = 2,
                    MaxRound = maxRound,
                    timeOfDay = TimeOfDay.Morning,
                    weatherType = WeatherType.Sunny
                };
            }
            else
            {
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
                yield break;
            }
        }
        else
        {
            bool rpcReady = false;
            yield return WaitForServerRPCAndStart(result => rpcReady = result);
            if (rpcReady)
            {
                yield return new WaitUntil(() => runner != null && runner.IsRunning);
                GameManagerNetWork.Instance.serverRPC.RpcReady_CLIENT();
            }
            else
            {
                LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
                yield break;
            }
        }

        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
    }

    private IEnumerator NotifyQuickMatchServerQueueStatus()
    {
        TryNotifyQueueStatus(QuickMatchServer.QuickMatchPlayerStatusCodes.Waiting);

        float elapsed = 0f;
        const float timeout = 5f;

        while (elapsed < timeout)
        {
            var runner = GameManagerNetWork.Instance?.runner;
            var quickMatchServer = QuickMatchServer.Instance;

            if (quickMatchServer != null && quickMatchServer.Runner == runner)
            {
                int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
                quickMatchServer.NotifyClientQueueStatus(userId, QuickMatchServer.QuickMatchPlayerStatusCodes.Waiting);
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("[QuickMatch] Unable to notify QuickMatchServer about queue status after joining room.");
    }

    private void TryNotifyQueueStatus(int status)
    {
        try
        {
            var quickMatchServer = QuickMatchServer.Instance;
            if (quickMatchServer == null)
            {
                return;
            }

            int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
            if (userId <= 0)
            {
                return;
            }

            quickMatchServer.NotifyClientQueueStatus(userId, status);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }



    public IEnumerator RejoinQuickMatchCoroutine()
    {
        if (string.IsNullOrEmpty(GameManagerNetWork.Instance.currentQuickMatchId))
            yield break;

        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(true);
        yield return GameManagerNetWork.Instance.StartCoroutine(
            GameManagerNetWork.Instance.ReconnectQuickMatch(GameManagerNetWork.Instance.currentQuickMatchId));

        bool rpcReady = false;
        yield return WaitForServerRPCAndStart(result => rpcReady = result);
        if (rpcReady)
        {
            bool clientReady = false;
            yield return WaitForClientReady(r => clientReady = r);
        }
        LoadingManager.Instance.UILoadingScreenPrefab.SetActive(false);
    }



  
}
*/
