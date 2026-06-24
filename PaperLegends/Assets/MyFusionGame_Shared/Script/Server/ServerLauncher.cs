#if UNITY_SERVER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

// LÃƒâ€ Ã‚Â°u ÃƒÆ’Ã‚Â½: mÃƒÆ’Ã‚Â´ hÃƒÆ’Ã‚Â¬nh mÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºi "1 container = 1 match".
// - MODE=IDLE: chÃƒÂ¡Ã‚Â»Ã¢â‚¬Â° boot + (tuÃƒÂ¡Ã‚Â»Ã‚Â³ chÃƒÂ¡Ã‚Â»Ã‚Ân) register vÃƒÂ¡Ã‚Â»Ã‚Â backend rÃƒÂ¡Ã‚Â»Ã¢â‚¬Å“i chÃƒÂ¡Ã‚Â»Ã‚Â/thoÃƒÆ’Ã‚Â¡t.
// - MODE=MATCH: StartGame(GameMode.Server) Ãƒâ€žÃ¢â‚¬ËœÃƒÆ’Ã‚Âºng 1 SessionName, bÃƒÆ’Ã‚Â¡o READY, chÃƒÂ¡Ã‚Â»Ã‚Â EndGame, bÃƒÆ’Ã‚Â¡o RESULT, shutdown + exit.
//
// ENV Ãƒâ€žÃ¢â‚¬ËœÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Â£c truyÃƒÂ¡Ã‚Â»Ã‚Ân tÃƒÂ¡Ã‚Â»Ã‚Â« Orchestrator:
//   MODE=IDLE|MATCH
//   REGION=asia
//   BACKEND_URL=http://backend:3000
//   MATCH_ID=...
//   SESSION_NAME=ASIA-XXXXXXX
//   MAX_PLAYERS=3
//   BET=100
//   TYPE_MATCH_GID=0
//   SERVER_PORT=27015 (optional)

public class ServerLauncher : MonoBehaviour
{
    // Singleton Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚Â»Ã†â€™ cÃƒÆ’Ã‚Â¡c hÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡ thÃƒÂ¡Ã‚Â»Ã¢â‚¬Ëœng khÃƒÆ’Ã‚Â¡c truy cÃƒÂ¡Ã‚ÂºÃ‚Â­p ServerLauncher hiÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡n tÃƒÂ¡Ã‚ÂºÃ‚Â¡i.
    public static ServerLauncher? Instance { get; private set; }
    // CÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¢ng UDP nÃƒÂ¡Ã‚Â»Ã¢â€žÂ¢i bÃƒÂ¡Ã‚Â»Ã¢â€žÂ¢ mÃƒÆ’Ã‚Â  server bind trong container.
    public ushort ServerPort { get; private set; }
    // CÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¢ng public/backend sÃƒÂ¡Ã‚ÂºÃ‚Â½ thÃƒÆ’Ã‚Â´ng bÃƒÆ’Ã‚Â¡o cho client kÃƒÂ¡Ã‚ÂºÃ‚Â¿t nÃƒÂ¡Ã‚Â»Ã¢â‚¬Ëœi tÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºi.
    public ushort PublicPort { get; private set; }
    // ID cÃƒÂ¡Ã‚Â»Ã‚Â§a Dedicated Server (thÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Âng do orchestrator cÃƒÂ¡Ã‚ÂºÃ‚Â¥p).
    public string DsId { get; private set; } = string.Empty;
    // MÃƒÆ’Ã‚Â£ loÃƒÂ¡Ã‚ÂºÃ‚Â¡i trÃƒÂ¡Ã‚ÂºÃ‚Â­n Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚ÂºÃ‚Â¥u (game mode/type) lÃƒÂ¡Ã‚ÂºÃ‚Â¥y tÃƒÂ¡Ã‚Â»Ã‚Â« payload hoÃƒÂ¡Ã‚ÂºÃ‚Â·c ENV.
    public int TypeMatchGid { get; private set; }
    // MÃƒÂ¡Ã‚Â»Ã‚Â©c cÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Â£c mÃƒÂ¡Ã‚Â»Ã¢â‚¬â€i ngÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Âi chÃƒâ€ Ã‚Â¡i cÃƒÂ¡Ã‚Â»Ã‚Â§a trÃƒÂ¡Ã‚ÂºÃ‚Â­n hiÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡n tÃƒÂ¡Ã‚ÂºÃ‚Â¡i.
    public int BetPerPlayer { get; private set; }
    public int MaxRound { get; private set; }

    [Serializable]
    private class SceneConfigBinding
    {
        // TÃƒÆ’Ã‚Âªn scene dÃƒÆ’Ã‚Â¹ng lÃƒÆ’Ã‚Â m key ÃƒÆ’Ã‚Â¡nh xÃƒÂ¡Ã‚ÂºÃ‚Â¡ sang cÃƒÂ¡Ã‚ÂºÃ‚Â¥u hÃƒÆ’Ã‚Â¬nh logic.
        public string sceneName = string.Empty;
        // ScriptableObject chÃƒÂ¡Ã‚Â»Ã‚Â©a cÃƒÂ¡Ã‚ÂºÃ‚Â¥u hÃƒÆ’Ã‚Â¬nh logic cho scene tÃƒâ€ Ã‚Â°Ãƒâ€ Ã‚Â¡ng ÃƒÂ¡Ã‚Â»Ã‚Â©ng.
        public SceneLogicConfig sceneConfig;
    }

    [Header("Match Prefab (server authoritative object)")]
    [SerializeField]
    // Prefab network object Ãƒâ€žÃ¢â‚¬ËœiÃƒÂ¡Ã‚Â»Ã‚Âu khiÃƒÂ¡Ã‚Â»Ã†â€™n match, spawn bÃƒÂ¡Ã‚Â»Ã…Â¸i server.
    private NetworkPrefabRef _matchGameNetworkPrefab;

    [Header("Scene Logic Configurations")]
    [SerializeField]
    // Danh sÃƒÆ’Ã‚Â¡ch ÃƒÆ’Ã‚Â¡nh xÃƒÂ¡Ã‚ÂºÃ‚Â¡ sceneName -> sceneConfig Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚Â»Ã†â€™ khÃƒÂ¡Ã‚Â»Ã…Â¸i tÃƒÂ¡Ã‚ÂºÃ‚Â¡o map/rule theo scene.
    private List<SceneConfigBinding> _sceneConfigBindings = new();

    // Host quÃƒÂ¡Ã‚ÂºÃ‚Â£n lÃƒÆ’Ã‚Â½ state gameplay phÃƒÆ’Ã‚Â­a server cho trÃƒÂ¡Ã‚ÂºÃ‚Â­n hiÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡n tÃƒÂ¡Ã‚ÂºÃ‚Â¡i.
    private GameSessionNetWork_Host? _host;
    // Helper xÃƒÂ¡Ã‚Â»Ã‚Â­ lÃƒÆ’Ã‚Â½ map/template host theo cÃƒÂ¡Ã‚ÂºÃ‚Â¥u hÃƒÆ’Ã‚Â¬nh scene.
    private ServerMapHelper? _mapHelper;

    // NetworkRunner chÃƒÆ’Ã‚Â­nh cÃƒÂ¡Ã‚Â»Ã‚Â§a Fusion cho vÃƒÆ’Ã‚Â²ng Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚Â»Ã‚Âi server trÃƒÂ¡Ã‚ÂºÃ‚Â­n.
    private NetworkRunner? _runner;
    // HTTP listener nÃƒÂ¡Ã‚Â»Ã¢â€žÂ¢i bÃƒÂ¡Ã‚Â»Ã¢â€žÂ¢ dÃƒÆ’Ã‚Â¹ng nhÃƒÂ¡Ã‚ÂºÃ‚Â­n lÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡nh assign match khi MODE=IDLE.
    private HttpListener? _assignListener;
    // Thread chÃƒÂ¡Ã‚ÂºÃ‚Â¡y vÃƒÆ’Ã‚Â²ng lÃƒÂ¡Ã‚ÂºÃ‚Â·p nhÃƒÂ¡Ã‚ÂºÃ‚Â­n request assign.
    private Thread? _assignListenerThread;
    // Lock Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚Â»Ã¢â‚¬Å“ng bÃƒÂ¡Ã‚Â»Ã¢â€žÂ¢ khi Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚Â»Ã‚Âc/ghi _pendingAssign giÃƒÂ¡Ã‚Â»Ã‚Â¯a thread listener vÃƒÆ’Ã‚Â  main thread.
    private readonly object _assignLock = new();
    // Payload assign match chÃƒÂ¡Ã‚Â»Ã‚Â Ãƒâ€žÃ¢â‚¬ËœÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Â£c main thread consume.
    private AssignMatchPayload? _pendingAssign;
    // CÃƒÂ¡Ã‚Â»Ã‚Â trÃƒÆ’Ã‚Â¡nh gÃƒÂ¡Ã‚Â»Ã‚Â­i sÃƒÂ¡Ã‚Â»Ã‚Â± kiÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡n "match started" nhiÃƒÂ¡Ã‚Â»Ã‚Âu lÃƒÂ¡Ã‚ÂºÃ‚Â§n.
    private bool _reportedMatchStarted;

