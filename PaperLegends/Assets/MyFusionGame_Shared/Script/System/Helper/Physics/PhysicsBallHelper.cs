using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsBallHelper : MonoBehaviour
{
    private Rigidbody rb;
    private const float stationaryThreshold = 0.05f;
    private const float stationarySpinDamping = 0.9f;
    private bool deform30;
    private bool deform50;
    private bool deform90;
    [SerializeField] private bool runFramePhysicsInFixedUpdate = true;

    // prefab references for crack effects
    [SerializeField] private GameObject minorCrackPrefab;
    [SerializeField] private GameObject mediumCrackPrefab;
    [SerializeField] private GameObject heavyCrackPrefab;
    [SerializeField] private Material decalMaterial;

    private GameObject spawnedCrack;

    [Header("DAMAGED ROLLING PHYSICS")]
    [SerializeField, Tooltip("Bật hiệu ứng lăn lệch khi bi bị hư hỏng.")]
    private bool enableDamagedRollingPhysics = true;
    [SerializeField, Range(0f, 1f), Tooltip("Tỉ lệ hư hỏng tối thiểu trước khi bi bắt đầu lăn lệch.")]
    private float damagedRollingMinDamagePercent = 0.08f;
    [SerializeField, Tooltip("Tỉ lệ lực lệch tâm thêm vào lúc bắt đầu bắn, nhân với lực bắn và mức hư hỏng.")]
    private float damagedShotLateralImpulseScale = 0.08f;
    [SerializeField, Tooltip("Torque lệch tâm thêm vào lúc bắt đầu bắn.")]
    private float damagedShotTorqueImpulseScale = 0.16f;
    [SerializeField, Tooltip("Gia tốc ngang khi bi hư hỏng đang lăn trên mặt đất.")]
    private float damagedRollLateralAcceleration = 0.45f;
    [SerializeField, Tooltip("Torque ngẫu nhiên khi bi hư hỏng đang lăn.")]
    private float damagedRollAngularAcceleration = 1.8f;
    [SerializeField, Tooltip("Ma sát phụ không đều khi bi hư hỏng đang lăn.")]
    private float damagedRollExtraDamping = 0.16f;
    [SerializeField, Tooltip("Tần suất dao động của lực lệch khi bi lăn.")]
    private float damagedRollNoiseFrequency = 7f;
    [SerializeField, Range(0.1f, 1f), Tooltip("Tỉ lệ giữ lại độ nảy khi bi hư hỏng tối đa.")]
    private float damagedBounceRetainAtFullDamage = 0.55f;
    [SerializeField, Tooltip("Vận tốc lệch ngang tối đa thêm vào khi va chạm/nảy do bi hư.")]
    private float damagedBounceMaxSkewVelocity = 0.8f;
    [SerializeField, Tooltip("Hệ số lệch ngang khi va chạm/nảy do bi hư.")]
    private float damagedBounceSkewVelocityScale = 0.18f;
    [SerializeField, Range(0f, 1f), Tooltip("Mức tăng ma sát collider khi bi hư hỏng tối đa.")]
    private float damagedFrictionIncreaseAtFullDamage = 0.45f;

    [Header("RARE REAL-WORLD COLLISION PHYSICS")]
    [SerializeField, Tooltip("Bật các phản ứng va chạm hiếm mô phỏng bi thật: nhảy sau va chạm lệch tâm và dead-stop khi va chạm đúng tâm.")]
    private bool enableRareRealCollisionPhysics = true;
    [SerializeField, Range(0f, 1f), Tooltip("Tỷ lệ hard-code để bi đang bắn bật văng nhẹ lên khi va chạm với bi khác.")]
    private float forcedShooterHopChance = 0.5f;
    [SerializeField, Tooltip("Vận tốc tiếp cận tối thiểu để roll hiệu ứng bi bắn bật lên.")]
    private float forcedShooterHopMinApproachSpeed = 0.45f;
    [SerializeField, Tooltip("Vận tốc bật lên tối thiểu cho hiệu ứng hard-code.")]
    private float forcedShooterHopMinLiftSpeed = 0.75f;
    [SerializeField, Tooltip("Vận tốc tương đối tối thiểu để bi bắn có thể bật bay sau va chạm lệch tâm.")]
    private float collisionHopMinApproachSpeed = 2.4f;
    [SerializeField, Range(0f, 1f), Tooltip("Độ thẳng tâm tối thiểu cho case bật bay. Thấp hơn ngưỡng này là va quá sượt.")]
    private float collisionHopMinHeadOnAlignment = 0.45f;
    [SerializeField, Range(0f, 1f), Tooltip("Độ thẳng tâm tối đa cho case bật bay. Cao hơn ngưỡng này ưu tiên case truyền lực/dead-stop.")]
    private float collisionHopMaxHeadOnAlignment = 0.9f;
    [SerializeField, Tooltip("Vận tốc tiếp tuyến tối thiểu tại điểm va chạm để tạo mô-men bật bay/xoáy.")]
    private float collisionHopMinTangentSpeed = 0.3f;
    [SerializeField, Tooltip("Vận tốc bật lên tối đa được thêm cho bi bắn sau va chạm lệch tâm.")]
    private float collisionHopMaxLiftSpeed = 2.6f;
    [SerializeField, Tooltip("Hệ số chuyển vận tốc va chạm thành vận tốc bật lên.")]
    private float collisionHopLiftScale = 0.45f;
    [SerializeField, Tooltip("Góc bật tối thiểu của bi bắn sau va chạm lệch tâm.")]
    private float collisionHopMinLaunchAngle = 8f;
    [SerializeField, Tooltip("Góc bật tối đa của bi bắn sau va chạm lệch tâm.")]
    private float collisionHopMaxLaunchAngle = 28f;
    [SerializeField, Tooltip("Hệ số thêm xoáy quanh trục đứng khi bi bắn bật lên.")]
    private float collisionHopVerticalSpinScale = 0.85f;
    [SerializeField, Tooltip("Hệ số giữ lại vận tốc ngang của bi bắn khi bật lên.")]
    private float collisionHopHorizontalRetain = 0.72f;
    [SerializeField, Tooltip("Vận tốc tương đối tối thiểu để xảy ra dead-stop/truyền lực hoàn hảo.")]
    private float perfectTransferMinApproachSpeed = 1.4f;
    [SerializeField, Range(0f, 1f), Tooltip("Độ thẳng tâm tối thiểu để bi bắn dừng tại chỗ và truyền lực sang bi bị trúng.")]
    private float perfectTransferMinHeadOnAlignment = 0.985f;
    [SerializeField, Tooltip("Vận tốc tiếp tuyến tối đa còn được xem là bắn đúng tâm.")]
    private float perfectTransferMaxTangentSpeed = 0.12f;
    [SerializeField, Tooltip("Vận tốc ngang tối đa của bi bị trúng trước va chạm để được xem là không bị tác động cản trở.")]
    private float perfectTransferMaxTargetPreSpeed = 0.18f;
    [SerializeField, Range(0f, 1f), Tooltip("Sai lệch khối lượng tối đa giữa hai bi để dead-stop diễn ra tự nhiên.")]
    private float perfectTransferMassTolerance = 0.25f;
    [SerializeField, Range(0f, 1f), Tooltip("Mức giảm xoáy còn lại của bi bắn sau dead-stop.")]
    private float perfectTransferMoverSpinRetain = 0.18f;

    private float initialColliderBounciness = -1f;
    private float initialDynamicFriction = -1f;
    private float initialStaticFriction = -1f;
    private float damagedRollPhase;
    private float damagedRollSeed;
    private float damagedShotSideSign = 1f;
    private readonly RaycastHit[] damagedGroundHits = new RaycastHit[8];

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void ApplyFramePhysics(float dt)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
        if (rb == null)
            return;

        // Áp dụng các hiệu ứng vật lý liên quan đến xoáy khi viên bi còn
        // chuyển động trên bàn. Điều này giúp quỹ đạo cong và phản ứng sau va
        // chạm trở nên tự nhiên hơn.
        if (IsOnGround())
        {
            ApplyRollingFriction();

            if (rb.linearVelocity.magnitude > 0.1f)
            {
                ApplySpinInfluence();
                ApplyMagnusEffect();
            }
        }
    }

    private void FixedUpdate()
    {
        if (!runFramePhysicsInFixedUpdate)
            return;

        ApplyFramePhysics(Time.fixedDeltaTime);
    }

    public void SetFramePhysicsFixedUpdateEnabled(bool enabled)
    {
        runFramePhysicsInFixedUpdate = enabled;
    }

    // Kiểm tra viên bi có đang tiếp xúc với mặt bàn không
    private bool IsOnGround()
    {
        return Physics.Raycast(transform.position, Vector3.down, 0.55f);
    }

    private void ApplyRollingFriction()
    {
        // Mô phỏng ma sát mặt bàn, giúp viên bi dừng lại tự nhiên
        rb.linearVelocity *= 0.995f;
        rb.angularVelocity *= 0.99f;

        // Nếu viên bi gần như đứng yên nhưng vẫn còn xoáy,
        // tăng giảm xoáy để dừng lại nhanh hơn
        if (rb.linearVelocity.magnitude < stationaryThreshold &&
            rb.angularVelocity.magnitude > 0.1f)
        {
            rb.angularVelocity *= stationarySpinDamping;
        }
    }

    private void ApplySpinInfluence()
    {
        Vector3 angularVelocity = rb.angularVelocity;
        Vector3 velocity = rb.linearVelocity;

        if (angularVelocity.magnitude > 0.1f)
        {
            // Tạo lực Magnus (xoáy làm cong quỹ đạo)
            Vector3 magnusForce = Vector3.Cross(angularVelocity, velocity) * 0.02f;
            rb.AddForce(magnusForce, ForceMode.Acceleration);

            // Xử lý xoáy ngang (trái/phải)
            Vector3 sideEffect = new Vector3(angularVelocity.z * 0.1f, 0, -angularVelocity.x * 0.1f);
            rb.AddForce(sideEffect, ForceMode.Acceleration);

            // Xử lý backspin (xoáy ngược -> viên bi lùi)
            float spinDirection = Vector3.Dot(angularVelocity, velocity);
            if (spinDirection < 0)
            {
                rb.linearVelocity *= 0.97f; // Giảm tốc nhanh hơn
            }
        }
    }

    private void ApplyMagnusEffect()
    {
        if (!IsOnGround() && rb.angularVelocity.magnitude > 0.1f)
        {
            Vector3 magnusForce = Vector3.Cross(rb.angularVelocity, rb.linearVelocity) * 0.05f;
            rb.AddForce(magnusForce, ForceMode.Acceleration);
        }
    }

    // old mesh deformation visuals removed
    public void UpdateDamageVisual(float pct, Vector3 damagePoint, BallMeshDeformer meshDeformer,
        Rigidbody rb, float initialAngularDrag, float initialLinearDamping)
    {
        // Intentionally left blank as damage visuals are handled by crack prefabs
    }

    public void CaptureBasePhysicsMaterial(Collider col, float bounciness, float dynamicFriction, float staticFriction)
    {
        initialColliderBounciness = bounciness;
        initialDynamicFriction = dynamicFriction;
        initialStaticFriction = staticFriction;

        if (col == null || col.material == null)
            return;

        col.material.bounciness = bounciness;
        col.material.dynamicFriction = dynamicFriction;
        col.material.staticFriction = staticFriction;
    }

    public void RefreshDamagedPhysicsMaterial(Collider col, float initialImpactResistance, float currentImpactResistance)
    {
        if (col == null || col.material == null)
            return;

        float strength = GetDamagedRollingStrength(initialImpactResistance, currentImpactResistance);
        if (initialColliderBounciness >= 0f)
        {
            float bounceRetain = Mathf.Lerp(1f, damagedBounceRetainAtFullDamage, strength);
            col.material.bounciness = Mathf.Max(0f, initialColliderBounciness * bounceRetain);
        }

        if (initialDynamicFriction >= 0f)
        {
            float frictionMultiplier = Mathf.Lerp(1f, 1f + damagedFrictionIncreaseAtFullDamage, strength);
            col.material.dynamicFriction = Mathf.Max(0f, initialDynamicFriction * frictionMultiplier);
        }

        if (initialStaticFriction >= 0f)
        {
            float frictionMultiplier = Mathf.Lerp(1f, 1f + damagedFrictionIncreaseAtFullDamage * 0.75f, strength);
            col.material.staticFriction = Mathf.Max(0f, initialStaticFriction * frictionMultiplier);
        }
    }

    public void ResetDamagedRollingState()
    {
        damagedRollPhase = 0f;
        damagedRollSeed = Random.Range(0f, 1000f);
        damagedShotSideSign = Random.value < 0.5f ? -1f : 1f;
    }

    public void ApplyDamagedShotStartImpulse(
        Rigidbody targetBody,
        Vector3 baseDirection,
        float force,
        float shootAngle,
        float initialImpactResistance,
        float currentImpactResistance)
    {
        float strength = GetDamagedRollingStrength(initialImpactResistance, currentImpactResistance);
        if (strength <= 0f || force <= 0f || targetBody == null || targetBody.isKinematic)
            return;

        Vector3 shotDirection = ResolveDamagedShotDirection(baseDirection, shootAngle);
        Vector3 horizontalDirection = ProjectHorizontal(shotDirection);
        if (horizontalDirection.sqrMagnitude < 0.0001f)
            return;

        horizontalDirection.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, horizontalDirection).normalized * damagedShotSideSign;
        float sideNoise = Mathf.Lerp(0.65f, 1.25f, Random.value);
        float torqueNoise = Mathf.Lerp(0.75f, 1.35f, Random.value);

        targetBody.AddForce(side * force * damagedShotLateralImpulseScale * strength * sideNoise, ForceMode.Impulse);
        targetBody.AddTorque(
            (side + Vector3.up * 0.35f + horizontalDirection * 0.25f) *
            force * damagedShotTorqueImpulseScale * strength * torqueNoise,
            ForceMode.Impulse);
        targetBody.WakeUp();
    }

    public void ApplyDamagedRollingPhysics(
        Rigidbody targetBody,
        float deltaTime,
        float initialImpactResistance,
        float currentImpactResistance)
    {
        float strength = GetDamagedRollingStrength(initialImpactResistance, currentImpactResistance);
        if (strength <= 0f || deltaTime <= 0f || targetBody == null || targetBody.isKinematic)
            return;

        Vector3 horizontalVelocity = ProjectHorizontal(targetBody.linearVelocity);
        float speed = horizontalVelocity.magnitude;
        if (speed < 0.12f || !IsDamagedRollGrounded(targetBody))
            return;

        float radius = Mathf.Max(0.05f, ResolveRigidbodyRadius(targetBody));
        damagedRollPhase += deltaTime * Mathf.Max(0.5f, speed / radius);

        float mainWave = Mathf.Sin(damagedRollPhase * damagedRollNoiseFrequency + damagedRollSeed);
        float chipWave = Mathf.Sin(damagedRollPhase * (damagedRollNoiseFrequency * 0.37f + 1.1f) + damagedRollSeed * 1.37f);
        float biteWave = Mathf.Abs(mainWave);

        Vector3 forward = horizontalVelocity / speed;
        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;

        float dampingAcceleration = damagedRollExtraDamping * strength * Mathf.Lerp(0.35f, 1f, biteWave) * speed;
        targetBody.AddForce(-forward * dampingAcceleration, ForceMode.Acceleration);

        float sideAcceleration = damagedRollLateralAcceleration * strength * speed * (mainWave * 0.7f + chipWave * 0.3f);
        targetBody.AddForce(side * sideAcceleration, ForceMode.Acceleration);

        float speed01 = Mathf.Clamp01(speed / 8f);
        Vector3 torqueAxis =
            side * mainWave +
            forward * (chipWave * 0.45f) +
            Vector3.up * Mathf.Sin(damagedRollPhase * 0.73f + damagedRollSeed * 0.91f) * 0.25f;
        targetBody.AddTorque(torqueAxis * damagedRollAngularAcceleration * strength * Mathf.Lerp(0.35f, 1f, speed01), ForceMode.Acceleration);
    }

    public void ApplyDamagedCollisionBounceModifier(
        Rigidbody targetBody,
        Collision collision,
        float initialImpactResistance,
        float currentImpactResistance)
    {
        float strength = GetDamagedRollingStrength(initialImpactResistance, currentImpactResistance);
        if (strength <= 0f || collision == null || collision.contactCount == 0 || targetBody == null || targetBody.isKinematic)
            return;

        ContactPoint contact = collision.contacts[0];
        Vector3 normal = contact.normal.sqrMagnitude > 0.0001f ? contact.normal.normalized : Vector3.up;
        float normalSpeed = Vector3.Dot(targetBody.linearVelocity, normal);
        if (normalSpeed > 0f)
        {
            float noise = Mathf.Sin(damagedRollSeed + damagedRollPhase * 1.93f + collision.relativeVelocity.sqrMagnitude);
            float retain = Mathf.Lerp(1f, damagedBounceRetainAtFullDamage, strength);
            float unevenRetain = retain * Mathf.Lerp(0.88f, 1.08f, (noise + 1f) * 0.5f);
            Vector3 normalVelocity = normal * normalSpeed;
            targetBody.linearVelocity += normalVelocity * (unevenRetain - 1f);
        }

        Vector3 horizontalNormal = ProjectHorizontal(normal);
        Vector3 tangent = horizontalNormal.sqrMagnitude > 0.0001f
            ? Vector3.Cross(Vector3.up, horizontalNormal.normalized)
            : ProjectHorizontal(Vector3.Cross(normal, Vector3.forward));

        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.right;

        tangent.Normalize();
        float sideSign = Mathf.Sin(damagedRollSeed * 0.37f + damagedRollPhase) >= 0f ? 1f : -1f;
        float skewVelocity = Mathf.Min(damagedBounceMaxSkewVelocity, collision.relativeVelocity.magnitude * damagedBounceSkewVelocityScale * strength);
        targetBody.AddForce(tangent * sideSign * skewVelocity, ForceMode.VelocityChange);

        Vector3 torqueAxis = Vector3.Cross(normal, tangent);
        if (torqueAxis.sqrMagnitude > 0.0001f)
            targetBody.AddTorque(torqueAxis.normalized * skewVelocity * 2.5f, ForceMode.VelocityChange);

        targetBody.WakeUp();
    }

    public void ApplyRareRealWorldBallCollision(
        Rigidbody moverBody,
        Rigidbody targetBody,
        Vector3 collisionNormal,
        Vector3 moverVelocityBefore,
        Vector3 targetVelocityBefore,
        float approachSpeed,
        float restitution,
        bool moverWasShot)
    {
        if (!enableRareRealCollisionPhysics || moverBody == null || targetBody == null || !moverWasShot)
            return;

        Vector3 normal = ProjectHorizontal(collisionNormal);
        if (normal.sqrMagnitude < 0.0001f)
            return;

        normal.Normalize();

        Vector3 relativeBefore = ProjectHorizontal(moverVelocityBefore - targetVelocityBefore);
        float relativeSpeed = relativeBefore.magnitude;
        if (relativeSpeed < 0.001f)
            return;

        float normalSpeed = Mathf.Max(0f, Vector3.Dot(relativeBefore, normal));
        float headOnAlignment = Mathf.Clamp01(normalSpeed / relativeSpeed);
        Vector3 tangentVelocity = relativeBefore - normal * normalSpeed;
        float tangentSpeed = tangentVelocity.magnitude;
        float targetPreSpeed = ProjectHorizontal(targetVelocityBefore).magnitude;

        if (TryApplyPerfectMomentumTransfer(
                moverBody,
                targetBody,
                normal,
                approachSpeed,
                restitution,
                headOnAlignment,
                tangentSpeed,
                targetPreSpeed))
        {
            return;
        }

        TryApplyShooterHopAfterObliqueHit(
            moverBody,
            normal,
            tangentVelocity,
            approachSpeed,
            headOnAlignment,
            tangentSpeed,
            targetPreSpeed);
    }

    private bool TryApplyPerfectMomentumTransfer(
        Rigidbody moverBody,
        Rigidbody targetBody,
        Vector3 normal,
        float approachSpeed,
        float restitution,
        float headOnAlignment,
        float tangentSpeed,
        float targetPreSpeed)
    {
        if (approachSpeed < perfectTransferMinApproachSpeed ||
            headOnAlignment < perfectTransferMinHeadOnAlignment ||
            tangentSpeed > perfectTransferMaxTangentSpeed ||
            targetPreSpeed > perfectTransferMaxTargetPreSpeed)
        {
            return false;
        }

        float moverMass = Mathf.Max(0.05f, moverBody.mass);
        float targetMass = Mathf.Max(0.05f, targetBody.mass);
        float massRatio = moverMass / targetMass;
        if (Mathf.Abs(1f - massRatio) > perfectTransferMassTolerance)
            return false;

        float transferredSpeed = approachSpeed * (1f + Mathf.Clamp01(restitution)) * moverMass / (moverMass + targetMass);
        transferredSpeed = Mathf.Min(transferredSpeed, approachSpeed * 1.03f);

        moverBody.linearVelocity = Vector3.up * Mathf.Max(0f, moverBody.linearVelocity.y);
        moverBody.angularVelocity *= perfectTransferMoverSpinRetain;

        Vector3 targetVelocity = normal * transferredSpeed;
        targetVelocity.y = targetBody.linearVelocity.y;
        targetBody.linearVelocity = targetVelocity;
        ApplyRollingAngularVelocity(targetBody, targetVelocity, 1f);

        moverBody.WakeUp();
        targetBody.WakeUp();
        Debug.Log($"[BallPhysics] Perfect transfer dead-stop. approach={approachSpeed:F2}, align={headOnAlignment:F3}, transferred={transferredSpeed:F2}");
        return true;
    }

    private void TryApplyShooterHopAfterObliqueHit(
        Rigidbody moverBody,
        Vector3 normal,
        Vector3 tangentVelocity,
        float approachSpeed,
        float headOnAlignment,
        float tangentSpeed,
        float targetPreSpeed)
    {
        if (forcedShooterHopChance <= 0f ||
            approachSpeed < forcedShooterHopMinApproachSpeed ||
            moverBody.linearVelocity.y > collisionHopMaxLiftSpeed * 0.5f ||
            Random.value > forcedShooterHopChance)
        {
            return;
        }

        float middle = (collisionHopMinHeadOnAlignment + collisionHopMaxHeadOnAlignment) * 0.5f;
        float halfRange = Mathf.Max(0.001f, (collisionHopMaxHeadOnAlignment - collisionHopMinHeadOnAlignment) * 0.5f);
        float alignmentPeak = Mathf.Clamp01(1f - Mathf.Abs(headOnAlignment - middle) / halfRange);
        float tangent01 = Mathf.Clamp01(tangentSpeed / Mathf.Max(0.001f, approachSpeed));
        float energy01 = Mathf.Clamp01(approachSpeed / Mathf.Max(0.001f, collisionHopMinApproachSpeed * 1.5f));
        float randomLift = Random.Range(0.82f, 1.16f);
        float lift01 = Mathf.Clamp01(Mathf.Lerp(0.45f, 1f, energy01) *
                                     Mathf.Lerp(0.85f, 1.15f, tangent01) *
                                     Mathf.Lerp(0.85f, 1.05f, alignmentPeak) *
                                     randomLift);
        float launchAngle = Mathf.Lerp(collisionHopMinLaunchAngle, collisionHopMaxLaunchAngle, lift01) * Mathf.Deg2Rad;
        float liftSpeed = Mathf.Clamp(
            approachSpeed * Mathf.Tan(launchAngle) * collisionHopLiftScale + Random.Range(0.15f, 0.35f),
            forcedShooterHopMinLiftSpeed,
            collisionHopMaxLiftSpeed);
        if (liftSpeed <= 0.05f)
            return;

        Vector3 horizontalVelocity = ProjectHorizontal(moverBody.linearVelocity) * Mathf.Clamp01(collisionHopHorizontalRetain);
        moverBody.linearVelocity = horizontalVelocity + Vector3.up * Mathf.Max(moverBody.linearVelocity.y, liftSpeed);

        Vector3 tangent = tangentVelocity.sqrMagnitude > 0.0001f
            ? tangentVelocity.normalized
            : Vector3.Cross(Vector3.up, normal).normalized * (Random.value < 0.5f ? -1f : 1f);
        float spinSign = Mathf.Sign(Vector3.Dot(tangent, Vector3.Cross(Vector3.up, normal)));
        if (Mathf.Approximately(spinSign, 0f))
            spinSign = 1f;

        Vector3 spinImpulse =
            Vector3.up * spinSign * approachSpeed * collisionHopVerticalSpinScale * lift01 +
            tangent * approachSpeed * 0.18f * lift01;
        moverBody.AddTorque(spinImpulse, ForceMode.VelocityChange);
        moverBody.WakeUp();
        Debug.Log($"[BallPhysics] Forced shooter hop. chance={forcedShooterHopChance:P0}, approach={approachSpeed:F2}, align={headOnAlignment:F2}, tangent={tangentSpeed:F2}, lift={liftSpeed:F2}");
    }

    private float GetDamagePercent(float initialImpactResistance, float currentImpactResistance)
    {
        if (initialImpactResistance <= 0f)
            return 0f;

        return Mathf.Clamp01((initialImpactResistance - currentImpactResistance) / initialImpactResistance);
    }

    private float GetDamagedRollingStrength(float initialImpactResistance, float currentImpactResistance)
    {
        if (!enableDamagedRollingPhysics)
            return 0f;

        float damagePercent = GetDamagePercent(initialImpactResistance, currentImpactResistance);
        if (damagePercent <= damagedRollingMinDamagePercent)
            return 0f;

        float t = Mathf.InverseLerp(damagedRollingMinDamagePercent, 1f, damagePercent);
        return Mathf.SmoothStep(0f, 1f, t);
    }

    private bool IsDamagedRollGrounded(Rigidbody targetBody)
    {
        if (targetBody == null)
            return false;

        float radius = Mathf.Max(0.05f, ResolveRigidbodyRadius(targetBody));
        Vector3 origin = targetBody.worldCenterOfMass + Vector3.up * 0.03f;
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, damagedGroundHits, radius + 0.16f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            var hitCollider = damagedGroundHits[i].collider;
            if (hitCollider == null || IsOwnCollider(hitCollider))
                continue;

            return true;
        }

        return false;
    }

    private bool IsBodyGrounded(Rigidbody targetBody)
    {
        if (targetBody == null)
            return false;

        float radius = Mathf.Max(0.05f, ResolveRigidbodyRadius(targetBody));
        Vector3 origin = targetBody.worldCenterOfMass + Vector3.up * 0.03f;
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, damagedGroundHits, radius + 0.16f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            var hitCollider = damagedGroundHits[i].collider;
            if (hitCollider == null || IsColliderOwnedByBody(hitCollider, targetBody))
                continue;

            return true;
        }

        return false;
    }

    private bool IsOwnCollider(Collider targetCollider)
    {
        if (targetCollider == null)
            return false;

        Transform targetTransform = targetCollider.transform;
        return targetTransform == transform ||
               targetTransform.IsChildOf(transform) ||
               transform.IsChildOf(targetTransform);
    }

    private static bool IsColliderOwnedByBody(Collider targetCollider, Rigidbody targetBody)
    {
        if (targetCollider == null || targetBody == null)
            return false;

        Transform targetTransform = targetCollider.transform;
        Transform bodyTransform = targetBody.transform;
        return targetTransform == bodyTransform ||
               targetTransform.IsChildOf(bodyTransform) ||
               bodyTransform.IsChildOf(targetTransform);
    }

    private static Vector3 ProjectHorizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static float ResolveRigidbodyRadius(Rigidbody targetBody)
    {
        var sphere = targetBody.GetComponent<SphereCollider>();
        if (sphere != null)
            return Mathf.Max(0.01f, sphere.radius * MaxAbsScale(targetBody.transform.lossyScale));

        var collider = targetBody.GetComponent<Collider>();
        if (collider != null)
            return Mathf.Max(0.01f, Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.z));

        return 0.1f;
    }

    private static float MaxAbsScale(Vector3 scale)
    {
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    private static void ApplyRollingAngularVelocity(Rigidbody targetBody, Vector3 velocity, float blend)
    {
        if (targetBody == null)
            return;

        Vector3 horizontalVelocity = ProjectHorizontal(velocity);
        if (horizontalVelocity.sqrMagnitude < 0.0001f)
            return;

        float radius = ResolveRigidbodyRadius(targetBody);
        if (radius <= 0.001f)
            return;

        Vector3 rollingAngularVelocity = Vector3.Cross(Vector3.up, horizontalVelocity) / radius;
        targetBody.angularVelocity = Vector3.Lerp(targetBody.angularVelocity, rollingAngularVelocity, Mathf.Clamp01(blend));
    }

    private static Vector3 ResolveDamagedShotDirection(Vector3 baseDirection, float shootAngle)
    {
        Vector3 horizontalDirection = ProjectHorizontal(baseDirection);
        if (horizontalDirection.sqrMagnitude < 0.0001f)
        {
            horizontalDirection = new Vector3(baseDirection.x, 0f, baseDirection.z);
            if (horizontalDirection.sqrMagnitude < 0.0001f)
                horizontalDirection = Vector3.forward;
        }

        horizontalDirection.Normalize();
        if (Mathf.Abs(shootAngle) < 0.01f)
            return horizontalDirection;

        Vector3 right = Vector3.Cross(Vector3.up, horizontalDirection).normalized;
        return Quaternion.AngleAxis(shootAngle, right) * horizontalDirection;
    }

    // spawn crack prefab when ball stops
    public void SpawnCrackPrefab(float pct)
    {
        if (spawnedCrack != null)
            return;

        GameObject prefab = null;
        if (pct <= 0.1f && heavyCrackPrefab != null)
            prefab = heavyCrackPrefab;
        else if (pct <= 0.5f && mediumCrackPrefab != null)
            prefab = mediumCrackPrefab;
        else if (pct <= 0.7f && minorCrackPrefab != null)
            prefab = minorCrackPrefab;

        if (prefab != null)
        {
            spawnedCrack = Instantiate(prefab, transform);
            spawnedCrack.transform.localPosition = Vector3.zero;
            var col = spawnedCrack.GetComponent<Collider>();
            if (col != null) col.enabled = true;
            if (decalMaterial != null)
            {
                var rend = spawnedCrack.GetComponent<Renderer>();
                if (rend != null)
                    rend.material = decalMaterial;
            }
        }
    }
}
