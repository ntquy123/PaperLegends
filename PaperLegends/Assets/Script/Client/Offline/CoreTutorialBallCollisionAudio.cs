using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class CoreTutorialBallCollisionAudio : MonoBehaviour
{
    private static readonly Dictionary<ulong, float> PlayedCollisionFixedTimeByPair = new();

    private Rigidbody body;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PlayedCollisionFixedTimeByPair.Clear();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || body == null)
        {
            return;
        }

        Rigidbody otherBody = collision.rigidbody != null
            ? collision.rigidbody
            : collision.collider != null ? collision.collider.attachedRigidbody : null;
        if (otherBody == null
            || otherBody == body
            || !IsTutorialBall(otherBody.gameObject)
            || ShouldSkipDuplicateSound(body, otherBody))
        {
            return;
        }

        float impactForce = CalculateImpactForce(collision);
        Vector3 impactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;
        ClientGameplayBridge.Sound.PlayBallHit(Mathf.Max(impactForce, 0.1f), impactPoint);
    }

    private static float CalculateImpactForce(Collision collision)
    {
        float impulse = collision.impulse.magnitude;
        return impulse > 0f
            ? impulse / Time.fixedDeltaTime
            : collision.relativeVelocity.magnitude;
    }

    private static bool ShouldSkipDuplicateSound(Rigidbody first, Rigidbody second)
    {
        if (PlayedCollisionFixedTimeByPair.Count > 512)
        {
            PlayedCollisionFixedTimeByPair.Clear();
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
        float fixedTime = Time.fixedTime;
        if (PlayedCollisionFixedTimeByPair.TryGetValue(key, out float lastFixedTime)
            && Mathf.Approximately(lastFixedTime, fixedTime))
        {
            return true;
        }

        PlayedCollisionFixedTimeByPair[key] = fixedTime;
        return false;
    }

    private static bool IsTutorialBall(GameObject collisionObject)
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