    // --------- CONFIG (timeouts) ----------
    // Chu kÃƒÂ¡Ã‚Â»Ã‚Â³ poll khi chÃƒÂ¡Ã‚Â»Ã‚Â EndGame hoÃƒÂ¡Ã‚ÂºÃ‚Â·c chÃƒÂ¡Ã‚Â»Ã‚Â cÃƒÆ’Ã‚Â¡c Ãƒâ€žÃ¢â‚¬ËœiÃƒÂ¡Ã‚Â»Ã‚Âu kiÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡n kÃƒÂ¡Ã‚ÂºÃ‚Â¿t thÃƒÆ’Ã‚Âºc trÃƒÂ¡Ã‚ÂºÃ‚Â­n.
    private const float ENDGAME_POLL_INTERVAL = 0.25f;
    // ThÃƒÂ¡Ã‚Â»Ã‚Âi gian chÃƒÂ¡Ã‚Â»Ã‚Â giai Ãƒâ€žÃ¢â‚¬ËœoÃƒÂ¡Ã‚ÂºÃ‚Â¡n broadcast kÃƒÂ¡Ã‚ÂºÃ‚Â¿t quÃƒÂ¡Ã‚ÂºÃ‚Â£ EndGame ban Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚ÂºÃ‚Â§u.
    private const float ENDGAME_BROADCAST_WAIT_SECONDS = 10f;
    // ThÃƒÂ¡Ã‚Â»Ã‚Âi gian tÃƒÂ¡Ã‚Â»Ã¢â‚¬Ëœi Ãƒâ€žÃ¢â‚¬Ëœa chÃƒÂ¡Ã‚Â»Ã‚Â ACK tÃƒÂ¡Ã‚Â»Ã‚Â« client sau khi gÃƒÂ¡Ã‚Â»Ã‚Â­i kÃƒÂ¡Ã‚ÂºÃ‚Â¿t quÃƒÂ¡Ã‚ÂºÃ‚Â£.
    private const float ENDGAME_ACK_WAIT_SECONDS = 60f;
    // Timeout hard-stop trÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºc khi buÃƒÂ¡Ã‚Â»Ã¢â€žÂ¢c exit tiÃƒÂ¡Ã‚ÂºÃ‚Â¿n trÃƒÆ’Ã‚Â¬nh nÃƒÂ¡Ã‚ÂºÃ‚Â¿u shutdown treo.
    private const float FORCE_EXIT_TIMEOUT_SECONDS = 8f;
    // ThÃƒÂ¡Ã‚Â»Ã‚Âi gian delay Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚Â»Ã†â€™ shutdown graceful trÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºc khi kill tiÃƒÂ¡Ã‚ÂºÃ‚Â¿n trÃƒÆ’Ã‚Â¬nh.
    private const float GRACEFUL_SHUTDOWN_DELAY_SECONDS = 60f;
    // Timeout tÃƒÂ¡Ã‚Â»Ã‚Â± hÃƒÂ¡Ã‚Â»Ã‚Â§y khi phÃƒÆ’Ã‚Â²ng trÃƒÂ¡Ã‚Â»Ã¢â‚¬Ëœng quÃƒÆ’Ã‚Â¡ lÃƒÆ’Ã‚Â¢u (khÃƒÆ’Ã‚Â´ng cÃƒÆ’Ã‚Â²n ngÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Âi chÃƒâ€ Ã‚Â¡i).
    private const float EMPTY_ROOM_SUICIDE_TIMEOUT_SECONDS = 90f;
    // Timeout tÃƒÂ¡Ã‚Â»Ã‚Â± hÃƒÂ¡Ã‚Â»Ã‚Â§y khi match khÃƒÆ’Ã‚Â´ng cÃƒÆ’Ã‚Â³ tiÃƒÂ¡Ã‚ÂºÃ‚Â¿n triÃƒÂ¡Ã‚Â»Ã†â€™n gameplay (nghi treo).
    private const float MATCH_STUCK_SUICIDE_TIMEOUT_SECONDS = 300f;
    // KhoÃƒÂ¡Ã‚ÂºÃ‚Â£ng grace sau khi Ãƒâ€žÃ¢â‚¬ËœÃƒÆ’Ã‚Â£ yÃƒÆ’Ã‚Âªu cÃƒÂ¡Ã‚ÂºÃ‚Â§u tÃƒÂ¡Ã‚Â»Ã‚Â± hÃƒÂ¡Ã‚Â»Ã‚Â§y match bÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¹ treo trÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºc khi force exit.
    private const float MATCH_STUCK_FORCE_EXIT_GRACE_SECONDS = 30f;
    // Timeout chÃƒÂ¡Ã‚Â»Ã‚Â Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚Â»Ã‚Â§ tÃƒÆ’Ã‚Â­n hiÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡u tÃƒÂ¡Ã‚Â»Ã‚Â« client real-player ÃƒÂ¡Ã‚Â»Ã…Â¸ quickmatch.
    private const float QUICKMATCH_WAIT_REAL_PLAYERS_TIMEOUT_SECONDS = 15f;
    // Endpoint nÃƒÂ¡Ã‚Â»Ã¢â€žÂ¢i bÃƒÂ¡Ã‚Â»Ã¢â€žÂ¢ Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚Â»Ã†â€™ nhÃƒÂ¡Ã‚ÂºÃ‚Â­n assign tÃƒÂ¡Ã‚Â»Ã‚Â« orchestrator/backend.
    private const string AssignPath = "/internal/assign";

    private void Awake()
    {
        ConfigureDedicatedServerEnvironment();
        int bootTypeMatchGid = GetEnvInt("TYPE_MATCH_GID", 0);
        if (IsPaperLegendTypeMatch(bootTypeMatchGid))
            PaperLegendRuntimeState.SetPaperLegendMatch(true);

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _mapHelper = new ServerMapHelper();
        _mapHelper?.EnsureMapConfigurations(EnumerateSceneBindings());
        _mapHelper?.PrepareSceneHostTemplates(EnumerateSceneBindings(), _host);
    }

