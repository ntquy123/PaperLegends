using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Cinemachine;
using DG.Tweening;
using TMPro;
using Unity.Services.Authentication;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine.UI;
public class CameraRotation : MonoBehaviour
{
    public static CameraRotation Instance;
    [Header("SYSTEM CONFIG")]
    public CinemachineVirtualCamera followCamera; // Camera theo bi
    private Vector2 touchStartPos;
    public float minY = -80f, maxY = -10f; // Giới hạn góc xoay trục X
    private float rotationX = 0f; // Lưu góc xoay X
    public Transform overviewPosition;
    private CameraFollowMode currentMode = CameraFollowMode.FollowFromDistance;
    public Transform ViewMapPosition;// vị trí cho camera theo dõi map
    public Transform ViewMapLookAt; //vị trí camera nhìn vào lúc theo dõi
    public Transform ViewMapPositionExam;// vị trí cho camera theo dõi map khi thi
    public Transform ViewMapLookAtExam; //vị trí camera nhìn vào lúc theo dõi khi this
    [SerializeField, Tooltip("FOV góc nhìn thứ nhất online khi người chơi tới lượt bắn.")]
    private float onlineFirstPersonFov = 50f;
    [SerializeField, Tooltip("FOV khi mở camera xem toàn map online.")]
    private float onlineViewMapFov = 60f;

    public float transitionSpeed = 2f;
  //  private bool hasRotatedToTarget = false;
    public float followSpeed = 5f; // Tốc độ di chuyển theo viên bi
    public Vector3 closeOffset = new Vector3(0, 0.5f, -1f); // Khoảng cách camera với viên bi

    [Header("PAPER LEGENDS CAMERA")]
    private float paperLegendFollowFov = 36f;
    [SerializeField] private Vector3 paperLegendFollowOffset = new Vector3(0f, 9f, -1.2f);
    [SerializeField] private float paperLegendLookAtHeight = 0.05f;
    [SerializeField] private float paperLegendFollowDamping = 0.25f;
    [SerializeField] private int paperLegendFollowPriority = 20;

    private Transform ball; // Điểm trung gian theo dõi
    public Vector3 offset = new Vector3(0, 0f, -5f); // Điều chỉnh cao & xa
    public float maxRotationSpeed = 10f;
    private CinemachineBrain brain;
    private Coroutine followBallCoroutine;
    private Coroutine cameraLoopRoutine;
    private Coroutine slowMotionCoroutine; // handle to current slow motion routine
    private Coroutine bananaSlipCinematicRoutine;
    private float defaultFOV;               // store original camera FOV
    private Tween paperLegendFovTween;
    [Header("FOLLOW SETTINGS")]
    [SerializeField] private float followDistanceThreshold = 2f;
    private readonly Dictionary<int, Coroutine> followDistanceCoroutines = new();
    [Header("MINI CAMERA CONFIG")]
    public Camera miniCamera;                       // Camera phụ quan sát playArea
    // Vị trí tương đối của mini camera so với trung tâm playArea
    // Để quan sát gần với góc nghiêng nhẹ
    public Vector3 miniCamOffset = new Vector3(0f, 0.2f, -0.3f);
    // Viewport đặt mini camera ở 1/4 góc phải phía dưới màn hình
    public Rect miniCamRect = new Rect(0.75f, 0f, 0.25f, 0.25f);
    public Color miniCamBorderColor = Color.white;  // Màu viền camera mini
    public float miniCamBorderSize = 2f;            // Độ dày viền

    // Zoom configuration for mini camera
    private Rect defaultMiniCamRect;                // lưu viewport gốc
    private float defaultMiniCamFOV;                // lưu FOV gốc
    public Rect lockedMiniCamRect = new Rect(0.6f, 0f, 0.4f, 0.4f); // viewport khi khóa bi
    public float lockedMiniCamFOV = 30f;            // FOV khi khóa bi

    private float minRotationY = 150f; // Giới hạn trái trong lượt thi
    private float maxRotationY = 180f;  // Giới hạn phải trong lượt thi
    public float startX;  // Lưu vị trí ban đầu
    //điểm trung gian cho cho online
    public Transform localFollowTarget;
    //Điểm trung gian cho local
    public Transform cameraTarget;
    [Header("PLAYER CONFIG")]
    private NetworkObject playerObject;
    //private Tween followTween;
    //zoom
    public Slider zoomSlider;
    private float minFOV = 10f;         // Zoom in mạnh
    private float maxFOV = 120f;         // Bình thường
    private bool miniCamPrevState = false; // trạng thái trước đó của mini camera
    private Texture2D miniCamBorderTex;
    private Transform trackedRingBall; // ring ball currently focused by mini camera

    [Header("CONFIG SlowMotion")]
    // Field of view camera khi kích hoạt slow motion (tạo cảm giác zoom gần)
    [SerializeField, Tooltip("Độ zoom của camera khi slow motion (FOV)")] private float slowMotionZoomFOV = 10f;
    // Thời gian hiệu ứng slow motion
    [SerializeField, Tooltip("Thời gian hiệu ứng slow motion (giây)")] private float slowMotionDuration = 4f;
    [SerializeField, Tooltip("Độ chậm của slow motion (Time.timeScale)")] private float slowMotionScale = 0.6f;
    [SerializeField, Tooltip("Độ chậm cho người bắn khi kích hoạt kill cam")] private float killCamShooterTimeScale = 0.15f;
    [SerializeField, Tooltip("Độ zoom FOV cho người bắn khi kích hoạt kill cam")] private float killCamShooterZoomFov = 10f;
    [SerializeField, Tooltip("Thời gian ramp vào slow motion cho người bắn")] private float killCamShooterRampTime = 0.2f;
    [SerializeField, Tooltip("Giữ kill cam thêm sau khi xác nhận va chạm (người bắn)")] private float killCamShooterPostHitHold = 1.5f;
    [SerializeField, Tooltip("Độ chậm cho nạn nhân khi bị bắn trúng")] private float killCamVictimTimeScale = 0.25f;
    [SerializeField, Tooltip("Zoom FOV cho nạn nhân")] private float killCamVictimZoomFov = 13f;
    [SerializeField, Tooltip("Thời gian ramp vào slow motion cho nạn nhân")] private float killCamVictimRampTime = 0.18f;
    [SerializeField, Tooltip("Giữ hiệu ứng sau khi xác nhận va chạm (nạn nhân)")] private float killCamVictimPostHitHold = 1.2f;
    [SerializeField, Tooltip("Thời lượng tối thiểu của kill cam")] private float killCamMinDuration = 2.0f;
    [SerializeField, Tooltip("Thời gian safety timeout tối đa — server RPC sẽ tắt kill cam trước thời gian này")] private float killCamSafetyTimeout = 12f;
    [SerializeField, Tooltip("Khoảng đệm thêm vào thời gian dự đoán trước va chạm")] private float killCamPredictionBuffer = 0.6f;
    [SerializeField, Tooltip("Độ cao cộng thêm cho điểm focus kill cam")] private float killCamFocusHeight = 0.3f;
    [Header("KillCam UI")]
    [SerializeField, Tooltip("Overlay vignette/darken dành cho nạn nhân")] private Image killCamVictimVignetteImage;
    [SerializeField, Tooltip("Độ tối mục tiêu của vignette nạn nhân")] private float killCamVictimVignetteAlpha = 0.45f;
    [SerializeField, Tooltip("Thời gian fade vignette")] private float killCamVignetteFadeTime = 0.2f;

