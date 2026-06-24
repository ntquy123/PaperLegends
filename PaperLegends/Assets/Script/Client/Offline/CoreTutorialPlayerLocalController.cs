using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if !UNITY_SERVER
using UnityEngine.Animations.Rigging;
#endif

[DefaultExecutionOrder(10000)]
public sealed class CoreTutorialPlayerLocalController : MonoBehaviour
{
    private const string GroundTag = "Ground";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private float readyDelay = 0.35f;

    [Header("Grounding")]
    [SerializeField, Tooltip("Khoang cach nho giua chan nhan vat va mat Ground.")]
    private float groundSurfaceOffset = 0.02f;
    [SerializeField] private float groundRaycastHeight = 10f;
    [SerializeField] private float groundRaycastDistance = 50f;

    [Header("Aim")]
    [SerializeField] private float pointAimDistance = 1.5f;
    [SerializeField] private Vector3 pointAimOriginOffset = new Vector3(0f, 1.55f, 0f);
    [SerializeField] private bool maintainAimOffset;
    [SerializeField] private Vector3 constraintOffset = new Vector3(35f, 0f, 0f);
    [SerializeField] private Vector2 constraintLimits = new Vector2(-180f, 180f);

    [Header("Finger Rig")]
    [SerializeField] private float fingerRigBlendSpeed = 6f;
    [SerializeField] private Vector3 fingerJointPrimaryPullRotation = new Vector3(-25f, 0f, 0f);
    [SerializeField] private Vector3 fingerJointSecondaryPullRotation = new Vector3(-40f, 0f, 0f);
    [SerializeField] private AnimationCurve fingerJointPrimaryWeightCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.6f, 1f));
    [SerializeField] private AnimationCurve fingerJointSecondaryWeightCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.6f, 0f),
        new Keyframe(1f, 1f));

    public PlayerInfoStruct PlayerModel { get; private set; }
    public CharacterAnimState CurrentAnimState { get; private set; }
    public bool IsTutorialActive { get; private set; }
    public bool IsVisualReady { get; private set; }
    public bool IsReadyToShoot { get; private set; }
    public bool CanReceivePlayerInput => IsTutorialActive && IsVisualReady && IsReadyToShoot;
    public Transform FingerPosition => visual?.FingerPosition;
    public Transform HeadTransform => visual?.HeadTransform;
    public Transform FPPPosition => visual?.FPPPosition;
    public Transform PointPosition => visual?.PointPosition;
    public Vector3 AimDirection => PointPosition != null ? PointPosition.forward.normalized : transform.forward;
    public float CurrentAimPitch { get; private set; }

    private PlayerModelVisualComponent visual;
    private GameObject visualInstance;
    private Animator animator;
    private Rigidbody heldBall;
    private RigidbodyInterpolation heldBallOriginalInterpolation;
    private bool hasHeldBallOriginalInterpolation;
    private Action pendingShotRelease;
    private Coroutine shotFallbackRoutine;
    private string lastPlayedAnimation;
    private Terrain groundTerrain;
#if !UNITY_SERVER
    private RigBuilder rigBuilder;
    private Rig rigLayer;
    private MultiAimConstraint aimConstraint;
    private Transform rigLayerTransform;
    private Transform spineTargetTransform;
    private MultiRotationConstraint fingerJointPrimaryConstraint;
    private MultiRotationConstraint fingerJointSecondaryConstraint;
    private Transform fingerJointPrimaryTarget;
    private Transform fingerJointSecondaryTarget;
    private bool rigInitialized;
    private bool fingerRigInitialized;
    private float smoothedFingerRigPower;