    private IEnumerator Start()
    {
        Debug.Log("ServerLauncher.Start() - Single-match Dedicated Server boot 2026-19-01");

        // ---- Read ENV / args ----
        string mode = GetEnv("MODE", "MATCH");
        string region = GetEnv("REGION", "asia");
        string backendUrl = ApiConfig.BaseUrl;
        string matchId = GetEnv("MATCH_ID", "testID");
        string sessionName = GetEnv("SESSION_NAME", "test");

        int typeMatchGid = GetEnvInt("TYPE_MATCH_GID", 0);
        if (IsPaperLegendTypeMatch(typeMatchGid))
            PaperLegendRuntimeState.SetPaperLegendMatch(true);

        int defaultMaxPlayers = PaperLegendRuntimeState.IsPaperLegendMatch
            ? PaperLegendRuntimeState.DefaultFreeForAllPlayers
            : 3;
        int maxPlayers = GetEnvInt("MAX_PLAYERS", defaultMaxPlayers);
        if (PaperLegendRuntimeState.IsPaperLegendMatch)
            maxPlayers = PaperLegendRuntimeState.ResolveMaxPlayers(maxPlayers);
        int defaultRealPlayerCount = PaperLegendRuntimeState.IsPaperLegendMatch
            ? PaperLegendRuntimeState.DefaultMinRealPlayers
            : maxPlayers;
        int bet = GetEnvInt("BET", 0);
        int maxRound = GetEnvInt("MAX_ROUND", GetEnvInt("MAX_ROUNDS", 0));
        string dsId = GetEnv("DS_ID", SystemInfo.deviceUniqueIdentifier);

        ushort port = (ushort)GetEnvInt("SERVER_PORT", 27015);
        string portStrArg = GetArg("--port");
        if (!string.IsNullOrEmpty(portStrArg) && ushort.TryParse(portStrArg, out var parsedPort))
            port = parsedPort;

        ServerPort = port;
        ushort publicPort = (ushort)GetEnvInt("HOST_PORT", port);
        PublicPort = publicPort > 0 ? publicPort : port;
        DsId = dsId;
        TypeMatchGid = typeMatchGid;
        BetPerPlayer = Mathf.Max(0, bet);
        MaxRound = ResolveMaxRound(typeMatchGid, maxRound);

        // ---- Optional UDP port validation (helpful when debugging host networking) ----
        if (!TryValidateUdpPort(port, out string portError))
        {
            Debug.LogWarning($"UDP port validation failed for {port}: {portError}. " +
                             $"Ensure Docker exposes UDP (e.g. '-p {port}:{port}/udp').");
        }
        else
        {
            Debug.Log($"UDP port {port} is available for the dedicated server.");
        }

        // ---- Dedicated server MODE=IDLE ----
        if (string.Equals(mode, "IDLE", StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"DS MODE=IDLE region={region} typeMatchGid={typeMatchGid} port={port}");

            // Optional: register this idle container to backend warm pool
            // Note: bÃƒÂ¡Ã‚ÂºÃ‚Â¡n nÃƒÆ’Ã‚Âªn truyÃƒÂ¡Ã‚Â»Ã‚Ân dsId/containerId tÃƒÂ¡Ã‚Â»Ã‚Â« Orchestrator nÃƒÂ¡Ã‚ÂºÃ‚Â¿u muÃƒÂ¡Ã‚Â»Ã¢â‚¬Ëœn quÃƒÂ¡Ã‚ÂºÃ‚Â£n lÃƒÆ’Ã‚Â½ chuÃƒÂ¡Ã‚ÂºÃ‚Â©n.
            if (!string.IsNullOrWhiteSpace(backendUrl))
            {
                var payload = new DsRegisterPayload
                {
                    dsId = dsId,
                    region = region,
                    status = "IDLE"
                };
                yield return PostJson($"{backendUrl}/internal/ds/register", payload);
            }

            float idleTtl = GetEnvFloat("IDLE_TTL_SECONDS", 0f);
            int assignPort = GetEnvInt("DS_INTERNAL_HTTP_PORT", 8080);
            StartAssignListener(assignPort);
            Debug.Log($"Waiting assign on http://*:{assignPort}{AssignPath}");

            float startTime = Time.realtimeSinceStartup;
            while (true)
            {
                AssignMatchPayload? assignedMatch = null;
                lock (_assignLock)
                {
                    if (_pendingAssign != null)
                    {
                        assignedMatch = _pendingAssign;
                        _pendingAssign = null;
                    }
                }

                if (assignedMatch != null)
                {
                    Debug.Log($"Assigned match {assignedMatch.matchId} -> session {assignedMatch.sessionName}");
                    StopAssignListener();
                    yield return RunMatch(assignedMatch, backendUrl, port, dsId, region);
                    yield break;
                }

                if (idleTtl > 0f && Time.realtimeSinceStartup - startTime >= idleTtl)
                {
                    Debug.Log($"IDLE_TTL_SECONDS={idleTtl}, exiting idle server.");
                    StopAssignListener();
                    Application.Quit();
                    yield return new WaitForSeconds(1f);
                    Environment.Exit(0);
                }

                yield return null;
            }

            yield break;
        }

        // ---- Dedicated server MODE=MATCH ----
        var matchPayload = new AssignMatchPayload
        {
            matchId = matchId,
            sessionName = sessionName,
            maxPlayers = maxPlayers,
            realPlayerCount = GetEnvInt("REAL_PLAYER_COUNT", defaultRealPlayerCount),
            bet = bet,
            region = region,
            typeMatchGid = typeMatchGid,
            maxRound = maxRound,
            characterSelectionsCsv = GetEnv("PAPER_LEGENDS_CHARACTER_SELECTIONS", string.Empty),
            botCharacterModelIdsCsv = GetEnv("PAPER_LEGENDS_BOT_CHARACTER_MODEL_IDS", string.Empty)
        };

        yield return RunMatch(matchPayload, backendUrl, port, dsId, region);
    }

    private static int ResolveMaxRound(int typeMatchGid, int requestedMaxRound)
    {
        if (typeMatchGid == (int)global::TypeMatchGid.MatchRandomRank)
            return 6;

        if (typeMatchGid == (int)global::TypeMatchGid.MatchRoom)
            return Mathf.Clamp(requestedMaxRound > 0 ? requestedMaxRound : 5, 5, 10);

        return requestedMaxRound > 0 ? requestedMaxRound : 6;
    }

    private static bool IsPaperLegendTypeMatch(int typeMatchGid)
    {
        return typeMatchGid == 10000002;
    }

    private static void ApplyPaperLegendCharacterSelections(string selectionsCsv, string botCharacterModelIdsCsv)
    {
        PaperLegendRuntimeState.ClearCharacterSelections();

        var reservedBotModels = new List<int>();
        var reservedBotModelSet = new HashSet<int>();
        if (!string.IsNullOrWhiteSpace(botCharacterModelIdsCsv))
        {
            foreach (string rawModelId in botCharacterModelIdsCsv.Split(','))
            {
                if (int.TryParse(rawModelId.Trim(), out int modelId) && modelId > 0)
                    AddReservedBotModel(modelId);
            }
        }

        if (!string.IsNullOrWhiteSpace(selectionsCsv))
        {
            foreach (string rawPair in selectionsCsv.Split(','))
            {
                string pair = rawPair.Trim();
                if (string.IsNullOrEmpty(pair))
                    continue;

                string[] parts = pair.Split(':');
                if (parts.Length != 2)
                    continue;

                if (!int.TryParse(parts[0].Trim(), out int playerId) ||
                    !int.TryParse(parts[1].Trim(), out int modelId) ||
                    modelId <= 0)
                {
                    continue;
                }

                if (playerId > 0)
                    PaperLegendRuntimeState.SetSelectedCharacterModel(playerId, modelId);
                else
                    AddReservedBotModel(modelId);
            }
        }

        PaperLegendRuntimeState.SetReservedBotCharacterModels(reservedBotModels);
        Debug.Log($"[PaperLegends] Applied socket character selections. realSelections='{selectionsCsv}', botModels='{string.Join(",", reservedBotModels)}'.");

        void AddReservedBotModel(int modelId)
        {
            if (modelId > 0 && reservedBotModelSet.Add(modelId))
                reservedBotModels.Add(modelId);
        }
    }

