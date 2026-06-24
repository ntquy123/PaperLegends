using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class CoreTutorialBallLocalPhysicsController : MonoBehaviour
{
    private const float BallCollisionMinRelativeSpeed = 0.25f;
    private const float BallCollisionRestitutionFallback = 0.88f;
    private const float BallCollisionShotNormalRetainLimit = 0.35f;
    private const float BallCollisionMaxTargetSpeedMultiplier = 1.15f;
    private const float BallCollisionRollingSpinBlend = 0.35f;
    private const float ShotContactOffset = 0.08f;
    private const float ShotTorqueScaleMultiplier = 0.05f;
    private static readonly Dictionary<ulong, int> CollisionResponseFrameByPair = new();
    private static readonly Dictionary<ulong, int> CollisionSoundFrameByPair = new();

    private Rigidbody body;
    private PhysicsBallHelper physicsHelper;
    private bool hasBeenShot;
    private Coroutine backSpinRoutine;
    private bool shotGroundImpactPlayed;
    private bool shotRollingLoopStarted;
    private float shotRollingStopElapsed;
    private const float ShotRollingStopConfirmSeconds = 0.25f;
    private const float ShotGroundProbeOffset = 0.35f;
    private const float ShotGroundContactTolerance = 0.12f;

    public event Action<GameObject> BallHit;

    public void Initialize()
    {
        body = GetComponent<Rigidbody>();
        physicsHelper = GetComponent<PhysicsBallHelper>();
        if (physicsHelper == null)
        {
            physicsHelper = gameObject.AddComponent<PhysicsBallHelper>();
        }

        // The online server invokes only collision/damage helpers, not per-frame helper movement.
        physicsHelper.SetFramePhysicsFixedUpdateEnabled(false);
    }

    public void MarkShotStarted()
    {
        hasBeenShot = true;
        ResetShotGroundAudio();
        shotGroundImpactPlayed = false;
        physicsHelper?.ResetDamagedRollingState();
    }

    public void ApplyOnlineStyleShot(Vector3 direction, float force, Vector3 spin)
    {
        if (body == null)
        {
            Initialize();
        }

        Vector3 shotDirection = ProjectHorizontal(direction);
        if (body == null || force <= 0f || shotDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        StopBackSpinRoutine();
        shotDirection.Normalize();
        MarkShotStarted();

        body.isKinematic = false;
        body.collisionDetectionMode = force >= 3f
            ? CollisionDetectionMode.ContinuousDynamic
            : CollisionDetectionMode.Continuous;
        body.maxAngularVelocity = 50f;

        Vector3 right = Vector3.Cross(Vector3.up, shotDirection).normalized;
        float backSpin = Vector3.Dot(spin, shotDirection);
        float sideSpin = Vector3.Dot(spin, right);
        Vector3 contactOffsetWorld = (-Vector3.up * backSpin + right * sideSpin) * ShotContactOffset;

        body.AddForceAtPosition(
            shotDirection * force,
            body.worldCenterOfMass + contactOffsetWorld,
            ForceMode.Impulse);

        Vector3 torque = Vector3.zero;
        float torqueScale = force * ShotTorqueScaleMultiplier;
        if (Mathf.Abs(backSpin) > 0.01f)
        {
            torque += right * (-backSpin) * torqueScale;
        }

        if (Mathf.Abs(sideSpin) > 0.01f)
        {
            torque += Vector3.up * sideSpin * torqueScale;
        }

        if (torque.sqrMagnitude > 0.0001f)
        {
            body.AddTorque(torque, ForceMode.Impulse);
        }

        if (backSpin < -0.01f)
        {
            backSpinRoutine = StartCoroutine(ApplyBackSpinPullbackRoutine(shotDirection, force, backSpin));
        }

        body.WakeUp();
    }

    public void ResetShotState()
    {
        hasBeenShot = false;
        StopBackSpinRoutine();
        ResetShotGroundAudio();
    }

    public void CaptureBasePhysicsMaterial(Collider collider, float bounciness, float dynamicFriction, float staticFriction)
    {
        physicsHelper?.CaptureBasePhysicsMaterial(collider, bounciness, dynamicFriction, staticFriction);
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        ResetShotGroundAudio();
    }

    private void FixedUpdate()
    {
        if (!hasBeenShot || body == null)
        {
            return;
        }

        if (!shotGroundImpactPlayed)
        {
            TryPlayShotGroundAudioFromProbe();
        }

        if (!shotRollingLoopStarted)
        {
            return;
        }

        bool stopped = body.linearVelocity.magnitude <= 0.03f
            && body.angularVelocity.magnitude <= 0.8f;
        if (!stopped)
        {
            shotRollingStopElapsed = 0f;
            return;
        }

        shotRollingStopElapsed += Time.fixedDeltaTime;
        if (shotRollingStopElapsed >= ShotRollingStopConfirmSeconds)
        {
            StopShotRollingLoop();
        }
    }

    private IEnumerator ApplyBackSpinPullbackRoutine(Vector3 shotDirection, float force, float backSpin)
    {
        float pullDelay = Mathf.Lerp(0.08f, 0.2f, Mathf.Clamp01(force / 10f));
        float backSpinMagnitude = Mathf.Clamp01(Mathf.Abs(backSpin));
        float pullbackForce = force * Mathf.Lerp(0.15f, 0.45f, backSpinMagnitude);

        yield return new WaitForSeconds(pullDelay);
        backSpinRoutine = null;
        if (body == null || !hasBeenShot)
        {
            yield break;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
        body.AddForce(-shotDirection * pullbackForce, ForceMode.Impulse);
    }

    private void StopBackSpinRoutine()
    {
        if (backSpinRoutine == null)
        {
            return;
        }

        StopCoroutine(backSpinRoutine);
        backSpinRoutine = null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryPlayShotGroundAudio(collision);

        Rigidbody otherBody = collision != null
            ? collision.rigidbody != null
                ? collision.rigidbody
                : collision.collider != null ? collision.collider.attachedRigidbody : null
            : null;
        if (otherBody != null
            && otherBody != body
            && IsBallCollisionObject(otherBody.gameObject))
        {
            PlayImmediateBallCollisionSound(collision, otherBody);

            // Tutorial objectives should count a real touch even when the shot is too soft
            // to enter the enhanced online-style momentum response below.
            BallHit?.Invoke(otherBody.gameObject);
        }

        ApplyBallCollisionResponse(collision);
    }

    private void PlayImmediateBallCollisionSound(Collision collision, Rigidbody otherBody)
    {
        if (collision == null || body == null || otherBody == null || ShouldSkipRepeatedBallCollisionSound(body, otherBody))
        {
            return;
        }

        CoreTutorialBallLocalPhysicsController otherController = otherBody.GetComponent<CoreTutorialBallLocalPhysicsController>();
        if (!hasBeenShot && (otherController == null || !otherController.hasBeenShot))
        {
            return;
        }

        Vector3 contactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;
        float force = Mathf.Max(
            Mathf.Max(collision.relativeVelocity.magnitude, body.linearVelocity.magnitude),
            otherBody.linearVelocity.magnitude);
        SoundManager.Instance?.PlayBallHit(Mathf.Max(force, 0.1f), contactPoint);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryPlayShotGroundAudio(collision);
    }

    private void TryPlayShotGroundAudio(Collision collision)
    {
        if (!hasBeenShot || shotGroundImpactPlayed || collision == null || !IsGroundCollision(collision.gameObject))
        {
            return;
        }

        Vector3 contactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;
        float speed = body != null ? body.linearVelocity.magnitude : collision.relativeVelocity.magnitude;
        StartShotGroundAudio(contactPoint, Mathf.Max(speed, collision.relativeVelocity.magnitude));
    }

    private bool TryPlayShotGroundAudioFromProbe()
    {
        float radius = ResolveShotGroundAudioRadius();
        float probeOffset = Mathf.Max(ShotGroundProbeOffset, radius + 0.05f);
        float probeDistance = probeOffset + radius + ShotGroundContactTolerance;
        Vector3 origin = transform.position + Vector3.up * probeOffset;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            probeDistance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        Vector3 contactPoint = transform.position;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || !IsGroundCollision(hit.collider.gameObject))
            {
                continue;
            }

            float centerToGround = transform.position.y - hit.point.y;
            if (centerToGround > radius + ShotGroundContactTolerance || centerToGround < -ShotGroundContactTolerance)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                contactPoint = hit.point;
            }
        }

        if (closestDistance >= float.MaxValue)
        {
            return false;
        }

        StartShotGroundAudio(contactPoint, body != null ? body.linearVelocity.magnitude : 0f);
        return true;
    }

    private void StartShotGroundAudio(Vector3 contactPoint, float speed)
    {
        float intensity = Mathf.Clamp01(Mathf.Max(speed, 0.2f) / 2.5f);
        SoundManager.Instance?.PlayShotBallGroundImpact(contactPoint, intensity);
        SoundManager.Instance?.StartShotBallRollingLoop(gameObject, GetShotRollingSpeed);
        shotGroundImpactPlayed = true;
        shotRollingLoopStarted = true;
        shotRollingStopElapsed = 0f;
    }

    private float ResolveShotGroundAudioRadius()
    {
        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return Mathf.Max(0.01f, sphere.radius * maxScale);
        }

        Collider ballCollider = GetComponent<Collider>();
        return ballCollider != null
            ? Mathf.Max(0.01f, Mathf.Min(ballCollider.bounds.extents.x, ballCollider.bounds.extents.z))
            : 0.1f;
    }

    private float GetShotRollingSpeed()
    {
        return body != null ? body.linearVelocity.magnitude : 0f;
    }

    private void ResetShotGroundAudio()
    {
        StopShotRollingLoop();
        shotGroundImpactPlayed = false;
        shotRollingStopElapsed = 0f;
    }

    private void StopShotRollingLoop()
    {
        if (!shotRollingLoopStarted)
        {
            return;
        }

        SoundManager.Instance?.StopShotBallRollingLoop(gameObject);
        shotRollingLoopStarted = false;
        shotRollingStopElapsed = 0f;
    }

    private static bool IsGroundCollision(GameObject collisionObject)
    {
        Transform current = collisionObject != null ? collisionObject.transform : null;
        while (current != null)
        {
            if (current.CompareTag("Ground"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void ApplyBallCollisionResponse(Collision collision)
    {
        if (collision == null || body == null || body.isKinematic)
        {
            return;
        }

        Rigidbody otherBody = collision.rigidbody != null
            ? collision.rigidbody
            : collision.collider != null ? collision.collider.attachedRigidbody : null;
        if (otherBody == null
            || otherBody == body
            || otherBody.isKinematic
            || (!IsBallCollisionObject(collision.gameObject) && !IsBallCollisionObject(otherBody.gameObject))
            || ShouldSkipRepeatedBallCollisionResponse(body, otherBody))
        {
            return;
        }

        Rigidbody moverBody = body;
        Rigidbody targetBody = otherBody;
        CoreTutorialBallLocalPhysicsController moverController = this;

        float thisSpeedSqr = ProjectHorizontal(body.linearVelocity).sqrMagnitude;
        float otherSpeedSqr = ProjectHorizontal(otherBody.linearVelocity).sqrMagnitude;
        if (otherSpeedSqr > thisSpeedSqr)
        {
            moverBody = otherBody;
            targetBody = body;
            moverController = otherBody.GetComponent<CoreTutorialBallLocalPhysicsController>();
        }

        Vector3 moverVelocity = ProjectHorizontal(moverBody.linearVelocity);
        Vector3 targetVelocity = ProjectHorizontal(targetBody.linearVelocity);
        Vector3 relativeVelocity = moverVelocity - targetVelocity;
        if (relativeVelocity.sqrMagnitude < BallCollisionMinRelativeSpeed * BallCollisionMinRelativeSpeed)
        {
            return;
        }

        Vector3 normal = ResolveBallCollisionNormal(moverBody, targetBody, collision);
        if (Vector3.Dot(relativeVelocity, normal) < 0f)
        {
            normal = -normal;
        }

        float moverNormalSpeed = Vector3.Dot(moverVelocity, normal);
        float targetNormalSpeed = Vector3.Dot(targetVelocity, normal);
        float approachSpeed = moverNormalSpeed - targetNormalSpeed;
        if (approachSpeed < BallCollisionMinRelativeSpeed)
        {
            return;
        }

        float moverMass = Mathf.Max(0.05f, moverBody.mass);
        float targetMass = Mathf.Max(0.05f, targetBody.mass);
        float restitution = ResolveBallCollisionRestitution(moverBody, targetBody, collision);
        float massSum = moverMass + targetMass;

        float newMoverNormalSpeed =
            ((moverMass - restitution * targetMass) * moverNormalSpeed
             + (1f + restitution) * targetMass * targetNormalSpeed) / massSum;
        float newTargetNormalSpeed =
            ((1f + restitution) * moverMass * moverNormalSpeed
             + (targetMass - restitution * moverMass) * targetNormalSpeed) / massSum;

        if (moverController != null && moverController.hasBeenShot && moverNormalSpeed > 0f)
        {
            float retainLimit = moverNormalSpeed * BallCollisionShotNormalRetainLimit;
            if (newMoverNormalSpeed > retainLimit)
            {
                newMoverNormalSpeed = retainLimit;
            }
        }

        Vector3 newMoverVelocity = moverVelocity - normal * moverNormalSpeed + normal * newMoverNormalSpeed;
        Vector3 newTargetVelocity = targetVelocity - normal * targetNormalSpeed + normal * newTargetNormalSpeed;
        newMoverVelocity.y = moverBody.linearVelocity.y;
        newTargetVelocity.y = targetBody.linearVelocity.y;

        float maxTargetSpeed = Mathf.Max(
            ProjectHorizontal(targetBody.linearVelocity).magnitude,
            ProjectHorizontal(moverBody.linearVelocity).magnitude * BallCollisionMaxTargetSpeedMultiplier);
        newTargetVelocity = ClampHorizontalSpeed(newTargetVelocity, maxTargetSpeed);

        moverBody.linearVelocity = newMoverVelocity;
        targetBody.linearVelocity = newTargetVelocity;
        ApplyRollingAngularVelocity(moverBody, newMoverVelocity);
        ApplyRollingAngularVelocity(targetBody, newTargetVelocity);
        moverController?.physicsHelper?.ApplyRareRealWorldBallCollision(
            moverBody,
            targetBody,
            normal,
            moverVelocity,
            targetVelocity,
            approachSpeed,
            restitution,
            moverController.hasBeenShot);
        moverBody.WakeUp();
        targetBody.WakeUp();

        Debug.Log(
            $"[TUTORIAL][BallCollision] mover={moverBody.name} target={targetBody.name} "
            + $"approach={approachSpeed:F3} targetSpeed={ProjectHorizontal(targetBody.linearVelocity).magnitude:F3}");
    }

    private static Vector3 ProjectHorizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static Vector3 ClampHorizontalSpeed(Vector3 velocity, float maxHorizontalSpeed)
    {
        if (maxHorizontalSpeed <= 0f)
        {
            return velocity;
        }

        Vector3 horizontal = ProjectHorizontal(velocity);
        if (horizontal.sqrMagnitude <= maxHorizontalSpeed * maxHorizontalSpeed)
        {
            return velocity;
        }

        horizontal = horizontal.normalized * maxHorizontalSpeed;
        velocity.x = horizontal.x;
        velocity.z = horizontal.z;
        return velocity;
    }

    private static Vector3 ResolveBallCollisionNormal(Rigidbody moverBody, Rigidbody targetBody, Collision collision)
    {
        Vector3 normal = ProjectHorizontal(targetBody.worldCenterOfMass - moverBody.worldCenterOfMass);
        if (normal.sqrMagnitude > 0.0001f)
        {
            return normal.normalized;
        }

        if (collision.contactCount > 0)
        {
            normal = ProjectHorizontal(targetBody.worldCenterOfMass - collision.contacts[0].point);
            if (normal.sqrMagnitude > 0.0001f)
            {
                return normal.normalized;
            }
        }

        normal = ProjectHorizontal(moverBody.linearVelocity - targetBody.linearVelocity);
        return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
    }

    private static float ResolveBallCollisionRestitution(Rigidbody moverBody, Rigidbody targetBody, Collision collision)
    {
        float restitution = Mathf.Max(
            BallCollisionRestitutionFallback,
            GetColliderBounciness(collision.collider),
            GetColliderBounciness(GetPrimaryEnabledCollider(moverBody)),
            GetColliderBounciness(GetPrimaryEnabledCollider(targetBody)));
        return Mathf.Clamp(restitution, 0.65f, 0.95f);
    }

    private static Collider GetPrimaryEnabledCollider(Rigidbody targetBody)
    {
        Collider directCollider = targetBody.GetComponent<Collider>();
        if (directCollider != null && directCollider.enabled)
        {
            return directCollider;
        }

        Collider[] colliders = targetBody.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled)
            {
                return colliders[i];
            }
        }

        return directCollider;
    }

    private static float GetColliderBounciness(Collider collider)
    {
        return collider != null && collider.sharedMaterial != null
            ? collider.sharedMaterial.bounciness
            : 0f;
    }

    private static void ApplyRollingAngularVelocity(Rigidbody targetBody, Vector3 velocity)
    {
        Vector3 horizontalVelocity = ProjectHorizontal(velocity);
        if (horizontalVelocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float radius = ResolveRigidbodyRadius(targetBody);
        if (radius <= 0.001f)
        {
            return;
        }

        Vector3 rollingVelocity = Vector3.Cross(Vector3.up, horizontalVelocity) / radius;
        targetBody.angularVelocity = Vector3.Lerp(targetBody.angularVelocity, rollingVelocity, BallCollisionRollingSpinBlend);
    }

    private static float ResolveRigidbodyRadius(Rigidbody targetBody)
    {
        SphereCollider sphere = targetBody.GetComponent<SphereCollider>();
        if (sphere != null)
        {
            Vector3 scale = targetBody.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return Mathf.Max(0.01f, sphere.radius * maxScale);
        }

        Collider collider = targetBody.GetComponent<Collider>();
        return collider != null
            ? Mathf.Max(0.01f, Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.z))
            : 0.1f;
    }

    private static bool ShouldSkipRepeatedBallCollisionResponse(Rigidbody first, Rigidbody second)
    {
        if (CollisionResponseFrameByPair.Count > 512)
        {
            CollisionResponseFrameByPair.Clear();
        }

        uint firstId = unchecked((uint)first.GetInstanceID());
        uint secondId = unchecked((uint)second.GetInstanceID());
        if (firstId > secondId)
        {
            uint temp = firstId;
            firstId = secondId;
            secondId = temp;
        }

        ulong key = ((ulong)firstId << 32) | secondId;
        int frame = Time.frameCount;
        if (CollisionResponseFrameByPair.TryGetValue(key, out int lastFrame) && lastFrame == frame)
        {
            return true;
        }

        CollisionResponseFrameByPair[key] = frame;
        return false;
    }

    private static bool ShouldSkipRepeatedBallCollisionSound(Rigidbody first, Rigidbody second)
    {
        if (CollisionSoundFrameByPair.Count > 512)
        {
            CollisionSoundFrameByPair.Clear();
        }

        uint firstId = unchecked((uint)first.GetInstanceID());
        uint secondId = unchecked((uint)second.GetInstanceID());
        if (firstId > secondId)
        {
            uint temp = firstId;
            firstId = secondId;
            secondId = temp;
        }

        ulong key = ((ulong)firstId << 32) | secondId;
        int frame = Time.frameCount;
        if (CollisionSoundFrameByPair.TryGetValue(key, out int lastFrame) && lastFrame == frame)
        {
            return true;
        }

        CollisionSoundFrameByPair[key] = frame;
        return false;
    }

    private static bool IsBallCollisionObject(GameObject collisionObject)
    {
        if (collisionObject == null)
        {
            return false;
        }

        int ballLayer = LayerMask.NameToLayer("Ball");
        return (ballLayer >= 0 && collisionObject.layer == ballLayer)
            || collisionObject.CompareTag("RingBall")
            || collisionObject.CompareTag("BallPlayer");
    }
}