#endif

    public void Initialize(PlayerInfoStruct playerInfo, Vector3 initialAimTarget)
    {
        PlayerModel = playerInfo;
        IsTutorialActive = true;
        ResolveGroundTerrain();
        KeepOnGround();
        StartCoroutine(LoadVisualModelRoutine(initialAimTarget));
    }

    private void Update()
    {
#if !UNITY_SERVER
        if (IsTutorialActive)
        {
            UpdateFingerRig(Time.deltaTime);
        }
#endif
    }

    private void LateUpdate()
    {
        if (!IsTutorialActive)
        {
            return;
        }

        KeepOnGround();

        if (heldBall == null
            || FingerPosition == null
            || (!IsReadyToShoot && pendingShotRelease == null))
        {
            return;
        }

        SnapHeldBallToFinger();
    }

    public IEnumerator MoveToShootingPosition(Vector3 requestedPosition, Vector3 aimTarget)
    {
        yield return new WaitUntil(() => IsVisualReady);
        IsReadyToShoot = false;
        Vector3 targetPosition = GetGroundedTargetPosition(requestedPosition);
        SetAnimationState(CharacterAnimState.Running);

        bool shouldPlayFootsteps = Vector2.Distance(ToXZ(transform.position), ToXZ(targetPosition)) > 0.03f;
        if (shouldPlayFootsteps)
            SoundManager.Instance?.StartFootstepLoop(gameObject);

        try
        {
            while (Vector2.Distance(ToXZ(transform.position), ToXZ(targetPosition)) > 0.03f)
            {
                Vector3 direction = targetPosition - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, turnSpeed * Time.deltaTime);
                }

                Vector3 nextPosition = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                transform.position = GetGroundedTargetPosition(nextPosition);
                yield return null;
            }
        }
        finally
        {
            if (shouldPlayFootsteps)
                SoundManager.Instance?.StopFootstepLoop(gameObject);
        }

        transform.position = GetGroundedTargetPosition(targetPosition);
        RotateSightingPoint(aimTarget);
        SetAnimationState(CharacterAnimState.SitToShoot);
        yield return new WaitForSeconds(readyDelay);
        IsReadyToShoot = true;
    }

    public void ApplyLookRotation(float yaw, float pitch)
    {
        if (!IsTutorialActive || !IsVisualReady)
        {
            return;
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        CurrentAimPitch = pitch;
        UpdatePointPosition(pitch);
        SnapHeldBallToFinger();
    }

    public void MoveHorizontal(int direction)
    {
        if (!CanReceivePlayerInput || direction == 0)
        {
            return;
        }

        Vector3 nextPosition = transform.position + Vector3.right * direction * moveSpeed * Time.deltaTime;
        transform.position = GetGroundedTargetPosition(nextPosition);
        UpdatePointPosition(CurrentAimPitch);
        SnapHeldBallToFinger();
    }

    public bool RequestShoot(Action onReleaseBall)
    {
        if (!IsTutorialActive || !IsReadyToShoot || pendingShotRelease != null || onReleaseBall == null)
        {
            return false;
        }

        IsReadyToShoot = false;
        pendingShotRelease = onReleaseBall;
        SetAnimationState(CharacterAnimState.Shoot);

        if (shotFallbackRoutine != null)
        {
            StopCoroutine(shotFallbackRoutine);
        }

        shotFallbackRoutine = StartCoroutine(ReleaseIfAnimationEventMissing());
        return true;
    }

    public void HandleShootAnimationEvent()
    {
        if (!IsTutorialActive || pendingShotRelease == null)
        {
            return;
        }

        if (shotFallbackRoutine != null)
        {
            StopCoroutine(shotFallbackRoutine);
            shotFallbackRoutine = null;
        }

        Action releaseBall = pendingShotRelease;
        SnapHeldBallToFinger(true);
        pendingShotRelease = null;
        RestoreHeldBallInterpolation();
        heldBall = null;
        releaseBall.Invoke();
    }

    public void HoldBall(Rigidbody ball)
    {
        if (heldBall != ball)
        {
            RestoreHeldBallInterpolation();
        }

        heldBall = ball;
        CacheHeldBallInterpolation();
        SnapHeldBallToFinger();
    }

    private void CacheHeldBallInterpolation()
    {
        if (heldBall == null || hasHeldBallOriginalInterpolation)
        {
            return;
        }

        heldBallOriginalInterpolation = heldBall.interpolation;
        hasHeldBallOriginalInterpolation = true;
        heldBall.interpolation = RigidbodyInterpolation.None;
    }

    private void RestoreHeldBallInterpolation()
    {
        if (heldBall != null && hasHeldBallOriginalInterpolation)
        {
            heldBall.interpolation = heldBallOriginalInterpolation;
        }

        hasHeldBallOriginalInterpolation = false;
    }

    private void SnapHeldBallToFinger(bool force = false)
    {
        if (heldBall == null
            || FingerPosition == null
            || (!force && !IsReadyToShoot && pendingShotRelease == null)
            || !heldBall.isKinematic)
        {
            return;
        }

        Vector3 targetPosition = FingerPosition.position;
        Quaternion targetRotation = FingerPosition.rotation;
        heldBall.transform.SetPositionAndRotation(targetPosition, targetRotation);
        heldBall.position = targetPosition;
        heldBall.rotation = targetRotation;
        heldBall.linearVelocity = Vector3.zero;
        heldBall.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }

    public void ResetForNextShot(Vector3 aimTarget)
    {
        if (!IsTutorialActive)
        {
            return;
        }

        RotateSightingPoint(aimTarget);
        SetAnimationState(CharacterAnimState.SitToShoot);
        IsReadyToShoot = true;
    }

    public void IgnoreCollisionsWith(IEnumerable<GameObject> balls)
    {
        if (balls == null)
        {
            return;
        }

        Collider[] playerColliders = GetComponentsInChildren<Collider>(true);
        foreach (GameObject ball in balls)
        {
            Collider ballCollider = ball != null ? ball.GetComponentInChildren<Collider>() : null;
            if (ballCollider == null)
            {
                continue;
            }

            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider != null)
                {
                    Physics.IgnoreCollision(playerCollider, ballCollider, true);
                }
            }
        }
    }

    public void StopTutorial()
    {
        IsTutorialActive = false;
        IsVisualReady = false;
        IsReadyToShoot = false;
        RestoreHeldBallInterpolation();
        heldBall = null;
        pendingShotRelease = null;
        if (shotFallbackRoutine != null)
        {
            StopCoroutine(shotFallbackRoutine);
            shotFallbackRoutine = null;
        }

        MovePlayerOnlineHandler.Instance?.ClearLocalTutorialPlayerHandler(this);
    }

    private IEnumerator LoadVisualModelRoutine(Vector3 initialAimTarget)
    {
        GameObject modelPrefab = GameInitializer.Instance != null ? GameInitializer.Instance.PlayerModelVisual : null;
        if (modelPrefab == null)
        {
            Debug.LogWarning("Core tutorial player has no PlayerModelVisual configured; continuing without character animation.");
            IsVisualReady = true;
            MovePlayerOnlineHandler.Instance?.SetLocalTutorialPlayerHandler(this);
            yield break;
        }

        visualInstance = Instantiate(modelPrefab, transform);
        visualInstance.name = "TutorialPlayerModelVisual";
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = Quaternion.identity;
        visualInstance.transform.localScale = Vector3.one;

        ResolveVisual();
        if (visual == null)
        {
            Debug.LogWarning("Core tutorial player could not load PlayerModelVisual; animation presentation is unavailable.");
            IsVisualReady = true;
            MovePlayerOnlineHandler.Instance?.SetLocalTutorialPlayerHandler(this);
            yield break;
        }

        visual.PlayerId = PlayerModel.playerId;
#if !UNITY_SERVER
        InitializeRigSetup();
        yield return ApplyDefaultMaterialRoutine();
#endif
        RotateSightingPoint(initialAimTarget);
        SetAnimationState(CharacterAnimState.None);
        IsVisualReady = true;
        MovePlayerOnlineHandler.Instance?.SetLocalTutorialPlayerHandler(this);
    }

    private IEnumerator ReleaseIfAnimationEventMissing()
    {
        yield return new WaitForSeconds(1.25f);
        shotFallbackRoutine = null;

        if (pendingShotRelease != null)
        {
            Debug.LogWarning("Tutorial player Shoot animation did not invoke its event; releasing the ball by fallback.");
            HandleShootAnimationEvent();
        }
    }

    private void ResolveVisual()
    {
        visual = GetComponentInChildren<PlayerModelVisualComponent>(true);
        animator = visual != null ? visual.Animator : GetComponentInChildren<Animator>(true);
    }

    private void SetAnimationState(CharacterAnimState state)
    {
        CurrentAnimState = state;

        string clipName;
        switch (state)
        {
            case CharacterAnimState.Running:
                clipName = "Running";
                break;
            case CharacterAnimState.SitToShoot:
                clipName = "SitToShoot";
                break;
            case CharacterAnimState.Shoot:
                clipName = "Shoot";
                break;
            case CharacterAnimState.StandingUp:
                clipName = "StandingJump";
                break;
            default:
                clipName = "Sitting Idle";
                break;
        }

        SetConstraintActive(state == CharacterAnimState.SitToShoot || state == CharacterAnimState.Shoot);
        PlayAnimation(clipName);
    }

    private void PlayAnimation(string clipName)
    {
        if (animator == null || string.Equals(lastPlayedAnimation, clipName, StringComparison.Ordinal))
        {
            return;
        }

        animator.CrossFade(clipName, 0.1f);
        lastPlayedAnimation = clipName;
    }

    private void RotateSightingPoint(Vector3 aimTarget)
    {
        Vector3 horizontalDirection = aimTarget - transform.position;
        horizontalDirection.y = 0f;
        if (horizontalDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
        }

        Vector3 origin = visual != null && visual.HeadTransform != null
            ? visual.HeadTransform.position
            : transform.position + pointAimOriginOffset;
        Vector3 lookDirection = aimTarget - origin;
        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float pitch = Quaternion.LookRotation(lookDirection.normalized, Vector3.up).eulerAngles.x;
        if (pitch > 180f)
        {
            pitch -= 360f;
        }

        CurrentAimPitch = pitch;
        UpdatePointPosition(pitch);
    }

    private void UpdatePointPosition(float pitch)
    {
        if (PointPosition == null)
        {
            return;
        }

        Vector3 origin = visual != null && visual.HeadTransform != null
            ? visual.HeadTransform.position
            : transform.position + pointAimOriginOffset;
        Quaternion pitchRotation = Quaternion.AngleAxis(pitch, Vector3.right);
        PointPosition.position = origin + transform.rotation * (pitchRotation * Vector3.forward * pointAimDistance);
    }

    private Vector3 GetGroundedTargetPosition(Vector3 requestedPosition)
    {
        if (TryGetGroundHeight(requestedPosition, out float groundHeight))
        {
            requestedPosition.y = groundHeight;
        }

        return requestedPosition;
    }

    private void KeepOnGround()
    {
        if (!TryGetGroundHeight(transform.position, out float groundHeight))
        {
            return;
        }

        Vector3 groundedPosition = transform.position;
        groundedPosition.y = groundHeight;
        transform.position = groundedPosition;
    }

    private bool TryGetGroundHeight(Vector3 position, out float groundHeight)
    {
        Vector3 origin = position + Vector3.up * groundRaycastHeight;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            groundRaycastDistance,
            ~0,
            QueryTriggerInteraction.Collide);
        float closestDistance = float.MaxValue;
        groundHeight = position.y;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || !hit.collider.CompareTag(GroundTag))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundHeight = hit.point.y + groundSurfaceOffset;
            }
        }

        if (closestDistance < float.MaxValue)
        {
            return true;
        }

        if (groundTerrain == null)
        {
            ResolveGroundTerrain();
        }

        if (groundTerrain == null)
        {
            return false;
        }

        groundHeight = groundTerrain.SampleHeight(position)
            + groundTerrain.transform.position.y
            + groundSurfaceOffset;
        return true;
    }

    private void ResolveGroundTerrain()
    {
        GameObject groundObject = GameObject.FindWithTag(GroundTag);
        groundTerrain = groundObject != null ? groundObject.GetComponent<Terrain>() : null;
    }

    private static Vector2 ToXZ(Vector3 position)
    {
        return new Vector2(position.x, position.z);
    }

    private void SetConstraintActive(bool active)
    {
#if !UNITY_SERVER
        if (aimConstraint != null)
        {
            aimConstraint.weight = active ? 1f : 0f;
        }
#endif
    }

