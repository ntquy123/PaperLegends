// Assets/Script/GamePlay/Culi/BallSkill_99000008.cs
using UnityEngine;

/// <summary>
/// Tăng khối lượng viên bi trong một số lượt.
/// </summary>
public class BallSkill_99000008 : MonoBehaviour, IFBall
{
    public int level;
    public float[] weightBonusByLevel = { 0f, 10f, 15f, 20f, 25f, 30f, 40f, 50f, 60f, 70f };
    public GameObject shootVFX, hitVFX;
    float originalMass;
    int remainingTurns;
    bool buffActive;
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originalMass = rb.mass;
    }

    public void SetStatusUltimate()
    {
        // Hiển thị trạng thái nếu cần
    }

    public void ShootUltimate()
    {
        if (weightBonusByLevel == null || level < 0 || level >= weightBonusByLevel.Length)
            return;

        float percent = weightBonusByLevel[level];
        rb.mass = originalMass * (1f + percent / 100f);
        remainingTurns = level >= 10 ? 3 : level >= 5 ? 2 : 1;
        shootVFX?.SetActive(true);
        buffActive = true;
    }

    public void ShootEffect()
    {
        if (buffActive)
        {
            shootVFX?.SetActive(true);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!buffActive) return;
        if (hitVFX != null)
            Instantiate(hitVFX, collision.contacts[0].point, Quaternion.identity);
    }

    public void StopShootEffect()
    {
        if (!buffActive)
            shootVFX?.SetActive(false);
    }

    public void OnEndTurn()
    {
        if (!buffActive) return;
        remainingTurns--;
        if (remainingTurns <= 0)
        {
            rb.mass = originalMass;
            shootVFX?.SetActive(false);
            buffActive = false;
        }
    }
}
