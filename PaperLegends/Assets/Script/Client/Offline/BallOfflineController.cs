using UnityEngine;

public class BallOfflineController : MonoBehaviour
{
    public int playerId;
    public int BallIndex;
    public int BallMaterialId;
    public int BallLevel;
    public bool IsActive = true;
    public bool IsHolding = false;

    private bool hasBeenShoot = false;
    private float stoppedTime = 0f;
    private const float minVelocity = 0.03f;
    private const float minAngular = 1f;
    private const float requiredStopDuration = 0.5f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void ApplyPhysics(BallPhysicsStruct info)
    {
        if (rb == null) return;
        rb.mass = info.Mass;
        rb.linearDamping = info.Drag;
        rb.useGravity = true;
        //if (rb.sharedMaterial == null)
        //    rb.sharedMaterial = new PhysicsMaterial();
        //rb.sharedMaterial.bounciness = info.Bounciness;
        //rb.sharedMaterial.dynamicFriction = info.Elasticity;
    }

    // Online: RpcApplyPhysics -> local direct call ApplyPhysics

    public void ShootBall(Vector3 direction, float force, Vector3 spin)
    {
        if (rb == null) return;
        rb.isKinematic = false;
        IsHolding = false;
        hasBeenShoot = true;
        rb.AddForce(direction * force, ForceMode.Impulse);
        rb.AddTorque(spin * force * 0.5f, ForceMode.VelocityChange);
    }

    // Online: RpcSetActive -> direct SetBallActive
    public void SetBallActive(bool active)
    {
        IsActive = active;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = active;
        var rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = active;
        if (rb != null) rb.isKinematic = !active;
    }

    private void FixedUpdate()
    {
        if (!hasBeenShoot || rb == null)
            return;

        if (rb.linearVelocity.magnitude < minVelocity && rb.angularVelocity.magnitude < minAngular)
        {
            stoppedTime += Time.fixedDeltaTime;
            if (stoppedTime >= requiredStopDuration)
            {
                hasBeenShoot = false;
                stoppedTime = 0f;
                GameSessionOffline.Instance?.HandleAfterShoot(playerId);
            }
        }
        else
        {
            stoppedTime = 0f;
        }
    }
}
