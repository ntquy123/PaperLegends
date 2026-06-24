using UnityEngine;

public class BallSkill_99000004_Trap : MonoBehaviour
{
    BallSkill_99000004 owner;

    public void Init(BallSkill_99000004 skill)
    {
        owner = skill;
    }

    void OnTriggerEnter(Collider other)
    {
        if (owner == null || !owner.IsTrapActive)
            return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            float force = 0f;
            if (owner.launchForceByLevel != null && owner.level < owner.launchForceByLevel.Length)
                force = owner.launchForceByLevel[owner.level];

            rb.AddForce(Vector3.up * force, ForceMode.Impulse);
            owner.DeactivateTrap();
        }
    }
}