    private Transform killCamFocusTarget;
    private Transform previousFollowTarget;
    private Transform previousLookAtTarget;
    private Transform killCamShooterTarget;
    private Transform killCamFocusBallTarget;
    private Vector3 killCamFallbackPoint;
    private Coroutine killCamFocusRoutine;
    private Tween killCamTimeScaleTween;
    private Tween killCamFovTween;
    private Tween killCamVignetteTween;
    private int killCamShooterId;
    private bool killCamActive;
    private bool killCamIsShooter;
    private bool killCamIsVictim;
    private float killCamReleaseAtRealtime;
    private void Awake()
    {
        Instance = this;
    }

    //private void SyncLocalYawWithTarget(Transform target)
    //{
    //    if (target == null)
    //        return;

    //    if (!ClientGameplayBridge.PlayerMovement.HasInstance())
    //        return;

    //    var handler = target.GetComponentInParent<PlayerNetworkHandler>();
    //    if (handler == null)
    //        handler = target.GetComponent<PlayerNetworkHandler>();

    //    if (handler != null && handler.HasInputAuthority)
    //    {
    //        ClientGameplayBridge.PlayerMovement.SetYaw(handler.transform.eulerAngles.y);
    //    }
    //}
    private void Start()
    {
        brain = Camera.main.GetComponent<CinemachineBrain>();
        if (followCamera != null)
            defaultFOV = followCamera.m_Lens.FieldOfView;

        // Đảm bảo camera chưa được kích hoạt ban đầu
        followCamera.Priority = 0;
        if (miniCamera == null)
        {
            GameObject miniObj = new GameObject("MiniCamera");
            miniObj.transform.SetParent(null); // detach so it stays independent
            miniCamera = miniObj.AddComponent<Camera>();
            miniCamera.depth = Camera.main.depth + 1;
        }
        miniCamera.rect = miniCamRect;
        miniCamera.enabled = false;
        defaultMiniCamRect = miniCamRect;
        defaultMiniCamFOV = miniCamera.fieldOfView;
        miniCamBorderTex = Texture2D.whiteTexture;
        zoomSlider.onValueChanged.AddListener(OnZoomSliderChanged);
        if (killCamVictimVignetteImage != null)
        {
            var color = killCamVictimVignetteImage.color;
            color.a = 0f;
            killCamVictimVignetteImage.color = color;
            killCamVictimVignetteImage.gameObject.SetActive(false);
        }
        //Camera.main.farClipPlane = 20f;
        //Lấy obj hiện tại của user
        if (cameraLoopRoutine == null)
            cameraLoopRoutine = StartCoroutine(CameraLoop());
    }
  
    private void OnEnable()
    {
        if (cameraLoopRoutine == null)
            cameraLoopRoutine = StartCoroutine(CameraLoop());
    }

    private void OnDisable()
    {
        StopCameraLoop();
        if (miniCamera != null)
            miniCamera.enabled = false;
        transform.DOKill();
         if (followCamera != null)
               followCamera.transform.DOKill();
    }
    void DisableCinemachine()
    {
        if (brain != null)
            brain.enabled = false;
    }
    void EnableCinemachine()
    {
        if (brain != null)
            brain.enabled = true;
    }

    private IEnumerator CameraLoop()
    {
        while (true)
        {
            //if (GameSessionNetWork_Host.Instance != null && GameSessionNetWork_Host.Instance.IsStartedGame)
            //{
            //    if (GameManagerNetWork.Instance.serverRPC != null && GameManagerNetWork.Instance.serverRPC.HasStateAuthority)
            //        UpdateMiniCamera(); // Chỉ host kiểm tra chuyển động
            //}
            if (GameManagerNetWork.Instance.serverRPC != null && GameManagerNetWork.Instance.serverRPC.HasStateAuthority)
                UpdateMiniCamera();



            yield return null;           // wait next frame
        }
    }

    public void StopCameraLoop()
    {
        if (cameraLoopRoutine != null)
        {
            StopCoroutine(cameraLoopRoutine);
            cameraLoopRoutine = null;
        }
    }

    private void StopDistanceCheck(int playerId)
    {
        if (followDistanceCoroutines.TryGetValue(playerId, out var routine) && routine != null)
        {
            StopCoroutine(routine);
        }
        followDistanceCoroutines.Remove(playerId);
    }

    private bool ShouldAutoFollow(int playerId, int isExam)
    {
        var loginUser = GameManagerNetWork.Instance?.loginUserModel;
        if (loginUser == null)
            return false;

        if (loginUser.UserId != playerId && isExam == 0)
            return true;

        if (loginUser.UserId == playerId && isExam == 1)
            return true;

        return false;
    }

    public void HandleBallShot(Transform ballTransform, int playerId, bool hasStateAuthority, int isExam)
    {
        if (ballTransform == null)
            return;

        StopDistanceCheck(playerId);

        // Luồng xử lý HandleBallShot:
        // 1. Dừng mọi coroutine đang kiểm tra khoảng cách của người chơi trước đó để tránh xung đột trạng thái.
        // 2. Lấy thông tin user hiện tại và xác định xem viên bi thuộc về mình hay người khác.
        // 3. Nếu đang trong lượt thi của người khác (và đã xác định chắc chắn không phải mình), bỏ qua việc chiếm camera.
        // 4. Nếu cấu hình cho phép auto-follow thì lập tức kích hoạt camera bám theo, ngược lại tạo coroutine chờ tới khi bi đủ xa mới bám.
        var loginUser = GameManagerNetWork.Instance?.loginUserModel;
        bool isMyBall = loginUser != null && loginUser.UserId == playerId;
        bool isExamPhase = NetworkObjectManager.Instance != null &&
                           NetworkObjectManager.Instance.StatusLoading == StatusLoadingGame.isExam;
        int effectiveExam = isExamPhase ? 1 : isExam;
        bool isExamTurn = effectiveExam == 1;

        bool shouldSkipForOtherExam = isExamTurn && loginUser != null && !isMyBall;
        if (shouldSkipForOtherExam)
        {
            Debug.Log("Bỏ qua theo dõi bi của người chơi khác trong lượt thi");
            return;
        }

        if (ShouldAutoFollow(playerId, effectiveExam))
        {
            Debug.Log($"Chuyển camera theo dõi {playerId}");
            if (hasStateAuthority)
            {
                StartFollowingBallForMyself(playerId, effectiveExam);
            }
            else
            {
                StartFollowingBallOtherPlayer(playerId, effectiveExam);
            }
            return;
        }

        var routine = StartCoroutine(CheckDistanceToCamera(ballTransform, playerId, effectiveExam, hasStateAuthority));
        followDistanceCoroutines[playerId] = routine;
    }

    public void ResetBallShot(int playerId)
    {
        StopDistanceCheck(playerId);
    }

    private IEnumerator CheckDistanceToCamera(Transform ballTransform, int playerId, int isExam, bool hasStateAuthority)
    {
        while (ballTransform != null)
        {
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                yield return null;
                continue;
            }

            float distance = Vector3.Distance(ballTransform.position, mainCam.transform.position);
            if (distance > followDistanceThreshold)
            {
                if (hasStateAuthority)
                {
                    StartFollowingBallForMyself(playerId, isExam);
                }
                else
                {
                    StartFollowingBallOtherPlayer(playerId, isExam);
                }
                break;
            }

            yield return null;
        }

