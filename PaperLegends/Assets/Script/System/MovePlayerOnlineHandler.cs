using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MovePlayerOnlineHandler : MonoBehaviour
{
    public static MovePlayerOnlineHandler Instance;

    [Header("PLAYER CONTROL CONFIG")]
    public float rotationSpeed = 3.0f;

    [Header("MOVE BUTTON UI")]
    [SerializeField, Tooltip("Button giữ để nhân vật dịch sang trái")]
    private Button moveLeftButton;
    [SerializeField, Tooltip("Button giữ để nhân vật dịch sang phải")]
    private Button moveRightButton;

    public PlayerNetworkHandler LocalPlayerHandler { get; private set; }
    public CoreTutorialPlayerLocalController LocalTutorialPlayerHandler { get; private set; }

    private bool moveLeft = false;
    private bool moveRight = false;
    //Hướng đầu lên hoặc xuống
    private float currentPitch = 0f;
    //Quay nhân vật rotation tại chỗ sang trái hoặc phải
    private float currentYaw = 0f;
    [SerializeField, Tooltip("Giới hạn xoay ngang tối đa của đầu nhân vật")] private float maxHeadYaw = 60f;
    [SerializeField, Tooltip("Giới hạn xoay dọc tối đa của đầu nhân vật")] private float maxHeadPitch = 30f;
    private Vector2 lastTouchPosition;
    private bool isTouching = false;
    private bool yawInputRegistered;

    private Coroutine movementLoop;
    private ShotParams? pendingShot;
    private bool animStateRequested;
    private CharacterAnimState pendingAnimState;

    private void Awake()
    {
        Instance = this;
        BindMoveButtons();
    }

    private void Start()
    {
        if (movementLoop == null)
            movementLoop = StartCoroutine(MovementRoutine());
    }

    private void OnEnable()
    {
        BindMoveButtons();

        if (movementLoop == null)
            movementLoop = StartCoroutine(MovementRoutine());
    }

    private void OnDisable()
    {
        StopMoveLeft();
        StopMoveRight();
        StopMovementLoop();
        LocalPlayerHandler = null;
        LocalTutorialPlayerHandler = null;
    }

    public void SetMoveButtons(Button leftButton, Button rightButton)
    {
        moveLeftButton = leftButton;
        moveRightButton = rightButton;
        BindMoveButtons();
    }

    private void BindMoveButtons()
    {
        BindMoveButton(moveLeftButton, true);
        BindMoveButton(moveRightButton, false);
    }

    private void BindMoveButton(Button button, bool isLeftButton)
    {
        if (button == null)
            return;

        var relay = button.GetComponent<MovePlayerOnlineHoldButtonRelay>();
        if (relay == null)
            relay = button.gameObject.AddComponent<MovePlayerOnlineHoldButtonRelay>();

        relay.Configure(this, isLeftButton);
    }

    public void StopMovementLoop()
    {
        if (movementLoop != null)
        {
            StopCoroutine(movementLoop);
            movementLoop = null;
        }
    }

    public void SetLocalPlayerHandler(PlayerNetworkHandler handler)
    {
        if (handler == null)
            return;

        LocalPlayerHandler = handler;
    }

    public void SetLocalTutorialPlayerHandler(CoreTutorialPlayerLocalController handler)
    {
        if (handler == null || !handler.IsTutorialActive)
            return;

        LocalTutorialPlayerHandler = handler;
        currentYaw = Mathf.Repeat(handler.transform.rotation.eulerAngles.y, 360f);
        currentPitch = ClampPitch(handler.CurrentAimPitch);
        yawInputRegistered = false;
        handler.ApplyLookRotation(currentYaw, currentPitch);
    }

    public void ClearLocalTutorialPlayerHandler(CoreTutorialPlayerLocalController handler)
    {
        if (LocalTutorialPlayerHandler == handler)
            LocalTutorialPlayerHandler = null;
    }

    private PlayerNetworkHandler ResolveLocalPlayerHandler()
    {
        if (LocalPlayerHandler != null)
            return LocalPlayerHandler;

        var server = GameManagerNetWork.Instance?.serverRPC;
        var loginUser = GameManagerNetWork.Instance?.loginUserModel;
        if (server == null || loginUser == null)
            return null;

        var playerObject = server.GetPlayerObject(loginUser.UserId);
        if (playerObject == null)
            return null;

        LocalPlayerHandler = playerObject.GetComponent<PlayerNetworkHandler>();
        return LocalPlayerHandler;
    }

    private CoreTutorialPlayerLocalController ResolveLocalTutorialPlayerHandler()
    {
        if (LocalTutorialPlayerHandler != null && LocalTutorialPlayerHandler.IsTutorialActive)
            return LocalTutorialPlayerHandler;

        LocalTutorialPlayerHandler = CoreShootingTutorialController.Instance?.LocalPlayerController;
        return LocalTutorialPlayerHandler != null && LocalTutorialPlayerHandler.IsTutorialActive
            ? LocalTutorialPlayerHandler
            : null;
    }

    private IEnumerator MovementRoutine()
    {
        while (true)
        {
            CoreTutorialPlayerLocalController tutorialPlayer = ResolveLocalTutorialPlayerHandler();
            if (tutorialPlayer != null)
            {
                HandleTutorialMovement(tutorialPlayer);
                HandleTutorialRotation(tutorialPlayer);
            }
            else if (GameManagerNetWork.Instance != null
                && GameManagerNetWork.Instance.serverRPC != null
                && GameManagerNetWork.Instance.serverRPC.TryGetStatusLoading(out var status)
                && (status == StatusLoadingGame.isExam
                || status == StatusLoadingGame.StartTurn
                || status == StatusLoadingGame.ContinueTurn
                || status == StatusLoadingGame.NextTurn

                ))
            {
                HandleMovement();
                HandleRotation();
            }
            yield return null;
        }
    }

    private void HandleTutorialMovement(CoreTutorialPlayerLocalController handler)
    {
        if (handler == null || !handler.CanReceivePlayerInput)
            return;

        if (moveLeft)
            handler.MoveHorizontal(-1);
        if (moveRight)
            handler.MoveHorizontal(1);
    }

    private void HandleMovement()
    {
        var handler = ResolveLocalPlayerHandler();
        if (handler == null || !handler.IsNetworkStateReady || handler.IsMarkedDestroyed)
            return;

        // Online movement is sent through Fusion input in GetInput().
        // The server applies MoveHorizontal so the position remains authoritative.
    }
    private void HandleRotation()
    {
        var handler = ResolveLocalPlayerHandler();
        if (handler == null || !handler.IsNetworkStateReady || handler.IsMarkedDestroyed)
            return;

        ApplyRotationFromPointerInput();
    }

    private void HandleTutorialRotation(CoreTutorialPlayerLocalController handler)
    {
        if (handler == null || !handler.CanReceivePlayerInput)
            return;

        ApplyRotationFromPointerInput();
    }

    private void ApplyRotationFromPointerInput()
    {
        if (IsPointerOverUi_New())
            return;

        Vector2 delta = Vector2.zero;

        // TOUCH (Android / iOS)
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed &&
            !ShootBallJoystick.isDraggingJoystick)
        {
            var touch = Touchscreen.current.primaryTouch;

            if (touch.press.wasPressedThisFrame)
            {
                lastTouchPosition = touch.position.ReadValue();
                isTouching = true;
            }
            else if (isTouching && touch.position.ReadValue() != lastTouchPosition)
            {
                Vector2 currentPos = touch.position.ReadValue();
                delta = currentPos - lastTouchPosition;
                lastTouchPosition = currentPos;
            }

            if (touch.press.wasReleasedThisFrame)
            {
                isTouching = false;
            }
        }
        // MOUSE (Editor / PC)
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            delta.x = Mouse.current.delta.ReadValue().x;
            delta.y = Mouse.current.delta.ReadValue().y;
        }

        if (delta.sqrMagnitude > 0.0001f)
        {
            float deltaX = delta.x * rotationSpeed * Time.deltaTime;
            float deltaY = delta.y * rotationSpeed * Time.deltaTime;
            ApplyCameraAndPlayerRotation(deltaX, deltaY);
        }
    }


    private bool IsPointerOverUi_New()
    {
        if (EventSystem.current == null)
            return false;

        // Touch
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed)
        {
            return EventSystem.current.IsPointerOverGameObject();
        }

        // Mouse
        if (Mouse.current != null)
        {
            return EventSystem.current.IsPointerOverGameObject();
        }

        return false;
    }


    private void ApplyCameraAndPlayerRotation(float deltaX, float deltaY)
    {
        // Cộng dồn góc quay ngang, giữ dương quay phải - âm quay trái
        currentYaw += deltaX;
        // Pitch đảo dấu vì kéo lên màn hình tương đương với nhìn xuống
        currentPitch -= deltaY;
        // Giới hạn góc nhìn dọc theo cấu hình để tránh gập đầu quá mức
        currentPitch = ClampPitch(currentPitch);
        // Đưa yaw về khoảng [0, 360) để tránh tràn số và dễ đồng bộ
        currentYaw = Mathf.Repeat(currentYaw, 360f);

        // Đánh dấu đã có input yaw nhằm gửi lên server trong gói input tiếp theo
        if (Mathf.Abs(deltaX) > 0.001f)
            yawInputRegistered = true;

        // Cập nhật ngay lập tức vị trí point mà nhân vật đang nhìn
        UpdatePointPositionImmediate();
    }

    private void UpdatePointPositionImmediate()
    {
        var tutorialHandler = ResolveLocalTutorialPlayerHandler();
        if (tutorialHandler != null)
        {
            tutorialHandler.ApplyLookRotation(currentYaw, currentPitch);
            return;
        }

        var handler = ResolveLocalPlayerHandler();
        if (handler == null)
            return;
        // Đẩy pitch hiện tại sang handler để cập nhật target nhìn (điểm tay/chân) ngay trong frame này
        handler.UpdatePointPosition(currentPitch);
    }

    public void RotateYawTowardsPosition(Vector3 targetPosition)
    {
        var handler = ResolveLocalPlayerHandler();
        if (handler == null || !handler.IsNetworkStateReady || handler.IsMarkedDestroyed)
            return;

        Vector3 origin = handler.HeadTransform != null
            ? handler.HeadTransform.position
            : handler.transform.position;

        Vector3 direction = targetPosition - origin;
        direction.y = 0f;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        float previousYaw = currentYaw;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        currentYaw = Mathf.Repeat(targetRotation.eulerAngles.y, 360f);

        float yawDelta = Mathf.Abs(Mathf.DeltaAngle(previousYaw, currentYaw));
        yawInputRegistered = yawDelta > 0.001f;

        UpdatePointPositionImmediate();
    }

    public void RotateSightingPoint(Vector3 lookAtTarget)
    {
        Debug.Log($"Xoay nhân vật về hướng {lookAtTarget}");
        var handler = ResolveLocalPlayerHandler();
        if (handler == null || !handler.IsNetworkStateReady || handler.IsMarkedDestroyed)
            return;

        float previousYaw = currentYaw;

        Vector3 origin = handler.HeadTransform != null
            ? handler.HeadTransform.position
            : handler.transform.position;

        Vector3 lookDirection = lookAtTarget - origin;
        if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
            return;
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

        float targetYaw = targetRotation.eulerAngles.y;
        float targetPitch = targetRotation.eulerAngles.x;
        if (targetPitch > 180f)
            targetPitch -= 360f;

        currentYaw = Mathf.Repeat(targetYaw, 360f);
        currentPitch = ClampPitch(targetPitch);

        float yawDelta = Mathf.Abs(Mathf.DeltaAngle(previousYaw, currentYaw));
        yawInputRegistered = yawDelta > 0.001f;

        UpdatePointPositionImmediate();
    }

    public void ApplyServerRotation(float yaw, float pitch)
    {
        currentYaw = Mathf.Repeat(yaw, 360f);
        currentPitch = ClampPitch(pitch);
        yawInputRegistered = false;

        UpdatePointPositionImmediate();
    }

    public bool CanSendNetworkInput()
    {
        var handler = ResolveLocalPlayerHandler();
        return handler != null && handler.IsNetworkStateReady && !handler.IsMarkedDestroyed;
    }

    public PlayerInputData GetInput()
    {
        var input = new PlayerInputData
        {
            yaw = currentYaw,
            pitch = currentPitch,
            hasYawInput = yawInputRegistered
        };

        yawInputRegistered = false;

        var handler = ResolveLocalPlayerHandler();
        if (handler != null && handler.IsNetworkStateReady && !handler.IsMarkedDestroyed)
        {
            input.moveHorizontal = ResolveHorizontalMoveInput(handler);

            if (handler.FingerPosition != null)
            {
                Vector3 fingerPosition = handler.FingerPosition.position;
                input.fingerPosition = fingerPosition;
                input.hasFingerPosition = true;
            }
        }

        var powerBar = PowerBarController.Instance;
        if (powerBar != null && powerBar.powerSlider != null)
        {
            float powerValue = powerBar.isShootting ? powerBar.powerSlider.value : 0f;
            input.fingerRigPower = Mathf.Clamp01(powerValue);
            input.hasFingerRigPower = true;
        }

        if (pendingShot.HasValue)
        {
            input.shotRequested = true;
            input.shotParams = pendingShot.Value;
            pendingShot = null;
        }

        if (animStateRequested)
        {
            input.animStateRequested = true;
            input.animState = pendingAnimState;
            animStateRequested = false;
        }

        return input;
    }

    private int ResolveHorizontalMoveInput(PlayerNetworkHandler handler)
    {
        if (handler == null || !handler.IsNetworkStateReady || handler.IsMarkedDestroyed)
            return 0;

        if (moveLeft == moveRight)
            return 0;

        bool isExamMove = handler.PlayerModel.statusPlayer == StatusPlayer.ShootExam;
        if (isExamMove)
            return moveLeft ? -1 : 1;

        // Ngoài lượt thi camera/mức bắn đang ngược hướng, giữ mapping cũ để nút bấm vẫn đúng cảm giác trái/phải.
        return moveLeft ? 1 : -1;
    }

    public void QueueShotInput(ShotParams shot)
    {
        pendingShot = shot;
    }

    public void RequestAnimState(CharacterAnimState state)
    {
        pendingAnimState = state;
        animStateRequested = true;
    }

    public void SetYaw(float yaw)
    {
        currentYaw = Mathf.Repeat(yaw, 360f);
        yawInputRegistered = false;
    }

    private float ClampPitch(float pitch)
    {
        float minPitch = -Mathf.Abs(maxHeadPitch);
        float maxPitch = Mathf.Abs(maxHeadPitch);
        // Giữ pitch trong biên độ cho phép để tránh xoay đầu vượt quá giới hạn tự nhiên
        return Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    public void StartMoveLeft() => moveLeft = true;
    public void StopMoveLeft() => moveLeft = false;
    public void StartMoveRight() => moveRight = true;
    public void StopMoveRight() => moveRight = false;
}

public sealed class MovePlayerOnlineHoldButtonRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
{
    private MovePlayerOnlineHandler owner;
    private bool isLeftButton;

    public void Configure(MovePlayerOnlineHandler handler, bool leftButton)
    {
        owner = handler;
        isLeftButton = leftButton;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (owner == null)
            return;

        if (isLeftButton)
            owner.StartMoveLeft();
        else
            owner.StartMoveRight();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopMove();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopMove();
    }

    public void OnCancel(BaseEventData eventData)
    {
        StopMove();
    }

    private void OnDisable()
    {
        StopMove();
    }

    private void StopMove()
    {
        if (owner == null)
            return;

        if (isLeftButton)
            owner.StopMoveLeft();
        else
            owner.StopMoveRight();
    }
}
