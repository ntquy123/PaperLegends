using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SocialPlatforms.Impl;


public class APIManager : MonoBehaviour
{
    public static APIManager Instance;
    private List<PlayerInfoStruct> players = new List<PlayerInfoStruct>();
    public string LastErrorMessage { get; private set; }
    public long LastErrorCode { get; private set; }

    /// <summary>
    /// Show an error message on screen and return <c>false</c>.
    /// </summary>
    /// <param name="msg">Message to display.</param>
    /// <param name="responseCode">Optional HTTP response code from the backend.</param>
    /// <param name="responseText">Optional response body for additional context.</param>
    private bool ShowError(string msg, long responseCode = 0, string responseText = null)
    {
        LastErrorCode = responseCode;
        string resolvedMessage = ExtractBackendMessage(responseText) ?? msg;
        if (LooksLikeTlsCertificateError(msg))
        {
            resolvedMessage += BuildPinnedCertificateHelpMessage();
        }

        LastErrorMessage = resolvedMessage;

        // Log detailed backend information if available
        if (responseCode != 0 || !string.IsNullOrEmpty(responseText))
        {
            //NotificationHelper.Instance.ShowNotification(responseText,false);
            Debug.LogWarning($"HTTP {responseCode}: {responseText}");
        }

        if (ClientGameplayBridge.UI.HasInstance())
        {
            ClientGameplayBridge.UI.ShowMessage(LastErrorMessage, 300f, 2f);
        }
        return false;
    }

    private string ExtractBackendMessage(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        try
        {
            var json = JObject.Parse(responseText);
            var message = json.Value<string>("message");
            return string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        }
        catch
        {
            return responseText.Trim();
        }
    }
    private static readonly HashSet<string> EmptyCertificatePins = new HashSet<string>();

    private static bool HasPinnedCertificates
    {
        get
        {
            var pins = ApiConfig.HttpsCertificatePins;
            return pins != null && pins.Any(pin => !string.IsNullOrWhiteSpace(pin));
        }
    }

    private static bool ShouldUsePinnedCertificatesForUrl(string url)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!ApiConfig.UsePinnedHttpsCertificates)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HasPinnedCertificates;
#else
        return false;