        followDistanceCoroutines.Remove(playerId);
    }












    public void StartFollowingBall(Transform followTarget)
    {
        EnableCinemachine();
        //UIControllerOnline.Instance.UIforView();
        followCamera.Priority = 10;  // Bật camera theo viên bi
        followCamera.Follow = followTarget;
        followCamera.LookAt = followTarget;
        // DOTween để thay đổi Field of View của camera theo tốc độ viên bi

        if (NPCController.Instance.currentState == TurnState.Exam)
            followCamera.m_Lens.FieldOfView = 50f;
        else
        {
            // followCamera.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            followCamera.m_Lens.FieldOfView = 20f;
        }   
    }
     


    public void StartFollowingAI(Transform followTarget)
    {
        EnableCinemachine();
        if (UIControllerOffline.Instance != null)
        {
            UIControllerOffline.Instance.UIforView();
        }
        else if (UIControllerOnline.Instance != null)
        {
            UIControllerOnline.Instance.UIforViewOnline();
        }
        followCamera.Priority = 10;  // Bật camera theo viên bi
        followCamera.Follow = followTarget;
        followCamera.LookAt = followTarget;
        followCamera.m_Lens.FieldOfView = 60f;

    }
    public void StartFollowingPlayerOnline(Transform followTarget)
    {
        EnableCinemachine();
        SetCameraFollowMode(CameraFollowMode.FollowFromDistance);
        UIControllerOnline.Instance.UIforViewOnline();
        followCamera.Priority = 20;
        followCamera.Follow = followTarget;
        followCamera.LookAt = followTarget;
        followCamera.m_Lens.FieldOfView = 60f;
        //SyncLocalYawWithTarget(followTarget);

    }
    public void StopFollowingBall()
    {
        followCamera.Priority = 0;  // Hạ priority để Main Camera trở lại
        if (followBallCoroutine != null)
        {
            StopCoroutine(followBallCoroutine);
            followBallCoroutine = null;
        }
        DisableCinemachine();
        Camera.main.farClipPlane = 20;
    }

    

    public void MoveCameraToFPP(Vector3 targetPosition, Vector3 targetLookAt)
    {
        followCamera.Follow = null;
        followCamera.LookAt = null;
        followCamera.Priority = 10; // Ưu tiên hiển thị camera này

        // Đảm bảo Camera bắt đầu ở vị trí mong muốn
        followCamera.transform.position = targetPosition;

        // Tính toán hướng quay và đảm bảo giá trị hợp lệ
        Vector3 lookDirection = targetLookAt - targetPosition;
        if (lookDirection.magnitude < 0.01f) lookDirection = followCamera.transform.forward;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized);
        followCamera.transform.rotation = targetRotation;
         //SyncLocalYawWithTarget(targetPosition);

        // 🌟 Hiệu ứng xuất hiện mượt mà (chạy độc lập với FPS)
        //followCamera.transform.DOMoveY(targetPosition.y + 0.5f, 0.5f)
        //    .SetEase(Ease.OutExpo)
        //    .SetUpdate(true); // ✅ Hoạt động ổn định trên Mobile

        followCamera.transform.DOMove(targetPosition, 0.5f)
    .SetEase(Ease.OutExpo)
    .SetUpdate(true);

        followCamera.transform.DORotateQuaternion(targetRotation, 0.5f)
            .SetEase(Ease.OutExpo)
            .SetUpdate(true); // ✅ Không bị ảnh hưởng bởi FPS

        // 🔥 Hiệu ứng rung nhẹ giúp tạo cảm giác tự nhiên
        followCamera.transform.DOShakePosition(0.2f, 0.1f, 3, 90, false, true)
            .SetUpdate(true);
        
        // ⏳ Kết thúc hiệu ứng, camera sẽ theo nhân vật
        // DOVirtual.DelayedCall(1f, () => StopFollowingBall()).SetUpdate(true);
    }
    public void MoveCameraViewMap()
    {
        MoveCameraToFPPOnline(ViewMapPosition, ViewMapLookAt, onlineViewMapFov);
    }
    public void MoveCameraViewMapExam()
    {
        MoveCameraToFPPOnline(ViewMapPositionExam, ViewMapLookAtExam, onlineViewMapFov);
    }
    public void MoveCameraToFPPOnline(Transform targetPosition, Transform targetLookAt, float? fieldOfView = null)
    {
        float targetFieldOfView = fieldOfView ?? onlineFirstPersonFov;
        followCamera.Follow = null;
        followCamera.LookAt = null;
        followCamera.Priority = 10; // Ưu tiên hiển thị camera này

        // Đảm bảo Camera bắt đầu ở vị trí mong muốn
        followCamera.transform.position = targetPosition.position;

        // Tính toán hướng quay và đảm bảo giá trị hợp lệ
        Vector3 lookDirection = targetLookAt.position - targetPosition.position;
        if (lookDirection.magnitude < 0.01f) lookDirection = followCamera.transform.forward;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized);
        followCamera.transform.rotation = targetRotation;

        // 🌟 Hiệu ứng xuất hiện mượt mà (chạy độc lập với FPS)
        //followCamera.transform.DOMoveY(targetPosition.y + 0.5f, 0.5f)
        //    .SetEase(Ease.OutExpo)
        //    .SetUpdate(true); // ✅ Hoạt động ổn định trên Mobile

        followCamera.transform.DOMove(targetPosition.position, 0.5f)
    .SetEase(Ease.OutExpo)
    .SetUpdate(true);

        //followCamera.transform.DORotateQuaternion(targetRotation, 0.5f)
        //    .SetEase(Ease.OutExpo)
        //    .SetUpdate(true); // ✅ Không bị ảnh hưởng bởi FPS
        //thay đổi ngay
        followCamera.transform.LookAt(targetLookAt, Vector3.up);

        // 🔥 Hiệu ứng rung nhẹ giúp tạo cảm giác tự nhiên
        followCamera.transform.DOShakePosition(0.2f, 0.1f, 3, 90, false, true)
            .SetUpdate(true);
        // Sau khi tween xong, bắt đầu follow head
        DOVirtual.DelayedCall(0.6f, () =>
        {
            followCamera.Follow = targetPosition;
            followCamera.LookAt = targetLookAt; // Optional nếu không cần
            followCamera.transform.LookAt(targetLookAt, Vector3.up);
            SetCameraFollowMode(CameraFollowMode.FixedOnHead, targetFieldOfView);
            //SyncLocalYawWithTarget(targetPosition);
           // SyncLocalYawWithTarget(targetLookAt);
        }).SetUpdate(true);
        // ⏳ Kết thúc hiệu ứng, camera sẽ theo nhân vật
        // DOVirtual.DelayedCall(1f, () => StopFollowingBall()).SetUpdate(true);
    }
    public void MoveCameraToFirstPersonAnchorOnline(Transform targetPosition, float? fieldOfView = null)
    {
        if (targetPosition == null || followCamera == null)
            return;

        float targetFieldOfView = fieldOfView ?? onlineFirstPersonFov;
        EnableCinemachine();
        followCamera.Follow = null;
        followCamera.LookAt = null;
        followCamera.Priority = 10;

        followCamera.transform.position = targetPosition.position;
        followCamera.transform.rotation = targetPosition.rotation;

        followCamera.transform.DOMove(targetPosition.position, 0.5f)
            .SetEase(Ease.OutExpo)
            .SetUpdate(true);

        followCamera.transform.DORotateQuaternion(targetPosition.rotation, 0.5f)
            .SetEase(Ease.OutExpo)
            .SetUpdate(true);

        followCamera.transform.DOShakePosition(0.2f, 0.1f, 3, 90, false, true)
            .SetUpdate(true);

        DOVirtual.DelayedCall(0.6f, () =>
        {
            followCamera.Follow = targetPosition;
            followCamera.LookAt = null;
            followCamera.transform.rotation = targetPosition.rotation;
            SetCameraFollowMode(CameraFollowMode.FixedOnHead, targetFieldOfView);
        }).SetUpdate(true);
    }

    public void SetCameraFollowMode(CameraFollowMode mode, float? fixedOnHeadFov = null)
    {
        var transposer = followCamera.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null) return;
        // Debug thông tin camera trước khi thay đổi
       // Debug.Log($"Current FollowOffset: {transposer.m_FollowOffset}");
        //Debug.Log($"Current XDamping: {transposer.m_XDamping}, YDamping: {transposer.m_YDamping}, ZDamping: {transposer.m_ZDamping}");
        if (mode == CameraFollowMode.FixedOnHead)
        {
            followCamera.m_Lens.FieldOfView = fixedOnHeadFov ?? onlineFirstPersonFov;
            transposer.m_FollowOffset = Vector3.zero;
            transposer.m_XDamping = 0f;
            transposer.m_YDamping = 0f;
            transposer.m_ZDamping = 0f; // giữ camera tương đối sát
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTarget;
        }
        else if (mode == CameraFollowMode.FollowFromDistance)
        {
            transposer.m_FollowOffset = new Vector3(0f, 2f, 2f);
            transposer.m_XDamping = 1f;
            transposer.m_YDamping = 1f;
            transposer.m_ZDamping = 1f;
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        }
    }

    public void PlayBananaSlipCinematic(Transform target, float duration)
    {
        if (target == null || followCamera == null)
            return;

        EnableCinemachine();

        if (bananaSlipCinematicRoutine != null)
            StopCoroutine(bananaSlipCinematicRoutine);

        bananaSlipCinematicRoutine = StartCoroutine(BananaSlipCinematicRoutine(target, duration));
    }

    private IEnumerator BananaSlipCinematicRoutine(Transform target, float duration)
    {
        var transposer = followCamera.GetCinemachineComponent<CinemachineTransposer>();

        int originalPriority = followCamera.Priority;
        int slipPriority = Mathf.Max(originalPriority, 30);
        float originalFov = followCamera.m_Lens.FieldOfView;
        Vector3 originalOffset = transposer != null ? transposer.m_FollowOffset : Vector3.zero;
        float originalXDamping = transposer != null ? transposer.m_XDamping : 0f;
        float originalYDamping = transposer != null ? transposer.m_YDamping : 0f;
        float originalZDamping = transposer != null ? transposer.m_ZDamping : 0f;
        var originalBinding = transposer != null ? transposer.m_BindingMode : CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;

        followCamera.Priority = slipPriority;
        followCamera.Follow = target;
        followCamera.LookAt = target;

        if (transposer != null)
        {
            transposer.m_FollowOffset = new Vector3(0.25f, 1.3f, -2.5f);
            transposer.m_XDamping = 0.2f;
            transposer.m_YDamping = 0.2f;
            transposer.m_ZDamping = 0.2f;
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        }

        followCamera.m_Lens.FieldOfView = 50f;
        followCamera.GetComponent<Camera>().DOShakeRotation(0.35f, 1.5f, 10, 90f, false, DG.Tweening.ShakeRandomnessMode.Harmonic)
         .SetUpdate(true);
 
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (target == null)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (transposer != null)
        {
            transposer.m_FollowOffset = originalOffset;
            transposer.m_XDamping = originalXDamping;
            transposer.m_YDamping = originalYDamping;
            transposer.m_ZDamping = originalZDamping;
            transposer.m_BindingMode = originalBinding;
        }

        followCamera.m_Lens.FieldOfView = originalFov;

        if (followCamera.Priority == slipPriority)
            followCamera.Priority = originalPriority;

        bananaSlipCinematicRoutine = null;
    }
    public void MoveCameraToFPPForExam(Vector3 targetPosition, Vector3 targetLookAt)
    {
        followCamera.Follow = null;
        followCamera.LookAt = null;
        followCamera.Priority = 10; // Ưu tiên hiển thị camera này

        // Đảm bảo Camera bắt đầu ở vị trí mong muốn
        followCamera.transform.position = targetPosition;

        // Tính toán hướng quay và đảm bảo giá trị hợp lệ
        Vector3 lookDirection = targetLookAt - targetPosition;
        if (lookDirection.magnitude < 0.01f) lookDirection = followCamera.transform.forward;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized);
        followCamera.transform.rotation = targetRotation;

        // 🌟 Hiệu ứng xuất hiện mượt mà (chạy độc lập với FPS)
            followCamera.transform.DOMoveY(targetPosition.y + 0.5f, 0.5f)
           .SetEase(Ease.OutExpo)
           .SetUpdate(true);
        // ✅ Hoạt động ổn định trên Mobile

 

        followCamera.transform.DORotateQuaternion(targetRotation, 0.5f)
            .SetEase(Ease.OutExpo)
            .SetUpdate(true); // ✅ Không bị ảnh hưởng bởi FPS

        // 🔥 Hiệu ứng rung nhẹ giúp tạo cảm giác tự nhiên
        followCamera.transform.DOShakePosition(0.2f, 0.1f, 3, 90, false, true)
            .SetUpdate(true);

        // ⏳ Kết thúc hiệu ứng, camera sẽ theo nhân vật
        //DOVirtual.DelayedCall(1f, () => StopFollowingBall()).SetUpdate(true);
    }



    //public IEnumerator MoveCameraToFPP3(Vector3 targetPosition, Vector3 targetLookAt)
    //{
    //    followCamera.Follow = null;
    //    followCamera.LookAt = null;
    //    followCamera.Priority = 10; // Đảm bảo camera này được ưu tiên

    //    float elapsedTime = 0f;
    //    Vector3 startPosition = followCamera.transform.position;
    //    Quaternion startRotation = followCamera.transform.rotation;
    //    float duration = 0.5f; // Điều chỉnh tốc độ di chuyển

    //    Quaternion targetRotation = Quaternion.LookRotation(targetLookAt - targetPosition); // Tính góc nhìn về targetLookAt

    //    while (elapsedTime < duration)
    //    {
    //        elapsedTime += Time.deltaTime;
    //        float t = Mathf.SmoothStep(0f, 1f, elapsedTime / duration); // Làm mượt chuyển động

    //        followCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
    //        followCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

    //        yield return null;
    //    }

    //    // Đảm bảo đến đúng vị trí
    //    followCamera.transform.position = targetPosition;
    //    followCamera.transform.rotation = targetRotation;

    //    StopFollowingBall();
    //}



    
    public void RotateCameraToPoint(Vector3 targetPosition)
    {
        // Xác định góc quay
        Quaternion targetRotation = Quaternion.LookRotation(targetPosition - transform.position);

        // Sử dụng DOTween để quay camera
        transform.DORotateQuaternion(targetRotation, 1.5f)
            .SetEase(Ease.OutExpo); // Làm mượt bằng Ease
    }
    

    public void OnSliderValueChanged(float value)
    {
        float shakeStrength = (value / 2) * 0.5f; // Giảm rung theo sức khỏe
        transform.DOShakePosition(0.3f, shakeStrength);
    }
 

    //Di chuyển trái phải
    public void StartMoveLeft()
    {
        ClientGameplayBridge.PlayerMovement.StartMoveLeft();
    }

    public void StopMoveLeft()
    {
        ClientGameplayBridge.PlayerMovement.StopMoveLeft();
    }

    public void StartMoveRight()
    {
        ClientGameplayBridge.PlayerMovement.StartMoveRight();
    }

    public void StopMoveRight()
    {
        ClientGameplayBridge.PlayerMovement.StopMoveRight();
    }
    #region Game online
    public void StartFollowingBallForMyself(int playerId, int IsExam)
    {
        if (NetworkObjectManager.Instance == null)
            return;

        var ball = NetworkObjectManager.Instance.GetActiveBallObject(playerId);
        if (ball == null)
            return;
        foreach (var entry in NetworkObjectManager.Instance.PlayerBalls)
        {
            var active = NetworkObjectManager.Instance.GetActiveBallObject(entry.Key);
            if (active != null)
            {
                var s = active.GetComponent<BallServerController>();
                if (s != null) s.cameraFollowBall = null; // Reset tất cả bi
            }
        }
        var script = ball.GetComponent<BallServerController>();
        script.cameraFollowBall = cameraTarget;
        EnableCinemachine();
        SetCameraFollowMode(CameraFollowMode.FollowFromDistance);
        //UIControllerOnline.Instance.UIforView();
        followCamera.Priority = 10;  // Bật camera theo viên bi
        followCamera.Follow = script.cameraFollowBall;
        followCamera.LookAt = script.cameraFollowBall;
        // DOTween để thay đổi Field of View của camera theo tốc độ viên bi

        if (IsExam == 1)
            followCamera.m_Lens.FieldOfView = 30f;
        else
        {
            // followCamera.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            followCamera.m_Lens.FieldOfView = 20f;
        }
    }

    public void StartFollowingPaperLegendCharacter(Transform followTarget)
    {
        if (followTarget == null || followCamera == null)
            return;

        EnsureCameraTarget();

        if (followBallCoroutine != null)
        {
            StopCoroutine(followBallCoroutine);
            followBallCoroutine = null;
        }

        cameraTarget.position = followTarget.position + Vector3.up * paperLegendLookAtHeight;
        cameraTarget.rotation = Quaternion.identity;

        EnableCinemachine();
        SetCameraFollowMode(CameraFollowMode.FollowFromDistance);

        var transposer = followCamera.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            transposer.m_FollowOffset = paperLegendFollowOffset;
            transposer.m_XDamping = paperLegendFollowDamping;
            transposer.m_YDamping = paperLegendFollowDamping;
            transposer.m_ZDamping = paperLegendFollowDamping;
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        }

        followCamera.Priority = paperLegendFollowPriority;
        followCamera.Follow = followTarget;
        followCamera.LookAt = cameraTarget;
        followCamera.m_Lens.FieldOfView = paperLegendFollowFov;

        followBallCoroutine = StartCoroutine(FollowPaperLegendCharacterPosition(followTarget));
    }

    public void PulsePaperLegendFollowFov(float extraFov, float holdSeconds)
    {
        if (followCamera == null)
            return;

        if (paperLegendFovTween != null && paperLegendFovTween.IsActive())
            paperLegendFovTween.Kill();

        float baseFov = Mathf.Max(1f, paperLegendFollowFov);
        float targetFov = baseFov + Mathf.Max(0f, extraFov);
        followCamera.m_Lens.FieldOfView = targetFov;

        paperLegendFovTween = DOTween
            .To(() => followCamera.m_Lens.FieldOfView, value => followCamera.m_Lens.FieldOfView = value, baseFov, 0.35f)
            .SetDelay(Mathf.Max(0f, holdSeconds))
            .OnComplete(() => paperLegendFovTween = null);
    }

    private void EnsureCameraTarget()
    {
        if (cameraTarget != null)
            return;

        var targetObject = new GameObject("CameraTarget_PaperLegends");
        cameraTarget = targetObject.transform;
    }

    private IEnumerator FollowPaperLegendCharacterPosition(Transform target)
    {
        while (target != null)
        {
            if (cameraTarget != null)
                cameraTarget.position = target.position + Vector3.up * paperLegendLookAtHeight;

            yield return null;
        }
    }



   
    public void StartFollowingBallOtherPlayer(int playerId,int IsExam)
    {
        Debug.Log($"Client đang theo dõi {playerId} ");
        var ball = NetworkObjectManager.Instance?.GetActiveBallObject(playerId);
        // var handle = ball.GetComponent<BallServerController>();
        if (ball == null)
        {
            Debug.LogWarning($"❌ Không tìm thấy bi đang hoạt động của người chơi {playerId}.");
            return;
        }

        var rbNet = ball.GetComponent<NetworkRigidbody3D>();
        var ballController = ball.GetComponent<BallServerController>();
        Transform followTarget = ballController != null ? ballController.GetCameraFocusTarget() : null;
        if (followTarget == null)
            followTarget = rbNet != null ? rbNet.InterpolationTarget : null;

        if (followTarget == null)
        {
            Debug.LogWarning($"❌ Missing camera follow target for player {playerId}");
            return;
        }
        // Giữ orientation ổn định để camera không xoay theo viên bi
        cameraTarget.position = followTarget.position;
        cameraTarget.rotation = Quaternion.identity;

        // Vị trí theo trực tiếp nhưng hướng nhìn dùng cameraTarget (không xoay)
        EnableCinemachine();
        SetCameraFollowMode(CameraFollowMode.FollowFromDistance);
        followCamera.Priority = 10;  // Bật camera theo viên bi
        followCamera.Follow = followTarget;
        followCamera.LookAt = cameraTarget;
        if (followBallCoroutine != null)
        {
            StopCoroutine(followBallCoroutine);
        }
        followBallCoroutine = StartCoroutine(FollowBallPosition(followTarget));
        // DOTween để thay đổi Field of View của camera theo tốc độ viên bi

        if (IsExam == 1)
            followCamera.m_Lens.FieldOfView = 30f;
        else
        {
            // followCamera.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            followCamera.m_Lens.FieldOfView = 20f;
        }
    }
    private IEnumerator FollowBallPosition(Transform target)
    {
        while (target != null)
        {
            cameraTarget.position = target.position;
            yield return null;
        }
    }

    private bool IsInsidePlayArea(BoxCollider area, Vector3 pos)
    {
        Vector3 local = area.transform.InverseTransformPoint(pos);
        Vector3 half = area.size / 2f;
        return (local.x >= -half.x && local.x <= half.x) &&
               (local.y >= -half.y && local.y <= half.y) &&
               (local.z >= -half.z && local.z <= half.z);
    }

    private Transform FindEscapingRingBall(NetworkObjectManager manager, NetworkRunner runner, BoxCollider area)
    {
        if (area == null)
            return null;

        List<Transform> escaping = new List<Transform>();
        foreach (var id in manager.ringBalls.EnumerateIds())
        {
            if (runner.TryFindObject(id, out var obj))
            {
                var rb = obj.GetComponent<NetworkRigidbody3D>();
                if (rb == null) continue;
                bool moving = rb.Rigidbody.linearVelocity.sqrMagnitude > 0.001f;
                bool inside = IsInsidePlayArea(area, obj.transform.position);
                if (!inside && moving)
                    escaping.Add(obj.transform);
            }
        }

        if (escaping.Count == 0)
            return null;
        return escaping[Random.Range(0, escaping.Count)];
    }
    private void UpdateMiniCamera()
    {
        if (miniCamera == null || NetworkObjectManager.Instance == null)
            return;

        var server = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.serverRPC : null;
        if (server == null || !server.HasStateAuthority)
            return; // chỉ host kiểm tra trạng thái

        var manager = NetworkObjectManager.Instance;
        var runner = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.runner : null;

        if (runner == null)
        {
            runner = GetComponent<NetworkRunner>() ?? FindObjectOfType<NetworkRunner>();
            if (GameManagerNetWork.Instance != null)
                GameManagerNetWork.Instance.runner = runner;
        }

        if (runner == null)
            return;

        bool moving = false;
        foreach (var id in manager.ringBalls.EnumerateIds())
        {
            if (runner.TryFindObject(id, out var obj))
            {
                var rb = obj.GetComponent<NetworkRigidbody3D>();
                if (rb != null && rb.Rigidbody.linearVelocity.sqrMagnitude > 0.001f)
                {
                    moving = true;
                }
            }
        }

        var host = GameSessionClientLocal.Instance;
        if (host != null)
        {
            var escape = FindEscapingRingBall(manager, runner, host.playArea);
            if (escape != null && trackedRingBall == null)
            {
                trackedRingBall = escape;
                UpdateMiniCamZoom();
            }
        }

        bool myBallOutside = false;
        if (host != null && GameManagerNetWork.Instance != null)
        {
            int myId = GameManagerNetWork.Instance.loginUserModel.UserId;
            var myBall = NetworkObjectManager.Instance.GetActiveBallObject(myId);
            if (myBall != null && host.playArea != null)
            {
                Vector3 localPos = host.playArea.transform.InverseTransformPoint(myBall.transform.position);
                Vector3 half = host.playArea.size / 2f;
                bool inside = (localPos.x >= -half.x && localPos.x <= half.x) &&
                               (localPos.y >= -half.y && localPos.y <= half.y) &&
                               (localPos.z >= -half.z && localPos.z <= half.z);
                myBallOutside = !inside;
            }
        }

        bool shouldShow = myBallOutside && (moving || miniCamPrevState);
        if (shouldShow != miniCamPrevState)
        {
            SetMiniCameraActive(shouldShow);
            server.RPC_ToggleMiniCamera(shouldShow); // gửi RPC cho toàn bộ client
        }

        MaintainMiniCameraOrientation();
    }

    public void SetMiniCameraActive(bool active)
    {
        if (miniCamera == null)
            return;

        if (active)
        {
            var area = GameSessionClientLocal.Instance.playArea;
            if (area != null)
            {
                Vector3 center = area.bounds.center;
                float size = Mathf.Max(area.bounds.size.x, area.bounds.size.z);
                Vector3 pos = center + miniCamOffset +
                               Vector3.up * size * 0.2f + Vector3.back * size * 0.3f;
                miniCamera.transform.position = pos;
                if (trackedRingBall != null)
                    miniCamera.transform.LookAt(trackedRingBall);
                else
                    miniCamera.transform.LookAt(center);
            }
            UpdateMiniCamZoom();
        }

        if (miniCamera.transform.parent != null)
            miniCamera.transform.SetParent(null); // detach to avoid inheriting rotations

        miniCamera.enabled = active;
        miniCamPrevState = active;
        if (!active)
        {
            trackedRingBall = null;
            UpdateMiniCamZoom();
        }
    }

    private void UpdateMiniCamZoom()
    {
        if (miniCamera == null)
            return;

        if (trackedRingBall != null)
        {
            miniCamera.rect = lockedMiniCamRect;
            miniCamera.fieldOfView = lockedMiniCamFOV;
        }
        else
        {
            miniCamera.rect = defaultMiniCamRect;
            miniCamera.fieldOfView = defaultMiniCamFOV;
        }
    }

    private void MaintainMiniCameraOrientation()
    {
        if (miniCamera == null || !miniCamera.enabled)
            return;

        bool examShoot = false;
        if (GameManagerNetWork.Instance != null)
        {
            var player = GameManagerNetWork.Instance.GetCurrentPlayerGame();
            examShoot = player.statusPlayer == StatusPlayer.ShootExam;
        }
        else if (NPCController.Instance != null)
        {
            examShoot = NPCController.Instance.currentState == TurnState.Exam;
        }

        if (examShoot)
            return;

        var area = GameSessionClientLocal.Instance != null ? GameSessionClientLocal.Instance.playArea : null;
        if (area == null)
            return;

        Vector3 center = area.bounds.center;
        float size = Mathf.Max(area.bounds.size.x, area.bounds.size.z);
        Vector3 pos = center + miniCamOffset + Vector3.up * size * 0.2f + Vector3.back * size * 0.3f;
        miniCamera.transform.position = pos;

        if (trackedRingBall != null)
        {
            bool outside = !IsInsidePlayArea(area, trackedRingBall.position);
            if (trackedRingBall.gameObject.activeInHierarchy && outside)
                miniCamera.transform.LookAt(trackedRingBall);
            else
            {
                trackedRingBall = null;
                UpdateMiniCamZoom();
            }
        }

        if (trackedRingBall == null)
            miniCamera.transform.rotation = Quaternion.LookRotation(center - pos);
    }

    public void PlayFloodCinematic(Transform waterTarget)
    {
        StartCoroutine(FloodCinematicRoutine(waterTarget));
    }

    private IEnumerator FloodCinematicRoutine(Transform waterTarget)
    {
        StartCoroutine(UIControllerOnline.Instance.ShowTurnIndicatorRunTime("Nước lũ dâng cao", 1, 1));
        EnableCinemachine();
        followCamera.Priority = 30;
        followCamera.Follow = null;
        followCamera.LookAt = null;

        Vector3 viewPos = overviewPosition != null ? overviewPosition.position : new Vector3(0, 10, 0);
        Quaternion viewRot = Quaternion.Euler(90f, 0f, 0f);
        followCamera.transform.DOMove(viewPos, 1f).SetEase(Ease.InOutSine);
        followCamera.transform.DORotateQuaternion(viewRot, 1f).SetEase(Ease.InOutSine);
        SoundManager.Instance?.PlayFloodWarning();
        yield return new WaitForSeconds(3f);

        if (waterTarget != null)
        {
            Vector3 dir = waterTarget.position - followCamera.transform.position;
            Quaternion lookRot = Quaternion.LookRotation(dir.normalized);
            followCamera.transform.DORotateQuaternion(lookRot, 1f).SetEase(Ease.InOutSine);
            DOTween.To(() => followCamera.m_Lens.FieldOfView, x => followCamera.m_Lens.FieldOfView = x, 40f, 4f);
        }
        yield return new WaitForSeconds(4f);
    }
  
    // Gọi mỗi khi kéo slider
    public void OnZoomSliderChanged(float value)
    {
        // Lerp FOV từ zoom mạnh tới FOV bình thường
        float newFOV = Mathf.Lerp(minFOV, maxFOV, value);
        followCamera.m_Lens.FieldOfView = newFOV;
    }
    public void PlaySlowMotionWithDetection(Vector3 direction)
    {
        StopKillCamInternal(true);
        if (slowMotionCoroutine != null)
            StopCoroutine(slowMotionCoroutine);
        slowMotionCoroutine = StartCoroutine(SlowMotionRoutine(direction, slowMotionScale, slowMotionDuration));
    }

    public void StopSlowMotion()
    {
        if (slowMotionCoroutine != null)
        {
            StopCoroutine(slowMotionCoroutine);
            slowMotionCoroutine = null;
        }
        StopKillCamInternal(true);
        KillKillCamTweens();
        Time.timeScale = 1f;
        if (brain != null)
            brain.m_IgnoreTimeScale = false;
        if (followCamera != null)
            followCamera.m_Lens.FieldOfView = defaultFOV;
    }

    public void PlayKillCamSlowMotionShooter(int shooterId, Transform shooterTarget, Transform focusTarget, Vector3 predictedPoint, float predictedTimeToHit)
    {
        killCamShooterTarget = shooterTarget != null ? shooterTarget : killCamShooterTarget;
        killCamFocusBallTarget = focusTarget != null ? focusTarget : killCamFocusBallTarget;
        killCamFallbackPoint = predictedPoint;
        BeginKillCam(shooterId, true, false, predictedTimeToHit, killCamShooterRampTime, killCamShooterTimeScale, killCamShooterZoomFov, true);
        SetVictimVignette(false);
    }

    public void PlayKillCamSlowMotionVictim(int shooterId, Transform shooterTarget, Transform victimTarget, Vector3 predictedPoint, float predictedTimeToHit)
    {
        killCamShooterTarget = shooterTarget != null ? shooterTarget : victimTarget;
        killCamFocusBallTarget = victimTarget != null ? victimTarget : shooterTarget;
        killCamFallbackPoint = predictedPoint;
        BeginKillCam(shooterId, false, true, predictedTimeToHit, killCamVictimRampTime, killCamVictimTimeScale, killCamVictimZoomFov, false);
        SetVictimVignette(true);
    }

    public void ConfirmKillCamHit(int shooterId, int victimId, Transform shooterTarget, Transform victimTarget, Vector3 hitPoint)
    {
        int localPlayerId = GetLocalPlayerId();
        bool shouldBeShooter = localPlayerId != 0 && localPlayerId == shooterId;
        bool shouldBeVictim = localPlayerId != 0 && localPlayerId == victimId;
        if (!shouldBeShooter && !shouldBeVictim)
            return;

        if (!killCamActive || killCamShooterId != shooterId)
        {
            if (shouldBeShooter)
                PlayKillCamSlowMotionShooter(shooterId, shooterTarget, victimTarget, hitPoint, 0f);
            else
                PlayKillCamSlowMotionVictim(shooterId, shooterTarget, victimTarget, hitPoint, 0f);
        }

        killCamShooterTarget = shooterTarget != null ? shooterTarget : killCamShooterTarget;
        killCamFocusBallTarget = victimTarget != null ? victimTarget : killCamFocusBallTarget;
        killCamFallbackPoint = hitPoint;

        float holdTime = killCamIsShooter ? killCamShooterPostHitHold : killCamVictimPostHitHold;
        ExtendKillCamHold(holdTime);

        if (killCamIsShooter)
            PlayShooterImpactPunch();

        if (killCamIsVictim)
            SetVictimVignette(true);
    }

    public void EndKillCamSlowMotion(int shooterId)
    {
        if (!killCamActive && killCamShooterId == 0)
            return;

        if (shooterId > 0 && killCamShooterId > 0 && shooterId != killCamShooterId)
            return;

        StopKillCamInternal(true);
    }

    private int GetLocalPlayerId()
    {
        return GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
    }

    private void BeginKillCam(int shooterId, bool isShooterRole, bool isVictimRole, float predictedTimeToHit, float rampTime, float targetTimeScale, float targetFov, bool overrideFollowWithFocus)
    {
        if (followCamera == null)
            return;

        if (slowMotionCoroutine != null)
        {
            StopCoroutine(slowMotionCoroutine);
            slowMotionCoroutine = null;
        }

        EnsureKillCamFocusTarget();
        if (!killCamActive)
            CacheCurrentFollowTargets();

        bool extendExistingHold = killCamActive && killCamShooterId == shooterId;
        killCamActive = true;
        killCamShooterId = shooterId;
        killCamIsShooter = isShooterRole;
        killCamIsVictim = isVictimRole;

        // Dùng safety timeout lớn — server sẽ gửi RPC_EndKillCamSlowMotion để tắt kill cam đúng lúc
        float safetyRelease = Time.realtimeSinceStartup + killCamSafetyTimeout;
        killCamReleaseAtRealtime = extendExistingHold ? Mathf.Max(killCamReleaseAtRealtime, safetyRelease) : safetyRelease;

        EnableCinemachine();
        // Bắt Cinemachine dùng unscaled time để camera phản hồi nhanh khi Time.timeScale thấp
        if (brain != null)
            brain.m_IgnoreTimeScale = true;
        followCamera.Priority = 10;
        if (overrideFollowWithFocus && killCamFocusTarget != null)
        {
            followCamera.Follow = killCamFocusTarget;
            followCamera.LookAt = killCamFocusTarget;
        }

        StartKillCamFocusRoutine();
        TweenTimeScale(targetTimeScale, rampTime);
        TweenFov(targetFov, rampTime);
    }

    private void EnsureKillCamFocusTarget()
    {
        if (killCamFocusTarget != null)
            return;

        GameObject focusObj = new GameObject("KillCamFocusTarget");
        focusObj.transform.SetParent(transform, false);
        killCamFocusTarget = focusObj.transform;
        killCamFocusTarget.position = cameraTarget != null ? cameraTarget.position : transform.position;
    }

    private void CacheCurrentFollowTargets()
    {
        previousFollowTarget = followCamera != null ? followCamera.Follow : null;
        previousLookAtTarget = followCamera != null ? followCamera.LookAt : null;
    }

    private void RestoreFollowTargets(Transform fallbackFollowTarget)
    {
        if (followCamera == null)
            return;

        if (previousFollowTarget != null)
            followCamera.Follow = previousFollowTarget;
        else if (fallbackFollowTarget != null)
            followCamera.Follow = fallbackFollowTarget;

        if (previousLookAtTarget != null)
            followCamera.LookAt = previousLookAtTarget;
        else if (cameraTarget != null)
            followCamera.LookAt = cameraTarget;

        previousFollowTarget = null;
        previousLookAtTarget = null;
    }

    private void StartKillCamFocusRoutine()
    {
        if (killCamFocusRoutine != null)
            StopCoroutine(killCamFocusRoutine);
        killCamFocusRoutine = StartCoroutine(KillCamFocusRoutine());
    }

    private IEnumerator KillCamFocusRoutine()
    {
        while (killCamActive)
        {
            UpdateKillCamFocusTarget();
            if (killCamReleaseAtRealtime > 0f && Time.realtimeSinceStartup >= killCamReleaseAtRealtime)
            {
                StopKillCamInternal(true);
                yield break;
            }

            yield return null;
        }
    }

    private void UpdateKillCamFocusTarget()
    {
        if (killCamFocusTarget == null)
            return;

        Vector3 shooterPos = killCamShooterTarget != null ? killCamShooterTarget.position : killCamFallbackPoint;
        Vector3 focusPos = killCamFocusBallTarget != null ? killCamFocusBallTarget.position : killCamFallbackPoint;
        Vector3 midpoint = (shooterPos + focusPos) * 0.5f;
        midpoint.y += killCamFocusHeight;
        killCamFocusTarget.position = midpoint;
    }

    private void ExtendKillCamHold(float holdTime)
    {
        float clampedHold = Mathf.Max(holdTime, killCamMinDuration);
        killCamReleaseAtRealtime = Mathf.Max(killCamReleaseAtRealtime, Time.realtimeSinceStartup + clampedHold);
    }

    private void TweenTimeScale(float targetScale, float duration)
    {
        if (killCamTimeScaleTween != null && killCamTimeScaleTween.IsActive())
            killCamTimeScaleTween.Kill();

        float safeDuration = Mathf.Max(0.01f, duration);
        killCamTimeScaleTween = DOTween.To(() => Time.timeScale, x => Time.timeScale = x, targetScale, safeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void TweenFov(float targetFov, float duration)
    {
        if (followCamera == null)
            return;

        if (killCamFovTween != null && killCamFovTween.IsActive())
            killCamFovTween.Kill();

        float safeDuration = Mathf.Max(0.01f, duration);
        killCamFovTween = DOTween.To(() => followCamera.m_Lens.FieldOfView, x => followCamera.m_Lens.FieldOfView = x, targetFov, safeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void PlayShooterImpactPunch()
    {
        if (!killCamIsShooter || followCamera == null)
            return;

        if (killCamFovTween != null && killCamFovTween.IsActive())
            killCamFovTween.Kill();

        float impactFov = Mathf.Max(6f, killCamShooterZoomFov - 2f);
        followCamera.m_Lens.FieldOfView = impactFov;
        killCamFovTween = DOTween.To(() => followCamera.m_Lens.FieldOfView, x => followCamera.m_Lens.FieldOfView = x, killCamShooterZoomFov, 0.18f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void SetVictimVignette(bool enabled)
    {
        if (killCamVictimVignetteImage == null)
            return;

        if (killCamVignetteTween != null && killCamVignetteTween.IsActive())
            killCamVignetteTween.Kill();

        killCamVictimVignetteImage.gameObject.SetActive(true);
        float targetAlpha = enabled ? killCamVictimVignetteAlpha : 0f;
        killCamVignetteTween = killCamVictimVignetteImage.DOFade(targetAlpha, Mathf.Max(0.01f, killCamVignetteFadeTime))
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (!enabled && killCamVictimVignetteImage != null)
                {
                    var color = killCamVictimVignetteImage.color;
                    color.a = 0f;
                    killCamVictimVignetteImage.color = color;
                    killCamVictimVignetteImage.gameObject.SetActive(false);
                }
            });
    }

    private void StopKillCamInternal(bool resetFollow)
    {
        if (killCamFocusRoutine != null)
        {
            StopCoroutine(killCamFocusRoutine);
            killCamFocusRoutine = null;
        }

        bool wasActive = killCamActive || killCamIsShooter || killCamIsVictim;
        Transform fallbackFollowTarget = killCamShooterTarget;
        killCamActive = false;
        killCamIsShooter = false;
        killCamIsVictim = false;
        killCamShooterId = 0;
        killCamReleaseAtRealtime = 0f;

        KillKillCamTweens();

        if (wasActive)
        {
            Time.timeScale = 1f;
            // Trả Cinemachine về dùng scaled time bình thường
            if (brain != null)
                brain.m_IgnoreTimeScale = false;
            if (followCamera != null)
                followCamera.m_Lens.FieldOfView = defaultFOV;
            if (resetFollow)
                RestoreFollowTargets(fallbackFollowTarget);
        }

        SetVictimVignette(false);

        killCamShooterTarget = null;
        killCamFocusBallTarget = null;
    }

    private void KillKillCamTweens()
    {
        if (killCamTimeScaleTween != null && killCamTimeScaleTween.IsActive())
            killCamTimeScaleTween.Kill();
        if (killCamFovTween != null && killCamFovTween.IsActive())
            killCamFovTween.Kill();
        if (killCamVignetteTween != null && killCamVignetteTween.IsActive())
            killCamVignetteTween.Kill();

        killCamTimeScaleTween = null;
        killCamFovTween = null;
        killCamVignetteTween = null;
    }

    private IEnumerator SlowMotionRoutine(Vector3 direction, float targetScale, float duration)
    {
        float originalTimeScale = Time.timeScale;
        float originalFOV = followCamera != null ? followCamera.m_Lens.FieldOfView : 30f;

        // Tween TimeScale chậm lại mượt
        DOTween.To(() => Time.timeScale, x => Time.timeScale = x, targetScale, 0.25f).SetUpdate(true);

        // Thu hẹp FOV để tạo cảm giác "zoom-in"
        if (followCamera != null)
            DOTween.To(() => followCamera.m_Lens.FieldOfView, x => followCamera.m_Lens.FieldOfView = x, slowMotionZoomFOV, 0.25f).SetUpdate(true);

        float elapsed = 0f;
        float detectDistance = 3f;
        float radius = 0.25f;

        bool exitedEarly = false;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            RaycastHit hit;
            if (Physics.SphereCast(transform.position, radius, direction, out hit, detectDistance))
            {
                if (hit.collider.CompareTag("BallPlayer") && hit.collider.gameObject != gameObject)
                {
                    // Nếu phát hiện gần trúng viên bi đối thủ → kết thúc sớm slow-mo
                    exitedEarly = true;
                    break;
                }
            }

            yield return null;
        }

        // Tween trở về bình thường nhanh chóng
        DOTween.To(() => Time.timeScale, x => Time.timeScale = x, originalTimeScale, 0.15f).SetUpdate(true);

        if (followCamera != null)
            DOTween.To(() => followCamera.m_Lens.FieldOfView, x => followCamera.m_Lens.FieldOfView = x, originalFOV, 0.2f).SetUpdate(true);

        // Đảm bảo TimeScale khôi phục hoàn toàn
        Time.timeScale = originalTimeScale;
        slowMotionCoroutine = null;
    }

    void OnGUI()
    {
        if (miniCamera != null && miniCamera.enabled && miniCamBorderTex != null)
        {
            Rect camRect = miniCamera.rect;
            float x = camRect.x * Screen.width;
            float y = (1f - camRect.y - camRect.height) * Screen.height;
            float w = camRect.width * Screen.width;
            float h = camRect.height * Screen.height;
            Rect r = new Rect(x, y, w, h);

            GUI.color = miniCamBorderColor;
            float t = miniCamBorderSize;
            GUI.DrawTexture(new Rect(r.x - t, r.y - t, r.width + 2 * t, t), miniCamBorderTex);
            GUI.DrawTexture(new Rect(r.x - t, r.y + r.height, r.width + 2 * t, t), miniCamBorderTex);
            GUI.DrawTexture(new Rect(r.x - t, r.y, t, r.height), miniCamBorderTex);
            GUI.DrawTexture(new Rect(r.x + r.width, r.y, t, r.height), miniCamBorderTex);
            GUI.color = Color.white;
        }
    }

    //private void OnDisable()
    //{
    //    // Kill any tweens on this camera when disabled
    //    transform.DOKill();
    //    if (followCamera != null)
    //        followCamera.transform.DOKill();
    //}

    #endregion

}