#if !UNITY_SERVER
    private void InitializeRigSetup()
    {
        if (rigInitialized || visual == null)
        {
            return;
        }

        rigInitialized = true;
        GameObject rootObject = visual.gameObject;
        rigBuilder = visual.RigBuilder != null ? visual.RigBuilder : rootObject.GetComponent<RigBuilder>();
        if (rigBuilder == null)
        {
            rigBuilder = rootObject.AddComponent<RigBuilder>();
        }

        visual.RigBuilder = rigBuilder;
        rigLayerTransform = visual.RigLayerTransform;
        if (rigLayerTransform == null)
        {
            var layerObject = new GameObject("TutorialRigLayer");
            layerObject.transform.SetParent(rootObject.transform, false);
            rigLayerTransform = layerObject.transform;
            visual.RigLayerTransform = rigLayerTransform;
        }

        rigLayer = visual.RigLayer != null ? visual.RigLayer : rigLayerTransform.GetComponent<Rig>();
        if (rigLayer == null)
        {
            rigLayer = rigLayerTransform.gameObject.AddComponent<Rig>();
        }

        visual.RigLayer = rigLayer;
        spineTargetTransform = visual.SpineTargetTransform;
        if (spineTargetTransform == null)
        {
            var targetObject = new GameObject("TutorialSpineTarget");
            targetObject.transform.SetParent(rigLayerTransform, false);
            spineTargetTransform = targetObject.transform;
            visual.SpineTargetTransform = spineTargetTransform;
        }

        aimConstraint = visual.AimConstraint != null
            ? visual.AimConstraint
            : spineTargetTransform.GetComponent<MultiAimConstraint>();
        if (aimConstraint == null)
        {
            aimConstraint = spineTargetTransform.gameObject.AddComponent<MultiAimConstraint>();
        }

        visual.AimConstraint = aimConstraint;
        ConfigureAimConstraint();
        InitializeFingerRig();

        List<RigLayer> layers = rigBuilder.layers ?? new List<RigLayer>();
        if (!layers.Exists(layer => layer.rig == rigLayer))
        {
            layers.Add(new RigLayer(rigLayer));
            rigBuilder.layers = layers;
        }

        rigBuilder.Build();
    }

    private IEnumerator ApplyDefaultMaterialRoutine()
    {
        Material characterMaterial = null;
        yield return AddressablesHelper.LoadAsset<Material>(
            AddressablePaths.Character.DefaultMaterial,
            material => characterMaterial = material);

        if (characterMaterial == null || visual == null)
        {
            yield break;
        }

        Renderer[] renderers = visual.CharacterRenderers;
        if (renderers == null || renderers.Length == 0)
        {
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            visual.CharacterRenderers = renderers;
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !(renderer is SkinnedMeshRenderer || renderer is MeshRenderer))
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = characterMaterial;
                continue;
            }

            for (int index = 0; index < materials.Length; index++)
            {
                materials[index] = characterMaterial;
            }

            renderer.sharedMaterials = materials;
        }
    }

    private void ConfigureAimConstraint()
    {
        if (aimConstraint == null || visual == null || visual.HeadTransform == null || PointPosition == null)
        {
            return;
        }

        MultiAimConstraintData data = aimConstraint.data;
        data.constrainedObject = visual.HeadTransform;
        data.aimAxis = MultiAimConstraintData.Axis.Z;
        data.upAxis = MultiAimConstraintData.Axis.Y;
        data.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
        data.maintainOffset = maintainAimOffset;
        data.offset = constraintOffset;
        data.constrainedXAxis = true;
        data.constrainedYAxis = true;
        data.constrainedZAxis = false;
        data.limits = constraintLimits;
        var sources = new WeightedTransformArray();
        sources.Add(new WeightedTransform(PointPosition, 1f));
        data.sourceObjects = sources;
        aimConstraint.data = data;
        aimConstraint.weight = 0f;
    }

    private void InitializeFingerRig()
    {
        if (fingerRigInitialized || visual == null || rigLayerTransform == null)
        {
            return;
        }

        Transform primaryJoint = visual.FingerJointPrimary;
        Transform secondaryJoint = visual.FingerJointSecondary;
        if (primaryJoint == null || secondaryJoint == null)
        {
            return;
        }

        fingerRigInitialized = true;
        var targetRoot = new GameObject("TutorialFingerRigTargets").transform;
        targetRoot.SetParent(rigLayerTransform, false);
        fingerJointPrimaryTarget = new GameObject("TutorialFingerJointPrimaryTarget").transform;
        fingerJointPrimaryTarget.SetParent(targetRoot, false);
        fingerJointSecondaryTarget = new GameObject("TutorialFingerJointSecondaryTarget").transform;
        fingerJointSecondaryTarget.SetParent(targetRoot, false);
        fingerJointPrimaryConstraint = CreateFingerConstraint("TutorialFingerPrimaryConstraint", primaryJoint, fingerJointPrimaryTarget);
        fingerJointSecondaryConstraint = CreateFingerConstraint("TutorialFingerSecondaryConstraint", secondaryJoint, fingerJointSecondaryTarget);
        UpdateFingerRigTargets();
        ApplyFingerRigWeights(0f);
    }

    private MultiRotationConstraint CreateFingerConstraint(string objectName, Transform joint, Transform target)
    {
        var constraintObject = new GameObject(objectName);
        constraintObject.transform.SetParent(rigLayerTransform, false);
        var constraint = constraintObject.AddComponent<MultiRotationConstraint>();
        MultiRotationConstraintData data = constraint.data;
        data.constrainedObject = joint;
        var sources = new WeightedTransformArray();
        sources.Add(new WeightedTransform(target, 1f));
        data.sourceObjects = sources;
        data.maintainOffset = false;
        constraint.data = data;
        constraint.weight = 0f;
        return constraint;
    }

    private void UpdateFingerRig(float deltaTime)
    {
        if (!fingerRigInitialized)
        {
            return;
        }

        float targetPower = 0f;
        PowerBarController powerBar = PowerBarController.Instance;
        if (powerBar != null && powerBar.isShootting && powerBar.powerSlider != null)
        {
            targetPower = Mathf.Clamp01(powerBar.powerSlider.value);
        }

        smoothedFingerRigPower = Mathf.MoveTowards(
            smoothedFingerRigPower,
            targetPower,
            fingerRigBlendSpeed * Mathf.Max(deltaTime, 0.001f));
        UpdateFingerRigTargets();
        ApplyFingerRigWeights(smoothedFingerRigPower);
    }

    private void UpdateFingerRigTargets()
    {
        if (visual == null || fingerJointPrimaryTarget == null || fingerJointSecondaryTarget == null)
        {
            return;
        }

        fingerJointPrimaryTarget.SetPositionAndRotation(
            visual.FingerJointPrimary.position,
            visual.FingerJointPrimary.rotation * Quaternion.Euler(fingerJointPrimaryPullRotation));
        fingerJointSecondaryTarget.SetPositionAndRotation(
            visual.FingerJointSecondary.position,
            visual.FingerJointSecondary.rotation * Quaternion.Euler(fingerJointSecondaryPullRotation));
    }

    private void ApplyFingerRigWeights(float power)
    {
        if (fingerJointPrimaryConstraint != null)
        {
            fingerJointPrimaryConstraint.weight = Mathf.Clamp01(fingerJointPrimaryWeightCurve.Evaluate(power));
        }

        if (fingerJointSecondaryConstraint != null)
        {
            fingerJointSecondaryConstraint.weight = Mathf.Clamp01(fingerJointSecondaryWeightCurve.Evaluate(power));
        }
    }
#endif

    private void OnDisable()
    {
        SoundManager.Instance?.StopFootstepLoop(gameObject);
    }

    private void OnDestroy()
    {
        SoundManager.Instance?.StopFootstepLoop(gameObject);
        MovePlayerOnlineHandler.Instance?.ClearLocalTutorialPlayerHandler(this);
    }
}