    private IEnumerator RunMatch(AssignMatchPayload payload, string backendUrl, ushort port, string dsId, string fallbackRegion)
    {
        if (payload == null)
        {
            Debug.LogError("Missing match payload, aborting.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(payload.sessionName) || string.IsNullOrWhiteSpace(payload.matchId))
        {
            Debug.LogError("Missing matchId/sessionName. Orchestrator must set match data.");
            yield break;
        }

        string region = string.IsNullOrWhiteSpace(payload.region) ? fallbackRegion : payload.region;
        int typeMatchGid = payload.typeMatchGid;
        if (IsPaperLegendTypeMatch(typeMatchGid))
            PaperLegendRuntimeState.SetPaperLegendMatch(true);

        int defaultMaxPlayers = PaperLegendRuntimeState.IsPaperLegendMatch
            ? PaperLegendRuntimeState.DefaultFreeForAllPlayers
            : 3;
        int maxPlayers = payload.maxPlayers > 0 ? payload.maxPlayers : GetEnvInt("MAX_PLAYERS", defaultMaxPlayers);
        if (PaperLegendRuntimeState.IsPaperLegendMatch)
            maxPlayers = PaperLegendRuntimeState.ResolveMaxPlayers(maxPlayers);

        int defaultRealPlayerCount = PaperLegendRuntimeState.IsPaperLegendMatch
            ? PaperLegendRuntimeState.DefaultMinRealPlayers
            : maxPlayers;
        int realPlayerCount = payload.realPlayerCount > 0 ? payload.realPlayerCount : GetEnvInt("REAL_PLAYER_COUNT", defaultRealPlayerCount);
        if (PaperLegendRuntimeState.IsPaperLegendMatch)
            realPlayerCount = PaperLegendRuntimeState.ResolveMinRealPlayers(realPlayerCount, maxPlayers);
        if (PaperLegendRuntimeState.IsPaperLegendMatch)
            ApplyPaperLegendCharacterSelections(payload.characterSelectionsCsv, payload.botCharacterModelIdsCsv);
        int bet = payload.bet;
        BetPerPlayer = Mathf.Max(0, bet);
        MaxRound = ResolveMaxRound(typeMatchGid, payload.maxRound);

        Debug.Log($"DS MODE=MATCH matchId={payload.matchId} session={payload.sessionName} region={region} maxPlayers={maxPlayers} realPlayers={realPlayerCount} bet={bet} typeMatchGid={typeMatchGid} port={port}");

        // Photon settings
        var customSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
        customSettings.FixedRegion = region;
        customSettings.AppVersion = PhotonAppSettings.Global.AppSettings.AppVersion;
        
        // Critical: Ensure EnableLobbyStatistics is false for dedicated servers
        customSettings.EnableLobbyStatistics = false;
        
        Debug.Log($"Using Photon region: {customSettings.FixedRegion} | AppVersion={customSettings.AppVersion}");
        Debug.Log($"Photon AppId: {(string.IsNullOrEmpty(customSettings.AppIdFusion) ? "MISSING!" : "Set")}");
        Debug.Log($"Network Config: Protocol={customSettings.Protocol}, Port={customSettings.Port}");

        // Cleanup any existing runner instances (e.g., from container reuse)
        if (_runner != null)
        {
            Debug.LogWarning("Found existing runner instance, cleaning up...");
            if (_runner.IsRunning)
            {
                _runner.Shutdown(shutdownReason: ShutdownReason.DisconnectedByPluginLogic);
                // Wait for shutdown to complete
                float shutdownWait = 0f;
                while (_runner.IsRunning && shutdownWait < 2f)
                {
                    shutdownWait += Time.deltaTime;
                    yield return null;
                }
            }
            if (_runner != null && _runner.gameObject != null)
            {
                Destroy(_runner.gameObject);
            }
            _runner = null;
            // Additional wait to ensure complete cleanup
            yield return new WaitForSeconds(0.5f);
        }

        // Also cleanup any orphaned runners in the scene
#if UNITY_2023_1_OR_NEWER
        var orphanedRunners = FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);
#else
        var orphanedRunners = FindObjectsOfType<NetworkRunner>();
#endif
        if (orphanedRunners != null && orphanedRunners.Length > 0)
        {
            Debug.LogWarning($"Found {orphanedRunners.Length} orphaned runner(s), cleaning up...");
            foreach (var orphan in orphanedRunners)
            {
                if (orphan != null)
                {
                    if (orphan.IsRunning)
                    {
                        orphan.Shutdown(shutdownReason: ShutdownReason.DisconnectedByPluginLogic);
                    }
                    if (orphan != null && orphan.gameObject != null)
                    {
                        Destroy(orphan.gameObject);
                    }
                }
            }
            yield return new WaitForSeconds(0.5f);
        }

        // Session properties for browser/debug
        var props = new Dictionary<string, SessionProperty>
        {
            { "MatchId", (SessionProperty)payload.matchId },
            { "Bet", (SessionProperty)bet },
            { "TypeMatchGid", (SessionProperty)typeMatchGid }
        };

        // Start Fusion Server session
        // VÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºi GameMode.Server, Fusion vÃƒÂ¡Ã‚ÂºÃ‚Â«n tÃƒÂ¡Ã‚ÂºÃ‚Â¡o player Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚ÂºÃ‚Â¡i diÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡n server (thÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Âng id=1024),
        // nÃƒÆ’Ã‚Âªn PlayerCount cÃƒÂ¡Ã‚ÂºÃ‚Â§n >= sÃƒÂ¡Ã‚Â»Ã¢â‚¬Ëœ client mong muÃƒÂ¡Ã‚Â»Ã¢â‚¬Ëœn + 1 slot cho server.
        // TrÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºc Ãƒâ€žÃ¢â‚¬ËœÃƒÆ’Ã‚Â¢y Ãƒâ€žÃ¢â‚¬ËœÃƒÆ’Ã‚Â£ bÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¹ hard-code =2 nÃƒÆ’Ã‚Âªn trÃƒÂ¡Ã‚ÂºÃ‚Â­n 2 ngÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Âi sÃƒÂ¡Ã‚ÂºÃ‚Â½ bÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¹ full ngay khi client thÃƒÂ¡Ã‚Â»Ã‚Â© 2 join.
        int fusionPlayerCapacity = Mathf.Max(2, maxPlayers + 1);
        const int maxStartAttempts = 3;
        const float startGameTimeout = 15f;
        StartGameResult startResult = default;
        bool startSucceeded = false;

        for (int startAttempt = 1; startAttempt <= maxStartAttempts; startAttempt++)
        {
            if (_runner != null && _runner.gameObject != null)
            {
                if (_runner.IsRunning)
                {
                    var shutdownExisting = _runner.Shutdown();
                    while (!shutdownExisting.IsCompleted)
                    {
                        yield return null;
                    }
                }

                Destroy(_runner.gameObject);
                _runner = null;
                yield return null;
            }

            var runnerGO = new GameObject("Runner_SingleMatch");
            DontDestroyOnLoad(runnerGO);

            _runner = runnerGO.AddComponent<NetworkRunner>();
            _runner.ProvideInput = false;

            var sceneManager = runnerGO.AddComponent<NetworkSceneManagerDefault>();

            var startArgs = new StartGameArgs
            {
                GameMode = GameMode.Server,
                SessionName = payload.sessionName,
                PlayerCount = fusionPlayerCapacity,
                CustomPhotonAppSettings = customSettings,
                SessionProperties = props,
                SceneManager = sceneManager,

                // ChÃƒÂ¡Ã‚ÂºÃ‚Â¡y DS trong Docker vÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºi publish port (hostPort != containerPort) cÃƒÆ’Ã‚Â³ thÃƒÂ¡Ã‚Â»Ã†â€™ khiÃƒÂ¡Ã‚ÂºÃ‚Â¿n
                // endpoint reflexive bÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¹ lÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡ch (client nhÃƒÆ’Ã‚Â¬n thÃƒÂ¡Ã‚ÂºÃ‚Â¥y :27015 thay vÃƒÆ’Ã‚Â¬ hostPort thÃƒÂ¡Ã‚Â»Ã‚Â±c tÃƒÂ¡Ã‚ÂºÃ‚Â¿).
                // ÃƒÆ’Ã¢â‚¬Â°p Ãƒâ€žÃ¢â‚¬Ëœi relay qua Photon Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚Â»Ã†â€™ trÃƒÆ’Ã‚Â¡nh direct-connect nhÃƒÂ¡Ã‚ÂºÃ‚Â§m endpoint vÃƒÆ’Ã‚Â  lÃƒÂ¡Ã‚Â»Ã¢â‚¬â€i GameIsFull giÃƒÂ¡Ã‚ÂºÃ‚Â£.
                DisableNATPunchthrough = true,

                // Bind UDP
                Address = NetAddress.CreateFromIpPort("0.0.0.0", port),
            };

            Debug.Log($"Starting Fusion Server: Session={payload.sessionName}, Players={fusionPlayerCapacity}, Port={port}, PublicPort={PublicPort}, DisableNATPunchthrough={startArgs.DisableNATPunchthrough}, Attempt={startAttempt}/{maxStartAttempts}");

            var startTask = _runner.StartGame(startArgs);
            float startElapsed = 0f;
            while (!startTask.IsCompleted && startElapsed < startGameTimeout)
            {
                startElapsed += Time.deltaTime;
                yield return null;
            }

            if (!startTask.IsCompleted)
            {
                Debug.LogWarning($"StartGame timeout after {startGameTimeout}s (attempt {startAttempt}/{maxStartAttempts})");
                if (startAttempt < maxStartAttempts)
                {
                    yield return new WaitForSeconds(1.0f * startAttempt);
                    continue;
                }

                Debug.LogError($"StartGame timeout after {maxStartAttempts} attempts");
                yield break;
            }

            startResult = startTask.Result;
            if (startResult.Ok)
            {
                startSucceeded = true;
                break;
            }

            bool isDuplicateMatchContainer =
                startResult.ShutdownReason == ShutdownReason.ServerInRoom ||
                string.Equals(startResult.ErrorMessage, "ServerAlreadyInRoom", StringComparison.OrdinalIgnoreCase);

            if (isDuplicateMatchContainer)
            {
                Debug.LogWarning($"Duplicate MATCH container detected for session '{payload.sessionName}'. Another server is already hosting this room. Exiting this container gracefully.");

                if (_runner != null)
                {
                    var shutdownTask = _runner.Shutdown();
                    while (!shutdownTask.IsCompleted)
                    {
                        yield return null;
                    }
                }

                yield return ReleaseServerPortBeforeExit(backendUrl);

                Application.Quit();
                yield return new WaitForSeconds(0.5f);
                Environment.Exit(0);
                yield break;
            }

            bool isTransientPluginDisconnect =
                string.Equals(startResult.ShutdownReason.ToString(), "DisconnectedByPluginLogic", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(startResult.ErrorMessage) &&
                 startResult.ErrorMessage.IndexOf("DisconnectedByPluginLogic", StringComparison.OrdinalIgnoreCase) >= 0);

            if (isTransientPluginDisconnect && startAttempt < maxStartAttempts)
            {
                Debug.LogWarning($"StartGame transient failure ({startResult.ShutdownReason}) on attempt {startAttempt}/{maxStartAttempts}. Retrying...");
                yield return new WaitForSeconds(1.0f * startAttempt);
                continue;
            }

            Debug.LogError($"StartGame failed: {startResult.ShutdownReason}");
            if (!string.IsNullOrEmpty(startResult.ErrorMessage))
            {
                Debug.LogError($"   Error details: {startResult.ErrorMessage}");
            }
            Debug.LogError($"   Verify Photon AppId is configured in PhotonAppSettings and network connectivity is available.");
            yield break;
        }

        if (!startSucceeded)
        {
            Debug.LogError($"StartGame failed after {maxStartAttempts} attempts. Last reason: {startResult.ShutdownReason}");
            yield break;
        }

        if (PaperLegendRuntimeState.IsPaperLegendMatch)
        {
            string sceneName = ResolveMatchSceneName();
            if (!ValidatePaperLegendMapSpawnConfig(sceneName, out var mapConfigError))
            {
                Debug.LogError(mapConfigError);
                yield break;
            }
        }

        if (!SpawnMatchGameNetworkController(_runner, maxPlayers, realPlayerCount))
        {
            Debug.LogError("Unable to spawn MatchGameNetworkController. QuickMatchServer will not be available.");
            yield break;
        }

        Debug.Log($"DS READY session={payload.sessionName}");

        // Notify backend READY -> backend phÃƒÆ’Ã‚Â¡t ticket cho client
        if (!string.IsNullOrWhiteSpace(backendUrl))
        {
            var readyPayload = new MatchReadyPayload
            {
                matchId = payload.matchId,
                sessionName = payload.sessionName,
                region = region,
                dsId = dsId,
                hostPort = PublicPort
            };
            yield return PostJson($"{backendUrl}/internal/match/ready", readyPayload);
        }

        _reportedMatchStarted = false;
        StartCoroutine(NotifyMatchStartedWhenPlayerJoins(_runner, backendUrl, payload.matchId));

        // Ensure game host exists if needed (depends on your scene flow)
        // NÃƒÂ¡Ã‚ÂºÃ‚Â¿u game host Ãƒâ€žÃ¢â‚¬ËœÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Â£c spawn bÃƒÂ¡Ã‚Â»Ã…Â¸i scene template, bÃƒÂ¡Ã‚ÂºÃ‚Â¡n khÃƒÆ’Ã‚Â´ng cÃƒÂ¡Ã‚ÂºÃ‚Â§n gÃƒÂ¡Ã‚Â»Ã‚Âi.
        // NÃƒÂ¡Ã‚ÂºÃ‚Â¿u cÃƒÂ¡Ã‚ÂºÃ‚Â§n, bÃƒÂ¡Ã‚ÂºÃ‚Â­t dÃƒÆ’Ã‚Â²ng dÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºi:
        // EnsureGameRuleHost();

        // Wait EndGame -> Report result -> Shutdown -> Exit
        float matchAssignedRealtime = Time.realtimeSinceStartup;
        yield return WaitForEndGameThenReportAndExit(_runner, backendUrl, payload.matchId, matchAssignedRealtime);
    }

    // -------------------- ENDGAME: report & exit --------------------

    private IEnumerator WaitForEndGameThenReportAndExit(NetworkRunner runner, string backendUrl, string matchId, float matchAssignedRealtime)
    {
        // Wait until Host singleton exists
        while (GameSessionNetWork_Host.Instance == null)
        {
            if (runner == null || !runner.IsRunning || runner.IsShutdown)
            {
                Debug.LogWarning("Runner stopped before GameSessionNetWork_Host was initialized. Shutting down.");
                yield return ForceShutdownAndExit(runner, backendUrl);
                yield break;
            }

            var quickMatchServer = QuickMatchServer.Instance;
            if (quickMatchServer != null &&
                quickMatchServer.IsWaitingForAssignedRealPlayers &&
                quickMatchServer.WaitingForAssignedRealPlayersDurationSeconds >= QUICKMATCH_WAIT_REAL_PLAYERS_TIMEOUT_SECONDS)
            {
                if (quickMatchServer.TryStartWithConnectedPlayersAfterAssignedPlayerTimeout())
                {
                    yield return null;
                    continue;
                }

                Debug.LogWarning(
                    $"[QuickMatch] Did not receive all assigned client signals within {QUICKMATCH_WAIT_REAL_PLAYERS_TIMEOUT_SECONDS:0}s. " +
                    "Treating the room connection as failed, kicking all players, and shutting down the server.");
                yield return ForceShutdownAndExit(runner, backendUrl);
                yield break;
            }

            yield return null;
        }

        Debug.Log("Waiting for EndGame signal...");
        GameSessionNetWork_Host.Instance.MarkMatchProgress("MatchAssigned");
        float emptyDuration = 0f;
        bool requestedEmptyShutdown = false;
        float waitingForEndGameDuration = 0f;
        bool requestedStuckShutdown = false;
        while (!GameSessionNetWork_Host.Instance.IsGameEnded)
        {
            waitingForEndGameDuration += ENDGAME_POLL_INTERVAL;
            var hostgame = GameSessionNetWork_Host.Instance;
            float matchElapsedSinceAssign = Mathf.Max(0f, Time.realtimeSinceStartup - matchAssignedRealtime);
            float secondsSinceProgress = hostgame != null
                ? Mathf.Min(hostgame.SecondsSinceLastMatchProgress, matchElapsedSinceAssign)
                : matchElapsedSinceAssign;

            if (runner != null && runner.IsRunning && !runner.IsShutdown)
            {
                if (GetClientActivePlayerCount(runner) > 0)
                {
                    emptyDuration = 0f;
                    requestedEmptyShutdown = false;
                }
                else
                {
                    emptyDuration += ENDGAME_POLL_INTERVAL;
                    if (!requestedEmptyShutdown && emptyDuration >= EMPTY_ROOM_SUICIDE_TIMEOUT_SECONDS)
                    {
                        Debug.LogWarning("No players remained in the room for the configured timeout. Ending the match automatically.");
                        GameSessionNetWork_Host.Instance.ForceAbandonedRoomDueToNoPlayers();
                        requestedEmptyShutdown = true;
                    }
                }
            }

            if (!requestedStuckShutdown && secondsSinceProgress >= MATCH_STUCK_SUICIDE_TIMEOUT_SECONDS)
            {
                Debug.LogWarning($"Match made no gameplay progress for {MATCH_STUCK_SUICIDE_TIMEOUT_SECONDS:0}s. Requesting self-abandon to avoid a stuck server.");
                GameSessionNetWork_Host.Instance.ForceAbandonedRoomDueToNoPlayers();
                requestedStuckShutdown = true;
            }

            if (requestedStuckShutdown &&
                secondsSinceProgress >= MATCH_STUCK_SUICIDE_TIMEOUT_SECONDS + MATCH_STUCK_FORCE_EXIT_GRACE_SECONDS &&
                !GameSessionNetWork_Host.Instance.IsGameEnded)
            {
                Debug.LogError($"Match is still not ended {MATCH_STUCK_FORCE_EXIT_GRACE_SECONDS:0}s after self-abandon was requested. Forcing server shutdown.");
                yield return ForceShutdownAndExit(runner, backendUrl);
                yield break;
            }

            yield return new WaitForSeconds(ENDGAME_POLL_INTERVAL);
        }

        Debug.Log("EndGame detected. Building result payload...");

        var host = GameSessionNetWork_Host.Instance;
        if (host != null)
        {
            // ChÃƒÂ¡Ã‚Â»Ã‚Â cho Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚ÂºÃ‚Â¿n khi HandleEndGameAndBroadcastRoutine hoÃƒÆ’Ã‚Â n thÃƒÆ’Ã‚Â nh.
            // KhÃƒÆ’Ã‚Â´ng dÃƒÆ’Ã‚Â¹ng timeout cÃƒÂ¡Ã‚Â»Ã‚Â©ng ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â nÃƒÂ¡Ã‚ÂºÃ‚Â¿u vÃƒÂ¡Ã‚ÂºÃ‚Â«n Ãƒâ€žÃ¢â‚¬Ëœang xÃƒÂ¡Ã‚Â»Ã‚Â­ lÃƒÆ’Ã‚Â½, phÃƒÂ¡Ã‚ÂºÃ‚Â£i chÃƒÂ¡Ã‚Â»Ã‚Â.
            float waitElapsed = 0f;
            while (host.IsProcessingEndGame && waitElapsed < ENDGAME_BROADCAST_WAIT_SECONDS)
            {
                waitElapsed += ENDGAME_POLL_INTERVAL;
                yield return new WaitForSeconds(ENDGAME_POLL_INTERVAL);
            }

            if (host.IsProcessingEndGame)
            {
                Debug.LogWarning("HandleEndGameAndBroadcastRoutine is still running after broadcast timeout. Waiting up to 30 more seconds...");
                float extendedWait = 0f;
                while (host.IsProcessingEndGame && extendedWait < 30f)
                {
                    extendedWait += ENDGAME_POLL_INTERVAL;
                    yield return new WaitForSeconds(ENDGAME_POLL_INTERVAL);
                }
            }

            if (!host.HasBroadcastGameOverResults)
            {
                Debug.LogWarning("EndGame results were NOT broadcast to clients. Proceeding with shutdown.");
            }
        }

        // POST kÃƒÂ¡Ã‚ÂºÃ‚Â¿t quÃƒÂ¡Ã‚ÂºÃ‚Â£ lÃƒÆ’Ã‚Âªn WEB_SERVER NGAY LÃƒÂ¡Ã‚ÂºÃ‚Â¬P TÃƒÂ¡Ã‚Â»Ã‚Â¨C ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â trÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºc khi chÃƒÂ¡Ã‚Â»Ã‚Â ACK.
        // LÃƒÆ’Ã‚Â½ do: nÃƒÂ¡Ã‚ÂºÃ‚Â¿u Fusion RPC khÃƒÆ’Ã‚Â´ng Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚ÂºÃ‚Â¿n client (mÃƒÂ¡Ã‚ÂºÃ‚Â¥t kÃƒÂ¡Ã‚ÂºÃ‚Â¿t nÃƒÂ¡Ã‚Â»Ã¢â‚¬Ëœi, network issue...),
        // WEB_SERVER sÃƒÂ¡Ã‚ÂºÃ‚Â½ emit match:finished qua socket lÃƒÆ’Ã‚Â m backup.
        // NÃƒÂ¡Ã‚ÂºÃ‚Â¾U chÃƒÂ¡Ã‚Â»Ã‚Â ACK trÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºc (15s timeout) mÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºi POST ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ client phÃƒÂ¡Ã‚ÂºÃ‚Â£i Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚Â»Ã‚Â£i 15-22s mÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºi nhÃƒÂ¡Ã‚ÂºÃ‚Â­n Ãƒâ€žÃ¢â‚¬ËœÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Â£c kÃƒÂ¡Ã‚ÂºÃ‚Â¿t quÃƒÂ¡Ã‚ÂºÃ‚Â£.
        // GÃƒÂ¡Ã‚Â»Ã‚Â­i ngay ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ socket backup Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚ÂºÃ‚Â¿n client trong 1-2s thay vÃƒÆ’Ã‚Â¬ 22s.
        var results = GameSessionNetWork_Host.Instance.LastOverGameResults ?? new List<OverGameRequest>();
        var dto = new List<OverGameResultDto>(results.Count);

        foreach (var r in results)
        {
            dto.Add(new OverGameResultDto
            {
                playerId = r.playerId,
                tunrOrder = r.tunrOrder,
                typeMatchGid = r.typeMatchGid,
                StatusWin = r.StatusWin,
                rounds = r.rounds,
                MapGame = r.MapGame,
                MaxPlayer = r.MaxPlayer,
                marbBet = r.marbBet,
                marblesWon = r.marblesWon,
                marblesLost = r.marblesLost,
                expGained = r.expGained,
                playerName = r.playerName,
                description = r.description,
                avatarUrl = r.avatarUrl
            });
        }

        if (!string.IsNullOrWhiteSpace(backendUrl))
        {
            var payload = new MatchResultPayload
            {
                matchId = matchId,
                result = new MatchResultData
                {
                    endedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    overGameResults = dto
                }
            };

            yield return PostJsonWithRetry($"{backendUrl}/internal/match/result", payload, maxRetries: 4, baseDelaySeconds: 2f);
        }

        Debug.Log("Result reported to WEB_SERVER (socket backup sent).");

        // ChÃƒÂ¡Ã‚Â»Ã‚Â ACK tÃƒÂ¡Ã‚Â»Ã‚Â« client ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â CHÃƒÂ¡Ã‚Â»Ã‹â€  gate shutdown, KHÃƒÆ’Ã¢â‚¬ÂNG gate viÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡c gÃƒÂ¡Ã‚Â»Ã‚Â­i kÃƒÂ¡Ã‚ÂºÃ‚Â¿t quÃƒÂ¡Ã‚ÂºÃ‚Â£.
        // NÃƒÂ¡Ã‚ÂºÃ‚Â¿u client Ãƒâ€žÃ¢â‚¬ËœÃƒÆ’Ã‚Â£ nhÃƒÂ¡Ã‚ÂºÃ‚Â­n RPC vÃƒÆ’Ã‚Â  ACK ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ shutdown nhanh.
        // NÃƒÂ¡Ã‚ÂºÃ‚Â¿u client khÃƒÆ’Ã‚Â´ng ACK (mÃƒÂ¡Ã‚ÂºÃ‚Â¥t kÃƒÂ¡Ã‚ÂºÃ‚Â¿t nÃƒÂ¡Ã‚Â»Ã¢â‚¬Ëœi) ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ timeout rÃƒÂ¡Ã‚Â»Ã¢â‚¬Å“i shutdown, client vÃƒÂ¡Ã‚ÂºÃ‚Â«n cÃƒÆ’Ã‚Â³ kÃƒÂ¡Ã‚ÂºÃ‚Â¿t quÃƒÂ¡Ã‚ÂºÃ‚Â£ qua socket.
        if (host != null && host.HasBroadcastGameOverResults)
        {
            float ackWaitElapsed = 0f;
            float nextAckLogAt = 1f;
            while ((!host.AreAllClientsGameOverAcked || !host.AreAllClientsReadyToDisconnect) &&
                   ackWaitElapsed < ENDGAME_ACK_WAIT_SECONDS)
            {
                ackWaitElapsed += ENDGAME_POLL_INTERVAL;
                if (ackWaitElapsed >= nextAckLogAt)
                {
                    Debug.Log($"[SERVER] Waiting for client ACK/ready-to-disconnect GameOver... ({ackWaitElapsed:0.0}s/{ENDGAME_ACK_WAIT_SECONDS:0.0}s)");
                    nextAckLogAt += 1f;
                }

                yield return new WaitForSeconds(ENDGAME_POLL_INTERVAL);
            }

            if (!host.AreAllClientsGameOverAcked || !host.AreAllClientsReadyToDisconnect)
            {
                Debug.LogWarning("[SERVER] Timed out waiting for client GameOver ACK/ready-to-disconnect. Continuing shutdown.");
            }
            else
            {
                Debug.Log("[SERVER] All clients ACKed and are ready to disconnect after GameOver.");
            }
        }

        Debug.Log("Waiting before graceful shutdown...");
        yield return WaitForGracefulShutdown(runner);
        yield return ReleaseServerPortBeforeExit(backendUrl);

        Debug.Log("Exiting process (container will stop).");

        // Ensure exit on Linux headless
        float t = 0f;
        Application.Quit();
        while (t < FORCE_EXIT_TIMEOUT_SECONDS)
        {
            t += Time.deltaTime;
            yield return null;
        }
        Environment.Exit(0);
    }

    private IEnumerator ForceShutdownAndExit(NetworkRunner runner, string backendUrl)
    {
        if (runner != null && runner.IsRunning && !runner.IsShutdown)
        {
            Debug.Log("Force shutdown runner (stuck match watchdog)...");
            var shutdown = runner.Shutdown();
            while (!shutdown.IsCompleted)
            {
                yield return null;
            }
        }

        yield return ReleaseServerPortBeforeExit(backendUrl);

        Application.Quit();
        yield return new WaitForSeconds(0.5f);
        Environment.Exit(0);
    }

    private IEnumerator NotifyMatchStartedWhenPlayerJoins(NetworkRunner runner, string backendUrl, string matchId)
    {
        if (runner == null || string.IsNullOrWhiteSpace(backendUrl) || string.IsNullOrWhiteSpace(matchId))
        {
            yield break;
        }

        while (!_reportedMatchStarted && runner.IsRunning)
        {
            int clientPlayerCount = GetClientActivePlayerCount(runner);
            if (clientPlayerCount > 0)
            {
                var payload = new MatchStartedPayload
                {
                    matchId = matchId,
                    playerCount = clientPlayerCount
                };

                yield return PostJson($"{backendUrl}/internal/match/started", payload);
                _reportedMatchStarted = true;
                yield break;
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator WaitForGracefulShutdown(NetworkRunner runner)
    {
        if (runner == null)
        {
            yield break;
        }

        float startTime = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startTime < GRACEFUL_SHUTDOWN_DELAY_SECONDS)
        {
            if (runner.IsShutdown || !runner.IsRunning)
            {
                yield break;
            }

            if (GetClientActivePlayerCount(runner) <= 0)
            {
                break;
            }

            yield return null;
        }

        var remainingPlayers = runner.ActivePlayers.Where(player => IsClientPlayer(runner, player)).ToList();
        if (remainingPlayers.Count > 0)
        {
            Debug.LogWarning($"Graceful shutdown timeout reached. Forcing disconnect for {remainingPlayers.Count} client(s).");
        }

        Debug.Log("Shutting down runner...");
        var shutdown = runner.Shutdown();
        while (!shutdown.IsCompleted)
            yield return null;

        yield return new WaitForSeconds(1.5f);
    }

    private IEnumerator ReleaseServerPortBeforeExit(string backendUrl)
    {
        if (string.IsNullOrWhiteSpace(backendUrl))
        {
            yield break;
        }

        var payload = new ReleaseServerPortPayload
        {
            portNo = PublicPort,
            containerId = DsId
        };

        bool released = false;
        while (!released)
        {
            yield return PostJsonWithResult($"{backendUrl}/internal/server/port/release", payload, success =>
            {
                released = success;
            });

            if (!released)
            {
                Debug.LogWarning("Release server port failed, retrying...");
                yield return new WaitForSeconds(1f);
            }
        }

        Debug.Log("Released server port entry.");
    }

    private bool SpawnMatchGameNetworkController(NetworkRunner runner, int maxPlayers, int realPlayerCount)
    {
        if (runner == null)
        {
            Debug.LogError("Cannot spawn MatchGameNetworkController because runner is null.");
            return false;
        }

        if (!_matchGameNetworkPrefab.IsValid)
        {
            Debug.LogError("MatchGameNetworkController prefab reference is not valid. Ensure it is assigned in ServerLauncher.");
            return false;
        }

        NetworkObject? matchGameNetworkObject = null;

        try
        {
            var serverAuthorityPlayer = GetServerAuthorityPlayer(runner);
            matchGameNetworkObject = runner.Spawn(
                _matchGameNetworkPrefab,
                Vector3.zero,
                Quaternion.identity,
                serverAuthorityPlayer
            );
            Debug.Log($"Spawned MatchGameNetworkController with authority '{serverAuthorityPlayer}'.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to spawn MatchGameNetworkController: {ex.Message}");
            return false;
        }

        if (matchGameNetworkObject == null)
        {
            Debug.LogError("MatchGameNetworkController spawn returned null NetworkObject.");
            return false;
        }

        var quickMatchServer = matchGameNetworkObject.GetComponent<QuickMatchServer>();
        if (quickMatchServer != null)
        {
            quickMatchServer.SetExpectedPlayerCount(maxPlayers);
            quickMatchServer.SetExpectedRealPlayerCount(realPlayerCount);
        }
        else
        {
            Debug.LogWarning("MatchGameNetworkController does not have QuickMatchServer component.");
        }

        var matchGameGo = matchGameNetworkObject.gameObject;
        if (matchGameGo != null)
        {
            DontDestroyOnLoad(matchGameGo);

            try
            {
                runner.MakeDontDestroyOnLoad(matchGameGo);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Unable to mark '{matchGameGo.name}' as DontDestroyOnLoad via runner: {ex.Message}");
            }
        }

        return true;
    }

    private static PlayerRef GetServerAuthorityPlayer(NetworkRunner runner)
    {
        if (runner == null)
        {
            return PlayerRef.None;
        }

        if (runner.GameMode == GameMode.Server)
        {
            var localPlayer = runner.LocalPlayer;
            if (!localPlayer.IsNone)
            {
                return localPlayer;
            }

            var activePlayers = runner.ActivePlayers;
            if (activePlayers.Any())
            {
                return activePlayers.First();
            }

            return PlayerRef.None;
        }

        return runner.LocalPlayer;
    }

    private static bool IsClientPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || player.IsNone)
        {
            return false;
        }

        // Dedicated server local player is a synthetic server actor (usually id=1024),
        // not an actual game client.
        if (runner.IsServer && player == runner.LocalPlayer)
        {
            return false;
        }

        return true;
    }

    private static int GetClientActivePlayerCount(NetworkRunner runner)
    {
        if (runner == null || runner.ActivePlayers == null)
        {
            return 0;
        }

        return runner.ActivePlayers.Count(player => IsClientPlayer(runner, player));
    }

    // -------------------- HTTP helpers --------------------

    private static IEnumerator PostJson(string url, object body)
    {
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest(url, "POST"))
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"POST failed: {url} | {req.responseCode} | {req.error} | {req.downloadHandler?.text}");
            }
            else
            {
                Debug.Log($"POST ok: {url} | {req.responseCode}");
            }
        }
    }

    /// <summary>
    /// POST vÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºi retry cho cÃƒÆ’Ã‚Â¡c endpoint quan trÃƒÂ¡Ã‚Â»Ã‚Âng (vÃƒÆ’Ã‚Â­ dÃƒÂ¡Ã‚Â»Ã‚Â¥: match/result).
    /// Retry khi gÃƒÂ¡Ã‚ÂºÃ‚Â·p lÃƒÂ¡Ã‚Â»Ã¢â‚¬â€i mÃƒÂ¡Ã‚ÂºÃ‚Â¡ng hoÃƒÂ¡Ã‚ÂºÃ‚Â·c HTTP 5xx. Delay tÃƒâ€žÃ†â€™ng dÃƒÂ¡Ã‚ÂºÃ‚Â§n (exponential backoff).
    /// </summary>
    private static IEnumerator PostJsonWithRetry(string url, object body, int maxRetries = 3, float baseDelaySeconds = 2f)
    {
        string json = JsonUtility.ToJson(body);

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            using (var req = new UnityWebRequest(url, "POST"))
            {
                byte[] data = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(data);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 15;

                yield return req.SendWebRequest();

                bool isSuccess = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
                if (isSuccess)
                {
                    Debug.Log($"POST ok: {url} | {req.responseCode}" + (attempt > 0 ? $" (retry {attempt})" : ""));
                    yield break;
                }

                bool isRetryable = req.result == UnityWebRequest.Result.ConnectionError
                                || req.responseCode >= 500
                                || req.responseCode == 0;

                Debug.LogWarning($"POST failed: {url} | {req.responseCode} | {req.error} | attempt {attempt}/{maxRetries}");

                if (!isRetryable || attempt >= maxRetries)
                {
                    Debug.LogError($"POST failed permanently: {url} | {req.responseCode} | {req.downloadHandler?.text}");
                    yield break;
                }
            }

            float delay = baseDelaySeconds * Mathf.Pow(2, attempt);
            Debug.Log($"Retrying POST {url} in {delay:F1}s (attempt {attempt + 1}/{maxRetries})...");
            yield return new WaitForSecondsRealtime(delay);
        }
    }

    private static IEnumerator PostJsonWithResult(string url, object body, Action<bool> onComplete)
    {
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest(url, "POST"))
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"POST failed: {url} | {req.responseCode} | {req.error} | {req.downloadHandler?.text}");
                onComplete(false);
                yield break;
            }

            if (req.responseCode < 200 || req.responseCode >= 300)
            {
                Debug.LogWarning($"POST failed: {url} | {req.responseCode} | {req.downloadHandler?.text}");
                onComplete(false);
                yield break;
            }

            var responseText = req.downloadHandler?.text;
            bool success = true;
            if (!string.IsNullOrWhiteSpace(responseText))
            {
                try
                {
                    var parsed = JsonUtility.FromJson<ReleaseServerPortResponse>(responseText);
                    if (parsed != null && !string.IsNullOrWhiteSpace(parsed.status))
                    {
                        success = string.Equals(parsed.status, "DELETED", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(parsed.status, "NOT_FOUND", StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Unable to parse response: {ex.Message}");
                }
            }

            if (success)
            {
                Debug.Log($"POST ok: {url} | {req.responseCode}");
            }
            else
            {
                Debug.LogWarning($"POST not successful: {url} | {req.responseCode} | {responseText}");
            }

            onComplete(success);
        }
    }

    // -------------------- Payload DTOs (serializable) --------------------

    [Serializable]
    private class DsRegisterPayload
    {
        public string dsId = string.Empty;
        public string region = string.Empty;
        public string status = "IDLE";
    }

    [Serializable]
    private class AssignMatchPayload
    {
        public string matchId = string.Empty;
        public string sessionName = string.Empty;
        public int maxPlayers = 3;
        public int realPlayerCount;
        public int bet;
        public string region = string.Empty;
        public int typeMatchGid;
        public int maxRound;
        public string characterSelectionsCsv = string.Empty;
        public string botCharacterModelIdsCsv = string.Empty;
    }

    [Serializable]
    private class MatchReadyPayload
    {
        public string matchId = string.Empty;
        public string sessionName = string.Empty;
        public string region = string.Empty;
        public string dsId = string.Empty;
        public ushort hostPort;
    }

    [Serializable]
    private class MatchResultPayload
    {
        public string matchId = string.Empty;
        public MatchResultData result = new MatchResultData();
    }

    [Serializable]
    private class MatchStartedPayload
    {
        public string matchId = string.Empty;
        public int playerCount;
    }

    [Serializable]
    private class ReleaseServerPortPayload
    {
        public ushort portNo;
        public string containerId = string.Empty;
    }

    [Serializable]
    private class ReleaseServerPortResponse
    {
        public string status = string.Empty;
    }

    [Serializable]
    private class MatchResultData
    {
        public long endedAtUnixMs;
        public List<OverGameResultDto> overGameResults = new List<OverGameResultDto>();
    }

    [Serializable]
    private class OverGameResultDto
    {
        public int playerId;
        public int tunrOrder;
        public int typeMatchGid;
        public int StatusWin;
        public int rounds;
        public string MapGame = string.Empty;
        public int MaxPlayer;
        public int marbBet;
        public int marblesWon;
        public int marblesLost;
        public int expGained;
        public string playerName = string.Empty;
        public string description = string.Empty;
        public string avatarUrl = string.Empty;
    }

    // -------------------- Existing utility & scene host helpers --------------------

    private static void ConfigureDedicatedServerEnvironment()
    {
        // Only run when headless (Null device)
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            return;

        try { RenderSettings.skybox = null; } catch { }
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.antiAliasing = 0;
        Shader.globalMaximumLOD = 100;

        // Disable SRP if accidentally assigned
        try { GraphicsSettings.defaultRenderPipeline = null; } catch { }
        try { GraphicsSettings.defaultRenderPipeline = null; } catch { }

        // Disable all Builtin shaders in current Unity version (best-effort)
        try
        {
            var values = (BuiltinShaderType[])Enum.GetValues(typeof(BuiltinShaderType));
            foreach (var t in values)
            {
                try { GraphicsSettings.SetShaderMode(t, BuiltinShaderMode.Disabled); }
                catch (Exception e) { Debug.Log($"[SERVER] Skip BuiltinShaderType {t}: {e.Message}"); }
            }
        }
        catch (Exception e)
        {
            Debug.Log($"[SERVER] Unable to enumerate BuiltinShaderType: {e.Message}");
        }

        Debug.Log("[SERVER] Headless graphics configured.");
    }

    private void StartAssignListener(int port)
    {
        if (_assignListener != null)
        {
            return;
        }

        _assignListener = new HttpListener();
        _assignListener.Prefixes.Add($"http://*:{port}/");
        _assignListener.Start();

        _assignListenerThread = new Thread(AssignListenerLoop) { IsBackground = true };
        _assignListenerThread.Start();
    }

    private void StopAssignListener()
    {
        if (_assignListener == null)
        {
            return;
        }

        try
        {
            _assignListener.Stop();
            _assignListener.Close();
        }
        catch
        {
        }

        _assignListener = null;
        _assignListenerThread = null;
    }

    private void AssignListenerLoop()
    {
        while (_assignListener != null && _assignListener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = _assignListener.GetContext();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            HandleAssignRequest(context);
        }
    }

    private void HandleAssignRequest(HttpListenerContext context)
    {
        if (context.Request.Url == null || context.Request.Url.AbsolutePath != AssignPath)
        {
            WriteAssignResponse(context.Response, 404, "{\"error\":\"NOT_FOUND\"}");
            return;
        }

        if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            WriteAssignResponse(context.Response, 405, "{\"error\":\"METHOD_NOT_ALLOWED\"}");
            return;
        }

        string body = string.Empty;
        if (context.Request.HasEntityBody)
        {
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            body = reader.ReadToEnd();
        }

        AssignMatchPayload? payload = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            payload = JsonUtility.FromJson<AssignMatchPayload>(body);
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.matchId) || string.IsNullOrWhiteSpace(payload.sessionName))
        {
            WriteAssignResponse(context.Response, 400, "{\"error\":\"INVALID_PAYLOAD\"}");
            return;
        }

        lock (_assignLock)
        {
            if (_pendingAssign != null)
            {
                WriteAssignResponse(context.Response, 409, "{\"error\":\"ALREADY_ASSIGNED\"}");
                return;
            }

            _pendingAssign = payload;
        }

        WriteAssignResponse(context.Response, 200, "{\"status\":\"ASSIGNED\"}");
    }

    private void WriteAssignResponse(HttpListenerResponse response, int statusCode, string body)
    {
        if (response == null)
        {
            return;
        }

        try
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            byte[] data = Encoding.UTF8.GetBytes(body);
            response.ContentLength64 = data.Length;
            using var output = response.OutputStream;
            output.Write(data, 0, data.Length);
        }
        catch (ObjectDisposedException)
        {
            // Listener may be stopped after assignment; ignore late response writes.
        }
        catch (IOException)
        {
            // Client closed the connection or listener stopped; safe to ignore.
        }
    }

    private bool TryValidateUdpPort(ushort port, out string error)
    {
        try
        {
            using var udpClient = new UdpClient(port);
            error = string.Empty;
            return true;
        }
        catch (SocketException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return false;
        }
    }

    private string GetEnv(string key, string defaultValue)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(v) ? defaultValue : v.Trim();
    }

    private int GetEnvInt(string key, int defaultValue)
    {
        var v = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(v))
            return defaultValue;

        return int.TryParse(v.Trim(), out var n) ? n : defaultValue;
    }

    private float GetEnvFloat(string key, float defaultValue)
    {
        var v = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(v))
            return defaultValue;

        return float.TryParse(v.Trim(), out var n) ? n : defaultValue;
    }

    private string? GetArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var argMatch = args.FirstOrDefault(arg => arg.StartsWith(name) && arg.Contains('='));

        if (argMatch != null)
        {
            return argMatch.Split('=').Skip(1).FirstOrDefault();
        }

        return null;
    }

    private void EnsureGameRuleHost()
    {
        if (GameSessionNetWork_Host.Instance != null)
        {
            _host = GameSessionNetWork_Host.Instance;
            return;
        }

        var hostGO = new GameObject("RuleGame");
        DontDestroyOnLoad(hostGO);

        _host = hostGO.AddComponent<GameSessionNetWork_Host>();
    }

    public List<SceneLogicConfig> GetSceneConfigsForScene(string sceneName)
    {
        var result = new List<SceneLogicConfig>();

        if (string.IsNullOrWhiteSpace(sceneName) || _sceneConfigBindings == null || _sceneConfigBindings.Count == 0)
        {
            return result;
        }

        foreach (var binding in _sceneConfigBindings)
        {
            if (binding == null || binding.sceneConfig == null || string.IsNullOrWhiteSpace(binding.sceneName))
            {
                continue;
            }

            if (string.Equals(binding.sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(binding.sceneConfig);
            }
        }

        return result;
    }

    private IEnumerable<(string SceneName, SceneLogicConfig Config)> EnumerateSceneBindings()
    {
        if (_sceneConfigBindings == null || _sceneConfigBindings.Count == 0)
        {
            yield break;
        }

        foreach (var binding in _sceneConfigBindings)
        {
            if (binding?.sceneConfig == null || string.IsNullOrWhiteSpace(binding.sceneName))
            {
                continue;
            }

            yield return (binding.sceneName, binding.sceneConfig);
        }
    }

    public bool TryCreateSessionHost(string sceneName, out GameSessionNetWork_Host host)
    {
        host = null;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (_mapHelper == null || !_mapHelper.TryGetSceneHostTemplate(sceneName, out var template) || template == null)
        {
            Debug.LogWarning($"Host template not found for map '{sceneName}'.");
            return false;
        }

        var templateGo = template.gameObject;
        templateGo.SetActive(true);

        host = template;
        host.enabled = true;
        _host = host;
        GameSessionNetWork_Host.Instance = host;
        host.TryResolveWaterObject(logFailure: true);
        return true;
    }

    private string ResolveMatchSceneName()
    {
        var configuredScene = _sceneConfigBindings?
            .FirstOrDefault(binding => binding != null
                                       && binding.sceneConfig != null
                                       && !string.IsNullOrWhiteSpace(binding.sceneName))
            ?.sceneName;

        return string.IsNullOrWhiteSpace(configuredScene)
            ? GameMapHelper.ToSceneName(GameMapId.HometownHouse)
            : configuredScene;
    }

    private bool ValidatePaperLegendMapSpawnConfig(string sceneName, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            error = "[PaperLegends][Spawn] Missing map sceneName. Dedicated server will not report READY.";
            return false;
        }

        if (!TryCreateSessionHost(sceneName, out var host) || host == null)
        {
            error = $"[PaperLegends][Spawn] SceneLogicConfig host template for map '{sceneName}' is missing. Dedicated server will not report READY.";
            return false;
        }

        int configuredCount = host.PaperLegendSpawnPoints?
            .Count(point => point != null) ?? 0;

        if (configuredCount < SceneLogicConfig.PaperLegendSpawnPointCount)
        {
            error = $"[PaperLegends][Spawn] SceneLogicConfig for map '{sceneName}' must configure at least {SceneLogicConfig.PaperLegendSpawnPointCount} spawn point(s) with tag '{SceneLogicConfig.PaperLegendSpawnTag}' before matchmaking. Current count: {configuredCount}. Dedicated server will not report READY.";
            return false;
        }

        Debug.Log($"[PaperLegends][Spawn] READY validation passed for map '{sceneName}' with {configuredCount} spawn point(s).");
        return true;
    }

    public bool HasSceneHostTemplate(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        return _mapHelper != null && _mapHelper.HasSceneHostTemplate(sceneName);
    }
}
#endif
