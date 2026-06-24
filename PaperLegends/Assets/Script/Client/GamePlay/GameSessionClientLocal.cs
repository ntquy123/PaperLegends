
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Serialization;

public class GameSessionClientLocal : MonoBehaviour
{
    public static GameSessionClientLocal Instance { get; set; }
    [Header("GAME CONFIG")]
    public BoxCollider playArea;
    public Transform StartPointMain;
    public Transform StartPointMainEffect;
    [Header("Lighting")]
    public Light directionalLight;
    [Header("Particle Effects")]
    public GameObject rainEffect;
    public GameObject poolWaterEffect;
    private Coroutine playerTurnCoroutine;
    [FormerlySerializedAs("startPointArrow")]
    [SerializeField] private GameObject startPointArrowPrefab;
    private GameObject startPointArrowInstance;
    public GameObject playerArrowPrefab;
    public GameObject playAreaGuard;
    public List<Transform> BananaSpawnPoints { get; } = new();
    private ShotParams? pendingShot;
    public bool IsNormalPlayerTurnActive { get; private set; }
    private bool hasRegisteredTimeoutHandler;
    private Coroutine timeoutRegistrationRoutine;
 

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        TryRegisterTimeoutHandler();
    }

    private void OnDisable()
    {
        IsNormalPlayerTurnActive = false;
        UnregisterTimeoutHandler();
    }

    //private void Start()
    //{
           
    //}

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        DestroyStartPointArrow();

        pendingShot = null;
        UnregisterTimeoutHandler();
    }
    public void SetPlayAreaGuardActive(bool active)
    {
        if (playAreaGuard == null)
            return;

        if (playAreaGuard.activeSelf != active)
            playAreaGuard.SetActive(active);
    }

    public void RefreshBananaSpawnPoints()
    {
        BananaSpawnPoints.Clear();

        var spawnObjects = GameObject.FindGameObjectsWithTag("BananaSpawn");
        if (spawnObjects == null || spawnObjects.Length == 0)
            return;

        System.Array.Sort(spawnObjects, (a, b) =>
        {
            if (a == null && b == null)
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            return a.transform.position.x.CompareTo(b.transform.position.x);
        });

        foreach (var obj in spawnObjects)
        {
            if (obj == null)
                continue;

            var transform = obj.transform;
            if (transform != null)
                BananaSpawnPoints.Add(transform);
        }
    }

    public void ApplyBananaSpawnStates(IReadOnlyList<int> activeIndices)
    {
        /*
         * Hàm này chỉ chạy ở phía client: nhận danh sách chỉ số bẫy chuối từ server
         * và bật/tắt GameObject tương ứng trong scene local. Client tự đảm bảo danh sách
         * điểm spawn đã được cache (nếu chưa sẽ tự refresh lại từ scene).
         */

        if (BananaSpawnPoints.Count == 0)
            RefreshBananaSpawnPoints();

        var indices = new HashSet<int>();
        if (activeIndices != null)
        {
            foreach (var index in activeIndices)
            {
                if (index >= 0 && index < BananaSpawnPoints.Count)
                    indices.Add(index);
            }
        }

        for (int i = 0; i < BananaSpawnPoints.Count; i++)
        {
            var point = BananaSpawnPoints[i];
            if (point == null)
                continue;

            var go = point.gameObject;
            if (go == null)
                continue;

            bool shouldBeActive = indices.Contains(i);
            if (go.activeSelf != shouldBeActive)
                go.SetActive(shouldBeActive);
        }
    }

    public void DeactivateBananaSpawnAt(int index)
    {
        if (index < 0)
            return;

        if (BananaSpawnPoints.Count == 0)
            RefreshBananaSpawnPoints();

        if (index < 0 || index >= BananaSpawnPoints.Count)
            return;

        var point = BananaSpawnPoints[index];
        if (point == null)
            return;

        var go = point.gameObject;
        if (go != null && go.activeSelf)
            go.SetActive(false);
    }
    public void onShootBallByPlayer(float power)
    {

        int UserId = GameManagerNetWork.Instance.loginUserModel.UserId;
        SkillManager.Instance?.StopViewSkillEffect();

        var BallGo = NetworkObjectManager.Instance.GetActiveBallObject(UserId);
        if (BallGo == null)
        {
            Debug.LogWarning("Không tìm thấy bi đang hoạt động của người chơi để bắn");
            return;
        }
        var playerGO = NetworkObjectManager.Instance.GetPlayerObject(UserId);
        if (playerGO == null)
        {
            Debug.LogWarning($"Không tìm thấy đối tượng người chơi với id {UserId}");
            return;
        }

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        if (handler == null)
        {
            Debug.LogWarning("Không tìm thấy PlayerNetworkHandler để phát animation bắn");
            return;
        }
        //step 1. Lấy thông tin lực
        //float power = PowerBarController.Instance.powerSlider.value;
        ShootBallJoystick shootJoystick = ShootBallJoystick.Instance;
        float spinX = shootJoystick != null ? shootJoystick.SpinX : 0f;
        float spinZ = shootJoystick != null ? shootJoystick.SpinZ : 0f;
        float normalizedPower = (power - 1) / (10 - 1);

        // Chuyển giá trị xoáy từ hệ trục của joystick sang thế giới dựa trên
        // hướng của ngón tay. Điều này giúp xoáy luôn đúng với hướng người
        // chơi kéo joystick.
        Quaternion fingerRot = handler.PointPosition.rotation;
        Vector3 localSpin = new Vector3(spinX, 0, spinZ) * handler.PlayerModel.spinForce;
        Vector3 spin = fingerRot * localSpin;
        Vector3 direction = handler.PointPosition.forward.normalized;
        if (shootJoystick != null)
            direction = shootJoystick.ApplyShotAccuracy(direction);
        float force = Mathf.Lerp(0, handler.PlayerModel.powerForce, normalizedPower);
        float shootAngle = UIControllerOnline.Instance != null ? UIControllerOnline.Instance.GetShootAngle() : 0f;
        float accuracyOffset = shootJoystick != null ? shootJoystick.LastShotAccuracyOffsetAngle : 0f;
        if (handler.PlayerModel.statusPlayer == StatusPlayer.Power) 
            force *= 2;

        Vector3 ballPosition = BallGo.transform.position;
        //if (FloodController.Instance != null && FloodController.Instance.IsSubmerged(ballPosition))
        //{
        //    force *= 0.5f; // apply drag when submerged
        //}

        // Không đủ lực bắn -> thoát và reset thanh lực
        if (force <= 0.01f)
        {
            PowerBarController.Instance.ResetBar();
            return;
        }
        Debug.Log($"Client đã bắn | pid={UserId} force={force:F2} spin=({spin.x:F2},{spin.y:F2},{spin.z:F2}) dir=({direction.x:F2},{direction.y:F2},{direction.z:F2}) angle={shootAngle:F2} accuracyOffset={accuracyOffset:F1}");
        var shot = new ShotParams
        {
            direction = direction,
            spin = spin,
            force = force,
            shootAngle = shootAngle
        };
        pendingShot = shot;
        handler.RPC_NotifyShotCommitted();
        UIControllerOnline.Instance?.StopTurnCountdown(true);
        MovePlayerOnlineHandler.Instance?.RequestAnimState(CharacterAnimState.Shoot);
        
    }

    public void ConfirmPendingShot()
    {
        if (!pendingShot.HasValue)
        {
            Debug.LogWarning("Không có dữ liệu bắn đang chờ để xác nhận");
            return;
        }
        var shot = pendingShot.Value;

        var handler = MovePlayerOnlineHandler.Instance?.LocalPlayerHandler;
        if (handler == null)
        {
            int userId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
            if (userId != 0)
            {
                var playerGO = NetworkObjectManager.Instance?.GetPlayerObject(userId);
                if (playerGO != null)
                    handler = playerGO.GetComponent<PlayerNetworkHandler>();
            }
        }

        if (handler == null)
        {
            Debug.LogWarning("Không tìm thấy PlayerNetworkHandler để gửi yêu cầu bắn");
            return;
        }

        pendingShot = null;
        handler.RPC_RequestShot(shot);
        UIControllerOnline.Instance?.UIforViewOnline();
        UIControllerOnline.Instance?.StopTurnCountdown(true);
    }
 
    //ping mỗi 5 giây duy trình kết nối
    public IEnumerator KeepNetwork()
    {
        Debug.Log("Tiến hành ping...");
        while (true)
        {
            yield return new WaitForSeconds(5f);
            NetworkObjectManager.Instance.RpcKeepAlive();
        }
    }
    public IEnumerator TURN_EXAM()
    {
        NetworkObjectManager.Instance.SwitchActiveBall(GameManagerNetWork.Instance.loginUserModel.UserId, 0);
        LoadingManager.Instance.UpdateProgress(1f, "Hoàn tất");
        LoadingManager.Instance.FinishLoading();
        SoundManager.Instance?.PlayBackGroundSound();
        UIControllerOnline.Instance?.ShowPlayerList_Online();
        UIControllerOnline.Instance?.ShowInforList_Online();

        playerTurnCoroutine = StartCoroutine(StartPlayerTurn());
        yield return playerTurnCoroutine;
    }

    public IEnumerator StartTurn()
    {
        // Đảm bảo hủy coroutine lượt trước (đặc biệt khi bị trượt chuối)
        // để camera không tiếp tục bám theo người chơi cũ khi host đã chuyển lượt.
        StopPlayerTurn();

        // Danh sách người chơi đã được sắp xếp thứ tự từ server.
        var listUserOrder = NetworkObjectManager.Instance.GetOrderedPlayerInfos().ToList();
        if (listUserOrder.Count == 0)
        {
            Debug.Log("[ERROR] Không có ai chơi hết");
            yield break;
        }

        //yield return GameManagerNetWork.Instance.WaitForStableConnection();
        CameraRotation.Instance.StopSlowMotion();
        CameraRotation.Instance.SetMiniCameraActive(false);
        //currentHost.isReadyToShoot = false;

        UIControllerOnline.Instance.ShowPlayerList_Online();
        UIControllerOnline.Instance.ShowInforList_Online();
      
 
            foreach (var entry in listUserOrder)
            {
                var obj = NetworkObjectManager.Instance?.GetPlayerObject(entry.playerId);
                if (obj == null) continue;
                var ph = obj.GetComponent<PlayerNetworkHandler>();
                if (ph.PlayerModel.isCatAnTienActive == 1)
                {
                    SkillManager.Instance.EndTurnCleanup(entry.playerId);
                }
            }
 

        UIControllerOnline.Instance?.UpdateViewMapButtonState();
        UIControllerOnline.Instance?.UpdateHidePlayerButtonState();

        var serverRPC = GameManagerNetWork.Instance.serverRPC;
        if (serverRPC == null)
            yield break;

        if (serverRPC.IsGameEnded)
            yield break;

        // currentOrder là index lượt do host broadcast, đảm bảo tất cả client dùng chung.
        int currentOrder = serverRPC.currentPlayerIndex;
        var data = listUserOrder.FirstOrDefault(t => t.turnOrder == currentOrder);
        // Ghi log để kiểm tra tình trạng lệch lượt (đặc biệt sau khi trượt chuối).
        Debug.Log($"[CLIENT] StartTurn => order {currentOrder}, playerId {data.playerId}, local {GameManagerNetWork.Instance.loginUserModel.UserId}");
        if (data.playerId == 0)
        {
            Debug.LogWarning($"[ERROR] Không tìm thấy người chơi order: {currentOrder}");
            yield break;
        }

        bool isMyTurn = data.playerId == GameManagerNetWork.Instance.loginUserModel.UserId;

        // Nếu không phải lượt của mình thì reset các trạng thái ẩn/hiện UI liên quan.
        if (!isMyTurn)
        {
            UIControllerOnline.Instance?.ResetHiddenPlayers();
        }

        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.ClearSkillsUsedThisTurn(data.playerId);
        }

        var playerGO = serverRPC.GetPlayerObject(data.playerId);
        if (playerGO == null)
        {
            Debug.Log("[ERROR] Không tìm thấy player object");
            yield break;
        }

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        if (handler.PlayerModel.isDestroy)
        {
            serverRPC.HandelNextTurn();
            yield break;
        }
        //Camera theo dõi người chơi
        CameraRotation.Instance?.StartFollowingPlayerOnline(playerGO.transform);
        // Chỉ client của người đang đến lượt mới chạy HandlePlayerTurn, tránh cả hai chạy cùng lúc.
        if (isMyTurn)
        {
            yield return HandlePlayerTurn(handler);
        }
        else
        {
            yield return PlayOpponentTurn(handler);
        }
        SkillManager.Instance.ShowSkilldList();
        RefreshBallArrows();
    }

    private void TryRegisterTimeoutHandler()
    {
        if (hasRegisteredTimeoutHandler)
            return;

        var uiController = UIControllerOnline.Instance;
        if (uiController == null)
        {
            if (timeoutRegistrationRoutine == null)
                timeoutRegistrationRoutine = StartCoroutine(WaitForUIControllerOnline());
            return;
        }

        uiController.OnTimeOut -= HandleTurnTimeout;
        uiController.OnTimeOut += HandleTurnTimeout;
        hasRegisteredTimeoutHandler = true;
    }

    private void UnregisterTimeoutHandler()
    {
        if (timeoutRegistrationRoutine != null)
        {
            StopCoroutine(timeoutRegistrationRoutine);
            timeoutRegistrationRoutine = null;
        }

        if (!hasRegisteredTimeoutHandler)
            return;

        var uiController = UIControllerOnline.Instance;
        if (uiController != null)
            uiController.OnTimeOut -= HandleTurnTimeout;

        hasRegisteredTimeoutHandler = false;
    }

    private IEnumerator WaitForUIControllerOnline()
    {
        while (UIControllerOnline.Instance == null)
            yield return null;

        timeoutRegistrationRoutine = null;
        TryRegisterTimeoutHandler();
    }

    private void HandleTurnTimeout()
    {
        var manager = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.serverRPC : null;
        if (manager == null)
            manager = NetworkObjectManager.Instance;

        var loginModel = GameManagerNetWork.Instance?.loginUserModel;
        if (manager == null || loginModel == null)
            return;

        int playerId = loginModel.UserId;
        var playerObject = manager.GetPlayerObject(playerId);
        var handler = playerObject != null ? playerObject.GetComponent<PlayerNetworkHandler>() : null;
        bool isHumanExamTurn = handler != null &&
            handler.PlayerModel.statusPlayer == StatusPlayer.ShootExam &&
            !IsBotControlledTurn(handler);

        if (!isHumanExamTurn && !manager.IsYourTurn(playerId))
            return;

        pendingShot = null;

        if (!isHumanExamTurn)
            MovePlayerOnlineHandler.Instance?.RequestAnimState(CharacterAnimState.Sleeping);

        manager.RpcHandleTurnTimeout(playerId);
    }

    public void StopPlayerTurn()
    {
        if (playerTurnCoroutine != null)
        {
            StopCoroutine(playerTurnCoroutine);
            playerTurnCoroutine = null;
        }
    }

    public void ShowInforExam()
    {
        var currentPlayer = GameManagerNetWork.Instance.GetCurrentPlayerGame();
        string turnOrder = (currentPlayer.turnOrder + 1).ToString();
        var indicator = UIControllerOnline.Instance?.ShowTurnIndicatorRunTime("Bạn đi thứ " + turnOrder, 1, 1);
        if (indicator != null)
            StartCoroutine(indicator);
    }

    public void HandleExamTurnOrderNotification(int playerId, int turnOrder)
    {
        if (GameManagerNetWork.Instance == null || GameManagerNetWork.Instance.loginUserModel.UserId != playerId)
            return;

        if (!gameObject.activeInHierarchy)
            return;

        StartCoroutine(HandlExam_CLIENT(turnOrder));
    }

    public IEnumerator HandlExam_CLIENT(int turnOrder)
    {
        if (StartPointMainEffect != null)
            StartPointMainEffect.gameObject.SetActive(false);

        var indicator = UIControllerOnline.Instance?.ShowTurnIndicatorRunTime("Bạn đi thứ " + turnOrder, 1, 1);
        if (indicator != null)
            yield return StartCoroutine(indicator);
    }

    public void SetStartPointEffectActive(bool active)
    {
        if (StartPointMainEffect != null)
            StartPointMainEffect.gameObject.SetActive(active);
    }

    public void OnExamOrderFinished()
    {
        // Additional client-side handling can be added here if needed.
    }

    public void ShowSkilldList()
    {
        SkillManager.Instance?.ShowSkilldList();
    }

    public void ShowSkillUsedList()
    {
        SkillManager.Instance?.ShowSkillUsedList();
    }

    private IEnumerator HandlePlayerTurn(PlayerNetworkHandler handler)
    {
        IsNormalPlayerTurnActive = false;
        yield return new WaitForSeconds(0.5f);
        Debug.Log("🧍[CLIENT] Lượt của bạn");
        SoundManager.Instance?.PlayYourTurn();

        var turnIndicator = handler.isContinueTurn
            ? UIControllerOnline.Instance?.ShowTurnIndicatorRunTime("Lượt tiếp tục", 0.5f, 1)
            : UIControllerOnline.Instance?.ShowTurnIndicatorRunTime("Lượt Của bạn", 0.5f, 1);
        if (turnIndicator != null)
            yield return StartCoroutine(turnIndicator);

        yield return new WaitForSeconds(1f);
        playerTurnCoroutine = StartCoroutine(StartPlayerTurn());
        yield return playerTurnCoroutine;
        yield break;
    }

    private IEnumerator PlayOpponentTurn(PlayerNetworkHandler handler)
    {
        IsNormalPlayerTurnActive = false;
        bool isBotTurn = IsBotControlledTurn(handler);

        if (!isBotTurn)
            yield return new WaitForSeconds(1.5f);

        SoundManager.Instance?.PlayOpponentTurn();
        Debug.Log(isBotTurn
            ? $"🧍[CLIENT] Lượt của BOT {handler.PlayerModel.fullname}"
            : "🧍[CLIENT] Lượt của Đối thủ");
        var opponentIndicator = UIControllerOnline.Instance?.ShowTurnIndicatorRunTime("Lượt của " + handler.PlayerModel.fullname, 1, 1);
        if (opponentIndicator != null)
            yield return StartCoroutine(opponentIndicator);

        if (!isBotTurn)
            yield return new WaitForSeconds(1.5f);
    }

    private static bool IsBotControlledTurn(PlayerNetworkHandler handler)
    {
        if (handler == null)
            return false;

        string providerType = handler.PlayerModel.providerType.ToString();
        return !string.IsNullOrWhiteSpace(providerType) &&
               providerType.Equals("BOT", System.StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerator StartPlayerTurn()
    {
        IsNormalPlayerTurnActive = false;
 
        var manager = GameManagerNetWork.Instance.serverRPC;
        var networkManager = NetworkObjectManager.Instance;
        int localPlayerId = GameManagerNetWork.Instance.loginUserModel.UserId;

        GameObject playerGO = null;
        GameObject myBall = null;

        float waitTime = 0f;
        const float waitTimeout = 5f;
        while (playerGO == null && waitTime < waitTimeout)
        {
            NetworkObject playerObject = manager != null ? manager.GetPlayerObject(localPlayerId) : null;
            if (playerObject == null && networkManager != null)
            {
                playerObject = networkManager.GetPlayerObject(localPlayerId);
            }

            playerGO = playerObject != null ? playerObject.gameObject : null;

            if (playerGO == null)
            {
                waitTime += Time.deltaTime;
                yield return null;
            }
        }

        waitTime = 0f;
        while (myBall == null && waitTime < waitTimeout)
        {
 
            NetworkObject ballObject = manager != null ? manager.GetActiveBallObject(localPlayerId) : null;
            if (ballObject == null && networkManager != null)
            {
                ballObject = networkManager.GetActiveBallObject(localPlayerId);
            }

            myBall = ballObject != null ? ballObject.gameObject : null;
 
            if (myBall == null)
            {
                waitTime += Time.deltaTime;
                yield return null;
            }
        }

        if (playerGO == null)
        {
            Debug.LogError("[ERROR] Không tìm thấy player object");
            yield break;
        }

        var handler = playerGO.GetComponent<PlayerNetworkHandler>();
        Transform fpp = handler.FPPPosition;
        Transform pointView = handler.PointPosition;
        if (fpp != null)
        {
            if (handler.PlayerModel.statusPlayer == StatusPlayer.ShootExam)
            {
                //yield return WaitForServerControlledMovement(handler);
                yield return new WaitForSeconds(1f);
                Debug.Log($"📷 Chuyển cảnh góc nhìn thứ 1: Lượt Thi của {handler.PlayerModel.playerId}");
                SoundManager.Instance?.PlayExamTurn();
                UIControllerOnline.Instance?.ShowTurnIndicator("Lượt Thi", 1, 1);
                ShowStartPointArrow();
                RotateLocalPlayerTowards(StartPointMain);
                if (!(UIControllerOnline.Instance?.MoveCameraToCurrentFirstPersonView(handler) ?? false))
                    CameraRotation.Instance?.MoveCameraToFPPOnline(fpp, pointView);
                UIControllerOnline.Instance?.UIforStartPointOnline();
                UIControllerOnline.Instance?.StartTurnCountdown();
            }
            else if (handler.PlayerModel.statusPlayer == StatusPlayer.StartPoint)
            {
                HideStartPointArrow();
                Debug.Log($"📷 Chuyển cảnh góc nhìn thứ 1: Lượt bắn từ mức");
                yield return WaitForServerControlledMovement(handler);
                RotateLocalPlayerTowards(ChooseSmartRotationTarget(handler, myBall));
                if (!(UIControllerOnline.Instance?.MoveCameraToCurrentFirstPersonView(handler) ?? false))
                    CameraRotation.Instance?.MoveCameraToFPPOnline(fpp, pointView);
                UIControllerOnline.Instance?.UIforStartPointOnline();
                UIControllerOnline.Instance?.StartTurnCountdown();
            }
            else
            {
                HideStartPointArrow();
                Vector3 targetFirstPosition = myBall != null ? myBall.transform.position : StartPointMain.position;
                if (handler.PlayerModel.statusPlayer == StatusPlayer.MoveStartPoint)
                {
                    targetFirstPosition = StartPointMain.position;
                }

                if (manager == null || !manager.IsYourTurn(localPlayerId))
                {
                    Debug.LogWarning($"[CLIENT] Bỏ qua StartPlayerTurn vì localPlayerId={localPlayerId} không còn là lượt hiện tại.");
                    yield break;
                }

                manager.RpcRequestPlayerMovement(handler.PlayerModel.playerId, targetFirstPosition, PlayerMovementRequestType.MoveToPlayArea);
                yield return WaitForServerControlledMovement(handler);
                IsNormalPlayerTurnActive = true;
                Debug.Log($"📷 Chuyển cảnh góc nhìn thứ 1: Lượt bình thường");
                RotateLocalPlayerTowards(ChooseSmartRotationTarget(handler, myBall));
                if (!(UIControllerOnline.Instance?.MoveCameraToCurrentFirstPersonView(handler) ?? false))
                    CameraRotation.Instance?.MoveCameraToFPPOnline(fpp, pointView);
                UIControllerOnline.Instance?.UIforPlayNormalOnline();
                UIControllerOnline.Instance?.StartTurnCountdown();
                //currentHost.isReadyToShoot = true;

            }
        }
        else
        {
            Debug.LogWarning("❗ Không tìm thấy fPPPosition trong player object.");
        }

        PowerBarController.Instance.ResetBar();
        handler.isContinueTurn = false;
    }

    private void RotateLocalPlayerTowards(Transform target)
    {
        if (target == null)
            return;

        MovePlayerOnlineHandler.Instance?.RotateYawTowardsPosition(target.position);
    }

    /// <summary>
    /// Chọn mục tiêu xoay thông minh: nếu score > 0 và có bi đối thủ gần hơn playArea thì xoay về bi đó,
    /// ngược lại xoay về playArea như bình thường.
    /// </summary>
    private Transform ChooseSmartRotationTarget(PlayerNetworkHandler handler, GameObject myBall)
    {
        Transform fallback = playArea != null ? playArea.transform : null;

        if (handler == null || myBall == null)
            return fallback;

        // Chỉ tìm bi đối thủ khi mình có score > 0 (có thể tiêu diệt)
        if (handler.PlayerModel.score <= 0)
            return fallback;

        var networkManager = NetworkObjectManager.Instance;
        if (networkManager == null)
            return fallback;

        int localId = handler.PlayerModel.playerId;
        Vector3 myBallPos = myBall.transform.position;
        float distToPlayArea = fallback != null ? Vector3.Distance(myBallPos, fallback.position) : float.MaxValue;

        Transform closestEnemyBall = null;
        float closestDist = distToPlayArea;

        foreach (var kvp in networkManager.PlayerBalls)
        {
            if (kvp.Key == localId)
                continue;

            if (kvp.Value == null)
                continue;

            foreach (var ball in kvp.Value)
            {
                if (ball == null || ball.gameObject == null || !ball.gameObject.activeInHierarchy)
                    continue;

                float dist = Vector3.Distance(myBallPos, ball.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestEnemyBall = ball.transform;
                }
            }
        }

        return closestEnemyBall != null ? closestEnemyBall : fallback;
    }

    private IEnumerator WaitForServerControlledMovement(PlayerNetworkHandler handler, float timeoutSeconds = 20f)
    {
        if (handler == null)
            yield break;

        float elapsed = 0f;
        while (handler.CurrentAnimState != CharacterAnimState.SitToShoot && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void RefreshBallArrows()
    {
        var manager = NetworkObjectManager.Instance;
        if (manager == null)
            return;

        var playerBallSnapshot = manager.PlayerBalls.ToList();
        foreach (var kvp in playerBallSnapshot)
        {
            if (kvp.Value == null)
                continue;

            var ballSnapshot = kvp.Value.ToList();
            foreach (var ball in ballSnapshot)
            {
                ball.GetComponent<BallServerController>()?.RefreshNameArrow();
            }
        }
    }

    private void HideStartPointArrow()
    {
        if (startPointArrowInstance != null)
            startPointArrowInstance.SetActive(false);
    }

    private void ShowStartPointArrow()
    {
        DestroyStartPointArrow();
        startPointArrowInstance = ArrowTextHelper.ShowArrow(StartPointMain, "Mức thi", startPointArrowPrefab);
    }

    private void DestroyStartPointArrow()
    {
        if (startPointArrowInstance == null)
            return;

        Destroy(startPointArrowInstance);
        startPointArrowInstance = null;
    }

}
 