#endif
    }

    private static HashSet<string> GetNormalizedCertificatePins()
    {
        var pins = ApiConfig.HttpsCertificatePins;
        if (pins == null || pins.Count == 0)
        {
            return EmptyCertificatePins;
        }

        return new HashSet<string>(
            pins
                .Where(pin => !string.IsNullOrWhiteSpace(pin))
                .Select(NormalizeCertificatePin),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeCertificatePin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return string.Empty;
        }

        return pin
            .Trim()
            .Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToUpperInvariant();
    }

    private static string ComputeSha256Hex(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return string.Empty;
        }

        using (var sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("X2"));
            }

            return builder.ToString();
        }
    }

    private static string BuildPinnedCertificateHelpMessage()
    {
        if (ApiConfig.UsePinnedHttpsCertificates && HasPinnedCertificates)
        {
            return " Kiá»ƒm tra láº¡i fingerprint/public key pin trong ServerConfig náº¿u server vá»«a Ä‘á»•i chá»©ng chá»‰.";
        }

        if (ApiConfig.IsHttpsBaseUrl)
        {
            return " Náº¿u lá»—i chá»‰ xáº£y ra trÃªn má»™t sá»‘ mÃ¡y Android, hÃ£y cáº¥u hÃ¬nh pin SHA-256 cá»§a chá»©ng chá»‰/public key trong ServerConfig Ä‘á»ƒ bypass trust store lá»—i thá»i trÃªn thiáº¿t bá»‹.";
        }

        return string.Empty;
    }

    private static bool LooksLikeTlsCertificateError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return false;
        }

        string normalized = error.ToLowerInvariant();
        return normalized.Contains("certificate")
               || normalized.Contains("cert")
               || normalized.Contains("ssl")
               || normalized.Contains("tls")
               || normalized.Contains("secure channel")
               || normalized.Contains("handshake");
    }

    private UnityWebRequest ConfigureRequest(UnityWebRequest request, string url)
    {
        if (request == null)
        {
            return null;
        }

        if (ShouldUsePinnedCertificatesForUrl(url))
        {
            request.disposeCertificateHandlerOnDispose = true;
            request.certificateHandler = new AndroidPinnedCertificateHandler(GetNormalizedCertificatePins(), url);
        }

        return request;
    }

    private IEnumerator SendConfiguredWebRequest(UnityWebRequest request, string url)
    {
        ConfigureRequest(request, url);
        yield return request.SendWebRequest();
    }

    private sealed class AndroidPinnedCertificateHandler : CertificateHandler
    {
        private readonly HashSet<string> validPins;
        private readonly string targetUrl;

        public AndroidPinnedCertificateHandler(HashSet<string> validPins, string targetUrl)
        {
            this.validPins = validPins ?? EmptyCertificatePins;
            this.targetUrl = targetUrl;
        }

        protected override bool ValidateCertificate(byte[] certificateData)
        {
            if (certificateData == null || certificateData.Length == 0 || validPins.Count == 0)
            {
                Debug.LogWarning($"HTTPS pinning has no valid data for {targetUrl}.");
                return false;
            }

            try
            {
                using (var certificate = new X509Certificate2(certificateData))
                {
                    string certificateHash = ComputeSha256Hex(certificate.RawData);
                    string publicKeyHash = ComputeSha256Hex(certificate.GetPublicKey());

                    bool matched = validPins.Contains(certificateHash) || validPins.Contains(publicKeyHash);
                    if (!matched)
                    {
                        Debug.LogWarning($"HTTPS certificate pin mismatch for {targetUrl}. cert={certificateHash}, publicKey={publicKeyHash}");
                    }

                    return matched;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not validate HTTPS certificate for {targetUrl}: {ex.Message}");
                return false;
            }
        }
    }

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

    public Task<string> ExchangeUgsForFirebaseCustomTokenAsync(string ugsAccessToken)
    {
        if (string.IsNullOrEmpty(ugsAccessToken))
        {
            var failed = new TaskCompletionSource<string>();
            failed.SetException(new ArgumentException("UGS access token is required", nameof(ugsAccessToken)));
            return failed.Task;
        }

        var tcs = new TaskCompletionSource<string>();
        StartCoroutine(ExchangeUgsForFirebaseCustomTokenCoroutine(ugsAccessToken, tcs));
        return tcs.Task;
    }

    private IEnumerator ExchangeUgsForFirebaseCustomTokenCoroutine(string ugsAccessToken, TaskCompletionSource<string> tcs)
    {
        string url = ApiConfig.BaseUrl + "/auth/ugs-to-firebase";
        string json = $"{{\"ugsToken\":\"{ugsAccessToken}\"}}";
        byte[] body = Encoding.UTF8.GetBytes(json);

        var request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + request.error, request.responseCode, responseText);
            tcs.TrySetException(new Exception($"Request failed: {request.error}"));
            request.Dispose();
            yield break;
        }

        try
        {
            var responseJson = JObject.Parse(responseText ?? string.Empty);
            var customToken = responseJson.Value<string>("customToken");
            if (string.IsNullOrEmpty(customToken))
            {
                ShowError("âŒ customToken missing in response", request.responseCode, responseText);
                tcs.TrySetException(new Exception("customToken missing in response"));
            }
            else
            {
                tcs.TrySetResult(customToken);
            }
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message, request.responseCode, responseText);
            tcs.TrySetException(ex);
        }

        request.Dispose();
    }
    #region  [====== System ]
    public IEnumerator RunTask<T>(Task<T> task, Action<T> onComplete)
    {
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            ShowError("âŒ Task lá»—i: " + task.Exception);
            onComplete?.Invoke(default);
            yield break;
        }

        onComplete?.Invoke(task.Result);
    }
    #endregion

    #region [====== QuickMatch Queue ]
    public Task<QueueJoinResponse> JoinMatchQueueAsync(int userId, int bet, int typeMatchGid, string region = "asia", int maxPlayers = 0)
    {
        var tcs = new TaskCompletionSource<QueueJoinResponse>();
        StartCoroutine(JoinMatchQueueCoroutine(userId, bet, typeMatchGid, region, maxPlayers, response => tcs.TrySetResult(response)));
        return tcs.Task;
    }

    private IEnumerator JoinMatchQueueCoroutine(int userId, int bet, int typeMatchGid, string region, int maxPlayers, Action<QueueJoinResponse> onComplete)
    {
        var payload = new QueueJoinRequest
        {
            userId = userId,
            bet = bet,
            typeMatchGid = typeMatchGid,
            region = region,
            maxPlayers = maxPlayers
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        UnityWebRequest request = new UnityWebRequest(ApiConfig.BaseUrl + "/queue/join", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Join queue failed: " + request.error, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onComplete?.Invoke(null);
            yield break;
        }

        try
        {
            var response = JsonUtility.FromJson<QueueJoinResponse>(request.downloadHandler.text);
            onComplete?.Invoke(response);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing join queue response: " + ex.Message, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onComplete?.Invoke(null);
        }
    }

    public Task<QueueCancelResponse> CancelMatchQueueAsync(int userId)
    {
        var tcs = new TaskCompletionSource<QueueCancelResponse>();
        StartCoroutine(CancelMatchQueueCoroutine(userId, response => tcs.TrySetResult(response)));
        return tcs.Task;
    }

    public Task<bool> MarkMatchEarlyExitAsync(string matchId, int roomId, int playerId)
    {
        var tcs = new TaskCompletionSource<bool>();
        if (playerId <= 0)
        {
            tcs.TrySetResult(false);
            return tcs.Task;
        }

        StartCoroutine(MarkMatchEarlyExitCoroutine(matchId, roomId, playerId, success => tcs.TrySetResult(success)));
        return tcs.Task;
    }

    public Task<ForceStartResponse> ForceStartMatchAsync(int userId, int bet, int typeMatchGid, string region = "asia", int maxPlayers = 0)
    {
        var tcs = new TaskCompletionSource<ForceStartResponse>();
        StartCoroutine(ForceStartMatchCoroutine(userId, bet, typeMatchGid, region, maxPlayers, response => tcs.TrySetResult(response)));
        return tcs.Task;
    }

    public Task<QueueResyncResponse> ResyncMatchQueueAsync(int userId, string matchId = null)
    {
        var tcs = new TaskCompletionSource<QueueResyncResponse>();
        StartCoroutine(ResyncMatchQueueCoroutine(userId, matchId, response => tcs.TrySetResult(response)));
        return tcs.Task;
    }

    private IEnumerator ForceStartMatchCoroutine(int userId, int bet, int typeMatchGid, string region, int maxPlayers, Action<ForceStartResponse> onComplete)
    {
        var payload = new ForceStartRequest
        {
            userId = userId,
            bet = bet,
            typeMatchGid = typeMatchGid,
            region = region,
            maxPlayers = maxPlayers
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        UnityWebRequest request = new UnityWebRequest(ApiConfig.BaseUrl + "/queue/force-start", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Force-start failed: " + request.error, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onComplete?.Invoke(null);
            yield break;
        }

        try
        {
            var response = JsonUtility.FromJson<ForceStartResponse>(request.downloadHandler.text);
            onComplete?.Invoke(response);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing force-start response: " + ex.Message, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onComplete?.Invoke(null);
        }
    }

    private IEnumerator ResyncMatchQueueCoroutine(int userId, string matchId, Action<QueueResyncResponse> onComplete)
    {
        var payload = new QueueResyncRequest
        {
            userId = userId,
            matchId = string.IsNullOrWhiteSpace(matchId) ? null : matchId
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        UnityWebRequest request = new UnityWebRequest(ApiConfig.BaseUrl + "/queue/resync", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowError("Queue resync failed: " + request.error, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onComplete?.Invoke(null);
            yield break;
        }

        try
        {
            var response = JsonUtility.FromJson<QueueResyncResponse>(request.downloadHandler.text);
            onComplete?.Invoke(response);
        }
        catch (Exception ex)
        {
            ShowError("Error parsing queue resync response: " + ex.Message, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onComplete?.Invoke(null);
        }
    }

    private IEnumerator CancelMatchQueueCoroutine(int userId, Action<QueueCancelResponse> onComplete)
    {
        var payload = new QueueCancelRequest
        {
            userId = userId
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        UnityWebRequest request = new UnityWebRequest(ApiConfig.BaseUrl + "/queue/cancel", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Cancel queue failed: " + request.error, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onComplete?.Invoke(null);
            yield break;
        }

        try
        {
            var response = JsonUtility.FromJson<QueueCancelResponse>(request.downloadHandler.text);
            onComplete?.Invoke(response);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing cancel queue response: " + ex.Message, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onComplete?.Invoke(null);
        }
    }

    private IEnumerator MarkMatchEarlyExitCoroutine(string matchId, int roomId, int playerId, Action<bool> onComplete)
    {
        var payload = new MatchEarlyExitRequest
        {
            matchId = matchId,
            roomId = roomId,
            playerId = playerId,
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        UnityWebRequest request = new UnityWebRequest(ApiConfig.BaseUrl + "/match/early-exit", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        bool success = request.result == UnityWebRequest.Result.Success;
        if (!success)
        {
            success = ShowError("âŒ Mark match early-exit failed", request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }
    #endregion

    #region [====== RoomManager ]

    public Task<bool> JoinRoomAPI(int roomId, int userId)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(JoinRoomAPICoroutine(roomId, userId, success =>
        {
            tcs.SetResult(success);
        }));
        return tcs.Task;
    }

    private IEnumerator JoinRoomAPICoroutine(int roomId, int userId, Action<bool> onComplete)
    {
        RoomResponse roomRequest = new RoomResponse { roomId = roomId, userId = userId };
        string json = JsonUtility.ToJson(roomRequest);
        Debug.Log("Body: " + json);

        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(ApiConfig.BaseUrl + "/joinRoom", "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        // Gá»­i yÃªu cáº§u vÃ  Ä‘á»£i pháº£n há»“i
        yield return SendConfiguredWebRequest(req, req.url);
        bool success = false;

        // Kiá»ƒm tra lá»—i káº¿t ná»‘i
        if (req.result != UnityWebRequest.Result.Success)
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }
        else
        {
            // Náº¿u thÃ nh cÃ´ng, kiá»ƒm tra pháº£n há»“i tá»« server
            Debug.Log("Response received: " + req.downloadHandler.text);

            // Xá»­ lÃ½ pháº£n há»“i JSON (náº¿u cÃ³)
            try
            {
                var response = JsonUtility.FromJson<RoomResponse>(req.downloadHandler.text);
                Debug.Log("Room joined: " + response.roomId);
                success = true;
            }
            catch (System.Exception ex)
            {
                success = ShowError("âŒ Error parsing response: " + ex.Message);
            }
        }

        onComplete?.Invoke(success);
    }

    public Task<RoomResponse> CreateRoomAsync(int userId, int bet, int maxPlayer, int mapId, int maxRound, string sessionName = null)
    {
        var tcs = new TaskCompletionSource<RoomResponse>();
        StartCoroutine(CreateRoomCoroutine(userId, bet, maxPlayer, mapId, maxRound, sessionName, result => tcs.TrySetResult(result)));
        return tcs.Task;
    }

    public Task<List<RoomData>> GetRoomsAsync()
    {
        var tcs = new TaskCompletionSource<List<RoomData>>();
        StartCoroutine(GetRoomsCoroutine(tcs));
        return tcs.Task;
    }

    private IEnumerator GetRoomsCoroutine(TaskCompletionSource<List<RoomData>> tcs)
    {
        UnityWebRequest req = UnityWebRequest.Get(ApiConfig.BaseUrl + "/getroom");
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Load room failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            tcs.TrySetResult(null);
            yield break;
        }

        try
        {
            string json = "{\"rooms\":" + req.downloadHandler.text + "}";
            RoomListWrapper wrapper = JsonUtility.FromJson<RoomListWrapper>(json);
            tcs.TrySetResult(wrapper != null ? wrapper.rooms : null);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing rooms: " + ex.Message, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            tcs.TrySetResult(null);
        }
    }

    private IEnumerator CreateRoomCoroutine(int userId, int bet, int maxPlayer, int mapId, int maxRound, string sessionName, Action<RoomResponse> onComplete)
    {
        var createRoomRequest = new CreateRoomRequest
        {
            userId = userId,
            bet = bet,
            maxPlayer = maxPlayer,
            mapId = mapId,
            maxRound = maxRound,
            rounds = maxRound,
            sessionName = sessionName,
            roomName = sessionName
        };

        string json = JsonUtility.ToJson(createRoomRequest);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(ApiConfig.BaseUrl + "/createroom", "PUT");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onComplete?.Invoke(null);
            yield break;
        }

        try
        {
            var response = JsonUtility.FromJson<RoomResponse>(req.downloadHandler.text);
            onComplete?.Invoke(response);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onComplete?.Invoke(null);
        }
    }
 

    public Task<bool> LeaveRoomAsync(int roomId, int userId)
    {
        if (roomId <= 0 || userId <= 0)
        {
            var invalid = new TaskCompletionSource<bool>();
            invalid.SetResult(false);
            return invalid.Task;
        }

        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(LeaveRoomsBatchCoroutine(roomId, new List<int> { userId }, success => tcs.SetResult(success)));
        return tcs.Task;
    }

    public Task<bool> MarkRoomUserLeftAsync(int roomId, int userId)
    {
        if (roomId <= 0 || userId <= 0)
        {
            var invalid = new TaskCompletionSource<bool>();
            invalid.SetResult(false);
            return invalid.Task;
        }

        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(MarkRoomUserLeftCoroutine(roomId, userId, success => tcs.SetResult(success)));
        return tcs.Task;
    }

    public Task<bool> PostOverGame(List<OverGameRequest> data)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(PostOverGameCoroutine(data, success => { tcs.SetResult(success); }));
        return tcs.Task;
    }

    public Task<bool> DeductBetsOnGameStartAsync(List<BetDeductionEntry> entries)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(DeductBetsOnGameStartCoroutine(entries, success => tcs.SetResult(success)));
        return tcs.Task;
    }

    public Task<JoinRoomsBatchResult> JoinRoomsBatchAsync(List<int> userIds, string roomName, GameMapId mapId, int port = 0, string containerId = null, int typeMatchGid = 0, int bet = 0, int maxRound = 0)
    {
        var tcs = new TaskCompletionSource<JoinRoomsBatchResult>();
        StartCoroutine(JoinRoomsBatchCoroutine(userIds, roomName, mapId, port, containerId, typeMatchGid, bet, maxRound, result => tcs.SetResult(result)));
        return tcs.Task;
    }

    public Task<bool> LeaveRoomsBatchAsync(int roomId, List<int> userIds)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(LeaveRoomsBatchCoroutine(roomId, userIds, success => tcs.SetResult(success)));
        return tcs.Task;
    }

    private IEnumerator PostOverGameCoroutine(List<OverGameRequest> data, Action<bool> onComplete)
    {
        string json = JsonHelper.ToJson(data);
        Debug.Log("JSON to send: " + json);

        const int maxRetries = 3;
        const float baseDelay = 2f;
        string url = ApiConfig.BaseUrl + "/over-game";

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(json);
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(jsonToSend);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 15;

                yield return SendConfiguredWebRequest(req, req.url);

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Response received: " + req.downloadHandler.text + (attempt > 0 ? $" (retry {attempt})" : ""));
                    onComplete?.Invoke(true);
                    yield break;
                }

                bool isRetryable = req.result == UnityWebRequest.Result.ConnectionError
                                || req.responseCode >= 500
                                || req.responseCode == 0;

                Debug.LogWarning($"POST failed: {url} | {req.responseCode} | {req.error} | attempt {attempt}/{maxRetries}");

                if (!isRetryable || attempt >= maxRetries)
                {
                    ShowError("âŒ POST /over-game failed permanently: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
                    onComplete?.Invoke(false);
                    yield break;
                }
            }

            float delay = baseDelay * Mathf.Pow(2, attempt);
            Debug.Log($"Retrying POST {url} in {delay:F1}s (attempt {attempt + 1}/{maxRetries})...");
            yield return new WaitForSecondsRealtime(delay);
        }

        onComplete?.Invoke(false);
    }

    private IEnumerator MarkRoomUserLeftCoroutine(int roomId, int userId, Action<bool> onComplete)
    {
        var payload = new MarkRoomUserLeftRequest
        {
            roomId = roomId,
            userId = userId,
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(ApiConfig.BaseUrl + "/roomUserLeft", "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = req.result == UnityWebRequest.Result.Success;
        if (!success)
        {
            success = ShowError("âŒ KhÃ´ng thá»ƒ cáº­p nháº­t tráº¡ng thÃ¡i rá»i phÃ²ng", req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    private IEnumerator DeductBetsOnGameStartCoroutine(List<BetDeductionEntry> entries, Action<bool> onComplete)
    {
        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning("No valid bet-deduction data when starting the match.");
            onComplete?.Invoke(false);
            yield break;
        }

        string json = JsonHelper.ToJson(entries);
        byte[] jsonToSend = new UTF8Encoding().GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(ApiConfig.BaseUrl + "/game-start/bets", "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = req.result == UnityWebRequest.Result.Success;
        if (!success)
        {
            success = ShowError("âŒ KhÃ´ng thá»ƒ trá»« bi cÆ°á»£c khi báº¯t Ä‘áº§u tráº­n", req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    private IEnumerator JoinRoomsBatchCoroutine(List<int> userIds, string roomName, GameMapId mapId, int port, string containerId, int typeMatchGid, int bet, int maxRound, Action<JoinRoomsBatchResult> onComplete)
    {
        var result = new JoinRoomsBatchResult
        {
            Success = false,
            RoomId = 0,
            RoomName = roomName ?? string.Empty,
            Port = 0
        };

        if (userIds == null || userIds.Count == 0)
        {
            Debug.LogWarning("joinRoomsBatch was called with an empty list.");
            onComplete?.Invoke(result);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(roomName))
        {
            Debug.LogWarning("joinRoomsBatch was called without a valid roomName.");
            onComplete?.Invoke(result);
            yield break;
        }

        var payload = new JoinRoomsRequest
        {
            userIds = userIds,
            roomName = roomName,
            mapId = (int)mapId,
            port = port,
            containerId = string.IsNullOrWhiteSpace(containerId) ? null : containerId,
            typeMatchGid = typeMatchGid,
            bet = bet,
            maxRound = maxRound,
            rounds = maxRound
        };

        string json = JsonUtility.ToJson(payload);
        byte[] jsonToSend = new UTF8Encoding().GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(ApiConfig.BaseUrl + "/joinRooms", "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var response = JsonUtility.FromJson<JoinRoomsBatchResponse>(req.downloadHandler.text);
                if (response != null && response.room != null)
                {
                    result.RoomId = response.room.id;
                    result.RoomName = string.IsNullOrWhiteSpace(response.room.roomName) ? result.RoomName : response.room.roomName;
                    result.Port = response.room.port;
                    result.Success = true;
                }
                else
                {
                    ShowError("âŒ Response joinRooms khÃ´ng há»£p lá»‡", req.responseCode, req.downloadHandler.text);
                }
            }
            catch (Exception ex)
            {
                ShowError("âŒ Lá»—i parse response joinRooms: " + ex.Message, req.responseCode, req.downloadHandler.text);
            }
        }
        else
        {
            ShowError("âŒ KhÃ´ng thá»ƒ Ä‘á»“ng bá»™ joinRooms", req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator LeaveRoomsBatchCoroutine(int roomId, List<int> userIds, Action<bool> onComplete)
    {
        if (roomId <= 0 || userIds == null || userIds.Count == 0)
        {
            Debug.LogWarning("leaveRoomsBatch was called with invalid parameters.");
            onComplete?.Invoke(false);
            yield break;
        }

        var payload = new LeaveRoomsRequest
        {
            roomId = roomId,
            userIds = userIds,
        };

        string json = JsonUtility.ToJson(payload);
        byte[] jsonToSend = new UTF8Encoding().GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(ApiConfig.BaseUrl + "/leaveRooms", "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = req.result == UnityWebRequest.Result.Success;
        if (!success)
        {
            success = ShowError("âŒ KhÃ´ng thá»ƒ Ä‘á»“ng bá»™ leaveRooms", req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }


    [Serializable]
    private class JoinRoomsRequest
    {
        public string roomName;
        public List<int> userIds;
        public int mapId;
        public int port;
        public string containerId;
        public int typeMatchGid;
        public int maxPlayers;
        public int bet;
        public int maxRound;
        public int rounds;
    }

    [Serializable]
    private class LeaveRoomsRequest
    {
        public int roomId;
        public List<int> userIds;
    }

    [Serializable]
    private class MarkRoomUserLeftRequest
    {
        public int roomId;
        public int userId;
    }

    [Serializable]
    public class BetDeductionEntry
    {
        public int userId;
        public int playerId;
        public int ringBall;
        public int bet;
        public int marbBet;
        public int money;
        public string description;
        public string eventType;
    }

    [Serializable]
    public class JoinRoomsBatchResult
    {
        public bool Success;
        public int RoomId;
        public string RoomName;
        public int Port;
    }

    [Serializable]
    private class JoinRoomsBatchResponse
    {
        public JoinRoomsBatchRoom room;
    }

    [Serializable]
    private class JoinRoomsBatchRoom
    {
        public int id;
        public string roomName;
        public int port;
    }

    public Task<List<UserRoom>> GetUsersInRoomAsync(int roomId)
    {
        var tcs = new TaskCompletionSource<List<UserRoom>>();

        StartCoroutine(GetUsersInRoomCoroutine(roomId, (List<UserRoom> players) =>
        {
            tcs.SetResult(players); // Khi coroutine xong â†’ set káº¿t quáº£
        }));

        return tcs.Task;
    }

    public Task<PlayerRoomApiResponse> GetCurrentPlayerRoomAsync(int userId)
    {
        var tcs = new TaskCompletionSource<PlayerRoomApiResponse>();
        StartCoroutine(GetCurrentPlayerRoomCoroutine(userId, response => tcs.SetResult(response)));
        return tcs.Task;
    }


    IEnumerator GetUsersInRoomCoroutine(int roomId, Action<List<UserRoom>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/getUserRooms?roomId={roomId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Load user failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        string json = "{\"users\":" + req.downloadHandler.text + "}";
        UserRoomListWrapper wrapper = JsonUtility.FromJson<UserRoomListWrapper>(json);
        if (wrapper == null || wrapper.users == null)
        {
            ShowError("âŒ Users data is null!");
            onSuccess?.Invoke(null);
            yield break;
        }

    //    players = wrapper.users.Select(x => new PlayerInfoStruct(
    //    tagPlyer: "DefaultTag",  // ThÃªm tham sá»‘ tagPlyer, cÃ³ thá»ƒ láº¥y tá»« dá»¯ liá»‡u tráº£ vá» hoáº·c Ä‘á»ƒ máº·c Ä‘á»‹nh
    //    fullname: x.player.PlayerName,  // GÃ¡n tÃªn ngÆ°á»i chÆ¡i
    //    powerForce: 0,  // GiÃ¡ trá»‹ máº·c Ä‘á»‹nh, cÃ³ thá»ƒ tá»± Ä‘iá»n sau
    //    exactRatio: 0,  // GiÃ¡ trá»‹ máº·c Ä‘á»‹nh, cÃ³ thá»ƒ tá»± Ä‘iá»n sau
    //    avatar: null,   // GÃ¡n null, báº¡n sáº½ gÃ¡n Sprite avatar sau
    //    isAI: false,    // Máº·c Ä‘á»‹nh lÃ  ngÆ°á»i chÆ¡i tháº­t
    //    ball: null,     // GÃ¡n null, gÃ¡n Rigidbody ball sau
    //    animator: null, // GÃ¡n null, gÃ¡n Animator sau
    //    playerbody: null,  // GÃ¡n null, gÃ¡n GameObject playerbody sau
    //    score: 0,       // GÃ¡n máº·c Ä‘á»‹nh, báº¡n cÃ³ thá»ƒ tá»± tÃ­nh Ä‘iá»ƒm sau
    //    isDestroy: false,  // Máº·c Ä‘á»‹nh lÃ  khÃ´ng bá»‹ há»§y
    //    distance: 0f,   // GÃ¡n máº·c Ä‘á»‹nh, cÃ³ thá»ƒ tá»± tÃ­nh khoáº£ng cÃ¡ch sau
    //    statusPlayer: StatusPlayer.Normal,  // Máº·c Ä‘á»‹nh lÃ  Active
    //    isHolding: false,  // Máº·c Ä‘á»‹nh lÃ  khÃ´ng cáº§m
    //    positionShowMess: null  // GÃ¡n null, gÃ¡n TextMeshProUGUI sau
    //)).ToList();

        Debug.Log($"Room {roomId} has {wrapper.users.Count} user(s).");
        onSuccess?.Invoke(wrapper.users); // tráº£ danh sÃ¡ch user

    }

    private IEnumerator GetCurrentPlayerRoomCoroutine(int userId, Action<PlayerRoomApiResponse> onComplete)
    {
        if (userId <= 0)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        string url = $"{ApiConfig.BaseUrl}/playerRoom/{userId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        PlayerRoomApiResponse response = null;

        if (req.result == UnityWebRequest.Result.Success)
        {
            try
            {
                response = JsonUtility.FromJson<PlayerRoomApiResponse>(req.downloadHandler.text);
            }
            catch (Exception ex)
            {
                ShowError("âŒ Error parsing room response: " + ex.Message, req.responseCode, req.downloadHandler.text);
            }
        }
        else if (req.responseCode != 404)
        {
            ShowError("âŒ KhÃ´ng thá»ƒ láº¥y thÃ´ng tin phÃ²ng hiá»‡n táº¡i", req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(response);
    }

    #endregion

    #region [====== RewardsManager ]

    public Task<List<RewardItem>> GetRewardsTodayAsync()
    {
        var tcs = new TaskCompletionSource<List<RewardItem>>();

        StartCoroutine(FetchRewardsToday(rewards =>
        {
            tcs.SetResult(rewards);
        }));

        return tcs.Task;
    }

    IEnumerator FetchRewardsToday(Action<List<RewardItem>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/getAdsRewardsToday";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Load user failed: " + req.error);
            onSuccess?.Invoke(new List<RewardItem>());
            yield break;
        }

        List<RewardItem> rewards = ParseRewardItems(req.downloadHandler.text, "rewards");

        onSuccess?.Invoke(rewards ?? new List<RewardItem>());
    }

    public Task<List<RewardItem>> GetPlayerAchievementRewardsAsync(int rewardType, int playerId)
    {
        var tcs = new TaskCompletionSource<List<RewardItem>>();

        StartCoroutine(GetPlayerAchievementRewardsCoroutine(rewardType, playerId, rewards => tcs.SetResult(rewards)));

        return tcs.Task;
    }

    private IEnumerator GetPlayerAchievementRewardsCoroutine(int rewardType, int playerId, Action<List<RewardItem>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/rewards/player-achievements?rewardType={rewardType}&playerId={playerId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Load reward achievements failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(new List<RewardItem>());
            yield break;
        }

        List<RewardItem> rewards = ParseRewardItems(req.downloadHandler.text, "reward achievements");

        for (int i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (reward == null)
            {
                continue;
            }

            if (reward.seq <= 0)
            {
                reward.seq = i + 1;
            }
        }

        onSuccess?.Invoke(rewards);
    }

    private List<RewardItem> ParseRewardItems(string json, string errorContext)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<RewardItem>();
        }

        try
        {
            var tokens = JArray.Parse(json);
            var rewards = new List<RewardItem>(tokens.Count);

            foreach (var token in tokens)
            {
                var reward = CreateRewardItemFromToken(token);
                if (reward != null)
                {
                    rewards.Add(reward);
                }
            }

            return rewards;
        }
        catch (Exception ex)
        {
            string context = string.IsNullOrEmpty(errorContext) ? "rewards" : errorContext;
            ShowError($"âŒ Error parsing {context}: " + ex.Message);
            return new List<RewardItem>();
        }
    }

    private RewardItem ParseRewardItem(string json, string errorContext)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            var token = JToken.Parse(json);
            var rewardToken = ResolveRewardToken(token);
            if (rewardToken == null)
            {
                return null;
            }

            if (rewardToken.Type != JTokenType.Object && rewardToken.Type != JTokenType.Array)
            {
                return null;
            }

            if (rewardToken.Type == JTokenType.Array)
            {
                foreach (var child in rewardToken.Children())
                {
                    var parsedReward = CreateRewardItemFromToken(child);
                    if (parsedReward != null)
                    {
                        return parsedReward;
                    }
                }
                return null;
            }

            return CreateRewardItemFromToken(rewardToken);
        }
        catch (Exception ex)
        {
            string context = string.IsNullOrEmpty(errorContext) ? "reward" : errorContext;
            ShowError($"âŒ Error parsing {context}: " + ex.Message);
            return null;
        }
    }

    private JToken ResolveRewardToken(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token.Type == JTokenType.Array)
        {
            return token;
        }

        if (token.Type != JTokenType.Object)
        {
            return token;
        }

        foreach (string key in new[] { "reward", "data", "result", "payload" })
        {
            var nested = token[key];
            if (nested == null || nested.Type == JTokenType.Null)
            {
                continue;
            }

            var resolved = ResolveRewardToken(nested);
            if (resolved != null)
            {
                return resolved;
            }
        }

        return token;
    }

    private RewardItem CreateRewardItemFromToken(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        int? itemId = null;
        var itemIdToken = token["itemId"];
        if (itemIdToken != null && itemIdToken.Type != JTokenType.Null)
        {
            switch (itemIdToken.Type)
            {
                case JTokenType.Integer:
                    itemId = itemIdToken.Value<int>();
                    break;
                case JTokenType.Float:
                    itemId = Mathf.RoundToInt(itemIdToken.Value<float>());
                    break;
                default:
                    if (int.TryParse(itemIdToken.ToString(), out int parsedItemId))
                    {
                        itemId = parsedItemId;
                    }
                    break;
            }
        }

        return new RewardItem
        {
            rewardType = token.Value<string>("rewardType"),
            seq = token.Value<int?>("seq") ?? 0,
            locationId = token.Value<int?>("locationId") ?? 0,
            itemId = itemId,
            rewardAmount = token.Value<int?>("rewardAmount") ?? 0,
            isUsed = token.Value<bool?>("isUsed") ?? false,
            isGiftReceived = token.Value<bool?>("isGiftReceived") ?? false,
            isComplete = token.Value<bool?>("isComplete") ?? false,
            updatedAt = token.Value<string>("updatedAt"),
            countGif = token.Value<int?>("countGif") ?? 0
        };
    }

    public Task<RewardItem> ConfirmRewardClaimAsync(int playerId, string rewardType, int achievementId, int locationId)
    {
        var tcs = new TaskCompletionSource<RewardItem>();

        StartCoroutine(ConfirmRewardClaimCoroutine(playerId, rewardType, achievementId, locationId, reward =>
        {
            tcs.SetResult(reward);
        }));

        return tcs.Task;
    }

    private IEnumerator ConfirmRewardClaimCoroutine(int playerId, string rewardType, int achievementId, int locationId, Action<RewardItem> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/rewards/confirm-ad";
        var data = new ConfirmRewardClaimRequest
        {
            playerId = playerId,
            rewardType = rewardType,
            achievementId = achievementId,
            locationId = locationId
        };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ KhÃ´ng thá»ƒ xÃ¡c nháº­n Ä‘Ã£ xem quáº£ng cÃ¡o: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onComplete?.Invoke(null);
            yield break;
        }

        RewardItem reward = ParseRewardItem(req.downloadHandler.text, "confirm ad reward");
        if (reward == null)
        {
            Debug.LogWarning("Confirm ad response did not contain a reward payload.");
        }

        onComplete?.Invoke(reward);
    }

    public Task<bool> SendRewardWatchedAsync(int seq, int locationId)
    {
        var tcs = new TaskCompletionSource<bool>();

        StartCoroutine(SendRewardWatchedCoroutine(seq, locationId, success =>
        {
            tcs.SetResult(success);
        }));

        return tcs.Task;
    }

    private IEnumerator SendRewardWatchedCoroutine(int seq, int locationId, Action<bool> onComplete)
    {
        string url = BuildRewardActionUrl($"{ApiConfig.BaseUrl}/watched", "seq", seq, "locationId", locationId);
        UnityWebRequest req = UnityWebRequest.PostWwwForm(url, string.Empty);
        yield return SendConfiguredWebRequest(req, req.url);

        bool success;

        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Server khÃ´ng xÃ¡c nháº­n Ä‘Ã£ xem: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    public Task<bool> SendRewardClaimAsync(int seq, int locationId)
    {
        var tcs = new TaskCompletionSource<bool>();

        StartCoroutine(SendRewardClaimCoroutine(seq, locationId, success =>
        {
            tcs.SetResult(success);
        }));

        return tcs.Task;
    }

    private IEnumerator SendRewardClaimCoroutine(int seq, int locationId, Action<bool> onComplete)
    {
        string url = BuildRewardActionUrl($"{ApiConfig.BaseUrl}/claim", "locationId", locationId, "seq", seq);
        UnityWebRequest req = UnityWebRequest.PostWwwForm(url, string.Empty);
        yield return SendConfiguredWebRequest(req, req.url);

        bool success;

        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Nháº­n quÃ  tháº¥t báº¡i: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    private static string BuildRewardActionUrl(string baseUrl, string preferredParam, int preferredValue, string fallbackParam, int fallbackValue)
    {
        if (preferredValue > 0)
        {
            return $"{baseUrl}?{preferredParam}={preferredValue}";
        }

        if (fallbackValue > 0)
        {
            return $"{baseUrl}?{fallbackParam}={fallbackValue}";
        }

        Debug.LogWarning($"Missing identifier when calling {baseUrl}. Using base URL without query parameters.");
        return baseUrl;
    }

    #endregion

    #region [====== GameSessionNetWork_Host ]

    public async Task<PlayerInfoStruct[]> GetListPlayerGameById(List<int> userIds)
    {
        try
        {
            List<PlayerSchema> result = await GetUsersAsync(userIds);

            if (result == null || result.Count == 0)
            {
                ShowError("âŒ KhÃ´ng thá»ƒ láº¥y danh sÃ¡ch ngÆ°á»i chÆ¡i hoáº·c danh sÃ¡ch trá»‘ng!");
                return Array.Empty<PlayerInfoStruct>();
            }

            PlayerInfoStruct[] players = result.Select(x => new PlayerInfoStruct
            {
                playerId = x.id,
                level = x.Level,
                fullname = x.PlayerName,
                playerbody = (PlayerBodyType)x.Body,
                score = 0,
                combo = 0,
                ball = (ItemCode)x.Ball,
                RingBall = x.RingBall,
                avatar = 1,
                exactRatio = 1,
                powerForce = x.totalPower,
                spinForce = x.totalSpin,
                avatarUrl = x.AvatarUrl ?? string.Empty,
                providerType = x.ProviderType ?? string.Empty,
                idAccount = x.IdAccount ?? string.Empty,
                statusPlayer = StatusPlayer.ShootExam,
                distance = 0,
                isDestroy = false,
                isHolding = true,
                //FingerPosition = Vector3.zero,
                turnOrder = 0,
            }).ToArray();

            return players;
        }
        catch (Exception e)
        {
            ShowError($"âŒ Lá»—i khi láº¥y danh sÃ¡ch ngÆ°á»i chÆ¡i: {e.Message}");
            return Array.Empty<PlayerInfoStruct>();
        }
    }
      Task<List<PlayerSchema>> GetUsersAsync(List<int> userIds)
    {
        var tcs = new TaskCompletionSource<List<PlayerSchema>>();

        StartCoroutine(GetUsersByIdCoroutine(userIds, (List<PlayerSchema> players) =>
        {
            tcs.SetResult(players);
        }));

        return tcs.Task;
    }

    private IEnumerator GetUsersByIdCoroutine(List<int> userIds, Action<List<PlayerSchema>> onSuccess)
    {
        string idsParam = string.Join(",", userIds); // "1,2,3"
        string url = $"{ApiConfig.BaseUrl}/player/by-list-id"; // Äáº£m báº£o Ä‘Ãºng route phÃ­a server

        // Táº¡o body JSON
        var requestData = new { ids = userIds };
        string jsonBody = JsonUtility.ToJson(new Wrapper { ids = userIds });

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return SendConfiguredWebRequest(request, request.url);

            if (request.result == UnityWebRequest.Result.Success)
            {
                var json = request.downloadHandler.text;
                var wrapper = JsonUtility.FromJson<UserRoomListWrapperFix>(json);
                onSuccess?.Invoke(wrapper.players);
            }
            else
            {
                ShowError("âŒ Lá»—i khi gá»i API: " + request.error, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
                onSuccess?.Invoke(null);
            }
        }
    }

    #endregion

    #region [====== EffectPlayerController ]

    public Task<List<EffectPlayerSchema>> GetEffectPlayersAsync(int playerId)
    {
        var tcs = new TaskCompletionSource<List<EffectPlayerSchema>>();
        StartCoroutine(GetEffectPlayersCoroutine(playerId, list => tcs.SetResult(list)));
        return tcs.Task;
    }

    private IEnumerator GetEffectPlayersCoroutine(int playerId, Action<List<EffectPlayerSchema>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/effect-player/{playerId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Load effect players failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        var wrapper = JsonUtility.FromJson<EffectPlayerListWrapper>("{\"effects\":" + req.downloadHandler.text + "}");
        onSuccess?.Invoke(wrapper != null ? wrapper.effects : new List<EffectPlayerSchema>());
    }

    #endregion

    #region [====== InventoryController ]

    public Task<PlayerInventorySchema> GetPlayerInventoryAsync(int playerId)
    {
        var tcs = new TaskCompletionSource<PlayerInventorySchema>();
        StartCoroutine(GetPlayerInventoryCoroutine(playerId, inv => tcs.SetResult(inv)));
        return tcs.Task;
    }

    public Task<PlayerInventorySchema> EquipItemAsync(int playerId, int typeGid, int itemId, int seqBall)
    {
        var tcs = new TaskCompletionSource<PlayerInventorySchema>();
        StartCoroutine(EquipItemCoroutine(playerId, typeGid, itemId, seqBall, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<PlayerInventorySchema> UnequipItemAsync(int playerId, int locationId)
    {
        var tcs = new TaskCompletionSource<PlayerInventorySchema>();
        StartCoroutine(UnequipItemCoroutine(playerId, locationId, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<PlayerInventorySchema> EquipBallAsync(int playerId, int itemId)
    {
        var tcs = new TaskCompletionSource<PlayerInventorySchema>();
        StartCoroutine(EquipBallCoroutine(playerId, itemId, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<List<ItemSchema>> GetItemsAsync(int LocationItemGid, int? userId = null)
    {
        var tcs = new TaskCompletionSource<List<ItemSchema>>();
        StartCoroutine(GetItemsCoroutine(LocationItemGid, userId, list => tcs.SetResult(list)));
        return tcs.Task;
    }

    public Task<PlayerInventorySchema> BuyItemAsync(int playerId, int itemId, ShopPurchaseCurrency currencyType = ShopPurchaseCurrency.Money)
    {
        var tcs = new TaskCompletionSource<PlayerInventorySchema>();
        StartCoroutine(BuyItemCoroutine(playerId, itemId, currencyType, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<bool> SellItemAsync(int playerId, int itemId, int seq, int price)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(SellItemCoroutine(playerId, itemId, seq, price, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<bool> SellItemOnMarketAsync(int playerId, int itemId, int seq, int price)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(SellItemOnMarketCoroutine(playerId, itemId, seq, price, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<bool> CancelSellMarketAsync(int playerId, int itemId, int seq)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(CancelSellMarketCoroutine(playerId, itemId, seq, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<PlayerInventorySchema> DismantleBallAsync(int playerId, int itemId, int seq)
    {
        var tcs = new TaskCompletionSource<PlayerInventorySchema>();
        StartCoroutine(DismantleBallCoroutine(playerId, itemId, seq, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<PlayerInventorySchema> RepairBallAsync(int playerId, int itemId, int seq)
    {
        var tcs = new TaskCompletionSource<PlayerInventorySchema>();
        StartCoroutine(RepairBallCoroutine(playerId, itemId, seq, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<PlayerInventorySchema> BuyItemOnMarketAsync(int buyerId, int sellerId, int itemId, int seq)
    {
        var tcs = new TaskCompletionSource<PlayerInventorySchema>();
        StartCoroutine(BuyItemOnMarketCoroutine(buyerId, sellerId, itemId, seq, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<List<MarketItemSchema>> GetMarketItemsAsync(string itemName, int? levelFrom, int? levelTo, List<int> rarityGids, int page)
    {
        var tcs = new TaskCompletionSource<List<MarketItemSchema>>();
        StartCoroutine(GetMarketItemsCoroutine(itemName, levelFrom, levelTo, rarityGids, page, res => tcs.SetResult(res)));
        return tcs.Task;
    }


    public Task<List<MarketOrderBoardEntry>> GetMarketOrderBoardAsync(int itemId)
    {
        var tcs = new TaskCompletionSource<List<MarketOrderBoardEntry>>();
        StartCoroutine(GetMarketOrderBoardCoroutine(itemId, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<bool> PlaceBuyRequestOrderAsync(int playerId, int itemId, int price, int quantity)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(PlaceBuyRequestOrderCoroutine(playerId, itemId, price, quantity, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    private IEnumerator GetMarketOrderBoardCoroutine(int itemId, Action<List<MarketOrderBoardEntry>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/market/order-board?itemId={itemId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Request failed: " + req.error);
            onSuccess?.Invoke(new List<MarketOrderBoardEntry>());
            yield break;
        }

        var responseText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
        var board = JsonUtility.FromJson<MarketOrderBoardResponse>(responseText);
        if (board == null)
        {
            onSuccess?.Invoke(new List<MarketOrderBoardEntry>());
            yield break;
        }

        var entriesByPrice = new Dictionary<int, MarketOrderBoardEntry>();

        foreach (var order in board.buyOrders ?? Enumerable.Empty<MarketOrderBoardOrder>())
        {
            if (!entriesByPrice.TryGetValue(order.price, out var entry))
            {
                entry = new MarketOrderBoardEntry { price = order.price };
                entriesByPrice[order.price] = entry;
            }
            entry.quantityBuy += order.count > 0 ? order.count : 1;
        }

        foreach (var order in board.sellingOrders ?? Enumerable.Empty<MarketOrderBoardOrder>())
        {
            if (!entriesByPrice.TryGetValue(order.price, out var entry))
            {
                entry = new MarketOrderBoardEntry { price = order.price };
                entriesByPrice[order.price] = entry;
            }
            entry.quantitySell += order.count > 0 ? order.count : 1;
        }

        var entries = entriesByPrice.Values
            .OrderByDescending(x => x.price)
            .ToList();

        onSuccess?.Invoke(entries);
    }

    private IEnumerator PlaceBuyRequestOrderCoroutine(int playerId, int itemId, int price, int quantity, Action<bool> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/market/buy-request";
        var data = new PlaceBuyRequestOrderRequest { playerId = playerId, itemId = itemId, price = price, quantity = quantity };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);
        onSuccess?.Invoke(req.result == UnityWebRequest.Result.Success);
    }
    public Task<List<ItemSchema>> GetMarketCatalogItemsAsync(int typeGid = 1)
    {
        var tcs = new TaskCompletionSource<List<ItemSchema>>();
        StartCoroutine(GetMarketCatalogItemsCoroutine(typeGid, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    private IEnumerator GetItemPriceOverviewCoroutine(int itemId, TaskCompletionSource<ItemPriceOverviewData> tcs)
    {
        string url = $"{ApiConfig.BaseUrl}/market/item-price-overview?itemId={itemId}";
        UnityWebRequest req = UnityWebRequest.Get(url);

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Failed to fetch price overview: {req.error}");
            tcs.TrySetResult(null);
            req.Dispose();
            yield break;
        }

        ItemPriceOverviewData data = null;
        try
        {
            data = JsonUtility.FromJson<ItemPriceOverviewData>(req.downloadHandler != null ? req.downloadHandler.text : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error parsing price overview response: {ex.Message}");
        }

        tcs.TrySetResult(data);
        req.Dispose();
    }

    public Task<ItemPriceOverviewData> GetItemPriceOverviewAsync(int itemId)
    {
        var tcs = new TaskCompletionSource<ItemPriceOverviewData>();
        StartCoroutine(GetItemPriceOverviewCoroutine(itemId, tcs));
        return tcs.Task;
    }

    private IEnumerator EquipItemCoroutine(int playerId, int typeGid, int itemId,int seqBall, Action<PlayerInventorySchema> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/player/equip";
        var data = new EquipItemRequest { playerId = playerId, locationId = typeGid, itemId = itemId, seqItem = seqBall };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Request failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<PlayerInventorySchema>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    private IEnumerator UnequipItemCoroutine(int playerId, int locationId, Action<PlayerInventorySchema> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/player/unequip";
        var data = new UnequipItemRequest { playerId = playerId, locationId = locationId };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Request failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<PlayerInventorySchema>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    private IEnumerator EquipBallCoroutine(int playerId, int itemId, Action<PlayerInventorySchema> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/player/equip-ball";
        var data = new EquipBallRequest { playerId = playerId, itemId = itemId };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Request failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<PlayerInventorySchema>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    private IEnumerator GetItemsCoroutine(int LocationItemGid, int? userId, Action<List<ItemSchema>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/items";
        url += $"?locationGid={LocationItemGid}";
        if (userId.HasValue)
            url += $"&userId={userId.Value}";

        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Load items failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        var wrapper = JsonUtility.FromJson<ItemListWrapper>("{\"items\":" + req.downloadHandler.text + "}");
        onSuccess?.Invoke(wrapper != null ? wrapper.items : new List<ItemSchema>());
    }

    private IEnumerator BuyItemCoroutine(int playerId, int itemId, ShopPurchaseCurrency currencyType, Action<PlayerInventorySchema> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/player-item/buy";
        var data = new BuyItemRequest { playerId = playerId, itemId = itemId, currencyType = (int)currencyType };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Request failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<PlayerInventorySchema>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    private IEnumerator DismantleBallCoroutine(int playerId, int itemId, int seq, Action<PlayerInventorySchema> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/player-item/dismantle";
        var data = new DismantleBallRequest { playerId = playerId, itemId = itemId, seq = seq };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Request failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<PlayerInventorySchema>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    private IEnumerator RepairBallCoroutine(int playerId, int itemId, int seq, Action<PlayerInventorySchema> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/player-item/repair";
        var data = new RepairBallRequest { playerId = playerId, itemId = itemId, seq = seq };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<PlayerInventorySchema>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    private IEnumerator SellItemCoroutine(int playerId, int itemId, int seq, int price, Action<bool> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/player-item/sell";
        var data = new SellItemRequest { playerId = playerId, itemId = itemId, seq = seq, price = price };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    #endregion

    #region [====== FriendMessageUIController ]

    public Task<bool> ReadMessageAsync(int playerId, int seqMess)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(ReadMessageCoroutine(playerId, seqMess, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public Task<bool> ReadMessage(int playerId, int seqMess)
    {
        return ReadMessageAsync(playerId, seqMess);
    }

    private IEnumerator ReadMessageCoroutine(int playerId, int seqMess, Action<bool> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/read-message";
        var data = new ReadMessageRequest
        {
            playerId = playerId,
            seqMess = seqMess
        };
        string json = JsonUtility.ToJson(data);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    #endregion

    #region [====== InventoryController ]

    private IEnumerator SellItemOnMarketCoroutine(int playerId, int itemId, int seq, int price, Action<bool> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/market/sell";
        var data = new SellItemRequest { playerId = playerId, itemId = itemId, seq = seq, price = price };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    private IEnumerator CancelSellMarketCoroutine(int playerId, int itemId, int seq, Action<bool> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/market/cancel";
        var data = new CancelSaleRequest { playerId = playerId, itemId = itemId, seq = seq };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    private IEnumerator BuyItemOnMarketCoroutine(int buyerId, int sellerId, int itemId, int seq, Action<PlayerInventorySchema> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/market/buy";
        var data = new BuyMarketRequest { buyerId = buyerId, sellerId = sellerId, itemId = itemId, seq = seq };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<PlayerInventorySchema>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    private IEnumerator GetMarketItemsCoroutine(string itemName, int? levelFrom, int? levelTo, List<int> rarityGids, int page, Action<List<MarketItemSchema>> onSuccess)
    {
        var queries = new List<string>();
        if (!string.IsNullOrEmpty(itemName)) queries.Add($"itemName={UnityWebRequest.EscapeURL(itemName)}");
        if (levelFrom.HasValue) queries.Add($"levelFrom={levelFrom.Value}");
        if (levelTo.HasValue) queries.Add($"levelTo={levelTo.Value}");
        if (rarityGids != null && rarityGids.Count > 0) queries.Add($"rarityGids={string.Join(",", rarityGids)}");
        queries.Add($"page={page}");

        string url = $"{ApiConfig.BaseUrl}/market/items";
        if (queries.Count > 0)
            url += "?" + string.Join("&", queries);
        Debug.Log(url);
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Load market items failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        var wrapper = JsonUtility.FromJson<MarketItemListWrapper>("{\"items\":" + req.downloadHandler.text + "}");
        onSuccess?.Invoke(wrapper != null ? wrapper.items : new List<MarketItemSchema>());
    }

    private IEnumerator GetMarketCatalogItemsCoroutine(int typeGid, Action<List<ItemSchema>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/market/catalog?typeGid={typeGid}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Load market catalog failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        var wrapper = JsonUtility.FromJson<ItemListWrapper>("{\"items\":" + req.downloadHandler.text + "}");
        onSuccess?.Invoke(wrapper != null ? wrapper.items : new List<ItemSchema>());
    }

    private IEnumerator GetPlayerInventoryCoroutine(int playerId, Action<PlayerInventorySchema> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/players/{playerId}/inventory";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Load inventory failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        PlayerInventorySchema resp = JsonUtility.FromJson<PlayerInventorySchema>(req.downloadHandler.text);
        onSuccess?.Invoke(resp);
    }


    #endregion


    #region [====== GameInitializer ]

    public Task<List<SysMasLanguage>> GetLanguagesAsync()
    {
        var tcs = new TaskCompletionSource<List<SysMasLanguage>>();
        StartCoroutine(GetLanguagesCoroutine(langs => tcs.SetResult(langs)));
        return tcs.Task;
    }

    private IEnumerator GetLanguagesCoroutine(Action<List<SysMasLanguage>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/languages"; 
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Load languages failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        var wrapper = JsonUtility.FromJson<LanguageListWrapper>("{\"languages\":" + req.downloadHandler.text + "}");
        onSuccess?.Invoke(wrapper != null ? wrapper.languages : new List<SysMasLanguage>());
    }

    public Task<List<BotPlayerData>> GetBotPlayersAsync(int count = 1)
    {
        var tcs = new TaskCompletionSource<List<BotPlayerData>>();
        StartCoroutine(GetBotPlayersCoroutine(count, bots => tcs.SetResult(bots)));
        return tcs.Task;
    }

    private IEnumerator GetBotPlayersCoroutine(int count, Action<List<BotPlayerData>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/player/bots?count={count}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Load bot players failed: " + req.error);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var wrapper = JsonUtility.FromJson<BotPlayerListWrapper>(req.downloadHandler.text);
            var bots = wrapper != null ? wrapper.players : new List<BotPlayerData>();
            // Danh sÃ¡ch tÃªn ngáº«u nhiÃªn (Anh + Viá»‡t)
            string[] randomNames = new string[] {
                "Alex", "Minh", "Liam", "An", "Emma", "nhingi", "Noah", "BÃ¬nh", "Olivia", "Provip123",
                "Mason", "HÃ¹ng", "Lucas", "123aabb", "Ethan", "PhÃºc", "Ava", "minhday", "David", "DÅ©ng",
                "Sophia", "nguoivip", "Jack", "Tuáº¥n", "Henry", "Quang", "Chloe", "Okayonla", "Daniel", "KhÃ¡nh",
                "Mia", "Vy", "Ben", "Nam", "Tom", "Linh", "Sam", "KhÃ¡ báº£nh", "Anna", "aaabbbb"
            };
            System.Random rnd = new System.Random();
            var usedNames = new HashSet<string>();
            foreach (var bot in bots)
            {
                // GÃ¡n tÃªn ngáº«u nhiÃªn, khÃ´ng trÃ¹ng trong 1 láº§n gá»i
                string name;
                int tries = 0;
                do {
                    name = randomNames[rnd.Next(randomNames.Length)];
                    tries++;
                } while (usedNames.Contains(name) && tries < 10);
                usedNames.Add(name);
                bot.PlayerName = name;
            }
            onSuccess?.Invoke(bots);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error parsing bot players: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    #endregion

    #region [====== EffectPlayerController ]

    public Task<LevelUpEffectResponse> LevelUpEffectPlayer(int playerId, int effectId)
    {
        var tcs = new TaskCompletionSource<LevelUpEffectResponse>();
        StartCoroutine(LevelUpEffectPlayerCoroutine(playerId, effectId, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<EquipEffectResponse> EquipEffectPlayer(int playerId, int oldEffectId, int newEffectId)
    {
        var tcs = new TaskCompletionSource<EquipEffectResponse>();
        StartCoroutine(EquipEffectPlayerCoroutine(playerId, oldEffectId, newEffectId, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    private IEnumerator LevelUpEffectPlayerCoroutine(int playerId, int effectId, Action<LevelUpEffectResponse> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/effect-player/level-up";
        var data = new LevelUpEffectRequest { playerId = playerId, effectId = effectId };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var response = JsonUtility.FromJson<LevelUpEffectResponse>(req.downloadHandler.text);
            onSuccess?.Invoke(response);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    private IEnumerator EquipEffectPlayerCoroutine(int playerId, int oldEffectId, int newEffectId, Action<EquipEffectResponse> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/effect-player/equip";
        var data = new EquipEffectRequest { playerId = playerId, oldEffectId = oldEffectId, newEffectId = newEffectId };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var response = JsonUtility.FromJson<EquipEffectResponse>(req.downloadHandler.text);
            if (response == null)
            {
                response = new EquipEffectResponse { message = req.downloadHandler.text };
            }
            onSuccess?.Invoke(response);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    #endregion

    #region [====== BallUpgradeController ]

    public Task<PlayerItemSchema> LevelUpItem(int playerId, int itemId, int seq, float successRate, List<UpgradeMaterial> materials)
    {
        var tcs = new TaskCompletionSource<PlayerItemSchema>();
        StartCoroutine(LevelUpItemCoroutine(playerId, itemId, seq, successRate, materials, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    private IEnumerator LevelUpItemCoroutine(int playerId, int itemId, int seq, float successRate, List<UpgradeMaterial> materials, Action<PlayerItemSchema> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/effect-player/item-level-up";
        var data = new LevelUpItemRequest { playerId = playerId, itemId = itemId, seq = seq, successRate = successRate, materials = materials };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<PlayerItemSchema>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    #endregion

    #region [====== PlayerItem Damage ]

    public Task<bool> UpdatePlayerItemDamageAsync(List<PlayerItemDamageUpdateEntry> entries)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(UpdatePlayerItemDamageCoroutine(entries, success => tcs.SetResult(success)));
        return tcs.Task;
    }

    private IEnumerator UpdatePlayerItemDamageCoroutine(List<PlayerItemDamageUpdateEntry> entries, Action<bool> onComplete)
    {
        if (entries == null || entries.Count == 0)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        string url = $"{ApiConfig.BaseUrl}/player-item/damage";
        string json = JsonHelper.ToJson(entries);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    #endregion

    #region [====== BallFusionController ]

    public Task<PlayerInventorySchema> FusionItems(int playerId, int ballAId, int ballBId, int catalystId, float successRate)
    {
        var tcs = new TaskCompletionSource<PlayerInventorySchema>();
        StartCoroutine(FusionItemsCoroutine(playerId, ballAId, ballBId, catalystId, successRate, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    private IEnumerator FusionItemsCoroutine(int playerId, int ballAId, int ballBId, int catalystId, float successRate, Action<PlayerInventorySchema> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/ball-fusion";
        var data = new FusionRequest { playerId = playerId, ballAId = ballAId, ballBId = ballBId, catalystId = catalystId, successRate = successRate };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<PlayerInventorySchema>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    #endregion

    #region [====== Paper Legends Heroes ]

    public Task<PaperLegendHeroListResponse> GetPaperLegendHeroesAsync()
    {
        return GetPaperLegendHeroesAsync(null);
    }

    public Task<PaperLegendHeroListResponse> GetPaperLegendHeroesByModelIdsAsync(List<int> modelIds)
    {
        List<string> ids = null;
        if (modelIds != null && modelIds.Count > 0)
        {
            ids = modelIds
                .Where(modelId => modelId > 0)
                .Select(modelId => modelId.ToString())
                .Distinct()
                .ToList();
        }

        return GetPaperLegendHeroesAsync(ids);
    }

    public Task<PaperLegendHeroListResponse> GetPaperLegendHeroesAsync(List<string> ids)
    {
        var tcs = new TaskCompletionSource<PaperLegendHeroListResponse>();
        StartCoroutine(GetPaperLegendHeroesCoroutine(ids, response => tcs.TrySetResult(response)));
        return tcs.Task;
    }

    private IEnumerator GetPaperLegendHeroesCoroutine(List<string> ids, Action<PaperLegendHeroListResponse> onComplete)
    {
        string url = ApiConfig.BaseUrl + "/heroes";
        if (ids != null && ids.Count > 0)
        {
            string query = string.Join(",", ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => UnityWebRequest.EscapeURL(id.Trim())));

            if (!string.IsNullOrWhiteSpace(query))
                url += "?ids=" + query;
        }

        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        string responseText = req.downloadHandler != null ? req.downloadHandler.text : null;
        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("[PaperLegends] Load heroes failed: " + req.error, req.responseCode, responseText);
            onComplete?.Invoke(null);
            req.Dispose();
            yield break;
        }

        try
        {
            var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PaperLegendHeroListResponse>(responseText);
            if (response == null)
                response = new PaperLegendHeroListResponse();

            if (response.heroes == null)
                response.heroes = new List<PaperLegendHeroData>();

            onComplete?.Invoke(response);
        }
        catch (Exception ex)
        {
            ShowError("[PaperLegends] Error parsing heroes: " + ex.Message, req.responseCode, responseText);
            onComplete?.Invoke(null);
        }

        req.Dispose();
    }

    #endregion

    #region [====== GameSessionNetWork_Host ]

    public Task<List<PlayerBallPhysics>> GetBallPhysicsAsync(List<int> ids)
    {
        var tcs = new TaskCompletionSource<List<PlayerBallPhysics>>();
        StartCoroutine(GetBallPhysicsCoroutine(ids, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    private IEnumerator GetBallPhysicsCoroutine(List<int> ids, Action<List<PlayerBallPhysics>> onSuccess)
    {
        string url = ApiConfig.BaseUrl + "/players/ball-physics";
        string json = JsonUtility.ToJson(new Wrapper { ids = ids });
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var root = JsonUtility.FromJson<PlayerBallPhysicsRoot>(req.downloadHandler.text);
            onSuccess?.Invoke(root != null ? root.physics : new List<PlayerBallPhysics>());
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    #endregion

    #region [====== BallForge ]

    public Task<bool> SaveBallForgeSkillAsync(BallForgeSkillRequest request)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(SaveBallForgeSkillCoroutine(request?.slotIndex ?? 0, request, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public IEnumerator SaveBallForgeSkillCoroutine(int slotIndex, BallForgeSkillRequest request, Action<bool> onComplete)
    {
        if (request == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        string url = $"{ApiConfig.BaseUrl}/ball-forge/skill/{slotIndex}";
        var payload = BuildBallForgeSkillPayload(slotIndex, request);
        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ LÆ°u ká»¹ nÄƒng rÃ¨n bi tháº¥t báº¡i: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onComplete?.Invoke(false);
            yield break;
        }

        onComplete?.Invoke(true);
    }

    private BallForgeSkillRequest BuildBallForgeSkillPayload(int slotIndex, BallForgeSkillRequest request)
    {
        var payload = new BallForgeSkillRequest
        {
            playerId = request.playerId,
            itemId = request.itemId,
            seq = request.seq,
            slotIndex = slotIndex,
            materialItemId = request.materialItemId,
            materialSeq = request.materialSeq,
            ringballCost = request.ringballCost,
            skill = null
        };

        if (request.skill != null)
        {
            payload.skill = new BallForgeSkillData
            {
                SkillType = request.skill.SkillType,
                BallShootingTechnique = request.skill.SkillType == BallForgeSkillType.BallShootingTechnique ? request.skill.BallShootingTechnique : null,
                SupportBonus = request.skill.SkillType == BallForgeSkillType.Support ? request.skill.SupportBonus : null
            };
        }

        return payload;
    }

    public Task<bool> ActivateBallForgeSkillAsync(BallForgeActivationRequest request)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(ActivateBallForgeSkillCoroutine(request, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public IEnumerator ActivateBallForgeSkillCoroutine(BallForgeActivationRequest request, Action<bool> onComplete)
    {
        if (request == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        string url = $"{ApiConfig.BaseUrl}/ball-forge/activate";
        string json = JsonUtility.ToJson(request);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ KÃ­ch hoáº¡t ká»¹ nÄƒng rÃ¨n bi tháº¥t báº¡i: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onComplete?.Invoke(false);
            yield break;
        }

        onComplete?.Invoke(true);
    }

    #endregion

    #region [====== FriendController ]

    public Task<bool> SendFriendRequest(int senderId, string FriendCode)
    {
        return SendFriendRequestAsync(senderId, FriendCode);
    }
    public Task<bool> SendFriendRequestAsync(int senderId, string FriendCode)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(SendFriendRequestCoroutine(senderId, FriendCode, r => tcs.SetResult(r)));
        return tcs.Task;
    }



    private IEnumerator SendFriendRequestCoroutine(int senderId, string FriendCode, Action<bool> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/friend-request";
        var data = new FriendRequestModel { senderId = senderId, friendCode = FriendCode };
        string json = JsonUtility.ToJson(data);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    public Task<bool> RemoveFriendAsync(int playerId, int friendId)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(RemoveFriendCoroutine(playerId, friendId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public Task<bool> RemoveFriend(int playerId, int friendId)
    {
        return RemoveFriendAsync(playerId, friendId);
    }

    private IEnumerator RemoveFriendCoroutine(int playerId, int friendId, Action<bool> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/friend-remove";
        var data = new FriendRemovePayload { playerId = playerId, friendId = friendId };
        string json = JsonUtility.ToJson(data);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    public Task<bool> RespondFriendRequestAsync(int senderId, int receiverId, int status)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(RespondFriendRequestCoroutine(senderId, receiverId, status, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public Task<bool> RespondFriendRequest(int senderId, int receiverId, int status)
    {
        return RespondFriendRequestAsync(senderId, receiverId, status);
    }

    private IEnumerator RespondFriendRequestCoroutine(int senderId, int receiverId, int status, Action<bool> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/friend-respond";
        var data = new FriendResponseModel { senderId = senderId, receiverId = receiverId, status = status };
        string json = JsonUtility.ToJson(data);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    public Task<PlayerInfoStruct[]> GetPendingFriendRequests(int receiverId)
    {
        var tcs = new TaskCompletionSource<PlayerInfoStruct[]>();
        StartCoroutine(GetPendingFriendRequestsCoroutine(receiverId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    private IEnumerator GetPendingFriendRequestsCoroutine(int receiverId, Action<PlayerInfoStruct[]> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/friend-requests/{receiverId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onComplete?.Invoke(Array.Empty<PlayerInfoStruct>());
            yield break;
        }

        var wrapper = JsonUtility.FromJson<PlayerList>("{\"items\":" + req.downloadHandler.text + "}");
        if (wrapper == null || wrapper.items == null)
        {
            onComplete?.Invoke(Array.Empty<PlayerInfoStruct>());
            yield break;
        }

        PlayerInfoStruct[] players = wrapper.items.Select(x => new PlayerInfoStruct
        {
            playerId = x.id,
            level = x.Level,
            fullname = x.PlayerName,
            playerbody = (PlayerBodyType)x.Body,
            ball = (ItemCode)x.Ball,
            RingBall = x.RingBall,
            avatar = 1,
            exactRatio = 1,
            powerForce = x.totalPower,
            spinForce = x.totalSpin,
            avatarUrl = x.AvatarUrl ?? string.Empty,
            providerType = x.ProviderType ?? string.Empty,
            idAccount = x.IdAccount ?? string.Empty,
            score = 0,
            combo = 0,
            statusPlayer = StatusPlayer.Normal,
            distance = 0,
            isDestroy = false,
            isHolding = false,
            turnOrder = 0,
            isCatAnTienActive = 0
        }).ToArray();

        onComplete?.Invoke(players);
    }

    public Task<PlayerInfoStruct[]> GetFriendList(int playerId)
    {
        var tcs = new TaskCompletionSource<PlayerInfoStruct[]>();
        StartCoroutine(GetFriendListCoroutine(playerId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    private IEnumerator GetFriendListCoroutine(int playerId, Action<PlayerInfoStruct[]> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/friend-list/{playerId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onComplete?.Invoke(Array.Empty<PlayerInfoStruct>());
            yield break;
        }

        var wrapper = JsonUtility.FromJson<PlayerList>("{\"items\":" + req.downloadHandler.text + "}");
        if (wrapper == null || wrapper.items == null)
        {
            onComplete?.Invoke(Array.Empty<PlayerInfoStruct>());
            yield break;
        }

        PlayerInfoStruct[] players = wrapper.items.Select(x => new PlayerInfoStruct
        {
            playerId = x.id,
            level = x.Level,
            fullname = x.PlayerName,
            playerbody = (PlayerBodyType)x.Body,
            ball = (ItemCode)x.Ball,
            RingBall = x.RingBall,
            avatar = 1,
            exactRatio = 1,
            powerForce = x.totalPower,
            spinForce = x.totalSpin,
            avatarUrl = x.AvatarUrl ?? string.Empty,
            providerType = x.ProviderType ?? string.Empty,
            idAccount = x.IdAccount ?? string.Empty,
            score = 0,
            combo = 0,
            statusPlayer = StatusPlayer.Normal,
            distance = 0,
            isDestroy = false,
            isHolding = false,
            turnOrder = 0,
            isCatAnTienActive = 0
        }).ToArray();

        onComplete?.Invoke(players);
    }

    #endregion 

    #region [====== FriendMessageUIController ]

    public Task<bool> SendMessageAsync(int senderId, int receiverId, string content, int itemId = 0, int seqId = 0)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(SendMessageCoroutine(senderId, receiverId, content, itemId, seqId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public Task<bool> SendMessage(int senderId, int receiverId, string content, int itemId = 0, int seqId = 0)
    {
        return SendMessageAsync(senderId, receiverId, content, itemId, seqId);
    }

    private IEnumerator SendMessageCoroutine(int senderId, int receiverId, string content, int itemId, int seqId, Action<bool> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/send-message";
        var data = new MessageRequestModel
        {
            senderId = senderId,
            receiverId = receiverId,
            content = content,
            itemId = itemId,
            seqId = seqId
        };
        string json = JsonUtility.ToJson(data);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }


    public Task<MessageModel[]> GetSystemMessages(int playerId)
    {
        var tcs = new TaskCompletionSource<MessageModel[]>();
        StartCoroutine(GetSystemMessagesCoroutine(playerId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public Task<bool> ClaimSystemMessageReward(MessageModel message)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(ClaimSystemMessageRewardCoroutine(message, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public Task<SystemMessageClaimResponse> ClaimSystemMessageRewardWithResult(MessageModel message)
    {
        var tcs = new TaskCompletionSource<SystemMessageClaimResponse>();
        StartCoroutine(ClaimSystemMessageRewardWithResultCoroutine(message, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public Task<MessageModel[]> GetFriendMessages(int receiverId)
    {
        var tcs = new TaskCompletionSource<MessageModel[]>();
        StartCoroutine(GetFriendMessagesCoroutine(receiverId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public Task<bool> DeleteFriendMessage(int playerId, int partnerId)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(DeleteFriendMessageCoroutine(playerId, partnerId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public Task<MessageModel[]> GetConversationHistory(int playerId, int friendId)
    {
        var tcs = new TaskCompletionSource<MessageModel[]>();
        StartCoroutine(GetConversationHistoryCoroutine(playerId, friendId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    private IEnumerator GetSystemMessagesCoroutine(int playerId, Action<MessageModel[]> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/messages/system/{playerId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onComplete?.Invoke(Array.Empty<MessageModel>());
            yield break;
        }

        try
        {
            var wrapper = JsonUtility.FromJson<MessageList>("{\"items\":" + req.downloadHandler.text + "}");
            if (wrapper == null || wrapper.items == null)
            {
                onComplete?.Invoke(Array.Empty<MessageModel>());
                yield break;
            }
            onComplete?.Invoke(wrapper.items.ToArray());
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onComplete?.Invoke(Array.Empty<MessageModel>());
        }
    }

    private IEnumerator ClaimSystemMessageRewardCoroutine(MessageModel message, Action<bool> onComplete)
    {
        if (message == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        string url = $"{ApiConfig.BaseUrl}/messages/system/claim";
        var payload = new JObject
        {
            ["receiverId"] = message.receiverId,
            ["seqMess"] = message.seqMess
        };

        if (message.ringBallReward > 0)
            payload["ringBallReward"] = message.ringBallReward;
        if (message.moneyReward > 0)
            payload["moneyReward"] = message.moneyReward;
        if (message.itemRewardId > 0)
            payload["itemRewardId"] = message.itemRewardId;
        if (message.itemId > 0)
            payload["itemId"] = message.itemId;
        if (message.seqId > 0)
            payload["seqId"] = message.seqId;

        string jsonBody = payload.ToString(Newtonsoft.Json.Formatting.None);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    private IEnumerator ClaimSystemMessageRewardWithResultCoroutine(MessageModel message, Action<SystemMessageClaimResponse> onComplete)
    {
        if (message == null)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        string url = $"{ApiConfig.BaseUrl}/messages/system/claim";
        var payload = new JObject
        {
            ["receiverId"] = message.receiverId,
            ["seqMess"] = message.seqMess
        };

        if (message.ringBallReward > 0)
            payload["ringBallReward"] = message.ringBallReward;
        if (message.moneyReward > 0)
            payload["moneyReward"] = message.moneyReward;
        if (message.itemRewardId > 0)
            payload["itemRewardId"] = message.itemRewardId;
        if (message.itemId > 0)
            payload["itemId"] = message.itemId;
        if (message.seqId > 0)
            payload["seqId"] = message.seqId;

        string jsonBody = payload.ToString(Newtonsoft.Json.Formatting.None);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("Ã¢ÂÅ’ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onComplete?.Invoke(null);
            yield break;
        }

        try
        {
            var response = JsonUtility.FromJson<SystemMessageClaimResponse>(req.downloadHandler.text);
            if (response != null)
                response.success = true;
            onComplete?.Invoke(response);
        }
        catch (Exception ex)
        {
            ShowError("Ã¢ÂÅ’ Error parsing response: " + ex.Message);
            onComplete?.Invoke(null);
        }
    }

    private IEnumerator GetFriendMessagesCoroutine(int receiverId, Action<MessageModel[]> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/messages/{receiverId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onComplete?.Invoke(Array.Empty<MessageModel>());
            yield break;
        }

        try
        {
            var wrapper = JsonUtility.FromJson<MessageList>("{\"items\":" + req.downloadHandler.text + "}");
            if (wrapper == null || wrapper.items == null)
            {
                onComplete?.Invoke(Array.Empty<MessageModel>());
                yield break;
            }
            onComplete?.Invoke(wrapper.items.ToArray());
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onComplete?.Invoke(Array.Empty<MessageModel>());
        }
    }

    private IEnumerator DeleteFriendMessageCoroutine(int playerId, int partnerId, Action<bool> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/messages/{playerId}/{partnerId}";
        UnityWebRequest req = UnityWebRequest.Delete(url);
        req.downloadHandler = new DownloadHandlerBuffer();

        yield return SendConfiguredWebRequest(req, req.url);

        bool success = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            success = true;
        }
        else
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }

        onComplete?.Invoke(success);
    }

    private IEnumerator GetConversationHistoryCoroutine(int playerId, int friendId, Action<MessageModel[]> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/messages/conversation/{playerId}/{friendId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onComplete?.Invoke(Array.Empty<MessageModel>());
            yield break;
        }

        try
        {
            var wrapper = JsonUtility.FromJson<MessageList>("{\"items\":" + req.downloadHandler.text + "}");
            if (wrapper == null || wrapper.items == null)
            {
                onComplete?.Invoke(Array.Empty<MessageModel>());
                yield break;
            }

            onComplete?.Invoke(wrapper.items.ToArray());
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onComplete?.Invoke(Array.Empty<MessageModel>());
        }
    }

    #endregion

    #region [====== Player History / Rank ]

    public Task<PlayerHistoryStats> GetPlayerHistoryStatsAsync(int playerId)
    {
        var tcs = new TaskCompletionSource<PlayerHistoryStats>();
        StartCoroutine(GetPlayerHistoryStatsCoroutine(playerId, tcs));
        return tcs.Task;
    }

    private IEnumerator GetPlayerHistoryStatsCoroutine(int playerId, TaskCompletionSource<PlayerHistoryStats> tcs)
    {
        string url = $"{ApiConfig.BaseUrl}/histories/{playerId}/stats";
        UnityWebRequest req = UnityWebRequest.Get(url);

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            tcs.TrySetResult(null);
            yield break;
        }

        try
        {
            var stats = JsonUtility.FromJson<PlayerHistoryStats>(req.downloadHandler.text);
            tcs.TrySetResult(stats);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            tcs.TrySetException(ex);
        }
    }

    public Task<PlayerRankLeaderboardResponse> GetRankLeaderboardAsync(int playerId)
    {
        var tcs = new TaskCompletionSource<PlayerRankLeaderboardResponse>();
        StartCoroutine(GetRankLeaderboardCoroutine(playerId, tcs));
        return tcs.Task;
    }

    private IEnumerator GetRankLeaderboardCoroutine(int playerId, TaskCompletionSource<PlayerRankLeaderboardResponse> tcs)
    {
        string url = $"{ApiConfig.BaseUrl}/histories/leaderboard?playerId={playerId}";
        UnityWebRequest req = UnityWebRequest.Get(url);

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            tcs.TrySetResult(null);
            yield break;
        }

        try
        {
            var response = JsonUtility.FromJson<PlayerRankLeaderboardResponse>(req.downloadHandler.text);
            tcs.TrySetResult(response);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            tcs.TrySetException(ex);
        }
    }

    public Task<List<PlayerMatchHistory>> GetPlayerMatchHistoriesAsync(int playerId, int page, int pageSize)
    {
        var tcs = new TaskCompletionSource<List<PlayerMatchHistory>>();
        StartCoroutine(GetPlayerMatchHistoriesCoroutine(playerId, page, pageSize, tcs));
        return tcs.Task;
    }

    private IEnumerator GetPlayerMatchHistoriesCoroutine(int playerId, int page, int pageSize, TaskCompletionSource<List<PlayerMatchHistory>> tcs)
    {
        string url = $"{ApiConfig.BaseUrl}/histories?playerId={playerId}&page={page}&pageSize={pageSize}";
        UnityWebRequest req = UnityWebRequest.Get(url);

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            tcs.TrySetResult(null);
            yield break;
        }

        try
        {
            var histories = JsonHelper.FromJson<PlayerMatchHistory>(req.downloadHandler.text);
            tcs.TrySetResult(histories);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            tcs.TrySetException(ex);
        }
    }

    public Task<List<PlayerMatchHistory>> GetMatchHistoriesByTransNoAsync(string transNo)
    {
        var tcs = new TaskCompletionSource<List<PlayerMatchHistory>>();
        StartCoroutine(GetMatchHistoriesByTransNoCoroutine(transNo, tcs));
        return tcs.Task;
    }

    private IEnumerator GetMatchHistoriesByTransNoCoroutine(string transNo, TaskCompletionSource<List<PlayerMatchHistory>> tcs)
    {
        string url = $"{ApiConfig.BaseUrl}/histories/transno/{UnityWebRequest.EscapeURL(transNo)}";
        UnityWebRequest req = UnityWebRequest.Get(url);

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[API] GetMatchHistoriesByTransNo failed: {req.error}");
            tcs.TrySetResult(null);
            yield break;
        }

        try
        {
            var histories = JsonHelper.FromJson<PlayerMatchHistory>(req.downloadHandler.text);
            tcs.TrySetResult(histories);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[API] GetMatchHistoriesByTransNo parse error: {ex.Message}");
            tcs.TrySetResult(null);
        }
    }

    #endregion

    #region [====== LuckyDrawManager ]

    public Task<LuckyDrawResponse> LuckyDrawAsync(int playerId)
    {
        var tcs = new TaskCompletionSource<LuckyDrawResponse>();
        StartCoroutine(LuckyDrawCoroutine(playerId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    public Task<LuckyDrawAfterMatchReward> LuckyDrawAfterMatchAsync(int playerId)
    {
        var tcs = new TaskCompletionSource<LuckyDrawAfterMatchReward>();
        StartCoroutine(LuckyDrawAfterMatchCoroutine(playerId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    private IEnumerator LuckyDrawCoroutine(int playerId, Action<LuckyDrawResponse> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/draw-reward";
        var data = new LuckyDrawRequest { playerId = playerId };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<LuckyDrawResponse>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    private IEnumerator LuckyDrawAfterMatchCoroutine(int playerId, Action<LuckyDrawAfterMatchReward> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/lucky-draw";
        var data = new LuckyDrawAfterMatchRequest { playerId = playerId.ToString() };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(json);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<LuckyDrawAfterMatchReward>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    public Task<List<LuckyDrawItem>> GetDailyLuckyDrawItemsAsync()
    {
        var tcs = new TaskCompletionSource<List<LuckyDrawItem>>();
        StartCoroutine(GetDailyLuckyDrawItemsCoroutine(r => tcs.SetResult(r)));
        return tcs.Task;
    }

    private IEnumerator GetDailyLuckyDrawItemsCoroutine(Action<List<LuckyDrawItem>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/draw-reward/list-today";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        List<LuckyDrawItem> items = JsonHelper.FromJson<LuckyDrawItem>(req.downloadHandler.text);
        onSuccess?.Invoke(items);
    }

    public Task<List<LuckyDrawResult>> GetPlayerDailyDrawHistoryAsync(int playerId)
    {
        var tcs = new TaskCompletionSource<List<LuckyDrawResult>>();
        StartCoroutine(GetPlayerDailyDrawHistoryCoroutine(playerId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    private IEnumerator GetPlayerDailyDrawHistoryCoroutine(int playerId, Action<List<LuckyDrawResult>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/draw-reward/history/{playerId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        List<LuckyDrawResult> results = JsonHelper.FromJson<LuckyDrawResult>(req.downloadHandler.text);
        onSuccess?.Invoke(results);
    }

    public Task<List<RewardLocation>> GetRewardLocationsAsync(int rewardType, int playerId)
    {
        var tcs = new TaskCompletionSource<List<RewardLocation>>();
        StartCoroutine(GetRewardLocationsCoroutine(rewardType, playerId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    private IEnumerator GetRewardLocationsCoroutine(int rewardType, int playerId, Action<List<RewardLocation>> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/rewards?rewardType={rewardType}&playerId={playerId}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        List<RewardLocation> locations = null;
        try
        {
            locations = JsonHelper.FromJson<RewardLocation>(req.downloadHandler.text);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
            yield break;
        }

        onSuccess?.Invoke(locations ?? new List<RewardLocation>());
    }

    public Task<RewardClaimResponse> ClaimRewardAsync(int playerId, int locationId, RewardType rewardType)
    {
        var tcs = new TaskCompletionSource<RewardClaimResponse>();
        StartCoroutine(ClaimRewardCoroutine(playerId, locationId, rewardType, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    private IEnumerator ClaimRewardCoroutine(int playerId, int locationId, RewardType rewardType, Action<RewardClaimResponse> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/rewards/claim";
        var data = new RewardClaimRequest { playerId = playerId, locationId = locationId, rewardType = ((int)rewardType).ToString() };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        Debug.Log("Response received: " + req.downloadHandler.text);

        RewardClaimResponse resp = null;
        try
        {
            resp = JsonUtility.FromJson<RewardClaimResponse>(req.downloadHandler.text);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing response: " + ex.Message);
            onSuccess?.Invoke(null);
            yield break;
        }

        onSuccess?.Invoke(resp);
    }

    public Task<bool> RefreshRewardsAsync(int playerId)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(RefreshRewardsCoroutine(playerId, r => tcs.SetResult(r)));
        return tcs.Task;
    }

    private IEnumerator RefreshRewardsCoroutine(int playerId, Action<bool> onSuccess)
    {
        string url = $"{ApiConfig.BaseUrl}/rewards/refresh";
        var data = new RefreshRewardsRequest { playerId = playerId };
        string json = JsonUtility.ToJson(data);
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(req, req.url);

        bool success;
        if (req.result != UnityWebRequest.Result.Success)
        {
            success = ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
        }
        else
        {
            Debug.Log("Rewards refreshed: " + req.downloadHandler.text);
            success = true;
        }

        onSuccess?.Invoke(success);
    }

    #endregion

    #region [====== LoginManager ]

    public Task<LoginUserModel> LoginOrCreateAccount(string idToken)
    {
        var tcs = new TaskCompletionSource<LoginUserModel>();
        StartCoroutine(LoginOrCreateAccountCoroutine(idToken, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<LoginUserModel> LoginOrCreateSocialAccount(string firebaseUid, string email, AuthenticationProviderType providerType, string avatarUrl = null)
    {
        var tcs = new TaskCompletionSource<LoginUserModel>();
        StartCoroutine(LoginOrCreateSocialAccountCoroutine(firebaseUid, email, providerType, avatarUrl, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<LoginUserModel> ConfirmPlayerName(LoginUserModel currentModel, string playerName, int companionBallItemId)
    {
        var tcs = new TaskCompletionSource<LoginUserModel>();
        StartCoroutine(ConfirmPlayerNameCoroutine(currentModel, playerName, companionBallItemId, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    public Task<LoginUserModel> CheckAccount(string idToken)
    {
        var tcs = new TaskCompletionSource<LoginUserModel>();
        StartCoroutine(CheckAccountCoroutine(idToken, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    private IEnumerator LoginOrCreateAccountCoroutine(string idToken, Action<LoginUserModel> onSuccess)
    {
        string url = ApiConfig.BaseUrl + "/login";
        var tokenData = new GoogleToken { idToken = idToken };
        string json = JsonUtility.ToJson(tokenData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Login API failed: " + request.error, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
            var model = new LoginUserModel
            {
                UserId = resp.userId,
                Username = resp.username,
                Token = resp.token,
                FriendCode = resp.friendCode,
                CreatedAt = resp.createdAt,
                LastLoginAt = resp.lastLoginAt,
                IsTutorialCompleted = resp.isTutorialCompleted
            };
            onSuccess?.Invoke(model);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing login response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    private IEnumerator LoginOrCreateSocialAccountCoroutine(string firebaseUid, string email, AuthenticationProviderType providerType, string avatarUrl, Action<LoginUserModel> onSuccess)
    {
        LastErrorMessage = null;
        LastErrorCode = 0;

        if (string.IsNullOrEmpty(firebaseUid))
        {
            ShowError("âŒ Firebase UID is missing");
            onSuccess?.Invoke(null);
            yield break;
        }

        string url = ApiConfig.BaseUrl + "/social-login";
        var requestBody = new SocialLoginRequest
        {
            firebaseUid = firebaseUid,
            providerType = providerType.ToString(),
            email = email,
            deviceId = SystemInfo.deviceUniqueIdentifier,
            avatarUrl = avatarUrl
        };

        string json = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        if (request.result != UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (request.responseCode == 409)
            {
                ShowError("âŒ TÃ i khoáº£n Ä‘ang Ä‘Äƒng nháº­p thiáº¿t bá»‹ khÃ¡c.", request.responseCode, responseText);
            }
            else
            {
                ShowError("âŒ Social login API failed: " + request.error, request.responseCode, responseText);
            }
            onSuccess?.Invoke(null);
            request.Dispose();
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<SocialLoginResponse>(request.downloadHandler.text);
            var model = CreateLoginModelFromSocialResponse(resp);
            onSuccess?.Invoke(model);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing social login response: " + ex.Message, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onSuccess?.Invoke(null);
        }

        request.Dispose();
    }

    private IEnumerator ConfirmPlayerNameCoroutine(LoginUserModel currentModel, string playerName, int companionBallItemId, Action<LoginUserModel> onSuccess)
    {
        if (currentModel == null)
        {
            ShowError("âŒ Missing current login model for confirming player name");
            onSuccess?.Invoke(null);
            yield break;
        }

        int userId = currentModel.UserId;
        string idAccount = currentModel.Token;

        if (userId <= 0)
        {
            ShowError("âŒ Invalid user id for confirming player name");
            onSuccess?.Invoke(null);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            ShowError("âŒ Player name is required");
            onSuccess?.Invoke(null);
            yield break;
        }

        if (string.IsNullOrEmpty(idAccount))
        {
            ShowError("âŒ Account identifier is missing");
            onSuccess?.Invoke(null);
            yield break;
        }

        if (companionBallItemId <= 0)
        {
            ShowError("âŒ Companion ball item id is required");
            onSuccess?.Invoke(null);
            yield break;
        }

        string url = ApiConfig.BaseUrl + "/social-login/confirm-name";
        var requestBody = new ConfirmPlayerNameRequest
        {
            id = userId,
            PlayerName = playerName,
            IdAccount = idAccount,
            CompanionBallItemId = companionBallItemId
        };

        string json = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Confirm player name API failed: " + request.error, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            request.Dispose();
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<SocialLoginResponse>(request.downloadHandler.text);
            string updatedName = !string.IsNullOrWhiteSpace(resp?.player?.PlayerName)
                ? resp.player.PlayerName
                : (!string.IsNullOrWhiteSpace(resp?.PlayerName) ? resp.PlayerName : playerName);
            currentModel.Username = updatedName;
            onSuccess?.Invoke(currentModel);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing confirm player name response: " + ex.Message, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onSuccess?.Invoke(null);
        }

        request.Dispose();
    }

    private LoginUserModel CreateLoginModelFromSocialResponse(SocialLoginResponse resp)
    {
        if (resp == null)
        {
            return null;
        }

        var player = resp.player ?? new SocialPlayer
        {
            id = resp.id,
            friendCode = resp.friendCode,
            PlayerName = resp.PlayerName,
            Level = resp.Level,
            Exp = resp.Exp,
            Body = resp.Body,
            RingBall = resp.RingBall,
            Money = resp.Money,
            TalentPoint = resp.TalentPoint,
            IdAccount = resp.IdAccount,
            IsActive = resp.IsActive,
            Email = resp.Email,
            ProviderType = resp.ProviderType,
            AvatarUrl = resp.AvatarUrl,
            isTutorialCompleted = resp.isTutorialCompleted,
            createdAt = resp.createdAt,
            lastLoginAt = resp.lastLoginAt
        };

        var tokens = resp.tokens;

        var loginModel = new LoginUserModel
        {
            UserId = player != null ? player.id : 0,
            Username = player != null ? player.PlayerName : null,
            Token = player != null ? player.IdAccount : null,
            FriendCode = player != null ? player.friendCode : null,
            AvatarUrl = player != null ? player.AvatarUrl : null,
            Level = player != null ? player.Level : 0,
            Exp = player != null ? player.Exp : 0,
            Body = player != null ? player.Body : 0,
            RingBall = player != null ? player.RingBall : 0,
            Money = player != null ? player.Money : 0,
            TalentPoint = player != null ? player.TalentPoint : 0,
            Email = player != null ? player.Email : null,
            ProviderType = player != null ? player.ProviderType : null,
            IsActive = player != null && player.IsActive,
            AccessToken = tokens?.accessToken,
            RefreshToken = tokens?.refreshToken,
            AccessTokenExpiresAt = tokens?.accessTokenExpiresAt,
            RefreshTokenExpiresAt = tokens?.refreshTokenExpiresAt,
            IsTutorialCompleted = player != null && player.isTutorialCompleted,
            CreatedAt = player != null ? player.createdAt : null,
            LastLoginAt = player != null ? player.lastLoginAt : null
        };
        Debug.Log($"LoginUserModel: UserId = {loginModel.UserId}, Username = {loginModel.Username}, Token = {loginModel.Token}");
        return loginModel;
    }

    private IEnumerator CheckAccountCoroutine(string idToken, Action<LoginUserModel> onSuccess)
    {
        string url = ApiConfig.BaseUrl + "/check-account";
        var tokenData = new GoogleToken { idToken = idToken };
        string json = JsonUtility.ToJson(tokenData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Check account API failed: " + request.error, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
            if (resp != null && resp.userId > 0)
            {
                var model = new LoginUserModel
                {
                    UserId = resp.userId,
                    Username = resp.username,
                    Token = resp.token,
                    FriendCode = resp.friendCode,
                    IsTutorialCompleted = resp.isTutorialCompleted,
                    CreatedAt = resp.createdAt,
                    LastLoginAt = resp.lastLoginAt
                };
                onSuccess?.Invoke(model);
            }
            else
            {
                onSuccess?.Invoke(null);
            }
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing check account response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    public Task<LoginUserModel> CreateAccount(string idToken, string playerName, string avatarUrl = null)
    {
        var tcs = new TaskCompletionSource<LoginUserModel>();
        StartCoroutine(CreateAccountCoroutine(idToken, playerName, avatarUrl, res => tcs.SetResult(res)));
        return tcs.Task;
    }

    private IEnumerator CreateAccountCoroutine(string idToken, string playerName, string avatarUrl, Action<LoginUserModel> onSuccess)
    {
        string url = ApiConfig.BaseUrl + "/create-account";
        var data = new CreateAccountRequest
        {
            idToken = idToken,
            playerName = playerName,
            avatarUrl = avatarUrl
        };
        string json = JsonUtility.ToJson(data);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Create account API failed: " + request.error, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onSuccess?.Invoke(null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<PlayerCreated>(request.downloadHandler.text);
            var model = new LoginUserModel
            {
                UserId = resp.id,
                Username = resp.PlayerName,
                Token = resp.IdAccount,
                FriendCode = resp.friendCode,
                IsTutorialCompleted = resp.isTutorialCompleted,
                CreatedAt = resp.createdAt,
                LastLoginAt = resp.lastLoginAt
            };
            onSuccess?.Invoke(model);
        }
        catch (Exception ex)
        {
            ShowError("âŒ Error parsing create account response: " + ex.Message);
            onSuccess?.Invoke(null);
        }
    }

    public Task<bool> CompleteTutorialAsync(int playerId)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(CompleteTutorialCoroutine(playerId, result => tcs.SetResult(result)));
        return tcs.Task;
    }

    private IEnumerator CompleteTutorialCoroutine(int playerId, Action<bool> onComplete)
    {
        string url = $"{ApiConfig.BaseUrl}/player/{playerId}/tutorial-complete";
        UnityWebRequest request = new UnityWebRequest(url, "PATCH");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return SendConfiguredWebRequest(request, request.url);

        if (request.result != UnityWebRequest.Result.Success)
        {
            ShowError("Failed to complete tutorial: " + request.error, request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : null);
            onComplete?.Invoke(false);
            request.Dispose();
            yield break;
        }

        onComplete?.Invoke(true);
        request.Dispose();
    }


    #endregion


    #region [====== WebSocketHelper ]

    public void LoadNotifications()
    {
        StartCoroutine(LoadNotificationsCoroutine());
    }

    private IEnumerator LoadNotificationsCoroutine()
    {
        string url = $"{ApiConfig.BaseUrl}/notifications";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            yield break;
        }

        // TODO: parse notification list and update UI
    }

    public void LoadMessages()
    {
        StartCoroutine(LoadMessagesCoroutine());
    }

    private IEnumerator LoadMessagesCoroutine()
    {
        string url = $"{ApiConfig.BaseUrl}/messages";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return SendConfiguredWebRequest(req, req.url);

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError("âŒ Request failed: " + req.error, req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : null);
            yield break;
        }

        // TODO: parse message list and update UI
    }

    #endregion


    [Serializable]
    private class PlayerList
    {
        public List<PlayerSchema> items;
    }

    [Serializable]
    private class MessageList
    {
        public List<MessageModel> items;
    }
    [Serializable]
    private class FriendRemovePayload
    {
        public int playerId;
        public int friendId;
    }

    [Serializable]
    private class GoogleToken
    {
        public string idToken;
    }

    [Serializable]
    private class SocialLoginRequest
    {
        public string firebaseUid;
        public string providerType;
        public string email;
        public string deviceId;
        public string avatarUrl;
    }

    [Serializable]
    private class LoginResponse
    {
        public int userId;
        public string username;
        public string token;
        public string friendCode;
        public bool isTutorialCompleted;
        public string createdAt;
        public string lastLoginAt;
    }

    [Serializable]
    private class CreateAccountRequest
    {
        public string idToken;
        public string playerName;
        public string avatarUrl;
    }
    [Serializable]
    private class PlayerCreated
    {
        public int id;
        public string PlayerName;
        public string IdAccount;
        public string friendCode;
        public bool isTutorialCompleted;
        public string createdAt;
        public string lastLoginAt;
    }

    [Serializable]
    private class SocialLoginResponse
    {
        public SocialPlayer player;
        public SocialTokens tokens;

        // Legacy fields for backward compatibility
        public int id;
        public string friendCode;
        public string PlayerName;
        public int Level;
        public int Exp;
        public int Body;
        public int RingBall;
        public int Money;
        public int TalentPoint;
        public string IdAccount;
        public bool IsActive;
        public string Email;
        public string ProviderType;
        public string AvatarUrl;
        public bool isTutorialCompleted;
        public string createdAt;
        public string lastLoginAt;
    }

    [Serializable]
    private class SocialPlayer
    {
        public int id;
        public string friendCode;
        public string PlayerName;
        public int Level;
        public int Exp;
        public int Body;
        public int RingBall;
        public int Money;
        public int TalentPoint;
        public string IdAccount;
        public bool IsActive;
        public string Email;
        public string ProviderType;
        public string AvatarUrl;
        public bool isTutorialCompleted;
        public string createdAt;
        public string lastLoginAt;
    }

    [Serializable]
    private class SocialTokens
    {
        public string accessToken;
        public string accessTokenExpiresAt;
        public string refreshToken;
        public string refreshTokenExpiresAt;
    }

    [Serializable]
    private class ConfirmPlayerNameRequest
    {
        public int id;
        public string PlayerName;
        public string IdAccount;
        public int CompanionBallItemId;
    }

    [Serializable]
    private class QueueJoinRequest
    {
        public int userId;
        public int bet;
        public string region;
        public int typeMatchGid;
        public int maxPlayers;
    }

    [Serializable]
    public class QueueJoinResponse
    {
        public string status;
        public string message;
        public string bucket;
    }

    [Serializable]
    private class QueueCancelRequest
    {
        public int userId;
    }

    [Serializable]
    private class QueueResyncRequest
    {
        public int userId;
        public string matchId;
    }

    [Serializable]
    public class QueueCancelResponse
    {
        public string status;
    }

    [Serializable]
    public class QueueResyncTicket
    {
        public string type;
        public string matchId;
        public string sessionName;
        public string region;
        public string joinToken;
        public long deadlineMs;
        public int hostPort;
        public string reason;
    }

    [Serializable]
    public class QueueResyncResponse
    {
        public string status;
        public int emittedCount;
        public string matchId;
        public string state;
        public string sessionName;
        public string region;
        public int hostPort;
        public string matchLoadingStage;
        public string characterSelections;
        public QueueResyncTicket ticket;
    }

    [Serializable]
    private class ForceStartRequest
    {
        public int userId;
        public int bet;
        public string region;
        public int typeMatchGid;
        public int maxPlayers;
    }

    [Serializable]
    private class MatchEarlyExitRequest
    {
        public string matchId;
        public int roomId;
        public int playerId;
    }

    [Serializable]
    public class ForceStartResponse
    {
        public string status;
        public string message;
    }
}

[Serializable]
public class ConfirmRewardClaimRequest
{
    public int playerId;
    public string rewardType;
    public int achievementId;
    public int locationId;
}

[Serializable]
public class RewardClaimRequest
{
    public int playerId;
    public int locationId;
    public string rewardType;
}

[Serializable]
public class RewardClaimResponse
{
    public int playerId;
    public int seq;
    public string rewardType;
    public int locationId;
    public int rewardAmount;
    public int? itemId;
    public bool isUsed;
    public string achievedAt;
}

[Serializable]
public class SystemMessageClaimResponse
{
    public bool success;
    public MessageModel message;
    public SystemMessageRewardInfo rewards;
    public LuckyDrawAfterMatchReward luckyDrawReward;
}

[Serializable]
public class SystemMessageRewardInfo
{
    public int ringBallReward;
    public int moneyReward;
    public int itemRewardId;
}

[Serializable]
public class RefreshRewardsRequest
{
    public int playerId;
}

[Serializable]
public class PlayerRoomApiResponse
{
    public PlayerRoomUser roomUser;
    public PlayerRoomInfo room;
}

[Serializable]
public class PlayerRoomUser
{
    public int id;
    public int roomId;
    public int userId;
    public string joinedAt;
    public PlayerRoomInfo room;
}

[Serializable]
public class PlayerRoomInfo
{
    public int id;
    public string roomName;
    public int maxPlayers;
    public int currentPlayers;
    public int port;
}
