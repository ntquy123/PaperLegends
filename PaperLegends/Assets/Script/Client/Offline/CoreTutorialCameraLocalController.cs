using Cinemachine;
using DG.Tweening;
using UnityEngine;

public sealed class CoreTutorialCameraLocalController : MonoBehaviour
{
    // Tutorial camera tuning lives here only; this controller is created at runtime.
    private const int TutorialPriority = 30;
    private const float FirstPersonFieldOfView = 60f;
    private const float ShotFieldOfView = 46f;
    private const float FieldOfViewTransition = 0.25f;
    private const float ShotFollowHeightOffset = 1.05f;
    private const float ShotFollowBackOffset = -0.625f;
    private static readonly Vector3 ShotFollowOffset = new Vector3(0f, ShotFollowHeightOffset, ShotFollowBackOffset);
    private const float ShotFollowDamping = 0.28f;
    private const float FollowRotationSpeed = 6f;
    private const float VelocityDirectionThreshold = 0.02f;

    private CinemachineVirtualCamera virtualCamera;
    private CinemachineTransposer transposer;
    private CinemachineBrain brain;
    private CameraRotation onlineCameraController;
    private Transform ballFollowTarget;
    private Rigidbody followedBall;
    private Tween fovTween;

    private Transform originalFollow;
    private Transform originalLookAt;
    private int originalPriority;
    private float originalFieldOfView;
    private bool originalBrainEnabled;
    private bool hasOriginalBrainState;
    private bool onlineCameraWasEnabled;
    private Vector3 originalFollowOffset;
    private float originalXDamping;
    private float originalYDamping;
    private float originalZDamping;
    private CinemachineTransposer.BindingMode originalBindingMode;
    private bool hasOriginalTransposerState;
    private bool isInitialized;

    public bool InitializeForTutorial()
    {
        if (isInitialized)
        {
            return virtualCamera != null;
        }

        onlineCameraController = FindFirstObjectByType<CameraRotation>(FindObjectsInactive.Include);
        virtualCamera = onlineCameraController != null
            ? onlineCameraController.followCamera
            : FindFirstObjectByType<CinemachineVirtualCamera>(FindObjectsInactive.Include);

        if (virtualCamera == null)
        {
            Debug.LogWarning("Core tutorial camera could not find a CinemachineVirtualCamera in the map.");
            return false;
        }

        originalFollow = virtualCamera.Follow;
        originalLookAt = virtualCamera.LookAt;
        originalPriority = virtualCamera.Priority;
        originalFieldOfView = virtualCamera.m_Lens.FieldOfView;

        if (onlineCameraController != null)
        {
            onlineCameraWasEnabled = onlineCameraController.enabled;
            onlineCameraController.enabled = false;
        }

        transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            hasOriginalTransposerState = true;
            originalFollowOffset = transposer.m_FollowOffset;
            originalXDamping = transposer.m_XDamping;
            originalYDamping = transposer.m_YDamping;
            originalZDamping = transposer.m_ZDamping;
            originalBindingMode = transposer.m_BindingMode;
        }

        Camera mainCamera = Camera.main;
        brain = mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
        {
            hasOriginalBrainState = true;
            originalBrainEnabled = brain.enabled;
            brain.enabled = true;
        }

        GameObject targetObject = new GameObject("CoreTutorialBallCameraTarget");
        targetObject.transform.SetParent(transform, false);
        ballFollowTarget = targetObject.transform;
        isInitialized = true;
        return true;
    }

    public void ShowPlayerAimView(Transform cameraAnchor, Transform lookTarget)
    {
        if (!EnsureInitialized() || cameraAnchor == null || lookTarget == null)
        {
            return;
        }

        followedBall = null;
        virtualCamera.Follow = cameraAnchor;
        virtualCamera.LookAt = lookTarget;
        virtualCamera.Priority = TutorialPriority;

        if (transposer != null)
        {
            transposer.m_FollowOffset = Vector3.zero;
            transposer.m_XDamping = 0f;
            transposer.m_YDamping = 0f;
            transposer.m_ZDamping = 0f;
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTarget;
        }

        Vector3 direction = lookTarget.position - cameraAnchor.position;
        Quaternion rotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : cameraAnchor.rotation;
        virtualCamera.transform.SetPositionAndRotation(cameraAnchor.position, rotation);
        TweenFieldOfView(FirstPersonFieldOfView);
    }

    public void FollowShot(Rigidbody ballBody, Vector3 initialDirection)
    {
        if (!EnsureInitialized() || ballBody == null || ballFollowTarget == null)
        {
            return;
        }

        followedBall = ballBody;
        ballFollowTarget.position = ballBody.worldCenterOfMass;
        ballFollowTarget.rotation = CreateFollowRotation(initialDirection, transform.rotation);

        virtualCamera.Priority = TutorialPriority;
        virtualCamera.Follow = ballFollowTarget;
        virtualCamera.LookAt = ballBody.transform;

        if (transposer != null)
        {
            transposer.m_FollowOffset = ShotFollowOffset;
            transposer.m_XDamping = ShotFollowDamping;
            transposer.m_YDamping = ShotFollowDamping;
            transposer.m_ZDamping = ShotFollowDamping;
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        }

        TweenFieldOfView(ShotFieldOfView);
    }

    public void StopFollowingShot()
    {
        followedBall = null;
    }

    public void Shutdown()
    {
        followedBall = null;
        fovTween?.Kill();
        fovTween = null;

        if (!isInitialized || virtualCamera == null)
        {
            return;
        }

        virtualCamera.Follow = originalFollow;
        virtualCamera.LookAt = originalLookAt;
        virtualCamera.Priority = originalPriority;
        virtualCamera.m_Lens.FieldOfView = originalFieldOfView;

        if (transposer != null && hasOriginalTransposerState)
        {
            transposer.m_FollowOffset = originalFollowOffset;
            transposer.m_XDamping = originalXDamping;
            transposer.m_YDamping = originalYDamping;
            transposer.m_ZDamping = originalZDamping;
            transposer.m_BindingMode = originalBindingMode;
        }

        if (brain != null && hasOriginalBrainState)
        {
            brain.enabled = originalBrainEnabled;
        }

        if (onlineCameraController != null && onlineCameraWasEnabled)
        {
            onlineCameraController.enabled = true;
        }

        isInitialized = false;
    }

    private void LateUpdate()
    {
        if (followedBall == null || ballFollowTarget == null)
        {
            return;
        }

        ballFollowTarget.position = followedBall.worldCenterOfMass;
        Vector3 velocity = followedBall.linearVelocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude <= VelocityDirectionThreshold * VelocityDirectionThreshold)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        ballFollowTarget.rotation = Quaternion.Slerp(
            ballFollowTarget.rotation,
            targetRotation,
            FollowRotationSpeed * Time.deltaTime);
    }

    private bool EnsureInitialized()
    {
        return isInitialized || InitializeForTutorial();
    }

    private void TweenFieldOfView(float targetValue)
    {
        fovTween?.Kill();
        fovTween = DOTween.To(
                () => virtualCamera.m_Lens.FieldOfView,
                value => virtualCamera.m_Lens.FieldOfView = value,
                targetValue,
                FieldOfViewTransition)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private static Quaternion CreateFollowRotation(Vector3 direction, Quaternion fallback)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : fallback;
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
