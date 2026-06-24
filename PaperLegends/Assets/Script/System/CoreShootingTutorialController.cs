using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CoreShootingTutorialController : MonoBehaviour
{
    private const int RequiredShots = 3;
    private const string MenuSceneName = "Menu";
    private const float BigBallScalePerLevel = 0.15f;
    private const float TutorialBallDiameterMultiplier = 0.8f;
    private const float TutorialPlayerScaleMultiplier = 0.5f;

    public static CoreShootingTutorialController Instance { get; private set; }
    private static bool showNewPlayerGiftPopupAfterMenuLoad;
    public static bool HasNewPlayerGiftPopupRequest => showNewPlayerGiftPopupAfterMenuLoad;
    public bool IsRunning { get; private set; }
    public CoreTutorialPlayerLocalController LocalPlayerController => tutorialPlayerController;
    public bool CanShoot => IsRunning
        && !isIntroductionVisible
        && !shotInProgress
        && !shotAwaitingAnimation
        && !waitingForBallSkillPress
        && !tutorialBallSkillAnimating
        && !isCompleting
        && cueBallRigidbody != null
        && tutorialPlayerController != null
        && tutorialPlayerController.IsReadyToShoot;

    [Header("Tutorial Shot")]
    [SerializeField] private float minShotImpulse = 0.2f;
    [SerializeField] private float maxShotImpulse = 0.75f;
    [SerializeField] private float stoppedVelocity = 0.025f;
    [SerializeField] private float stoppedAngularVelocity = 0.8f;
    [SerializeField] private float stoppedConfirmSeconds = 0.45f;

    private readonly List<GameObject> targetBalls = new();
    private readonly List<GameObject> tutorialPlayerBalls = new();
    private readonly List<BallPhysicsItem> playerBallItems = new();
    private int playerId;
    private int completedShots;
    private bool shotInProgress;
    private bool shotAwaitingAnimation;
    private bool isCompleting;
    private float stoppedElapsed;
    private float ballDiameter;
    private PlayerInfoStruct localPlayerInfo;
    private CoreTutorialMapConfig tutorialMapConfig;
    private BoxCollider playArea;
    private Terrain tutorialTerrain;
    private Collider[] tutorialGroundColliders = new Collider[0];
    private bool reportedGroundSurfaceMiss;
    private Transform mapShotReference;
    private Transform spawnPlayerPoint;
    private Transform spawnBallPoint;
    private GameObject spawnedPlayer;
    private CoreTutorialPlayerLocalController tutorialPlayerController;
    private CoreTutorialCameraLocalController tutorialCameraController;
    private GameObject cueBall;
    private Rigidbody cueBallRigidbody;
    private CoreTutorialBallLocalPhysicsController cueBallPhysicsController;
    private Vector3 cueBallBaseScale = Vector3.one;
    private bool waitingForBallSkillPress;
    private bool tutorialBigBallSkillUsed;
    private bool tutorialBallSkillAnimating;
    private Sequence tutorialBigBallSkillSequence;
    private readonly List<GameObject> tutorialBallSkillEffectObjects = new();
    private GameObject stepThreeOpponentBall;
    private CoreTutorialWorldTargetMarker stepThreeTargetMarker;
    private bool stepThreeOpponentBallWasHit;
    private Vector3 cueBallStartPosition;
    private Vector3 pendingShotDirection;
    private Vector3 pendingShotSpin;
    private float pendingShotImpulse;
    private CoreTutorialUIController tutorialUIController;
    private bool isIntroductionVisible;
    private bool completionAcknowledged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        showNewPlayerGiftPopupAfterMenuLoad = false;
    }

    public static bool ConsumeNewPlayerGiftPopupRequest()
    {
        if (!showNewPlayerGiftPopupAfterMenuLoad)
        {
            return false;
        }

        showNewPlayerGiftPopupAfterMenuLoad = false;
        return true;
    }

    public static bool TryStartAfterLogin(LoginUserModel player)
    {
#if UNITY_SERVER
        return false;
#else
        if (player == null || player.UserId <= 0 || player.IsTutorialCompleted)
        {
            return false;
        }

        if (Instance != null)
        {
            return true;
        }

        var tutorialObject = new GameObject(nameof(CoreShootingTutorialController));
        DontDestroyOnLoad(tutorialObject);
        var tutorial = tutorialObject.AddComponent<CoreShootingTutorialController>();
        tutorial.playerId = player.UserId;
        tutorial.StartCoroutine(tutorial.LoadTutorialRoutine());
        return true;
#endif
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void FixedUpdate()
    {
        if (!shotInProgress || cueBallRigidbody == null)
        {
            return;
        }

        bool stopped = cueBallRigidbody.linearVelocity.magnitude <= stoppedVelocity
            && cueBallRigidbody.angularVelocity.magnitude <= stoppedAngularVelocity;

        if (!stopped)
        {
            stoppedElapsed = 0f;
            return;
        }

        stoppedElapsed += Time.fixedDeltaTime;
        if (stoppedElapsed >= stoppedConfirmSeconds)
        {
            HandleShotStopped();
        }
    }

    public bool TryShoot(float power, float spinX, float spinZ)
    {
        if (!CanShoot)
        {
            return false;
        }

        float normalizedPower = Mathf.Clamp01(power / 10f);
        if (normalizedPower <= 0.01f)
        {
            ShowInstruction("Giu nut ban de lay luc, sau do tha tay de ban.");
            return false;
        }

        Vector3 direction = ResolveTutorialShotDirection();
        if (ShootBallJoystick.Instance != null)
            direction = ShootBallJoystick.Instance.ApplyShotAccuracy(direction);

        float impulse = Mathf.Lerp(minShotImpulse, maxShotImpulse, normalizedPower)
            * tutorialMapConfig.PlayerShotImpulseMultiplier;
        Vector3 spin = ResolveTutorialShotSpin(direction, spinX, spinZ);

        pendingShotDirection = direction.normalized;
        pendingShotSpin = spin;
        pendingShotImpulse = impulse;
        shotAwaitingAnimation = true;

        if (!tutorialPlayerController.RequestShoot(ConfirmPendingTutorialShot))
        {
            shotAwaitingAnimation = false;
            return false;
        }

        if (IsStepThreeActive())
        {
            tutorialUIController?.HideOpponentShotIntroduction();
        }

        ShowInstruction("Dang thuc hien dong tac ban...");
        SetShootHintVisible(false);
        SetTutorialHideForViewVisible(false);
        return true;
    }

    private Vector3 ResolveTutorialShotDirection()
    {
        Vector3 cuePosition = GetPhysicsPosition(cueBall);
        Vector3 toPlayArea = GetPlayAreaCenter() - cuePosition;
        toPlayArea.y = 0f;
        Vector3 direction = tutorialPlayerController != null ? tutorialPlayerController.AimDirection : toPlayArea;
        direction.y = 0f;

        if (IsStepThreeActive())
        {
            Vector3 toOpponent = GetPhysicsPosition(stepThreeOpponentBall) - cuePosition;
            toOpponent.y = 0f;
            if (toOpponent.sqrMagnitude > Mathf.Epsilon)
            {
                if (direction.sqrMagnitude <= Mathf.Epsilon)
                {
                    return toOpponent.normalized;
                }

                float aimAngle = Vector3.Angle(direction, toOpponent);
                if (aimAngle <= tutorialMapConfig.StepThreeAimAssistMaxAngle)
                {
                    float assist = tutorialMapConfig.StepThreeAimAssistStrength;
                    Vector3 assistedDirection = Vector3.Slerp(direction.normalized, toOpponent.normalized, assist);
                    Debug.Log(
                        $"[TUTORIAL][Step3AimAssist] angle={aimAngle:F1} assist={assist:F2} "
                        + $"target={GetPhysicsPosition(stepThreeOpponentBall).ToString("F3")}");
                    return assistedDirection.normalized;
                }

                return direction.normalized;
            }
        }

        if (Vector3.Dot(direction, toPlayArea) <= 0f)
        {
            direction = toPlayArea;
        }

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            direction = mapShotReference != null ? mapShotReference.forward : Vector3.forward;
        }

        return direction.normalized;
    }

    private Vector3 ResolveTutorialShotSpin(Vector3 direction, float spinX, float spinZ)
    {
        Vector3 forward = direction.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float spinForce = localPlayerInfo.spinForce > 0f ? localPlayerInfo.spinForce : 1f;
        return (right * spinX + forward * spinZ) * spinForce;
    }

    private bool IsStepThreeActive()
    {
        return completedShots == 2 && stepThreeOpponentBall != null;
    }

    private void ConfirmPendingTutorialShot()
    {
        if (!shotAwaitingAnimation || cueBallRigidbody == null)
        {
            return;
        }

        shotAwaitingAnimation = false;
        shotInProgress = true;
        stoppedElapsed = 0f;
        BallOfflineController ballController = cueBall.GetComponent<BallOfflineController>();
        if (ballController != null)
        {
            ballController.IsHolding = false;
        }

        cueBallRigidbody.isKinematic = false;
        if (IsStepThreeActive())
        {
            SetStepThreeTargetMarkerVisible(false);
        }

        if (cueBallPhysicsController != null)
        {
            cueBallPhysicsController.ApplyOnlineStyleShot(
                pendingShotDirection,
                pendingShotImpulse,
                pendingShotSpin);
        }
        else
        {
            cueBallRigidbody.AddForce(pendingShotDirection * pendingShotImpulse, ForceMode.Impulse);
        }

        tutorialCameraController?.FollowShot(cueBallRigidbody, pendingShotDirection);
        ShowInstruction("Cho cac vien bi dung lai...");
    }

    private IEnumerator LoadTutorialRoutine()
    {
        IsRunning = true;
        GameMapId tutorialMapId = GameMapId.CoreShootingTutorial;

        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.UILoadingScreenPrefab?.SetActive(false);
            LoadingManager.Instance.SetLoadingMapInfo(tutorialMapId);
            LoadingManager.Instance.StartLoadingLocal();
        }

        GameManagerNetWork.Instance?.CloseConnectToRunner();

        string mapSceneName = GameMapHelper.ToSceneName(tutorialMapId);
        LoadingManager.LoadScene(mapSceneName);
        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == mapSceneName);
        SoundManager.Instance?.PlayTutorialBackground();

        if (!ResolveMapReferences())
        {
            Debug.LogError("Core tutorial map configuration is missing or incomplete.");
            ReturnToMenu();
            yield break;
        }

        tutorialCameraController = gameObject.AddComponent<CoreTutorialCameraLocalController>();
        tutorialCameraController.InitializeForTutorial();

        yield return LoadTutorialPlayerDataRoutine();
        if (playerBallItems.Count == 0)
        {
            Debug.LogError("Core tutorial cannot start because equipped ball physics were not loaded from the database.");
            LoadingManager.Instance?.FinishLoading();
            ReturnToMenu();
            yield break;
        }

        BuildTutorialObjects();
        BindTutorialUI();

        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.FinishLoading();
        }

        ShowInstruction("Nhan vat dang vao vi tri tap ban...");
        yield return StartCoroutine(PrepareTutorialPlayerRoutine());
        ShowInstruction("Buoc 1/3: keo va tha nut ban de day mot vien bi ra khoi vong.");
        SetShootHintVisible(true);
    }

    private bool ResolveMapReferences()
    {
        tutorialMapConfig = FindFirstObjectByType<CoreTutorialMapConfig>(FindObjectsInactive.Include);
        if (tutorialMapConfig == null)
        {
            Debug.LogError("Add CoreTutorialMapConfig to the CoreShootingTutorial scene before starting the tutorial.");
            return false;
        }

        if (!tutorialMapConfig.TryValidate(out string error))
        {
            Debug.LogError($"Core tutorial map config is invalid: {error}");
            return false;
        }

        playArea = tutorialMapConfig.PlayArea;
        mapShotReference = tutorialMapConfig.ShotTarget;
        spawnPlayerPoint = tutorialMapConfig.PlayerSpawnPoint;
        spawnBallPoint = tutorialMapConfig.ReserveBallSpawnPoint;
        ApplyTutorialGroundPhysics();

        return true;
    }

    private void ApplyTutorialGroundPhysics()
    {
        GameObject ground = tutorialMapConfig != null ? tutorialMapConfig.TerrainGround : null;
        if (ground == null)
        {
            Debug.LogWarning("Core tutorial TerrainGround is not configured for physics setup.");
            return;
        }

        Terrain terrain = ground.GetComponentInChildren<Terrain>(true);
        tutorialTerrain = terrain;
        TerrainCollider terrainCollider = terrain != null ? terrain.GetComponent<TerrainCollider>() : null;
        if (terrain != null && terrainCollider == null)
        {
            terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
            terrainCollider.terrainData = terrain.terrainData;
        }
        else if (terrain != null && terrainCollider != null && terrainCollider.terrainData == null)
        {
            terrainCollider.terrainData = terrain.terrainData;
        }

        tutorialGroundColliders = ground.GetComponentsInChildren<Collider>(true);
        if (tutorialGroundColliders.Length == 0)
        {
            Debug.LogError("Core tutorial Ground does not contain a collider for balls to collide with.");
            return;
        }

        PhysicsMaterial material = GameInitializer.Instance != null
            ? GameInitializer.Instance.GroundPhysicsMaterial
            : null;
        if (material == null)
        {
            Debug.LogWarning("Core tutorial ground physics material is not configured on GameInitializer.");
        }
        else
        {
            foreach (Collider collider in tutorialGroundColliders)
            {
                collider.material = material;
                collider.sharedMaterial = material;
            }
        }

        Collider primaryCollider = terrainCollider != null ? terrainCollider : tutorialGroundColliders[0];
        int ballLayer = LayerMask.NameToLayer("Ball");
        bool ignoresBallLayer = ballLayer >= 0
            && Physics.GetIgnoreLayerCollision(ballLayer, primaryCollider.gameObject.layer);
        Debug.Log(
            $"[TUTORIAL][GroundSetup] ground={ground.name} terrain={(terrain != null ? terrain.name : "null")} "
            + $"colliders={tutorialGroundColliders.Length} primary={primaryCollider.GetType().Name} "
            + $"enabled={primaryCollider.enabled} trigger={primaryCollider.isTrigger} "
            + $"groundLayer={primaryCollider.gameObject.layer} ballLayer={ballLayer} ignored={ignoresBallLayer} "
            + $"bounds={primaryCollider.bounds}");
    }

    private IEnumerator LoadTutorialPlayerDataRoutine()
    {
        localPlayerInfo = CreateFallbackPlayerInfo();
        playerBallItems.Clear();

        if (APIManager.Instance == null)
        {
            Debug.LogError("Core tutorial cannot load equipped ball physics because APIManager is unavailable.");
            yield break;
        }

        var ids = new List<int> { playerId };
        PlayerInfoStruct[] players = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetListPlayerGameById(ids),
            result => players = result));

        if (players != null && players.Length > 0)
        {
            localPlayerInfo = players[0];
        }

        List<PlayerBallPhysics> physicsData = null;
        yield return StartCoroutine(APIManager.Instance.RunTask(
            APIManager.Instance.GetBallPhysicsAsync(ids),
            result => physicsData = result));

        if (physicsData == null)
        {
            Debug.LogError($"Core tutorial did not receive ball physics data for player {playerId}.");
            yield break;
        }

        foreach (PlayerBallPhysics physics in physicsData)
        {
            if (physics == null || physics.playerId != playerId || physics.physics == null)
            {
                continue;
            }

            foreach (BallPhysicsItem item in physics.physics)
            {
                if (item != null)
                {
                    playerBallItems.Add(item);
                }
            }

            break;
        }

        if (playerBallItems.Count == 0)
        {
            Debug.LogError($"Core tutorial found no equipped ball physics in the database for player {playerId}.");
        }
    }

    private PlayerInfoStruct CreateFallbackPlayerInfo()
    {
        LoginUserModel login = GameManagerNetWork.Instance?.loginUserModel;
        return new PlayerInfoStruct
        {
            playerId = playerId,
            level = login != null ? login.Level : 1,
            fullname = login != null && !string.IsNullOrEmpty(login.Username) ? login.Username : "Player",
            avatarUrl = login != null ? login.AvatarUrl ?? string.Empty : string.Empty,
            playerbody = login != null ? (PlayerBodyType)login.Body : default,
            RingBall = login != null ? login.RingBall : 0,
            statusPlayer = StatusPlayer.Normal,
            isHolding = true,
            turnOrder = 0
        };
    }

    private void BuildTutorialObjects()
    {
        Vector3 dimensions = Vector3.Scale(playArea.size, playArea.transform.lossyScale);
        float baseBallDiameter = Mathf.Clamp(Mathf.Min(Mathf.Abs(dimensions.x), Mathf.Abs(dimensions.z)) * 0.075f, 0.02f, 0.06f);
        ballDiameter = baseBallDiameter * TutorialBallDiameterMultiplier;
        SpawnTutorialRingBalls();

        cueBallStartPosition = tutorialMapConfig.CueBallSpawnPoint.position;
        SpawnTutorialPlayer();
        SpawnTutorialPlayerBalls();
    }

    private void SpawnTutorialRingBalls()
    {
        targetBalls.Clear();
        MarbleSpawnData[] spawnDataList = GenerateTutorialRingBallSpawnData(tutorialMapConfig.RingBallCount);
        for (int i = 0; i < spawnDataList.Length; i++)
        {
            MarbleSpawnData spawnData = spawnDataList[i];
            if (!TryGetTutorialGroundSurface(spawnData.Position, out _, out _))
            {
                Debug.LogError(
                    $"[TUTORIAL][RingBallSpawn] Skip idx={i}: no TerrainGround collider exists below "
                    + $"{spawnData.Position.ToString("F3")} after relocation attempts.");
                continue;
            }

            GameObject ringBall = CreateTutorialRingBall(i);
            ringBall.transform.SetParent(null, true);
            PositionTutorialRingBall(ringBall, spawnData);
            EnableTutorialRingBallPhysics(ringBall);
            targetBalls.Add(ringBall);
            LogTutorialRingBallSpawn(i, ringBall);
            if (i == 0)
            {
                StartCoroutine(LogTutorialRingBallTrace(ringBall));
            }
        }
    }

    private MarbleSpawnData[] GenerateTutorialRingBallSpawnData(int totalAmount)
    {
        MarbleSpawnData[] result = new MarbleSpawnData[Mathf.Max(totalAmount, 0)];
        if (playArea == null || totalAmount <= 0)
        {
            return result;
        }

        Bounds areaBounds = playArea.bounds;
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(totalAmount));
        float cellSizeX = areaBounds.size.x / gridSize;
        float cellSizeZ = areaBounds.size.z / gridSize;
        List<Vector3> positions = new();

        for (int xIndex = 0; xIndex < gridSize; xIndex++)
        {
            for (int zIndex = 0; zIndex < gridSize; zIndex++)
            {
                if (positions.Count >= totalAmount)
                {
                    break;
                }

                float x = areaBounds.min.x + (xIndex + 0.5f) * cellSizeX;
                float z = areaBounds.min.z + (zIndex + 0.5f) * cellSizeZ;
                float offsetX = Random.Range(-cellSizeX * 0.3f, cellSizeX * 0.3f);
                float offsetZ = Random.Range(-cellSizeZ * 0.3f, cellSizeZ * 0.3f);
                positions.Add(new Vector3(x + offsetX, areaBounds.min.y, z + offsetZ));
            }
        }

        positions = positions.OrderBy(_ => Random.value).ToList();
        for (int i = 0; i < positions.Count; i++)
        {
            result[i] = new MarbleSpawnData
            {
                Position = positions[i],
                Rotation = Quaternion.Euler(
                    Random.Range(0f, 360f),
                    Random.Range(0f, 360f),
                    Random.Range(0f, 360f))
            };
        }

        AdjustTutorialRingBallSpawnData(result, tutorialTerrain);
        return result;
    }

    private void AdjustTutorialRingBallSpawnData(MarbleSpawnData[] spawnDataList, Terrain terrain)
    {
        if (spawnDataList == null || spawnDataList.Length == 0 || playArea == null)
        {
            return;
        }

        Bounds bounds = playArea.bounds;
        float radius = ballDiameter * 0.5f;
        float boundsMargin = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.02f, 0f, 0.5f);
        float horizontalMargin = Mathf.Max(boundsMargin, radius);
        float heightOffset = radius + tutorialMapConfig.RingBallGroundClearance;

        for (int i = 0; i < spawnDataList.Length; i++)
        {
            MarbleSpawnData spawnData = spawnDataList[i];
            Vector3 position = spawnData.Position;
            position.x = Mathf.Clamp(position.x, bounds.min.x + horizontalMargin, bounds.max.x - horizontalMargin);
            position.z = Mathf.Clamp(position.z, bounds.min.z + horizontalMargin, bounds.max.z - horizontalMargin);

            bool hasGround = TryGetTutorialGroundSurface(position, out float groundY, out _);
            for (int attempt = 0; !hasGround && attempt < 32; attempt++)
            {
                position.x = Random.Range(bounds.min.x + horizontalMargin, bounds.max.x - horizontalMargin);
                position.z = Random.Range(bounds.min.z + horizontalMargin, bounds.max.z - horizontalMargin);
                hasGround = TryGetTutorialGroundSurface(position, out groundY, out _);
            }

            if (!hasGround)
            {
                groundY = bounds.min.y;
                if (terrain != null)
                {
                    float sampledHeight = terrain.SampleHeight(position) + terrain.transform.position.y;
                    if (!float.IsNaN(sampledHeight))
                    {
                        groundY = Mathf.Max(groundY, sampledHeight);
                    }
                }

                if (!reportedGroundSurfaceMiss)
                {
                    reportedGroundSurfaceMiss = true;
                    Debug.LogWarning(
                        $"[TUTORIAL][RingBallSpawn] No configured ground collider is below a spawn point at {position}. "
                        + "Falling balls indicate PlayArea is outside TerrainGround or its collider is not active.");
                }
            }

            position.y = groundY + heightOffset;
            spawnData.Position = position;
            spawnDataList[i] = spawnData;
        }
    }

    private bool TryGetTutorialGroundSurface(Vector3 position, out float groundY, out Collider supportCollider)
    {
        groundY = position.y;
        supportCollider = null;
        if (tutorialGroundColliders == null || tutorialGroundColliders.Length == 0)
        {
            return false;
        }

        float rayStartY = playArea != null ? playArea.bounds.max.y + 1f : position.y + 1f;
        foreach (Collider collider in tutorialGroundColliders)
        {
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger)
            {
                rayStartY = Mathf.Max(rayStartY, collider.bounds.max.y + 1f);
            }
        }

        Ray ray = new Ray(new Vector3(position.x, rayStartY, position.z), Vector3.down);
        float nearestDistance = float.MaxValue;
        foreach (Collider collider in tutorialGroundColliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger)
            {
                continue;
            }

            if (collider.Raycast(ray, out RaycastHit hit, 10000f) && hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                groundY = hit.point.y;
                supportCollider = collider;
            }
        }

        return supportCollider != null;
    }

    private void SpawnTutorialPlayer()
    {
        spawnedPlayer = new GameObject($"Tutorial_Player_{playerId}");
        spawnedPlayer.transform.SetPositionAndRotation(spawnPlayerPoint.position, spawnPlayerPoint.rotation);
        spawnedPlayer.transform.localScale = Vector3.one * TutorialPlayerScaleMultiplier;
        tutorialPlayerController = spawnedPlayer.AddComponent<CoreTutorialPlayerLocalController>();

        tutorialPlayerController.Initialize(localPlayerInfo, GetPlayAreaCenter());
    }

    private void SpawnTutorialPlayerBalls()
    {
        int count = Mathf.Min(playerBallItems.Count, 3);
        Vector3 inactivePosition = spawnBallPoint.position;

        for (int index = 0; index < count; index++)
        {
            BallPhysicsItem item = playerBallItems[index];
            GameObject ball = CreateTutorialPlayerBall(index);
            ball.transform.position = index == 0 ? cueBallStartPosition : inactivePosition;

            Rigidbody body = EnsureBallPhysics(ball);
            BallOfflineController controller = ball.GetComponent<BallOfflineController>();
            if (controller == null)
            {
                controller = ball.AddComponent<BallOfflineController>();
            }

            controller.playerId = playerId;
            controller.BallIndex = index;
            controller.BallMaterialId = item.itemId;
            controller.BallLevel = item.level;
            ApplyTutorialBallPhysics(controller, body, ball, item);
            Debug.Log($"[TUTORIAL][BallPhysics] pid={playerId} idx={index} mass={item.Mass:F2} g={item.GravityScale:F2} drag={item.Drag:F2} bounce={item.Bounciness:F2} elast={item.Elasticity:F2} resist={item.ImpactResistance:F2}");

            Renderer ballRenderer = ball.GetComponent<Renderer>();
            if (item.itemId > 0 && ballRenderer != null)
            {
                StartCoroutine(ItemVisualHelper.ApplyMaterial(
                    null,
                    ballRenderer,
                    null,
                    item.itemId,
                    (int)TypeItemGid.Culi,
                    null,
                    item.isCateye));
            }

            bool active = index == 0;
            controller.SetBallActive(active);
            controller.IsHolding = active;
            ball.GetComponent<BallClientLocalVfx>()?.SetLevel(item.level);
            tutorialPlayerBalls.Add(ball);

            if (active)
            {
                cueBall = ball;
                cueBallRigidbody = body;
                cueBallBaseScale = ball.transform.localScale;
                cueBallPhysicsController = ball.GetComponent<CoreTutorialBallLocalPhysicsController>();
                if (cueBallPhysicsController != null)
                {
                    cueBallPhysicsController.BallHit += HandleTutorialCueBallHit;
                }
                ResetCueBall();
            }
            else
            {
                ball.SetActive(false);
            }
        }

    }

    private IEnumerator PrepareTutorialPlayerRoutine()
    {
        if (tutorialPlayerController == null)
        {
            yield break;
        }

        while (tutorialPlayerController.IsTutorialActive && !tutorialPlayerController.IsVisualReady)
        {
            yield return null;
        }

        if (!tutorialPlayerController.IsTutorialActive)
        {
            yield break;
        }

        tutorialPlayerController.IgnoreCollisionsWith(tutorialPlayerBalls);

        yield return StartCoroutine(tutorialPlayerController.MoveToShootingPosition(
            cueBallStartPosition,
            GetPlayAreaCenter()));

        ResetCueBall();
        tutorialPlayerController.HoldBall(cueBallRigidbody);
        ShowTutorialAimCamera();
        SetTutorialHideForViewVisible(true);
    }

    private GameObject CreateTutorialPlayerBall(int index)
    {
        GameObject ballPrefab = ResolveOfflineBallPrefab();
        GameObject ball = ballPrefab != null
            ? Instantiate(ballPrefab)
            : CreatePhysicalBall($"TutorialPlayerBall_{index + 1}", new Color(0.18f, 0.85f, 0.32f));

        ball.name = $"TutorialPlayerBall_{index + 1}";
        ball.tag = "BallPlayer";
        ball.transform.localScale = Vector3.one * ballDiameter;
        Rigidbody body = EnsureBallPhysics(ball);
        body.linearDamping = 0.1f;
        body.angularDamping = 0.5f;
        body.maxAngularVelocity = Mathf.Max(body.maxAngularVelocity, 50f);

        CoreTutorialBallLocalPhysicsController localPhysics = ball.GetComponent<CoreTutorialBallLocalPhysicsController>();
        if (localPhysics == null)
        {
            localPhysics = ball.AddComponent<CoreTutorialBallLocalPhysicsController>();
        }
        localPhysics.Initialize();
        EnsureTutorialBallCollisionAudio(ball);
        return ball;
    }

    private GameObject CreateTutorialRingBall(int index)
    {
        GameObject ballVisualPrefab = GameInitializer.Instance != null
            ? GameInitializer.Instance.BallModelVisual
            : null;
        GameObject ringBall;

        if (ballVisualPrefab == null)
        {
            Debug.LogWarning("Core tutorial could not load the ring ball visual prefab; using a primitive target.");
            ringBall = CreatePhysicalBall($"TutorialTargetBall_{index + 1}", new Color(0.84f, 0.18f, 0.18f));
        }
        else
        {
            ringBall = new GameObject($"TutorialTargetBall_{index + 1}");
            GameObject visual = Instantiate(ballVisualPrefab, ringBall.transform);
            visual.name = "TutorialRingBallVisual";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            visual.transform.localScale = Vector3.one;

            Renderer ballRenderer = visual.GetComponent<Renderer>();
            Transform cateyeTransform = visual.transform.Find("Cateye");
            Renderer cateyeRenderer = cateyeTransform != null ? cateyeTransform.GetComponent<Renderer>() : null;
            StartCoroutine(ApplyTutorialRingBallVisuals(ballRenderer, cateyeRenderer));
        }

        ringBall.tag = "RingBall";
        int ballLayer = LayerMask.NameToLayer("Ball");
        if (ballLayer >= 0)
        {
            ringBall.layer = ballLayer;
        }

        ringBall.transform.localScale = Vector3.one * ballDiameter;
        Rigidbody body = EnsureBallPhysics(ringBall);
        body.mass = 1.5f;
        body.linearDamping = 0.1f;
        body.angularDamping = 0.5f;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = false;
        body.isKinematic = true;
        body.detectCollisions = true;
        body.maxAngularVelocity = Mathf.Max(body.maxAngularVelocity, 50f);

        foreach (Collider collider in ringBall.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = true;
            collider.isTrigger = false;
            ApplyConfiguredBallPhysicsMaterial(collider);
        }

        EnsureTutorialBallCollisionAudio(ringBall);
        return ringBall;
    }

    private static void EnsureTutorialBallCollisionAudio(GameObject ball)
    {
        if (ball != null && ball.GetComponent<CoreTutorialBallCollisionAudio>() == null)
        {
            ball.AddComponent<CoreTutorialBallCollisionAudio>();
        }
    }

    private static void PositionTutorialRingBall(GameObject ringBall, MarbleSpawnData spawnData)
    {
        Rigidbody body = ringBall != null ? ringBall.GetComponent<Rigidbody>() : null;
        if (body == null)
        {
            if (ringBall != null)
            {
                ringBall.transform.SetPositionAndRotation(spawnData.Position, spawnData.Rotation);
            }

            return;
        }

        // Physics auto-sync is disabled in the project; set the rigidbody pose before simulation starts.
        body.position = spawnData.Position;
        body.rotation = spawnData.Rotation;
    }

    private static void EnableTutorialRingBallPhysics(GameObject ringBall)
    {
        Rigidbody body = ringBall != null ? ringBall.GetComponent<Rigidbody>() : null;
        if (body == null)
        {
            return;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = false;
        body.useGravity = true;
        body.WakeUp();
    }

    private void LogTutorialRingBallSpawn(int index, GameObject ringBall)
    {
        if (ringBall == null)
        {
            return;
        }

        Collider ballCollider = ringBall.GetComponent<Collider>();
        Rigidbody body = ringBall.GetComponent<Rigidbody>();
        Vector3 physicsPosition = body != null ? body.position : ringBall.transform.position;
        bool hasSurface = TryGetTutorialGroundSurface(physicsPosition, out float surfaceY, out Collider surface);
        bool ignored = hasSurface && Physics.GetIgnoreLayerCollision(ringBall.layer, surface.gameObject.layer);
        float bottomY = physicsPosition.y - ballDiameter * 0.5f;

        if (index != 0 && hasSurface && !ignored)
        {
            return;
        }

        Debug.Log(
            $"[TUTORIAL][RingBallSpawn] idx={index} physicsPos={physicsPosition.ToString("F3")} renderPos={ringBall.transform.position.ToString("F3")} "
            + $"bottomY={bottomY:F4} surface={(hasSurface ? surfaceY.ToString("F4") : "none")} "
            + $"gap={(hasSurface ? (bottomY - surfaceY).ToString("F4") : "n/a")} "
            + $"collider={(ballCollider != null ? ballCollider.GetType().Name : "null")} "
            + $"rb={(body != null ? $"mass={body.mass:F2},gravity={body.useGravity},kinematic={body.isKinematic},detect={body.detectCollisions}" : "null")} "
            + $"surfaceCollider={(surface != null ? surface.GetType().Name : "null")} ignored={ignored}");
    }

    private IEnumerator LogTutorialRingBallTrace(GameObject ringBall)
    {
        if (ringBall == null)
        {
            yield break;
        }

        Rigidbody initialBody = ringBall.GetComponent<Rigidbody>();
        Vector3 spawnedAt = initialBody != null ? initialBody.position : ringBall.transform.position;
        yield return new WaitForSeconds(0.5f);
        if (ringBall == null)
        {
            yield break;
        }

        Rigidbody body = ringBall.GetComponent<Rigidbody>();
        Collider ballCollider = ringBall.GetComponent<Collider>();
        bool hasSurface = TryGetTutorialGroundSurface(ringBall.transform.position, out float surfaceY, out Collider surface);
        float bottomY = ballCollider != null ? ballCollider.bounds.min.y : ringBall.transform.position.y;
        Debug.Log(
            $"[TUTORIAL][RingBallTrace] spawned={spawnedAt.ToString("F3")} current={ringBall.transform.position.ToString("F3")} "
            + $"velocity={(body != null ? body.linearVelocity.ToString("F3") : "null")} "
            + $"surface={(hasSurface ? surfaceY.ToString("F4") : "none")} "
            + $"gap={(hasSurface ? (bottomY - surfaceY).ToString("F4") : "n/a")} "
            + $"surfaceCollider={(surface != null ? surface.GetType().Name : "null")}");
    }

    private IEnumerator ApplyTutorialRingBallVisuals(Renderer ballRenderer, Renderer cateyeRenderer)
    {
        Material ballMaterial = null;
        yield return AddressablesHelper.LoadAsset<Material>(
            AddressablePaths.Items.DefaultCuliMaterial,
            material => ballMaterial = material);

        if (ballRenderer != null && ballMaterial != null)
        {
            ballRenderer.enabled = true;
            ballRenderer.material = ballMaterial;
        }

        if (cateyeRenderer == null)
        {
            yield break;
        }

        Material cateyeMaterial = null;
        int cateyeId = Random.Range(1, 4);
        string cateyePath = $"{AddressablePaths.Items.CuliCateyeRingBall}/{cateyeId}.mat";
        yield return AddressablesHelper.LoadAsset<Material>(cateyePath, material => cateyeMaterial = material);

        if (cateyeMaterial == null)
        {
            yield return AddressablesHelper.LoadAsset<Material>(
                AddressablePaths.Items.DefaultCateyeCuliMaterial,
                material => cateyeMaterial = material);
        }

        if (cateyeRenderer != null && cateyeMaterial != null)
        {
            cateyeRenderer.gameObject.SetActive(true);
            cateyeRenderer.enabled = true;
            cateyeRenderer.material = cateyeMaterial;
        }
    }

    private GameObject ResolveOfflineBallPrefab()
    {
        GameSessionOffline configuredSession = ResolveConfiguredOfflineSessionPrefab();
        if (configuredSession != null && configuredSession.BallPlayerPrefab != null)
        {
            return configuredSession.BallPlayerPrefab;
        }

        return GameInitializer.Instance != null ? GameInitializer.Instance.BallModelVisual : null;
    }

    private static GameSessionOffline ResolveConfiguredOfflineSessionPrefab()
    {
        if (GameInitializer.Instance == null || GameInitializer.Instance.GameSessionOffline == null)
        {
            return null;
        }

        return GameInitializer.Instance.GameSessionOffline.GetComponent<GameSessionOffline>();
    }

    private BallPhysicsStruct ToBallPhysics(BallPhysicsItem item)
    {
        return new BallPhysicsStruct
        {
            playerId = playerId,
            name = item.name ?? string.Empty,
            skillGenCode = item.activeSkill?.GenCode ?? 0,
            Mass = item.Mass,
            GravityScale = item.GravityScale,
            Drag = item.Drag,
            Bounciness = item.Bounciness,
            Elasticity = item.Elasticity,
            ImpactResistance = item.ImpactResistance
        };
    }

    private void ApplyTutorialBallPhysics(
        BallOfflineController controller,
        Rigidbody body,
        GameObject ball,
        BallPhysicsItem item)
    {
        controller.ApplyPhysics(ToBallPhysics(item));
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.maxAngularVelocity = Mathf.Max(body.maxAngularVelocity, 50f);

        Collider collider = ball.GetComponent<Collider>();
        if (collider == null)
        {
            return;
        }

        ApplyConfiguredBallPhysicsMaterial(collider);
        if (collider.material == null)
        {
            collider.material = new PhysicsMaterial();
        }

        collider.material.bounciness = item.Bounciness;
        collider.material.dynamicFriction = item.Elasticity;
        collider.material.staticFriction = item.ImpactResistance;
        ball.GetComponent<CoreTutorialBallLocalPhysicsController>()?.CaptureBasePhysicsMaterial(
            collider,
            item.Bounciness,
            item.Elasticity,
            item.ImpactResistance);
    }

    private static void ApplyConfiguredBallPhysicsMaterial(Collider collider)
    {
        if (collider == null)
        {
            return;
        }

        PhysicsMaterial material = GameInitializer.Instance != null
            ? GameInitializer.Instance.BallPhysicsMaterial
            : null;
        if (material != null)
        {
            collider.sharedMaterial = material;
        }
    }

    private static Rigidbody EnsureBallPhysics(GameObject ball)
    {
        if (ball.GetComponent<Collider>() == null)
        {
            ball.AddComponent<SphereCollider>();
        }

        Rigidbody body = ball.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = ball.AddComponent<Rigidbody>();
        }

        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        return body;
    }

    private GameObject CreatePhysicalBall(string objectName, Color color)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = objectName;
        ball.transform.localScale = Vector3.one * ballDiameter;

        Renderer renderer = ball.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }

        Rigidbody body = ball.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.linearDamping = 0.12f;
        body.angularDamping = 0.22f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        return ball;
    }

    private Vector3 GetPlayAreaCenter()
    {
        return playArea.transform.TransformPoint(playArea.center);
    }

    private void ResetCueBall()
    {
        if (cueBallRigidbody == null)
        {
            return;
        }

        if (tutorialPlayerController != null
            && tutorialPlayerController.IsReadyToShoot
            && tutorialPlayerController.FingerPosition != null)
        {
            cueBallStartPosition = tutorialPlayerController.FingerPosition.position;
        }

        cueBallRigidbody.isKinematic = false;
        cueBallRigidbody.linearVelocity = Vector3.zero;
        cueBallRigidbody.angularVelocity = Vector3.zero;
        cueBallRigidbody.position = cueBallStartPosition;
        cueBallRigidbody.rotation = Quaternion.identity;
        cueBallRigidbody.isKinematic = true;
        cueBallPhysicsController?.ResetShotState();

        BallOfflineController ballController = cueBall.GetComponent<BallOfflineController>();
        if (ballController != null)
        {
            ballController.IsHolding = true;
        }
    }

    private void HandleShotStopped()
    {
        shotInProgress = false;
        stoppedElapsed = 0f;

        int currentStep = completedShots + 1;
        bool completedObjective;
        if (currentStep <= 2)
        {
            completedObjective = RemoveEscapedTargets() > 0;
        }
        else
        {
            completedObjective = stepThreeOpponentBallWasHit;
        }

        if (!completedObjective)
        {
            PrepareRetryForCurrentStep(currentStep);
            return;
        }

        completedShots++;
        UpdateProgressText();

        if (completedShots >= RequiredShots)
        {
            StartCoroutine(CompleteTutorialRoutine());
            return;
        }

        if (completedShots == 2)
        {
            StartCoroutine(PrepareOpponentBallStepRoutine());
            return;
        }

        ClearTutorialTargetBalls();
        SpawnTutorialRingBalls();
        PrepareShotAtCurrentPosition(
            GetPlayAreaCenter(),
            "Buoc 2/3: thu dung ky nang Ban Manh de day mot vien bi ra khoi vong.");
        ShowBallSkillIntroduction();
    }

    private void ShowBallSkillIntroduction()
    {
        waitingForBallSkillPress = tutorialUIController != null
            && tutorialUIController.ShowBallSkillIntroduction(ResolveCueBallActiveSkill());
        if (!waitingForBallSkillPress)
        {
            return;
        }

        SetShootHintVisible(false);
        ShowInstruction("Buoc 2/3: cham icon ky nang Ban Manh de phong to vien bi.");
    }

    private ActiveSkillSchema ResolveCueBallActiveSkill()
    {
        BallOfflineController controller = cueBall != null ? cueBall.GetComponent<BallOfflineController>() : null;
        int ballIndex = controller != null ? controller.BallIndex : 0;
        if (ballIndex >= 0 && ballIndex < playerBallItems.Count)
        {
            return playerBallItems[ballIndex]?.activeSkill;
        }

        return playerBallItems.Count > 0 ? playerBallItems[0]?.activeSkill : null;
    }

    private void HandleBallSkillPressed()
    {
        if (!waitingForBallSkillPress || completedShots != 1 || !TryUseBigBallSkill())
        {
            return;
        }

        waitingForBallSkillPress = false;
        tutorialUIController?.HideBallSkillIntroduction();
        ShowInstruction("Buoc 2/3: ky nang Ban Manh da kich hoat. Hay ban de day mot vien bi ra khoi vong.");
        SetShootHintVisible(true);
    }

    private bool TryUseBigBallSkill()
    {
        if (tutorialBigBallSkillUsed || cueBall == null || cueBallRigidbody == null)
        {
            return false;
        }

        BallOfflineController controller = cueBall.GetComponent<BallOfflineController>();
        float level = Mathf.Max(1, controller != null ? controller.BallLevel : 1);
        float multiplier = 1f + level * BigBallScalePerLevel;

        tutorialBigBallSkillUsed = true;
        tutorialBallSkillAnimating = true;
        PlayTutorialBigBallSkillEffect(cueBallBaseScale * multiplier, multiplier);
        Debug.Log($"[TUTORIAL][BigBall] Applied local scale multiplier={multiplier:F2} level={level:F0}.");
        return true;
    }

    private void ResetBigBallSkill()
    {
        if (cueBall == null || !tutorialBigBallSkillUsed)
        {
            return;
        }

        CleanupTutorialBallSkillEffect(true);
        cueBall.transform.localScale = cueBallBaseScale;
        Physics.SyncTransforms();
        tutorialBigBallSkillUsed = false;
        tutorialBallSkillAnimating = false;
    }

    private void PlayTutorialBigBallSkillEffect(Vector3 targetScale, float targetMultiplier)
    {
        if (cueBall == null)
        {
            tutorialBallSkillAnimating = false;
            return;
        }

        CleanupTutorialBallSkillEffect(true);
        tutorialBallSkillAnimating = true;

        Transform ballTransform = cueBall.transform;
        Vector3 currentScale = ballTransform.localScale;
        if (currentScale.sqrMagnitude <= 0.0001f)
            currentScale = cueBallBaseScale;

        Vector3 center = ResolveTutorialBallSkillEffectCenter();
        float radius = ResolveTutorialBallSkillEffectRadius(targetMultiplier);

        SoundManager.Instance?.PlayBallBigSkillEffect(center);
        CreateTutorialSkillGlow(center, new Color(0.35f, 0.9f, 1f, 1f), 1.8f, 0.55f);
        CreateTutorialBallSkillSparks(center, radius, 14, new Color(0.4f, 0.95f, 1f, 1f), true);
        CreateTutorialBallSkillShadow(center, 1f, targetMultiplier, 0.55f);
        ShakeTutorialBallSkillCamera(0.1f, 0.025f);

        Sequence sequence = DOTween.Sequence();
        tutorialBigBallSkillSequence = sequence;
        sequence.Append(ballTransform.DOScale(currentScale * 0.85f, 0.08f).SetEase(Ease.InQuad));
        sequence.InsertCallback(0.12f, () =>
        {
            CreateTutorialBallSkillSparks(ResolveTutorialBallSkillEffectCenter(), radius * 1.1f, 10, new Color(1f, 0.7f, 0.25f, 1f), true);
        });
        sequence.Append(ballTransform.DOScale(targetScale * 1.125f, 0.18f).SetEase(Ease.OutBack));
        sequence.Append(ballTransform.DOScale(targetScale, 0.12f).SetEase(Ease.OutQuad));
        sequence.OnUpdate(() => Physics.SyncTransforms());
        sequence.OnComplete(() =>
        {
            if (cueBall != null)
                cueBall.transform.localScale = targetScale;

            Physics.SyncTransforms();
            tutorialBallSkillAnimating = false;
            if (tutorialBigBallSkillSequence == sequence)
                tutorialBigBallSkillSequence = null;
            CleanupTutorialBallSkillEffect(false);
        });
        sequence.OnKill(() =>
        {
            if (tutorialBigBallSkillSequence == sequence)
                tutorialBigBallSkillSequence = null;
        });
    }

    private Vector3 ResolveTutorialBallSkillEffectCenter()
    {
        return cueBall != null ? cueBall.transform.position : Vector3.zero;
    }

    private float ResolveTutorialBallSkillEffectRadius(float targetMultiplier)
    {
        Collider collider = cueBall != null ? cueBall.GetComponent<Collider>() : null;
        float baseRadius = 0.25f;
        if (collider != null)
        {
            Vector3 extents = collider.bounds.extents;
            baseRadius = Mathf.Max(0.05f, Mathf.Max(extents.x, extents.z));
        }
        else if (cueBallBaseScale.sqrMagnitude > 0.0001f)
        {
            baseRadius = Mathf.Max(0.05f, Mathf.Max(Mathf.Abs(cueBallBaseScale.x), Mathf.Abs(cueBallBaseScale.z)) * 0.5f);
        }

        return Mathf.Clamp(baseRadius * Mathf.Max(targetMultiplier, 1f) * 2.2f, 0.35f, 2.4f);
    }

    private void CreateTutorialBallSkillSparks(Vector3 center, float radius, int count, Color color, bool pullInward)
    {
        Material sparkMaterial = CreateTutorialBallSkillMaterial(color);
        if (sparkMaterial != null)
            Destroy(sparkMaterial, 1f);

        for (int i = 0; i < count; i++)
        {
            Vector3 direction = Random.onUnitSphere;
            direction.y = Mathf.Abs(direction.y) * 0.55f + 0.1f;
            direction.Normalize();

            Vector3 start = pullInward
                ? center + direction * Random.Range(radius * 0.65f, radius * 1.35f)
                : center + direction * Random.Range(radius * 0.08f, radius * 0.3f);
            Vector3 end = pullInward
                ? center + direction * 0.03f
                : center + direction * Random.Range(radius * 0.45f, radius * 0.9f);

            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = pullInward ? "TutorialBallSkillSpark_In" : "TutorialBallSkillSpark_Out";
            spark.transform.position = start;
            spark.transform.localScale = Vector3.one * Mathf.Clamp(radius * 0.08f, 0.025f, 0.09f);
            Collider sparkCollider = spark.GetComponent<Collider>();
            if (sparkCollider != null)
                Destroy(sparkCollider);

            Renderer renderer = spark.GetComponent<Renderer>();
            if (renderer != null && sparkMaterial != null)
                renderer.sharedMaterial = sparkMaterial;

            tutorialBallSkillEffectObjects.Add(spark);
            float delay = Random.Range(0f, 0.06f);
            float duration = pullInward ? Random.Range(0.12f, 0.2f) : Random.Range(0.08f, 0.14f);
            spark.transform.DOMove(end, duration).SetDelay(delay).SetEase(pullInward ? Ease.InQuad : Ease.OutQuad);
            spark.transform.DOScale(Vector3.zero, duration).SetDelay(delay).SetEase(Ease.InQuad);
        }
    }

    private void CreateTutorialBallSkillShadow(Vector3 center, float previousMultiplier, float targetMultiplier, float duration)
    {
        Vector3 shadowPosition = center + Vector3.down * 0.08f;
        if (Physics.Raycast(center + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            shadowPosition = hit.point + Vector3.up * 0.015f;

        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadow.name = "TutorialBallSkillTweenShadow";
        shadow.transform.position = shadowPosition;
        shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Collider shadowCollider = shadow.GetComponent<Collider>();
        if (shadowCollider != null)
            Destroy(shadowCollider);

        Material shadowMaterial = CreateTutorialBallSkillMaterial(new Color(0f, 0f, 0f, 0.22f));
        Renderer renderer = shadow.GetComponent<Renderer>();
        if (renderer != null && shadowMaterial != null)
            renderer.sharedMaterial = shadowMaterial;

        float startSize = Mathf.Clamp(previousMultiplier * 0.48f, 0.18f, 1.2f);
        float endSize = Mathf.Clamp(targetMultiplier * 0.48f, 0.12f, 1.35f);
        shadow.transform.localScale = new Vector3(startSize, startSize, 1f);
        tutorialBallSkillEffectObjects.Add(shadow);

        shadow.transform.DOScale(new Vector3(endSize, endSize, 1f), duration).SetEase(Ease.OutQuad);
        if (shadowMaterial != null)
        {
            Color materialColor = shadowMaterial.color;
            DOTween.To(() => materialColor.a, value =>
            {
                if (shadowMaterial == null)
                    return;

                materialColor.a = value;
                shadowMaterial.color = materialColor;
            }, 0f, duration).SetDelay(Mathf.Max(0f, duration - 0.12f));
            Destroy(shadowMaterial, duration + 0.2f);
        }
    }

    private void CreateTutorialSkillGlow(Vector3 center, Color color, float intensity, float duration)
    {
        GameObject lightObject = new GameObject("TutorialBallSkillGlowLight");
        lightObject.transform.position = center;
        Light skillLight = lightObject.AddComponent<Light>();
        skillLight.type = LightType.Point;
        skillLight.color = color;
        skillLight.range = 2.2f;
        skillLight.intensity = intensity;
        tutorialBallSkillEffectObjects.Add(lightObject);

        DOTween.To(() => skillLight != null ? skillLight.intensity : 0f, value =>
        {
            if (skillLight != null)
                skillLight.intensity = value;
        }, 0f, duration).SetEase(Ease.OutQuad);
    }

    private static Material CreateTutorialBallSkillMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            return null;

        return new Material(shader)
        {
            color = color
        };
    }

    private void ShakeTutorialBallSkillCamera(float duration, float strength)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        camera.transform.DOShakePosition(duration, strength, 8, 45f, false, true);
    }

    private void CleanupTutorialBallSkillEffect(bool killSequence)
    {
        if (killSequence && tutorialBigBallSkillSequence != null)
        {
            tutorialBigBallSkillSequence.Kill();
            tutorialBigBallSkillSequence = null;
            tutorialBallSkillAnimating = false;
        }

        for (int i = 0; i < tutorialBallSkillEffectObjects.Count; i++)
        {
            GameObject effectObject = tutorialBallSkillEffectObjects[i];
            if (effectObject == null)
                continue;

            effectObject.transform.DOKill();
            Destroy(effectObject);
        }

        tutorialBallSkillEffectObjects.Clear();
    }

    private void PrepareRetryForCurrentStep(int currentStep)
    {
        Vector3 aimTarget = currentStep == 3 && stepThreeOpponentBall != null
            ? GetPhysicsPosition(stepThreeOpponentBall)
            : GetPlayAreaCenter();
        string instruction = currentStep == 3
            ? "Chua ban trung bi doi thu. Hay can chinh va thu lai buoc 3/3."
            : currentStep == 2
                ? "Chua day duoc bi ra ngoai. Hay dung ky nang Ban Manh va thu lai buoc 2/3."
                : "Chua day duoc bi ra ngoai. Hay thu lai buoc 1/3.";

        PrepareShotAtCurrentPosition(aimTarget, instruction);
    }

    private void PrepareShotAtCurrentPosition(Vector3 aimTarget, string instruction)
    {
        tutorialPlayerController?.ResetForNextShot(aimTarget);
        ResetCueBall();
        tutorialPlayerController?.HoldBall(cueBallRigidbody);
        ShowTutorialAimCamera();
        ShowInstruction(instruction);
        if (IsStepThreeActive())
        {
            SetStepThreeTargetMarkerVisible(true);
        }
        else
        {
            SetStepThreeTargetMarkerVisible(false);
        }
        SetShootHintVisible(true);
        SetTutorialHideForViewVisible(true);
    }

    private IEnumerator PrepareOpponentBallStepRoutine()
    {
        ResetBigBallSkill();
        ClearTutorialTargetBalls();
        SpawnStepThreeOpponentBall();
        if (stepThreeOpponentBall == null)
        {
            Debug.LogError("Core tutorial cannot prepare step 3 because the opponent ball could not be spawned.");
            ReturnToMenu();
            yield break;
        }

        stepThreeOpponentBallWasHit = false;
        Vector3 aimTarget = GetPhysicsPosition(stepThreeOpponentBall);
        ShowInstruction("Buoc 3/3: dang di chuyen den vi tri ban bi doi thu...");
        yield return StartCoroutine(tutorialPlayerController.MoveToShootingPosition(
            tutorialMapConfig.StepThreePlayerSpawnPoint.position,
            aimTarget));

        ResetCueBall();
        tutorialPlayerController.HoldBall(cueBallRigidbody);
        ShowTutorialAimCamera();
        ShowInstruction("Buoc 3/3: ban trung vien bi doi thu o phia truoc.");
        SetStepThreeTargetMarkerVisible(true);
        SetShootHintVisible(true);
        SetTutorialHideForViewVisible(true);
        tutorialUIController?.ShowOpponentShotIntroduction();
    }

    private void SpawnStepThreeOpponentBall()
    {
        Transform spawnPoint = tutorialMapConfig.StepThreeOpponentBallSpawnPoint;
        Vector3 position = spawnPoint.position;
        if (!TryGetTutorialGroundSurface(position, out float groundY, out _))
        {
            Debug.LogError(
                $"[TUTORIAL][Step3] No TerrainGround collider exists below opponent ball spawn point at "
                + $"{position.ToString("F3")}.");
            return;
        }

        position.y = groundY + ballDiameter * 0.5f + tutorialMapConfig.RingBallGroundClearance;
        MarbleSpawnData spawnData = new MarbleSpawnData
        {
            Position = position,
            Rotation = spawnPoint.rotation
        };

        stepThreeOpponentBall = CreateTutorialRingBall(0);
        stepThreeOpponentBall.name = "TutorialOpponentBall";
        PositionTutorialRingBall(stepThreeOpponentBall, spawnData);
        EnableTutorialRingBallPhysics(stepThreeOpponentBall);
        targetBalls.Add(stepThreeOpponentBall);
        CreateStepThreeTargetMarker();
        LogTutorialRingBallSpawn(0, stepThreeOpponentBall);
    }

    private void HandleTutorialCueBallHit(GameObject otherBall)
    {
        if (completedShots == 2 && stepThreeOpponentBall != null && otherBall == stepThreeOpponentBall)
        {
            stepThreeOpponentBallWasHit = true;
            SetStepThreeTargetMarkerVisible(false);
            Debug.Log("[TUTORIAL][Step3] Opponent ball was hit.");
        }
    }

    private void CreateStepThreeTargetMarker()
    {
        DestroyStepThreeTargetMarker();
        if (stepThreeOpponentBall == null || tutorialMapConfig.StepThreeTargetMarkerPrefab == null)
        {
            return;
        }

        GameObject markerObject = Instantiate(tutorialMapConfig.StepThreeTargetMarkerPrefab);
        markerObject.name = "TutorialStepThreeTargetMarker";
        stepThreeTargetMarker = markerObject.GetComponent<CoreTutorialWorldTargetMarker>();
        if (stepThreeTargetMarker == null)
        {
            stepThreeTargetMarker = markerObject.AddComponent<CoreTutorialWorldTargetMarker>();
        }

        stepThreeTargetMarker.Initialize(
            stepThreeOpponentBall.transform,
            tutorialMapConfig.StepThreeTargetMarkerOffset);
        stepThreeTargetMarker.SetVisible(false);
    }

    private void SetStepThreeTargetMarkerVisible(bool visible)
    {
        stepThreeTargetMarker?.SetVisible(visible);
    }

    private void DestroyStepThreeTargetMarker()
    {
        if (stepThreeTargetMarker != null)
        {
            Destroy(stepThreeTargetMarker.gameObject);
            stepThreeTargetMarker = null;
        }
    }

    private static Vector3 GetPhysicsPosition(GameObject target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        Rigidbody targetBody = target.GetComponent<Rigidbody>();
        return targetBody != null ? targetBody.position : target.transform.position;
    }

    private void ShowTutorialAimCamera()
    {
        if (tutorialPlayerController == null
            || tutorialPlayerController.FPPPosition == null
            || tutorialPlayerController.PointPosition == null)
        {
            return;
        }

        tutorialCameraController?.ShowPlayerAimView(
            tutorialPlayerController.FPPPosition,
            tutorialPlayerController.PointPosition);
    }

    private int RemoveEscapedTargets()
    {
        int escapedCount = 0;
        for (int i = targetBalls.Count - 1; i >= 0; i--)
        {
            GameObject target = targetBalls[i];
            if (target == null)
            {
                targetBalls.RemoveAt(i);
                continue;
            }

            Vector3 local = playArea.transform.InverseTransformPoint(target.transform.position) - playArea.center;
            bool outside = Mathf.Abs(local.x) > playArea.size.x * 0.5f
                || Mathf.Abs(local.z) > playArea.size.z * 0.5f;
            if (outside)
            {
                Destroy(target);
                targetBalls.RemoveAt(i);
                escapedCount++;
            }
        }

        return escapedCount;
    }

    private void ClearTutorialTargetBalls()
    {
        DestroyStepThreeTargetMarker();
        foreach (GameObject target in targetBalls)
        {
            if (target != null)
            {
                Destroy(target);
            }
        }

        targetBalls.Clear();
        stepThreeOpponentBall = null;
        stepThreeOpponentBallWasHit = false;
    }

    private IEnumerator CompleteTutorialRoutine()
    {
        if (isCompleting)
        {
            yield break;
        }

        isCompleting = true;
        tutorialPlayerController?.StopTutorial();
        tutorialCameraController?.StopFollowingShot();
        SetShootHintVisible(false);
        SetTutorialHideForViewVisible(false);
        ShowInstruction("Hoan thanh huong dan ban co ban.");
        tutorialUIController?.HideOpponentShotIntroduction();
        completionAcknowledged = false;
        bool waitForAcknowledgement = tutorialUIController != null
            && tutorialUIController.ShowCompletionIntroduction();

        bool saved = false;
        if (APIManager.Instance != null)
        {
            yield return StartCoroutine(APIManager.Instance.RunTask(
                APIManager.Instance.CompleteTutorialAsync(playerId),
                result => saved = result));
        }

        if (saved && GameManagerNetWork.Instance?.loginUserModel != null)
        {
            GameManagerNetWork.Instance.loginUserModel.IsTutorialCompleted = true;
        }
        else if (!saved)
        {
            Debug.LogWarning("Core tutorial finished locally, but completion could not be saved.");
        }

        if (waitForAcknowledgement)
        {
            yield return new WaitUntil(() => completionAcknowledged);
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.8f);
        }

        ReturnToMenu(showNewPlayerGiftPopup: true);
    }

    private void BindTutorialUI()
    {
        tutorialUIController = FindFirstObjectByType<CoreTutorialUIController>(FindObjectsInactive.Include);
        if (tutorialUIController == null)
        {
            Debug.LogWarning("Core tutorial UI is not configured. Add CoreTutorialUIController to the tutorial scene.");
            isIntroductionVisible = false;
            return;
        }

        tutorialUIController.IntroductionDismissed -= HandleIntroductionDismissed;
        tutorialUIController.IntroductionDismissed += HandleIntroductionDismissed;
        tutorialUIController.BallSkillPressed -= HandleBallSkillPressed;
        tutorialUIController.BallSkillPressed += HandleBallSkillPressed;
        tutorialUIController.CompletionAcknowledged -= HandleCompletionAcknowledged;
        tutorialUIController.CompletionAcknowledged += HandleCompletionAcknowledged;
        isIntroductionVisible = tutorialUIController.Initialize(RequiredShots);
    }

    private void HandleIntroductionDismissed()
    {
        isIntroductionVisible = false;
        if (CanShoot)
        {
            SetShootHintVisible(true);
        }
    }

    private void HandleCompletionAcknowledged()
    {
        completionAcknowledged = true;
    }

    private void ShowInstruction(string message)
    {
        tutorialUIController?.ShowInstruction(message);
    }

    private void UpdateProgressText()
    {
        tutorialUIController?.SetProgress(completedShots, RequiredShots);
    }

    private void SetShootHintVisible(bool visible)
    {
        tutorialUIController?.SetShootHintVisible(visible && !isIntroductionVisible);
    }

    private static void SetTutorialHideForViewVisible(bool visible)
    {
#if !UNITY_SERVER
        UIControllerOnline uiController = UIControllerOnline.Instance != null
            ? UIControllerOnline.Instance
            : FindFirstObjectByType<UIControllerOnline>(FindObjectsInactive.Include);
        if (uiController != null && uiController.ZoneUINeedToHide != null)
        {
            uiController.ZoneUINeedToHide.SetActive(visible);
        }
#endif
    }

    private void ReturnToMenu(bool showNewPlayerGiftPopup = false)
    {
        if (showNewPlayerGiftPopup)
        {
            showNewPlayerGiftPopupAfterMenuLoad = true;
        }

        SoundManager.Instance?.StopTutorialAudio(resumeBackground: false);
        IsRunning = false;
        LoadingManager.LoadScene(MenuSceneName);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (IsRunning)
        {
            SoundManager.Instance?.StopTutorialAudio(resumeBackground: false);
        }

        if (Instance == this)
        {
            Instance = null;
        }

        if (tutorialUIController != null)
        {
            tutorialUIController.IntroductionDismissed -= HandleIntroductionDismissed;
            tutorialUIController.BallSkillPressed -= HandleBallSkillPressed;
            tutorialUIController.CompletionAcknowledged -= HandleCompletionAcknowledged;
            tutorialUIController.Shutdown();
        }
        tutorialCameraController?.Shutdown();
        CleanupTutorialBallSkillEffect(true);
        DestroyStepThreeTargetMarker();
        if (cueBallPhysicsController != null)
        {
            cueBallPhysicsController.BallHit -= HandleTutorialCueBallHit;
        }

        foreach (GameObject target in targetBalls)
        {
            if (target != null)
            {
                Destroy(target);
            }
        }

        foreach (GameObject ball in tutorialPlayerBalls)
        {
            if (ball != null)
            {
                Destroy(ball);
            }
        }

        if (spawnedPlayer != null)
        {
            tutorialPlayerController?.StopTutorial();
            Destroy(spawnedPlayer);
        }
    }
}
