using Fusion;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public class PlayerModelVisualComponent : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int playerId;
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform fingerPosition;
    [SerializeField] private Transform fingerJointPrimary;
    [SerializeField] private Transform fingerJointSecondary;
    [SerializeField] private Transform fppPosition;
    [SerializeField] private Transform fppPositionCam2;
    [SerializeField] private Transform pointPosition;
    [SerializeField] private Transform pointPositionCam2;
    [SerializeField] private Transform powerPosition;
    [SerializeField] private MultiAimConstraint aimConstraint;
    [SerializeField] private RigBuilder rigBuilder;
    [SerializeField] private Rig rigLayer;
    [SerializeField] private Transform rigLayerTransform;
    [SerializeField] private Transform spineTargetTransform;
    [SerializeField] private Renderer[] characterRenderers;

    public Animator Animator
    {
        get
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                    Log.Error("Không tìm thấy Animator");
            }    

            return animator;
        }
        set => animator = value;
    }

    public Transform HeadTransform
    {
        get => headTransform;
        set => headTransform = value;
    }

    public Transform FingerPosition
    {
        get => fingerPosition;
        set => fingerPosition = value;
    }

    public Transform FingerJointPrimary
    {
        get => fingerJointPrimary;
        set => fingerJointPrimary = value;
    }

    public Transform FingerJointSecondary
    {
        get => fingerJointSecondary;
        set => fingerJointSecondary = value;
    }

    public Transform FPPPosition
    {
        get => fppPosition;
        set => fppPosition = value;
    }

    public Transform FPPPositionCam2
    {
        get => fppPositionCam2;
        set => fppPositionCam2 = value;
    }

    public Transform PointPosition
    {
        get => pointPosition;
        set => pointPosition = value;
    }

    public Transform PointPositionCam2
    {
        get => pointPositionCam2;
        set => pointPositionCam2 = value;
    }

    public Transform PowerPosition
    {
        get => powerPosition;
        set => powerPosition = value;
    }

    public MultiAimConstraint AimConstraint
    {
        get => aimConstraint;
        set => aimConstraint = value;
    }

    public RigBuilder RigBuilder
    {
        get => rigBuilder;
        set => rigBuilder = value;
    }

    public Rig RigLayer
    {
        get => rigLayer;
        set => rigLayer = value;
    }

    public Transform RigLayerTransform
    {
        get => rigLayerTransform;
        set => rigLayerTransform = value;
    }

    public Transform SpineTargetTransform
    {
        get => spineTargetTransform;
        set => spineTargetTransform = value;
    }

    public Renderer[] CharacterRenderers
    {
        get => characterRenderers;
        set => characterRenderers = value;
    }

    public int PlayerId
    {
        get => playerId;
        set => playerId = value;
    }
    /// <summary>
    /// Được gọi từ animation event khi nhân vật bắn.hàm này phải được đặt tại đây vì animator được gắn ở model chung vs csript này
    /// </summary>
    public void OnShootAnimationEvent()
    {
        var tutorialPlayer = GetComponentInParent<CoreTutorialPlayerLocalController>();
        if (tutorialPlayer != null && tutorialPlayer.IsTutorialActive)
        {
            tutorialPlayer.HandleShootAnimationEvent();
            return;
        }

        var currentLoginId = GameManagerNetWork.Instance?.loginUserModel?.UserId ?? 0;
        if (playerId != currentLoginId)
            return;

        Debug.Log("Bắn bi từ Event");
        var session = GameSessionClientLocal.Instance;
        if (session == null)
        {
            Debug.LogWarning("Không tìm thấy GameSessionClientLocal để xác nhận lượt bắn");
            return;
        }

        session.ConfirmPendingShot();
    }
 }
