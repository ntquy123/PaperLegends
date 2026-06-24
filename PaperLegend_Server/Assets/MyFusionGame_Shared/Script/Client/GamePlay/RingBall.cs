using Fusion;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RingBall : MonoBehaviour
{
    private Rigidbody rb;

    private bool isRolling = false;
    [Networked, OnChangedRender(nameof(OnBallHit))] public HitInfo LastHitInfo { get; set; }
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    //void Update()
    //{
    //    float speed = rb.velocity.magnitude;

    //    if (speed > 0.05f && !isRolling)
    //    {
    //        isRolling = true;
    //        SoundManager.Instance.StartBallRollingLoop(gameObject, () => rb.velocity.magnitude);
    //    }
    //    else if (speed <= 0.05f && isRolling)
    //    {
    //        isRolling = false;
    //        SoundManager.Instance.StopBallRollingLoop(gameObject);
    //    }
    //}

    private void OnCollisionEnter(Collision collision)
    {
        // Chỉ xử lý va chạm với vật thể có tag là "Ball"
        if (collision.gameObject.layer == LayerMask.NameToLayer("Ball"))
        {
            float impactForce = CalculateImpactForce(collision);
            // Phát âm va chạm 1 lần
            LastHitInfo = new HitInfo
            {
                magnitude = impactForce,
                point = collision.contacts[0].point,
                surfaceType = HitSurface.Ball
            };
        }
    }
    private void OnBallHit()
    {
        // Phát âm ngay trong client/host render lần kế tiếp
        PlayBallHitSound(LastHitInfo);
    }

    private void PlayBallHitSound(HitInfo info)
    {
        float force = Mathf.Max(info.magnitude, 0.1f);

        switch (info.surfaceType)
        {
            case HitSurface.Water:
                ClientGameplayBridge.Sound.PlayBallHitWater(info.point, force);
                break;
            case HitSurface.Puddle:
                ClientGameplayBridge.Sound.PlayBallHitPuddle(info.point, force);
                break;
            case HitSurface.Grass:
                ClientGameplayBridge.Sound.PlayBallHitGrass(info.point, force);
                break;
            case HitSurface.Swamp:
                ClientGameplayBridge.Sound.PlayBallHitSwamp(info.point, force);
                break;
            case HitSurface.Rock:
                ClientGameplayBridge.Sound.PlayBallHitRock(info.point, force);
                break;
            case HitSurface.Tree:
                ClientGameplayBridge.Sound.PlayBallHitTree(info.point, force);
                break;
            default:
                ClientGameplayBridge.Sound.PlayBallHit(force, info.point);
                break;
        }
    }

    private static float CalculateImpactForce(Collision collision)
    {
        float impulse = collision.impulse.magnitude;
        if (impulse > 0f)
            return impulse / Time.fixedDeltaTime;

        return collision.relativeVelocity.magnitude;
    }
}
